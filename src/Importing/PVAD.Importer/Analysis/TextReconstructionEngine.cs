using PVAD.Licensing;

namespace PVAD.Importer.Analysis;

public sealed record ReconstructedWord(string Text, IReadOnlyList<PrimitiveText> GlyphRuns, Graphics.PdfRectangle Bounds);

public sealed class TextReconstructionEngine
{
    public IReadOnlyList<ReconstructedWord> BuildWords(ReadingLine line)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfAnalysis);
        return BuildWordsAdmitted(line);
    }

    internal IReadOnlyList<ReconstructedWord> BuildWordsAdmitted(ReadingLine line)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        var words = new List<ReconstructedWord>();
        var current = new List<PrimitiveText>();
        PrimitiveText? previous = null;

        foreach (var text in line.Texts.OrderBy(static text => text.Bounds.Left))
        {
            if (previous is not null && StartsNewWord(previous, text))
            {
                AddWord(words, current);
                current = [];
            }

            current.Add(text);
            previous = text;
        }

        AddWord(words, current);
        return words;
    }

    public string BuildParagraphText(ReadingParagraph paragraph)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfAnalysis);
        return BuildParagraphTextAdmitted(paragraph);
    }

    internal string BuildParagraphTextAdmitted(ReadingParagraph paragraph)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        return string.Join(Environment.NewLine, paragraph.Lines.Select(line => string.Join(" ", BuildWordsAdmitted(line).Select(static word => word.Text))));
    }

    private static bool StartsNewWord(PrimitiveText previous, PrimitiveText current)
    {
        var gap = current.Bounds.Left - previous.Bounds.Right;
        var fontContinuity = string.Equals(previous.FontName, current.FontName, StringComparison.Ordinal);
        var threshold = Math.Max(previous.FontSize, current.FontSize) * (fontContinuity ? 0.35d : 0.2d);
        return gap > threshold || previous.Text.EndsWith(' ');
    }

    private static void AddWord(List<ReconstructedWord> words, List<PrimitiveText> runs)
    {
        if (runs.Count == 0)
        {
            return;
        }

        words.Add(new ReconstructedWord(
            string.Concat(runs.Select(static run => run.Text)).Trim(),
            runs.ToArray(),
            ReadingOrderEngine.Union(runs.Select(static run => run.Bounds))));
    }
}
