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

### Current Routes And Project Examples - 2026-11-06

Baseline: `a5dadf24`. The fresh tracked-only inventory is
`artifacts/rename/search-replace-routes/scan.json`: 584 matching text files and
20,388 occurrences of the former abbreviation. It excludes private IDE state,
symlinks, ignored output, untracked notes and binary files. These values include
intentional compatibility and historical data, not that many remaining defects.

One explicit Search and Replace pass removes **58 occurrences in 14 files**.
The literal replacement list and before/after hashes are stored in
`artifacts/rename/search-replace-routes/replacements.json`.

| Reviewed area | Change |
| --- | --- |
| Generic API workflow requests | Current document, template, Spreadsheet, export, Viewer, subscription and migration tests call the canonical prefix. Several formerly mismatched current-route test names now match their actual requests. |
| Designer project examples | Current PDF and Spreadsheet examples use PVADP/PVADS filenames and matching manifest format IDs. The explicit old-format in-memory-upgrade case is preserved. |
| Synthetic job, Viewer and relay inputs | New temporary paths, fake document names, a synthetic metrics license number and current operational labels use PVAD. |
| New PDF/UA validation artifacts | The generator writes four current-named structure, custom-role, table and widget PDFs. Original corpus and approved visual references are not regenerated. |
| Arabic status-page setup | The browser test seeds the canonical locale key. Its separate retained recovery-storage scenario remains unchanged. |

Dedicated old/new route integration, authorization-path, CORS, header, project
reader and diagnostic tests remain separate from generic current workflows.
Stable serialized migration fields, original example content used specifically
for preservation, runbook/dashboard IDs, cookie aliases and cryptographic values
are not renamed. No application route, database migration or runtime capability
is added or removed. A direct source guard prevents generic workflows from
silently returning to the former prefix.

The release fragment uses `none` impact because this batch changes tests,
generated test artifacts and internal verification only. VERSION, published
release history, accepted Legal records, installed applications and databases
remain unchanged. No commit, push, publication or deployment is performed.

#### Verification

- All **812 Designer tests in 92 suites** pass, including the updated project examples and retained old-format upgrade case. Existing snapshot assertions are unchanged.
- Designer typecheck and production build pass. Existing dynamic-import and bundle-size warnings remain unchanged. The targeted Chromium Arabic status-page case passes with canonical locale storage; authentication/features are mocked, and unavailable ancillary notification endpoints are not real API acceptance evidence.
- All **409 configuration/versioning tests** pass with zero failures or skips. Residual, structural JSON, version synchronization and release-fragment validation pass.
- Focused .NET validation passes **195 tests**: API current/compatibility/authorization workflows 172, PDF/UA generation 16 and webhook relay seven, with zero failures or skips. PostgreSQL opt-in is enabled for the isolated job-storage test. Unchanged project references are reused; no full Solution regression is claimed.
- The generator creates exactly four current-named PDF/UA files in the ignored artifact directory. Each has a valid PDF header and is readable as a one-page PDF by Poppler. This is not a fresh independent PDF/UA certification or approval of a visual baseline.
- All 179 checked corpus, fixture, Legal, schema-alias, audit/register, public-download, release/version and dedicated compatibility files are byte-identical to HEAD. Seven dedicated compatibility files remain exact. All 816 original review notes and their order are preserved; all 281 tracked Markdown files remain free of the old name, and historical checkbox states are unchanged.
- Exact TRX counters, new PDF hashes and preservation checks are recorded in `artifacts/rename/search-replace-routes/final-checks.json`. Existing compiler warnings are not waived. `git diff --check` passes; no commit, push or runtime deployment is performed.

### MCP Current Test Configuration - 2026-11-06

Baseline: `629013e5`. The owner approved a scoped pass through four MCP test
files: remote configuration, HTTP application, JWT verification and API client.
The explicit replacement record is
`artifacts/rename/search-replace-mcp/replacements.json`, including the baseline,
exact literals, counts, mechanical-stage hashes and final reviewed source hashes.

The mechanical stage makes **30 literal replacements**. General configuration
uses current environment keys and synthetic hostnames; HTTP/JWT test defaults
use the current tenant claim; general JWT and delegation inputs use registered
current scopes. The existing alias/conflict blocks in configuration and JWT
tests remain unchanged. Stable diagnostics, resource URIs, schema readers,
CLI aliases and registration lists are not renamed.

Three explicit compatibility/preservation cases accompany the replacement:
both HTTP read-grant generations return a canonical access context; an old
template grant delegates only its registered canonical scope; an unregistered
scope is preserved without becoming a current render, validate or read grant.
The general JWT example now uses the existing document-validation capability,
not an unregistered render capability. No new scope or alias is invented.
These deliberate cases retain four old-name occurrences, so the four MCP files
have **26 fewer occurrences overall**, not a zero-match promise.

A direct naming contract checks the current setups independently of the review
ledger while requiring their explicit compatibility cases. The release fragment
uses `none` impact because only tests and internal verification change. Product
VERSION, production MCP behavior and packaged compatibility resources remain
unchanged. No commit, push, publication, host restart or deployment is performed.

#### Verification

- MCP runtime tests pass **33/33** and toolchain contracts pass **9/9**, with no failures or skips. Real local Streamable HTTP clients exercise both read-grant generations; JWT keys are generated synthetically and verified locally.
- MCP typecheck, production build and packaged stdio smoke pass. The smoke lists and reads both resource generations and resolves packaged schema aliases without requiring a live API or remote identity service.
- All **410 configuration/versioning tests** pass with zero failures or skips. Residual, structural JSON, version synchronization and release-fragment validation pass. Existing schema-format warnings in the MCP smoke remain outside this naming change.
- All 32 other tracked MCP files, including production sources, package/lock metadata, resource/CLI contracts and remaining tests, compare byte-for-byte with HEAD. Across the broader protection set, **204 original files** remain unchanged. The two original configuration/JWT alias-conflict test bodies match their baseline hashes; the exact pins are in `artifacts/rename/search-replace-mcp/final-checks.json`.
- All 816 original review notes and their order are preserved. All 281 tracked Markdown files remain free of the old name, and existing checklist states are unchanged. `git diff --check` passes. No new .NET, Designer, website or deployed-identity regression run is claimed for this MCP-test-only batch.

### Project Archive Current Examples - 2026-11-06

Baseline: `32ffddd9`. Seven literal replacements normalize the generic atomic
PDF/Spreadsheet filenames and their temporary-file assertions, the editable
greeting and the general workbook schema hint. Two explicit roundtrip cases
verify that current and former schema hints are both preserved exactly. This
leaves six fewer old-name occurrences in the archive test source; the retained
hint is compatibility input, not the default for new test documents.

- [x] Use current project extensions, sample text and schema in generic archive tests.
- [x] Preserve explicit old-format fixtures, envelope/version aliases and diagnostic expectations.
- [x] Verify current and former schema hints with complete-workbook roundtrips.
- [x] Guard current test inputs independently of the reviewed-hash ledger.
- [x] Pass the 80 archive tests and all 411 configuration/versioning tests, with no failures or skips.
- [x] Keep production sources, original evidence, product version and published releases unchanged.

The first restricted VSTest launch could not open its local communication
socket. The compiled tests then passed outside that socket restriction. No
whole-Solution, Designer, website or deployed-runtime acceptance is claimed.

The exact seven replacements, baseline/current hashes and preservation checks
are recorded in `artifacts/rename/search-replace-project-archive/review.json`.
All 91 ledger-classified original-evidence files compare byte-for-byte with the
baseline, and all 816 original review notes retain their wording and order.
The release fragment uses `none` impact: only tests and internal documentation
change. No commit, push, publication or service restart is part of this batch.

### Smoke And Browser Current Inputs - 2026-11-06

Baseline: `eba91b97`. General observability checks now call five canonical API
routes and look for the canonical recovery phrase. Existing datasource and
dashboard UIDs, authentication headers, service bindings and compatibility
readers remain unchanged. Shell syntax and source contracts are verified; this
is not a fresh full-stack observability acceptance or an installed-state cutover.

The two live authenticated browser opt-ins use the existing environment reader
with current keys. Former keys remain accepted, matching dual values work and
conflicting values fail rather than selecting an arbitrary generation.

