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
| `Canvas.Pdf` | `PXA.Generator` |
| `Canvas.Migration.*` | `PXA.Migration.*` |
| `Canvas.Importer` | `PXA.Importer` |
| `Canvas.FileImporter.*` | `PXA.FileImporter.*` |
| `Canvas.Core` | `PXA.Core` |
| `Canvas.Application` | `PXA.Application` |
| `Canvas.Infrastructure.*` | `PXA.Infrastructure.*` |
| `Canvas.WebApi` | `PXA.WebApi` |
| `Canvas.Domain` | `PXA.Domain` |

## Compatibility Rules

- [ ] Keep `Canvas.*` for one major version as `[Obsolete]` shims.
- [ ] Keep old HTTP endpoints compatible.
- [ ] Keep old JSON fields compatible.
- [ ] Add new PXA-oriented fields only alongside legacy fields.
- [ ] Keep `CANMIG...` diagnostic IDs stable for now.
- [ ] Do not rename unrelated terms such as HTML Canvas, `html2canvas`, `SKCanvas`, or iText `PdfCanvas`.

## Documentation Plan

- [ ] Move main docs to **Power Dox Automation / PXA** naming later.
- [ ] Update active examples in main docs to use future `PXA.*` APIs later.
- [ ] Keep historical checklist wording when it describes legacy Canvas implementation history.
- [ ] Add clear legacy notes to historical checklists instead of blindly replacing every `Canvas` occurrence.

## Future Implementation Phases

- [ ] Phase 1: introduce new `PXA.*` public API layer.
- [ ] Phase 2: move generator API target from `Canvas.Pdf` to `PXA.Generator`.
- [ ] Phase 3: move migration namespaces from `Canvas.Migration.*` to `PXA.Migration.*`.
- [ ] Phase 4: move importer, file importer, infrastructure, application, core, and domain namespaces.
- [ ] Phase 5: update Web/API/UI branding to **Power Dox Automation** and **PXA**.
- [ ] Phase 6: update main documentation and add legacy notes to historical checklists.
- [ ] Phase 7: optional later physical rename of solution, project files, and folders.

## Future Test Plan

- [ ] `dotnet build Canvas.sln`
- [ ] Relevant migration tests.
- [ ] Generator compatibility tests:
      - New code with `using PXA.Generator;`
      - Legacy code with `using Canvas.Pdf;`
- [ ] `npm run build` in `ui-designer-v2`.
- [ ] UI smoke test for migration page, designer open flow, export, and preview.
- [ ] Documentation link check after main docs are updated.

## Assumptions

- [x] This checklist reserves `.pxa`; it does not implement the file format.
- [x] This checklist reserves `pxa`; it does not implement the CLI.
- [x] Existing uncommitted workspace changes are left untouched.
- [x] The first implementation block should avoid physical path/project renames unless explicitly approved.
