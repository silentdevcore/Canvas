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
| Frontend | `pxa-designer` Jest tests | Editor, services, template utilities, export service, validation |

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

For ASP.NET, Identity, EF or OpenAPI dependency changes, run the focused contracts
and the PostgreSQL-enabled API/persistence matrix before and after the update:

```bash
dotnet tool restore
dotnet ef --version
dotnet test tests/PXA.Api.Tests --filter "FullyQualifiedName~DotnetDependencyAlignmentTests|FullyQualifiedName~OpenApiCompatibilityTests"
PXA_RUN_POSTGRES_TESTS=1 dotnet test tests/PXA.Infrastructure.Persistence.Tests -- xUnit.ParallelizeTestCollections=false
PXA_RUN_POSTGRES_TESTS=1 dotnet test tests/PXA.Api.Tests --filter "FullyQualifiedName!~ImageConversionControllerTests" -- xUnit.ParallelizeTestCollections=false
dotnet test tests/PXA.Observability.WebhookRelay.Tests
```

Docker must be available to Testcontainers; use isolated containers, never a
customer database. Also check `dotnet ef migrations has-pending-model-changes`
against `PxaDbContext` without applying migrations. Keep the local EF tool,
Identity, Design, InMemory, MVC test hosts and EF health checks aligned. The
OpenAPI model library has its own version and must match its resolved runtime
reference. The OpenAPI test generates a document from the actual controller
assembly in a minimal TestServer, verifies canonical/legacy paths and resolves
all local schema references without starting application workers or a database.
Its generated interceptor namespace is enabled only in the test project.
This is a schema-generation test, not a replacement for real authentication tests.
`DesignerDraftValidationTests` also protects the draft request's property-based
MVC validation path, optional JSON fields, existing positional API and concurrent
first-use validation. Keep the authenticated PostgreSQL draft/concurrency test;
do not replace it with the focused metadata test.
`DotnetDependencyAlignmentTests` also verifies that the Git build task resolves
to its reviewed version with `PrivateAssets=all` and stays out of the API runtime
dependency manifest.
The native ImageConversion/OCR cases remain outside this maintenance matrix;
their previously recorded platform limitation is not claimed fixed.

Frontend:

For Admin bundler maintenance, run its 26 Node contracts, production build and
full npm audit, then `node tools/dependencies/admin-tooling-smoke.mjs` from the
repository root. Designer dependencies provide the pinned Playwright browser.
The runner starts disposable loopback development and production-preview servers
and checks desktop/mobile login, failed login, navigation, users, empty search,
release-note deep links/reload and the role-denied UI. It reuses the protected
handbook smoke in both modes. Synthetic API fixtures do not prove real
authentication, session cookies, tenant isolation or backend authorization.

The report and 24 screenshots contain only synthetic test data. Protected
handbook screenshots are explicitly disabled and must not enter public CI
artifacts. Output defaults to `artifacts/admin-tooling-smoke/current`; an optional
first argument selects a separate output directory. `PXA_ADMIN_SMOKE_ROOT` can
point to an isolated old-version Admin worktree for a genuine before comparison.
Screenshots reset browser scroll restoration so scroll timing cannot
be confused with a bundler regression. Do not silently regenerate an old-version
baseline using new dependencies. Keep platform-specific native binding metadata
in the lockfile and verify the actual Linux graph through CI as well as macOS.
The development audit policy covers both Designer and Admin; neither gate
replaces the existing production dependency audits.

CI action maintenance also runs the `registry-login` job. It uses the pinned
Docker login action against a loopback-only TLS Registry V2 ping server with
random synthetic credentials, an isolated Docker configuration and a temporary
process-local trust root. It verifies credential scoping, invalid-password
rejection and explicit logout; the action's post-job logout must also succeed.
The test forces client-side authentication and never uses the host daemon,
real registry credentials, image publication or deployment. This is not proof
of production GHCR authorization, OIDC or ECR behavior. Do not install its test
CA globally. The initial macOS HTTP fixture attempts failed before login because
the Docker client required TLS; their failures are not classified as passes.

```bash
node --test tools/dependencies/dependency-compliance.test.mjs tools/dependencies/registry-login-smoke.test.mjs
node tools/dependencies/validate-dependency-compliance.mjs
```

Use the CI-validated Node.js 22 toolchain (22.12 or newer within that major).
Jest-DOM 7 requires Node 22; the Vite toolchain additionally requires 22.12 in
that major. This is a contributor build/test requirement, not a browser or
deployed application runtime requirement.

```bash
cd pxa-designer
npm ci
npm run type-check
npm test -- --runInBand
npm run test:coverage -- --runInBand
npm run build
```

The Designer declares `@testing-library/dom` explicitly as a development
dependency because both Jest-DOM 7 and React Testing Library require it. Keep
the DOM peer in the compatible 10.x range; do not rely on an undeclared peer
or use forced dependency installation to hide a mismatch.

DOM test files use `@jest-environment jsdom` and import
`@testing-library/jest-dom` before using its matchers. The default Jest environment
remains Node for non-DOM tests. The focused
`src/__tests__/jestDomCompatibility.test.tsx` suite checks accessible queries,
form interaction, invalid assertions, and v7 query-matcher integration with
the actual DOM peer. The release-history component test also uses real DOM
matchers; it is not sufficient merely to import the new package.

After building, `node tools/dependencies/designer-tooling-smoke.mjs` from the
repository root exercises development/production bundles on desktop and mobile.
Install Playwright Chromium first. Its API responses are synthetic; this is not
an authenticated backend or PDF-generation test.

Chart dependency changes also run the actual Live Preview and Canvas components:

```bash
npm --prefix pxa-designer exec -- tsc --project pxa-designer/tsconfig.chart-review.json
node tools/dependencies/designer-chart-smoke.mjs --label baseline
# After the targeted dependency update and a clean install:
node tools/dependencies/designer-chart-smoke.mjs --label updated \
  --compare artifacts/designer-chart-smoke/baseline/report.json
```

The fixture covers the current bar, line and pie consumers with normal, negative,
zero, null, empty and long-label inputs. Desktop/mobile development and explicit
production fixture builds produce 60 Preview screenshots, six desktop Canvas
screenshots and geometry/pixel reports. Comparison requires the same platform
and Chromium build. Labels, legends and dimensions must remain stable; path and
pixel differences are reported for inspection rather than silently rebaselined.
CI runs the current-version contracts and retains screenshots on success/failure.
The fixture is not an application route and is excluded from the ordinary
Designer production build. All inputs are synthetic, external font CSS is stubbed,
and other external/API requests are rejected. This does not certify backend PDF fidelity, extra chart types or
existing Canvas/Preview equivalence; the current Canvas uses a separate simplified
renderer, and null values retain the existing zero-value fallback.

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
| Frontend UI/service | `cd pxa-designer && npm test`, `npm run build` |

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
4. `cd pxa-designer && npm run build`
5. frontend tests when UI/service/template code changes

## Notes

- `PXA.*` projects and namespaces were removed in the PXA breaking rename. Use `PXA.*` projects and namespaces for active code.
- `CANMIG...` diagnostic identifiers remain stable even though provider namespaces are now `PXA.Migration.*`.
- Many migration providers are intentionally conservative. Tests should assert diagnostics for unsupported APIs instead of forcing unsafe rewrites.
- Report converters should test both element mapping and final renderability where possible.
- Importer tests should prefer small, stable fixtures and assert semantic `DesignExportDto` output instead of brittle byte-for-byte comparisons.
