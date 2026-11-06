# PVAD Remaining Code Naming Cleanup

## Scope And Baseline

Baseline: `e51a00aacfb2eb3148f119527ab4e2c1a0ca36cf`.
The tracked-file search found 20,729 textual occurrences in 634 files, 42
matching paths and raw matches in 28 binary files. These are inventory values,
not counts of defects. All 279 tracked Markdown files passed the naming gate.
The ignored detailed inventory is `artifacts/rename/remaining-identities/scan.json`.

Technical output uses PVAD; public product and Designer names use PVADE.
Existing diagnostic IDs, accepted Legal records, cryptographic identifiers,
published releases, historical audits and signed fixtures remain unchanged.
Previous checklist results are point-in-time evidence, not acceptance of this work.

## P0 - Internal Names And Migration Access

- [x] Correct the migration permission fallback to recognize the canonical API prefix after legacy-route normalization.
- [x] Test canonical and historical routes, domain/kind separation, denied access, mutation permissions and CSRF enforcement.
- [x] Rename all five internal step-up policy names without changing their authorization requirements.
- [x] Use the canonical non-root MCP container user, group and ownership consistently.
- [x] Replace freely chosen synthetic license numbers and key prefixes in the Admin screenshot producer.
- [x] Validate API authorization, MCP packaging and screenshot-producer contracts.

## P1 - Canonical Document Writers

- [x] Write the canonical embedded chart metadata key and retain the previous key as an input alias.
- [x] Define and test deterministic handling of PDFs containing both chart metadata generations.
- [x] Write canonical redacted-image markers and resource names; retain old-marker validation for existing PDFs.
- [x] Test pixel clearing, masks, residue checks, repeated redaction and old/new marker interoperability without weakening security checks.
- [x] Canonicalize freely chosen parser-tool variables and temporary paths.
- [x] Migrate newly generated parser reports and corpus descriptions only with explicit readers and unchanged historical evidence.

## P1 - Protocol Writer Transitions

- [x] Introduce canonical mail correlation headers with a bounded compatibility policy.
- [x] Introduce canonical webhook signature, timestamp and idempotency headers with tested receiver compatibility.
- [x] Review remaining response aliases and browser compatibility writers individually; retain only documented transition requirements.
- [x] Preserve existing signature algorithms, payload bytes, key IDs, persistent audit contracts and authorization semantics.

## P2 - Monitoring And Deployment

- [x] Review current monitoring display names separately from dashboard, datasource, alert and folder identities.
- [ ] Apply only an authorized identity cutover with backup, permissions, link, delivery and rollback checks.
- [x] Review current deployment defaults separately from immutable historical image families and protected environments.
- [ ] Keep production and external acceptance gates open until actual operator evidence is available.

## Validation And Delivery

- [x] Replace overly broad exceptions for changed files with exact, reviewed reasons and checksums.
- [x] Add guards preventing new canonical writers from falling back to the previous identity.
- [x] Pass the affected .NET, MCP, naming and website tests and applicable builds.
- [x] Pass strict Markdown naming, structural JSON naming and residual-file guards.
- [x] Preserve original audit/register bytes and existing acceptance records.
- [x] Add accurate Patch release fragments without changing VERSION or published releases.
- [x] Refresh the ignored inventory and report completed, retained and operationally deferred work separately.

### Batch 1 And 2 Verification - 2026-11-05

This evidence applies to the internal-name, migration-access and PDF-writer
changes, not the still-open protocol, report-contract or operational tasks.