The Company/Account test now supplies a synthetic already-reviewed legal
profile, acknowledges necessary storage through the UI and verifies that the
one-navigation sign-in hint is not persisted under either storage key. Reload
returns to the anonymous presentation. No persistent login marker is reintroduced.

The general footer test uses the current storage cookie. A separate browser
test preserves the former acknowledgement, verifies canonical-cookie migration
and confirms that settings can still be reopened.

- [x] Use canonical smoke API routes and recovery instructions without changing infrastructure bindings.
- [x] Normalize live browser opt-ins while preserving explicit compatibility and conflict checks.
- [x] Match Company sign-in expectations to the existing navigation-only behavior.
- [x] Separate current-cookie footer coverage from legacy-cookie migration coverage.
- [x] Pass all eight targeted Chromium desktop/mobile cases and the 12 shared signal/cookie tests.
- [x] Pass Designer typecheck/build, all 413 configuration/versioning tests and naming guards.
- [x] Keep application behavior, original evidence, product version and published releases unchanged.

The initial browser run exposed missing legal-profile test setup and a mobile
storage-notice obstruction. Both test fixtures were corrected; the final run
passes all eight cases without retry. Account authentication/profile responses
are mocked: this is not a new live registration or PDF Trust acceptance run.
The actual live opt-in workflows remain opt-in and are not claimed as executed.
Both changed live-workflow files load successfully in Playwright test discovery.
The final browser report, Node test log and preservation summary are stored in
`artifacts/rename/search-replace-smoke/`. All 91 ledger-classified evidence files
and all 816 original review notes remain unchanged; original task states are retained.
Existing build/configuration warnings are not waived. The internal release
fragment has `none` impact. No commit, push or deployment is part of this batch.

### Project API, Recovery And Telemetry Examples - 2026-11-06

Baseline: `eba91b97`, with the preceding smoke/browser batch still uncommitted.
Twenty-two generic project API filename occurrences now use current PDF and
Spreadsheet extensions. Original reference fixtures, stable diagnostic values,
audit action names and historical audit JSON remain unchanged. Four explicit
API cases verify that canonical packages remain readable with either extension
generation, separately from the general current-workflow tests.

General telemetry inputs use the current session-cookie identity and database
dependency name; the metric test also checks the emitted dependency tag. The
separate legacy formatter and datasource assertions remain exact. Recovery
browser coverage distinguishes current and legacy pending state, validates both
success/failure outcomes and consumes both storage keys without recording route
details. No application, database, audit or monitoring identity is migrated.

- [x] Normalize current project API filenames without changing original fixtures or audit contracts.
- [x] Keep current/legacy extension acceptance explicit for PDF and Spreadsheet.
- [x] Use canonical general telemetry inputs and verify the database metric tag.
- [x] Exercise current and retained recovery state separately on desktop and mobile.
- [x] Pass all 82 focused .NET tests, all 414 configuration/versioning tests and naming guards, without failures or skips.
- [x] Verify preservation of the preceding uncommitted work and protected original evidence.

Replacement details and baseline preservation records are stored under
`artifacts/rename/search-replace-api-examples/`. Four Chromium desktop/mobile
recovery cases pass. The .NET run recompiles the changed test assembly and reuses
unchanged project references; it is not a full Solution build. All 91 protected
evidence files and 816 original review notes remain unchanged. Earlier source
guards and documentation are preserved with only the new scoped additions;
seven other files from the preceding batch remain byte-identical. Existing
compiler warnings remain outside this test-only naming pass. The internal release fragment
has `none` impact. Product VERSION and published releases remain unchanged;
no commit, push, host restart or deployment is authorized by this batch.

### Repository Classification And Pending Review - 2026-11-06

Baseline: `24390509`. The final tracked-only inventory scans 3,214 regular files
and is stored at `artifacts/rename/final-repository-audit/scan.json`, with compact
counts in `summary.json`. Known source files containing null bytes are scanned;
private paths, untracked notes, symlinks and ignored output are excluded.

The retained inventory has 590 text-scanned files with 20,319 content matches,
42 matching paths and 28 binary inputs with 127 raw-byte matches. These are not
open defect counts. The paths are three schema aliases, one CLI adapter, two
original project archives and 36 original signature/validation records. Binary
findings are original evidence, vendor language data or incidental compressed
image bytes. The three affected screenshots were visually inspected; no old
branding is visible. No binary, original audit or accepted evidence is edited.

- [x] Classify every reported text, path and binary finding without directory-wide exemptions.
- [x] Review remaining current identifiers and defaults individually; classification alone is not proof that replacement is unnecessary.
- [x] Keep the self-referential review register explicit rather than treating it as product output.
- [x] Pass all 414 configuration/versioning tests, strict Markdown/JSON/residual gates and evidence-preservation checks.
- [x] Close repository-only naming after the remaining semantic review, separately from operational and release gates.

