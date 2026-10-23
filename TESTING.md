# Testing Strategy

## Test Project Matrix

| Group | Projects | Focus |
|-------|----------|-------|
| Core and application | `tests/PVAD.Core.Tests`, `tests/PVAD.Application.Tests` | Contracts, primitives, expression engine, formatters, use-case orchestration |
| API | `tests/PVAD.Api.Tests` | Controller integration and WebApi wiring |
| PDF engine | `tests/PVAD.Infrastructure.Pdf.Tests` | PVAD PDF writer, encryption, diagnostics, PDF rendering services, golden snapshots |
| Export integration | `tests/PVAD.Export.Tests` | DOCX, ODT, XLSX, HTML, CSV, Markdown, PDF, image/TIFF, localization, security, fidelity |
| PDF importer SDK | `tests/PVAD.Importer.Tests` | Tokenizer, object parsing, stream/content parsing, graphics interpretation, editable model |
| File importers | `tests/PVAD.FileImporter.Tests`, `tests/PVAD.FileImporter.ImageAnalysis.Tests`, `tests/PVAD.FileImporter.ImageOcr.Tests` | File-to-`DesignExportDto` adapters, raster analysis, OCR pipeline |
| PDF migration providers | `tests/PVAD.Migration.*.Tests` provider projects | Vendor C# PDF code migration to PVAD PDF code |
| Report migration | `tests/PVAD.Migration.Report.Designer.DevExpress.Tests`, `tests/PVAD.Migration.Report.Designer.Rdl.Tests`, `tests/PVAD.Migration.Report.Designer.Rpx.Tests`, plus the other report provider suites | Report source to editable PVAD design |
| Frontend | `pvad-designer` Jest tests | Editor, services, template utilities, export service, validation |

## Running Tests

```bash
# All .NET projects
dotnet build PVAD.sln
dotnet test PVAD.sln

# Core/application/API
dotnet test tests/PVAD.Core.Tests
dotnet test tests/PVAD.Application.Tests
dotnet test tests/PVAD.Api.Tests

# PDF engine and export integration
dotnet test tests/PVAD.Infrastructure.Pdf.Tests
dotnet test tests/PVAD.Export.Tests

# Importers
dotnet test tests/PVAD.Importer.Tests
dotnet test tests/PVAD.FileImporter.Tests
dotnet test tests/PVAD.FileImporter.ImageAnalysis.Tests
dotnet test tests/PVAD.FileImporter.ImageOcr.Tests

# Migration aggregators and examples
dotnet test tests/PVAD.Migration.Pdf.Tests
dotnet test tests/PVAD.Migration.Spreadsheet.Tests
dotnet test tests/PVAD.Migration.Report.Tests
dotnet test tests/PVAD.Migration.Pdf.Code.DevExpress.Tests
dotnet test tests/PVAD.Migration.Report.Designer.DevExpress.Tests
dotnet test tests/PVAD.Migration.Report.Designer.Rdl.Tests
dotnet test tests/PVAD.Migration.Report.Designer.Rpx.Tests
```

Frontend:

```bash
cd pvad-designer
npm test
npm run test:coverage
npm run build
```

## Recommended Scope By Change Type

| Change type | Minimum validation |
|-------------|--------------------|
| Documentation only | Link/readability check; no build required unless commands or APIs changed |
| Core contracts/DTOs | `PVAD.Core.Tests`, affected export/import/migration tests, `dotnet build PVAD.sln` |
| PDF engine/writer | `PVAD.Infrastructure.Pdf.Tests`, `PVAD.Export.Tests`, affected API tests |
| Exporter | `PVAD.Export.Tests`, affected infrastructure tests, API smoke test if endpoint behavior changed |
| File importer | matching PVAD importer test project, API import smoke test |
| PDF importer engine | `PVAD.Importer.Tests`, file-importer PDF coverage, PDF import API tests |
| Image analysis/OCR | image-analysis/OCR test projects plus endpoint smoke test if WebApi changed |
| Migration provider | matching `PVAD.Migration.<Provider>.Tests`, migration API smoke test if registration changed |
| Report migration | matching report migration tests plus one end-to-end render test where practical |
| Frontend UI/service | `cd pvad-designer && npm test`, `npm run build` |

## Golden Snapshot Workflow

Primary PDF snapshot details live in:

```text
tests/PVAD.Infrastructure.Pdf.Tests/SNAPSHOT_TESTING.md
```

When intentionally updating a snapshot:

1. Run the focused PDF test and capture the produced hash or diff from the failure output.
2. Verify that the output change is expected and acceptable.
3. Update the expected hash or fixture.
4. Re-run `tests/PVAD.Infrastructure.Pdf.Tests`.
5. Re-run broader export tests if the change affects common rendering behavior.
6. Document the reason in PR notes or the related checklist.

## CI Expectations

CI should validate:

1. `dotnet build PVAD.sln`
2. .NET test projects relevant to the changed areas
3. full `dotnet test PVAD.sln` for shared contracts, rendering, API, importer, or migration infrastructure changes
4. `cd pvad-designer && npm run build`
5. frontend tests when UI/service/template code changes

## Notes

- Active projects and namespaces use `PVAD.*`. Previous `PXA.*` names survive only in documented compatibility adapters, historical fixtures and signed evidence.
- `CANMIG...` diagnostic identifiers remain stable even though provider namespaces are now `PVAD.Migration.*`.
- Many migration providers are intentionally conservative. Tests should assert diagnostics for unsupported APIs instead of forcing unsafe rewrites.
- Report converters should test both element mapping and final renderability where possible.
- Importer tests should prefer small, stable fixtures and assert semantic `DesignExportDto` output instead of brittle byte-for-byte comparisons.
