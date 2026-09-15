using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using GenericInventory.Data;
using GenericInventory.Data.Import;
using GenericInventory.Products.Dtos;
using GenericInventory.Products.Entities;

namespace GenericInventory.Products.Services;

public class CatalogImportService
{
    private readonly AppDbContext _db;
    private readonly XlsxWorkbookReader _xlsxReader;

    public CatalogImportService(AppDbContext db, XlsxWorkbookReader xlsxReader)
    {
        _db = db;
        _xlsxReader = xlsxReader;
    }

    public async Task<CatalogImportPreviewDto> PreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var rows = await ReadRowsAsync(file, cancellationToken);
        var columns = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(column => column)
            .ToList();

        return new CatalogImportPreviewDto
        {
            Columns = columns,
            Rows = rows.Take(8).Select(row => new Dictionary<string, string>(row.Values, StringComparer.OrdinalIgnoreCase)).ToList(),
            TotalRows = rows.Count,
            Message = rows.Count == 0 ? "Nenhuma linha encontrada no arquivo." : "Previa carregada com sucesso."
        };
    }

    public async Task<CatalogImportResultDto> ImportAsync(
        IFormFile file,
        CatalogImportMappingDto mapping,
        CancellationToken cancellationToken = default)
    {
        var rows = await ReadRowsAsync(file, cancellationToken);
        var warnings = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        if (string.IsNullOrWhiteSpace(mapping.CodeColumn) || string.IsNullOrWhiteSpace(mapping.DescriptionColumn))
        {
            throw new InvalidOperationException("Selecione as colunas de código e descrição.");
        }

        foreach (var row in rows)
        {
            var code = Get(row, mapping.CodeColumn);
            var description = Get(row, mapping.DescriptionColumn);
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
            {
                skipped++;
                continue;
            }

            var product = await _db.Products.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (product == null)
            {
                product = new Product { Code = code };
                _db.Products.Add(product);
                created++;
            }
            else
            {
                updated++;
            }

            product.Description = description;
            product.CurrentStock = ReadDecimal(row, mapping.CurrentStockColumn, product.CurrentStock);
            product.MinimumStock = ReadDecimal(row, mapping.MinimumStockColumn, product.MinimumStock);
            product.SaleValue = ReadDecimal(row, mapping.SaleValueColumn, product.SaleValue);

            var image = Get(row, mapping.ImagePathColumn);
            if (!string.IsNullOrWhiteSpace(image))
            {
                product.ImagePath = image.StartsWith("/", StringComparison.Ordinal) ? image : product.ImagePath;
                product.LegacyImageUrl = image.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? image : product.LegacyImageUrl;
            }

            product.CustomFieldsJson = JsonSerializer.Serialize(ReadCustomFields(row, mapping.CustomColumns));
            product.Catalyst = string.Empty;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (skipped > 0)
        {
            warnings.Add($"{skipped} linha(s) ignorada(s) por falta de código ou descrição.");
        }

        return new CatalogImportResultDto
        {
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Warnings = warnings
        };
    }

    private async Task<IReadOnlyList<SpreadsheetRow>> ReadRowsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Envie um arquivo com conteúdo.");
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" => _xlsxReader.ReadRows(bytes),
            ".csv" => ReadDelimitedRows(Encoding.UTF8.GetString(bytes)),
            ".pdf" => ReadDelimitedRows(ExtractPdfText(bytes)),
            _ => throw new InvalidOperationException("Use XLSX, CSV ou PDF com texto pesquisável.")
        };
    }

    private static IReadOnlyList<SpreadsheetRow> ReadDelimitedRows(string text)
    {
        var lines = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
        {
            return Array.Empty<SpreadsheetRow>();
        }

        var delimiter = DetectDelimiter(lines[0]);
        var headers = SplitLine(lines[0], delimiter);
        return lines
            .Skip(1)
            .Select(line => SplitLine(line, delimiter))
            .Where(values => values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Select(values => ToRow(headers, values))
            .ToList();
    }

    private static SpreadsheetRow ToRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index].Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            row[SpreadsheetRow.Normalize(header)] = index < values.Count ? values[index].Trim() : string.Empty;
        }

        return new SpreadsheetRow(row);
    }

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { ';', ',', '\t', '|' };
        return candidates
            .OrderByDescending(candidate => header.Count(ch => ch == candidate))
            .First();
    }

    private static IReadOnlyList<string> SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter).Select(value => value.Trim().Trim('"')).ToList();
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var matches = Regex.Matches(raw, @"\((?<text>(?:\\.|[^\\)])*)\)");
        return string.Join("\n", matches
            .Select(match => Regex.Unescape(match.Groups["text"].Value))
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string Get(SpreadsheetRow row, string column)
    {
        return string.IsNullOrWhiteSpace(column) ? string.Empty : row.Get(column);
    }

    private static decimal ReadDecimal(SpreadsheetRow row, string column, decimal fallback)
    {
        var value = Get(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        value = value.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(value, NumberStyles.Number, new CultureInfo("pt-BR"), out var ptValue))
        {
            return ptValue;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : fallback;
    }

    private static IReadOnlyList<ProductFieldDto> ReadCustomFields(SpreadsheetRow row, IEnumerable<string> columns)
    {
        return columns
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(column => new ProductFieldDto { Name = column.Trim(), Value = Get(row, column) })
            .Where(field => !string.IsNullOrWhiteSpace(field.Name) && !string.IsNullOrWhiteSpace(field.Value))
            .Take(24)
            .ToList();
    }
}
