using Canvas.Core.Contracts;
using Canvas.Infrastructure.Sheet;
using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

/// <summary>
/// Spreadsheet Editor SDK endpoints: round-trips a <see cref="SpreadsheetDto"/> workbook to/from
/// <c>.xlsx</c> (preserving formulas, typed values, styles, merges).
/// </summary>
[ApiController]
[Route("api/spreadsheet")]
public class SpreadsheetController : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ExcelWorkbookExporter _exporter;
    private readonly ExcelWorkbookImporter _importer;

    public SpreadsheetController(ExcelWorkbookExporter exporter, ExcelWorkbookImporter importer)
    {
        _exporter = exporter;
        _importer = importer;
    }

    /// <summary>Exports a workbook to an <c>.xlsx</c> file (real A1 formulas + typed values).</summary>
    [HttpPost("export")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Export([FromBody] SpreadsheetDto workbook)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });

        var bytes = _exporter.Export(workbook);
        return File(bytes, XlsxMime, $"{SanitizeFileName(workbook.Name)}.xlsx");
    }

    /// <summary>Imports an uploaded <c>.xlsx</c> file into a workbook model.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(SpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An .xlsx file is required." });

        // ClosedXML needs a seekable stream — copy the upload into memory first.
        using var ms = new MemoryStream();
        await using (var stream = file.OpenReadStream())
            await stream.CopyToAsync(ms);
        ms.Position = 0;

        try
        {
            var workbook = _importer.Import(ms, file.FileName);
            return Ok(workbook);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not read the spreadsheet: {ex.Message}" });
        }
    }

    private static string SanitizeFileName(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "workbook" : name;
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return n.Trim();
    }
}
