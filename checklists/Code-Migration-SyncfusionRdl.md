# Canvas Migration: RDL (SSRS/RDLC) Report → Canvas Designer

## Goal

Convert an **RDL report** (`.rdl` / `.rdlc`) — the XML standard emitted by Microsoft SSRS/RDLC and by
the **Syncfusion / Bold Reports** designer — into a **Canvas design** that opens and is editable in
`ui-designer-v2`. One generic RDL converter serves every RDL-emitting vendor (Syncfusion now;
ActiveReports/DsReport `.rdlx` and `.rpx` are V2), mirroring the DevExpress XtraReport → Canvas
converter ([Code-Migration-DevExpressReport.md](Code-Migration-DevExpressReport.md)).

- **Input**: RDL XML — `<Report>` in a `…/reportdefinition` namespace, with `<Page>`, `<Body>`,
  `<PageHeader>`/`<PageFooter>`, and report items (`Textbox`, `Line`, `Rectangle`, `Image`,
  `Tablix`/`Table`, `Subreport`). Geometry/style are child elements; positions are CSS lengths.
- **Output**: a `DesignExportDto` (pages + `ElementDto[]` + shared elements) loaded into the visual
  designer — not a C# code string.

## Status

**V1 shipped.** Syncfusion/SSRS `.rdl`/`.rdlc` convert end-to-end and open in the designer: page
size/margins from CSS lengths, absolute item positioning, page header/footer → repeating shared
elements, textbox style + field bindings, tablix/table, rectangle flattening, embedded images.
**V2** below is the next milestone.

---

# V1 — Shipped ✅

### Scope
- [x] Input is **RDL XML** (`System.Xml.Linq`); namespace-agnostic `LocalName` matching covers the
      2005/2008/2010/2016 schemas. Vendor-neutral (Syncfusion, SSRS, RDLC).
- [x] **ActiveReports / DsReport `.rdlx`** — these are plain RDL XML using the Microsoft RDL namespaces
      (verified against MESCIUS docs), so they detect and convert through the same pipeline. Their
      barcode is the RDL-standard `<CustomReportItem>` (mapped below).

### Core conversion
- [x] **Length parser** — CSS lengths → points: `in`×72, `cm`×28.3465, `mm`×2.8346, `pt`×1,
      `px`×0.75, `pc`×12; unit-less → pt; `%` unsupported (`CANMIGRDL011`). Replaces DevExpress's
      numeric `ReportUnit` scale.
- [x] **Region model** — no bands: each item tagged `Body` | `PageHeader` | `PageFooter`. Body items
      Y-offset below the page header; footer anchored to page bottom
      (`pageHeight − bottomMargin − footerHeight`).
- [x] **Page size & margins** — from `<Page>` (2016) or top-level (2008/2010) `PageWidth`/`PageHeight`/
      `*Margin` child elements; defaults to A4 (595×842 pt) when absent.
- [x] **Page header/footer** → `DesignExportDto.SharedElements` (repeat on every page).
- [x] **Rectangle flattening** — `Rectangle` containers recurse into nested `<ReportItems>`,
      accumulating offsets so children land at absolute coordinates (depth guard → `CANMIGRDL013`).

### Architecture
- [x] Self-contained project `src/Canvas.Migration.Rdl` (refs `Canvas.Core` + `Canvas.Migration.Abstractions`;
      no Roslyn). Its own neutral `RawReport`/`RawElement` model + region-based build, adapted from the
      DevExpress converter (which is untouched). Returns a `DesignExportDto`, not a code string.
- [x] `RdlToDesignConverter.Convert(string)` → `{ DesignExportDto Design, IReadOnlyList<MigrationDiagnostic> Diagnostics }`;
      `static LooksLikeRdl(string)` for endpoint routing.

### Control mapping
| RDL item | Canvas `ElementDto.Type` | Status |
| --- | --- | --- |
| `Textbox` (Paragraphs/TextRuns or `<Value>`) | `text` / `richtext` (+ font/colour/align style) | [x] |
| `Line` | `line` (stroke width + dash style) | [x] |
| `Rectangle` | `rect` (+ child flatten) | [x] |
| `Image` (Embedded / External / Database) | `image` (data URL / preserved reference / binding) | [x] |
| `Tablix` (2016) / `Table` (2008) | `table` (CellData, header row, column widths/alignments) | [x] |
| `CustomReportItem` barcode (ActiveReports/DsReport/SSRS) | `barcode` / `qrcode` (value + symbology → type) | [x] |
| `Chart` / `CustomReportItem` chart | editable `chart` placeholder + RDL metadata (`CANMIGRDL017`) | [x] |
| `CustomReportItem` shape | `rect` / `circle` / `arrow` + style + RDL metadata (`CANMIGRDL020`) | [x] |
| native `GaugePanel` | positioned placeholder + structured gauge metadata (`CANMIGRDL021`) | [x] |
| native `Map` | positioned placeholder + structured map metadata (`CANMIGRDL022`) | [x] |
| `CustomReportItem` gauge/map/etc. | labeled placeholder + RDL metadata (`CANMIGRDL018` for gauge) | [x] |
| `Subreport` | labeled placeholder (`CANMIGRDL011`) | [x] |
| Value `=Fields!X.Value` / `=expr` | `binding` / `expression` | [x] |
| `Visibility.Hidden` | `Hidden` / inverted `VisibleExpression` | [x] |

