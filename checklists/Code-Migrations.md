# Canvas PDF Code Migrations

## Goal

Build a `Canvas.Migration.*` feature family for migrating existing C# PDF-generation code from third-party vendors to `Canvas.Pdf`.

The first milestone started with checklist and architecture scaffolding. Syncfusion PDF and iText7 now have Roslyn-backed pilot implementations; the remaining providers are still planned as separate follow-up slices.

## Current Pilot

- [x] Use `SyncfusionPdf` as the first provider pilot
- [x] Use `iText7` as the second provider pilot
- [x] Use `AsposePdf` as the third provider pilot
- [x] Start with deterministic C# PDF-generation patterns, not existing-PDF editing
- [x] Treat `PdfDocument`, `Pages.Add`, `PdfGraphics.DrawString`, simple fonts, simple brushes, and `Save` as the first conversion slice
- [x] Use the Syncfusion pilot to validate shared abstraction names before adding all provider projects
- [ ] Promote repeated Syncfusion/iText7/Aspose rules into provider-neutral abstractions after the third prototype

## Architecture Checklist

- [ ] Keep migration projects separate from rendering/importing infrastructure
- [ ] Do not reference migration projects from `Canvas.Core`
- [ ] Do not reference migration projects from `Canvas.Infrastructure.Pdf`
- [ ] Use `Canvas.Pdf` as the target output API
- [x] Add shared abstractions under `src/Canvas.Migration.Abstractions`
- [x] Add Roslyn migration infrastructure under `src/Canvas.Migration.Roslyn`
- [ ] Add one provider project per vendor under future `src/Canvas.Migration.<Provider>`
- [x] Define provider-neutral migration diagnostics
- [ ] Define provider-neutral mapping result model
- [x] Define migration report output for unsupported or manual follow-up work
- [ ] Document dependency direction in `ARCHITECTURE.md` when projects are added

## Future Project Layout

- [x] `src/Canvas.Migration.Abstractions`
- [x] `src/Canvas.Migration.Roslyn`
- [x] `src/Canvas.Migration.iText7`
- [x] `src/Canvas.Migration.AsposePdf`
- [ ] `src/Canvas.Migration.IronPdf`
- [ ] `src/Canvas.Migration.Apryse`
- [x] `src/Canvas.Migration.SyncfusionPdf`
- [ ] `src/Canvas.Migration.DsPdf`
- [ ] `src/Canvas.Migration.GemBoxPdf`
- [ ] `src/Canvas.Migration.SpirePdf`
- [ ] `src/Canvas.Migration.PdfKitNet`
- [ ] `src/Canvas.Migration.LeadtoolsPdf`
- [ ] `src/Canvas.Migration.FoxitPdf`
- [ ] `src/Canvas.Migration.DevExpressPdf`
- [ ] `src/Canvas.Migration.ActivePdf`

## Shared Roslyn Analyzer And Code Fix Tasks

- [ ] Detect vendor package references where possible
- [ ] Detect vendor namespaces via syntax model
- [ ] Resolve vendor types via semantic model
- [ ] Detect deterministic document creation patterns
- [ ] Detect deterministic page creation patterns
- [ ] Detect deterministic drawing calls
- [ ] Detect save/export calls
- [ ] Produce diagnostics for unsupported APIs
- [ ] Produce diagnostics for ambiguous APIs
- [ ] Offer code fixes only for deterministic mappings
- [ ] Preserve user formatting where practical
- [ ] Add `using Canvas.Pdf` when code fixes introduce Canvas APIs
- [ ] Remove obsolete vendor `using` directives only when safe
- [x] Emit a migration report with converted, skipped, and manual items

## Common Mapping Categories

- [ ] Document creation
- [ ] Page creation and page sizes
- [ ] Text drawing
- [ ] Fonts and font styles
- [ ] Colors and color spaces
- [ ] Images
- [ ] Tables
- [ ] Shapes and lines
- [ ] Headers and footers
- [ ] Page numbering
- [ ] Metadata and document info
- [ ] Bookmarks and outlines
- [ ] Links and annotations
- [ ] Save/export
- [ ] Unsupported APIs

## Vendor Progress

| Provider | Checklist | Package/API identified | Namespaces/classes listed | Mapping placeholders | Samples | Tests planned | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| iText7 | `Code-Migration-iText7.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| Aspose.PDF | `Code-Migration-AsposePdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| IronPDF | `Code-Migration-IronPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| Apryse SDK | `Code-Migration-Apryse.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| Syncfusion PDF | `Code-Migration-SyncfusionPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| DsPdf / Document Solutions | `Code-Migration-DsPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| GemBox.Pdf | `Code-Migration-GemBoxPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| Spire.PDF | `Code-Migration-SpirePdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| PDFKit.NET | `Code-Migration-PdfKitNet.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| LEADTOOLS PDF | `Code-Migration-LeadtoolsPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| Foxit PDF SDK | `Code-Migration-FoxitPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| DevExpress PDF | `Code-Migration-DevExpressPdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |
| ActivePDF | `Code-Migration-ActivePdf.md` | [ ] | [ ] | [ ] | [ ] | [ ] | Skeleton |

## Provider Migration-Ready Acceptance Criteria

- [ ] Provider package names and target namespaces are documented
- [ ] Common classes and factory patterns are documented
- [ ] At least one basic document creation sample is mapped
- [ ] At least one text drawing sample is mapped
- [ ] At least one image or shape sample is mapped, if supported by the provider
- [ ] Unsupported or manual-only APIs are listed
- [ ] Analyzer diagnostics are defined
- [ ] Code fix behavior is defined
- [ ] Before/after migration snapshots are planned
- [ ] Integration sample is planned
- [ ] Expected `Canvas.Pdf` output compiles in a test fixture

## Test Plan For Future Implementation

- [ ] Unit-test each vendor mapper with small C# snippets
- [ ] Verify diagnostics for unsupported APIs
- [ ] Verify diagnostics for ambiguous APIs
- [ ] Verify code fixes produce compilable `Canvas.Pdf` code
- [ ] Add snapshot tests for before/after migrations
- [ ] Add one integration test per provider project using a realistic sample
- [ ] Run `dotnet build Canvas.sln` after adding projects

## Assumptions

- [x] Use `Canvas.Migration.*`, not `Convas.Migration.*`
- [x] Use Markdown extension `.md`; `Code-Migrations.dm` is treated as a typo
- [x] Use canonical provider names in checklist titles and future project names
- [x] Keep this milestone to architecture and checklist scaffolding
- [x] Fill detailed provider API mappings later, one provider at a time
