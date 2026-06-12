# Designer Migration: FastReport .NET (`.frx`) → Canvas Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
FastReport `.frx` → Canvas `DesignExportDto` converter (format research, schema, control mapping,
diagnostics, status).

- **Designer:** FastReport .NET · **Manufacturer:** Fast Reports Inc.
- **Format:** `.frx` — plain, namespace-free **XML**, **banded** report.
- **Status:** ✅ **Shipped** (`Canvas.Migration.FastReport`) — band-flatten mirroring `Canvas.Migration.Rpx`;
  schema confirmed against real `FastReports/FastReport` `Demos/Reports/*.frx`. 14 unit tests + render test.

---

## Why FastReport next

- Plain XML, banded → reuses the `Canvas.Migration.Rpx` band-flatten model and the
  `Canvas.Migration.DevExpressReport` `.repx` font-string parser (`"Tahoma, 9pt, style=Bold"`).
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
  footer anchored to page bottom. (Same flatten as `Canvas.Migration.Rpx`.)

## Detection / routing

Root is `<Report>` — shared with RDL and RPX, so order matters in `MigrationController`:
**RDL → RPX → FRX → DevExpress**. `LooksLikeFrx`: root `<Report>`, has a `<ReportPage>` descendant,
namespace does **not** contain `reportdefinition`, and no `<Sections>` (RPX). Accept a leading `<`.

## Control mapping (planned)

| `.frx` object | Canvas `ElementDto.Type` | Notes |
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

**Bindings:** `TextObject.Text` of form `[DataSource.Column]` (or `[Column]`) → Canvas binding; other
`[...]`/script expressions → `Expression` + warning. Literal text → plain content.

**To confirm against real samples:** exact fill/border attribute names (`Fill.Color`/`<Fill>`,
`Border.Lines`/`Border.Color`/`Border.Width`), colour encoding (named vs ARGB int vs `R,G,B`), the
`Band` suffix on band element names, and the px-vs-mm unit split.

## Diagnostics (planned, mirroring `CANMIGRDL/RPX`)

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGFRX001` | Info | FastReport `.frx` detected — N band(s), M object(s) mapped |
| `CANMIGFRX002` | Info | Per-object mapping (`name (type) → Canvas type`) |
| `CANMIGFRX010` | Info / Warning | `[Source.Col]` → binding (Info); complex expression → expression (Warning) |
| `CANMIGFRX011` | Warning | Unsupported object / Subreport / script — labeled placeholder |
| `CANMIGFRX012` | Warning | Picture data not embeddable — placeholder inserted |

## Architecture & delivery (planned)

- New self-contained project `src/Canvas.Migration.FastReport` (refs `Canvas.Core` +
  `Canvas.Migration.Abstractions`), `FrxToDesignConverter` with `Convert`/`ConvertAuto`/static
  `LooksLikeFrx`; band-based `RawReport`/`RawBand`/`RawElement` model (adapted from `Canvas.Migration.Rpx`).
- Wire `report-to-design` routing (RDL → RPX → FRX → DevExpress) + WebApi ref + `Canvas.sln` entries.
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

- [ ] `TableObject` → Canvas table (grid extraction from TableColumn/TableRow/TableCell)
- [ ] Multi-`ReportPage` (currently first page); `ChildBand` join semantics; `GroupHeader/Footer` repeat
- [ ] Per-side `Border.Lines` rendering; `Padding`; `Columns.*` multi-column bands
- [ ] `PictureObject` non-PNG MIME sniffing; richer `RichObject` RTF→HTML
- [ ] Validate against more `Demos/Reports/*.frx` (charts/matrix/gauge → placeholder)
