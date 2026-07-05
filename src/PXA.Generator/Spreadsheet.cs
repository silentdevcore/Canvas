using Canvas.Infrastructure.Spreadsheet;

namespace PXA.Generator;

/// <summary>
/// Additive Power Dox Automation facade for spreadsheet generation.
/// </summary>
public static class Spreadsheet
{
    /// <summary>
    /// Creates a workbook using the current Canvas spreadsheet implementation.
    /// </summary>
    public static CanvasWorkbook CreateWorkbook(string name = "Workbook") =>
        new(name);
}