- API: 1,296 passed, 82 skipped, no failures. Skipped scenarios are not accepted as verified coverage.
- Core: 153 passed; Importer: 655 passed, none skipped. The three full .NET suites total 2,104 passing tests.
- Node configuration, versioning, release-author and Admin contracts: 388 passed, none skipped.
- MCP: 29 runtime tests and nine toolchain tests passed; typecheck, production build and distribution smoke passed.
- Documentation and Admin production builds passed. The parser-fuzzer project build passed.
- Focused regression tests failed before the route, policy and container corrections, then passed after them.
- Chart tests cover both key generations, identical dual keys, conflicting dual keys and malformed payloads; invalid metadata preserves original graphics.
- Redaction tests retain pixel/mask checks and add old/new marker, forgery and repeated-redaction coverage. Historical PDF bytes are not rewritten by loading.
- Three formerly broad exceptions are resolved records, not active allowlists. Their exact clean-source hashes and reintroduction failures are tested without moving historical JSON pointers.
- Markdown, structural JSON and residual guards pass. Eight protected audit, register, Legal and release files compare byte-for-byte with HEAD.
- The refreshed tracked-only report is `artifacts/rename/remaining-identities/scan-current.json`: 20,711 occurrences in 631 text files, 42 matching paths and 28 binary files with raw matches. New untracked source/checklist/fragment files are checked separately.
- Local MCP dependencies were restored from the unchanged lockfile before testing. Existing compiler, license, JSON-format and bundler warnings remain outside this naming change.
- No Docker image was built or published, no deployment was performed, and no commit, push, version bump or database change occurred.

### Batch 3 - Protocol And Alias Review

The prior two batches were committed as `3c9d996c`, without a push. SMTP already
emitted canonical and matching old correlation headers; this batch validates
single values, stable correlation on message recreation and distinct outbox IDs
without adding a second implementation. It documents the compatibility-major
boundary rather than removing existing integrations.

Webhook output now includes canonical and identical old headers. Both simulated
receiver generations verify the same sanitized bytes, timestamp, HMAC and digest.
A fixed independently calculated vector pins the signature algorithm. The local
rehearsal receiver reads either complete family or identical pairs and rejects
conflicts, duplicates, blank values and partial families. This is not acceptance
of a customer receiver or real mail/alert delivery.

