using Revia.Modules.Lending.Services;

namespace Revia.Tests;

/// <summary>In-memory stand-in for ILendingPhotoStorage used in unit tests.</summary>
internal sealed class FakeLendingPhotoStorage : ILendingPhotoStorage
{
    public Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default)
        => Task.FromResult($"{Guid.NewGuid():N}{fileExtension}");

    public string GetPath(string fileName) => fileName;

    public void Delete(string fileName) { }
}
