# Canvas Migration: ActiveReports `.rpx` Section Report → Canvas Designer

## Goal

Convert a **GrapeCity / MESCIUS ActiveReports "Section Report"** (`.rpx`) — a **banded** XML layout —
into a **Canvas design** that opens and is editable in `ui-designer-v2`. Unlike RDL (which is
absolutely-positioned `Body`/`PageHeader`/`PageFooter`), `.rpx` is band-relative, so this converter
mirrors the **DevExpress XtraReport band-flatten** approach
([Code-Migration-DevExpressReport.md](Code-Migration-DevExpressReport.md)) rather than the RDL one
([Code-Migration-SyncfusionRdl.md](Code-Migration-SyncfusionRdl.md)).

- **Input**: `.rpx` XML — root `<Report>` with a `<Sections>` collection (`ReportHeader`, `PageHeader`,
  `GroupHeader`, `Detail`, `GroupFooter`, `PageFooter`, `ReportFooter`), each section holding
  `<Controls>` (`Label`, `TextBox`, `Line`, `Shape`, `Picture`, `Barcode`, `CheckBox`, `SubReport`).
  Positions are in inches; fonts are hyphenated attributes (`Font-FamilyName`, `Font-Size`, …).
- **Output**: a `DesignExportDto` (pages + `ElementDto[]` + shared elements) loaded into the designer.

## Status

**V1 shipped.** `.rpx` section reports convert end-to-end and open in the designer: sections flattened
to absolute page coordinates (inches → points), page header/footer → repeating shared elements,
label/textbox/line/shape/picture/barcode/checkbox controls, fonts/colours/alignment, and
`DataField` → Canvas binding.

> **Note on validation:** the converter is built against the documented ActiveReports section-report
> structure + the standard control schema (verified via MESCIUS docs) and is covered by unit + render
> tests. It parses defensively (multiple attribute fallbacks); validating against real designer-saved
> `.rpx` files is recommended before relying on it in production.

---

# V1 — Shipped ✅

### Core conversion
- [x] **Band-flatten** — section `Height`s (inches → pt) stack in canonical order (ReportHeader →
      PageHeader → GroupHeader → Detail → GroupFooter → ReportFooter → PageFooter) into absolute Y.
- [x] **Units** — all geometry in inches × 72 → points; `Line` uses `X1/Y1/X2/Y2` endpoints.
- [x] **Page size** — `<PageSettings>` `PaperKind` + `Margins` (inches) + `Orientation`; defaults to
      **Letter** (612×792 pt, the ActiveReports default) when absent.
- [x] **Page header/footer** → `DesignExportDto.SharedElements`; footer anchored to the page bottom.

### Architecture
- [x] Self-contained project `src/Canvas.Migration.Rpx` (refs `Canvas.Core` + `Canvas.Migration.Abstractions`).
      Own band-based `RawReport`/`RawBand`/`RawElement` model + flatten, adapted from the DevExpress
      report converter. Returns a `DesignExportDto`.
- [x] `RpxToDesignConverter.Convert(string)` → `{ Design, Diagnostics }`; `static LooksLikeRpx(string)`
      for endpoint routing (root `<Report>` + `<Sections>`, not an RDL namespace).

### Control mapping
| `.rpx` control | Canvas `ElementDto.Type` | Status |
| --- | --- | --- |
| `Label` / `TextBox` | `text` (+ font/colour/align) | [x] |
| `TextBox` `DataField` | `binding` (`{{field}}`) | [x] |
| `Line` (`X1/Y1/X2/Y2`) | `line` (stroke width + dash) | [x] |
| `Shape` | `rect` / `circle` (ellipse) | [x] |
| `Picture` | `image` (embedded → data URL, else placeholder `CANMIGRPX012`) | [x] |
| `Barcode` | `barcode` (DataField/Text + symbology → type) | [x] |
| `CheckBox` | `checkmark` | [x] |
| `RichTextBox` | `richtext` | [x] |
| `SubReport` / unsupported control | labeled placeholder (`CANMIGRPX011`) | [x] |

### Delivery
- [x] Backend `POST /api/migration/report-to-design` auto-detects `.rpx` (after RDL, before DevExpress)
      → `RpxToDesignConverter` ([MigrationController.cs](../Canvas.WebApi/Controllers/MigrationController.cs)).
- [x] Frontend **"ActiveReports (.rpx)"** entry + **Open in Designer**
      ([MigrationsPage.tsx](../ui-designer-v2/src/pages/MigrationsPage.tsx)).

### Tests (19 + 1 render)
- [x] Sections/Letter default; inches→points band-flatten; label style; `DataField` binding; line
      endpoints + stroke/dash; page header/footer → shared; embedded picture; barcode; shape ellipse →
      circle; checkbox; subreport; embedded script warning; PageSettings paper/margins/landscape;
      colour formats (named / R,G,B / 0xAARRGGBB / #RGB); invalid XML; `LooksLikeRpx`.
- [x] **End-to-end**: a converted `.rpx` renders to a valid PDF (`DesignJsonMapper` → `ToBytes`).

### Diagnostics
| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGRPX001` | Info | Section report detected — N section(s), M control(s) mapped |
| `CANMIGRPX002` | Info | Per-control mapping (`name (type) → Canvas type`) |
| `CANMIGRPX010` | Info | `DataField` → Canvas binding |
| `CANMIGRPX011` | Warning | Unsupported control / SubReport / embedded script — skipped or manual |
| `CANMIGRPX012` | Warning | Picture data not embeddable — placeholder inserted |

---

# V2 — Next 🔜

- [ ] **GroupHeader/GroupFooter** repeat/section semantics (currently flat page elements).
- [ ] `CanGrow`/`CanShrink` auto-sizing; `OutputFormat` → Canvas formatter.
- [ ] Per-control `.rpx` subreport inlining; `OleObject`, `PageBreak`, `CrossSectionLine`.
- [ ] Validate + tune against real designer-saved `.rpx` files (measurement units, colour/format edge cases).
- [ ] Embedded-script → no-op (currently a warning only).

## Assumptions
- [x] `.rpx` is a banded section report (distinct from RDL); self-contained band model + flatten.
- [x] Output is Canvas design JSON (designer), not Canvas.Pdf C# code.
- [x] Default page size Letter when the report declares none.
