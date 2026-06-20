# Designer Migration: JasperReports (`.jrxml`) → Canvas Designer

Per-designer companion to the roadmap [`Designer-Migration.md`](Designer-Migration.md). Tracks the
JasperReports `.jrxml` → Canvas `DesignExportDto` converter.

- **Designer:** JasperReports / Jaspersoft Studio (iReport) · **Manufacturer:** Cloud Software Group (Jaspersoft)
- **Format:** `.jrxml` — namespaced XML (`http://jasperreports.sourceforge.net/jasperreports`), **banded**.
- **Status:** ✅ **Shipped** (`Canvas.Migration.JasperReports`). Band-flatten mirroring
  `Canvas.Migration.Rpx`/`Canvas.Migration.Telerik`; schema confirmed against real Jaspersoft `.jrxml` samples.

---

## Format overview (confirmed against real `.jrxml` samples)

```xml
<jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
    name="invoice" pageWidth="595" pageHeight="842" columnWidth="555"
    leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
  <style name="Header" forecolor="#0066CC"><font fontName="Arial" size="20" isBold="true"/></style>
  <field name="customerName" class="java.lang.String"/>
  <title>
    <band height="40">
      <staticText>
        <reportElement x="0" y="0" width="555" height="30" forecolor="#0066CC"/>
        <textElement textAlignment="Center"><font fontName="Arial" size="20" isBold="true"/></textElement>
        <text><![CDATA[INVOICE]]></text>
      </staticText>
    </band>
  </title>
  <pageHeader><band height="20">…</band></pageHeader>
  <detail>
    <band height="20">
      <textField>
        <reportElement x="0" y="0" width="200" height="20"/>
        <textElement/>
        <textFieldExpression><![CDATA[$F{customerName}]]></textFieldExpression>
      </textField>
      <line><reportElement x="0" y="19" width="555" height="1" forecolor="#808080"/></line>
    </band>
  </detail>
  <pageFooter><band height="20">…</band></pageFooter>
  <summary><band>…</band></summary>
</jasperReport>
```

- **Root** `<jasperReport>` (note lowercase `j`) with the JasperReports namespace, `pageWidth`/`pageHeight`/
  `columnWidth`/`*Margin`, `orientation`. **Units are points** (JasperReports pixels = 1/72in), so no scaling.
- **Band sections** (canonical order): `background`, `title`, `pageHeader`, `columnHeader`, `detail`,
  `columnFooter`, `pageFooter`/`lastPageFooter`, `summary` (+ `groupHeader`/`groupFooter`). Each wraps a
  `<band height="…">` holding elements; `detail` may hold several `<band>`s.
- **Elements**: `staticText`, `textField`, `line`, `rectangle`, `ellipse`, `image`, `frame` (container),
  `subreport`, `componentElement` (barcodes/charts). Each has a `<reportElement x y width height forecolor
  backcolor mode style>`; text elements add `<textElement textAlignment><font fontName size isBold
  isItalic isUnderline/></textElement>`; `staticText` → `<text><![CDATA[…]]></text>`; `textField` →
  `<textFieldExpression><![CDATA[$F{field}]]></textFieldExpression>`.
