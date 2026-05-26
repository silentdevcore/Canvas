# Code-Migrations-UI — Feature Checklist

## Overview

A **Migrations** page in the Canvas web app (`ui-designer-v2`) that lets developers paste code written for another PDF library, convert it to Canvas.Pdf C# code, and preview the resulting PDF — all in the browser.

---

## User Flow

1. Developer opens the Canvas web app and clicks **Migrations** in the top navigation bar.
2. The `/migrations` route renders the full `MigrationsPage` component.
3. Developer selects the source PDF framework from the dropdown.
4. Developer pastes their existing code into the **Source Code** textarea, or clicks **Load example** to fill in a realistic sample for the selected framework.
5. Developer clicks **Convert →** → the right panel shows the equivalent **Canvas.Pdf** C# code, plus a diagnostics bar.
6. Developer clicks **Generate Preview** → an iframe (860 px tall) renders the actual PDF.
7. Developer copies or downloads the canvas code with the **Copy** / **Download .cs** buttons.
8. Switching the framework dropdown **clears** the source code, output, diagnostics and preview.

---

## UI Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  [Canvas logo]  Home  Templates  Docs  [Migrations]              │  ← AppHeader nav
├──────────────────────────────────────────────────────────────────┤
│  ⌨  Code Migrations                                              │
│     Paste code from another PDF library, convert to Canvas.Pdf   │
│                                                                  │
│  Source framework: [iText7 (pilot) ▼]  <description>  [Pilot]   │
│                                                     [Load example]│
├─────────────────────────┬────────────────────────────────────────┤
│  Source Code — iText7   │  Canvas.Pdf Code                       │
│                         │                         [Copy] [↓ .cs] │
│  <textarea>             │  <pre> (read-only)                     │
│                         │                                        │
│  [Convert →]            │  [▶ Generate Preview]                  │
├─────────────────────────┴────────────────────────────────────────┤
│  Diagnostics: ● 4 info  ⚠ 1 warning                             │
├──────────────────────────────────────────────────────────────────┤
│  PDF Preview                                                     │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │ [blue strip] Code migration for iText7          [footer] │    │  ← 860 px tall iframe
│  │              <rendered content or manual note>           │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

---

## Framework Status Badges

| Badge | Colour | Meaning |
|-------|--------|---------|
| **Full** | Green | Roslyn-based automatic conversion of all common patterns |
| **Pilot** | Orange | Roslyn-based conversion of the core lifecycle; complex APIs flagged for manual review |
| **Skeleton** | Gray | Placeholder — conversion not yet implemented |

---

## Framework Support Matrix

| ID | Display Name | Status | Example included |
|----|-------------|--------|-----------------|
| `Syncfusion` | Syncfusion PDF | **Full** | ✅ |
| `iText7` | iText7 | **Full** | ✅ |
| `Apryse` | Apryse (PDFTron) | **Full** | ✅ |
| `Aspose` | Aspose.PDF | **Full** | ✅ |
| `DsPdf` | DsPdf (GrapeCity) | **Full** | ✅ |
| `Foxit` | Foxit PDF SDK | **Full** | ✅ |
| `DevExpress` | DevExpress PDF | **Full** | ✅ |
| `IronPdf` | IronPDF | **Pilot** | ✅ |
| `Spire` | Spire.PDF | Skeleton | — |
| `GemBox` | GemBox.Pdf | Skeleton | — |
| `ActivePdf` | ActivePDF | Skeleton | — |
| `Leadtools` | LEADTOOLS | Skeleton | — |
| `PdfKitNet` | PDFKit.NET | Skeleton | — |

---

## Frontend Components

