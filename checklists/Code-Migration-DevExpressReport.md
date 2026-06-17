# Canvas Migration: DevExpress XtraReport → Canvas Designer

## Goal

Convert a **C# DevExpress XtraReport class** (Report Designer output) into a **Canvas design** that opens
and is editable in `ui-designer-v2` — replacing the dead-end `CANMIGDEVEXP020` "report export requires
manual migration" warning emitted by the code converter
([DevExpressPdfMigration.cs](../src/Canvas.Migration.DevExpressPdf/DevExpressPdfMigration.cs)).

- **Input**: a C# `XtraReport` subclass + designer code (`InitializeComponent()` configuring `XRLabel`,
  `XRLine`, `XRPictureBox`, `XRShape`, `XRTable`, … via `LocationF`/`SizeF`/`Text`/`Font`/`ForeColor`).
- **Output**: a `DesignExportDto` (pages + `ElementDto[]`) loaded into the visual designer — **not** a
  C# code string (this is distinct from the other `Code-Migration-*` providers).

## Status

**V1 shipped.** C#-source XtraReports convert end-to-end and open in the designer: band flattening, unit
conversion, real page size, repeating page headers/footers, tables, and data bindings. **V2** below is
the next, unstarted milestone.

---

# V1 — Shipped ✅

### Scope
- [x] Static-content reports (literal `Text`, fixed positions/sizes, fonts, colours).
- [x] Input is **C# source** (Roslyn). `.repx` XML input is a V2 item.

### Core conversion
- [x] **Unit conversion** — `ReportUnit` default `HundredthsOfAnInch` (×0.72); `Pixels` (×0.75, 96 DPI);
      `TenthsOfAMillimeter`; defaults to hundredths-of-inch.
- [x] **Band flattening** — accumulate each band's `HeightF` (canonical order) to turn band-relative
      `LocationF.Y` into absolute page coordinates.
- [x] **Page size** — from `PaperKind` (A4/A3/A5/Letter/Legal/Tabloid), or `PageWidth`/`PageHeight`
      (report units) when `Custom`; `Landscape` swaps dimensions; defaults to A4.
- [x] **Page header/footer** → `DesignExportDto.SharedElements` (repeat on every page); footer anchored
      to the page bottom (`pageHeight − bottomMargin − footerHeight`).

### Architecture
- [x] Isolated project `src/Canvas.Migration.DevExpressReport` (refs `Canvas.Core` + Roslyn); returns a
      `DesignExportDto`, not a code string.
- [x] `XtraReportToDesignConverter.Convert(string)` → `{ DesignExportDto Design, IReadOnlyList<MigrationDiagnostic> Diagnostics }`.
- [x] Roslyn pre-scan: locate `: XtraReport`; collect control field declarations; read
      `InitializeComponent()` assignments into per-control property bags; band membership + table
      rows/cells from `*.Controls/Rows/Cells.Add(Range)`.

### Control mapping
| XtraReport control | Canvas `ElementDto.Type` | Status |
| --- | --- | --- |
| `XRLabel` / `XRPageInfo` | `text` (+ font/colour/align style) | [x] |
| `XRLine` | `line` | [x] |
| `XRShape` / `XRPanel` | `rect` | [x] |
| `XRPictureBox` | `image` (placeholder, `CANMIGDEVREP013`) | [x] |
| `XRTable`/`XRTableRow`/`XRTableCell` | `table` (CellData from cell `.Text`) | [x] |
| `XRBarCode` | `barcode` | [x] |
| `XRRichText` | `richtext` | [x] |
| Data binding (`ExpressionBindings`/`DataBindings`) | `binding` for `[Field]`, `expression` otherwise | [x] |

### Delivery
- [x] Backend `POST /api/migration/report-to-design` → `{ design, diagnostics }`
      ([MigrationController.cs](../Canvas.WebApi/Controllers/MigrationController.cs)).
