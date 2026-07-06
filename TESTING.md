# Testing Strategy

## Test Project Matrix

| Group | Projects | Focus |
|-------|----------|-------|
| Core and application | `tests/Canvas.Core.Tests`, `tests/Canvas.Application.Tests` | Contracts, primitives, expression engine, formatters, use-case orchestration |
| API | `tests/Canvas.Api.Tests` | Controller integration and WebApi wiring |
| PDF engine | `tests/Canvas.Infrastructure.Pdf.Tests` | PXA-compatible PDF writer, encryption, diagnostics, PDF rendering services, golden snapshots |
| Export integration | `tests/Canvas.Export.Tests` | DOCX, ODT, XLSX, HTML, CSV, Markdown, PDF, image/TIFF, localization, security, fidelity |
| PDF importer SDK | `tests/Canvas.Importer.Tests` | Tokenizer, object parsing, stream/content parsing, graphics interpretation, editable model |
| File importers | `tests/Canvas.FileImporter.Pdf.Tests`, `Docx`, `Doc`, `Odt`, `Pptx`, `Svg`, `Image` | File-to-`DesignExportDto` adapters |
| Image analysis/OCR | `tests/Canvas.FileImporter.ImageAnalysis.Tests`, `tests/Canvas.FileImporter.ImageOcr.Tests` | Raster analysis, OCR pipeline, visual fusion, image-to-PDF behavior |
| PDF migration providers | `tests/Canvas.Migration.*.Tests` provider projects | Vendor C# PDF code migration to PXA-compatible PDF code |
| Report migration | `tests/Canvas.Migration.DevExpressReport.Tests`, `tests/Canvas.Migration.Rdl.Tests`, `tests/Canvas.Migration.Rpx.Tests` | Report source to editable PXA design |
| Frontend | `ui-designer-v2` Jest tests | Editor, services, template utilities, export service, validation |

---

## Running Tests

```bash
# All .NET tests
dotnet test Canvas.sln

# Build only
dotnet build Canvas.sln

# Core/application/API
dotnet test tests/Canvas.Core.Tests
dotnet test tests/Canvas.Application.Tests
dotnet test tests/Canvas.Api.Tests

# PDF engine and export integration
dotnet test tests/Canvas.Infrastructure.Pdf.Tests
dotnet test tests/Canvas.Export.Tests

# PDF importer SDK
dotnet test tests/Canvas.Importer.Tests

# File importers
dotnet test tests/Canvas.FileImporter.Pdf.Tests
dotnet test tests/Canvas.FileImporter.Docx.Tests
dotnet test tests/Canvas.FileImporter.Doc.Tests
dotnet test tests/Canvas.FileImporter.Odt.Tests
dotnet test tests/Canvas.FileImporter.Pptx.Tests
dotnet test tests/Canvas.FileImporter.Svg.Tests
dotnet test tests/Canvas.FileImporter.Image.Tests

# Image analysis / OCR
dotnet test tests/Canvas.FileImporter.ImageAnalysis.Tests
dotnet test tests/Canvas.FileImporter.ImageOcr.Tests

# Example migration provider
dotnet test tests/Canvas.Migration.DevExpressPdf.Tests

# Report migrations
dotnet test tests/Canvas.Migration.DevExpressReport.Tests
dotnet test tests/Canvas.Migration.Rdl.Tests
dotnet test tests/Canvas.Migration.Rpx.Tests
```

Frontend:

```bash
cd ui-designer-v2
npm test
npm run test:coverage
npm run build
```

---

## Recommended Scope By Change Type

| Change type | Minimum validation |
|-------------|--------------------|
| Documentation only | Link/readability check; no build required unless commands or APIs changed |
| Core contracts/DTOs | `Canvas.Core.Tests`, affected export/import/migration tests, `dotnet build Canvas.sln` |
| PDF engine/writer | `Canvas.Infrastructure.Pdf.Tests`, `Canvas.Export.Tests`, affected API tests |
| Exporter | `Canvas.Export.Tests`, affected infrastructure tests, API smoke test if endpoint behavior changed |
| File importer | matching `Canvas.FileImporter.<Format>.Tests`, API import smoke test |
| PDF importer engine | `Canvas.Importer.Tests`, `Canvas.FileImporter.Pdf.Tests`, PDF import API tests |
| Image analysis/OCR | matching image test project plus endpoint smoke test if WebApi changed |
| Migration provider | matching `Canvas.Migration.<Provider>.Tests`, migration API smoke test if registration changed |
| Report migration | matching report migration tests plus one end-to-end render test where practical |
| Frontend UI/service | `cd ui-designer-v2 && npm test`, `npm run build` |

---

## Golden Snapshot Workflow (PDF)

Primary snapshot details live in:

```text
tests/Canvas.Infrastructure.Pdf.Tests/SNAPSHOT_TESTING.md
```

When intentionally updating a snapshot:

1. Run the focused PDF test and capture the produced hash or diff from the failure output.
2. Verify that the output change is expected and acceptable.
3. Update the expected hash or fixture.
4. Re-run `tests/Canvas.Infrastructure.Pdf.Tests`.
5. Re-run broader export tests if the change affects common rendering behavior.
6. Document the reason in PR notes or the related checklist.

---

## CI Expectations

CI should validate:

1. `dotnet build Canvas.sln`
2. .NET test projects relevant to the changed areas
3. full `dotnet test Canvas.sln` for shared contracts, rendering, API, importer, or migration infrastructure changes
4. `cd ui-designer-v2 && npm run build`
5. frontend tests when UI/service/template code changes

---

## Notes

- Many migration providers are intentionally conservative. Tests should assert diagnostics for unsupported APIs instead of forcing unsafe rewrites.
- Report converters should test both element mapping and final renderability where possible.
- Importer tests should prefer small, stable fixtures and assert semantic `DesignExportDto` output instead of brittle byte-for-byte comparisons.
