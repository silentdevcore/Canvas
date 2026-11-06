# PVAD Legacy Rename Archive

Historical record: results and task states are retained. Names and rerun references
are editorially normalized; original commands and wording remain in Git at
`30496722`. This is not a new execution or release approval.

## Summary

Track the completed predecessor-to-current document automation rename.

Decision: **Option A / breaking rename is accepted**. This historical rollout retired the predecessor source namespace rather than shipping source-level shims. HTTP routes, JSON fields, browser storage and stable migration diagnostics retain their documented compatibility boundaries.

Current technical identities are PVAD; the market identity is PVADE. Original names, package decisions and execution statements remain available in the baseline Git snapshot.

## Product Naming

- [x] Product/Web name: **PVADE**
- [x] Short/developer name: **PVAD**
- [x] CLI name reserved: `pvad`
- [x] Native Designer formats are `.pvadp` and `.pvads`; the earlier generic
  project-format reservation is retired. See
  [PVAD.Native-Designer-Formats.md](PVAD.Native-Designer-Formats.md).

## Namespace Mapping

| Historical responsibility | Current technical responsibility |
| --- | --- |
| PDF generation | `PVAD.Pdf` and `PVAD.Generator` |
| Migration providers | `PVAD.Migration.*` |
| PDF import | `PVAD.Importer` |
| File import | `PVAD.FileImporter.*` |
| Core contracts | `PVAD.Core` |
| Application services | `PVAD.Application` |
| Infrastructure | `PVAD.Infrastructure.*` |
| Web API | `PVAD.WebApi` |
| Domain model | `PVAD.Domain` |

Original project-name columns belong to the Git snapshot, not a current rename
from one identical namespace to itself. Later sections use historical role names
for retired facades and wrappers. Current paths are rerun or navigation references,
not a claim that those paths existed when the recorded results were produced.

## Current Repo Additions To Include

- [x] Include `PVAD.sln` and `PVAD.slnx` in the rename plan.
- [x] Include all predecessor implementation projects in the namespace/project rename inventory.
- [x] Include `samples/PVAD.Demo`.
- [x] Include `tools/PVAD.Mcp` package name, README, and smoke script.
- [x] Include `docs/schema/canvas-workbook.schema.json` and schema `$id`/title naming.
- [x] Include `llms.txt`, `llms-full.txt`, and generated OpenAPI/doc artifacts when docs are renamed.
      At this checkpoint, the AI reference files used the then-current identity while
      documenting predecessor PDF compatibility. The OpenAPI snapshot used the then-current title and
      included additive API aliases for the documented document/export/migration/pdf-viewer/template
      routes while retaining the legacy `/api/...` paths. Auth remains on `/api/auth` by design.
- [x] Include frontend folder cleanup: legacy `ui-designer` removed; active frontend folder is `pvad-designer`.
- [x] Include package names/routes/visible branding in frontend only after backend API compatibility is protected.

## Compatibility Rules

- [x] Treat the code rename as breaking: legacy namespace family source projects and namespaces are retired.
- [x] Do not promise source-level `[Obsolete]` shims for legacy namespace family in the active repository.
- [x] Keep old NuGet/package identities or publish forwarding packages only if a prior external package was
      actually published under a legacy namespace family identity. Current repo audit found no published NuGet/package
      identity to forward yet: no `.nuspec`, no explicit `<PackageId>`, and no `<GeneratePackageOnBuild>`
      metadata for product projects.
- [x] Keep old HTTP endpoints compatible.
- [x] Keep old JSON fields compatible.
- [x] Add then-current response fields only alongside legacy fields.
- [x] Keep `CANMIG...` diagnostic IDs stable for now.
- [x] Do not rename unrelated terms such as HTML canvas, `html2canvas`, `SKCanvas`, or iText `PdfCanvas`.
- [x] Do not rename user/domain words like "canvas" in HTML/CSS drawing contexts unless they refer to the product brand.
- [x] Keep existing document JSON schema fields stable unless a versioned schema migration is added.

Compatibility verification note: runtime/API compatibility is preserved for legacy HTTP routes, JSON
contracts, localStorage keys, `CANMIG...` diagnostics, and technical canvas terms. Source compatibility for
legacy namespace family namespaces is intentionally not preserved after the breaking predecessor rename.

## Option A Completion

- [x] Active main docs updated to describe the promoted implementation instead of an additive facade over the predecessor.
- [x] Source-level legacy namespace family shim promises removed from active documentation.
- [x] DocFX and schema docs updated to point at predecessor implementation projects and contracts.
- [x] Spreadsheet migration generated output updated from the predecessor infrastructure namespace to
      the then-current Spreadsheet API.
- [x] PDF document constructor guidance updated to recommend the promoted generator
      without referring to a source-compatibility window.
- [x] OCR worker lookup strings updated to the then-current worker name.
- [x] Active code/docs scan completed: remaining legacy namespace family mentions in main docs are limited to explicit
      historical/glossary notes and compatibility aliases.
- [x] Final product-name cleanup completed:
      - generated migration responses expose the paired compatibility code fields
      - spreadsheet authoring uses workbook, worksheet, cell, range and column APIs
      - OCR layout builder uses `PvadElementBuilder`
      - MCP exposes the then-current resource and tool identity
      - obsolete workbook schema alias removed; the promoted workbook schema became canonical
      - active documentation and UI migration docs no longer reference predecessor compatibility names

Verification for this slice:

- [x] `git diff --check`
- [x] `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `dotnet test tests/PVAD.Migration.Spreadsheet.Tests/PVAD.Migration.Spreadsheet.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] Spreadsheet provider tests for AsposeCells, ClosedXML, EPPlus, GemBox, NPOI, Spire.XLS,
      SpreadsheetLight, and Syncfusion XlsIO.
- [x] `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-build --no-restore --disable-build-servers -m:1 --filter "Migration"`
- [x] `dotnet test tests/PVAD.Infrastructure.Spreadsheet.Tests/PVAD.Infrastructure.Spreadsheet.Tests.csproj --no-build --no-restore --disable-build-servers -m:1`
- [x] `npm run build` in `pvad-designer`

## Documentation Plan

- [x] Move main docs to the then-current product and technical names.
- [x] Update active examples in main docs to use legacy namespace family APIs.
      Active examples use the generator and PDF engine for PDF option/value
      types such as `PdfColor`, `PdfDrawTextOptions`, and `PdfPagePreset`.
- [x] Preserve historical checklist context; original wording remains in Git after the later Markdown cleanup.
- [x] Distinguish historical records from current instructions without changing their task states.
- [x] Add a glossary separating product, developer/API/CLI and retired source identities.
- [x] Update docs schemas and generated OpenAPI only after code/API names are stabilized.
      predecessor workbook schema alias and OpenAPI predecessor route aliases are present; legacy schema/path contracts remain.

## Future Implementation Phases

- [x] Phase 0: inventory all the predecessor identity occurrences and classify as product namespace, file/project path, UI branding, docs, schema, or unrelated HTML/graphics canvas.
      Initial inventory completed from current repo state: product namespaces/projects, UI/editor canvas terms, docs/checklists,
      schemas, MCP, samples, and unrelated HTML/graphics canvas usages were identified as separate rename buckets.
- [x] Phase 1: introduce new legacy namespace family public API layer without physical path/project renames.
      Started with additive legacy Generator component project and PDF/Word/Spreadsheet facades that delegate to the existing
      predecessor implementations.
      Completed for the current compatibility layer: additive predecessor projects now cover generator, importer,
      file importer, migration, core, application, domain, infrastructure, API/export test aliases, MCP,
      schema, and sample entry points while legacy predecessor project paths remain compatible.
- [x] Phase 2: add generator facade target for PDF/Word/Spreadsheet:
      - legacy Pdf component -> legacy Generator.Pdf component / legacy Generator component
      - Word export APIs -> legacy Generator.Word component
      - Spreadsheet APIs -> legacy Generator.Spreadsheet component
- [x] Phase 3: promote the public migration facade over predecessor providers, including PDF, report, and spreadsheet providers.
      Started with additive legacy Migration.Abstractions component project, legacy facing component migration result/diagnostic types,
      and a legacy SourceMigrationAdapter component bridge for existing legacy Migration.Abstractions.ISourceMigration component
      implementations.
      First concrete provider facade added with legacy Migration.Pdf.Code.DevExpress component, delegating to the existing
      legacy Migration.Pdf.Code.DevExpress.DevExpressPdfMigration component while returning predecessor migration abstractions.
      PDF-code migration facade set expanded with legacy Migration.Pdf.Code.Syncfusion component and legacy Migration.Pdf.Code.IText7 component,
      both delegating to existing predecessor providers and preserving `CANMIG...` diagnostic IDs.
      Additional central PDF provider facades added with legacy Migration.Pdf.Code.Aspose component and legacy Migration.Pdf.Code.Apryse component.
      PDF provider facade coverage expanded with legacy Migration.Pdf.Code.DsPdf component and legacy Migration.Pdf.Code.Foxit component.
      PDF provider facade coverage expanded with legacy Migration.Pdf.Code.IronPdf component and legacy Migration.Pdf.Code.GemBox component.
      PDF provider facade coverage expanded with legacy Migration.Pdf.Code.Spire component and legacy Migration.Pdf.Code.PdfKitNet component.
      PDF provider facade coverage expanded with legacy Migration.Pdf.Code.Leadtools component and legacy Migration.Pdf.Code.ActivePdf component.
      PDF provider facade coverage expanded with legacy Migration.Pdf.Code.PdfTools component and legacy Migration.Pdf.Code.PdfToolsToolbox component.
      Shared PDF migration entry point added with legacy Migration.Pdf component, exposing provider keys and
      `PdfMigrationProviders.Create(...)` / `TryCreate(...)` for CLI/API use.
      Report migration entry point added with legacy Migration.Report component, legacy facing component report result/contract,
      and provider keys for DevExpress Report, RDL, and RPX designer migrations.
      Report migration registry expanded with JasperReports JRXML and ActiveReports JS JSON facades.
      Report migration registry expanded with FastReport FRX, Telerik TRDX, and Stimulsoft MRT facades.
      Spreadsheet migration entry point added with legacy Migration.Spreadsheet component, provider facades, and
      registry keys for Aspose.Cells, ClosedXML, EPPlus, GemBox.Spreadsheet, NPOI, Spire.XLS,
      SpreadsheetLight, and Syncfusion XlsIO.
