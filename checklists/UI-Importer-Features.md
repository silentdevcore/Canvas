# Importer Project Refactoring Checklist

Scope: split every file-format importer into its own self-contained project, following the `Canvas.Migration.*` pattern. Each importer gets a dedicated `src/Canvas.FileImporter.<Format>/` project and a corresponding `tests/Canvas.FileImporter.<Format>.Tests/` project.

---

## Abstractions

- [ ] Create `src/Canvas.FileImporter.Abstractions/Canvas.FileImporter.Abstractions.csproj` (references only Canvas.Core).
- [ ] Create `IFileImporter` interface with `SupportedExtensions` and `ImportAsync(Stream, string?)`.
- [ ] Add `Canvas.FileImporter.Abstractions` to `Canvas.sln`.

---

## Canvas.FileImporter.Pdf

- [ ] Create `src/Canvas.FileImporter.Pdf/Canvas.FileImporter.Pdf.csproj` (references Abstractions, Canvas.Core, Canvas.Importer).
- [ ] Move `CanvasImporterPdfImporter.cs` → `PdfFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pdf"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Pdf.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Docx

- [ ] Create `src/Canvas.FileImporter.Docx/Canvas.FileImporter.Docx.csproj` (references Abstractions, Canvas.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `DocxImporter.cs` → `DocxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["docx"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Docx.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Pptx

- [ ] Create `src/Canvas.FileImporter.Pptx/Canvas.FileImporter.Pptx.csproj` (references Abstractions, Canvas.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `PptxImporter.cs` → `PptxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pptx"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Pptx.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Doc

- [ ] Create `src/Canvas.FileImporter.Doc/Canvas.FileImporter.Doc.csproj` (references Abstractions, Canvas.Core; no extra NuGet).
- [ ] Move `DocImporter.cs` → `DocFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["doc"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Doc.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Odt

- [ ] Create `src/Canvas.FileImporter.Odt/Canvas.FileImporter.Odt.csproj` (references Abstractions, Canvas.Core; no extra NuGet).
- [ ] Move `OdtImporter.cs` → `OdtFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["odt"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Odt.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Svg

- [ ] Create `src/Canvas.FileImporter.Svg/Canvas.FileImporter.Svg.csproj` (references Abstractions, Canvas.Core; no extra NuGet).
- [ ] Move `SvgImporter.cs` → `SvgFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["svg"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Svg.Tests/` with basic round-trip test.

---

## Canvas.FileImporter.Image

- [ ] Create `src/Canvas.FileImporter.Image/Canvas.FileImporter.Image.csproj` (references Abstractions, Canvas.Core; NuGet: SkiaSharp).
- [ ] Move `ImageImporter.cs` → `ImageFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["png","jpg","jpeg","gif","webp","bmp","tiff","tif"]`.
- [ ] Add project to `Canvas.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/Canvas.FileImporter.Image.Tests/` with basic round-trip test.

---

## Source cleanup

- [ ] Delete `DocImporter.cs`, `OdtImporter.cs`, `ImageImporter.cs`, `SvgImporter.cs`, `CanvasImporterPdfImporter.cs` from `Canvas.Infrastructure.Converters`.
- [ ] Remove `SkiaSharp` from `Canvas.Infrastructure.Converters.csproj` if no longer used.
- [ ] Remove `Canvas.Importer` project reference from `Canvas.Infrastructure.Converters.csproj` if no longer used.
- [ ] Delete `DocxImporter.cs`, `PptxImporter.cs` from `Canvas.Infrastructure.Word`.

---

## DI and controller wiring

- [ ] Register all 7 `IFileImporter` implementations in `PXA.WebApi/Program.cs`.
- [ ] Inject `IEnumerable<IFileImporter>` into `DocumentOpsController`; dispatch each import endpoint via `importers.Single(i => i.SupportedExtensions.Contains(ext)).ImportAsync(stream, name)`.

---

## Verification

- [ ] `dotnet build Canvas.sln` — 0 errors.
- [ ] Each importer project builds in isolation.
- [ ] POST PDF to `/api/document/import-pdf-engine` → valid `DesignExportDto`.
- [ ] POST DOCX to `/api/document/import-docx` → valid `DesignExportDto`.
- [ ] POST PPTX to `/api/document/import-pptx` → valid `DesignExportDto`.
- [ ] POST SVG to `/api/document/import-svg` → valid `DesignExportDto`.
- [ ] `Canvas.Api.Tests` — all existing tests pass.
