using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace GenericInventory.Data.Import;

public class XlsxWorkbookReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public IReadOnlyList<SpreadsheetRow> ReadRows(byte[] workbookBytes, string sheetName)
    {
        using var stream = new MemoryStream(workbookBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sharedStrings = ReadSharedStrings(archive);
        var sheetPath = ResolveWorksheetPath(archive, sheetName);
        if (string.IsNullOrWhiteSpace(sheetPath))
        {
            return Array.Empty<SpreadsheetRow>();
        }

        var sheet = ReadXml(archive, sheetPath);
        var rows = sheet.Descendants(Main + "row")
            .Select(row => ReadRawRow(row, sharedStrings))
            .Where(row => row.Count > 0)
            .ToList();

        if (rows.Count == 0)
        {
            return Array.Empty<SpreadsheetRow>();
        }

        var headers = rows[0];
        return rows
            .Skip(1)
            .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Select(row => ToSpreadsheetRow(headers, row))
            .ToList();
    }

    private static SpreadsheetRow ToSpreadsheetRow(Dictionary<int, string> headers, Dictionary<int, string> values)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (index, header) in headers)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            row[SpreadsheetRow.Normalize(header)] = values.TryGetValue(index, out var value) ? value : string.Empty;
        }

        return new SpreadsheetRow(row);
    }

    private static Dictionary<int, string> ReadRawRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var cells = new Dictionary<int, string>();
        foreach (var cell in row.Elements(Main + "c"))
        {
            var reference = cell.Attribute("r")?.Value ?? string.Empty;
            var columnIndex = ColumnIndex(reference);
            if (columnIndex < 1)
            {
                continue;
            }

            cells[columnIndex] = ReadCellValue(cell, sharedStrings);
        }

        return cells;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value ?? string.Empty;
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(Main + "t").Select(text => text.Value));
        }

        var raw = cell.Element(Main + "v")?.Value ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex) &&
            sharedIndex >= 0 &&
            sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex];
        }

        return raw;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return Array.Empty<string>();
        }

        var document = ReadXml(entry);
        return document.Root?
            .Elements(Main + "si")
            .Select(item => string.Concat(item.Descendants(Main + "t").Select(text => text.Value)))
            .ToList() ?? new List<string>();
    }

    private static string ResolveWorksheetPath(ZipArchive archive, string sheetName)
    {
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var sheet = workbook.Descendants(Main + "sheet")
            .FirstOrDefault(item => string.Equals(item.Attribute("name")?.Value, sheetName, StringComparison.OrdinalIgnoreCase));
        var relationId = sheet?.Attribute(Relationships + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relationId))
        {
            return string.Empty;
        }

        var rels = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = rels.Descendants(PackageRelationships + "Relationship")
            .FirstOrDefault(item => string.Equals(item.Attribute("Id")?.Value, relationId, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Target")?.Value;

        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        return target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : $"xl/{target.TrimStart('/')}";
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new FileNotFoundException($"XLSX entry not found: {path}");
        return ReadXml(entry);
    }

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            index = (index * 26) + char.ToUpperInvariant(ch) - 'A' + 1;
        }

        return index;
    }
}
