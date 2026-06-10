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

## V1 Scope

- [ ] V1 handles **static-content reports** (literal `Text`, fixed positions/sizes, fonts, colours).
- [ ] Input is **C# source** only; `.repx` XML input is deferred to a later version.
- [ ] Deferred → emit a diagnostic + keep static value/placeholder: data bindings / `ExpressionBindings`,
      calculated fields, sub-reports, scripts, grouping/sorting, anchored/auto sizing.

## Two structural problems

- [ ] **Unit conversion.** XtraReports default `ReportUnit = HundredthsOfAnInch`; `LocationF`/`SizeF` are
      in report units. Canvas uses points (1/72"). Convert `pt = value × 0.72` (hundredths-of-inch).
      Detect `report.ReportUnit` (`Pixels`, `TenthsOfAMillimeter`, …) and scale accordingly; default to
      hundredths-of-inch when unspecified.
- [ ] **Band flattening.** Controls live in bands (`TopMarginBand`, `ReportHeaderBand`, `PageHeaderBand`,
      `DetailBand`, `GroupHeaderBand`, `PageFooterBand`, …) with `LocationF.Y` relative to their band.
      Compute absolute page Y by accumulating each preceding band's `HeightF` (margin bands fold into the
      page margin). This is the core of the converter.

## Architecture

- [ ] New isolated project `src/Canvas.Migration.DevExpressReport` (refs `Canvas.Core` for
      `Canvas.Core.Contracts.DesignExportDto` + `Microsoft.CodeAnalysis.CSharp`). Mirrors the
      `Canvas.Migration.*` separation but returns a `DesignExportDto`, not a code string.
- [ ] `XtraReportToDesignConverter.Convert(string csharpSource)` →
      `{ DesignExportDto Design, List<MigrationDiagnostic> Diagnostics }`.
- [ ] Roslyn pre-scan: locate the `: XtraReport` declaration; collect control **field declarations**
      (type → variable name); read `InitializeComponent()` assignments into a per-control property bag
      (`.Text`, `.LocationF = new PointF(x,y)`, `.SizeF = new SizeF(w,h)`, `.Font = new Font(...)`,
      `.ForeColor`, `.TextAlignment`, `.BackColor`, `.Borders`).
- [ ] Band membership from `band.Controls.AddRange(new[] { ... })` / `band.Controls.Add(...)`; read each
      band's `HeightF` + band type to compute the vertical offset.
- [ ] Build `ElementDto` ([DesignExportDto.cs:140](../src/Canvas.Core/Contracts/DesignExportDto.cs#L140))
      into `PageDto.Elements`; set `PageSettingsDto` from `report.PageWidth/PageHeight` or default A4.

## Mapping Table

| XtraReport control | Canvas `ElementDto.Type` | Key fields |
| --- | --- | --- |
| `XRLabel` / `XRPageInfo` | `text` | `Content` ← `.Text`; `Style` ← `{ fontSize, color, fontFamily, fontWeight, textAlign }` |
| `XRLine` | `line` | x/y/w/h; `Style.color` ← `.ForeColor` |
| `XRShape` / `XRPanel` (rectangle) | `rect` | border/fill ← `.Borders`/`.BackColor` |
| `XRPictureBox` | `image` | `FitMode`; image data deferred → placeholder |
| `XRTable`/`XRTableRow`/`XRTableCell` | `table` | `CellData` from row/cell `.Text`, equal `ColumnWidths`; rows/cells folded into the table |
| `XRBarCode` | `barcode` | `BarcodeValue`/`BarcodeType` |
| `XRRichText` | `richtext` | `HtmlContent` from static text |
| Unsupported control | *(skipped)* | `CANMIGDEVREP011` warning with the control type |

All elements: `X/Y/Width/Height` = unit-converted, band-flattened absolute points.

## Delivery (endpoint + designer)

- [ ] Backend: `POST /api/migration/report-to-design` (`{ sourceCode }`) →
      `{ design: DesignExportDto, diagnostics: [...] }`. Reference the new project from
      `Canvas.WebApi.csproj`; wire near the existing converters
      ([Services/Converters/](../Canvas.WebApi/Services/Converters/)).
- [ ] Frontend: a **"DevExpress Reports"** entry on the Migrations page
      ([MigrationsPage.tsx](../ui-designer-v2/src/pages/MigrationsPage.tsx)) — calls the endpoint, then
      loads the design via `bulkReplaceContent(pages, sharedElements)`
      ([store.ts:130](../ui-designer-v2/src/store.ts#L130)) (same hop the Importer's `loadFromFile`
      uses) and navigates to the editor; shows a diagnostics summary.
- [ ] `ExportService.convertReportToDesign(sourceCode)` helper mirroring existing fetch methods.

## Diagnostic IDs

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGDEVREP001` | Info | XtraReport + bands detected (N bands, M controls) |
| `CANMIGDEVREP002` | Info | Control mapped to a Canvas element |
| `CANMIGDEVREP010` | Warning | Data binding / expression dropped — static value kept |
| `CANMIGDEVREP011` | Warning | Unsupported control skipped |
| `CANMIGDEVREP012` | Warning | Sub-report / script requires manual migration |
| `CANMIGDEVREP013` | Warning | Picture data not embeddable — placeholder inserted |

## Tests Checklist

- [ ] `ReportHeaderBand` (title `XRLabel`) + `DetailBand` (`XRLabel` + `XRLine`) → `DesignExportDto` with
      correct **band-flattened** absolute Y and **unit-converted** coordinates.
- [ ] `XRLabel` style maps font size/family/weight/colour/alignment.
- [ ] `XRLine` → `line`; `XRShape` → `rect`; `XRPictureBox` → `image` placeholder (`CANMIGDEVREP013`).
- [ ] `ReportUnit = Pixels` vs default hundredths-of-inch scale correctly.
- [ ] Data-bound label emits `CANMIGDEVREP010` and keeps any static text.
- [ ] Unsupported control emits `CANMIGDEVREP011`.
- [ ] Endpoint returns design JSON; `dotnet build Canvas.sln` succeeds.

## Assumptions

- [ ] Use `Canvas.Migration.DevExpressReport`, separate from `Canvas.Migration.DevExpressPdf`.
- [ ] Output target is Canvas design JSON (designer), not Canvas.Pdf C# code.
- [ ] Default page size A4 when the report declares none.
