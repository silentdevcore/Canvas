using PVAD.Importer.Graphics;

namespace PVAD.Importer.Redaction;

internal static class PdfTextRedactionEngine
{
    public static bool CanRedactPartial(
        PdfTextElement text,
        IReadOnlyList<PdfRectangle> regions,
        out string reason)
    {
        if (text.Glyphs.Count == 0)
        {
            reason = "The text run does not expose original glyph bytes and advances.";
            return false;
        }

        var covered = text.Glyphs.Count(glyph => IsCovered(text, glyph, regions));
        if (covered == 0 || covered == text.Glyphs.Count)
        {
            reason = "The region does not split the run into covered and retained glyphs.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static bool TryRedactPartial(
        PdfTextElement text,
        IReadOnlyList<PdfRectangle> regions,
        ICollection<string> removedText,
        out string reason)
    {
        if (!CanRedactPartial(text, regions, out reason))
        {
            return false;
        }

        var runs = new List<PdfTextReplacementRun>();
        foreach (var glyph in text.Glyphs)
        {
            if (IsCovered(text, glyph, regions))
            {
                if (!string.IsNullOrWhiteSpace(glyph.Text))
                {
                    removedText.Add(glyph.Text);
                }

                continue;
            }

            var origin = MatrixEngine.TransformPoint(new PdfPoint(glyph.Offset, 0), text.Transform);
            var transform = text.Transform with { E = origin.X, F = origin.Y };
            runs.Add(new PdfTextReplacementRun(glyph.Bytes, glyph.Text, transform));
        }

        text.ReplacementRuns = runs;
        text.Text = string.Concat(runs.Select(static run => run.Text));
        reason = string.Empty;
        return true;
    }

    private static bool IsCovered(
        PdfTextElement text,
        PdfTextGlyph glyph,
        IReadOnlyList<PdfRectangle> regions)
    {
        var height = Math.Max(text.FontSize, 1d);
        var width = Math.Max(Math.Abs(glyph.Advance), height * 0.1d);
        var local = new PdfRectangle(glyph.Offset, -height * 0.2d, width, height);
        var bounds = MatrixEngine.TransformBounds(local, text.Transform);
        return regions.Any(bounds.Intersects);
    }
}
