# Report Designer Migration Roadmap

Tracks which report-designer / report-file formats can be opened as **editable PXA designs** via
`POST /api/migration/report-to-design`. Prioritized to start with vendors whose PDF-code migration we
already ship (`PXA.Migration.*Pdf`); the report-designer vendors among them are the natural
report-to-design targets.

Legend — **Done?**: ✅ shipped · 🔜 recommended next · ❌ not started · ⛔ blocked (not feasible here) · — out of scope (no report designer)

## Current status summary

**Overall status:** most major text/XML report-designer formats now have a V1 path into editable PXA
designs. The remaining work is mostly fidelity, runtime semantics, and real-sample validation rather
than adding another basic XML parser.

| Status | Designers / formats | Notes |
| --- | --- | --- |
| ✅ Shipped | DevExpress XtraReports (`.repx`, C#), Syncfusion/Bold Reports (`.rdl`, `.rdlc`), SSRS/RDLC/Power BI Report Builder (`.rdl`, `.rdlc`), ActiveReports Page (`.rdlx`), ActiveReports Section (`.rpx`), ActiveReports JS (`.json` marked), FastReport (`.frx`), Telerik Reporting (`.trdx`), Stimulsoft (`.mrt`), JasperReports (`.jrxml`) | Openable in `ui-designer-v2` through `POST /api/migration/report-to-design`. Fidelity varies by provider; advanced regions may be preserved as metadata-rich placeholders. |
| ❌ Not started | List & Label (`.lst`, `.lsr`) | Best next candidate if the goal is adding a new designer family. Needs format/sample audit first. |
| ⛔ Blocked | Crystal Reports (`.rpt`) | Proprietary binary/OLE format; practical conversion needs Windows + SAP Crystal Reports SDK or an intermediate export path. |
| — Out of scope | iText 7, Apryse/PDFTron, Aspose.PDF, Foxit PDF SDK, IronPDF, Spire.PDF, GemBox.Pdf, PDFKit.NET, LEADTOOLS, ActivePDF, PDF Tools/Toolbox | PDF SDKs/libraries, not visual report designers. They belong to PDF-code migration or PXA.PDF feature parity, not report-designer migration. |

**Recommended direction:** if we want a new designer, start **List & Label** with a format/sample audit.
If we want product quality, continue with **DevExpress fidelity** and real-sample validation for
**ActiveReports RPX / ActiveReports JS**.

## Priority 1 — Vendors already in our migration set

| Designer | Manufacturer | Tech (format) | Features | Done? |
| --- | --- | --- | --- | --- |
| DevExpress XtraReports | DevExpress | `.repx` XML + C# (banded) | Bands (Report/Page/Group/Detail); XRLabel/Table/Line/Shape/PictureBox/BarCode/RichText/CheckBox; expression bindings | ✅ `PXA.Migration.DevExpressReport` |
| Syncfusion Report Designer / Bold Reports | Syncfusion | `.rdl` / `.rdlc` (RDL XML, region) | Body + Page header/footer; Textbox/Tablix/Table/Line/Rectangle/Image; `=Fields!` bindings | ✅ `PXA.Migration.Rdl` |
| ActiveReports — Page / RDLX reports | MESCIUS (GrapeCity) | `.rdlx` (RDL XML) | RDL-family; report items + `CustomReportItem` barcodes | ✅ `PXA.Migration.Rdl` — see [Designer-Migration-ActiveReports.md](Designer-Migration-ActiveReports.md) |
| ActiveReports — Section reports | MESCIUS (GrapeCity) | `.rpx` (XML, banded) | Section bands; Label/TextBox/Line/Shape/Picture/Barcode/CheckBox/RichText/SubReport; `DataField` bindings | ✅ `PXA.Migration.Rpx` — see [Designer-Migration-ActiveReports.md](Designer-Migration-ActiveReports.md) |
| iText 7 | iText Group | C# PDF library | PDF-generation API — no visual report designer | — |
| Apryse (PDFTron) | Apryse | C# PDF SDK | PDF SDK — no report designer | — |
| Aspose.PDF | Aspose | C# PDF library | No banded report designer | — |
| Foxit PDF SDK | Foxit Software | C# PDF SDK | No report designer | — |
| IronPDF | Iron Software | C# (HTML→PDF) | No report designer | — |
| Spire.PDF | E-iceblue | C# PDF library | No report designer | — |
| GemBox.Pdf | GemBox Software | C# PDF library | No report designer | — |
| PDFKit.NET | TallComponents | C# PDF library | No report designer | — |
| LEADTOOLS | LEAD Technologies | C# imaging/PDF SDK | No banded report designer | — |
| ActivePDF | ActivePDF | C# PDF toolkit | No report designer | — |
| PDF Tools / Toolbox | PDF Tools AG | C# PDF SDK | No report designer | — |

> Microsoft **SSRS**, **RDLC**, and **Power BI Report Builder** all emit the same **RDL** XML — already
> covered by `PXA.Migration.Rdl`.

## Priority 2 — Other report designers (future candidates, outside our set)

| Designer | Manufacturer | Tech (format) | Features | Done? |
| --- | --- | --- | --- | --- |
| FastReport .NET | Fast Reports Inc. | `.frx` (plain XML, banded) | ReportTitle/PageHeader/DataBand/…/PageFooter; TextObject/Line/Shape/Picture/Barcode/CheckBox; `[Source.Column]` bindings | ✅ `PXA.Migration.FastReport` — see [Designer-Migration-FastReport.md](Designer-Migration-FastReport.md) |
| Telerik Reporting | Progress (Telerik) | `.trdx` (XML) / `.trdp` (zip) | Sections; TextBox/HtmlTextBox/PictureBox/Shape/Barcode/Panel; StyleSheet styles; `=Fields.X` bindings | ✅ `PXA.Migration.Telerik` — see [Designer-Migration-Telerik.md](Designer-Migration-Telerik.md) |
| Stimulsoft Reports | Stimulsoft | `.mrt` (XML `StiSerializer`; JSON is V2) | Bands + Text/Image/Line/Rect/BarCode/Panel; `{Source.Field}` bindings | ✅ `PXA.Migration.Stimulsoft` — see [Designer-Migration-Stimulsoft.md](Designer-Migration-Stimulsoft.md) |
| JasperReports | Cloud Software Group (Jaspersoft) | `.jrxml` (XML, banded) | title/pageHeader/detail/…; staticText/textField/line/rectangle/ellipse/image/frame; named styles; `$F{}` bindings | ✅ `PXA.Migration.JasperReports` — see [Designer-Migration-JasperReports.md](Designer-Migration-JasperReports.md) |
| Crystal Reports | SAP | `.rpt` (binary, proprietary) | Banded; **proprietary binary OLE format — no open parser, needs the Windows-only SAP SDK** | ⛔ Blocked — see [Designer-Migration-Crystal.md](Designer-Migration-Crystal.md) |
| List & Label | combit | `.lst` / `.lsr` | Banded report container | ❌ |
| ActiveReports JS | MESCIUS | JSON report model | Web/JS designer (distinct from `.rdlx`/`.rpx`) | ✅ V1 `PXA.Migration.ActiveReportsJs` — marked JSON only |

## Recommended next work — provider fidelity

Most major text/XML report-designer formats now have a V1 converter. The highest-value work is no
longer adding another XML parser; it is improving layout fidelity in the converters that are already
openable in `ui-designer-v2`.

**Current audit, 2026-06-21:** the shared table-fidelity pass is mostly complete. RDL/Syncfusion,
FastReport, Telerik, DevExpress table cells, and the exporter/codegen paths now preserve and render
`CellStyles` for per-cell background, text alignment, borders, padding, and font where the source format
provides it. The remaining high-value work is no longer basic table extraction; it is repeat/group
semantics, real-sample validation for RPX/FastReport/Telerik, and native editing/runtime support for
advanced regions.

### P0 — make common business reports round-trip better

1. **DevExpress XtraReports fidelity** — finish the features most visible in real designer files:
   `GroupHeaderBand`/`GroupFooterBand` repeat semantics, `ReportFooterBand` once-at-end behaviour,
   `CanGrow`/`CanShrink`, anchoring, multi-`DetailReportBand`, non-text data bindings, and high-use
   controls such as `XRChart`, `XRGauge`, and `XRPivotGrid`.
2. **RDL / Syncfusion / SSRS table fidelity** — core P0/P1 fidelity is done: row/column hierarchy
   headers, nested/detail groups, percentage/relative column widths, external/database images,
   multi-run textbox formatting, chart metadata, report parameters, filters, navigation, and per-cell
   styles are preserved/rendered. Remaining work is P2 native PXA UX/runtime polish such as parameter
   editor controls and compound table-cell rendering.
3. **ActiveReports RPX section fidelity** — core metadata/visual preservation is implemented:
   `GroupHeader`/`GroupFooter`, `CanGrow`/`CanShrink`, `OutputFormat`, `PageBreak`,
   `CrossSectionLine`, `CrossSectionBox`, subreport inlining, and embedded-script metadata. Remaining
   P0 is tuning against real designer-saved `.rpx` files.

### P1 — normalize fidelity across shipped non-core providers

4. **Shared group/repeat model** — define a common internal representation for group headers, group
   footers, detail repeats, report footers, and child/sub-detail bands, then apply it to DevExpress,
   RPX, FastReport, JasperReports, Telerik, and Stimulsoft instead of each converter flattening those
   concepts differently.
5. **Tables and complex regions** — FastReport `TableObject`, Telerik `Table`/`CrossTab`, JasperReports
   `jr:table`, RDL/ActiveReports table cells, and DevExpress XRTableCell imports now produce editable
   PXA tables with preserved cell data/styles. Remaining complex regions are charts, maps, gauges,
   crosstabs, pivot-like regions, and subreport/book-part orchestration; keep these as positioned,
   metadata-rich placeholders until PXA has native equivalents.
6. **Styles and borders** — per-cell table styling is now modelled via `CellStyles` and rendered across
   canvas/preview/export/codegen. Remaining provider-specific style work is mainly non-table style
   inheritance/conditional styles and runtime expression evaluation.

### P2 — package and binary inputs

7. **Binary/package upload path** — ✅ **done (backend):** `report-to-design` accepts a base64 binary
   upload (`sourceBase64`); `ReportPackageExtractor` (PXA.Migration.Abstractions) detects ZIP, unzips,
   picks the inner report (first entry a detector recognizes), and feeds the existing converters, with the
   remaining text entries passed as resources. Unblocks `.trdp` and packaged `.rdlx`. Remaining: a binary
   file picker in the migration UI (currently a text textarea), and any Crystal `.rpt` OLE workaround.
8. **New provider candidates** — only after P0/P1: List & Label (`.lst`/`.lsr`). ActiveReports JS JSON
   has a conservative V1 path for explicitly marked reports; real vendor-saved samples are still needed
   before expanding the detector/schema support. Crystal Reports remains blocked unless a Windows + SAP
   SDK conversion path is introduced.
