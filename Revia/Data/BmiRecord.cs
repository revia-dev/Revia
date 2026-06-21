namespace Revia.Data;

/// <summary>
/// A single weight/BMI measurement for a person on a given day, with an optional
/// snapshot photo. Belongs to a household (tenant).
/// </summary>
public sealed class BmiRecord
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public ApplicationUser? Person { get; set; }

    public DateTime MeasuredOn { get; set; }

    public double WeightKg { get; set; }
    public double HeightCm { get; set; }

    /// <summary>Body mass index, stored so lists/charts don't recompute it.</summary>
    public double Bmi { get; set; }

    /// <summary>Stored file name of the snapshot photo, if any.</summary>
    public string? PhotoFileName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>kg / (m^2); 0 when height is not positive.</summary>
    public static double Calculate(double weightKg, double heightCm)
    {
        if (heightCm <= 0) return 0;
        var m = heightCm / 100.0;
        return Math.Round(weightKg / (m * m), 1);
    }
}
