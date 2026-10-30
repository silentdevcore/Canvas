# PVAD Repository Naming Follow-up

## Baseline And Scope

Baseline commit: `aa7c18cb`. The tracked-only case-insensitive deep search found
20,917 textual matches in 625 files, 42 matching file paths, and 153 raw byte
matches in 38 binary assets. These are inventory totals, not outstanding defects.
All 274 tracked Markdown files were clear. Private IDE files, untracked notes,
symlinks, ignored outputs and Git history are outside the scan.

Technical names use PVAD; product and Designer names use PVADE. Preserve exact
compatibility contracts and original evidence. Repository-wide zero matches are
not an acceptance criterion. Earlier inventories and release gates remain historical.

## P0 - Current Outputs

- [x] Normalize freely chosen synthetic users, labels, test passwords and runtime-generated certificate subjects together with their consumers.
- [x] Preserve explicit legacy inputs, stable diagnostic expectations and signed original fixtures.
- [x] Write the canonical chart-metadata recognition source and accept the previous value in both backend and Designer normalizers.
- [x] Keep typed recognition and its style projection consistent, without changing chart geometry or confidence.
- [x] Keep project audit event keys and existing format IDs stable; add canonicalFormatId to newly written audit details.
- [x] Leave historical audit rows untouched and document old/new field semantics.

## P1 - Search And Compatibility Boundaries

- [x] Restrict the residual scanner to tracked regular files, excluding private paths and symlinks.
- [x] Scan known source formats despite embedded null bytes and cover the ten baseline matches in the invoice attachment test.
- [x] Keep binary assets outside the textual guard without silently excluding source files.
- [x] Refresh only explicitly reviewed residual hashes and preserve unknown/changed/stale finding failures.
- [x] Keep authentication schemes, operator identifiers, monitoring bindings, wire diagnostics, cookies, headers and legacy routes unchanged.
- [x] Preserve prior Markdown/JSON audit bytes and historical checklist states.

## P1 - Validation And Delivery

- [x] Test mixed-case content and filenames, null-byte source, private/untracked paths, symlinks and binary exclusion.
- [x] Test legacy and canonical chart inputs, immutable source drafts and canonical roundtrip outputs.
- [x] Test unchanged audit actions and legacy fields, additive canonical fields and unchanged existing rows.
- [x] Pass focused .NET, Designer, naming and documentation tests plus typecheck and production builds.
- [x] Pass the Markdown naming gate, structural JSON guard and git diff --check.
- [x] Add a customer-facing Patch fragment without changing VERSION, published releases or live databases.

## Implementation Notes

The [current output guide](../docs/pvad-current-output-naming.md) describes the
canonical chart source and additive audit details. The public chart and native
project guides document the same behavior. Earlier embedded chart hashes are
validated before the recognition source is normalized; modified series remain
rejected. Original metadata bytes are never rewritten during validation.

Synthetic account and runtime signer names are updated with their expectations.
Explicit customer-content preservation tests, compatibility fixtures and the
visual-baseline fixture producer retain their original inputs. In particular,
baseline-bound visible signature subjects are not rebranded by this source batch.

The invoice attachment source keeps its one literal null byte. The ten baseline
matches are now covered by a regression test; the runtime certificate subject is
renamed while stable diagnostic expectations remain intact.

## Verified Results

- Node contract suites: 514 passed, none skipped.
- Designer: 92 suites, 812 tests and one snapshot passed. Typecheck and production build passed.
- Core: 152 passed; Importer: 646 passed; PDF Infrastructure: 57 passed, none skipped.
- Export: 221 passed, none skipped. The five .NET suites total 2,333 passing tests.
- API: 1,257 passed, 80 skipped, no failures. This does not certify the skipped integration scenarios or close external release gates.
- Documentation production build passed, including the updated public chart and project-format guides.
- Structural JSON guard: no unclassified findings. Prior audit bytes and their hash pins remain unchanged.
- Strict Markdown gate: zero matches in tracked files; the two new Markdown files are additionally checked before tracking.
- Authentication/session and monitoring identities, original PDF/certificate assets, Legal records, VERSION and published releases remain unchanged.
- Existing compiler, obsolete-API, native-library-license and bundler warnings are not resolved by this naming follow-up.

## Completion And Remaining Boundaries

The source follow-up is complete. Thirty-nine synthetic test-source files were
renamed together with their expectations; chart and audit regression tests are
additional coverage. The same tracked-text scan now finds 20,704 matches in 624
files, versus 20,917 in 625 baseline files. Counts can increase when explicit
legacy tests are added; raw match reduction is not a compatibility acceptance.

The two deferred operations below remain open. Old wire diagnostics, legacy
readers, historical inventories, original signed assets and baseline-bound test
inputs intentionally retain their exact identities. No release, migration or
deployment is certified by this checklist. Changes are not committed or pushed.

## Deferred Operations

- [ ] Coordinate authentication/session identifier cutover separately.
- [ ] Coordinate operator and monitoring identity cutover with backup and rollback separately.

These deferred operations do not block this source follow-up. No commit, push,
deployment or service restart is authorized by this checklist.