- [x] Phase 4: promote importer and file importer namespaces:
      - consolidate the predecessor PDF importer behind the public importer facade
      - consolidate file-format importers behind the public file-import contract
      Started with additive legacy Importer component project and PDF import facade that delegates to the existing
      legacy Importer.PdfImporter component.
      File importer entry point added with legacy FileImporter component, legacy facing component `IFileImporter`, provider keys,
      and registry/facades for PDF, DOCX, DOC, ODT, SVG, PPTX, and raster image import.
      Specialized additive facades added with legacy FileImporter.ImageAnalysis component and legacy FileImporter.ImageOcr component,
      including legacy facing component analysis options/results/diagnostics, OCR options/results/models, OCR engine contract,
      Tesseract engine facades, and predecessor adapters. The OCR worker executable/package name remains a later
      physical rename item in Phase 8/9.
- [x] Phase 5: move infrastructure, application, core, and domain namespaces.
      Started with additive legacy Core component project and predecessor-owned contract types for design documents,
      spreadsheet workbooks, and export options. JSON-compatible adapters bridge legacy Core.Contracts component
      to the existing legacy Core.Contracts component types while engines still use the legacy internals.
      Public predecessor generator and file importer facades now expose legacy Core.Contracts component instead of
      legacy Core.Contracts component for document import/export surfaces. Remaining follow-up: report migration
      result contracts, application use cases, infrastructure facades, and domain-facing model aliases.
      Report migration results now expose legacy Core.Contracts.DesignExportDto component while existing
      predecessor report converters remain the internal implementation.
      Application facade started with additive legacy Application component project for Clone, ExtractPages,
      FindAndReplace, and ValidateTemplate use cases. Design use cases expose legacy Core.Contracts component;
      ValidateTemplate now accepts legacy Domain.Repositories.ITemplateRepository component and uses the
      legacy to component repository adapter internally.
      PDF infrastructure facade started with additive legacy Infrastructure.Pdf component project for renderer,
      facade, diagnostics reader, output writer, renderer capabilities, and PDF service hooks.
      Word infrastructure facade started with additive legacy Infrastructure.Word component project for DOCX export,
      renderer capabilities, and DOCX digital-signing facade.
      Spreadsheet infrastructure facade started with additive legacy Infrastructure.Spreadsheet component project for
      fluent workbook authoring, XLSX/XLS/CSV IO, spreadsheet calculation, validation, spreadsheet-to-design
      conversion, Excel document export, operations, and sheet renderer capabilities.
      Converter infrastructure facade started with additive legacy Infrastructure.Converters component project for
      HTML, Markdown, CSV, XML, SVG, PNG/JPEG/TIFF, ODT exports and converter renderer capabilities.
      Domain facade started with additive legacy Domain component project for design templates, template metadata,
      page settings, designer elements, element config models, validation results, template name info,
      and predecessor implementation repository adapters.
      Completed for additive facade coverage and verified through the predecessor solution/test aliases. Remaining
      work here is physical namespace/path replacement, which belongs to Phase 9 and the later breaking
      rename plan.
- [x] Phase 6: update Web/API branding to the then-current identity while preserving legacy endpoints.
      Started with additive Web API branding discovery endpoint exposed on both legacy
      branding discovery routes. The response advertised
      legacy PVADE component naming, the reserved predecessor implementation CLI, native legacy p component/legacy s component formats, and legacy
      predecessor compatibility notes.
      Export API alias added with predecessor implementation while legacy `/api/export` remains compatible.
      Migration API alias added with predecessor implementation while legacy `/api/migration` remains compatible.
      Document API alias added with predecessor implementation while legacy `/api/document` remains compatible,
      covering document operations and image-to-PDF conversion routes.
      Spreadsheet API alias added with predecessor implementation while legacy `/api/spreadsheet` remains compatible.
      Templates API alias added with predecessor implementation while legacy `/api/templates` remains compatible.
      PDF Viewer API aliases added with predecessor implementation and predecessor implementation
      while legacy `/api/pdf-viewer/*` routes remain compatible.
      Auth remains intentionally on the platform route `/api/auth`; no predecessor implementation alias is planned
      unless all public platform routes are later duplicated under predecessor.
- [x] Phase 7: update UI branding in `pvad-designer` and decide whether legacy `ui-designer` is renamed, archived, or left as historical.
      Started visible branding in `pvad-designer`: browser title/loading text, package metadata,
      app header logo/accessibility label, home hero/feature copy, importer format copy, and migrations hub
      product copy now use legacy PVADE component. The migrations conversion page now uses predecessor wording
      for report-designer targets, backend error messages, spreadsheet/PDF conversion copy, and output
      panel labels. The in-app docs now use legacy PVADE component wording for import/export,
      migration, REST API, AI/codegen, spreadsheet, and documentation-map product descriptions while
      marking current legacy namespace family project paths and namespaces as legacy compatibility where needed.
      Smaller UI surfaces now use predecessor wording for spreadsheet import, workbook JSON errors,
      code-panel descriptions, generated PDF-code comments, starter text, and global style comments.
      Final inventory leaves only intentional technical canvas terms, legacy compatibility references,
      localStorage/sessionStorage keys, current legacy project paths, and sample content. The legacy
      `ui-designer` folder remained historical during this slice and was deferred to Phase 8/9 instead of
      being renamed in this UI-branding slice.
      Technical HTML canvas terms, localStorage keys, and legacy Pdf component API references remain
      unchanged for compatibility until their specific surfaces are migrated.
      Frontend package naming slice completed: the active `pvad-designer` package now publishes as
      `pvad-designer`, code-editor sample/download/schema identifiers use predecessor naming, and the legacy
      `ui-designer` package/title was explicitly marked as historical compatibility before later removal.
      Legacy localStorage keys remain unchanged so existing user drafts and preferences survive the rename.
- [x] Phase 8: update MCP/sample/docs/package identities:
      - promote the MCP server package; current source reference: `tools/PVAD.Mcp`
      - promote the demo sample; current source reference: `samples/PVAD.Demo`
      - active docs and schemas to predecessor naming
      Started with MCP package identity: the MCP server advertises legacy mcp component as package/server
      identity, exposes legacy mcp component as the primary binary while retaining `canvas-mcp` compatibility,
      accepts predecessor implementation while retaining `CANVAS_API_URL`, and publishes predecessor implementation resources while
      retaining legacy `canvas://...` aliases.
      Sample identity started with legacy facing component demo metadata and renderer notes.
      Active docs/AI references started: `llms.txt`, `llms-full.txt`, DocFX landing/config files,
      cookbook intro, documentation approach, and schema descriptions now present legacy PVADE component
      as the primary identity while preserving legacy namespace family xrefs, project paths, and schema URIs
      for compatibility.
      Root `README.md` now presents legacy PVADE component as the primary product identity, describes
      predecessor importer/generator/migration surfaces, and keeps current legacy namespace family paths as legacy project names.
      `ARCHITECTURE.md` now documents the additive legacy namespace family facade layer, predecessor importer/generator/migration
      public entry points, and legacy predecessor project boundaries until the later physical rename phase.
      `PROJECT_SUMMARY.md` now presents predecessor as the primary product identity, adds the predecessor facade project
      group, and keeps legacy namespace family project/test names as current legacy implementation inventory.
      `TESTING.md` now describes legacy compatible component PDF code/output and editable predecessor design targets while
      keeping current predecessor implementation project names as the real test inventory.
      `CONTRIBUTING_RENDERERS.md` now names legacy PVADE component as the extension target, asks new
      developer-facing work to expose legacy namespace family facades, and documents legacy namespace family implementation
      project patterns during the compatibility phase.
