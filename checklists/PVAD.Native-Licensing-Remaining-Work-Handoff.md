# PVAD Native Licensing Remaining Work Handoff

The current execution assignment is the
[complete-to-100 handoff](PVAD.Native-Licensing-Completion-Handoff.md).
It includes the latest uncommitted corrections, two new fuzz crashes/one timeout,
ImageAnalysis reference decision and all six final closure gates. Read it before
using this earlier assignment; historical evidence below remains unchanged.

## Round Two: Reviewed Baseline

Continue the remaining work in the original native licensing/protection plan.
The previous AI implementation has been reviewed and corrected. Do not repeat
completed work or broaden this assignment into unrelated product development.
The reviewing agent will inspect your complete diff, verify evidence and correct
defects after your work is finished.

- Branch: `feature/pvad-native-licensing-and-protection`.
- Reviewed baseline: `b0faa29a0c9c2d37a8ecbcaf95ce04b2720b83d1`.
- Primary plan: [Native Library Licensing And Protection](PVAD.Native-Library-Licensing-And-Protection.md).
- Previous execution and review: [Round One Report](PVAD.Native-Licensing-Remaining-Work-Report.md).
- Progress: **70/76 primary items complete (92.1%); six remain open**.
- Product version: **1.1.0**. No version bump or published-release rewrite.

### Current Checkpoint After Review

The later [final report](PVAD.Native-Licensing-Final-Work-Report.md) and its
numbered supplements supersede the initial assignment's pending observations.
The complete current working-tree Solution now builds in the existing
`PvadOriginalBaseline=true` source mode, including all three owner-added probes;
default package mode still requires the protected local feed. Do not repeat
that build or present it as package/test acceptance without a relevant change.

The upstream repository does contain build instructions at the retained wrapper
commit. A new hash-bound supplement corrects the earlier absence claim. The
instructions do not pin the vcpkg dependency versions, so liblzma attribution
and exact DLL-build provenance remain unresolved. Historical observations stay
unchanged. The six primary tasks are still open, **70/76 (92.1%)**.

The owner then authorized the deferred parser/validation follow-up. The
Importer correction passed the complete 696-case suite and all 37 saved-input
replays. ImageAnalysis still fails four of 181 tests; the owner chose a
deterministic reference environment and explicit review before promoting new
baseline candidates. See final report section 12 for actual scope and evidence.
The old deferred-work instructions below remain historical context, not a
prohibition of this specifically authorized follow-up. No remote execution,
baseline approval or publication authority is implied.

This revision replaces the earlier assignment at baseline `f783ae9a`. Read the
current checkout and the previous report, especially section 8, before working.

## Decisions Already Made

1. Start local engineering and isolated tests. Do not ask again whether local
   work may begin.
2. The owner's commit instruction applied to the reviewing agent's completed
   snapshot, not unrestricted future commits. Do not commit, push, open/review/
   merge PRs, dispatch remote workflows, publish or deploy without a new explicit
   owner instruction for your work.
3. There is no confirmed authenticated Debian keyring available. Do not import
   an arbitrary downloaded key, mark the signature verified or claim the signer
   fingerprint is authenticated. Record the missing trust input as a blocker.
4. The main plan's handoff link belongs to this work. Preserve it.
5. `PVAD.sln` has a separate user-owned 51-line addition, and `.vscode/` is
   user-owned. Do not edit, stage, revert, clean or stash them. Never use broad
   `git add .` or destructive cleanup commands.
6. Newly discovered adjacent improvements are recorded and deferred until the
   original licensing plan is finished. Escalate critical security/data-loss
   findings immediately instead of silently expanding scope.
7. No production keys, customer licenses, live databases or acceptance records
   may be created/changed by this assignment. Disposable test material belongs
   only to the existing isolated test runners and must be cleaned up.

## Read First

- [Native tooling guide](../tools/native-licensing/README.md).
- [Native contract and operation matrix](../docs/native-library-licensing.md).
- [Customer activation guide](../docs/guides/PVAD.Native-Library-Activation.md).
- [Restricted issuer runbook](../operator-docs/PVAD.Native-Library-License-Issuance.md).
- [Runtime attribution guide](../assets/licenses/runtime-assets/README.md),
  [original registry](../assets/licenses/runtime-assets/manifest.v1.json) and
  [supplemental evidence index](../assets/licenses/runtime-assets/source-evidence.v1.json).
- [NPOI decision](PVAD.NPOI-License-Decision.md), with all three options retained.
- `product-metadata/native-library-products.v1.json`, native dependency/entry/
  surface inventories and `product-metadata/dependency-compliance.json`.
