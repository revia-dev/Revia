using Microsoft.AspNetCore.Identity;

namespace PrivateLibrary.Data;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsSystemUser { get; set; }
}
