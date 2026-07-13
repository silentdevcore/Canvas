# Migration Namespace Taxonomy

## Summary

Tracker for restructuring migration projects and namespaces so that each provider clearly communicates:

`PXA.Migration.<Domain>.<Kind>.<Provider>`

Examples:

- `PXA.Migration.Pdf.Code.Aspose`
- `PXA.Migration.Report.Designer.DevExpress`
- `PXA.Migration.Spreadsheet.Code.Aspose`
- `PXA.Migration.Spreadsheet.Datasource.Aspose` when a real datasource/file-format provider exists

Implementation keeps legacy provider IDs and public routes compatible. For `/api/migration/frameworks`, legacy
`kind` remains the current target-domain field (`pdf` or `spreadsheet`); the canonical taxonomy is exposed as
`domain`, `migrationKind`, and `provider`.

## Naming Rules

- [x] Use `Pdf`, not `PDF`.
- [x] Use `Spreadsheet`.
- [x] Use `Datasource`.
- [x] Use `Code` for source-code migration from third-party APIs to PXA APIs.
- [x] Use `Designer` for report/designer layout migration into editable PXA design JSON.
- [x] Use `Datasource` for file/data import migration flows only when a concrete backend provider exists.
- [x] Keep shared abstractions outside provider-specific taxonomy.

## PDF Code Migration Targets

- [x] `PXA.Migration.ActivePdf` -> `PXA.Migration.Pdf.Code.ActivePdf`
- [x] `PXA.Migration.Apryse` -> `PXA.Migration.Pdf.Code.Apryse`
- [x] `PXA.Migration.AsposePdf` -> `PXA.Migration.Pdf.Code.Aspose`
- [x] `PXA.Migration.DevExpressPdf` -> `PXA.Migration.Pdf.Code.DevExpress`
- [x] `PXA.Migration.DsPdf` -> `PXA.Migration.Pdf.Code.DsPdf`
- [x] `PXA.Migration.FoxitPdf` -> `PXA.Migration.Pdf.Code.Foxit`
- [x] `PXA.Migration.GemBoxPdf` -> `PXA.Migration.Pdf.Code.GemBox`
- [x] `PXA.Migration.IronPdf` -> `PXA.Migration.Pdf.Code.IronPdf`
- [x] `PXA.Migration.iText7` -> `PXA.Migration.Pdf.Code.IText7`
- [x] `PXA.Migration.LeadtoolsPdf` -> `PXA.Migration.Pdf.Code.Leadtools`
- [x] `PXA.Migration.PdfKitNet` -> `PXA.Migration.Pdf.Code.PdfKitNet`
- [x] `PXA.Migration.PdfTools` -> `PXA.Migration.Pdf.Code.PdfTools`
- [x] `PXA.Migration.PdfToolsToolbox` -> `PXA.Migration.Pdf.Code.PdfToolsToolbox`
- [x] `PXA.Migration.SpirePdf` -> `PXA.Migration.Pdf.Code.Spire`
- [x] `PXA.Migration.SyncfusionPdf` -> `PXA.Migration.Pdf.Code.Syncfusion`

## Spreadsheet Code Migration Targets

- [x] `PXA.Migration.AsposeCells` -> `PXA.Migration.Spreadsheet.Code.Aspose`
- [x] `PXA.Migration.ClosedXmlSpreadsheet` -> `PXA.Migration.Spreadsheet.Code.ClosedXml`
- [x] `PXA.Migration.EpplusSpreadsheet` -> `PXA.Migration.Spreadsheet.Code.Epplus`
- [x] `PXA.Migration.GemBoxSpreadsheet` -> `PXA.Migration.Spreadsheet.Code.GemBox`
- [x] `PXA.Migration.Npoi` -> `PXA.Migration.Spreadsheet.Code.Npoi`
- [x] `PXA.Migration.SpireXls` -> `PXA.Migration.Spreadsheet.Code.Spire`
- [x] `PXA.Migration.SpreadsheetLight` -> `PXA.Migration.Spreadsheet.Code.SpreadsheetLight`
- [x] `PXA.Migration.SyncfusionXlsIo` -> `PXA.Migration.Spreadsheet.Code.Syncfusion`

## Report Designer Migration Targets

- [x] `PXA.Migration.ActiveReportsJs` -> `PXA.Migration.Report.Designer.ActiveReportsJs`
- [x] `PXA.Migration.DevExpressReport` -> `PXA.Migration.Report.Designer.DevExpress`
- [x] `PXA.Migration.FastReport` -> `PXA.Migration.Report.Designer.FastReport`
- [x] `PXA.Migration.JasperReports` -> `PXA.Migration.Report.Designer.JasperReports`
- [x] `PXA.Migration.Rdl` -> `PXA.Migration.Report.Designer.Rdl`
- [x] `PXA.Migration.Rpx` -> `PXA.Migration.Report.Designer.Rpx`
- [x] `PXA.Migration.Stimulsoft` -> `PXA.Migration.Report.Designer.Stimulsoft`
- [x] `PXA.Migration.Telerik` -> `PXA.Migration.Report.Designer.Telerik`

## Reserved Datasource Structure

- [x] Reserve `PXA.Migration.Spreadsheet.Datasource.*` for backend spreadsheet datasource/file import providers.
- [x] Do not create fake datasource provider projects until a concrete backend migrator exists.
- [x] Keep current UI route `/migrations/spreadsheet/datasource` compatible.
- [x] Keep document/file importers separate from source-code migration projects unless a dedicated migration provider is introduced.

