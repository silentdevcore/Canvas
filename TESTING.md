# Testing Strategy

## Test Project Matrix

| Group | Projects | Focus |
|-------|----------|-------|
| Core and application | `tests/PXA.Core.Tests`, `tests/PXA.Application.Tests` | Contracts, primitives, expression engine, formatters, use-case orchestration |
| API | `tests/PXA.Api.Tests` | Controller integration and WebApi wiring |
| PDF engine | `tests/PXA.Infrastructure.Pdf.Tests` | PXA PDF writer, encryption, diagnostics, PDF rendering services, golden snapshots |
| Export integration | `tests/PXA.Export.Tests` | DOCX, ODT, XLSX, HTML, CSV, Markdown, PDF, image/TIFF, localization, security, fidelity |
| PDF importer SDK | `tests/PXA.Importer.Tests` | Tokenizer, object parsing, stream/content parsing, graphics interpretation, editable model |
| File importers | `tests/PXA.FileImporter.Tests`, `tests/PXA.FileImporter.ImageAnalysis.Tests`, `tests/PXA.FileImporter.ImageOcr.Tests` | File-to-`DesignExportDto` adapters, raster analysis, OCR pipeline |
| PDF migration providers | `tests/PXA.Migration.*.Tests` provider projects | Vendor C# PDF code migration to PXA PDF code |
| Report migration | `tests/PXA.Migration.Report.Designer.DevExpress.Tests`, `tests/PXA.Migration.Report.Designer.Rdl.Tests`, `tests/PXA.Migration.Report.Designer.Rpx.Tests`, plus the other report provider suites | Report source to editable PXA design |
| Frontend | `ui-designer-v2` Jest tests | Editor, services, template utilities, export service, validation |

## Running Tests

```bash
# All .NET projects
dotnet build PXA.sln
dotnet test PXA.sln

# Core/application/API
dotnet test tests/PXA.Core.Tests
dotnet test tests/PXA.Application.Tests
dotnet test tests/PXA.Api.Tests

# PDF engine and export integration
dotnet test tests/PXA.Infrastructure.Pdf.Tests
dotnet test tests/PXA.Export.Tests

# Importers
dotnet test tests/PXA.Importer.Tests
dotnet test tests/PXA.FileImporter.Tests
dotnet test tests/PXA.FileImporter.ImageAnalysis.Tests
dotnet test tests/PXA.FileImporter.ImageOcr.Tests

# Migration aggregators and examples
dotnet test tests/PXA.Migration.Pdf.Tests
dotnet test tests/PXA.Migration.Spreadsheet.Tests
dotnet test tests/PXA.Migration.Report.Tests
dotnet test tests/PXA.Migration.Pdf.Code.DevExpress.Tests
dotnet test tests/PXA.Migration.Report.Designer.DevExpress.Tests
dotnet test tests/PXA.Migration.Report.Designer.Rdl.Tests
dotnet test tests/PXA.Migration.Report.Designer.Rpx.Tests
```

Frontend:

```bash
cd ui-designer-v2
npm test
npm run test:coverage
npm run build
```

## Recommended Scope By Change Type

| Change type | Minimum validation |
|-------------|--------------------|
| Documentation only | Link/readability check; no build required unless commands or APIs changed |
| Core contracts/DTOs | `PXA.Core.Tests`, affected export/import/migration tests, `dotnet build PXA.sln` |
| PDF engine/writer | `PXA.Infrastructure.Pdf.Tests`, `PXA.Export.Tests`, affected API tests |
| Exporter | `PXA.Export.Tests`, affected infrastructure tests, API smoke test if endpoint behavior changed |
| File importer | matching PXA importer test project, API import smoke test |
| PDF importer engine | `PXA.Importer.Tests`, file-importer PDF coverage, PDF import API tests |
| Image analysis/OCR | image-analysis/OCR test projects plus endpoint smoke test if WebApi changed |
| Migration provider | matching `PXA.Migration.<Provider>.Tests`, migration API smoke test if registration changed |
| Report migration | matching report migration tests plus one end-to-end render test where practical |
| Frontend UI/service | `cd ui-designer-v2 && npm test`, `npm run build` |

## Golden Snapshot Workflow

Primary PDF snapshot details live in:

```text
tests/PXA.Infrastructure.Pdf.Tests/SNAPSHOT_TESTING.md
```

When intentionally updating a snapshot:

1. Run the focused PDF test and capture the produced hash or diff from the failure output.
2. Verify that the output change is expected and acceptable.
3. Update the expected hash or fixture.
4. Re-run `tests/PXA.Infrastructure.Pdf.Tests`.
5. Re-run broader export tests if the change affects common rendering behavior.
6. Document the reason in PR notes or the related checklist.

## CI Expectations

CI should validate:

1. `dotnet build PXA.sln`
2. .NET test projects relevant to the changed areas
3. full `dotnet test PXA.sln` for shared contracts, rendering, API, importer, or migration infrastructure changes
4. `cd ui-designer-v2 && npm run build`
5. frontend tests when UI/service/template code changes

## Notes

- `PXA.*` projects and namespaces were removed in the PXA breaking rename. Use `PXA.*` projects and namespaces for active code.
- `CANMIG...` diagnostic identifiers remain stable even though provider namespaces are now `PXA.Migration.*`.
- Many migration providers are intentionally conservative. Tests should assert diagnostics for unsupported APIs instead of forcing unsafe rewrites.
- Report converters should test both element mapping and final renderability where possible.
- Importer tests should prefer small, stable fixtures and assert semantic `DesignExportDto` output instead of brittle byte-for-byte comparisons.
