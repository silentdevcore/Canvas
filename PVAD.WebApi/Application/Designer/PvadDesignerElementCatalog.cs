namespace PVAD.WebApi.Application.Designer;

public static class PvadDesignerElementCatalog
{
    public static readonly IReadOnlyList<string> ElementTypes =
    [
        "text", "image", "shape", "table", "line", "qrcode", "barcode", "signature",
        "richtext", "field", "textarea", "checkbox", "rect", "circle", "chart",
        "subsection", "area", "button", "dropdown", "optionlist", "radio", "watermark",
        "note", "arrow", "draw", "date", "highlight", "checkmark", "pageboundary",
        "pagenumber", "link", "number", "toc", "footnote", "endnote", "bookmark",
        "comment", "contentcontrol",
    ];

    public static readonly ISet<string> ElementTypeSet =
        new HashSet<string>(ElementTypes, StringComparer.Ordinal);
}
