# Rename Canvas To PXA

## Summary

Create a later implementation path for renaming **Canvas** to **Power Dox Automation / PXA**.

This checklist is a planning tracker only. No code rename, namespace rename, docs rewrite, commits, or
server restarts are part of this step.

## Product Naming

- [x] Product/Web name: **Power Dox Automation**
- [x] Short/developer name: **PXA**
- [x] CLI name reserved: `pxa`
- [x] Future native file format reserved: `.pxa`

## Namespace Mapping

| Current name | Future name |
| --- | --- |
| `Canvas.Pdf` | `PXA.Generator.Pdf` or public facade `PXA.Generator` |
| `Canvas.Migration.*` | `PXA.Migration.*` |
| `Canvas.Importer` | `PXA.Importer` |
| `Canvas.FileImporter.*` | `PXA.FileImporter.*` |
| `Canvas.Core` | `PXA.Core` |
| `Canvas.Application` | `PXA.Application` |
| `Canvas.Infrastructure.*` | `PXA.Infrastructure.*` |
| `Canvas.WebApi` | `PXA.WebApi` |
| `Canvas.Domain` | `PXA.Domain` |
| `Canvas.Infrastructure.Pdf` | `PXA.Infrastructure.Pdf` |
| `Canvas.Infrastructure.Word` | `PXA.Infrastructure.Word` |
| `Canvas.Infrastructure.Spreadsheet` | `PXA.Infrastructure.Spreadsheet` |
| `Canvas.Infrastructure.Converters` | `PXA.Infrastructure.Converters` |
| `Canvas.FileImporter.ImageOcr.Worker` | `PXA.FileImporter.ImageOcr.Worker` |
| `Canvas.Mcp` | `PXA.Mcp` or `pxa-mcp` package/tool name |
| `Canvas.Demo` | `PXA.Demo` |

## Current Repo Additions To Include

- [x] Include `Canvas.sln` and `Canvas.slnx` in the rename plan.
- [x] Include all `tests/Canvas.*.Tests` projects in the namespace/project rename plan.
- [x] Include `samples/Canvas.Demo`.
- [x] Include `tools/Canvas.Mcp` package name, README, and smoke script.
- [x] Include `docs/schema/canvas-workbook.schema.json` and schema `$id`/title naming.
- [x] Include `llms.txt`, `llms-full.txt`, and generated OpenAPI/doc artifacts when docs are renamed.
      `llms.txt` and `llms-full.txt` already use Power Dox Automation/PXA as the primary identity while
      documenting legacy `Canvas.Pdf` compatibility. `docs/schema/openapi.json` now uses a PXA title and
      includes additive `/api/pxa/...` aliases for the documented document/export/migration/pdf-viewer/template
      routes while retaining the legacy `/api/...` paths. Auth remains on `/api/auth` by design.
- [x] Include both frontend folders: legacy `ui-designer` and current `ui-designer-v2`.
- [x] Include package names/routes/visible branding in frontend only after backend API compatibility is protected.

## Compatibility Rules

- [ ] Keep `Canvas.*` for one major version as `[Obsolete]` shims.
      Started with the direct legacy `Canvas.Pdf.PdfDocument(...)` constructor marked obsolete with
      diagnostic `PXA0001`, guiding new code to `PXA.Generator.Pdf.CreateDocument(...)` while preserving
      compatibility. The legacy `Canvas.Importer.PdfImporter(...)` constructor now uses diagnostic `PXA0002`
      and guides new integrations to `PXA.Importer.Pdf.LoadAsync(...)`; the PXA facade and intentional
      implementation/compatibility tests suppress that diagnostic locally. Broader namespace/type-level
      obsolete coverage remains open until PXA-owned PDF option
      and value types exist, otherwise active compatibility examples would produce noisy internal warnings.
      Internal implementation-layer usages suppress `PXA0001` locally where they intentionally bridge through
      the legacy Canvas.Pdf engine.
