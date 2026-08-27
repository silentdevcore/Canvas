using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Migration.Pdf.Code.Nutrient;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/migrations/pdf/code/nutrient")]
[Authorize(Policy = PxaDesignerPermissions.MigrationPdfExecute)]
[Authorize(Policy = "designer.migration.pdf.provider.nutrient.execute")]
[EnableRateLimiting("designer-code")]
public sealed class NutrientCodeMigrationController : ControllerBase
{
    [HttpGet("examples/comprehensive")]
    public ActionResult<NutrientCodeComprehensiveExample> GetComprehensiveExample() => Ok(
        new NutrientCodeComprehensiveExample(NutrientCodeExamples.ComprehensiveGdPictureCSharp));
}

public sealed record NutrientCodeComprehensiveExample(string GdPictureCSharp);
