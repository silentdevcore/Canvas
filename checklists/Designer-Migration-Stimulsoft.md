# Designer Migration: Stimulsoft Reports (`.mrt`) → PXA Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
Stimulsoft `.mrt` → PXA `DesignExportDto` converter.

- **Designer:** Stimulsoft Reports (Designer / Reports.JS) · **Manufacturer:** Stimulsoft
- **Format:** `.mrt` — `StiSerializer` **XML** (the format real `.mrt` files use, incl. Stimulsoft's own
  sample repos). A newer **JSON** `.mrt` variant also exists (V2). **Banded.**
- **Status:** ✅ **Shipped** (`PXA.Migration.Stimulsoft`). Band-flatten with explicit band positions
  (mirrors `PXA.Migration.FastReport`); schema confirmed against real Stimulsoft sample `.mrt` files.

---

## Format overview (confirmed against real `.mrt`)

```xml
<StiSerializer version="1.02" type="Net" application="StiReport">
  <Dictionary>…data sources…</Dictionary>          <!-- ignored -->
  <Pages isList="true" count="1">
    <Page1 Ref="4" type="Page" isKey="true">
      <Components isList="true" count="…">
        <ReportTitleBand2 type="ReportTitleBand">
          <ClientRectangle>0,20,749,80</ClientRectangle>     <!-- x,y,w,h in hundredths of an inch -->
          <Components isList="true">
            <Text20 type="Text">
              <ClientRectangle>570,0,179,40</ClientRectangle> <!-- relative to the band -->
              <Font>Segoe UI,14,Bold,Point,False,0</Font>
              <HorAlignment>Center</HorAlignment>
              <Text>{Customers.CompanyName}</Text>
              <TextBrush>[0:102:204]</TextBrush>
              <Brush>Transparent</Brush>
            </Text20>
          </Components>
        </ReportTitleBand2>
        <PageFooterBand1 type="PageFooterBand"><ClientRectangle>0,1071,749,20</ClientRectangle>…</PageFooterBand1>
      </Components>
    </Page1>
  </Pages>
</StiSerializer>
```

- **Root** `<StiSerializer …application="StiReport">` (unique). `<Pages>` → first `<Page>` → `<Components>`.
- **Bands** are the page's components (`type` ends with `Band`: `ReportTitleBand`, `HeaderBand`,
  `DataBand`, `FooterBand`, `PageHeaderBand`, `PageFooterBand`, `GroupHeaderBand`, …); each carries a
  `<ClientRectangle>` (absolute band position) and a nested `<Components>` of report items.
- **Items**: `Text`, `Image`, `HorizontalLinePrimitive`/`VerticalLinePrimitive`,
  `RectanglePrimitive`/`RoundedRectanglePrimitive`, `BarCode`, `Panel` (container), `SubReport`.
  `<ClientRectangle>` is **relative to the band**; absolute = `band.xy + item.xy`.
- **Units**: `ClientRectangle` is in **hundredths of an inch** → ×0.72 → points (same scale as DevExpress).
- **Style**: `<Font>Family,size,Style,…</Font>`, `<TextBrush>` (text colour), `<Brush>` (background),
  `<HorAlignment>`. Colours are `[R:G:B]`, `solid:Color`, named, or `#hex`.
- **Text/bindings**: `<Text>{DataSource.Field}</Text>` → binding (`Field`); `{Page}`/`{PageNofM}`/other
  `{…}` → expression; plain text → literal.

## Detection / routing

Root LocalName `StiSerializer` is unique → `LooksLikeMrt` = root `<StiSerializer>`. Routed in
`MigrationController` before the DevExpress fallback.

## Control mapping

