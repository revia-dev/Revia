using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using PrivateLibrary.Modules.Lending.Models;

namespace PrivateLibrary.Data;

public sealed class ApplicationUser : IdentityUser
{
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Nickname { get; set; }

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

    public bool IsSystemUser { get; set; }

    public ICollection<LendingEntry> LentEntries { get; set; } = new List<LendingEntry>();
    public ICollection<LendingEntry> BorrowedEntries { get; set; } = new List<LendingEntry>();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Nickname)
            ? $"{FirstName} {LastName}".Trim()
            : $"{Nickname} ({FirstName} {LastName})";
}