- `.agents/skills/pvad-release-author/SKILL.md` before release-fragment changes.

## Completed Work: Preserve, Do Not Reimplement

The ten native product areas and current entry/continuation integrations are
reviewed. There are 35 migration catalog entries: 34 concrete grants and one
unlicensable `auto` routing entry. Existing API, tenant, session, entitlement,
feature and worker authorization remain mandatory alongside native licensing.

The following provenance work is complete within its stated technical scope:

- Five historical ARM64 package copyright mappings and three common-license
  links are resolved from the exact original notice archive. All 162 installed
  packages have a mapped original copyright text. This is not distribution
  approval or a complete static-component inventory.
- All fifteen local macOS libraries have reproducible build/section correlations
  and byte-difference explanations. Their whole-file differences are confined
  to checked header/load-command regions; publisher authenticity is not proven.
- Liberation Sans descriptor checksums cover every supplied archive for all
  three algorithms. Its OpenPGP signature remains unverified.
- Native marker scanning covers eight packages and 45 payloads. It identifies
  a remaining six-component Tesseract attribution gap, not exhaustive coverage.
- Five validation defects are fixed: chain replay, binary Mach-O bounds,
  component-version aggregation, complete checksum lists and strict TAR endings.
- The report now distinguishes local test subsets from full validation and
  correctly records the use of disposable PostgreSQL containers.

Retain the hardened negative tests. Do not loosen them or regress to validating
only record counters, regex-shaped hashes or self-reported success flags.

Preserve the existing native contract: ECDSA P-256/SHA-256, explicit 30-day Trial,
seven-day paid-subscription grace, immutable release cutoffs for perpetual rights,
atomic renewal with failed replacement retaining the prior license, and fresh
admission for public/reentrant calls and worker dispatch/start. No hidden
watermarks, automatic Trials, runtime trust overrides or mandatory internet.

Obfuscar and ILSpy stay pinned. No public API/serializer/DI changes, native asset
replacement, anti-debugger tricks or claims of unbreakable code protection.

## The Six Open Primary Items

| Existing primary item | Remaining agent work | Owner or external input |
| --- | --- | --- |
| Manufacturer public verification keys | Maintain reviewed provisioning/validation instructions and existing negative tests; only prepare a public-register change after authentic approved input arrives | Approved manufacturer public key, key ID and provenance; reviewed provisioning |
| Publisher/commercial/legal decisions | Maintain one concise decision/blocker table and keep publication disabled | Publisher ownership, prices, commercial terms and counsel approval |
| Third-party distribution gates | Complete defensible technical source/component evidence and original-notice collection where possible; preserve NPOI gate | Owner/counsel selects NPOI A, B or C and approves actual distribution obligations |
| Production private-key custody | Confirm existing isolation and document concrete missing custody inputs; avoid redundant synthetic issuer reimplementation | Named custodian, secret storage, backup, rotation and recovery ownership |
| Complete general validation | Complete missing local checks; run available bounded fuzzing; prepare remote commands without executing them | Separate remote authorization, platform infrastructure and human visual acceptance; known deferred failures remain blockers |
| Final package documentation/distribution review | Reconcile real package/source notices, examples, XML/docs and scope of evidence | Final EULA, corresponding-source and distribution approvals |

These remain six composite tasks, not six new implementation projects. Partial
engineering evidence must not close an entire primary item. Technical work
remains possible; the remainder is not exclusively dependent on outsiders.

## Priority 1: Missing Local Application Validation

- [x] Run the full Solution build through the existing isolated signed-session
  tooling, without changing the user-owned Solution file or skipping projects.
  If that file's unrelated changes block the build, report the exact cause and
  request an owner decision; do not remove or silently exclude their projects.
  Completed in source mode; see final report section 10, not default package mode.
- [ ] Run Designer Jest, typecheck and production build. Record actual test
  counts, failures, warnings, process exit status and cleanup. Do not rename
  existing failures into passes because source changes were internal-only.
- [ ] Reconcile relevant roundtrip/visual coverage with the actual candidate.
  A report-only visual run is not enforced regression acceptance or human
  baseline approval. Do not approve new images yourself.
- [ ] Record full runs versus selected tests, runtime architecture, source commit
  and working-tree scope. Avoid rerunning hours of already-passing unchanged
  product checks without a reason, but never call reused evidence a fresh run.

Read the current runner prerequisites before execution. Representative commands:

```bash
node tools/native-licensing/test-session.mjs exec -- dotnet build PVAD.sln --disable-build-servers -m:1 -nr:false -p:PvadOriginalBaseline=true
npm test --prefix pvad-designer -- --runInBand
npm run type-check --prefix pvad-designer
npm run build --prefix pvad-designer
```

