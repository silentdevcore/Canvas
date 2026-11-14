using System.Globalization;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class DateFormattingParityTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var file in new[] { "date-format-cases.json", "date-historical-cases.json" })
        {
            using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "test-assets", "visual-parity", file)));
            foreach (var example in source.RootElement.GetProperty("cases").EnumerateArray())
                yield return [example.GetProperty("id").GetString()!, example.GetRawText(), source.RootElement.GetProperty("instant").GetString()!];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EverySharedCaseMatchesThePortableContract(string id, string source, string defaultInstant)
    {
        using var json = JsonDocument.Parse(source);
        var example = json.RootElement;
        var element = JsonSerializer.Deserialize<ElementDto>(example.GetProperty("input").GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var instant = DateTimeOffset.Parse(example.TryGetProperty("instant", out var time) ? time.GetString()! : defaultInstant, CultureInfo.InvariantCulture);
        var result = DesignerDateFormatter.Format(element.DateMode, element.Content, element.FallbackText,
            element.Binding, element.DateFormat, element.Locale, element.Timezone, instant);
        Assert.Equal(example.GetProperty("expected").GetString(), result.Text);
        Assert.Equal(example.GetProperty("warnings").EnumerateArray().Select(value => value.GetString()), result.Warnings);
    }

    [Fact]
    public async Task MapperUsesOneInstantAndResolvesBindingWithoutMutatingTheTemplate()
    {
        var design = new DesignExportDto
        {
            PageSettings = new() { Width = 400, Height = 250, CustomProperties = [new() { Name = "Invoice.Date", Value = "2026-11-13T12:00:00Z" }] },
            Pages = [new() { Id = "page", Elements = [
                new() { Id = "render", Type = "date", X = 20, Y = 20, Width = 300, Height = 30, DateMode = "render", DateFormat = "yyyy-MM-dd HH:mm:ss", Timezone = "Europe/Berlin" },
                new() { Id = "bound", Type = "date", X = 20, Y = 60, Width = 300, Height = 30, DateMode = "binding", Binding = "invoice.date", DateFormat = "dd.MM.yyyy HH:mm", Timezone = "Europe/Berlin" }
            ] }]
        };
        var before = JsonSerializer.Serialize(design);
        var bytes = DesignJsonMapper.MapToPdfDocument(design, renderInstant: DateTimeOffset.Parse("2026-03-29T01:00:00Z", CultureInfo.InvariantCulture)).ToBytes();
        var imported = await new PVAD.Importer.PdfImporter().LoadAsync(new MemoryStream(bytes));
        var text = string.Join(" ", imported.Pages.SelectMany(page => page.TextObjects).Select(value => value.Text));
        Assert.Contains("2026-03-29 03:00:00", text);
        Assert.Contains("13.11.2026 13:00", text);
        Assert.Equal(before, JsonSerializer.Serialize(design));
    }

    [Fact]
    public void EmptyBoundPropertyUsesFallbackInsteadOfStaleContent()
    {
        var document = DesignJsonMapper.MapToPdfDocument(new()
        {
            PageSettings = new() { CustomProperties = [new() { Name = "Invoice.Date", Value = "" }] },
            Pages = [new() { Id = "page", Elements = [new() { Id = "date", Type = "date", X = 20, Y = 20, Width = 300, Height = 30,
                DateMode = "binding", Binding = "Invoice.Date", Content = "2025-01-01", FallbackText = "No date" }] }]
        });
        var text = document.CreateDesignSnapshot().Pages[0].Elements.Select(element => element.Text);
        Assert.Contains("No date", text);
        Assert.DoesNotContain("2025-01-01", text);
    }

    [Theory]
    [InlineData("1890-01-01T00:00:00Z", "1890-01-01 00:00:00 +00:00")]
    [InlineData("1973-01-01T00:00:00Z", "1973-01-01 01:00:00 +01:00")]
    public async Task HistoricalBindingUsesThePortableBoundaryWithoutRewritingSource(string source, string expected)
    {
        var design = new DesignExportDto
        {
            PageSettings = new() { CustomProperties = [new() { Name = "Archive.Date", Value = source }] },
            Pages = [new() { Id = "page", Elements = [new() { Id = "historical", Type = "date", X = 20, Y = 20,
                Width = 500, Height = 40, DateMode = "binding", Binding = "Archive.Date", Timezone = "Europe/Paris",
                DateFormat = "yyyy-MM-dd HH:mm:ss zzz" }] }]
        };
        var before = JsonSerializer.Serialize(design);
        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();
        var imported = await new PVAD.Importer.PdfImporter().LoadAsync(new MemoryStream(bytes));
        var text = string.Join(" ", imported.Pages.SelectMany(page => page.TextObjects).Select(value => value.Text));
        Assert.Contains(expected, text);
        Assert.DoesNotContain("supported timezone", text);
        Assert.Equal(before, JsonSerializer.Serialize(design));
    }

    private static string Root()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !File.Exists(Path.Combine(path.FullName, "PVAD.sln"))) path = path.Parent;
        return path?.FullName ?? throw new DirectoryNotFoundException();
    }
}