| File | Purpose |
|------|---------|
| `ui-designer-v2/src/pages/MigrationsPage.tsx` | Full page: framework selector, source/output split, diagnostics, PDF preview |
| `ui-designer-v2/src/styles/migrations.css` | All styles under `mgr-` prefix; PDF preview 860 px tall |
| `ui-designer-v2/src/styles/index.css` | `@import './migrations.css'` added |
| `ui-designer-v2/src/components/Layout/AppHeader.tsx` | `activePage` extended with `'migrations'`; nav link to `/migrations` |
| `ui-designer-v2/src/App.tsx` | `<Route path="/migrations" element={<MigrationsPage />} />` added |
| `ui-designer-v2/vite.config.ts` | Vite proxy: `/api` → `http://localhost:5086` |

### Key Frontend Behaviours

- **Framework switch** clears source code, output, diagnostics and PDF preview.
- **Load example** button appears only for frameworks that have a bundled example; loads the correct snippet for the selected framework.
- **Dropdown label** appends `(pilot)` or `(skeleton)` so users can see maturity at a glance.
- **PDF preview iframe** is 860 px tall to show a full A4 page without scrolling.

---

## Backend API

Base path: `Canvas.WebApi/Controllers/MigrationController.cs`  
Backend port: **5086** (proxied via Vite `/api`)

### `GET /api/migration/frameworks`

Returns metadata for all supported frameworks (id, name, status, description).

### `POST /api/migration/convert`

```json
// Request
{ "framework": "Apryse", "sourceCode": "..." }

// Response
{
  "canvasCode": "using Canvas.Pdf;\n...",
  "summary": { "convertedCount": 4, "warningCount": 0, "errorCount": 0, "totalDiagnostics": 4 },
  "diagnostics": [
    { "code": "CANMIGAPRYSE001", "severity": "Info", "message": "new PDFDoc() → new PdfDocument()" }
  ]
}
```

### `POST /api/migration/preview`

Same request body as `/convert`. Returns `application/pdf` bytes.

---

## Backend Services

| File | Purpose |
|------|---------|
| `Canvas.WebApi/Services/ICodeConverter.cs` | Interface: `FrameworkId`, `FrameworkName`, `Status`, `Description`, `ConvertCode`, `GeneratePreview`, `GetDiagnostics` |
| `Canvas.WebApi/Services/MigrationDiagnostic.cs` | Record: `Code`, `Severity`, `Message` |
| `Canvas.WebApi/Services/MigrationResult.cs` | Record: `CanvasCode`, `Diagnostics`, `Summary` |
| `Canvas.WebApi/Services/MigrationService.cs` | Singleton — dispatches to the right converter by framework ID |
| `Canvas.WebApi/Services/Converters/BasePdfConverter.cs` | Abstract base; `DrawPreviewChrome`, `ReplayCanvasCalls`, `GeneratePreview` shared logic |
| `Canvas.WebApi/Services/Converters/SyncfusionPdfConverter.cs` | Roslyn-based full conversion |
| `Canvas.WebApi/Services/Converters/IText7PdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/AprysePdfConverter.cs` | Roslyn-based full conversion |
| `Canvas.WebApi/Services/Converters/AsposePdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/DsPdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/FoxitPdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/DevExpressPdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/IronPdfConverter.cs` | Roslyn-based pilot |
| `Canvas.WebApi/Services/Converters/SpirePdfConverter.cs` | Skeleton |
| `Canvas.WebApi/Services/Converters/GemBoxPdfConverter.cs` | Skeleton |
| `Canvas.WebApi/Services/Converters/ActivePdfConverter.cs` | Skeleton |
| `Canvas.WebApi/Services/Converters/LeadtoolsPdfConverter.cs` | Skeleton |
| `Canvas.WebApi/Services/Converters/PdfKitNetConverter.cs` | Skeleton |
| `Canvas.WebApi/Program.cs` | `AddSingleton<MigrationService>()` registered |

---

## PDF Preview — Document Layout

Every preview PDF is generated by `BasePdfConverter.GeneratePreview` and uses `DrawPreviewChrome`:

