using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Canvas.Infrastructure.Word;

/// <summary>
/// Manages the Word comments part (comments.xml).
/// Call <see cref="EnsurePart"/> once, then <see cref="AddComment"/> per element,
/// and finally <see cref="Save"/> to flush the part.
/// </summary>
internal sealed class CommentService
{
    private readonly WordprocessingDocument _doc;
    private WordprocessingCommentsPart? _part;
    private int _nextId = 1;

    internal CommentService(WordprocessingDocument doc) => _doc = doc;

    internal void EnsurePart()
    {
        var main = _doc.MainDocumentPart!;
        _part = main.WordprocessingCommentsPart ?? main.AddNewPart<WordprocessingCommentsPart>();
        _part.Comments ??= new Comments();
    }

    /// <summary>
    /// Appends a comment to the comments part and returns a pair of
    /// <see cref="OpenXmlElement"/> values: the start and end markers that
    /// bracket the anchor run in the document body.
    /// </summary>
    internal (CommentRangeStart start, CommentRangeEnd end, RunProperties refRunProps) AddComment(
        string text, string author, string? date = null)
    {
        EnsurePart();

        var id = _nextId++;
        var idVal = new StringValue(id.ToString());

        var comment = new Comment
        {
            Id = idVal,
            Author = author,
            Date = DateTime.TryParse(date, out var dt) ? dt : DateTime.UtcNow,
            Initials = InitialsFrom(author),
        };

        var p = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.Append(new ParagraphStyleId { Val = "CommentText" });
        p.Append(pPr);

        var run = new Run();
        run.Append(new RunProperties(new RunStyle { Val = "CommentReference" },
                                     new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }));
        run.Append(new AnnotationReferenceMark());
        p.Append(run);

        var textRun = new Run();
        textRun.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        p.Append(textRun);

        comment.Append(p);
        _part!.Comments!.Append(comment);

        var refRunProps = new RunProperties();
        refRunProps.Append(new CommentReference { Id = idVal });

        return (
            new CommentRangeStart { Id = idVal },
            new CommentRangeEnd   { Id = idVal },
            refRunProps
        );
    }

    internal void Save() => _part?.Comments?.Save();

    private static string InitialsFrom(string author)
    {
        var parts = author.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpper(p[0])));
    }
}
