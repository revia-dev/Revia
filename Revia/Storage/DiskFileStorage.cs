using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Revia.Storage;

/// <summary>
/// Shared on-disk file store (kept outside wwwroot): random-named files under a root that
/// comes from a per-module config key, falling back to <c>ContentRoot/data/&lt;folder&gt;</c>.
/// Concrete stores (BMI photos, lending photos, person photos, game files) become one-liners
/// that just pass their key + folder; this base holds the identical save / read / delete
/// logic each of them used to duplicate.
/// </summary>
public abstract class DiskFileStorage
{
    private readonly string _root;

    protected DiskFileStorage(IConfiguration configuration, IWebHostEnvironment environment,
                              string configKey, string defaultFolder)
    {
        var configured = configuration[configKey];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "data", defaultFolder)
            : configured;
        Directory.CreateDirectory(_root);
    }

    /// <summary>Writes <paramref name="content"/> to a new random file and returns its name.</summary>
    public async Task<string> SaveAsync(Stream content, string fileExtension,
                                        CancellationToken cancellationToken = default)
    {
        var safeExtension = string.IsNullOrWhiteSpace(fileExtension) ? ".img" : fileExtension;
        if (!safeExtension.StartsWith('.')) { safeExtension = "." + safeExtension; }

        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var path = Path.Combine(_root, fileName);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(target, cancellationToken);
        }
        return fileName;
    }

    /// <summary>Absolute path of a stored file (name is sanitised to prevent traversal).</summary>
    public string GetPath(string fileName) => Path.Combine(_root, Path.GetFileName(fileName));

    public void Delete(string fileName)
    {
        var path = GetPath(fileName);
        if (File.Exists(path)) { File.Delete(path); }
    }
}
