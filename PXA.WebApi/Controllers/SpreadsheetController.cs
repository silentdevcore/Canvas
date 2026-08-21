using PXA.Pdf;
using PXA.Application.UseCases;
using PXA.Infrastructure.Spreadsheet;
using PXA.WebApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using PXA.Core.Primitives;
using PxaDesignExportDto = PXA.Core.Contracts.DesignExportDto;
using PxaSpreadsheetDto = PXA.Core.Contracts.SpreadsheetDto;

namespace PXA.WebApi.Controllers;

/// <summary>
/// Spreadsheet Editor SDK endpoints: round-trips a PXA spreadsheet workbook to/from
/// <c>.xlsx</c> (preserving formulas, typed values, styles, merges).
/// </summary>
[ApiController]
[Route("api/spreadsheet")]
[Route("api/pxa/spreadsheet")]
public class SpreadsheetController : ControllerBase
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ExcelWorkbookExporter _exporter;
    private readonly ExcelWorkbookImporter _importer;
    private readonly SpreadsheetToDesignConverter _toDesign;
    private readonly SpreadsheetCalculator _calculator;
    private readonly SpreadsheetOperations _ops;
    private readonly ExportDocumentUseCase _export;
    private readonly XlsWorkbookIo _xls;
    private readonly SpreadsheetData _data;
    private readonly SpreadsheetValidator _validator;
    private readonly PdfFontLoader? _fontLoader;

    public SpreadsheetController(ExcelWorkbookExporter exporter, ExcelWorkbookImporter importer, SpreadsheetToDesignConverter toDesign, SpreadsheetCalculator calculator, SpreadsheetOperations ops, ExportDocumentUseCase export, XlsWorkbookIo xls, SpreadsheetData data, SpreadsheetValidator validator, PdfFontLoader? fontLoader = null)
    {
        _exporter = exporter;
        _importer = importer;
        _toDesign = toDesign;
        _calculator = calculator;
        _ops = ops;
        _export = export;
        _xls = xls;
        _data = data;
        _validator = validator;
        _fontLoader = fontLoader;
    }

    /// <summary>Exports a workbook to an <c>.xlsx</c> file (real A1 formulas + typed values). Pass
    /// <c>recalculate=true</c> to evaluate formulas server-side so the file carries fresh cached values.</summary>
    [HttpPost("export")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Export([FromBody] PxaSpreadsheetDto workbook, [FromQuery] bool recalculate = false, [FromQuery] string format = "xlsx")
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });

        var name = ExportFileNameSanitizer.Sanitize(workbook.Name, "workbook");
        return format.ToLowerInvariant() switch
        {
            "xls" => File(_xls.Export(workbook), "application/vnd.ms-excel", $"{name}.xls"),
            "csv" => File(System.Text.Encoding.UTF8.GetBytes(workbook.Sheets.Count > 0 ? CsvSheetIo.ToCsv(workbook.Sheets[0]) : ""), "text/csv", $"{name}.csv"),
            "tsv" => File(System.Text.Encoding.UTF8.GetBytes(workbook.Sheets.Count > 0 ? CsvSheetIo.ToCsv(workbook.Sheets[0], '\t') : ""), "text/tab-separated-values", $"{name}.tsv"),
            "xlsx" => File(_exporter.Export(workbook, recalculate), XlsxMime, $"{name}.xlsx"),
            _ => StatusCode(StatusCodes.Status415UnsupportedMediaType, new
            {
                error = $"Spreadsheet export format '{format}' is not supported.",
                supportedFormats = new[] { "xlsx", "xls", "csv", "tsv" },
            }),
        };
    }

    /// <summary>Recalculates all formulas server-side (ClosedXML) and returns the workbook with each formula
    /// cell's computed value filled in. The authoritative engine for headless/API callers.</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PxaSpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult Calculate([FromBody] PxaSpreadsheetDto workbook)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });
        return Ok(_calculator.Calculate(workbook));
    }

    /// <summary>Imports an uploaded <c>.xlsx</c> file into a workbook model.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(PxaSpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A spreadsheet file (.xlsx, .xls, .csv, or .tsv) is required." });

        using var ms = new MemoryStream();
        await using (var stream = file.OpenReadStream())
            await stream.CopyToAsync(ms);
        ms.Position = 0;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        try
        {
            PxaSpreadsheetDto workbook = ext switch
            {
                ".xls" => _xls.Import(ms, file.FileName),
                ".csv" or ".tsv" => new PxaSpreadsheetDto
                {
                    Id = Guid.NewGuid().ToString("n"),
                    Name = Path.GetFileNameWithoutExtension(file.FileName),
                    Sheets = [CsvSheetIo.FromCsv(new StreamReader(ms).ReadToEnd(), "Sheet1", ext == ".tsv" ? '\t' : ',')],
                },
                _ => _importer.Import(ms, file.FileName), // .xlsx (default)
            };
            return Ok(workbook);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not read the spreadsheet: {ex.Message}" });
        }
    }

    /// <summary>Renders a worksheet to a document: <c>pdf</c> (PXA.Pdf), or <c>html</c>/<c>png</c>/<c>jpeg</c>
    /// via the standard exporters. The sheet is mapped to a gridlined table.</summary>
    [HttpPost("render")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(415)]
    public IActionResult Render([FromBody] PxaSpreadsheetDto workbook, [FromQuery] string format = "pdf", [FromQuery] int sheet = 0)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });

        var design = _toDesign.Convert(workbook, sheet, gridlines: true);
        var name = ExportFileNameSanitizer.Sanitize(workbook.Name, "workbook");

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var doc = DesignJsonMapper.MapToPdfDocument(design, _fontLoader, null);
            var bytes = doc.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
            return File(bytes, "application/pdf", $"{name}.pdf");
        }

        try
        {
            var result = _export.Execute(new ExportDocumentRequest(design, format, null));
            return File(result.Data, result.MimeType, result.FileName);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(415, new { error = ex.Message });
        }
    }

    /// <summary>Converts a worksheet to a PXA design (a <c>table</c> element) so it can be embedded in a
    /// PDF/Word/HTML document via the standard exporters.</summary>
    [HttpPost("to-design")]
    [ProducesResponseType(typeof(PxaDesignExportDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult ToDesign([FromBody] PxaSpreadsheetDto workbook, [FromQuery] int sheet = 0)
    {
        if (workbook is null)
            return BadRequest(new { error = "Request body is required." });
        return Ok(_toDesign.Convert(workbook, sheet));
    }

    /// <summary>Sorts a worksheet range by a key column (0-based offset within the range).</summary>
    [HttpPost("sort")]
    [ProducesResponseType(typeof(PxaSpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult Sort([FromBody] PxaSpreadsheetDto workbook, [FromQuery] int sheet = 0, [FromQuery] string range = "", [FromQuery] int keyColumn = 0, [FromQuery] bool ascending = true)
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
    public IActionResult FindReplace([FromBody] PxaSpreadsheetDto workbook, [FromQuery] string find = "", [FromQuery] string replace = "", [FromQuery] bool matchCase = false)
    {
        if (workbook is null || string.IsNullOrEmpty(find)) return BadRequest(new { error = "Body + 'find' are required." });
        var count = _ops.FindReplace(workbook, find, replace, matchCase);
        return Ok(new { workbook, count });
    }

    /// <summary>Validates a workbook (PXA Workbook JSON): structural + schemaVersion checks. Returns
    /// <c>{ valid, version, supportedVersion, issues[] }</c>.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Validate([FromBody] PxaSpreadsheetDto workbook)
    {
        if (workbook is null) return BadRequest(new { error = "Request body is required." });
        return Ok(_validator.Validate(workbook));
    }

    /// <summary>Builds a workbook from JSON row objects — a bold header row (union of keys) + one typed row
    /// per object. The DataTable equivalent for API callers.</summary>
    [HttpPost("from-data")]
    [ProducesResponseType(typeof(PxaSpreadsheetDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult FromData([FromBody] List<Dictionary<string, System.Text.Json.JsonElement>>? rows, [FromQuery] string sheetName = "Sheet1")
    {
        if (rows is null) return BadRequest(new { error = "A JSON array of row objects is required." });
        var sheet = _data.FromRows(rows, sheetName);
        return Ok(new PxaSpreadsheetDto { Id = Guid.NewGuid().ToString("n"), Name = sheetName, Sheets = [sheet] });
    }

    /// <summary>Fills a template workbook's <c>{{token}}</c> placeholders from a data object; returns the
    /// workbook + the number of cells changed.</summary>
    [HttpPost("fill")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Fill([FromBody] FillRequest? request)
    {
        if (request?.Workbook is null) return BadRequest(new { error = "Body with 'workbook' is required." });
        var count = _data.Fill(request.Workbook, request.Data ?? []);
        return Ok(new { workbook = request.Workbook, count });
    }

    public sealed class FillRequest
    {
        public PxaSpreadsheetDto? Workbook { get; set; }
        public Dictionary<string, System.Text.Json.JsonElement>? Data { get; set; }
    }

}
