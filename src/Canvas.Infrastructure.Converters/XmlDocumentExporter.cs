using System.Text;
using System.Xml.Linq;
using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;

namespace Canvas.Infrastructure.Converters;

public sealed class XmlDocumentExporter : IDocumentExporter
{
    public string FormatKey     => "xml";
    public string MimeType      => "application/xml; charset=utf-8";
    public string FileExtension => ".xml";
    public IExporterCapabilities Capabilities => new ExporterCapabilities();

    public byte[] Export(DesignExportDto design)
    {
        var ps = design.PageSettings ?? new PageSettingsDto();

        var pageSettings = new XElement("PageSettings",
            new XAttribute("width", ps.Width),
            new XAttribute("height", ps.Height),
            new XAttribute("orientation", ps.Orientation),
            ps.BackgroundColor is not null ? new XAttribute("backgroundColor", ps.BackgroundColor) : null,
            ps.Margins is not null
                ? new XElement("Margins",
                    new XAttribute("top",    ps.Margins.Top),
                    new XAttribute("right",  ps.Margins.Right),
                    new XAttribute("bottom", ps.Margins.Bottom),
                    new XAttribute("left",   ps.Margins.Left))
                : null);

        var pagesEl = new XElement("Pages",
            design.Pages.Select((page, idx) =>
                new XElement("Page",
                    new XAttribute("id", page.Id),
                    new XAttribute("index", idx),
                    new XElement("Elements",
                        page.Elements
                            .Where(e => e.Hidden != true)
                            .Select(MapElement)))));

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("CanvasDocument",
                new XAttribute("name", design.Name),
                new XAttribute("id", design.Id),
                new XAttribute("version", "1.0"),
                pageSettings,
                pagesEl));

        using var ms = new MemoryStream();
        using var writer = new System.Xml.XmlTextWriter(ms, Encoding.UTF8)
        {
            Formatting = System.Xml.Formatting.Indented
        };
        doc.WriteTo(writer);
        writer.Flush();
        return ms.ToArray();
    }

    private static XElement MapElement(ElementDto el)
    {
        var elem = new XElement("Element",
            new XAttribute("id", el.Id),
            new XAttribute("type", el.Type),
            new XAttribute("x", el.X),
            new XAttribute("y", el.Y),
            new XAttribute("width", el.Width),
            new XAttribute("height", el.Height));

        if (el.Name is not null) elem.Add(new XAttribute("name", el.Name));

        // Common style
        if (el.Style is { Count: > 0 })
        {
            var styleEl = new XElement("Style");
            foreach (var kv in el.Style)
                styleEl.Add(new XAttribute(kv.Key, kv.Value?.ToString() ?? ""));
            elem.Add(styleEl);
        }

        // Type-specific content
        switch (el.Type)
        {
            case "text":
            case "link":
            case "button":
                if (el.Content is not null) elem.Add(new XElement("Content", el.Content));
                if (el.Href is not null)    elem.Add(new XElement("Href", el.Href));
                if (el.ButtonAction is not null) elem.Add(new XElement("ButtonAction", el.ButtonAction));
                break;

            case "richtext":
                if (el.HtmlContent is not null) elem.Add(new XElement("HtmlContent", new XCData(el.HtmlContent)));
                break;

            case "image":
                if (el.Content is not null) elem.Add(new XElement("Src", el.Content));
                if (el.FitMode is not null) elem.Add(new XAttribute("fitMode", el.FitMode));
                break;

            case "qrcode":
                if (el.QrValue is not null) elem.Add(new XElement("QrValue", el.QrValue));
                break;

            case "barcode":
                if (el.BarcodeValue is not null) elem.Add(new XElement("BarcodeValue", el.BarcodeValue));
                if (el.BarcodeType is not null)  elem.Add(new XAttribute("barcodeType", el.BarcodeType));
                break;

            case "table":
                elem.Add(new XAttribute("headerRow", el.HeaderRow ?? false));
                if (el.CellData is not null)
                {
                    var tableEl = new XElement("CellData",
                        el.CellData.Select((row, r) =>
                            new XElement("Row", new XAttribute("index", r),
                                (row ?? []).Select((cell, c) =>
                                    new XElement("Cell", new XAttribute("col", c), cell ?? "")))));
                    elem.Add(tableEl);
                }
                break;

            case "signature":
                if (el.SignatureLabel is not null) elem.Add(new XElement("Label", el.SignatureLabel));
                break;

            case "note":
                if (el.NoteTitle is not null)  elem.Add(new XElement("Title", el.NoteTitle));
                if (el.NoteBody is not null)   elem.Add(new XElement("Body", el.NoteBody));
                if (el.NoteAuthor is not null) elem.Add(new XElement("Author", el.NoteAuthor));
                break;

            case "arrow":
                if (el.ArrowMode is not null)      elem.Add(new XAttribute("arrowMode", el.ArrowMode));
                if (el.ArrowDirection is not null)  elem.Add(new XAttribute("direction", el.ArrowDirection));
                if (el.StartMarker is not null)     elem.Add(new XAttribute("startMarker", el.StartMarker));
                if (el.EndMarker is not null)       elem.Add(new XAttribute("endMarker", el.EndMarker));
                break;

            case "optionlist":
            case "dropdown":
            case "radio":
                if (el.Options is not null)
                    elem.Add(new XElement("Options",
                        el.Options.Select(o => new XElement("Option", o))));
                if (el.ListStyle is not null) elem.Add(new XAttribute("listStyle", el.ListStyle));
                break;

            case "number":
                if (el.NumberValue.HasValue)    elem.Add(new XElement("Value", el.NumberValue));
                if (el.NumberStyle is not null) elem.Add(new XAttribute("numberStyle", el.NumberStyle));
                if (el.NumberCurrency is not null) elem.Add(new XAttribute("currency", el.NumberCurrency));
                break;
        }

        return elem;
    }
}