### Delivery
- [x] Backend `POST /api/migration/report-to-design` auto-detects RDL (`<Report>` in an RDL namespace)
      → `RdlToDesignConverter`, else falls through to the DevExpress converter
      ([MigrationController.cs](../Canvas.WebApi/Controllers/MigrationController.cs)).
- [x] Frontend **"Syncfusion / RDL Reports"** entry + **Open in Designer** (loads via
      `bulkReplaceContent`) ([MigrationsPage.tsx](../ui-designer-v2/src/pages/MigrationsPage.tsx)).

### Tests (39 passing)
- [x] Page size from lengths; length-unit parsing; absolute positioning; textbox style; named colours;
      `=Fields!X.Value` binding vs complex expression; literal text; Tablix-2016 + Table-2008 → table;
      column alignments/widths; page header/footer → shared; rectangle flatten; line stroke/dash;
      embedded vs external image; subreport; namespace variants; invalid XML; `LooksLikeRdl`; A4 default.
- [x] ActiveReports `.rdlx` `<CustomReportItem>` barcode → Canvas barcode; QR symbology → qrcode;
      non-barcode custom item Chart → Canvas chart placeholder.
- [x] Native RDL `<Chart>` items, such as Syncfusion/Bold Reports chart output, map to Canvas `chart`
      placeholders with series/category/value metadata.
- [x] Comprehensive Syncfusion/Bold Reports style fixture:
      `tests/Canvas.Migration.Rdl.Tests/Fixtures/ComprehensiveSyncfusionReport.rdl`.
      Covers page header/footer, embedded images, nested rectangles, mixed units (`cm`, `mm`, `in`, `pt`),
      field bindings, complex expressions, multi-run rich text, dashed lines, a multi-column Tablix with
      hierarchy/pagination metadata, visibility rules, database image binding, barcode custom item,
      Chart/Gauge placeholders, and Subreport placeholder.
- [x] **End-to-end**: a converted RDL renders to a valid PDF through the real export pipeline
      (`DesignJsonMapper` → `ToBytes`) — in `Canvas.Export.Tests`.

