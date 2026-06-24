# Designer Migration: Telerik Reporting (`.trdx`) → Canvas Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
Telerik Reporting `.trdx` → Canvas `DesignExportDto` converter.

- **Designer:** Telerik Reporting · **Manufacturer:** Progress (Telerik)
- **Format:** `.trdx` — **namespaced XML** (`http://schemas.telerik.com/reporting/<ver>`). `.trdp` is the
  same XML zipped (V2). **Sectioned** layout with Unit-string geometry.
- **Status:** ✅ **Shipped** (`Canvas.Migration.Telerik`). Hybrid of `Canvas.Migration.Rdl` (length-string
  units, `<Style>` elements) and `Canvas.Migration.Rpx` (section/band flatten); `<StyleSheet>` `StyleName`
  resolution. Schema confirmed against real `.trdx` samples. 11 unit tests + render test.

---

## Format overview (confirmed against real `.trdx` samples)

```xml
<Report Width="8.1in" Name="ProductCatalog" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
  <DataSources>…</DataSources>                              <!-- ignored -->
  <PageSettings>                                            <!-- optional; else A4 -->
    <PaperKind>Letter</PaperKind>
    <Margins><MarginsU Left="1in" Right="1in" Top="1in" Bottom="1in"/></Margins>
  </PageSettings>
  <Items>
    <PageHeaderSection Height="0.5in" Name="pageHeaderSection1">
      <Items>
        <TextBox Width="3.5in" Height="0.2in" Left="0.45in" Top="0.15in" Value="PAGE HEADER" Name="textBox4"/>
      </Items>
    </PageHeaderSection>
    <DetailSection Height="0.13in" Name="detailSection1">
      <Style><Font Size="8pt"/><BorderColor Default="137, 145, 164"/></Style>
      <Items>
        <TextBox Width="8.1in" Height="0.6in" Left="0in" Top="0in" Value="=Fields.ProductName" Name="t1" StyleName="Header"/>
        <PictureBox Width="1in" Height="1in" Left="7in" Top="0in" Value="=Fields.LargePhoto" Name="p1"/>
      </Items>
    </DetailSection>
    <PageFooterSection Height="0.4in" Name="pageFooterSection1"><Items>…</Items></PageFooterSection>
  </Items>
  <StyleSheet>
    <StyleRule>
      <Style><Font Name="Segoe UI Light" Size="25pt" Bold="True"/></Style>
      <Selectors><StyleSelector Type="ReportItemBase" StyleName="Header"/></Selectors>
    </StyleRule>
  </StyleSheet>
</Report>
```

- **Root** `<Report>` with a `schemas.telerik.com/reporting` namespace and a `Width` (body width, Unit string).
- **`<Items>`** under `<Report>` holds the **sections** — `ReportHeaderSection`, `PageHeaderSection`,
  `GroupHeaderSection`, `DetailSection`, `GroupFooterSection`, `PageFooterSection`, `ReportFooterSection`.
  Each has `Height` (Unit), an optional `<Style>`, and an `<Items>` of report items.
- **Report items**: `TextBox`, `HtmlTextBox`, `PictureBox`, `Shape`, `Table`, `Panel`, `SubReport`,
  `Barcode`, `CrossTab`, `Chart`/`Graph`. Geometry as separate Unit-string attributes
  `Left`/`Top`/`Width`/`Height`; `Value` (literal or `=Fields.X`); `Name`; optional `StyleName`.
- **Styling**: inline `<Style>` on the item **and/or** a `StyleName` resolved from `<StyleSheet>`
  (`<StyleRule><Style/><Selectors><StyleSelector StyleName="…"/></Selectors></StyleRule>`). `<Style>`
  carries `<Font Name Size Bold Italic Underline>`, text colour, `BackgroundColor`, `TextAlign`,
  `<BorderColor Default="R,G,B"/>`. (V1 reads inline + StyleName; `TypeSelector` rules are V2.)

## Units & coordinates

- Unit strings (`in`/`cm`/`mm`/`pt`/`px`) → points — reuse a length parser like `Canvas.Migration.Rdl`'s
  `LengthToPt`. Sections **stack** in canonical order accumulating `Height` (no explicit Top), so
  absolute Y = `marginTop + Σ(prior section heights) + section-relative item Top` — the
  `Canvas.Migration.Rpx`/DevExpress band-flatten. PageHeader/PageFooter → SharedElements; footer anchored
  to page bottom.

## Detection / routing

Root `<Report>` is shared with RDL/RPX/FRX, so order in `MigrationController`:
**RDL → RPX → FRX → TRDX → DevExpress**. `LooksLikeTrdx`: root `<Report>`, namespace contains
`telerik.com/reporting`. (Unambiguous — none of the others use a Telerik namespace.)

## Control mapping (planned)