- [x] Phase 9: optional later physical rename of solution, project files, folders, and test assemblies.
      Started with an additive solution alias, copied from the predecessor solution so developers could
      build through a legacy named component entry point while legacy project paths remain stable. Verified with
      predecessor implementation and `dotnet build PVAD.sln` (0 errors; existing dependency/analyzer/nullability
      warnings remain).
      Added `PVAD.WebApi/PVAD.WebApi.http` as a legacy named component HTTP request alias for the predecessor implementation
      endpoint. The WebApi folder and project file are now physically named `PVAD.WebApi/PVAD.WebApi.csproj`,
      both solution generations referenced the promoted Web API, and API/export/ImageOCR test project
      references point to the new project file. Implementation namespaces were moved from the legacy WebApi
      namespace to predecessor implementation in the follow-up namespace slice. Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed). Solution-level verification after this slice:
      `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1` and
      `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1` both passed with 0 errors
      and existing predecessor implementation compatibility warnings.
      The WebApi namespace slice was re-verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullable warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
      Removed the stale default legacy-named WebApi scratch request after the folder/namespace rename;
      `PVAD.WebApi/PVAD.WebApi.http` is now the only tracked WebApi HTTP scratch file.
      Follow-up documentation cleanup updated active checklist references from the old WebApi project filename
      to `PVAD.WebApi/PVAD.WebApi.csproj`.
      Moved the tracked MCP server files to the promoted package. Current reference: `tools/PVAD.Mcp`. The retired folder then
      keeps a README pointer only; untracked local `node_modules` / lock artifacts may remain there locally.
      The server still exposes the compatibility `canvas-mcp` binary and `canvas://...` resource aliases.
      Moved the tracked demo sample files to the promoted sample, including
      its project file, and updated both solution generations. Current reference: `samples/PVAD.Demo`. The retired
      sample folder now keeps a README pointer only; local `bin` / `obj` artifacts may remain there locally.
      Added predecessor implementation as the primary predecessor Workbook JSON schema with a predecessor `$id`;
      kept `docs/schema/canvas-workbook.schema.json` as the legacy compatibility alias and updated MCP,
      in-app docs, AI docs, and schema tests to prefer the predecessor path.
      Added additive legacy Api.Tests component and legacy Export.Tests component project aliases that compile the existing
      legacy API/export test sources under legacy named component test assemblies. Both `PVAD.sln` and `PVAD.sln` now
      include the aliases while the legacy Api.Tests component / legacy Export.Tests component projects remain.

## Phase 9 Collision Plan

The remaining physical rename work is not a simple folder move because many predecessor implementation projects already
exist as public facades or provider aggregators over legacy namespace family implementation projects.

Recommended order:

1. **Keep facade projects stable first.**
   legacy Core component, legacy Application component, legacy Domain component, predecessor implementation, legacy FileImporter component,
   legacy Importer component, legacy Migration.Pdf component, legacy Migration.Report component, and legacy Migration.Spreadsheet component
   should stay in place as the public surface while implementation moves are done underneath them.
2. **Promote shared contracts before implementation engines.**
   Move/copy remaining canonical contracts from legacy Core component into legacy Core component, then convert legacy facing component
   types to compatibility aliases or adapters. Do this before moving Application or Infrastructure because
   almost every project depends on Core DTOs and abstractions.
3. **Promote Domain separately.**
   legacy Domain component currently wraps legacy Domain component; do not rename legacy Domain component directly into
   `src/Core/PVAD.Domain/` because that folder already exists. First move missing implementation types into the
   existing `src/Core/PVAD.Domain` project, then keep legacy Domain component as a compatibility project that forwards
   to predecessor.
4. **Promote Application after Core/Domain.**
   legacy Application component currently references legacy Application component, legacy Core component, and legacy Domain component.
   Move use cases into the existing `src/Core/PVAD.Application` project in small groups, then leave
   legacy Application component as compatibility shims.
5. **Promote Infrastructure by capability family.**
   legacy Infrastructure.Pdf component, `Word`, `Spreadsheet`, and `Converters` already exist and reference the
   predecessor engines. Move one family at a time, keeping tests green after each family. PDF should go first
   because it is the generator core, then Word/Spreadsheet, then Converters.
6. **Promote Importers after Core and PDF infrastructure.**
   legacy Importer component and legacy FileImporter component now own their active predecessor source, and `PVAD.WebApi` composes through
   the predecessor importer projects. Remaining importer follow-up is limited to compatibility cleanup such as legacy
   test/project aliases and shared OCR data assets.
7. **Promote Migration provider projects last.**
   predecessor provider facades already exist for PDF migrations; report/spreadsheet aggregators still reference
   predecessor implementation provider engines. Move individual provider projects one provider family at a time
   only after Core/Application/Infrastructure identities are stable.
8. **Tests follow the same pattern.**
   Keep current predecessor implementation until each underlying project is promoted, then add or switch to
   predecessor implementation aliases before removing legacy test project names.

Current classification:

| Area | Current state | Next action |
| --- | --- | --- |
| `PVAD.WebApi` | Physically renamed; namespace renamed; API tests green | Done except future route/diagnostic cleanup |
| legacy Core component | Facade over legacy Core component plus selected predecessor contracts | Promote canonical contracts/abstractions first |
| legacy Domain component | Facade/adapters over legacy Domain component | Move missing domain implementation into existing predecessor project |
| legacy Application component | Facade over legacy Application component plus selected predecessor use cases | Move use cases after Core/Domain |
| legacy Pdf component | predecessor-owned PDF generator engine (predecessor implementation), moved out of legacy Pdf component; single assembly, no dual-link | Done; migration providers still emit legacy Pdf component output (separate slice) |
| legacy Infrastructure.Pdf component | predecessor-owned PDF services over legacy Pdf component engine + legacy Core component | Done |
| legacy Infrastructure.Word component | Facade over Word infrastructure | Promote after PDF |
| legacy Infrastructure.Spreadsheet component | Facade over Spreadsheet infrastructure | Promote after PDF or alongside spreadsheet migration |
| legacy Infrastructure.Converters component | Facade over converter exporters | Promote after Word/Spreadsheet dependencies settle |
| legacy Importer component | predecessor-owned low-level importer source; still bridges to legacy PDF component engine where needed; predecessor tests now use predecessor importer directly | Keep compatibility shims until PDF engine rename |
| legacy FileImporter component | predecessor-owned built-in importers; WebApi composes through predecessor projects | Keep legacy predecessor aliases/tests for compatibility window |
| legacy Migration.Pdf component | Aggregator over predecessor provider facades | Keep, then retire legacy provider engines later |
| legacy Migration.Report component | Aggregator over predecessor implementation report engines; direct legacy Core component reference removed | Promote report providers one by one late |
| legacy Migration.Spreadsheet component | Aggregator over predecessor implementation spreadsheet engines | Promote spreadsheet providers one by one late |
| legacy Export.Tests component | predecessor-owned export composition tests over predecessor Application/Core/Infrastructure projects | Keep full legacy regression suite in `tests/PVAD.Export.Tests` |

Remaining compatibility bridges after the latest promotion slices:

- legacy Core component and legacy Domain component still reference the legacy core/domain projects because they own the active
  adapter boundary for the additive rename window.
- Individual predecessor implementation provider facades still reference legacy provider engines; the WebApi no longer
  composes directly against those legacy provider projects.
- legacy Migration.Report component and legacy Migration.Spreadsheet component still aggregate legacy report/spreadsheet engines
  until those provider implementations are promoted one by one.
- The PDF engine namespace/physical rename is **done**: the engine source moved from `src/Generation/PVAD.Pdf/**`
  (predecessor implementation) into the new `src/Generation/PVAD.Pdf` project (predecessor implementation), and all in-repo
  consumers (both infrastructure projects, legacy Generator component, legacy Importer component, WebApi, legacy namespace family
  consumers, and tests) now reference legacy Pdf component. The one remaining legacy Pdf component surface is intentional:
  migration providers still *emit* predecessor implementation in generated conversion output, which belongs to
  the migration-output-namespace decision in the provider-promotion slice (not the physical engine rename).
- legacy Api.Tests component intentionally links the legacy API test sources and OCR data paths for route compatibility
  coverage; replacing it with predecessor-owned API tests should be a separate coverage-preserving slice.

Guardrails:

- Do not move a legacy namespace family implementation folder into an already-existing predecessor implementation facade folder without
  first deciding whether the facade or the implementation owns each file.
- Do not remove legacy namespace family projects until the matching predecessor project has source-level ownership and the legacy
  project can compile as a compatibility shim.
- Verify each slice with the smallest relevant tests plus `dotnet build PVAD.WebApi/PVAD.WebApi.csproj`;
  run full solution builds only after several small green slices because they are currently slow and can hang.

Completed Phase 9 promotion slices:

- [x] legacy Core component primitive promotion:
      Added predecessor-owned legacy Core.Primitives.PdfPoint component, `PdfTextAlignment`, `PdfVerticalAlignment`, and
      `ExportFormat` while keeping the legacy predecessor primitives intact. Added adapter methods between the
      predecessor and predecessor primitive types so compatibility projects can bridge explicitly. Verified with
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (12 passed),
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed), and
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain).
- [x] legacy Core component capability promotion:
      Added predecessor-owned legacy Core.Abstractions.IRendererCapabilities component,
      legacy Core.Capabilities.RendererFeature component, `UnsupportedFeatureFallbackMode`, and
      `RendererCapabilityFallback` while keeping legacy predecessor capabilities intact. Added adapter methods
      for capability enums and predecessor capability tests. Verified with
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (30 passed),
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed), and
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain).
- [x] legacy Core component simple abstraction promotion:
      Added predecessor-owned legacy Core.Abstractions.IExporterCapabilities component, `ExporterCapabilities`,
      `IDiagnosticsReader`, `IOutputWriter`, and `IImageReader` while keeping legacy predecessor abstractions
      intact. Added adapter methods for exporter capabilities and predecessor abstraction tests. Verified with
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (33 passed) and
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed). `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      was started for this slice but stopped after a silent hang; previous WebApi composition builds passed
      before this additive Core-only interface slice.
- [x] legacy Core component document exporter abstraction promotion:
      Added predecessor-owned legacy Core.Abstractions.IDocumentExporter component over legacy Core.Contracts.DesignExportDto component
      and `ExportOptions` while keeping the legacy predecessor document exporter abstraction intact. Added tests
      for default exporter capabilities and the default options-overload fallback. Verified with
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (35 passed) and
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed).
- [x] legacy Domain component designer element behavior promotion:
      Promoted the legacy `DesignerElement.MigratePropsToConfig()` behavior into
      legacy Domain.ValueObjects.DesignerElement component, including the typed config migration and value parsing
      helpers. predecessor numeric string parsing now uses invariant culture to avoid locale-dependent prop
      conversion. Added predecessor-owned behavior tests for text and page-number props migration. Verified with
      `dotnet test tests/PVAD.Domain.Tests/PVAD.Domain.Tests.csproj --no-restore --disable-build-servers -m:1`
      (6 passed),
      `dotnet test tests/PVAD.Application.Tests/PVAD.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (4 passed), and
      `dotnet test tests/PVAD.Application.Tests/PVAD.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed). The first predecessor Application test attempt raced with a parallel predecessor Application build in
      the shared legacy obj component apphost output; rerunning it serially passed. The broader
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1` composition
      build also passed for this Core/Domain promotion block (0 errors; 22 existing warnings remain).
