# Designer Migration: FastReport .NET (`.frx`) → PXA Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
FastReport `.frx` → PXA `DesignExportDto` converter (format research, schema, control mapping,
diagnostics, status).

- **Designer:** FastReport .NET · **Manufacturer:** Fast Reports Inc.
- **Format:** `.frx` — plain, namespace-free **XML**, **banded** report.
- **Status:** ✅ **Shipped** (`PXA.Migration.Report.Designer.FastReport`) — band-flatten mirroring `PXA.Migration.Report.Designer.Rpx`;
  schema confirmed against real `FastReports/FastReport` `Demos/Reports/*.frx`. 14 unit tests + render test.

---

## Why FastReport next

- Plain XML, banded → reuses the `PXA.Migration.Report.Designer.Rpx` band-flatten model and the
  `PXA.Migration.Report.Designer.DevExpress` `.repx` font-string parser (`"Tahoma, 9pt, style=Bold"`).
- **Open-source corpus of real `.frx` files** (`FastReports/FastReport` → `Demos/Reports/*.frx`) to
  validate against genuine designer output — closes the "no real sample" gap noted for `.rpx`.

## Format overview (to confirm against real `Demos/Reports/*.frx`)

```xml
<Report ScriptLanguage="CSharp" ...>
  <Dictionary>...</Dictionary>                     <!-- data sources/connections (ignored) -->
  <ReportPage Name="Page1" PaperWidth="210" PaperHeight="297"
              LeftMargin="10" RightMargin="10" TopMargin="10" BottomMargin="10">
    <ReportTitleBand Name="ReportTitle1" Top="0" Width="718.2" Height="37.8">
      <TextObject Name="Text1" Left="0" Top="0" Width="718.2" Height="37.8"
                  Text="Employees" HorzAlign="Center" Font="Tahoma, 14pt, style=Bold"/>
    </ReportTitleBand>
    <PageHeaderBand  Name="PageHeader1"  Top="41.8"  Width="718.2" Height="18.9">...</PageHeaderBand>
    <DataBand        Name="Data1"        Top="64.7"  Width="718.2" Height="18.9" DataSource="Employees">
      <TextObject Name="Text3" Left="9.45" Top="0" Width="103.95" Height="18.9"
                  Text="[Employees.FirstName]" VertAlign="Center" Font="Tahoma, 9pt"/>
    </DataBand>
    <PageFooterBand  Name="PageFooter1"  Top="87.6"  Width="718.2" Height="18.9">...</PageFooterBand>
  </ReportPage>
</Report>
```

- **Root** `<Report>` (no namespace), one or more `<ReportPage>` children.
- **Page size** on `ReportPage`: `PaperWidth`/`PaperHeight` + `*Margin` in **millimetres**; `Landscape`.
- **Bands** are `ReportPage` children, each with `Name`, `Top`, `Width`, `Height` in **pixels** (96 dpi).
  Band names end in `Band` (e.g. `ReportTitleBand`, `PageHeaderBand`, `DataBand`, `PageFooterBand`,
  `GroupHeaderBand`, `GroupFooterBand`, `ColumnHeaderBand`, `ReportSummaryBand`, `ChildBand`).
- **Objects** are band children: `TextObject`, `LineObject`, `ShapeObject`, `PictureObject`,
  `TableObject`, `BarcodeObject`, `CheckBoxObject`, `RichObject`, `SubreportObject`. Geometry
  `Left/Top/Width/Height` in **pixels** (relative to the band).

## Units & coordinates

- Objects: pixels → points `× 72/96` (`× 0.75`). Paper/margins: mm → points `× 72/25.4`.
- **Absolute Y** = `band.Top + object.Top` (both px → pt). PageHeader/PageFooter bands → SharedElements;
  footer anchored to page bottom. (Same flatten as `PXA.Migration.Report.Designer.Rpx`.)

## Detection / routing

Root is `<Report>` — shared with RDL and RPX, so order matters in `MigrationController`:
**RDL → RPX → FRX → DevExpress**. `LooksLikeFrx`: root `<Report>`, has a `<ReportPage>` descendant,
namespace does **not** contain `reportdefinition`, and no `<Sections>` (RPX). Accept a leading `<`.

## Control mapping (planned)

| `.frx` object | PXA `ElementDto.Type` | Notes |
| --- | --- | --- |
| `TextObject` | `text` | `Text`, `Font`, `HorzAlign`, fill/text colour; `[Source.Col]` → binding `{{Col}}` |
| `LineObject` | `line` | `Diagonal` false = horizontal/vertical; `Border`/line width + style |
| `ShapeObject` | `rect` / `circle` | `Shape="Ellipse"` → circle; `RoundRectangle`/`Rectangle` → rect |
| `PictureObject` | `image` | embedded `Image`/`ImageData` base64 → data URL, else placeholder |
| `TableObject` | `table` | rows/columns/cells → `CellData` grid |
| `BarcodeObject` | `barcode` | `Barcode` symbology → type; `Text`/`DataColumn` → value |
| `CheckBoxObject` | `checkmark` | `Checked` |
| `RichObject` | `richtext` | RTF/`Text` |
| `SubreportObject` / unknown | labeled placeholder | `CANMIGFRX011` |

**Bindings:** `TextObject.Text` of form `[DataSource.Column]` (or `[Column]`) → PXA binding; other
`[...]`/script expressions → `Expression` + warning. Literal text → plain content.

