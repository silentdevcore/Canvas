using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PXA.CodeWorker;
using PXA.Core.Contracts;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Controllers;
using System.Text.Json;

namespace PXA.Api.Tests;

public sealed class CodeWorkspaceConversionTests
{
    [Fact]
    public void ProductionContainer_PackagesWorkerAndAppliesBaselineResourceIsolation()
    {
        var root = FindRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(root, "PXA.WebApi", "Dockerfile"));
        var compose = File.ReadAllText(Path.Combine(root, "deploy", "api", "docker-compose.api.yml"));

        Assert.Contains("src/Execution/PXA.CodeWorker/PXA.CodeWorker.csproj", dockerfile);
        Assert.Contains("/app/publish/code-worker", dockerfile);
        Assert.Contains("USER $APP_UID", dockerfile);
        Assert.Contains("CodeWorker__Hardened=false", dockerfile);
        Assert.Contains("read_only: true", compose);
        Assert.Contains("/app/tmp:rw,noexec,nosuid,nodev", compose);
        Assert.Contains("no-new-privileges:true", compose);
        Assert.Contains("cap_drop:", compose);
        Assert.Contains("pids:", compose);
        Assert.Contains("cpus:", compose);
        Assert.Contains("memory:", compose);
    }

    [Fact]
    public async Task WorkerClient_ExecutesThePackagedFrameworkDependentDll()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var client = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions
            {
                Enabled = true,
                WorkerPath = workerDirectory,
            }),
            NullLogger<PxaCodeWorkerClient>.Instance);

        var response = await client.RunAsync(new PxaCodeWorkerRequest
        {
            Language = PxaCodeLanguages.CSharpPdf,
            Operation = "execute",
            Source = """
                var document = new PdfDocument();
                var page = document.AddPage();
                page.DrawText("Portable worker", 40, 800, 18);
                return document;
                """,
        }, default);

        Assert.True(response.Success);
        Assert.NotEmpty(response.PdfBytes!);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PXA.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
    }

    [Fact]
    public async Task WorkerProcess_ExecutesSafePdfAndReturnsCanonicalSourceMap()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-worker-test-").FullName;
        try
        {
            var input = Path.Combine(directory, "input.json");
            var output = Path.Combine(directory, "output.json");
            var request = new PxaCodeWorkerRequest
            {
                Language = PxaCodeLanguages.CSharpPdf,
                Operation = "execute",
                Source = """
                    var document = new PdfDocument();
                    var page = document.AddPage();
                    // pxa-element-id: greeting
                    page.DrawText("Hello", 40, 800, 18);
                    return document;
                    """,
            };
            await File.WriteAllTextAsync(input, System.Text.Json.JsonSerializer.Serialize(request));
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{typeof(SandboxPolicy).Assembly.Location}\" \"{input}\" \"{output}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
            })!;
            await process.WaitForExitAsync();
            var response = System.Text.Json.JsonSerializer.Deserialize<PxaCodeWorkerResponse>(
                await File.ReadAllTextAsync(output), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.Equal(0, process.ExitCode);
            Assert.True(response!.Success);
            Assert.NotEmpty(response.PdfBytes!);
            Assert.Equal("greeting", response.CanonicalDesign!.Pages[0].Elements[0].Id);
            Assert.Contains(response.SourceMap, value => value.ElementId == "greeting" && value.StartLine == 3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task JsonToModel_IsDeterministicAndKeepsCanonicalIds()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """
            {"id":"design-1","name":"Invoice","pages":[{"id":"page-1","elements":[{"id":"title","type":"text","x":10,"y":20,"width":100,"height":20,"content":"Hello"}]}]}
            """;

        var first = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpModel, source, default);
        var second = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpModel, source, default);

        Assert.Equal(PxaCodeFidelity.Exact, first.Fidelity);
        Assert.Equal(first.GeneratedSource, second.GeneratedSource);
        Assert.Equal(first.ResultChecksum, second.ResultChecksum);
        Assert.Equal("title", first.CanonicalDesign!.Pages[0].Elements[0].Id);
        Assert.Contains("new DesignExportDto", first.GeneratedSource);
        Assert.Contains("new PageDto", first.GeneratedSource);
        Assert.Contains("new ElementDto", first.GeneratedSource);
        Assert.DoesNotContain("FromBase64String", first.GeneratedSource);
    }

    [Fact]
    public async Task JsonRoundtrip_PreservesUnknownExtensionProperties()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """
            {"id":"design-1","vendorDocumentFlag":true,"pages":[{"id":"page-1","vendorPageValue":7,"elements":[{"id":"title","type":"text","x":10,"y":20,"width":100,"height":20,"vendorElementValue":"kept"}]}]}
            """;

        var result = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.Json, source, default);

        Assert.True(result.CanonicalDesign!.Extensions!["vendorDocumentFlag"].GetBoolean());
        Assert.Equal(7, result.CanonicalDesign.Pages[0].Extensions!["vendorPageValue"].GetInt32());
        Assert.Equal("kept", result.CanonicalDesign.Pages[0].Elements[0].Extensions!["vendorElementValue"].GetString());
        Assert.Contains("vendorElementValue", result.GeneratedSource);
    }

    [Fact]
    public void PersistenceAdapter_PreservesDesignerEnvelopeAndPageExtensions()
    {
        const string stored = """
            {"template":{"id":"design-1","name":"Invoice","pages":[{"id":"page-1","elements":[]}]},"pageSettings":{"width":600,"height":800,"gridVisible":true},"jsonData":{"customer":"Ada"},"documentMode":"pdf","currentPageIndex":0}
            """;

        var canonical = DesignerCodeWorkspacesController.CanonicalFromStoredDesign(stored);
        var design = System.Text.Json.JsonSerializer.Deserialize<DesignExportDto>(canonical, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        design.Name = "Updated invoice";
        var reapplied = DesignerCodeWorkspacesController.ApplyCanonicalToStoredDesign(stored, design);
        using var document = System.Text.Json.JsonDocument.Parse(reapplied);

        Assert.Equal("Updated invoice", document.RootElement.GetProperty("template").GetProperty("name").GetString());
        Assert.True(document.RootElement.GetProperty("pageSettings").GetProperty("gridVisible").GetBoolean());
        Assert.Equal("Ada", document.RootElement.GetProperty("jsonData").GetProperty("customer").GetString());
    }

    [Fact]
    public async Task JsonToPdfCode_PreservesAllElementsThroughTheSemanticBuilder()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """
            {"pages":[{"id":"page-1","elements":[{"id":"known","type":"text","x":10,"y":20,"width":100,"height":20,"content":"Hello"},{"id":"chart-1","type":"chart","x":10,"y":60,"width":200,"height":100}]}]}
            """;

        var result = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpPdf, source, default);

        Assert.Contains("pxa-element-id: known", result.GeneratedSource);
        Assert.Contains("pxa-element-id: chart-1", result.GeneratedSource);
        Assert.Contains("new PxaPdfCodeBuilder", result.GeneratedSource);
        Assert.DoesNotContain(result.Diagnostics, value => value.Code == "PXACODE110");
        Assert.Contains(result.SourceMap, value => value.ElementId == "known");
        Assert.Contains(result.SourceMap, value => value.ElementId == "chart-1");
    }

    [Fact]
    public async Task JsonToPdfCode_GeneratesEditableRichTextWithoutUnsupportedDiagnostic()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """
            {"pages":[{"id":"page-1","elements":[{"id":"rich-1","type":"richtext","x":10,"y":20,"width":220,"height":80,"htmlContent":"<p>Hello <strong>PXA</strong></p>","style":{"fontSize":14,"lineHeight":19,"color":"#123456"}}]}]}
            """;

        var result = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpPdf, source, default);

        Assert.Contains("pxa-element-id: rich-1", result.GeneratedSource);
        Assert.Contains("new PxaPdfCodeBuilder", result.GeneratedSource);
        Assert.Contains("HtmlContent", result.GeneratedSource);
        Assert.Contains("PXA", result.GeneratedSource);
        Assert.DoesNotContain(result.Diagnostics, value => value.Code == "PXACODE110" && value.ElementId == "rich-1");
        Assert.Contains(result.SourceMap, value => value.ElementId == "rich-1");
    }

    [Fact]
    public async Task GeneratedRichTextPdfCode_ExecutesInPackagedWorker()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var worker = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory }),
            NullLogger<PxaCodeWorkerClient>.Instance);
        var service = new PxaCodeConversionService(worker);
        const string source = """
            {"pages":[{"id":"page-1","elements":[{"id":"rich-1","type":"richtext","x":10,"y":20,"width":220,"height":80,"htmlContent":"<p>Hello <strong>PXA</strong> <a href=\"https://example.com\">Docs</a></p>","style":{"fontSize":14,"color":"#123456"}}]}]}
            """;

        var conversion = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpPdf, source, default);
        var execution = await service.ExecuteAsync(PxaCodeLanguages.CSharpPdf, conversion.GeneratedSource, default);

        Assert.True(execution.Success, string.Join(Environment.NewLine, execution.Diagnostics.Select(value => $"{value.Code}: {value.Message}")));
        Assert.NotEmpty(execution.PdfBytes!);
        Assert.DoesNotContain(execution.Diagnostics, value => value.Severity == "error");
    }

    [Fact]
    public async Task GeneratedRepresentations_CompleteTheTwelveDirectedRoundtrips()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var service = new PxaCodeConversionService(new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory }),
            NullLogger<PxaCodeWorkerClient>.Instance));
        const string source = """
            {
              "id":"roundtrip","name":"Roundtrip","vendorDocumentFlag":true,
              "pageSettings":{"width":612,"height":792,"activeLanguages":["en","ar"],"targetLanguage":"ar","encryption":{"enabled":true,"algorithm":"Aes128","permissions":{"print":true,"copy":false}}},
              "pages":[{"id":"page-1","elements":[
                {"id":"rich-1","type":"richtext","x":10,"y":20,"width":220,"height":80,"htmlContent":"<p>Hello <strong>PXA</strong></p>","language":"ar","textDirection":"rtl","binding":"customer.name","vendorElementValue":"kept"},
                {"id":"table-1","type":"table","x":10,"y":120,"width":300,"height":100,"cellData":[["A","B"],["1","2"]],"columnWidths":[120,180]},
                {"id":"chart-1","type":"chart","x":10,"y":240,"width":300,"height":180,"chartType":"bar","chartData":{"labels":["A","B"],"values":[3,7]}}
              ]}]
            }
            """;

        var canonical = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.Json, source, default);
        var normalized = JsonSerializer.Serialize(canonical.CanonicalDesign, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        var expected = CanonicalJson(canonical.CanonicalDesign!);
        var representations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PxaCodeLanguages.Json] = normalized,
        };
        foreach (var target in PxaCodeLanguages.Supported.Where(value => value != PxaCodeLanguages.Json))
        {
            var generated = await service.ConvertAsync(PxaCodeLanguages.Json, target, normalized, default);
            Assert.DoesNotContain(generated.Diagnostics, value => value.Severity == "error");
            representations[target] = generated.GeneratedSource;
        }

        foreach (var sourceRepresentation in representations)
        foreach (var target in PxaCodeLanguages.Supported.Where(value => value != sourceRepresentation.Key))
        {
            var result = await service.ConvertAsync(sourceRepresentation.Key, target, sourceRepresentation.Value, default);
            Assert.True(!result.Diagnostics.Any(value => value.Severity == "error"),
                $"{sourceRepresentation.Key} -> {target}: {string.Join(" | ", result.Diagnostics.Select(value => $"{value.Code}: {value.Message}"))}");
            Assert.Equal(PxaCodeFidelity.Exact, result.DocumentFidelity);
            Assert.Equal(expected, CanonicalJson(result.CanonicalDesign!));
        }
    }

    [Fact]
    public async Task Base64Representation_IsSeparateFromTheReadableModelAndUsesStrictUtf8()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """{"id":"design-1","pages":[{"id":"page-1","elements":[]}]}""";

        var model = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpModel, source, default);
        var base64 = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.CSharpBase64, source, default);

        Assert.DoesNotContain("FromBase64String", model.GeneratedSource);
        Assert.Contains("new DesignExportDto", model.GeneratedSource);
        Assert.Contains("FromBase64String", base64.GeneratedSource);
        Assert.Contains("new UTF8Encoding(false, true)", base64.GeneratedSource);
        Assert.Collection(base64.SourceMap, value =>
        {
            Assert.Equal("__payload__", value.ElementId);
            Assert.Equal(PxaCodeLanguages.CSharpBase64, value.Language);
        });
    }

    [Fact]
    public void ModelWriter_CoversEveryWritableCanonicalContractProperty()
    {
        var writer = new CSharpDesignSourceWriter();
        foreach (var type in ReachableDtoTypes(typeof(DesignExportDto)))
        {
            var value = Activator.CreateInstance(type)!;
            var source = writer.WriteContractObject(value);
            foreach (var property in type.GetProperties().Where(value => value.CanRead && value.CanWrite))
                Assert.Contains($"{property.Name} =", source);
        }
    }

    [Fact]
    public async Task GeneratedRepresentations_PreserveEveryCurrentDesignerElementFamily()
    {
        string[] elementTypes =
        [
            "text", "image", "shape", "table", "line", "qrcode", "barcode", "signature",
            "richtext", "field", "textarea", "checkbox", "rect", "circle", "chart", "subsection",
            "area", "button", "dropdown", "optionlist", "radio", "watermark", "note", "arrow",
            "draw", "date", "highlight", "checkmark", "pageboundary", "pagenumber", "link", "number",
            "toc", "footnote", "endnote", "bookmark", "comment", "contentcontrol",
        ];
        var design = new DesignExportDto
        {
            Id = "all-elements",
            Pages = [new PageDto
            {
                Id = "page-1",
                Elements = elementTypes.Select((type, index) => new ElementDto
                {
                    Id = $"element-{index + 1}", Type = type, X = 10 + index % 4 * 120,
                    Y = 10 + index / 4 * 55, Width = 100, Height = 40, Content = type,
                }).ToList(),
            }],
        };
        var json = JsonSerializer.Serialize(design, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var expected = CanonicalJson(design);
        var service = CreatePackagedWorkerService();

        foreach (var language in PxaCodeLanguages.Supported.Where(value => value != PxaCodeLanguages.Json))
        {
            var generated = await service.ConvertAsync(PxaCodeLanguages.Json, language, json, default);
            var executed = await service.ExecuteAsync(language, generated.GeneratedSource, default);

            Assert.True(executed.Success,
                $"{language}: {string.Join(" | ", executed.Diagnostics.Select(value => $"{value.Code}: {value.Message}"))}");
            Assert.Equal(PxaCodeFidelity.Exact, executed.Fidelity);
            Assert.Equal(expected, CanonicalJson(executed.CanonicalDesign!));
            if (language == PxaCodeLanguages.CSharpPdf) Assert.NotEmpty(executed.PdfBytes!);
        }
    }

    [Theory]
    [InlineData("%%%", "PXACODE130")]
    [InlineData("/w==", "PXACODE131")]
    public async Task Base64Worker_ReportsStablePayloadDiagnostics(string payload, string expectedCode)
    {
        var source = $$"""
            var json = new UTF8Encoding(false, true).GetString(Convert.FromBase64String("{{payload}}"));
            return JsonSerializer.Deserialize<DesignExportDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            """;

        var response = await CreatePackagedWorkerService().ExecuteAsync(PxaCodeLanguages.CSharpBase64, source, default);

        Assert.False(response.Success);
        Assert.Contains(response.Diagnostics, value => value.Code == expectedCode);
    }

    [Fact]
    public async Task WorkerClient_ReturnsStableDiagnosticWhenWorkerPackageIsMissing()
    {
        var client = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = "missing-code-worker" }),
            NullLogger<PxaCodeWorkerClient>.Instance);

        var response = await client.RunAsync(new PxaCodeWorkerRequest
        {
            Language = PxaCodeLanguages.CSharpModel, Source = "return new DesignExportDto();",
        }, default);

        Assert.False(response.Success);
        Assert.Contains(response.Diagnostics, value => value.Code == "PXACODE041");
    }

    [Fact]
    public async Task PdfWorker_ReportsUnknownBuilderOperationsAsCompilationDiagnostics()
    {
        var response = await CreatePackagedWorkerService().ExecuteAsync(PxaCodeLanguages.CSharpPdf,
            "var document = new PxaPdfCodeBuilder(new DesignExportDto()); document.AddUnknown(); return document.Build();",
            default);

        Assert.False(response.Success);
        Assert.Contains(response.Diagnostics, value => value.Code == "PXACODE030");
    }

    [Fact]
    public async Task Worker_StopsUnboundedControlFlowAtTheRequestTimeout()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var client = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory, TimeoutSeconds = 3 }),
            NullLogger<PxaCodeWorkerClient>.Instance);

        var response = await client.RunAsync(new PxaCodeWorkerRequest
        {
            Language = PxaCodeLanguages.CSharpModel,
            TimeoutSeconds = 1,
            Source = "while (true) { } return new DesignExportDto();",
        }, default);

        Assert.False(response.Success);
        Assert.Contains(response.Diagnostics, value => value.Code == "PXACODE031");
    }

    [Fact]
    public async Task WorkerClient_RejectsOversizedWorkerResponses()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var client = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory, MaximumOutputBytes = 10 }),
            NullLogger<PxaCodeWorkerClient>.Instance);

        var response = await client.RunAsync(new PxaCodeWorkerRequest
        {
            Language = PxaCodeLanguages.CSharpModel,
            Source = "return new DesignExportDto { Pages = new List<PageDto> { new PageDto { Id = \"page-1\" } } };",
        }, default);

        Assert.False(response.Success);
        Assert.Contains(response.Diagnostics, value => value.Code == "PXACODE043");
    }

    [Fact]
    public async Task WorkerClient_HonorsCallerCancellation()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        var client = new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory }),
            NullLogger<PxaCodeWorkerClient>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RunAsync(new PxaCodeWorkerRequest
        {
            Language = PxaCodeLanguages.CSharpModel,
            Source = "return new DesignExportDto();",
        }, cancellation.Token));
    }

    [Theory]
    [InlineData("{\"pages\":[{\"id\":\"same\",\"elements\":[]},{\"id\":\"same\",\"elements\":[]}]}", "PXACODE104")]
    [InlineData("{\"pages\":[{\"id\":\"page-1\",\"elements\":[{\"id\":\"same\",\"type\":\"text\"},{\"id\":\"same\",\"type\":\"text\"}]}]}", "PXACODE105")]
    public void JsonValidation_RejectsDuplicateStableIds(string source, string code)
    {
        var result = new PxaCodeConversionService(new RejectingWorker()).ValidateJson(source);

        Assert.Equal(PxaCodeFidelity.Unsupported, result.DocumentFidelity);
        Assert.Contains(result.Diagnostics, value => value.Code == code);
    }

    [Theory]
    [InlineData(PxaCodeLanguages.CSharpModel, PxaCodeLanguages.Json)]
    [InlineData(PxaCodeLanguages.CSharpModel, PxaCodeLanguages.CSharpPdf)]
    [InlineData(PxaCodeLanguages.CSharpPdf, PxaCodeLanguages.Json)]
    [InlineData(PxaCodeLanguages.CSharpPdf, PxaCodeLanguages.CSharpModel)]
    public async Task CSharpConversions_UseWorkerCanonicalResult(string sourceLanguage, string targetLanguage)
    {
        var service = new PxaCodeConversionService(new CanonicalWorker());

        var result = await service.ConvertAsync(sourceLanguage, targetLanguage, "return a safe PXA document;", default);

        Assert.NotNull(result.CanonicalDesign);
        Assert.Equal("worker-element", result.CanonicalDesign.Pages[0].Elements[0].Id);
        Assert.NotEmpty(result.GeneratedSource);
        Assert.NotEmpty(result.SourceChecksum);
        Assert.NotEmpty(result.CanonicalChecksum);
    }

    [Fact]
    public async Task ModelControlFlow_PreservesTheDocumentAndReportsLostSourceStructure()
    {
        var service = CreatePackagedWorkerService();
        const string source = """
            var design = new DesignExportDto
            {
                Id = "control-flow",
                Pages = [new PageDto { Id = "page-1" }],
            };
            for (var index = 0; index < 2; index++)
            {
                design.Pages[0].Elements.Add(new ElementDto
                {
                    Id = $"item-{index}",
                    Type = "text",
                    Content = $"Item {index}",
                });
            }
            return design;
            """;

        var result = await service.ConvertAsync(
            PxaCodeLanguages.CSharpModel, PxaCodeLanguages.Json, source, default);

        Assert.Equal(PxaCodeFidelity.Exact, result.DocumentFidelity);
        Assert.Equal(PxaCodeSourcePreservation.StructureLost, result.SourcePreservation);
        Assert.Equal(["item-0", "item-1"],
            result.CanonicalDesign!.Pages[0].Elements.Select(value => value.Id));
    }

    [Fact]
    public async Task JsonRoundtrip_PreservesPagesBindingsAssetsRtlAndLocalization()
    {
        var service = new PxaCodeConversionService(new RejectingWorker());
        const string source = """
            {"id":"localized","pageSettings":{"activeLanguages":["en","ar"],"targetLanguage":"ar"},"pages":[{"id":"page-1","elements":[{"id":"rtl","type":"text","x":10,"y":20,"width":100,"height":20,"content":"مرحبا","binding":"customer.name","language":"ar","textDirection":"rtl","assetId":"asset-1"}]},{"id":"page-2","elements":[]}]}
            """;

        var result = await service.ConvertAsync(PxaCodeLanguages.Json, PxaCodeLanguages.Json, source, default);
        var element = result.CanonicalDesign!.Pages[0].Elements[0];

        Assert.Equal(2, result.CanonicalDesign.Pages.Count);
        Assert.Equal("customer.name", element.Binding);
        Assert.Equal("rtl", element.TextDirection);
        Assert.Equal("ar", element.Language);
        Assert.Equal("asset-1", element.Extensions!["assetId"].GetString());
        Assert.Equal(["en", "ar"], result.CanonicalDesign.PageSettings!.ActiveLanguages);
    }

    [Theory]
    [InlineData("System.IO.File.ReadAllText(\"x\")")]
    [InlineData("new System.Net.Http.HttpClient()")]
    [InlineData("System.Diagnostics.Process.Start(\"x\")")]
    [InlineData("typeof(string).Assembly")]
    [InlineData("unsafe { int* p = null; }")]
    [InlineData("dynamic value = 1;")]
    [InlineData("#r \"package.dll\"")]
    [InlineData("Environment.GetEnvironmentVariable(\"SECRET\")")]
    [InlineData("new System.Threading.Thread(() => { })")]
    [InlineData("System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(\"x\")")]
    [InlineData("System.Runtime.InteropServices.Marshal.AllocHGlobal(16)")]
    [InlineData("[System.Runtime.InteropServices.DllImport(\"native\")] static extern void Call();")]
    public void Sandbox_RejectsDangerousCapabilities(string source)
    {
        Assert.Contains(SandboxPolicy.Analyze(source), value => value.Severity == "error");
    }

    private sealed class RejectingWorker : IPxaCodeWorkerClient
    {
        public Task<PxaCodeWorkerResponse> RunAsync(PxaCodeWorkerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PxaCodeWorkerResponse
            {
                Diagnostics = [new PxaCodeDiagnosticDto { Code = "TEST", Severity = "error", Message = "Worker should not run for JSON generation." }],
            });
    }

    private sealed class CanonicalWorker : IPxaCodeWorkerClient
    {
        public Task<PxaCodeWorkerResponse> RunAsync(PxaCodeWorkerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PxaCodeWorkerResponse
            {
                Success = true,
                Fidelity = request.Language == PxaCodeLanguages.CSharpPdf ? PxaCodeFidelity.Compatible : PxaCodeFidelity.Exact,
                CanonicalDesign = new DesignExportDto
                {
                    Id = "worker-design",
                    Pages = [new PageDto
                    {
                        Id = "page-1",
                        Elements = [new ElementDto { Id = "worker-element", Type = "text", X = 1, Y = 2, Width = 3, Height = 4 }],
                    }],
                },
                SourceMap = [new PxaCodeSourceMapEntryDto { ElementId = "worker-element", Language = request.Language, StartLine = 1, EndLine = 1, StartColumn = 1, EndColumn = 1 }],
            });
    }

    private static string CanonicalJson(object value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = value is string source ? source : JsonSerializer.Serialize(value, options);
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, options);
    }

    private static PxaCodeConversionService CreatePackagedWorkerService()
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        return new PxaCodeConversionService(new PxaCodeWorkerClient(
            new TestWebHostEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory }),
            NullLogger<PxaCodeWorkerClient>.Instance));
    }

    private static IEnumerable<Type> ReachableDtoTypes(Type root)
    {
        var pending = new Stack<Type>([root]);
        var seen = new HashSet<Type>();
        while (pending.TryPop(out var type))
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsArray) type = type.GetElementType()!;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments()) pending.Push(argument);
                continue;
            }
            if (type.Namespace != typeof(DesignExportDto).Namespace || !type.Name.EndsWith("Dto", StringComparison.Ordinal) || !seen.Add(type))
                continue;
            yield return type;
            foreach (var property in type.GetProperties().Where(value => value.CanRead && value.CanWrite))
                pending.Push(property.PropertyType);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PXA.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
