using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Revia.Storage;

namespace Revia.Modules.Games;

/// <summary>Persists game files (rulebooks and match photos) on disk, outside wwwroot.</summary>
public interface IGameFileStorage
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
    string GetPath(string fileName);
    void Delete(string fileName);
}

public sealed class GameFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    : DiskFileStorage(configuration, environment, "GameFiles:StoragePath", "game-files"), IGameFileStorage;