- **Named styles**: top-level `<style name=… forecolor= backcolor=><font…/></style>`; a `reportElement`'s
  `style` attribute references one (resolved like Telerik's stylesheet).

## Units & coordinates

- All geometry already in **points** (×1). Sections **stack** in canonical order accumulating band
  `height`; absolute Y = `marginTop + Σ(prior band heights) + element.y`. `pageHeader`/`pageFooter` →
  SharedElements (footer bottom-anchored). Mirrors `Canvas.Migration.Rpx`.

## Detection / routing

Root LocalName is `jasperReport` — **unique** (no other format uses it), so detection is unambiguous:
`LooksLikeJrxml` = root `<jasperReport>`. Routed in `MigrationController` before the DevExpress fallback.

## Control mapping

| `.jrxml` element | Canvas `ElementDto.Type` | Notes |
| --- | --- | --- |
| `staticText` | `text` | `<text>` CDATA literal |
| `textField` | `text` | `$F{field}` → binding; other expressions → expression |
| `line` | `line` | `<graphicElement><pen lineWidth lineColor>` → stroke |
| `rectangle` / `ellipse` | `rect` / `circle` | forecolor/backcolor + `<box>`/per-side pens → border/fill |
| `image` | `image` | embedded base64 → data URL, else placeholder |
| `frame` | `rect` + flatten children | container |
| `componentElement` barcode / QR | `barcode` / `qrcode` | `barbecue` / barcode-like components + code expressions |
| `componentElement` `jr:table` | `table` | header/detail rows + dataset metadata; repeat semantics require review |
| `componentElement` chart / `crosstab` | labeled placeholder + component metadata | `CANMIGJRXML011` |
| `subreport` | labeled placeholder + subreport metadata | `CANMIGJRXML011` |
| unknown | labeled placeholder | `CANMIGJRXML011` |

**Bindings:** `textFieldExpression` `$F{field}` → Canvas binding; simple `$P{…}`/`$V{…}` →
Canvas parameter/variable placeholders plus normalized expressions; complex JasperReports expressions preserve the
original in metadata and normalize `$F{}`/`$P{}`/`$V{}` references to Canvas-style expression paths.

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGJRXML001` | Info | `.jrxml` detected — N band(s), M element(s) mapped |
| `CANMIGJRXML002` | Info | Per-element mapping |
| `CANMIGJRXML010` | Info / Warning | `$F{X}` → binding (Info); `$P{X}`/`$V{X}` and complex expressions → normalized Canvas expression with review warning |
| `CANMIGJRXML011` | Warning | Unsupported element / subreport / component — labeled placeholder |
| `CANMIGJRXML012` | Warning | Image not embeddable — placeholder inserted |
| `CANMIGJRXML013` | Info | JasperReports barcode/QR component mapped to Canvas barcode/QR |
| `CANMIGJRXML014` | Warning | JasperReports table component mapped to Canvas table; dataset/repeat semantics require review |
| `CANMIGJRXML015` | Warning | JasperReports data declarations preserved in `PageSettings.CustomProperties`; runtime dataset/query evaluation needs review |
| `CANMIGJRXML016` | Warning | JasperReports `printWhenExpression` mapped to Canvas visibility metadata; runtime semantics need review |
| `CANMIGJRXML017` | Warning | JasperReports group header/footer mapped to Canvas repeat metadata; runtime group semantics need review |
| `CANMIGJRXML018` | Warning | Multiple JasperReports detail bands mapped to shared Canvas repeat metadata; runtime multi-band detail semantics need review |

## V1 checklist

- [x] `JrxmlToDesignConverter` + project + sln + WebApi ref
- [x] Detection (`<jasperReport>`) + routing
- [x] Page size/margins (points, ×1) + section-flatten + header/footer → shared
- [x] staticText/textField/line/rectangle/ellipse/image/frame(flatten); subreport/component → placeholder
- [x] Named `<style>` resolution + inline reportElement/font; `$F{}` bindings
- [x] Unit + end-to-end render tests (schema from real samples)
- [x] Frontend "JasperReports (.jrxml)" entry; roadmap row → ✅

## V2 — next

**Current recommendation:** align JasperReports with the shared P1 fidelity pass: group/repeat
semantics first, then styles/borders and better component placeholders.

## Sample audit — Jaspersoft resources

Local sample source:
`designer-simples/JasperReports/jaspersoft-resources-master/Samples` (local-only test resources).

Analyzed 13 `.jrxml` files:
- `Invoice/Invoice.jrxml`
- `Json/Json_Master.jrxml`, `Json/Json_Sub.jrxml`
- `09. Customer Detail Report/main.jrxml`, `CustomerPurchasesReport.jrxml`
- `Monthly Store Report/Monthly_Store_Report.jrxml`, `Cover.jrxml`, `Backcover.jrxml`, `toc.jrxml`,
  `Store.jrxml`, `Store_Crosstab.jrxml`, `Stores_Overview.jrxml`, `Stores_Overview_Table.jrxml`

Observed feature coverage:

| Feature family | Seen in samples | Current converter status | Priority |
| --- | ---: | --- | --- |
| Basic band layout, static text, text fields, lines, rectangles, images, frames | broad | supported; multiple detail bands carry shared repeat metadata | Done/P1 runtime review |
| `<box>` and per-side pens | 150 boxes, 125 left / 118 bottom / 112 top / 111 right pens | supported for named styles + element boxes | Done |
| `componentElement` barcode/QR | component path present | supported as Canvas `barcode`/`qrcode` | Done |
| `componentElement` charts / Highcharts | 5 chart components, 116 chart properties, 9 series | structured placeholder metadata only | P1/P2 |
| `componentElement` `jr:table` | 4 table components | Canvas table with header/detail rows + dataset metadata | Done/P1 review |
| `crosstab` | 1 crosstab, row/column groups and measures | structured placeholder metadata only | P1/P2 |
| `subreport` | 2 direct subreports, 9 subreport expressions, 11 parameters | direct subreport metadata preserved; part subreports still need orchestration | Done/P1 parts |
| `sectionType="Part"` / `<part>` book reports | Monthly Store Report has 7 parts | preserved in `jrxmlParts`; rendering/inlining still later | Done/P1 runtime review |
| `groupHeader` / `groupFooter` | 2 groups | header/footer bands are stacked around detail and mapped to `jrxmlGroup`/`jrxmlRepeat` + `RepeatDto` metadata | Done/P1 runtime review |
| `subDataset`, `datasetRun`, SQL/JSON query metadata | 9 subdatasets, 8 dataset runs, 22 queries | report/subdataset/query metadata preserved; datasetRun kept on table/component styles | Done/P1 runtime review |
| Parameters / fields / variables | 26 parameters, 136 fields, 18 variables | declarations preserved in `PageSettings.CustomProperties`; runtime expressions normalize `$P{}`/`$V{}` where consumed | Done/P1 runtime review |
| Conditional styles | 8 conditional styles | open | P1 |
| `printWhenExpression` | 9 occurrences | maps to `Hidden`/`VisibleExpression` + `style.jrxmlPrintWhenExpression` | Done/P1 runtime review |
| Hyperlink / anchor expressions | 7 hyperlink anchors, 1 anchor name | open | P2 |
| External image paths | 10 image expressions, many path-based | currently placeholder if not embedded | P2 |

Key sample-driven conclusions:
- `Invoice.jrxml` is the best table-component test bed: nested `jr:table`, `datasetRun`, dataset parameters,
  table column/detail cells, variables, formatted `$P{}`/`$V{}` expressions, external images, and box styles.
- `Monthly_Store_Report.jrxml` is the best part/subreport orchestration test bed: `sectionType="Part"`,
  `<part>` blocks, group header/footer, subreport expressions and parameters.
- `Store.jrxml` / `Stores_Overview*.jrxml` are chart/table fidelity samples: Highcharts components,
  dataset runs, series, chart properties, and table columns.
- `Store_Crosstab.jrxml` is the crosstab sample: row/column group and measure metadata.
- `Json_Master.jrxml` / `Json_Sub.jrxml` prove that datasource/query metadata must not assume SQL only.

- [x] **P1** `groupHeader`/`groupFooter` repeat semantics: group bands are flattened in Jasper order,
      group declarations are preserved in `jrxmlGroups`, and group header/footer elements receive
      `style.jrxmlGroup`, `style.jrxmlRepeat`, and Canvas `RepeatDto` metadata for runtime review.
- [x] **P1** Multiple `detail` bands with shared repeat container semantics: each detail-band element now
      receives `style.jrxmlDetailRepeat` and Canvas `RepeatDto` with shared `DetailRows` data path, while
      band order/height metadata is preserved in `jrxmlDetailBands`.
- [x] **P1** `sectionType="Part"` / `<part>` orchestration metadata: preserve part order/context,
      `partNameExpression`, `evaluationTime`, subreport expression, and parameters in `jrxmlParts` so
      book-style reports such as Monthly Store Report do not lose their structure.
- [x] **P1** `jr:table` component → Canvas table with `CellData`, column widths, header/detail row extraction,
      datasetRun/parameter metadata, and expression preservation from Invoice and Store samples.
- [x] **P1** Preserve report-level data declarations: parameters, fields, variables, subDataset/queryString,
      SQL/JSON query language metadata in `PageSettings.CustomProperties`, plus datasetRun metadata on table/component styles.
- [x] **P1** `<box>`/per-side pens + named style inheritance for box borders.
- [ ] **P1** Conditional styles and full chained style inheritance.
- [x] **P1** `printWhenExpression` → Canvas `Hidden`/`VisibleExpression` plus preserved
      `style.jrxmlPrintWhenExpression`, matching the RDL hidden-expression pattern.
- [x] **P1** `componentElement` barcode/QR components → Canvas `barcode` / `qrcode` with field expression values.
- [x] **P1** `componentElement` charts/crosstab placeholders with captions and preserved component metadata
      (`jrxmlComponentType`, `style.jrxmlComponent`, dataset hints, expressions, group/measure counts).
- [x] **P1** Direct subreport metadata: preserve `subreportExpression`, `subreportParameter`,
      `connectionExpression`/`dataSourceExpression`, and return values on `style.jrxmlSubreport`.
- [ ] **P1** Subreport inlining/part orchestration: convert `sectionType="Part"` + `subreportPart`
      into structured Canvas/page metadata; direct placeholders already preserve subreport metadata.
- [x] **P1** `$P{}` / `$V{}` expression dialect: simple parameter/variable references map to Canvas-friendly
      placeholders/expressions; complex expressions preserve originals and add normalized Canvas-style references
      for review.
- [ ] **P2** Hyperlink/anchor expressions and external image resource resolution.