- [x] Frontend **"DevExpress Reports"** entry + **Open in Designer** (loads via `bulkReplaceContent`,
      navigates to the editor) ([MigrationsPage.tsx](../ui-designer-v2/src/pages/MigrationsPage.tsx)).

### Tests (17 passing)
- [x] Band-flattened + unit-converted coordinates; label style mapping; line/rect/image.
- [x] `ReportUnit = Pixels` scaling; PaperKind/Custom/Landscape page sizes.
- [x] XRTable rows/cells; single-field binding + complex expression; page header/footer → shared.
- [x] Unsupported control → `CANMIGDEVREP011`.
- [x] **End-to-end**: a converted report (C# and `.repx`) renders to a valid PDF through the real export
      pipeline (`DesignJsonMapper` → `ToBytes`) — in `Canvas.Export.Tests`.

### Diagnostics (implemented)
| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGDEVREP001` | Info | XtraReport + bands detected (N bands, M controls) |
| `CANMIGDEVREP002` | Info | Per-control mapping (`name (XRType) → Canvas type`) |
| `CANMIGDEVREP010` | Info / Warning | Text binding mapped — `[X]` → `binding` (Info); complex expression → `expression` (Warning) |
| `CANMIGDEVREP011` | Warning | Unsupported control skipped |
| `CANMIGDEVREP012` | Warning | Sub-report placeholder inserted, or report scripts/event handlers require manual migration |
| `CANMIGDEVREP013` | Warning | Picture data not embeddable — placeholder inserted |
| `CANMIGDEVREP014` | Warning | `DetailReportBand` / sub-detail layout flattened; data-repeat semantics require Canvas template wiring |
| `CANMIGDEVREP015` | Warning | `GroupHeaderBand` / `GroupFooterBand` layout flattened; group/sort repeat semantics require Canvas template wiring |
| `CANMIGDEVREP016` | Warning | `CanGrow` / `CanShrink` imported as Canvas text wrapping/overflow hints; dynamic band reflow requires review |
| `CANMIGDEVREP017` | Warning | DevExpress anchoring imported as metadata; responsive positioning requires review |
| `CANMIGDEVREP018` | Warning | `XRChart` imported as editable Canvas chart placeholder; `XRGauge`/`XRPivotGrid` imported as positioned placeholders |
| `CANMIGDEVREP019` | Warning | `XRShape` arrow imported as Canvas arrow; direction/head style requires visual review |
| `CANMIGDEVREP020` | Warning | DevExpress `Visible` expression preserved as Canvas `visibleExpression`; simple BookingReceipt-style cases are evaluated during PDF export |

---

# V2 — Next 🔜

Pick from these, roughly in value order:

**Current recommendation:** make DevExpress the first fidelity pass because it is a core customer path
and the gaps show up immediately in real designer files. Prioritize group/repeat semantics, once-at-end
report footers, auto-sizing/anchoring, sub-detail bands, non-text bindings, and high-use visual controls
before lower-value table styling.

### 1. `.repx` XML input  ✅ *(done)*
- [x] Parse the serialized DevExpress report XML (`.repx`) — shares the band-flatten / unit / page-size /
      mapping / binding core via a neutral `RawReport` model. `ConvertAuto` detects XML vs C#; the
      `report-to-design` endpoint accepts either. Covers page size, bands, label/line/shape/table
      controls, fonts/colours, alignment, expression bindings, and page header/footer → shared elements.

### 2. Richer controls & styling  *(partly done)*
- [x] `XRPictureBox` embedded image (`.repx` base64 `ImageSource`/`Image` → Canvas image `Content` data
      URL), replacing the `CANMIGDEVREP013` placeholder when image data is present.
- [x] `XRCheckBox` → `checkmark` with `CheckState` (from `CheckBoxState`/`Checked`).
- [x] `XRShape` ellipse → `circle`, line → `line`, otherwise `rect`.
- [x] Label `BackColor` → text `backgroundColor`; font `Underline`/`Strikeout` → `textDecoration`.
- [x] `XRControlStyle` / `StyleName` → inherited Canvas font/colour/border/padding style hints.
- [x] Table column alignments from the header-row cell `TextAlignment` → `ColumnAlignments`.
- [ ] Per-cell font/colour table styling — Canvas tables only support column-level alignment +
      header/zebra colours, so arbitrary per-cell styling can't round-trip (low value).
- [x] **P0** More controls: `XRChart` → editable Canvas `chart`; `XRGauge`/`XRPivotGrid` →
      positioned placeholders with `CANMIGDEVREP018` diagnostics instead of layout holes.
- [x] `XRLine`/`XRShape` `LineWidth` → `strokeWidth`/`borderWidth`; `XRLine` `LineStyle` → `dashStyle`.
- [x] **P1** `XRShape` arrow kinds; per-side `.Borders` selection.

### 3. Data & layout fidelity
- [~] ~~Translate DevExpress expression syntax to the Canvas expression DSL.~~ **Not worth doing** —
      Canvas's `ExpressionEvaluator` ([src/Canvas.Core/Primitives/ExpressionEvaluator.cs](../src/Canvas.Core/Primitives/ExpressionEvaluator.cs))
      is a stub (bare-identifier substitution + `==`/`!=` only; no arithmetic/functions), so there's no
      richer target to translate into. Single-field `[X]` → `binding` (done); other expressions are
      preserved verbatim on `expression` with a warning.
- [x] Detect sub-reports (`XRSubreport`) and insert positioned placeholders; report scripts/event handlers → `CANMIGDEVREP012`.
- [x] **P0** Grouping/sorting bands (`GroupHeaderBand`/`GroupFooterBand`) — group/footer layout is
      imported and stacked in band order; C# `GroupFields`/`SortFields` and `.repx` `<GroupFields>`/
      `<SortFields>` are captured in `CANMIGDEVREP015` diagnostics. True repeat/sort execution still
      requires Canvas template wiring.
- [x] **P0** `AnchorVertical`/`AnchorHorizontal` and `CanGrow`/`CanShrink` auto-sizing: imported as
      Canvas style hints (`whiteSpace`, `overflow`, `verticalAlign`) plus DevExpress metadata, with
      `CANMIGDEVREP016/017` diagnostics for dynamic reflow/anchoring review.
- [x] **P0** `ReportFooterBand` once-at-end semantics: report-footer elements are imported as
      `PageScope = "last"` so export renders them only on the final page while single-page designs
      still show them.

### 4. Polish
- [x] Emit `CANMIGDEVREP002` per mapped control (traceability beyond the `001` summary).
- [x] Nested controls inside an `XRPanel` are flattened to absolute positions (container chain walked
      up to the owning band, accumulating panel offsets) — both C# and `.repx`.
- [x] **P0** Map non-text data bindings instead of the generic warning: `XRBarCode` text/value
      bindings now update `BarcodeValue`, and `XRPictureBox` image bindings now update image `Content`.
- [x] **P0** Preserve `Visible` expression bindings as `visibleExpression` metadata with
      `CANMIGDEVREP020` diagnostics; PDF export evaluates simple cases such as `Len([X]) > 0`,
      `[A].[B] == 'Value'`, `[Collection].Count > 0`, and `IIF(condition, True, False)`.
- [x] **P0** Multi-`DetailReportBand` (sub-detail) handling: nested child bands are parsed from C#
      `detailReport.Bands.AddRange(...)` and `.repx` nested `<Bands>`, flattened below their parent
      `DetailReportBand`, with `CANMIGDEVREP014` warning for repeat semantics.

## Assumptions
- [x] Use `Canvas.Migration.DevExpressReport`, separate from `Canvas.Migration.DevExpressPdf`.
- [x] Output target is Canvas design JSON (designer), not Canvas.Pdf C# code.
- [x] Default page size A4 when the report declares none.
