namespace Revia.Shared.Backup;

/// <summary>
/// Portable backup document, the cross-app contract shared with the native app. Each
/// table is optional: a backup only carries the tables it covers, and a restore
/// overwrites only the tables present in the file (selective restore). Field names are
/// serialised camelCase to match the native app's JSON exactly.
///
/// This .NET side maps the <see cref="Movies"/> catalogue (clean Guid ids), plus the
/// person-keyed <see cref="WatchProgress"/> and <see cref="BmiRecords"/> tables. The
/// latter carry <c>personName</c> so each app can re-attach the rows to its OWN person
/// (web Identity ids ≠ native UUIDs — they reconcile by name). Tables not modelled here
/// are accepted and ignored.
/// </summary>
public sealed class BackupDocument
{
    public int FormatVersion { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    public List<MovieBak>? Movies { get; set; }
    public List<WatchProgressBak>? WatchProgress { get; set; }
    public List<BmiBak>? BmiRecords { get; set; }
    public List<LoanBak>? Loans { get; set; }
    public List<MatchBak>? Matches { get; set; }
    public List<WishlistBak>? Wishlists { get; set; }
    public List<TvAiringBak>? TvAirings { get; set; }
    public List<GameBak>? Games { get; set; }

    /// <summary>Table keys actually present (non-null) — the ones a restore overwrites.</summary>
    public List<string> PresentTables()
    {
        var t = new List<string>();
        if (Movies is not null) t.Add("movies");
        if (WatchProgress is not null) t.Add("watchProgress");
        if (BmiRecords is not null) t.Add("bmiRecords");
        if (Loans is not null) t.Add("loans");
        if (Matches is not null) t.Add("matches");
        if (Wishlists is not null) t.Add("wishlists");
        if (TvAirings is not null) t.Add("tvAirings");
        if (Games is not null) t.Add("games");
        return t;
    }
}

/// <summary>A movie in the backup. Field names mirror the native <c>MovieBak</c>.</summary>
public sealed class MovieBak
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? AgeRating { get; set; }
    public string Category { get; set; } = "movie";
    public List<string> Providers { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string? Plot { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? SeriesName { get; set; }
    public double? Rating { get; set; }
    public bool Watched { get; set; }
    public string? PosterPath { get; set; }
}

/// <summary>Per-person watch progress. <c>personName</c> is the cross-app key (the row
/// re-attaches to the local person of that name); <c>movieId</c> is the shared Guid.</summary>
public sealed class WatchProgressBak
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public Guid MovieId { get; set; }
    public string Status { get; set; } = "unwatched";
    public int? PositionSeconds { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A BMI measurement. <c>personName</c> is the cross-app key; the snapshot photo
/// travels inline as bytes (web stores it as a file, native as data).</summary>
public sealed class BmiBak
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public DateTime MeasuredOn { get; set; }
    public double WeightKg { get; set; }
    public double HeightCm { get; set; }
    public byte[]? PhotoData { get; set; }
}

/// <summary>A loan. Lender/borrower reconcile by name; the names are also stored
/// denormalised so the record stays readable even if a person can't be resolved.</summary>
public sealed class LoanBak
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public List<string> Items { get; set; } = new();
    public Guid LenderPersonId { get; set; }
    public string LenderName { get; set; } = string.Empty;
    public Guid BorrowerPersonId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public DateTime LoanDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public byte[]? PhotoData { get; set; }
}

/// <summary>A game match. The game reconciles by name; participants by name (+score).</summary>
public sealed class MatchBak
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public DateTime PlayedOn { get; set; }
    public string? Note { get; set; }
    public byte[]? PhotoData { get; set; }
    public List<ParticipantBak> Participants { get; set; } = new();
}

public sealed class ParticipantBak
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>A wishlist. The native app keeps lists household-wide (no owner); the web
/// app owns them per user, so <c>ownerName</c> carries the owner across (null = no owner,
/// the importer becomes the owner on the web side).</summary>
public sealed class WishlistBak
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? OwnerName { get; set; }
    public List<WishlistItemBak> Items { get; set; } = new();
}

public sealed class WishlistItemBak
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? Url { get; set; }
    public byte[]? PhotoData { get; set; } // native only; the web item has no photo field
    public bool Fulfilled { get; set; }
}

/// <summary>A single TV airing. Native keeps these flat; the web groups them under a
/// named <c>TvProgram</c> with an owner, so <c>programName</c>/<c>ownerName</c> let the
/// web rebuild its programs. Web entries always reference a movie (no custom title) and
/// require it to exist locally; <c>scope</c> is "family" (shared) or "privateProgram".</summary>
public sealed class TvAiringBak
{
    public Guid Id { get; set; }
    public Guid? MovieId { get; set; }
    public string? CustomTitle { get; set; }
    public byte[]? PhotoData { get; set; }
    public string? Channel { get; set; }
    public string? Note { get; set; }
    public DateTime StartsAt { get; set; }
    public string Scope { get; set; } = "privateProgram";
    public Guid? OwnerPersonId { get; set; }
    public string? OwnerName { get; set; }
    public int? BaseEpisode { get; set; }
    public string? ProgramName { get; set; }
}

/// <summary>A game in the catalogue. Reconciles by <c>name</c> (web int ids ≠ native
/// UUIDs); types are the native strings (board/console/card/other); rules &amp; expansion
/// files travel inline as bytes (both apps store them as files).</summary>
public sealed class GameBak
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public List<string> Types { get; set; } = new();
    public bool HasRules { get; set; }
    public byte[]? RulesData { get; set; }
    public string? RulesFileName { get; set; }
    public List<ExpansionBak> Expansions { get; set; } = new();
    public List<ScoreDefBak> ScoreDefinitions { get; set; } = new();
}

public sealed class ExpansionBak
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<ExpansionFileBak> Files { get; set; } = new();
}

public sealed class ExpansionFileBak
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[]? Data { get; set; }
}

public sealed class ScoreDefBak
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool HigherIsBetter { get; set; } = true;
}