## Shared And Aggregator Projects

- [x] Keep `PXA.Migration.Abstractions`.
- [x] Keep `PXA.Migration.Roslyn`.
- [x] Keep aggregator `PXA.Migration.Pdf`.
- [x] Keep aggregator `PXA.Migration.Spreadsheet`.
- [x] Keep aggregator `PXA.Migration.Report`.
- [x] Update aggregators to reference the new provider namespaces after implementation.

## Physical Project Layout

- [x] Use `src/Migrations` as the physical root for migration projects.
- [x] Move shared migration infrastructure to `src/Migrations/Common`:
  - `PXA.Migration.Abstractions`
  - `PXA.Migration.Roslyn`
- [x] Move PDF code migration projects to `src/Migrations/PDF`.
- [x] Move report designer migration projects to `src/Migrations/Report`.
- [x] Move spreadsheet code migration projects to `src/Migrations/Spreadsheet`.
- [x] Keep project names and namespaces stable while changing physical folders.

## Remaining Source Project Layout

- [x] Use physical folders for non-migration projects while keeping namespaces stable.
- [x] Group core/application projects under `src/Core`:
  - `PXA.Core`
  - `PXA.Domain`
  - `PXA.Application`
- [x] Group generation projects under `src/Generation`:
  - `PXA.Generator`
  - `PXA.Pdf`
- [x] Group importer projects under `src/Importing`:
  - `PXA.Importer`
  - `PXA.FileImporter`
  - `PXA.FileImporter.ImageAnalysis`
  - `PXA.FileImporter.ImageOcr`
  - `PXA.FileImporter.ImageOcr.Worker`
- [x] Group infrastructure projects under `src/Infrastructure`:
  - `PXA.Infrastructure.Converters`
  - `PXA.Infrastructure.Pdf`
  - `PXA.Infrastructure.Spreadsheet`
  - `PXA.Infrastructure.Word`
- [x] Keep `PXA.Infrastructure.Pdf` under `src/Infrastructure`, because the namespace represents the technical implementation layer.

## Compatibility Rules

- [x] Existing provider IDs continue to work as aliases:
  - `aspose-pdf`
  - `devexpress-report`
  - `aspose-cells`
  - all other current PDF, spreadsheet, and report provider IDs
- [x] Existing endpoints remain compatible:
  - `POST /api/migration/convert`
  - `POST /api/migration/preview`
  - `POST /api/migration/report-to-design`
- [x] `/api/migration/frameworks` keeps existing fields and adds canonical taxonomy metadata:
  - legacy `kind` (`pdf` or `spreadsheet`)
  - `domain`
  - `migrationKind`
  - `provider`
- [x] Do not require UI users to change existing selections during the rename.

## UI And Documentation Tasks

- [x] Update UI grouping labels:
  - PDF: `Code Migration`
  - Spreadsheet: `Code Migration`, `Datasource Migration`
  - Report designers: `Report Designer Migration`
- [x] Update docs/checklists to explain the new taxonomy.
- [x] Add old-to-new provider mapping in migration documentation.
- [x] Avoid describing report designer migration as PDF designer migration unless the source technology is actually PDF-specific.

## Test Plan

- [x] `dotnet build PXA.sln --no-restore --disable-build-servers -m:1`
  - Current result: passed with warnings, 0 errors.
  - Restore needed to run outside the sandbox because sandboxed restore hung.
- [x] Static checks:
  - `dotnet sln PXA.sln list` shows the new taxonomy project paths.
  - No old flat migration namespace references remain in `src`, `tests`, or `PXA.WebApi`.
  - No old provider project directories remain under `src` or `tests`.
- [x] API contract test updated for taxonomy metadata:
  - `/api/migration/frameworks` returns legacy `kind`, plus `domain`, `migrationKind`, `provider`.
- [x] `npm run build` in `ui-designer-v2`.
- [x] Targeted .NET migration/API tests:
  - `PXA.Api.Tests` filtered to `MigrationControllerTests|MigrationServiceTests`: 16 passed.
  - `PXA.Migration.Pdf.Tests`: 18 passed.
  - `PXA.Migration.Spreadsheet.Tests`: 19 passed.
  - `PXA.Migration.Report.Tests`: 20 passed.
- [x] Full .NET test pass after restore succeeds.
  - Current result: `dotnet test PXA.sln --no-restore --disable-build-servers -m:1` passed.
- [x] Smoke test PDF code migration with `aspose-pdf`.
  - Added API smoke coverage for provider-key alias routing through `/api/pxa/migration/convert`.
- [x] Smoke test spreadsheet code migration with `aspose-cells`.
  - Added API smoke coverage for provider-key alias routing through `/api/pxa/migration/convert`.
- [x] Smoke test designer migration with DevExpress and RDL samples.
  - Added API smoke coverage for `/api/pxa/migration/report-to-design` with DevExpress `.repx` and RDL XML samples.
  - Verified with `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter MigrationControllerTests` (6 passed).

## Assumptions

- Provider IDs remain backward compatible.
- Report designer migrations belong under `Report.Designer`, not `Pdf.Designer`.
- Datasource provider projects are added only when real backend datasource/file migration providers exist.
