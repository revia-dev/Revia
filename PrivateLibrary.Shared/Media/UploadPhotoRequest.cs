namespace PrivateLibrary.Shared.Media;

/// <summary>A photo uploaded to an existing entity (match, lending, …). The bytes
/// ride as base64 so it fits the same JSON pipeline as the rest of the API.</summary>
public sealed class UploadPhotoRequest
{
    /// <summary>Base64-encoded image bytes (no data: prefix).</summary>
    public string Base64 { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. "image/jpeg".</summary>
    public string ContentType { get; set; } = "image/jpeg";

    /// <summary>Original file name (optional, used to derive the extension).</summary>
    public string? FileName { get; set; }
}
