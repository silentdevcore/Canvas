# Documentation Audit

## Goal

Keep the documentation set coherent as PXA grows across PDF generation, importers, migrations, report conversion, and the `ui-designer-v2` frontend.

## Source Of Truth Rules

| Area | Source of truth |
|------|-----------------|
| Product overview and user-facing capabilities | `README.md` |
| Project boundaries and dependency direction | `ARCHITECTURE.md` |
| Compact inventory of projects, endpoints, and tests | `PROJECT_SUMMARY.md` |
| Extension patterns | `CONTRIBUTING_RENDERERS.md` |
| Test matrix and commands | `TESTING.md` |
| PDF engine API | `PXA/TECHNICAL_DOCUMENTATION.md`, `PXA/README.md` |
| Feature/milestone history | `checklists/*.md` |
| Multi-language UI behavior | `ui-designer-v2/MULTILANGUAGE.md`, `checklists/multi-languages.md` |
| **Element definitions (props, format support, examples)** | `ui-designer-v2/src/docs/elementCatalog.ts` (drives DocsPage, HelpModal, llms-full, schema enum, MCP) |
| **Design JSON contract for validation** | `docs/schema/design-export.schema.json` |
| **AI/agent capability reference** | `llms.txt`, `llms-full.txt`, and the `tools/PXA.Mcp` MCP server |
| **C# `PXA.Pdf` API reference + recipes** | XML doc comments + DocFX (`docs/docfx.json`), `docs/csharp-cookbook.md` |
| **Documentation strategy** | `docs/Documentation-Approach.md` |

## Completed In This Audit

- [x] **[high]** Update `ARCHITECTURE.md` for `PXA.Importer`, `PXA.FileImporter.*`, `PXA.Migration.*`, RDL, and RPX.
- [x] **[high]** Replace historical `PdfImporter`/PdfPig architecture language with current `PXA.Importer` source of truth.
- [x] **[high]** Update `PROJECT_SUMMARY.md` with current project groups, endpoints, migration providers, report converters, importers, and test groups.
- [x] **[high]** Add migration endpoints and current import endpoints to `README.md`.
- [x] **[medium]** Add a `README.md` documentation map.
- [x] **[high]** Expand `CONTRIBUTING_RENDERERS.md` into renderers, importers, migrations, report converters, and document operations.
- [x] **[high]** Expand `TESTING.md` with file importer, PDF importer, image analysis/OCR, migration, report migration, and frontend test groups.
- [x] **[medium]** Link `PXA/README.md` and `PXA/TECHNICAL_DOCUMENTATION.md` to PDF encryption and provider feature-gap roadmaps.
- [x] **[medium]** Add `checklists/PxaPdf-Provider-Feature-Gaps.md`.

## Follow-Up Audit Items

- [ ] **[medium]** Review old `ui-designer/` checklists and label which are legacy vs active `ui-designer-v2`.
- [ ] **[medium]** Review `checklists/Last-Features.md` and replace remaining old `PdfImporter.cs`/PdfPig wording with current `PXA.Importer` wording or a historical note.
- [ ] **[medium]** Review `handoff.md` and decide whether it is still an active handoff document or should be archived.
- [ ] **[low]** Add a short `checklists/README.md` index if checklist count keeps growing.
- [ ] **[low]** Add a generated docs/link check script if broken links become recurring.

## Documentation Change Checklist

Use this when adding a user-facing capability:

- [ ] Update `README.md` feature/API tables.
- [ ] Update `PROJECT_SUMMARY.md` inventory.
- [ ] Update `ARCHITECTURE.md` if project boundaries or dependencies changed.
- [ ] Update `CONTRIBUTING_RENDERERS.md` if a new extension pattern appears.
- [ ] Update `TESTING.md` if test project groups or commands changed.
- [ ] Update or create a focused checklist under `checklists/`.
- [ ] Verify relative links to new docs/files.

## Notes

- Checklists are retained as history and implementation trackers; they are not expected to be perfectly deduplicated.
- Main docs should be concise and current. Detailed implementation notes belong in focused checklists.
- Repo state is the source of truth for this audit; external vendor documentation is only needed when a checklist explicitly requires provider research.
