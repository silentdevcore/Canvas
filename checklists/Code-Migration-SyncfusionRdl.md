# PXA Migration: RDL (SSRS/RDLC) Report → PXA Designer

## Goal

Convert an **RDL report** (`.rdl` / `.rdlc`) — the XML standard emitted by Microsoft SSRS/RDLC and by
the **Syncfusion / Bold Reports** designer — into a **PXA design** that opens and is editable in
`ui-designer-v2`. One generic RDL converter serves every RDL-emitting vendor (Syncfusion, SSRS/RDLC,
ActiveReports/DsReport plain-XML `.rdlx`), mirroring the DevExpress XtraReport → PXA converter
([Code-Migration-DevExpressReport.md](Code-Migration-DevExpressReport.md)). GrapeCity Section Reports
`.rpx` are covered by the separate RPX converter.

- **Input**: RDL XML — `<Report>` in a `…/reportdefinition` namespace, with `<Page>`, `<Body>`,
  `<PageHeader>`/`<PageFooter>`, and report items (`Textbox`, `Line`, `Rectangle`, `Image`,
  `Tablix`/`Table`, `Subreport`). Geometry/style are child elements; positions are CSS lengths.
- **Output**: a `DesignExportDto` (pages + `ElementDto[]` + shared elements) loaded into the visual
  designer — not a C# code string.

## Status

**V1 shipped.** Syncfusion/SSRS `.rdl`/`.rdlc` convert end-to-end and open in the designer: page
size/margins from CSS lengths, absolute item positioning, page header/footer → repeating shared
elements, textbox style + field bindings, tablix/table, rectangle flattening, embedded images.
The remaining roadmap is limited to optional P2/native-product features and expression-engine work.

**Corpus validation (2026-06-20):** all **145** real-world Syncfusion / Bold Reports `.rdl` files
under `designer-simples/syncfustion` convert with **0 throws, 0 undetected, 0 empty designs**. The
only unsupported-control placeholders are `Map` (no PXA equivalent) and PDF/HTML custom items;
every other element maps or preserves review metadata. Locked in by the `SyncfusionRdlSamplesTests`
smoke test. Conclusion: the converter is robust against the available corpus — remaining fidelity
gaps (per-cell table styling, native maps/charts) require **PXA model** changes, not converter work.

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
- [x] Self-contained project `src/PXA.Migration.Rdl` (refs `PXA.Core` + `PXA.Migration.Abstractions`;
      no Roslyn). Its own neutral `RawReport`/`RawElement` model + region-based build, adapted from the
      DevExpress converter (which is untouched). Returns a `DesignExportDto`, not a code string.
- [x] `RdlToDesignConverter.Convert(string)` → `{ DesignExportDto Design, IReadOnlyList<MigrationDiagnostic> Diagnostics }`;
      `static LooksLikeRdl(string)` for endpoint routing.

### Control mapping
| RDL item | PXA `ElementDto.Type` | Status |
| --- | --- | --- |
| `Textbox` (Paragraphs/TextRuns or `<Value>`) | `text` / `richtext` (+ font/colour/align style) | [x] |
| `Line` | `line` (stroke width + dash style) | [x] |
| `Rectangle` | `rect` (+ child flatten) | [x] |
| `Image` (Embedded / External / Database) | `image` (data URL / preserved reference / binding) | [x] |
| `Tablix` (2016) / `Table` (2008) | `table` (CellData, header row, column widths/alignments) | [x] |
| `CustomReportItem` barcode (ActiveReports/DsReport/SSRS) | `barcode` / `qrcode` (value + symbology → type) | [x] |
| `Chart` / `CustomReportItem` chart | editable `chart` placeholder + RDL metadata (`CANMIGRDL017`) | [x] |
| `CustomReportItem` shape | `rect` / `circle` / `arrow` + style + RDL metadata (`CANMIGRDL020`) | [x] |
| `CustomReportItem` html/pdf document | positioned placeholder + preserved/truncated document metadata (`CANMIGRDL027`) | [x] |
| `CustomReportItem` ESignature/PDFSignature | `signature` placeholder + preserved/truncated signing metadata (`CANMIGRDL028`) | [x] |
| `CustomReportItem` sparkline | compact editable `chart` with RDL category/value/data metadata (`CANMIGRDL017`) | [x] |
| `CustomReportItem` map | positioned placeholder + structured map custom-property metadata (`CANMIGRDL022`) | [x] |
| native `GaugePanel` | positioned placeholder + structured gauge metadata (`CANMIGRDL021`) | [x] |
| native `Map` | positioned placeholder + structured map metadata (`CANMIGRDL022`) | [x] |
| `Subreport` | labeled placeholder + report/parameter metadata (`CANMIGRDL011`) | [x] |
| Value `=Fields!X.Value` / `=expr` | `binding` / `expression` | [x] |
| `Visibility.Hidden` | `Hidden` / inverted `VisibleExpression` | [x] |
| `ReportParameters` / `ReportParametersLayout` | `PageSettings.CustomProperties` JSON metadata (`CANMIGRDL024`) | [x] |
| `Filters` | element/table/map metadata (`CANMIGRDL025`) | [x] |
| `ActionInfo` / `Drillthrough` / `Bookmark` / `DocumentMapLabel` / `ToggleItem` | element/table navigation metadata (`CANMIGRDL026`) | [x] |
| `TablixRowHierarchy` / `TablixColumnHierarchy` headers | table header-row detection + hierarchy/header metadata (`CANMIGRDL029`) | [x] |

