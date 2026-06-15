using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace PrivateLibrary.Modules.Export;

/// <summary>Small helper to build a semicolon-separated CSV download (UTF-8 BOM).</summary>
public static class CsvExport
{
    public static async Task<byte[]> BuildAsync(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        await using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" }))
        {
            foreach (var header in headers) csv.WriteField(header);
            await csv.NextRecordAsync();

            foreach (var row in rows)
            {
                foreach (var field in row) csv.WriteField(field ?? string.Empty);
                await csv.NextRecordAsync();
            }
        }

        return stream.ToArray();
    }
}
