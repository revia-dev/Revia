namespace Revia.Modules.Games;

/// <summary>Persists game files (rulebooks and match photos) on disk, outside wwwroot.</summary>
public interface IGameFileStorage
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
    string GetPath(string fileName);
    void Delete(string fileName);
}

public sealed class GameFileStorage : IGameFileStorage
{
    private readonly string _root;

    public GameFileStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["GameFiles:StoragePath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "data", "game-files")
            : configured;

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default)
    {
        var safeExtension = string.IsNullOrWhiteSpace(fileExtension) ? ".img" : fileExtension;
        if (!safeExtension.StartsWith('.'))
        {
            safeExtension = "." + safeExtension;
        }

        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var path = Path.Combine(_root, fileName);

        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return fileName;
    }

    public string GetPath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return Path.Combine(_root, safeName);
    }

    public void Delete(string fileName)
    {
        var path = GetPath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
