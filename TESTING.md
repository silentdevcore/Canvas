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

### Coverage collector maintenance

The 50 collector-enabled projects use VSTest, Coverlet collector 10.1.0 and
Microsoft.NET.Test.Sdk 18.11.0. The collector-free Webhook Relay uses the same
private SDK version. Coverlet's VSTest integration requires test SDK
18.4.0 or newer; it is not an MTP v2 collector. Keep the existing xUnit and
runner versions unless a separately reviewed update requires changes.

Playwright Test, Playwright and Playwright Core resolve together to 1.64.1.
Use its matching Chromium installation, not an older cached browser. Run Vite
smoke scripts sequentially: they share the Designer dependency optimization
cache. A separate browser-tool version does not change the shipped React 18,
Grid 6.0.3 or Node 22 contract. OpenAPI.NET remains on 2.x while ASP.NET Core 10
is used. Telemetry components resolve together to 1.20.1; API tests exercise
real SDK export with synthetic trace, metric and log capture, privacy processors,
and the existing unavailable-collector integration cases. Local capture is not
proof of a deployed OTLP collector or production routing.

After restoring and building Release, install `xmllint` (`libxml2-utils` on
Ubuntu; supplied by macOS) and run:

```bash
node --test tools/dependencies/coverage-smoke.test.mjs
node tools/dependencies/coverage-smoke.mjs artifacts/coverage-smoke-local
# Revalidate the retained old/new matrix attachments without rerunning tests:
node tools/dependencies/coverage-smoke.mjs --audit artifacts/coverage-review/before-filtered-exact
node tools/dependencies/coverage-smoke.mjs --audit artifacts/coverage-review/after
node tools/dependencies/coverage-smoke.mjs --compare \
  artifacts/coverage-review/before-filtered-exact/audited-summary.json \
  artifacts/coverage-review/after/audited-summary.json
```

Use a fresh output directory for every run. The runner requires exactly 50
collector hosts, uses all migration tests and focused production-exercising
tests for the other hosts, and fails on missing attachments, zero tests,
non-passing or unexecuted tests, empty denominators, absent production hits,
test/tool assemblies, generated `obj` sources and inconsistent XML/JSON modules.
It parses TRX and Cobertura with a non-network XML parser and retains both raw
reports and a machine-readable summary. CI uploads synthetic evidence for seven
days even when a later host fails. No customer database or documents are used.

This is a collector compatibility smoke, not a full-suite coverage target or a
claim about native OCR accuracy. The existing full CI suites, PostgreSQL tests
and documented native OCR limitation remain unchanged. The OCR smoke uses a
fake OCR engine but real image-to-design mapping rather than silently returning
when native libraries are unavailable. Image, image-analysis and converter hosts use a private,
Linux-only Skia native-assets reference matching their managed 3.119.2 version;
this is test support, not a shipped native-runtime or OCR deployment change.
The API filter uses the fully qualified
class name: `VersionControllerTests` alone also matches
`ImageConversionControllerTests` with VSTest's case-insensitive substring filter.

Comparison requires identical project sets, discovered/executed/passed counts,
test names and instrumented assemblies, with hits in each expected product
module. It reports line denominators and every lost/gained
covered source location without silently rebaselining them. Coverage 10 fixes
record, branch and async instrumentation, so changed percentages need review;
they do not by themselves prove a product regression or improvement. Collector
integration does not enforce percentage thresholds automatically.

References: [Coverlet VSTest integration](https://github.com/coverlet-coverage/coverlet/blob/v10.1.0/Documentation/VSTestIntegration.md),
[Coverlet 10.1.0 changes](https://github.com/coverlet-coverage/coverlet/releases/tag/v10.1.0).

### Test groups

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

### Stable React Compatibility

The approved baseline remains React/React DOM 18.3.1, matching 18.x declarations
and stable Glide Data Grid 6.0.3. Do not install the official Grid alpha solely
to merge a React-major update. Future React, DOM and declaration updates are
grouped for coordinated review.

```bash
node --test tools/dependencies/designer-react-compatibility.test.mjs
node tools/dependencies/designer-react-smoke.mjs
```

Run after `npm ci` and the ordinary Designer production build. Four dependency
contracts verify the approved versions, replacement peers, removed legacy
packages and the complete installed npm tree. Two existing debts remain visible:
Grid expects `marked` 4.x while the root resolves 18.0.5, and Monaco React's
`monaco-editor` peer is not installed. The existing `.npmrc` legacy-peer setting
is not expanded or described as strict resolution. Any additional peer problem
fails the contract. Do not add a new exception without explicit investigation.

The browser runner checks actual components under StrictMode in development
and an isolated production fixture build, on desktop and mobile viewports.
It exercises JSON edit/add/delete callbacks, selection and keyboard drag
cancellation in the legacy surface, and editing/formula recalculation in the
stable Spreadsheet grid. Three additional Jest regressions cover controlled
selection feedback, sheet-switch reset and the application's required single
overlay portal. `ComplexEditor` and its legacy `PxaSurface` and
`JsonEditorPanel` are not routed from the current application; their compatibility
test does not claim new active-Designer functionality or a working legacy
reorder operation. The ordinary application is tested separately by the existing
bundle smoke runner. Test fixtures are excluded from ordinary production output.
That runner also edits and reopens a cell on the actual `/spreadsheet/create`
route in all four modes/viewports, using the real sidebar control on mobile.
The Grid wrapper must pass its selected state back when using a selection
callback, and `index.html` must retain the `portal` host required by Glide.
For a genuine old-dependency visual baseline only, use
`PXA_DESIGNER_SMOKE_ROOT=/absolute/old/pxa-designer node tools/dependencies/designer-tooling-smoke.mjs artifacts/before --baseline-only`.
That explicit mode covers home/gallery/search only, not Spreadsheet editing.
Both comparison captures wait for every staggered gallery animation to finish;
do not replace an old-dependency baseline with a new-dependency render.
Screenshots and reports contain only synthetic data and make no real backend,
authentication, cloud deployment or native mobile/touch claim.

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
