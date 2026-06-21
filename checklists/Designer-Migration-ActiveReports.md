# Designer Migration: ActiveReports (`.rpx` / `.rdlx`) → Canvas Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
MESCIUS (GrapeCity) **ActiveReports** family of report designers.

- **Designer:** ActiveReports / ActiveReports JS · **Manufacturer:** MESCIUS (formerly GrapeCity)
- **Two distinct file formats — different converters:**
  | Format | Designer kind | Layout | Converter | Companion doc |
  | --- | --- | --- | --- | --- |
  | `.rpx` | **Section** report | banded XML (inches) | `Canvas.Migration.Rpx` | [Code-Migration-ActiveReportsRpx.md](Code-Migration-ActiveReportsRpx.md) |
  | `.rdlx` | **Page** report | RDL XML (`reportdefinition` ns) | `Canvas.Migration.Rdl` | [Code-Migration-SyncfusionRdl.md](Code-Migration-SyncfusionRdl.md) |
- **Routing:** `.rdlx` uses the Microsoft RDL `reportdefinition` namespace, so it is detected and
  routed by `LooksLikeRdl` (the RPX detector explicitly **rejects** that namespace). `.rpx` is the
  banded section format detected by `LooksLikeRpx` (root `<Report>` + `<Sections>`).
- **Status:** ✅ **Shipped** for both paths. This doc focuses the V2 fidelity work that the **local
  `.rdlx` samples** actually exercise.

---

## Sample audit — ActiveReports `ReportSamples-master`

Local sample source: `designer-simples/ActiveReports/ReportSamples-master` (local-only test resources).

Analyzed 8 `.rdlx` files — all in the RDL-2005 `reportdefinition` namespace (ActiveReports **Page**
reports, → `Canvas.Migration.Rdl`):

- `Enterprise Reports - Marketing Plan Data.rdlx`
- `Financial Reports - BalanceSheetReport.rdlx`, `…- CashFlowReport.rdlx`,
  `…- IncomeStatementReport.rdlx`, `…- IncomeStatementReport2.rdlx`
- `Medical Reports - BloodTestReport.rdlx`, `…- PatientDiseaseSummaryReport.rdlx`
- `Telecom Reports - TelephoneBillSample.rdlx`

> **Note:** the local sample set contains **no `.rpx` Section reports** — only `.rdlx`. The `.rpx`
> converter is covered separately in [Code-Migration-ActiveReportsRpx.md](Code-Migration-ActiveReportsRpx.md);
> validating it still needs real designer-saved `.rpx` files.

Observed feature coverage:

| Feature family | Seen in samples | Converter status | Priority |
| --- | ---: | --- | --- |
| `Textbox` (value/expression, font, colour, align, padding) | 470 | supported | Done |
| RDL-2005 `<Table>` Header/Details rows | 22 tables, 20 Header / 20 Details | supported | Done |
| RDL-2005 `<Table>` **`<Footer>`** rows (totals/summary) | 14 footers | **now included** in the Canvas grid | Done (this pass) |
| **`<List>`** region (grouped repeat) + nested `<Table>` + `<Grouping>` | 1 (BloodTestReport) | **now parsed**: container + repeat metadata; children extracted | Done (this pass) |
| `Image` (embedded/base64/reference) | 9 | supported | Done |
| `Line` | 17 | supported | Done |
| Per-cell `<BorderStyle>` / `<Padding>` | broad | column-level only (Canvas has no per-cell table styling) | P1 / documented limit |
| `Tablix` / `Chart` / `CustomReportItem` / `Subreport` / `Matrix` | 0 in these samples | supported by the RDL converter (untested here) | n/a |

Key sample-driven conclusions:
- The financial reports (`BalanceSheet`/`CashFlow`/`IncomeStatement`) are the **footer-row** test bed:
  totals live in `<Table><Footer>`, which was previously dropped.
- `BloodTestReport.rdlx` is the **`<List>`** test bed: a `<List>` with `<Grouping>` wrapping a nested
  `<Table>` — previously the entire List subtree (table + grouping) was silently dropped.

