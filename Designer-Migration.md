# Report Designer Migration Roadmap

Tracks which report-designer / report-file formats can be opened as **editable Canvas designs** via
`POST /api/migration/report-to-design`. Prioritized to start with vendors whose PDF-code migration we
already ship (`Canvas.Migration.*Pdf`); the report-designer vendors among them are the natural
report-to-design targets.

Legend — **Done?**: ✅ shipped · 🔜 recommended next · ❌ not started · — out of scope (no report designer)

## Priority 1 — Vendors already in our migration set

| Designer | Manufacturer | Tech (format) | Features | Done? |
| --- | --- | --- | --- | --- |
| DevExpress XtraReports | DevExpress | `.repx` XML + C# (banded) | Bands (Report/Page/Group/Detail); XRLabel/Table/Line/Shape/PictureBox/BarCode/RichText/CheckBox; expression bindings | ✅ `Canvas.Migration.DevExpressReport` |
| Syncfusion Report Designer / Bold Reports | Syncfusion | `.rdl` / `.rdlc` (RDL XML, region) | Body + Page header/footer; Textbox/Tablix/Table/Line/Rectangle/Image; `=Fields!` bindings | ✅ `Canvas.Migration.Rdl` |
| ActiveReports — Page / RDLX reports | MESCIUS (GrapeCity) | `.rdlx` (RDL XML) | RDL-family; report items + `CustomReportItem` barcodes | ✅ `Canvas.Migration.Rdl` |
| ActiveReports — Section reports | MESCIUS (GrapeCity) | `.rpx` (XML, banded) | Section bands; Label/TextBox/Line/Shape/Picture/Barcode/CheckBox/RichText/SubReport; `DataField` bindings | ✅ `Canvas.Migration.Rpx` |
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
| FastReport .NET | Fast Reports Inc. | `.frx` (plain XML, banded) | ReportTitle/PageHeader/DataBand/…/PageFooter; TextObject/Line/Shape/Picture/Table/Barcode; `[Data.Field]` expressions; **public real `.frx` samples on GitHub** | 🔜 Recommended next |
| Telerik Reporting | Progress (Telerik) | `.trdx` (XML) / `.trdp` (zip) | Sections; TextBox/Table/Crosstab/Chart/Barcode; bindings | ❌ |
| Stimulsoft Reports | Stimulsoft | `.mrt` (XML or JSON, banded) | Bands + components; tables/charts/barcodes | ❌ |
| JasperReports | Cloud Software Group (Jaspersoft) | `.jrxml` (XML, banded) | title/pageHeader/detail/…; staticText/textField/image/table/chart | ❌ |
| Crystal Reports | SAP | `.rpt` (binary, proprietary) | Banded; binary format — hard to parse, low priority | ❌ |
| List & Label | combit | `.lst` / `.lsr` | Banded report container | ❌ |
| ActiveReports JS | MESCIUS | JSON report model | Web/JS designer (distinct from `.rdlx`/`.rpx`) | ❌ |

## Recommended next: FastReport .NET (`.frx`)

- **Plain namespace-free XML, banded** → reuses the `Canvas.Migration.Rpx` band-flatten model and the
  `.repx` "Family, 9pt, style=Bold" font-string parser; routes after RDL/RPX in `MigrationController`
  (detect root `<Report>` with a `<ReportPage>` child, no `reportdefinition` namespace, no `<Sections>`).
- **Real-sample validation**: the open-source FastReports/FastReport repo ships a `Demos/Reports/*.frx`
  corpus — lets us validate against genuine designer output, closing the gap noted for `.rpx`.
