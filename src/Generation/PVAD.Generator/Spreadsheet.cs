using PVAD.Infrastructure.Spreadsheet;

namespace PVAD.Generator;

/// <summary>
/// Additive PVADE facade for spreadsheet generation.
/// </summary>
public static class Spreadsheet
{
    /// <summary>
    /// Creates a workbook using the current PVAD spreadsheet implementation.
    /// </summary>
    public static PvadWorkbook CreateWorkbook(string name = "Workbook") =>
        new(name);
}
