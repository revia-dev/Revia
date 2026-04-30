using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Modules.Lending.Models;

public sealed class LendingEntry : IValidatableObject
{
    public int Id { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Category { get; set; }

    [Required]
    public int LenderId { get; set; }

    public Person? Lender { get; set; }

    [Required]
    public int BorrowerId { get; set; }

    public Person? Borrower { get; set; }

    [Required]
    public DateTime LoanDate { get; set; } = DateTime.Today;

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public LendingEntryStatus Status { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

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

        if (LenderId > 0 && BorrowerId > 0 && LenderId == BorrowerId)
        {
            yield return new ValidationResult(
                "Verleiher und Entleiher dürfen nicht identisch sein.",
                new[] { nameof(LenderId), nameof(BorrowerId) });
        }
    }
}
