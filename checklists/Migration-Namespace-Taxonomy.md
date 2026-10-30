# Migration Namespace Taxonomy

## Summary

Tracker for restructuring migration projects and namespaces so that each provider clearly communicates:

`PVAD.Migration.<Domain>.<Kind>.<Provider>`

Examples:

- `PVAD.Migration.Pdf.Code.Aspose`
- `PVAD.Migration.Pdf.Viewer.Nutrient`
- `PVAD.Migration.Report.Designer.DevExpress`
- `PVAD.Migration.Spreadsheet.Code.Aspose`
- `PVAD.Migration.Spreadsheet.Datasource.Aspose` when a real datasource/file-format provider exists

Implementation keeps legacy provider IDs and public routes compatible. For `/api/migration/frameworks`, legacy
`kind` remains the current target-domain field (`pdf` or `spreadsheet`); the canonical taxonomy is exposed as
`domain`, `migrationKind`, and `provider`.

The physical namespace work in this checklist is the implemented foundation for
[`PVAD.Unified-Migration-Frameworks.md`](PVAD.Unified-Migration-Frameworks.md), which owns the canonical registry,
v1 API and permissions. Technical domains remain separate in the API, while Report Designer is intentionally
presented inside the PDF product experience at `/pdf/migrations/designer`.

## Naming Rules

- [x] Use `Pdf`, not `PDF`.
- [x] Use `Spreadsheet`.
- [x] Use `Datasource`.
- [x] Use `Code` for source-code migration from third-party APIs to PVADE APIs.
- [x] Use `Designer` for report/designer layout migration into editable PVADE design JSON.
- [x] Use `Viewer` for migration of third-party viewer integrations and interchange data into PVADE Viewer contracts.
- [x] Use `Datasource` for file/data import migration flows only when a concrete backend provider exists.
- [x] Keep shared abstractions outside provider-specific taxonomy.

## PDF Code Migration Targets

- [x] `PVAD.Migration.ActivePdf` -> `PVAD.Migration.Pdf.Code.ActivePdf`
- [x] `PVAD.Migration.Apryse` -> `PVAD.Migration.Pdf.Code.Apryse`
- [x] `PVAD.Migration.AsposePdf` -> `PVAD.Migration.Pdf.Code.Aspose`
- [x] `PVAD.Migration.DevExpressPdf` -> `PVAD.Migration.Pdf.Code.DevExpress`
- [x] `PVAD.Migration.DsPdf` -> `PVAD.Migration.Pdf.Code.DsPdf`
- [x] `PVAD.Migration.FoxitPdf` -> `PVAD.Migration.Pdf.Code.Foxit`
- [x] `PVAD.Migration.GemBoxPdf` -> `PVAD.Migration.Pdf.Code.GemBox`
- [x] `PVAD.Migration.IronPdf` -> `PVAD.Migration.Pdf.Code.IronPdf`
- [x] `PVAD.Migration.iText7` -> `PVAD.Migration.Pdf.Code.IText7`
- [x] `PVAD.Migration.LeadtoolsPdf` -> `PVAD.Migration.Pdf.Code.Leadtools`
- [x] Add `PVAD.Migration.Pdf.Code.Nutrient` for Nutrient .NET and legacy GdPicture.NET code migration.
- [x] `PVAD.Migration.PdfKitNet` -> `PVAD.Migration.Pdf.Code.PdfKitNet`
- [x] `PVAD.Migration.PdfTools` -> `PVAD.Migration.Pdf.Code.PdfTools`
- [x] `PVAD.Migration.PdfToolsToolbox` -> `PVAD.Migration.Pdf.Code.PdfToolsToolbox`
- [x] `PVAD.Migration.SpirePdf` -> `PVAD.Migration.Pdf.Code.Spire`
- [x] `PVAD.Migration.SyncfusionPdf` -> `PVAD.Migration.Pdf.Code.Syncfusion`

## PDF Viewer Migration Targets

- [x] Add `PVAD.Migration.Pdf.Viewer.Nutrient` for Nutrient Web SDK and legacy PSPDFKit Viewer migration.
- [x] Use canonical code provider key `nutrient` and Viewer provider key `nutrient-viewer`.
- [x] Keep Nutrient/PSPDFKit separate from Apryse/PDFTron; they are unrelated provider families.
- [x] Accept historical product names only as input aliases and return canonical Nutrient metadata.

## Spreadsheet Code Migration Targets

- [x] `PVAD.Migration.AsposeCells` -> `PVAD.Migration.Spreadsheet.Code.Aspose`
- [x] `PVAD.Migration.ClosedXmlSpreadsheet` -> `PVAD.Migration.Spreadsheet.Code.ClosedXml`
- [x] `PVAD.Migration.EpplusSpreadsheet` -> `PVAD.Migration.Spreadsheet.Code.Epplus`
- [x] `PVAD.Migration.GemBoxSpreadsheet` -> `PVAD.Migration.Spreadsheet.Code.GemBox`
- [x] `PVAD.Migration.Npoi` -> `PVAD.Migration.Spreadsheet.Code.Npoi`
- [x] `PVAD.Migration.SpireXls` -> `PVAD.Migration.Spreadsheet.Code.Spire`
- [x] `PVAD.Migration.SpreadsheetLight` -> `PVAD.Migration.Spreadsheet.Code.SpreadsheetLight`
- [x] `PVAD.Migration.SyncfusionXlsIo` -> `PVAD.Migration.Spreadsheet.Code.Syncfusion`

## Report Designer Migration Targets

