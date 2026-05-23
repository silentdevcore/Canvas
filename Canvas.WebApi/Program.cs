using Canvas.Application.UseCases;
using Canvas.Core.Abstractions;
using Canvas.Core.Primitives;
using Canvas.Domain.Repositories;
using Canvas.Infrastructure.Converters;
using Canvas.Infrastructure.Pdf;
using Canvas.Infrastructure.Sheet;
using Canvas.Infrastructure.Word;
using Canvas.Pdf;
using Canvas.WebApi.Infrastructure;
using Canvas.WebApi.Middleware;

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
                  "http://localhost:3000",
                  "http://localhost:4173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Register template rendering services
builder.Services.AddScoped<ITemplateRepository, InMemoryTemplateRepository>();
builder.Services.AddScoped<ITemplateExpander, TemplateExpander>();
builder.Services.AddScoped<IExpressionEvaluator, ExpressionEvaluator>();
builder.Services.AddScoped<IValueFormatter, ValueFormatter>();
builder.Services.AddScoped<IRepeatExpander, RepeatExpander>();

// Register PDF infrastructure
builder.Services.AddScoped<IDocumentRenderer, PdfDocumentRenderer>();
builder.Services.AddScoped<IOutputWriter, FileOutputWriter>();

// Register export format exporters
builder.Services.AddScoped<IDocumentExporter, HtmlDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, XmlDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, SvgDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, CsvDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, MarkdownDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, ImageDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, JpegDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, TiffDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, OdtDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, WordDocumentExporter>();
builder.Services.AddScoped<IDocumentExporter, ExcelDocumentExporter>();

// Register use cases
builder.Services.AddScoped<ExportDocumentUseCase>();
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