- [x] Prefer additive PXA facades first; avoid one massive physical rename as the first implementation step.
- [ ] Keep old NuGet/package identities or publish forwarding packages for one major version if packages are introduced.
- [x] Keep old HTTP endpoints compatible.
- [x] Keep old JSON fields compatible.
- [x] Add new PXA-oriented fields only alongside legacy fields.
- [x] Keep `CANMIG...` diagnostic IDs stable for now.
- [x] Do not rename unrelated terms such as HTML Canvas, `html2canvas`, `SKCanvas`, or iText `PdfCanvas`.
- [x] Do not rename user/domain words like "canvas" in HTML/CSS drawing contexts unless they refer to the product brand.
- [x] Keep existing document JSON schema fields stable unless a versioned schema migration is added.

Compatibility verification note: the implementation has stayed additive through `PXA.*` facade projects,
PXA `/api/pxa/...` route aliases, PXA schema aliases, and PXA package/tool aliases while retaining the
legacy Canvas routes, JSON contracts, localStorage keys, `CANMIG...` diagnostics, and technical canvas
terms. `[Obsolete]` shims and any future package-forwarding policy remain open because they require a
dedicated public package/versioning decision.

## Documentation Plan

- [x] Move main docs to **Power Dox Automation / PXA** naming later.
- [x] Update active examples in main docs to use future `PXA.*` APIs later.
      Active examples use `PXA.Generator.Pdf.CreateDocument()` as the entry point while retaining
      `using Canvas.Pdf;` for compatibility-phase PDF option/value types such as `PdfColor`,
      `PdfDrawTextOptions`, and `PdfPagePreset`. Full removal of `Canvas.Pdf` from examples depends on a
      future PXA-owned PDF type facade and belongs with the physical/breaking rename phase.
- [x] Keep historical checklist wording when it describes legacy Canvas implementation history.
- [x] Add clear legacy notes to historical checklists instead of blindly replacing every `Canvas` occurrence.
- [x] Add a short glossary: **Power Dox Automation** = product, **PXA** = developer/API/CLI identity, `Canvas.*` = legacy namespace.
- [x] Update docs schemas and generated OpenAPI only after code/API names are stabilized.
      PXA workbook schema alias and OpenAPI PXA route aliases are present; legacy schema/path contracts remain.

## Future Implementation Phases

- [x] Phase 0: inventory all `Canvas` occurrences and classify as product namespace, file/project path, UI branding, docs, schema, or unrelated HTML/graphics canvas.
      Initial inventory completed from current repo state: product namespaces/projects, UI/editor canvas terms, docs/checklists,
      schemas, MCP, samples, and unrelated HTML/graphics canvas usages were identified as separate rename buckets.
- [x] Phase 1: introduce new `PXA.*` public API layer without physical path/project renames.
      Started with additive `PXA.Generator` project and PDF/Word/Spreadsheet facades that delegate to the existing
      Canvas implementations.
      Completed for the current compatibility layer: additive PXA projects now cover generator, importer,
      file importer, migration, core, application, domain, infrastructure, API/export test aliases, MCP,
      schema, and sample entry points while legacy Canvas project paths remain compatible.
- [x] Phase 2: add generator facade target for PDF/Word/Spreadsheet:
      - `Canvas.Pdf` -> `PXA.Generator.Pdf` / `PXA.Generator`
      - Word export APIs -> `PXA.Generator.Word`
      - Spreadsheet APIs -> `PXA.Generator.Spreadsheet`
