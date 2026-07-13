# Rename PXA To PXA

## Summary

Track the completed breaking rename from **PXA** to **Power Dox Automation / PXA**.

Decision: **Option A / breaking rename is accepted**. Active code uses `PXA.*` project names and namespaces.
The old `PXA.*` projects and namespaces are not kept as source-level `[Obsolete]` shims. HTTP routes,
JSON fields, localStorage keys, and `CANMIG...` diagnostic identifiers remain stable where they are external
contracts.

Final cleanup decision: active product/API/docs names are PXA-only. The previous PXA compatibility names
were removed from active code-generation output, MCP resources, schema aliases, UI migration payload names,
and the public workbook authoring API. Remaining `PXA` words are historical checklist entries or technical
terms such as editor canvas surfaces, `PdfCanvas`, `SKCanvas`, and third-party APIs.

## Product Naming

- [x] Product/Web name: **Power Dox Automation**
- [x] Short/developer name: **PXA**
- [x] CLI name reserved: `pxa`
- [x] Future native file format reserved: `.pxa`

## Namespace Mapping

| Current name | Future name |
| --- | --- |
| `PXA.Pdf` | `PXA.Generator.Pdf` or public facade `PXA.Generator` |
| `PXA.Migration.*` | `PXA.Migration.*` |
| `PXA.Importer` | `PXA.Importer` |
| `PXA.FileImporter.*` | `PXA.FileImporter.*` |
| `PXA.Core` | `PXA.Core` |
| `PXA.Application` | `PXA.Application` |
| `PXA.Infrastructure.*` | `PXA.Infrastructure.*` |
| `PXA.WebApi` | `PXA.WebApi` |
| `PXA.Domain` | `PXA.Domain` |
| `PXA.Infrastructure.Pdf` | `PXA.Infrastructure.Pdf` |
| `PXA.Infrastructure.Word` | `PXA.Infrastructure.Word` |
| `PXA.Infrastructure.Spreadsheet` | `PXA.Infrastructure.Spreadsheet` |
| `PXA.Infrastructure.Converters` | `PXA.Infrastructure.Converters` |
| `PXA.FileImporter.ImageOcr.Worker` | `PXA.FileImporter.ImageOcr.Worker` |
| `PXA.Mcp` | `PXA.Mcp` or `pxa-mcp` package/tool name |
| `PXA.Demo` | `PXA.Demo` |

## Current Repo Additions To Include

- [x] Include `PXA.sln` and `PXA.slnx` in the rename plan.
- [x] Include all `tests/PXA.*.Tests` projects in the namespace/project rename plan.
- [x] Include `samples/PXA.Demo`.
- [x] Include `tools/PXA.Mcp` package name, README, and smoke script.
- [x] Include `docs/schema/canvas-workbook.schema.json` and schema `$id`/title naming.
- [x] Include `llms.txt`, `llms-full.txt`, and generated OpenAPI/doc artifacts when docs are renamed.
      `llms.txt` and `llms-full.txt` already use Power Dox Automation/PXA as the primary identity while
      documenting legacy `PXA.Pdf` compatibility. `docs/schema/openapi.json` now uses a PXA title and
      includes additive `/api/pxa/...` aliases for the documented document/export/migration/pdf-viewer/template
      routes while retaining the legacy `/api/...` paths. Auth remains on `/api/auth` by design.
- [x] Include both frontend folders: legacy `ui-designer` and current `ui-designer-v2`.
- [x] Include package names/routes/visible branding in frontend only after backend API compatibility is protected.

## Compatibility Rules

- [x] Treat the code rename as breaking: `PXA.*` source projects and namespaces are retired.
- [x] Do not promise source-level `[Obsolete]` shims for `PXA.*` in the active repository.
- [x] Keep old NuGet/package identities or publish forwarding packages only if a prior external package was
      actually published under a `PXA.*` identity. Current repo audit found no published NuGet/package
      identity to forward yet: no `.nuspec`, no explicit `<PackageId>`, and no `<GeneratePackageOnBuild>`
      metadata for product projects.
- [x] Keep old HTTP endpoints compatible.
- [x] Keep old JSON fields compatible.
- [x] Add new PXA-oriented fields only alongside legacy fields.
- [x] Keep `CANMIG...` diagnostic IDs stable for now.
- [x] Do not rename unrelated terms such as HTML PXA, `html2canvas`, `SKCanvas`, or iText `PdfCanvas`.
- [x] Do not rename user/domain words like "canvas" in HTML/CSS drawing contexts unless they refer to the product brand.
- [x] Keep existing document JSON schema fields stable unless a versioned schema migration is added.

Compatibility verification note: runtime/API compatibility is preserved for legacy HTTP routes, JSON
contracts, localStorage keys, `CANMIG...` diagnostics, and technical canvas terms. Source compatibility for
`PXA.*` namespaces is intentionally not preserved after the breaking PXA rename.

## Option A Completion

- [x] Active main docs updated to describe PXA as the implementation identity instead of an additive facade over PXA.
- [x] Source-level `PXA.*` shim promises removed from active documentation.
- [x] DocFX and schema docs updated to point at `src/PXA.*` projects and contracts.
- [x] Spreadsheet migration generated output updated from `PXA.Infrastructure.Spreadsheet` to
      `PXA.Infrastructure.Spreadsheet`.
- [x] `PXA.Pdf.PdfDocument` constructor guidance updated to recommend `PXA.Generator.Pdf.CreateDocument(...)`
      without referring to a PXA compatibility window.
- [x] `PXA.FileImporter.ImageOcr.Worker` lookup strings updated to the PXA worker name.
- [x] Active code/docs scan completed: remaining `PXA.*` mentions in main docs are limited to explicit
      historical/glossary notes and compatibility aliases.
- [x] Final product-name cleanup completed:
      - generated migration responses use `pxaCode` / `PxaCode`
      - spreadsheet authoring API uses `PxaWorkbook`, `PxaWorksheet`, `PxaCell`, `PxaRange`, and `PxaColumn`
      - OCR layout builder uses `PxaElementBuilder`
      - MCP exposes only `pxa://...` resources and `PXA_API_URL`
      - workbook schema alias `docs/schema/canvas-workbook.schema.json` removed; `pxa-workbook.schema.json` is canonical
      - active documentation and UI migration docs no longer reference PXA compatibility names

Verification for this slice:

- [x] `git diff --check`
- [x] `dotnet build PXA.sln --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PXA.Migration.Spreadsheet.Tests/PXA.Migration.Spreadsheet.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] Spreadsheet provider tests for AsposeCells, ClosedXML, EPPlus, GemBox, NPOI, Spire.XLS,
      SpreadsheetLight, and Syncfusion XlsIO.
- [x] `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-build --no-restore --disable-build-servers -m:1 --filter "Migration"`
- [x] `dotnet test tests/PXA.Infrastructure.Spreadsheet.Tests/PXA.Infrastructure.Spreadsheet.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `npm run build` in `ui-designer-v2`

## Documentation Plan

- [x] Move main docs to **Power Dox Automation / PXA** naming later.
- [x] Update active examples in main docs to use `PXA.*` APIs.
      Active examples use `PXA.Generator.Pdf.CreateDocument()` plus `using PXA.Pdf;` for PDF option/value
      types such as `PdfColor`, `PdfDrawTextOptions`, and `PdfPagePreset`.
- [x] Keep historical checklist wording when it describes legacy PXA implementation history.
- [x] Add clear legacy notes to historical checklists instead of blindly replacing every `PXA` occurrence.
- [x] Add a short glossary: **Power Dox Automation** = product, **PXA** = developer/API/CLI identity,
      `PXA.*` = historical namespace removed by the breaking rename.
- [x] Update docs schemas and generated OpenAPI only after code/API names are stabilized.
      PXA workbook schema alias and OpenAPI PXA route aliases are present; legacy schema/path contracts remain.

## Future Implementation Phases

- [x] Phase 0: inventory all `PXA` occurrences and classify as product namespace, file/project path, UI branding, docs, schema, or unrelated HTML/graphics canvas.
      Initial inventory completed from current repo state: product namespaces/projects, UI/editor canvas terms, docs/checklists,
      schemas, MCP, samples, and unrelated HTML/graphics canvas usages were identified as separate rename buckets.
- [x] Phase 1: introduce new `PXA.*` public API layer without physical path/project renames.
      Started with additive `PXA.Generator` project and PDF/Word/Spreadsheet facades that delegate to the existing
      PXA implementations.
      Completed for the current compatibility layer: additive PXA projects now cover generator, importer,
      file importer, migration, core, application, domain, infrastructure, API/export test aliases, MCP,
      schema, and sample entry points while legacy PXA project paths remain compatible.
- [x] Phase 2: add generator facade target for PDF/Word/Spreadsheet:
      - `PXA.Pdf` -> `PXA.Generator.Pdf` / `PXA.Generator`
      - Word export APIs -> `PXA.Generator.Word`
      - Spreadsheet APIs -> `PXA.Generator.Spreadsheet`
