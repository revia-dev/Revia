using System.ComponentModel.DataAnnotations;

namespace Revia.Shared.Bmi;

/// <summary>A BMI measurement as returned to the client.</summary>
public sealed class BmiRecordDto
{
    public int Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string PersonDisplayName { get; set; } = string.Empty;
    public DateTime MeasuredOn { get; set; }
    public double WeightKg { get; set; }
    public double HeightCm { get; set; }
    public double Bmi { get; set; }
    public bool HasPhoto { get; set; }
    /// <summary>URL the client can use to load the photo (server-served, SW-cached).</summary>
    public string? PhotoUrl { get; set; }
}

/// <summary>Create payload for a BMI measurement. The optional photo is carried as
/// base64 so it fits the JSON-based offline outbox; small phone snapshots only.</summary>
public sealed class AddBmiRecordRequest
{
    [Required]
    public string PersonId { get; set; } = string.Empty;

    public DateTime MeasuredOn { get; set; }

    [Range(0.1, 1000)]
    public double WeightKg { get; set; }

    [Range(0.1, 300)]
    public double HeightCm { get; set; }

    /// <summary>Base64-encoded photo bytes (no data: prefix), or null.</summary>
    public string? PhotoBase64 { get; set; }

    /// <summary>MIME type of the photo (e.g. "image/jpeg"), required if PhotoBase64 is set.</summary>
    public string? PhotoContentType { get; set; }

    public string? PhotoName { get; set; }
}
