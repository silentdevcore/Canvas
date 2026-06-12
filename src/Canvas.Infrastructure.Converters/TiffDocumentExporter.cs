using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using SkiaSharp;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Exports a design as a multi-page TIFF image archive (one TIFF per page, packed in a ZIP).
/// Single-page designs are returned as a raw TIFF byte array.
/// </summary>
public sealed class TiffDocumentExporter : IDocumentExporter
{
    private const float DefaultDpi = 150f;

    public string FormatKey     => "tiff";
    public string MimeType      => "image/tiff";
    public string FileExtension => ".tiff";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsFormFields: false);

    public byte[] Export(DesignExportDto design) => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options)
    {
        var dpi   = options?.Dpi ?? DefaultDpi;
        var scale = dpi / 72f;
        var ps    = design.PageSettings ?? new PageSettingsDto();

        var pages = design.Pages.Count > 0
            ? design.Pages
            : [new PageDto { Id = "p1", Elements = design.SharedElements }];

        if (pages.Count == 1)
            return RenderPageAsTiff(pages[0], design.SharedElements, ps, scale);

        // Multi-page → ZIP of TIFFs
        using var zipStream = new System.IO.MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(zipStream,
                   System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var tiff  = RenderPageAsTiff(pages[i], design.SharedElements, ps, scale);
                var entry = zip.CreateEntry($"page-{i + 1}.tiff");
                using var es = entry.Open();
                es.Write(tiff, 0, tiff.Length);
            }
        }
        return zipStream.ToArray();
    }

    private static byte[] RenderPageAsTiff(
        PageDto page, List<ElementDto> shared, PageSettingsDto ps, float scale)
    {
        int bmpW = (int)(ps.Width  * scale);
        int bmpH = (int)(ps.Height * scale);

        // Render through the shared image pipeline so TIFF has full element fidelity
        // (wrapped text, rich text, tables, images, borders, …). SkiaSharp has no native
        // TIFF encoder, so we take the PNG bitmap and repackage it as a baseline TIFF.
        var pngBytes = ImageDocumentExporter.RenderPage(page, shared, ps, SKEncodedImageFormat.Png, 100, scale);

        return ConvertPngToTiff(pngBytes, bmpW, bmpH);
    }

    /// <summary>
    /// Converts a PNG byte array to a minimal baseline TIFF (uncompressed, RGB).
    /// Uses SkiaSharp to decode the PNG into raw pixels, then writes a TIFF header.
    /// </summary>
    private static byte[] ConvertPngToTiff(byte[] pngBytes, int width, int height)
    {
        using var skData = SKData.CreateCopy(pngBytes);
        using var img    = SKImage.FromEncodedData(skData);
        if (img is null) return pngBytes;

        using var bitmap = SKBitmap.FromImage(img);
        var pixels = new byte[width * height * 3];
        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                var c = bitmap.GetPixel(px, py);
                int idx = (py * width + px) * 3;
                pixels[idx]     = c.Red;
                pixels[idx + 1] = c.Green;
                pixels[idx + 2] = c.Blue;
            }
        }

        return WriteTiff(width, height, pixels);
    }

    /// <summary>Writes a minimal 8-bit RGB baseline TIFF.</summary>
    private static byte[] WriteTiff(int width, int height, byte[] rgbPixels)
    {
        // TIFF uses little-endian in this implementation.
        using var ms = new System.IO.MemoryStream();
        using var w  = new System.IO.BinaryWriter(ms);

        // ── Header ──────────────────────────────────────────────────────────
        w.Write((byte)'I'); w.Write((byte)'I'); // little-endian
        w.Write((ushort)42);                    // TIFF magic
        w.Write((uint)8);                       // IFD offset (immediately after header)

        // ── IFD ─────────────────────────────────────────────────────────────
        // Fields: ImageWidth, ImageLength, BitsPerSample, Compression,
        //         PhotometricInterpretation, StripOffsets, SamplesPerPixel,
        //         RowsPerStrip, StripByteCounts, XResolution, YResolution,
        //         ResolutionUnit  (12 entries)
        const int entryCount = 12;
        // IFD starts at offset 8; each IFD entry = 12 bytes; IFD end = 8 + 2 + 12*12 + 4 = 158
        // Value data (BitsPerSample array, X/Y resolution) follows at offset 158.
        const uint ifdEnd         = 8 + 2 + entryCount * 12 + 4; // 158
        const uint bpsOffset      = ifdEnd;           // 3 × USHORT = 6 bytes
        const uint xResOffset     = bpsOffset + 6;    // 2 × LONG   = 8 bytes
        const uint yResOffset     = xResOffset + 8;
        const uint stripOffset    = (uint)(yResOffset + 8);

        w.Write((ushort)entryCount);

        WriteIfdEntry(w, 256, 4, 1, (uint)width);               // ImageWidth
        WriteIfdEntry(w, 257, 4, 1, (uint)height);              // ImageLength
        WriteIfdEntry(w, 258, 3, 3, bpsOffset);                  // BitsPerSample (offset)
        WriteIfdEntry(w, 259, 3, 1, 1);                          // Compression = none
        WriteIfdEntry(w, 262, 3, 1, 2);                          // PhotometricInterpretation = RGB
        WriteIfdEntry(w, 273, 4, 1, stripOffset);                // StripOffsets
        WriteIfdEntry(w, 277, 3, 1, 3);                          // SamplesPerPixel
        WriteIfdEntry(w, 278, 4, 1, (uint)height);              // RowsPerStrip
        WriteIfdEntry(w, 279, 4, 1, (uint)rgbPixels.Length);   // StripByteCounts
        WriteIfdEntry(w, 282, 5, 1, xResOffset);                 // XResolution (72/1)
        WriteIfdEntry(w, 283, 5, 1, yResOffset);                 // YResolution (72/1)
        WriteIfdEntry(w, 296, 3, 1, 2);                          // ResolutionUnit = inch

        w.Write((uint)0); // next IFD offset = 0 (none)

        // ── Value data ──────────────────────────────────────────────────────
        // BitsPerSample: 8, 8, 8
        w.Write((ushort)8); w.Write((ushort)8); w.Write((ushort)8);

        // XResolution: 72/1 (rational = two LONGs)
        w.Write((uint)72); w.Write((uint)1);
        // YResolution: 72/1
        w.Write((uint)72); w.Write((uint)1);

        // ── Strip data ──────────────────────────────────────────────────────
        w.Write(rgbPixels);

        return ms.ToArray();
    }

    private static void WriteIfdEntry(System.IO.BinaryWriter w, ushort tag, ushort type, uint count, uint value)
    {
        w.Write(tag);
        w.Write(type);
        w.Write(count);
        w.Write(value);
    }
}
