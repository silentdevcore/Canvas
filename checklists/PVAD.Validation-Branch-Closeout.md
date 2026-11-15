# PVAD Validation Branch Closeout

Started: 2026-11-15 (Europe/Berlin).

## Scope

Finish the remaining API-test diagnostics and operator-browser validation work,
then prepare a draft integration pull request to `develop`. This does not
authorize merging, publishing, deploying, bypassing reviews, or changing VERSION.

The native Linux/x64 and Windows/x64 rendering work is already accepted in
[the visual parity checklist](PVAD.Designer-PDF-Visual-Parity.md#native-platform-results-2026-11-15).
Historical results and the owner's platform-specific image approvals remain intact.

## Review And Corrections

- [x] Review every remaining tracked/untracked work item except private IDE files.
- [x] Replace the duplicate operator-header self-comparison in the API test with
  a canonical-only header assertion; retain real authentication/authorization checks.
- [x] Test that browser validation accepts the canonical pseudonymous identity
  and rejects missing, malformed or retired identity headers, even with equal values.
- [x] Add a reproducible full API runner using an allowlisted child environment,
  disposable PostgreSQL, private owned storage and fresh source rebuild.
- [x] Audit discovery against TRX names, outcomes, counters, timestamps and unique
  execution IDs; allow only the four reviewed late-enumerated exception cases.
- [x] Add negative auditor tests for missing/substituted/duplicate cases,
  unexpected expansion, non-passing outcomes and inconsistent/missing evidence.
- [x] Correct workflow syntax validation to respect explicit/inherited shells
  and Windows matrix defaults. Use the real PowerShell parser without executing
  installation scripts, and retain Bash syntax validation.
- [x] Pass all 214 configuration/API-auditor/versioning Node contracts locally.
- [x] Add the API-auditor contracts to the existing CI tooling step.
- [x] Document prerequisites, isolation, diagnostics, budgets and evidence paths
  in [TESTING.md](../TESTING.md#full-api-diagnostics).

## Execution And Evidence

- [x] Complete the freshly rebuilt, unfiltered API suite with PostgreSQL enabled;
  record concrete case count, discovery expansion, zero skips and assembly hash.
- [x] Execute the real API/PostgreSQL/Nginx/Grafana operator-browser rehearsal,
  including desktop/mobile screenshots, denials, logout/replay, expiry and cleanup.
- [x] Record exact source/evidence hashes and preserve failures as well as fixes.
- [x] Pass active naming/Markdown guards, version/fragment validation and whitespace checks.
- [x] Commit reviewed changes in coherent groups without private IDE files.

## Integration

- [x] Fetch current `develop`/`main` and inspect existing PRs without merging.
- [x] Identify the prior squash: `origin/develop` at `1ffd62cc` has exactly the
  same tree as the original branch's integration checkpoint `a4e9c148`.
  A direct three-way merge of the old history causes historical conflicts.
- [x] Prepare a separate integration branch based on that current `develop` by
  replaying only commits after the identical-tree checkpoint. Keep the original
  branch and its commits unchanged; verify the resulting source tree is identical.
- [x] Push only the integration branch and open a draft PR targeting `develop`.
- [x] Inspect actual integration CI results and report failures/blockers honestly.
  Do not self-approve, bypass protections, merge, publish or deploy.

## Recorded Results

The unfiltered local API run passes 1,429/1,429 concrete cases with zero failed,
skipped or aborted outcomes. Discovery has 1,426 entries; the sole reviewed
four-case theory expansion explains the difference. The rebuilt assembly hash
is `1b0dae894b9b7a2a2fa6169c3d50173d97e9636361641cb24d2cf4c8d4464da8`.
Private diagnostics and the complete audit are under
`artifacts/api-full-validation/2026-11-14T23-24-06.268Z-4e1ada58-98f5-4f78-b06a-a4c228192ec6/`.
This run precedes the narrow compliance follow-up below; do not represent it as
testing a later rebuilt binary. Its original TRX/audit remain unchanged.

The real browser rehearsal at integration source `aede0f1a` passes all eight
scenarios, without API authorization mocks, and records six desktop/mobile
screenshots. The owned temporary resources are removed. Local evidence is in
`artifacts/validation-closeout-2026-11-15/operator-browser/`.

[Draft PR 216](https://github.com/silentdevcore/Canvas/pull/216) targets `develop`.
The 37 replayed commits produce exactly the original source tree
`d2d8fab9ed6f3a24dd547efe44c596ee4bce4885`; the original branch is preserved.
Native parity on that PR source passes both hosts in run 43653601018. The first
main CI run 43653601051 passes version policy, artifact contract, frontend,
dependency security, MCP and visual parity, but fails independent PDF/UA checking.
Supply-chain is consequently skipped, not passed. Preserve that failed run.

## Compliance Follow-Up

- [x] Diagnose the real veraPDF failure: the new normal appearance of a list box
  references an unembedded Helvetica font when no font loader is configured.
- [x] Reject missing/unloadable PDF/UA choice appearance fonts with the existing
  embedding diagnostic and configure the valid widget fixture with packaged fonts.
- [x] Add positive combo/list font-resource tests and negative missing-loader /
  missing-font cases without loosening conformance requirements.
- [x] Cover all unsupported interactive widget families in the existing PDF/A
  no-interactive-forms preflight policy.
- [x] Keep batch validator reports after a nonzero tool exit, continue remaining
  inputs, and reject tool failures even if their partial XML claims compliance.
- [x] Pass current targeted .NET regressions and independent veraPDF validation:
  22 PDF/UA cases, 12 PDF/A cases, 57 API export/code-roundtrip cases, and six
  independently compliant PDF/UA fixtures. Documentation has 73 passing tests
  and a successful production build. Validator wrapper failure-path tests pass.
- [x] Commit the correction separately as `f97ec6ff`; transfer it to the same
  draft integration PR without changing the original branch history.
- [x] Reinspect the new CI run; record its actual status without rewriting the
  first failed run or claiming skipped jobs succeeded.

The [machine-readable evidence](PVAD.Validation-Branch-Closeout.Evidence.json)
pins source versions, assembly/TRX/report hashes, exact counts and the eight real
browser scenarios. It explicitly distinguishes the complete API run before the
compliance follow-up from the 57-case API regression against the corrected build.
Full private logs, screenshots and validator XML stay in ignored artifacts.

## Boundaries

- The active former-name guard uses the reviewed v2 allowlist. The historical
  JSON-audit CLI still checks pre-hard-cut pins and reports stale findings on
  the current source; it is not a passing current-source gate. Preserve its
  original inventory and report rather than rewriting historical evidence.
- Legal/license approvals, public hosting, package publishers, production rollout
  and unrelated dependency PRs remain separate work.
- A successful local API/browser run does not certify all customer environments
  or replace the independent review required before integration.
- Product VERSION and published release entries remain unchanged. Internal test
  work uses `none` release fragments, not a product-version increase.

## Production Startup Follow-Up

The follow-up main CI run 43656096061 passed the complete .NET job, frontend,
MCP, dependency security, version policy, artifact contract and visual parity.
Native run 43656096133 also passed both hosts. All 68 Linux and 88 Windows
reviewed crops, thirteen font hashes, fixtures and the pinned browser match the
explicit owner approvals. The strict final local API run again passed
1,429/1,429 with PostgreSQL enabled and its default database connection blocked.

Supply-chain did **not** pass: its production-startup rejection smoke failed,
so the SBOM steps were skipped. The generic log does not establish the exact
container exit reason. The source inspection nevertheless found a concrete
ordering defect: protected-data activation queried the database before the
retention hosted-service gate could refuse startup.

- [x] Move initialization into the controlled host lifetime and run the retention
  refusal check before data activation, OAuth and development identity bootstrap.
- [x] Dispose the host when initialization fails before Host.RunAsync takes
  ownership; retain unrelated exceptions instead of converting them to exit 78.
- [x] Preserve the offline maintenance command path and the activation check
  before any hosted background consumers start.
- [x] Add focused initialization-order/disposal tests without introducing a
  second non-serializable MemberData expansion into the full-suite audit.
- [x] Add safe failure-phase metadata and an always-on CI report upload without
  publishing startup logs, process arguments or generated private keys.
- [x] Pass the focused .NET startup tests against the new code: 16/16 cases
  confirmed in `startup-order-local-confirmed/startup-order.trx`. The broader
  configuration/API-auditor/versioning command passes 223 Node tests; these
  groups overlap earlier results and are not an aggregate total.
- [x] Reproduce the startup rejection with the actual rebuilt Linux/ARM64
  container: independent exit 78 after 592 ms, diagnostic present, no listener,
  no unhandled exception and all owned resources removed. This is a local
  working-tree image, not a published artifact or Linux/x64 CI certification.
- [x] Repeat the real API/PostgreSQL/Nginx/Grafana browser rehearsal against
  committed startup source `d5ce016a`: eight scenarios passed, six unchanged
  desktop/mobile screenshots, no authorization mocking, owned resources removed.
- [x] Repeat the unfiltered API suite against the startup correction:
  1,435/1,435 concrete cases passed, zero failed/skipped/aborted, 1,432 discovery
  entries with the sole original four-case expansion. The freshly rebuilt
  assembly hash is `f0b76132369b555c3c5299a31bbe25b1654ec5138b36efd212232d3ad1f53ea4`.
  Default database fallback is blocked and the owned temporary storage removed.
  All nine normal startup/canonical routing cases also pass in this complete
  run; the earlier sandbox-aborted attempt is not counted as their evidence.
- [x] Reinspect native CI 43703853878: Linux/x64 and Windows/x64 each pass 118
  visual cases with no P0/P1. Verify the exact approved 68 Linux / 88 Windows
  crop pixels, native tools, runner images, browser, fonts and fixtures. Backend
  and C# are pixel-identical; the measured 17 / 22 P2 findings remain unchanged.
- [x] Reinspect complete main CI 43703853871 on functional source `d5ce016a`:
  all nine non-snapshot jobs passed, including .NET, frontend, version policy,
  artifact contract, registry login, MCP, dependency security, visual parity and
  supply-chain. Snapshot is correctly skipped for a PR. Container startup exits
  independently with code 78 after 714 ms on Linux/x64; owned resources are
  removed. API/Designer and container SBOM generation/upload also passed.
- [x] Commit the locally validated startup correction as `dd5619ee`, replay it
  as `d5ce016a` and push to the existing draft PR. Both committed source trees
  are `8e6f5e570a2fcb8d8db2508a66d61011136ae062`. Keep the PR draft while its
  full API/browser/CI revalidation is in progress; no merge is authorized.

An earlier sandboxed route-test attempt could not open VSTest's communication
socket and executed zero cases; its failed report is retained, not counted as a
pass. Execution approval is available again. The local container check passed;
the real browser rehearsal and the unfiltered API run also passed. Native CI
43703853878 passed Linux/x64 and Windows/x64; main CI 43703853871 passed including
supply-chain. Earlier full-suite results predate this startup correction and
remain preserved separately. The final documentation-only evidence commit is
not retroactively represented as the source of these functional runs: a push of
that commit starts separate PR checks. Independent review and an explicit merge
decision remain required; this checklist is not a release or deployment approval.
