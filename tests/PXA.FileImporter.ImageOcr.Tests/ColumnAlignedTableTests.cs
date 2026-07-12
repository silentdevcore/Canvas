using PXA.Core.Contracts;
using PXA.FileImporter.ImageOcr;

namespace PXA.FileImporter.ImageOcr.Tests;

/// <summary>
/// Regression: borderless, light-header tables (invoice line-item style) must be reconstructed in
/// the "Full layout" (text-background) import mode. Previously table detection was gated to the
/// structured layout mode the UI never sends, so these tables came through only as loose text.
/// </summary>
public sealed class ColumnAlignedTableTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RealSamples", "Agency-Invoice.png");

    private static (string tessData, string native)? OcrPaths()
    {
        var tessData = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var native   = Path.Combine(AppContext.BaseDirectory, "native");
        return Directory.Exists(native) && Directory.Exists(tessData) ? (tessData, native) : null;
    }

    [Fact]
    public async Task TextBackground_ReconstructsBothLineItemTables_WithoutSummaryFalsePositive()
    {
        if (OcrPaths() is not { } paths) return;     // OCR native deps unavailable in this env
        if (!File.Exists(ImagePath)) return;

        var converter = new ImageToPdfConverter(new EmbeddedTesseractOcrEngine(paths.tessData, paths.native));
        using var stream = File.OpenRead(ImagePath);

        ImageToPdfConversionResult result;
        try
        {
            result = await converter.ConvertAsync(stream, "Agency-Invoice.png", new ImageToPdfConversionOptions
            {
                Languages = "eng",
                SourceDpiX = 150,
                SourceDpiY = 150,
                EnablePreprocessing = true,
                NativeLibraryPath = paths.native,
                LayoutMode = "text-background",   // the UI's "Full layout" mode
            });
        }
        catch (OcrNativeDependencyMissingException) { return; }
        catch (DllNotFoundException) { return; }

        var tables = result.Design.Pages[0].Elements.Where(e => e.Type == "table").ToList();

        // Both PHASE line-item tables are reconstructed — and only those (the 2-column totals
        // summary and FROM/BILL-TO block must NOT become tables).
        Assert.Equal(2, tables.Count);

        foreach (var table in tables)
        {
            Assert.NotNull(table.CellData);
            Assert.Equal(4, table.CellData![0].Length);                       // SERVICE | HOURS | RATE | AMOUNT
            Assert.True(table.CellData.Length >= 3);                          // header + >=2 rows
            var flat = string.Join(" ", table.CellData.SelectMany(r => r));
            Assert.Contains("AMOUNT", flat, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("£180/h", flat);                                  // rate column recovered
            Assert.DoesNotContain("Subtotal", flat, StringComparison.OrdinalIgnoreCase);
        }

        // The distinctive line-item amounts land in the tables, not loose text.
        var allTableText = string.Join(" ", tables.SelectMany(t => t.CellData!.SelectMany(r => r)));
        Assert.Contains("£1,440.00", allTableText);
        Assert.Contains("£3,600.00", allTableText);
    }
}
