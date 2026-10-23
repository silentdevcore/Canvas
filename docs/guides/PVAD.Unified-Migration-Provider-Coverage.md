# PVAD Unified Migration Provider Coverage

This report mirrors `MigrationProviderCatalog`. Canonical provider paths use
`{domain}/{kind}/{provider}`. Every concrete provider has a dedicated test project;
`report/designer/auto` is a compatibility detector and is not counted as a provider.

## PDF Code

Input is C# (`.cs`), output is PVAD C#, and server preview is available for every row.

| Provider | Status | Test project | Documentation ID |
|---|---|---|---|
| `pdf/code/active-pdf` | pilot | `PVAD.Migration.Pdf.Code.ActivePdf.Tests` | `pdf-code-migration-active-pdf` |
| `pdf/code/apryse` | full | `PVAD.Migration.Pdf.Code.Apryse.Tests` | `pdf-code-migration-apryse` |
| `pdf/code/aspose-pdf` | full | `PVAD.Migration.Pdf.Code.Aspose.Tests` | `pdf-code-migration-aspose-pdf` |
| `pdf/code/devexpress-pdf` | full | `PVAD.Migration.Pdf.Code.DevExpress.Tests` | `pdf-code-migration-devexpress-pdf` |
| `pdf/code/dspdf` | full | `PVAD.Migration.Pdf.Code.DsPdf.Tests` | `pdf-code-migration-dspdf` |
| `pdf/code/foxit-pdf` | full | `PVAD.Migration.Pdf.Code.Foxit.Tests` | `pdf-code-migration-foxit-pdf` |
| `pdf/code/gembox-pdf` | full | `PVAD.Migration.Pdf.Code.GemBox.Tests` | `pdf-code-migration-gembox-pdf` |
| `pdf/code/iron-pdf` | pilot | `PVAD.Migration.Pdf.Code.IronPdf.Tests` | `pdf-code-migration-iron-pdf` |
| `pdf/code/itext7` | full | `PVAD.Migration.Pdf.Code.IText7.Tests` | `pdf-code-migration-itext7` |
| `pdf/code/leadtools-pdf` | full | `PVAD.Migration.Pdf.Code.Leadtools.Tests` | `pdf-code-migration-leadtools-pdf` |
| `pdf/code/nutrient` | beta | `PVAD.Migration.Pdf.Code.Nutrient.Tests` | `pdf-code-migration-nutrient` |
| `pdf/code/pdfkitnet` | full | `PVAD.Migration.Pdf.Code.PdfKitNet.Tests` | `pdf-code-migration-pdfkitnet` |
| `pdf/code/pdftools` | pilot | `PVAD.Migration.Pdf.Code.PdfTools.Tests` | `pdf-code-migration-pdftools` |
| `pdf/code/pdftools-toolbox` | pilot | `PVAD.Migration.Pdf.Code.PdfToolsToolbox.Tests` | `pdf-code-migration-pdftools-toolbox` |
| `pdf/code/spire-pdf` | full | `PVAD.Migration.Pdf.Code.Spire.Tests` | `pdf-code-migration-spire-pdf` |
| `pdf/code/syncfusion-pdf` | full | `PVAD.Migration.Pdf.Code.Syncfusion.Tests` | `pdf-code-migration-syncfusion-pdf` |

## PDF Viewer

| Provider | Status | Inputs | Outputs | Preview | Test project | Documentation ID |
|---|---|---|---|---|---|---|
| `pdf/viewer/nutrient-viewer` | beta | JavaScript, TypeScript, Instant JSON | importable PVAD Viewer package | PVAD Viewer | `PVAD.Migration.Pdf.Viewer.Nutrient.Tests` | `nutrient-viewer-migration` |

The Viewer route is the lower-level SDK and compatibility contract. The Designer
uses the combined workflow below so customers receive one editable document.

## PDF Designer

| Provider | Status | Inputs | Output | Preview | API tests | Documentation ID |
|---|---|---|---|---|---|---|
| `pdf/designer/nutrient-viewer` | beta | original PDF plus JavaScript, TypeScript, or Instant JSON | editable `DesignExportDto` | backend PDF and Open in Designer | `PVAD.Api.Tests/MigrationControllerTests` | `nutrient-viewer-designer-migration` |

## Spreadsheet Code

Input is C# (`.cs`), output is PVAD Spreadsheet C#, and HTML preview is available for every row.