- [x] legacy Core component remaining behavior promotion:
      Promoted the remaining shared Core service abstractions and behavior primitives into legacy Core component,
      including `IDocumentRenderer`, expression/value formatter interfaces, page/header/table/watermark
      service interfaces, `A1Reference`, `PvadExpressionEvaluator`, `ExpressionEvaluator`,
      `DesignLayoutPlanner`, and `ValueFormatter`. The predecessor evaluator type is legacy named component instead of exposing
      the legacy `PvadExpressionEvaluator` name. Verified with
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (40 passed) and
      `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1`
      (29 passed). The broader
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1` composition
      build also passed for this Core/Domain promotion block (0 errors; 22 existing warnings remain).
- [x] legacy Application component use case ownership promotion:
      Promoted the predecessor application use cases from predecessor facades to predecessor-owned implementations over
      legacy Core component and legacy Domain component, including clone, extract pages, find/replace, validation, template
      create/get/update, export, generate, diagnostics, page coverage, header/footer, page numbering,
      table flow, table of contents, and watermark application. Removed the direct
      legacy Application component project reference from legacy Application component. `RenderTemplateUseCase` remains
      deferred to the PDF/infrastructure promotion because it currently owns direct PDF rendering/output
      behavior. Verified with
      `dotnet test tests/PVAD.Application.Tests/PVAD.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (8 passed) and
      `dotnet test tests/PVAD.Application.Tests/PVAD.Application.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed). A follow-up
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1` started and
      compiled through the early dependency chain, then stopped after a silent hang; the previous WebApi
      composition build passed before this Application-only ownership slice.
- [x] legacy Infrastructure.Pdf component service ownership promotion:
      Promoted PDF infrastructure services from wrappers to predecessor-owned implementations over legacy Core component
      abstractions, including document rendering, diagnostics, file output, renderer capabilities, page
      numbering, header/footer, watermark, table flow, table of contents, page coverage, and facade
      orchestration. legacy Infrastructure.Pdf component no longer references legacy Infrastructure.Pdf component; it links the
      current legacy Pdf component engine source directly while excluding the unused legacy predecessor Core primitive
      compatibility adapter. The public PDF engine namespace remains legacy Pdf component until the later PDF engine
      type-facade/physical rename phase. legacy Generator component remains on legacy Infrastructure.Pdf component for now
      because switching it to legacy Infrastructure.Pdf component while Word/Spreadsheet still transitively reference
      legacy Infrastructure.Pdf component creates duplicate legacy Pdf component type identities. Verified with
      `dotnet build src/Infrastructure/PVAD.Infrastructure.Pdf/PVAD.Infrastructure.Pdf.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PVAD.Infrastructure.Pdf.Tests/PVAD.Infrastructure.Pdf.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed),
      `dotnet test tests/PVAD.Infrastructure.Pdf.Tests/PVAD.Infrastructure.Pdf.Tests.csproj --no-restore --disable-build-servers -m:1`
      (34 passed), and
      `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed).
- [x] legacy Infrastructure.Word component ownership promotion:
      Promoted Word infrastructure from predecessor wrappers to predecessor-owned implementations over legacy Core component,
      including DOCX export, renderer capabilities, document protection, custom properties, style
      definitions, rich text span parsing, comments, footnotes, unit conversion, and DOCX digital signing.
      legacy Infrastructure.Word component no longer references legacy Infrastructure.Word component; it carries the same
      OpenXML/QR/signing package dependencies directly. legacy Generator component remains on the legacy Word/Spreadsheet
      references until Spreadsheet and Converter infrastructure are promoted, so duplicate transitive
      legacy Pdf component type identities are avoided. Verified with
      `dotnet build src/Infrastructure/PVAD.Infrastructure.Word/PVAD.Infrastructure.Word.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing copied nullable warning remains),
      `dotnet test tests/PVAD.Infrastructure.Word.Tests/PVAD.Infrastructure.Word.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed),
      `dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Word"`
      (85 passed), and
      `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed).
- [x] legacy Infrastructure.Spreadsheet component ownership promotion:
      Promoted Spreadsheet infrastructure from predecessor wrappers to predecessor-owned implementations over
      legacy Core component and legacy Application component, including fluent workbook authoring, XLSX/XLS/CSV IO,
      spreadsheet calculation, validation, spreadsheet-to-design conversion, Excel document export,
      workbook operations, sheet renderer capabilities, and spreadsheet data contracts.
      legacy Infrastructure.Spreadsheet component no longer references legacy Infrastructure.Spreadsheet component; it
      carries the same ClosedXML/NPOI dependencies directly. With PDF, Word, and Spreadsheet
      infrastructure promoted, legacy Generator component now references legacy Infrastructure.Pdf component,
      legacy Infrastructure.Word component, and legacy Infrastructure.Spreadsheet component instead of the legacy predecessor
      infrastructure projects. The public PDF engine namespace remains legacy Pdf component until the later
      PDF engine type-facade/physical rename phase. Verified with
      `dotnet build src/Infrastructure/PVAD.Infrastructure.Spreadsheet/PVAD.Infrastructure.Spreadsheet.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/ClosedXML/nullability warnings remain),
      `dotnet test tests/PVAD.Infrastructure.Spreadsheet.Tests/PVAD.Infrastructure.Spreadsheet.Tests.csproj --no-restore --disable-build-servers -m:1`
      (6 passed),
      `dotnet build src/Generation/PVAD.Generator/PVAD.Generator.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PDF XML-doc, dependency/NPOI, and nullable warnings remain),
      `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-restore --disable-build-servers -m:1`
      (5 passed), and
      `dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Spreadsheet"`
      (20 passed).
- [x] legacy Infrastructure.Converters component ownership promotion:
      Promoted converter infrastructure from predecessor wrappers to predecessor-owned implementations over
      legacy Core component and legacy Application component, including HTML, Markdown, CSV, XML, SVG, PNG/JPEG/TIFF,
      ODT export, Google font CSS helpers, style extensions, and converter renderer capabilities.
      legacy Infrastructure.Converters component no longer references legacy Infrastructure.Converters component; it
      carries the same NPOI/SkiaSharp dependencies directly and keeps the predecessor `DocumentExporter`
      public contract for converter facade tests. Initial WebApi export composition switching was completed
      in the follow-up Web/API export wiring slice. Verified with
      `dotnet build src/Infrastructure/PVAD.Infrastructure.Converters/PVAD.Infrastructure.Converters.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI warnings remain),
      `dotnet test tests/PVAD.Infrastructure.Converters.Tests/PVAD.Infrastructure.Converters.Tests.csproj --no-restore --disable-build-servers -m:1`
      (9 passed), and
      `dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~ExporterTests"`
      (66 passed).
