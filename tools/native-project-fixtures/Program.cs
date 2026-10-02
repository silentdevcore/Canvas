using System.Security.Cryptography;
using PXA.Core.Contracts;
using PXA.Core.ProjectFormats;

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var outputDirectory = Path.Combine(repositoryRoot, "docs", "fixtures", "native-projects");
Directory.CreateDirectory(outputDirectory);

var pdf = new DesignExportDto
{
    Id = "pxap-reference",
    Name = "PXAP Reference",
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
                    Id = "title", Type = "text", Content = "Portable PXA PDF Project",
                    X = 36, Y = 48, Width = 300, Height = 30,
                },
            ],
        },
    ],
};
var spreadsheet = new SpreadsheetDto
{
    Id = "pxas-reference",
    Name = "PXAS Reference",
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
    ["minimal.pxap"] = PxaProjectArchive.WritePdf(pdf),
    ["minimal.pxas"] = PxaProjectArchive.WriteSpreadsheet(spreadsheet),
};
foreach (var fixture in fixtures)
    File.WriteAllBytes(Path.Combine(outputDirectory, fixture.Key), fixture.Value);

var hashes = fixtures.Select(fixture =>
    $"{Convert.ToHexString(SHA256.HashData(fixture.Value)).ToLowerInvariant()}  {fixture.Key}");
File.WriteAllText(Path.Combine(outputDirectory, "SHA256SUMS"), string.Join('\n', hashes) + "\n");
