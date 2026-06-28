using Canvas.Core.Contracts;
using Canvas.Infrastructure.Spreadsheet;
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
    private readonly SpreadsheetToDesignConverter _toDesign;
    private readonly SpreadsheetCalculator _calculator;
    private readonly SpreadsheetOperations _ops;

    public SpreadsheetController(ExcelWorkbookExporter exporter, ExcelWorkbookImporter importer, SpreadsheetToDesignConverter toDesign, SpreadsheetCalculator calculator, SpreadsheetOperations ops)
    {
        _exporter = exporter;
        _importer = importer;
        _toDesign = toDesign;
        _calculator = calculator;
        _ops = ops;
    }

    /// <summary>Exports a workbook to an <c>.xlsx</c> file (real A1 formulas + typed values). Pass
    /// <c>recalculate=true</c> to evaluate formulas server-side so the file carries fresh cached values.</summary>
    [HttpPost("export")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Export([FromBody] SpreadsheetDto workbook, [FromQuery] bool recalculate = false)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });

        var bytes = _exporter.Export(workbook, recalculate);
        return File(bytes, XlsxMime, $"{SanitizeFileName(workbook.Name)}.xlsx");
    }

    /// <summary>Recalculates all formulas server-side (ClosedXML) and returns the workbook with each formula
    /// cell's computed value filled in. The authoritative engine for headless/API callers.</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(SpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult Calculate([FromBody] SpreadsheetDto workbook)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });
        return Ok(_calculator.Calculate(workbook));
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

    /// <summary>Converts a worksheet to a Canvas design (a <c>table</c> element) so it can be embedded in a
    /// PDF/Word/HTML document via the standard exporters.</summary>
    [HttpPost("to-design")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult ToDesign([FromBody] SpreadsheetDto workbook, [FromQuery] int sheet = 0)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });
        return Ok(_toDesign.Convert(workbook, sheet));
    }

    /// <summary>Sorts a worksheet range by a key column (0-based offset within the range).</summary>
    [HttpPost("sort")]
    [ProducesResponseType(typeof(SpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult Sort([FromBody] SpreadsheetDto workbook, [FromQuery] int sheet = 0, [FromQuery] string range = "", [FromQuery] int keyColumn = 0, [FromQuery] bool ascending = true)
    {
        if (workbook is null || string.IsNullOrWhiteSpace(range)) return BadRequest(new { error = "Body + 'range' are required." });
        if (sheet < 0 || sheet >= workbook.Sheets.Count) return BadRequest(new { error = "Invalid sheet index." });
        _ops.SortRange(workbook.Sheets[sheet], range, keyColumn, ascending);
        return Ok(workbook);
    }

    /// <summary>Find &amp; replace across all text/formula cells; returns the workbook + the change count.</summary>
    [HttpPost("find-replace")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult FindReplace([FromBody] SpreadsheetDto workbook, [FromQuery] string find = "", [FromQuery] string replace = "", [FromQuery] bool matchCase = false)
    {
        if (workbook is null || string.IsNullOrEmpty(find)) return BadRequest(new { error = "Body + 'find' are required." });
        var count = _ops.FindReplace(workbook, find, replace, matchCase);
        return Ok(new { workbook, count });
    }

    private static string SanitizeFileName(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "workbook" : name;
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return n.Trim();
    }
}