- [x] `PVAD.WebApi` export/spreadsheet composition wiring:
      Switched the active WebApi export registrations to legacy Application.UseCases.ExportDocumentUseCase component,
      legacy Core.Abstractions.IDocumentExporter component, legacy Infrastructure.Converters component, legacy Infrastructure.Word component,
      and legacy Infrastructure.Spreadsheet component. `ExportController` and `SpreadsheetController` now accept
      predecessor contracts for export/spreadsheet routes and adapt only the PDF-special rendering paths back to
      the legacy `DesignJsonMapper`/legacy Pdf component bridge. WebApi no longer references
      legacy Infrastructure.Converters component or legacy Infrastructure.Spreadsheet component; legacy Infrastructure.Word component
      remains for current document-operation compatibility endpoints. Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Export|FullyQualifiedName~Spreadsheet"`
      (19 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Export|FullyQualifiedName~Spreadsheet"`
      (19 passed), and
      `dotnet test tests/PVAD.Infrastructure.Converters.Tests/PVAD.Infrastructure.Converters.Tests.csproj --no-restore --disable-build-servers -m:1`
      (9 passed).
- [x] legacy Importer component ownership promotion:
      Promoted the low-level PDF importer from a facade over legacy Importer component to predecessor-owned source over
      legacy Infrastructure.Pdf component, including tokenizer, xref/object parsing, stream decoding, font parsing,
      graphics interpretation, document model, editing session, semantic analysis, debug overlay, and PDF
      regeneration bridge. legacy Importer component no longer references legacy Importer component; the legacy
      legacy Importer component project remains for compatibility and keeps its predecessor implementation obsolete diagnostic.
      predecessor implementation remains the preferred predecessor entry point. Verified with
      `dotnet build src/Importing/PVAD.Importer/PVAD.Importer.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed), and
      `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (95 passed).
- [x] legacy FileImporter component PDF ownership promotion:
      Promoted the PDF file importer inside the legacy FileImporter component aggregator from a wrapper over
      legacy FileImporter.Pdf component to predecessor-owned source over legacy Importer component and legacy Core.Contracts component.
      legacy FileImporter component no longer references legacy FileImporter.Pdf component; the other file importer
      providers still wrap their legacy predecessor implementations until their individual promotion slices.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter component image ownership promotion:
      Promoted the raster image importer inside the legacy FileImporter component aggregator from a wrapper over
      legacy FileImporter.Image component to predecessor-owned source over legacy Core.Contracts component and SkiaSharp, preserving
      A4 page placement, EXIF orientation handling, JPEG/PNG data URI passthrough, and corrupt-image
      validation behavior. legacy FileImporter component no longer references legacy FileImporter.Image component; the other
      file importer providers still wrap their legacy predecessor implementations until their individual slices.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX/PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      `dotnet test tests/PVAD.FileImporter.Image.Tests/PVAD.FileImporter.Image.Tests.csproj --no-restore --disable-build-servers -m:1`
      (8 passed).
- [x] legacy FileImporter component SVG ownership promotion:
      Promoted the SVG importer inside the legacy FileImporter component aggregator from a wrapper over
      legacy FileImporter.Svg component to predecessor-owned source over legacy Core.Contracts component, preserving SVG page-size
      detection, rect/text/image mapping, inline vector fallback for path/circle/ellipse/line/polyline/polygon,
      transforms, style color parsing, and `use`/defs expansion behavior. legacy FileImporter component no longer
      references legacy FileImporter.Svg component; DOC/DOCX/ODT/PPTX still wrap their legacy predecessor implementations
      until their individual slices.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (15 passed), and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter component DOC ownership promotion:
      Promoted the legacy Word 97-2003 `.doc` importer inside the legacy FileImporter component aggregator from a
      wrapper over legacy FileImporter.Doc component to predecessor-owned source over legacy Core.Contracts component, preserving CFBF
      validation, `WordDocument` stream extraction, FIB text offsets, printable-text fallback, and single-page
      text layout behavior. legacy FileImporter component no longer references legacy FileImporter.Doc component; DOCX/ODT/PPTX
      still wrap their legacy predecessor implementations until their individual slices.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (16 passed after rerunning serially; the first parallel attempt hit a transient `.deps.json` file lock),
      and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter component ODT ownership promotion:
      Promoted the OpenDocument Text `.odt` importer inside the legacy FileImporter component aggregator from a wrapper
      over legacy FileImporter.Odt component to predecessor-owned source over legacy Core.Contracts component, preserving ZIP/content.xml
      parsing, style extraction, paragraph/header text mapping, frame image mapping, metadata-title fallback,
      and portrait page layout behavior. legacy FileImporter component no longer references legacy FileImporter.Odt component;
      DOCX/PPTX still wrap their legacy predecessor implementations until their individual slices.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (17 passed), and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter component DOCX ownership promotion:
      Promoted the OOXML `.docx` importer inside the legacy FileImporter component aggregator from a wrapper over
      legacy FileImporter.Docx component to predecessor-owned source over legacy Core.Contracts component and `DocumentFormat.OpenXml`,
      preserving page size/margin extraction, paragraph typography mapping, table mapping, inline image data URI
      extraction, and document metadata mapping. legacy FileImporter component no longer references
      legacy FileImporter.Docx component; PPTX is the final remaining legacy predecessor file importer wrapper.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (18 passed), and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter component PPTX ownership promotion:
      Promoted the PowerPoint `.pptx` importer inside the legacy FileImporter component aggregator from the final wrapper
      over legacy FileImporter.Pptx component to predecessor-owned source over legacy Core.Contracts component and `DocumentFormat.OpenXml`,
      preserving slide/page mapping, shape/text/picture extraction, theme and background color resolution,
      inline image data URI extraction, and presentation metadata naming. legacy FileImporter component no longer
      references any predecessor implementation implementation project; all built-in file importers in the
      aggregator are now predecessor-owned source.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing PPTX nullable and PDF XML-doc warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (19 passed), and
      predecessor implementation
      (1 passed).
- [x] legacy FileImporter.ImageAnalysis component ownership promotion:
      Promoted the editable raster image analysis importer from a facade over
      legacy FileImporter.ImageAnalysis component to predecessor-owned source over legacy Core.Contracts component, legacy FileImporter component,
      and SkiaSharp. The predecessor project now owns preprocessing, color analysis, shape detection, glyph/text
      recognition, scene assembly, debug overlay rendering, and character templates directly while preserving
      the public predecessor `ImageAnalysisOptions` / `ImageAnalysisImportResult` contracts. Static
      `SupportedExtensions` remains available for existing call-sites, with explicit `IFileImporter`
      implementation for the common predecessor importer contract. legacy FileImporter.ImageAnalysis component no longer
      references legacy FileImporter.ImageAnalysis component.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter.ImageAnalysis/PVAD.FileImporter.ImageAnalysis.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing SkiaSharp filter-quality warnings remain),
      `dotnet test tests/PVAD.FileImporter.ImageAnalysis.Tests/PVAD.FileImporter.ImageAnalysis.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed), and
      `dotnet test tests/PVAD.FileImporter.ImageAnalysis.Tests/PVAD.FileImporter.ImageAnalysis.Tests.csproj --no-restore --disable-build-servers -m:1`
      (179 passed; existing xUnit analyzer warnings remain).
- [x] legacy FileImporter.ImageOcr component ownership promotion:
      Promoted the scanned-image OCR-to-editable-design converter from adapters over
      legacy FileImporter.ImageOcr component to predecessor-owned source over legacy Core.Contracts component, SkiaSharp, and Tesseract.
      The predecessor project now owns OCR layout/fusion, visual element detection, text extraction, debug overlay
      rendering, embedded and process-isolated Tesseract engines, worker contracts, OCR pixel helpers, and
      converter orchestration directly while preserving the public predecessor `IOcrEngine`, OCR model,
      `ImageToPdfConversionOptions`, and `ImageToPdfConversionResult` contracts. Adapter files
      `OcrEngineAdapters`, `OcrModelMapper`, and wrapper `TesseractOcrEngines` were removed.
      legacy FileImporter.ImageOcr component no longer references the legacy FileImporter.ImageOcr component project. The
      large `tessdata` / `native` assets are still linked from the legacy folder to avoid duplicating data.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter.ImageOcr/PVAD.FileImporter.ImageOcr.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet test tests/PVAD.FileImporter.ImageOcr.Tests/PVAD.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed), and
      `dotnet test tests/PVAD.FileImporter.ImageOcr.Tests/PVAD.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (97 passed; existing dependency/WebApi warnings remain).
- [x] legacy FileImporter.ImageOcr.Worker component physical promotion:
      Added a predecessor-owned process-isolated OCR worker project over legacy FileImporter.ImageOcr component, updated the
      predecessor OCR engine default worker resolution to prefer legacy FileImporter.ImageOcr.Worker.dll component, and kept
      fallback resolution for the legacy FileImporter.ImageOcr.Worker.dll component during the compatibility
      window. `PVAD.WebApi`, legacy Api.Tests component, and `PVAD.sln` now use the predecessor worker project; `PVAD.sln` and
      legacy predecessor API tests continue to use the predecessor worker. The WebApi OCR controller now uses the predecessor
      OCR engine contract and converts the predecessor design DTO back to the legacy predecessor DTO only at the existing
      `DesignJsonMapper`/legacy Pdf component boundary.
      Verified with
      `dotnet build src/Importing/PVAD.FileImporter.ImageOcr.Worker/PVAD.FileImporter.ImageOcr.Worker.csproj --no-restore --disable-build-servers -m:1`
      (0 warnings, 0 errors),
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.FileImporter.ImageOcr.Tests/PVAD.FileImporter.ImageOcr.Tests.csproj --no-restore --disable-build-servers -m:1`
      (2 passed),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed). A full `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1`
      initially exposed a pre-existing `samples/PVAD.Demo` ambiguity between legacy Infrastructure.Pdf component and
      legacy Infrastructure.Pdf component; the demo now references only legacy Generator component directly, and the full predecessor
      solution build passes (0 errors; existing package/NPOI/analyzer/nullability/obsolete warnings remain).
- [x] `PVAD.WebApi` file importer composition switch:
      Switched the active WebApi file importer registrations from predecessor implementation projects to
      legacy FileImporter component, legacy FileImporter.ImageAnalysis component, legacy FileImporter.ImageOcr component, and the predecessor OCR
      worker. Import endpoints still return the existing legacy compatible component design JSON contract by converting
      predecessor importer DTOs at the controller boundary. The PDF engine debug endpoint now uses legacy Importer component
      analysis/debugging types directly. `PVAD.WebApi` no longer has project references or implementation
      `using` directives for predecessor implementation; OCR `tessdata` / `native` assets remain linked from
      the legacy folder to avoid duplicating large data during the compatibility window.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj --no-restore --disable-build-servers -m:1`
      (19 passed),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
- [x] `PVAD.WebApi` PDF viewer importer extraction switch:
      Switched the PDF viewer native annotation extraction and AcroForm extraction/fill parsing services from
      legacy Importer component to legacy Importer component. Follow-up WebApi composition work also moved the annotation
      flattening/redaction service to legacy Importer.Generation.PdfGeneratorBridge component; the service still
      renders through the compatibility legacy Pdf component engine boundary, but no longer needs the legacy
      legacy Importer component project identity. A full restore was required after the project-reference changes to
      clear the stale legacy Application component -> the predecessor identity -> legacy Infrastructure.Pdf component asset graph.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed).
