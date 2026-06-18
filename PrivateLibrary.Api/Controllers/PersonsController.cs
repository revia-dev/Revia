using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Shared.Persons;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/persons")]
public sealed class PersonsController(IPersonService persons) : ControllerBase
{
    private readonly IPersonService _persons = persons;

    /// <summary>Lightweight person lookup for pickers (BMI, lending, games, …).</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<List<PersonLookupDto>>> Lookup(CancellationToken ct)
        => (await _persons.GetLookupAsync(ct))
            .Select(p => new PersonLookupDto { Id = p.Id, DisplayName = p.DisplayName })
            .ToList();
}