| `.trdx` item | Canvas `ElementDto.Type` | Notes |
| --- | --- | --- |
| `TextBox` / `HtmlTextBox` | `text` / `richtext` | `Value`; `=Fields.X` → binding; style from inline + `StyleName` |
| `PictureBox` | `image` | embedded `Value`/base64 → data URL, else placeholder |
| `Shape` | `rect` / `circle` | `ShapeType` Ellipse → circle |
| `Table` / `CrossTab` | `table` | grid from rows/cells (basic) |
| `Barcode` | `barcode` | symbology → type; `Value` |
| `Panel` | `rect` + flatten children | container |
| `SubReport` / `Chart` / `Graph` / unknown | labeled placeholder | `CANMIGTRDX011` |

**Bindings:** `Value="=Fields.Col"` (or `=Fields.Col.Value`) → Canvas binding; other `=…` → expression.

## Diagnostics (planned, mirroring the others)

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGTRDX001` | Info | `.trdx` detected — N section(s), M item(s) mapped |
| `CANMIGTRDX002` | Info | Per-item mapping |
| `CANMIGTRDX010` | Info / Warning | `=Fields.X` → binding (Info); complex expression → expression (Warning) |
| `CANMIGTRDX011` | Warning | Unsupported item / SubReport / Chart — labeled placeholder |
| `CANMIGTRDX012` | Warning | Picture not embeddable — placeholder inserted |
| `CANMIGTRDX013` | Warning | `Table`/`CrossTab` mapped to a Canvas table (best-effort cell anchoring) — review |
| `CANMIGTRDX014` | Warning | `GroupHeaderSection`/`GroupFooterSection` item mapped to Canvas repeat metadata; group runtime semantics need review |

## V1 checklist

- [x] `TrdxToDesignConverter` + project + sln + WebApi ref
- [x] Detection (Telerik namespace) + ordered routing (RDL → RPX → FRX → TRDX → DevExpress)
- [x] PageSettings (PaperKind/margins) + section-flatten (Unit strings) + header/footer → shared
- [x] TextBox/HtmlTextBox/PictureBox/Shape/Barcode/Panel(flatten) mapping; Table/CrossTab/Chart/SubReport → placeholder
- [x] `<StyleSheet>` `StyleName` resolution + inline `<Style>` override; `=Fields.X` bindings
- [x] 11 unit tests (schema derived from real samples) + end-to-end render test
- [x] Frontend "Telerik Reporting (.trdx)" entry; roadmap row → ✅

## V2 — next

**Current recommendation:** defer packaged `.trdp` until a binary upload path exists. Before that, focus
on XML `.trdx` fidelity: real-sample validation, group/repeat semantics, expression dialect coverage,
and unsupported visual regions. Basic `Table`/`CrossTab` extraction and table cell styles are now done.

- [x] **P2** `.trdp` (zipped package): the `report-to-design` endpoint accepts a base64 binary upload
      (`sourceBase64`), `ReportPackageExtractor` unzips it, picks the inner `.trdx` (first entry the
      detectors recognize), and feeds the existing parser; other text entries become sub-report resources.
      *(Backend path done + tested; a binary file picker in the migration UI is the remaining frontend piece.)*
- [x] **P1** Group-section repeat: `GroupHeaderSection`/`GroupFooterSection` items carry Canvas `RepeatDto`
      (data path from the `<Grouping>` expression, e.g. `=Fields.Country`→`Country`) + `style.trdxGroup`
      (name/role/band/condition); footers inherit the paired header's group key. Diagnostic `CANMIGTRDX014`.
- [x] **P1** `TypeSelector` stylesheet rules: `<StyleSelector Type=…>` rules apply to every control of that
      type (precedence: type → named `StyleName` → inline `<Style>`). `Panel` nesting is handled by the
      recursive `ParseItems` (offsets folded so children stay absolute; depth-guarded).
- [x] **P1** `Table`/`CrossTab` cell extraction → Canvas table: column widths from `TableBodyColumn`,
      content items placed by attached cell-anchor properties (`*.CellRowIndex`/`*.CellColumnIndex`,
      prefix-agnostic, attribute or element), `=Fields.X`→binding tokens, sequential-fill fallback when
      no anchors are present (`CANMIGTRDX013`). ⚠️ Unverified against real `.trdx` (no local samples) —
      cell anchoring is best-effort. `Chart`/`Graph`/`Map` remain captioned placeholders.
- [x] **P1** Telerik table `CellStyles`: named + inline content-item `<Style>` values inside table cells
      preserve background, text alignment, font and border metadata for Canvas table rendering/export.
- [x] **P1** Telerik expression dialect: single `=Fields.X` → binding; compound expressions/functions are
      preserved on `element.Expression` + `style.trdxExpression` with every `Fields.X` reference normalized
      to a Canvas `{{X}}` token in the rendered content.
