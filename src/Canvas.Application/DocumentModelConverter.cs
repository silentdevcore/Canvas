using Canvas.Domain.ValueObjects;
using Canvas.Pdf;
using Canvas.Core.Primitives;
using System.Text.Json;

namespace Canvas.Application;

public static class DocumentModelConverter
{
    public static object ConvertToPdfDocument(object documentModel)
    {
        if (documentModel is not Dictionary<string, object> model)
        {
            throw new ArgumentException("Document model must be a dictionary", nameof(documentModel));
        }

        // Extract page settings
        var pageSettings = ExtractPageSettings(model);

        // Extract elements
        var elements = ExtractElements(model);

        // Create PDF document
        var pdfDocument = new PdfDocument();

        // Create page with correct size
        PdfPage page;
        if (pageSettings != null)
        {
            var pageSize = GetPageSize(pageSettings);
            page = pdfDocument.AddPage(pageSize.Width, pageSize.Height);
        }
        else
        {
            page = pdfDocument.AddPage(); // Default A4
        }

        // Render elements
        foreach (var element in elements)
        {
            RenderElement(page, element);
        }

        return pdfDocument;
    }

    private static PageSettings? ExtractPageSettings(Dictionary<string, object> model)
    {
        if (!model.TryGetValue("PageSettings", out var pageSettingsObj) ||
            pageSettingsObj is not Dictionary<string, object> pageSettingsDict)
        {
            return null;
        }

        return new PageSettings
        {
            Width = GetDoubleValue(pageSettingsDict, "Width") ?? 595.276,
            Height = GetDoubleValue(pageSettingsDict, "Height") ?? 841.89,
            Orientation = GetStringValue(pageSettingsDict, "Orientation") ?? "portrait"
        };
    }

    private static List<ExpandedElement> ExtractElements(Dictionary<string, object> model)
    {
        if (!model.TryGetValue("Elements", out var elementsObj) ||
            elementsObj is not List<object> elementsList)
        {
            return new List<ExpandedElement>();
        }

        var elements = new List<ExpandedElement>();
        foreach (var elementObj in elementsList)
        {
            if (elementObj is Dictionary<string, object> elementDict)
            {
                elements.Add(CreateExpandedElement(elementDict));
            }
        }

        return elements;
    }

    private static ExpandedElement CreateExpandedElement(Dictionary<string, object> elementDict)
    {
        return new ExpandedElement
        {
            Id = GetStringValue(elementDict, "Id") ?? Guid.NewGuid().ToString(),
            Type = ParseElementType(GetStringValue(elementDict, "Type")),
            Props = elementDict.TryGetValue("Props", out var props) && props is Dictionary<string, object> propsDict
                ? propsDict
                : new Dictionary<string, object>(),
            X = GetDoubleValue(elementDict, "X"),
            Y = GetDoubleValue(elementDict, "Y"),
            Width = GetDoubleValue(elementDict, "Width"),
            Height = GetDoubleValue(elementDict, "Height"),
            Children = new List<ExpandedElement>(),
            Index = GetIntValue(elementDict, "Index") ?? 0
        };
    }

    private static ElementType ParseElementType(string? typeString)
    {
        return typeString switch
        {
            "Text" => ElementType.Text,
            "Image" => ElementType.Image,
            "Rectangle" => ElementType.Rectangle,
            "Line" => ElementType.Line,
            "QRCode" => ElementType.QRCode,
            "Barcode" => ElementType.Barcode,
            "Signature" => ElementType.Signature,
            "RichText" => ElementType.RichText,
            "Link" => ElementType.Link,
            "Button" => ElementType.Button,
            "Checkbox" => ElementType.Checkbox,
            "Radio" => ElementType.Radio,
            "Table" => ElementType.Table,
            "List" => ElementType.List,
            "Chart" => ElementType.Chart,
            "TextField" => ElementType.TextField,
            "Dropdown" => ElementType.Dropdown,
            "Watermark" => ElementType.Watermark,
            "Note" => ElementType.Note,
            "Arrow" => ElementType.Arrow,
            "Draw" => ElementType.Draw,
            "Date" => ElementType.Date,
            "Highlight" => ElementType.Highlight,
            "CheckMark" => ElementType.CheckMark,
            "PageBoundary" => ElementType.PageBoundary,
            "PageNumber" => ElementType.PageNumber,
            _ => ElementType.Text
        };
    }

