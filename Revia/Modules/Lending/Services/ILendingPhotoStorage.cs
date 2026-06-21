namespace Revia.Modules.Lending.Services;

/// <summary>
/// Persists lending photo files on disk, outside wwwroot, so they are only
/// reachable through the authenticated photo endpoint.
/// </summary>
public interface ILendingPhotoStorage
{
    /// <summary>Writes the stream to a new file and returns the generated file name.</summary>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);

    /// <summary>Absolute path for a stored file name.</summary>
    string GetPath(string fileName);

    /// <summary>Deletes the file if it exists. Never throws on a missing file.</summary>
    void Delete(string fileName);
}
