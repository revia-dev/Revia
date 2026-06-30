using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Revia.Api.Backup;
using Revia.Shared.Backup;

namespace Revia.Api.Controllers;

/// <summary>
/// Full-database backup as the portable JSON the native app reads/writes, so the two
/// databases can be kept consistent by exchanging one file. Admin-only (the buttons in
/// the client are admin-gated too). Restore is selective: only the tables present in
/// the uploaded document are overwritten.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/backup")]
public sealed class BackupController(BackupService backup) : ControllerBase
{
    private readonly BackupService _backup = backup;

    /// <summary>Exports the household's data as a backup document (download).</summary>
    [HttpGet]
    public async Task<ActionResult<BackupDocument>> Export(CancellationToken ct)
        => await _backup.ExportAsync(ct);

    /// <summary>Restores from an uploaded backup document, overwriting only the tables
    /// it contains. Returns which tables were overwritten.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Restore([FromBody] BackupDocument document, CancellationToken ct)
    {
        var overwritten = await _backup.RestoreAsync(document, ct);
        return new { overwritten };
    }
}
