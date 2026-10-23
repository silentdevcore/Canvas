using System.Text.Json;
using PVAD.Migration.Pdf.Viewer.Nutrient;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Pdf.Viewer.Nutrient.Tests;

public sealed class NutrientInstantJsonConverterTests
{
    [Fact]
    public void Convert_MapsSupportedAnnotationsAndMetadata()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                {
                  "id": "rect-1",
                  "type": "pspdfkit/shape/rectangle",
                  "pageIndex": 0,
                  "bbox": [59.5, 84.2, 119, 168.4],
                  "strokeColor": "#112233",
                  "fillColor": [240, 241, 242],
                  "opacity": 0.75,
                  "strokeWidth": 2,
                  "creatorName": "Synthetic User",
                  "createdAt": "2026-08-25T10:00:00Z",
                  "locked": true
                },
                {
                  "id": "note-1",
                  "type": "pspdfkit/comment/note",
                  "pageIndex": 0,
                  "bbox": [297.5, 421, 59.5, 84.2],
                  "text": { "value": "Review this" }
                }
              ]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(
            source,
            [new NutrientPageGeometry(595, 842)],
            "sample.pdf");

        Assert.Equal("Nutrient", result.Provider);
        Assert.Equal("nutrient-viewer", result.ProviderKey);
        Assert.Equal("compatible", result.DocumentFidelity);
        Assert.Empty(result.Diagnostics);

        using var sidecar = JsonDocument.Parse(result.AnnotationSidecarJson);
        var annotations = sidecar.RootElement.GetProperty("annotations");
        Assert.Equal(2, annotations.GetArrayLength());
        Assert.Equal("rectangle", annotations[0].GetProperty("type").GetString());
        Assert.Equal(10, annotations[0].GetProperty("xPct").GetDouble());
        Assert.Equal(10, annotations[0].GetProperty("yPct").GetDouble());
        Assert.Equal(20, annotations[0].GetProperty("widthPct").GetDouble());
        Assert.Equal(20, annotations[0].GetProperty("heightPct").GetDouble());
        Assert.Equal("#F0F1F2", annotations[0].GetProperty("fillColor").GetString());
        Assert.Equal(75, annotations[0].GetProperty("opacity").GetDouble());
        Assert.Equal("Review this", annotations[1].GetProperty("text").GetString());
    }

    [Fact]
    public void Convert_MapsRotatedAndCroppedCoordinates()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [{
                "id": "rotated",
                "type": "pspdfkit/shape/ellipse",
                "pageIndex": 0,
                "bbox": [20, 30, 40, 50]
              }]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(
            source,
            [new NutrientPageGeometry(200, 100, 90, 10, 20)]);

        using var sidecar = JsonDocument.Parse(result.AnnotationSidecarJson);
        var annotation = sidecar.RootElement.GetProperty("annotations")[0];
        Assert.Equal("circle", annotation.GetProperty("type").GetString());
        Assert.Equal(40, annotation.GetProperty("xPct").GetDouble());
        Assert.Equal(5, annotation.GetProperty("yPct").GetDouble());
        Assert.Equal(50, annotation.GetProperty("widthPct").GetDouble());
        Assert.Equal(20, annotation.GetProperty("heightPct").GetDouble());
    }

    [Fact]
    public void Convert_ReportsUnsupportedAndRedactionWithoutClaimingRemoval()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                { "id": "polygon", "type": "pspdfkit/shape/polygon", "pageIndex": 0, "bbox": [1, 1, 5, 5] },
                { "id": "redact", "type": "pspdfkit/markup/redaction", "pageIndex": 0, "bbox": [10, 10, 20, 20] }
              ]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(
            source,
            [new NutrientPageGeometry(100, 100)]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON011");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON020");
        Assert.Contains("\"type\": \"redaction\"", result.AnnotationSidecarJson);
        Assert.NotNull(result.PdfTrust);
        var trust = Assert.Single(result.PdfTrust.Evidence, item =>
            item.Capability == PdfTrustCapability.SecureRedaction);
        Assert.Equal(PdfTrustMigrationDisposition.Unsupported, trust.Disposition);
        Assert.Equal(PdfTrustSourceSemantics.VisualRedactionOnly, trust.SourceSemantics);
        Assert.Equal("false", trust.ProviderOptions["contentRemoved"]);
    }

    [Fact]
    public void Convert_RemapsDuplicateIds()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                { "id": "duplicate", "type": "pspdfkit/text", "pageIndex": 0, "bbox": [1, 1, 5, 5] },
                { "id": "duplicate", "type": "pspdfkit/text", "pageIndex": 0, "bbox": [10, 10, 5, 5] }
              ]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(
            source,
            [new NutrientPageGeometry(100, 100)]);

        Assert.Equal("duplicate-2", result.IdRemap["duplicate"]);
        Assert.Contains("\"id\": \"duplicate-2\"", result.AnnotationSidecarJson);
    }

    [Fact]
    public void Convert_PreservesInkLinesMarkupRectsLineCapsAndSafeMetadata()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                {
                  "id": "ink-1", "type": "pspdfkit/ink", "pageIndex": 0,
                  "bbox": [0, 0, 100, 100], "lineWidth": 4,
                  "lines": { "points": [[[10, 20], [20, 30]], [[60, 70], [80, 90]]], "intensities": [[0.5, 0.5], [0.5, 0.5]] },
                  "flags": ["lockedContents"], "updatedAt": "2026-08-25T11:00:00Z",
                  "subject": "Sketch", "note": "Synthetic note"
                },
                {
                  "id": "highlight-1", "type": "pspdfkit/markup/highlight", "pageIndex": 0,
                  "bbox": [10, 10, 80, 20], "rects": [[10, 10, 30, 10], [50, 10, 40, 10]]
                },
                {
                  "id": "line-1", "type": "pspdfkit/shape/line", "pageIndex": 0,
                  "bbox": [10, 40, 80, 10], "lineCaps": { "start": "circle", "end": "openArrow" }
                }
              ]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(source, [new NutrientPageGeometry(100, 100)]);
        using var sidecar = JsonDocument.Parse(result.AnnotationSidecarJson);
        var annotations = sidecar.RootElement.GetProperty("annotations");

        Assert.Equal(2, annotations[0].GetProperty("pointLines").GetArrayLength());
        Assert.Equal(10, annotations[0].GetProperty("pointLines")[0][0].GetProperty("xPct").GetDouble());
        Assert.True(annotations[0].GetProperty("locked").GetBoolean());
        Assert.Equal("Sketch", annotations[0].GetProperty("metadata").GetProperty("subject").GetString());
        Assert.Equal(2, annotations[1].GetProperty("quadPoints").GetArrayLength());
        Assert.Equal("circle", annotations[2].GetProperty("lineEndingStart").GetString());
        Assert.Equal("arrow", annotations[2].GetProperty("lineEndingEnd").GetString());
    }

    [Fact]
    public void Convert_MapsSupportedFormValuesAndDiagnosesSensitiveFields()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "formFields": [
                { "type": "pspdfkit/form-field/text", "name": "Name", "flags": ["required"], "multiLine": true, "defaultValue": "" },
                { "type": "pspdfkit/form-field/checkbox", "name": "Accepted", "options": [{ "label": "Yes", "value": "Yes" }] },
                { "type": "pspdfkit/form-field/radio", "name": "Color", "options": [{ "label": "Blue", "value": "Blue" }] },
                { "type": "pspdfkit/form-field/combobox", "name": "Country", "options": [{ "label": "CH", "value": "CH" }] },
                { "type": "pspdfkit/form-field/listbox", "name": "Tags", "options": [{ "label": "A", "value": "A" }, { "label": "B", "value": "B" }] },
                { "type": "pspdfkit/form-field/signature", "name": "Signature" }
              ],
              "formFieldValues": [
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Name", "value": "Ada" },
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Accepted", "value": ["Yes"] },
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Color", "value": ["Blue"] },
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Country", "value": "CH" },
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Tags", "value": ["A", "B"] },
                { "type": "pspdfkit/form-field-value", "v": 1, "name": "Signature", "value": "saved-signature" }
              ]
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(source, [new NutrientPageGeometry(100, 100)], "forms.pdf");
        using var payload = JsonDocument.Parse(result.FormValuePayloadJson);
        var fields = payload.RootElement.GetProperty("fields");

        Assert.Equal(5, fields.GetArrayLength());
        Assert.Equal("text", fields[0].GetProperty("kind").GetString());
        Assert.Equal("Ada", fields[0].GetProperty("value").GetString());
        Assert.True(fields[0].GetProperty("multiline").GetBoolean());
        Assert.True(fields[0].GetProperty("required").GetBoolean());
        Assert.True(fields[1].GetProperty("value").GetBoolean());
        Assert.Equal("Blue", fields[2].GetProperty("value").GetString());
        Assert.Equal(2, fields[4].GetProperty("value").GetArrayLength());
        Assert.DoesNotContain("saved-signature", result.FormValuePayloadJson);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON046");
        Assert.Equal("reviewRequired", result.DocumentFidelity);
    }

    [Fact]
    public void Convert_OmitsSignatureInkAndExecutableActions()
    {
        var source = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                { "id": "signature", "type": "pspdfkit/ink", "isSignature": true, "pageIndex": 0, "bbox": [1, 1, 10, 10], "lines": { "points": [[[1, 1], [2, 2]]], "intensities": [[0.5, 0.5]] } },
                { "id": "action", "type": "pspdfkit/shape/rectangle", "pageIndex": 0, "bbox": [1, 1, 10, 10], "action": { "type": "javaScript", "script": "secret()" } }
              ],
              "comments": [{ "text": "thread" }],
              "attachments": { "secret": { "binary": "payload", "contentType": "text/plain" } }
            }
            """;

        var result = new NutrientInstantJsonConverter().Convert(source, [new NutrientPageGeometry(100, 100)]);

        Assert.DoesNotContain("signature", result.AnnotationSidecarJson);
        Assert.DoesNotContain("secret()", result.AnnotationSidecarJson);
        Assert.DoesNotContain("payload", result.AnnotationSidecarJson);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON030");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON031");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON032");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGNUTJSON033");
    }

    [Theory]
    [InlineData("{}", "CANMIGNUTJSON004")]
    [InlineData("[]", "CANMIGNUTJSON003")]
    [InlineData("not-json", "CANMIGNUTJSON002")]
    public void Convert_RejectsMalformedOrUnsupportedDocuments(string source, string expectedCode)
    {
        var exception = Assert.Throws<NutrientInstantJsonException>(() =>
            new NutrientInstantJsonConverter().Convert(source, [new NutrientPageGeometry(100, 100)]));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void ComprehensiveFixture_MapsSupportedContentAndRemovesSensitiveSignatureData()
    {
        var result = new NutrientInstantJsonConverter().Convert(
            NutrientViewerExamples.ComprehensiveInstantJson,
            NutrientViewerExamples.ComprehensivePageGeometry,
            "booking-receipt.pdf");

        Assert.Contains("review-note", result.AnnotationSidecarJson);
        Assert.Contains("approval-box", result.AnnotationSidecarJson);
        Assert.DoesNotContain("sensitive-signature-material", result.FormValuePayloadJson);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTJSON020");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTJSON011");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTJSON046");
        Assert.Equal("reviewRequired", result.DocumentFidelity);
    }
}
