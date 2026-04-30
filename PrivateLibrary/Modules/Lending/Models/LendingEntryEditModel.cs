using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Features.Lending.Models;

public sealed class LendingEntryEditModel : IValidatableObject
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

    [Required(ErrorMessage = "Bitte einen Verleiher auswählen.")]
    public int? LenderId { get; set; }

    [Required(ErrorMessage = "Bitte einen Entleiher auswählen.")]
    public int? BorrowerId { get; set; }

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

        if (LenderId.HasValue && BorrowerId.HasValue && LenderId.Value == BorrowerId.Value)
        {
            yield return new ValidationResult(
                "Verleiher und Entleiher dürfen nicht identisch sein.",
                new[] { nameof(LenderId), nameof(BorrowerId) });
        }
    }
}