| `.mrt` item | PXA `ElementDto.Type` | Notes |
| --- | --- | --- |
| `Text` | `text` | `{Source.Field}` → binding; other `{…}` → expression |
| `Image` | `image` | embedded base64 → data URL, else placeholder |
| `HorizontalLinePrimitive` / `VerticalLinePrimitive` | `line` | colour + width |
| `RectanglePrimitive` / `RoundedRectanglePrimitive` | `rect` | border/fill |
| `BarCode` | `barcode` | value + symbology |
| `Panel` | `rect` + flatten children | container |
| `SubReport` / unknown | labeled placeholder | `CANMIGMRT011` |

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGMRT001` | Info | `.mrt` detected — N band(s), M item(s) mapped |
| `CANMIGMRT002` | Info | Per-item mapping |
| `CANMIGMRT010` | Info / Warning | `{Source.Field}` → binding (Info); complex expression → expression (Warning) |
| `CANMIGMRT011` | Warning | Unsupported item / SubReport — labeled placeholder |
| `CANMIGMRT012` | Warning | Image not embeddable — placeholder inserted |
| `CANMIGMRT013` | Warning | `GroupHeaderBand`/`GroupFooterBand` item mapped to PXA repeat metadata; group runtime semantics need review |
| `CANMIGMRT014` | Warning | `Chart`/`CrossTab` has no native PXA equivalent — positioned placeholder inserted |

## V1 checklist

- [x] `MrtToDesignConverter` + project + sln + WebApi ref
- [x] Detection (`StiSerializer`) + routing
- [x] Band-flatten (explicit band positions, hundredths-inch ×0.72) + header/footer → shared
- [x] Text/Image/Line/Rect/BarCode/Panel(flatten) mapping; SubReport/unknown → placeholder
- [x] Font/`TextBrush`/`Brush` parsing; `{Source.Field}` bindings
- [x] Unit + end-to-end render tests (schema from real samples)
- [x] Frontend "Stimulsoft (.mrt)" entry; roadmap row → ✅

## V2 — next

**Current recommendation:** keep XML `.mrt` fidelity ahead of the JSON variant unless a real customer
sample requires Reports.JS JSON. The shared group/repeat model and style/border support should carry
more value than another parser path.

- [x] **P2** JSON `.mrt` variant (modern Reports.JS): `LooksLikeMrt`/`Convert` detect `{` vs `<` and parse
      with `System.Text.Json`. Walks `Pages → Components` (index-keyed objects), maps `Ident` (`StiText`→
      `Text`, `StiGroupHeaderBand`→`GroupHeaderBand`, …), converts geometry by `ReportUnit` (cm/in/mm/px→pt),
      and fills the same `RawReport` so the shared `BuildDesign` (group repeat, borders, expressions,
      named styles) is reused. ⚠️ Schema-based — validated with a synthetic fixture; needs real
      vendor-saved JSON `.mrt` for full tuning.
- [x] **P1** Named `<ComponentStyle>` / report `<Styles>` resolution: a component's referenced report style
      supplies `Font`/`TextBrush`/`Brush`/`HorAlignment` defaults the element doesn't set itself.
- [x] **P1** Per-side `<Border>` parsing: StiBorder `Sides;Color;Size;…` strings map to a uniform
      `borderColor`/`borderWidth` (sides include `All`) or per-side `border{Side}Color/Width` keys, on text
      and rectangle elements (element + named-style fallback).
- [x] **P1** Group bands repeat semantics: `GroupHeaderBand`/`GroupFooterBand` items carry PXA `RepeatDto`
      (data path from the band `<Condition>`, e.g. `{Customers.Country}`→`Country`) + `style.mrtGroup`
      (name/role/condition); footers inherit the paired header's group key. Diagnostic `CANMIGMRT013`.
- [x] **P1** Page `PaperSize`/`PageWidth` units (named PaperSize, else explicit `PageWidth`/`PageHeight`
      hundredths-inch → pt); `Chart`/`CrossTab` → positioned placeholders with `CANMIGMRT014`.
- [x] **P1** Stimulsoft expression dialect: single `{Source.Field}` → binding; compound expressions/functions
      preserved on `Expression` + `style.mrtExpression` with every `{Source.Field}`/`{Field}` normalized to a
      PXA `{{Field}}` token (system variables left intact).
