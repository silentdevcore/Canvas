# Importer Project Refactoring Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Current headings, goals and open tasks use PVAD/PVADE. Checked records, original examples and compatibility identifiers retain their historical names; task statuses are unchanged.

Scope: split every file-format importer into its own self-contained project, following the `PVAD.Migration.*` pattern. Each importer gets a dedicated `src/Importing/PVAD.FileImporter.<Format>/` project and a corresponding `tests/PVAD.FileImporter.<Format>.Tests/` project.

---

## Abstractions

- [ ] Create `src/Importing/PVAD.FileImporter.Abstractions/PVAD.FileImporter.Abstractions.csproj` (references only PVAD.Core).
- [ ] Create `IFileImporter` interface with `SupportedExtensions` and `ImportAsync(Stream, string?)`.
- [ ] Add `PVAD.FileImporter.Abstractions` to `PVAD.sln`.

---

## PVAD.FileImporter.Pdf

- [ ] Create `src/Importing/PVAD.FileImporter.Pdf/PVAD.FileImporter.Pdf.csproj` (references Abstractions, PVAD.Core, PVAD.Importer).
- [ ] Move `PdfFileImporter.cs` → `PdfFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pdf"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Pdf.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Docx

- [ ] Create `src/Importing/PVAD.FileImporter.Docx/PVAD.FileImporter.Docx.csproj` (references Abstractions, PVAD.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `DocxImporter.cs` → `DocxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["docx"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Docx.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Pptx

- [ ] Create `src/Importing/PVAD.FileImporter.Pptx/PVAD.FileImporter.Pptx.csproj` (references Abstractions, PVAD.Core; NuGet: DocumentFormat.OpenXml).
- [ ] Move `PptxImporter.cs` → `PptxFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["pptx"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Pptx.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Doc

- [ ] Create `src/Importing/PVAD.FileImporter.Doc/PVAD.FileImporter.Doc.csproj` (references Abstractions, PVAD.Core; no extra NuGet).
- [ ] Move `DocImporter.cs` → `DocFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["doc"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Doc.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Odt

- [ ] Create `src/Importing/PVAD.FileImporter.Odt/PVAD.FileImporter.Odt.csproj` (references Abstractions, PVAD.Core; no extra NuGet).
- [ ] Move `OdtImporter.cs` → `OdtFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["odt"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Odt.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Svg

- [ ] Create `src/Importing/PVAD.FileImporter.Svg/PVAD.FileImporter.Svg.csproj` (references Abstractions, PVAD.Core; no extra NuGet).
- [ ] Move `SvgImporter.cs` → `SvgFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["svg"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Svg.Tests/` with basic round-trip test.

---

## PVAD.FileImporter.Image

- [ ] Create `src/Importing/PVAD.FileImporter.Image/PVAD.FileImporter.Image.csproj` (references Abstractions, PVAD.Core; NuGet: SkiaSharp).
- [ ] Move `ImageImporter.cs` → `ImageFileImporter.cs`; rename class; implement `IFileImporter`; `SupportedExtensions = ["png","jpg","jpeg","gif","webp","bmp","tiff","tif"]`.
- [ ] Add project to `PVAD.sln` and `PVAD.WebApi/PVAD.WebApi.csproj`.
- [ ] Create `tests/PVAD.FileImporter.Image.Tests/` with basic round-trip test.

---

## Source cleanup

- [ ] Delete `DocImporter.cs`, `OdtImporter.cs`, `ImageImporter.cs`, `SvgImporter.cs`, `PdfFileImporter.cs` from `PVAD.Infrastructure.Converters`.
- [ ] Remove `SkiaSharp` from `PVAD.Infrastructure.Converters.csproj` if no longer used.
- [ ] Remove `PVAD.Importer` project reference from `PVAD.Infrastructure.Converters.csproj` if no longer used.
- [ ] Delete `DocxImporter.cs`, `PptxImporter.cs` from `PVAD.Infrastructure.Word`.

---

## DI and controller wiring

- [ ] Register all 7 `IFileImporter` implementations in `PVAD.WebApi/Program.cs`.
- [ ] Inject `IEnumerable<IFileImporter>` into `DocumentOpsController`; dispatch each import endpoint via `importers.Single(i => i.SupportedExtensions.Contains(ext)).ImportAsync(stream, name)`.

---

## Verification

- [ ] `dotnet build PVAD.sln` — 0 errors.
- [ ] Each importer project builds in isolation.
- [ ] POST PDF to `/api/document/import-pdf-engine` → valid `DesignExportDto`.
- [ ] POST DOCX to `/api/document/import-docx` → valid `DesignExportDto`.
- [ ] POST PPTX to `/api/document/import-pptx` → valid `DesignExportDto`.
- [ ] POST SVG to `/api/document/import-svg` → valid `DesignExportDto`.
- [ ] `PVAD.Api.Tests` — all existing tests pass.