## Diagnostics (this pass)

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGRDL030` | Info | RDL-2005 `<Table>` `<Footer>` rows were included in the Canvas table grid |
| `CANMIGRDL031` | Warning | RDL `<List>` region mapped to a Canvas container with repeat metadata; child items extracted as positioned elements — review grouping/repeat semantics |

## V2 checklist

- [x] **P0** RDL-2005 `<Table>` `<Footer>` rows included in the Canvas grid (financial totals no longer lost).
- [x] **P0** `<List>` region support: recurse `<ReportItems>` (nested Table parsed), `<Grouping>`/
      `<GroupExpressions>` preserved as Canvas `RepeatDto` + `style.rdlList` metadata; transparent container.
- [x] **P0** `<Grouping>` recognized alongside `<Group>` in `ParseTablixGroups` (RDL-2005 groupings no longer ignored).
- [x] **P0** Validate all 8 `.rdlx` samples convert with no dropped top-level regions (integration test).
- [x] **P1** Per-cell border/background/alignment fidelity — see *Per-cell table styling* below (v1 vertical slice).
- [ ] **P1** RDL-2005 `<TableGroups>`/group header-footer rows → repeat/section semantics (none present in samples).
- [ ] **P1** Validate the `.rpx` Section-report converter against real designer-saved `.rpx` files
      (see [Code-Migration-ActiveReportsRpx.md](Code-Migration-ActiveReportsRpx.md) for its own P0 list).
- [ ] **P2** ActiveReports **JS** JSON report model (distinct web/JS designer; not yet started).

## Implementation notes

- Converter: [`src/Canvas.Migration.Rdl/RdlToDesignConverter.cs`](../src/Canvas.Migration.Rdl/RdlToDesignConverter.cs)
  — footer concat in the 2005/2008 `<Table>` branch; `case "List"` in `ParseReportItems` + recursion;
  `MapList`/`RdlListRepeatMetadata`; `ParseTablixGroups` matches `Group`|`Grouping`.
- Tests: [`tests/Canvas.Migration.Rdl.Tests/RdlToDesignConverterTests.cs`](../tests/Canvas.Migration.Rdl.Tests/RdlToDesignConverterTests.cs)
  — footer-rows, List-with-nested-table, and an 8-sample integration check (locates `designer-simples`).

## Per-cell table styling (Canvas-model track)

Canvas tables historically carried only `CellData` / `ColumnWidths` / `ColumnAlignments` / `HeaderRow`
plus table-level `HeaderBgColor` / `ZebraColor` — so per-cell borders, backgrounds, and alignment from
real reports (ActiveReports/RDL per-side cell pens, FastReport/Telerik cell borders) were dropped. This
adds an **additive, backward-compatible** per-cell style model and wires a **v1 vertical slice**
(RDL converter → contract → frontend canvas/preview + server image preview). Unset = previous behaviour.

**Contract:** `ElementDto.CellStyles` — a sparse `CellStyleDto[]` (only styled cells listed), each with
`Row`, `Col`, `BackgroundColor`, `TextAlign`, uniform `BorderColor`/`BorderWidth`, and per-side
`BorderTop/Right/Bottom/Left` (`CellBorderSideDto { Color, Width }`) for per-side pens.
[`src/Canvas.Core/Contracts/DesignExportDto.cs`](../src/Canvas.Core/Contracts/DesignExportDto.cs)

**v1 scope (done):** borders + background + text-align; render/import only (no manual cell editor yet).

- [x] Contract: `CellStyleDto` / `CellBorderSideDto` + `ElementDto.CellStyles` (sparse, nullable).
- [x] RDL converter populates `CellStyles` from each `<TableCell>`'s `<Style>` — handles both border
      shapes (2008 `<Border>`/`<TopBorder>…`, RDL-2005 `<BorderColor>`/`<BorderWidth>`/`<BorderStyle>`
      with `<Default>`/per-side children). `ExtractCellStyle` in `RdlToDesignConverter.cs`.
- [x] Server image preview (`ImageDocumentExporter.DrawTable`) applies per-cell bg/borders/alignment.
- [x] Frontend renders per-cell styles: `SimpleCanvas` `tdStyle` + `LivePreview` `tdSt`, keyed by the
      absolute data-row index; explicit per-cell borders replace the default grid border (exporter parity).
- [x] Frontend type mirror (`ui-designer-v2/src/types.ts`: `CellStyle` / `CellBorderSide`).
- [x] Tests: RDL `CellStyles` extraction + null-when-unstyled (backward-compat); image-export render diff.

**Follow-up phases (not in v1):**
- [ ] Remaining exporters honour `CellStyles`: SVG / HTML / Word / Excel / ODT.
- [ ] Codegen (`jsonToCSharp.ts` / `CodeGenerator.ts`) emit `CellStyles` + Canvas.Pdf runtime consumes it (real PDF output).
- [ ] Other converters populate `CellStyles`: FastReport / Telerik / Jasper / DevExpress.
- [ ] Frontend manual per-cell style editor in the inspector.
- [ ] Per-cell padding + font (family/size/weight/color) — v1 carries borders/background/align only.
