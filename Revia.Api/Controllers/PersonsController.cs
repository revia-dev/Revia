using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Revia.Api.Auth;
using Revia.Data;
using Revia.Services;
using Revia.Modules.Lending.Services;
using Revia.Shared.Persons;

namespace Revia.Api.Controllers;

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
            .Select(ToCard)
            .ToList();

    private static PersonCardDto ToCard(ApplicationUser p) => new()
    {
        Id = p.Id,
        DisplayName = p.DisplayName,
        FirstName = p.FirstName,
        LastName = p.LastName,
        Nickname = p.Nickname,
        Email = p.Email,
        Phone = p.Phone,
        IsLoginless = p.IsLoginless,
        LinkToken = p.PersonLinkToken.ToString()
    };

    /// <summary>Creates a person without a login (data only). Any user with persons
    /// write access may add these; an admin can later grant a login.</summary>
    [HttpPost]
    public async Task<ActionResult<PersonResultDto>> Create([FromBody] SavePersonRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) && string.IsNullOrWhiteSpace(request.LastName))
        {
            return new PersonResultDto { Success = false, Error = "Name fehlt." };
        }

        // A login may be set on create — but only by an admin. Non-admins (or empty
        // login fields) get a loginless data-only person.
        var wantsLogin = !string.IsNullOrWhiteSpace(request.UserName) || !string.IsNullOrWhiteSpace(request.Password);
        if (wantsLogin && !User.IsInRole("Admin"))
        {
            return new PersonResultDto { Success = false, Error = "Nur Admins dürfen Anmeldedaten vergeben." };
        }

        try
        {
            if (wantsLogin)
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new PersonResultDto { Success = false, Error = "Benutzername und Passwort erforderlich." };
                }
                var user = Apply(new ApplicationUser(), request);
                user.UserName = request.UserName!.Trim();
                var withLogin = await _persons.CreateAsync(user, request.Password!,
                    string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role!, ct);
                return new PersonResultDto { Success = true, Id = withLogin.Id };
            }

            var created = await _persons.CreateLoginlessAsync(Apply(new ApplicationUser(), request), ct);
            return new PersonResultDto { Success = true, Id = created.Id };
        }
        catch (Exception ex)
        {
            return new PersonResultDto { Success = false, Error = ex.Message };
        }
    }

    /// <summary>Updates a person's data fields (household-scoped).</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PersonResultDto>> Update(string id, [FromBody] SavePersonRequest request, CancellationToken ct)
    {
        var person = await _persons.GetByIdAsync(id, ct);
        if (person is null) { return new PersonResultDto { Success = false, Error = "Person nicht gefunden." }; }

        var updated = await _persons.UpdateAsync(Apply(person, request), roleName: null, ct);
        return updated is null
            ? new PersonResultDto { Success = false, Error = "Aktualisierung fehlgeschlagen." }
            : new PersonResultDto { Success = true, Id = id };
    }

    /// <summary>Deletes a person (household-scoped).</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<PersonResultDto>> Delete(string id, CancellationToken ct)
    {
        var r = await _persons.DeleteAsync(id, ct);
        return new PersonResultDto { Success = r.Succeeded, Error = r.ErrorMessage, Id = id };
    }

    /// <summary>Admin-only: grants an existing loginless person a login (username +
    /// password + role) so they can sign in.</summary>
    [HttpPost("{id}/login")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PersonResultDto>> AddLogin(string id, [FromBody] AddLoginRequest request, CancellationToken ct)
    {
        var person = await _persons.GetByIdAsync(id, ct);
        if (person is null) { return new PersonResultDto { Success = false, Error = "Person nicht gefunden." }; }
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new PersonResultDto { Success = false, Error = "Benutzername und Passwort erforderlich." };
        }

        person.UserName = request.UserName.Trim();
        person.IsLoginless = false;
        var updated = await _persons.UpdateAsync(person, string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role, ct);
        if (updated is null) { return new PersonResultDto { Success = false, Error = "Anmeldung konnte nicht angelegt werden." }; }

        var pw = await _persons.SetPasswordAsync(id, request.Password, ct);
        return new PersonResultDto { Success = pw.Succeeded, Error = pw.ErrorMessage, Id = id };
    }

    private static ApplicationUser Apply(ApplicationUser u, SavePersonRequest r)
    {
        u.FirstName = r.FirstName?.Trim() ?? string.Empty;
        u.LastName = r.LastName?.Trim() ?? string.Empty;
        u.Nickname = Blank(r.Nickname);
        u.Email = Blank(r.Email);
        u.Phone = Blank(r.Phone);
        u.Street = Blank(r.Street);
        u.HouseNumber = Blank(r.HouseNumber);
        u.PostalCode = Blank(r.PostalCode);
        u.City = Blank(r.City);
        return u;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
