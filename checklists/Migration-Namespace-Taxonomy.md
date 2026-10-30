# Migration Namespace Taxonomy

Recorded outcomes remain historical. Current project names in rerun references
do not claim that the checks were newly executed; original commands are in Git.

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

- [x] Historical `ActivePdf` provider responsibility -> `PVAD.Migration.Pdf.Code.ActivePdf` (current taxonomy; original source name in Git).
- [x] Historical `Apryse` provider responsibility -> `PVAD.Migration.Pdf.Code.Apryse` (current taxonomy; original source name in Git).
- [x] Historical `AsposePdf` provider responsibility -> `PVAD.Migration.Pdf.Code.Aspose` (current taxonomy; original source name in Git).
- [x] Historical `DevExpressPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.DevExpress` (current taxonomy; original source name in Git).
- [x] Historical `DsPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.DsPdf` (current taxonomy; original source name in Git).
- [x] Historical `FoxitPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.Foxit` (current taxonomy; original source name in Git).
- [x] Historical `GemBoxPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.GemBox` (current taxonomy; original source name in Git).
- [x] Historical `IronPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.IronPdf` (current taxonomy; original source name in Git).
- [x] Historical `iText7` provider responsibility -> `PVAD.Migration.Pdf.Code.IText7` (current taxonomy; original source name in Git).
- [x] Historical `LeadtoolsPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.Leadtools` (current taxonomy; original source name in Git).
- [x] Add `PVAD.Migration.Pdf.Code.Nutrient` for Nutrient .NET and legacy GdPicture.NET code migration.
- [x] Historical `PdfKitNet` provider responsibility -> `PVAD.Migration.Pdf.Code.PdfKitNet` (current taxonomy; original source name in Git).
- [x] Historical `PdfTools` provider responsibility -> `PVAD.Migration.Pdf.Code.PdfTools` (current taxonomy; original source name in Git).
- [x] Historical `PdfToolsToolbox` provider responsibility -> `PVAD.Migration.Pdf.Code.PdfToolsToolbox` (current taxonomy; original source name in Git).
- [x] Historical `SpirePdf` provider responsibility -> `PVAD.Migration.Pdf.Code.Spire` (current taxonomy; original source name in Git).
- [x] Historical `SyncfusionPdf` provider responsibility -> `PVAD.Migration.Pdf.Code.Syncfusion` (current taxonomy; original source name in Git).

## PDF Viewer Migration Targets

- [x] Add `PVAD.Migration.Pdf.Viewer.Nutrient` for Nutrient Web SDK and legacy PSPDFKit Viewer migration.
- [x] Use canonical code provider key `nutrient` and Viewer provider key `nutrient-viewer`.
- [x] Keep Nutrient/PSPDFKit separate from Apryse/PDFTron; they are unrelated provider families.
- [x] Accept historical product names only as input aliases and return canonical Nutrient metadata.

## Spreadsheet Code Migration Targets

- [x] Historical `AsposeCells` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.Aspose` (current taxonomy; original source name in Git).
- [x] Historical `ClosedXmlSpreadsheet` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.ClosedXml` (current taxonomy; original source name in Git).
- [x] Historical `EpplusSpreadsheet` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.Epplus` (current taxonomy; original source name in Git).
- [x] Historical `GemBoxSpreadsheet` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.GemBox` (current taxonomy; original source name in Git).
- [x] Historical `Npoi` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.Npoi` (current taxonomy; original source name in Git).
- [x] Historical `SpireXls` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.Spire` (current taxonomy; original source name in Git).
- [x] Historical `SpreadsheetLight` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.SpreadsheetLight` (current taxonomy; original source name in Git).
- [x] Historical `SyncfusionXlsIo` provider responsibility -> `PVAD.Migration.Spreadsheet.Code.Syncfusion` (current taxonomy; original source name in Git).

## Report Designer Migration Targets

- [x] Historical `ActiveReportsJs` provider responsibility -> `PVAD.Migration.Report.Designer.ActiveReportsJs` (current taxonomy; original source name in Git).
- [x] Historical `DevExpressReport` provider responsibility -> `PVAD.Migration.Report.Designer.DevExpress` (current taxonomy; original source name in Git).
- [x] Historical `FastReport` provider responsibility -> `PVAD.Migration.Report.Designer.FastReport` (current taxonomy; original source name in Git).
- [x] Historical `JasperReports` provider responsibility -> `PVAD.Migration.Report.Designer.JasperReports` (current taxonomy; original source name in Git).
- [x] Historical `Rdl` provider responsibility -> `PVAD.Migration.Report.Designer.Rdl` (current taxonomy; original source name in Git).
- [x] Historical `Rpx` provider responsibility -> `PVAD.Migration.Report.Designer.Rpx` (current taxonomy; original source name in Git).
- [x] Historical `Stimulsoft` provider responsibility -> `PVAD.Migration.Report.Designer.Stimulsoft` (current taxonomy; original source name in Git).
- [x] Historical `Telerik` provider responsibility -> `PVAD.Migration.Report.Designer.Telerik` (current taxonomy; original source name in Git).

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
  - [compatibility entry `compat-bef49bdc75d9`](../docs/reference/compatibility-identifiers.v1.json) filtered to `MigrationControllerTests|MigrationServiceTests`: 16 passed.
  - [compatibility entry `compat-ed4418e50a42`](../docs/reference/compatibility-identifiers.v1.json): 18 passed.
  - [compatibility entry `compat-90ba86cbb4fd`](../docs/reference/compatibility-identifiers.v1.json): 19 passed.
  - [compatibility entry `compat-d49108d6335d`](../docs/reference/compatibility-identifiers.v1.json): 20 passed.
- [x] Full .NET test pass after restore succeeds.
  - Recorded result: `dotnet test PVAD.sln --no-restore --disable-build-servers -m:1` passed.
- [x] Smoke test PDF code migration with `aspose-pdf`.
  - Added API smoke coverage for provider-key alias routing through [compatibility entry `compat-dc4844699c70`](../docs/reference/compatibility-identifiers.v1.json).
- [x] Smoke test spreadsheet code migration with `aspose-cells`.
  - Added API smoke coverage for provider-key alias routing through [compatibility entry `compat-dc4844699c70`](../docs/reference/compatibility-identifiers.v1.json).
- [x] Smoke test designer migration with DevExpress and RDL samples.
  - Added API smoke coverage for [compatibility entry `compat-309eed4f0fc7`](../docs/reference/compatibility-identifiers.v1.json) with DevExpress `.repx` and RDL XML samples.
  - Verified with `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter MigrationControllerTests` (6 passed).

## Assumptions

- Provider IDs remain backward compatible.
- Report designer migrations belong under `Report.Designer`, not `Pdf.Designer`.
- Datasource provider projects are added only when real backend datasource/file migration providers exist.