### Delivery
- [x] Backend `POST /api/migration/report-to-design` auto-detects RDL (`<Report>` in an RDL namespace)
      → `RdlToDesignConverter`, else falls through to the DevExpress converter
      ([MigrationController.cs](../PXA.WebApi/Controllers/MigrationController.cs)).
- [x] Frontend **"Syncfusion / RDL Reports"** entry + **Open in Designer** (loads via
      `bulkReplaceContent`) ([MigrationsPage.tsx](../ui-designer-v2/src/pages/MigrationsPage.tsx)).

### Tests (60 passing)
- [x] Page size from lengths; length-unit parsing; absolute positioning; textbox style; named colours;
      `=Fields!X.Value` binding vs complex expression; literal text; Tablix-2016 + Table-2008 → table;
      column alignments/widths; page header/footer → shared; rectangle flatten; line stroke/dash;
      embedded vs external image; subreport; namespace variants; invalid XML; `LooksLikeRdl`; A4 default.
- [x] ActiveReports `.rdlx` `<CustomReportItem>` barcode → PXA barcode; QR symbology → qrcode;
      non-barcode custom item Chart → PXA chart placeholder.
- [x] Native RDL `<Chart>` items, such as Syncfusion/Bold Reports chart output, map to PXA `chart`
      placeholders with series/category/value metadata.
- [x] Comprehensive Syncfusion/Bold Reports style fixture:
      `tests/PXA.Migration.Rdl.Tests/Fixtures/ComprehensiveSyncfusionReport.rdl`.
      Covers page header/footer, embedded images, nested rectangles, mixed units (`cm`, `mm`, `in`, `pt`),
      field bindings, complex expressions, multi-run rich text, dashed lines, a multi-column Tablix with
      hierarchy/pagination metadata, visibility rules, database image binding, barcode custom item,
      Chart/Gauge/Map/Sparkline metadata, and Subreport metadata placeholders.
- [x] **End-to-end**: a converted RDL renders to a valid PDF through the real export pipeline
      (`DesignJsonMapper` → `ToBytes`) — in `PXA.Export.Tests`.

