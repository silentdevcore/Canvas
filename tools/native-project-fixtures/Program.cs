using System.Security.Cryptography;
using PVAD.Core.Contracts;
using PVAD.Core.ProjectFormats;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var outputDirectory = Path.Combine(repositoryRoot, "docs", "fixtures", "native-projects");
Directory.CreateDirectory(outputDirectory);

var pdf = new DesignExportDto
{
    Id = "pvadp-reference",
    Name = "PVADP Reference",
    PageSettings = new PageSettingsDto { Width = 595, Height = 842, Orientation = "portrait" },
    Pages =
    [
        new PageDto
        {
            Id = "page-1",
            Elements =
            [
                new ElementDto
                {
                    Id = "title", Type = "text", Content = "Portable PVAD PDF Project",
                    X = 36, Y = 48, Width = 300, Height = 30,
                },
            ],
        },
    ],
};
var spreadsheet = new SpreadsheetDto
{
    Id = "pvads-reference",
    Name = "PVADS Reference",
    Sheets =
    [
        new SheetDto
        {
            Id = "sheet-1", Name = "Summary", RowCount = 20, ColCount = 8,
            Cells =
            [
                new CellDto { Row = 0, Col = 0, Type = "text", Value = "Amount" },
                new CellDto { Row = 1, Col = 0, Type = "number", Value = 42m },
                new CellDto { Row = 2, Col = 0, Type = "formula", Formula = "=A2*2", Value = 84m },
            ],
        },
    ],
};

var fixtures = new Dictionary<string, byte[]>(StringComparer.Ordinal)
{
    ["minimal.pvadp"] = PvadProjectArchive.WritePdf(pdf),
    ["minimal.pvads"] = PvadProjectArchive.WriteSpreadsheet(spreadsheet),
};
foreach (var fixture in fixtures)
    File.WriteAllBytes(Path.Combine(outputDirectory, fixture.Key), fixture.Value);

var hashes = new[] { "minimal.pxap", "minimal.pxas", "minimal.pvadp", "minimal.pvads" }.Select(name =>
    $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(outputDirectory, name)))).ToLowerInvariant()}  {name}");
File.WriteAllText(Path.Combine(outputDirectory, "SHA256SUMS"), string.Join('\n', hashes) + "\n");