**To confirm against real samples:** exact fill/border attribute names (`Fill.Color`/`<Fill>`,
`Border.Lines`/`Border.Color`/`Border.Width`), colour encoding (named vs ARGB int vs `R,G,B`), the
`Band` suffix on band element names, and the px-vs-mm unit split.

## Diagnostics (planned, mirroring `CANMIGRDL/RPX`)

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGFRX001` | Info | FastReport `.frx` detected — N band(s), M object(s) mapped |
| `CANMIGFRX002` | Info | Per-object mapping (`name (type) → PXA type`) |
| `CANMIGFRX010` | Info / Warning | `[Source.Col]` → binding (Info); complex expression → expression (Warning) |
| `CANMIGFRX011` | Warning | Unsupported object / Subreport / script — labeled placeholder |
| `CANMIGFRX012` | Warning | Picture data not embeddable — placeholder inserted |
| `CANMIGFRX013` | Info | `TableObject` mapped to a PXA table (N rows × M columns; ColSpan padded) |
| `CANMIGFRX014` | Warning | `GroupHeader`/`GroupFooter` object mapped to PXA repeat metadata; group runtime semantics need review |
| `CANMIGFRX015` | Info | Multiple `ReportPage`s mapped to multiple PXA pages |
| `CANMIGFRX016` | Warning | Multi-column band flattened — PXA has no column reflow; review manually |

## Architecture & delivery (planned)

- New self-contained project `src/PXA.Migration.Report.Designer.FastReport` (refs `PXA.Core` +
  `PXA.Migration.Abstractions`), `FrxToDesignConverter` with `Convert`/`ConvertAuto`/static
  `LooksLikeFrx`; band-based `RawReport`/`RawBand`/`RawElement` model (adapted from `PXA.Migration.Report.Designer.Rpx`).
- Wire `report-to-design` routing (RDL → RPX → FRX → DevExpress) + WebApi ref + `PXA.sln` entries.
- Frontend "FastReport (.frx)" entry in `MigrationsPage.tsx`.
- Tests: detection, unit conversion, each control, binding, placeholder, invalid XML, and an
  end-to-end render test; **plus** parse a real `Demos/Reports/*.frx` sample.

## V1 checklist

- [x] `FrxToDesignConverter` + project + sln + WebApi ref
- [x] Detection + ordered routing (RDL → RPX → FRX → DevExpress)
- [x] Page size (mm) + band-flatten (px) + header/footer → shared
- [x] TextObject/Line/Shape/Picture/Barcode/CheckBox/Rich/Subreport mapping (Subreport/unknown → placeholder)
- [x] `[Source.Column]` bindings; `"Family, 9pt, style=Bold"` font + named/ARGB colour parsing
- [x] 14 unit tests (schema derived from real samples) + end-to-end render test
- [x] Frontend "FastReport (.frx)" entry; `Designer-Migration.md` row → ✅

## V2 — next

**Current recommendation:** FastReport table fidelity is now in good shape. The next useful work is the
band/runtime layer: multi-page reports, group/detail repeats, child-band join semantics, and validation
against a wider set of real `Demos/Reports/*.frx` samples.

- [x] **P1** `TableObject` → PXA table: grid extraction from `TableColumn` (widths px→pt) /
      `TableRow` / `TableCell` (text + `[Source.Col]`→binding); `ColSpan` padded to keep column alignment;
      first row treated as header when >1 row. Diagnostic `CANMIGFRX013`.
- [x] **P1** `TableCell` per-cell styles → `CellStyles`: `Fill.Color`, `TextFill.Color`, `HorzAlign`,
      `Font`, `Padding`, and `Border.Lines`/`Border.Color`/`Border.Width` map to sparse PXA cell style
      metadata and are covered by converter/exporter tests.
- [x] **P1** `GroupHeader`/`GroupFooter` repeat: group-band objects carry PXA `RepeatDto` (data path from
      the `GroupHeaderBand` `Condition`, e.g. `[Items.Country]`→`Country`) + `style.frxGroup`
      (name/role/band/condition); footers inherit the paired header's group key. Diagnostic `CANMIGFRX014`.
- [x] **P1** Multi-`ReportPage` → one PXA page per `ReportPage` (`CANMIGFRX015`); single-page reports
      keep PageHeader/PageFooter as SharedElements, multi-page reports keep per-page header/footer.
      `ChildBand` content is positioned by its absolute band `Top`, so it flattens in place.
- [x] **P1** `Columns.*` multi-column bands → flattened with a `CANMIGFRX016` review warning (PXA has no
      column reflow); non-table per-side borders: `Border.Lines` on objects → uniform or per-side
      `border{Side}Color/Width` style keys.
- [x] **P1** `PictureObject` MIME sniffing (JPEG/GIF/BMP/WEBP/TIFF magic bytes → correct data-URL type);
      richer `RichObject` RTF → brace-aware text extraction (skips font/colour/style tables, `\par`
      paragraphs, `\'hh`/escaped chars) producing escaped `<p>` HTML.
- [x] **P1** Validate against representative `.frx` samples: `designer-simples/FastReport/frx-samples/`
      (`EmployeesByCountry.frx`) is exercised by the `FastReportSamplesTests` harness + a fidelity test
      (multi-page, group repeat, table). Genuine vendor `Demos/Reports/*.frx` welcome — harness auto-discovers them.
