using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PrivateLibrary.Client.Offline;
using PrivateLibrary.Shared.Lending;
using PrivateLibrary.Shared.Offline;
using PrivateLibrary.Shared.Persons;

namespace PrivateLibrary.Client.Features.Lending;

/// <summary>
/// Offline-capable lending entries (step 1: entries with items/categories/persons/
/// status/dates, plus categories). Entries, categories and the person lookup are
/// cached for offline reads; create/update/return/delete apply optimistically and
/// queue in the outbox (server-wins). Photos/household-links come later.
/// </summary>
public sealed class LendingClientService
{
    public const string Collection = "lending";
    private const string EntriesKey = "col:lending";
    private const string CategoriesKey = "col:lending-categories";

    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;
    private readonly SyncEngine _sync;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public LendingClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(Collection, ReplayAsync);
    }

    public Task<List<LendingEntryDto>> GetAllAsync()
        => CachedOrFetchAsync(EntriesKey, "api/lending");

    public Task<List<LendingCategoryDto>> GetCategoriesAsync()
        => CachedOrFetchAsync<LendingCategoryDto>(CategoriesKey, "api/lending/categories");

    public async Task<List<PersonLookupDto>> GetPersonsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<PersonLookupDto>>("api/persons/lookup");
                if (fresh is not null) { await _store.CacheSetAsync("col:persons", fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<PersonLookupDto>>("col:persons") ?? new();
    }

    private Task<List<LendingEntryDto>> CachedOrFetchAsync(string key, string path)
        => CachedOrFetchAsync<LendingEntryDto>(key, path);

    private async Task<List<T>> CachedOrFetchAsync<T>(string key, string path)
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<T>>(path);
                if (fresh is not null) { await _store.CacheSetAsync(key, fresh); return fresh; }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<T>>(key) ?? new();
    }

    // ---- entry writes ----

    public async Task SaveAsync(SaveLendingEntryRequest request, string lenderName, string borrowerName, List<string> categoryNames)
    {
        var list = await _store.CacheGetAsync<List<LendingEntryDto>>(EntriesKey) ?? new();
        var optimistic = new LendingEntryDto
        {
            Id = request.Id != 0 ? request.Id : TempId(),
            Title = request.Title,
            Description = request.Description,
            Items = request.Items.ToList(),
            CategoryIds = request.CategoryIds.ToList(),
            CategoryNames = categoryNames,
            LenderId = request.LenderId, LenderName = lenderName,
            BorrowerId = request.BorrowerId, BorrowerName = borrowerName,
            LoanDate = request.LoanDate == default ? DateTime.Today : request.LoanDate,
            DueDate = request.DueDate,
            ReturnedAt = request.ReturnedAt,
            Status = request.ReturnedAt is not null ? LendingStatusDto.Returned : LendingStatusDto.Active,
            Notes = request.Notes
        };

        var idx = list.FindIndex(x => x.Id == optimistic.Id);
        if (idx >= 0) { list[idx] = optimistic; } else { list.Insert(0, optimistic); }
        await _store.CacheSetAsync(EntriesKey, list);

        if (request.Id > 0)
        {
            await EnqueueAsync(MutationKind.Update, request.Id.ToString(), request);
        }
        else
        {
            await EnqueueAsync(MutationKind.Create, null, request);
        }
    }

    public async Task MarkReturnedAsync(int id)
    {
        var list = await _store.CacheGetAsync<List<LendingEntryDto>>(EntriesKey) ?? new();
        var e = list.FirstOrDefault(x => x.Id == id);
        if (e is not null) { e.ReturnedAt = DateTime.Today; e.Status = LendingStatusDto.Returned; }
        await _store.CacheSetAsync(EntriesKey, list);

        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Update, $"{id}/returned", null);
        }
    }

    public async Task DeleteAsync(int id)
    {
        var list = await _store.CacheGetAsync<List<LendingEntryDto>>(EntriesKey) ?? new();
        list.RemoveAll(x => x.Id == id);
        await _store.CacheSetAsync(EntriesKey, list);
        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Delete, id.ToString(), null);
        }
    }

    // ---- category writes ----

    public async Task<int> AddCategoryAsync(string name)
    {
        var list = await _store.CacheGetAsync<List<LendingCategoryDto>>(CategoriesKey) ?? new();
        var tempId = TempId();
        list.Add(new LendingCategoryDto { Id = tempId, Name = name });
        await _store.CacheSetAsync(CategoriesKey, list);

        await EnqueueAsync(MutationKind.Create, "categories", new CreateLendingCategoryRequest { Name = name });
        return tempId;
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var list = await _store.CacheGetAsync<List<LendingCategoryDto>>(CategoriesKey) ?? new();
        list.RemoveAll(c => c.Id == id);
        await _store.CacheSetAsync(CategoriesKey, list);
        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Delete, $"categories/{id}", null);
        }
    }

    private async Task EnqueueAsync(MutationKind kind, string? entityId, object? payload)
    {
        await _store.OutboxAddAsync(new OutboxMutation
        {
            Collection = Collection, Kind = kind, EntityId = entityId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, Json)
        });
        if (_connectivity.IsOnline)
        {
            await _sync.FlushAsync();
        }
    }

    private async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            // EntityId routing:
            //  create entry: null         -> POST   api/lending
            //  update entry: "{id}"       -> PUT    api/lending/{id}
            //  mark returned: "{id}/returned" -> PUT api/lending/{id}/returned
            //  delete entry: "{id}"       -> DELETE api/lending/{id}
            //  create cat:   "categories" -> POST   api/lending/categories
            //  delete cat:   "categories/{id}" -> DELETE api/lending/categories/{id}
            HttpResponseMessage response = (m.Kind, m.EntityId) switch
            {
                (MutationKind.Create, null) => await _http.PostAsync("api/lending", Body(m.PayloadJson)),
                (MutationKind.Create, "categories") => await _http.PostAsync("api/lending/categories", Body(m.PayloadJson)),
                (MutationKind.Update, var p) => await _http.PutAsync($"api/lending/{p}", Body(m.PayloadJson)),
                (MutationKind.Delete, var p) => await _http.DeleteAsync($"api/lending/{p}"),
                _ => throw new InvalidOperationException()
            };
            if (response.IsSuccessStatusCode || (int)response.StatusCode is >= 400 and < 500)
            {
                await _store.OutboxRemoveAsync(m.Id);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static StringContent Body(string? json) => new(json ?? "{}", Encoding.UTF8, "application/json");

    private static int _temp = -1;
    private static int TempId() => _temp--;
}