- [x] Phase 3: move migration namespaces from `Canvas.Migration.*` to `PXA.Migration.*`, including PDF, report, and spreadsheet providers.
      Started with additive `PXA.Migration.Abstractions` project, PXA-facing migration result/diagnostic types,
      and a `CanvasSourceMigrationAdapter` bridge for existing `Canvas.Migration.Abstractions.ISourceMigration`
      implementations.
      First concrete provider facade added with `PXA.Migration.DevExpressPdf`, delegating to the existing
      `Canvas.Migration.DevExpressPdf.DevExpressPdfMigration` while returning PXA migration abstractions.
      PDF-code migration facade set expanded with `PXA.Migration.SyncfusionPdf` and `PXA.Migration.iText7`,
      both delegating to existing Canvas providers and preserving `CANMIG...` diagnostic IDs.
      Additional central PDF provider facades added with `PXA.Migration.AsposePdf` and `PXA.Migration.Apryse`.
      PDF provider facade coverage expanded with `PXA.Migration.DsPdf` and `PXA.Migration.FoxitPdf`.
      PDF provider facade coverage expanded with `PXA.Migration.IronPdf` and `PXA.Migration.GemBoxPdf`.
      PDF provider facade coverage expanded with `PXA.Migration.SpirePdf` and `PXA.Migration.PdfKitNet`.
      PDF provider facade coverage expanded with `PXA.Migration.LeadtoolsPdf` and `PXA.Migration.ActivePdf`.
      PDF provider facade coverage expanded with `PXA.Migration.PdfTools` and `PXA.Migration.PdfToolsToolbox`.
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
      - `Canvas.Importer` -> `PXA.Importer`
      - `Canvas.FileImporter.*` -> `PXA.FileImporter.*`
      Started with additive `PXA.Importer` project and PDF import facade that delegates to the existing
      `Canvas.Importer.PdfImporter`.
      File importer entry point added with `PXA.FileImporter`, PXA-facing `IFileImporter`, provider keys,
      and registry/facades for PDF, DOCX, DOC, ODT, SVG, PPTX, and raster image import.
      Specialized additive facades added with `PXA.FileImporter.ImageAnalysis` and `PXA.FileImporter.ImageOcr`,
      including PXA-facing analysis options/results/diagnostics, OCR options/results/models, OCR engine contract,
      Tesseract engine facades, and Canvas adapters. The OCR worker executable/package name remains a later
      physical rename item in Phase 8/9.
- [x] Phase 5: move infrastructure, application, core, and domain namespaces.
      Started with additive `PXA.Core` project and PXA-owned contract types for design documents,
      spreadsheet workbooks, and export options. JSON-compatible adapters bridge `PXA.Core.Contracts`
      to the existing `Canvas.Core.Contracts` types while engines still use the legacy internals.
      Public PXA generator and file importer facades now expose `PXA.Core.Contracts` instead of
      `Canvas.Core.Contracts` for document import/export surfaces. Remaining follow-up: report migration
      result contracts, application use cases, infrastructure facades, and domain-facing model aliases.
      Report migration results now expose `PXA.Core.Contracts.DesignExportDto` while existing
      Canvas report converters remain the internal implementation.
      Application facade started with additive `PXA.Application` project for Clone, ExtractPages,
      FindAndReplace, and ValidateTemplate use cases. Design use cases expose `PXA.Core.Contracts`;
      ValidateTemplate now accepts `PXA.Domain.Repositories.ITemplateRepository` and uses the
      PXA-to-Canvas repository adapter internally.
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
      and Canvas/PXA repository adapters.
      Completed for additive facade coverage and verified through the PXA solution/test aliases. Remaining
      work here is physical namespace/path replacement, which belongs to Phase 9 and the later breaking
      rename plan.
- [x] Phase 6: update Web/API branding to **Power Dox Automation** and **PXA** while preserving legacy endpoints.
      Started with additive Web API branding discovery endpoint exposed on both legacy
      `/api/system/brand` and PXA `/api/pxa/system/brand` routes. Response advertises
      Power Dox Automation/PXA naming, reserved `pxa` CLI and `.pxa` extension, and legacy
      Canvas compatibility notes.
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
      marking current `Canvas.*` project paths and namespaces as legacy compatibility where needed.
      Smaller UI surfaces now use PXA wording for spreadsheet import, workbook JSON errors,
      code-panel descriptions, generated PDF-code comments, starter text, and global style comments.
      Final inventory leaves only intentional technical canvas terms, legacy compatibility references,
      localStorage/sessionStorage keys, current legacy project paths, and sample content. The legacy
      `ui-designer` folder remains historical for now and is deferred to Phase 8/9 instead of being
      renamed in this UI-branding slice.
      Technical HTML canvas terms, localStorage keys, and legacy `Canvas.Pdf` API references remain
      unchanged for compatibility until their specific surfaces are migrated.
      Frontend package naming slice completed: the active `ui-designer-v2` package now publishes as
      `pxa-designer`, code-editor sample/download/schema identifiers use PXA naming, and the legacy
      `ui-designer` package/title is explicitly marked as historical compatibility rather than active PXA UI.
      Legacy localStorage keys remain unchanged so existing user drafts and preferences survive the rename.
