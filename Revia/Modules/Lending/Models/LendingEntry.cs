using System.ComponentModel.DataAnnotations;
using Revia.Data;

// Legacy-compat: the obsolete ItemName/Category members map old DB columns and are
// read here on purpose during the transition to Items/CategoryLinks.
#pragma warning disable CS0618


namespace Revia.Modules.Lending.Models;

public sealed class LendingEntry : IValidatableObject
{
    public int Id { get; set; }

    /// <summary>Owning household (tenant). The lending is created in one household;
    /// it may additionally be visible to a linked household via the person QR link.</summary>
    public int HouseholdId { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Obsolete("Use Items instead.")]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Obsolete("Use CategoryLinks instead.")]
    [StringLength(100)]
    public string? Category { get; set; }

    [Required]
    public string LenderId { get; set; } = string.Empty;

    public ApplicationUser? Lender { get; set; }

    [Required]
    public string BorrowerId { get; set; } = string.Empty;

    public ApplicationUser? Borrower { get; set; }

    [Required]
    public DateTime LoanDate { get; set; } = DateTime.Today;

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public LendingEntryStatus Status { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<LendingItem> Items { get; set; } = new List<LendingItem>();
    public ICollection<LendingEntryCategory> CategoryLinks { get; set; } = new List<LendingEntryCategory>();
    public ICollection<LendingPhoto> Photos { get; set; } = new List<LendingPhoto>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDate.HasValue && DueDate.Value.Date < LoanDate.Date)
        {
            yield return new ValidationResult(
                "Das geplante Rückgabedatum darf nicht vor dem Ausleihdatum liegen.",
                new[] { nameof(DueDate) });
        }

        if (ReturnedAt.HasValue && ReturnedAt.Value.Date < LoanDate.Date)
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

        if (Items.Count == 0 || Items.All(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            yield return new ValidationResult(
                "Mindestens ein Gegenstand muss angegeben werden.",
                new[] { nameof(Items), nameof(ItemName) });
        }
    }
}