| Provider | Status | Test project | Documentation ID |
|---|---|---|---|
| `spreadsheet/code/aspose-cells` | full | `PVAD.Migration.Spreadsheet.Code.Aspose.Tests` | `spreadsheet-code-migration-aspose-cells` |
| `spreadsheet/code/closedxml` | full | `PVAD.Migration.Spreadsheet.Code.ClosedXml.Tests` | `spreadsheet-code-migration-closedxml` |
| `spreadsheet/code/epplus` | full | `PVAD.Migration.Spreadsheet.Code.Epplus.Tests` | `spreadsheet-code-migration-epplus` |
| `spreadsheet/code/gembox-spreadsheet` | full | `PVAD.Migration.Spreadsheet.Code.GemBox.Tests` | `spreadsheet-code-migration-gembox-spreadsheet` |
| `spreadsheet/code/npoi` | full | `PVAD.Migration.Spreadsheet.Code.Npoi.Tests` | `spreadsheet-code-migration-npoi` |
| `spreadsheet/code/spire-xls` | full | `PVAD.Migration.Spreadsheet.Code.Spire.Tests` | `spreadsheet-code-migration-spire-xls` |
| `spreadsheet/code/spreadsheetlight` | full | `PVAD.Migration.Spreadsheet.Code.SpreadsheetLight.Tests` | `spreadsheet-code-migration-spreadsheetlight` |
| `spreadsheet/code/syncfusion-xlsio` | full | `PVAD.Migration.Spreadsheet.Code.Syncfusion.Tests` | `spreadsheet-code-migration-syncfusion-xlsio` |

## Report Designer

Input is the listed report source or package, output is editable `DesignExportDto` JSON,
and server preview is not advertised by the catalog.

| Provider | Status | Inputs | Test project | Documentation ID |
|---|---|---|---|---|
| `report/designer/activereports-js` | full | `.json` | `PVAD.Migration.Report.Designer.ActiveReportsJs.Tests` | `report-designer-migration-activereports-js` |
| `report/designer/devexpress-report` | full | `.cs`, `.repx`, `.resx` | `PVAD.Migration.Report.Designer.DevExpress.Tests` | `report-designer-migration-devexpress-report` |
| `report/designer/fastreport` | full | `.frx` | `PVAD.Migration.Report.Designer.FastReport.Tests` | `report-designer-migration-fastreport` |
| `report/designer/jasperreports` | full | `.jrxml` | `PVAD.Migration.Report.Designer.JasperReports.Tests` | `report-designer-migration-jasperreports` |
| `report/designer/rdl` | full | `.rdl`, `.rdlc`, `.rdlx` | `PVAD.Migration.Report.Designer.Rdl.Tests` | `report-designer-migration-rdl` |
| `report/designer/rpx` | full | `.rpx` | `PVAD.Migration.Report.Designer.Rpx.Tests` | `report-designer-migration-rpx` |
| `report/designer/stimulsoft` | full | `.mrt` | `PVAD.Migration.Report.Designer.Stimulsoft.Tests` | `report-designer-migration-stimulsoft` |
| `report/designer/telerik` | full | `.trdx`, `.trdp` | `PVAD.Migration.Report.Designer.Telerik.Tests` | `report-designer-migration-telerik` |

## Compatibility

- `report/designer/auto` detects a report provider for legacy integrations.
- Historical provider aliases are accepted as input and normalized to canonical keys.
- Legacy `/api/migration/*` routes remain adapters for one major release and advertise
  deprecation, sunset, and successor information.

## Electronic-invoice capabilities per PDF code provider

This section mirrors `PdfEInvoiceMigrationCatalog` and is drift-guarded by
`PVAD.Migration.Pdf.Tests/PdfEInvoiceMigrationCoverageReportTests`.

The mappings are keyed on API identifiers a customer's source may contain, not on a
claim about which invoice features each vendor ships. `automatic` means the call
transfers onto a PVAD API with no judgement left; `reviewRequired` means a person has
to confirm something the source leaves implicit; `unsupported` means no invoice API
of that kind was identified and PVAD will not invent one. Nothing in this table is a
conformance claim — conformance is established by the pinned official artifact sets.

- Attachment and extraction are `automatic` wherever the provider has the API,
  because embedding and reading back an embedded file is byte-preserving on both
  sides.
- Factur-X, ZUGFeRD and XRechnung generation is never `automatic`: the standard
  version and profile are a business obligation the source code usually leaves
  implicit.
- Invoice validation is never `automatic`: the detected call may validate PDF/A
  structure rather than invoice rules, and a migration must not turn that into an
  invoice-conformance claim.

<!-- BEGIN GENERATED: e-invoice-provider-coverage -->

| Provider | Factur-X | ZUGFeRD | XRechnung | PDF/A-3 attachment | Extraction | Validation |
|---|---|---|---|---|---|---|
| `active-pdf` | unsupported | unsupported | unsupported | automatic | unsupported | unsupported |
| `apryse` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `aspose-pdf` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `devexpress-pdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `dspdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `foxit-pdf` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `gembox-pdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `iron-pdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `itext7` | reviewRequired | reviewRequired | unsupported | automatic | automatic | unsupported |
| `leadtools-pdf` | unsupported | unsupported | unsupported | automatic | unsupported | unsupported |
| `nutrient` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `pdfkitnet` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `pdftools` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `pdftools-toolbox` | unsupported | unsupported | unsupported | automatic | automatic | reviewRequired |
| `spire-pdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
| `syncfusion-pdf` | unsupported | unsupported | unsupported | automatic | automatic | unsupported |
<!-- END GENERATED: e-invoice-provider-coverage -->