- [x] `PVAD.Migration.ActiveReportsJs` -> `PVAD.Migration.Report.Designer.ActiveReportsJs`
- [x] `PVAD.Migration.DevExpressReport` -> `PVAD.Migration.Report.Designer.DevExpress`
- [x] `PVAD.Migration.FastReport` -> `PVAD.Migration.Report.Designer.FastReport`
- [x] `PVAD.Migration.JasperReports` -> `PVAD.Migration.Report.Designer.JasperReports`
- [x] `PVAD.Migration.Rdl` -> `PVAD.Migration.Report.Designer.Rdl`
- [x] `PVAD.Migration.Rpx` -> `PVAD.Migration.Report.Designer.Rpx`
- [x] `PVAD.Migration.Stimulsoft` -> `PVAD.Migration.Report.Designer.Stimulsoft`
- [x] `PVAD.Migration.Telerik` -> `PVAD.Migration.Report.Designer.Telerik`

## Reserved Datasource Structure

- [x] Reserve `PVAD.Migration.Spreadsheet.Datasource.*` for backend spreadsheet datasource/file import providers.
- [x] Do not create fake datasource provider projects until a concrete backend migrator exists.
- [x] Keep current UI route `/migrations/spreadsheet/datasource` compatible.
- [x] Keep document/file importers separate from source-code migration projects unless a dedicated migration provider is introduced.

## Shared And Aggregator Projects

- [x] Keep `PVAD.Migration.Abstractions`.
- [x] Keep `PVAD.Migration.Roslyn`.
- [x] Keep aggregator `PVAD.Migration.Pdf`.
- [x] Keep aggregator `PVAD.Migration.Spreadsheet`.
- [x] Keep aggregator `PVAD.Migration.Report`.
- [x] Update aggregators to reference the new provider namespaces after implementation.

## Physical Project Layout

- [x] Use `src/Migrations` as the physical root for migration projects.
- [x] Move shared migration infrastructure to `src/Migrations/Common`:
  - `PVAD.Migration.Abstractions`
  - `PVAD.Migration.Roslyn`
- [x] Move PDF code migration projects to `src/Migrations/PDF`.
- [x] Keep PDF Viewer migration projects under `src/Migrations/PDF` using the `.Viewer.<Provider>` namespace segment.
- [x] Move report designer migration projects to `src/Migrations/Report`.
- [x] Move spreadsheet code migration projects to `src/Migrations/Spreadsheet`.
- [x] Keep project names and namespaces stable while changing physical folders.

## Remaining Source Project Layout

- [x] Use physical folders for non-migration projects while keeping namespaces stable.
- [x] Group core/application projects under `src/Core`:
  - `PVAD.Core`
  - `PVAD.Domain`
  - `PVAD.Application`
- [x] Group generation projects under `src/Generation`:
  - `PVAD.Generator`
  - `PVAD.Pdf`
- [x] Group importer projects under `src/Importing`:
  - `PVAD.Importer`
  - `PVAD.FileImporter`
  - `PVAD.FileImporter.ImageAnalysis`
  - `PVAD.FileImporter.ImageOcr`
  - `PVAD.FileImporter.ImageOcr.Worker`
- [x] Group infrastructure projects under `src/Infrastructure`:
  - `PVAD.Infrastructure.Converters`
  - `PVAD.Infrastructure.Pdf`
  - `PVAD.Infrastructure.Spreadsheet`
  - `PVAD.Infrastructure.Word`
- [x] Keep `PVAD.Infrastructure.Pdf` under `src/Infrastructure`, because the namespace represents the technical implementation layer.

## Compatibility Rules

- [x] Use `/api/pvad/v1/migrations/{domain}/{kind}/{provider}` as the canonical provider route.
- [x] Keep PDF Code, PDF Viewer, Spreadsheet Code, and Report Designer as distinct catalog paths.
- [x] Return canonical provider IDs from the v1 API while accepting historical aliases at input boundaries.

- [x] Existing provider IDs continue to work as aliases:
  - `aspose-pdf`
  - `devexpress-report`
  - `aspose-cells`
  - all other current PDF, spreadsheet, and report provider IDs
  - `gdpicture` and `GdPicture.NET` as aliases for `nutrient`
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

## Recorded Taxonomy Validation

The results below record the original taxonomy rollout, not a new test run.
For runnable commands against the current tree, use [TESTING.md](../TESTING.md).

- [x] `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1`
  - Current result: passed with warnings, 0 errors.
  - Restore needed to run outside the sandbox because sandboxed restore hung.
- [x] Static checks:
  - `dotnet sln PVAD.sln list` shows the new taxonomy project paths.
  - No old flat migration namespace references remain in `src`, `tests`, or `PVAD.WebApi`.
  - No old provider project directories remain under `src` or `tests`.
- [x] API contract test updated for taxonomy metadata:
  - `/api/migration/frameworks` returns legacy `kind`, plus `domain`, `migrationKind`, `provider`.
- [x] `npm run build` in `pvad-designer`.
- [x] Targeted .NET migration/API tests:
  - `PXA.Api.Tests` filtered to `MigrationControllerTests|MigrationServiceTests`: 16 passed.
  - `PXA.Migration.Pdf.Tests`: 18 passed.
  - `PXA.Migration.Spreadsheet.Tests`: 19 passed.
  - `PXA.Migration.Report.Tests`: 20 passed.
- [x] Full .NET test pass after restore succeeds.
  - Recorded result: `dotnet test PXA.sln --no-restore --disable-build-servers -m:1` passed.
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