| Region | Content |
|--------|---------|
| **Left strip** (22 pt wide, blue) | "Code migration for {Framework Name}" — white text, rotated 90° |
| **Footer bar** (28 pt tall, blue) | "Generated with Canvas.Pdf · The modern .NET PDF library · canvas-pdf.io" — white text |
| **Content area** | Replayed Canvas draw calls from the converted code |

### Multi-page support

`GeneratePreview` counts `document.AddPage()` calls in the converted code and creates that many pages. Each page that has no replayed draw calls shows:

> **Content requires manual migration.**  
> The converted code structure is correct — see the code panel for the draw calls to add.

### `ReplayCanvasCalls` — supported patterns

| Pattern parsed | Canvas method called |
|---------------|---------------------|
| `page.DrawTextFromTop("…", x: X, topY: Y, fontSize: F)` | `DrawTextFromTop` |
| `page.DrawTextFromTop("…", X, Y, F)` | `DrawTextFromTop` |
| `page.DrawText("…", X, Y[, F])` | `DrawText` (bottom-left coords) |
| `page.DrawLineFromTop(x1, y1, x2, y2)` | `DrawLineFromTop` |
| `page.DrawLine(x1, y1, x2, y2)` | `DrawLine` (bottom-left coords) |
| `page.DrawRectangleFromTop(x, y, w, h)` | `DrawRectangleFromTop` |
| `page.DrawRectangle(x, y, w, h, lw, fill)` | `DrawRectangle` (bottom-left coords) |

---

## Conversion Mapping — Syncfusion PDF (Full)

| Syncfusion pattern | Canvas.Pdf equivalent |
|---|---|
| `using Syncfusion.Pdf;` | `using Canvas.Pdf;` |
| `using Syncfusion.Pdf.Graphics;` | *(removed)* |
| `new PdfDocument()` | `new PdfDocument()` |
| `document.Pages.Add()` | `document.AddPage()` |
| `page.Graphics.DrawString(text, font, brush, x, y)` | `page.DrawTextFromTop(text, x, topY, fontSize)` |
| `page.Graphics.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLineFromTop(x1, y1, x2, y2, lineWidth)` |
| `page.Graphics.DrawRectangle(pen/brush, x, y, w, h)` | `page.DrawRectangleFromTop(x, y, w, h, …)` |
| `page.Graphics.DrawImage(img, x, y, w, h)` | `page.DrawImageFromTop(…)` |
| `document.Save(path/stream)` | `document.Save(path/stream)` |
| `document.Close(…)` | *(removed)* |

---

## Conversion Mapping — Apryse (PDFTron) (Full)

| Apryse pattern | Canvas.Pdf equivalent |
|---|---|
| `using pdftron;` / `using pdftron.PDF;` / `using pdftron.SDF;` | *(removed)* → `using Canvas.Pdf;` added |
| `PDFNet.Initialize(…)` | *(removed)* — Canvas.Pdf requires no SDK initialisation |
| `using var doc = new PDFDoc()` | `var document = new PdfDocument()` |
| `var page = doc.PageCreate(…)` | *(removed)* — AddPage() creates and attaches in one step |
| `doc.PagePushBack(page)` | `var page = document.AddPage()` (uses the pushed variable name) |
| `doc.Save(path, SDFDoc.SaveOptions.…)` | `document.Save(path)` — save flags removed |
| ElementBuilder / ElementWriter / CreateText* | Kept as-is — manual migration; warnings emitted |
| SDF / annotations / forms / OCR / signatures | Kept as-is — out of v1 scope; warnings emitted |

---

## Implementation Status

