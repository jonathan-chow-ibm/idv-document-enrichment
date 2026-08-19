using ClosedXML.Excel;
using System.Text;

namespace IdvEnrichment.Functions.Shared;

public static class SpreadsheetExtractor
{
    // Guard against pathologically large workbooks before loading into memory
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public static bool IsSpreadsheet(string fileName) =>
        Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(fileName).Equals(".xlsm", StringComparison.OrdinalIgnoreCase);

    public static bool IsUnsupportedSpreadsheet(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".xlsb", StringComparison.OrdinalIgnoreCase);
    }

    public static string ExtractToMarkdown(Stream stream, long contentLength = -1)
    {
        if (contentLength > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Workbook exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        using var workbook = new XLWorkbook(stream);
        var sb = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            sb.AppendLine($"## Sheet: {worksheet.Name}");
            sb.AppendLine();

            var range = worksheet.RangeUsed();
            if (range is null)
            {
                sb.AppendLine("_(empty sheet)_");
                sb.AppendLine();
                continue;
            }

            var rows = range.RowsUsed().ToList();
            if (rows.Count == 0)
            {
                sb.AppendLine("_(empty sheet)_");
                sb.AppendLine();
                continue;
            }

            // Header row
            var headerCells = rows[0].Cells().Select(c => EscapeCell(c.GetFormattedString())).ToList();
            sb.AppendLine("| " + string.Join(" | ", headerCells) + " |");
            sb.AppendLine("| " + string.Join(" | ", headerCells.Select(_ => "---")) + " |");

            // Data rows
            foreach (var row in rows.Skip(1))
            {
                var cells = row.Cells(1, range.ColumnCount()).Select(c => EscapeCell(c.GetFormattedString()));
                sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCell(string value) =>
        value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