### Diagnostics (implemented)
| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGRDL001` | Info | RDL report detected — N item(s) mapped |
| `CANMIGRDL002` | Info | Per-item mapping (`name (rdlType) → Canvas type`) |
| `CANMIGRDL010` | Info / Warning | `=Fields!X.Value` → binding (Info); complex `=expr` → expression (Warning) |
| `CANMIGRDL011` | Warning | Unsupported item / Subreport / unparseable Tablix / `<Code>` — skipped |
| `CANMIGRDL012` | Warning | Image not embedded; external/database reference preserved or placeholder inserted |
| `CANMIGRDL013` | Warning | Container nesting too deep — flatten stopped at guard depth |
| `CANMIGRDL014` | Warning | Tablix grouping/sorting metadata preserved; Canvas repeat/group semantics still require review |
| `CANMIGRDL015` | Warning | RDL `Visibility.Hidden` expression mapped to inverted Canvas `visibleExpression`; runtime semantics need review |
| `CANMIGRDL016` | Warning | Multi-run/styled RDL textbox imported as Canvas richtext; inline formatting needs review |
| `CANMIGRDL017` | Warning | RDL Chart imported as editable Canvas chart placeholder; series/category/value bindings need review |
| `CANMIGRDL018` | Warning | RDL Gauge metadata preserved on a positioned placeholder; Canvas has no native gauge element yet |
| `CANMIGRDL019` | Warning | RDL pagination/repeat metadata preserved; Canvas pagination behaviour needs review |
| `CANMIGRDL020` | Warning | RDL Shape custom item imported as Canvas rect/circle/arrow; geometry/rotation needs review |
| `CANMIGRDL021` | Warning | Native RDL GaugePanel metadata preserved on positioned placeholder; Canvas has no native gauge element yet |
| `CANMIGRDL022` | Warning | Native RDL Map metadata preserved on positioned placeholder; Canvas has no native map element yet |

---

# V2 — Next 🔜

**Current recommendation:** make RDL the second fidelity pass after DevExpress. The biggest user-visible
gap is `Tablix` fidelity: group headers, nested/detail groups, relative widths, and mixed text runs.

### 1. ActiveReports / DsReport `.rdlx`  *(plain-XML done)*
- [x] Plain RDL-XML `.rdlx` (the designer's native save format) detects + converts; barcode
      `<CustomReportItem>` mapped.
- [ ] **P2** If a *packaged* `.rdlx` (OPC/zip with embedded resources) is ever encountered, unzip and locate
      the `<Report>` part — needs a binary upload path (the endpoint is currently text/JSON).

### 2. GrapeCity Section Reports `.rpx`  ✅ *(shipped — separate converter)*
- [x] Banded format closer to XtraReports — handled by `Canvas.Migration.Rpx` (sections flattened like
      the DevExpress band converter). See [Code-Migration-ActiveReportsRpx.md](Code-Migration-ActiveReportsRpx.md).

### 3. Richer RDL fidelity
- [x] **P0** Preserve Tablix hierarchy metadata (`Group`, `GroupExpressions`, `SortExpressions`,
      `KeepWithGroup`) on the Canvas table style as `rdlTablixGroups`, `rdlTablixSorts`, and
      `rdlTablixKeepWithGroup`, with `CANMIGRDL014`.
- [x] **P0** Preserve pagination/repeat metadata (`PageBreak`, `PageName`, `KeepTogether`,
      `RepeatOnNewPage`, `FixedData`, and TablixMember repeat flags) on `style.rdlPagination`, with
      `CANMIGRDL019`.
- [x] **P0** `Visibility.Hidden` mapping: static `true/false` maps to `ElementDto.Hidden`; dynamic
      expressions map to inverted `VisibleExpression` via `IIF(hiddenExpr, False, True)`, with
      `CANMIGRDL015`.
- [ ] **P0** Tablix row/column-group header extraction (`<TablixColumnHierarchy>`/`<TablixMember>`) instead of
      treating the first row as header. The comprehensive fixture includes hierarchy metadata and
      acts as the regression target for this work.
- [ ] **P0** Nested Tablix / detail grouping → repeat semantics.
- [ ] `<Code>` / custom-function expression translation (blocked on Canvas `ExpressionEvaluator` being a
      stub — same limitation as DevExpress).
- [x] Native `Chart` and `CustomReportItem` Chart → Canvas `chart` placeholder with `ChartData` and RDL metadata
      (`rdlCategoryExpression`, `rdlValueExpression`, `style.rdlCustomProperties`) plus `CANMIGRDL017`.
- [x] `CustomReportItem` Shape → Canvas `rect` / `circle` / `arrow` with fill/line style, rotation metadata,
      `style.rdlShapeType`, and preserved custom properties plus `CANMIGRDL020`.
- [x] Native `GaugePanel` → positioned placeholder with `style.rdlGaugePanel` metadata (`DataSetName`, radial/linear
      gauge kind, scales, pointers, ranges, labels) plus `CANMIGRDL021`.
- [x] Native `Map` → positioned placeholder with `style.rdlMap` metadata (layers, binding field pairs,
      field definitions, rule kinds, spatial element counts, data regions, viewport, legends/titles/scales)
      plus `CANMIGRDL022`.
- [ ] **P0** Nested report items inside Tablix cells (for example Syncfusion GaugePanel-in-Tablix samples) need
      explicit extraction or richer table-cell content modeling; direct body-level `GaugePanel` is preserved today.
- [x] `CustomReportItem` Gauge → positioned placeholder with `style.rdlCustomItemType = "Gauge"` and
      custom properties preserved, plus `CANMIGRDL018`.
- [x] `CustomReportItem` Map / Sparkline + `Subreport` → labeled placeholder elements
      (kept at original position/size so the layout isn't silently holed; still `CANMIGRDL011` for generic unsupported items).
- [x] **P0** External / database image sources: external image references are preserved as image
      `Content` plus `style.rdlImageSource = "External"`; database image field expressions map to
      image binding/content placeholders plus `style.rdlImageSource = "Database"` (`CANMIGRDL012`).
- [ ] **P0** Percentage / relative lengths for Tablix columns.
- [x] **P0** Multi-run textbox per-run formatting: multiple/styled `TextRun`s import as Canvas
      `richtext` with inline HTML spans; simple single-run textboxes keep the old `text`/binding path.
- [ ] **P1** Promote Chart placeholder data from metadata-only expressions to real sample/series extraction
      once Canvas chart data-series modeling is stable enough for RDL category/value expressions.

## Assumptions
- [x] Use `Canvas.Migration.Rdl`, separate from the DevExpress converters; self-contained model + build.
- [x] Output target is Canvas design JSON (designer), not Canvas.Pdf C# code.
- [x] Default page size A4 when the report declares none.
