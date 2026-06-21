using Microsoft.Extensions.Options;

namespace Revia.Modules.Lending.Services;

public sealed class LendingPhotoStorage : ILendingPhotoStorage
{
    private readonly string _root;

    public LendingPhotoStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Configurable for deployment (e.g. an Azure /home/data volume); defaults to
        // a writable data directory next to the content root, never inside wwwroot.
        var configured = configuration["LendingPhotos:StoragePath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "data", "lending-photos")
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
        // Guard against path traversal — only the bare file name is ever valid.
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