- [x] Phase 3: move migration namespaces from `PXA.Migration.*` to `PXA.Migration.*`, including PDF, report, and spreadsheet providers.
      Started with additive `PXA.Migration.Abstractions` project, PXA-facing migration result/diagnostic types,
      and a `PxaSourceMigrationAdapter` bridge for existing `PXA.Migration.Abstractions.ISourceMigration`
      implementations.
      First concrete provider facade added with `PXA.Migration.Pdf.Code.DevExpress`, delegating to the existing
      `PXA.Migration.Pdf.Code.DevExpress.DevExpressPdfMigration` while returning PXA migration abstractions.
      PDF-code migration facade set expanded with `PXA.Migration.Pdf.Code.Syncfusion` and `PXA.Migration.Pdf.Code.IText7`,
      both delegating to existing PXA providers and preserving `CANMIG...` diagnostic IDs.
      Additional central PDF provider facades added with `PXA.Migration.Pdf.Code.Aspose` and `PXA.Migration.Pdf.Code.Apryse`.
      PDF provider facade coverage expanded with `PXA.Migration.Pdf.Code.DsPdf` and `PXA.Migration.Pdf.Code.Foxit`.
      PDF provider facade coverage expanded with `PXA.Migration.Pdf.Code.IronPdf` and `PXA.Migration.Pdf.Code.GemBox`.
      PDF provider facade coverage expanded with `PXA.Migration.Pdf.Code.Spire` and `PXA.Migration.Pdf.Code.PdfKitNet`.
      PDF provider facade coverage expanded with `PXA.Migration.Pdf.Code.Leadtools` and `PXA.Migration.Pdf.Code.ActivePdf`.
      PDF provider facade coverage expanded with `PXA.Migration.Pdf.Code.PdfTools` and `PXA.Migration.Pdf.Code.PdfToolsToolbox`.
      Shared PDF migration entry point added with `PXA.Migration.Pdf`, exposing provider keys and
      `PdfMigrationProviders.Create(...)` / `TryCreate(...)` for CLI/API use.
      Report migration entry point added with `PXA.Migration.Report`, PXA-facing report result/contract,
      and provider keys for DevExpress Report, RDL, and RPX designer migrations.
      Report migration registry expanded with JasperReports JRXML and ActiveReports JS JSON facades.
      Report migration registry expanded with FastReport FRX, Telerik TRDX, and Stimulsoft MRT facades.
      Spreadsheet migration entry point added with `PXA.Migration.Spreadsheet`, provider facades, and
      registry keys for Aspose.Cells, ClosedXML, EPPlus, GemBox.Spreadsheet, NPOI, Spire.XLS,
      SpreadsheetLight, and Syncfusion XlsIO.
- [x] Phase 4: move importer and file importer namespaces:
      - `PXA.Importer` -> `PXA.Importer`
      - `PXA.FileImporter.*` -> `PXA.FileImporter.*`
      Started with additive `PXA.Importer` project and PDF import facade that delegates to the existing
      `PXA.Importer.PdfImporter`.
      File importer entry point added with `PXA.FileImporter`, PXA-facing `IFileImporter`, provider keys,
      and registry/facades for PDF, DOCX, DOC, ODT, SVG, PPTX, and raster image import.
      Specialized additive facades added with `PXA.FileImporter.ImageAnalysis` and `PXA.FileImporter.ImageOcr`,
      including PXA-facing analysis options/results/diagnostics, OCR options/results/models, OCR engine contract,
      Tesseract engine facades, and PXA adapters. The OCR worker executable/package name remains a later
      physical rename item in Phase 8/9.
- [x] Phase 5: move infrastructure, application, core, and domain namespaces.
      Started with additive `PXA.Core` project and PXA-owned contract types for design documents,
      spreadsheet workbooks, and export options. JSON-compatible adapters bridge `PXA.Core.Contracts`
      to the existing `PXA.Core.Contracts` types while engines still use the legacy internals.
      Public PXA generator and file importer facades now expose `PXA.Core.Contracts` instead of
      `PXA.Core.Contracts` for document import/export surfaces. Remaining follow-up: report migration
      result contracts, application use cases, infrastructure facades, and domain-facing model aliases.
      Report migration results now expose `PXA.Core.Contracts.DesignExportDto` while existing
      PXA report converters remain the internal implementation.
      Application facade started with additive `PXA.Application` project for Clone, ExtractPages,
      FindAndReplace, and ValidateTemplate use cases. Design use cases expose `PXA.Core.Contracts`;
      ValidateTemplate now accepts `PXA.Domain.Repositories.ITemplateRepository` and uses the
      PXA-to-PXA repository adapter internally.
      PDF infrastructure facade started with additive `PXA.Infrastructure.Pdf` project for renderer,
      facade, diagnostics reader, output writer, renderer capabilities, and PDF service hooks.
      Word infrastructure facade started with additive `PXA.Infrastructure.Word` project for DOCX export,
      renderer capabilities, and DOCX digital-signing facade.
      Spreadsheet infrastructure facade started with additive `PXA.Infrastructure.Spreadsheet` project for
      fluent workbook authoring, XLSX/XLS/CSV IO, spreadsheet calculation, validation, spreadsheet-to-design
      conversion, Excel document export, operations, and sheet renderer capabilities.
      Converter infrastructure facade started with additive `PXA.Infrastructure.Converters` project for
      HTML, Markdown, CSV, XML, SVG, PNG/JPEG/TIFF, ODT exports and converter renderer capabilities.
      Domain facade started with additive `PXA.Domain` project for design templates, template metadata,
      page settings, designer elements, element config models, validation results, template name info,
      and PXA/PXA repository adapters.
      Completed for additive facade coverage and verified through the PXA solution/test aliases. Remaining
      work here is physical namespace/path replacement, which belongs to Phase 9 and the later breaking
      rename plan.
- [x] Phase 6: update Web/API branding to **Power Dox Automation** and **PXA** while preserving legacy endpoints.
      Started with additive Web API branding discovery endpoint exposed on both legacy
      `/api/system/brand` and PXA `/api/pxa/system/brand` routes. Response advertises
      Power Dox Automation/PXA naming, reserved `pxa` CLI and `.pxa` extension, and legacy
      PXA compatibility notes.
      Export API alias added with `/api/pxa/export` while legacy `/api/export` remains compatible.
      Migration API alias added with `/api/pxa/migration` while legacy `/api/migration` remains compatible.
      Document API alias added with `/api/pxa/document` while legacy `/api/document` remains compatible,
      covering document operations and image-to-PDF conversion routes.
      Spreadsheet API alias added with `/api/pxa/spreadsheet` while legacy `/api/spreadsheet` remains compatible.
      Templates API alias added with `/api/pxa/templates` while legacy `/api/templates` remains compatible.
      PDF Viewer API aliases added with `/api/pxa/pdf-viewer/annotations` and `/api/pxa/pdf-viewer/forms`
      while legacy `/api/pdf-viewer/*` routes remain compatible.
      Auth remains intentionally on the platform route `/api/auth`; no `/api/pxa/auth` alias is planned
      unless all public platform routes are later duplicated under PXA.
- [x] Phase 7: update UI branding in `ui-designer-v2` and decide whether legacy `ui-designer` is renamed, archived, or left as historical.
      Started visible branding in `ui-designer-v2`: browser title/loading text, package metadata,
      app header logo/accessibility label, home hero/feature copy, importer format copy, and migrations hub
      product copy now use Power Dox Automation/PXA. The migrations conversion page now uses PXA wording
      for report-designer targets, backend error messages, spreadsheet/PDF conversion copy, and output
      panel labels. The in-app docs now use Power Dox Automation/PXA wording for import/export,
      migration, REST API, AI/codegen, spreadsheet, and documentation-map product descriptions while
      marking current `PXA.*` project paths and namespaces as legacy compatibility where needed.
      Smaller UI surfaces now use PXA wording for spreadsheet import, workbook JSON errors,
      code-panel descriptions, generated PDF-code comments, starter text, and global style comments.
      Final inventory leaves only intentional technical canvas terms, legacy compatibility references,
      localStorage/sessionStorage keys, current legacy project paths, and sample content. The legacy
      `ui-designer` folder remains historical for now and is deferred to Phase 8/9 instead of being
      renamed in this UI-branding slice.
      Technical HTML canvas terms, localStorage keys, and legacy `PXA.Pdf` API references remain
      unchanged for compatibility until their specific surfaces are migrated.
      Frontend package naming slice completed: the active `ui-designer-v2` package now publishes as
      `pxa-designer`, code-editor sample/download/schema identifiers use PXA naming, and the legacy
      `ui-designer` package/title is explicitly marked as historical compatibility rather than active PXA UI.
      Legacy localStorage keys remain unchanged so existing user drafts and preferences survive the rename.
- [x] Phase 8: update MCP/sample/docs/package identities:
      - `tools/PXA.Mcp` -> `tools/PXA.Mcp` or package `pxa-mcp`
      - `samples/PXA.Demo` -> `samples/PXA.Demo`
      - active docs and schemas to PXA naming
      Started with MCP package identity: the MCP server advertises `pxa-mcp` as package/server
      identity, exposes `pxa-mcp` as the primary binary while retaining `canvas-mcp` compatibility,
      accepts `PXA_API_URL` while retaining `CANVAS_API_URL`, and publishes `pxa://...` resources while
      retaining legacy `canvas://...` aliases.
      Sample identity started with PXA-facing demo metadata and renderer notes.
      Active docs/AI references started: `llms.txt`, `llms-full.txt`, DocFX landing/config files,
      cookbook intro, documentation approach, and schema descriptions now present Power Dox Automation/PXA
      as the primary identity while preserving legacy `PXA.*` xrefs, project paths, and schema URIs
      for compatibility.
      Root `README.md` now presents Power Dox Automation/PXA as the primary product identity, describes
      PXA importer/generator/migration surfaces, and keeps current `PXA.*` paths as legacy project names.
      `ARCHITECTURE.md` now documents the additive `PXA.*` facade layer, PXA importer/generator/migration
      public entry points, and legacy PXA project boundaries until the later physical rename phase.
      `PROJECT_SUMMARY.md` now presents PXA as the primary product identity, adds the PXA facade project
      group, and keeps `PXA.*` project/test names as current legacy implementation inventory.
      `TESTING.md` now describes PXA-compatible PDF code/output and editable PXA design targets while
      keeping current `PXA.*.Tests` project names as the real test inventory.
      `CONTRIBUTING_RENDERERS.md` now names Power Dox Automation/PXA as the extension target, asks new
      developer-facing work to expose `PXA.*` facades, and documents legacy `PXA.*` implementation
      project patterns during the compatibility phase.