- [x] Phase 8: update MCP/sample/docs/package identities:
      - `tools/Canvas.Mcp` -> `tools/PXA.Mcp` or package `pxa-mcp`
      - `samples/Canvas.Demo` -> `samples/PXA.Demo`
      - active docs and schemas to PXA naming
      Started with MCP package identity: the MCP server advertises `pxa-mcp` as package/server
      identity, exposes `pxa-mcp` as the primary binary while retaining `canvas-mcp` compatibility,
      accepts `PXA_API_URL` while retaining `CANVAS_API_URL`, and publishes `pxa://...` resources while
      retaining legacy `canvas://...` aliases.
      Sample identity started with PXA-facing demo metadata and renderer notes.
      Active docs/AI references started: `llms.txt`, `llms-full.txt`, DocFX landing/config files,
      cookbook intro, documentation approach, and schema descriptions now present Power Dox Automation/PXA
      as the primary identity while preserving legacy `Canvas.*` xrefs, project paths, and schema URIs
      for compatibility.
      Root `README.md` now presents Power Dox Automation/PXA as the primary product identity, describes
      PXA importer/generator/migration surfaces, and keeps current `Canvas.*` paths as legacy project names.
      `ARCHITECTURE.md` now documents the additive `PXA.*` facade layer, PXA importer/generator/migration
      public entry points, and legacy Canvas project boundaries until the later physical rename phase.
      `PROJECT_SUMMARY.md` now presents PXA as the primary product identity, adds the PXA facade project
      group, and keeps `Canvas.*` project/test names as current legacy implementation inventory.
      `TESTING.md` now describes PXA-compatible PDF code/output and editable PXA design targets while
      keeping current `Canvas.*.Tests` project names as the real test inventory.
      `CONTRIBUTING_RENDERERS.md` now names Power Dox Automation/PXA as the extension target, asks new
      developer-facing work to expose `PXA.*` facades, and documents legacy `Canvas.*` implementation
      project patterns during the compatibility phase.
- [ ] Phase 9: optional later physical rename of solution, project files, folders, and test assemblies.
      Started with additive solution alias `PXA.sln`, copied from the current `Canvas.sln` so developers can
      build through a PXA-named entry point while legacy project paths remain stable. Verified with
      `dotnet sln PXA.sln list` and `dotnet build PXA.sln` (0 errors; existing dependency/analyzer/nullability
      warnings remain).
      Added `Canvas.WebApi/PXA.WebApi.http` as a PXA-named HTTP request alias for the `/api/pxa/system/brand`
      endpoint. Assembly, namespace, and folder rename for `Canvas.WebApi` remains a later dedicated slice
      because many API/export tests reference the current `Program` type and `Canvas.WebApi.*` namespaces.
      Renamed the tracked MCP server files from `tools/Canvas.Mcp` to `tools/PXA.Mcp`. The legacy folder now
      keeps a README pointer only; untracked local `node_modules` / lock artifacts may remain there locally.
      The server still exposes the compatibility `canvas-mcp` binary and `canvas://...` resource aliases.
      Renamed the tracked demo sample files from `samples/Canvas.Demo` to `samples/PXA.Demo`, including
      `PXA.Demo.csproj`, and updated `Canvas.sln` / `PXA.sln` to point at the new sample path. The legacy
      sample folder now keeps a README pointer only; local `bin` / `obj` artifacts may remain there locally.
      Added `docs/schema/pxa-workbook.schema.json` as the primary PXA Workbook JSON schema with a PXA `$id`;
      kept `docs/schema/canvas-workbook.schema.json` as the legacy compatibility alias and updated MCP,
      in-app docs, AI docs, and schema tests to prefer the PXA path.
      Added additive `PXA.Api.Tests` and `PXA.Export.Tests` project aliases that compile the existing
      legacy API/export test sources under PXA-named test assemblies. Both `Canvas.sln` and `PXA.sln` now
      include the aliases while the legacy `Canvas.Api.Tests` / `Canvas.Export.Tests` projects remain.