    private static void RenderElement(PdfPage page, ExpandedElement element)
    {
        switch (element.Type)
        {
            case ElementType.Text:
                RenderTextElement(page, element);
                break;
            case ElementType.Rectangle:
                RenderRectangleElement(page, element);
                break;
            case ElementType.Line:
                RenderLineElement(page, element);
                break;
            case ElementType.QRCode:
                RenderQRCodeElement(page, element);
                break;
            case ElementType.Barcode:
                RenderBarcodeElement(page, element);
                break;
            case ElementType.Signature:
                RenderSignatureElement(page, element);
                break;
            case ElementType.RichText:
                RenderRichTextElement(page, element);
                break;
            case ElementType.Chart:
                RenderChartElement(page, element);
                break;
            case ElementType.Watermark:
                RenderWatermarkElement(page, element);
                break;
            case ElementType.Note:
                RenderNoteElement(page, element);
                break;
            case ElementType.Arrow:
                RenderArrowElement(page, element);
                break;
            case ElementType.Draw:
                RenderDrawElement(page, element);
                break;
            case ElementType.Date:
                RenderDateElement(page, element);
                break;
            case ElementType.Highlight:
                RenderHighlightElement(page, element);
                break;
            case ElementType.CheckMark:
                RenderCheckMarkElement(page, element);
                break;
            case ElementType.PageBoundary:
                RenderPageBoundaryElement(page, element);
                break;
            case ElementType.PageNumber:
                RenderPageNumberElement(page, element);
                break;
            // Add other element types as needed
            default:
                // Skip unsupported elements for now
                break;
        }
    }

    private static void RenderTextElement(PdfPage page, ExpandedElement element)
    {
        if (!element.Props.TryGetValue("text", out var textObj) || textObj is not string text)
        {
            return;
        }

        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var fontSize = GetFontSize(element.Props);

        page.DrawText(text, (float)x, (float)y, fontSize);
    }

    private static void RenderRectangleElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 100;
        var height = element.Height ?? 50;