- [x] Phase 9: optional later physical rename of solution, project files, folders, and test assemblies.
      Started with additive solution alias `PXA.sln`, copied from the current `PXA.sln` so developers can
      build through a PXA-named entry point while legacy project paths remain stable. Verified with
      `dotnet sln PXA.sln list` and `dotnet build PXA.sln` (0 errors; existing dependency/analyzer/nullability
      warnings remain).
      Added `PXA.WebApi/PXA.WebApi.http` as a PXA-named HTTP request alias for the `/api/pxa/system/brand`
      endpoint. The WebApi folder and project file are now physically named `PXA.WebApi/PXA.WebApi.csproj`,
      both `PXA.sln` and `PXA.sln` reference it as `PXA.WebApi`, and API/export/ImageOCR test project
      references point to the new project file. Implementation namespaces were moved from the legacy WebApi
      namespace to `PXA.WebApi.*` in the follow-up namespace slice. Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed). Solution-level verification after this slice:
      `dotnet build PXA.sln --no-restore --disable-build-servers -m:1` and
      `dotnet build PXA.sln --no-restore --disable-build-servers -m:1` both passed with 0 errors
      and existing dependency/NPOI/PXA0001 compatibility warnings.
      The WebApi namespace slice was re-verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullable warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
      Removed the stale default legacy-named WebApi scratch request after the folder/namespace rename;
      `PXA.WebApi/PXA.WebApi.http` is now the only tracked WebApi HTTP scratch file.
      Follow-up documentation cleanup updated active checklist references from the old WebApi project filename
      to `PXA.WebApi/PXA.WebApi.csproj`.
      Renamed the tracked MCP server files from `tools/PXA.Mcp` to `tools/PXA.Mcp`. The legacy folder now
      keeps a README pointer only; untracked local `node_modules` / lock artifacts may remain there locally.
      The server still exposes the compatibility `canvas-mcp` binary and `canvas://...` resource aliases.
      Renamed the tracked demo sample files from `samples/PXA.Demo` to `samples/PXA.Demo`, including
      `PXA.Demo.csproj`, and updated `PXA.sln` / `PXA.sln` to point at the new sample path. The legacy
      sample folder now keeps a README pointer only; local `bin` / `obj` artifacts may remain there locally.
      Added `docs/schema/pxa-workbook.schema.json` as the primary PXA Workbook JSON schema with a PXA `$id`;
      kept `docs/schema/canvas-workbook.schema.json` as the legacy compatibility alias and updated MCP,
      in-app docs, AI docs, and schema tests to prefer the PXA path.
      Added additive `PXA.Api.Tests` and `PXA.Export.Tests` project aliases that compile the existing
      legacy API/export test sources under PXA-named test assemblies. Both `PXA.sln` and `PXA.sln` now
      include the aliases while the legacy `PXA.Api.Tests` / `PXA.Export.Tests` projects remain.

## Phase 9 Collision Plan

The remaining physical rename work is not a simple folder move because many `src/PXA.*` projects already
exist as public facades or provider aggregators over legacy `PXA.*` implementation projects.

Recommended order:

1. **Keep facade projects stable first.**
   `PXA.Core`, `PXA.Application`, `PXA.Domain`, `PXA.Infrastructure.*`, `PXA.FileImporter`,
   `PXA.Importer`, `PXA.Migration.Pdf`, `PXA.Migration.Report`, and `PXA.Migration.Spreadsheet`
   should stay in place as the public surface while implementation moves are done underneath them.
2. **Promote shared contracts before implementation engines.**
   Move/copy remaining canonical contracts from `PXA.Core` into `PXA.Core`, then convert PXA-facing
   types to compatibility aliases or adapters. Do this before moving Application or Infrastructure because
   almost every project depends on Core DTOs and abstractions.
3. **Promote Domain separately.**
   `PXA.Domain` currently wraps `PXA.Domain`; do not rename `PXA.Domain/` directly into
   `src/PXA.Domain/` because that folder already exists. First move missing implementation types into the
   existing `src/PXA.Domain` project, then keep `PXA.Domain` as a compatibility project that forwards
   to PXA.
4. **Promote Application after Core/Domain.**
   `PXA.Application` currently references `PXA.Application`, `PXA.Core`, and `PXA.Domain`.
   Move use cases into the existing `src/PXA.Application` project in small groups, then leave
   `PXA.Application` as compatibility shims.
5. **Promote Infrastructure by capability family.**
   `PXA.Infrastructure.Pdf`, `Word`, `Spreadsheet`, and `Converters` already exist and reference the
   PXA engines. Move one family at a time, keeping tests green after each family. PDF should go first
   because it is the generator core, then Word/Spreadsheet, then Converters.
6. **Promote Importers after Core and PDF infrastructure.**
   `PXA.Importer` and `PXA.FileImporter` now own their active PXA source, and `PXA.WebApi` composes through
   the PXA importer projects. Remaining importer follow-up is limited to compatibility cleanup such as legacy
   test/project aliases and shared OCR data assets.
7. **Promote Migration provider projects last.**
   PXA provider facades already exist for PDF migrations; report/spreadsheet aggregators still reference
   `PXA.Migration.*` provider engines. Move individual provider projects one provider family at a time
   only after Core/Application/Infrastructure identities are stable.
8. **Tests follow the same pattern.**
   Keep current `PXA.*.Tests` until each underlying project is promoted, then add or switch to
   `PXA.*.Tests` aliases before removing legacy test project names.

Current classification:

| Area | Current state | Next action |
| --- | --- | --- |
| `PXA.WebApi` | Physically renamed; namespace renamed; API tests green | Done except future route/diagnostic cleanup |
| `PXA.Core` | Facade over `PXA.Core` plus selected PXA contracts | Promote canonical contracts/abstractions first |
| `PXA.Domain` | Facade/adapters over `PXA.Domain` | Move missing domain implementation into existing PXA project |
| `PXA.Application` | Facade over `PXA.Application` plus selected PXA use cases | Move use cases after Core/Domain |
| `PXA.Pdf` | PXA-owned PDF generator engine (`namespace PXA.Pdf`), moved out of `PXA/Pdf`; single assembly, no dual-link | Done; migration providers still emit `PXA.Pdf` output (separate slice) |
| `PXA.Infrastructure.Pdf` | PXA-owned PDF services over `PXA.Pdf` engine + `PXA.Core` | Done |
| `PXA.Infrastructure.Word` | Facade over Word infrastructure | Promote after PDF |
| `PXA.Infrastructure.Spreadsheet` | Facade over Spreadsheet infrastructure | Promote after PDF or alongside spreadsheet migration |
| `PXA.Infrastructure.Converters` | Facade over converter exporters | Promote after Word/Spreadsheet dependencies settle |
| `PXA.Importer` | PXA-owned low-level importer source; still bridges to PXA/PDF engine where needed; PXA tests now use PXA importer directly | Keep compatibility shims until PDF engine rename |
| `PXA.FileImporter` | PXA-owned built-in importers; WebApi composes through PXA projects | Keep legacy PXA aliases/tests for compatibility window |
| `PXA.Migration.Pdf` | Aggregator over PXA provider facades | Keep, then retire legacy provider engines later |
| `PXA.Migration.Report` | Aggregator over `PXA.Migration.*` report engines; direct `PXA.Core` reference removed | Promote report providers one by one late |
| `PXA.Migration.Spreadsheet` | Aggregator over `PXA.Migration.*` spreadsheet engines | Promote spreadsheet providers one by one late |
| `PXA.Export.Tests` | PXA-owned export composition tests over PXA Application/Core/Infrastructure projects | Keep full legacy regression suite in `tests/PXA.Export.Tests` |

Remaining compatibility bridges after the latest promotion slices:

- `PXA.Core` and `PXA.Domain` still reference the legacy core/domain projects because they own the active
  adapter boundary for the additive rename window.
- Individual `PXA.Migration.*` provider facades still reference legacy provider engines; the WebApi no longer
  composes directly against those legacy provider projects.
- `PXA.Migration.Report` and `PXA.Migration.Spreadsheet` still aggregate legacy report/spreadsheet engines
  until those provider implementations are promoted one by one.
- The PDF engine namespace/physical rename is **done**: the engine source moved from `PXA/Pdf/**`
  (`namespace PXA.Pdf`) into the new `src/PXA.Pdf` project (`namespace PXA.Pdf`), and all in-repo
  consumers (both infrastructure projects, `PXA.Generator`, `PXA.Importer`, WebApi, legacy `PXA.*`
  consumers, and tests) now reference `PXA.Pdf`. The one remaining `PXA.Pdf` surface is intentional:
  migration providers still *emit* `using PXA.Pdf;` in generated conversion output, which belongs to
  the migration-output-namespace decision in the provider-promotion slice (not the physical engine rename).
- `PXA.Api.Tests` intentionally links the legacy API test sources and OCR data paths for route compatibility
  coverage; replacing it with PXA-owned API tests should be a separate coverage-preserving slice.

Guardrails:

- Do not move a `PXA.*` implementation folder into an already-existing `src/PXA.*` facade folder without
  first deciding whether the facade or the implementation owns each file.
- Do not remove `PXA.*` projects until the matching PXA project has source-level ownership and the legacy
  project can compile as a compatibility shim.
- Verify each slice with the smallest relevant tests plus `dotnet build PXA.WebApi/PXA.WebApi.csproj`;
  run full solution builds only after several small green slices because they are currently slow and can hang.

Completed Phase 9 promotion slices:

- [x] `PXA.Core` primitive promotion:
      Added PXA-owned `PXA.Core.Primitives.PdfPoint`, `PdfTextAlignment`, `PdfVerticalAlignment`, and
      `ExportFormat` while keeping the legacy PXA primitives intact. Added adapter methods between the
      PXA and PXA primitive types so compatibility projects can bridge explicitly. Verified with
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (12 passed),
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed), and
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain).
- [x] `PXA.Core` capability promotion:
      Added PXA-owned `PXA.Core.Abstractions.IRendererCapabilities`,
      `PXA.Core.Capabilities.RendererFeature`, `UnsupportedFeatureFallbackMode`, and
      `RendererCapabilityFallback` while keeping legacy PXA capabilities intact. Added adapter methods
      for capability enums and PXA capability tests. Verified with
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (30 passed),
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed), and
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain).
- [x] `PXA.Core` simple abstraction promotion:
      Added PXA-owned `PXA.Core.Abstractions.IExporterCapabilities`, `ExporterCapabilities`,
      `IDiagnosticsReader`, `IOutputWriter`, and `IImageReader` while keeping legacy PXA abstractions
      intact. Added adapter methods for exporter capabilities and PXA abstraction tests. Verified with
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (33 passed) and
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed). `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      was started for this slice but stopped after a silent hang; previous WebApi composition builds passed
      before this additive Core-only interface slice.
- [x] `PXA.Core` document exporter abstraction promotion:
      Added PXA-owned `PXA.Core.Abstractions.IDocumentExporter` over `PXA.Core.Contracts.DesignExportDto`
      and `ExportOptions` while keeping the legacy PXA document exporter abstraction intact. Added tests
      for default exporter capabilities and the default options-overload fallback. Verified with
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (35 passed) and
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed).
- [x] `PXA.Domain` designer element behavior promotion:
      Promoted the legacy `DesignerElement.MigratePropsToConfig()` behavior into
      `PXA.Domain.ValueObjects.DesignerElement`, including the typed config migration and value parsing
      helpers. PXA numeric string parsing now uses invariant culture to avoid locale-dependent prop
      conversion. Added PXA-owned behavior tests for text and page-number props migration. Verified with
      `dotnet test tests/PXA.Domain.Tests/PXA.Domain.Tests.csproj --no-restore --disable-build-servers -m:1`
      (6 passed),
      `dotnet test tests/PXA.Application.Tests/PXA.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (4 passed), and
      `dotnet test tests/PXA.Application.Tests/PXA.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed). The first PXA Application test attempt raced with a parallel PXA Application build in
      the shared `PXA/obj` apphost output; rerunning it serially passed. The broader
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1` composition
      build also passed for this Core/Domain promotion block (0 errors; 22 existing warnings remain).
- [x] `PXA.Core` remaining behavior promotion:
      Promoted the remaining shared Core service abstractions and behavior primitives into `PXA.Core`,
      including `IDocumentRenderer`, expression/value formatter interfaces, page/header/table/watermark
      service interfaces, `A1Reference`, `PxaExpressionEvaluator`, `ExpressionEvaluator`,
      `DesignLayoutPlanner`, and `ValueFormatter`. The PXA evaluator type is PXA-named instead of exposing
      the legacy `PxaExpressionEvaluator` name. Verified with
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (40 passed) and
      `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed). The broader
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1` composition
      build also passed for this Core/Domain promotion block (0 errors; 22 existing warnings remain).
- [x] `PXA.Application` use case ownership promotion:
      Promoted the PXA application use cases from PXA facades to PXA-owned implementations over
      `PXA.Core` and `PXA.Domain`, including clone, extract pages, find/replace, validation, template
      create/get/update, export, generate, diagnostics, page coverage, header/footer, page numbering,
      table flow, table of contents, and watermark application. Removed the direct
      `PXA.Application` project reference from `PXA.Application`. `RenderTemplateUseCase` remains
      deferred to the PDF/infrastructure promotion because it currently owns direct PDF rendering/output
      behavior. Verified with
      `dotnet test tests/PXA.Application.Tests/PXA.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (8 passed) and
      `dotnet test tests/PXA.Application.Tests/PXA.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed). A follow-up
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1` started and
      compiled through the early dependency chain, then stopped after a silent hang; the previous WebApi
      composition build passed before this Application-only ownership slice.
- [x] `PXA.Infrastructure.Pdf` service ownership promotion:
      Promoted PDF infrastructure services from wrappers to PXA-owned implementations over `PXA.Core`
      abstractions, including document rendering, diagnostics, file output, renderer capabilities, page
      numbering, header/footer, watermark, table flow, table of contents, page coverage, and facade
      orchestration. `PXA.Infrastructure.Pdf` no longer references `PXA.Infrastructure.Pdf`; it links the
      current `PXA.Pdf` engine source directly while excluding the unused legacy PXA Core primitive
      compatibility adapter. The public PDF engine namespace remains `PXA.Pdf` until the later PDF engine
      type-facade/physical rename phase. `PXA.Generator` remains on `PXA.Infrastructure.Pdf` for now
      because switching it to `PXA.Infrastructure.Pdf` while Word/Spreadsheet still transitively reference
      `PXA.Infrastructure.Pdf` creates duplicate `PXA.Pdf` type identities. Verified with
      `dotnet build src/PXA.Infrastructure.Pdf/PXA.Infrastructure.Pdf.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PXA.Infrastructure.Pdf.Tests/PXA.Infrastructure.Pdf.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed),
      `dotnet test tests/PXA.Infrastructure.Pdf.Tests/PXA.Infrastructure.Pdf.Tests.csproj --no-restore --disable-build-servers -m:1`
      (34 passed), and
      `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed).
- [x] `PXA.Infrastructure.Word` ownership promotion:
      Promoted Word infrastructure from PXA wrappers to PXA-owned implementations over `PXA.Core`,
      including DOCX export, renderer capabilities, document protection, custom properties, style
      definitions, rich text span parsing, comments, footnotes, unit conversion, and DOCX digital signing.
      `PXA.Infrastructure.Word` no longer references `PXA.Infrastructure.Word`; it carries the same
      OpenXML/QR/signing package dependencies directly. `PXA.Generator` remains on the legacy Word/Spreadsheet
      references until Spreadsheet and Converter infrastructure are promoted, so duplicate transitive
      `PXA.Pdf` type identities are avoided. Verified with
      `dotnet build src/PXA.Infrastructure.Word/PXA.Infrastructure.Word.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing copied nullable warning remains),
      `dotnet test tests/PXA.Infrastructure.Word.Tests/PXA.Infrastructure.Word.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed),
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Word"`
      (85 passed), and
      `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed).
- [x] `PXA.Infrastructure.Spreadsheet` ownership promotion:
      Promoted Spreadsheet infrastructure from PXA wrappers to PXA-owned implementations over
      `PXA.Core` and `PXA.Application`, including fluent workbook authoring, XLSX/XLS/CSV IO,
      spreadsheet calculation, validation, spreadsheet-to-design conversion, Excel document export,
      workbook operations, sheet renderer capabilities, and spreadsheet data contracts.
      `PXA.Infrastructure.Spreadsheet` no longer references `PXA.Infrastructure.Spreadsheet`; it
      carries the same ClosedXML/NPOI dependencies directly. With PDF, Word, and Spreadsheet
      infrastructure promoted, `PXA.Generator` now references `PXA.Infrastructure.Pdf`,
      `PXA.Infrastructure.Word`, and `PXA.Infrastructure.Spreadsheet` instead of the legacy PXA
      infrastructure projects. The public PDF engine namespace remains `PXA.Pdf` until the later
      PDF engine type-facade/physical rename phase. Verified with
      `dotnet build src/PXA.Infrastructure.Spreadsheet/PXA.Infrastructure.Spreadsheet.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/ClosedXML/nullability warnings remain),
      `dotnet test tests/PXA.Infrastructure.Spreadsheet.Tests/PXA.Infrastructure.Spreadsheet.Tests.csproj --no-restore --disable-build-servers -m:1`
      (6 passed),
      `dotnet build src/PXA.Generator/PXA.Generator.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PDF XML-doc, dependency/NPOI, and nullable warnings remain),
      `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed), and
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Spreadsheet"`
      (20 passed).
- [x] `PXA.Infrastructure.Converters` ownership promotion:
      Promoted converter infrastructure from PXA wrappers to PXA-owned implementations over
      `PXA.Core` and `PXA.Application`, including HTML, Markdown, CSV, XML, SVG, PNG/JPEG/TIFF,
      ODT export, Google font CSS helpers, style extensions, and converter renderer capabilities.
      `PXA.Infrastructure.Converters` no longer references `PXA.Infrastructure.Converters`; it
      carries the same NPOI/SkiaSharp dependencies directly and keeps the PXA `DocumentExporter`
      public contract for converter facade tests. Initial WebApi export composition switching was completed
      in the follow-up Web/API export wiring slice. Verified with
      `dotnet build src/PXA.Infrastructure.Converters/PXA.Infrastructure.Converters.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI warnings remain),
      `dotnet test tests/PXA.Infrastructure.Converters.Tests/PXA.Infrastructure.Converters.Tests.csproj --no-restore --disable-build-servers -m:1`
      (9 passed), and
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~ExporterTests"`
      (66 passed).
