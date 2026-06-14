using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Data;

/// <summary>
/// A tenant. Every domain record (movies, lendings, categories, …) belongs to
/// exactly one household, and every person belongs to a household. Households are
/// separated logically in a single shared database via a HouseholdId column plus
/// explicit filtering in the services.
/// </summary>
public sealed class Household
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short, human-shareable code used to invite new members into this household.</summary>
    [Required]
    [StringLength(32)]
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();

    /// <summary>Generates a compact, unambiguous invite code (no easily-confused characters).</summary>
    public static string NewInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[8];
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }
        return new string(chars);
    }
}
