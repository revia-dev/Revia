using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Api.Auth;
using PrivateLibrary.Data;
using PrivateLibrary.Services;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Shared.Persons;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/persons")]
[ModuleAccess(AppModules.Persons)]
public sealed class PersonsController(IPersonService persons) : ControllerBase
{
    private readonly IPersonService _persons = persons;

    /// <summary>Lightweight person lookup for pickers (BMI, lending, games, …).</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<List<PersonLookupDto>>> Lookup(CancellationToken ct)
        => (await _persons.GetLookupAsync(ct))
            .Select(p => new PersonLookupDto { Id = p.Id, DisplayName = p.DisplayName })
            .ToList();

    /// <summary>People in the caller's household, each with their share token.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PersonCardDto>>> GetAll(CancellationToken ct)
        => (await _persons.GetAllAsync(ct))
            .Where(p => !p.IsSystemUser)
            .Select(p => new PersonCardDto
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Email = p.Email,
                Phone = p.Phone,
                IsLoginless = p.IsLoginless,
                LinkToken = p.PersonLinkToken.ToString()
            })
            .ToList();

    /// <summary>Households currently linked to the caller's (shared lendings).</summary>
    [HttpGet("linked-households")]
    public async Task<ActionResult<List<LinkedHouseholdDto>>> LinkedHouseholds(
        [FromServices] HouseholdLinkService links,
        [FromServices] CurrentUserContext currentUser,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var householdId = await currentUser.RequireHouseholdIdAsync();
        var ids = await links.GetLinkedHouseholdIdsAsync(householdId, ct);
        return await db.Set<Household>().AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .OrderBy(h => h.Name)
            .Select(h => new LinkedHouseholdDto { Id = h.Id, Name = h.Name })
            .ToListAsync(ct);
    }

    /// <summary>Previews a person referenced by a share token (cross-household), for
    /// the import-and-link confirmation screen.</summary>
    [HttpGet("link/{token}")]
    public async Task<ActionResult<PersonLinkPreviewDto>> PreviewLink(
        string token,
        [FromServices] CurrentUserContext currentUser,
        CancellationToken ct)
    {
        if (!Guid.TryParse(token, out var guid))
        {
            return new PersonLinkPreviewDto { Found = false };
        }

        var source = await _persons.GetByLinkTokenAsync(guid, ct);
        if (source is null)
        {
            return new PersonLinkPreviewDto { Found = false };
        }

        var householdId = await currentUser.RequireHouseholdIdAsync();
        return new PersonLinkPreviewDto
        {
            Found = true,
            SameHousehold = source.HouseholdId == householdId,
            DisplayName = source.DisplayName,
            HouseholdName = source.Household?.Name,
            Email = source.Email,
            Phone = source.Phone,
            Location = $"{source.PostalCode} {source.City}".Trim()
        };
    }

    /// <summary>Imports the shared person into the caller's household and links the
    /// two households so their lendings become mutually visible.</summary>
    [HttpPost("link/{token}")]
    public async Task<ActionResult<PersonLinkResultDto>> ImportLink(
        string token,
        [FromServices] HouseholdLinkService links,
        [FromServices] CurrentUserContext currentUser,
        CancellationToken ct)
    {
        if (!Guid.TryParse(token, out var guid))
        {
            return new PersonLinkResultDto { Success = false, Error = "Ungültiger Link." };
        }

        var source = await _persons.GetByLinkTokenAsync(guid, ct);
        if (source?.HouseholdId is null)
        {
            return new PersonLinkResultDto { Success = false, Error = "Person nicht gefunden." };
        }

        var householdId = await currentUser.RequireHouseholdIdAsync();
        if (source.HouseholdId == householdId)
        {
            return new PersonLinkResultDto { Success = false, Error = "Person gehört bereits zu deinem Haushalt." };
        }

        try
        {
            await _persons.CreateLoginlessAsync(new ApplicationUser
            {
                FirstName = source.FirstName,
                LastName = source.LastName,
                Nickname = source.Nickname,
                Email = source.Email,
                Phone = source.Phone,
                Street = source.Street,
                HouseNumber = source.HouseNumber,
                PostalCode = source.PostalCode,
                City = source.City,
                Notes = source.Notes
            }, ct);

            await links.LinkAsync(householdId, source.HouseholdId.Value, ct);
            return new PersonLinkResultDto { Success = true };
        }
        catch (Exception ex)
        {
            return new PersonLinkResultDto { Success = false, Error = ex.Message };
        }
    }
}