- [x] `PXA.WebApi` export/spreadsheet composition wiring:
      Switched the active WebApi export registrations to `PXA.Application.UseCases.ExportDocumentUseCase`,
      `PXA.Core.Abstractions.IDocumentExporter`, `PXA.Infrastructure.Converters`, `PXA.Infrastructure.Word`,
      and `PXA.Infrastructure.Spreadsheet`. `ExportController` and `SpreadsheetController` now accept
      PXA contracts for export/spreadsheet routes and adapt only the PDF-special rendering paths back to
      the legacy `DesignJsonMapper`/`PXA.Pdf` bridge. WebApi no longer references
      `PXA.Infrastructure.Converters` or `PXA.Infrastructure.Spreadsheet`; `PXA.Infrastructure.Word`
      remains for current document-operation compatibility endpoints. Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Export|FullyQualifiedName~Spreadsheet"`
      (19 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Export|FullyQualifiedName~Spreadsheet"`
      (19 passed), and
      `dotnet test tests/PXA.Infrastructure.Converters.Tests/PXA.Infrastructure.Converters.Tests.csproj --no-restore --disable-build-servers -m:1`
      (9 passed).
- [x] `PXA.Importer` ownership promotion:
      Promoted the low-level PDF importer from a facade over `PXA.Importer` to PXA-owned source over
      `PXA.Infrastructure.Pdf`, including tokenizer, xref/object parsing, stream decoding, font parsing,
      graphics interpretation, document model, editing session, semantic analysis, debug overlay, and PDF
      regeneration bridge. `PXA.Importer` no longer references `PXA.Importer`; the legacy
      `PXA.Importer` project remains for compatibility and keeps its `PXA0002` obsolete diagnostic.
      `PXA.Importer.Pdf.LoadAsync(...)` remains the preferred PXA entry point. Verified with
      `dotnet build src/PXA.Importer/PXA.Importer.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed), and
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (95 passed).
- [x] `PXA.FileImporter` PDF ownership promotion:
      Promoted the PDF file importer inside the `PXA.FileImporter` aggregator from a wrapper over
      `PXA.FileImporter.Pdf` to PXA-owned source over `PXA.Importer` and `PXA.Core.Contracts`.
      `PXA.FileImporter` no longer references `PXA.FileImporter.Pdf`; the other file importer
      providers still wrap their legacy PXA implementations until their individual promotion slices.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      `dotnet test tests/PXA.FileImporter.Pdf.Tests/PXA.FileImporter.Pdf.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter` image ownership promotion:
      Promoted the raster image importer inside the `PXA.FileImporter` aggregator from a wrapper over
      `PXA.FileImporter.Image` to PXA-owned source over `PXA.Core.Contracts` and SkiaSharp, preserving
      A4 page placement, EXIF orientation handling, JPEG/PNG data URI passthrough, and corrupt-image
      validation behavior. `PXA.FileImporter` no longer references `PXA.FileImporter.Image`; the other
      file importer providers still wrap their legacy PXA implementations until their individual slices.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX/PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      `dotnet test tests/PXA.FileImporter.Image.Tests/PXA.FileImporter.Image.Tests.csproj --no-restore --disable-build-servers -m:1`
      (8 passed).
- [x] `PXA.FileImporter` SVG ownership promotion:
      Promoted the SVG importer inside the `PXA.FileImporter` aggregator from a wrapper over
      `PXA.FileImporter.Svg` to PXA-owned source over `PXA.Core.Contracts`, preserving SVG page-size
      detection, rect/text/image mapping, inline vector fallback for path/circle/ellipse/line/polyline/polygon,
      transforms, style color parsing, and `use`/defs expansion behavior. `PXA.FileImporter` no longer
      references `PXA.FileImporter.Svg`; DOC/DOCX/ODT/PPTX still wrap their legacy PXA implementations
      until their individual slices.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      `dotnet test tests/PXA.FileImporter.Svg.Tests/PXA.FileImporter.Svg.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter` DOC ownership promotion:
      Promoted the legacy Word 97-2003 `.doc` importer inside the `PXA.FileImporter` aggregator from a
      wrapper over `PXA.FileImporter.Doc` to PXA-owned source over `PXA.Core.Contracts`, preserving CFBF
      validation, `WordDocument` stream extraction, FIB text offsets, printable-text fallback, and single-page
      text layout behavior. `PXA.FileImporter` no longer references `PXA.FileImporter.Doc`; DOCX/ODT/PPTX
      still wrap their legacy PXA implementations until their individual slices.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (16 passed after rerunning serially; the first parallel attempt hit a transient `.deps.json` file lock),
      and
      `dotnet test tests/PXA.FileImporter.Doc.Tests/PXA.FileImporter.Doc.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter` ODT ownership promotion:
      Promoted the OpenDocument Text `.odt` importer inside the `PXA.FileImporter` aggregator from a wrapper
      over `PXA.FileImporter.Odt` to PXA-owned source over `PXA.Core.Contracts`, preserving ZIP/content.xml
      parsing, style extraction, paragraph/header text mapping, frame image mapping, metadata-title fallback,
      and portrait page layout behavior. `PXA.FileImporter` no longer references `PXA.FileImporter.Odt`;
      DOCX/PPTX still wrap their legacy PXA implementations until their individual slices.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (17 passed), and
      `dotnet test tests/PXA.FileImporter.Odt.Tests/PXA.FileImporter.Odt.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter` DOCX ownership promotion:
      Promoted the OOXML `.docx` importer inside the `PXA.FileImporter` aggregator from a wrapper over
      `PXA.FileImporter.Docx` to PXA-owned source over `PXA.Core.Contracts` and `DocumentFormat.OpenXml`,
      preserving page size/margin extraction, paragraph typography mapping, table mapping, inline image data URI
      extraction, and document metadata mapping. `PXA.FileImporter` no longer references
      `PXA.FileImporter.Docx`; PPTX is the final remaining legacy PXA file importer wrapper.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (18 passed), and
      `dotnet test tests/PXA.FileImporter.Docx.Tests/PXA.FileImporter.Docx.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter` PPTX ownership promotion:
      Promoted the PowerPoint `.pptx` importer inside the `PXA.FileImporter` aggregator from the final wrapper
      over `PXA.FileImporter.Pptx` to PXA-owned source over `PXA.Core.Contracts` and `DocumentFormat.OpenXml`,
      preserving slide/page mapping, shape/text/picture extraction, theme and background color resolution,
      inline image data URI extraction, and presentation metadata naming. `PXA.FileImporter` no longer
      references any `PXA.FileImporter.*` implementation project; all built-in file importers in the
      aggregator are now PXA-owned source.
      Verified with
      `dotnet build src/PXA.FileImporter/PXA.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (19 passed), and
      `dotnet test tests/PXA.FileImporter.Pptx.Tests/PXA.FileImporter.Pptx.Tests.csproj --no-restore --disable-build-servers -m:1`
      (1 passed).
- [x] `PXA.FileImporter.ImageAnalysis` ownership promotion:
      Promoted the editable raster image analysis importer from a facade over
      `PXA.FileImporter.ImageAnalysis` to PXA-owned source over `PXA.Core.Contracts`, `PXA.FileImporter`,
      and SkiaSharp. The PXA project now owns preprocessing, color analysis, shape detection, glyph/text
      recognition, scene assembly, debug overlay rendering, and character templates directly while preserving
      the public PXA `ImageAnalysisOptions` / `ImageAnalysisImportResult` contracts. Static
      `SupportedExtensions` remains available for existing call-sites, with explicit `IFileImporter`
      implementation for the common PXA importer contract. `PXA.FileImporter.ImageAnalysis` no longer
      references `PXA.FileImporter.ImageAnalysis`.
      Verified with
      `dotnet build src/PXA.FileImporter.ImageAnalysis/PXA.FileImporter.ImageAnalysis.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing SkiaSharp filter-quality warnings remain),
      `dotnet test tests/PXA.FileImporter.ImageAnalysis.Tests/PXA.FileImporter.ImageAnalysis.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed), and
      `dotnet test tests/PXA.FileImporter.ImageAnalysis.Tests/PXA.FileImporter.ImageAnalysis.Tests.csproj --no-restore --disable-build-servers -m:1`
      (179 passed; existing xUnit analyzer warnings remain).
- [x] `PXA.FileImporter.ImageOcr` ownership promotion:
      Promoted the scanned-image OCR-to-editable-design converter from adapters over
      `PXA.FileImporter.ImageOcr` to PXA-owned source over `PXA.Core.Contracts`, SkiaSharp, and Tesseract.
      The PXA project now owns OCR layout/fusion, visual element detection, text extraction, debug overlay
      rendering, embedded and process-isolated Tesseract engines, worker contracts, OCR pixel helpers, and
      converter orchestration directly while preserving the public PXA `IOcrEngine`, OCR model,
      `ImageToPdfConversionOptions`, and `ImageToPdfConversionResult` contracts. Adapter files
      `OcrEngineAdapters`, `OcrModelMapper`, and wrapper `TesseractOcrEngines` were removed.
      `PXA.FileImporter.ImageOcr` no longer references the `PXA.FileImporter.ImageOcr` project. The
      large `tessdata` / `native` assets are still linked from the legacy folder to avoid duplicating data.
      Verified with
      `dotnet build src/PXA.FileImporter.ImageOcr/PXA.FileImporter.ImageOcr.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PXA.FileImporter.ImageOcr.Tests/PXA.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed), and
      `dotnet test tests/PXA.FileImporter.ImageOcr.Tests/PXA.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (97 passed; existing dependency/WebApi warnings remain).
- [x] `PXA.FileImporter.ImageOcr.Worker` physical promotion:
      Added a PXA-owned process-isolated OCR worker project over `PXA.FileImporter.ImageOcr`, updated the
      PXA OCR engine default worker resolution to prefer `PXA.FileImporter.ImageOcr.Worker.dll`, and kept
      fallback resolution for the legacy `PXA.FileImporter.ImageOcr.Worker.dll` during the compatibility
      window. `PXA.WebApi`, `PXA.Api.Tests`, and `PXA.sln` now use the PXA worker project; `PXA.sln` and
      legacy PXA API tests continue to use the PXA worker. The WebApi OCR controller now uses the PXA
      OCR engine contract and converts the PXA design DTO back to the legacy PXA DTO only at the existing
      `DesignJsonMapper`/`PXA.Pdf` boundary.
      Verified with
      `dotnet build src/PXA.FileImporter.ImageOcr.Worker/PXA.FileImporter.ImageOcr.Worker.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.FileImporter.ImageOcr.Tests/PXA.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed). A full `dotnet build PXA.sln --no-restore --disable-build-servers -m:1`
      initially exposed a pre-existing `samples/PXA.Demo` ambiguity between `PXA.Infrastructure.Pdf` and
      `PXA.Infrastructure.Pdf`; the demo now references only `PXA.Generator` directly, and the full PXA
      solution build passes (0 errors; existing package/NPOI/analyzer/nullability/obsolete warnings remain).
- [x] `PXA.WebApi` file importer composition switch:
      Switched the active WebApi file importer registrations from `PXA.FileImporter.*` projects to
      `PXA.FileImporter`, `PXA.FileImporter.ImageAnalysis`, `PXA.FileImporter.ImageOcr`, and the PXA OCR
      worker. Import endpoints still return the existing PXA-compatible design JSON contract by converting
      PXA importer DTOs at the controller boundary. The PDF engine debug endpoint now uses `PXA.Importer`
      analysis/debugging types directly. `PXA.WebApi` no longer has project references or implementation
      `using` directives for `PXA.FileImporter.*`; OCR `tessdata` / `native` assets remain linked from
      the legacy folder to avoid duplicating large data during the compatibility window.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (19 passed),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
- [x] `PXA.WebApi` PDF viewer importer extraction switch:
      Switched the PDF viewer native annotation extraction and AcroForm extraction/fill parsing services from
      `PXA.Importer` to `PXA.Importer`. Follow-up WebApi composition work also moved the annotation
      flattening/redaction service to `PXA.Importer.Generation.PxaPdfGeneratorBridge`; the service still
      renders through the compatibility `PXA.Pdf` engine boundary, but no longer needs the legacy
      `PXA.Importer` project identity. A full restore was required after the project-reference changes to
      clear the stale `PXA.Application` -> `PXA` -> `PXA.Infrastructure.Pdf` asset graph.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
- [x] `PXA.WebApi` DocumentOps application composition switch:
      Switched the document operations controller for find/replace, clone, and page extraction from
      `PXA.Application.UseCases` to `PXA.Application.UseCases`. The HTTP contract still accepts and
      returns the existing PXA-compatible design JSON by converting through `PXA.Core.Contracts`
      adapters at the controller boundary. The DOCX signing endpoint now calls the PXA Word signing service.
      Template rendering/authentication remain on the legacy PXA application services until their
      controller contracts are moved or explicitly adapter-wrapped.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~DocumentOpsControllerTests"`
      (3 passed), and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~DocumentOpsControllerTests"`
      (3 passed).
