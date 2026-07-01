using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Revia.Storage;

namespace Revia.Modules.Lending.Services;

/// <summary>
/// Persists person profile photos on disk, outside wwwroot, so they are only
/// reachable through the authenticated photo endpoint.
/// </summary>
public interface IPersonPhotoStorage
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
    string GetPath(string fileName);
    void Delete(string fileName);
}

public sealed class PersonPhotoStorage(IConfiguration configuration, IWebHostEnvironment environment)
    : DiskFileStorage(configuration, environment, "PersonPhotos:StoragePath", "person-photos"), IPersonPhotoStorage;
