using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Modules.Lending.Models;

public sealed class PersonEditModel : IValidatableObject
{
    public string? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Nickname { get; set; }

    [Required]
    [StringLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

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

    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    [StringLength(100)]
    public string? ConfirmPassword { get; set; }

    public bool IsSystemUser { get; set; }
    public bool CanManageRoles { get; set; }
    public string SelectedRole { get; set; } = "User";
    public IReadOnlyList<string> AvailableRoles { get; set; } = Array.Empty<string>();

    public bool IsNew => string.IsNullOrWhiteSpace(Id);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsNew && string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult(
                "Bitte ein Passwort eingeben.",
                new[] { nameof(Password) });
        }

        if (!string.IsNullOrWhiteSpace(Password) && Password != ConfirmPassword)
        {
            yield return new ValidationResult(
                "Die Passwörter stimmen nicht überein.",
                new[] { nameof(ConfirmPassword) });
        }

        if (CanManageRoles && string.IsNullOrWhiteSpace(SelectedRole))
        {
            yield return new ValidationResult(
                "Bitte eine Rolle auswählen.",
                new[] { nameof(SelectedRole) });
        }
    }
}
