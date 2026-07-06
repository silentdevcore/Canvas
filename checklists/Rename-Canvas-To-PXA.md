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

- [ ] Include `Canvas.sln` and `Canvas.slnx` in the rename plan.
- [ ] Include all `tests/Canvas.*.Tests` projects in the namespace/project rename plan.
- [ ] Include `samples/Canvas.Demo`.
- [ ] Include `tools/Canvas.Mcp` package name, README, and smoke script.
- [ ] Include `docs/schema/canvas-workbook.schema.json` and schema `$id`/title naming.
- [ ] Include `llms.txt`, `llms-full.txt`, and generated OpenAPI/doc artifacts when docs are renamed.
- [ ] Include both frontend folders: legacy `ui-designer` and current `ui-designer-v2`.
- [ ] Include package names/routes/visible branding in frontend only after backend API compatibility is protected.

## Compatibility Rules

- [ ] Keep `Canvas.*` for one major version as `[Obsolete]` shims.
- [ ] Prefer additive PXA facades first; avoid one massive physical rename as the first implementation step.
- [ ] Keep old NuGet/package identities or publish forwarding packages for one major version if packages are introduced.
- [ ] Keep old HTTP endpoints compatible.
- [ ] Keep old JSON fields compatible.
- [ ] Add new PXA-oriented fields only alongside legacy fields.
- [ ] Keep `CANMIG...` diagnostic IDs stable for now.
- [ ] Do not rename unrelated terms such as HTML Canvas, `html2canvas`, `SKCanvas`, or iText `PdfCanvas`.
- [ ] Do not rename user/domain words like "canvas" in HTML/CSS drawing contexts unless they refer to the product brand.
- [ ] Keep existing document JSON schema fields stable unless a versioned schema migration is added.

## Documentation Plan

- [ ] Move main docs to **Power Dox Automation / PXA** naming later.
- [ ] Update active examples in main docs to use future `PXA.*` APIs later.
- [ ] Keep historical checklist wording when it describes legacy Canvas implementation history.
- [ ] Add clear legacy notes to historical checklists instead of blindly replacing every `Canvas` occurrence.
- [ ] Add a short glossary: **Power Dox Automation** = product, **PXA** = developer/API/CLI identity, `Canvas.*` = legacy namespace.
- [ ] Update docs schemas and generated OpenAPI only after code/API names are stabilized.

## Future Implementation Phases

- [x] Phase 0: inventory all `Canvas` occurrences and classify as product namespace, file/project path, UI branding, docs, schema, or unrelated HTML/graphics canvas.
      Initial inventory completed from current repo state: product namespaces/projects, UI/editor canvas terms, docs/checklists,
      schemas, MCP, samples, and unrelated HTML/graphics canvas usages were identified as separate rename buckets.
- [ ] Phase 1: introduce new `PXA.*` public API layer without physical path/project renames.
      Started with additive `PXA.Generator` project and PDF/Word/Spreadsheet facades that delegate to the existing
      Canvas implementations.
- [ ] Phase 2: add generator facade target for PDF/Word/Spreadsheet:
      - `Canvas.Pdf` -> `PXA.Generator.Pdf` / `PXA.Generator`
      - Word export APIs -> `PXA.Generator.Word`
      - Spreadsheet APIs -> `PXA.Generator.Spreadsheet`
- [ ] Phase 3: move migration namespaces from `Canvas.Migration.*` to `PXA.Migration.*`, including PDF, report, and spreadsheet providers.
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
- [ ] Phase 5: move infrastructure, application, core, and domain namespaces.
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
- [ ] Phase 7: update UI branding in `ui-designer-v2` and decide whether legacy `ui-designer` is renamed, archived, or left as historical.
- [ ] Phase 8: update MCP/sample/docs/package identities:
      - `tools/Canvas.Mcp` -> `tools/PXA.Mcp` or package `pxa-mcp`
      - `samples/Canvas.Demo` -> `samples/PXA.Demo`
      - active docs and schemas to PXA naming