### Diagnostics (implemented)
| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGRDL001` | Info | RDL report detected — N item(s) mapped |
| `CANMIGRDL002` | Info | Per-item mapping (`name (rdlType) → PXA type`) |
| `CANMIGRDL010` | Info / Warning | `=Fields!X.Value` → binding (Info); complex `=expr` → expression (Warning) |
| `CANMIGRDL011` | Warning | Unsupported item / Subreport / unparseable Tablix / `<Code>` — manual review needed; placeholders/metadata are kept where possible |
| `CANMIGRDL012` | Warning | Image not embedded; external/database reference preserved or placeholder inserted |
| `CANMIGRDL013` | Warning | Container nesting too deep — flatten stopped at guard depth |
| `CANMIGRDL014` | Warning | Tablix grouping/sorting metadata preserved; PXA repeat/group semantics still require review |
| `CANMIGRDL015` | Warning | RDL `Visibility.Hidden` expression mapped to inverted PXA `visibleExpression`; runtime semantics need review |
| `CANMIGRDL016` | Warning | Multi-run/styled RDL textbox imported as PXA richtext; inline formatting needs review |
| `CANMIGRDL017` | Warning | RDL Chart imported as editable PXA chart placeholder; series/category/value bindings need review |
| `CANMIGRDL018` | Warning | RDL Gauge metadata preserved on a positioned placeholder; PXA has no native gauge element yet |
| `CANMIGRDL019` | Warning | RDL pagination/repeat metadata preserved; PXA pagination behaviour needs review |
| `CANMIGRDL020` | Warning | RDL Shape custom item imported as PXA rect/circle/arrow; geometry/rotation needs review |
| `CANMIGRDL021` | Warning | Native RDL GaugePanel metadata preserved on positioned placeholder; PXA has no native gauge element yet |
| `CANMIGRDL022` | Warning | Native/custom RDL Map metadata preserved on positioned placeholder; PXA has no native map element yet |
| `CANMIGRDL023` | Warning | Non-text Tablix cell item extracted as separate positioned PXA element; repeat semantics need review |
| `CANMIGRDL024` | Warning | RDL report parameters preserved in `PageSettings.CustomProperties`; PXA has no native report-parameter UI yet |
| `CANMIGRDL025` | Warning | RDL filters preserved as metadata; PXA does not evaluate report filters yet |
| `CANMIGRDL026` | Warning | RDL navigation/action metadata preserved; PXA does not execute drillthrough/bookmark/drilldown behaviour yet |
| `CANMIGRDL027` | Warning | RDL HTML/PDF document custom item preserved as positioned placeholder; PXA has no native embedded document item yet |
| `CANMIGRDL028` | Warning | RDL signature custom item mapped to PXA signature placeholder; signing/certificate semantics need review |
| `CANMIGRDL029` | Warning | RDL Tablix row/column hierarchy headers preserved as metadata; PXA has limited native matrix/group header rendering |
| `CANMIGRDL032` | Warning | RDL-2005 TableGroups header/footer repeat metadata preserved on PXA table style; runtime group-section rendering needs review |

---

# Remaining Roadmap

**Current recommendation:** treat the RDL/Syncfusion converter as functionally complete for P0/P1 parity.
The remaining items are optional native PXA capabilities or blocked expression-engine work.

### 1. ActiveReports / DsReport `.rdlx`  *(plain-XML done)*
- [x] Plain RDL-XML `.rdlx` (the designer's native save format) detects + converts; barcode
      `<CustomReportItem>` mapped.
- [x] **P2** Packaged `.rdlx` (OPC/zip): `report-to-design` now accepts a base64 binary upload
      (`sourceBase64`); `ReportPackageExtractor` unzips it and picks the inner RDL `<Report>` part (first
      entry `LooksLikeRdl` recognizes), then the existing converter runs. *(Backend path done + tested;
      migration-UI binary file picker is the remaining frontend piece.)*

### 2. GrapeCity Section Reports `.rpx`  ✅ *(shipped — separate converter)*
- [x] Banded format closer to XtraReports — handled by `PXA.Migration.Rpx` (sections flattened like
      the DevExpress band converter). See [Code-Migration-ActiveReportsRpx.md](Code-Migration-ActiveReportsRpx.md).

### 3. Richer RDL fidelity
- [x] **P0** Preserve Tablix hierarchy metadata (`Group`, `GroupExpressions`, `SortExpressions`,
      `KeepWithGroup`) on the PXA table style as `rdlTablixGroups`, `rdlTablixSorts`, and
      `rdlTablixKeepWithGroup`, with `CANMIGRDL014`.
- [x] **P0** Preserve pagination/repeat metadata (`PageBreak`, `PageName`, `KeepTogether`,
      `RepeatOnNewPage`, `FixedData`, and TablixMember repeat flags) on `style.rdlPagination`, with
      `CANMIGRDL019`.
- [x] **P0** `Visibility.Hidden` mapping: static `true/false` maps to `ElementDto.Hidden`; dynamic
      expressions map to inverted `VisibleExpression` via `IIF(hiddenExpr, False, True)`, with
      `CANMIGRDL015`.
- [x] **P0** Tablix row/column-group header extraction (`<TablixColumnHierarchy>`/`<TablixMember>`) now preserves
      row/column hierarchy members, `TablixHeader` text/size, group/sort expressions, and repeat/keep flags as
      `style.rdlTablixRowHierarchy` / `style.rdlTablixColumnHierarchy`; `HeaderRow` is derived from static
      row-hierarchy headers when present instead of blindly treating the first row as a header (`CANMIGRDL029`).
- [x] **P1** Designer/Preview rendering for preserved Matrix/group headers: `rdlTablixRowHierarchy` and
      `rdlTablixColumnHierarchy` header/group labels render as additional PXA table header rows.
- [x] **P1** Export rendering for multi-level Matrix/group headers in HTML, Word, and Excel from preserved
      Tablix hierarchy metadata.
- [x] **P1** Extend Matrix/group-header rendering to remaining visual export paths: native PDF mapping,
      SVG, and image exports now prepend preserved RDL hierarchy labels as table header rows.
- [x] **P0** Nested Tablix/Table inside Tablix cells are extracted as positioned PXA `table` elements with
      `rdlParentTablix`, cell row/column, row/column span, and `rdlParentTablixRepeatScope` metadata that preserves
      the parent row/column hierarchy and group expressions (`CANMIGRDL023`).
- [x] **P1** Extracted nested Tablix/detail rows now map preserved RDL repeat scope into PXA repeat metadata:
      `ElementDto.Repeat` plus `style.rdlRepeat` with parent, row/column, data path, aliases, dataset/group metadata.
- [x] **P1** Runtime execution for extracted nested Tablix/detail rows in preview/export: backend export planning
      expands `ElementDto.Repeat` from JSON payload custom properties, and frontend preview accepts the same
      `repeat.dataPath` contract with token substitution.
- [x] Compound-expression field normalization: multi-field/function/operator expressions preserve the
      original on `element.Expression` + `style.rdlExpression` and render a PXA template with every
      `Fields!X[.Value]` normalized to `{{X}}` (consistent with the Telerik/Stimulsoft converters).
- [x] Standard expressions are now **executable** in the designer/preview: the compound-expression branch
      emits a translated PXA-grammar `Expression` via `ExpressionTranslator.TranslateRdl` (raw kept on
      `style.rdlExpression`). See [Migration_RDL+DevExpress.md](Migration_RDL+DevExpress.md).
- [ ] `<Code>` / custom-function *evaluation* — arbitrary embedded VB, **not runnable** in PXA (kept
      preserved). Note the old "blocked on `ExpressionEvaluator` being a stub" reason is stale: the
      evaluator now exists. Standard single-row expressions are translated + executed; **dataset aggregates
      (`Sum`/`Avg`/`Count`/`Min`/`Max`/`First`/`Last`) translate + execute, and group-scoped aggregates
      inside a List/group region resolve per group** — see
      [Migration_Dataset-Aggregates.md](Migration_Dataset-Aggregates.md) and
      [Group-Scoped-Aggregates.md](Group-Scoped-Aggregates.md).
- [x] Native `Chart` and `CustomReportItem` Chart → PXA `chart` placeholder with `ChartData` and RDL metadata
      (`rdlCategoryExpression`, `rdlValueExpression`, `style.rdlCustomProperties`) plus `CANMIGRDL017`.
- [x] Native `Chart` fidelity pass: multiple series, `DataSetName`, title, original RDL series type, X/Y/Size
      expressions, and advanced chart types (`Area`, `Scatter`, `Range`, `Polar`, `Shape` families) are preserved
      in `chartData.rdlSeries` while mapped to the nearest PXA `bar`/`line`/`pie` rendering type.
- [x] `CustomReportItem` Shape → PXA `rect` / `circle` / `arrow` with fill/line style, rotation metadata,
      `style.rdlShapeType`, and preserved custom properties plus `CANMIGRDL020`.
- [x] `CustomReportItem` HTML/PDF document (`htmldocument`, `pdfdocument`) → positioned PXA placeholder with
      document source/sizing metadata and truncated large embedded values plus `CANMIGRDL027`.
- [x] `CustomReportItem` signature (`ESignature`, `PDFSignature`) → PXA `signature` placeholder with
      electronic/PDF signature kind, certificate metadata, and truncated large signature payloads plus `CANMIGRDL028`.
- [x] `CustomReportItem` Sparkline → compact editable PXA `chart` with `line`/nearest chart type,
      `chartData.rdlSparkline`, and preserved category/value/dataset metadata plus `CANMIGRDL017`.
- [x] `CustomReportItem` Map → positioned placeholder with structured `style.rdlMap` metadata
      (`MapType`, `DataSetName`, binding field pairs, value/label expressions, viewport hints) and preserved
      custom properties plus `CANMIGRDL022`.
- [x] Native `GaugePanel` → positioned placeholder with `style.rdlGaugePanel` metadata (`DataSetName`, radial/linear
      gauge kind, scales, pointers, ranges, labels) plus `CANMIGRDL021`.
- [x] Native `Map` → positioned placeholder with `style.rdlMap` metadata (layers, binding field pairs,
      field definitions, rule kinds, spatial element counts, data regions, viewport, legends/titles/scales)
      plus `CANMIGRDL022`.
- [x] **P0** Nested non-text report items inside Tablix/Table cells (for example Syncfusion GaugePanel-in-Tablix
      samples) are extracted as separate positioned PXA elements with `style.rdlParentTablix`,
      `style.rdlParentTablixRow`, `style.rdlParentTablixColumn`, and table-level `style.rdlExtractedCellItems`
      plus `CANMIGRDL023`.
- [x] **P1** Richer Table cell-content modeling: parent tables now preserve structured
      `style.rdlExtractedCellItemLayouts` for extracted nested cell items, including row/column, spans,
      table-relative bounds, visibility, repeat scope, and repeat metadata.
- [ ] **P2** PXA-native compound table-cell rendering for extracted nested items, instead of positioned
      overlay elements, if/when the PXA table model supports mixed cell content.
- [x] `ReportParameters` / `ReportParametersLayout` → `PageSettings.CustomProperties` JSON metadata
      (`rdlReportParameters`, `rdlReportParametersLayout`) plus `CANMIGRDL024`.
- [x] Element/DataRegion/Tablix group `Filters` → `style.rdlFilters`, `style.rdlTablixGroupFilters`, or nested
      map data-region metadata plus `CANMIGRDL025`.
- [x] **P1** Report-parameter defaults and filter evaluation in runtime exports: `rdlReportParameters`
      default values become substitution/evaluation properties, and `style.rdlFilters` filters repeated rows
      against payload fields and parameter values.
- [ ] **P2** Native report-parameter UI/editor controls; runtime defaults and repeat-filter evaluation exist,
      but users still need a PXA-native parameter input surface.
- [x] Element `ActionInfo` (`Hyperlink`, `BookmarkLink`, `Drillthrough` with parameters), `Bookmark`,
      `DocumentMapLabel`, and Tablix `ToggleItem`/group document-map metadata → `style.rdlNavigation` or
      `style.rdlTablixNavigation` plus `CANMIGRDL026`.
- [x] **P1** Execute/preview navigation semantics in PXA where possible: RDL Hyperlink, BookmarkLink, and
      simple Drillthrough actions map to PXA `link` elements with `Href`/`LinkTarget`, while full
      `style.rdlNavigation` metadata remains preserved.
- [x] `CustomReportItem` Gauge → positioned placeholder with field/range preview, structured `style.rdlGauge`
      metadata (`Value`, `MinimumValue`, `MaximumValue`, `TargetValue`, `DataSetName`) and preserved custom
      properties, plus `CANMIGRDL018`.
- [x] `Subreport` → labeled placeholder with structured `style.rdlSubreport` metadata (`ReportName`, parameters)
      and preserved pagination/repeat metadata; still manual because PXA has no native subreport composition.
- [x] **P0** External / database image sources: external image references are preserved as image
      `Content` plus `style.rdlImageSource = "External"`; database image field expressions map to
      image binding/content placeholders plus `style.rdlImageSource = "Database"` (`CANMIGRDL012`).
- [x] **P0** Percentage / relative lengths for Tablix columns: `%` widths are resolved against the Tablix/Table
      width, and simple relative weights such as `*`, `1*`, `2*` share remaining width after absolute columns.
- [x] **P0** Multi-run textbox per-run formatting: multiple/styled `TextRun`s import as PXA
      `richtext` with inline HTML spans; simple single-run textboxes keep the old `text`/binding path.
- [x] **P1** Promote preserved RDL chart expressions to sample extraction: backend layout planning fills PXA
      chart `labels`/`datasets` from `rdlDataSetName` payload rows for preserved category/value field expressions,
      while keeping the original RDL chart metadata for review.
- [x] **P1** RDL-2005 `<TableGroups>` metadata: group name, group/sort expressions, header/footer row
      counts, and nested groups are preserved as `style.rdlTableGroups` for ActiveReports/DsReport-style
      RDL-2005 tables.

## Assumptions
- [x] Use `PXA.Migration.Rdl`, separate from the DevExpress converters; self-contained model + build.
- [x] Output target is PXA design JSON (designer), not PXA.Pdf C# code.
- [x] Default page size A4 when the report declares none.
