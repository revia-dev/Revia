using Revia.Client.Offline;
using Revia.Shared.Movies;

namespace Revia.Client.Features.Movies;

/// <summary>Maps the movies collection to its REST API for the generic offline repository.</summary>
public sealed class MovieCollection : IOfflineCollection<MovieDto>
{
    public string Name => "movies";
    public string ListPath => "api/movies";
    public string ItemPath(string id) => $"api/movies/{id}";
    public string GetId(MovieDto item) => item.Id.ToString();
}
