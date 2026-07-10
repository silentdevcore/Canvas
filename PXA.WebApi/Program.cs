using Canvas.Application.UseCases;
using Canvas.Core.Abstractions;
using Canvas.FileImporter.Abstractions;
using Canvas.FileImporter.Doc;
using Canvas.FileImporter.Docx;
using Canvas.FileImporter.Image;
using Canvas.FileImporter.Odt;
using Canvas.FileImporter.Pdf;
using Canvas.FileImporter.Pptx;
using Canvas.FileImporter.Svg;
using Canvas.FileImporter.ImageAnalysis;
using Canvas.FileImporter.ImageOcr;
using Canvas.Core.Primitives;
using Canvas.Domain.Repositories;
using Canvas.Infrastructure.Pdf;
using Canvas.Pdf;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Middleware;
using PxaConverters = PXA.Infrastructure.Converters;
using PxaSpreadsheet = PXA.Infrastructure.Spreadsheet;
using PxaWord = PXA.Infrastructure.Word;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
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
builder.Services.AddScoped<ITemplateRepository, InMemoryTemplateRepository>();
builder.Services.AddScoped<IExpressionEvaluator, ExpressionEvaluator>();
builder.Services.AddScoped<IValueFormatter, ValueFormatter>();

// Register PDF infrastructure
builder.Services.AddScoped<IDocumentRenderer, PdfDocumentRenderer>();
builder.Services.AddScoped<IOutputWriter, FileOutputWriter>();

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
builder.Services.AddScoped<RenderTemplateUseCase>();
builder.Services.AddScoped<CreateTemplateUseCase>();
builder.Services.AddScoped<UpdateTemplateUseCase>();
builder.Services.AddScoped<GetTemplateUseCase>();
builder.Services.AddScoped<ValidateTemplateUseCase>();
builder.Services.AddScoped<AuthenticateUserUseCase>();
builder.Services.AddScoped<FindAndReplaceUseCase>();
builder.Services.AddScoped<CloneTemplateUseCase>();
builder.Services.AddScoped<ExtractPagesUseCase>();

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

app.Run();

public partial class Program {}
