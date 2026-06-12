# Removing PdfImporter & SvgPdfImporter

## Analysis

### Why removing them
Consolidating to the single custom Canvas.Importer engine which covers all use cases
and gives maximum fidelity (SVG data-URIs, font metadata, element classification).

### What each removed importer was

|  | PdfImporter | SvgPdfImporter |
|---|---|---|
| File | `src/Canvas.Infrastructure.Converters/PdfImporter.cs` | `src/Canvas.Infrastructure.Converters/SvgPdfImporter.cs` |
| Library | UglyToad.PdfPig 1.7.0-custom-5 | PdfToSvg.NET 1.8.0 |
| API endpoint | `POST /api/document/import-pdf` | `POST /api/document/import-pdf-svg` |
| Lines of code | ~762 | ~1044 |
| Table detection | Yes (H/V line clustering) | Yes (H/V line clustering) |
| Vector curves | No | No |
| Font metadata | Basic | Basic |

### What is kept
`CanvasImporterPdfImporter` → `POST /api/document/import-pdf-engine`  
Uses the fully custom Canvas.Importer engine (40+ files in `src/Canvas.Importer/`).

| Feature | Canvas.Importer engine |
|---|---|
| Library | Custom (owned) |
| Vector curves | Yes — SVG data-URIs |
| Font metadata | Full (embedded bytes, ToUnicode, display name) |
| Element classification | Text, VectorIcon, SymbolFontIcon, Image, Barcode, Separator, TableLine |
| Parallel page parsing | Yes |
| Editable PDF DOM | Yes (text replace, delete mutations) |

---

## Checklist

### Backend
- [x] Delete `src/Canvas.Infrastructure.Converters/PdfImporter.cs`
- [x] Delete `src/Canvas.Infrastructure.Converters/SvgPdfImporter.cs`
- [x] Remove `UglyToad.PdfPig` from `Canvas.Infrastructure.Converters.csproj`
- [x] Remove `PdfToSvg.NET` from `Canvas.Infrastructure.Converters.csproj`
- [x] Remove `POST /api/document/import-pdf` endpoint from `DocumentOpsController.cs`
- [x] Remove `POST /api/document/import-pdf-svg` endpoint from `DocumentOpsController.cs`
- [x] Remove `POST /api/document/debug-pdf-svg` endpoint from `DocumentOpsController.cs`
- [x] Remove related `using` statements in `DocumentOpsController.cs`

### Frontend
- [x] Remove `ExportService.importPdfSvg()` from `ExportService.ts`
- [x] Remove `ExportService.debugPdfSvg()` from `ExportService.ts`
- [x] Redirect `ExportService.importPdf()` to `import-pdf-engine`
- [x] Remove `loadFromFileSvg()` function from `useTemplateLoader.ts`
- [x] Remove `loadFromFilePdfEngine()` function from `useTemplateLoader.ts`
- [x] Remove "Import PDF (SVG)" button + handler from `TemplatePage.tsx`
- [x] Remove "Debug SVG" button + handler from `TemplatePage.tsx`
- [x] Remove "Import PDF (Engine)" button from `TemplatePage.tsx`
- [x] Update `/api/document/import-pdf` → `import-pdf-engine` row in `DocsPage.tsx`

### Verification
- [x] `dotnet build` passes — 0 errors
- [ ] `dotnet test tests/Canvas.Importer.Tests` all pass
- [ ] PDF import via frontend works end-to-end through the Canvas engine
