using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PVAD.Migration.Pdf.Code.Nutrient;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Route("api/pvad/v1/migrations/pdf/code/nutrient")]
[Authorize(Policy = PvadDesignerPermissions.MigrationPdfExecute)]
[Authorize(Policy = "designer.migration.pdf.provider.nutrient.execute")]
[EnableRateLimiting("designer-code")]
public sealed class NutrientCodeMigrationController : ControllerBase
{
    [HttpGet("examples/comprehensive")]
    public ActionResult<NutrientCodeComprehensiveExample> GetComprehensiveExample() => Ok(
        new NutrientCodeComprehensiveExample(NutrientCodeExamples.ComprehensiveGdPictureCSharp));
}

public sealed record NutrientCodeComprehensiveExample(string GdPictureCSharp);