| Task | Status |
|------|--------|
| `/migrations` route in `ui-designer-v2` | ✅ Done |
| `MigrationsPage.tsx` with split panes, diagnostics, preview | ✅ Done |
| `migrations.css` (860 px preview, `mgr-` prefix) | ✅ Done |
| `AppHeader.tsx` nav link | ✅ Done |
| Vite proxy `/api` → port 5086 | ✅ Done |
| Framework switch clears source code | ✅ Done |
| Framework-aware Load example button | ✅ Done |
| Full/Pilot/Skeleton badge system | ✅ Done |
| Bundled examples for 8 frameworks | ✅ Done |
| `ICodeConverter` interface + DTOs | ✅ Done |
| `MigrationService` singleton | ✅ Done |
| `MigrationController` (3 endpoints) | ✅ Done |
| `BasePdfConverter` — `DrawPreviewChrome` (blue strip + footer) | ✅ Done |
| `BasePdfConverter` — `ReplayCanvasCalls` (7 patterns) | ✅ Done |
| `BasePdfConverter` — multi-page preview (counts `AddPage()`) | ✅ Done |
| `SyncfusionPdfConverter` — Roslyn-backed full conversion | ✅ Done |
| `IText7PdfConverter` — Roslyn-backed full conversion (PdfWriter+Document → PdfDocument, Paragraph/SetFontSize → DrawTextFromTop, Close/SetMargins removed, PdfCanvas drawing) | ✅ Done |
| `AprysePdfConverter` — Roslyn-backed full conversion (PDFDoc → PdfDocument, PageCreate+PagePushBack → AddPage, Save) | ✅ Done |
| `AsposePdfConverter` — Roslyn-backed full conversion (Document → PdfDocument, TextFragment/TextBuilder → DrawText/DrawTextFromTop) | ✅ Done |
| `DsPdfConverter` — Roslyn-backed full conversion (GcPdfDocument → PdfDocument, NewPage → AddPage, DrawString/DrawLine/DrawRectangle/FillRectangle via Graphics → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop, Save) | ✅ Done |
| `FoxitPdfConverter` — Roslyn-backed full conversion (PDFDoc → PdfDocument, InsertPage → AddPage, Library.Initialize + GetGraphics/GenerateContent removed, graphics.DrawText/DrawLine/DrawRect/FillRect → Draw*FromTop, Save/SaveAs → Save) | ✅ Done |
| `DevExpressPdfConverter` — Roslyn-backed full conversion (PdfDocumentProcessor → PdfDocument, draw calls repositioned after AddPage, SaveDocument → Save) | ✅ Done |
| `IronPdfConverter` — Roslyn-backed pilot (ChromePdfRenderer/HtmlToPdf → PdfDocument scaffold; SaveAs → Save; HTML/URL/Razor render calls removed with diagnostics) | ✅ Done |
| 5 skeleton converters (Spire, GemBox, ActivePdf, LEADTOOLS, PDFKit.NET) | ✅ Done |
| `Program.cs` DI registration | ✅ Done |
| `bin/` `obj/` removed from git tracking | ✅ Done |

---

## Verification Steps

1. `dotnet run --project Canvas.WebApi` — API starts on http://localhost:5086
2. `cd ui-designer-v2 && npm run dev` — UI starts on http://localhost:5174
3. Open http://localhost:5174 → click **Migrations** in nav
4. Select **Syncfusion PDF** → click **Load example** → click **Convert →**
   - Canvas code appears; diagnostics show info/warning chips
5. Click **Generate Preview** → 860 px iframe renders PDF with blue strip + footer
6. Select **Apryse (PDFTron)** → source code clears automatically
7. Click **Load example** → realistic PDFDoc + two pages + ElementBuilder sample loads
8. Click **Convert →** → `PDFNet.Initialize` removed; `PDFDoc` → `PdfDocument`; two `AddPage()` calls; `doc.Save(…flags)` → `document.Save(path)`
9. Click **Generate Preview** → PDF with **2 pages** renders; each page shows "Content requires manual migration"
10. Select **iText7** → click **Load example** → convert → preview shows rendered text and lines
11. Select **DevExpress PDF** → convert → preview shows "Content requires manual migration" (reporting pilot)
