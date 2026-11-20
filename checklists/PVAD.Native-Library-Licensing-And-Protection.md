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
- [x] Recheck validity and product/provider rights at the start of current protected operations; verifier boundary tests and the product/provider entry milestones cover admission, while broad package/platform acceptance remains separate.
- [x] Bind perpetual update eligibility to immutable release metadata rather than filesystem or build timestamps.
- [x] Test Trial boundaries, grace boundaries, perpetual updates, rotation, concurrent replacement, and malformed inputs.

## P0: Issuer And Server Separation

- [x] Add a private-key-file-controlled offline preparation CLI; explicitly distinguish it from authenticated issuance and durable audit.
- [x] Implement manufacturer-only issuance/renewal with authorization and audit; see the [manufacturer backend milestone](#manufacturer-native-issuance-and-renewal-backend-milestone). Native Admin/Account screens remain separate.
- [ ] Keep private keys outside packages, customer images, logs, and source control; never generate production replacement keys automatically.
- [x] Split the existing server signing/verifying services without changing existing server artifacts.
- [x] Default server deployments to verification-only mode; block issuance/replacement even for System Administrators unless issuer mode is explicitly configured.
- [x] Load issuer private keys lazily, validate the key pair, and never generate missing keys in any environment.
- [x] Mount only the public verification key in customer Compose; supply a separate manufacturer-only issuer overlay.
- [x] Keep license listing, reads, downloads, and schema-1/schema-2 validation functional without a private key.
- [x] Add Admin management and authorized Account downloads for native licenses; see the [Admin and Account milestone](#native-admin-and-account-license-workflow-milestone).
- [ ] Use the same verification core in WebApi and workers while preserving tenant, session, entitlement, and feature checks.
- [ ] Transfer only locally verifiable worker configuration; no source-visible secrets or public bypass modes.
- [x] Support explicit import of signed revocation lists; document offline revocation limitations. See the [offline revocation milestone](#signed-offline-revocation-list-milestone).

## P0: Product And Operation Coverage

The [operation matrix](../docs/native-library-licensing.md#operation-matrix)
is the starting inventory, not completed enforcement coverage.

- [x] Represent all ten native product areas and test domain/kind/provider-scoped grants against all 35 current migration catalog entries.

- [x] Guard PDF generation and direct PDF engine entry points, including bytes, files, and streams.
- [x] Recheck PDF rights at shared writer entry before implicit page creation or destination writes.
- [x] Keep metadata/options DTOs free and avoid a second Converter grant for native PDF generation.
- [x] Guard Word generation, both direct exporter overloads, interface calls, and the public DOCX signing service; see the Word milestone below.
- [x] Guard Spreadsheet generation, retained builder handles, calculation, IO, direct exporters, data operations, and design conversion; see the Spreadsheet milestone below.
- [x] Guard PDF importing, editing, saving, and all FileImporter/analysis/OCR paths; the public API checkpoint reconciles all current protected entries, with package/platform acceptance kept separate.
- [x] Guard format conversion and direct converter implementations.
- [x] Guard every PDF Code/Viewer/Designer, Spreadsheet Code, and Report Designer migration provider; see the completed Code, Nutrient, and Report admission milestones below.
- [x] Resolve automatic report detection to its concrete provider before admission; the core rejects `auto` grants. See the Report Designer admission milestone and its scoped service tests.
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
- [x] Provide encrypted private retention and checksum-bound recovery for joint mappings and original portable symbols; production recipient/storage custody remains a separate deployment gate in the support-retention milestone below.
- [ ] Audit protected output with ILSpy; compare public API and internal-name visibility without claiming logic is unrecoverable.
- [x] Test local-feed package consumers and complete native transitive dependency graphs; the 54-assembly/52-library milestone below closes local graph acceptance, not WebApi/friend-boundary or cross-platform acceptance.
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

## PDF Trust Inspection And Change-Impact Continuation Milestone

This bounded milestone covers read-only Trust inspection, structural validation
and change-impact evaluation. Signing execution, redaction, e-invoice, archival
conversion/post-processing, advanced regeneration and other product chains
remain separate work. Existing public admission coverage is not equivalent to
complete composite-operation lifetime coverage.

- [x] Add Importer-owned Trust inspection and change-impact purposes with no private PDF dependencies; retain verifier ownership, closed scopes and fresh public/worker admission.
- [x] Carry the inspection admission through bounded asynchronous reads, parsing, embedded CRL/OCSP historical evidence, certification revision inspection and structural PDF/A content validation without changing their algorithms.
- [x] Keep public CRL/OCSP validators freshly guarded and custom revocation/issuer providers on their interface paths; do not turn callbacks into license bypasses.
- [x] Pass the expanded native probe with unchanged Trust reports, all mutation/save-strategy combinations, synthetic signature/evidence cases, reentrant denial, cancellation, IO and size failures.
- [x] Run and independently audit affected licensing, Importer, FileImporter, Generator, infrastructure, image/OCR and selected API regression suites with signed isolated sessions and complete cleanup.
- [x] Compare normalized processing/validation bodies with the Editing checkpoint, verify documentation links, metadata, tooling contracts, naming guards and the Patch release fragment.

The prior Editing checkpoint was committed as `bdf33a5f`; it remains a separate
milestone and its historical results are unchanged. Importer remains partially
enforced. VERSION, published releases, production keys and live databases must
remain unchanged throughout this continuation work.

### Native Trust Evidence

The final expanded probe at
`artifacts/native-engine-tests/1794857759946-96a0f0fb/` passed 1,669 distinct
checks, including 235 new `trust-continuation:` checks and all 196 editing
continuation checks. It verifies full inspection report equality, excluding only
the volatile structural-conformance validation timestamp. Synthetic PDF/A-2b/3b,
AES-256, signed, certified, tampered, malformed and long-term-evidence inputs
retain their original outcomes. Disallowed certified-revision changes still
produce invalid certification and `PVADPDFTRUST147`. Historical embedded CRL/OCSP
validation remains valid for the synthetic test chain.

All 14 save-strategy/mutation combinations retain signature-impact warnings,
confirmation requirements and independent compliance revalidation. CRL/OCSP
validators, configured issuer/revocation callbacks, genuinely asynchronous
reads, source ownership, size/IO/cancellation errors and actual callback license
failures are covered. Reentrant and subsequent public work is freshly denied;
neither new purpose grants any private PDF dependency. Fixture timestamps use
an in-memory handler; no network request is made to the synthetic hostname.

Earlier successful probes at
`artifacts/native-engine-tests/1794857519619-79d51828/` (1,665 checks) and
`artifacts/native-engine-tests/1794857677533-babc7bd4/` (1,669 checks before the
Extractor entry correction) remain separate. The initial build at
`artifacts/native-engine-tests/1794857346063-e168d130/` stalled and was explicitly
terminated; it is not a passing run. The runner now disables compiler-server
and MSBuild node reuse. All four probe cleanup reports confirm removed synthetic
licenses/builds, unchanged source trust and unchanged regular licensing outputs.

The first regression session at
`artifacts/native-test-sessions/03680dca-fdaf-49dd-be81-3194ecf9a729/` passed 89
licensing tests but had one failed direct internal DSS extractor test among 666
Importer cases. A fresh guarded extractor entry and a separate admitted internal
path fixed the failure; the original test was not weakened. The failed TRX and
successful cleanup remain retained, not relabeled as complete acceptance.

`artifacts/native-test-sessions/1c27e564-30cc-4fc6-ac52-c2d22b1f28df/trust-body-audit.json`
compares all seven changed Trust processing files against `bdf33a5f` after
normalizing only six explicit admission wrappers, internal call names and
whitespace. All remaining algorithm/validation bodies match. The unchanged
signature validator and shadow-analysis algorithms are not regenerated.

### Final Trust Regression Evidence

The final signed session is
`artifacts/native-test-sessions/1c27e564-30cc-4fc6-ac52-c2d22b1f28df/`.
All 1,683 result rows passed: 89 licensing-core, 666 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407
selected API cases. Each TRX agrees on total, executed, passed and actual row
counts. All failure, skip, timeout, aborted and pending counters are zero.
The API selection includes export, generated migration code, code workspace,
Trust/signature/redaction/e-invoice and viewer compatibility; this is not a
complete API/PostgreSQL rerun or new platform/visual/package acceptance.

`trust-validation-audit.json` independently confirms the raw results, final
probe, unchanged compiled/test inputs, body hashes, all 1,434 original checks
retained with the one explicitly migrated mid-read assertion, and cleanup.
Only tooling documentation changed while the regression compiled; no build
input changed. `cleanup.json` confirms removed session builds/secrets, unchanged
source trust and intact synthetic artifacts before deletion.

All 51 native/version/release-author tooling contracts, 45 local documentation
links/anchors, product/version/fragment validation, former-name/Markdown guards
and `git diff --check` passed. The Patch fragment
`native-pdf-trust-inspection-continuations.json` covers Importer and
Documentation. VERSION 1.1.0, published releases, original test fixtures and
production trust remain unchanged; no live database, service, deployment or
registry was modified. Existing compiler and third-party notices remain open.

This completes only the Trust-inspection/change-impact continuation subset.
Signing/redaction/e-invoice, archival conversion/post-processing, advanced
regeneration/XObject/color/debug composites, Converter/Migration enforcement,
native issuance/Admin/Account integration, complete protection/package and
platform acceptance remain open. Importer remains partial. This subset is not
committed or pushed yet; the preceding Editing subset is commit `bdf33a5f`.

## PDF Signing And Timestamp Continuation Milestone

This extends the completed Trust-inspection subset without committing or
rewriting it. Only PDF signing and timestamps are covered here; redaction,
e-invoice, archival conversion/post-processing, advanced regeneration and other
product/packaging/platform milestones remain open.

- [x] Add Importer-owned signing and timestamp purposes without private PDF dependency rights; extend Trial expiry, verifier ownership, closed-scope and fresh-worker checks.
- [x] Carry PAdES B/T/LT/LTA through built-in CAdES, parsing, incremental save, DSS/VRI, archive timestamp and post-sign Trust inspection continuations.
- [x] Keep keys/authorities sealed; restrict internal dispatch to known built-ins and retain custom key, timestamp and material-provider interface calls with fresh public native guards.
- [x] Preserve synchronous public `SignAsync` license denial without leaking its ambient scope to callers; keep source, signature/profile validation, cancellation and timestamp transport rules unchanged.
- [x] Pass and audit the full expanded native probe, including all prior checks, all supported profiles/digests, mid-read/HTTP/provider replacement, cryptographic validation and failure behavior.
- [x] Run and independently audit licensing, Importer, FileImporter, Generator and selected API regressions with signed isolated builds and complete cleanup.
- [x] Verify normalized signing bodies, documentation links, metadata, tooling and naming guards, unchanged fixtures/version/production trust, and the separate Patch fragment.

The initial probe at
`artifacts/native-engine-tests/1794897347242-396a63c5/` failed to compile because
a new test attempted to derive the already sealed PKCS#12 key. The test now
uses `IPdfSigningKey`; the product API was not unsealed. Its cleanup report
confirms removal of synthetic licenses/builds and unchanged source trust.
A new source-contract regex also initially matched unrelated StringBuilder
`Append` calls; it was narrowed to actual PDF parser/writer calls, not removed.

### Native Signing Evidence

`artifacts/native-engine-tests/1794897439550-58517e9a/` passed 1,948 distinct
checks: all 1,669 prior checks plus 279 new `signing-continuation:` checks.
The probe verifies 30 profile/digest/trigger combinations across B/T/LT/LTA,
SHA-256/384/512, visible appearance, source replacement, timestamp HTTP callbacks
and asynchronous material providers. Each output preserves its input prefix and
is cryptographically inspected after explicit license renewal. Requested
profile, certificate/integrity status, signature/revision count, LT evidence and
LTA archive timestamp must match their contracts.

Direct document/RFC 3161 timestamps, retained public authorities/keys, source
preservation, malformed/encrypted input, cancellation and actual HTTP errors are
covered. Custom key/timestamp/material callbacks retain interface dispatch and
fresh public license failures. `SignAsync` still rejects a wrong current license
synchronously. Both built-ins remain sealed; no derived-key bypass or public
admission API exists. Fixture HTTP is in memory and synthetic keys/certificates
are disposed; no production trust or real endpoint is used.

The probe cleanup report confirms removed ephemeral licenses/builds, unchanged
source trust and unchanged regular licensing outputs. The preceding failed
compile remains separate. Importer stays partial; this is local macOS/x64 .NET
evidence, not protected-package, general CI or platform acceptance.

`artifacts/native-test-sessions/3878b60d-309a-4b7b-a22a-081fb2dd964f/signing-body-audit.json`
compares all six existing signing files with `bdf33a5f`, normalizing only eight
explicit entry/async wrappers, admitted call names and the two known built-in
dispatch forwarders. All remaining algorithm/validation bodies match. The new
internal dispatch helper is separately hashed and checked for sealed built-ins,
ordinary custom-interface forwarding and original-verifier admission.

### Final Signing Regression Evidence

The final signed session at
`artifacts/native-test-sessions/3878b60d-309a-4b7b-a22a-081fb2dd964f/` passed all
1,326 selected .NET result rows: 89 licensing-core, 666 Importer, 23
FileImporter, 141 Generator and 407 API cases. Total, executed, passed and actual
row counts agree; all failure, skip, timeout, aborted and pending counters are
zero. `signing-validation-audit.json` independently confirms those TRX results,
the 1,948-check native probe, retention of all prior checks, current body and
dispatch hashes, unchanged compiled/test inputs and successful cleanup.

All 52 native/version/release-author tooling contracts, 47 local documentation
links/anchors, product/version/fragment validation, former-name/Markdown guards
and `git diff --check` passed. The separate Patch fragment
`native-pdf-signing-operation-continuations.json` covers Importer and
Documentation. Only tooling documentation changed during compilation; no build
input changed. Temporary licenses/builds are removed, production trust remains
empty, original test fixtures are unchanged, and VERSION 1.1.0 and published
releases are unchanged. Existing compiler/third-party notices remain.

These are local macOS/x64 results, not a fresh full API/PostgreSQL, visual,
cross-platform or protected-package acceptance run. Previous milestone reports
remain snapshots of their own source states and are not relabeled as new
evidence. No commit, push, deployment, service restart or live database change
was performed for this signing subset. The existing unrelated solution/private
IDE changes are left untouched.

Next continuation work is Redaction preflight/application/audit, followed by
e-invoice, archival conversion/post-processing and advanced regeneration/XObject/
color/debug composites. Converter/Migration enforcement, native-license
issuance/Admin/Account integration, full protection/packaging and platform
acceptance also remain open; completing signing does not close the overall
native licensing roadmap.

## PDF Redaction And Audit Continuation Milestone

This subset covers existing redaction and its audit, not new removal algorithms
or codec support. It extends the still-uncommitted Trust/signing work without
changing prior historical results or their checkbox states. E-invoice, archival
conversion/post-processing, advanced regeneration and other product/protection/
packaging/platform acceptance remain open.

- [x] Add Importer-owned redaction, audit and bounded color-resolution purposes without private PDF dependency rights; extend expiry, verifier binding and fresh-operation checks.
- [x] Split preflight/apply, image processing and direct residue scanning into freshly guarded entries and internal continuations; retain custom preflight dispatch.
- [x] Preserve original admission through bounds, decoder/color-space/function construction, content rewriting, full rewrite and residue verification; leave general color conversion/evaluation independently guarded.
- [x] Carry optional audit CMS/timestamp creation and canonical audit verification under admission while keeping custom keys/authorities freshly dispatched and optional no-key metadata behavior unchanged.
- [x] Pass and audit all native checks, exact reference PDFs, reopened extraction/geometry, image/carrier removal, actual resolver/decoder failures, custom-provider denial and audit tampering.
- [x] Pass and independently audit all eight affected .NET regression groups, including the unchanged forged-marker/residual-image tests, image/OCR and selected API cases.
- [x] Verify normalized algorithm bodies, unchanged safety helpers/fixtures, documentation links, metadata, tooling/naming guards and the separate Patch fragment; keep production trust, VERSION and published releases unchanged.

The initial probe at
`artifacts/native-engine-tests/1794898761233-ee106a90/` failed an old assertion
that expected license replacement alone to abort nested color resolution. The
three sampled/PostScript/ICC cases are now explicitly migrated: admitted
resolution completes, subsequent work is denied, and genuine resolver-native
failures still propagate. The old labels are not retained as false evidence of
unchanged semantics. New decoder-memory callbacks additionally test the actual
exception boundaries inside sampled/PostScript/ICC decoding.

The probe at `artifacts/native-engine-tests/1794899197693-cd77800f/` exposed an
overly strict new fixture expectation that the entire names dictionary vanish.
The existing sanitizer intentionally keeps an empty dictionary. The fixture now
checks removed embedded-file references, absent metadata/attachment streams and
absent payload bytes; no sanitizer or removal rule was weakened.

The expanded probe at
`artifacts/native-engine-tests/1794899316375-cbe3fa23/` passed 2,248 distinct
checks. The decoder-memory expansion at
`artifacts/native-engine-tests/1794899500682-78c54ee8/` passed 2,257 checks before
the final direct-scanner entry correction. These are separate intermediate
reports, not the final source checkpoint.

The first regression session,
`artifacts/native-test-sessions/ff97bfde-6d5f-4b6d-b27c-f64e5819de3a/`, passed 89
licensing tests but had three failures among 666 Importer cases. Those tests call
the residue scanner directly to reject forged markers/live pixels. A fresh
scanner entry and a separate admitted core corrected the missing scope without
modifying those tests. The failed TRX and cleanup remain retained separately.

### Native Redaction Evidence

The final probe at
`artifacts/native-engine-tests/1794899570736-ba27ddea/` passed 2,257 distinct
checks, including 303 new `redaction-continuation:` checks. The 1,948 prior
signing-checkpoint checks remain covered with three explicitly migrated
sampled/PostScript/ICC labels. Six new checks distinguish admitted factory
completion from fresh subsequent-call denial. The migrated genuine-error cases
remain separate from three additional failures thrown during decoded-byte access.

All eight reference documents (mixed-page text, metadata/attachment and six
RGB/gray/CMYK/indexed/inline/masked image variants) retain byte-identical
redaction output across synchronous/genuinely asynchronous replacement. Removal
counts, rewritten pages, preflight findings and verification diagnostics match.
Reopened output preserves unaffected text and mixed page geometry, has one
revision, and no longer contains metadata/attachment payload or references.
Covered image samples are still checked by the unchanged verification algorithm.

Optional audit CMS/timestamps verify after explicit license renewal; altered
record/signature metadata remains invalid. New public verification stays denied
after replacement. Custom preflight/key/authority calls retain their public
boundaries and do not dispatch denied timestamp HTTP. Actual IO, cancellation,
preflight-size, encrypted/outside-region and callback-native failures retain
their types and stream ownership. Public color evaluation/conversion receives
no grant from completing a resolver/factory operation.

`artifacts/native-test-sessions/1d65f924-c2eb-4c12-8266-d29825a85cd6/redaction-body-audit.json`
compares nine processing files with `bdf33a5f`, normalizing only eleven entry
wrappers, sealed-preflight dispatch, admitted call names and equivalent default
SHA-256 arguments. All other bodies match. Geometry, text redaction, sanitizer
and resource-inspection files remain byte-identical. Source hashes pin the final
comparison; no removal rule, codec limit or safety threshold is relaxed.

The final native cleanup report confirms removed synthetic licenses/builds,
unchanged source trust and unchanged regular licensing outputs. Failed and
intermediate reports remain separate. Native execution is local macOS/x64 .NET;
production keys, registries, live databases and published releases are not used.

### Final Redaction Regression Evidence

The final signed session at
`artifacts/native-test-sessions/1d65f924-c2eb-4c12-8266-d29825a85cd6/` passed all
1,683 actual result rows: 89 licensing-core, 666 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407 selected
API cases. All eight TRX files agree on total, executed, passed and actual rows;
failure, skip, timeout, aborted and pending counters are zero. The original
forged-marker/live-pixel tests remain unchanged and pass.

`redaction-validation-audit.json` independently confirms the raw counters,
2,257-check native report, the three explicit historical assertion migrations,
six new resolution cases, nine current body hashes and unchanged safety helpers.
All compiled/test inputs remain unchanged during the run; only tooling
documentation changed. `cleanup.json` confirms removed session builds/secrets,
unchanged source trust and valid synthetic artifacts before deletion.

All 53 native/version/release-author tooling contracts, 49 local documentation
links/anchors, product/version/fragment validation, former-name/Markdown guards
and `git diff --check` passed. The separate Patch fragment
`native-pdf-redaction-operation-continuations.json` covers Importer and
Documentation. VERSION 1.1.0, published releases, original fixtures, live data
and manufacturer trust remain unchanged. Existing compiler/third-party notices
are not waived. No commit, push, deployment or service restart was performed.
Unrelated solution and private IDE changes remain untouched.

This closes only the Redaction/audit continuation subset. It is local macOS/x64
evidence, not a new full API/PostgreSQL, visual, cross-platform or protected-
package acceptance run. Next are e-invoice/associated-file and archival
conversion/post-processing continuations, followed by advanced regeneration,
general color/XObject/debug composites and other product graphs. Converter/
Migration enforcement, native issuance/Admin/Account, full protection/packaging
and platform acceptance remain open; Importer remains partial.

## E-Invoice And Archival Continuation Milestone

This milestone adds operation lifetime coverage to existing E-Invoice attachment
and extraction, archival conversion and built-in color processing. It does not
add fiscal XML validation, new PDF/A capability, tax certification or general
cross-product rights. Importer remains partial.

- [x] Give public attachment preflight/write and associated-file extraction fresh Importer admission; share only internal continuations through parsing, decoding, writing and verification.
- [x] Carry admission through explicitly approved built-in PDF/A-2b/3b conversion and its sealed built-in Trust inspector; preserve ordinary custom-converter and custom-inspector dispatch.
- [x] Add fresh color-analysis, image/content rewrite, ICC and function-evaluation entries with bounded internal continuations. Only the five sealed built-in function families use internal evaluation; external subclasses retain fresh public admission.
- [x] Give E-Invoice, associated-file, archive and color purposes no private PDF dependency rights. Extend existing expiry/dependency-policy tests to cover all four purposes.
- [x] Keep source ownership, original bytes, limits, supported filters, XML payload/filename rules, PDF/A structural checks, signature-modification and replacement approvals unchanged.
- [x] Preserve exact attachment extraction, declared size/checksum and page-content verification; document that incremental replacement retains historical bytes and is not secure erasure.
- [x] Add synchronous/asynchronous replacement, approved/unapproved conversion, standalone extraction, exact output/XML, mixed geometry, unrelated-attachment, custom-provider, native-color, failure and subsequent-call denial checks to the signed isolated probe.
- [x] Pass 2,443 distinct native checks, including 186 new E-Invoice/archive/color cases, with real LittleCMS required and complete isolated cleanup.
- [x] Compare nine processing files with `bdf33a5f`, restoring fourteen explicit entry wrappers, known sealed dispatch and admitted call names only. Preserve remaining processing bodies and three safety/model files unchanged.
- [x] Complete all eight selected signed regression suites and independently reconcile actual TRX rows, source hashes and cleanup reports.
- [x] Pass all 54 native/version/release-author tooling contracts after updating the operation-coverage matrix and preserving partial Importer status.
- [x] Finish documentation links, naming/version/fragment validation and whitespace checks on the final source checkpoint.

### Native E-Invoice Evidence

The final probe at
`artifacts/native-engine-tests/1794901459357-889c6c7e/` passed 2,443 unique checks,
including 186 new `einvoice-continuation:` cases. Existing 2,257 probe assertions
remain covered. PDF/A-3 sources retain their original byte prefix and unchanged
pages. Explicitly approved plain-PDF/PDF/A-2 conversion uses the normal converter
and validates the result structurally. Both flows produce byte-identical output
to licensed reference executions after synchronous/genuinely asynchronous license
replacement. Independently extracted XML, hashes, declared size/checksum,
unrelated supplements and mixed page geometry are verified.

The same probe checks standalone conversion to both archival profiles,
associated-file extraction, all five built-in function families, tint/alternate
recursion, named-color rewriting and actual ICC transformation. New calls after
replacement remain denied. Custom converter/inspector dispatch is not bypassed;
a custom function cannot inherit the built-in evaluator continuation. Genuine
callback-native errors propagate. Missing conversion/signature approvals,
duplicate invoice conflicts, encrypted input, size limits, IO and cancellation
retain their failure types and caller-owned streams.

The first attempt at
`artifacts/native-engine-tests/1794901358778-5c56bcb8/` passed compilation but
failed its final rewriter fixture assertion. That fixture expected a particular
whitespace splice and used a direct device operator, which did not invoke its
resolver callback. The corrected fixture selects a named CMYK resource and
checks retained text independently. No product algorithm changed to satisfy the
assertion. Its failed status and complete cleanup are preserved separately.

`artifacts/native-test-sessions/739f7803-836b-4619-a8b7-0262279631fd/einvoice-body-audit.json`
pins source/base hashes for nine processing files and normalizes fourteen fresh
entries, including two prior resolution entries. Only explicit builtin dispatch,
admitted calls and fresh guards on graph extraction/color analysis are restored
for comparison. Other bodies compare equal modulo whitespace. Attachment
models, XMP writer and archival conversion models remain byte-identical.

### Final E-Invoice Regression Evidence

The signed session at
`artifacts/native-test-sessions/739f7803-836b-4619-a8b7-0262279631fd/` passed
all 1,683 actual result rows: 89 licensing-core, 666 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407 selected
API cases. All eight TRX files agree on total, executed, passed and actual rows.
Failure, error, skipped, aborted, timeout, pending and other non-passing counters
are zero. These are selected local macOS/x64 regressions, not a new complete API,
PostgreSQL, visual, cross-platform or obfuscated-package acceptance run.

`einvoice-validation-audit.json` independently reconciles those counters, all
1,089 compiled/test input hashes, the nine-file/fourteen-wrapper body audit and
complete native/session cleanup. Every prior 2,257 native assertion remains in
the 2,443 unique checks without migration or removal. The only initial failed
probe remains a separate failed record. Source trust is still empty; temporary
synthetic licenses/builds are removed and regular licensing outputs unchanged.

All 54 tooling contracts, 51 local documentation links/anchors, native product,
version and release-fragment validation, former-name/Markdown guards and
`git diff --check` passed. The separate Patch fragment
`native-pdf-einvoice-archive-continuations.json` covers Importer and Documentation.
VERSION 1.1.0, release metadata, published releases, original fixtures, live data
and manufacturer trust remain unchanged. Existing compiler/analyzer notices
are not waived. Unrelated solution and private IDE changes remain untouched.

Trust post-processing and advanced regeneration/XObject/debug composite chains
remain open, followed by other native product graphs. Converter/Migration
enforcement, native issuance/Admin/Account, protection, packaging and platform
acceptance are not completed by this local milestone. No commit, push, deploy,
live database, production key or published version is changed.

## Trust Post-Processing Continuation Milestone

This milestone joins the existing Redaction, signing and Trust-inspection
continuations into one admitted final-byte operation. It changes license lifetime,
not document capabilities, conformance claims, plan policy or validation rules.
Importer enforcement remains partial.

- [x] Give public `ProcessAsync` fresh Importer admission before source access, with an internal verifier-bound continuation spanning the existing built-in stages.
- [x] Dispatch only sealed built-in Redaction, signing and inspector implementations through admitted cores. Preserve all custom interfaces and fresh native calls started by callbacks.
- [x] Preserve synchronous public signing license denial while providing a private-to-the-assembly admitted signing forwarder.
- [x] Give the pipeline purpose no private PDF dependencies and cover expiry, foreign-authority rejection and dependency denial through core policy tests.
- [x] Keep Redaction before Signing before Validation, fail closed on unsuccessful removal verification and inspect exactly the returned final bytes.
- [x] Keep plan errors, final structure/PDF-A/PDF-UA/signature/risk policies, credential separation, cancellation and original processing errors unchanged.
- [x] Pass 2,853 unique isolated native checks, including 410 new pipeline assertions, with all previous 2,443 assertions retained and complete synthetic-file cleanup.
- [x] Add focused regression tests for failed Redaction verification and invalid organization, long-term-material and revocation plans before any stage dispatch.
- [x] Compare pipeline and signing processing bodies with their previously reviewed checkpoints; pin current source hashes.
- [x] Complete all eight signed regression suites and independently reconcile actual TRX rows, unchanged inputs and cleanup.
- [x] Pass all 55 native/version/release-author tooling contracts with the updated partial coverage matrix.
- [x] Complete documentation links, product/version/fragment checks, naming guards and whitespace validation.

### Native Pipeline Evidence

`artifacts/native-engine-tests/1794902476581-693cafab/` passed 2,853 checks,
including 410 new `pipeline-continuation:` assertions. It exercises validation
only, Redaction only, signing and combined processing through all four PAdES
profiles. Source memory, region enumeration, timestamp HTTP and asynchronous
material callbacks replace the live license after admission. New public calls
remain denied; the admitted built-in operation completes.

Checks cover Redaction-before-signing order, final-byte SHA-256, independently
reopened Trust reports, valid signature integrity/trust, historical CRL/OCSP,
archive timestamps, signed/timestamped Redaction audits, mixed page geometry,
removed/retained text and unchanged original input. Validation-only retains
the exact input bytes. Custom service/key/timestamp/material dispatch is not
bypassed. Providers that start actual denied native calls fail normally;
providers returning data after an await do not acquire new public rights.

All five final-policy rejection codes/reports are exercised after replacement,
as are source IO, cancellation, encrypted/outside-region Redaction, actual
callback-native errors and failed timestamp HTTP. Temporary grants/builds are
removed; production trust and regular licensing outputs remain unchanged.
Keys/certificates/CRLs/OCSP and timestamps are synthetic; no external service is
used. Native execution is local macOS/x64 .NET with real LittleCMS required,
not new visual, complete API/PostgreSQL, platform or protected-package acceptance.

`artifacts/native-test-sessions/0b32773c-533e-4428-b65a-a1d9c23ec7c9/pipeline-body-audit.json`
compares the pipeline against `bdf33a5f` after restoring exactly one fresh entry
and three sealed-built-in dispatch blocks. All other bytes are identical,
including `ValidatePlan`, `EnforceValidation`, `Reject`, stage order, serializable
policy and runtime credential models. Removing the one new internal signing
forwarder reproduces the signer hash from the preceding E-Invoice source audit;
public synchronous license denial and all signing algorithms stay unchanged.

### Final Pipeline Regression Evidence

The signed session at
`artifacts/native-test-sessions/0b32773c-533e-4428-b65a-a1d9c23ec7c9/` passed
all 1,689 actual .NET result rows: 89 licensing-core, 672 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407 selected
API cases. The six additional Importer cases verify unsuccessful Redaction
verification and invalid organization/material/revocation plans before subsequent
stage dispatch. Existing test expectations and safety thresholds are unchanged.
All eight TRX files agree on total, executed, passed and actual rows; every
non-passing, skipped, failed, timeout, aborted and pending counter is zero.

`pipeline-validation-audit.json` independently reconciles those raw counters,
1,090 unchanged compiled/test inputs, the two-file algorithm comparison,
2,853 distinct native checks with all 2,443 prior assertions retained and complete
native/session cleanup. Temporary licenses/builds are removed; source trust,
release metadata and regular licensing outputs remain unchanged. All 55 tooling
contracts and 53 local documentation links/anchors pass, together with product,
version/fragment, former-name/Markdown and whitespace guards.

The separate Patch fragment `native-pdf-trust-pipeline-continuations.json` covers
Importer and Documentation. VERSION remains 1.1.0. Existing compiler/analyzer
notices are not waived. These are local macOS/x64 selected regressions, not a new
full API/PostgreSQL, visual, cross-platform or obfuscated-package acceptance run.

Advanced regeneration/XObject/debug composite chains and other native product
graphs remain open. Converter/Migration enforcement, native issuance/Admin/Account,
obfuscation, package and platform acceptance are not closed by this milestone.
No commit, push, deployment, version bump, live data or manufacturer key change
is authorized or performed here. Unrelated solution and private IDE changes stay
untouched.

## Regeneration, XObject And Debug Continuation Milestone

This milestone closes the selected PDF-regeneration/mapping/XObject/debug
continuation subset. It does not introduce rendering, decoding or form-recursion
features. Importer remains partial until other native product graphs, bundle
rights and full package/platform acceptance have been assessed.

- [x] Carry ordinary regeneration and built-in save through one Importer admission and only the existing purpose-bound `RegenerationPdf` private dependency.
- [x] Preserve public save's synchronous license denial and ordinary custom-bridge dispatch, including after asynchronous callbacks.
- [x] Keep caller configuration separate: require current PDF rights, recheck current Importer rights after the callback and render through the normal public PDF boundary.
- [x] Give recursive object/graphics/resource mapping, XObject resolution and debug overlays fresh analysis admission with internal continuations and no private PDF rights.
- [x] Migrate built-in image/palette decoding and shading parse/build/rewrite/append to their admitted cores without changing processing bodies.
- [x] Extend verifier tests so regeneration retains only its authorized private dependency after Trial expiry; new admissions remain denied and foreign authority cannot reuse it.
- [x] Pass 3,036 unique native assertions, including 183 new regeneration cases, with every prior 2,853 assertion preserved.
- [x] Verify byte-identical regenerated references, mixed pages/rotation, images/masks/palettes/shadings, recursive mapping, form bounds and overlays after replacement.
- [x] Verify fresh public/retained/callback boundaries, custom bridges, cancellation, original input/output errors and untouched output when failure precedes writing.
- [x] Compare all five processing files against `bdf33a5f`, restoring ten admission entries, trusted built-in dispatch and admitted call names only; preserve rendering and callback rules.
- [x] Complete all eight signed .NET regression suites and independently reconcile raw TRX results, source hashes and cleanup.
- [x] Pass all 56 native/version/release-author tooling contracts with the updated partial product matrix.
- [x] Complete documentation links, naming/version/product/fragment validation and whitespace checks.

### Native Regeneration Evidence

`artifacts/native-engine-tests/1794906763186-0dcfaf93/` passed 3,036 unique
checks, including 183 new `regeneration-continuation:` cases. Metadata,
RGB/indexed images, compressed palettes, soft masks and shadings compare
byte-identically against licensed reference executions through both the bridge
and built-in save pipeline. Independent reimport verifies mixed page dimensions,
rotation and retained text/path/image/shading output. Recursive object/resource
mapping, XObject form transforms/bounds, missing/recursion behavior and debug
items/labels are checked separately.

The previous configured-callback, revocation, retained-document and public-PDF
denial cases remain unchanged. New output callbacks and custom bridges cover
synchronous and genuinely asynchronous boundaries. Input/output IO, cancellation
and actual callback-native errors retain their types. Untouched-output assertions
apply to failures before any write, not rollback of arbitrary partial writes.

The first probe at `artifacts/native-engine-tests/1794906662714-e48d1c06/`
compiled but failed its test assumption: the live replacement was a PDF-only
license, which legitimately permits general PDF generation. Moving the denial
check to Importer-only rights corrected the probe without changing product code.
Both runs keep their actual statuses and complete cleanup reports.

`artifacts/native-test-sessions/b1c2d74f-cb1f-4b8d-810a-bfb1aa77b775/regeneration-body-audit.json`
pins hashes for bridge, save pipeline, XObject resolver, debug builder and PDF
writer. Ten explicit entries, internal forwarders, sealed bridge dispatch and
admitted calls are normalized; trusted writer completion checks the same private
dependency instead of re-admitting. All remaining algorithms compare equal
modulo whitespace. Configured-callback checks, dependency policy, XObject
recursion/content handling, image/shading algorithms and writer output remain
unchanged. One initial audit-tool normalization incorrectly stripped an existing
blank-page method suffix; correcting the comparison rule required no source edit.

### Final Regeneration Regression Evidence

The signed session at
`artifacts/native-test-sessions/b1c2d74f-cb1f-4b8d-810a-bfb1aa77b775/` passed
all 1,690 actual .NET result rows: 90 licensing-core, 672 Importer, 23 FileImporter,
141 Generator, 76 PDF infrastructure, 181 image-analysis, 100 OCR and 407 selected
API cases. The additional core case verifies private regeneration dependency
retention after Trial expiry without new public rights or foreign-authority reuse.
Existing regression cases and safety thresholds remain unchanged.

`regeneration-validation-audit.json` independently reconciles all eight TRX files,
with identical total/executed/passed/actual-row counts and every non-passing
counter zero. It verifies 1,091 unchanged compiled/test inputs, the five-file/
ten-entry body audit, all 3,036 unique native assertions including the previous
2,853 unchanged checks, and complete signed-session/native cleanup. The initial
failed probe remains a separate failed record with complete cleanup.

All 56 tooling contracts and 55 local documentation links/anchors pass, together
with native product, version, release-fragment, former-name/Markdown and whitespace
guards. The Patch fragment `native-pdf-regeneration-operation-continuations.json`
covers Generator, Importer and Documentation. VERSION stays 1.1.0, published
release metadata and production trust are unchanged, and existing compiler/
analyzer notices are not waived. These are selected local macOS/x64 regressions,
not new full API/PostgreSQL, visual, cross-platform or protected-package approval.

Remaining work is other native product composite graphs, Converter/Migration
enforcement, native issuance/Admin/Account and protection/package/platform
acceptance. Local macOS/x64 synthetic tests do not replace those release gates.
VERSION, published releases, live databases, manufacturer trust, unrelated
solution changes and private IDE files remain untouched. No commit, push,
deployment or service restart is performed.

## Product Dependency And Spreadsheet Continuation Milestone

This milestone records current cross-product boundaries and closes the selected
built-in Spreadsheet export/calculation/file-save continuation graph. It does not
decide prices, editions or universal bundle rights. Converter/Migration and other
unreviewed native graphs remain separate tasks.

- [x] Add the machine-readable [dependency inventory](../product-metadata/native-library-dependencies.v1.json), with implemented and planned rows clearly distinguished.
- [x] Validate unique rule IDs, known products/boundaries/statuses, source paths and existing private dependency names; reject malformed or invented permission entries.
- [x] Preserve PDF-only, Word-only and Spreadsheet-only product boundaries without requiring an extra Converter purchase for their existing native exports.
- [x] Record that a Spreadsheet DTO conversion does not authorize a separately started public PDF render, and configured Importer callbacks still require PDF rights.
- [x] Add Spreadsheet-only admission and internal continuation through workbook build, XLSX export/calculation, CSV/XLS byte export and builder file-save dispatch.
- [x] Keep public entries fresh, authoring/style callbacks independently guarded and all existing private PDF dependency rules unchanged.
- [x] Cover Trial expiry, missing/wrong rights, foreign authority and all private-dependency denials in the verifier-core tests.
- [x] Pass the 302-check Spreadsheet probe, including 157 new continuation checks, and the 34-check Word-only probe without accepting third-party distribution terms.
- [x] Compare five Spreadsheet processing files with `bdf33a5f`, restoring only six admission entries and equivalent built-in routing; preserve formulas, formatting and IO algorithms.
- [x] Complete the ten selected signed .NET suites and reconcile actual result rows, source hashes and cleanup.
- [x] Pass all 58 native/version/release-author tooling contracts, including inventory validation and fresh callback boundaries.
- [x] Validate final documentation links, product/version/fragment state, naming guards and whitespace.

### Native Spreadsheet And Dependency Evidence

`artifacts/native-engine-tests/1794915905592-2b3255ed/` passed 302 unique
checks, including 157 `sheet-continuation:` checks. Direct XLSX/XLS/CSV/TSV
export, calculation, builder XLSX and four file-save routes complete after a
custom cell value replaces the live license. New public/reentrant calls remain
denied. Reopened outputs retain text/formulas and requested calculated values.
Native callback, source IO and filesystem errors keep their original types;
failures before the final write retain the existing target or create no file.
This does not promise arbitrary file-write rollback or byte-identical ZIPs.

The unchanged Word-only probe at
`artifacts/native-engine-tests/1794916023581-d01ee29a/` passed 34 checks.
DOCX export/signing require no PDF or Converter grant, while separate calls to
those products remain denied. Both native reports confirm synthetic-only
licenses, removed temporary grants/builds and unchanged source trust and regular
licensing outputs.

`artifacts/native-test-sessions/fc549b8c-d959-43a7-a8c0-af2070f0eeb4/sheet-body-audit.json`
pins five processing-file hashes. Six explicit entries, admitted calls and
equivalent save routing are normalized; all remaining algorithms compare equal
modulo whitespace. Only one new Spreadsheet operation mapping and one first-party
friend assembly are added. Existing private PDF dependency rules, public APIs,
builder mutation guards and Word/PDF processing algorithms are unchanged.
The previous Importer-only exhaustive test now explicitly selects Importer
purposes; a dedicated Spreadsheet test covers the new product purpose rather
than incorrectly treating every non-PDF purpose as Importer.

The same session's `sheet-validation-audit.json` reconciles 1,710 passing TRX
rows: Licensing 91, Spreadsheet 13, Word 6, Importer 672, FileImporter 23,
Generator 141, Infrastructure.Pdf 76, ImageAnalysis 181, ImageOcr 100 and the
selected API regression 407. All ten processes exited successfully; failed,
skipped, aborted, timed-out and other non-passing rows are zero. All 1,092
snapshotted compiled/test source inputs (`.cs`, `.csproj`, `.mjs`) remain
unchanged. Session cleanup removed temporary synthetic licenses and isolated
builds without modifying source trust. This is not a full API, PostgreSQL,
visual, cross-platform or protected-package acceptance run.

All 58 native/version/release-author tooling tests and 59 local documentation
links/anchors passed. Product metadata, pending fragments, the former-name guard,
Markdown naming and `git diff --check` passed. The patch fragment
`native-spreadsheet-operation-continuations` records the compatible fix;
`VERSION`, published releases, production trusted keys and native release
metadata remain byte-identical to `bdf33a5f`. Existing compiler/analyzer and NPOI
warnings are not waived by these successful runs.

The inventory has twelve audited rows; Converter and provider-specific Migration
rows remain planned, not enforcement evidence. NPOI distribution approval,
manufacturer keys, native issuance/Admin/Account, obfuscation, package/platform
acceptance and unreviewed product graphs remain open. Local macOS/x64 tests do
not satisfy those external or release gates. No commit, push, deployment, live
database, version or published release change is performed. Existing solution
and private IDE edits stay untouched.

## Native Converter Enforcement Milestone

This milestone protects the native document exporters in
`PVAD.Infrastructure.Converters`. It does not decide commercial bundle pricing,
enforce Migration providers or close the remaining Importer/package gates.
Earlier milestone evidence and its original scope remain unchanged.

- [x] Guard all nine built-in exporters and thirteen declared public overloads with fresh Converter admission before processing the design.
- [x] Cover both `IDocumentExporter` overloads, including default interface dispatch, without a facade-only guard.
- [x] Retain original admission through built-in multi-page output and TIFF-to-PNG rendering; keep callbacks and new public operations fresh.
- [x] Preserve free metadata, public API signatures, rendering algorithms, DPI/quality defaults, external asset handling and output formats.
- [x] Verify missing/wrong/expired rights, failed renewal, successful renewal and non-mutating denials with a Converter-only fixture.
- [x] Verify no private PDF dependencies or implicit PDF/Word/Spreadsheet/Importer grants, including expiry and verifier-authority boundaries.
- [x] Exercise single-page and mixed-layout multi-page native bytes, ZIP entry names, MIME/signature expectations, dimensions and preserved document content.
- [x] Preserve original callback failures and typed native API Problem Details instead of partial/corrupt downloads.
- [x] Add Converter-only and integrated synthetic test grants, owned-session cleanup and CI probe/compatibility coverage without production trust changes.
- [x] Run selected signed .NET regressions, tooling contracts, source comparisons, documentation links, naming guards and whitespace checks; record actual results.
- [x] Document activation, dependency boundaries and limits, and add a Major release fragment for mandatory native Converter admission without changing `VERSION`.

### Converter Evidence

The final native probe at
`artifacts/native-engine-tests/1794917388776-4af2568d/` passed 519 unique checks:
nine exporters, thirteen declared public export methods and eighteen interface
calls. It compiles without warnings. Single/mixed-page results retain native
signatures, content, dimensions and archive entry names; admission is checked
before input access. Eight style callback cases retain admitted completion while
new calls remain denied. CSV has no style callback hook; verifier-core tests
cover expiry and product/authority isolation without inventing one.

The first probe attempt (`1794917245295-83845bca`) rejected its own SVG parser's
UTF-8 BOM handling; the second (`1794917324742-a2b3a9a2`) found an uninitialized
style dictionary in its synthetic callback fixture. These are failed
test-harness attempts, not passing evidence. Neither correction changes the
renderer or relaxes format/layout assertions. All three `run.json` records
confirm removed synthetic licenses/builds, unchanged source trust and unchanged
regular licensing outputs; only the final attempt has `succeeded: true`.

`artifacts/native-test-sessions/853ca2b7-bc20-4cc1-902e-f8a045b1a95c/converter-validation-audit.json`
reconciles 596 passing TRX rows: Licensing 92, Converters 10, selected Export 78
and selected API 416. The API cases include all nine Converter native-failure
routes through the export use case, controller and Problem Details handler.
There are no failed, skipped, aborted, timed-out or other non-passing rows. All
four processes exited successfully. All 1,095 snapshotted compiled/test source
inputs are unchanged; session cleanup removed synthetic grants and isolated
builds without modifying source trust. This is not full solution, API/PostgreSQL,
visual, cross-platform or protected-package acceptance.

`converter-body-audit.json` compares eight Converter processing files exactly
with `bdf33a5f` after removing only nine admission sites, the internal raster
continuation demand and the XML public wrapper. Algorithms and public signatures
are unchanged. The 300 Importer, Generator/PDF and Word/Spreadsheet source inputs
from the previous Spreadsheet session are unchanged. Only the new Converter
purpose/mapping and first-party friend assembly extend the execution core;
existing private PDF dependency rules are unchanged.

All 59 native/version/release-author tooling tests and 61 local documentation
links/anchors pass. Current CI former-name, Markdown, metadata, fragment and
whitespace checks pass. A separately invoked historical JSON naming audit still
reports 475 pre-existing findings. Its 48 affected files/audit inputs, including
retired-file absence, match `HEAD`; `preexisting-json-audit.json` records this.
That historical audit is not the current CI naming guard, is not silently waived
and is not changed by this milestone.

The Major fragment `native-converter-license-enforcement` records the new
mandatory native right. `VERSION`, published releases, production trusted keys
and native release metadata remain unchanged. Existing compiler/analyzer and
NPOI warnings are not accepted or suppressed. Cross-platform execution,
protected-package acceptance, manufacturer issuance/UI, third-party license
approval and Migration providers remain separate gates. No commit, publication,
deployment or database change is part of this milestone. Existing solution and
private IDE edits remain untouched.

## Native Code Migration Provider Entry Milestone

This bounded milestone covers 16 PDF Code and 8 Spreadsheet Code providers.
It does not complete Viewer/Designer/Report migration, standalone source-specific
catalog analysis or composite migration/diagnostic/preview lifetime policy.
Both Code product rows remain explicitly partial until those boundaries are
audited; the global all-provider acceptance task remains open.

- [x] Inventory all 24 sealed direct `Migrate` entries against the canonical domain/kind/provider catalog without changing provider IDs or aliases.
- [x] Admit each direct processing call with its own PDF Code or Spreadsheet Code product and exact canonical provider before parsing source.
- [x] Preserve constructors, contracts, factory discovery, example/catalog metadata and generic Roslyn helper behavior without facade-only enforcement.
- [x] Verify direct, abstract-base, interface, factory, compatibility-wrapper and PDF-aggregate dispatch.
- [x] Verify all 24 individual provider licenses against every other Code provider and document domain, including identical vendor families.
- [x] Verify unlicensed, wrong-product, expired-load, failed-renewal and renewed-license behavior without executing the converted code.
- [x] Preserve successful code/diagnostic behavior and compare all 24 converter bodies unchanged except admission.
- [x] Extend integrated synthetic sessions with only the two tested Code products; do not pre-enable Viewer, Designer or Report grants.
- [x] Test actual canonical/alias/legacy API routes, provider suites and tooling with owned-session cleanup and unchanged production trust.
- [x] Add documentation, prepared CI coverage and a Major fragment; keep `VERSION`, published releases and live databases unchanged.

### Code Migration Entry Evidence

The entry inventory is
`product-metadata/native-code-migration-entries.v1.json`; it supplements, rather
than replaces, the application provider registry. The registry remains the source
of canonical provider identities. Tests compare the two inventories and
reject missing, duplicate, cross-domain or alias-based native guards.

The native probe at `artifacts/native-engine-tests/1794919955149-1c186c7e/`
passed 1,187 unique checks across all 24 providers, including direct/base/interface,
factory/wrapper and PDF-aggregate calls. Every single-provider license is tried
against all 24 Code providers; the same vendor's other domain is not authorized.
It checks canonical routing aliases, non-mutating denial before parsing,
expired-load rejection, failed renewal and successful renewal on retained
instances. The probe generates text only, records 24 output checksums and does
not execute converted source. `run.json` confirms removed temporary licenses and
isolated builds, unchanged source trust and unchanged regular licensing outputs.

The signed session
`artifacts/native-test-sessions/2b4aee9c-5ecc-4107-8720-19f39b8a0a94/`
passed 712 TRX rows across 30 suites: Licensing 93, all sixteen PDF Code provider
suites 210, all eight Spreadsheet Code provider suites 22, PDF aggregator 101,
Spreadsheet aggregator 19, Converters 10, Generator 141 and selected API 116.
`migration-code-validation-audit.json` reconciles actual rows, process statuses
and cleanup with no failed, skipped, aborted, timed-out or other non-passing
cases. All 1,098 snapshotted compiled/test source inputs remain unchanged. API
tests cover actual canonical, alias and legacy denial with PDF-only rights,
typed `PVADLIC012` Problem Details and restored conversion after signed renewal.

`migration-code-body-audit.json` verifies all 24 provider files byte-identical to
`bdf33a5f` after removing only the added Licensing import and admission line.
The Roslyn project adds only the common Licensing dependency. All 436 snapshotted
other-product sources, including the execution core and private PDF dependency
policy, remain unchanged from the preceding Converter checkpoint. No parser,
rewriter, diagnostic ID, alias, public signature or rendering rule is changed.

All 62 native/version/release-author tooling tests and 63 local documentation
links/anchors pass, along with current CI former-name, Markdown, metadata,
fragment and whitespace guards. Earlier historical JSON-audit findings remain
separate; their records are not rewritten or waived. The Major fragment
`native-code-migration-provider-admission` records mandatory provider admission.
`VERSION`, published releases, production trusted keys and native release metadata
remain unchanged. Existing Apryse/compiler/analyzer and NPOI warnings are not
suppressed or accepted. These local macOS/x64 checks are not a complete solution,
API/PostgreSQL, visual, cross-platform or protected-package acceptance run.

Remaining work includes standalone source-specific analysis, composite lifetime
and dependency rules, Nutrient Viewer/Designer and all Report Designer native
admission. Native code conversion does not grant execution or rendering rights
for the generated program. Existing auth/entitlement/feature checks still apply.
No commit, push, deployment, product-version change or manufacturer key
provisioning is part of this milestone. Existing solution/private IDE edits and
earlier milestone result records remain untouched.

## Code Migration Analysis And Composite Milestone

This milestone closes the built-in PDF/Spreadsheet Code processing boundaries.
It does not implement Viewer, PDF Designer or Report Designer licensing. PDF
preview conversion is a Code operation; subsequent public PDF creation and
serialization deliberately check current PDF rights independently. Spreadsheet
preview only replays converted text to HTML and needs no additional engine SKU.

- [x] Bind internal Code continuations to exact product, canonical provider, originating verifier and open execution scope.
- [x] Keep every public provider entry fresh; expose admitted processing only through a first-party internal interface with provider-specific revalidation.
- [x] Protect standalone Trust/E-Invoice source analysis while retaining free catalog and DTO metadata.
- [x] Share admission across PDF conversion and Trust evidence; convert Spreadsheet code and diagnostics once instead of invoking two native conversions.
- [x] Share Code admission in built-in preview conversion, but preserve fresh PDF constructor/writer checks and legacy custom-converter dispatch.
- [x] Cover every provider and standalone analysis with incorrect/single-provider rights and output equivalence.
- [x] Cover expiry, nested providers, revoked current rights, foreign authorities, callback errors and closed-scope rejection without private PDF grants.
- [x] Test Code-only HTML preview, additional PDF preview rights, custom callbacks and real API error propagation.
- [x] Compare conversion, analysis, HTML/PDF drawing and diagnostic algorithms with their pre-change bodies without weakening fidelity thresholds.
- [x] Run selected signed .NET suites, tooling, link/naming/version/fragment checks and owned-session cleanup; record actual results.
- [x] Update documentation and dependency matrix and add a Major release fragment; retain original checkpoint evidence, version and production trust.

### Composite Evidence

The first compile/admission checkpoint at
`artifacts/native-engine-tests/1794922975083-0d8c019c/` passed the preceding
1,187 provider checks. The extended attempt `1794923256615-54cc6639` failed on a
test-harness assumption that immutable invoice capabilities were a mutable array.
The fixture was corrected without changing the catalog: adversarial callbacks
use caller-supplied Trust capability lists, while Invoice checks use public
admission, output comparison, rejection and renewal. The first tooling attempt
also matched a workbook name in a documentation comment; the assertion now tests
engine construction rather than prose. Neither test correction changes runtime
behavior or loosens rendering thresholds.

The final native run at
`artifacts/native-engine-tests/1794923471792-e3600ded/` passed 2,039 unique
checks, including 812 analysis cases (32 unlicensed cases plus 780 extended
checks). All 24 generated-code checksums match the preceding provider-entry
checkpoint. It tests single-provider isolation across analyses, public/reentrant
denial after a caller-triggered license change, preserved callback-native/IO
errors, retained admitted analysis output and subsequent renewal. Verifier-core
tests separately cover expiry, exact provider identity, omitted providers,
nested providers, foreign authority, closed scopes and private-dependency denial.
The initial, failed and final native runs all confirm synthetic-only licenses,
removed test builds/grants, unchanged source trust and unchanged regular licensing
outputs. The failed run remains failed evidence, not a passing result.

The signed session
`artifacts/native-test-sessions/1a44debf-1860-4156-891a-6a35a247d8c0/`
passed 715 TRX rows in 30 suites: Licensing 95, all PDF Code provider suites 210,
all Spreadsheet Code provider suites 22, PDF aggregator 101, Spreadsheet
aggregator 19, Converters 10, Generator 141 and selected API 117. The API cases
include all 24 built-in conversions/previews with Code-only rights: Spreadsheet
HTML succeeds without other engines and PDF preview fails without PDF rights.
Real HTTP routes preserve typed native errors; valid rights restore real PDF
preview. External custom converters retain their own callback dispatch and error
identity, and cannot reserve PDF rights across serialization.

`code-composite-validation-audit.json` reconciles all rows and process statuses
without failed, skipped, aborted, timed-out or other non-passing cases. All 1,100
snapshotted compiled/test source inputs are unchanged; cleanup removed synthetic
grants and isolated builds. `code-composite-body-audit.json` verifies the 24
conversion bodies, two source-analysis algorithm tails and two preview
drawing/replay bodies unchanged after removing the explicit admission/routing
wrappers. All 429 other-product source inputs are unchanged, and the existing
private PDF dependency switch is unchanged. No source is executed by migration.

All 63 tooling tests and 65 local documentation links/anchors pass, along with
current CI former-name, Markdown, metadata, fragment and whitespace checks.
The six implemented product areas now include PDF Code and Spreadsheet Code;
Viewer, PDF Designer and Report Designer remain unenforced. Earlier historical
JSON-audit findings remain separate and are not silently waived or rewritten.
The Major fragment `native-code-migration-analysis-continuations` records the
new standalone-analysis requirement and preview rules. `VERSION`, published
releases, production trusted keys and native release metadata remain unchanged.
Existing Apryse/compiler/analyzer and NPOI warnings are not suppressed or accepted.
No complete solution, full API/PostgreSQL, visual, cross-platform, protected-package,
manufacturer provisioning, commit, push, deployment or database acceptance is
implied. Existing solution and private IDE edits remain untouched.

## Nutrient Viewer And PDF Designer Milestone

- [x] Protect JavaScript, transpiled TypeScript and Instant JSON public native conversion entries with exact Viewer/provider admission.
- [x] Protect the composed source-PDF Designer operation with separate Designer/provider admission.
- [x] Keep private Viewer interpretation and source-PDF import purpose-bound to the admitted Designer operation, without additional public engine rights.
- [x] Preserve current conversion, PDF mapping, diagnostics and authorization behavior; return typed license problems instead of parse errors.
- [x] Test missing/wrong rights, standalone Viewer and Designer-only grants, public reentrancy, replacement, expiry, closed scopes, failure and renewal.
- [x] Run scoped Viewer, licensing, Importer and API regressions with synthetic signed licenses and isolated build cleanup.
- [x] Update the product/dependency inventory, usage documentation and Major release fragment from actual evidence.

### Implementation And Evidence

The three public Viewer conversion methods require `migration.pdf.viewer` with
the exact provider `nutrient-viewer`. The composed source-PDF operation requires
`migration.pdf.designer` with that provider instead. Its internal Viewer mapping
and private source import retain the original Designer admission, without an
additional public Viewer, Importer, PDF or Converter grant. The import continuation
is fixed to the Nutrient Designer purpose/provider and originating authority.
It cannot use the private PDF output dependency policy. New public and reentrant
operations still require current rights; expired, replaced, foreign-authority or
closed contexts cannot become reusable public grants.

The canonical Designer controller now preserves native license exceptions rather
than disguising them as invalid PDF input. Existing conversion algorithms, source
limits, diagnostics, chart analysis, page geometry, annotation/form mapping,
permission checks and feature gates are unchanged. The product/dependency
inventories now record eight enforced product areas. Importer remains partial;
Report Designer remains unenforced and is the next migration milestone.

The final signed session is
`artifacts/native-test-sessions/2d5d551b-7f0e-43b7-8c02-99eb5764aed5/`.
Its raw TRX audit confirms 230 passing rows in five suites: selected API 77,
Licensing 98, Nutrient Viewer 24, FileImporter 23 and selected Importer/chart
cases 8. There are no failed, skipped, aborted, timed-out or other non-passing
rows. All 1,344 snapshotted source/test/tool inputs remained unchanged during the
run; 1,337 compiled C# and project inputs are independently rechecked afterwards.
`viewer-validation-audit.json` reconciles process statuses and raw results;
`sources-unchanged.json` and `cleanup.json` record source integrity and removal
of synthetic grants and isolated binaries.

HTTP tests cover canonical and legacy Viewer routes for JavaScript, transpiled
TypeScript and Instant JSON, plus editable two-page mixed-orientation Designer
conversion. Wrong product/kind licenses return native `PVADLIC012` Problem
Details with status 503 and no attachment. Viewer-only and Designer-only grants
remain separate. Public nested calls after source-stream or compiler-diagnostic
callbacks are denied after license replacement, while the admitted operation
finishes; caller exception identity, subsequent denial and renewal are checked.

The authenticated Viewer fixture uses synthetic user/organization identities,
an isolated in-memory policy database, the real feature gate and a narrowly
granted synthetic Designer entitlement. A valid native license still leaves
anonymous legacy requests at 401 and missing permissions at 403. Earlier 401/403
failures were incomplete test prerequisites, not a reason to weaken application
authorization. This is not a persistent browser-session or live PostgreSQL test.

The standalone native probe at
`artifacts/native-engine-tests/1794936135959-a16ee8f2/` passed 60 unique checks
starting without an active license. All three normalized output checksums match
the preceding passing probe `1794934825519-e9974354`. Actual output hashes are
validated before normalizing the existing export timestamp and dependent hashes.
Composed design comparisons additionally validate and normalize the existing
random document ID; element/page IDs, content, geometry and other metadata remain
compared. No production timestamp or identity behavior was changed.

`viewer-body-audit.json` proves seven exact original-body/dispatch comparisons,
including the unchanged private PDF dependency switch. All 64 native-tooling and
versioning tests and 67 local Markdown links/anchors pass. Current naming,
version/fragment and whitespace checks pass. Existing compiler, analyzer and NPOI
warnings are not suppressed or accepted. The ordinary-source trusted key store,
native release metadata, `VERSION` and published release manifest are unchanged.

Earlier interrupted builds, socket restrictions and failed test-harness attempts
remain non-passing evidence. Parallel project builds are now disabled in the
isolated runner after the observed waiting build; this does not alter library
behavior. Synthetic material from failed attempts was also cleaned up.

The Major fragment `native-nutrient-viewer-designer-license-admission` documents
the new requirements. CI is prepared, not remotely executed. No full solution,
complete API/PostgreSQL, visual, Linux/Windows, obfuscated-package, manufacturer
provisioning or commercial-release acceptance is implied. No commit, push,
deployment, service restart or live database change was performed. Existing
solution and private IDE edits remain untouched.

## Report Designer Migration Milestone

- [x] Protect every public conversion overload on all eight native report converters and their compatibility wrappers.
- [x] Bind built-in conversion, subreport resolution and resource handling to one exact-provider admission without other product grants.
- [x] Resolve auto-detection and aliases to the canonical concrete provider before admitting conversion; keep detection and generic package utilities separate from licensed processing.
- [x] Enforce standalone resource-parser rights and preserve typed license errors on canonical and legacy APIs.
- [x] Test direct, auto, factory/interface, resource, subreport, binary/package, reentrant, expiry, isolation and renewal paths.
- [x] Run all affected report suites, licensing, native probes, targeted API and tooling tests with synthetic-only cleanup.
- [x] Update contracts, inventories, documentation, prepared CI and the Major release fragment from actual results.

### Implementation And Evidence

All eight concrete Report Designer providers now require
`migration.report.designer` with their exact canonical provider key. This covers
19 public native converter overloads, 11 public aggregate-wrapper overloads and
both public RESX parser paths. Factory and interface dispatch reach the same
guarded sealed implementations. There are 25 admitted conversion wrappers;
simple overloads delegate to their guarded overload without intervening caller
work. Metadata/detection and generic bounded package utilities remain free;
they do not grant conversion rights or make `auto` a licensable provider.

The API service resolves detection/aliases before admitting the concrete
provider, then merges caller resources and invokes only built-in internal
continuations. Shared RESX preprocessing uses the selected report grant, not an
extra DevExpress grant. Standalone DevExpress RESX parsing has its own fresh
DevExpress check. Subreport recursion retains admission without admitting a
second provider. Public/reentrant calls always check current rights, and no
private PDF dependency or other engine permission is introduced. Existing input
limits, provider capabilities, fidelity, diagnostics and renderer behavior are
unchanged. Nine native product areas now have their listed entry enforcement;
Importer composite coverage and full package/product acceptance remain partial.

The successful signed session is
`artifacts/native-test-sessions/37f05ac5-b11b-4299-8b23-a1ebf4eb1998/`.
`report-validation-audit.json` reconciles all 490 raw TRX rows and eleven process
results: selected API 75, Licensing 99, report aggregate 20, ActiveReports JS 4,
DevExpress 75, FastReport 26, JasperReports 28, RDL 94, RPX 30, Stimulsoft 18 and
Telerik 21. There are no failed, skipped, aborted, timed-out or other non-passing
rows. All 1,349 snapshotted source/test/tool inputs are unchanged. The owned
session removed its synthetic grants and binaries, with unchanged source trust.
Existing optional external-sample test behavior is not evidence that every
upstream sample/corpus was present or that provider feature gaps are closed.

The API checks exercise all providers through canonical, alias, auto and legacy
routes with wrong-product and valid report grants. Native failures retain
`PVADLIC012`, status 503, Problem Details and no download. All eight composed
conversions finish after caller resource enumeration replaces the license, while
fresh public calls are denied. They compare output, preserve caller exception
identity and recover after renewal. Binary/ZIP selection checks rights before
caller resource enumeration. A direct RPX subreport lookup exercises replacement
during native resource access and still inlines the child under the original
admission. Existing permission/Viewer/Code migration cases remain covered.

The native probe at
`artifacts/native-engine-tests/1795115577122-5a2e33c6/` passed 703 unique checks
across 37 direct/factory/resource call cases and eight separately signed provider
grants. It starts unlicensed, checks admission before source validation, rejects
wrong products and all other report providers, preserves failed-renewal behavior
and compares valid renewed output. Only each pre-existing random root document
ID is validated and normalized; element/page IDs, geometry, content and diagnostics
remain compared. The preceding probe `1795114430736-b38bd13b` was interrupted
while its build waited without CPU activity. It remains failed evidence. The
local retry used `MSBUILDNOINPROCNODE=1`; both attempts removed synthetic material
and preserved regular license outputs and source trust. No runtime bypass or
production build setting was introduced.

The initial successful session `757e3b86-ce66-4ef2-be89-6c719f3ed50a` also passed
490 cases. A subsequent naming review removed an incidental former-name substring
from a new internal method, without changing public names or behavior. The RPX
source-file naming pin was refreshed only after exact original-body comparison;
existing RPX style keys remain unchanged. The final eleven-suite session passes
again, and all 37 normalized native output checksums match the first successful
probe `1795114808783-88806c5c`. No naming exception was added or broadened.

`report-body-audit.json` proves all 16 converter/wrapper bodies unchanged after
removing explicit admission/private-dispatch wrappers. Resource parsing,
detection, resource merging and the existing private PDF dependency switch are
also verified unchanged. All 65 tooling/versioning tests and 69 local Markdown
links/anchors pass. Naming, release metadata, fragment and whitespace checks
pass; existing compiler, analyzer and NPOI warnings are not suppressed or accepted.

The Major fragment `native-report-designer-license-admission` records the new
mandatory rights. `VERSION`, published releases, production trusted keys and
native release metadata are unchanged. The CI workflow is prepared, not remotely
executed. No full solution, full API/PostgreSQL, browser/visual, Linux/Windows,
obfuscated-package, manufacturer provisioning or commercial-release acceptance
is implied. No commit, push, deployment, service restart or live database change
was performed. Existing solution and private IDE edits remain untouched.

## Non-PDF File Importer Continuation And Stream Ownership Milestone

This milestone reconciles the two current Migration parent tasks with the
completed provider milestones. Historical checkpoint results and task states
remain unchanged. It does not close the all-product or package acceptance gates.

- [x] Reconcile migration-provider guards and concrete automatic report detection in the current master checklist using the completed Code, Nutrient and Report milestones.
- [x] Inventory all eight registry implementations and twenty declared public processing overloads; compare source metadata and actual exported methods and reject unknown/missing types.
- [x] Exercise all seven non-PDF implementations through sixteen direct, seven registry and seven cancellation-interface call paths after license replacement during input reads.
- [x] Compare full DTOs, input bytes, layout and diagnostics on seekable/non-seekable inputs and yielded asynchronous reads without granting extra product rights.
- [x] Verify that fresh public calls fail before consuming input and that yielded Markdown resolver callbacks cannot inherit native rights; preserve resolver errors and successful renewal.
- [x] Correct Markdown and ODT readers to leave caller-owned input streams open; retain private reader/package disposal and existing parsing algorithms and limits.
- [x] Pass the complete FileImporter, Importer, image-analysis, OCR and licensing suites and selected native API regressions in a signed isolated session; audit every raw result row and cleanup.
- [x] Finish tooling, naming, release-fragment, local link and whitespace checks and record the actual local evidence without implying platform/package acceptance.

The successful native probe at
`artifacts/native-engine-tests/1795117284167-54d6764f/` passed 3,703 unique checks,
including 667 new `file-importer-continuation:` checks. The earlier run
`1795117152614-2e271dd5` exposed the Markdown stream-ownership failure and remains
failed evidence. Both attempts removed synthetic keys/licenses and isolated
binaries and preserved regular licensing outputs and source trust.

The runtime changes only alter reader ownership: two Markdown `leaveOpen`
settings and the ODT archive's `leaveOpen` option. All parsing/rendering algorithms,
license checks, private dependency policies and document limits remain unchanged.
The probe normalizes only validated random document IDs and the image importer's
random page/element IDs. All remaining content, IDs, geometry, assets and
diagnostics are compared. The synchronous importers do not become asynchronous;
both stream implementations are exercised, and Markdown additionally yields in
its asynchronous read and resolver paths. No external requests are issued.

The Patch fragment `file-importer-caller-stream-lifetime` describes the compatible
caller-stream fix, not a new licensing requirement. The existing mandatory
enforcement fragments retain their Major impact. Importer remains partial pending
complete public-surface and cross-product/package/worker/platform acceptance;
authenticated native issuance, Admin/Account integration, signed revocation-list
import and joint protected-product packaging remain separate open work.

All 66 native/versioning/release-author tooling contracts pass, as do 73 local
Markdown links/anchors, the 239-file reviewed naming guard, the 297-file Markdown
zero-match check, release-fragment/version validation and `git diff --check`.
The existing partial-coverage contract now names the remaining public-surface,
dependency, package and worker/platform acceptance explicitly instead of leaving
already-tested operation lifetimes categorically described as unimplemented.
No exception or test threshold was relaxed.

The signed regression session at
`artifacts/native-test-sessions/23590eb2-dc75-40fd-a759-8941cff72b59/`
passed all 1,092 actual result rows: Licensing 99, FileImporter 29, Importer 672,
image analysis 181, OCR 100 and selected API 11. This includes six new stream
ownership cases for successful Markdown/ODT reads, oversized Markdown and an ODT
package without content. All raw total/executed/passed counts agree; failed,
skipped, timed-out, aborted and other non-passing counters are zero. Every selected
API class produced result rows. Native OCR and LittleCMS requirements were enabled
for this local invocation; optional external corpus behavior is not a claim of
complete third-party format coverage.

`file-importer-validation-audit.json` independently reconciles the six TRX files,
process statuses, all 1,350 unchanged snapshotted source/test/tool inputs, the
120 full-output comparisons and the unchanged algorithms apart from stream
ownership. `cleanup.json` confirms removed synthetic grants and isolated build
output, unchanged signed fixtures and unchanged source trust. The normal license
build outputs are also unchanged after the native probe.

This is local macOS evidence using the x64 .NET SDK/runtime. It does not claim a
fresh complete solution/API/PostgreSQL run, visual/platform acceptance, protected
NuGet compatibility or production trust provisioning. Existing compiler/analyzer
and NPOI decision warnings remain visible; no supplier terms were accepted.
VERSION, published releases, native release metadata and production trust are
byte-identical to HEAD. No commit, push, service restart, deployment or live
database change was performed. The existing solution and private IDE edits remain
untouched.

## Importer Public API Contract Milestone

- [x] Capture all exported types and public members from the four Importer libraries, including base types and explicit interface bindings, without touching production trust or regular build output.
- [x] Classify 363 types as 81 processing types, 236 models, 25 utilities and 21 interface contracts; list all 201 protected members and remaining callable helpers explicitly.
- [x] Require exact protected-member registration evidence from successfully completed Importer and image/OCR probes; retain all existing output, negative, callback and lifetime assertions.
- [x] Add unlicensed/wrong-product free graph, model, geometry, raw-buffer, metadata and invalid-URI adapter checks without making network requests.
- [x] Verify image analysis through its explicit interface and default cancellation interface method with correct and missing rights before source consumption.
- [x] Reject unreviewed types, methods, constructors, properties, base/interface changes, duplicate evidence and missing native coverage; avoid assembly-version-dependent generic type names in snapshots.
- [x] Separate current entry-point enforcement from still-open cross-product, protected-package and worker/platform acceptance; preserve historical checkpoint results and broad acceptance gates.
- [x] Pass the updated licensing suite, tooling contracts, documentation links, naming/version/fragment and whitespace checks and independently audit local evidence and cleanup.

The successful owned session at
`artifacts/native-test-sessions/243dff1b-4359-4961-a051-2d1d2260bebc/`
compares all 5,087 declared public members and both explicit interface bindings.
It passes 3,721 Importer checks and 249 image/OCR checks, with 201 exact protected-
member registrations. Real native OCR succeeded. All 1,690 source/test/tool/JSON
inputs stayed unchanged during the run. The session removed its synthetic grants
and binaries and preserved source trust and signed fixtures. No test selection,
threshold, native-processing algorithm, private dependency rule or runtime
licensing boundary was changed by this milestone.

The initial generic capture could not resolve SkiaSharp in its minimal loading
context. A dedicated typed metadata probe now references the four libraries;
the existing protected-package API probe is unchanged. The first combined run
at `568477d9-76e9-4fb0-81c4-31149f072a72` failed a new free-model fixture: an
indirect object correctly retains its original object ID rather than comparing
equal to an untagged integer record. The fixture now checks the stored value,
original ID and object identity. Product code was not changed. Both unsuccessful
attempts retain their diagnostics and confirmed cleanup; they are not counted as
passing runs. Captured generic interface names were also made version-independent
before the final successful run.

The inventory pins the current public shape, not method bodies. The native
registration set is checked only after each probe completes successfully; it does
not replace full functional or platform tests. The 21 interfaces are declarations
and caller extension points, not grants to execute built-in processing. The nine
file-import implementations comprise eight registry entries plus optional image
analysis. The worker executable is tested separately, not miscounted as another
public library. Constructor/setup/disposal and detached model/value behavior are
classified separately from processing, including the protected PKCS12 constructor.

All ten product rows now record current entry-point enforcement. The Importer row
retains a partial `acceptanceStatus` and explicit `remainingAcceptance` gates;
`remainingEntryPoints` is empty for the reviewed current API. The probes' coarse
`completeImporterEnforcement: false` flag still makes no broad whole-product
release claim. Authenticated manufacturer issuance/audit, native Admin/Account
integration, signed revocation import, joint protected packaging and platform
acceptance remain open. The internal-only fragment
`native-importer-public-api-contract` has impact `none`: this milestone adds no
new customer license requirement or shipped processing behavior.

The independent `public-api-validation-audit.json` reconciles the raw compiled
surface, both completed native reports and all 99 raw licensing TRX result rows.
Every total/executed/passed counter agrees; no failed, skipped, aborted or other
non-passing result remains. Core-test evidence is in
`artifacts/native-engine-tests/1795119280626-afbc67d9/`. Both owned runs removed
synthetic material and isolated binaries. The audit additionally confirms that
all 909 shipping source/project inputs from the preceding checkpoint are
unchanged and the generic protected-package API probe is byte-identical to HEAD.

All 69 native/version/release-author tooling contracts and 77 local Markdown
links/anchors pass. Reviewed naming, Markdown zero-match, version/fragment
validation and `git diff --check` pass. The prepared CI command uses one shared
session for API capture and both native probes rather than accepting old artifact
globs; it has not been executed remotely. There is no new full solution,
API/PostgreSQL, visual, Windows/Linux or protected-package acceptance claim.
Existing compiler/native/NPOI warnings remain visible and no supplier terms were
accepted. VERSION, published releases, trusted keys and native release metadata
remain unchanged. No commit, push, service restart, live database change or
deployment was performed; solution and private IDE changes remain untouched.

## Manufacturer Native Issuance And Renewal Backend Milestone

- [x] Add a separate default-disabled manufacturer issuer profile; require an explicit user-ID allowlist, actual global System Administrator role, permission, verified active user, platform-operator access and a live recent persistent session.
- [x] Keep native signing separate from existing server licenses; validate every new artifact with the native embedded public trust and never generate replacement keys.
- [x] Atomically append the native artifact and successful audit event in PostgreSQL without recording customer references, payloads or key material in audit details.
- [x] Protect issued records against EF and direct SQL update/delete, including a database trigger and immutable renewal chain.
- [x] Support idempotent request retries and reject changed replays, invalid providers, inactive customer organizations and concurrent duplicate successors.
- [x] Renew subscription end dates or perpetual update cutoffs without expanding rights or modifying old artifacts; reject Trial renewal.
- [x] Protect manufacturer HTTP mutations with CSRF, recent authentication, rate limits and request-size limits; exclude the internal API from public OpenAPI and disable response caching.
- [x] Test all three models, cryptographic verification, transaction rollback, concurrent renewal, denied manufacturer access, HTTP download and protected operator-guide access using synthetic keys and real PostgreSQL.
- [x] Document the operator workflow in a protected runbook, add a prepared Linux PostgreSQL CI job and keep production trust, customer deployments and published versions unchanged.

### Evidence And Boundaries

On 2026-11-20 the isolated run at
`artifacts/native-engine-tests/1795153517612-c70eea5a/` passed all 39 selected
API tests without skips. Eight database-backed native issuance cases used the
actual production signer with ephemeral embedded test trust, not the fallback
business-transaction test double. Existing server-profile separation and
replacement tests also passed. The core run at
`artifacts/native-issuer-core-tests/results/core.trx` passed all 99 cases.
All 72 native/version/release-author contracts and six operator-guide contracts
passed. The mutation contract now covers 52 Admin mutations. Documentation
production build and all 73 Documentation tests pass; the built public output
contains neither the native issuer configuration nor the protected runbook.
An independent XML audit reconciles all 138 raw .NET result rows with their
total/executed/passed counters and confirms all eight real-signer database cases.

The issuer run removes its temporary private key, licenses and isolated binaries;
its cleanup record confirms unchanged source trust and regular licensing build
output. The subsequent exact-label Docker query returned no testcontainers.
Only an additive EF migration was generated; no live database was migrated.

Native Admin management screens, tenant-authorized Account downloads, signed
revocation-list import, joint obfuscated packages, full worker/platform/visual
acceptance, and production key provisioning remain open. Renewal preserves old
offline artifacts; it does not remotely revoke them. The prepared CI job has not
been executed remotely. The initial local test selection did not include the
existing AdminSystemController test class; the updated prepared selection does.
The new native HTTP test directly verifies the protected guide route.

The current former-name guard, Markdown zero-match, version synchronization,
release-fragment validation and `git diff --check` pass. The historical JSON
naming-audit tool still reports stale pre-hard-cut baseline findings; it is not
the current CI naming gate and was not rewritten as part of licensing work.
Existing compiler/NPOI warnings remain visible; no supplier terms were accepted.
The new customer-facing fragment is `minor`. VERSION, published releases,
manufacturer trust and release-date metadata remain unchanged. No commit,
push, service restart, live database update or deployment was performed.
Unrelated solution and private IDE changes remain untouched.

## Native Admin And Account License Workflow Milestone

- [x] Add a separate manufacturer-only Admin workspace with catalog-driven product/provider selection, organization search, model-specific terms, paginated records, details, downloads and immutable renewal navigation.
- [x] Preserve draft contents and idempotency keys on failed retries; expose disabled, loading, error, recent-sign-in and success states without putting signing secrets in the browser.
- [x] Add Account list, details and exact downloads with server-resolved organization context and active verified user, membership, organization and persistent-session checks; deny API-key access and foreign records.
- [x] Keep customer reads independent of manufacturer mode/private keys and server offline-license schemas; verify stored content integrity before delivery without rewriting or re-signing artifacts.
- [x] Discard late responses after identity/workspace changes and stop Account SPA routing from intercepting API/file downloads, including existing server-license downloads.
- [x] Exercise issuance, renewal, exact downloads and organization switching at desktop/mobile sizes, inspect screenshots, and enforce control/heading bounds in addition to document-width checks.
- [x] Add protected Admin handbook coverage and synthetic screenshots; publish only the three read-only Account API routes and required DTOs while excluding manufacturer routes from public OpenAPI.
- [x] Pass selected API/PostgreSQL, frontend, contract, build and naming checks; add a customer-facing Minor fragment without changing VERSION, production trust or published releases.

### Evidence And Boundaries

The successful isolated run on 2026-11-20 at
`artifacts/native-engine-tests/1795155409384-6a3c69d2/` passes all 54 selected
API tests without skips. This includes all 13 native issuance/workflow cases,
ten of them database-backed with the real signer and ephemeral embedded trust,
existing server licensing separation/replacement, Admin System workflows,
mutation contracts and canonical/OpenAPI tests. Customer HTTP reads work with
manufacturer configuration disabled, reject anonymous and foreign requests,
return exact bytes, and do not grant manufacturer access. Direct database cases
also cover revoked sessions, removed memberships, cross-context access,
pagination, catalog grants and renewal history.

The first combined `dotnet test` attempt at
`artifacts/native-engine-tests/1795154679110-e251d24d/` stopped making progress
inside its own MSBuild node. SIGINT and SIGTERM did not complete cancellation;
only the two identified owned build processes were stopped. The Node runner
then confirmed removal of synthetic licenses, keys and binaries with unchanged
source trust and regular build outputs. The successful repeat uses explicit
restore, serial build and VSTest phases. No IDE or unrelated build process was
terminated, and the interrupted attempt is not passing evidence.

Frontend evidence: 48 Admin tests, 57 Account tests, Account type-check and both
production builds pass. All 73 native/version/release-author contracts and 73
Documentation tests pass; Documentation also builds with the updated public
Account API contract. Only three new paths and their four required schemas were
merged from the generated OpenAPI; existing paths and schemas are unchanged.

`artifacts/native-license-ui/report.json` records successful 1440px/390px
Chromium rehearsals. Browser HTTP is explicitly mocked with non-usable synthetic
artifacts, not represented as a real login or cryptographic test. Initial
rehearsals exposed an actual Account download-routing defect and clipped layout
inside mobile Admin grids; both are corrected. Other failed attempts corrected
Playwright datetime input and mobile-navigation fixture assumptions. Final
screenshots were visually inspected; controls and headings fit, and only the
tables intentionally scroll horizontally. Protected desktop/mobile screenshots
are stored under the Admin API asset directory, never public Documentation.

The final raw-result audit checks all 54 result rows and counters, ten real-signer
database outputs, the passing browser report and cleanup metadata. The exact-label
Docker query reports no remaining testcontainers. Current naming, Markdown,
version/fragment and whitespace checks pass. Existing NPOI/compiler/Vite warnings
remain visible; no supplier terms were accepted.

This closes the native Admin/Account task, not complete SDK distribution or
platform acceptance. Signed offline revocation-list import, joint Obfuscar
processing of product assemblies, local-feed consumers, cross-platform/visual
acceptance and manufacturer key provisioning remain open. Next recommended work
is the signed revocation-list contract and local verifier/import path. No product
version bump, key provisioning, live database migration, commit, push or deployment
was performed. Unrelated solution and private IDE changes remain untouched.

## Signed Offline Revocation List Milestone

- [x] Add a bounded, purpose-separated, versioned signed-list contract using exact payload bytes, ECDSA-P256/SHA-256/P1363 and only embedded known manufacturer keys.
- [x] Provide explicit file/stream import and content-free status; serialize list changes and license activation so rejected imports/reloads preserve accepted state.
- [x] Require increasing global sequences and cumulative revoked IDs; reject changed same-sequence payloads, future dates, backward issue dates and removed revocations across known-key rotation.
- [x] Preserve known revocations in stale lists, deny new operations and revoked-license reloads, and allow previously admitted private continuations to finish.
- [x] Extend the owner-controlled offline preparation CLI to sign revocation lists without generating keys, overwriting artifacts or claiming authenticated durable revocation management.
- [x] Import configured lists before API/worker activation; propagate verified signed snapshots to Code/OCR workers without source-visible secrets or inherited unverified OCR configuration.
- [x] Test cryptographic/JSON boundaries, rollback, concurrency, stream ownership, issuer preparation, real worker admission and cleanup; document freshness and restart limitations and prepare a Minor fragment.

### Evidence And Boundaries

On 2026-11-20 the core run at
`artifacts/native-revocation-core-tests/results/core.trx` passes all 118 tests,
including 17 revocation cases and two additional revocation-issuer cases. An
independent XML audit reconciles every result row and total/executed/passed
counter; no failures, skipped, aborted or other non-passing results remain.

The final isolated run at
`artifacts/native-engine-tests/1795170672176-5973d8f5/` passes all 13 named
integration checks. The actual CodeWorker rejects a revoked configured PDF
license with `PVADLIC014` and no design/PDF result, while a non-revoked license
still produces a real PDF. The real OCR worker rejects a revoked Importer license
before reading its input. A controlled child of the real OCR process runner
separately verifies signed-list propagation and removal of inherited unverified
configuration; this is a transport/admission test, not a Tesseract recognition
accuracy claim. API bootstrap rejects an unreadable configured list without
discarding the existing valid license. License reload cannot erase revocations.

The runner confirms removal of synthetic licenses, lists and isolated binaries,
unchanged source trust, and unchanged regular licensing build output. The earlier
successful run at `artifacts/native-engine-tests/1795170216414-12143dee/` had 11
checks before the two additional bootstrap assertions; the final report above
supersedes it. No private key is exported for this integration probe.

All 77 native/version/release-author tooling tests and 73 Documentation tests
pass. Documentation builds successfully; all 82 local links/anchors in the five
licensing guides/checklist resolve. Current naming, Markdown zero-match,
version/fragment and whitespace checks pass. The isolated WebApi/Code/OCR build
succeeds with existing compiler/NPOI warnings visible; no supplier terms were
accepted. This is not a new full API/PostgreSQL, solution, visual or cross-platform
acceptance run. The added CI command is prepared but has not run remotely.

Lists are optional explicit offline inputs, limited to 512 cumulative license
IDs. Stale lists keep all known denials and expose freshness without expiring
unrelated valid licenses. Anti-rollback state is process-local: operators must
persist and reimport the latest approved file after restart. No trusted clock,
mandatory Internet, automatic remote kill, public reset/unrevoke, signing-key
revocation, Admin revoke button, automated durable sequence allocation or
revocation-distribution service is claimed. Those management/scale extensions
remain separate follow-ups, not silently completed by the offline signing CLI.

The customer-facing fragment is `native-offline-license-revocations` with Minor
impact. Next recommended work is joint first-party Obfuscar processing and
independent local-feed package consumers, followed by platform acceptance.
VERSION, published releases, embedded production trust and release-date metadata
remain unchanged. No commit, push, service restart, live database update or
deployment was performed. Unrelated solution and private IDE changes remain
untouched.

## Joint Generator Protection And Package Consumer Milestone

- [x] Define and validate the complete nine-assembly Generator project closure; fail on missing or unexpected first-party projects instead of silently shrinking coverage.
- [x] Process Licensing, Core, Domain, Application, Pdf, Infrastructure.Pdf, Infrastructure.Word, Infrastructure.Spreadsheet and Generator in one pinned Obfuscar invocation; leave third-party binaries unchanged.
- [x] Compare 413 exported types across public signatures, parameter names/defaults, literal values and JSON member attributes; use ILSpy to verify renaming of two known internal implementation types.
- [x] Prepare opt-in packages containing each exact protected DLL, XML API documentation and no source/private-symbol/key files; retain the existing prohibition on synthetic test-session publication.
- [x] Restore an independent package-only consumer from source-mapped local feeds and an empty private cache; verify all nine product versions/hashes and the downstream build-task constraint.
- [x] Compare baseline and protected JSON/DOCX/XLSX semantics and two PDF pages at 72 DPI; test missing, incorrect and revoked rights, retained objects, embedded resources and friend metadata.
- [x] Remove candidate binaries, packages, maps, caches and synthetic licenses on success/failure; document limits, prepare separate Linux CI and add an internal-only release fragment without publishing.

### Evidence And Boundaries

The successful local macOS/x64 run on 2026-11-20 is recorded in
`artifacts/native-joint-packages/1795171992772-f8655f81/report.json` and
`cleanup.json`. All 30 original baseline assertions and 187 protected-consumer
assertions pass. The consumer uses only NuGet package references, not source
project references. Its nine loaded first-party DLL hashes equal the protected
DLLs and package entries. All 413 public types compare equal; the license
authority and PDF writer internal names no longer appear in ILSpy's type list.
Thirty third-party DLL resolution inputs are byte-identical. Thirty-six original
third-party packages provide the offline consumer feed. The consumer's existing
Git build-task pin resolves to 10.0.403 rather than being lost at the package
boundary. Normal source/project build settings remain unchanged.

Both PDF page rasters match exactly. JSON and Word document XML compare equal;
Spreadsheet cells, formulas, values and styles compare equal. Only the freshly
generated workbook/sheet workspace IDs are normalized, after validating their
GUID shape; XLSX does not carry these PVAD workspace identities. This comparison
does not relax geometry, pixel or semantic thresholds or replace existing golden
PDF Trust/visual-parity matrices.

The initial run at `artifacts/native-joint-packages/1795171420263-bc30c4a9/`
exposed the missing downstream build-task constraint and is not passing evidence.
The following run at `artifacts/native-joint-packages/1795171636669-32416b7d/`
resolved dependencies but stopped making progress in its own external MSBuild
node. Only its identified build process was interrupted/terminated; its child
node also exited. The runner cleaned its temporary material. The final run
removes the forced external-node environment setting only for the single-project
consumer build. Both earlier cleanup reports show failure honestly and confirm
unchanged source trust/regular product outputs and removed candidate files.

All 83 native/version/release-author tooling tests and 73 Documentation tests
pass, as do the Documentation build, current naming/Markdown gates, version and
fragment validation and `git diff --check`. Existing compiler, Vite and NPOI
warnings remain visible. No NPOI supplier terms were accepted. Offline consumer
restore disables only its network NuGet audit, not repository security gates.
The prepared Linux job uploads only non-secret report/cleanup JSON, never the
candidate feed, binaries, source listings, maps or synthetic artifacts. It has
not been executed remotely.

This closes a scoped nine-assembly rehearsal, not the P1 protection/package
acceptance for every native product. Importer, Converter and Migration assemblies,
actual Code/OCR workers and API integration, complete DI/serialization/rendering
regressions, Windows/Linux/macOS acceptance and support-map retention remain
open. Do not mix this protected subset into an unprocessed platform deployment:
friend-visible internal members can change names, so consuming first-party
assemblies must join the graph or use reviewed preservation boundaries. Commercial
approval, manufacturer keys, notices/EULA and distribution gates remain separate.

The fragment `native-generator-protected-package-rehearsal` has impact `none`:
these are disposable internal candidates, not a shipped SDK change. The next
recommended step is to extend the reviewed graph to Importer and its worker
boundaries before addressing the remaining native providers. VERSION, published
releases, production trust and native release-date metadata remain unchanged.
No commit, push, service restart, live database change or deployment occurred.
Unrelated solution and private IDE changes remain untouched.

## Joint Importer And OCR Worker Protection Milestone

- [x] Add an explicit 14-assembly Generator/Importer/ImageAnalysis/OCR profile, preserving the existing nine-assembly profile and rejecting unexpected project closures.
- [x] Jointly protect all profile assemblies, package 13 libraries and prepare a separate OCR runtime bundle with four matching first-party DLLs; never package the executable as a library.
- [x] Preserve the four reflection-resolved PDF regeneration types and the page element field with narrow annotations; retain compiler-generated types/members and their unmarked nested collection helpers without excluding ordinary product implementations.
- [x] Verify 776 public types, package/loaded-DLL hashes, all eight registry import formats, source-stream ownership, PDF load/rewrite/regeneration and real Skia image analysis in an independent package consumer.
- [x] Run actual original/protected OCR workers on macOS/x64 with synthetic English text, bundled font/native libraries/language data and exact recognized word/bounds/confidence comparison; enforce real worker license denials before input reads.
- [x] Preserve typed `PVADLIC014`/`PVADLIC015` errors through the parent OCR engine and verify error-path cleanup with targeted source tests and protected-package assertions.
- [x] Retain named assertion evidence, remove all private candidates on every completed run, extend prepared CI/documentation and add separate internal-only and Patch fragments without publication.

### Evidence And Remaining Regression Gate

The successful local macOS/x64 run on 2026-11-20 is
`artifacts/native-joint-packages/1795176805589-8def3ffd/`. It passes 100 unique
baseline assertions and 465 unique protected-consumer assertions. All 776 public
types compare equal. The 13 restored libraries match their protected package
DLL hashes. The worker bundle records matching Core, Licensing, ImageOcr and
Worker assembly hashes. Thirty-four third-party DLL resolution inputs remain
byte-identical; 40 original dependency packages supply the offline restore feed.

Actual native OCR executes on both sides and must recognize the synthetic
English text. Recognized words, bounds and confidence compare exactly. Real
worker processes reject missing, wrong-product and revoked licenses before
reading the request. Parent-engine tests retain both revocation codes and remove
staged files. The fixture projection compares page layout/extensions and element
type/content/geometry, not every import property or provider fidelity. Both
Generator PDF pages and the regenerated PDF raster match their baselines.
No image threshold, geometry threshold or source fixture content was relaxed.

The targeted source run at
`artifacts/native-engine-tests/1795175620846-4d3679dc/` passes all nine OCR engine
tests, including four native-diagnostic theory cases, without skips. Its raw TRX
rows/counters and cleanup record were independently reconciled. All 85
native/version/release-author tooling contracts and 73 Documentation tests pass;
Documentation builds successfully. Existing compiler, Vite and NPOI warnings
remain visible; no supplier terms were accepted.

Earlier candidate runs are retained only as failed cleanup records, never passing
evidence: `1795175571564-fab458c1` exposed inconsistent record virtual-method
rules; `1795175736484-d45b1643` exposed the global short-alias collision;
`1795176107230-831da152` and `1795176342223-eacacac4` exposed generated generic
collection field-reference failures; `1795176565975-2e09e17c` narrowed the remaining
failure to an unmarked nested enumerator. `SkipGenerated` alone was insufficient.
The final decorator/member and compiler-name rules preserve that machinery.
The writer and license-authority ILSpy checks still confirm implementation-name
obfuscation. Shared fixture implementation/literals remain byte-identical after
removing only the added namespace directive. Its aliases are now namespace-scoped;
Obfuscar's short internal namespaces remain a documented consumer-source caveat.

**The broader source Importer regression is not green.** The additional run at
`artifacts/native-engine-tests/1795176550618-cc838b49/` compiled successfully but
failed `pdfa2-cmyk:async:identical-output-and-strategy` in the existing E-Invoice
continuation test. `PdfEInvoiceAttacher` writes attachment `ModDate` using current
UTC time, a known nondeterministic input to this raw byte comparison. The failed
run is not counted as acceptance. Make that clock/input deterministic, preserve
the strict byte comparison and rerun the full source probe before closing the
existing broad Trust/regression gate. No E-Invoice production behavior or test
assertion was changed in this milestone.

The final package and source-test runners confirm removed synthetic material and
unchanged source trust/regular build outputs. This is a scoped package/OCR smoke
milestone, not complete Importer/Trust/platform or native-product acceptance.
Converter, CodeWorker, WebApi, Migration/provider protection, general source/DI
compatibility, other native OCR runtime bundles, support-map retention and
commercial distribution remain open. The Linux CI step is prepared but has not
run remotely. No protected subset may replace unprocessed platform DLLs.

The packaging fragment has impact `none`; typed OCR diagnostic propagation has
impact `patch`. Next recommended work is the deterministic E-Invoice regression
input/clock, followed by the remaining CodeWorker/Converter and provider graph.
VERSION, published releases, production keys and native release-date metadata
remain unchanged. No commit, push, live database mutation, service restart or
deployment occurred. Unrelated solution and private IDE changes remain untouched.

## Deterministic E-Invoice Attachment Clock Follow-Up

- [x] Keep the original public attacher constructor and add an explicit `TimeProvider` overload for attachment metadata only; reject null dependencies and leave license/signature/archival clocks independent.
- [x] Supply a fixed UTC metadata clock in the continuation probe without stripping metadata, normalizing output bytes or relaxing its exact PDF comparison.
- [x] Pass 29 focused E-Invoice tests, including five clock cases covering exact bytes, advancing time, UTC conversion, system-clock compatibility, blocked preflight/attachment behavior and null dependencies.
- [x] Review the sole public-surface change and pass the complete Importer/Image API and processing-coverage verification with 363 types, 5,088 members, 201 protected entries and two explicit interface bindings.
- [x] Complete the full Importer source regression and independently reconcile its raw results and cleanup before claiming this follow-up is closed.
- [x] Document the clock contract and add a backward-compatible Minor fragment; retain existing package/platform/commercial gates and unchanged product version, published releases and source trust.

The focused run at
`artifacts/native-engine-tests/1795178358043-be33f51b/` passes all 29 selected
result rows without skips. The five new clock cases parse the actual embedded
file `Params/ModDate`, compare full output bytes and result hashes for equal
inputs, and require different metadata and result hashes after advancing time.
XML bytes, attachment/source hashes, archival strategy and incremental source
prefix remain checked. No sleep or wall-clock coincidence is needed for the
determinism test. The legacy constructor is separately checked against real UTC.

The public-surface capture at
`artifacts/native-test-sessions/fbe7c163-39f2-4cc3-a859-a469f9ee72fd/` identifies
only the additional `PdfEInvoiceAttacher(IPdfA2bConverter, TimeProvider)` overload.
The original constructor and both processing methods remain. Only this type's
reviewed inventory hash changes; no protected-entry classification is relaxed.

The subsequent complete surface/processing run at
`artifacts/native-test-sessions/4926e42e-bc62-4309-ab4c-68667fbd1316/` passes
3,729 unique Importer checks and 252 unique Image/OCR checks. It covers 194
E-Invoice/archive/color checks with the optional native LittleCMS requirement
unset: all six synchronous/asynchronous byte comparisons pass, including the
previously failing CMYK source; three cases assert fixed attachment metadata,
and six cases confirm a newly denied attachment never reads the clock.
All 1,720 snapshotted source inputs stay unchanged during verification, and
temporary builds/licenses are removed. This supersedes the preceding failed
run only for the verified source-processing scope, not protected packages,
cross-platform/native dependency matrices or whole-product release acceptance.

The complete Importer test-project run at
`artifacts/native-engine-tests/1795178721316-f9677cdb/` passes all 677 tests with
no failures or skips, including the five new clock cases. An independent raw TRX
audit reconciles both result-row sets and all counters for the 29-test focused
run and 677-test full run. Their cleanup records confirm removed ephemeral
material, unchanged source trust and unchanged regular licensing build outputs.
This closes the failed E-Invoice follow-up for local macOS/x64 source processing;
the earlier failed run remains a failed historical record, never rewritten as
passing. No pre-existing test assertion or input fixture was weakened.

All 86 tooling/version/release-author tests and 73 Documentation tests pass;
Documentation builds successfully. All 84 local documentation links/anchors,
current naming/Markdown gates, version/fragment validation and `git diff --check`
pass. Existing compiler, Vite and NPOI warnings remain visible and supplier terms
are not accepted. The additional public constructor is a Minor capability,
recorded in `einvoice-attachment-metadata-clock`; no release is prepared.

No new protected-package run, API/database acceptance, visual baseline approval,
Windows/Linux acceptance or remote CI execution is claimed. Continue with the
remaining jointly protected CodeWorker/Converter/provider graphs and their
consumer tests. VERSION, published release history, production trust, release
metadata, live databases and unrelated solution/private IDE changes remain
untouched. No commit, push, server restart, package publication or deployment
occurred.

## Joint Converter And CodeWorker Protection Milestone

- [x] Add an explicit 16-assembly profile while retaining the nine- and fourteen-assembly profiles; validate the full dependency closure and exclude both executables from the 14 library packages.
- [x] Prepare separate OCR/CodeWorker bundles with four/ten matching protected first-party DLLs, unchanged vendor resolution inputs and isolated source-free consumer restore.
- [x] Compare nine Converter formats across 13 direct overloads and 18 interface calls, single/multiple-page output, missing/wrong rights and Converter-only entitlement without general PDF rights.
- [x] Execute original and protected CodeWorkers for Model, Base64, semantic PDF Builder and legacy low-level PDF; retain source maps, fidelity, canonical models, actual mixed-size PDF pixels and stable error/license diagnostics.
- [x] Correct late worker failures to clear unvalidated canonical design, PDF bytes and source maps while retaining diagnostics; reproduce the duplicate-ID failure before the fix and pass the same raw-worker test afterward.
- [x] Complete the full CodeWorkspace conversion test class and Converter source regressions, then independently reconcile raw result rows and cleanup.
- [x] Prepare a separate Linux CI job with report/cleanup-only artifact uploads, document exact comparison scope and add independent internal-only/Patch fragments without preparing a release or claiming remote/platform acceptance.

### Joint Candidate Evidence

The local macOS/x64 run at
`artifacts/native-joint-packages/1795190540843-3ec08eb9/` passes all 359 baseline
and 739 protected-consumer assertions, each with a unique name. All 788 exported
types retain their reviewed public APIs. The 14 libraries match their package
DLL hashes, and the separate bundles retain four OCR and ten CodeWorker
first-party module hashes from the same joint protection output.

The Converter comparison executes 62 single/multiple-page cases across all 31
direct/interface paths. Text and extracted image bytes compare exactly. ZIP
container timestamps are excluded by comparing parts. ODT `meta.xml` is parsed
and reserialized with only its validated generated creation-date value replaced;
the remaining metadata values are covered semantically and other parts remain
byte-hashed. The two generated Spreadsheet workspace IDs retain the prior narrow
normalization. No image, page-geometry or PDF raster threshold is relaxed.

Twelve real CodeWorker requests run on each side. Model/Base64 DTO operations
remain license-free. PDF Builder and low-level PDF requests require current PDF
rights. Canonical models, source maps, fidelity and diagnostics compare without
normalization. All five compared PDF pages pass: two Generator pages, one
regenerated page and two mixed-size worker pages. The semantic Builder returns a
canonical model for trusted host rendering, not a PDF rendered inside the worker.
Missing, wrong-product, expired and revoked licenses produce stable diagnostics
without design/PDF output; file-capability rejection, compilation errors, invalid
Base64 and duplicate IDs remain covered. Real native OCR executes on macOS/x64.

Thirty-eight vendor DLL resolution inputs stay byte-identical; 40 original
dependency packages supply the offline restore feed. The usual licensing-authority
and PDF-writer ILSpy checks pass. An additional independent type-name inspection
of these same candidates confirms `StyleExtensions` and `PdfSnapshotDesignMapper`
are present before protection and renamed afterward. This does not establish
unrecoverable methods or general decompilation prevention.

The first attempt at `1795179251768-78308296` failed on a syntax error in the new
consumer and is not passing evidence. The second at `1795190349029-80d1f0c6`
exposed a real late-validation error: the worker returned an unvalidated model
despite a failure diagnostic. The same raw duplicate-ID request passes after
central failure-result cleanup. Both failed cleanup records confirm removed
private candidates, unchanged source trust and unchanged regular build outputs;
the successful run confirms those same cleanup invariants.

The full `CodeWorkspaceConversionTests` class at
`artifacts/native-engine-tests/1795190809599-9bc7034a/` passes all 52 tests,
including three new Model/Base64/PDF failed-result cases. The complete Converter
test project at `artifacts/native-engine-tests/1795191177915-381a7294/` passes
all ten tests. Neither run has failures or skips. Independent TRX audits
reconcile the result rows, counters, new regressions and cleanup records.
The package run's generated `validation.json` joins those source-test facts
with the 16-assembly candidate counters and unique assertions; broad acceptance
and publication remain explicitly false. The WebApi compiled for this filtered
test class, but the entire API test suite and live tenant/database flows were
not rerun.

All 88 native/version/release-author tooling tests, 73 Documentation tests,
Documentation production build, 85 local documentation links/anchors, current
naming/Markdown guards, version/fragment validation and `git diff --check` pass.
Existing compiler, analyzer, Vite and NPOI warnings remain visible; no supplier
terms are accepted and no test threshold is relaxed.

This milestone excludes Migration provider protection, WebApi/tenant integration,
general OS sandbox hardening, remote CI execution, other native OCR platforms,
full visual/fuzz/platform matrices, support-map retention and commercial package
distribution. The two release fragments are independent: internal package
rehearsal has impact `none`; failed-result cleanup has impact `patch`. VERSION,
published releases, manufacturer trust, native release metadata, live databases
and unrelated solution/private IDE changes remain untouched. No commit, push,
service restart, registry publication or deployment occurred.

## Joint Native Migration Package Milestone

- [x] Add an explicit 54-assembly profile covering all 38 Migration projects, 52 library packages and both separate workers; reject incomplete project or package dependency closures.
- [x] Reuse the full Code/Report/Viewer admission probes against original project outputs and independent protected-package consumers, including all 33 native providers and 24 representative Code fixtures read through Roslyn.
- [x] Preserve public API, provider aliases, diagnostics, checksums and existing narrow Report/Viewer normalization contracts without modifying fixture contents or product dependency versions.
- [x] Retain the complete Generator/Importer/Converter/worker comparisons, separate Roslyn runtime bundles and no-publication/cleanup safeguards while extending the graph.
- [x] Complete the final strict run with the union of all original dependency feeds and per-probe third-party version checks, without the initial dependency substitution warning.
- [x] Independently audit final counters, provider coverage, source fixture checksums, public API, cleanup and documentation before closing this milestone.

### Intermediate Evidence And Tightened Dependency Gate

The first attempt at `artifacts/native-joint-packages/1795191974278-23c99462/`
failed because the fixture reader recognized `source` but the Spreadsheet tests
use `src`. The correction still uses Roslyn syntax/literal APIs and leaves the
source fixtures untouched. Its failed cleanup record confirms removed private
material, unchanged source trust and unchanged regular product build outputs.

The next run at `artifacts/native-joint-packages/1795192412078-4edbb01e/` passed
its then-configured checks: 54 modules, 52 packages, 909 public types, 397
baseline and 1,347 protected main-consumer assertions; each side also passed
2,063 Code, 703 Report and 60 Viewer assertions. All 24 representative Code
fixture source/result hashes matched. An independent ILSpy inspection of those
same candidates confirms renamed private Roslyn admission, Aspose PDF rewriter,
ClosedXML rewriter and RDL report implementation names. This verifies reduced
name readability, not unrecoverable logic or general anti-tamper protection.

That intermediate run also emitted NU1603: its Code consumer restored PKCS
10.0.14 instead of the original smaller project's 10.0.13 because only the
combined graph's dependency package set had populated the local feed. Although
outputs matched and the recorded run succeeded, it is not the final dependency-
parity acceptance. The harness now copies the union of the main and all three
original probe restores and requires every original third-party package version
in the corresponding protected restore. Product package references remain
unchanged; no warning is suppressed. The final strict rerun is recorded below.

The prepared Linux job uploads only report/cleanup JSON, not licenses, source
code, packages, worker bundles or maps. The release fragment has impact `none`:
these are disposable internal candidates, not a shipped SDK or deployment.
Full WebApi/friend-boundary integration, HTTP Nutrient Designer composition,
cross-platform/native dependency matrices, support-map retention, general
fuzz/visual acceptance and commercial distribution remain separate gates.

### Final Strict Migration Evidence

The final local macOS/x64 run at
`artifacts/native-joint-packages/1795193227001-5eb94299/` passes the tightened
dependency-version assertions without the intermediate NU1603 substitution.
Its offline dependency feed contains 45 original package versions. The 39
vendor DLL resolution inputs remain byte-identical; the two separately recorded
Roslyn resolution/standalone-worker variants stay unchanged in their own bundles.
No product dependency version is changed or warning suppressed.

The same 54 modules, 52 independently restored library packages and 909 public
types pass. There are 397 baseline and 1,347 protected main-consumer assertions,
plus 2,063 Code, 703 Report and 60 Viewer assertions on each side: 7,396 total.
Assertion names are unique within each run. All 24 Code fixture source/output
checksums, 37 Report entry outputs and three Viewer outputs match exactly under
the documented existing projections. Full original/protected probe reports
compare equal, not only their case counts. The Generator, regeneration and
CodeWorker PDF raster comparisons and actual macOS/x64 OCR still pass.

The generated `validation.json` independently reconciles provider counts,
representative fixtures, public types, unique assertions, comparisons and
cleanup. Candidate packages, workers, synthetic licenses, private support maps
and caches are removed. Source trust and normal product build outputs remain
unchanged. The original fixture files are read-only. This closes the local-feed
native graph task above; broader preservation/DI, protected-support retention,
platform and application integration tasks remain open.

All 90 native/version/release-author tooling tests and 73 Documentation tests
pass; Documentation builds successfully. Existing compiler, Vite and NPOI
warnings remain visible, and supplier terms are not accepted. No complete
Solution/API/fuzz/visual/platform regression run is claimed in this milestone.
The prepared Linux workflow has not been executed remotely. VERSION, published
release history, native release-date metadata, manufacturer trust, databases
and unrelated solution/private IDE changes remain untouched. No commit, push,
service restart, package publication or deployment occurred.

## Private Support Retention Milestone

- [x] Add an explicit map/portable-symbol allowlist and build manifest containing commit, dirty-state flag, version, tool version, retention deadline and original/protected assembly hashes.
- [x] Seal the private manifest and payload with fresh authenticated encryption using only an approved public recipient key; never generate or register a production recipient implicitly.
- [x] Require an independently recorded archive checksum for recovery; validate payload hashes, retention, names, limits and regular paths before creating an exclusive private restore directory.
- [x] Prove exact recovery using a real isolated DLL, pinned Obfuscar mapping and original portable PDB after deleting the plaintext staging copy; verify tamper rejection, expiry and cleanup.
- [x] Complete the final joint-package regression with its mapping-retention check and unchanged API, package-content and PDF-pixel gates.
- [x] Add operator instructions, synthetic CI preparation and report-only upload contracts; keep customer packages, public symbol feeds and routine artifact uploads free of support material.
- [ ] Provision the reviewed production recipient, independent checksum ledger, protected storage/Windows ACLs, key backups, retention lifecycle and access audit under an approved operational owner before retaining real release artifacts.

### Support Retention Evidence And Boundaries

The dedicated final local run at
`artifacts/native-support-retention/1795194958028-736afc2f/` verifies one real
DLL, joint mapping and original portable PDB. Node runs as ARM64; the actual
.NET host is x64. The mapping and symbols are sealed using an ephemeral
synthetic RSA recipient and recovered byte-for-byte after removing their
plaintext staging copy. Tampering and expired recovery are rejected. Cleanup
confirms removed candidate material, unchanged source trust and unchanged
regular product outputs. No private key, archive, mapping or PDB is uploaded.

The final nine-assembly package regression at
`artifacts/native-joint-packages/1795194888364-264507fb/` also passes: nine
independently restored protected packages, 413 unchanged public types, 30
baseline and 187 protected-consumer assertions, identical PDF pixels and
JSON/Word/Spreadsheet semantics, and byte-identical third-party assemblies.
Its complete joint mapping survives encrypted recovery with matching hashes;
no private PDB is generated in that package profile. Both final directories
contain only report/cleanup JSON after their private work is removed. The final
counters, restore flags and cleanup records were independently reconciled.
The earlier 54-assembly Migration evidence remains valid for that earlier
milestone; the new support-retention integration is not claimed as rerun across
all 54 assemblies or remote operating systems in this milestone.

The tooling provides AES-256-GCM encryption with RSA-OAEP/SHA-256 key wrapping
and an independently required archive hash. Encryption does not authenticate a
producer by itself; trusted ledger/provenance controls remain required. Original
portable symbols describe pre-obfuscation DLLs, not rewritten protected-code
debugger symbols. Input ownership and matching debug-directory identities are
trusted build responsibilities, not certified by the PDB-header check.

All 100 native/version/release-author tooling tests and 73 Documentation tests
pass, and Documentation builds. Synthetic CI preparation spans three operating
systems but has not been executed remotely. The dedicated and joint rehearsals
remove synthetic archives; durable production storage, key ownership, automatic
lifecycle deletion, Windows ACLs and access auditing remain open above. The
operator procedure is recorded in
[Native Library Support Artifacts](../operator-docs/PVAD.Native-Support-Artifacts.md).

The internal-only release fragment has impact `none`. VERSION, published
releases, native release metadata, manufacturer trust, databases, unrelated
solution changes and private IDE files remain untouched. No commit, push,
service restart, public package publication or deployment is performed.

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