- [x] `PXA.WebApi` template/auth application composition switch:
      Switched auth middleware/controller and stored-template repository wiring from `PXA.Application`
      / `PXA.Domain` to `PXA.Application` / `PXA.Domain`. `PXA.Application` now owns
      `AuthenticateUserUseCase`; `PXA.WebApi` no longer has direct project references to
      `PXA.Application`, `PXA.Domain`, `PXA.Infrastructure.Pdf`, or `PXA.Infrastructure.Word`.
      Stored-template rendering no longer depends on the removed legacy `RenderTemplateUseCase` registration;
      it uses the PXA template lookup and the compatibility `PXA.Pdf` engine boundary directly until a
      PXA-owned template renderer is introduced.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain) and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~TemplatesControllerTests|FullyQualifiedName~PdfViewerFormsControllerTests|FullyQualifiedName~PdfViewerAnnotationsControllerTests"`
      (16 passed).
- [x] `PXA.WebApi` PDF migration provider composition switch:
      Switched all active PDF code-converter services from direct `PXA.Migration.*Pdf` namespaces to
      `PXA.Migration.*` facades and replaced the many direct PDF-provider project references with the
      `PXA.Migration.Pdf` aggregator. Legacy PXA PDF migration provider projects still build as internal
      dependencies of the PXA facades during the compatibility window, but `PXA.WebApi` no longer composes
      directly against those provider identities. Report and spreadsheet migration providers remain a
      separate follow-up slice.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain) and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed).
- [x] `PXA.WebApi` spreadsheet migration provider composition switch:
      Switched all active spreadsheet code-converter services from direct `PXA.Migration.*` spreadsheet
      namespaces to the `PXA.Migration.Spreadsheet` facades and replaced the eight direct spreadsheet-provider
      project references with the `PXA.Migration.Spreadsheet` aggregator. Legacy PXA spreadsheet migration
      provider projects still build as internal dependencies of the PXA facade during the compatibility
      window, but `PXA.WebApi` no longer composes directly against those spreadsheet provider identities.
      Report migration providers remain the next WebApi migration-composition follow-up.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain) and
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed).
- [x] `PXA.WebApi` report migration provider composition switch:
      Switched the report-to-design API path from direct `PXA.Migration.*` report converters to
      `PXA.Migration.Report` facades and replaced the eight direct report-provider project references with
      the `PXA.Migration.Report` aggregator. The PXA report facade now exposes the WebApi-required
      detection/resource helpers: report package extraction, DevExpress `.resx` resource parsing,
      `LooksLike(...)` checks, and resource-aware DevExpress/RPX/Jasper conversion overloads. Legacy PXA
      report migration provider projects still build as internal dependencies of the PXA facade during the
      compatibility window, but `PXA.WebApi` no longer composes directly against those report provider
      identities.
      Verified with
      `dotnet build src/PXA.Migration.Report/PXA.Migration.Report.csproj --disable-build-servers -m:1`
      (0 errors; existing Stimulsoft warning remains),
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed), and
      `dotnet test tests/PXA.Migration.Report.Tests/PXA.Migration.Report.Tests.csproj --no-restore --disable-build-servers -m:1`
      (20 passed).