- [x] `PVAD.WebApi` DocumentOps application composition switch:
      Switched the document operations controller for find/replace, clone, and page extraction from
      legacy Application.UseCases component to legacy Application.UseCases component. The HTTP contract still accepts and
      returns the existing legacy compatible component design JSON by converting through legacy Core.Contracts component
      adapters at the controller boundary. The DOCX signing endpoint now calls the predecessor Word signing service.
      Template rendering/authentication remain on the legacy predecessor application services until their
      controller contracts are moved or explicitly adapter-wrapped.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~DocumentOpsControllerTests"`
      (3 passed), and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~DocumentOpsControllerTests"`
      (3 passed).
- [x] `PVAD.WebApi` template/auth application composition switch:
      Switched auth middleware/controller and stored-template repository wiring from legacy Application component
      / legacy Domain component to legacy Application component / legacy Domain component. legacy Application component now owns
      `AuthenticateUserUseCase`; `PVAD.WebApi` no longer has direct project references to
      legacy Application component, legacy Domain component, legacy Infrastructure.Pdf component, or legacy Infrastructure.Word component.
      Stored-template rendering no longer depends on the removed legacy `RenderTemplateUseCase` registration;
      it uses the predecessor template lookup and the compatibility legacy Pdf component engine boundary directly until a
      predecessor-owned template renderer is introduced.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain) and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~TemplatesControllerTests|FullyQualifiedName~PdfViewerFormsControllerTests|FullyQualifiedName~PdfViewerAnnotationsControllerTests"`
      (16 passed).
- [x] `PVAD.WebApi` PDF migration provider composition switch:
      Switched all active PDF code-converter services from direct predecessor implementation namespaces to
      predecessor implementation facades and replaced the many direct PDF-provider project references with the
      legacy Migration.Pdf component aggregator. Legacy predecessor PDF migration provider projects still build as internal
      dependencies of the predecessor facades during the compatibility window, but `PVAD.WebApi` no longer composes
      directly against those provider identities. Report and spreadsheet migration providers remain a
      separate follow-up slice.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain) and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed).
- [x] `PVAD.WebApi` spreadsheet migration provider composition switch:
      Switched all active spreadsheet code-converter services from direct predecessor implementation spreadsheet
      namespaces to the legacy Migration.Spreadsheet component facades and replaced the eight direct spreadsheet-provider
      project references with the legacy Migration.Spreadsheet component aggregator. Legacy predecessor spreadsheet migration
      provider projects still build as internal dependencies of the predecessor facade during the compatibility
      window, but `PVAD.WebApi` no longer composes directly against those spreadsheet provider identities.
      Report migration providers remain the next WebApi migration-composition follow-up.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability/XML-doc warnings remain) and
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed).
- [x] `PVAD.WebApi` report migration provider composition switch:
      Switched the report-to-design API path from direct predecessor implementation report converters to
      legacy Migration.Report component facades and replaced the eight direct report-provider project references with
      the legacy Migration.Report component aggregator. The predecessor report facade now exposes the WebApi-required
      detection/resource helpers: report package extraction, DevExpress `.resx` resource parsing,
      `LooksLike(...)` checks, and resource-aware DevExpress/RPX/Jasper conversion overloads. Legacy predecessor
      report migration provider projects still build as internal dependencies of the predecessor facade during the
      compatibility window, but `PVAD.WebApi` no longer composes directly against those report provider
      identities.
      Verified with
      `dotnet build src/Migrations/Report/PVAD.Migration.Report/PVAD.Migration.Report.csproj --disable-build-servers -m:1`
      (0 errors; existing Stimulsoft warning remains),
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Migration"`
      (16 passed), and
      `dotnet test tests/PVAD.Migration.Report.Tests/PVAD.Migration.Report.Tests.csproj --no-restore --disable-build-servers -m:1`
      (20 passed).
- [x] `PVAD.WebApi` core contract composition switch:
      Switched active WebApi design/workbook contract usage from direct legacy Core.Contracts component to
      legacy Core.Contracts component. Document operations, export, image conversion, spreadsheet PDF rendering, and
      image-analysis debug paths now pass predecessor design contracts directly instead of adapting through predecessor
      DTOs. `DesignJsonMapper` now uses the predecessor core layout planner facade, and the direct
      legacy Core component project reference was removed from `PVAD.WebApi`; legacy Core component still builds as
      an internal dependency of legacy Core component during the compatibility window.
      Verified with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --disable-build-servers -m:1 -v:minimal`
      (0 errors; existing dependency/NPOI/nullability warnings remain),
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --disable-build-servers -m:1`
      (61 passed), and predecessor implementation (no matches).
- [x] legacy Migration.Report component core dependency cleanup:
      Removed the direct legacy Core component project reference from the predecessor report migration aggregator. The report
      facade still adapts legacy report converter outputs through legacy Core.Contracts component/legacy Core component, while
      legacy Core component remains only a transitive compatibility dependency during the additive rename phase.
      Verified with
      `dotnet build src/Migrations/Report/PVAD.Migration.Report/PVAD.Migration.Report.csproj --no-restore --disable-build-servers -m:1`
      (0 errors) and
      `dotnet test tests/PVAD.Migration.Report.Tests/PVAD.Migration.Report.Tests.csproj --no-restore --disable-build-servers -m:1`
      (20 passed).
