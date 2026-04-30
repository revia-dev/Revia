using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Features.Lending.Models;

public sealed class Person
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Nickname { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Street { get; set; }

    [StringLength(20)]
    public string? HouseNumber { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<LendingEntry> LentEntries { get; set; } = new List<LendingEntry>();

    public ICollection<LendingEntry> BorrowedEntries { get; set; } = new List<LendingEntry>();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Nickname)
            ? $"{FirstName} {LastName}".Trim()
            : $"{Nickname} ({FirstName} {LastName})";
}