- [x] `PXA.WebApi` core contract composition switch:
      Switched active WebApi design/workbook contract usage from direct `PXA.Core.Contracts` to
      `PXA.Core.Contracts`. Document operations, export, image conversion, spreadsheet PDF rendering, and
      image-analysis debug paths now pass PXA design contracts directly instead of adapting through PXA
      DTOs. `DesignJsonMapper` now uses the PXA core layout planner facade, and the direct
      `PXA.Core` project reference was removed from `PXA.WebApi`; legacy `PXA.Core` still builds as
      an internal dependency of `PXA.Core` during the compatibility window.
      Verified with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --disable-build-servers -m:1 -v:minimal`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and `rg -n "PXA\.Core" PXA.WebApi --glob "*.cs" --glob "*.csproj"` (no matches).
- [x] `PXA.Migration.Report` core dependency cleanup:
      Removed the direct `PXA.Core` project reference from the PXA report migration aggregator. The report
      facade still adapts legacy report converter outputs through `PXA.Core.Contracts`/`PXA.Core`, while
      `PXA.Core` remains only a transitive compatibility dependency during the additive rename phase.
      Verified with
      `dotnet build src/PXA.Migration.Report/PXA.Migration.Report.csproj --no-restore --disable-build-servers -m:1`
      (0 errors) and
      `dotnet test tests/PXA.Migration.Report.Tests/PXA.Migration.Report.Tests.csproj --no-restore --disable-build-servers -m:1`
      (20 passed).
- [x] `PXA.Importer.Tests` direct importer dependency switch:
      Switched the PXA importer facade tests from a direct `PXA.Importer` project reference and legacy
      `PXA.Importer.PdfImporter` construction to the PXA-owned importer facade/class. Legacy importer
      compatibility remains covered by the dedicated PXA importer tests; the PXA test project now validates
      the active PXA importer surface without a direct PXA importer project reference.
      Verified with
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed) and `rg -n "PXA\.Importer|src/PXA\.Importer" tests/PXA.Importer.Tests --glob "*.cs" --glob "*.csproj"`
      (no matches).
- [x] `PXA.Export.Tests` direct export dependency switch:
      Replaced the PXA export test project wrapper over linked `PXA.Export.Tests` sources with
      PXA-owned composition tests. The project now references `PXA.Application`, `PXA.Core`,
      `PXA.Infrastructure.Converters`, `PXA.Infrastructure.Word`, and `PXA.Infrastructure.Spreadsheet`
      directly, covering HTML exporter lookup plus Word/Excel bytes through the active PXA contract types.
      The full legacy PXA export regression suite remains in `tests/PXA.Export.Tests`.
      Verified with
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed) and `rg -n "PXA\.|PXA\.Export|src/PXA|PXA.WebApi" tests/PXA.Export.Tests --glob "*.cs" --glob "*.csproj"`
      (no matches).
- [x] PDF engine physical/namespace rename (`PXA.Pdf` -> `PXA.Pdf`):
      Moved the PDF generator engine source (63 types, 77 files) out of the dual-linked `PXA/Pdf/**`
      folder into a new owned project `src/PXA.Pdf` and renamed `namespace PXA.Pdf` -> `namespace PXA.Pdf`
      (including `.Layout`, `.Rendering`, `.Serialization[.Security]` sub-namespaces). The engine is now a
      single assembly instead of being `Compile Include`-linked into both `PXA.Infrastructure.Pdf` and
      `PXA.Infrastructure.Pdf`, which removes the long-standing duplicate `PXA.Pdf` type-identity hazard.
      Both infrastructure projects now `ProjectReference` `PXA.Pdf`; the dead `PXA.Pdf.Compatibility.
      CorePrimitiveAdapters` file (the only `PXA.Core` dependency, referenced nowhere) was dropped. The
      engine namespace is `PXA.Pdf` rather than `PXA.Generator.Pdf` to avoid colliding with the promoted
      facade class `PXA.Generator.Pdf.CreateDocument()`; that facade entry point is unchanged. All in-repo
      consumers were switched to `PXA.Pdf`: both infrastructure projects, `PXA.Generator`, `PXA.Importer`,
      `PXA.Importer`, `PXA.Application`, `samples/PXA.Demo`, the root `PXA` exe, and WebApi. Runtime
      string surfaces were updated where they must track the type identity: the `PxaPdfGeneratorBridge`
      reflection lookups (`Assembly.GetType("PXA.Pdf.PdfImageData")`, `PXA.Pdf.PdfPage._elements`, etc.) and
      the template-script Roslyn imports (`.WithImports("PXA.Pdf", ...)`). Migration-provider *generated
      output* (Roslyn `SyntaxFactory.ParseName("PXA.Pdf")`, skeleton code, diagnostic messages) and the
      matching test assertions were intentionally left on `PXA.Pdf` — the emitted target namespace belongs
      to the separate migration-provider-promotion slice. `src/PXA.Pdf` added to `PXA.sln` and `PXA.sln`.
      Verified with per-project builds (`PXA.Pdf`, `PXA.Infrastructure.Pdf`, `PXA.Infrastructure.Pdf`,
      `PXA.Generator`, `PXA.Importer`, `PXA.Importer`, `PXA.Application`, `PXA.FileImporter`,
      `samples/PXA.Demo`, `PXA`, and `PXA.WebApi`; all 0 errors) and with
      `dotnet test` for `PXA.Infrastructure.Pdf.Tests` (5), `PXA.Infrastructure.Pdf.Tests` (34),
      `PXA.Generator.Tests` (5), `PXA.Importer.Tests` (3), `PXA.Importer.Tests` (95), `PXA.Api.Tests` (61),
      `PXA.Api.Tests` (61), `PXA.Export.Tests` (3), and the PXA/PXA PDF migration provider suites
      (iText7/Apryse/DevExpressPdf etc.) — all passing.
      Note: `tests/PXA.Export.Tests` has a pre-existing build break unrelated to this slice — it passes a
      `PXA.Core.Contracts.DesignExportDto` to `DesignJsonMapper.MapToPdfDocument(...)`, which already took
      `PXA.Core.Contracts.DesignExportDto` at HEAD after the earlier WebApi core-contract promotion. That is
      a Core-contract tail item, not part of the PDF engine rename.
- [x] Orphan legacy file-importer cleanup:
      Deleted the fully-unreferenced legacy file-importer implementation projects
      `PXA.FileImporter.{Doc,Docx,Odt,Pptx,Svg}` (source already promoted into `PXA.FileImporter`) together
      with their placeholder `UnitTest1.cs` test shells (no project reference / no real coverage), and removed
      the stale untracked `src/PXA.Infrastructure.Sheet` build-artifact directory (no source, no references).
      All removed from `PXA.sln` and `PXA.sln`. Verified both solutions still enumerate cleanly and
      `PXA.FileImporter.Tests` (19 passed) still covers the promoted importer functionality.
      Remaining legacy file-importer teardown (Image/ImageAnalysis/ImageOcr/Pdf/Abstractions + `PXA.Importer`)
      is deferred because those carry real legacy test coverage (Image 8, ImageAnalysis 179, ImageOcr 97,
      Importer 95) that should be retargeted to PXA before the projects are removed.
- [x] File-importer domain fully retired to PXA (coverage-preserving):
      Retargeted every remaining legacy file-importer test suite onto the PXA-owned source (namespace swaps,
      project-reference repoint) and deleted the legacy implementation projects. Specifics:
      `PXA.FileImporter.Image` -> merged 8 tests into `PXA.FileImporter.Image.Tests` (8 passed);
      `PXA.FileImporter.ImageAnalysis` -> merged its 6 test files + `Fixtures/RealSamples` into
      `PXA.FileImporter.ImageAnalysis.Tests` (181 passed) — also removed a redundant `bool includeDebugOverlay`
      `ImportWithAnalysis` overload that collided with the `ImageAnalysisOptions?` overload;
      `PXA.FileImporter.ImageOcr` -> relocated the 69 MB `tessdata`/`native` assets into
      `src/PXA.FileImporter.ImageOcr`, repointed all four asset linkers (PXA impl, PXA.Api.Tests,
      PXA.Api.Tests), merged 7 test files + fixtures into `PXA.FileImporter.ImageOcr.Tests` (99 passed),
      and added `InternalsVisibleTo=PXA.FileImporter.ImageOcr.Tests`;
      `PXA.Importer` + `PXA.FileImporter.Pdf` -> merged the 95-test `PdfImporterCoreTests` into
      `PXA.Importer.Tests` (98 passed, adding `InternalsVisibleTo` on `PXA.Importer`/`PXA.FileImporter` and
      updating one font-asset assertion from the intentionally renamed `PxaPdf_` prefix to `PxaPdf_`);
      deleted the orphaned `PXA.FileImporter.Abstractions` and the broken `PXA.FileImporter.ImageOcr.Worker`
      (repointing `PXA.Api.Tests` to `PXA.FileImporter.ImageOcr.Worker`). No `PXA.FileImporter.*` projects
      remain. Checkpoint: both solutions enumerate cleanly, no dangling references, and `PXA.Api.Tests` (61) /
      `PXA.Api.Tests` (61) both green after the worker repoint.

## Future Test Plan

- [x] `dotnet build PXA.sln`
      Passed with `--disable-build-servers` after the physical sample/schema alias updates (0 errors;
      59 existing dependency/analyzer/nullability/XML-doc/NPOI warnings remain).
- [x] `dotnet build PXA.sln`
      Passed with `--disable-build-servers -m:1` after the frontend/package naming slice (0 errors;
      246 existing warnings remain, mostly NuGet security-index/network warnings plus known package,
      nullability, XML-doc, xUnit analyzer, and NPOI license warnings). A first parallel build attempt
      produced no compiler errors but was stopped after a silent five-minute hang; serial build completed.
- [x] `dotnet build PXA.slnx` if kept as an active solution entry.
      Not run as an active build target because current `PXA.slnx` is an empty `<Solution>` with no project entries.
      Added matching empty `PXA.slnx` as the PXA-named solution-file alias; real builds continue through
      `PXA.sln` and `PXA.sln`.
- [x] Relevant migration tests.
      Initial `PXA.Migration.Abstractions.Tests` coverage added for PXA-to-PXA source migration adapter mapping.
      Initial `PXA.Migration.Pdf.Code.DevExpress.Tests` coverage added for the first concrete PXA migration provider facade.
      Initial `PXA.Migration.Pdf.Code.Syncfusion.Tests` and `PXA.Migration.Pdf.Code.IText7.Tests` coverage added for core PDF
      provider facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.Pdf.Code.Aspose.Tests` and `PXA.Migration.Pdf.Code.Apryse.Tests` coverage added for provider
      facade compatibility and diagnostic severity mapping.
      Initial `PXA.Migration.Pdf.Code.DsPdf.Tests` and `PXA.Migration.Pdf.Code.Foxit.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.Pdf.Code.IronPdf.Tests` and `PXA.Migration.Pdf.Code.GemBox.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.Pdf.Code.Spire.Tests` and `PXA.Migration.Pdf.Code.PdfKitNet.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.Pdf.Code.Leadtools.Tests` and `PXA.Migration.Pdf.Code.ActivePdf.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.Pdf.Code.PdfTools.Tests` and `PXA.Migration.Pdf.Code.PdfToolsToolbox.Tests` coverage added for
      diagnostics-first and direct-generation provider facade compatibility.
      Initial `PXA.Migration.Pdf.Tests` coverage added for provider registry keys, factory creation,
      case-insensitive lookup, and unknown-key rejection.
      Initial `PXA.Migration.Report.Tests` coverage added for report provider registry keys and
      DevExpress Report/RDL/RPX facade conversion smoke tests.
      `PXA.Migration.Report.Tests` expanded for JasperReports and ActiveReports JS facade smoke coverage.
      `PXA.Migration.Report.Tests` expanded for FastReport, Telerik, and Stimulsoft facade smoke coverage.
      `PXA.Migration.Report.Tests` expanded to verify report migrations return the PXA core design contract.
      Initial `PXA.Migration.Spreadsheet.Tests` coverage added for spreadsheet provider registry keys,
      factory creation, case-insensitive lookup, unknown-key rejection, and ClosedXML/EPPlus smoke tests.
      Verified with `dotnet test --no-build` for `PXA.Migration.Pdf.Tests` (18 passed) and
      `PXA.Migration.Report.Tests` (20 passed). `PXA.Migration.Spreadsheet.Tests` passed with build
      after adding all-provider PXA facade smoke coverage (19 passed).
- [x] Spreadsheet migration tests:
      - Aspose.Cells
      - ClosedXML
      - EPPlus
      - GemBox.Spreadsheet
      - NPOI
      - Spire.XLS
      - SpreadsheetLight
      - Syncfusion XlsIO
      Added PXA facade smoke coverage for every listed provider and verified with
      `dotnet test tests/PXA.Migration.Spreadsheet.Tests/PXA.Migration.Spreadsheet.Tests.csproj`
      (19 passed).
- [x] Generator compatibility tests:
      - New code with `using PXA.Generator;`
      - Legacy code with `using PXA.Pdf;`
      Initial `PXA.Generator.Tests` coverage added for `Pdf.CreateDocument()`, `Spreadsheet.CreateWorkbook()`,
      and `Word.Export(...)`.
      `PXA.Generator.Word.Export(...)` now accepts `PXA.Core.Contracts.DesignExportDto` and adapts to
      the legacy PXA exporter internally.
      Added explicit legacy `PXA.Pdf.PdfDocument` smoke coverage and verified with
      `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj` (5 passed).
      Re-verified after adding `PXA0001` obsolete guidance to the legacy `PXA.Pdf.PdfDocument(...)`
      constructor with `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --disable-build-servers`
      (5 passed).
- [x] Importer compatibility tests:
      - New code with `using PXA.Importer;`
      - Legacy code with `using PXA.Importer;`
      Initial `PXA.Importer.Tests` coverage added for `Pdf.LoadAsync(...)` and PXA-facing import options.
      Initial `PXA.FileImporter.Tests` coverage added for file importer registry keys, factory creation,
      case-insensitive/extension lookup, unknown-key rejection, and SVG/Image facade smoke tests.
      Initial `PXA.FileImporter.ImageAnalysis.Tests` and `PXA.FileImporter.ImageOcr.Tests` coverage added for
      specialized analysis/OCR facades, diagnostics mapping, PXA OCR engine adapter flow, and Tesseract facade identity.
      File importer design results now return `PXA.Core.Contracts.DesignExportDto`.
      Added explicit legacy `PXA.Importer.PdfImporter` smoke coverage and verified with
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj` (3 passed),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj` (15 passed),
      `dotnet test --no-build` for `PXA.FileImporter.ImageAnalysis.Tests` (2 passed), and
      `PXA.FileImporter.ImageOcr.Tests` (2 passed).
      Re-verified after adding `PXA0002` obsolete guidance with
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --disable-build-servers --no-restore -m:1`
      (3 passed) and `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore
      --disable-build-servers -m:1` (0 errors; existing package/NPOI/nullable warnings remain; no internal
      `PXA0002` warnings).
- [x] Core compatibility tests:
      Initial `PXA.Core.Tests` coverage added for DesignExportDto, SpreadsheetDto, and ExportOptions adapters.
      Verified with `dotnet test tests/PXA.Core.Tests/PXA.Core.Tests.csproj --no-build` (3 passed).
- [x] Application compatibility tests:
      Initial `PXA.Application.Tests` coverage added for CloneTemplate, ExtractPages, FindAndReplace,
      and ValidateTemplate facades.
      Verified with `dotnet test tests/PXA.Application.Tests/PXA.Application.Tests.csproj --no-build` (4 passed).
- [x] Infrastructure compatibility tests:
      Initial `PXA.Infrastructure.Pdf.Tests` coverage added for PDF rendering, file generation,
      diagnostics reading, capabilities, and service delegation.
      Initial `PXA.Infrastructure.Word.Tests` coverage added for DOCX export with PXA core contracts,
      Word renderer capabilities, and digital-signing facade delegation.
      Initial `PXA.Infrastructure.Spreadsheet.Tests` coverage added for fluent workbook authoring,
      XLSX import/export, calculation, validation, spreadsheet-to-design, Excel document export,
      and sheet renderer capabilities.
      Initial `PXA.Infrastructure.Converters.Tests` coverage added for text/image/ODT converter exports,
      converter capabilities, and PXA core contract adaptation.
      Verified with `dotnet test --no-build` for `PXA.Infrastructure.Pdf.Tests` (5 passed),
      `PXA.Infrastructure.Word.Tests` (3 passed), `PXA.Infrastructure.Spreadsheet.Tests` (6 passed),
      and `PXA.Infrastructure.Converters.Tests` (9 passed).
- [x] Domain compatibility tests:
      Initial `PXA.Domain.Tests` coverage added for design template conversion, validation result conversion,
      and PXA/PXA repository adapter flows.
      Verified with `dotnet test tests/PXA.Domain.Tests/PXA.Domain.Tests.csproj --no-build` (4 passed).
- [x] Web/API compatibility tests:
      `PXA.Api.Tests` now covers legacy and PXA branding discovery routes with compatibility notes.
      `PXA.Api.Tests` now covers PXA export alias routes for format discovery and HTML export.
      `PXA.Api.Tests` now covers PXA migration alias routes for framework discovery and code conversion.
      `PXA.Api.Tests` now covers PXA document alias routes for find/replace, clone, and image conversion
      unsupported-format handling.
      `PXA.Api.Tests` now covers PXA spreadsheet alias routes for JSON-to-workbook creation and
      workbook validation.
      `PXA.Api.Tests` now covers PXA templates alias routes for template listing and validation.
      `PXA.Api.Tests` now covers PXA PDF Viewer alias routes for annotation sidecar save/get
      and form field extraction.
      Added and verified `PXA.Api.Tests` alias with
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --disable-build-servers` (61 passed).
      Re-verified WebApi compilation after adding the first obsolete shim with
      `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI warnings remain; no `PXA0001` internal warnings).
- [x] Export compatibility tests:
      Added and verified `PXA.Export.Tests` alias with
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --disable-build-servers` (206 passed).
- [x] `npm run build` in `ui-designer-v2`.
      Passed after adding the primary `docs/schema/pxa-workbook.schema.json` reference to the in-app docs
      (Vite chunk-size warning remains existing/non-blocking).
- [x] Relevant Jest tests in `ui-designer-v2`.
      `npm test -- --runTestsByPath src/__tests__/canvasWorkbookSchema.test.ts` passed for the primary PXA
      schema and legacy PXA schema alias.
- [x] MCP smoke test if `tools/PXA.Mcp` is renamed or rebranded.
      `npx tsx smoke.ts` passed and now verifies both `pxa://schema/pxa-workbook` and
      `canvas://schema/canvas-workbook` resources.
- [x] UI smoke test for migration page, designer open flow, export, and preview.
      Added `ui-designer-v2/src/__tests__/appRouteSmoke.test.tsx` using the existing Jest/jsdom setup.
      It covers the migrations landing route, report-designer conversion handoff into `/create`, designer
      preview mode, JSON export callback, and the PDF viewer route. Verified with
      `npm test -- --runTestsByPath src/__tests__/appRouteSmoke.test.tsx src/__tests__/pdfViewerSmoke.test.tsx`
      (4 passed) and `npm run build` in `ui-designer-v2` (passed; existing chunk-size warning remains).
      This is a lightweight route/component smoke test; a full Playwright/Cypress browser harness with
      screenshots remains optional future hardening, not a blocker for the current PXA rename checklist.
- [x] Documentation link check after main docs are updated.
      Checked 111 tracked `.md` / `.mdx` / `.txt` documentation files with a local-link verifier that resolves
      relative links, repo-root links such as `/docs/...`, `.md` suffix fallbacks, and `README.md` / `index.md`
      directory targets. No broken local markdown links found. External links and `xref:` API references were
      intentionally not crawled.

## Assumptions

- [x] This checklist reserves `.pxa`; it does not implement the file format.
- [x] This checklist reserves `pxa`; it does not implement the CLI.
- [x] Existing uncommitted workspace changes are left untouched.
- [x] The first implementation block should avoid physical path/project renames unless explicitly approved.
- [x] Current repo state includes Spreadsheet engine/migrations and PdfPreview V2 roadmap; the rename plan must cover those additions.
- [x] Migration domain fully retired to PXA (coverage-preserving):
      Promoted every `PXA.Migration.*` provider engine into a PXA-owned project and deleted all PXA
      migration projects (32 provider/base projects total). Steps:
      created `PXA.Migration.Roslyn` (the `CSharpSourceMigration` base) and added `ExpressionTranslator` /
      `ReportPackageExtractor` helpers into `PXA.Migration.Abstractions`; moved each provider's engine source
      into its `PXA.Migration.<Provider>` project (namespace `PXA.Migration.*` -> `PXA.Migration.*`,
      `PXA.Core.Contracts` -> `PXA.Core.Contracts`, `PXA.Migration.Abstractions/Roslyn` -> PXA); for the
      15 PDF providers the thin PXA facades were replaced by the promoted engines (aggregator unchanged); for
      the 8 spreadsheet and 8 report providers new `PXA.Migration.<Provider>` + `.Tests` projects were created
      and the `PXA.Migration.Spreadsheet` / `PXA.Migration.Report` aggregators were repointed. Because the
      engines now implement PXA `ISourceMigration` / return `PXA.Core.Contracts` directly, the aggregators'
      `.AsPxaMigration()`, `.ToPxa()`, and `ReportDiagnosticMapper` bridges were removed, and the dead
      `PxaSourceMigrationAdapter` / `SourceMigrationAdapters` were deleted. Legacy per-provider test suites
      were retargeted to the PXA engines (thin PXA facade tests renamed to `<Class>PxaFacadeTests` to avoid
      collisions; the Rdl `Fixtures/*.rdl` restored). No `PXA.Migration.*` projects remain.
      Verified: all 31 PXA migration provider/aggregator suites green (PDF 184, spreadsheet incl. aggregator,
      report incl. Rdl 94 + DevExpressReport 75, aggregators Pdf 18 / Spreadsheet 19 / Report 20),
      `PXA.Api.Tests` migration filter (16), and `dotnet build PXA.WebApi` (0 errors).
      Note: migration *generated output* still emits `using PXA.Pdf;` — switching the emitted target
      namespace to `PXA.Pdf` remains a separate deliberate step (the engine physical rename is done; the
      emitted-namespace product decision is independent).
- [x] Foundational teardown COMPLETE — the rename is finished (zero `PXA.*` projects/namespaces):
      Phase A: decoupled the PXA side from PXA by deleting the test-only compatibility bridges
      (`PXA.Core/Contracts/ContractAdapters.cs`, `PXA.Domain/DomainAdapters.cs`) and dropping the
      `PXA.Core`/`PXA.Domain` project references, making `PXA.Core`/`PXA.Domain` standalone; obsolete
      bridge tests were removed and mixed adapter tests trimmed to their PXA-only cases.
      Phase B: retargeted every remaining legacy foundational test suite onto PXA source, preserving coverage —
      `PXA.Core.Tests`->`PXA.Core.Tests` (40, `PxaExpressionEvaluator`->`PxaExpressionEvaluator`),
      `PXA.Application.Tests`->`PXA.Application.Tests` (12), `PXA.Infrastructure.Pdf.Tests`->
      `PXA.Infrastructure.Pdf.Tests` (39, re-added the `UglyToad.PdfPig` package), and the big
      `PXA.Export.Tests`->`PXA.Export.Tests` (209) whose pre-existing Core-contract build break was fixed
      by the `PXA.Core`->`PXA.Core` swap. `PXA.Api.Tests` sources were promoted into `PXA.Api.Tests`
      (61) and the `<Compile Include>` link dropped.
      Phase C: deleted the entire legacy foundational cluster once it had zero external consumers — the root
      `PXA` demo exe (+`PXA.MinimalPdf`), `PXA.Domain`, `PXA.Core`, `PXA.Application`, and
      `PXA.Infrastructure.{Pdf,Word,Spreadsheet,Converters}`, plus `PXA.Api.Tests`.
      Phase D: switched migration *generated output* from `using PXA.Pdf;` to `using PXA.Pdf;` across all
      provider engines + assertions (47 files); renamed the last PXA-named type
      `PxaPdfGeneratorBridge`->`PxaPdfGeneratorBridge`; deleted the legacy `PXA.sln`/`PXA.slnx`
      (PXA.sln/PXA.slnx are now the only solutions).
      Final state: 0 projects named `PXA.*`, 0 `namespace PXA.*` declarations, 0 real `using PXA.*`
      directives; `dotnet build PXA.sln` passes with 0 errors. Remaining `PXA` text is limited to cosmetic
      prose in comments/branding strings and legacy localStorage keys. The legacy `ui-designer/` frontend
      folder is intentionally left in place as historical (already branded PXA-legacy in Phase 7; it is a
      separate npm app that contributes no `PXA.*` project/namespace references).
