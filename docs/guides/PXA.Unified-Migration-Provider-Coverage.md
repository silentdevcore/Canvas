# PXA Unified Migration Provider Coverage

This report mirrors `MigrationProviderCatalog`. Canonical provider paths use
`{domain}/{kind}/{provider}`. Every concrete provider has a dedicated test project;
`report/designer/auto` is a compatibility detector and is not counted as a provider.

## PDF Code

Input is C# (`.cs`), output is PXA C#, and server preview is available for every row.

| Provider | Status | Test project | Documentation ID |
|---|---|---|---|
| `pdf/code/active-pdf` | pilot | `PXA.Migration.Pdf.Code.ActivePdf.Tests` | `pdf-code-migration-active-pdf` |
| `pdf/code/apryse` | full | `PXA.Migration.Pdf.Code.Apryse.Tests` | `pdf-code-migration-apryse` |
| `pdf/code/aspose-pdf` | full | `PXA.Migration.Pdf.Code.Aspose.Tests` | `pdf-code-migration-aspose-pdf` |
| `pdf/code/devexpress-pdf` | full | `PXA.Migration.Pdf.Code.DevExpress.Tests` | `pdf-code-migration-devexpress-pdf` |
| `pdf/code/dspdf` | full | `PXA.Migration.Pdf.Code.DsPdf.Tests` | `pdf-code-migration-dspdf` |
| `pdf/code/foxit-pdf` | full | `PXA.Migration.Pdf.Code.Foxit.Tests` | `pdf-code-migration-foxit-pdf` |
| `pdf/code/gembox-pdf` | full | `PXA.Migration.Pdf.Code.GemBox.Tests` | `pdf-code-migration-gembox-pdf` |
| `pdf/code/iron-pdf` | pilot | `PXA.Migration.Pdf.Code.IronPdf.Tests` | `pdf-code-migration-iron-pdf` |
| `pdf/code/itext7` | full | `PXA.Migration.Pdf.Code.IText7.Tests` | `pdf-code-migration-itext7` |
| `pdf/code/leadtools-pdf` | full | `PXA.Migration.Pdf.Code.Leadtools.Tests` | `pdf-code-migration-leadtools-pdf` |
| `pdf/code/nutrient` | beta | `PXA.Migration.Pdf.Code.Nutrient.Tests` | `pdf-code-migration-nutrient` |
| `pdf/code/pdfkitnet` | full | `PXA.Migration.Pdf.Code.PdfKitNet.Tests` | `pdf-code-migration-pdfkitnet` |
| `pdf/code/pdftools` | pilot | `PXA.Migration.Pdf.Code.PdfTools.Tests` | `pdf-code-migration-pdftools` |
| `pdf/code/pdftools-toolbox` | pilot | `PXA.Migration.Pdf.Code.PdfToolsToolbox.Tests` | `pdf-code-migration-pdftools-toolbox` |
| `pdf/code/spire-pdf` | full | `PXA.Migration.Pdf.Code.Spire.Tests` | `pdf-code-migration-spire-pdf` |
| `pdf/code/syncfusion-pdf` | full | `PXA.Migration.Pdf.Code.Syncfusion.Tests` | `pdf-code-migration-syncfusion-pdf` |

## PDF Viewer

| Provider | Status | Inputs | Outputs | Preview | Test project | Documentation ID |
|---|---|---|---|---|---|---|
| `pdf/viewer/nutrient-viewer` | beta | JavaScript, TypeScript, Instant JSON | importable PXA Viewer package | PXA Viewer | `PXA.Migration.Pdf.Viewer.Nutrient.Tests` | `nutrient-viewer-migration` |

The Viewer route is the lower-level SDK and compatibility contract. The Designer
uses the combined workflow below so customers receive one editable document.

## PDF Designer