- [x] legacy Importer.Tests component direct importer dependency switch:
      Switched the predecessor importer facade tests from a direct legacy Importer component project reference and legacy
      legacy Importer.PdfImporter component construction to the predecessor-owned importer facade/class. Legacy importer
      compatibility remains covered by the dedicated predecessor importer tests; the predecessor test project now validates
      the active predecessor importer surface without a direct predecessor importer project reference.
      Verified with
      `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed) and predecessor implementation
      (no matches).
- [x] legacy Export.Tests component direct export dependency switch:
      Replaced the predecessor export test project wrapper over linked legacy Export.Tests component sources with
      predecessor-owned composition tests. The project now references legacy Application component, legacy Core component,
      legacy Infrastructure.Converters component, legacy Infrastructure.Word component, and legacy Infrastructure.Spreadsheet component
      directly, covering HTML exporter lookup plus Word/Excel bytes through the active predecessor contract types.
      The full legacy predecessor export regression suite remains in `tests/PVAD.Export.Tests`.
      Verified with
      `dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj --no-restore --disable-build-servers -m:1`
      (3 passed) and predecessor implementation
      (no matches).
- [x] PDF engine physical/namespace rename (legacy Pdf component -> legacy Pdf component):
      Moved the PDF generator engine source (63 types, 77 files) out of the dual-linked `src/Generation/PVAD.Pdf/**`
      folder into a new owned project `src/Generation/PVAD.Pdf` and renamed predecessor implementation -> predecessor implementation
      (including `.Layout`, `.Rendering`, `.Serialization[.Security]` sub-namespaces). The engine is now a
      single assembly instead of being `Compile Include`-linked into both legacy Infrastructure.Pdf component and
      legacy Infrastructure.Pdf component, which removes the long-standing duplicate legacy Pdf component type-identity hazard.
      Both infrastructure projects now `ProjectReference` legacy Pdf component; the dead `legacy Pdf.Compatibility component.
      CorePrimitiveAdapters` file (the only legacy Core component dependency, referenced nowhere) was dropped. The
      engine namespace is legacy Pdf component rather than legacy Generator.Pdf component to avoid colliding with the promoted
      facade class predecessor implementation; that facade entry point is unchanged. All in-repo
      consumers were switched to legacy Pdf component: both infrastructure projects, legacy Generator component, legacy Importer component,
      legacy Importer component, legacy Application component, `samples/PVAD.Demo`, the root the predecessor identity exe, and WebApi. Runtime
      string surfaces were updated where they must track the type identity: the `PvadPdfGeneratorBridge`
      reflection lookups (predecessor implementation, predecessor implementation, etc.) and
      the template-script Roslyn imports (predecessor implementation). Migration-provider *generated
      output* (Roslyn predecessor implementation, skeleton code, diagnostic messages) and the
      matching test assertions were intentionally left on legacy Pdf component — the emitted target namespace belongs
      to the separate migration-provider-promotion slice. `src/Generation/PVAD.Pdf` added to `PVAD.sln` and `PVAD.sln`.
      Verified with per-project builds (legacy Pdf component, legacy Infrastructure.Pdf component, legacy Infrastructure.Pdf component,
      legacy Generator component, legacy Importer component, legacy Importer component, legacy Application component, legacy FileImporter component,
      `samples/PVAD.Demo`, the predecessor identity, and `PVAD.WebApi`; all 0 errors) and with
      `dotnet test` for legacy Infrastructure.Pdf.Tests component (5), legacy Infrastructure.Pdf.Tests component (34),
      legacy Generator.Tests component (5), legacy Importer.Tests component (3), legacy Importer.Tests component (95), legacy Api.Tests component (61),
      legacy Api.Tests component (61), legacy Export.Tests component (3), and the predecessor implementation PDF migration provider suites
      (iText7/Apryse/DevExpressPdf etc.) — all passing.
      Note: `tests/PVAD.Export.Tests` has a pre-existing build break unrelated to this slice — it passes a
      legacy Core.Contracts.DesignExportDto component to `DesignJsonMapper.MapToPdfDocument(...)`, which already took
      legacy Core.Contracts.DesignExportDto component at HEAD after the earlier WebApi core-contract promotion. That is
      a Core-contract tail item, not part of the PDF engine rename.
- [x] Orphan legacy file-importer cleanup:
      Deleted the fully-unreferenced legacy file-importer implementation projects
      predecessor implementation (source already promoted into legacy FileImporter component) together
      with their placeholder `UnitTest1.cs` test shells (no project reference / no real coverage), and removed
      the stale untracked predecessor implementation build-artifact directory (no source, no references).
      All removed from `PVAD.sln` and `PVAD.sln`. Verified both solutions still enumerate cleanly and
      legacy FileImporter.Tests component (19 passed) still covers the promoted importer functionality.
      Remaining legacy file-importer teardown (Image/ImageAnalysis/ImageOcr/Pdf/Abstractions + legacy Importer component)
      is deferred because those carry real legacy test coverage (Image 8, ImageAnalysis 179, ImageOcr 97,
      Importer 95) that should be retargeted to predecessor before the projects are removed.
- [x] File-importer domain fully retired to predecessor (coverage-preserving):
      Retargeted every remaining legacy file-importer test suite onto the predecessor-owned source (namespace swaps,
      project-reference repoint) and deleted the legacy implementation projects. Specifics:
      legacy FileImporter.Image component -> merged 8 tests into legacy FileImporter.Image.Tests component (8 passed);
      legacy FileImporter.ImageAnalysis component -> merged its 6 test files + `Fixtures/RealSamples` into
      legacy FileImporter.ImageAnalysis.Tests component (181 passed) — also removed a redundant `bool includeDebugOverlay`
      `ImportWithAnalysis` overload that collided with the `ImageAnalysisOptions?` overload;
      legacy FileImporter.ImageOcr component -> relocated the 69 MB `tessdata`/`native` assets into
      `src/Importing/PVAD.FileImporter.ImageOcr`, repointed all four asset linkers (predecessor impl, legacy Api.Tests component,
      legacy Api.Tests component), merged 7 test files + fixtures into legacy FileImporter.ImageOcr.Tests component (99 passed),
      and added predecessor implementation;
      legacy Importer component + legacy FileImporter.Pdf component -> merged the 95-test `PdfImporterCoreTests` into
      legacy Importer.Tests component (98 passed, adding `InternalsVisibleTo` on legacy Importer component/legacy FileImporter component and
      updating one font-asset assertion from the intentionally renamed legacy Pdf component prefix to legacy Pdf component);
      deleted the orphaned legacy FileImporter.Abstractions component and the broken legacy FileImporter.ImageOcr.Worker component
      (repointing legacy Api.Tests component to legacy FileImporter.ImageOcr.Worker component). No predecessor implementation projects
      remain. Checkpoint: both solutions enumerate cleanly, no dangling references, and legacy Api.Tests component (61) /
      legacy Api.Tests component (61) both green after the worker repoint.

## Future Test Plan

- [x] `dotnet build PVAD.sln`
      Passed with `--disable-build-servers` after the physical sample/schema alias updates (0 errors;
      59 existing dependency/analyzer/nullability/XML-doc/NPOI warnings remain).
- [x] `dotnet build PVAD.sln`
      Passed with `--disable-build-servers -m:1` after the frontend/package naming slice (0 errors;
      246 existing warnings remain, mostly NuGet security-index/network warnings plus known package,
      nullability, XML-doc, xUnit analyzer, and NPOI license warnings). A first parallel build attempt
      produced no compiler errors but was stopped after a silent five-minute hang; serial build completed.
- [x] `dotnet build PVAD.slnx` if kept as an active solution entry.
      Not run as an active build target because current `PVAD.slnx` is an empty `<Solution>` with no project entries.
      Added matching empty `PVAD.slnx` as the legacy named component solution-file alias; real builds continue through
      `PVAD.sln` and `PVAD.sln`.
- [x] Relevant migration tests.
      Initial legacy Migration.Abstractions.Tests component coverage added for legacy to component source migration adapter mapping.
      Initial legacy Migration.Pdf.Code.DevExpress.Tests component coverage added for the first concrete predecessor migration provider facade.
      Initial legacy Migration.Pdf.Code.Syncfusion.Tests component and legacy Migration.Pdf.Code.IText7.Tests component coverage added for core PDF
      provider facade compatibility and warning diagnostic mapping.
      Initial legacy Migration.Pdf.Code.Aspose.Tests component and legacy Migration.Pdf.Code.Apryse.Tests component coverage added for provider
      facade compatibility and diagnostic severity mapping.
      Initial legacy Migration.Pdf.Code.DsPdf.Tests component and legacy Migration.Pdf.Code.Foxit.Tests component coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial legacy Migration.Pdf.Code.IronPdf.Tests component and legacy Migration.Pdf.Code.GemBox.Tests component coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial legacy Migration.Pdf.Code.Spire.Tests component and legacy Migration.Pdf.Code.PdfKitNet.Tests component coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial legacy Migration.Pdf.Code.Leadtools.Tests component and legacy Migration.Pdf.Code.ActivePdf.Tests component coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial legacy Migration.Pdf.Code.PdfTools.Tests component and legacy Migration.Pdf.Code.PdfToolsToolbox.Tests component coverage added for
      diagnostics-first and direct-generation provider facade compatibility.
      Initial legacy Migration.Pdf.Tests component coverage added for provider registry keys, factory creation,
      case-insensitive lookup, and unknown-key rejection.
      Initial legacy Migration.Report.Tests component coverage added for report provider registry keys and
      DevExpress Report/RDL/RPX facade conversion smoke tests.
      legacy Migration.Report.Tests component expanded for JasperReports and ActiveReports JS facade smoke coverage.
      legacy Migration.Report.Tests component expanded for FastReport, Telerik, and Stimulsoft facade smoke coverage.
      legacy Migration.Report.Tests component expanded to verify report migrations return the predecessor core design contract.
      Initial legacy Migration.Spreadsheet.Tests component coverage added for spreadsheet provider registry keys,
      factory creation, case-insensitive lookup, unknown-key rejection, and ClosedXML/EPPlus smoke tests.
      Verified with `dotnet test --no-build` for legacy Migration.Pdf.Tests component (18 passed) and
      legacy Migration.Report.Tests component (20 passed). legacy Migration.Spreadsheet.Tests component passed with build
      after adding all-provider predecessor facade smoke coverage (19 passed).
- [x] Spreadsheet migration tests:
      - Aspose.Cells
      - ClosedXML
      - EPPlus
      - GemBox.Spreadsheet
      - NPOI
      - Spire.XLS
      - SpreadsheetLight
      - Syncfusion XlsIO
      Added predecessor facade smoke coverage for every listed provider and verified with
      `dotnet test tests/PVAD.Migration.Spreadsheet.Tests/PVAD.Migration.Spreadsheet.Tests.csproj`
      (19 passed).
- [x] Generator compatibility tests:
      - New code with predecessor implementation
      - Legacy code with predecessor implementation
      Initial legacy Generator.Tests component coverage added for `Pdf.CreateDocument()`, `Spreadsheet.CreateWorkbook()`,
      and `Word.Export(...)`.
      predecessor implementation now accepts legacy Core.Contracts.DesignExportDto component and adapts to
      the legacy predecessor exporter internally.
      Added explicit legacy Pdf.PdfDocument component smoke coverage and verified with
      `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj` (5 passed).
      Re-verified after adding predecessor implementation obsolete guidance to the legacy predecessor implementation
      constructor with `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --disable-build-servers`
      (5 passed).
- [x] Importer compatibility tests:
      - New code with predecessor implementation
      - Legacy code with predecessor implementation
      Initial legacy Importer.Tests component coverage added for `Pdf.LoadAsync(...)` and legacy facing component import options.
      Initial legacy FileImporter.Tests component coverage added for file importer registry keys, factory creation,
      case-insensitive/extension lookup, unknown-key rejection, and SVG/Image facade smoke tests.
      Initial legacy FileImporter.ImageAnalysis.Tests component and legacy FileImporter.ImageOcr.Tests component coverage added for
      specialized analysis/OCR facades, diagnostics mapping, predecessor OCR engine adapter flow, and Tesseract facade identity.
      File importer design results now return legacy Core.Contracts.DesignExportDto component.
      Added explicit legacy Importer.PdfImporter component smoke coverage and verified with
      `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj` (3 passed),
      `dotnet test tests/PVAD.FileImporter.Tests/PVAD.FileImporter.Tests.csproj` (15 passed),
      `dotnet test --no-build` for legacy FileImporter.ImageAnalysis.Tests component (2 passed), and
      legacy FileImporter.ImageOcr.Tests component (2 passed).
      Re-verified after adding predecessor implementation obsolete guidance with
      `dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --disable-build-servers --no-restore -m:1`
      (3 passed) and `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore
      --disable-build-servers -m:1` (0 errors; existing package/NPOI/nullable warnings remain; no internal
      predecessor implementation warnings).
- [x] Core compatibility tests:
      Initial legacy Core.Tests component coverage added for DesignExportDto, SpreadsheetDto, and ExportOptions adapters.
      Verified with `dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-build` (3 passed).
- [x] Application compatibility tests:
      Initial legacy Application.Tests component coverage added for CloneTemplate, ExtractPages, FindAndReplace,
      and ValidateTemplate facades.
      Verified with `dotnet test tests/PVAD.Application.Tests/PVAD.Application.Tests.csproj --no-build` (4 passed).
- [x] Infrastructure compatibility tests:
      Initial legacy Infrastructure.Pdf.Tests component coverage added for PDF rendering, file generation,
      diagnostics reading, capabilities, and service delegation.
      Initial legacy Infrastructure.Word.Tests component coverage added for DOCX export with predecessor core contracts,
      Word renderer capabilities, and digital-signing facade delegation.
      Initial legacy Infrastructure.Spreadsheet.Tests component coverage added for fluent workbook authoring,
      XLSX import/export, calculation, validation, spreadsheet-to-design, Excel document export,
      and sheet renderer capabilities.
      Initial legacy Infrastructure.Converters.Tests component coverage added for text/image/ODT converter exports,
      converter capabilities, and predecessor core contract adaptation.
      Verified with `dotnet test --no-build` for legacy Infrastructure.Pdf.Tests component (5 passed),
      legacy Infrastructure.Word.Tests component (3 passed), legacy Infrastructure.Spreadsheet.Tests component (6 passed),
      and legacy Infrastructure.Converters.Tests component (9 passed).
- [x] Domain compatibility tests:
      Initial legacy Domain.Tests component coverage added for design template conversion, validation result conversion,
      and predecessor implementation repository adapter flows.
      Verified with `dotnet test tests/PVAD.Domain.Tests/PVAD.Domain.Tests.csproj --no-build` (4 passed).
- [x] Web/API compatibility tests:
      legacy Api.Tests component now covers legacy and predecessor branding discovery routes with compatibility notes.
      legacy Api.Tests component now covers predecessor export alias routes for format discovery and HTML export.
      legacy Api.Tests component now covers predecessor migration alias routes for framework discovery and code conversion.
      legacy Api.Tests component now covers predecessor document alias routes for find/replace, clone, and image conversion
      unsupported-format handling.
      legacy Api.Tests component now covers predecessor spreadsheet alias routes for JSON-to-workbook creation and
      workbook validation.
      legacy Api.Tests component now covers predecessor templates alias routes for template listing and validation.
      legacy Api.Tests component now covers predecessor PDF Viewer alias routes for annotation sidecar save/get
      and form field extraction.
      Added and verified legacy Api.Tests component alias with
      `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --disable-build-servers` (61 passed).
      Re-verified WebApi compilation after adding the first obsolete shim with
      `dotnet build PVAD.WebApi/PVAD.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI warnings remain; no predecessor implementation internal warnings).
- [x] Export compatibility tests:
      Added and verified legacy Export.Tests component alias with
      `dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj --disable-build-servers` (206 passed).
- [x] `npm run build` in `pvad-designer`.
      Passed after adding the primary predecessor implementation reference to the in-app docs
      (Vite chunk-size warning remains existing/non-blocking).
- [x] Relevant Jest tests in `pvad-designer`.
      `npm test -- --runTestsByPath src/__tests__/canvasWorkbookSchema.test.ts` passed for the primary predecessor
      schema and legacy predecessor schema alias.
- [x] MCP smoke test if `tools/PVAD.Mcp` is renamed or rebranded.
      `npx tsx smoke.ts` passed and now verifies both predecessor implementation and
      `canvas://schema/canvas-workbook` resources.
- [x] UI smoke test for migration page, designer open flow, export, and preview.
      Added `pvad-designer/src/__tests__/appRouteSmoke.test.tsx` using the existing Jest/jsdom setup.
      It covers the migrations landing route, report-designer conversion handoff into `/create`, designer
      preview mode, JSON export callback, and the PDF viewer route. Verified with
      `npm test -- --runTestsByPath src/__tests__/appRouteSmoke.test.tsx src/__tests__/pdfViewerSmoke.test.tsx`
      (4 passed) and `npm run build` in `pvad-designer` (passed; existing chunk-size warning remains).
      This is a lightweight route/component smoke test; a full Playwright/Cypress browser harness with
      screenshots remains optional future hardening, not a blocker for the current predecessor rename checklist.
- [x] Documentation link check after main docs are updated.
      Checked 111 tracked `.md` / `.mdx` / `.txt` documentation files with a local-link verifier that resolves
      relative links, repo-root links such as `/docs/...`, `.md` suffix fallbacks, and `README.md` / `index.md`
      directory targets. No broken local markdown links found. External links and `xref:` API references were
      intentionally not crawled.

## Assumptions

- [x] The former predecessor implementation reservation was superseded by legacy p component and legacy s component.
- [x] This checklist reserves predecessor implementation; it does not implement the CLI.
- [x] Existing uncommitted workspace changes are left untouched.
- [x] The first implementation block should avoid physical path/project renames unless explicitly approved.
- [x] Current repo state includes Spreadsheet engine/migrations and PdfPreview V2 roadmap; the rename plan must cover those additions.
- [x] Migration domain fully retired to predecessor (coverage-preserving):
      Promoted every predecessor implementation provider engine into a predecessor-owned project and deleted all predecessor
      migration projects (32 provider/base projects total). Steps:
      created legacy Migration.Roslyn component (the `CSharpSourceMigration` base) and added `ExpressionTranslator` /
      `ReportPackageExtractor` helpers into legacy Migration.Abstractions component; moved each provider's engine source
      into its predecessor implementation project (namespace predecessor implementation -> predecessor implementation,
      legacy Core.Contracts component -> legacy Core.Contracts component, predecessor implementation -> predecessor); for the
      15 PDF providers the thin predecessor facades were replaced by the promoted engines (aggregator unchanged); for
      the 8 spreadsheet and 8 report providers new predecessor implementation + `.Tests` projects were created
      and the legacy Migration.Spreadsheet component / legacy Migration.Report component aggregators were repointed. Because the
      engines now implement predecessor `ISourceMigration` / return legacy Core.Contracts component directly, the aggregators'
      predecessor implementation, predecessor implementation, and `ReportDiagnosticMapper` bridges were removed, and the dead
      legacy SourceMigrationAdapter component / `SourceMigrationAdapters` were deleted. Legacy per-provider test suites
      were retargeted to the predecessor engines (thin predecessor facade tests renamed to predecessor implementation to avoid
      collisions; the Rdl `Fixtures/*.rdl` restored). No predecessor implementation projects remain.
      Verified: all 31 predecessor migration provider/aggregator suites green (PDF 184, spreadsheet incl. aggregator,
      report incl. Rdl 94 + DevExpressReport 75, aggregators Pdf 18 / Spreadsheet 19 / Report 20),
      legacy Api.Tests component migration filter (16), and `dotnet build PVAD.WebApi` (0 errors).
      Note: migration *generated output* still emits predecessor implementation — switching the emitted target
      namespace to legacy Pdf component remains a separate deliberate step (the engine physical rename is done; the
      emitted-namespace product decision is independent).
- [x] Foundational teardown COMPLETE — the rename is finished (zero legacy namespace family projects/namespaces):
      Phase A: decoupled the predecessor side from predecessor by deleting the test-only compatibility bridges
      (predecessor implementation, predecessor implementation) and dropping the
      legacy Core component/legacy Domain component project references, making legacy Core component/legacy Domain component standalone; obsolete
      bridge tests were removed and mixed adapter tests trimmed to their predecessor-only cases.
      Phase B: retargeted every remaining legacy foundational test suite onto predecessor source, preserving coverage —
      legacy Core.Tests component->legacy Core.Tests component (40, `PvadExpressionEvaluator`->`PvadExpressionEvaluator`),
      legacy Application.Tests component->legacy Application.Tests component (12), legacy Infrastructure.Pdf.Tests component->
      legacy Infrastructure.Pdf.Tests component (39, re-added the `UglyToad.PdfPig` package), and the big
      legacy Export.Tests component->legacy Export.Tests component (209) whose pre-existing Core-contract build break was fixed
      by the legacy Core component->legacy Core component swap. legacy Api.Tests component sources were promoted into legacy Api.Tests component
      (61) and the `<Compile Include>` link dropped.
      Phase C: deleted the entire legacy foundational cluster once it had zero external consumers — the root
      the predecessor identity demo exe (+legacy MinimalPdf component), legacy Domain component, legacy Core component, legacy Application component, and
      predecessor implementation, plus legacy Api.Tests component.
      Phase D: switched migration *generated output* from predecessor implementation to predecessor implementation across all
      provider engines + assertions (47 files); renamed the last legacy named component type
      `PvadPdfGeneratorBridge`->`PvadPdfGeneratorBridge`; deleted the legacy `PVAD.sln`/`PVAD.slnx`
      (predecessor implementation are now the only solutions).
      Final state: 0 projects named legacy namespace family, 0 predecessor implementation declarations, 0 real predecessor implementation
      directives; `dotnet build PVAD.sln` passes with 0 errors. Remaining the predecessor identity text is limited to cosmetic
      prose in comments/branding strings and legacy localStorage keys. The legacy `ui-designer/` frontend
      folder was later removed; active frontend source now lives in `pvad-designer/`.
- [x] Physical source layout and final active-string cleanup completed:
      Moved source projects into the agreed `src/Core`, `src/Generation`, `src/Importing`,
      `src/Infrastructure`, and `src/Migrations/*` folders while keeping project names/namespaces stable.
      Updated active runtime/test strings from Canvas to predecessor (predecessor implementation, demo emails,
      OCR/PDF-viewer temp folders). UI localStorage keys now use predecessor names with Canvas legacy fallback keys
      so existing browser drafts/preferences remain readable. Verified with
      `dotnet test PVAD.sln --no-restore --disable-build-servers -m:1` and `npm run build` in
      `pvad-designer` (both passed; existing package/security/chunk-size warnings remain).
