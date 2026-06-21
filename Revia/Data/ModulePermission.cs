using System.ComponentModel.DataAnnotations;

namespace Revia.Data;

/// <summary>
/// A per-user, per-module access grant. The absence of a row means "use the
/// default" (see <c>PermissionService</c>): admins always have full access;
/// non-admins default to read + write so existing behaviour is preserved until an
/// admin tightens a specific module for a specific user.
/// </summary>
public sealed class ModulePermission
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>Module key from <c>AppModules</c> (e.g. "movies", "lending").</summary>
    [Required, StringLength(40)]
    public string Module { get; set; } = string.Empty;

    public bool CanRead { get; set; } = true;

    public bool CanWrite { get; set; } = true;
}
