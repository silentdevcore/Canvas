# Importer Project Refactoring Checklist

Scope: split every file-format importer into its own self-contained project, following the `PXA.Migration.*` pattern. Each importer gets a dedicated `src/Importing/PXA.FileImporter.<Format>/` project and a corresponding `tests/PXA.FileImporter.<Format>.Tests/` project.

---

## Abstractions

- [ ] Create `src/Importing/PXA.FileImporter.Abstractions/PXA.FileImporter.Abstractions.csproj` (references only PXA.Core).
- [ ] Create `IFileImporter` interface with `SupportedExtensions` and `ImportAsync(Stream, string?)`.
- [ ] Add `PXA.FileImporter.Abstractions` to `PXA.sln`.

---

## PXA.FileImporter.Pdf

- [ ] Create `src/Importing/PXA.FileImporter.Pdf/PXA.FileImporter.Pdf.csproj` (references Abstractions, PXA.Core, PXA.Importer).
- [ ] Move `PdfFileImporter.cs` → `PdfFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pdf"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Pdf.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Docx

- [ ] Create `src/Importing/PXA.FileImporter.Docx/PXA.FileImporter.Docx.csproj` (references Abstractions, PXA.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `DocxImporter.cs` → `DocxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["docx"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Docx.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Pptx

- [ ] Create `src/Importing/PXA.FileImporter.Pptx/PXA.FileImporter.Pptx.csproj` (references Abstractions, PXA.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `PptxImporter.cs` → `PptxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pptx"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Pptx.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Doc

- [ ] Create `src/Importing/PXA.FileImporter.Doc/PXA.FileImporter.Doc.csproj` (references Abstractions, PXA.Core; no extra NuGet).
- [ ] Move `DocImporter.cs` → `DocFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["doc"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Doc.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Odt

- [ ] Create `src/Importing/PXA.FileImporter.Odt/PXA.FileImporter.Odt.csproj` (references Abstractions, PXA.Core; no extra NuGet).
- [ ] Move `OdtImporter.cs` → `OdtFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["odt"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Odt.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Svg

- [ ] Create `src/Importing/PXA.FileImporter.Svg/PXA.FileImporter.Svg.csproj` (references Abstractions, PXA.Core; no extra NuGet).
- [ ] Move `SvgImporter.cs` → `SvgFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["svg"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Svg.Tests/` with basic round-trip test.

---

## PXA.FileImporter.Image

- [ ] Create `src/Importing/PXA.FileImporter.Image/PXA.FileImporter.Image.csproj` (references Abstractions, PXA.Core; NuGet: SkiaSharp).
- [ ] Move `ImageImporter.cs` → `ImageFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["png","jpg","jpeg","gif","webp","bmp","tiff","tif"]`.
- [ ] Add project to `PXA.sln` and `PXA.WebApi/PXA.WebApi.csproj`.
- [ ] Create `tests/PXA.FileImporter.Image.Tests/` with basic round-trip test.

---

## Source cleanup

- [ ] Delete `DocImporter.cs`, `OdtImporter.cs`, `ImageImporter.cs`, `SvgImporter.cs`, `PdfFileImporter.cs` from `PXA.Infrastructure.Converters`.
- [ ] Remove `SkiaSharp` from `PXA.Infrastructure.Converters.csproj` if no longer used.
- [ ] Remove `PXA.Importer` project reference from `PXA.Infrastructure.Converters.csproj` if no longer used.
- [ ] Delete `DocxImporter.cs`, `PptxImporter.cs` from `PXA.Infrastructure.Word`.

---

## DI and controller wiring

- [ ] Register all 7 `IFileImporter` implementations in `PXA.WebApi/Program.cs`.
- [ ] Inject `IEnumerable<IFileImporter>` into `DocumentOpsController`; dispatch each import endpoint via `importers.Single(i => i.SupportedExtensions.Contains(ext)).ImportAsync(stream, name)`.

---

## Verification

- [ ] `dotnet build PXA.sln` — 0 errors.
- [ ] Each importer project builds in isolation.
- [ ] POST PDF to `/api/document/import-pdf-engine` → valid `DesignExportDto`.
- [ ] POST DOCX to `/api/document/import-docx` → valid `DesignExportDto`.
- [ ] POST PPTX to `/api/document/import-pptx` → valid `DesignExportDto`.
- [ ] POST SVG to `/api/document/import-svg` → valid `DesignExportDto`.
- [ ] `PXA.Api.Tests` — all existing tests pass.
