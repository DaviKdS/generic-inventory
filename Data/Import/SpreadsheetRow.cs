using System.Globalization;
using System.Text;

namespace GenericInventory.Data.Import;

public sealed class SpreadsheetRow
{
    private readonly Dictionary<string, string> _values;

    public SpreadsheetRow(Dictionary<string, string> values)
    {
        _values = values;
    }

    public string Get(params string[] headers)
    {
        foreach (var header in headers)
        {
            if (_values.TryGetValue(Normalize(header), out var value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    public static string Normalize(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }
}
