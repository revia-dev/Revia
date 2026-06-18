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

    /// <summary>The household this person belongs to. Nullable only for the seeded
    /// system admin before the default household exists; every real person has one.</summary>
    public int? HouseholdId { get; set; }

    public Household? Household { get; set; }

    /// <summary>Stored file name of this person's single profile photo, if any.</summary>
    [StringLength(260)]
    public string? ProfilePhotoFileName { get; set; }

    /// <summary>A loginless person is a pure data record (usable in lendings, games,
    /// BMI, …) that has no password and cannot sign in.</summary>
    public bool IsLoginless { get; set; }

    /// <summary>Stable token encoded in this person's QR code. Scanning it in another
    /// household imports the person there and links the two households.</summary>
    public Guid PersonLinkToken { get; set; } = Guid.NewGuid();

    /// <summary>Per-user dashboard layout: a compact CSV of section keys in display
    /// order, each optionally prefixed with '-' to mark it hidden. Empty/null means
    /// "use the default layout". See <c>DashboardLayoutService</c>.</summary>
    [StringLength(500)]
    public string? DashboardLayout { get; set; }

    /// <summary>Per-user module-tile layout: a compact CSV of module keys in display
    /// order, each optionally prefixed with '-' to mark it hidden. Empty/null means
    /// "use the default order, all visible". See <c>ModuleTileLayoutService</c>.</summary>
    [StringLength(500)]
    public string? ModuleTileLayout { get; set; }

    public ICollection<LendingEntry> LentEntries { get; set; } = new List<LendingEntry>();
    public ICollection<LendingEntry> BorrowedEntries { get; set; } = new List<LendingEntry>();

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Nickname)
            ? $"{FirstName} {LastName}".Trim()
            : $"{Nickname} ({FirstName} {LastName})";
}
