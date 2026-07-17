using PXA.FileImporter;
using PXA.FileImporter.ImageAnalysis;
using PXA.FileImporter.ImageOcr;
using PXA.Infrastructure.Persistence;
using PXA.Pdf;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Middleware;
using PxaConverters = PXA.Infrastructure.Converters;
using PxaSpreadsheet = PXA.Infrastructure.Spreadsheet;
using PxaWord = PXA.Infrastructure.Word;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddPxaPersistence(
    builder.Configuration.GetConnectionString("PxaDatabase")
        ?? throw new InvalidOperationException("Connection string 'PxaDatabase' is required."));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PxaDbContext>("pxa-database", tags: ["ready"]);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "http://localhost:5175",
                  "http://localhost:5176",
                  "http://localhost:5177",
                  "http://localhost:5178",
                  "http://localhost:3000",
                  "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Register template rendering services
builder.Services.AddScoped<PXA.Domain.Repositories.ITemplateRepository, InMemoryTemplateRepository>();

// Register font loader for multi-language PDF support (optional: gracefully absent if fonts dir missing)
builder.Services.AddSingleton<PdfFontLoader>(sp =>
{
    var fontsDir = builder.Configuration["Pdf:FontsDirectory"]
        ?? Path.Combine(AppContext.BaseDirectory, "fonts");
    return new PdfFontLoader(fontsDir);
});

// Register export format exporters
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.HtmlDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.XmlDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.SvgDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.CsvDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.MarkdownDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.ImageDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.JpegDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.TiffDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaConverters.OdtDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaWord.WordDocumentExporter>();
builder.Services.AddScoped<PXA.Core.Abstractions.IDocumentExporter, PxaSpreadsheet.ExcelDocumentExporter>();

// Spreadsheet Editor SDK: workbook (SpreadsheetDto) ⇄ .xlsx round-trip (distinct from the design exporters).
builder.Services.AddScoped<PxaSpreadsheet.ExcelWorkbookExporter>();
builder.Services.AddScoped<PxaSpreadsheet.ExcelWorkbookImporter>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetToDesignConverter>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetCalculator>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetOperations>();
builder.Services.AddScoped<PxaSpreadsheet.XlsWorkbookIo>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetData>();
builder.Services.AddScoped<PxaSpreadsheet.SpreadsheetValidator>();

// Register file importers
builder.Services.AddTransient<IFileImporter, PdfFileImporter>();
builder.Services.AddTransient<IFileImporter, DocxFileImporter>();
builder.Services.AddTransient<IFileImporter, PptxFileImporter>();
builder.Services.AddTransient<IFileImporter, DocFileImporter>();
builder.Services.AddTransient<IFileImporter, OdtFileImporter>();
builder.Services.AddTransient<IFileImporter, SvgFileImporter>();
builder.Services.AddTransient<IFileImporter, ImageFileImporter>();
builder.Services.AddTransient<ImageAnalysisFileImporter>();
builder.Services.AddSingleton<IOcrEngine>(sp =>
{
    var tessDataPath = builder.Configuration["Ocr:TessDataPath"];
    var nativeLibraryPath = builder.Configuration["Ocr:NativeLibraryPath"];
    var useIsolatedWorker = builder.Configuration.GetValue("Ocr:UseIsolatedWorker", true);
    if (!useIsolatedWorker)
        return new EmbeddedTesseractOcrEngine(tessDataPath, nativeLibraryPath);

    var workerPath = builder.Configuration["Ocr:WorkerPath"];
    return new ProcessIsolatedTesseractOcrEngine(workerPath, tessDataPath, nativeLibraryPath);
});
builder.Services.AddTransient<ImageToPdfConverter>();

// Register migration service
builder.Services.AddSingleton<PXA.WebApi.Services.MigrationService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerAnnotationStore>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerAnnotationFlatteningService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerNativeAnnotationExtractionService>();
builder.Services.AddSingleton<PXA.WebApi.Services.PdfViewerFormExtractionService>();

// Register use cases
builder.Services.AddScoped<PXA.Application.UseCases.ExportDocumentUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.CreateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.UpdateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.GetTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.ValidateTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.AuthenticateUserUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.FindAndReplaceUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.CloneTemplateUseCase>();
builder.Services.AddScoped<PXA.Application.UseCases.ExtractPagesUseCase>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
// app.UseAuthenticationMiddleware();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();

public partial class Program {}
