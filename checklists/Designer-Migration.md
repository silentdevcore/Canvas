# Report Designer Migration Roadmap

Tracks which report-designer / report-file formats can be opened as **editable Canvas designs** via
`POST /api/migration/report-to-design`. Prioritized to start with vendors whose PDF-code migration we
already ship (`Canvas.Migration.*Pdf`); the report-designer vendors among them are the natural
report-to-design targets.

Legend — **Done?**: ✅ shipped · 🔜 recommended next · ❌ not started · ⛔ blocked (not feasible here) · — out of scope (no report designer)

## Priority 1 — Vendors already in our migration set

| Designer | Manufacturer | Tech (format) | Features | Done? |
| --- | --- | --- | --- | --- |
| DevExpress XtraReports | DevExpress | `.repx` XML + C# (banded) | Bands (Report/Page/Group/Detail); XRLabel/Table/Line/Shape/PictureBox/BarCode/RichText/CheckBox; expression bindings | ✅ `Canvas.Migration.DevExpressReport` |
| Syncfusion Report Designer / Bold Reports | Syncfusion | `.rdl` / `.rdlc` (RDL XML, region) | Body + Page header/footer; Textbox/Tablix/Table/Line/Rectangle/Image; `=Fields!` bindings | ✅ `Canvas.Migration.Rdl` |
| ActiveReports — Page / RDLX reports | MESCIUS (GrapeCity) | `.rdlx` (RDL XML) | RDL-family; report items + `CustomReportItem` barcodes | ✅ `Canvas.Migration.Rdl` — see [Designer-Migration-ActiveReports.md](Designer-Migration-ActiveReports.md) |
| ActiveReports — Section reports | MESCIUS (GrapeCity) | `.rpx` (XML, banded) | Section bands; Label/TextBox/Line/Shape/Picture/Barcode/CheckBox/RichText/SubReport; `DataField` bindings | ✅ `Canvas.Migration.Rpx` — see [Designer-Migration-ActiveReports.md](Designer-Migration-ActiveReports.md) |
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
> covered by `Canvas.Migration.Rdl`.

## Priority 2 — Other report designers (future candidates, outside our set)

| Designer | Manufacturer | Tech (format) | Features | Done? |
| --- | --- | --- | --- | --- |
| FastReport .NET | Fast Reports Inc. | `.frx` (plain XML, banded) | ReportTitle/PageHeader/DataBand/…/PageFooter; TextObject/Line/Shape/Picture/Barcode/CheckBox; `[Source.Column]` bindings | ✅ `Canvas.Migration.FastReport` — see [Designer-Migration-FastReport.md](Designer-Migration-FastReport.md) |
| Telerik Reporting | Progress (Telerik) | `.trdx` (XML) / `.trdp` (zip) | Sections; TextBox/HtmlTextBox/PictureBox/Shape/Barcode/Panel; StyleSheet styles; `=Fields.X` bindings | ✅ `Canvas.Migration.Telerik` — see [Designer-Migration-Telerik.md](Designer-Migration-Telerik.md) |
| Stimulsoft Reports | Stimulsoft | `.mrt` (XML `StiSerializer`; JSON is V2) | Bands + Text/Image/Line/Rect/BarCode/Panel; `{Source.Field}` bindings | ✅ `Canvas.Migration.Stimulsoft` — see [Designer-Migration-Stimulsoft.md](Designer-Migration-Stimulsoft.md) |
| JasperReports | Cloud Software Group (Jaspersoft) | `.jrxml` (XML, banded) | title/pageHeader/detail/…; staticText/textField/line/rectangle/ellipse/image/frame; named styles; `$F{}` bindings | ✅ `Canvas.Migration.JasperReports` — see [Designer-Migration-JasperReports.md](Designer-Migration-JasperReports.md) |
| Crystal Reports | SAP | `.rpt` (binary, proprietary) | Banded; **proprietary binary OLE format — no open parser, needs the Windows-only SAP SDK** | ⛔ Blocked — see [Designer-Migration-Crystal.md](Designer-Migration-Crystal.md) |
| List & Label | combit | `.lst` / `.lsr` | Banded report container | ❌ |
| ActiveReports JS | MESCIUS | JSON report model | Web/JS designer (distinct from `.rdlx`/`.rpx`) | ❌ |

## Recommended next work — provider fidelity

Most major text/XML report-designer formats now have a V1 converter. The highest-value work is no
longer adding another XML parser; it is improving layout fidelity in the converters that are already
openable in `ui-designer-v2`.

### P0 — make common business reports round-trip better

1. **DevExpress XtraReports fidelity** — finish the features most visible in real designer files:
   `GroupHeaderBand`/`GroupFooterBand` repeat semantics, `ReportFooterBand` once-at-end behaviour,
   `CanGrow`/`CanShrink`, anchoring, multi-`DetailReportBand`, non-text data bindings, and high-use
   controls such as `XRChart`, `XRGauge`, and `XRPivotGrid`.
2. **RDL / Syncfusion / SSRS table fidelity** — improve `Tablix` extraction by reading row/column-group
   headers, nested/detail groups, percentage column widths, external/database images, and multi-run
   textbox formatting.
3. **ActiveReports RPX section fidelity** — implement `GroupHeader`/`GroupFooter` repeat semantics,
   `CanGrow`/`CanShrink`, `OutputFormat`, `PageBreak`, `CrossSectionLine`, and tune against real
   designer-saved `.rpx` files.

### P1 — normalize fidelity across shipped non-core providers

4. **Shared group/repeat model** — define a common internal representation for group headers, group
   footers, detail repeats, report footers, and child/sub-detail bands, then apply it to DevExpress,
   RPX, FastReport, JasperReports, Telerik, and Stimulsoft instead of each converter flattening those
   concepts differently.
5. **Tables and complex regions** — add full cell extraction for FastReport `TableObject`, Telerik
   `Table`/`CrossTab`, and richer JasperReports `componentElement` handling. Keep charts, maps, gauges,
   and crosstabs as positioned placeholders with captions until Canvas has native equivalents.
6. **Styles and borders** — support per-side borders/pens, style inheritance, conditional styles where
   practical, and keep unsupported per-cell styling documented when Canvas has no target model.

### P2 — package and binary inputs

7. **Binary/package upload path** — add a file/binary upload route before implementing packaged
   `.trdp`, packaged `.rdlx`, or any Crystal `.rpt` workaround. The current endpoint is string/JSON
   oriented and is a poor fit for ZIP/OLE inputs.
8. **New provider candidates** — only after P0/P1: List & Label (`.lst`/`.lsr`) and ActiveReports JS
   JSON. Crystal Reports remains blocked unless a Windows + SAP SDK conversion path is introduced.
