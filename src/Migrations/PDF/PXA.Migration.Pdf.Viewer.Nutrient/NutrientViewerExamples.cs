namespace PXA.Migration.Pdf.Viewer.Nutrient;

public static class NutrientViewerExamples
{
    public const string ComprehensiveTypeScript = """
        import NutrientViewer, {
          type Configuration,
          type Instance,
          type ViewState
        } from "@nutrient-sdk/viewer";

        const config: Configuration = {
          container: "#nutrient-viewer",
          document: "/documents/booking-receipt.pdf",
          locale: "en",
          theme: "LIGHT",
          initialViewState: {
            currentPageIndex: 1,
            zoom: "FIT_TO_WIDTH"
          } as ViewState,
          toolbarItems: [
            "sidebar-thumbnails",
            "pager",
            "zoom-out",
            "zoom-in",
            "search",
            "annotate",
            "form-creator",
            "print",
            "export-pdf"
          ],
          scrollMode: "PER_SPREAD",
          spreadMode: "EVEN"
        };

        NutrientViewer.load(config).then((instance: Instance) => {
          instance.addEventListener("document.loaded", () => applicationEvents.documentReady());
          instance.addEventListener("viewState.change", viewState => applicationEvents.pageChanged(viewState.currentPageIndex));
          instance.addEventListener("annotations.create", annotations => applicationEvents.annotationsCreated(annotations.size));
        });
        """;

    public const string ComprehensiveInstantJson = """
        {
          "format": "https://pspdfkit.com/instant-json/v1",
          "annotations": [
            {
              "id": "review-note",
              "type": "pspdfkit/comment/note",
              "pageIndex": 0,
              "bbox": [36, 48, 24, 24],
              "text": { "value": "Confirm customer address" },
              "creatorName": "Synthetic Reviewer",
              "createdAt": "2026-08-26T10:00:00Z"
            },
            {
              "id": "invoice-highlight",
              "type": "pspdfkit/markup/highlight",
              "pageIndex": 0,
              "bbox": [72, 180, 300, 24],
              "quadPoints": [[72, 180, 372, 180, 72, 204, 372, 204]],
              "color": [255, 214, 10],
              "opacity": 0.55
            },
            {
              "id": "approval-box",
              "type": "pspdfkit/shape/rectangle",
              "pageIndex": 1,
              "bbox": [52, 90, 220, 96],
              "strokeColor": [30, 132, 73],
              "fillColor": [232, 245, 233],
              "strokeWidth": 2
            },
            {
              "id": "visual-redaction",
              "type": "pspdfkit/markup/redaction",
              "pageIndex": 1,
              "bbox": [300, 420, 180, 30],
              "text": { "value": "Review before applying" }
            },
            {
              "id": "unsupported-attachment",
              "type": "pspdfkit/file-attachment",
              "pageIndex": 1,
              "bbox": [500, 700, 24, 24]
            }
          ],
          "formFields": [
            { "name": "customerName", "type": "pspdfkit/form-field/text", "flags": ["required"] },
            { "name": "approved", "type": "pspdfkit/form-field/checkbox" },
            { "name": "country", "type": "pspdfkit/form-field/combobox", "options": [{ "label": "Switzerland", "value": "CH" }, { "label": "Germany", "value": "DE" }, { "label": "France", "value": "FR" }] },
            { "name": "signature", "type": "pspdfkit/form-field/signature" }
          ],
          "formFieldValues": [
            { "type": "pspdfkit/form-field-value", "v": 1, "name": "customerName", "value": "Example Customer" },
            { "type": "pspdfkit/form-field-value", "v": 1, "name": "approved", "value": ["Yes"] },
            { "type": "pspdfkit/form-field-value", "v": 1, "name": "country", "value": "CH" },
            { "type": "pspdfkit/form-field-value", "v": 1, "name": "signature", "value": "sensitive-signature-material-must-not-migrate" }
          ]
        }
        """;

    public static IReadOnlyList<NutrientPageGeometry> ComprehensivePageGeometry { get; } =
    [
        new(595, 842),
        new(842, 595, Rotation: 90, CropLeft: 12, CropTop: 18)
    ];
}
