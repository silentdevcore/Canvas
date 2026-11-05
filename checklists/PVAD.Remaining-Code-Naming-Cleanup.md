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
- [ ] Migrate newly generated parser reports and corpus descriptions only with explicit readers and unchanged historical evidence.

## P1 - Protocol Writer Transitions

- [ ] Introduce canonical mail correlation headers with a bounded compatibility policy.
- [ ] Introduce canonical webhook signature, timestamp and idempotency headers with tested receiver compatibility.
- [ ] Review remaining response aliases and browser compatibility writers individually; retain only documented transition requirements.
- [ ] Preserve existing signature algorithms, payload bytes, key IDs, persistent audit contracts and authorization semantics.

## P2 - Monitoring And Deployment

- [ ] Review current monitoring display names separately from dashboard, datasource, alert and folder identities.
- [ ] Apply only an authorized identity cutover with backup, permissions, link, delivery and rollback checks.
- [ ] Review current deployment defaults separately from immutable historical image families and protected environments.
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

### Next Source Batch

Introduce canonical mail and webhook correlation/signature header names with
explicit receiver compatibility and stable payload signatures. Keep the parser
report/corpus transition separate from old report evidence. Review response and
browser writer aliases individually. Monitoring and deployment still require
their own operator-authorized cutover; earlier release gates remain open.

## Acceptance

- [ ] All reviewed current-output defects are fixed and covered by regression tests.
- [ ] Legacy readers remain functional; compatibility is not removed to reduce search counts.
- [ ] Every retained category has a specific reason, not blanket approval of a whole file.
- [ ] No source completion claim depends only on a passing exception ledger.

## Related Plans

- [Repository naming follow-up](PVAD.Repository-Naming-Followup.md)
- [Rename plan](PVAD.Rename-Plan.md)
- [Current output naming](../docs/pvad-current-output-naming.md)
- [Compatibility boundaries](../docs/pvad-rename-exceptions.md)
