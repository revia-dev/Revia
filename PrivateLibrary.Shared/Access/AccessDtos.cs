namespace PrivateLibrary.Shared.Access;

/// <summary>One module the client knows about: key, label, route, icon.</summary>
public sealed class ModuleInfoDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

/// <summary>Effective read/write access for one module for the current user.</summary>
public sealed class ModuleAccessDto
{
    public string Module { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
}

/// <summary>The current user's identity + effective access, cached for offline gating.</summary>
public sealed class MyAccessDto
{
    public bool IsAdmin { get; set; }
    public List<ModuleAccessDto> Modules { get; set; } = new();
}

/// <summary>One module tile in the user's dashboard tile layout.</summary>
public sealed class TileLayoutItemDto
{
    public string Key { get; set; } = string.Empty;
    public bool Visible { get; set; }
}

/// <summary>One dashboard section in the user's section layout.</summary>
public sealed class SectionLayoutItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Visible { get; set; }
}

/// <summary>The full personal layout: ordered tiles + ordered sections.</summary>
public sealed class LayoutDto
{
    public List<TileLayoutItemDto> Tiles { get; set; } = new();
    public List<SectionLayoutItemDto> Sections { get; set; } = new();
}

/// <summary>Save payload for the personal layout (order + visibility).</summary>
public sealed class SaveLayoutRequest
{
    public List<TileLayoutItemDto> Tiles { get; set; } = new();
    public List<SectionLayoutItemDto> Sections { get; set; } = new();
}

// ---- Admin: per-user module grants ----

/// <summary>A login-capable user, for the admin permissions picker.</summary>
public sealed class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>One user's explicit (default-filled) grants, for the admin editor.</summary>
public sealed class UserGrantsDto
{
    public string UserId { get; set; } = string.Empty;
    public List<ModuleAccessDto> Modules { get; set; } = new();
}

/// <summary>Admin upsert of one user/module grant.</summary>
public sealed class SetGrantRequest
{
    public string Module { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
}