The [HTTP alias review](../docs/pvad-http-compatibility.md#remaining-writer-alias-review)
records response headers, migration DTO/job fields, manifest readers, browser
storage, the single telemetry bridge and runtime/schema aliases individually.
Existing evidence is not backfilled and the old contracts are not newly renamed.

### Batch 3 Verification - 2026-11-05

- Webhook relay: seven tests passed, none skipped. Both receiver-generation cases failed before the canonical-header change and passed afterward.
- Focused API header, middleware and migration contracts: 87 tests passed, none skipped. Native project archive compatibility: 78 tests passed.
- Configuration, versioning, release-author and Admin contracts: 391 tests passed. Shared website, telemetry/storage and focused runtime/rehearsal contracts: 72 tests passed. These runs overlap and are not additive coverage totals.
- Designer: six focused suites with 47 passing tests cover document headers, runtime configuration, tenant state, migration artifacts, document handoff and charts.
- Documentation production build passed. Shared TypeScript-backed website tests used the repository-standard experimental type-stripping flag.
- Markdown naming: 280 tracked files, no old-name matches. Structural JSON naming: 493 scanned files, 70 matching files, no unclassified matches. The residual-file guard and release-fragment/version validation passed.
- Nine protected signing, sanitization, audit, register, Legal, published-release and version files compare byte-for-byte with the preceding commit.
- The fragment has Patch impact; VERSION remains unchanged. Existing compiler and bundler warnings remain outside this naming change.
- No live database, actual SMTP delivery, customer receiver, Docker deployment, push or operational cutover was changed or accepted by these tests.

### Batch 4 - Parser, Corpus And Operational Source Review

Batch 3 was committed as `3803580c`, without a push. New parser reports now write
the canonical parser ID. Their version-2 reader accepts the former ID in memory
without changing source bytes, hashes, decisions, durations, limits or stable
diagnostics. Alias collisions, missing outcomes and inconsistent counts fail.

Current synthetic Trust and PAdES generators use canonical document metadata,
certificate subjects, filenames and classified-profile fields. Generated-manifest
readers accept both generations and reject conflicting aliases. The negative
manifest builder requires one complete evidence generation per case and preserves
the actual file hashes. Signed originals and approved independent reports are not
replaced or relabeled. Existing encrypted-fixture passwords remain unchanged.

Monitoring display titles and tags were reviewed separately from persistent
UIDs, metrics, provisioning and folder ACLs. Two remaining display tags now use
PVAD. Exact pointer-level transitions live in the current review ledger; the
original JSON audit and its hash remain unchanged. Transitions cannot exempt
protected evidence, unrelated findings or a different current value.

Deployment validation defaults to the canonical image family, retains explicit
historical-family selection and rejects unknown choices. Candidate generation
accepts the older choice contract and writes the current contract idempotently.
Protected environments, publishers, locks, reviewers, permissions, secrets and
historical images are unchanged. No remote workflow is triggered.

### Batch 4 Verification - 2026-11-05

- All three changed .NET tool projects built successfully. Restricted build-server launches were stopped and replaced with completed builds using disabled build servers; existing dependency/compiler warnings were not waived.
- Real deterministic fuzz run: 30 cases, three installed parsers, no crashes and no timeouts. Eight parser disagreements remain explicit review evidence; qpdf and MuPDF were not installed for this run. Schema, seed and limits are unchanged.
- Real isolated generation produced seven Trust fixtures plus three positive and six negative PAdES fixtures under ignored artifacts. Manifest hashes/file lengths and the canonical synthetic root subject were checked. This is not fresh independent DSS certification.
- Importer suite: 655 passed, none skipped. The seven historical corpus tests also passed separately, including signed, encrypted and independent evidence readers.
- Node configuration, versioning, release-author and Admin contracts: 409 passed, none skipped. New cases cover both report generations, alias collisions, unchanged input bytes, complete evidence selection, canonical defaults, stubbed image selection and exact display-tag transitions.
- Documentation production build passed. Shell syntax passed for the changed launchers; external LibreOffice, qpdf, pyHanko and DSS generation scripts were not executed against the approved corpus.
- 102 original corpus, audit, register, Legal, release and VERSION files compare byte-for-byte with the preceding commit. No original manifest or accepted evidence was amended.
- Markdown naming: 280 tracked files with zero matches. Structural JSON: 496 scanned files, 70 matching files, zero unclassified findings. Residual guard, version/fragment validation and diff whitespace checks passed.
- The refreshed ignored inventory is `artifacts/rename/remaining-identities/scan-current.json`: 20,687 textual occurrences in 633 files, 42 matching paths and 28 matching binary files. New explicit compatibility tests affect these counts; they do not measure unfinished work.
- Two Patch fragments document corpus output and operational source defaults. VERSION, published release notes, databases and deployments are unchanged. This batch is not yet committed or pushed.

### Batch 5 - Scoped Source Acceptance

Batch 4 was committed as `6d37dd51`, without a push. The independent source
review found two remaining old MCP resource titles and a compatibility-resource
description exposing the previous schema path as display text. Titles and
descriptions now use current names; old resource URIs and original schema
payloads remain exact. Real MCP-client tests compare the returned payloads and
packaged stdio smoke verifies both generations.

The [source acceptance matrix](../docs/pvad-rename-source-acceptance.md) records
nine writer/reader boundaries and seven specific retained-finding categories.
It distinguishes canonical output, input compatibility, bounded output aliases,
persisted/cryptographic contracts, operational bindings and original evidence.
The new direct writer guards check eight boundaries without consulting the
exception ledger; deliberately reverted source fails every guard. Hash updates
alone therefore cannot satisfy source acceptance.

### Batch 5 Verification - 2026-11-05

- API: 1,296 passed, 82 skipped. Core: 153 passed; Importer: 655 passed; webhook relay: seven passed. Skipped API scenarios remain unverified and do not count as deployment or external acceptance.
- Node configuration, versioning, release-author and Admin contracts: 418 passed, none skipped. This includes eight direct writer checks and their deliberate-reversion test.
- Shared website storage, telemetry, status and navigation tests: 37 passed. Designer: six suites with 47 passing tests; typecheck and production build passed.
- MCP: 30 runtime tests and nine toolchain tests passed, none skipped. Typecheck, production build and packaged stdio smoke passed. The initial restricted IPC launch was rerun successfully outside the socket restriction; no network-facing deployment was started.
- Documentation production build passed. Existing schema-format, module-format and bundler-size warnings are not waived or treated as new certification.
- 104 original corpus, schema, audit, register, Legal, published-release and VERSION files remain byte-identical to the preceding commit. Five links in the new acceptance matrix resolve.
- Historical audit-pinned review notes remain exact; current review explanations are separate additive fields where required. No original audit pointer, value or approval is rewritten.
- Final guards pass: 281 tracked Markdown files have zero matches; structural JSON scans 497 files with 70 matching files and zero unclassified findings. The residual guard and diff whitespace check pass.
- The refreshed ignored scan records 20,706 occurrences in 634 text files, 42 matching paths and 28 matching binary files. Deliberate compatibility and reverted-writer fixtures increase raw search counts without introducing new canonical-output defects.
- A customer-facing Patch fragment covers MCP display metadata. VERSION remains 1.1.0, the published manifest is unchanged, and no new commit, push, merge, tag, publication or operational cutover is performed for this batch.

### Post-Acceptance Runtime Revalidation - 2026-11-05

Batch 5 was committed as `09d68f22`, without a push. The offline acceptance
review detected five changed monitoring source/helper pins and classified the
old integration report as historical. The old report and 12 screenshots were
archived byte-for-byte, with all 13 original hashes verified. No historical
source pin or acceptance result was rewritten.

The actual isolated monitoring/browser test then passed all 12 scenarios on
the tracked-clean source commit. The new report pins 42 integration sources,
eight harness sources and ten image IDs, and records 12 desktop/mobile screenshots.
Actual metric/log/trace correlation, query privacy, signed HTTPS retry/recovery,
Grafana permissions/identity, corrupt-backup rejection and point-in-time restore
all passed. Three notification attempts had no invalid deliveries, and the
baseline/restored snapshot hashes match. Owned-resource queries confirm complete
container/network/volume cleanup, with no temporary workspace remaining.

Both local evidence reports now match their pinned sources. The unchanged HTTPS
report was integrity-checked, not rerun. The
[runtime guide](../docs/pvad-integrated-monitoring-rehearsal.md#post-closeout-revalidation-2026-11-05)
and [final acceptance](../docs/pvad-rename-final-acceptance.md#post-closeout-runtime-revalidation-2026-11-05)
record the new report hashes and scope. This scoped checklist remains **29/31
complete (93.5%)**, with two operator/external tasks open. Primary acceptance
remains **90/98 (91.8%)**, with eight gates open; the local run grants no deployment
or publication approval. VERSION and original audit, corpus and Legal records
remain unchanged.

Post-run validation passes 425 configuration/versioning/Admin contracts, 60
Documentation tests and the Documentation production build. The 28 focused
rehearsal/acceptance cases are part of the contract total, not additional tests.
All 132 public build files were checked for five internal guide names, with no
leaks. Markdown, structural JSON, residual-ledger and release-fragment checks
pass. No full .NET or Designer regression is newly claimed for this internal
documentation-only update, and no installed system was changed.

### Committed Integration Checkpoint - 2026-11-05

The preceding monitoring-evidence update was committed as `2705019f`, without
a push. On that tracked-clean commit, all 22 local build/test/preview steps
passed. The Solution builds with zero errors and 115 warnings. Designer passes
typecheck, all 812 tests in 92 suites and production build. All five websites
pass their test/build commands, and Account typecheck passes. MCP passes typecheck,
30 runtime tests, nine toolchain tests, build and packaged distribution smoke.
The separate local CI-contract selection passes 416 tests across 58 files.

All six frontend build identities match version 1.1.0 and the committed source.
An actual temporary-copy release preparation validates the Major 2.0.0 proposal
from 209 fragments; 57 internal entries stay out of the 152 public changes.
Confirmation, invalid-PR, version-reuse and empty-repeat checks pass. The 239
original metadata hashes, checkout versions, published manifest, HEAD and tags
remain unchanged; the temporary copy is removed. The freshly built Documentation
also passes 45 static reference and three live focus-route checks.

The [integration checkpoint](../docs/pvad-rename-release-dry-run.md#committed-integration-preflight-2026-11-05)
records ignored report locations and exact limits. This is not a fresh full
.NET/database runtime, full Designer E2E, DocFX, native OS or remote PR CI run.
No operational checkbox changes: scoped acceptance stays **29/31 (93.5%)** and
primary acceptance **90/98 (91.8%)**. Subsequent documentation edits are not part
of that clean-commit execution.

### Remaining Operational Work

Only the two unchecked operational tasks below the P2 heading remain in this
scoped checklist: authorize and verify a real monitoring cutover, then obtain
the actual production/external acceptance evidence. The primary rename plan's
eight release gates and 90/98 historical total remain unchanged by this local
source acceptance. Local tests do not authorize either operational step.

On 2026-11-05 the owner confirmed that NuGet/npm publishers are not yet set up,
no non-local staging environment exists and no independent reviewer is available.
Source-only PR 214 now targets protected develop, but neither opening the PR nor
the owner's local-development responsibility closes those prerequisites. The
[current decision register](../docs/pvad-rename-release-approvals.md#owner-decisions-and-integration-status-2026-11-05)
records the answers and their limits. All original unchecked tasks remain open;
95% primary acceptance requires four actual gate closures, not more local repeats.

Source PR 214 passed all seven required GitHub Actions checks on exact head
`a4e9c1480d17dfd86603fae4df305864571b73ff` in run `42123810665`. The PR-only
snapshot skip is expected. The mandatory review remains missing, so no normal
protected merge has occurred. This does not grant production approval or change
either acceptance percentage. The current owner/CI notes are a local follow-up,
not part of that tested PR head until separately committed and pushed.

### Mechanical Test-Source Cleanup - 2026-11-06

A fresh source review found freely chosen test setup values that did not need
to retain the previous identity. An explicit mechanical replacement batch
updates 143 occurrences in 31 existing test files; source producers and their
assertions change together. Fifteen files now have no retained legacy identity
under the residual review, rather than receiving broader exceptions.

- Eighteen Account, Admin and Designer factories use canonical database configuration, CSRF/application/API-key request headers and synthetic Account origins.
- Current explanatory comments reference the actual renamed permission, role, entitlement, database and expression-evaluator types.
- Synthetic license numbers, barcode values, OCR output, PDF custom keys, encrypted-text probes and newly generated recipient subjects use current names.
- Synthetic mail-renderer links and PostgreSQL model/job users use reserved current-name test domains; fake connection details and the ordinary authentication fixture slug are canonical.
- The new synthetic license uses the canonical download MIME type and filename. Dedicated retained-license tests still exercise both original and current artifact generations.
- Dedicated configuration-alias, cookie/CSRF, API-key and diagnostic compatibility tests are not rewritten. Original signed documents, historical inventories, Legal records, published notes, VERSION and live databases remain unchanged.
- Three direct source contracts guard current factory inputs, mail/database examples and synthetic data independently of the reviewed-hash ledger. Original ledger review notes remain historical; new review explanations are additive.
- The exact replacement list and before/after hashes are recorded in the ignored `artifacts/rename/search-replace-20261106/replacements.json` report.

This is internal test-source cleanup, not a new application feature, a
deployment acceptance or closure of an operational gate. Its release fragment
uses `none` impact. Earlier execution totals remain point-in-time results.

#### Verification

- Configuration/versioning contracts: 399 passing tests. Residual-ledger, structural JSON, release-fragment, version synchronization and diff whitespace checks pass.
- The first real PostgreSQL API selection passed 162 of 163 tests and exposed one stale license MIME assertion. That original failed TRX remains available; it is not relabeled as a successful run.
- After correcting the current-license MIME assertion and adding its filename assertion, all 21 follow-up API tests pass, including old/new license downloads and the final mail/auth/job fixture changes.
- Focused Importer tests: 59 passed; expression tests: 15 passed; OCR tests: two passed; model/real PostgreSQL persistence tests: 13 passed. Across the .NET selections, all 266 distinct test names have a passing latest result, with none skipped.
- All 281 tracked Markdown files have zero old-name occurrences. Ninety-one protected originals and dedicated compatibility sources compare byte-for-byte with HEAD; all 815 original ledger review notes and their ordering are preserved.
- No isolated Testcontainers resources remain running after the tests. No live application, database, deployment, publisher, version or published release was changed. Existing compiler/license warnings are not waived by these scoped checks.

### Grafana Repository Defaults - 2026-11-06

This source-only batch closes a concrete default-provider naming finding. New
local Compose installations select
`deploy/observability/grafana/profiles/current/dashboards.yml`, with PVAD as both
provider and folder. The original provider and all shared datasource/dashboard
files remain unchanged for installed services.

- `deploy/rename/docker-compose.grafana-legacy.yml` explicitly selects only the original provider file. Actual resolved-model comparison proves no other setting changes. The complete legacy overlay also selects the retained provider while preserving its existing external resource bindings.
- Provider preparation defaults to the current profile and is idempotent, with no migration changes or blockers. `--legacy` explicitly prepares the compatibility transition. Both provider profiles are validated, source-pinned and written to separate ignored output directories; neither command can apply a change.
- Standalone operator-browser preparation uses the current provider. The integrated monitoring transition lab explicitly seeds the legacy provider in temporary configuration so its cutover remains a real transition, not a no-op.
- Profile rehearsal reports use `artifacts/rename/grafana-profiles-rehearsal/report.json`. The CLI creates the new report directory before its first write; a regression contract prevents accidental dependence on an existing output directory. Older reports and their original source pins are not overwritten or relabeled.
- The provider guide, observability startup instructions, compatibility-overlay guide and source/final-acceptance boundaries document the two choices. Existing live cutover procedures remain deferred, not requirements for repository-only completion.
- A Patch fragment records the fresh-installation default correction. VERSION, the published release manifest, original Markdown/JSON inventories and Legal compatibility registers are unchanged. No publication, version preparation, deployment, commit or push is performed.

#### Verification

- All configuration and versioning contracts pass: **403 tests, zero failures or skips**. The ignored log is `artifacts/rename/grafana-current-default/configuration-versioning-tests.txt`.
- Documentation passes **60 tests** and its production build. Existing Vite warnings about a future native configuration loader remain; no public UI or operator-navigation change is introduced.
- The actual isolated rehearsal passes with cached `grafana/grafana:13.1.0`: five dashboards, three datasources, retained legacy folder UID and permissions, denied unauthorized reads, collision rejection, corrupt-backup rejection and matching point-in-time restore. A separate fresh volume provisions exactly one PVAD folder with all five dashboards and unchanged datasource UIDs, and rejects invalid authentication.
- The real test uses network-isolated synthetic containers and no published ports or installed volumes. Owned test containers and volumes are removed. The existing local Grafana container remains running with its original read-only provisioning bind mount; it is not restarted.
- The nine original provisioning/dashboard files compare byte-for-byte with baseline commit `b3812027`. All 815 original ledger review notes and their order are preserved; current hash reviews and one precise compatibility-test entry are additive.
- Structural JSON and residual naming guards pass. Existing checklist states and historical result counts remain unchanged. The new result does not recertify the earlier combined HTTP/browser, telemetry, alert-delivery or production scenarios whose changed source pins are now historical.

### Large Mechanical Search-And-Replace Batch - 2026-11-06

The owner requested direct search and replacement in larger reviewed areas.
The fresh tracked-file inventory is
`artifacts/rename/search-replace-current/scan.json`. It records the working tree
based on `b3812027`, including the uncommitted Grafana batch, rather than claiming
a new clean-commit audit. Its 643 matching text files and 20,761 occurrences
include historical evidence and compatibility values, not that many open defects.

One explicit literal-replacement batch updates **120 matches in 50 files**:

| Reviewed area | Files | Replacements |
| --- | ---: | ---: |
| Designer test examples, metadata, labels and E2E inputs | 15 | 25 |
| Account synthetic users and test descriptions | 4 | 6 |
| Backend document, import/export, signing and archive test inputs | 23 | 73 |
| PDF, Spreadsheet and Report migration test examples and comments | 8 | 16 |

The exact replacement list, occurrence counts and before/after hashes are stored
in `artifacts/rename/search-replace-current/replacements.json`. Twenty-five files
now have no old product-name tokens. Twenty-four ledger entries also become
resolved; neutral drawing terminology accounts for the different classification.
All original review notes remain intact, with current review explanations added.

Only freely chosen current test inputs, matching assertions, explanatory comments
and newly allocated temporary names change. The historical release title remains
an explicit preservation test; current synthetic Legal notifications use PVADE.
Original PDFs, signature subjects, independent reports, corpus schema values,
project format aliases, diagnostic IDs, API routes, XML compatibility envelopes
and existing storage keys are not renamed. Direct source contracts independently
check current examples and the retained reader/history boundaries.

The internal-only release fragment uses `none` impact. No runtime behavior,
version, published release, database, live service, deployment or Git history is
changed by this mechanical batch. Earlier checklist states and results remain
point-in-time evidence, not a new acceptance or publication approval.

#### Verification

- Designer: all 812 tests in 92 suites, typecheck and production build pass. Existing module/chunk warnings are unchanged.
- Account: all 53 tests and typecheck pass.
- Configuration/versioning: all 405 tests pass, with no failures or skips. Residual, structural JSON, version synchronization and fragment validation pass.
- Playwright discovers all 24 selected cases in the four changed E2E files. This is syntax/discovery validation, not a new browser execution.
- 143 protected corpus, project-fixture, schema-alias, audit, Legal/register, published-release and VERSION files compare byte-for-byte with HEAD.
- The initial restricted .NET attempt could not open its test-runner socket and was stopped. Its logs remain under `artifacts/rename/search-replace-current/dotnet/`; it is not counted as passing evidence. The permitted local rerun writes separate logs and TRX files under `dotnet-local/`.
- Final focused .NET validation passes **590 tests across 16 projects**, with zero failures or skips in the selected cases. The summary is `artifacts/rename/search-replace-current/dotnet-validation.json`, generated from TRX counters through an XML parser. It is not a full Solution test run.
- The first broad Export selection exceeded the parent process timeout after its long dependency build. Its zero exit status does not count as a completed pass; a later broad retry was explicitly stopped. The final `dotnet-export-focused/export.trx` contains 25 passing export/spreadsheet tests. The additional 62-page real-world corpus case is excluded from accepted evidence; only its diagnostic-message edit is build-checked.
- Final naming checks retain zero old-name matches in all 281 tracked Markdown files, exact original review notes and unchanged checklist states. `git diff --check` passes. No commit or push is performed.

### API And Tooling Mechanical Batch - 2026-11-06

Baseline: `1c865ee9`. A new tracked-only scan is saved as
`artifacts/rename/search-replace-next/scan.json`; it does not include private IDE
files, ignored outputs or untracked notes. The explicit replacement list and
before/after hashes are in `artifacts/rename/search-replace-next/replacements.json`.

This batch removes **70 old-name occurrences in 29 files**:

| Reviewed area | Files | Removed occurrences |
| --- | ---: | ---: |
| API/security examples, telemetry test values and explanatory text | 21 | 53 |
| Core private version state and a synthetic archive header | 2 | 6 |
| Generated PDF recovery and timestamp test inputs | 3 | 8 |
| Internal tool messages and the synthetic incorrect-password probe | 3 | 3 |

Nine files now have no old product-name tokens. The private version field becomes
`legacyMinimumVersion`; its public compatibility property and serialized reader
key remain unchanged. The independent Core source guard is tightened to accept
only the quoted serialized key, not an arbitrary private identifier with that
name. No JSON schema, diagnostic, API route or public DTO alias changes.

Existing encrypted-fixture passwords, original certificates, signed PDFs,
customer-text preservation tests, audit/schema contracts and immutable corpus
hashes remain unchanged. Synthetic certificates and documents generated inside
the tests use current names. New metadata-review notes are additive; original
review notes and checkbox states remain intact.

No cleanup `--apply`, job-binary cutover, corpus regeneration, publication,
deployment, version bump, commit or push is performed. The internal-only release
fragment uses `none` impact. Earlier operational evidence retains its original
source pins; message-only tool edits are not a new operational acceptance run.

#### Verification

- Core builds with zero warnings or errors. Focused test builds reuse unchanged project references and worker packages; the changed Core binary is checked separately.
- All 407 configuration/versioning contracts pass, with zero failures or skips. Residual, structural JSON, version synchronization and release-fragment validation pass.
- Four real local Poppler checks accept the existing valid fixture password and reject the renamed synthetic incorrect password. The encrypted PDF remains byte-identical. This does not claim a fresh qpdf, Docker or LibreOffice interoperability run.
- All three changed internal tool scripts pass syntax checking. Their destructive or operational commands are not executed.
- Focused .NET validation passes **378 tests**: Core archive contracts 78, Importer recovery/revision/timestamp tests 16 and API/security tests 284, with zero failures or skips. Both PostgreSQL and external PDF-recovery opt-ins were enabled. The TRX-derived summary is `artifacts/rename/search-replace-next/dotnet-validation.json`.
- All three test output directories contain the exact newly built Core DLL, verified by SHA-256. The visual-artifact test's comment/message edits are build-checked only; approved image baselines are not regenerated. No full Solution, Designer or website build is claimed for this source-scoped batch.
- 143 protected fixture, corpus, schema-alias, audit, Legal/register, published-release and VERSION files compare byte-for-byte with HEAD. All 816 original review notes, their order and original checkbox states remain intact. All 281 tracked Markdown files have zero old-name matches, and `git diff --check` passes.

### Recovery And Current Example Naming - 2026-11-06

Baseline: `a58736ae`. The tracked-only inventory is
`artifacts/rename/search-replace-recovery/scan.json`: 586 matching text files and
20,419 occurrences. It excludes private IDE state, ignored outputs, untracked
notes and binary files. Counts include protected compatibility data and original
evidence; they are not an unfinished-work total.

An explicit mechanical pass removes **49 old-name occurrences in ten files**
using 43 reviewed literal replacements. The exact list, counts and before/after
hashes are in `artifacts/rename/search-replace-recovery/replacements.json`.
It covers temporary worker/fidelity/Legal paths, synthetic rich text, migration
test locals, fake container labels, test-author identity and explanatory comments.
The dedicated customer-text and historical-release preservation tests remain
unchanged, as do old element markers, diagnostic IDs and serialized response keys.

New recovery instructions use `RESTORE PVAD DATABASE`. The original exact phrase
remains an input alias; both environment-key generations remain conflict-checked.
Missing, partial, whitespace-modified or unknown confirmations cannot execute
database commands. An approved phrase never bypasses checksum, archive-catalog
or empty-target checks and never chooses the target connection.

The isolated drill creates its own PVAD database and role in fresh containers;
it does not rename or inspect an installed application database. The protected
runbook now contains an executable canonical example rather than a documentary
legacy placeholder. Original operator registers and accepted Legal records stay
byte-identical. A Patch fragment records the compatible recovery-command update;
VERSION and the published release manifest are unchanged.

#### Verification

- Recovery, Legal snapshot and fidelity contracts pass **46 tests**, with zero failures or skips. The initial new fake-tool test had a template-literal syntax error; it was corrected before the passing rerun and is not counted as successful evidence.
- Real isolated PostgreSQL recovery succeeds with normal and delayed startup. Each run validates backup/checksum, rejected missing confirmation, corrupt-backup rejection, successful restore, Legal relationships and rejected non-empty-target restore. A separate short readiness limit fails as expected and reports the current database name.
- All owned recovery containers and networks are removed after the three real scenarios. No installed database, live application, mounted secret or customer document is changed.
- Documentation passes all **60 tests** and its production build. Existing configuration-loader warnings remain outside this naming change; no restricted guide is added to public navigation.
- The combined configuration, versioning, Legal and fidelity suite passes **464 tests**, with zero failures or skips. Its recovery/fidelity selection above is a subset, not additional coverage. Residual and structural JSON guards, version synchronization and fragment validation pass.
- Focused .NET validation passes **203 tests**: API converter/migration/invoice contracts 108 and Importer core contracts 95, with zero failures or skips. The TRX-derived counters are in `artifacts/rename/search-replace-recovery/final-checks.json`. Existing compiler warnings are not waived; no full Solution or Designer regression is newly claimed.
- All 85 checked original fixture, corpus, Legal, schema-alias, audit/register, published-release and VERSION files are byte-identical to HEAD. All 816 original review notes and their order are preserved. All 281 tracked Markdown files have zero old-name matches; original checklist states are unchanged.
- All 132 Documentation build files are checked for the restricted recovery guide name and canonical restore phrase, with no leaks. The updated runbook remains restricted. The changes are not committed, pushed, published or deployed.

## Acceptance

- [x] All reviewed current-output defects are fixed and covered by regression tests.
- [x] Legacy readers remain functional; compatibility is not removed to reduce search counts.
- [x] Every retained category has a specific reason, not blanket approval of a whole file.
- [x] No source completion claim depends only on a passing exception ledger.

## Related Plans

- [Repository naming follow-up](PVAD.Repository-Naming-Followup.md)
- [Rename plan](PVAD.Rename-Plan.md)
- [Current output naming](../docs/pvad-current-output-naming.md)
- [Scoped source acceptance](../docs/pvad-rename-source-acceptance.md)
- [Compatibility boundaries](../docs/pvad-rename-exceptions.md)
