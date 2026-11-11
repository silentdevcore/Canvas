# PVAD Rename Review: AI Remediation Handoff

**Latest assignment:** [Follow-up review and AI remediation, 2026-11-11](#follow-up-review-and-ai-remediation-2026-11-11).
That section supersedes earlier completion claims for the findings it revisits.
Keep the original review and the remediating agent's report below as history;
neither is a final approval of the current working tree.

## Review Context

Review date: 2026-11-10, Europe/Berlin.
Reviewed target: uncommitted working-tree changes relative to
`89bfe27987c93be1bc230e6d34fbe8f787482072` on
`chore/pvad-release-readiness`. Product version: `1.1.0`.
The review covered 580 tracked changed/deleted files and the accompanying new
files. These are scope counts, not a count of defects.

**Review disposition: changes requested; not ready for commit or merge.**

The review was read-only. The findings below have not been corrected by the
reviewer. This handoff records the review and assigns remediation; creating it
does not make the underlying changes pass validation.

Technical identity: **PVAD**. Market and Designer identity: **PVADE**.
This document uses `{L}` for the former short name and `{E}` for its expanded
product identity, following the repository Markdown naming rule. Resolve exact
retired values from the reviewed source, Git baseline and protected registers;
do not invent replacement values.

## Binding Decisions And Scope

Read the [owner-approved hard-cut checklist](PVAD.Legacy-Name-Retirement.md)
before implementing anything. Its 2026-11-09 decisions supersede the previous
one-major compatibility period for the explicitly retired families.

- Canonical-only API, cookies, headers, scopes and project formats are intentional.
  Do not restore all legacy aliases to make old tests pass.
- Session, token and browser-state invalidation is accepted. Loss of encrypted
  mail payloads, revocation evidence or customer document contents is not.
- Stored operational data must be migrated, not discarded. Audit and accepted
  Legal history, signed evidence and published releases remain unchanged.
- Original signed/hashed fixture files may retain old identities. Reading those
  evidence files in a test tool is distinct from restoring a retired public API.
- External cutover and publication remain outside this assignment.

## Assignment

Resolve all eight findings below in one coordinated remediation effort. First
reproduce each issue against the current checkout; another agent may have changed
the files since this review. If already fixed, report the actual fixing diff and
tests instead of repeating or undoing the fix.

Preserve unrelated user changes. Never inspect, edit, stage or delete private
`.vscode/`, credential files or untracked personal notes. No commit, push, merge,
version bump, publication, installed-database migration or infrastructure change
is authorized by this document. Use disposable stores for destructive tests.

## R1 - P1: Migrate Protected Payloads Before Switching Discriminator

Review location: [PvadDataProtection.cs, line 8](../PVAD.WebApi/Security/PvadDataProtection.cs).

The application discriminator changes from `{E}` to `PVAD`, and old-purpose
fallbacks disappear. Keeping the same keyring does not make ciphertext protected
under the previous discriminator readable under the new one. The submitted SQL
data migration does not perform cryptographic re-protection, and no delivered
maintenance implementation was found in the reviewed change.

Affected consumers: `PvadMailDelivery` calls `Unprotect` on stored outbox payloads;
repeated failures eventually dead-letter pending mail. The revocation provider
treats unreadable cached evidence as unavailable. Accepted session invalidation
does not authorize either outcome. A local manual conversion, if performed,
does not replace a reproducible delivered conversion procedure.

- [ ] Implement an explicit, bounded offline re-protection procedure for existing
  mail payloads and revocation evidence, using the original discriminator and
  supported purposes for reading and canonical protection for writing.
- [ ] Validate backups, key access, consumer stop/start ordering, dry-run counts,
  already-converted inputs, restart safety, verification and rollback. Never log
  plaintext, tokens, key material or customer data.
- [ ] Refuse activation when required conversion is incomplete; do not solve this
  by silently discarding mail/evidence or permanently restoring public aliases.
- [ ] Test original protected inputs, both historical payload-purpose generations
  where applicable, canonical inputs, wrong keys, interruption, idempotency and
  post-conversion consumption. Tests must not protect and unprotect exclusively
  with the new discriminator and call that a migration test.

## R2 - P1: Align Retention Writer With The Database Trigger

Review location: [PvadEInvoiceValidationEvidenceService.cs, line 256](../PVAD.WebApi/Services/Pdf/PvadEInvoiceValidationEvidenceService.cs).

The service now sets `pvad.e_invoice_retention_purge`. The function installed by
`20260927050514_AddEInvoiceValidationRecords` still checks the previous setting.
No submitted follow-up updates that function. On PostgreSQL, valid bounded
retention deletes therefore fail with SQLSTATE `55000`.

- [ ] Add a follow-up migration that changes the trigger function to recognize
  the canonical transaction-local setting. Do not edit the historical migration.
- [ ] Keep ordinary DELETE and every UPDATE blocked; retain bounded retention,
  transaction-local scope and append-only evidence guarantees.
- [ ] Test a database created by all migrations and an upgraded disposable
  database: expired eligible rows delete, retained rows remain, unprivileged
  mutation fails, and the flag cannot leak into a subsequent transaction.
- [ ] Define and test the rollback behavior of the follow-up function change.

## R3 - P1: Repair PAdES Evidence Resolution

Review location: [corpus-contract.mjs, line 56](../tools/pdf-trust-pades-interop/corpus-contract.mjs).

The two candidate filenames are now identical because the replacement no longer
translates between naming generations. Existing canonical files produce two
active candidates and fail `active.length === 1`; original retained evidence
produces none. Both failures were reproduced. The negative manifest builder and
the full generation/validation script use this resolver.

- [ ] Resolve a single complete evidence generation, with explicitly bounded
  support for reading original retained evidence. Do not rename signed PDFs/XML
  or mix files from different generations.
- [ ] Review the whole modified module: original-ID normalization is now a no-op;
  original generator recognition was lost; the profile-normalization block uses
  the same key on both branches and deletes the canonical profile afterward.
- [ ] Add tests for canonical-only files, original-only files, missing, incomplete
  and mixed generations, and the chosen explicit ambiguity policy when both exist.
- [ ] Assert that classified profiles survive reading and original inputs remain
  byte-identical. Exercise `build-negative-manifest.mjs`, not only helper counts.

## R4 - P2: Update The Required Branding Browser Test

Review location: [visible-branding-browser.mjs, line 52](../tools/configuration/visible-branding-browser.mjs).

The Company About page deliberately removes `#rename-history`. The browser
test still waits for that element, expects a self-rename sentence and requires
one former-brand occurrence. The mandatory CI step runs this script after fresh
builds, so the current assertions cannot pass.

- [ ] Test the approved About page without the deleted notice. Verify current
  branding and absence of obsolete presentation instead of requiring old text.
- [ ] Retain coverage of Company, Documentation, Demo, Account, Admin, Designer,
  release presentation and localized access-denial states.
- [ ] Build current assets and run desktop/mobile browser checks. Do not use an
  old dist directory that happens to retain the removed section.

## R5 - P2: Correct Grafana Profile Classification

Review location: [rename-grafana-provider.mjs, line 141](../tools/configuration/rename-grafana-provider.mjs).

`identity === 'PVAD'` now labels the current provider as legacy, while the
retained original provider is rejected. Inventory preparation and dependent
Grafana/operator rehearsals fail. Ten failures in the broad Node selection were
traced to this area; the checklist already acknowledges unfinished R22 work.

- [ ] Decide which canonical preparation/rehearsal behavior remains required
  after the hard cut, then update profile selection, inventory, consumers and
  tests coherently. Current profiles must be idempotent, not a migration target.
- [ ] Preserve fail-closed checks for unexpected organization, placement,
  datasource/dashboard references, folder policy and destructive operations.
- [ ] If obsolete transition tools are removed, review every remaining caller,
  CI job, guide and replacement recovery test; do not merely delete failing tests.
- [ ] Pass the Grafana provider, Grafana cutover and operator browser rehearsal
  suites and the broad tooling selection without changed-identity failures.

## R6 - P2: Generate Valid Canonical Prometheus Scenarios

Review location: [pvad-telemetry-contract.mjs, line 48](../tools/configuration/pvad-telemetry-contract.mjs).

Mechanical replacement changed two distinct identities into `['pvad', 'pvad']`.
The generated fixture duplicates resource series, expected alerts and samples.
The final heartbeat case assigns different values to the exact same series.
CI passes these generated fixtures to `promtool`; Node shape tests alone do not
establish that the rule tests are valid.

- [ ] Replace the dual-generation matrix with meaningful canonical scenarios,
  unrelated controls, fresh/stale series and unique instance/label identities.
- [ ] Assert uniqueness of input series, expected sample labels and expected
  alerts, and remove obsolete duplicate test cases rather than duplicating labels.
- [ ] Generate the actual rule-test document and pass `promtool check rules` and
  `promtool test rules` using the CI-pinned image with no network during execution.
- [ ] Keep behavioral checks for CPU/memory, exporters, TLS and heartbeat recovery;
  do not weaken expressions, expected alerts or thresholds to obtain a pass.

## R7 - P2: Limit Data Migration To Exact Diagnostic Fields

Review location: [RetireFormerNameData.cs, line 49](../src/Infrastructure/PVAD.Infrastructure.Persistence/Migrations/20261109201328_RetireFormerNameData.cs).

The migration applies a prefix regex to complete `DiagnosticsJson` documents and
`FailureReason` text. That also changes messages, property names, source excerpts
and filenames matching the pattern, not just known diagnostic codes. It conflicts
with the instruction to preserve customer/source content and to use an exact map.

- [ ] Parse structured diagnostic data and change only known code fields using
  the reviewed exact mapping. Unknown codes and all other strings remain intact.
- [ ] Define safe handling of unstructured failure text; do not replace arbitrary
  matching substrings. Test null, nested, malformed and unknown diagnostic data.
- [ ] Test filenames, customer text and source snippets containing the previous
  prefix; verify unrelated values, keys, arrays and properties are unchanged.
- [ ] Account for the checklist's statement that this migration was already
  applied locally. Do not silently rewrite installed migration history or assume
  changed SQL will rerun. Agree on a follow-up/repair strategy using backed-up
  original values; any local reset or restoration needs separate authorization.
- [ ] Verify up, rerun/restart and rollback on mixed original/canonical data;
  generic prefix inversion is not proof of exact data restoration.

## R8 - P2: Replace The Impossible Proxy Conflict Scenario

Review location: [operator-proxy-smoke.mjs, line 14](../tools/configuration/operator-proxy-smoke.mjs).

The conflict scenario now supplies the same canonical Docker environment variable
twice. Docker selects its last value, which is a valid hostname. The removed
alias-conflict mechanism cannot emit the expected error mentioning the same
variable twice, so this operational smoke script cannot pass as written.

- [ ] Replace obsolete alias scenarios with valid canonical configuration and
  real invalid-value cases accepted by the current entrypoint contract.
- [ ] Verify missing/invalid host and upstream rejection without echoing secret
  values; retain syntax, routing and upstream-protection assertions.
- [ ] Run the real smoke script against the selected local image, using read-only
  mounts and isolated resources. Do not restore retired environment aliases.

## Baseline Test Evidence And Limits

The reviewer ran the 59 existing tracked `tools/` and `deploy/` `.test.mjs` files
that remained present. That selection executed **341 tests: 329 passed and 12
failed**. Ten failures were Grafana/profile-dependent regressions. Two registry
tests initially failed because the sandbox denied loopback listeners; all three
registry tests subsequently passed in a separate permitted local-only run.
Do not add that third already-passing test to the unique total.

The newly added former-name-guard tests also passed in a focused run. Canonical
and original PAdES resolution failures were reproduced with direct helper calls;
the canonical profile was also shown to disappear from the reader result.
`git diff --check` passed.

This review did not freshly execute the full .NET suite, PostgreSQL upgrade and
retention tests, authenticated browser suite, containerized Prometheus tests or
real mail delivery. Source-proven failures must not be described as successful
end-to-end tests. Earlier 573-test results predate the hard cut; many tests were
removed, and that historical count is not evidence for the present tree.

## Coordinated Execution And Final Acceptance

- [ ] Start by recording current HEAD, changed files and exact reproduction results.
  Confirm the supplied hard-cut decisions without reopening unrelated old work.
- [ ] Prioritize R1/R2 data safety, then R3 evidence tooling, then R4-R8. Assign one
  integrator to migrations, shared contracts and guard/allowlist changes.
- [ ] Add focused regression tests for every finding; run them before the full
  selection. Keep application behavior, authorization and original evidence intact.
- [ ] Rerun all current tooling tests, including newly added untracked test files
  explicitly; a Git-tracked selection alone does not include them yet.
- [ ] Run relevant .NET, isolated PostgreSQL, frontend/MCP tests and builds, actual
  browser and container validations. Report unavailable dependencies and skipped
  tests separately; none may be relabeled as successful.
- [ ] Review original-evidence preservation independently of the allowlist. The
  submitted diff also alters dated reviewer labels and redaction-fixture source
  markers: determine whether those are protected originals and restore original
  records or create explicit successors where necessary. Do not silently repin.
- [ ] Update the retirement checklist and current handoff with actual results.
  Preserve old execution records; distinguish review findings from completed fixes.
- [ ] Use the repository release-author skill for any implementation fragments.
  Keep VERSION and published releases unchanged; existing breaking work must not
  be relabeled as a compatible patch merely because these fixes repair defects.
- [ ] Return a finding-by-finding report: changed files, reproduction, fix, tests,
  preserved hashes, remaining limitations and new issues discovered during repair.
- [ ] Submit the complete diff for final independent review. Do not commit, push,
  merge, publish, apply installed-data migrations or activate external cutover
  without a separate explicit request.

This review handoff supplements the
[full remaining-work handoff](PVAD.Rename-Final-Handoff.md). Where that older
document still describes a one-major compatibility period, the later owner
decision in the [hard-cut checklist](PVAD.Legacy-Name-Retirement.md) is authoritative.

## Remediation Report (2026-11-10)

Recorded by the remediating agent against `89bfe27987c93be1bc230e6d34fbe8f787482072`
plus the uncommitted working tree. The findings and checkboxes above are the
review record and stay unchanged; this section reports what was actually done.
Nothing was committed, pushed, merged or published.

| Finding | Status | Fix | Evidence |
| --- | --- | --- | --- |
| R1 | Fixed; local data partly open | New offline `PvadProtectedDataReprotection` (`--reprotect-protected-data [--dry-run]`). It reads with the `{E}` discriminator and both historical purposes, writes canonically, verifies each entry, is restart-safe and logs IDs only. Startup refuses while convertible entries remain. Operator steps are in the [migration notes](../docs/pvad-rename-migration-notes.md). | `PvadProtectedDataReprotectionTests` (2) cover original payloads under both purposes, canonical payloads, the wrong keyring, dry run, run, rerun, cache conversion and consumption. Local run, owner-approved after a test on a disposable copy, with database and keyring backed up first: 79 mail payloads converted and verified. 9 are readable only with a key held in the old container volume; that key is not merged without separate approval. |
| R2 | Fixed; not applied locally | Follow-up migration `20261110193750_UseCanonicalRetentionPurgeSetting` replaces the trigger function; `Down` restores the previous check. The historical migration is unchanged. | Postgres test on an upgraded database: eligible rows purge, retained rows stay, plain DELETE/UPDATE fail, and the flag does not leak into the next transaction. It also covers rollback. All existing immutability tests pass on a database built from all migrations. Applying it to the installed local database needs approval. |
| R3 | Fixed | `corpus-contract.mjs` restored to its baseline bounded reader (current IDs, original `{l}-pades-*` evidence read-only). | New `corpus-contract.test.mjs` (6 tests), added to CI: current-only, original-only, missing, incomplete, mixed and both-present ambiguity; profiles preserved; inputs unchanged. A real `build-negative-manifest.mjs` run on a copy reproduces the published entries and trust evidence byte for byte, and published evidence is unchanged against HEAD. |
| R4 | Fixed | Every application asserts that `#rename-history` is absent and that no former name appears in the visible text. | All five websites and the Designer were built fresh: 12 desktop/mobile cases plus 12 localized Designer denial cases pass. |
| R5 | Open | Owner decision pending (options A/B/C, C recommended). | The 10 remaining tooling failures are exactly this area. |
| R6 | Fixed | Canonical scenarios: busy and quiet PVAD services, unrelated controls, down and up jobs, expiring and renewed TLS, a stale heartbeat with recovery, and a fresh replica beside a stale replica. Uniqueness is asserted in the generator. | `promtool check rules` (4 rule files) and `promtool test rules` pass with `prom/prometheus:v3.12.0`, `--network none`, read-only. |
| R7 | Fixed in place (owner-approved) | Exact map of 533 codes; only code fields change; originals are backed up for `Down`. | `RetireFormerNameDataMigrationTests` (Postgres): up, down with structural restore, and re-up. Locally applied data was checked: only 3 exact code values had changed. |
| R8 | Fixed | Valid configuration plus missing or invalid host and API/UI upstreams; rejected values are checked for no echo; the rendered config is asserted for routing and header stripping. | 6/6 against `nginx:1.29-alpine` with `--network none`, read-only, tmpfs. |

Original evidence: dated reviewer labels in
`product-metadata/document-fidelity/real-world-review-decisions.json` and the
redaction-fixture source markers were restored to their baseline bytes and are now
pinned (`published-evidence`) in the v2 allowlist. New files containing former names
are listed with reasons; the guard passes with 239 reviewed files.

New issues found during repair:

- The R1 startup check broke the OpenAPI test client, which runs without a database. That client now uses an empty in-memory database; the check itself was not weakened.
- A mechanically renamed duplicate template test asserted deprecation headers on the canonical route. It was removed; the former template prefix is added to the 404 route test.
- `PvadJobBinaryCutoverTests` was orphaned, since its harness and workflow had already been removed. It was deleted.
- The release fragment now names the one-time re-protection step. It stays major/breaking, and the release checks pass.

Test results (this tree):

- Tooling: 61 files including untracked, 350 tests, 340 pass and 10 fail. All 10 failures are in the R5 Grafana area.
- .NET with `PVAD_RUN_POSTGRES_TESTS=1`: all suites pass except API (1200/1205) and ImageAnalysis (180/181).
  - The 4 API failures were fixed as described above. The full API rerun passes 1204/1204 with 0 skipped, after the orphaned test was removed.
  - The ImageAnalysis failure was a runtime limit under parallel load (10.8 s against 5 s). It passes alone and is not related to naming.
- Designer 828/828; MCP 35/35; release checks pass; `git diff --check` is clean.
- Not run: real mail delivery, authenticated browser suite, external cutover.

## Follow-Up Review And AI Remediation (2026-11-11)

### Assignment And Authority

Implement the remaining corrections from this independent follow-up review, then
return the complete diff for another review. Do not repeat already-corrected work
or treat the previous agent's completion claims as independently verified facts.

Review baseline: `89bfe27987c93be1bc230e6d34fbe8f787482072`, branch
`chore/pvad-release-readiness`, plus the uncommitted remediation changes present
on 2026-11-11. The reviewer made no implementation changes. The current owner
request authorizes writing this handoff, not running the remediation now.

The binding decisions and safety boundaries at the start of this document still
apply. In particular, keep the approved hard cut, preserve protected originals,
and do not restore public compatibility aliases merely to obtain green tests.

### Follow-Up Findings

#### F1 - P1: Block Activation When Protected Data Remains Unreadable

Locations:
[startup guard](../PVAD.WebApi/Security/PvadProtectedDataReprotection.cs#L45),
[report predicate](../PVAD.WebApi/Security/PvadProtectedDataReprotection.cs#L20),
[tests](../tests/PVAD.Api.Tests/PvadProtectedDataReprotectionTests.cs), and
[mail consumer](../PVAD.WebApi/Services/Mail/PvadMailDelivery.cs).

The new conversion procedure is useful, but `RequiresConversion` considers only
convertible entries. If all remaining old payloads require a missing key, both
converted counters are zero and `EnsureConvertedAsync` lets the API start despite
nonzero unreadable counters. Pending mail is then retried by the normal consumer
and can reach Dead Letter. The previous remediation report explicitly records
nine mail payloads still dependent on a key in an old container volume. That is
not an accepted loss of data, and those reported local counts must not be treated
as a fresh inspection by this reviewer.

- [ ] Introduce an explicit activation-safety decision covering both conversion
  still required and unreadable mail/cache entries. Keep these reasons distinct
  so operators receive actionable, non-sensitive diagnostics.
- [ ] Refuse activation before background consumers start when either condition
  is unresolved. Do not silently discard, mark delivered, or exhaust retries on
  protected payloads that cannot be read.
- [ ] Keep offline maintenance available while normal startup is blocked. Preserve
  current payloads, bounded processing, idempotency, and failure exit codes.
- [ ] Test a report containing only unreadable mail, only unreadable cache,
  convertible plus unreadable entries, and entirely current entries. Exercise the
  actual startup guard, not just its counters.
- [ ] Test recovery after restoring a synthetic missing key and running conversion;
  startup may succeed only after all required content is readable canonically.
- [ ] Update operator instructions to match the actual startup behavior. Accessing
  or merging real old-volume keys still requires separate owner approval.

#### F2 - P2: Apply Retention To The New Job Backup Data

Locations:
[backup table and insertion](../src/Infrastructure/PVAD.Infrastructure.Persistence/Migrations/20261109201328_RetireFormerNameData.cs#L170),
[content and metadata purge](../PVAD.WebApi/Services/Jobs/PvadJobRetentionService.cs),
and [migration tests](../tests/PVAD.Api.Tests/RetireFormerNameDataMigrationTests.cs).

The migration now limits rewriting to exact code fields, but backs up each
affected job's complete diagnostics and failure text in
`administration.former_name_job_backup`. That table has no retention integration
or deletion relationship. Normal job content purge clears the live diagnostics;
metadata expiry deletes the live job. Neither removes the backed-up content, so
the change retains data beyond the existing job retention contract.

- [ ] Define a bounded backup lifecycle consistent with content deletion,
  metadata deletion, organization boundaries, and legal holds. Document any
  rollback limitation caused by intentional data deletion.
- [ ] Ensure content purge also removes the corresponding backed-up content;
  deleting backup rows only when the live job is deleted is insufficient.
- [ ] Ensure job deletion cannot leave orphaned backup records. Keep updates
  atomic where required and handle the migration/table lifecycle safely.
- [ ] Add disposable PostgreSQL tests for download-triggered purge, scheduled
  content expiry, metadata deletion, and held organizations. Verify both the live
  table and the backup, not merely the public API response.
- [ ] Do not solve this by increasing retention, retaining full diagnostics in
  another unbounded table, or exporting customer content into repository files.

#### F3 - P2: Do Not Blindly Restore Stale Job Diagnostics During Rollback

Location:
[Down restoration](../src/Infrastructure/PVAD.Infrastructure.Persistence/Migrations/20261109201328_RetireFormerNameData.cs#L222).

`Down()` restores backed-up fields by job ID without checking whether the live
job still contains the migration's output. A retry or later processing can update
those fields after `Up()`; retention can clear them. Rolling back then overwrites
newer results or resurrects content that was deliberately purged. The existing
immediate Up/Down test does not exercise either situation.

- [ ] Define a rollback strategy that distinguishes untouched migrated fields
  from subsequent processing and retention changes. Preserve newer values and
  never recreate purged content.
- [ ] Use a bounded before/after comparison, revision check, or another reviewed
  strategy; do not infer safety from the job ID alone. Coordinate this design
  with F2 rather than introducing another unrestricted backup.
- [ ] Test unchanged Up/Down restoration, a job updated after Up, a retried job,
  content purge before Down, deletion before Down, and Up/Down/Up.
- [ ] Document non-reversible cases honestly. Do not claim exact restoration
  where intentionally deleted content no longer exists.

The data migration has reportedly already been applied locally and was edited
in place with a previous owner approval. That approval is not permission for a
new installed-data rewrite. Inspect migration history and deliver an appropriate
follow-up/repair strategy; test it only on disposable databases. Keep the
historical e-invoice migration unchanged.

#### F4 - P2: Finish The Still-Broken Grafana Profile Contract

Locations:
[classification](../tools/configuration/rename-grafana-provider.mjs#L141),
[profile validation](../tools/configuration/rename-grafana-provider.mjs#L126), and
the R5 finding above.

This finding is not new. `identity === 'PVAD'` marks the current provider as
legacy, while the validation rejects any other identity. Current-profile
inventory checks therefore cannot succeed. Ten tooling tests still fail in the
Grafana/provider/rehearsal area. A pending live provisioning decision does not
make the current classification correct.

- [ ] Make current-profile preparation idempotent and consistent with its declared
  source path. Distinguish any explicitly retained original-evidence reader from
  the current deployment contract.
- [ ] Reconcile classification, inventory preparation, dependent rehearsals, and
  tests. Preserve strict policy, datasource, dashboard, and placement validation.
- [ ] Follow the owner decision recorded for R5 before removing transition tools
  or selecting a live provisioning policy. If still undecided, request that
  decision and report the exact blocked operation; do not apply changes to a live
  Grafana instance or delete failing tests to hide the problem.
- [ ] Pass provider, inventory, cutover, and operator-rehearsal regression tests,
  followed by the complete current tooling selection.

### Status Of The Original Eight Findings

| Original finding | Independent follow-up result |
| --- | --- |
| R1 | Conversion delivered, but activation safety remains incomplete; resolve F1. |
| R2 | Follow-up trigger migration and tests present; installed-local application remains a separately authorized operation. |
| R3 | Corrected; six focused PAdES tests passed in the follow-up review. |
| R4 | Browser assertion corrected in source; the reviewer did not rerun the browser suite. |
| R5 | Still open; resolve F4. |
| R6 | Scenario generation corrected; six telemetry contract tests passed. The reviewer did not rerun real promtool. |
| R7 | Exact diagnostic-field mapping delivered, but backup retention and rollback need F2/F3. |
| R8 | Realistic canonical configuration scenarios present; the reviewer did not rerun nginx. |

### Independently Executed Follow-Up Checks

- Current tooling selection, including new test files: 61 files, 350 tests,
  338 passed and 12 failed in the initial sandboxed run. Ten failures are actual
  Grafana-related failures; two came from denied loopback listeners.
- All three registry smoke tests passed in a separate permitted local-only run.
  The third had already passed initially: the combined unique result is 340 of
  350 passed, not 341. Do not double-count reruns.
- Focused PAdES, telemetry, and former-name-guard run: 15/15 passed. These tests
  were already included in the tooling selection.
- Existing Release-binary `PvadProtectedDataReprotectionTests`: 2/2 passed after
  permitting VSTest's local communication port. This was not a fresh compilation
  of the checked-out source and does not prove that F1 is fixed.
- `git diff --check` passed.

The reviewer did not rerun a full .NET build, PostgreSQL migration suite, real
Prometheus/nginx execution, authenticated browser suite, frontend production
builds, or external cutover. Previous-agent runs remain useful reported evidence,
not fresh results from this follow-up review.

### Execution Order And Completion Report

- [ ] Reproduce F1-F4 against the actual current tree and record HEAD, commands,
  exit codes, and any changes made since this handoff.
- [ ] Fix F1 first. Design F2 and F3 together, with one integrator owning the data
  migration, retention, and rollback contracts. Complete F4 in the same effort
  where no additional owner decision is required.
- [ ] Build current source before rerunning relevant .NET tests. Run new startup
  tests and real disposable PostgreSQL retention/rollback tests; do not substitute
  the previous existing-binary run for this validation.
- [ ] Rerun all current tooling tests, including untracked additions, plus the
  affected actual promtool/nginx/browser checks. Verify that R3, R4, R6, and R8
  remain corrected. Report skips and infrastructure failures separately.
- [ ] Run naming/allowlist guards, release checks, relevant frontend/MCP tests and
  builds, and `git diff --check`. Do not weaken checks or silently repin protected
  original evidence. Preserve VERSION and the published release manifest.
- [ ] Append an implementation report without rewriting the prior review or test
  history. For each F1-F4, list reproduction, changed files, regression tests,
  actual results, remaining limitations, and any owner approval still needed.
- [ ] Update the retirement checklist only with verified outcomes. Use the
  release-author skill for implementation fragments; this documentation-only
  handoff remains covered by the existing internal `impact: none` fragment.
- [ ] Return all changes for independent review. Do not commit, push, merge,
  publish, touch the installed database/keyring, or activate live infrastructure
  without a separate explicit instruction.

**Disposition: changes requested.** The rename remediation is not approved for
closure until F1-F4 are corrected and the applicable checks pass; unresolved
operational approvals must remain explicitly open rather than being counted as
completed implementation work.

### Implementation Report (2026-11-11)

Recorded by the remediating agent. HEAD is still
`89bfe27987c93be1bc230e6d34fbe8f787482072`. Nothing was committed, pushed, merged
or published. The follow-up findings and checkboxes above are unchanged.

Owner decisions given on 2026-11-11 for this run:

1. Merge the old container key into the local keyring.
2. Apply the pending migrations to the local database.
3. Remove the job backup table, correct `RetireFormerNameData` in place again and
   add a follow-up drop migration.
4. Option C for R5/F4.

#### F1 - Fixed

- **Reproduction:** `RequiresConversion` ignored the unreadable counters. Before
  the key merge, the local dry run reported 9 unreadable mail payloads and no
  convertible ones, so the previous guard would have allowed startup.
- **Fix:**
  - `PvadReprotectionReport.ActivationBlockers` returns the distinct reasons
    `conversion-required`, `mail-unreadable` and `revocation-cache-unreadable`.
  - `EnsureActivationSafeAsync` refuses activation with counts and actions only;
    entry IDs come from a dry run.
  - `Program.cs` calls the guard before `app.Run`, that is, before any hosted
    service starts. It is skipped only in `Testing`.
  - `--reprotect-protected-data` stays available, with exit code `2` while
    entries remain unreadable.
- **Changed files:**
  - `PVAD.WebApi/Security/PvadProtectedDataReprotection.cs`
  - `PVAD.WebApi/Program.cs`
  - `docs/pvad-rename-migration-notes.md`
  - `tests/PVAD.Api.Tests/PvadProtectedDataReprotectionTests.cs`
- **Tests (7 pass):**
  - The real guard with only unreadable mail, only unreadable cache, convertible
    plus unreadable entries, and only current entries; stored bytes stay unchanged.
  - Recovery: refused while the key is missing; still refused with
    `conversion-required` after the synthetic key is restored; allowed only after
    conversion. The payload is then readable canonically.
- **Local, owner-approved:**
  - Database and keyring were backed up first:
    `artifacts/rename/hard-cut-backup/local-db-before-key-merge.dump` and
    `data-protection-keys-before-key-merge/`.
  - The single key file was copied read-only from the old volume.
  - Dry run: 79 current, 9 convertible, 0 unreadable. Run: 9 converted.
    Verification: 88 current, no activation blockers.

#### F2 + F3 - Fixed

- **Reproduction:** `Up` created `administration.former_name_job_backup` with full
  diagnostics outside retention. `Down` restored by job ID only.
- **Fix:**
  - `Up` still rewrites only the 533 exact codes in the three code-field shapes
    and in whole `FailureReason` values. It keeps no copy of job content.
  - `Down` applies the exact inverse map to the same fields of the jobs that exist
    at rollback time. Newer results keep their content and only their codes are
    translated. Purged content stays null, and deleted jobs stay deleted.
  - Documented limitation: a code that was already current before `Up` also
    returns to its former value on `Down`. That is harmless for the previous
    release, which read both.
  - New `20261111100000_DropFormerNameJobBackup` drops a table left by the
    unreleased intermediate revision. Its `Down` intentionally recreates nothing.
- **Changed files:**
  - `20261109201328_RetireFormerNameData.cs`
  - `20261111100000_DropFormerNameJobBackup.cs` and its `.Designer.cs`
  - `tests/PVAD.Api.Tests/RetireFormerNameDataMigrationTests.cs`
- **Tests (3 pass, disposable PostgreSQL):**
  - Exact Up/Down/Up, with no backup table after `Up`.
  - Already current before `Up`, processed again after `Up`, retried, content
    purged before `Down`, deleted before `Down`, then Up/Down/Up.
  - The drop migration removes a seeded backup table, and rollback does not
    recreate it.
- **Retention:** Because no copy exists, the existing job content purge,
  metadata deletion and legal-hold rules again govern all diagnostics. No
  additional retention integration is needed.
- **Local, owner-approved:**
  - The history shows `RetireFormerNameData` was applied with the original
    revision. That revision never created the backup table (`to_regclass` was
    null), and its job data had been verified earlier.
  - `UseCanonicalRetentionPurgeSetting` and `DropFormerNameJobBackup` were
    applied with `dotnet ef database update`, after the backup above.
  - The trigger function now checks `pvad.e_invoice_retention_purge`.

#### F4 (= R5) - Fixed With Option C

- **Removed:**
  - `rename-grafana-provider.mjs` and its test.
  - `grafana-cutover-smoke.mjs` and its test.
  - The former provider file under
    `deploy/observability/grafana/provisioning/dashboards/` and its allowlist entry.
- **Added:**
  - `grafana-inventory.mjs`: current profile only, with strict exact-match
    validation. It is idempotent and read-only, and keeps the unchanged reference
    inventory with its datasource, dashboard, alert and runbook checks.
  - `grafana-backup-smoke.mjs`: the same isolated SQLite drill without the folder
    rename. It covers baseline, backup, corrupt-archive rejection, restart
    identity, a post-backup write, point-in-time restore and a fresh install.
- **Rehearsal step (d):** `monitoring/grafana-cutover` became
  `monitoring/grafana-backup`. The integrated lab and browser runner mount the
  current provider.
- **Updated:**
  - `monitoring-operator-checks.mjs`, `monitoring-operator-lab.mjs`,
    `operator-browser-rehearsal.mjs`
  - `monitoring-operator-rehearsal.test.mjs`, `operator-guide-contract.test.mjs`
  - `docs/pvad-grafana-provider-cutover.md` (same path, so existing links stay
    valid), `docs/pvad-integrated-monitoring-rehearsal.md` (dated results kept as
    history), `deploy/observability/README.md`
- **Tests:**
  - Inventory 8/8 and backup smoke 9/9 pass.
  - Real `grafana-backup-smoke.mjs` with `grafana/grafana:13.1.0`: passed, with
    restore hash equal to baseline hash and all temporary resources removed.
- **Not run:** `monitoring-operator-rehearsal.mjs` stopped before creating
  resources because `otel/opentelemetry-collector-contrib:0.157.0` is not cached
  locally, and the runner never pulls.

#### Verification (this tree)

- **Tooling:** 61 files including untracked, 349/349 pass with no skips.
- **.NET** (`PVAD.sln`, `PVAD_RUN_POSTGRES_TESTS=1`, fresh build): 52 suites pass,
  including API 1211/1211 with no skips. One ImageAnalysis test exceeded its
  runtime limit under parallel load (5.8 s against 5 s); it passes alone.
- **Designer and MCP:** Designer 828/828, MCP 35/35.
- **R4/R6/R8 still corrected:**
  - Branding browser test: 24/24 cases. It ran on builds from 2026-11-10; no
    website or Designer source changed since then.
  - `promtool` check and test pass offline.
  - nginx smoke 6/6.
- **Guards and release:** Former-name guard passes with 238 reviewed files;
  release checks pass; `git diff --check` is clean. VERSION and the published
  manifest are unchanged.

Owner approvals still needed: none for F1–F4. D9 (Legal publication) and I3 stay
open as before.
