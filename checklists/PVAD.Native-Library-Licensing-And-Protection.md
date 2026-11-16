# PVAD Native Library Licensing And Protection

## Goal And Decisions

Implement offline licensing for every native product and a free, reproducible
release protection pipeline. This checklist records implementation evidence, not
a claim that obfuscation prevents reverse engineering.

- [x] Record subscriptions, perpetual licenses, and explicitly issued 30-day Trials.
- [x] Define seven days of grace for paid subscriptions and none for Trials.
- [x] Require signed files without mandatory internet access or hidden telemetry.
- [x] Keep document contents unchanged; missing rights produce explicit errors.
- [x] Separate developer seats, redistribution rights, and server subscriptions.
- [ ] Complete all product integrations before declaring native enforcement ready.

## Dependencies

- [ ] Provision the manufacturer-owned public verification keys through a reviewed build change; never ship test signing keys.
- [ ] Keep NuGet publisher selection, publication, prices, and legal approval separate from technical implementation.
- [ ] Resolve third-party distribution gates, including the [NPOI license decision](PVAD.NPOI-License-Decision.md), before shipping affected product packages; do not silently accept third-party terms.
- [ ] Align [subscription licensing](PVAD.Subscription-Licensing.md) and [SDK contracts](PVAD.SDK-Roadmap.md).
- [x] Preserve the existing server-license schemas and persisted acceptance/audit evidence.

## P0: Offline Verification Core

- [x] Add standalone .NET 10 `PVAD.Licensing` without database, HTTP, or WebApi dependencies.
- [x] Provide file/stream loading, status inspection, and stable typed failures.
- [x] Validate bounded input, strict JSON, duplicate properties, schema, algorithm, signature encoding, and UTC timestamps.
- [x] Verify exact signed payload bytes using ECDSA P-256/SHA-256 and known embedded keys only.
- [x] Support multiple trusted key IDs without accepting customer-supplied runtime trust anchors.
- [x] Activate immutable snapshots atomically; failed replacement preserves the current license.
- [ ] Recheck validity and product/provider rights at the start of protected operations.
- [x] Bind perpetual update eligibility to immutable release metadata rather than filesystem or build timestamps.
- [x] Test Trial boundaries, grace boundaries, perpetual updates, rotation, concurrent replacement, and malformed inputs.

## P0: Issuer And Server Separation

- [x] Add a private-key-file-controlled offline preparation CLI; explicitly distinguish it from authenticated issuance and durable audit.
- [ ] Implement manufacturer-only issuance/renewal with authorization and audit.
- [ ] Keep private keys outside packages, customer images, logs, and source control; never generate production replacement keys automatically.
- [x] Split the existing server signing/verifying services without changing existing server artifacts.
- [x] Default server deployments to verification-only mode; block issuance/replacement even for System Administrators unless issuer mode is explicitly configured.
- [x] Load issuer private keys lazily, validate the key pair, and never generate missing keys in any environment.
- [x] Mount only the public verification key in customer Compose; supply a separate manufacturer-only issuer overlay.
- [x] Keep license listing, reads, downloads, and schema-1/schema-2 validation functional without a private key.
- [ ] Add Admin management and authorized Account downloads for native licenses.
- [ ] Use the same verification core in WebApi and workers while preserving tenant, session, entitlement, and feature checks.
- [ ] Transfer only locally verifiable worker configuration; no source-visible secrets or public bypass modes.
- [ ] Support explicit import of signed revocation lists; document offline revocation limitations.

## P0: Product And Operation Coverage

