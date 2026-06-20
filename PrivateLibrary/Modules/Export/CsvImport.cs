using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace PrivateLibrary.Modules.Export;

/// <summary>
/// Parses the semicolon-separated CSV produced by <see cref="CsvExport"/> back into
/// rows keyed by header name, so importers can read fields by their column title
/// regardless of column order. Tolerant of a UTF-8 BOM and missing trailing columns.
/// </summary>
public static class CsvImport
{
    /// <summary>Reads the CSV and returns one dictionary per data row (header → value).
    /// Header lookups are case-insensitive.</summary>
    public static List<Dictionary<string, string>> Parse(Stream content)
    {
        var rows = new List<Dictionary<string, string>>();

        using var reader = new StreamReader(content); // detects + strips the BOM
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            // Be forgiving about ragged rows / extra whitespace.
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            return rows;
        }

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        while (csv.Read())
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                dict[header] = csv.TryGetField(header, out string? value) ? (value ?? string.Empty) : string.Empty;
            }
            rows.Add(dict);
        }

        return rows;
    }
}
