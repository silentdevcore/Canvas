using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Infrastructure.Word;

/// <summary>
/// Manages footnote and endnote parts in a DOCX document.
/// Call <see cref="EnsureParts"/> once before adding any notes,
/// then <see cref="AddFootnote"/> / <see cref="AddEndnote"/> per element,
/// and finally <see cref="Save"/> to persist both parts.
/// </summary>
internal sealed class FootnoteService
{
    private readonly WordprocessingDocument _doc;
    private FootnotesPart? _fnPart;
    private EndnotesPart? _enPart;
    private int _fnId = 1;
    private int _enId = 1;

    internal FootnoteService(WordprocessingDocument doc) => _doc = doc;

    internal void EnsureParts()
    {
        var main = _doc.MainDocumentPart!;
        _fnPart = main.FootnotesPart ?? main.AddNewPart<FootnotesPart>();
        _enPart = main.EndnotesPart  ?? main.AddNewPart<EndnotesPart>();

        _fnPart.Footnotes ??= BuildDefaultFootnotes();
        _enPart.Endnotes  ??= BuildDefaultEndnotes();
    }

    /// <summary>
    /// Adds a footnote and returns the inline <see cref="Run"/> that contains
    /// the footnote reference mark (insert this into the paragraph at the
    /// anchor position).
    /// </summary>
    internal Run AddFootnote(string text)
    {
        EnsureParts();

        var id = _fnId++;
        var fn = new Footnote { Id = id, Type = FootnoteEndnoteValues.Normal };
        fn.Append(BuildNoteBody(text, isFootnote: true, id));
        _fnPart!.Footnotes!.Append(fn);

        return BuildRefRun(isFootnote: true, id);
    }

    /// <summary>
    /// Adds an endnote and returns the inline reference run.
    /// </summary>
    internal Run AddEndnote(string text)
    {
        EnsureParts();

        var id = _enId++;
        var en = new Endnote { Id = id, Type = FootnoteEndnoteValues.Normal };
        en.Append(BuildNoteBody(text, isFootnote: false, id));
        _enPart!.Endnotes!.Append(en);

        return BuildRefRun(isFootnote: false, id);
    }

    internal void Save()
    {
        _fnPart?.Footnotes?.Save();
        _enPart?.Endnotes?.Save();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Paragraph BuildNoteBody(string text, bool isFootnote, int id)
    {
        var p = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.Append(new ParagraphStyleId { Val = isFootnote ? "FootnoteText" : "EndnoteText" });
        p.Append(pPr);

        var refRun = new Run();
        refRun.Append(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }));
        if (isFootnote)
            refRun.Append(new FootnoteReferenceMark());
        else
            refRun.Append(new EndnoteReferenceMark());
        p.Append(refRun);

        var textRun = new Run();
        textRun.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        p.Append(textRun);

        return p;
    }

    private static Run BuildRefRun(bool isFootnote, int id)
    {
        var run = new Run();
        run.Append(new RunProperties(
            new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
            new RunStyle { Val = isFootnote ? "FootnoteReference" : "EndnoteReference" }));

        if (isFootnote)
            run.Append(new FootnoteReference { Id = id });
        else
            run.Append(new EndnoteReference { Id = id });

        return run;
    }

    private static Footnotes BuildDefaultFootnotes()
    {
        var fns = new Footnotes();

        // Separator footnote (Word requires id=-1)
        var sep = new Footnote { Id = -1, Type = FootnoteEndnoteValues.Separator };
        var sepP = new Paragraph();
        sepP.Append(new Run(new SeparatorMark()));
        sep.Append(sepP);
        fns.Append(sep);

        // Continuation separator (id=-2)
        var cont = new Footnote { Id = -2, Type = FootnoteEndnoteValues.ContinuationSeparator };
        var contP = new Paragraph();
        contP.Append(new Run(new ContinuationSeparatorMark()));
        cont.Append(contP);
        fns.Append(cont);

        return fns;
    }

    private static Endnotes BuildDefaultEndnotes()
    {
        var ens = new Endnotes();

        var sep = new Endnote { Id = -1, Type = FootnoteEndnoteValues.Separator };
        sep.Append(new Paragraph(new Run(new SeparatorMark())));
        ens.Append(sep);

        var cont = new Endnote { Id = -2, Type = FootnoteEndnoteValues.ContinuationSeparator };
        cont.Append(new Paragraph(new Run(new ContinuationSeparatorMark())));
        ens.Append(cont);

        return ens;
    }
}