        page.DrawRectangle((float)x, (float)y, (float)width, (float)height);
    }

    private static void RenderLineElement(PdfPage page, ExpandedElement element)
    {
        var x1 = element.X ?? 50;
        var y1 = element.Y ?? 50;
        var x2 = (element.X ?? 50) + (element.Width ?? 100);
        var y2 = element.Y ?? 50;

        page.DrawLine((float)x1, (float)y1, (float)x2, (float)y2);
    }

    private static (double Width, double Height) GetPageSize(PageSettings pageSettings)
    {
        // For now, return A4. In a real implementation, you'd map the settings to appropriate page sizes
        return (PdfPageSizes.A4Width, PdfPageSizes.A4Height);
    }

    private static string? GetStringValue(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value is string str ? str : null;
    }

    private static double? GetDoubleValue(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value is double d ? d : null;
    }

    private static int? GetIntValue(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value is int i ? i : null;
    }

    private static int GetFontSize(Dictionary<string, object> props)
    {
        if (props.TryGetValue("fontSize", out var fontSizeObj) && fontSizeObj is int fontSize)
        {
            return fontSize;
        }
        return 12; // Default font size
    }

    private static string? GetPropString(Dictionary<string, object> props, string key)
    {
        return props.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static double GetPropDouble(Dictionary<string, object> props, string key, double fallback)
    {
        if (!props.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            double number => number,
            float number => number,
            int number => number,
            long number => number,
            decimal number => (double)number,
            JsonElement { ValueKind: JsonValueKind.Number } json when json.TryGetDouble(out var number) => number,
            _ => double.TryParse(value.ToString(), out var parsed) ? parsed : fallback
        };
    }

    private static void RenderQRCodeElement(PdfPage page, ExpandedElement element)
    {
        if (!element.Props.TryGetValue("value", out var valueObj) || valueObj is not string value)
        {
            return;
        }

        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var size = element.Width ?? 100;

        // For now, render as text placeholder. In a real implementation, you'd use a QR code library
        page.DrawText($"QR: {value}", (float)x, (float)y, 10);
    }

    private static void RenderBarcodeElement(PdfPage page, ExpandedElement element)
    {
        if (!element.Props.TryGetValue("value", out var valueObj) || valueObj is not string value)
        {
            return;
        }

        var x = element.X ?? 50;
        var y = element.Y ?? 50;

        // For now, render as text placeholder. In a real implementation, you'd use a barcode library
        page.DrawText($"Barcode: {value}", (float)x, (float)y, 10);
    }

    private static void RenderSignatureElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 200;
        var height = element.Height ?? 60;

        // Draw signature line
        page.DrawLine((float)x, (float)(y + height - 10), (float)(x + width), (float)(y + height - 10));

        // Draw signature label
        if (element.Props.TryGetValue("label", out var labelObj) && labelObj is string label)
        {
            page.DrawText(label, (float)x, (float)y, 10);
        }
        else
        {
            page.DrawText("Signature", (float)x, (float)y, 10);
        }
    }

    private static void RenderRichTextElement(PdfPage page, ExpandedElement element)
    {
        if (!element.Props.TryGetValue("html", out var htmlObj) || htmlObj is not string html)
        {
            return;
        }

        var x = element.X ?? 50;
        var y = element.Y ?? 50;

        // For now, strip HTML tags and render as plain text
        // In a real implementation, you'd use an HTML-to-PDF library
        var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        page.DrawText(plainText, (float)x, (float)y, 12);
    }

    private static void RenderChartElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 220;
        var height = element.Height ?? 140;

        var chartType = "bar";
        if (element.Props.TryGetValue("chartType", out var chartTypeObj) && chartTypeObj is string typeValue)
        {
            chartType = typeValue.ToLowerInvariant();
        }

        var (labels, values) = ExtractChartSeries(element.Props);

        if (values.Count == 0)
        {
            page.DrawRectangle(x, y, width, height, lineWidth: 0.8, strokeColor: PdfColor.Gray);
            page.DrawText("Chart (no data)", x + 8, y + height - 16, 10);
            return;
        }

        switch (chartType)
        {
            case "line":
                RenderLineChart(page, x, y, width, height, labels, values);
                break;
            case "pie":
                RenderPieChart(page, x, y, width, height, labels, values);
                break;
            default:
                RenderBarChart(page, x, y, width, height, labels, values);
                break;
        }
    }

    private static void RenderWatermarkElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 80;
        var y = element.Y ?? 380;
        var content = GetPropString(element.Props, "content") ?? "WATERMARK";
        var fontSize = GetPropDouble(element.Props, "fontSize", 36);

        // Minimal renderer does not support rotated transparent text yet, so render readable fallback text.
        page.DrawText(content, x, y, (int)fontSize);
    }

    private static void RenderNoteElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 160;
        var height = element.Height ?? 90;
        var title = GetPropString(element.Props, "title") ?? "Note";
        var body = GetPropString(element.Props, "body") ?? string.Empty;

        page.DrawRectangle(x, y, width, height, lineWidth: 0.8, fill: true, strokeColor: new PdfColor(0.85, 0.56, 0.12), fillColor: new PdfColor(1, 0.95, 0.74));
        page.DrawText(title, x + 6, y + height - 16, 10);
        if (!string.IsNullOrWhiteSpace(body))
        {
            page.DrawText(body, x + 6, y + height - 32, 9);
        }
    }

    private static void RenderArrowElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 120;
        var height = element.Height ?? 40;
        var endX = x + width;
        var endY = y + height / 2;
        var startY = y + height / 2;

        page.DrawLine(x, startY, endX, endY, lineWidth: GetPropDouble(element.Props, "strokeWidth", 2), strokeColor: new PdfColor(0.86, 0.16, 0.16));
        page.DrawLine(endX, endY, endX - 10, endY + 5, lineWidth: 1.4, strokeColor: new PdfColor(0.86, 0.16, 0.16));
        page.DrawLine(endX, endY, endX - 10, endY - 5, lineWidth: 1.4, strokeColor: new PdfColor(0.86, 0.16, 0.16));
    }

    private static void RenderDrawElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 160;
        var height = element.Height ?? 70;

        // Fallback for path data until SVG path rasterization/vector replay is available.
        page.DrawRectangle(x, y, width, height, lineWidth: 0.6, strokeColor: new PdfColor(0.15, 0.39, 0.92));
        page.DrawText("Draw path", x + 8, y + height / 2, 9);
    }

    private static void RenderDateElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var value = GetPropString(element.Props, "value");
        var mode = GetPropString(element.Props, "mode") ?? "static";
        var text = mode == "render" ? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") : value;

        page.DrawText(string.IsNullOrWhiteSpace(text) ? "-" : text, x, y, (int)GetPropDouble(element.Props, "fontSize", 12));
    }

    private static void RenderHighlightElement(PdfPage page, ExpandedElement element)
    {
        page.DrawRectangle(
            element.X ?? 50,
            element.Y ?? 50,
            element.Width ?? 120,
            element.Height ?? 24,
            lineWidth: 0,
            fill: true,
            strokeColor: new PdfColor(0.99, 0.82, 0.19),
            fillColor: new PdfColor(0.99, 0.88, 0.28));
    }

    private static void RenderCheckMarkElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var label = GetPropString(element.Props, "label") ?? string.Empty;
        var state = GetPropString(element.Props, "state") ?? "checked";

        page.DrawRectangle(x, y, 14, 14, lineWidth: 0.8, strokeColor: PdfColor.Black);
        if (state == "checked")
        {
            page.DrawLine(x + 3, y + 7, x + 6, y + 3, lineWidth: 1.2, strokeColor: PdfColor.Black);
            page.DrawLine(x + 6, y + 3, x + 12, y + 11, lineWidth: 1.2, strokeColor: PdfColor.Black);
        }
        else if (state == "cross")
        {
            page.DrawLine(x + 3, y + 3, x + 11, y + 11, lineWidth: 1.2, strokeColor: PdfColor.Black);
            page.DrawLine(x + 11, y + 3, x + 3, y + 11, lineWidth: 1.2, strokeColor: PdfColor.Black);
        }

        if (!string.IsNullOrWhiteSpace(label))
        {
            page.DrawText(label, x + 20, y + 2, 10);
        }
    }

    private static void RenderPageBoundaryElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 50;
        var y = element.Y ?? 50;
        var width = element.Width ?? 300;
        var mode = GetPropString(element.Props, "mode") ?? "start";

        page.DrawLine(x, y, x + width, y, lineWidth: 0.8, strokeColor: new PdfColor(0.49, 0.23, 0.93));
        page.DrawText(mode == "end" ? "Page end" : "Page start", x + width / 2 - 24, y + 4, 8);
    }

    private static void RenderPageNumberElement(PdfPage page, ExpandedElement element)
    {
        var x = element.X ?? 280;
        var y = element.Y ?? 24;
        var prefix = GetPropString(element.Props, "prefix") ?? string.Empty;
        var suffix = GetPropString(element.Props, "suffix") ?? string.Empty;
        var format = GetPropString(element.Props, "format") ?? "pageOfTotal";
        var text = format == "pageOfTotal" ? $"{prefix}Page 1 of 1{suffix}" : $"{prefix}1{suffix}";

        page.DrawText(text, x, y, (int)GetPropDouble(element.Props, "fontSize", 10));
    }

    private static (List<string> Labels, List<double> Values) ExtractChartSeries(Dictionary<string, object> props)
    {
        var fallbackLabels = new List<string> { "Q1", "Q2", "Q3", "Q4" };
        var fallbackValues = new List<double> { 12, 19, 14, 22 };

        if (!props.TryGetValue("chartData", out var chartDataObj) || chartDataObj is null)
        {
            return (fallbackLabels, fallbackValues);
        }

        try
        {
            JsonElement root;

            if (chartDataObj is string json && !string.IsNullOrWhiteSpace(json))
            {
                using var jsonDoc = JsonDocument.Parse(json);
                root = jsonDoc.RootElement.Clone();
            }
            else if (chartDataObj is JsonElement element)
            {
                root = element;
            }
            else
            {
                var serialized = JsonSerializer.Serialize(chartDataObj);
                using var jsonDoc = JsonDocument.Parse(serialized);
                root = jsonDoc.RootElement.Clone();
            }

            var labels = new List<string>();
            var values = new List<double>();

            if (root.TryGetProperty("labels", out var labelsElement) && labelsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in labelsElement.EnumerateArray())
                {
                    labels.Add(item.ToString());
                }
            }

            if (root.TryGetProperty("datasets", out var datasetsElement)
                && datasetsElement.ValueKind == JsonValueKind.Array
                && datasetsElement.GetArrayLength() > 0)
            {
                var firstDataset = datasetsElement[0];
                if (firstDataset.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var number))
                        {
                            values.Add(number);
                        }
                        else if (double.TryParse(item.ToString(), out var parsed))
                        {
                            values.Add(parsed);
                        }
                    }
                }
            }

            if (values.Count == 0)
            {
                return (fallbackLabels, fallbackValues);
            }

            if (labels.Count < values.Count)
            {
                for (var i = labels.Count; i < values.Count; i++)
                {
                    labels.Add($"#{i + 1}");
                }
            }

            return (labels, values);
        }
        catch
        {
            return (fallbackLabels, fallbackValues);
        }
    }

    private static void RenderBarChart(PdfPage page, double x, double y, double width, double height, IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        var maxValue = Math.Max(values.Max(), 1);
        var leftPadding = 16d;
        var bottomPadding = 20d;
        var topPadding = 12d;
        var plotWidth = Math.Max(width - leftPadding - 8, 40);
        var plotHeight = Math.Max(height - topPadding - bottomPadding, 30);
        var barGap = 4d;
        var barCount = Math.Max(values.Count, 1);
        var barWidth = Math.Max((plotWidth - barGap * (barCount - 1)) / barCount, 2);

        page.DrawRectangle(x, y, width, height, lineWidth: 0.8, strokeColor: new PdfColor(0.78, 0.83, 0.9));
        page.DrawLine(x + leftPadding, y + bottomPadding, x + leftPadding + plotWidth, y + bottomPadding, lineWidth: 0.8, strokeColor: PdfColor.Gray);

        for (var i = 0; i < values.Count; i++)
        {
            var normalized = Math.Max(values[i], 0) / maxValue;
            var barHeight = Math.Max(plotHeight * normalized, 1);
            var barX = x + leftPadding + i * (barWidth + barGap);
            var barY = y + bottomPadding;
            page.DrawRectangle(
                barX,
                barY,
                barWidth,
                barHeight,
                lineWidth: 0.4,
                fill: true,
                strokeColor: new PdfColor(0.16, 0.39, 0.84),
                fillColor: new PdfColor(0.23, 0.51, 0.96));

            if (i < labels.Count)
            {
                page.DrawText(labels[i], barX, y + 6, 7);
            }
        }
    }

    private static void RenderLineChart(PdfPage page, double x, double y, double width, double height, IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        var maxValue = Math.Max(values.Max(), 1);
        var leftPadding = 16d;
        var bottomPadding = 20d;
        var topPadding = 12d;
        var plotWidth = Math.Max(width - leftPadding - 8, 40);
        var plotHeight = Math.Max(height - topPadding - bottomPadding, 30);

        page.DrawRectangle(x, y, width, height, lineWidth: 0.8, strokeColor: new PdfColor(0.78, 0.83, 0.9));
        page.DrawLine(x + leftPadding, y + bottomPadding, x + leftPadding + plotWidth, y + bottomPadding, lineWidth: 0.8, strokeColor: PdfColor.Gray);

        var points = new List<(double X, double Y)>();
        for (var i = 0; i < values.Count; i++)
        {
            var px = values.Count == 1
                ? x + leftPadding + plotWidth / 2
                : x + leftPadding + (plotWidth * i / (values.Count - 1));
            var py = y + bottomPadding + (plotHeight * Math.Max(values[i], 0) / maxValue);
            points.Add((px, py));

            page.DrawCircle(px, py, 1.6, fill: true, fillColor: new PdfColor(0.11, 0.3, 0.85), strokeColor: new PdfColor(0.11, 0.3, 0.85));
            if (i < labels.Count)
            {
                page.DrawText(labels[i], px - 6, y + 6, 7);
            }
        }

        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            page.DrawLine(previous.X, previous.Y, current.X, current.Y, lineWidth: 1.2, strokeColor: new PdfColor(0.11, 0.3, 0.85));
        }
    }

    private static void RenderPieChart(PdfPage page, double x, double y, double width, double height, IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        var total = values.Sum(v => Math.Max(v, 0));
        var fallbackTotal = total <= 0 ? 1 : total;
        var pieSize = Math.Min(width * 0.45, height - 16);
        var legendX = x + pieSize + 16;
        var legendY = y + height - 16;

        var colors = new[]
        {
            new PdfColor(0.15, 0.39, 0.92),
            new PdfColor(0.10, 0.64, 0.34),
            new PdfColor(0.96, 0.62, 0.04),
            new PdfColor(0.86, 0.16, 0.16),
            new PdfColor(0.48, 0.18, 0.74),
            new PdfColor(0.03, 0.57, 0.72)
        };

        page.DrawRectangle(x, y, width, height, lineWidth: 0.8, strokeColor: new PdfColor(0.78, 0.83, 0.9));
        page.DrawCircle(x + pieSize / 2 + 8, y + height / 2, pieSize / 2, lineWidth: 1, strokeColor: PdfColor.Gray);

        for (var i = 0; i < values.Count; i++)
        {
            var value = Math.Max(values[i], 0);
            var ratio = value / fallbackTotal;
            var barWidth = Math.Max(ratio * (width - pieSize - 28), 2);
            var rowY = legendY - (i * 12);

            page.DrawRectangle(legendX, rowY, barWidth, 8, lineWidth: 0.2, fill: true, strokeColor: colors[i % colors.Length], fillColor: colors[i % colors.Length]);
            var label = i < labels.Count ? labels[i] : $"#{i + 1}";
            page.DrawText($"{label} {(ratio * 100):0}%", legendX, rowY + 9, 7);
        }
    }
}
