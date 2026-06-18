using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Models;
using PrivateLibrary.Services;
using PrivateLibrary.Shared.Movies;

namespace PrivateLibrary.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/movies")]
public sealed class MoviesController(MovieService movies) : ControllerBase
{
    private readonly MovieService _movies = movies;

    [HttpGet]
    public async Task<ActionResult<List<MovieDto>>> GetAll(CancellationToken ct)
        => (await _movies.GetAllAsync(ct)).Select(ToDto).ToList();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MovieDto>> GetById(Guid id, CancellationToken ct)
    {
        var film = await _movies.GetByIdAsync(id, ct);
        return film is null ? NotFound() : ToDto(film);
    }

    [HttpPost]
    public async Task<ActionResult<MovieDto>> Save([FromBody] SaveMovieRequest request, CancellationToken ct)
        => ToDto(await _movies.UpsertAsync(ToFilm(request), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MovieDto>> Update(Guid id, [FromBody] SaveMovieRequest request, CancellationToken ct)
    {
        request.Id = id;
        return ToDto(await _movies.UpsertAsync(ToFilm(request), ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _movies.DeleteAsync(id, ct) ? NoContent() : NotFound();

    private static MovieDto ToDto(Film f) => new()
    {
        Id = f.Id, Titel = f.Titel, Jahr = f.Jahr, TmdbId = f.TmdbId, Fsk = f.Fsk,
        Anbieter = f.Anbieter, Kategorie = f.Kategorie, Tags = f.Tags,
        Handlung = f.Handlung, PosterPath = f.PosterPath, Runtime = f.Runtime
    };

    private static Film ToFilm(SaveMovieRequest r) => new()
    {
        Id = r.Id, Titel = r.Titel, Jahr = r.Jahr, TmdbId = r.TmdbId, Fsk = r.Fsk,
        Anbieter = r.Anbieter, Kategorie = r.Kategorie, Tags = r.Tags,
        Handlung = r.Handlung, PosterPath = r.PosterPath, Runtime = r.Runtime
    };
}
