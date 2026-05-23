# Testing Strategy

## Test projects

| Project | Focus |
|---------|-------|
| `tests/Canvas.Core.Tests` | Core primitives, expression engine, formatters, contracts |
| `tests/Canvas.Application.Tests` | Use-case orchestration (FindAndReplace, Clone, ExtractPages) with test doubles |
| `tests/Canvas.Infrastructure.Pdf.Tests` | PDF renderer/facade tests, serialiser integration checks, diagnostics counters, golden snapshot hash |
| `tests/Canvas.Export.Tests` | DOCX, ODT, XLSX, HTML, CSV, Markdown, TIFF export integration tests |
| `tests/Canvas.Api.Tests` | API endpoint integration tests (ExportController, DocumentOpsController) |

---

## Running tests

```bash
# All tests
dotnet test Canvas.sln

# Single project
dotnet test tests/Canvas.Export.Tests

# By category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## Golden snapshot workflow (PDF)

Primary test: `PdfGoldenSnapshotTests.ToBytes_ShouldMatchGoldenHash_ForRepresentativeDocument`

Details: `tests/Canvas.Infrastructure.Pdf.Tests/SNAPSHOT_TESTING.md`

### Updating a snapshot intentionally

1. Run the golden test; capture the produced hash from the failure output.
2. Verify the output change is expected and acceptable.
3. Update the expected hash constant in the test.
4. Re-run all infrastructure tests and full build.
5. Document the reason for the snapshot change in PR notes.

---

## CI expectations

CI must run in this order:

1. `dotnet build Canvas.sln` — 0 errors required
2. All five test projects
3. Frontend type-check: `cd ui-designer-v2 && npm run build` (or `tsc --noEmit`)

---

## Frontend tests

```bash
cd ui-designer-v2
npm test               # run all tests
npm run test:coverage  # with coverage report
```

Coverage targets: ExportService, useTemplateLoader, store actions (bulkReplaceContent, undo/redo).