The original Mac has a reviewed x64 SDK at
`/usr/local/share/dotnet-patched/10.0.113-x64`. Verify the actual .NET/runtime
architecture; Node's architecture does not establish .NET's. Follow the runners'
no-server/no-node-reuse settings and preserve caller-owned processes/files.

Known ImageAnalysis snapshot/glyph/quality failures remain deferred. Do not alter
algorithms, snapshots, expected text, line endings, fonts, fixtures or thresholds;
do not skip tests to clear the general-validation item. An unfiltered failing run
must stay failing even if subsequent selected tests pass.

## Priority 2: Remaining Native Component And Font Evidence

- [ ] Investigate the six component hints in Tesseract 5.2.0: Leptonica, Tesseract,
  LibTIFF, libjpeg-turbo, zlib and liblzma. Collect exact original notices/source
  evidence from authoritative upstream/package sources where identifiable.
- [ ] Label marker-derived versions as observations, not proof of source revision.
  Conflicting Tesseract strings such as 3.04/5.0.0 do not establish an exact
  embedded source build. Never guess a commit or attach an arbitrary nearby
  version's agreement and claim the gap is resolved.
- [ ] Keep acquired originals byte-exact with URL/revision, hashes, artifact
  association, remaining ambiguity and tests. Candidate source associations stay
  distinct from verified matches and legal approvals.
- [ ] Evaluate the technical corresponding-source/build evidence for Liberation
  Sans using the existing pinned archives. A missing FontForge executable is a
  tooling limitation, not a legal decision. A bounded candidate build may be
  prepared in isolated temporary output if its toolchain is available or can be
  installed with appropriate permission; never replace the shipped fonts.
- [ ] Leave Debian signature verification blocked until an authenticated keyring
  is supplied. Do not generate trust by downloading an arbitrary key or trusting
  the fingerprint read from the unverified signature.
- [ ] Reconcile unresolved static/transitive native components against the actual
  selected dependency graph. Wrapper notices and absence of marker strings do
  not prove full component attribution.

Do not reopen the five resolved container mappings or rewrite historical JSON.
The local ARM64 observation does not establish current-release/amd64 coverage or
a verified build of today's Dockerfile. Any new image observation must have a
separate immutable identity and honest scope. Never log full container
environments or use broad container pruning.

All legal/publication flags remain false. NPOI options A, B and C remain open.
Do not accept its agreement, enable `AcceptNPOIOSMFLicense`, purchase a license,
self-build a replacement or remove legacy XLS capabilities without an explicit
approved decision from the owner/counsel.

## Priority 3: Bounded Fuzz And Remote Preparation

- [ ] Inspect the existing Linux harness in `tools/pdf-parser-fuzzer/` and the
  reviewed `.github/workflows/pdf-trust-fuzz.yml`. Use it rather than inventing
  another fuzz engine or substituting unit tests for a coverage-guided campaign.
- [ ] If an authorized isolated local Linux environment/toolchain is available,
  run the existing bounded campaign with synthetic inputs, signed test sessions
  and cleanup. A missing local AFL++/SharpFuzz installation is not evidence that
  every possible local campaign is externally blocked.
- [ ] If unavailable, record the missing prerequisites and exact prepared command,
  including time/iteration limits. Do not claim a campaign ran because tools
  were installed or workflow YAML exists.
- [ ] Prepare, but do not dispatch, native licensing, platform visual parity,
  general CI and PDF Trust fuzz workflows for the exact future candidate commit.

Relevant workflows:

- `.github/workflows/native-library-licensing.yml`
- `.github/workflows/visual-parity-platforms.yml`
- `.github/workflows/pdf-trust-fuzz.yml`
- `.github/workflows/ci.yml`

The first three support dispatch. General CI has no `workflow_dispatch`; inspect
its current triggers and request an authorized PR if needed. Do not modify
triggers, repository protections or permissions just to run it. Do not approve
your own PR or impersonate an independent reviewer.

Historical run 45052686167 at commit `0006fc79` passed the three protected-package
platform jobs but failed ImageAnalysis. Its overall workflow is not green. Local
package success does not replace complete general or all-platform acceptance.

## Evidence To Reuse Honestly

The previous report contains full paths and counter details. Already verified:

- 239 native/version/release tooling tests, including 28 runtime-source tests.
- 73 Documentation tests and successful Documentation production build.
- Original raw TRX audit: 1,527 API cases and 54 native issuer cases.
- Local package rehearsal: 54 assemblies, 52 libraries, 654 original and 5,850
  protected checks; actual OCR, worker execution and exact PDF comparisons.
