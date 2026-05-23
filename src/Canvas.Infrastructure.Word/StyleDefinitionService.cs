using Canvas.Core.Contracts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Canvas.Infrastructure.Word;

/// <summary>
/// Builds the DOCX styles part from <see cref="NamedStyleDto"/> definitions.
/// Supports paragraph, character, list, and table style types with optional
/// basedOn / nextStyle inheritance chains.
/// </summary>
internal static class StyleDefinitionService
{
    internal static void Apply(WordprocessingDocument doc, IList<NamedStyleDto>? namedStyles)
    {
        var mainPart = doc.MainDocumentPart!;

        // Always create the styles part so built-in Normal exists.
        var stylesPart = mainPart.StyleDefinitionsPart
            ?? mainPart.AddNewPart<StyleDefinitionsPart>();

        stylesPart.Styles ??= new Styles();
        var styles = stylesPart.Styles;

        // Ensure a default Normal paragraph style exists so Word doesn't complain.
        EnsureDefaultNormalStyle(styles);

        if (namedStyles is null || namedStyles.Count == 0)
        {
            styles.Save();
            return;
        }

        foreach (var dto in namedStyles)
        {
            var styleType = dto.Type switch
            {
                "character" => StyleValues.Character,
                "list"      => StyleValues.Numbering,
                "table"     => StyleValues.Table,
                _           => StyleValues.Paragraph,
            };

            var style = new Style { Type = styleType, StyleId = SanitizeId(dto.Id) };

            style.Append(new StyleName { Val = dto.Name });

            if (!string.IsNullOrWhiteSpace(dto.BasedOn))
                style.Append(new BasedOn { Val = SanitizeId(dto.BasedOn) });

            if (!string.IsNullOrWhiteSpace(dto.NextStyle))
                style.Append(new NextParagraphStyle { Val = SanitizeId(dto.NextStyle) });

            if (dto.Style is not null)
            {
                if (styleType == StyleValues.Character)
                {
                    style.Append(BuildRunProperties(dto.Style));
                }
                else
                {
                    style.Append(BuildParagraphProperties(dto.Style));
                    style.Append(BuildRunProperties(dto.Style));
                }
            }

            // Replace an existing style with the same id, or append a new one.
            var existing = styles.Elements<Style>()
                .FirstOrDefault(s => s.StyleId?.Value == style.StyleId?.Value);
            if (existing is not null)
                existing.Remove();

            styles.Append(style);
        }

        styles.Save();
    }

    private static void EnsureDefaultNormalStyle(Styles styles)
    {
        var hasNormal = styles.Elements<Style>()
            .Any(s => s.StyleId?.Value == "Normal");
        if (hasNormal) return;

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" });
        normal.Append(new PrimaryStyle());
        styles.InsertAt(normal, 0);
    }

    private static StyleParagraphProperties BuildParagraphProperties(Dictionary<string, object> s)
    {
        var pPr = new StyleParagraphProperties();

        if (s.TryGetValue("textAlign", out var align))
        {
            var jc = (align?.ToString()) switch
            {
                "center"  => JustificationValues.Center,
                "right"   => JustificationValues.Right,
                "justify" => JustificationValues.Both,
                _         => JustificationValues.Left,
            };
            pPr.Append(new Justification { Val = jc });
        }

        if (s.TryGetValue("lineHeight", out var lh) && lh is not null)
        {
            // lineHeight in CSS units → twips (240 = single, 480 = double)
            if (double.TryParse(lh.ToString(), out var lhVal))
            {
                var twips = (int)(lhVal * 240);
                pPr.Append(new SpacingBetweenLines
                {
                    Line = twips.ToString(),
                    LineRule = LineSpacingRuleValues.Auto,
                });
            }
        }

        return pPr;
    }

    private static StyleRunProperties BuildRunProperties(Dictionary<string, object> s)
    {
        var rPr = new StyleRunProperties();

        if (s.TryGetValue("fontFamily", out var ff) && ff is not null)
            rPr.Append(new RunFonts { Ascii = ff.ToString(), HighAnsi = ff.ToString() });

        if (s.TryGetValue("fontSize", out var fs) && fs is not null &&
            double.TryParse(fs.ToString(), out var fsPt))
            rPr.Append(new FontSize { Val = ((int)(fsPt * 2)).ToString() });

        if (s.TryGetValue("fontWeight", out var fw))
        {
            var bold = fw?.ToString() is "bold" or "700" or "800" or "900";
            if (bold) rPr.Append(new Bold());
        }

        if (s.TryGetValue("fontStyle", out var fst) && fst?.ToString() == "italic")
            rPr.Append(new Italic());

        if (s.TryGetValue("color", out var col) && col is not null)
            rPr.Append(new DocumentFormat.OpenXml.Wordprocessing.Color
            {
                Val = SanitizeColor(col.ToString()!),
            });

        if (s.TryGetValue("textDecoration", out var td))
        {
            if (td?.ToString()?.Contains("underline") == true)
                rPr.Append(new Underline { Val = UnderlineValues.Single });
            if (td?.ToString()?.Contains("line-through") == true)
                rPr.Append(new Strike());
        }

        return rPr;
    }

    private static string SanitizeId(string id)
        => new(id.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

    private static string SanitizeColor(string hex)
        => hex.TrimStart('#').Length == 6 ? hex.TrimStart('#').ToUpperInvariant() : "000000";
}
