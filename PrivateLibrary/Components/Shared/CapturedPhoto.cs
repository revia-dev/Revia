namespace PrivateLibrary.Components.Shared;

/// <summary>
/// A photo captured live from the device camera via <c>MediaUpload</c>.
/// </summary>
public sealed record CapturedPhoto(byte[] Data, string ContentType, string FileName)
{
    public System.IO.Stream OpenReadStream() => new System.IO.MemoryStream(Data, writable: false);
}
