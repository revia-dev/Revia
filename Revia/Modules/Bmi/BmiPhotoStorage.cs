using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Revia.Storage;

namespace Revia.Modules.Bmi;

/// <summary>Persists BMI snapshot photos on disk, outside wwwroot.</summary>
public interface IBmiPhotoStorage
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
    string GetPath(string fileName);
    void Delete(string fileName);
}

public sealed class BmiPhotoStorage(IConfiguration configuration, IWebHostEnvironment environment)
    : DiskFileStorage(configuration, environment, "BmiPhotos:StoragePath", "bmi-photos"), IBmiPhotoStorage;
