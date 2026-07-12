# PXA PDF Code Migrations

## Goal

Build a `PXA.Migration.*` feature family for migrating existing C# PDF-generation code from third-party vendors to `PXA.Pdf`.

The first milestone started with checklist and architecture scaffolding. Syncfusion PDF, iText7, Aspose.PDF, IronPDF, DevExpress PDF, Apryse, Foxit PDF SDK, DsPdf, GemBox.Pdf, Spire.PDF, PDFKit.NET, LEADTOOLS PDF, ActivePDF, and PDFTools / Pdftools SDK now have Roslyn-backed pilot implementations. PDF Toolbox SDK / Toolbox add-on is queued as a separate direct-generation provider checklist.

## Current Pilot

- [x] Use `SyncfusionPdf` as the first provider pilot
- [x] Use `iText7` as the second provider pilot
- [x] Use `AsposePdf` as the third provider pilot
- [x] Use `IronPdf` as the fourth provider pilot
- [x] Use `DevExpressPdf` as the fifth provider pilot
- [x] Use `Apryse` as the sixth provider pilot
- [x] Use `FoxitPdf` as the seventh provider pilot
- [x] Use `DsPdf` as the eighth provider pilot
- [x] Use `GemBoxPdf` as the ninth provider pilot
- [x] Use `SpirePdf` as the tenth provider pilot
- [x] Use `PdfKitNet` as the eleventh provider pilot, with an explicit package/API identity warning
- [x] Use `LeadtoolsPdf` as the twelfth provider pilot, scoped to direct PDF generation and manual diagnostics for raster/OCR/conversion flows
- [x] Use `ActivePdf` as the thirteenth provider pilot, scoped to likely Toolkit-style generation and manual diagnostics for DocConverter/WebGrabber/COM/printer workflows
- [x] Use `PdfTools` as the fourteenth provider pilot, scoped to likely direct generation and manual diagnostics for conversion/processing workflows
- [x] Use `PdfToolsToolbox` as the fifteenth provider pilot, scoped to PDF Toolbox SDK direct-generation/content APIs
- [x] Start with deterministic C# PDF-generation patterns, not existing-PDF editing
- [x] Treat `PdfDocument`, `Pages.Add`, `PdfGraphics.DrawString`, simple fonts, simple brushes, and `Save` as the first conversion slice
- [x] Use the Syncfusion pilot to validate shared abstraction names before adding all provider projects
- [ ] Promote repeated Syncfusion/iText7/Aspose/IronPDF/DevExpress/Apryse/Foxit/DsPdf/GemBox/Spire/PDFKit.NET/LEADTOOLS/ActivePDF/PDFTools/Toolbox rules into provider-neutral abstractions after the fifteenth prototype

## Architecture Checklist

- [ ] Keep migration projects separate from rendering/importing infrastructure
- [ ] Do not reference migration projects from `PXA.Core`
- [ ] Do not reference migration projects from `PXA.Infrastructure.Pdf`
- [ ] Use `PXA.Pdf` as the target output API
- [x] Add shared abstractions under `src/PXA.Migration.Abstractions`
- [x] Add Roslyn migration infrastructure under `src/PXA.Migration.Roslyn`
- [ ] Add one provider project per vendor under future `src/PXA.Migration.<Provider>`
- [x] Define provider-neutral migration diagnostics
- [ ] Define provider-neutral mapping result model
- [x] Define migration report output for unsupported or manual follow-up work
- [ ] Document dependency direction in `ARCHITECTURE.md` when projects are added

## Future Project Layout

- [x] `src/PXA.Migration.Abstractions`
- [x] `src/PXA.Migration.Roslyn`
- [x] `src/PXA.Migration.iText7`
- [x] `src/PXA.Migration.AsposePdf`
- [x] `src/PXA.Migration.IronPdf`
- [x] `src/PXA.Migration.Apryse`
- [x] `src/PXA.Migration.SyncfusionPdf`
- [x] `src/PXA.Migration.DsPdf`
- [x] `src/PXA.Migration.GemBoxPdf`
- [x] `src/PXA.Migration.SpirePdf`
- [x] `src/PXA.Migration.PdfKitNet`
- [x] `src/PXA.Migration.LeadtoolsPdf`
- [x] `src/PXA.Migration.FoxitPdf`
- [x] `src/PXA.Migration.DevExpressPdf`
- [x] `src/PXA.Migration.ActivePdf`
- [x] `src/PXA.Migration.PdfTools`
- [x] `src/PXA.Migration.PdfToolsToolbox`

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
- [ ] Add `using PXA.Pdf` when code fixes introduce PXA APIs
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
| IronPDF | `Code-Migration-IronPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| Apryse SDK | `Code-Migration-Apryse.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| Syncfusion PDF | `Code-Migration-SyncfusionPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| DsPdf / Document Solutions | `Code-Migration-DsPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| GemBox.Pdf | `Code-Migration-GemBoxPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| Spire.PDF | `Code-Migration-SpirePdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| PDFKit.NET | `Code-Migration-PdfKitNet.md` | [ ] | [x] | [x] | [x] | [x] | Pilot cautious |
| LEADTOOLS PDF | `Code-Migration-LeadtoolsPdf.md` | [ ] | [x] | [x] | [x] | [x] | Pilot cautious |
| Foxit PDF SDK | `Code-Migration-FoxitPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| DevExpress PDF | `Code-Migration-DevExpressPdf.md` | [x] | [x] | [x] | [x] | [x] | Pilot detailed |
| ActivePDF | `Code-Migration-ActivePdf.md` | [ ] | [x] | [x] | [x] | [x] | Pilot cautious |
| PDFTools / Pdftools SDK | `Code-Migration-PdfTools.md` | [x] | [x] | [x] | [x] | [x] | Pilot cautious |
| PDF Toolbox SDK / Toolbox add-on | `Code-Migration-PdfToolsToolbox.md` | [x] | [x] | [x] | [x] | [x] | Pilot cautious |

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
- [ ] Expected `PXA.Pdf` output compiles in a test fixture

## Test Plan For Future Implementation

- [ ] Unit-test each vendor mapper with small C# snippets
- [ ] Verify diagnostics for unsupported APIs
- [ ] Verify diagnostics for ambiguous APIs
- [ ] Verify code fixes produce compilable `PXA.Pdf` code
- [ ] Add snapshot tests for before/after migrations
- [ ] Add one integration test per provider project using a realistic sample
- [ ] Run `dotnet build PXA.sln` after adding projects

## Assumptions

- [x] Use `PXA.Migration.*`, not `Convas.Migration.*`
- [x] Use Markdown extension `.md`; `Code-Migrations.dm` is treated as a typo
- [x] Use canonical provider names in checklist titles and future project names
- [x] Keep this milestone to architecture and checklist scaffolding
- [x] Fill detailed provider API mappings later, one provider at a time