The [operation matrix](../docs/native-library-licensing.md#operation-matrix)
is the starting inventory, not completed enforcement coverage.

- [x] Represent all ten native product areas and test domain/kind/provider-scoped grants against all 35 current migration catalog entries.

- [x] Guard PDF generation and direct PDF engine entry points, including bytes, files, and streams.
- [x] Recheck PDF rights at shared writer entry before implicit page creation or destination writes.
- [x] Keep metadata/options DTOs free and avoid a second Converter grant for native PDF generation.
- [x] Guard Word generation, both direct exporter overloads, interface calls, and the public DOCX signing service; see the Word milestone below.
- [x] Guard Spreadsheet generation, retained builder handles, calculation, IO, direct exporters, data operations, and design conversion; see the Spreadsheet milestone below.
- [ ] Guard PDF importing, editing, saving, and all FileImporter/analysis/OCR paths.
- [ ] Guard format conversion and direct converter implementations.
- [ ] Guard every PDF Code/Viewer/Designer, Spreadsheet Code, and Report Designer migration provider.
- [ ] Resolve automatic report detection to its concrete provider before admission; the core rejects `auto` grants.
- [ ] Resolve paid-operation dependency rights centrally; internal implementation dependencies must not become unexpected extra purchases.
- [ ] Keep pure contracts, DTOs, and metadata inspection free of licensing requirements.
- [ ] Test each public protected entry with missing, incorrect, and valid licenses.
- [x] Supply signed PDF/Word/Spreadsheet and partial Importer test licenses to known test and worker harnesses without production bypasses; retain product-specific negative fixtures and extend grants only for tested integration boundaries.
- [x] Add an isolated signed PDF test runner; never modify the source trust register or regular build directories.
- [x] Pass the configured native license file to CodeWorker outside user source; reject missing, incorrect, and expired PDF rights.
- [x] Add explicit API native-license startup configuration and stable HTTP 503 Problem Details for propagated license failures.
- [x] Wire shared signed sessions into general .NET CI, visual/platform parity, scheduled fuzzing, full API validation, coverage, and browser/operator/recovery launchers.
- [x] Resolve actual MSBuild assembly/assets paths and stamp test assemblies; block synthetic Pack/Publish and output redirection outside the session build.
- [x] Execute and strictly audit the complete unfiltered API/PostgreSQL suite in the signed isolated session; see the acceptance evidence below.
- [x] Execute all 52 existing collector hosts with signed isolated builds and independently audit their retained reports; see the local coverage checkpoint below.
- [ ] Execute complete general CI, visual/platform matrices, and fuzz campaigns before claiming broad acceptance; wiring and contract tests are not full execution evidence.

## P1: Packaging And Free Code Protection

- [x] Pin Obfuscar 2.2.50 and ILSpy 11.2.0.10050 in an isolated local tool manifest.
- [x] Rehearse the licensing core: internal-name obfuscation, unchanged public API, protected DLL packaging, and independent offline local-feed consumer.
- [x] Verify the licensing-core package excludes sources, private symbols, and private keys and contains the exact protected DLL hash.
- [x] Prepare read-only CI for Windows, Linux, and macOS without publishing packages or uploading maps/keys; actual CI runs remain open.
- [ ] Pin Obfuscar and preserve public APIs, serializers, reflection/DI contracts, resources, friend assemblies, and generated C#.
- [ ] Obfuscate related first-party assemblies together; leave third-party binaries unchanged.
- [ ] Exclude sources, embedded sources, SourceLink source references, private symbols, and signing material from release packages.
- [ ] Retain mapping files and symbols only as protected support artifacts.
- [ ] Audit protected output with ILSpy; compare public API and internal-name visibility without claiming logic is unrecoverable.
- [ ] Test local-feed package consumers and complete transitive dependency graphs.
- [ ] Verify protected packages on Windows, Linux, and macOS, including native dependencies and code execution.
- [ ] Supply EULA references, third-party notices, XML documentation, and offline examples.
- [x] Exclude anti-debugger crashes, process scanning, self-modifying loaders, and antivirus-hostile techniques.
- [x] Treat strong names as identities rather than an anti-tamper security boundary.

## P1: Validation, Documentation, And Release

- [x] Document the core API, diagnostics, offline limits, issuance-tool boundaries, and incomplete enforcement status.
- [x] Add a Minor fragment describing the additive verification foundation only; do not claim mandatory enforcement is complete.
- [ ] Run existing PDF Trust, visual parity, converter roundtrip, API, and worker regressions without relaxing thresholds.
- [x] Verify denied PDF exports preserve existing files, streams/positions, and page lists and do not create absent output files.
- [ ] Document activation, renewal, expiry, update eligibility, diagnostics, and offline limitations.
- [x] Add the Major breaking release fragment for mandatory native PDF construction/export and API/worker configuration.
- [x] Keep VERSION, published releases, live databases, and registries unchanged during implementation.
- [x] Record actual commands/results and leave unavailable platform evidence open.

### Current Verification Evidence

- `dotnet test tests/PVAD.Licensing.Tests/PVAD.Licensing.Tests.csproj --no-restore`: 80 passed, none failed or skipped.
- `node --test tools/versioning/pvad-version.test.mjs tools/versioning/pvad-release-author.test.mjs tools/native-licensing/pvad-native-licensing.test.mjs`: 33 passed.
- `dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-build --no-restore`: 141 passed, none failed or skipped.
- `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1 --verbosity minimal`: succeeded outside the restricted sandbox, zero errors and 150 existing warnings. Earlier sandboxed parallel and serial attempts stalled and were explicitly terminated; they are not counted as successful runs.
- Latest local protected-package rehearsal: `artifacts/native-licensing/1794762301071-a023bfb5/report.json` (macOS, actual .NET x64 runtime; Node.js ARM64 is recorded separately).
- Native test results: `artifacts/native-licensing-tests/native-licensing.trx`.
- Version/fragment validation, Markdown naming, former-name guard, and `git diff --check` passed.
- Manufacturer native trust is not provisioned. At this earlier foundation checkpoint, native engine/worker enforcement was still open; the PDF milestone below supersedes that status for PDF generation only. Authenticated native issuance, other product integrations, and native-license Admin/Account integration remain open; Windows/Linux CI has not been run. Existing server-license signer separation is covered below.

### Server Issuer/Verifier Boundary Evidence

- `server-license-boundary-verified.trx`: 49 targeted backend cases passed, none failed or skipped. Includes an actual API host with a blank private-key path and positive Admin list/read/download/validate operations.
- `server-license-postgres-verified.trx`: 10 existing Account/Admin/replacement cases passed with isolated PostgreSQL containers, including cross-tenant rejection, serializable replacement, immutable history, and rollback.
- Earlier PostgreSQL attempt: nine cases failed at container creation because Rancher's host socket path could not be mounted inside the VM. The rerun used `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` with Resource Reaper still enabled; the failed TRX remains separate.
- An expanded read-only fixture initially used an empty organization ID, which EF replaced on insert. The fixture now supplies an explicit synthetic organization ID; no application query or authorization rule was relaxed. The failed TRX remains separate.
- Compose, versioning, and release-author contracts: 40 passed. Protected Admin handbook and operator-guide contracts: 16 passed.
- Native verifier regression: 80 passed. Final WebApi build succeeded with zero errors and two existing NPOI license-decision warnings; no supplier terms were accepted automatically.
- Real `docker compose config` rendering passed for the public-only base profile and the explicit manufacturer overlay using synthetic paths. No deployment stack was started.
- Server verification/issuance separation is implemented for the existing server-license profile only. Native-license Admin/Account issuance remains open; the separate PDF milestone below records native generation enforcement.

### Native PDF Generation Milestone

- `PdfDocument` construction and the shared internal `PdfWriter.Write` require a current `pdf` grant. The Generator facade and mapper inherit this boundary; each bytes/file/stream export rechecks admission.
- Explicit `NativeLicensing:LicensePath` activation uses the same verifier in API and CodeWorker. Wrong explicit host configuration fails startup; absent configuration permits non-PDF Account/Admin operation, not unlicensed PDF processing.
- C# PDF worker results preserve native diagnostic codes and contain no PDF/design on license rejection. Sandbox access restrictions are not relaxed.
- The isolated runner signs ephemeral fixtures, builds into a separate artifacts tree, and removes licenses and synthetic-trust binaries in `finally`. Worker copy targets resolve actual outputs instead of stale hard-coded `bin` paths.
- Initial PDF probe: 18 checks passed. Expanded probe: 20 passed, including rejection before implicit page creation (`artifacts/native-engine-tests/1794773310792-8fbeb9b4/pdf-admission.json`).
- API/worker/converter/export/server-separation regressions: 105 passed, none failed or skipped (`artifacts/native-engine-tests/1794766071324-6b2563fb/licensed-tests.trx`). These include real child-worker success and missing/wrong/expired license diagnostics, as well as the existing export and conversion tests.
- Full Generator suite with a signed PDF-only native license: 141 passed, none failed or skipped (`artifacts/native-engine-tests/1794773211921-5277d259/licensed-tests.trx`). No Converter grant was added.
- Native verification-core regression after PDF integration: 80 passed, none failed or skipped (`artifacts/native-licensing-tests/native-core-pdf-integration.trx`); the ordinary embedded trust remains empty and the public unlicensed-state tests still pass.
- The expanded probe and Generator runner hash-checked that the source trust and ordinary Licensing `bin`/`obj` files remained unchanged; temporary license/trust files and isolated binaries were removed.
- Metadata/version/release-author/container contracts: 46 passed. Version/fragment validation, Markdown zero-match guard, and the reviewed former-name guard passed. VERSION and published releases remain unchanged.
- API and probe builds succeeded with existing nullability/deprecation and NPOI license-decision warnings. No supplier terms were accepted. No new desktop/mobile visual or full API-suite acceptance is implied.
- At this PDF checkpoint, cross-platform CI execution and general launcher adoption were still open. The next milestone records shared-session wiring and local evidence; manufacturer keys, other native products, complete pipeline execution, and full protected-package compatibility remain open.

### Shared CI And Launcher Milestone

- [x] Create/reuse one owned signed session across nested tools, with private temporary fixtures and an isolated build tree.
- [x] Keep release/container jobs outside synthetic sessions; reject Pack, Publish, and output redirection and mark test assemblies as not for publication.
- [x] Propagate only validated native session settings through the existing narrow API/browser child-environment allowlists; retain synthetic database/storage isolation.
- [x] Resolve actual MSBuild assembly/assets paths, including default CodeWorker lookup, rather than copying or executing stale ordinary builds.
- [x] Adapt visual, coverage, recovery, Legal, parser, external-corpus, operator, HTTPS, and monitoring harnesses. Monitoring/fuzzing use build-only harness output within the owned tree.
- [x] Verify shared-session API/worker/conversion/export regression: 84 passed, none failed or skipped (`artifacts/native-test-sessions/bdd421a3-44e4-4cb9-91cd-bcd504d9b557/session-api.trx`).
- [x] Verify actual negative Pack/Publish/output-escape checks and cleanup (`rehearsal.json` and `cleanup.json` in that session's evidence directory).
- [x] Run all 80 native-core tests inside the shared session; their public unlicensed-state assertions still pass because core tests are excluded from auto-activation.
- [x] Run the enforced local Canvas/Preview/backend/C# visual pack: four pages, 40 elements, 38 types, P0=0, P1=0, P2=1, match=39 (`artifacts/visual-parity/visual-parity-report.json`). The text raster difference remains visible; no baseline or new human approval was manufactured.
- [x] Run the bounded local mutation harness through the signed wrapper: 30 cases, three parsers, zero crashes/timeouts, eight differentials (`artifacts/native-licensing-tests/shared-parser-session/report.json`). All case/parser outcome classifications match the pre-existing local report; differences were not hidden or reclassified as conformance.
- [x] Run 153 tooling, workflow/shell, environment, release-author, and visual contract tests with zero failures or skips. Source trust and published releases remain unchanged; all session build trees were removed.
- [x] Execute the complete unfiltered API/PostgreSQL audit without filters, skips, or relaxed limits; see the completed checkpoint below.
- [x] Execute all 52 coverage hosts with actual production-module hits and no failed or skipped smoke cases; see the completed local coverage checkpoint below.
- [ ] Execute full visual/platform matrices, live GitHub jobs, a coverage-guided AFL campaign, and real browser/operator stacks before broad release acceptance.

An initial small build exposed incorrect nested quoting in an MSBuild condition.
The condition was moved into named properties and rerun successfully; the failed
attempt is not counted as a passing build. Local builds still report existing
NPOI/deprecation/nullability warnings; no supplier terms were accepted. This
milestone adds no runtime license bypass or production key. Its implementation
coverage does not close other native product integrations.

### Complete API And PostgreSQL Acceptance

- [x] Run `node tools/dependencies/api-full-validation.mjs` with the local Rancher socket and `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`; use only signed synthetic licenses and test-owned databases/storage.
- [x] Complete a fresh restore and serial rebuild with zero errors; retain the 74 existing compiler/analyzer/license-decision warnings without accepting supplier terms.
- [x] Discover 1,464 catalog entries and execute 1,467 concrete cases. One previously documented late-enumerated theory expands to four reviewed exception variants; every other entry matches exactly once.
- [x] Verify 1,467 passed, zero failed, skipped, timed out, aborted, missing, duplicated, or otherwise non-passing cases. Every non-pass TRX counter is zero.
- [x] Preserve the 40-minute session budget, 180-second per-test hang detection, serial execution, and unfiltered test selection.
- [x] Verify the API test assembly checksum remains unchanged throughout execution and the tracked source diff still matches its pre-run checksum before recording this checkpoint.
- [x] Remove temporary application storage, signed license/trust fixtures, and isolated build output. The native session cleanup confirms unchanged source trust and test artifacts; no session build directories remain.
- [x] Complete the separate post-run Docker check. The user ran `docker ps -aq --filter label=org.testcontainers=true` locally and reported an empty result in the follow-up. This manually reported result confirms no containers matching that label remained at the time of the check; it is not an agent-executed daemon inspection or an audit of unlabeled Docker resources. The initial automated query was not executed because automatic approval review hit a usage limit; no cleanup safeguard was bypassed and no broad deletion was attempted.

Evidence directory:
`artifacts/api-full-validation/2026-11-15T20-43-15.898Z-35557f3b-497c-408e-9869-3f5e3ff30f69/`.
It contains `summary.json`, `catalog.json`, `api-full.trx`,
`verified-results.json`, and the private build/discovery/test diagnostics.
Native file cleanup is recorded separately in
`artifacts/native-test-sessions/2a75b40e-048f-4e3c-b08e-bac7dda9c3bb/cleanup.json`.

The run started at 20:43:16 UTC and completed at 21:00:12 UTC on
2026-11-15, including restore/rebuild. The test phase took approximately
12 minutes 19 seconds. The tested assembly SHA-256 is
`52e2c67903daae44639169af3160a686e5127fc5b8f093a8122e9a7dadf591ef`.
No runtime correction was required by this run. This closes the local complete
API/PostgreSQL test-result gate and its manually confirmed labeled-container
post-check, not full solution, coverage, platform, production, native-product,
or all-Docker-resource acceptance. VERSION, published
releases, and live databases were not changed.

### Local Coverage Instrumentation Acceptance

- [x] Run `node tools/dependencies/coverage-smoke.mjs artifacts/coverage-native-licensing-20261115` on 2026-11-15 using one signed synthetic session and a fresh isolated Release solution build, with zero build errors. Existing compiler and NPOI license-decision warnings remain visible; supplier terms were not accepted.
- [x] Execute all 52 collector hosts: 947 concrete tests passed, with zero failures or skips. Preserve the existing smoke selections, production include/exclude settings, and per-host timeout.
- [x] Verify nonzero test counts, actual production-module hits, matching JSON/Cobertura module sets, and exactly one fresh attachment set per host. Test/tool assemblies and generated `obj` sources remain excluded.
- [x] Include the new `PVAD.Licensing` dependency in all 52 reports rather than filtering it out to imitate historical module sets or denominators.
- [x] Run all six `tools/dependencies/coverage-smoke.test.mjs` contracts against the still-active isolated Release artifacts. The real VSTest/collector dependency checks cover all 52 hosts and the collector-free relay.
- [x] Independently re-read all retained raw attachments with `node tools/dependencies/coverage-smoke.mjs --audit artifacts/coverage-native-licensing-20261115`. The audited summary is byte-identical to the execution summary; no raw report was changed.
- [x] Verify successful cleanup of temporary license/trust files and isolated binaries, unchanged source trust and signed fixtures, and no remaining session build trees.
- [x] Pass 29 version/release-author tests, metadata/fragment validation, Markdown naming, the reviewed former-name guard, and `git diff --check`; keep VERSION and published releases unchanged.

| Coverage group | Hosts | Passed cases |
| --- | ---: | ---: |
| Core and product smoke selections | 16 | 255 |
| PDF migration | 18 | 335 |
| Report migration | 9 | 316 |
| Spreadsheet migration | 9 | 41 |
| Total | 52 | 947 |

Retained evidence is under `artifacts/coverage-native-licensing-20261115/`:
each project has `tests.trx` and its Cobertura/JSON attachments, with
`summary.json` and `audited-summary.json` at the root. Both summaries have
SHA-256 `c7973fbeb95e3cb0de9fa1784c4168d6eda8c6e8fd8da4ad0cda86105f33b436`.
Cleanup is recorded in
`artifacts/native-test-sessions/8a4aaef6-4894-4e29-a676-1f4feccad397/cleanup.json`.
The host is macOS with the x64 .NET SDK/runtime and ARM64 Node.js; Linux and
Windows execution is not implied.

This closes the local 52-host instrumentation smoke gate, not full functional
coverage, a minimum overall coverage percentage, or complete native-product
enforcement. Verification-core tests are collector-free and remain a separate
80-case gate. No runtime correction, trust provisioning, package publication,
commit, or deployment was required by this run. This checkpoint preceded the
Word milestone below; its original reports and results remain unchanged.

### Native Word Generation And Signing Milestone

- [x] Require `word` at the shared direct exporter boundary before traversal, cancellation handling, asset processing, or package creation. The Generator facade, both overloads, and `IDocumentExporter` calls inherit the same check.
- [x] Guard `DigitalSigningService.SignDocx` before certificate loading or source-stream consumption; retain its existing signing implementation.
- [x] Preserve free exporter/capability inspection, design/options DTOs, and document metadata. A Word-only license succeeds without a PDF or Converter grant.
- [x] Recheck every new export/signing operation, including reused exporters after replacement; failed renewal preserves the active snapshot and valid renewal restores processing without restart.
- [x] Add the isolated `word` probe with 34 passing checks for missing/wrong/expired license handling, both overloads, facade/interface calls, unchanged input state, cancellation, real DOCX content, and synthetic OPC signing.
- [x] Keep native failures out of the signing controller's generic Bad Request catch. A regression test injects a native file error into the real signing action and verifies propagation through the central HTTP 503 Problem Details handler without exposing the path. Native admission denials themselves are checked separately in the isolated probe.
- [x] Activate only integrated `pdf`/`word` grants in shared regression sessions. Keep separately signed PDF-only, Word-only, and expired fixtures; do not grant unintegrated products or modify source trust.
- [x] Update the operation matrix and its .NET/Node contracts to `pdf-generation-and-word`: two enforced products and eight still open.
- [x] Prepare the dedicated CI workflow to run Word admission, exporter compatibility, and API regressions, with content-free evidence uploads. Actual Windows/Linux/macOS CI execution remains open.
- [x] Pass all 299 selected .NET cases: six complete Word infrastructure tests, 141 complete Generator tests, 80 complete licensing-core tests, and 72 targeted API/worker/export/document-operation cases. TRX counters and actual result rows confirm zero failures, skips, errors, timeouts, aborts, or inconclusive results.
- [x] Re-run the existing PDF admission probe: all 20 checks still pass, including PDF-only generation and wrong-product rejection.
- [x] Pass 45 native-tooling/workflow/version/release-author tests, metadata/fragment validation, naming guards, and `git diff --check`.
- [x] Document native Word activation and server migration, and add `native-word-license-enforcement.json` with Major/Breaking impact. Keep VERSION, published releases, production trust, databases, and registries unchanged.

Final Word probe evidence:
`artifacts/native-engine-tests/1794778392548-3db4123a/word-admission.json`
and `run.json`. The earlier successful probe exposed a new nullable warning in
its own inspection code; that warning was corrected and the final run passed
again with only the six existing product/compiler/license-decision warnings.
No supplier terms were accepted.

PDF probe evidence:
`artifacts/native-engine-tests/1794778501793-48500106/pdf-admission.json`
and `run.json`.

Word/Generator/core/API TRX files and cleanup evidence:
`artifacts/native-test-sessions/526663fb-505e-4e17-a69d-3eddae92a6e1/`.
Files are named `word-milestone-word.trx`, `word-milestone-generator.trx`,
`word-milestone-core.trx`, `word-milestone-api.trx`, and `cleanup.json`.
All three runs removed their signed fixtures and isolated binaries and verified
unchanged source trust. Probe runs also verified unchanged regular Licensing
build outputs. No source rendering, serialization, or signing algorithm was
changed; no baseline or test threshold was relaxed.

These are local macOS/x64 .NET results, not a new full API/PostgreSQL audit,
52-host coverage run, visual-parity acceptance, or an independent Office
signature validation. Earlier full API and coverage evidence remains historical
to its pre-Word checkpoint. Manufacturer trust, authenticated issuance,
Admin/Account integration, protected packages, and the other native products
remain open. This checkpoint preceded the Spreadsheet milestone below.

### Native Spreadsheet Enforcement Milestone

- [x] Require `spreadsheet` for the Generator/workbook constructor, fluent authoring mutations, and subsequent calls on retained worksheet/range/column/cell/style handles.
- [x] Guard shared ClosedXML workbook construction before export or calculation, direct XLSX/design exporters, XLSX/XLS import, XLS export, and CSV/TSV import/export.
- [x] Admit file saves before any output generation/write, including empty CSV/TSV builders. Reject direct imports before consuming or closing input streams.
- [x] Protect sort, find/replace, data-row generation, template binding, and conversion to `DesignExportDto` before changing or traversing the input model.
- [x] Keep DTOs, existing-model reads, exporter/capability metadata, workbook/sheet names, and structural validation free of native licensing; preserve separate application authorization.
- [x] Verify a Spreadsheet-only license with no PDF, Word, Importer, or Converter grants. Rendering the resulting design through a separate PDF/Word product remains a distinct admission; central paid-operation dependency resolution is still open.
- [x] Inventory public processing types, methods including overload signatures, constructors, and properties against protected/free classifications. Explicit metadata/validation types remain free; new processing entry points cannot silently escape coverage.
- [x] Pass the final 145-check isolated Spreadsheet probe covering 40 protected method/constructor entries, real XLSX/XLS formulas, CSV/TSV roundtrips, calculation, callbacks, retained handles, renewal, and non-destructive file/stream/model rejection.
- [x] Guard Spreadsheet API import and export before upload consumption or empty-workbook output; propagate native exceptions through the central HTTP 503 handler instead of the generic workbook Bad Request catch.
- [x] Exercise 15 actual API denial requests with a PDF-only license, plus license-free validation and successful XLSX export after restoration. Run the process-wide replacement test in a non-parallel collection and always restore the signed integrated license in `finally`.
- [x] Pass 370 selected .NET cases: 13 complete Spreadsheet infrastructure tests, 141 complete Generator tests, 80 complete licensing-core tests, and 136 targeted API/worker/export/generated-migration-code cases. TRX counters and result rows confirm all passed with no skips, failures, errors, timeouts, aborts, or inconclusive results.
- [x] Re-run product-specific PDF-only and Word-only probes after the Spreadsheet changes: 20 and 34 checks passed respectively; no extra Spreadsheet grant was needed by those products.
- [x] Extend shared signed test fixtures only to the three integrated products, retaining separate product-only and expired fixtures. Source manufacturer trust remains empty.
- [x] Update the operation matrix and .NET/Node contracts to `pdf-word-and-spreadsheet`, with three enforced product flags and seven still open.
- [x] Extend the prepared native CI workflow with Spreadsheet admission, compatibility, API, and content-free report coverage. Actual cross-platform CI acceptance remains open.
- [x] Pass all 45 native-tooling/workflow/version/release-author contracts, metadata/fragment validation, naming guards, and `git diff --check`; add the Major/Breaking `native-spreadsheet-license-enforcement.json` fragment without changing VERSION or published releases.

Final native probe reports and cleanup evidence:

- Spreadsheet: `artifacts/native-engine-tests/1794779582779-a6b7547b/spreadsheet-admission.json` and `run.json`.
- PDF: `artifacts/native-engine-tests/1794779639156-27ab1d0d/pdf-admission.json` and `run.json`.
- Word: `artifacts/native-engine-tests/1794779692100-469d532b/word-admission.json` and `run.json`.

TRX files and shared-session cleanup:
`artifacts/native-test-sessions/4601e60e-84ea-4c14-91f6-9021eb4bdf06/`,
with `spreadsheet-milestone-spreadsheet.trx`,
`spreadsheet-milestone-generator.trx`, `spreadsheet-milestone-core.trx`,
`spreadsheet-milestone-api.trx`, and `cleanup.json`.
Signed fixtures and isolated binaries were removed; source trust and regular
Licensing build outputs were unchanged. No session build trees remain. Builds
had zero errors and existing compiler/NPOI license-decision warnings; no supplier
terms were accepted. No calculation, serialization, or format algorithm,
baseline, or test threshold was changed.

These are local macOS/x64 .NET results. They do not replace the earlier full
API/PostgreSQL, 52-host coverage, or visual/platform checkpoints for the changed
source. Manufacturer keys, authenticated native issuance, Admin/Account,
protected-package acceptance, and the other seven native products remain open.
The following milestone records the first Importer boundaries; its remaining
engine and internal dependency-rights work stays open. No commit, push,
publication, live database change, or restart occurred.

### Native Importer Entry-Point Milestone

- [x] Require `importer` before processing in all eight registry importers and static entry points: DOC, DOCX, image, Markdown, ODT, PDF, PPTX, and SVG.
- [x] Guard all PDF load overloads through their shared core, the public PDF facade, high-level document saving, full rewrite, and incremental updates. Reject before reading input or enumerating update requests.
- [x] Preserve source streams and bytes on denied admission; do not invoke asset resolvers or write output. Reused readers/writers recheck after license replacement.
- [x] Verify these boundaries with an Importer-only license, without PDF, Word, Spreadsheet, or Converter grants. Ordinary PDF document creation still rejects this license; no ambient all-product bypass was added.
- [x] Guard nine API import/debug actions before upload consumption and propagate native exceptions through the central HTTP 503 Problem Details handler rather than generic file-error catches.
- [x] Test nine actual wrong-product HTTP requests and successful Markdown import after Importer-only renewal. Serialize process-wide license replacement and restore the original fixture in `finally`.
- [x] Pass 111 distinct isolated admission checks covering 31 entries, renewal, cancellation, unchanged source state, actual imported content, and real PDF save output.
- [x] Pass 860 .NET tests: 23 complete FileImporter, 661 complete Importer, 80 complete licensing-core, and 96 targeted API/worker/export cases. Audit TRX counters and result rows: all passed, with no skipped, failed, timed-out, aborted, erroneous, or inconclusive cases.
- [x] Extend synthetic sessions with an Importer-only fixture and combined PDF/Word/Spreadsheet/Importer grants for these partial boundaries. Verify unchanged empty source trust and cleanup of signed fixtures and isolated binaries.
- [x] Keep `importer.enforced` false, report `completeImporterEnforcement: false`, and enumerate covered and remaining entry points. The three fully enforced product flags and root matrix status remain unchanged.
- [x] Prepare Importer probe/full-suite CI steps and content-free reports; increase only the outer CI workload budget to 30 minutes. Actual platform CI acceptance remains open.
- [x] Pass 46 tooling/version/release-author contracts, naming and metadata checks, and `git diff --check`; document the boundary and prepare the Major/Breaking `native-import-entry-license-enforcement.json` fragment.
- [x] Guard direct tokenizer, parser, stream-decoder, and analysis entry points; see the separate direct-engine milestone below.
- [x] Guard color/function engines and retained font decoding models; see the color/font milestone below.
- [x] Guard editing APIs, retained editing sessions/documents, and content-stream rewriting; see the editing milestone below.
- [x] Guard Trust/security/redaction/signing/e-invoice operations; see the Trust/security milestone below.
- [x] Implement and test restricted internal PDF regeneration dependency rights without authorizing ordinary public Generator entry points; see the regeneration/debug milestone below.
- [x] Integrate image analysis, OCR engines, and worker license verification and error transfer; see the image-analysis/OCR milestone below.

Final probe evidence:
`artifacts/native-engine-tests/1794781019503-6bcb3a0c/importer-admission.json`
and `run.json`. The initial probe at
`artifacts/native-engine-tests/1794780731985-a8069b5e/` failed to compile
because its fake asset resolver used a URI instead of the interface's string
parameter. Only the fake was corrected; that failed result remains retained.
The next probe passed 110 checks; the final run added the explicit ordinary-PDF
creation rejection and passed 111. Synthetic DOC data exercises bounded text
fallback, not broad Word interoperability. Password/certificate overload checks
use plain PDF data; they do not themselves prove encrypted-PDF compatibility.

Regression evidence and cleanup:
`artifacts/native-test-sessions/83f11c88-6543-4fcf-87ce-31204660f388/`,
with `importer-entry-milestone-files.trx`,
`importer-entry-milestone-importer.trx`, `importer-entry-milestone-core.trx`,
`importer-entry-milestone-api.trx`, and `cleanup.json`.
Independent result-file inspection confirms all counts, cleanup flags, unchanged
source trust and regular Licensing build outputs, and no session build trees.
Builds had no errors after the fake correction; existing PPTX compiler and
dependency/license-decision warnings remain. No supplier terms were accepted.

These are local macOS/x64 .NET results, not complete Importer enforcement or a
new full API/PostgreSQL, 52-host coverage, visual, or cross-platform acceptance.
Regeneration and OCR dependency rights are not proven by file/read/save tests.
No format algorithm, original fixture, baseline, or test threshold changed.
Manufacturer keys, authenticated issuance, Admin/Account integration, protected
packages, and other products remain open. VERSION, published releases, live
databases, and registries are unchanged; no commit, push, or restart occurred.

### Native Importer Direct-Engine Milestone

- [x] Guard the tokenizer's read/seek operations, including default-initialized and retained tokenizers, before changing position or reading input.
- [x] Guard all four public object-parser methods, cross-reference parsing, content parsing, document building, and both graphics-interpreter overloads.
- [x] Guard the decoder registry and all five built-in decoders; keep capability inspection and registration free.
- [x] Guard simple-font and CMap parsing, including admission before resolver callbacks.
- [x] Guard primitive construction, bounding-box calculation, reading order, grouping, text reconstruction, barcode detection/classification, XObject resolution, scene construction, and semantic-layout analysis.
- [x] Inventory all 41 public processing methods on the selected types by exact reflected signatures; new methods on those types must receive an executable admission case. This is not full assembly/type coverage.
- [x] Keep DTOs, pure object-graph lookups, general geometry, metadata, and engine/configuration construction free; do not add public server-mode or all-product exemptions.
- [x] Keep Importer enforcement explicitly partial in metadata and CI; at this checkpoint color/retained-font processing, editing/Trust, regeneration/dependency rights, image analysis, and OCR remained open. The following milestone supersedes the color/font status only.
- [x] Pass the final enhanced probe: 290 distinct checks, including 31 prior import/load/save entries and 41 direct-engine methods, real parsed PDF content, custom-decoder dispatch, and retained-state renewal checks.
- [x] Complete and audit 860 .NET cases on the changed source: all 23 FileImporter, 661 Importer, and 80 licensing-core tests plus 96 targeted API/worker/export cases. Verify TRX counters and result rows with no failures, skips, errors, timeouts, aborts, or inconclusive cases.
- [x] Pass 46 native-tooling/version/release-author contracts and validate metadata and the Major/Breaking `native-import-direct-engine-license-enforcement.json` fragment.
- [x] Verify final probe/session cleanup, unchanged empty source trust and regular Licensing build outputs, no remaining session build trees, and passing naming/diff/version guards.
- [ ] Implement admitted-operation snapshot propagation so nested work can finish under its original admission during concurrent renewal/expiry. Per-public-invocation checks in this milestone do not establish that guarantee.

The final probe and its cleanup flags are retained under
`artifacts/native-engine-tests/1794782409426-59e39e4f/` as
`importer-admission.json` and `run.json`. All 290 check names are unique.
The earlier successful 286-check report is retained under
`artifacts/native-engine-tests/1794781856278-13884845/`; four additional checks
exercise custom-decoder dispatch and actual parsed PDF content in the final
run. Both probes built without errors and with the six existing PPTX nullable
warnings, and removed their isolated artifacts without changing regular
Licensing outputs or source trust.

Regression evidence is under
`artifacts/native-test-sessions/4c889cae-11a0-440a-bce6-0eb6ca558b59/`:
`direct-importer-files.trx`, `direct-importer-importer.trx`,
`direct-importer-core.trx`, `direct-importer-api.trx`, per-project logs, and
`cleanup.json`. The shared session removed its signed fixtures and build tree;
all cleanup flags passed independent inspection. Existing compiler/analyzer and
dependency-license warnings remain; no supplier terms were accepted.

These results are local macOS/x64 .NET validation, not full API/PostgreSQL,
52-host coverage, visual-parity, obfuscated-package, or cross-platform CI
acceptance. Parser/decoder/layout algorithms, existing fixtures, baselines,
thresholds, and source manufacturer trust were not changed. The Importer
product remains partially enforced. VERSION and published releases remain
unchanged; no commit, push, registry publication, database change, or restart
is included.

### Native Importer Color And Font Milestone

- [x] Guard color-space resolution/conversion, PDF-function creation/evaluation, and color-content rewriting before resolver callbacks, component access, or output construction.
- [x] Guard internal image-color conversion and ICC transformation before processing; keep existing color, decompression, and ICC algorithms unchanged.
- [x] Guard retained font-resource decoding/glyph extraction, identity encoding, and Unicode-map decoding. Preserve free mapping construction, glyph-width lookup, attributes, function metadata, and convertibility inspection.
- [x] Extend the exact-signature public inventory from 41 to 50 processing methods; explicitly classify only the existing free width/mapping signatures.
- [x] Verify wrong-product rejection and renewal on actual parsed fonts and exponential, sampled, PostScript, array, and stitching functions. Ensure custom encoding/function callbacks and component reads do not occur on denied calls.
- [x] Stop masking native-license failures as unsupported content in color/function/image decoder fallbacks; test real nested replacement failures for sampled, PostScript, and ICC decoding.
- [x] Pass the initial enhanced probe with 354 distinct checks. Retain its evidence under `artifacts/native-engine-tests/1794810523767-19bc8b70/`; the final strict-ICC probe below remains separate.
- [x] Pass the final probe with `PVAD_REQUIRE_LCMS2=1`: 357 distinct checks, 50 direct processing methods, and 31 prior import/load/save entries. Verify real ICC output, retained ICC denial/renewal, and cleanup; no unavailable-library fallback was taken.
- [x] Complete and independently audit all 860 .NET regression cases: full FileImporter (23), Importer (661), licensing-core (80), and targeted API/worker/export (96). TRX counters and result rows show no failures, skips, errors, timeouts, aborts, or inconclusive cases.
- [x] Pass 46 native-tooling/version/release-author contracts; prepare the Major/Breaking `native-import-color-font-license-enforcement.json` fragment without changing VERSION or published releases.
- [x] Record final evidence and verify naming, metadata, cleanup, and diff guards. Source trust stays empty, regular Licensing outputs stay unchanged, and no session build trees remain.

At this color/font checkpoint, editing/retained document processing remained
open; the following milestone supersedes that status only. Importer enforcement
remains partial. Trust/security/redaction/signing/e-invoice operations, regeneration/dependency
rights and admitted-operation lifetime, image analysis, and OCR remain open.
Free profile-support inspection is not a native color transform. Third-party
encoding overrides remain responsible for their own direct implementations;
PVAD's resource wrapper prevents dispatch when its admission fails.

Final probe evidence is retained under
`artifacts/native-engine-tests/1794811123960-e0df3702/` as
`importer-admission.json` and `run.json`. All 357 check names are unique.
The local x64 LittleCMS library performed the actual sRGB ICC transform; the
probe also rejected the retained transform with wrong rights and restored it
after renewal. The same existing synthetic profile factory used by the color
regression suite supplied the test profile. This is local transform/admission
evidence, not independent color conformance or cross-platform packaging proof.
Both the initial and final probes built with zero errors and only the six
existing PPTX nullable warnings. Signed temporary licenses/trust and isolated
binaries were removed without changing source trust or regular Licensing builds.

Regression evidence and per-project build/test logs are under
`artifacts/native-test-sessions/a072bca6-1908-4a6f-a7b4-a6d209d38091/`:
`color-font-importer-files.trx`, `color-font-importer-importer.trx`,
`color-font-importer-core.trx`, `color-font-importer-api.trx`, and
`cleanup.json`. Independent inspection confirms all result counts and cleanup
flags; the signed temporary fixtures and isolated build tree were removed.
These are local macOS/x64 .NET results, not a full API/PostgreSQL or 52-host
coverage rerun, visual review, platform CI run, or protected-package acceptance.
Existing compiler/analyzer and dependency-license warnings remain; no supplier
terms were accepted. Source manufacturer trust, published releases, databases,
and registries are unchanged. No commit, push, deployment, or restart is part of
this milestone.

### Native Importer Editing Milestone

- [x] Guard session insert/text/move/delete/metadata operations, page editing/composition, retained facade load/edit/compose/count/output, and content-stream rewriting before processing.
- [x] Propagate native license exceptions rather than converting them into ordinary facade failure statuses. Keep status inspection and disposal free.
- [x] Validate replacement file/stream bytes before committing the new loaded document. Preserve the old document after invalid replacement input.
- [x] Preserve ref outputs, streams/ownership, files, status/error references, selected page/measurement units, model mutations, and lazy input callbacks on initial admission denial.
- [x] Introduce a bounded internal blank-page dependency adapter through the existing PDF writer; accept only dimensions and a fresh Importer admission, no caller document or drawing callback. Keep public PDF admission unchanged.
- [x] Pass the initial 520-check probe covering 38 Editing entries and the prior 50 direct/31 import-load-save entries. Evidence: `artifacts/native-engine-tests/1794812082051-37e00e52/`. The final probe adds both content-rewriter overloads below.
- [x] Pass the final Importer probe with 528 distinct checks, including 40 editing/content-rewriter entries, the prior 50 direct-engine methods, and 31 import/load/save entries; retain real ICC validation. Re-run all 20 PDF-only admission checks after the shared-writer change.
- [x] Independently audit all 1,096 final regression cases: FileImporter 23, Importer 661, Generator 141, PDF infrastructure 70, licensing-core 80, and targeted API/worker/generated-migration-code 121. Two further 70-case PDF runs add 140 repeated passes, not additional unique coverage. All final TRX counters and result rows show zero failures/skips/errors/timeouts/aborts/inconclusive cases.
- [x] Record the initial RTL infrastructure failure without relaxing tests: one width assertion failed in `RtlMixedTextShapingTests.MixedRunsRetainNumbersAndClusterIntegrity`; three subsequent fresh test processes each passed all 70 PDF infrastructure cases unchanged.
- [x] Pass 46 native-tooling/version/release-author contracts and prepare the Major/Breaking `native-import-editing-license-enforcement.json` fragment. VERSION and published releases remain unchanged.
- [x] Verify final naming/metadata/diff guards and cleanup. No session build trees or editing temporary directories remain; source trust remains empty and regular Licensing outputs remain unchanged.

The initial broader run is retained under
`artifacts/native-test-sessions/55a3e299-a98d-4635-b869-ec6ff09228f8/`.
FileImporter (23), Importer (661), and Generator (141) passed; PDF infrastructure
had 69 passes and one RTL failure, so core/API were not executed in that run.
No font-shaping algorithm, font asset, visual baseline, or test threshold was
changed in response. The RTL root cause is not established: the subsequent
passing runs are repeat evidence, not proof of a corrected font bug.

The limited blank-page adapter does not complete general regeneration,
debug-rendering dependencies, admitted-operation snapshot propagation, or
Trust/security/redaction/signing/e-invoice/OCR enforcement. The Importer product
remains explicitly partial. No commit, push, publication, or restart is included.

Final regression evidence and build/test logs:
`artifacts/native-test-sessions/666b3813-80a4-4e59-87c6-36dfc49f867c/`.
Primary files are `editing-final-files.trx`, `editing-final-importer.trx`,
`editing-final-generator.trx`, `editing-final-pdf.trx`,
`editing-final-core.trx`, and `editing-final-api.trx`.
The two extra PDF process runs are `editing-final-pdf-repeat-2.trx` and
`editing-final-pdf-repeat-3.trx`. All counters and individual result rows were
independently checked, and all shared-session cleanup flags passed inspection.
These are local macOS/x64 .NET results, not a new full API/PostgreSQL, 52-host
coverage, visual, or platform CI acceptance. Existing compiler/analyzer and
third-party license-decision warnings remain; no supplier terms were accepted.

Final native probe evidence:

- Importer: `artifacts/native-engine-tests/1794813116748-d27856db/importer-admission.json` and `run.json` (528 unique checks; 40 editing entries).
- PDF-only: `artifacts/native-engine-tests/1794812994001-52ee66b7/pdf-admission.json` and `run.json` (20 unique checks).

All run/cleanup flags and result counts were independently inspected. Temporary
editing documents, signed fixtures/trust, and isolated binaries were removed;
regular Licensing builds, source trust, VERSION, and published releases were
unchanged. The Importer probe required real LittleCMS and still rejected
ordinary PDF construction without a PDF grant. No public writer/constructor
license exemption was added.

## Native Importer Trust And Security Milestone

This milestone extends native Importer admission, not the PDF Trust feature
implementation itself. The Importer product remains partially enforced.

- [x] Guard 31 public processing methods/constructors across Trust inspection/change analysis/post-processing, archival conversion, CRL/OCSP, password/certificate decryption, AES-256, PKCS#12/PAdES/RFC 3161, redaction/audit verification, invoice attachment, and associated-file extraction.
- [x] Check admission before input access, graph changes, certificate/key loading, signing callbacks, timestamp requests, and output production. Retained AES handlers and signing keys recheck the current snapshot.
- [x] Keep DTOs, certificate URI locators, capability flags, certificate metadata, unconfigured audit-key providers, and disposal free. Audit retrieval that cryptographically verifies evidence is processing, not a pure metadata read.
- [x] Propagate native failures through broad Trust/compliance/redaction recovery catches rather than reporting invalid or unsupported content. Test a license replacement from inside a source-stream read.
- [x] Pass 655 distinct Importer probe checks on macOS/x64, including the previous 31 import/save, 50 direct-engine, and 40 editing entries plus 31 Trust/security entries. The Trust addition includes 27 missing-license admissions, 31 wrong-product denials, and valid/renewed Importer-only execution of all 31 entries. Four retained methods are prepared only after admitted creation; no private object construction or license reset bypass is used.
- [x] Exercise real synthetic PAdES signatures, AES-256 byte encryption/decryption, password/certificate PDF decryption, CRL/OCSP verification, an in-memory RFC 3161 responder, redaction/audit verification, PDF/A conversion, and byte-exact XML attachment extraction. Prepare generated PDF fixtures separately under a PDF-only license and restore Importer-only rights before each processing phase.
- [x] Guard API upload processing and inspection-job enqueue before input persistence, background inspection before storage access, configured audit-key loading before filesystem access, and audit retrieval before database lookup/verification.
- [x] Pass all 389 selected API cases, including affected actions with synthetic authenticated/recent sessions, independent anonymous/permission failures, native-license failures, denied job/key loading, and Importer-only recovery. Keep production authorization and step-up requirements unchanged.
- [x] Execute and independently audit 1,364 distinct selected cases across the full Importer (661), FileImporter (23), Generator (141), PDF infrastructure (70), licensing-core (80), and targeted API (389) suites. Record the primary PDF failure and two complete passing repeats separately rather than claiming a clean overall run.
- [x] Resolve the repeatedly observed RTL-width instability through the deterministic font-buffer ownership reproduction and correction below, not through passing retries. Do not infer PostgreSQL, other-platform, or package acceptance from these local runs.
- [x] Pass all 46 native tooling/version/release-author contracts; extend the CI selection to the affected Trust/signature/redaction/e-invoice/API tests without enabling optional database tests on unsupported platform runners.
- [x] Prepare the Major/Breaking `native-import-trust-license-enforcement.json` fragment and update the operation inventory and native licensing documentation. Product VERSION and published release history remain unchanged.
- [x] Verify final naming/diff/version guards and fixture/build cleanup after all regression runs; source trust remains empty and VERSION/published releases are unchanged.

### Probe Evidence

`artifacts/native-engine-tests/1794820247601-ce02b04d/` contains
`importer-admission.json` and `run.json`: 655 unique checks, 31 Trust/security
entries, successful cleanup, unchanged empty source trust, unchanged regular
Licensing builds, and isolated build directories. The probe required actual
LittleCMS processing; the source PDFs and certificates are synthetic.

Initial probe development exposed two incorrect result-property names in the
test helper and an attachment fixture that required archival conversion. Only
the helper/fixture was corrected; the production archival preflight was not
relaxed. The initial API helper omitted a required job payload and then used an
anonymous client for protected routes. Both test setup issues were corrected;
production authorization is unchanged. An initial broader API selection also
included two opt-in PostgreSQL immutability tests, which were skipped without
their database-test environment flag. They are outside this native-admission
selection and are not counted as passed. These initial results remain in
`artifacts/native-test-sessions/8e805cb3-ee9e-4964-aa08-be55174ad5dc/` and
`artifacts/native-test-sessions/3a552618-4cda-4061-bd6c-6ecf95675f12/`.

### Regression Evidence And Open Failure

`artifacts/native-test-sessions/917b8c06-9dbe-4f44-8d66-4b1076293c3f/`
contains the final `trust-native-api.trx` (389), `trust-native-importer.trx`
(661), `trust-native-files.trx` (23), and `trust-native-generator.trx` (141):
all passed with no skips/errors/timeouts/aborts/inconclusive cases. The complete
`trust-native-pdf.trx` contains 69 passes and one failure in
`RtlMixedTextShapingTests.MixedRunsRetainNumbersAndClusterIntegrity` for mixed
Arabic/numeric text: expected width 4383.72, measured 92.376. This is the same
failure already retained at the editing checkpoint, not a new clean acceptance.

`artifacts/native-test-sessions/a8c79734-07f8-4b38-80d6-622f95136f38/`
contains `trust-followup-core.trx` (80 passes) and two separate complete PDF
process runs, `trust-followup-pdf-repeat-1.trx` and
`trust-followup-pdf-repeat-2.trx` (70 passes each). These repeats are not
additional unique coverage and do not close the intermittent failure.
`trust-validation-audit.json` independently verifies counters and result rows:
1,364 unique selected cases, 1,504 total executions, 1,503 passed executions,
one failed execution, no skips, and `unqualifiedRegressionAcceptance: false`.
Both sessions' `cleanup.json` files confirm removal of temporary trust/licenses
and isolated builds. No font source, font asset, shaping algorithm, assertion,
or visual threshold was changed to obtain passing repeats.

### Remaining Importer Work

General regeneration/debug rendering, scoped dependency rights and operation
lifetime propagation, and image analysis/OCR/worker admission remain open.
The root operation-matrix status and `importer.enforced: false` are unchanged.
That Trust checkpoint made no claim of complete native Importer enforcement,
cross-platform execution, obfuscated-package acceptance, or an RTL fix. The
font-lifetime follow-up below subsequently resolves the observed RTL failure.

## Font Shaping Buffer Lifetime Follow-Up

This follow-up addresses the intermittent RTL-width failure recorded above.
The earlier failed and repeated runs remain historical evidence; they are not
rewritten as successful runs.

- [x] Verify the exact installed HarfBuzzSharp 14.4.0.100 source revision: `Blob.FromStream` hands native code a managed-array pointer in read-only mode after its temporary pin ends.
- [x] Reproduce changed native font bytes with a deterministic compacting-GC ownership test before changing the implementation. Keep the failed TRX and cleanup record.
- [x] Give shaping a native-owned duplicate while the input array is pinned; release the pin in `finally`, and dispose native blob/face/font handles through their existing scoped lifetimes. Add no public API, unsafe compilation flag, temporary file, cache-based workaround, or process-wide lock.
- [x] Preserve all shaping/direction rules, font files, approved visual references, and existing assertions/tolerances.
- [x] Pass the ownership regression, all 10 mixed-direction/GC cases, and three complete 75-case PDF runs on the first corrected implementation. These repeated cases are not additional unique coverage.
- [x] Verify the additional exact 92.376-point width assertion for the original mixed Arabic/currency example at 12 pt in the final 76-case PDF suite.
- [x] Complete and independently audit all 1,370 final affected cases: API 389, PDF infrastructure 76, Generator 141, Importer 661, FileImporter 23, and licensing-core 80. All counters and result rows show passed, with no failures/skips/errors/timeouts/aborts/inconclusive results.
- [x] Re-run all 20 direct PDF licensing checks after the shared engine change.
- [x] Re-run all 655 Importer licensing checks with required real ICC processing.
- [x] Enforce all five local platform parity packs: 118 cases, zero P0/P1, 113 matches, and five visible P2 findings. Inspect Arabic/date results: zero geometry drift and pixel-identical normal/C# PDFs. Preserve thresholds and references; do not infer Linux/Windows execution or new human approval from a local run.
- [x] Add a customer-facing Patch fragment and explain the stable font-lifetime contract in the Preview guide. Keep VERSION and published release history unchanged.
- [x] Verify final guards, local links, fixture hashes, and temporary signed-build cleanup; retain the failed reproduction and earlier intermittent failures. Close this local RTL blocker only, not the remaining native-product or release gates.

### Root Cause And Red/Green Evidence

The installed NuGet manifest identifies upstream revision
`c4ef20229bd56e41731aa94eebe8b082b8ef886c`. Its
[stream-to-blob implementation](https://github.com/mono/SkiaSharp/blob/c4ef20229bd56e41731aa94eebe8b082b8ef886c/binding/HarfBuzzSharp/Blob.cs)
creates an array-backed native blob without retaining or pinning that array
for the native object's lifetime. The installed binding documents that
`MemoryMode.Duplicate` instead copies the bytes immediately. The correction
uses that ownership contract; it does not suppress GC or change font metrics.

The value 4383.72 in the old failure was derived from the first, corrupted
shaping result. It was not an approved expected text width. The healthy run
measured 92.376 points; the final regression explicitly asserts that value.

The failing ownership test is retained at
`artifacts/native-engine-tests/1794822555645-1bc99bda/licensed-tests.trx`.
It failed with changed native font bytes after a compacting collection; the
runner's `run.json` records the expected failed test run and successful cleanup.

`artifacts/native-test-sessions/b3d4a4a2-81db-407e-a12c-249ffcd281d2/`
contains the corrected ownership test, 10 mixed-direction/GC cases, and three
complete 75-case runs (`font-lifetime-*.trx`). A subsequent exact-width test
adds one case to the final suite; those initial 75-case runs are preserved.
The direct PDF probe passed at
`artifacts/native-engine-tests/1794822920911-e31f5ad6/`.

### Final Validation

`artifacts/native-test-sessions/088f6efa-343e-48a3-abd8-90af6acfbf8c/`
contains `font-final-api.trx`, `font-final-pdf.trx`,
`font-final-generator.trx`, `font-final-importer.trx`, `font-final-files.trx`,
and `font-final-core.trx`: all 1,370 selected cases passed without skips. This
is a clean final selection on the corrected code, not a relabeling of the
previous failed runs. The final Importer probe and cleanup evidence are at
`artifacts/native-engine-tests/1794823072271-06414519/` (655 checks).

The browser report is `artifacts/platform-parity/darwin-arm64/report.html`,
with fixture-specific JSON, screenshots, crops, overlays, and PDFs. The five
packs are font-families (22), date-format (18), date-historical (18),
date-layout (20), and all-elements (40). All 118 cases pass the existing
enforcement rules; all normal/C# PDF element comparisons have zero pixel
difference. The five P2 cases are serif-bold, mono-bold, mono-bold-italic,
short-height, and text. They are not silently classified as exact matches.

An extra comparison against the three historical font approvals confirms that
their six backend/C# crops remain pixel-identical. The six current browser
crops differ from those historical images. No renewed approval is asserted for
changed browser pixels; the existing thresholds/references remain untouched.
`font-p2-approval-check.json` retains this distinction. The local environment
uses bundled Chromium 156.0.8078.4 on macOS/ARM64 with x64 .NET. Linux/x64,
Windows, and authenticated customer-browser acceptance remain separate.

The final session's `font-validation-audit.json` records the reproduction,
strict TRX counters, visual findings, font/input protection checks, and cleanup.
All 53 native/version/release-author/font contracts passed. No production key,
license, database, VERSION, or published release was changed; nothing was
committed or pushed.

## Native Importer Regeneration And Debug Milestone

This milestone closes the built-in regeneration/mapping/debug-overlay boundary,
not generic bundle rights, image-analysis PNG rendering, OCR, or whole-operation
admission lifetime. The Importer product remains explicitly partial.

- [x] Guard both `PvadPdfGeneratorBridge.RegenerateAsync` overloads, all three map methods, `PdfGeneratorBridgePipeline.SaveAsync`, and `PdfDebugOverlayBuilder.Build` before processing or custom dispatch.
- [x] Render standard regeneration through an internal adapter that creates a fresh private document and returns bytes only. Keep the existing blank-page adapter restricted to empty pages. Add no public mode, ambient all-products grant, or public license-exempt constructor/writer.
- [x] Require explicit Importer and PDF rights before non-null caller generator callbacks. Keep null callbacks on the Importer-only path. Recheck after callback replacement and keep ordinary generator/renderer guards effective for retained handles and reentrant output streams.
- [x] Preserve output bytes and position on denied admission, pre-processing cancellation, and throwing/revoking callbacks; prevent invocation of a custom pipeline before admission. Do not claim atomic writes for caller streams that fail during `WriteAsync`.
- [x] Pass the isolated Importer probe with 706 distinct checks, including seven regeneration/debug processing signatures, required ICC processing, a multi-page text/path/Flate-image/shading case, rotation, both advanced-callback rights, and explicit no-public-boundary checks.
- [x] Re-run direct PDF admission after modifying the internal document/writer dependency boundary: all 20 checks passed.
- [x] Complete and independently audit affected .NET suites, including the full Importer bridge fixtures, PDF infrastructure, Generator, FileImporter, licensing core, debug API Importer-only recovery, and existing Viewer annotation-flattening integration: all 1,381 selected tests passed without skips.
- [x] Document the advanced-callback distinction and current host Viewer callback requirement. Keep generic bundle rights and operation lifetime open rather than treating the adapter as a global exemption.
- [x] Update covered/remaining matrix entries and CI contracts without changing `importer.enforced: false` or the three fully enforced product flags.
- [x] Prepare the Major/Breaking `native-import-regeneration-license-enforcement.json` fragment; leave VERSION and published releases unchanged.
- [x] Verify final naming/version/release/link guards, no source trust changes, and complete cleanup of synthetic licenses and isolated builds; record exact validation evidence below.

### Initial Probe Evidence

`artifacts/native-engine-tests/1794831118468-6dcb2aa7/` contains
`importer-admission.json` and `run.json` for the 706-check probe. All check names
are unique. A preliminary 700-check run is retained at
`artifacts/native-engine-tests/1794830775409-52edffbe/`.

The initial expanded image fixture used an unfiltered RGB stream, which the
existing regeneration image adapter does not support. That test fixture was
changed to a correctly compressed Flate image; no renderer/filter support or
validation was loosened. The failed run and cleanup evidence remain at
`artifacts/native-engine-tests/1794831019427-9bd6f1d2/`.

The combined callback test license contains Importer/PDF/Word/Spreadsheet grants.
The standard-regeneration and reentrant-denial cases use only Importer rights;
they do not imply extra product access. Viewer flattening deliberately uses the
public advanced callback and retains both Importer/PDF requirements. Further
host-specific bundle rights remain unfinished.

### Final Validation Evidence

The isolated signed session is
`artifacts/native-test-sessions/b2366b58-6434-440a-9a2e-111f3736ea23/`.
Its six TRX reports contain 661 Importer, 400 selected API, 76 PDF
infrastructure, 141 Generator, 23 FileImporter, and 80 licensing-core tests.
All 1,381 result rows are Passed; total, executed, and passed counters agree,
and failure, error, timeout, aborted, inconclusive, and not-executed counters
are zero. The API selection includes the new Importer-only debug route and
existing annotation-flattening integration. This is not the full API suite.

`regeneration-validation-audit.json` independently records these counters,
the 706 distinct Importer checks, and the 20 distinct ordinary PDF admission
checks at `artifacts/native-engine-tests/1794831560731-c49ce04c/`.
The session cleanup and both engine-run reports confirm removal of synthetic
licenses and isolated builds, unchanged source trust, and unchanged regular
license artifacts. The source verification-key registry remains empty.

All 46 native-tooling/version/release-author contract tests passed. Native
metadata, version synchronization, pending release-fragment validation,
former-name and Markdown naming guards, local documentation links, and
`git diff --check` passed. VERSION remains 1.1.0; the published release
manifest is unchanged. No production key, database, publication, commit, push,
new visual approval, or cross-platform run is part of this milestone.

## Native Image Analysis And OCR Milestone

This milestone covers image analysis, OCR admission, isolated worker verification,
and the API's built-in OCR PDF output. Generic product-bundle dependency rights
and admitted-operation lifetime remain open; `importer.enforced` remains false.

- [x] Inventory 43 public processing signatures: five image importer overloads, public preprocessing/color/shape/text/glyph/template processing, scene assembly, PNG debug rendering, converter, both OCR engines, and default process runner. Keep DTOs, metadata, disposal, and generic numeric color-distance/correlation helpers free.
- [x] Check Importer rights before input reads, atlas/native work, temporary input writes, or custom engine/runner dispatch. Verify denial both before first use and after atlas/native use and license replacement.
- [x] Atomically retain the original verified signed envelope with its payload, with no public export or runtime trust-injection API. Test independent returned buffers, invalid-renewal preservation, expiry, and concurrent replacement.
- [x] Transfer the admitted signed snapshot through a unique temporary file, with owner-only Unix file permissions, and override inherited child license configuration. Independently verify the worker's embedded trust, expiry, release eligibility, and Importer grant before reading request/image data.
- [x] Preserve known worker license diagnostics through the existing response and typed exception. Reject malformed and tampered licenses without logging license contents; keep language/native errors separate.
- [x] Guard both image API routes before upload consumption and retain HTTP 503 native-license Problem Details. Render only the built-in OCR result through a private Importer-only byte adapter using the existing mapper; ordinary public PDF mapping/construction/output remains licensed separately.
- [x] Pass the final isolated image probe with 229 unique checks, including a real local macOS/x64 Tesseract worker on a synthetic blank image, actual process timeout/cancellation cleanup, and temporary-file permissions. Require this native execution explicitly with `PVAD_REQUIRE_NATIVE_OCR=1`.
- [x] Complete and independently audit image-analysis, OCR, licensing-core, selected API, PDF, Generator, Importer, and FileImporter regression suites, including Importer-only PDF output and wrong-product callback renewal: all 1,670 selected tests passed without skips.
- [x] Update native metadata, CI coverage, and documentation without promoting the partial Importer product or claiming cross-platform native OCR acceptance. Prepare the Major/Breaking `native-image-ocr-license-enforcement.json` fragment; keep VERSION and published releases unchanged.
- [x] Verify final naming/version/release/link guards, unchanged source trust, and cleanup of signed fixtures and isolated binaries; record exact evidence below.

### Probe Evidence

The initial probe passed 177 checks at
`artifacts/native-engine-tests/1794832608469-a24b22d8/`. The expanded 227-check
probe is `artifacts/native-engine-tests/1794832742899-28f27cf1/`, with
`image-admission.json` and `run.json`. Both explicitly required real native OCR;
the blank image exercises successful native processing, not recognition accuracy.
The additional checks cover tampered signatures, failed process-start cleanup,
and denial after prior cache/native use. No original image fixture, recognition
algorithm, golden baseline, or threshold was changed.

The real process tests distinguish admission failure before request access from
a licensed worker's deliberate missing-language-data failure. The latter is not
native-success evidence. A separate actual Tesseract worker run establishes
`realNativeSucceeded: true` locally. Prepared Linux/Windows/ARM64 workflows and
existing tests with optional early returns are not native-runtime acceptance.
Windows temporary-file ACL hardening remains a deployment responsibility.
The worker rechecks at execution time; this does not promise uninterrupted
composite-operation admission after expiry or renewal.

### Final Validation Evidence

The final image probe is
`artifacts/native-engine-tests/1794833164166-52693a89/`: 229 distinct checks
across 43 processing signatures, with required real native OCR execution and
portable test-child modes for actual timeout, cancellation, and temporary-file
permission checks. The earlier 177- and 227-check reports remain intact.
The ordinary PDF probe passed all 20 distinct checks at
`artifacts/native-engine-tests/1794833216270-45e6e91d/`, confirming that
Importer-only admission does not authorize public PDF creation/output.

Regression reports and `image-validation-audit.json` are at
`artifacts/native-test-sessions/c4e60e68-cd27-414b-b826-1ff0fbec2219/`.
The eight reports contain 181 image-analysis, 100 OCR, 82 licensing-core,
406 selected API, 76 PDF infrastructure, 141 Generator, 661 Importer, and
23 FileImporter tests. All 1,670 result rows are Passed, and the total,
executed, and passed counters agree. Failed, error, timeout, aborted,
inconclusive, and not-executed counters are zero. API coverage includes the
existing real OCR controller tests and the new synthetic-engine Importer-only
output/renewal test; it is not the full API suite.

The audit distinguishes the independently required native blank-image worker
run from ordinary OCR tests with optional early returns. Only the former
establishes native execution; neither establishes broad OCR accuracy or
cross-platform acceptance. Both engine-run reports and the signed-session
cleanup report confirm removal of synthetic licenses and isolated builds,
unchanged source trust, and unchanged retained license artifacts. The source
key register remains empty; original image fixtures and bundled assets are
unchanged.

All 46 native-tooling/version/release-author contracts passed. Native metadata,
version synchronization, fragment validation, former-name and Markdown naming
guards, 35 local documentation links/anchors, and `git diff --check` passed.
VERSION remains 1.1.0 and published releases are unchanged. No commit, push,
deployment, live database change, new visual approval, or platform acceptance
is asserted.

## Native Operation Continuation Milestone

This milestone provides internal continuation infrastructure and migrates the
complete image-analysis and in-process OCR/API-output paths. It does not close
all native composite call chains or promote the Importer product to enforced.

- [x] Add an internal execution-context-local operation scope with immutable original admission, exact originating-verifier binding, stack-order disposal, parallel isolation, and a shared closed flag for captured/descendant contexts. Public `Require` always verifies the current license and never reads the internal slot.
- [x] Centralize purpose-bound private PDF dependencies for Importer regeneration, fresh blank pages, and built-in OCR-result bytes. Reject mismatched purposes and cross-product continuations; expose no public scope, dependency selector, or trust override.
- [x] Split all 39 public image-analysis processing entries into fresh-admission wrappers and internal-only continuations. Keep explicit `IFileImporter` dispatch on its public admission boundary. Preserve public signatures, algorithms, fixtures, and thresholds.
- [x] Carry admitted OCR conversion through built-in engine continuations and API PDF completion. Custom callbacks can finish their admitted result but cannot start new protected public work with the old admission.
- [x] Keep new OCR process dispatch and worker startup independently current-license checked. Reject unsigned/backdated admission claims; verify staged-input cleanup on denied dispatch.
- [x] Pass 87 core tests, including deterministic Trial expiry, purpose restrictions, failed nested admission, current public verification, closed captured/child contexts, nested restoration, and 64 parallel contexts.
- [x] Complete final isolated image/PDF/Importer admission probes and independently audit affected .NET regressions, including the API completion/new-request distinction: 239 image/OCR, 20 PDF, and 706 Importer checks; all 1,675 selected .NET tests passed without skips.
- [x] Record partial continuation coverage and remaining composite pipelines in metadata; add the Patch `native-image-operation-continuations.json` fragment without changing VERSION or published releases.
- [x] Verify final tooling/version/naming/link guards, source trust and fixture preservation, and synthetic-session cleanup; record exact evidence below.

The preliminary context probe passed 237 checks at
`artifacts/native-engine-tests/1794834509355-d0facc97/`.
The final expanded probe adds explicit default-worker fresh-dispatch and
staged-input cleanup checks. The earlier image/OCR checkpoint remains historical:
its API test rejected mid-conversion renewal; the new behavior deliberately
allows internal completion and rejects the next public request instead.

### Final Continuation Evidence

The final verifier-bound signed session is
`artifacts/native-test-sessions/a819f984-0515-4ca7-8f8a-78234915d644/`.
Its `bound-continuation-*.trx` reports contain 87 licensing-core, 181
image-analysis, 100 OCR, 406 selected API, 76 PDF infrastructure, 141
Generator, 661 Importer, and 23 FileImporter tests. All 1,675 result rows
are Passed; total, executed, and passed counters agree, with zero failed,
error, timeout, aborted, inconclusive, or not-executed counters. This is a
targeted API selection, not a new full API/PostgreSQL acceptance run.

`continuation-validation-audit.json` independently records the counters and
cleanup. `analysis-body-audit.json` compares all nine image-analysis source
files with HEAD after explicitly removing admission wrappers/guards, restoring
generated method aliases, normalizing whitespace, and accounting for the two
equivalent expression-to-return adaptations. It confirms 39 public wrappers
and unchanged remaining analysis bodies; it is not a new image-accuracy or
visual-baseline approval.

The final real-native image probe passed 239 distinct checks at
`artifacts/native-engine-tests/1794835364630-84c709e6/`.
The final ordinary PDF and Importer probes passed 20 and 706 distinct checks at
`artifacts/native-engine-tests/1794835552700-d8676382/` and
`artifacts/native-engine-tests/1794835606471-2a38ffdb/` respectively.
Each run verifies removal of synthetic licenses/builds and unchanged source
trust/regular license artifacts. The final session build tree is removed;
the source key registry remains empty. Original image fixtures and bundled
assets are unchanged. Native OCR evidence is local macOS with x64 .NET on
a synthetic blank image, not cross-platform acceptance or OCR accuracy.

The preliminary session at
`artifacts/native-test-sessions/71572abd-45c0-48ba-b83a-6738e19298cc/`
remains separate from the final verifier-bound rerun. Earlier probe reports
are retained rather than relabeled as final results. All 47 native-tooling,
version, and release-author contracts passed. Metadata, version/fragment,
former-name/Markdown naming, 37 local documentation links/anchors, and diff
guards passed. VERSION remains 1.1.0; published releases, production trust,
live databases, and services were not changed. Nothing was committed or pushed.

### Remaining Continuation Work

- [ ] Finish composite PDF admission beyond typed load/save, Designer DTO/analysis mapping and editing/composition: Trust/security/signing/redaction/e-invoice pipelines. The milestones below close their respective subsets; this broader task remains open.
- [ ] Propagate advanced regeneration and remaining decoder/color/font call chains outside the typed-load path end to end; retain explicit PDF rights before exposing a public generator callback.
- [ ] Audit and migrate remaining Word/Spreadsheet and later Converter/Migration composite call chains. Existing single-entry processing and guard coverage do not establish a complete lifetime guarantee for every pipeline.
- [ ] Validate protected/package/platform consumers after these remaining pipelines are migrated. New worker dispatch/start remains a fresh-admission boundary by design, not an unsigned host-side lifetime exemption.

## Typed PDF Load And Save Continuation Milestone

This milestone is limited to typed PDF loading, its built-in parsing/decryption/
decoding/font/graphics dependencies, and full/incremental saving with validation.
It does not cover the separate Designer PDF-to-DTO analysis chain, Editing,
Trust or advanced regeneration end to end. Importer remains partially enforced.

- [x] Add internal Importer-owned load/save purposes without granting private PDF-generation dependency rights. Keep new public entries current-license checked and preserve public signatures and existing diagnostic codes.
- [x] Carry typed load admission through both seekable and non-seekable input, xref/token/object parsing, password/certificate decryption, default stream decoders, default font parsers/encodings, and graphics/document building.
- [x] Carry full rewrite, incremental append and strategy-based saving through source parsing, update validation, encryption and output validation. Preserve signed-source and malformed-update rejection.
- [x] Preserve custom decoder/font/encoding override dispatch. Keep reentrant and retained public PVAD operations subject to fresh admission, including when a callback changes the active license.
- [x] Correct read-buffer ownership: return rented buffers after failures, never pool non-pooled arrays, make disposal idempotent, and retain stable source bytes for loaded stream objects.
- [x] Pass the expanded Importer probe with 127 new continuation checks (833 checks in total); keep all earlier failed compile/runtime/probe attempts as separate evidence.
- [x] Complete and independently audit the final affected .NET suites, ordinary PDF/image probes, and cleanup after the buffer-lifetime regression fix: 1,680 .NET tests, 20 ordinary PDF and 239 strict native-image/OCR checks passed.
- [x] Validate metadata, all 48 native/version/release-tool contracts, naming/link guards and the complete diff; add separate Patch fragments without changing VERSION or published releases.

The expanded probe passed at
`artifacts/native-engine-tests/1794838026650-8f3aff6e/` with 833 distinct
checks, including 127 new continuation checks. Its cleanup report confirms
synthetic-only artifacts, removal of temporary licenses/builds, and unchanged
source trust and regular license build. Earlier attempts at
`1794837384728-db75c098`, `1794837623926-5457e19a` and
`1794837816783-c9c187cc` remain retained as failures, not acceptance evidence.
They exposed a missing probe import, an internal token-reader public recheck,
and a repeated callback-check name, respectively; all were corrected before
the successful rerun.

### Typed PDF Regression Evidence

The final signed regression session is
`artifacts/native-test-sessions/ff1ac54a-429b-4b40-98e5-fd32a3f194b3/`.
Its `pdf-save-*.trx` reports contain 87 licensing-core, 666 Importer, 23
FileImporter, 141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR,
and 406 selected API tests. All 1,680 result rows are Passed; total, executed
and passed counters agree. Failed, error, timeout, aborted, inconclusive,
not-runnable, not-executed, disconnected, in-progress and pending counters
are zero. This is targeted API coverage, not a full API/PostgreSQL rerun.

`pdf-load-save-validation-audit.json` independently records the counters,
removed build tree, successful cleanup, empty source trust, and unchanged
VERSION, published releases, tracked original fixtures and bundled assets.
`pdf-continuation-boundary-audit.json` separately inventories 36 explicit
fresh-admission wrappers, the shared root for three public load overloads,
and no public admitted-continuation methods in the selected files. It is a
source-boundary audit, not package/API-surface or cross-platform acceptance.

The final ordinary PDF probe passed 20 distinct checks at
`artifacts/native-engine-tests/1794852268604-c1852a68/`.
The strict native-image/OCR probe passed 239 distinct checks at
`artifacts/native-engine-tests/1794852449805-42a91116/` with
`PVAD_REQUIRE_NATIVE_OCR=1`. Both verify removed temporary licenses/builds,
unchanged source trust and regular license outputs. The intermediate image
run at `1794852317090-c630268d` passed 238 checks and did execute native OCR,
but lacked its required-execution flag. It remains separate from the final
strict rerun. Together with the 833-check Importer probe, final native admission
evidence contains 1,092 distinct checks; the audit records all three runs.

All 48 tooling/version/release-author contracts, metadata and version/fragment
validation, former-name/Markdown guards, 39 local documentation links/anchors,
and `git diff --check` passed. Separate Patch fragments record typed PDF
continuations and source-buffer ownership. Builds still emit existing compiler
and third-party package notices; passing tests do not waive those notices or
establish package/publication readiness. VERSION stays 1.1.0. No commit, push,
deployment, service restart, live database update, full API/PostgreSQL rerun,
new visual approval or cross-platform acceptance is asserted.

At that checkpoint, remaining work was Designer `PdfFileImporter` scene/DTO/analysis mapping,
Editing/composition, Trust/security/signing/redaction/e-invoice and advanced
regeneration continuations, then the other product composite chains and
protected-package/platform validation. Existing public guard coverage is not
equivalent to complete admitted-operation lifetime coverage.

## Designer PDF Import And Analysis Continuation Milestone

This milestone covers `PdfFileImporter` PDF-to-Designer mapping and its built-in
analysis dependencies, not every importer, diagnostic endpoint or regeneration
pipeline. Public methods stay current-license guarded; Importer remains partial.

- [x] Start one internal Importer admission at the shared Designer PDF import root and reuse typed loading without opening a second public admission. Keep all public overloads, registry behavior, cancellation and input ownership unchanged.
- [x] Split 19 public geometry, primitive, classification/barcode, reading-order, grouping, reconstruction, scene and semantic methods into fresh public entries and internal-only continuations. Preserve recursive/nested processing and original algorithms.
- [x] Keep new analysis/Designer-import purposes ineligible for private PDF-generation dependencies. Preserve verifier binding, closed contexts and fresh public calls from input/enumeration callbacks.
- [x] Pass 405 new Designer/analysis continuation checks within a 1,238-check Importer probe: five public paths, six fixtures, seekable/non-seekable reads, forced asynchronous read transitions, exact output comparison, nine nested analysis paths, read failure, cancellation and new-call denial.
- [x] Independently audit all eight analysis files (19 public wrappers) and the Designer mapping body against HEAD after removing only admission wrappers, restoring internal continuation call names and normalizing whitespace. Record source hashes and preserve all remaining algorithm bodies.
- [x] Complete affected .NET regressions including the new API mixed-layout/license-per-request test: 1,681 tests passed across the six completed initial suites and two clean recovery suites. Independently audit exact TRX counters, source trust, original fixture preservation and both sessions' cleanup.
- [x] Validate partial metadata, all 49 tooling contracts, documentation/naming/link guards and the Patch release fragment without changing VERSION or published releases.

The expanded Importer probe passed at
`artifacts/native-engine-tests/1794853146285-aa1762b3/` with 1,228 distinct
checks, including 395 new Designer/analysis checks and all 833 prior checks.
Its cleanup report confirms removed synthetic licenses/builds, unchanged source
trust and unchanged regular licensing outputs. Failed preliminary runs at
`1794853011954-c52c4767` and `1794853062677-f55271d5` remain separate: the first
found a missing test import; the second exposed a test callback firing during
fixture construction rather than protected processing. The corrected fixture
uses a retained clipping list to change the license inside geometry processing.
No production algorithm or threshold was changed to make these tests pass.

The output fingerprint excludes only the generated document ID and generated
chart-element IDs. It retains all content, element counts, page settings,
geometry, styles, image data, chart values/status/confidence and diagnostics.
Metadata chart IDs remain checked explicitly. The existing registry/direct
engine tests still check missing, wrong and renewed grants independently.

The final expanded probe at
`artifacts/native-engine-tests/1794853862995-1ec51780/` passed 1,238 distinct
checks, including 405 Designer/analysis checks. Ten additional checks force
`Task.Yield` in seekable/non-seekable mixed-layout reads, verifying original
admission across asynchronous transitions and denial of the next public import
before touching input. The earlier 1,228-check run remains a separate checkpoint.
Its final `run.json` confirms successful execution, removed ephemeral builds and
licenses, unchanged source trust and unchanged regular licensing builds.

`artifacts/native-test-sessions/33c955b8-d041-47af-b021-0f010961e3cd/designer-pdf-body-audit.json`
independently records the normalized algorithm comparison, original commit,
source hashes and all 19 analysis wrappers. It covers the eight analysis files
and Designer mapper, not package obfuscation or the earlier loader-buffer fix.

### Designer Regression Run History

The first signed regression session at
`artifacts/native-test-sessions/33c955b8-d041-47af-b021-0f010961e3cd/`
completed six suites: 87 licensing-core, 666 Importer, 23 FileImporter, 141
Generator, 76 PDF infrastructure and 181 image-analysis tests (1,174 Passed).
Its OCR dependency build stopped progressing with no active compiler/test child
and no CPU activity. The identified build process did not exit on SIGTERM and
was stopped with SIGKILL; the owning harness then removed its isolated build
and license files. OCR had not produced a TRX and API had not started. Those
stages are not counted as passed in this session.

`designer-pdf-partial-validation-audit.json` independently records the six
completed suites and cleanup. The clean recovery session at
`artifacts/native-test-sessions/f4c7bef2-bcf2-46bd-a5da-cc4536e91320/`
reruns OCR/API with explicit build-server/node reuse and shared compilation
disabled. These flags record the recovery configuration, not a proven diagnosis
of the original build stall. The final probe/body audits remain separate from
this interrupted build history.

### Final Designer Import Evidence

The clean recovery completed 100 OCR and 407 selected API tests. Combined with
the six completed initial suites, all 1,681 actual result rows are Passed;
total, executed and passed counters agree for every suite. All failed, error,
timeout, aborted, inconclusive, not-runnable, not-executed, disconnected,
in-progress and pending counters are zero. The interrupted OCR build itself
is excluded, not relabeled as a passed test run.

`artifacts/native-test-sessions/f4c7bef2-bcf2-46bd-a5da-cc4536e91320/designer-pdf-validation-audit.json`
records both sessions, raw TRX paths/counters, all 1,238 final native probe checks,
source-body hashes and cleanup. Both session build/secret trees were removed;
source trust remains empty. Original tracked fixtures/assets, VERSION 1.1.0
and published releases are unchanged. The new API test confirms separate request
admission, mixed page geometry, wrong-product diagnostics and renewed access.

All 49 tooling/version/release-author tests, native metadata and release checks,
former-name/Markdown guards, 41 local documentation links/anchors and
`git diff --check` passed. The Patch fragment
`native-designer-pdf-import-continuations.json` covers Importer, API and
Documentation. Existing compiler and third-party package notices are not waived.
These are local macOS/x64 .NET results, not full API/PostgreSQL, cross-platform,
protected-package or new visual-baseline acceptance. No commit, push, service
restart, deployment or live database change was performed.

At that checkpoint, next work was PDF Editing/page composition, Trust/security/signing/redaction/
e-invoice, advanced regeneration and XObject/color/debug-overlay composite
lifetimes outside this mapping path, followed by other product chains and
protected-package/platform validation. Importer is still marked partially
enforced; this completed subset does not close the whole licensing roadmap.

## PDF Editing And Page Composition Continuation Milestone

This milestone covers bounded page editing/composition, content rewriting and
the migration-friendly editable facade. It does not change supported operation
semantics or close Trust/security, signing/redaction/e-invoice, advanced
regeneration, other products or protected-package/platform acceptance.

- [x] Add an Importer-owned editing purpose; permit only private blank-page bytes for that purpose and retain public PDF, regeneration and OCR-rendering restrictions. Test Trial expiry, original admission and verifier ownership.
- [x] Split ten page-editor/composer/content-rewriter entries into fresh public guards and internal continuations; carry admission through token/object parsing, owner decryption, full/incremental save validation and internal helper pages.
- [x] Carry the editable facade's load/count/edit/merge/clone/output chains under one per-public-call admission. Keep atomic `PdfEditingSession` mutations fresh, metadata/status/disposal free, and previous model/error/ref values untouched on denied calls.
- [x] Pass the 1,434-check Importer probe, including 196 new editing checks and all 1,238 previous checks. Cover memory-reader and enumeration callbacks, exact content rewriting, mixed page geometry/order/text, encryption and existing validation failures.
- [x] Audit all five changed processing files against commit `5e563631` after explicitly normalizing admission wrappers, private forwarding helpers and internal call names. Keep all remaining algorithms and validation bodies unchanged.
- [x] Complete and independently audit affected .NET regressions, including the new deterministic editing-expiry test, generated-code migration and API compatibility: all 1,682 result rows passed with exact counters and successful cleanup.
- [x] Validate partial metadata, all 50 tooling contracts, 43 local documentation links/anchors, naming/diff guards and the Patch fragment without modifying VERSION, published releases, production keys or live data.

The first expanded probe at
`artifacts/native-engine-tests/1794855546749-41dfb5c7/` passed 1,422 distinct
checks. The expanded probe at
`artifacts/native-engine-tests/1794855736110-fd1fbde9/` passed 1,434 distinct
checks, including 196 new editing checks. The added cases preserve or reset
selected-page/measurement-unit state correctly after stream success/failure and
inspect actual operation result metadata. The earlier run remains separate.
Both cleanup reports confirm removed temporary licenses/builds, unchanged source
trust and unchanged regular licensing outputs. All signing inputs are synthetic;
the fixture verifies that no network requests occurred.

`artifacts/native-test-sessions/edeb2ef5-77de-4188-b212-4a3fe94a7475/editing-body-audit.json`
records the original commit, source hashes, ten explicit core wrappers and
normalized equality of editor, composer, rewriter, facade and private blank-page
factory bodies. Normalization restores only license-entry code, private
forwarders, internal call names and whitespace. It is not a claim of byte-equal
encrypted output, unchanged timestamps, cross-platform behavior or obfuscated
package compatibility.

### Final Editing Evidence

The strengthened final Importer probe at
`artifacts/native-engine-tests/1794856334226-2596b491/` passed 1,434 distinct
checks, including 196 editing continuations and all 40 existing public editing
entry signatures. It compares both the caller's original bytes and the actual
custom memory-manager input after success and validation failure. Its cleanup
report confirms removed ephemeral licenses/builds, unchanged source trust and
unchanged regular license outputs. Earlier passed probes remain separate.

The final signed regression session is
`artifacts/native-test-sessions/edeb2ef5-77de-4188-b212-4a3fe94a7475/`.
Its `editing-*.trx` files contain 88 licensing-core, 666 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407 selected
API tests. All 1,682 actual result rows are Passed; total, executed and passed
counters agree for each suite. Failed, error, timeout, aborted, inconclusive,
not-runnable, not-executed, disconnected, in-progress and pending counters are
zero. This is a targeted API selection, not a full API/PostgreSQL rerun.

`editing-validation-audit.json` independently records exact TRX counters, the
final native probe, unchanged processing-file hashes and cleanup. The session
build/secret trees are removed; source trust remains empty. Original tracked
fixtures/assets, VERSION 1.1.0 and published releases are unchanged. Native
execution is local macOS with x64 .NET; no new platform, protected-package or
visual-baseline acceptance is claimed.

All 50 native/version/release-author contracts, metadata/version/fragment
validation, former-name/Markdown guards, 43 local documentation links/anchors
and `git diff --check` passed. The Patch fragment
`native-pdf-editing-operation-continuations.json` covers Importer, Generator
and Documentation. Existing compiler and third-party package notices remain;
passing tests do not grant third-party licensing or publication approval.
No commit, push, deployment, service restart or live database change occurred
in this milestone.

Remaining continuations are Trust/security/signing/redaction/e-invoice,
advanced regeneration, XObject/color/debug-overlay composites outside the
completed paths, and other product graphs. Complete protected-package and
platform validation still follows those steps. Importer remains partial and
the overall native-licensing roadmap is not complete.

## Acceptance Criteria

- [ ] All native products enforce the same signed-license contract, including direct low-level operations.
- [ ] Explicit Trial, subscription with seven-day grace, and perpetual update eligibility behave as specified.
- [ ] Customer artifacts contain only public manufacturer verification keys and no test backdoors.
- [ ] Admin, Account, API, workers, and local package consumers operate with the required license and existing authorization boundaries.
- [ ] Protected packages retain API, JSON, PDF, and rendering compatibility.
- [ ] No claim of unbreakable protection, reliable offline seat counting, immediate offline revocation, or trusted offline wall-clock time is made.

## Deferred Work

- [ ] Evaluate Native AOT or native components independently if stronger code concealment is commercially justified.
- [ ] Evaluate commercial obfuscation/anti-tamper tools only after a separate budget decision.
- [ ] Obtain commercial/legal approval and independent release review before publishing.
