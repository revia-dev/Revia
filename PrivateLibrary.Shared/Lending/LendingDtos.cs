using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Shared.Lending;

/// <summary>Lending status, mirrors the server enum (Active=0 … GivenAway=4).</summary>
public enum LendingStatusDto { Active = 0, Overdue = 1, Returned = 2, Lost = 3, GivenAway = 4 }

public sealed class LendingEntryDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string> Items { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public List<string> CategoryNames { get; set; } = new();

    public string LenderId { get; set; } = string.Empty;
    public string LenderName { get; set; } = string.Empty;
    public string BorrowerId { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;

    public DateTime LoanDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public LendingStatusDto Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class SaveLendingEntryRequest
{
    /// <summary>0 for create; existing id for update.</summary>
    public int Id { get; set; }

    [StringLength(150)]
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string> Items { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();

    [Required]
    public string LenderId { get; set; } = string.Empty;
    [Required]
    public string BorrowerId { get; set; } = string.Empty;

    public DateTime LoanDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class LendingCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateLendingCategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