- A report-only visual rerun with 39 matches and one P2; first attempt timed out.
- The original native regression runner failed ImageAnalysis; remaining selected
  checks were executed separately and do not turn that run into a full pass.

These are scoped historical results, not automatic acceptance of your changes.
Native/package/enforcement changes require new affected executable tests and
artifact verification. Internal evidence-only edits require tooling and actual
source-derivation checks rather than arbitrary repeated product builds.

The default evidence guard is offline consistency checking. It does not re-fetch
references, authenticate a publisher or recreate historical archive metadata.
Use explicit revalidation modes only when original local inputs exist:

```bash
node tools/native-licensing/runtime-source-evidence.mjs
node tools/native-licensing/runtime-source-evidence.mjs verify-container-links artifacts/native-runtime-provenance/1795374811152-19192c2c/os-notices.tar
node tools/native-licensing/runtime-source-evidence.mjs verify-downloads artifacts/native-runtime-provenance/source-downloads
```

Do not overwrite approved records just to make their pins match new output.

## Owner Inputs: Maintain One Decision Table

Record what is missing without asking for passwords, tokens or private keys:

- Authentic manufacturer public key, key ID and reviewed provenance.
- Manufacturer signing custodian, approved secret storage and backup/rotation/
  recovery responsibilities.
- Package publisher identities/ownership, prices and commercial terms.
- Legal approver for the final EULA, NPOI option and distribution obligations.
- Authenticated Debian keyring and font corresponding-source review.
- Separate remote execution authorization and any human visual acceptance.

No production key generation or infrastructure deployment is authorized.
Keep `TrustedKeys.json` empty until authentic approved input arrives. Existing
synthetic issuer, revocation and protected-package tests do not provision
manufacturer trust. Keep customer deployments verification-only and operator
runbooks excluded from public Documentation output.

## Deferred Work: Do Not Implement

Preserve the main plan's Later Findings list. In particular:

- Ukrainian OCR model repair/replacement: it remains a truncated official prefix
  missing 933,326 bytes. Do not modify or relabel the model.
- ImageAnalysis algorithm/quality and snapshot-portability fixes.
- The older JSON naming audit's 475 pre-existing findings; never refresh its
  historical pins to hide them.
- Operation-specific authorization/audit alignment, existing test analyzer
  warnings, broader revocation administration and unrelated CLI issuer changes.
- New KMS services, online activation, automatic remote revocation, marketplace
  or commercial obfuscator features.

Document new adjacent discoveries with scope/impact and defer them. Escalate
critical findings to the owner. Do not add primary tasks to inflate progress.

## Validation And Review Deliverables

```bash
git status --short
git diff --check
node --test tools/native-licensing/*.test.mjs tools/versioning/pvad-version.test.mjs tools/versioning/pvad-release-author.test.mjs
node tools/native-licensing/pvad-native-licensing.mjs check
node tools/native-licensing/runtime-assets.mjs
node tools/configuration/former-name-guard.mjs
node tools/configuration/markdown-path-audit.mjs --check
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
npm test --prefix websites/PVAD.Documentation
npm run build --prefix websites/PVAD.Documentation
```

Use the release-author skill for your actual logical change. Internal-only work
uses `none` plus a reason; shipped fixes need the appropriate impact. Do not
alter VERSION, published releases, original legal versions, acceptance evidence,
fixture/baseline bytes or customer data. Clean only owned disposable test work.

Create an English report at
`checklists/PVAD.Native-Licensing-Final-Work-Report.md`. Preserve the Round One
report and its review history. Include:

1. Baseline/candidate commits, working-tree scope and exact changed files.
2. Each work item: done, partial, blocked, failed, not run or deferred, with reason.
3. Every command/platform/runtime architecture and raw report path; actual
   counts, exit statuses, skips/timeouts/failures and cleanup.
4. New source/component mappings with before/after coverage and exact original
   hashes; distinguish reproducible evidence from inference and legal approval.
5. All six primary open items reconciled, including technical and human blockers.
   Progress uses the fixed 76-item denominator, not this handoff's task count.
6. Confirmation of unchanged user files, production trust/keys, version/releases,
   live data, original runtime assets/observations and approved fixtures.
7. A short owner decision list and exact unexecuted remote commands.
8. Known risks and focused areas for the reviewing agent.

Do not mark the overall plan 100% complete just because local work is exhausted.
Leave a focused uncommitted diff for review unless the owner explicitly requests
a commit. The reviewing agent will inspect code, rerun relevant tests, audit the
actual evidence and correct defects; neither your report nor green tooling
tests alone constitute final acceptance.
