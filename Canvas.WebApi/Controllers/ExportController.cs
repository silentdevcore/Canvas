using Canvas.Application.UseCases;
using Canvas.Core.Contracts;
using Microsoft.AspNetCore.Mvc;
using ExportOptions = Canvas.Core.Contracts.ExportOptions;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly ExportDocumentUseCase _useCase;

    public ExportController(ExportDocumentUseCase useCase)
    {
        _useCase = useCase;
    }

    /// <summary>
    /// Exports a design to the specified format and returns the file.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(415)]
    public IActionResult Export(
        [FromQuery] string format,
        [FromQuery] float? dpi,
        [FromQuery] int? quality,
        [FromBody] DesignExportDto design)
    {
        if (string.IsNullOrWhiteSpace(format))
            return BadRequest(new { error = "Query parameter 'format' is required." });

        if (design is null)
            return BadRequest(new { error = "Request body is required." });

        var options = (dpi.HasValue || quality.HasValue) ? new ExportOptions(dpi, quality) : null;

        try
        {
            var result = _useCase.Execute(new ExportDocumentRequest(design, format, options));
            return File(result.Data, result.MimeType, result.FileName);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(415, new
            {
                error = ex.Message,
                supportedFormats = _useCase.GetSupportedFormats()
                    .Select(f => f.Key)
                    .Order()
                    .ToList()
            });
        }
    }

    /// <summary>
    /// Returns a list of all supported export formats.
    /// </summary>
    [HttpGet("formats")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public IActionResult GetFormats()
    {
        var formats = _useCase.GetSupportedFormats()
            .OrderBy(f => f.Key)
            .Select(f => new
            {
                key               = f.Key,
                mimeType          = f.MimeType,
                extension         = f.Extension,
                supportsMultiPage  = f.Capabilities.SupportsMultiPage,
                supportsImages     = f.Capabilities.SupportsImages,
                supportsRichText   = f.Capabilities.SupportsRichText,
                supportsFormFields = f.Capabilities.SupportsFormFields,
            });

        return Ok(formats);
    }
}