| Provider | Status | Inputs | Output | Preview | API tests | Documentation ID |
|---|---|---|---|---|---|---|
| `pdf/designer/nutrient-viewer` | beta | original PDF plus JavaScript, TypeScript, or Instant JSON | editable `DesignExportDto` | backend PDF and Open in Designer | `PXA.Api.Tests/MigrationControllerTests` | `nutrient-viewer-designer-migration` |

## Spreadsheet Code

Input is C# (`.cs`), output is PXA Spreadsheet C#, and HTML preview is available for every row.

| Provider | Status | Test project | Documentation ID |
|---|---|---|---|
| `spreadsheet/code/aspose-cells` | full | `PXA.Migration.Spreadsheet.Code.Aspose.Tests` | `spreadsheet-code-migration-aspose-cells` |
| `spreadsheet/code/closedxml` | full | `PXA.Migration.Spreadsheet.Code.ClosedXml.Tests` | `spreadsheet-code-migration-closedxml` |
| `spreadsheet/code/epplus` | full | `PXA.Migration.Spreadsheet.Code.Epplus.Tests` | `spreadsheet-code-migration-epplus` |
| `spreadsheet/code/gembox-spreadsheet` | full | `PXA.Migration.Spreadsheet.Code.GemBox.Tests` | `spreadsheet-code-migration-gembox-spreadsheet` |
| `spreadsheet/code/npoi` | full | `PXA.Migration.Spreadsheet.Code.Npoi.Tests` | `spreadsheet-code-migration-npoi` |
| `spreadsheet/code/spire-xls` | full | `PXA.Migration.Spreadsheet.Code.Spire.Tests` | `spreadsheet-code-migration-spire-xls` |
| `spreadsheet/code/spreadsheetlight` | full | `PXA.Migration.Spreadsheet.Code.SpreadsheetLight.Tests` | `spreadsheet-code-migration-spreadsheetlight` |
| `spreadsheet/code/syncfusion-xlsio` | full | `PXA.Migration.Spreadsheet.Code.Syncfusion.Tests` | `spreadsheet-code-migration-syncfusion-xlsio` |

## Report Designer

Input is the listed report source or package, output is editable `DesignExportDto` JSON,
and server preview is not advertised by the catalog.

| Provider | Status | Inputs | Test project | Documentation ID |
|---|---|---|---|---|
| `report/designer/activereports-js` | full | `.json` | `PXA.Migration.Report.Designer.ActiveReportsJs.Tests` | `report-designer-migration-activereports-js` |
| `report/designer/devexpress-report` | full | `.cs`, `.repx`, `.resx` | `PXA.Migration.Report.Designer.DevExpress.Tests` | `report-designer-migration-devexpress-report` |
| `report/designer/fastreport` | full | `.frx` | `PXA.Migration.Report.Designer.FastReport.Tests` | `report-designer-migration-fastreport` |
| `report/designer/jasperreports` | full | `.jrxml` | `PXA.Migration.Report.Designer.JasperReports.Tests` | `report-designer-migration-jasperreports` |
| `report/designer/rdl` | full | `.rdl`, `.rdlc`, `.rdlx` | `PXA.Migration.Report.Designer.Rdl.Tests` | `report-designer-migration-rdl` |
| `report/designer/rpx` | full | `.rpx` | `PXA.Migration.Report.Designer.Rpx.Tests` | `report-designer-migration-rpx` |
| `report/designer/stimulsoft` | full | `.mrt` | `PXA.Migration.Report.Designer.Stimulsoft.Tests` | `report-designer-migration-stimulsoft` |
| `report/designer/telerik` | full | `.trdx`, `.trdp` | `PXA.Migration.Report.Designer.Telerik.Tests` | `report-designer-migration-telerik` |

## Compatibility

- `report/designer/auto` detects a report provider for legacy integrations.
- Historical provider aliases are accepted as input and normalized to canonical keys.
- Legacy `/api/migration/*` routes remain adapters for one major release and advertise
  deprecation, sunset, and successor information.
