using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Revia.Client.Offline;
using Revia.Shared.Bmi;
using Revia.Shared.Offline;
using Revia.Shared.Persons;

namespace Revia.Client.Features.Bmi;

/// <summary>
/// Offline-capable BMI measurements. Records (weight/height/BMI) read from cache
/// offline and write optimistically + queued. A new measurement's optional photo
/// rides along as base64 in the queued JSON payload, so it syncs with everything
/// else; the photo image itself is served (and viewable) when online.
/// </summary>
public sealed class BmiClientService
{
    public const string Collection = "bmi";
    private const string CacheKey = "col:bmi";

    private readonly HttpClient _http;
    private readonly OfflineStore _store;
    private readonly ConnectivityService _connectivity;
    private readonly SyncEngine _sync;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public BmiClientService(IHttpClientFactory httpFactory, OfflineStore store,
        ConnectivityService connectivity, SyncEngine sync)
    {
        _http = httpFactory.CreateClient("api");
        _store = store;
        _connectivity = connectivity;
        _sync = sync;
        _sync.RegisterHandler(Collection, ReplayAsync);
    }

    public async Task<List<BmiRecordDto>> GetAllAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<BmiRecordDto>>("api/bmi");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync(CacheKey, fresh);
                    return fresh;
                }
            }
            catch { /* fall through to cache */ }
        }
        return await _store.CacheGetAsync<List<BmiRecordDto>>(CacheKey) ?? new();
    }

    /// <summary>Person lookup, cached so the picker works offline too.</summary>
    public async Task<List<PersonLookupDto>> GetPersonsAsync()
    {
        if (_connectivity.IsOnline)
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<PersonLookupDto>>("api/persons/lookup");
                if (fresh is not null)
                {
                    await _store.CacheSetAsync("col:persons", fresh);
                    return fresh;
                }
            }
            catch { }
        }
        return await _store.CacheGetAsync<List<PersonLookupDto>>("col:persons") ?? new();
    }

    public async Task AddAsync(AddBmiRecordRequest request, string personDisplayName)
    {
        var list = await _store.CacheGetAsync<List<BmiRecordDto>>(CacheKey) ?? new();
        list.Add(new BmiRecordDto
        {
            Id = TempId(),
            PersonId = request.PersonId,
            PersonDisplayName = personDisplayName,
            MeasuredOn = request.MeasuredOn == default ? DateTime.Today : request.MeasuredOn,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            Bmi = request.HeightCm > 0
                ? Math.Round(request.WeightKg / Math.Pow(request.HeightCm / 100.0, 2), 1)
                : 0,
            HasPhoto = !string.IsNullOrEmpty(request.PhotoBase64)
        });
        await _store.CacheSetAsync(CacheKey, list);

        await EnqueueAsync(MutationKind.Create, null, request);
    }

    public async Task DeleteAsync(int id)
    {
        var list = await _store.CacheGetAsync<List<BmiRecordDto>>(CacheKey) ?? new();
        list.RemoveAll(r => r.Id == id);
        await _store.CacheSetAsync(CacheKey, list);

        if (id > 0)
        {
            await EnqueueAsync(MutationKind.Delete, id.ToString(), null);
        }
    }

    private async Task EnqueueAsync(MutationKind kind, string? entityId, object? payload)
    {
        await _store.OutboxAddAsync(new OutboxMutation
        {
            Collection = Collection,
            Kind = kind,
            EntityId = entityId,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, Json)
        });
        _sync.NotifyQueued();
        if (_connectivity.IsOnline)
        {
            await _sync.FlushAsync();
        }
    }

    private async Task<bool> ReplayAsync(OutboxMutation m)
    {
        try
        {
            HttpResponseMessage response = m.Kind switch
            {
                MutationKind.Create => await _http.PostAsync("api/bmi", Body(m.PayloadJson)),
                MutationKind.Delete => await _http.DeleteAsync($"api/bmi/{m.EntityId}"),
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