- [ ] Phase 9: optional later physical rename of solution, project files, folders, and test assemblies.

## Future Test Plan

- [ ] `dotnet build Canvas.sln`
- [ ] `dotnet build Canvas.slnx` if kept as an active solution entry.
- [ ] Relevant migration tests.
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
- [ ] Spreadsheet migration tests:
      - Aspose.Cells
      - ClosedXML
      - EPPlus
      - GemBox.Spreadsheet
      - NPOI
      - Spire.XLS
      - SpreadsheetLight
      - Syncfusion XlsIO
- [ ] Generator compatibility tests:
      - New code with `using PXA.Generator;`
      - Legacy code with `using Canvas.Pdf;`
      Initial `PXA.Generator.Tests` coverage added for `Pdf.CreateDocument()`, `Spreadsheet.CreateWorkbook()`,
      and `Word.Export(...)`.
      `PXA.Generator.Word.Export(...)` now accepts `PXA.Core.Contracts.DesignExportDto` and adapts to
      the legacy Canvas exporter internally.
- [ ] Importer compatibility tests:
      - New code with `using PXA.Importer;`
      - Legacy code with `using Canvas.Importer;`
      Initial `PXA.Importer.Tests` coverage added for `Pdf.LoadAsync(...)` and PXA-facing import options.
      Initial `PXA.FileImporter.Tests` coverage added for file importer registry keys, factory creation,
      case-insensitive/extension lookup, unknown-key rejection, and SVG/Image facade smoke tests.
      Initial `PXA.FileImporter.ImageAnalysis.Tests` and `PXA.FileImporter.ImageOcr.Tests` coverage added for
      specialized analysis/OCR facades, diagnostics mapping, PXA OCR engine adapter flow, and Tesseract facade identity.
      File importer design results now return `PXA.Core.Contracts.DesignExportDto`.
- [ ] Core compatibility tests:
      Initial `PXA.Core.Tests` coverage added for DesignExportDto, SpreadsheetDto, and ExportOptions adapters.
- [ ] Application compatibility tests:
      Initial `PXA.Application.Tests` coverage added for CloneTemplate, ExtractPages, FindAndReplace,
      and ValidateTemplate facades.
- [ ] Infrastructure compatibility tests:
      Initial `PXA.Infrastructure.Pdf.Tests` coverage added for PDF rendering, file generation,
      diagnostics reading, capabilities, and service delegation.
      Initial `PXA.Infrastructure.Word.Tests` coverage added for DOCX export with PXA core contracts,
      Word renderer capabilities, and digital-signing facade delegation.
      Initial `PXA.Infrastructure.Spreadsheet.Tests` coverage added for fluent workbook authoring,
      XLSX import/export, calculation, validation, spreadsheet-to-design, Excel document export,
      and sheet renderer capabilities.
      Initial `PXA.Infrastructure.Converters.Tests` coverage added for text/image/ODT converter exports,
      converter capabilities, and PXA core contract adaptation.
- [ ] Domain compatibility tests:
      Initial `PXA.Domain.Tests` coverage added for design template conversion, validation result conversion,
      and Canvas/PXA repository adapter flows.
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
- [ ] `npm run build` in `ui-designer-v2`.
- [ ] Relevant Jest tests in `ui-designer-v2`.
- [ ] MCP smoke test if `tools/Canvas.Mcp` is renamed or rebranded.
- [ ] UI smoke test for migration page, designer open flow, export, and preview.
- [ ] Documentation link check after main docs are updated.

## Assumptions

- [x] This checklist reserves `.pxa`; it does not implement the file format.
- [x] This checklist reserves `pxa`; it does not implement the CLI.
- [x] Existing uncommitted workspace changes are left untouched.
- [x] The first implementation block should avoid physical path/project renames unless explicitly approved.
- [x] Current repo state includes Spreadsheet engine/migrations and PdfPreview V2 roadmap; the rename plan must cover those additions.
