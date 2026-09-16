using ClosedXML.Excel;
using System.Text;

namespace IdvEnrichment.Functions.Shared;

public static class SpreadsheetExtractor
{
    // Public so callers can reject an oversized file before buffering it into memory, rather than
    // relying solely on the check below (which only guards the XLWorkbook parse, not the buffering
    // that happens upstream of it).
    public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const int ExtractionBudget = 24_000; // chars — generous for table-heavy content

    // Sheets matching these keywords get 2× budget share
    private static readonly string[] PriorityKeywords =
    [
        "summary", "sources", "uses", "returns", "overview", "assumptions",
        "development", "budget", "proforma", "pro forma", "cash flow",
        "investment", "sensitivity", "yield", "irr", "debt",
    ];

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

        // Render each sheet individually so we can prioritize
        var sheets = new List<(string Name, string Markdown, bool IsPriority)>();
        foreach (var worksheet in workbook.Worksheets)
        {
            var md = RenderSheet(worksheet);
            var isPriority = IsPrioritySheet(worksheet.Name);
            sheets.Add((worksheet.Name, md, isPriority));
        }

        var totalLength = sheets.Sum(s => s.Markdown.Length);
        if (totalLength <= ExtractionBudget)
        {
            return string.Concat(sheets.Select(s => s.Markdown));
        }

        return AssembleWithBudget(sheets);
    }

    private static string AssembleWithBudget(List<(string Name, string Markdown, bool IsPriority)> sheets)
    {
        // Priority sheets get 2 shares, others get 1
        var totalShares = sheets.Sum(s => s.IsPriority ? 2 : 1);
        var perShare = ExtractionBudget / totalShares;

        var sb = new StringBuilder();
        foreach (var (name, markdown, isPriority) in sheets)
        {
            var budget = perShare * (isPriority ? 2 : 1);
            if (markdown.Length <= budget)
            {
                sb.Append(markdown);
            }
            else
            {
                // Take header rows + as many data rows as fit within budget
                sb.Append(TruncateSheet(name, markdown, budget));
            }
        }

        return sb.ToString();
    }

    private static string TruncateSheet(string name, string markdown, int budget)
    {
        var lines = markdown.Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 1 > budget)
            {
                sb.AppendLine($"_[... {name}: truncated]_");
                sb.AppendLine();
                break;
            }
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static bool IsPrioritySheet(string name)
    {
        return PriorityKeywords.Any(kw =>
            name.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderSheet(IXLWorksheet worksheet)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Sheet: {worksheet.Name}");
        sb.AppendLine();

        var range = worksheet.RangeUsed();
        if (range is null)
        {
            sb.AppendLine("_(empty sheet)_");
            sb.AppendLine();
            return sb.ToString();
        }

        var rows = range.RowsUsed().ToList();
        if (rows.Count == 0)
        {
            sb.AppendLine("_(empty sheet)_");
            sb.AppendLine();
            return sb.ToString();
        }

        // Header row
        var headerCells = rows[0].Cells().Select(c => EscapeCell(SafeFormattedString(c))).ToList();
        sb.AppendLine("| " + string.Join(" | ", headerCells) + " |");
        sb.AppendLine("| " + string.Join(" | ", headerCells.Select(_ => "---")) + " |");

        // Data rows
        foreach (var row in rows.Skip(1))
        {
            var cells = row.Cells(1, range.ColumnCount()).Select(c => EscapeCell(SafeFormattedString(c)));
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string EscapeCell(string value) =>
        value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

    // Some .xlsm cells carry a date format applied to a numeric serial outside the representable
    // DateTime range (corrupt or misapplied formatting, not an actual date) -- GetFormattedString()
    // throws trying to convert that serial to a DateTime instead of just rendering the number.
    private static string SafeFormattedString(IXLCell cell)
    {
        try
        {
            return cell.GetFormattedString();
        }
        catch (ArgumentOutOfRangeException)
        {
            return "[unreadable value]";
        }
    }
}
