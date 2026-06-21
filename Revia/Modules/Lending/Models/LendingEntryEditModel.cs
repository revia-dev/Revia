using System.ComponentModel.DataAnnotations;

// Legacy-compat: the obsolete ItemName/Category members map old DB columns and are
// read here on purpose during the transition to Items/CategoryLinks.
#pragma warning disable CS0618


namespace Revia.Modules.Lending.Models;

public sealed class LendingEntryEditModel : IValidatableObject
{
    public int Id { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Obsolete("Use ItemNames instead.")]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Obsolete("Use SelectedCategoryIds instead.")]
    [StringLength(100)]
    public string? Category { get; set; }

    public List<string> ItemNames { get; set; } = new();
    public List<int> SelectedCategoryIds { get; set; } = new();
    public string NewItemName { get; set; } = string.Empty;
    public string NewCategoryName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitte einen Verleiher auswählen.")]
    public string? LenderId { get; set; }

    [Required(ErrorMessage = "Bitte einen Entleiher auswählen.")]
    public string? BorrowerId { get; set; }

    [Required(ErrorMessage = "Bitte ein Ausleihdatum angeben.")]
    public DateTime? LoanDate { get; set; } = DateTime.Today;

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public LendingEntryStatus Status { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LoanDate.HasValue && DueDate.HasValue && DueDate.Value.Date < LoanDate.Value.Date)
        {
            yield return new ValidationResult(
                "Das geplante Rückgabedatum darf nicht vor dem Ausleihdatum liegen.",
                new[] { nameof(DueDate) });
        }

        if (LoanDate.HasValue && ReturnedAt.HasValue && ReturnedAt.Value.Date < LoanDate.Value.Date)
        {
            yield return new ValidationResult(
                "Das tatsächliche Rückgabedatum darf nicht vor dem Ausleihdatum liegen.",
                new[] { nameof(ReturnedAt) });
        }

        if (!string.IsNullOrWhiteSpace(LenderId) &&
            !string.IsNullOrWhiteSpace(BorrowerId) &&
            string.Equals(LenderId, BorrowerId, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "Verleiher und Entleiher dürfen nicht identisch sein.",
                new[] { nameof(LenderId), nameof(BorrowerId) });
        }

        if (ItemNames.Count == 0 || ItemNames.All(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Mindestens ein Gegenstand muss angegeben werden.",
                new[] { nameof(ItemNames), nameof(ItemName) });
        }
    }
}
