using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Shared.Movies;

/// <summary>A movie as exchanged with the client (mirrors the server's Film view model).</summary>
public sealed class MovieDto
{
    public Guid Id { get; set; }
    public string? Titel { get; set; }
    public string? Jahr { get; set; }
    public int? TmdbId { get; set; }
    public string? Fsk { get; set; }
    public List<string>? Anbieter { get; set; }
    public string? Kategorie { get; set; }
    public List<string>? Tags { get; set; }
    public string? Handlung { get; set; }
    public string? PosterPath { get; set; }
    public int? Runtime { get; set; }
}

/// <summary>Create/update payload for a movie.</summary>
public sealed class SaveMovieRequest
{
    /// <summary>Empty for create; the existing id for update.</summary>
    public Guid Id { get; set; }

    [StringLength(300)]
    public string? Titel { get; set; }
    public string? Jahr { get; set; }
    public int? TmdbId { get; set; }
    public string? Fsk { get; set; }
    public List<string>? Anbieter { get; set; }
    public string? Kategorie { get; set; }
    public List<string>? Tags { get; set; }
    public string? Handlung { get; set; }
    public string? PosterPath { get; set; }
    public int? Runtime { get; set; }
}
