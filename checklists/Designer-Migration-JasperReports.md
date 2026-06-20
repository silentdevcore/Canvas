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
| `rectangle` / `ellipse` | `rect` / `circle` | forecolor/backcolor → border/fill |
| `image` | `image` | embedded base64 → data URL, else placeholder |
| `frame` | `rect` + flatten children | container |
| `componentElement` barcode / QR | `barcode` / `qrcode` | `barbecue` / barcode-like components + code expressions |
| `subreport` / `componentElement` chart / `crosstab` / unknown | labeled placeholder | `CANMIGJRXML011` |

**Bindings:** `textFieldExpression` `$F{field}` → Canvas binding; `$P{…}`/`$V{…}`/complex → expression.

## Diagnostics

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGJRXML001` | Info | `.jrxml` detected — N band(s), M element(s) mapped |
| `CANMIGJRXML002` | Info | Per-element mapping |
| `CANMIGJRXML010` | Info / Warning | `$F{X}` → binding (Info); complex expression → expression (Warning) |
| `CANMIGJRXML011` | Warning | Unsupported element / subreport / component — labeled placeholder |
| `CANMIGJRXML012` | Warning | Image not embeddable — placeholder inserted |
| `CANMIGJRXML013` | Info | JasperReports barcode/QR component mapped to Canvas barcode/QR |

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

- [ ] **P1** `groupHeader`/`groupFooter` repeat semantics; multiple `detail` bands
- [ ] **P1** `<box>`/per-side pens; `<style>` inheritance (`style` chains); conditional styles
- [x] **P1** `componentElement` barcode/QR components → Canvas `barcode` / `qrcode` with field expression values.
- [ ] **P1** `componentElement` charts/crosstab placeholders with captions and preserved component metadata.
- [ ] **P1** `subreport` inlining; `$P{}`/`$V{}` expression dialect