The detailed [naming review](../docs/pvad-rename-source-acceptance.md#repository-only-naming-review-2026-11-06)
records scope and limits. Existing checklist states and historical 90/98
release/cutover acceptance remain unchanged. The internal fragment has `none`
impact; VERSION remains 1.1.0. This review does not authorize a commit, push,
release, database change or deployment.

Final verification: all 281 tracked Markdown files have zero former-short-name
matches; the structural JSON guard scans 511 files with no unclassified findings
or failures. All 91 protected evidence files and 816 original review notes remain
unchanged. Removing only the three new review sections reproduces the original
documents exactly. The primary matrix remains 90/98 with the same eight open
operational/release gates. Detailed preservation results are in `final-checks.json`
beside the scan; `git diff --check` passes. No new full-stack or deployed test run
is claimed for this internal documentation-only review. The completion statement
is withdrawn; remaining current source names need individual semantic review.

### Current PDF Header And Delegation Workflows - 2026-11-07

Baseline: `2941cb62`. This batch reviews fourteen workflow test files rather
than treating a matching-file classification as completion. Thirty-five direct
replacements change general API header assertions, the synthetic browser
response, the Account delegation envelope and job trace data to current names.
The Viewer API tests additionally exercise both header generations explicitly,
equal dual headers and conflicting values for each redaction evidence header.

- [x] Use canonical headers in general invoice, signature, compliance and redaction assertions.
- [x] Keep explicit equal-alias assertions and the full backend document-header compatibility matrix.
- [x] Use the current delegation scheme, issuer, audience and scope together in the Account workflow.
- [x] Keep signed legacy-envelope acceptance and cross-generation rejection in dedicated delegation tests.
- [x] Use canonical synthetic job trace state in both the producer and persisted-value assertion.
- [x] Check both Viewer header generations, equal dual values and non-disclosing conflict failures.
- [x] Add independent source contracts against reintroducing old general workflow defaults.
- [x] Finish and record the focused backend, PostgreSQL, browser and naming validation.

The inspected AES interoperability defaults are credentials for the original
encrypted external fixture, not freely chosen new passwords. Its importer test,
generator and checked-in validator evidence agree on those bytes. Replacing only
the defaults would break decryption; regenerating that fixture would replace
original evidence. They remain unchanged, as do original signature/visual
baselines, released text and exact protocol identifiers.

This is test-fixture normalization, not an application behavior change or proof
that every remaining source identifier is unreplaceable. The pending semantic
review and final closure tasks above stay open. VERSION remains 1.1.0.

Validation:

- Focused API suite: 172 passed. Its seven PostgreSQL opt-in skips were then exercised in a separate enabled container run: 8 passed, none skipped, including one repeated queue test.
- Designer: 31 focused Jest tests passed; typecheck and production build passed. Existing bundler/configuration warnings were not suppressed.
- Browser: both synthetic-API Viewer workflows passed on desktop and mobile. The first selector found no tests, and the first Chromium launch was sandbox-blocked; the corrected, authorized browser run is the accepted result, not a live Account/API acceptance.
- Naming/configuration/versioning contracts: 417 passed, none failed or skipped. Residual and structural JSON checks passed; all 281 tracked Markdown files remain free of the former short-name substring.
- All 816 original review notes, 91 protected evidence originals, compatibility registers, audit inventories, VERSION and published release notes remain unchanged. Only reviewed file hashes, statuses and additive current review notes are updated.
- The API test assembly was rebuilt with unchanged project references reused. This is not a full Solution build. The temporary Viewer server was stopped after validation; no commit, push, deployment or version change was performed.

Backend result files and browser artifacts are under
`artifacts/rename/current-header-naming/`; they are local ignored output.
The internal release fragment is `pvad-current-document-header-test-data` with
`none` impact. Raw occurrence counts are not a completion percentage: additional
explicit compatibility/conflict assertions deliberately contain old inputs.

### Generator And Importer Artifact Metadata - 2026-11-07

Baseline: `231769ff`. The reviewed area includes the PDF Trust operation
generator, deterministic invoice fixtures, parser/corpus producers and their
validation consumers. Five nonvisual document metadata values in newly created
operation PDFs and one repository-root error message are freely replaceable.
They now use PVAD. This is not a bulk rewrite of approved invoice or visual
evidence, and does not claim all repository residuals have been eliminated.

- [x] Normalize the four generated operation-document titles and archival author metadata.
- [x] Read generated PDFs back through the importer and assert current title/author metadata.
- [x] Normalize the invoice fixture tool's current repository-root error message.
- [x] Keep the hash-bound invoice contents, provenance, schema IDs and approved manifest unchanged.
- [x] Keep the visible signature title and signer subject that produce the approved image contract.
- [x] Add a source guard that distinguishes replaceable metadata from the two exact visible signature inputs.
- [x] Verify the four operations against existing visual references without updating any baseline.
- [x] Record final naming, evidence-preservation and versioning validation.

Specific retained findings:

| Source | Why the remaining identity is retained |
| --- | --- |
| `EInvoicePdfaFixtureTests.cs` | Seller, buyer, provenance/license strings and the manifest schema reproduce approved deterministic PDF and manifest hashes. A mismatch writes a candidate rather than overwriting approval. |
| `PdfTrustVisualArtifactTests.cs` | Only the signature appearance title and certificate subject retain the previous identity; both are visibly drawn by `PdfPadesSigningService.CreateAppearance`. Other document metadata is canonical. |
| `tools/pdfa/einvoice-pdfa-evidence.mjs` | The report schema is a retained versioned contract, not a current display label. No schema migration is introduced here. |
| `tools/pdf-trust/validate-standards-baseline.mjs` | The validator checks the exact approved policy identity; relaxing or replacing that check would change its evidence contract. |
| Parser, corpus and security tools | Diagnostic families, explicit reader aliases and original encrypted-fixture credentials remain exact. Current producers already use canonical general names. |

Existing original review notes and primary acceptance states remain unchanged.
The pending semantic-review and closure tasks remain open. The internal release
fragment is `pvad-generated-trust-metadata`, with `none` impact; VERSION stays
1.1.0. No commit, push, deployment or approval of new baselines is implied.

Validation: all five focused operation/importer tests (including four
new metadata roundtrips), 64 generator/corpus/totals tests and 418
configuration/versioning contracts passed, with no failures or skips. Strict
residual, structural JSON and Markdown guards passed. All 816 original review
notes, 91 protected evidence originals, both operation baseline files, invoice
manifest, compatibility registers and published version/release records remain
unchanged. The visual comparison additionally passed for all four operations
and eight source/output page images: exact PNG hashes, page counts, widths and
heights match the original macOS operation baseline, without updated references
or relaxed thresholds.

The standard visual wrapper's redundant dependency build was explicitly stopped
and is not counted as a pass. The verified focused test assembly then generated
the actual source/output PDFs using `--no-build`; the first launch was blocked
by the test-platform socket sandbox and the authorized repeat passed all five
tests. The comparison reused the existing baseline validator, platform selection,
72-DPI Poppler rasterization and ImageMagick dimensions, and compared every page
against the original exact hashes. This is not a new full Solution or Linux run.

TRX files and the enforced `visual/comparison-report.json` with side-by-side
images are local ignored artifacts under
`artifacts/rename/generator-importer-naming/`.

### Designer Authentication And Template Adapter Routes - 2026-11-07

Baseline: `83853e12`. The modern template-persistence test still sent its
foreign-tenant render request through the previous branded prefix; it now uses
the canonical render route. The existing adapter lifecycle was deliberately a
legacy test, not eight independent naming defects. It is now exercised through
separate current-prefix and legacy-prefix test entrypoints sharing one lifecycle.
The versioned Designer template API and the older template DTO adapter remain
distinct contracts; this change does not merge their document formats.

- [x] Use the canonical rendering route in the current template-persistence workflow.
- [x] Run the full adapter lifecycle for both explicit prefix generations without duplicating its business assertions.
- [x] Cover anonymous access and missing-CSRF rejection on both generations.
- [x] Cover creation, trusted creator/updater metadata, listing, version reads, no-op updates and stale-revision conflicts.
- [x] Add owner PDF-response and foreign-tenant rendering checks to both lifecycles.
- [x] Check deprecation and exact successor links on legacy success and error responses, and no deprecation on current responses.
- [x] Keep the explicit legacy read and existing dual-generation feature-gate tests.
- [x] Add an independent source contract for current routes and explicit compatibility coverage.
- [x] Complete and record enabled PostgreSQL, authentication, template, alias and naming validation.

Outside the rename scope, inspection found an existing limitation in
`TemplatesController.RenderStoredTemplateAsync`: the older stored-template
adapter ignores its data payload and writes a confirmation-text PDF rather than
rendering the full stored design. The new owner-render assertion verifies access,
status, content type and PDF signature only; it is not a layout-fidelity test.

- [ ] Separate follow-up: replace the older stored-template render adapter's confirmation output with actual design/data rendering and dedicated content/binding regression tests.

No production route, session, cookie, permission, rendering implementation,
schema or original evidence is changed by this test-only pass. The internal
release fragment is `pvad-designer-template-route-tests`, with `none` impact.
VERSION stays 1.1.0; remaining repository semantic review and release gates are
not closed by these tests.

Validation:

- The three focused current-persistence/current-adapter/legacy-adapter PostgreSQL workflows passed.
- The broader authenticated template, handoff/cookie transition, product-feature and API-alias regression selection passed all 56 tests with PostgreSQL enabled; none failed or skipped. This includes the three focused cases, not 59 unique tests.
- Configuration/versioning contracts passed all 419 tests without failures or skips. Residual, structural JSON and tracked-Markdown checks passed; `git diff --check` is clean.
- The two remaining previous-prefix literals in the inspected authentication test are the explicit compatibility read and legacy lifecycle entrypoint. Shared lifecycle paths are exercised for both generations, not simply hidden behind an untested prefix variable.
- All 816 original review notes, 91 protected evidence files, compatibility/audit registers, production template controller/middleware, VERSION and published release records are unchanged. Reviewed test/plan hashes and additive review notes alone were refreshed.
- The API test assembly was rebuilt with unchanged project references reused. No full Solution, browser, deployment or document-fidelity acceptance is claimed. TRX results are ignored local artifacts under `artifacts/rename/designer-template-auth-naming/`.

### JavaScript And XML Review - 2026-11-07

Baseline: `d909de2b`. This extension-specific review scans all tracked regular
`.js` and `.xml` files, including filename matches, case-insensitively. Private
IDE files, untracked notes, symlinks and ignored build outputs are excluded.

| Extension | Files scanned | Matching files before / after | Content matches before / after | Matching paths |
| --- | ---: | ---: | ---: | ---: |
| JavaScript | 68 | 16 / 15 | 81 / 72 | 0 |
| XML | 47 | 27 / 27 | 379 / 379 | 27 |

- [x] Review every matching JavaScript location, separating general defaults from explicit compatibility and customer-content preservation tests.
- [x] Rename the project-format test label to PVADP/PVADS; its schema assertions were already canonical.
- [x] Use the canonical browser-telemetry collector in general status/privacy tests, retaining the explicit legacy bridge and cache-migration cases.
- [x] Exercise both current and legacy diagnostics in separate status-model cases without changing the raw diagnostic contract.
- [x] Add a source contract preventing general test inputs from reverting to the old identity.
- [x] Verify all 27 original XML reports against the existing positive/negative corpus manifests and the baseline bytes; parse each with network access disabled.
- [x] Run all tracked JavaScript test files, configuration/versioning contracts, affected website builds and naming guards.

The remaining JavaScript occurrences are specific compatibility readers/tests,
stable diagnostic IDs, installed dashboard UIDs, negative branding/secret checks,
deliberate customer-input preservation cases and the single approved public
rename-history notice. None is replaced by a new permissive exception.

All matching XML files are original DSS PAdES validation reports, not editable
application configuration. Their filenames, PDF references, certificate subjects
and encoded material belong to the original evidence. Editing their text or
renaming them in isolation would break the manifest hashes and evidence links.
The other 20 XML files have no old-name content or filename finding. No XML
report, certificate, corpus manifest or report link is changed.

The complete per-file before/after inventory, finding locations, line hashes and
dispositions are generated locally at
`artifacts/rename/javascript-xml-naming/scan.json`. This ignored report does not
replace or rewrite earlier audit evidence. XML hash/parsing results are in
`xml-evidence-verification.json` alongside it.

Validation: 139 tests across all 28 tracked JavaScript test files and all 420
configuration/versioning tests passed, without failures or skips. The first
JavaScript invocation omitted the repository's TypeScript-stripping flag and
could not load the shared TypeScript status module; the complete rerun used
`node --experimental-strip-types --test` and passed. Admin and Documentation
production builds passed. Documentation retains existing Vite configuration
module-format warnings; this test-only change does not suppress them.

Residual, structural JSON, tracked-Markdown, version/fragment validation and
`git diff --check` passed. All 816 original review notes and 91 protected
evidence files remain unchanged, as do both compatibility registers and the
original JSON/Markdown inventories. The reviewed-file ledger refresh is limited
to the five edited entries already present in that ledger.

The internal fragment `pvad-website-javascript-naming-tests` has `none` impact.
VERSION, published releases, compatibility registers, historical audits and
primary release-gate states remain unchanged. This scoped review is not a
zero-substring claim or completion of the overall repository semantic review.

### Designer And Account TypeScript Review - 2026-11-07

Baseline: `d909de2b`; the preceding JavaScript/XML changes remain uncommitted
and are preserved. This pass scans all 330 tracked regular `.ts`/`.tsx` files
under Designer and websites. Initially 77 files contain 368 former-short-name
occurrences; afterward 76 contain 361. There are no matching filenames.
These are inventory counts, not seven independent runtime defects or a progress
percentage. Private/untracked files, symlinks and ignored outputs are excluded.

- [x] Inspect every matching Designer, Account and shared-website source/test location.
- [x] Normalize three invoice-validation/pagination comments and one explanatory spreadsheet test label without changing diagnostic codes, validation limits or storage inputs.
- [x] Exercise chart export with both current and legacy recognition sources, checking nested/style normalization, shared/page elements, unchanged series and non-mutating input drafts.
- [x] Exercise Account return-origin overrides through both configuration-key generations, rejecting implicit old-host trust and unrelated origins.
- [x] Add independent source checks for canonical commentary, current-input coverage and exact retained diagnostic/storage expectations.
- [x] Run the complete Designer/Account unit suites, both typechecks and production builds, configuration/versioning contracts and naming guards.

Retained findings were individually inspected, not approved merely because a
whole-file hash was already recorded:

| Boundary | Concrete reason |
| --- | --- |
| Authentication, feature decisions, accessibility, project operations, invoice and Trust diagnostics | Exact raw codes are stable contracts used by backend matching, localization, telemetry allowlists and tests. Current display aliases are separate; no new raw diagnostic family is invented. |
| PKCE, locale, favorites, code drafts, sidebars, recent content, workbook recovery and handoffs | Old storage/input readers and cleanup prevent data loss or cross-owner assignment. Current producers already use canonical keys. Tests intentionally seed and preserve or remove old inputs. |
| Runtime configuration, security/document headers and Account URL markers | Explicit former-input readers, precedence and conflict cases remain necessary. Current requests/configuration are tested separately. The old deployment host is not an implicit trusted origin. |
| Chart recognition and migration artifacts | Old source/artifact IDs remain accepted and canonical values are emitted. Both chart sources are now exercised through the actual export payload builder. |
| Native file inputs and invoice/schema fixtures | File pickers accept both generations. Schema IDs, original fixture/schema paths and versioned parity contracts remain exact rather than being cosmetically rewritten. |
| Release, customer-content and visible-branding tests | Historical release titles, supplied text/filenames, raw/display assertions and negative branding checks deliberately contain the former identity. The tests would lose their purpose if globally replaced. |

The generated per-file before/after inventory, line locations/hashes, original
review provenance and scoped change notes are in
`artifacts/rename/frontend-typescript-naming/scan.json`. Existing review categories
are provenance, not a new allowlist or semantic completion claim.

Validation: all 819 Designer tests in 92 suites and the existing snapshot passed;
all 54 Account tests and 421 configuration/versioning contracts passed, with no
failures or skips. Designer and Account typechecks and production builds passed.
Existing Vite module-format, mixed static/dynamic locale import and bundle-size
warnings remain visible. No browser, deployment or fresh .NET run is claimed
for this comment/test-only pass.

Residual, structural JSON, tracked-Markdown, version/fragment validation and
`git diff --check` passed. Transpiling the two edited production files with
comments removed produces exactly the same JavaScript as the baseline. All
816 original review notes, 91 protected evidence files, compatibility registers,
original JSON/Markdown audits, VERSION and published release records are intact.
The earlier uncommitted JavaScript/XML work remains present and is not reverted.

The `pvad-frontend-typescript-naming-review` fragment has `none` impact. Runtime
behavior, original evidence, published release text, VERSION, compatibility
registers and previous task/approval states remain unchanged. Remaining overall
repository semantic review and the eight historical release gates stay open.

### Backend CSharp Review - 2026-11-07

Baseline: `d909de2b`. This pass covers all tracked regular C# files under
`src/`, `PVAD.WebApi/` and `tests/`, including source containing null bytes.
The preceding uncommitted JavaScript/TypeScript work remains intact.

| Group | Files scanned | Matching files before / after | Content matches before / after |
| --- | ---: | ---: | ---: |
| Production | 840 | 114 / 114 | 1,431 / 1,429 |
| Tests | 322 | 117 / 115 | 834 / 829 |

There are no matching C# filenames in this scope. Counts include intentional
contracts and incidental substrings; they are not independent naming defects.
Production changes are limited to two comments, not executable statements.

- [x] Inspect remaining production identifiers and non-diagnostic values separately from the stable diagnostic families and their central display-alias registry.
- [x] Make current OAuth scopes the general discovery/PKCE and service-account test inputs, retaining explicit old-scope requests.
- [x] Check missing PKCE and revoked credentials for both scope generations and run both current and retained service-account lifecycles.
- [x] Read current generated-code fields in PDF/Spreadsheet provider tests, checking equal compatibility fields for all three providers.
- [x] Rename the binary-cutover test method neutrally without changing either harness, process arguments or persisted-state checks.
- [x] Normalize three invoice comments while preserving the raw validation code and 4,096-character limit.
- [x] Update the existing source guard to require current generated-code usage and a separate exact compatibility equality assertion; do not weaken the wire-contract check.
- [x] Run the focused PostgreSQL/OAuth, migration and diagnostic/artifact contract selection: 103 passed without failures or skips.
- [x] Complete generator/core/importer regression validation, final naming guards and preservation checks, and record their actual results.

Specific retained boundaries:

| Area | Why the remaining identity stays |
| --- | --- |
| Code-worker, accessibility, invoice, Trust, layout, project and API diagnostics | Raw diagnostic IDs and compiler-warning IDs are stable. Display aliases are derived separately, and unknown IDs must not acquire invented aliases. |
| Native project manifests, migration results and audit writes | Exact legacy readers, public obsolete CLR aliases, serialized property names and stored event/format IDs remain compatible. Existing canonical output fields are tested alongside them. |
| Authentication, cookies, headers, OAuth scopes/clients, claims and data protection | Historical inputs, installed operator pseudonyms and cryptographic purposes remain valid during transition. Current producers are already canonical; replacing all readers would break old sessions or protected data. |
| License signing and replacement | Key IDs, signed envelopes and explicit retained-license test inputs are not free display names. A replacement must preserve the old signed record, not rewrite its identity. |
| Validation evidence and retention migration | Existing invoice-schema IDs and the database setting used by the immutable retention trigger remain exact. The applied migration and stored evidence are not cosmetically edited. |
| PDF/fixture tests and installed storage | Original corpus IDs, encrypted-fixture passwords, visible signature baselines, old mount/content preservation and independent validator identities remain unchanged. |
| Provider and codec substrings | Report-provider metadata and JPEG-2000 terminology can contain the searched letters incidentally; they are not first-party branding. |

The complete inventory with exact finding locations and line hashes is generated
locally at `artifacts/rename/backend-csharp-naming/scan.json`. Original review
categories are recorded as provenance, not blanket semantic approval. The
source containing a null byte remains unchanged and participates in the scan.

Validation:

- The API test project, Generator and Generator test project built successfully with unchanged project references reused. Existing API-test obsolete-constructor and analyzer warnings remain visible; Generator builds had no warnings. This is not a full Solution build.
- All 283 selected .NET tests passed without skips: 103 API/OAuth/contract tests, 32 generator pagination/totals tests, 96 Core project/layout/chart tests and 52 importer chart/attachment/encryption tests. The API selection includes three real PostgreSQL workflows.
- All 422 configuration/versioning tests passed serially. The first parallel run timed out in an unchanged five-second fake-launcher test; the complete serial rerun passed without changing that timeout or relaxing assertions.
- Residual, structural JSON, tracked-Markdown, version/fragment validation and `git diff --check` passed.
- Comparing all 840 production sources to the baseline confirms that only the two exact comment replacements changed; executable production statements are unchanged. All 816 original review notes, 91 protected evidence files, both compatibility registers, original JSON/Markdown audits, applied retention migration and null-containing importer source remain intact.
- TRX results, serial/parallel configuration logs, preservation verification and the full inventory are ignored local artifacts in `artifacts/rename/backend-csharp-naming/`.

The internal `pvad-backend-csharp-naming-review` fragment has `none` impact.
No database, session, signing key, runtime contract, original evidence, published
release, VERSION or operational approval is changed. The paired legacy-binary
process drill is a separate opt-in test; renaming its method is not a new claim
that the drill was executed. Overall semantic review remains open.

### Build And Configuration Review - 2026-11-08

Baseline: `d909de2b`. The extended scope covers 444 tracked regular files:
module/shell scripts, YAML/workflows, project/build properties, HTML/CSS,
Dockerfiles, server templates, environment scripts and the explicit historical
binding example. The earlier 430-file extension-only selection is included.
Private paths, symlinks and ignored outputs are excluded.

- [x] Review freely chosen labels separately from actual historical binary, environment, metric, database and storage identities.
- [x] Normalize the binary-cutover workflow comment/step title and deployment test title without changing execution or compatibility inputs.
- [x] Review remaining MCP/tool TypeScript and C# findings alongside the prior frontend/backend passes.
- [x] Add mixed-case and embedded-identifier coverage to the central detector.
- [x] Add a read-only tracked-file inventory with explicit opt-in report writing, text positions/hashes, binary offsets and private/symlink/ignored-path exclusions.
- [x] Test null-containing source, case variants, path-only matches, invalid UTF-8, PDF detection, unchanged source/index and parent-directory symlink substitution.
- [x] Consolidate retained contracts, original evidence and real follow-ups in the [remaining-items list](PVAD.Rename-Remaining-Items.md).

The scoped before/after artifact is
`artifacts/rename/build-configuration-naming/scan.json`. At baseline it contains
1,278 matches in 115 files; the working review contains 1,313 in 114 files.
The additional occurrences are explicit detector and exclusion regression
inputs/assertions, including the preceding uncommitted source-guard work, not
new product branding. Counts are not a completion metric. Newly untracked
implementation files are tested separately until they enter the Git index.

Specific retained findings include old Docker/configuration aliases and conflict
guards; Grafana UIDs and existing provider/folder names; stable metric/runbook
identifiers; template aliases; historical release artifact inputs; schema and
resource readers; and exact names used by the paired-binary proof. Signed PDF
indexes and original recovery HTML are evidence, not current naming defaults.
The tool-source review additionally retains fixed MCP diagnostics, scopes and
resource readers, reference-archive checksums and original warning pragmas.

Validation:

- All 25 focused inventory, workflow and deployment tests passed without skips.
- MCP typecheck and build passed; all 33 MCP runtime tests passed on the authorized local-host rerun. The sandbox-only attempt could not create its IPC listener. Existing schema-format warnings remain unchanged.
- The serial broad run across 79 tracked Node test files passed 557 of 559 tests with no skips. Two coverage-host checks failed because genuine Application/relay Release dependency manifests are absent; other collector hosts also lack Release outputs. This is not a passing full-suite result. The sandbox run additionally could not bind two loopback test listeners; the authorized rerun passed both without test changes.
- Full Release collector-host builds and the two coverage checks remain explicit follow-ups, not waived requirements. Logs are ignored artifacts alongside the scoped scan.
- The final serial selection passed all 439 configuration, deployment and versioning tests across 56 files without failures or skips.
- All 32 shell scripts passed syntax checks; all 119 project/build XML files parsed. The edited workflow's executable fields are unchanged after excluding the display title.
- All 135 local links/section references in the four review documents resolve. Residual, structural JSON, tracked-Markdown, version/fragment guards and whitespace validation passed; the new untracked documents and fragments were checked separately.
- All 816 original review notes/reason records and 91 protected original evidence files are unchanged. Compatibility registers, original inventories, VERSION and published releases remain intact.

No executable workflow configuration, runtime deployment binding, database,
signature or published release is changed. The internal release fragment has
`none` impact; VERSION is unchanged. Overall semantic review and historical
operational approvals are not closed by this scoped review.

### Current Application Output Review - 2026-11-08

Baseline: `13ec82ed`. Inspect remaining productive findings separately from
rendered output: raw diagnostic codes, obsolete response members, input readers,
audit identities and protected evidence are not freely renameable display text.
The Company naming-history note and compatibility fields in system metadata
remain intentional. No new diagnostic alias or machine-readable identifier is
introduced in this pass.

Eight new regression cases cover previously unprojected project/code/signing
failure surfaces and the blocked-mutation banner. Seven initially failed on
the old visible diagnostic references; the additional banner regression and
four independent source guards cover the complete correction. One new test's
initial draft assertion was corrected to match the actual pretty-printed initial
source; no production formatting or roundtrip behavior was changed.

- [x] Project import review and failure messages use the existing diagnostic display projection, while source filenames and original failure objects remain exact.
- [x] Workspace error banners and all three C# preview tabs apply the same projection at rendering time, without changing editor drafts or raw JSON validation messages.
- [x] PDF signing failure status and Trust mutation-blocking warnings agree with the existing Trust error presentation; request routes, uploaded files and raw Problem Details remain unchanged.
- [x] Add four source guards independent of the exception ledger and verify that reverting the corrected rendering boundary fails.
- [x] Run the complete Designer tests, typecheck and production build; preserve the published release manifest and VERSION.
- [x] Record the correction in the [remaining-items list](PVAD.Rename-Remaining-Items.md#current-output-follow-up-2026-11-08) with a customer-facing Patch fragment.

All 827 Designer tests in 92 suites and the existing snapshot passed. The focused
selection passed all 35 tests in five suites. Typecheck and production build
passed; existing Vite module-format, mixed locale-import and bundle-size warnings
remain visible. These are rendered-DOM and build checks, not a fresh authenticated
browser, full Solution, backend runtime or deployment test. The earlier two
broad coverage-host failures remain pending their genuine Release-build outputs.
Detailed test results are ignored artifacts under
`artifacts/rename/current-application-output-naming/`.

Final configuration/deployment/versioning validation passed all 443 tests across
56 files without failures or skips. The first run rejected a new negative-brand
assertion and a customer-title fixture in the general parity example. That
example was restored to its original canonical content; the C# failure cases
now assert the complete displayed list exactly. Customer-filename preservation
remains tested in the dedicated import/signing cases. No source guard was relaxed.

All 816 original review notes/reason sets and 91 protected evidence files are
unchanged. One exact diagnostic-regression review record was added, and a prior
resolved parity-test record now explicitly describes its new raw diagnostic
fixtures. The diagnostic projection helper, backend alias catalog, branding API,
diagnostic serialization contract, compatibility registers, original inventories,
VERSION and published releases are byte-identical to the baseline. The refreshed
tracked-only inventory contains 597 matching files; the additional matching
test sources are intentional failure inputs, not new product naming defects.

Raw code and structured document previews deliberately remain raw; customer
names and document contents are not application branding and must not be
rewritten. Historical release data, schemas, signed evidence, cookie/header
contracts and translation keys remain unchanged. The Patch fragment changes
neither VERSION nor published releases; no commit or push is implied.

### Account And Admin Output Review - 2026-11-08

Baseline: `9e0be8aa`. Review dynamic Account/Admin output independently of raw
source search. Correct 51 display expressions across both entry points and nine
Account page modules using the existing display helper. Keep structured error
objects unchanged for routing, authorization decisions and support evidence.

- [x] Project Account authentication, verification/confirmation, permission and page-load errors only at the display boundary; retain password clearing, recovery links, focus and retry behavior.
- [x] Project Admin login/public-action errors, system/governance failures, user/organization/role pages, subscriptions, licenses, service accounts, mail and audit error banners.
- [x] Keep informational customer text, organization names, license numbers, signing-key IDs, API-key secrets, Legal content and actual audit events unmodified.
- [x] Preserve license-validation result codes in API data and retain the already existing display-only projection for the successful result.
- [x] Fix the Admin client's JSON media-type reader to accept structured Problem Details and case variants, retaining raw detail, code, trace and body fields.
- [x] Verify that historical release text is already projected by the existing release presenter; do not rewrite the manifest or persisted notifications.
- [x] Execute production rendering/event functions in a synthetic DOM and add independent source/mutation guards.
- [x] Validate both production builds, Account typecheck, Account/Admin tests and the complete Designer regression suite.
- [x] Run and inspect desktop/mobile Chromium screenshots with isolated production previews and synthetic API responses; shut down the test processes afterward.

All 846 Designer tests in 92 suites and the snapshot passed, including 19 new
Account/Admin rendering/event regressions. All 54 Account tests and 42 Admin
tests passed; the Admin selection includes three raw-response tests for JSON,
Problem Details and uppercase media types. Account typecheck and both production
builds passed. Existing build-module warnings remain unchanged. Initial tests
exposed unprojected output and discarded Problem Details; the event harness was
also adjusted to await its asynchronous submit handler before asserting.

Four browser scenarios passed at 1280x900 and 390x844: one canonical-CSRF login
request, the projected diagnostic, literal rather than injected HTML, enabled
recovery controls, no page errors and no horizontal overflow. Screenshots were
visually inspected. These are mocked-API browser checks, not real authenticated
sessions, commercial licensing acceptance or deployment evidence. Logs, test
results and images are ignored artifacts in
`artifacts/rename/account-admin-output-naming/`.

The customer-facing Patch fragment changes neither VERSION nor published
releases. No real credentials, database rows, policy acceptances, signing keys,
original fixtures, cookie/header contracts or operational approvals are changed.
The earlier Release-output coverage prerequisites remain a separate follow-up.

Final verification passed all 447 configuration/deployment/versioning tests
across 56 files with no failures or skips. AST comparison confirms that all 11
UI source changes are limited to display-helper imports and rendered text
projection; state mutations and authorization branches are otherwise identical.
The Admin transport differs only in its JSON media-type reader. All 817 original
review notes/reason sets, 91 protected evidence files, both compatibility
registers, original audit inventories, existing diagnostic projector/catalog,
Account transport, VERSION and published releases remain unchanged. No new
diagnostic alias or review-record exception was introduced. Naming, version,
fragment and whitespace checks passed, and all review-document links resolve.

### Public Websites Output Review - 2026-11-08

Baseline: `754d3566`. Review Company, Documentation and Demo together, including
current source text, HTML metadata, release presentation, downloadable examples
and public PDF Trust catalogs. Separate editorial catalog presentation from the
original validation artifacts it links to.

- [x] Check tracked application sources and page entry points, allowing only the agreed single Company naming-history note.
- [x] Correct titles, headings and introductory product wording in the positive and negative PAdES download catalogs; explain retained original filenames without relabeling producer identities.
- [x] Keep every original download link, result classification and manifest unchanged; verify the hashes of all nine signed PDFs, 27 validator reports and eight trust artifacts against their existing manifests.
- [x] Execute all eight downloadable Demo example functions and verify current DTO/namespace labels, sample-file availability and exact retained migration diagnostic IDs.
- [x] Preserve supplied provider names, namespaces, document text and filenames; do not treat raw example diagnostics as editable product branding.
- [x] Add independent current-source and catalog-presentation guards, including two reverted-heading mutation cases.
- [x] Pass Company, Documentation and Demo tests and production builds.
- [x] Finish desktop/mobile production-preview browser checks and inspect screenshots.
- [x] Complete exact residual review, protected-evidence verification and naming/version/fragment checks.

The two catalog HTML wrappers are current navigation/presentation, not signed
PDFs or independent validator reports. Their six visible naming expressions are
corrected editorially; original wrapper wording remains in Git. This scoped
decision supersedes their earlier broad wrapper-retention classification only
for titles, headings and introductory prose. It does not authorize renaming
artifact paths, changing evidence bytes or reapproving historical results.

Company error handling already uses neutral outage text, Documentation already
projects historical release text at presentation, and Demo descriptors already
use current model labels. No new naming defect was confirmed in those paths,
so their production code is unchanged. The JSON playground's parse error may
quote user input and is not globally rebranded. Raw downloads and historical
PDF metadata intentionally retain their recorded identities.

Company passed 83 tests, Documentation 62, Demo six and the direct writer guard
19; all three production builds passed. Detailed logs are ignored artifacts in
`artifacts/rename/public-websites-output-naming/`. The Patch fragment concerns
only the corrected Documentation catalogs. VERSION and published release
history remain unchanged; no commit, push or deployment is implied.

All 60 production-preview browser scenarios passed at 1280x900 and 390x844:
19 Company pages, the Documentation release view, two PAdES catalogs and eight
Demo details at each viewport. Every displayed page uses current names except
the single agreed Company history note. All catalog targets and Demo source,
input and output downloads returned HTTP 200. Ten screenshots were inspected.
The two changed catalogs have no viewport overflow; their tables scroll
internally on narrow screens. Existing Demo workbench long-path/code overflow
is outside this naming correction and is not claimed fixed by these checks.
APIs were mocked unavailable, external requests blocked and all isolated
preview servers/browser contexts stopped. These are local presentation checks,
not real backend authentication, legal publication or deployment acceptance.

Final configuration/deployment/versioning validation passed all 449 tests across
56 files with no failures or skips. All 50 hash-pinned JSON-audit originals and
85 unchanged public corpus artifacts match the baseline. All 817 original review
notes/reason sets remain intact. For the two edited wrappers, comparison after
removing only title/heading/paragraph lines is exact, and every href remains
byte-for-byte unchanged. The independent artifact tests verify nine PDFs,
27 reports and eight trust artifacts against the original stored hashes; they
do not re-run an external signature validator or reapprove historical verdicts.

The residual ledger refresh is limited to eight reviewed changed files; no
new residual exception is added. Markdown has zero old-name matches, JSON has
zero unclassified findings, version/fragment checks and whitespace checks pass.
Current Company/Documentation/Demo entrypoint logic, shared diagnostic helper,
compatibility registers, original audit inventories and published releases are
unchanged. Only the two catalog HTML presentation wrappers change shipped
output. No API, database, authorization or deployment change is made.

### CLI MCP And Diagnostic Tool Review - 2026-11-08

Baseline: `b00577f0`. Review current tool output separately from protocol data,
compatibility readers and reproducible validation inputs. The tracked-only
tool inventory covers 259 regular files; its 111 matching files and 1,100 text
occurrences include explicit negative tests and are not outstanding rename
defects. The inventory is an ignored local artifact, not a new historical audit.

- [x] Correct the standards CLI policy-mismatch explanation without changing the recorded policy identifier or its validation condition.
- [x] Make MCP smoke summaries use current resource titles and primary/compatibility labels while continuing to request every original URI.
- [x] Identify offline schema checks by manifest/alias position, preserving exact registry keys and original packaged schema bytes.
- [x] Verify all 38 catalog element definitions and both JSON/C# example responses through MCP; keep missing C# examples explicitly unsupported rather than inventing code.
- [x] Preserve MCP structured result data and its text serialization exactly, including supplied names, paths, namespaces and stable diagnostics.
- [x] Keep scope/claim/environment aliases, retained deployment/job harnesses, corpus credentials, signature evidence and schema contracts unchanged.
- [x] Pass MCP tests, typecheck, build, toolchain contracts and both source/distribution Stdio smoke tests.
- [x] Run parser/corpus, fidelity, visual-tool, configuration/deployment/versioning and standards/evidence regression tests.
- [x] Finish exact source/evidence preservation, residual guards and review-document link checks.

Only two tool source files change presentation: one standards CLI error sentence
and MCP smoke summaries. Neither the production MCP server nor its tool-result
serializer, resource registrations, authorization, API client or schema content
changes. Raw protocol JSON remains raw; no new diagnostic aliases are added.
The standards CLI accepts the same recorded policy and rejects both an invalid
value and an unapproved technical-name replacement. Its release gate remains
closed until the independently governed standards approval is actually present.

All 35 MCP tests and nine toolchain contracts passed, as did typecheck and build.
The first npm/tsx test attempt was blocked by the sandbox IPC socket; the retry
with local test sockets passed. The initial source and distribution Stdio checks
passed without old product names in their console summaries. Expanded final
checks exercise 76 example responses per server (JSON and C# for 38 elements).
This tests returned source examples, not C# compilation or actual backend PDF
rendering. Existing AJV date/date-time format warnings remain visible and are
not interpreted as complete date validation. No schema strictness is weakened.

The 63-file tool regression selection passed 482 tests with no failures or
skips. It includes the standards mismatch, explicit release rejection, parser
aliases, corpus hashes, fidelity thresholds and all direct writer mutation
guards. Evidence logs are ignored under
`artifacts/rename/cli-mcp-tool-output-naming/`. The release fragment has impact
`none` because only internal validator/smoke output and regression coverage
change. VERSION, published releases and live databases remain unchanged.

Both expanded final Stdio runs passed all 76 example-response checks. The built
distribution resolved all 31 registered schema identities/aliases offline and
served all ten resource registrations. Their console output contains no old
product names, while the actual resource/alias keys remain unchanged. All 20
packaged resource files match repository source bytes exactly.

Preservation checks confirm that all 50 hash-pinned audit originals, 87 public
corpus files and 16 selected runtime/contract files match the baseline. The
standards validator differs by exactly its one explanation string. No original
review notes, historical results, hashes, schema IDs, signatures, fixture
passwords or permissions are changed. Residual hash updates are limited to the
reviewed changed files; no new whole-file or directory exception is introduced.
Markdown, JSON, release-fragment, version, link and whitespace checks pass.

### Generated Document Output Review - 2026-11-08

Baseline: `08258e16`. Inspect freshly generated output rather than inferring
document naming solely from source matches. Check PDF, DOCX, XLSX, ODT, HTML
and XML, plus native PDF/Spreadsheet generator facades. Existing documents and
signed evidence are inputs to preserve, not material to rebrand.

- [x] Inspect PDF creator/producer serialization, Office metadata, document-export filename selection and the canonical XML writer.
- [x] Add real-export tests for six formats with current synthetic data and a separate supplied-customer-name case.
- [x] Verify controller file-result metadata, filename, MIME type, source immutability, parsed PDF metadata and Office package contents.
- [x] Cover the Spreadsheet facade and explicit customer PDF creator/producer values without rewriting them.
- [x] Run the fresh-output tests and inspect generated artifacts; correct only confirmed naming defects.
- [x] Run relevant export/API and naming regressions, preserve original evidence and validate the final diff.

The tests write synthetic generated files only under ignored
`artifacts/rename/generated-document-output-naming/exports/`. Both variants
assert meaningful metadata/cell/body values, not just file signatures. PDF
checks parse metadata instead of searching arbitrary compressed or encoded
bytes for incidental substrings. Office checks inspect package paths and XML;
neutral third-party package metadata is not relabeled as PVADE.

Production writer inspection and the fresh-output tests found no additional
confirmed current-branding defect. All production writers remain unchanged;
the added coverage is retained instead of manufacturing a replacement. No
compatibility export, schema, diagnostic, database, signed fixture or published
release is changed by the added tests.

The 15 new cases live in the existing API branding suite alongside its two
original metadata/chart cases. The general facade example file is byte-identical
to the baseline and the existing canonical-example source guard is unchanged.
Initial test development exposed two harness errors: XML must be read as bytes
so its valid UTF-8 BOM is handled, and PDF dictionary values must be enumerated
through the underlying value collection. Both were corrected; metadata checks
also require a nonempty collection. Office checks include core-property parts
with the `.psmdcp` extension, not only `.xml` and `.rels` files.

Final .NET selections passed 50 API tests, 64 export tests and five filename
tests, with no failures or skips. They cover the new output cases, existing
HTTP export MIME/filename/signature and page-image ZIP cases, supported PDF
compliance settings, Word element output, Spreadsheet roundtrips and encryption
export behavior. This is focused regression coverage, not a full Solution,
browser, visual-parity or independent standards-conformance acceptance.

The first build attempt hit the sandbox MSBuild named-pipe restriction and was
stopped. The first serial retry compiled dependencies but reached its time
limit before tests. The incremental retry and final selections completed.
These interrupted attempts are retained in separate logs and do not count as
passing tests. Existing compiler/obsolete-API and NPOI licensing warnings remain
visible; no warning suppression, package update or licence acceptance was added.

All 14 synthetic files were independently inspected with Poppler `pdfinfo`,
`unzip` and a separate XML parser. The current PDF carries the expected PVADE
title and PVAD renderer creator; supplied customer metadata remains exact.
Neutral DOCX/XLSX/ODT packages contain no old product names in checked paths,
core properties, content types, relationships or XML content. The XML envelope
uses the canonical technical name. The local inspection report contains the
actual file sizes and SHA-256 values and does not replace original evidence.
Its initial archive reader was adjusted for literal square brackets in the
OpenXML content-types filename, without altering any exported bytes.

Final configuration, deployment and versioning selections passed all 451 tests
with no failures or skips. The residual path/hash review and structural JSON
guard pass; 282 tracked Markdown files have zero old-name occurrences. All 157
local links in the four updated review documents resolve. Version synchronization,
release-fragment validation and `git diff --check` pass.

Preservation checks against the baseline verify all 936 production files, 50
hash-pinned originals, 87 public PDF Trust files, both compatibility registers,
the original inventories, VERSION and published releases unchanged. All 817
original review notes and reason sets and all preceding checklist task states
are preserved. Only two existing residual-review hashes and their additive
current-review notes change for the extended tests and linked rename plan.
The 14 generated artifact hashes still match the independent inspection report.
Local results are stored under the ignored artifact directory, including
`artifact-review.json` and `preservation-verification.json`.

The release fragment has impact `none`: only tests and scoped review documents
change. VERSION, published releases, contracts and live databases remain intact.
No code defect, production rename, full metadata-feature parity or new external
approval is claimed by this review.

### Collector Host Release Verification - 2026-11-08

Source baseline: `36b9e1d2`. This follow-up addresses the missing Release
dependencies behind the earlier two coverage-host contract failures. The
earlier 557/559 result remains historical evidence, not a passing run.

- [x] Inventory the 52 collector hosts and separate collector-free relay host without changing package references or tests.
- [x] Identify and correct the temporary build selection's configuration handling so referenced projects keep Release configuration.
- [x] Complete genuine Release builds for all 53 selected test hosts and verify their dependency manifests.
- [x] Pass all coverage-smoke contract tests, including both previously failing dependency checks.
- [x] Repeat the broad tracked Node tooling/deployment selection without skipping tests or weakening assertions.
- [x] Execute the collector smoke selections with real coverage attachments and run the relay tests separately.
- [x] Validate naming, links, original evidence and the final diff; keep product version and published releases unchanged.

The initial inventory found 47 of the 52 collector Release manifests missing.
A generated temporary solution under ignored
`artifacts/rename/collector-host-release-verification/` selects the existing
projects, not substitute hosts. Its first build resolved omitted project
references as Debug and was deliberately stopped. The corrected invocation
sets `ShouldUnsetParentConfigurationAndPlatform=false`; the interrupted
attempt is not counted as a successful Release build.

The sandbox-only registry preflight could not bind local HTTP/TLS listeners.
The unchanged three registry tests passed with the required loopback permission.
This is a test-environment limitation, not a reason to relax the assertions.
No production source, runtime identity, database or external service is changed.

The corrected build completed with zero errors and 115 warnings. All 53 real
host DLLs, runtime configurations and dependency manifests exist; each manifest
resolves the required private test host. The corrected log contains no Debug
outputs. Existing compiler, nullable, analyzer and compatibility warnings remain
visible and are not suppressed. The six coverage contract tests pass, including
both previously failing checks, with no skips. An initial Application collector
preflight passed five tests and produced nonempty Cobertura and JSON production
coverage; this smoke selection is not a full product-coverage percentage.

The fresh broad run passed all 572 tests across the same 79 tracked tooling
and deployment test files, with zero failures or skips. The original 557/559
result is not rewritten; this is a new execution against the current source.
The separate collector-free relay passed all seven Release tests, including
canonical and retained signature headers. Logs and TRX results are kept under
the ignored verification directory. No test source, package reference or
production implementation was changed to obtain these results.

The unchanged collector runner completed all 52 projects with 955/955 selected
tests passed, none skipped. Its per-project validation requires matching TRX
counts/test names, nonempty production hits and consistent Cobertura/JSON module
sets, excluding test/tool assemblies. The separate audit then reread all 52
report sets successfully without changing their raw evidence. The earlier five
Application preflight tests are not counted again in the 955-test matrix.

The run produced 52 TRX files and 104 coverage attachments. Their SHA-256 values
are recorded in the local preservation report. All 53 host DLL/dependency
manifest pairs still match their post-build hashes after instrumentation. The
additional hash-report reader initially counted a VSTest auxiliary results
directory as a coverage directory; it was corrected to select actual Cobertura
attachment directories, matching the unchanged coverage reader. No
coverage attachment was deleted, substituted or rewritten.

Actual corrected build invocation, using the locally generated selection:

```bash
dotnet build artifacts/rename/collector-host-release-verification/collector-hosts.slnx \
  --configuration Release --no-restore --disable-build-servers -m:1 \
  -p:UseSharedCompilation=false \
  -p:ShouldUnsetParentConfigurationAndPlatform=false
```

The selection is generated from `collectorProjects()` plus the separate relay
project. A later execution must regenerate that selection and choose a fresh
coverage output directory rather than overwrite the retained run. The stored
`release-build-verification.json`, `coverage-all/summary.json`,
`coverage-all/audited-summary.json` and `preservation-verification.json` record
the actual local outputs and source baseline.

Preservation checks confirm 936 production files, all test/tool/workflow sources,
50 hash-pinned originals, 87 public PDF Trust files, both compatibility registers,
the original inventories and all 817 review records unchanged from the baseline.
This is the selected Release host/catalog verification, not a claim that every
Solution project, every test case or browser workflow was exercised. Existing
product-rendering limitations and operational/release gates stay separate.
The new fragment has `none` impact because only internal review records change.
VERSION remains `1.1.0`; published releases, databases and deployments are intact.

Final residual path/hash, structural JSON and tracked-Markdown guards pass.
All 158 local links in the four review documents resolve, and the audited
coverage summary exactly matches the completed run. Version synchronization,
fragment validation and `git diff --check` pass. No commit, push, package
publication or deployment was performed in this follow-up.

### Repository Closeout Audit - 2026-11-08

Baseline: `36b9e1d2`, with the preceding uncommitted collector-host verification
preserved as a separate work block. This audit includes all 3,229 tracked regular
files, including 228 tracked-but-ignored vendor/sample files. Private IDE data,
untracked notes, symlinks and ignored untracked build outputs are not read.

- [x] Capture a fresh short-name inventory and a separate broad former-name scan, without rewriting original audit inventories.
- [x] Review current defaults, writers, readers, diagnostic presentation, fixtures and operational probes against their actual consumers.
- [x] Correct the synthetic RDL author/description and mail-probe marker previously hidden behind overly broad retention explanations.
- [x] Assert canonical RDL metadata while preserving all existing geometry and conversion expectations.
- [x] Execute the mail probe with real Bash/jq and simulated Docker/Mailpit, and prove that substituting the old marker fails.
- [x] Parse affected vendor XML and verify all 192 matches are within canonical Base64 payloads rather than editable product text.
- [x] Refresh only changed review hashes/statuses, retaining all original review notes and reason sets.
- [x] Pass broad tooling, naming, RDL, link, version and preservation checks on the final working tree.
- [x] Consolidate repository-only acceptance separately from compatibility retirement and external release/cutover gates.

The fresh short-name-only scope scans 3,001 nonignored tracked files; its initial
597 matching files, 20,170 text matches, 153 raw binary matches and 42 path matches
are inventory values, not unresolved-defect counts. The additional broad scan
covers earlier long names and neutral drawing terminology too. Its initial 779
matching files include original evidence and the review register itself.

Two prior whole-file explanations were too broad. The comprehensive synthetic
RDL fixture's Author and Description are editorial metadata, not compatibility
inputs or hash-pinned external evidence. Only those two nodes change to current
technical/Designer names; report geometry, data, IDs and styling remain exact.
The mail probe's synthetic runbook marker is also freely chosen. Both its sent
payload and expected receipt now use the current marker, while the actual
dashboard UID and established operational runbook IDs remain unchanged.

All 94 RDL migration tests passed, none skipped, with the new source-metadata
assertions and existing layout expectations. The mail harness executes the real
shell and JSON predicates against isolated synthetic responses and rejects a
deliberate old-marker mutation. This is not an actual SMTP/container deployment
or Mailpit delivery acceptance. No stable runtime diagnostic is renamed.

The supplemental tracked-vendor scan found 192 short-name matches in 17 of 228
files. XML parsing locates each inside a canonical Base64 ImageData, VectorData,
or custom-report-item payload. Custom-property Value nodes are additionally
verified to decode to PDF or PNG. An initial attachment-only classification
assumption was corrected for these custom properties; no vendor bytes change.

Local reports live under `artifacts/rename/repository-closeout-review/`:
`all-tracked-legacy-names.json` records exact file/line hashes and positions,
`ignored-tracked-vendor-inventory.json` and
`vendor-encoded-value-verification.json` record the supplemental XML proof.
They supplement rather than overwrite the original tracked JSON/Markdown audits.
Hash matches identify unchanged reviewed inputs; they are not directory-wide
approval or permission to rewrite a future occurrence.

Final acceptance: the repository-only naming scope passes. The two pending
semantic/source-closeout tasks in the dated classification section above are
closed by this new review, not by retroactively changing its recorded results.
Earlier withdrawals of broad completion claims remain dated history; the
current decision does not assert a zero-substring repository or production
release readiness. No additional freely renameable current name was identified
in the reviewed source/default/output scope after the two corrections.

The broad selection passed 573 tests across 79 files, with no failures or skips;
the RDL suite passed all 94 tests. Independent guards reject regressions in the
synthetic marker, while the real shell/JSON harness verifies its producer and
receipt and rejects the specific old-marker mutation. Live SMTP/container
delivery, a new authenticated browser deployment and a whole-Solution test run
are not claimed by this pass. The preceding collector proof remains its own
unchanged 53-host build, 955-test matrix and seven relay tests.

All original review notes/reasons, protected audit and Legal records, compatibility
registers, signed documents and vendor inputs remain unchanged. The RDL fixture
is a resolved reviewed entry after only its two metadata edits; changed review
notes explicitly narrow the earlier overbroad retention decisions. The new
fragment has `none` impact. VERSION, published releases, databases, installed
resources and external gates are unchanged. No commit or push is performed.

Final short-name counts are 596 matching files, 20,170 text matches, 153 raw
binary matches and 42 path matches within the 3,001-file nonignored scope. The
broader 3,229-file scan has 779 matching files: 740 text and 39 binary, including
17 tracked vendor inputs. Apart from the explicitly self-inventoried review
register, every matching nonvendor text file has its exact reviewed hash/reason.
Raw text totals do not decrease because deliberate regression
inputs and retained-dashboard assertions counterbalance the four corrected
source occurrences. They are not an open-defect count or a new allowlist.

The final 573-test tooling run passed after documentation consolidation. Naming,
structural JSON, strict tracked-Markdown, version/fragment and whitespace checks
pass; all 197 local links in the five review documents resolve. Preservation
checks confirm 936 unchanged production files, 50 original hash pins, 87 public
Trust files, 228 vendor inputs, all 817 original review notes/reason sets and the
unchanged primary rename-plan record beneath its new dated acceptance. Only the
two pending source-review checkbox states advance; the preceding uncommitted
collector section and fragment are preserved exactly. Reports and execution
logs remain local ignored output under the closeout directory.

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
