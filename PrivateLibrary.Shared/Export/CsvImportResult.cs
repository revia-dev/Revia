namespace PrivateLibrary.Shared.Export;

/// <summary>Outcome of importing a CSV into a module: how many rows were added,
/// skipped (e.g. a referenced person not found), and any per-row messages.</summary>
public sealed class CsvImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();

    public bool Success => Errors.Count == 0 || Imported > 0;
}
