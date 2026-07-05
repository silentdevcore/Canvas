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
- [ ] Phase 4: move importer and file importer namespaces:
      - `Canvas.Importer` -> `PXA.Importer`
      - `Canvas.FileImporter.*` -> `PXA.FileImporter.*`
      Started with additive `PXA.Importer` project and PDF import facade that delegates to the existing
      `Canvas.Importer.PdfImporter`.
- [ ] Phase 5: move infrastructure, application, core, and domain namespaces.
- [ ] Phase 6: update Web/API branding to **Power Dox Automation** and **PXA** while preserving legacy endpoints.
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
- [ ] Importer compatibility tests:
      - New code with `using PXA.Importer;`
      - Legacy code with `using Canvas.Importer;`
      Initial `PXA.Importer.Tests` coverage added for `Pdf.LoadAsync(...)` and PXA-facing import options.
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