## Future Test Plan

- [x] `dotnet build Canvas.sln`
      Passed with `--disable-build-servers` after the physical sample/schema alias updates (0 errors;
      59 existing dependency/analyzer/nullability/XML-doc/NPOI warnings remain).
- [x] `dotnet build PXA.sln`
      Passed with `--disable-build-servers -m:1` after the frontend/package naming slice (0 errors;
      246 existing warnings remain, mostly NuGet security-index/network warnings plus known package,
      nullability, XML-doc, xUnit analyzer, and NPOI license warnings). A first parallel build attempt
      produced no compiler errors but was stopped after a silent five-minute hang; serial build completed.
- [x] `dotnet build Canvas.slnx` if kept as an active solution entry.
      Not run as an active build target because current `Canvas.slnx` is an empty `<Solution>` with no project entries.
- [x] Relevant migration tests.
      Initial `PXA.Migration.Abstractions.Tests` coverage added for Canvas-to-PXA source migration adapter mapping.
      Initial `PXA.Migration.DevExpressPdf.Tests` coverage added for the first concrete PXA migration provider facade.
      Initial `PXA.Migration.SyncfusionPdf.Tests` and `PXA.Migration.iText7.Tests` coverage added for core PDF
      provider facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.AsposePdf.Tests` and `PXA.Migration.Apryse.Tests` coverage added for provider
      facade compatibility and diagnostic severity mapping.
      Initial `PXA.Migration.DsPdf.Tests` and `PXA.Migration.FoxitPdf.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.IronPdf.Tests` and `PXA.Migration.GemBoxPdf.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.SpirePdf.Tests` and `PXA.Migration.PdfKitNet.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.LeadtoolsPdf.Tests` and `PXA.Migration.ActivePdf.Tests` coverage added for provider
      facade compatibility and warning diagnostic mapping.
      Initial `PXA.Migration.PdfTools.Tests` and `PXA.Migration.PdfToolsToolbox.Tests` coverage added for
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
      - Legacy code with `using Canvas.Pdf;`
      Initial `PXA.Generator.Tests` coverage added for `Pdf.CreateDocument()`, `Spreadsheet.CreateWorkbook()`,
      and `Word.Export(...)`.
      `PXA.Generator.Word.Export(...)` now accepts `PXA.Core.Contracts.DesignExportDto` and adapts to
      the legacy Canvas exporter internally.
      Added explicit legacy `Canvas.Pdf.PdfDocument` smoke coverage and verified with
      `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj` (5 passed).
      Re-verified after adding `PXA0001` obsolete guidance to the legacy `Canvas.Pdf.PdfDocument(...)`
      constructor with `dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj --disable-build-servers`
      (5 passed).
- [x] Importer compatibility tests:
      - New code with `using PXA.Importer;`
      - Legacy code with `using Canvas.Importer;`
      Initial `PXA.Importer.Tests` coverage added for `Pdf.LoadAsync(...)` and PXA-facing import options.
      Initial `PXA.FileImporter.Tests` coverage added for file importer registry keys, factory creation,
      case-insensitive/extension lookup, unknown-key rejection, and SVG/Image facade smoke tests.
      Initial `PXA.FileImporter.ImageAnalysis.Tests` and `PXA.FileImporter.ImageOcr.Tests` coverage added for
      specialized analysis/OCR facades, diagnostics mapping, PXA OCR engine adapter flow, and Tesseract facade identity.
      File importer design results now return `PXA.Core.Contracts.DesignExportDto`.
      Added explicit legacy `Canvas.Importer.PdfImporter` smoke coverage and verified with
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj` (3 passed),
      `dotnet test tests/PXA.FileImporter.Tests/PXA.FileImporter.Tests.csproj` (15 passed),
      `dotnet test --no-build` for `PXA.FileImporter.ImageAnalysis.Tests` (2 passed), and
      `PXA.FileImporter.ImageOcr.Tests` (2 passed).
      Re-verified after adding `PXA0002` obsolete guidance with
      `dotnet test tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj --disable-build-servers --no-restore -m:1`
      (3 passed) and `dotnet build Canvas.WebApi/Canvas.WebApi.csproj --no-restore
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
      and Canvas/PXA repository adapter flows.
      Verified with `dotnet test tests/PXA.Domain.Tests/PXA.Domain.Tests.csproj --no-build` (4 passed).
- [x] Web/API compatibility tests:
      `Canvas.Api.Tests` now covers legacy and PXA branding discovery routes with compatibility notes.
      `Canvas.Api.Tests` now covers PXA export alias routes for format discovery and HTML export.
      `Canvas.Api.Tests` now covers PXA migration alias routes for framework discovery and code conversion.
      `Canvas.Api.Tests` now covers PXA document alias routes for find/replace, clone, and image conversion
      unsupported-format handling.
      `Canvas.Api.Tests` now covers PXA spreadsheet alias routes for JSON-to-workbook creation and
      workbook validation.
      `Canvas.Api.Tests` now covers PXA templates alias routes for template listing and validation.
      `Canvas.Api.Tests` now covers PXA PDF Viewer alias routes for annotation sidecar save/get
      and form field extraction.
      Added and verified `PXA.Api.Tests` alias with
      `dotnet test tests/PXA.Api.Tests/PXA.Api.Tests.csproj --disable-build-servers` (61 passed).
      Re-verified WebApi compilation after adding the first obsolete shim with
      `dotnet build Canvas.WebApi/Canvas.WebApi.csproj --no-restore --disable-build-servers -m:1`
      (0 errors; existing package/NPOI warnings remain; no `PXA0001` internal warnings).
- [x] Export compatibility tests:
      Added and verified `PXA.Export.Tests` alias with
      `dotnet test tests/PXA.Export.Tests/PXA.Export.Tests.csproj --disable-build-servers` (206 passed).
- [x] `npm run build` in `ui-designer-v2`.
      Passed after adding the primary `docs/schema/pxa-workbook.schema.json` reference to the in-app docs
      (Vite chunk-size warning remains existing/non-blocking).
- [x] Relevant Jest tests in `ui-designer-v2`.
      `npm test -- --runTestsByPath src/__tests__/canvasWorkbookSchema.test.ts` passed for the primary PXA
      schema and legacy Canvas schema alias.
- [x] MCP smoke test if `tools/PXA.Mcp` is renamed or rebranded.
      `npx tsx smoke.ts` passed and now verifies both `pxa://schema/pxa-workbook` and
      `canvas://schema/canvas-workbook` resources.
- [ ] UI smoke test for migration page, designer open flow, export, and preview.
      Open: no Playwright/Cypress/E2E smoke harness exists in the repo yet. Current UI verification is limited
      to `npm run build` and targeted Jest tests until a browser smoke harness is added.
- [ ] Documentation link check after main docs are updated.

## Assumptions

- [x] This checklist reserves `.pxa`; it does not implement the file format.
- [x] This checklist reserves `pxa`; it does not implement the CLI.
- [x] Existing uncommitted workspace changes are left untouched.
- [x] The first implementation block should avoid physical path/project renames unless explicitly approved.
- [x] Current repo state includes Spreadsheet engine/migrations and PdfPreview V2 roadmap; the rename plan must cover those additions.
