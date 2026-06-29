using Canvas.Core.Contracts;
using Canvas.Core.Primitives;

namespace Canvas.Infrastructure.Spreadsheet;

/// <summary>Structural + version validation for a <see cref="SpreadsheetDto"/> (Canvas Workbook JSON).</summary>
public sealed class SpreadsheetValidator
{
    private static readonly string[] CellTypes = ["number", "text", "boolean", "date", "formula", "empty"];

    public SpreadsheetValidationResult Validate(SpreadsheetDto workbook)
    {
        var issues = new List<SpreadsheetValidationIssue>();
        var version = string.IsNullOrWhiteSpace(workbook.SchemaVersion) ? SpreadsheetDto.CurrentSchemaVersion : workbook.SchemaVersion;

        if (MajorOf(version) > MajorOf(SpreadsheetDto.CurrentSchemaVersion))
            issues.Add(new("error", "schemaVersion",
                $"Workbook schemaVersion '{version}' is newer than supported '{SpreadsheetDto.CurrentSchemaVersion}'; some fields may not be understood."));

        if (workbook.Sheets.Count == 0)
            issues.Add(new("warning", "sheets", "Workbook has no sheets."));

        for (var si = 0; si < workbook.Sheets.Count; si++)
        {
            var sheet = workbook.Sheets[si];
            var path = $"sheets[{si}]";
            if (string.IsNullOrWhiteSpace(sheet.Name))
                issues.Add(new("warning", path, "Sheet has no name."));

            foreach (var cell in sheet.Cells)
            {
                var cp = $"{path}.cell[{cell.Row},{cell.Col}]";
                if (cell.Row < 0 || cell.Col < 0)
                    issues.Add(new("error", cp, "Cell has a negative row/col."));
                if (!CellTypes.Contains(cell.Type))
                    issues.Add(new("error", cp, $"Unknown cell type '{cell.Type}'."));
                if (cell.Type == "formula" && (cell.Formula is null || !cell.Formula.StartsWith('=')))
                    issues.Add(new("error", cp, "Formula cell must have a Formula starting with '='."));
            }

            foreach (var m in sheet.Merges)
                if (!IsRange(m))
                    issues.Add(new("error", $"{path}.merges", $"Merge '{m}' is not a valid A1 range."));
        }

        return new SpreadsheetValidationResult(
            Valid: issues.All(i => i.Severity != "error"),
            Version: version,
            SupportedVersion: SpreadsheetDto.CurrentSchemaVersion,
            Issues: issues);
    }

    private static int MajorOf(string v) =>
        int.TryParse((v ?? "").Split('.')[0], out var m) ? m : 0;

    private static bool IsRange(string a1)
    {
        if (string.IsNullOrWhiteSpace(a1)) return false;
        try
        {
            foreach (var part in a1.Split(':')) A1Reference.Parse(part);
            return true;
        }
        catch { return false; }
    }
}

public sealed record SpreadsheetValidationIssue(string Severity, string Path, string Message);

public sealed record SpreadsheetValidationResult(
    bool Valid, string Version, string SupportedVersion, IReadOnlyList<SpreadsheetValidationIssue> Issues);
