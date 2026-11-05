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

### Remaining Operational Work

Only the two unchecked operational tasks below the P2 heading remain in this
scoped checklist: authorize and verify a real monitoring cutover, then obtain
the actual production/external acceptance evidence. The primary rename plan's
eight release gates and 90/98 historical total remain unchanged by this local
source acceptance. Local tests do not authorize either operational step.

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
