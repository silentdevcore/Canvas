# PVAD Native Licensing: Complete-To-100 Handoff

## Mission And Definition Of Completion

Finish all remaining work in
[Native Library Licensing And Protection](PVAD.Native-Library-Licensing-And-Protection.md).
The current status is **70/76 primary items complete, 92.1%, six open**.
The target is **76/76, 100%**, with actual implementation, execution evidence
and required human decisions, not a rewritten percentage or reduced scope.

Complete every feasible engineering task and actively collect missing owner,
manufacturer, operator and Legal inputs. Some approvals and production inputs
are not available yet. This handoff does not authorize inventing them, accepting
third-party agreements, self-approving baselines or generating substitute
production trust. If inputs remain unavailable, report their exact blockers;
do not declare 100%. Publication itself is not required or authorized.

This is the current execution handoff. It supersedes the pending-task guidance
in the [earlier handoff](PVAD.Native-Licensing-Remaining-Work-Handoff.md), not its
historical evidence. Preserve previous reports. A separate reviewing agent will
review your complete changes and correct defects after your implementation.

## Checkout And Uncommitted Work

- Branch: `feature/pvad-native-licensing-and-protection`.
- Handoff starting HEAD: `73a522e0796863c40ed01be295d16462b624cee3`.
- VERSION: `1.1.0`; do not bump it or rewrite published releases.
- The candidate includes uncommitted work. HEAD alone does not contain the
  latest source-evidence correction or parser fixes. Inspect `git status`,
  staged and unstaged diffs and untracked files before starting.
- `PVAD.sln` has an owner-owned **51-line addition** with three probe projects.
  `.vscode/` is also owner-owned. Do not edit, stage, revert, stash or delete
  either. Do not remove projects or bypass them to obtain a green build.
- Do not commit, push, open/merge/review PRs, dispatch remote workflows, deploy
  or publish without a separate explicit owner instruction for your work.

Preserve and continue these pre-existing implementation changes:

| Area | Files present at handoff |
| --- | --- |
| Exact upstream build-instruction supplement | `.gitattributes`; `assets/licenses/runtime-assets/README.md`; `assets/licenses/runtime-assets/source-evidence.v1.json`; new `assets/licenses/runtime-assets/component-sources/tesseract-5.2.0/build-instructions.txt` and `build-instructions.v1.json` |
| Evidence validator and negative tests | `tools/native-licensing/runtime-source-evidence.mjs` and `runtime-source-evidence.test.mjs` |
| Parser fixes and new regression tests | `src/Importing/PVAD.Importer/Graphics/PdfGraphicsInterpreter.cs`; `src/Importing/PVAD.Importer/Streams/PdfStreamDecoding.cs`; new `tests/PVAD.Importer.Tests/PdfMalformedContentTests.cs` |
| Progress, execution evidence and earlier handoff | `checklists/PVAD.Native-Library-Licensing-And-Protection.md`; `PVAD.Native-Licensing-Final-Work-Report.md`; `PVAD.Native-Licensing-Remaining-Work-Handoff.md` |
| Pending fragments | `product-metadata/release-fragments/native-upstream-build-instruction-evidence.json`; `pdf-malformed-content-reliability.json` |

This new handoff and its links/internal fragment are additional planning edits.
Re-read the actual checkout if commits have since been added. Do not overwrite
another contributor's subsequent changes.

## Read First

1. [Primary plan](PVAD.Native-Library-Licensing-And-Protection.md), including the
   current evidence and Later Findings sections.
2. [Latest execution report](PVAD.Native-Licensing-Final-Work-Report.md), especially
   sections 9 through 12; section 12 includes the failed fresh fuzz campaign.
3. [Earlier report](PVAD.Native-Licensing-Remaining-Work-Report.md), as history only.
4. [Tooling guide](../tools/native-licensing/README.md),
   [operation contract](../docs/native-library-licensing.md),
   [activation guide](../docs/guides/PVAD.Native-Library-Activation.md) and
   [restricted issuer runbook](../operator-docs/PVAD.Native-Library-License-Issuance.md).
5. [NPOI decision](PVAD.NPOI-License-Decision.md),
   [runtime source guide](../assets/licenses/runtime-assets/README.md), original
   runtime registry and supplemental evidence index in that directory.
6. `product-metadata/native-library-*.json`, native entry/surface inventories,
   `product-metadata/dependency-compliance.json`, relevant workflows and
   `.agents/skills/pvad-release-author/SKILL.md`.

## Preserve The Implemented Contract

Do not reimplement completed licensing or introduce a competing licensing system.
Keep ECDSA P-256/SHA-256 with explicit signature encoding, exact signed payload
bytes, known embedded verification keys, strict bounded parsing, explicit
30-day Trials, seven-day paid-subscription grace, perpetual release cutoffs,
atomic renewal and offline operation. No automatic Trial, hidden watermark,
mandatory telemetry, runtime-supplied trust anchor or public server-mode bypass.

All ten current native product areas and 35 migration catalog entries are
covered by existing integration inventories: 34 concrete grants and `auto` as
unlicensable routing. Preserve direct-engine, provider, retained-handle,
callback and worker admission checks. Keep contracts/DTOs free and avoid
unexpected extra internal-dependency purchases.

User, tenant, session, entitlement, feature and authorization checks still apply
in addition to native licensing. Native licenses, server offline licenses,
account editions, application roles and seats remain separate concepts.

Keep Obfuscar 2.2.50 and ILSpy 11.2.0.10050 pinned unless a separately justified
change is necessary. Preserve public APIs, serialization, DI, reflection,
resources, friend boundaries and generated C#. No anti-debugger crashes,
process scanning, self-modifying loaders or claim of unbreakable protection.

## Closure Matrix: All Six Remaining Primary Items

| Primary item | Engineering to finish | Required human/external input | Evidence required to close |
| --- | --- | --- | --- |
| Manufacturer public verification keys | Validate authentic approved input; provision the embedded public register through a reviewed build; verify valid, foreign, expired and rotated-key behavior | Manufacturer public key, key ID, provenance and provisioning approval | Reviewed public-register change and passing verifier/package/worker tests using the intended trust configuration; synthetic trust does not count |
| Publisher/commercial/legal decisions | Reconcile package identities, entitlement/redistribution terms and documentation with the recorded decisions; retain publication blocks | Confirmed publisher ownership, commercial terms, developer/OEM rights and qualified Legal approval | Named decision owner, date and approved evidence references; no required decision left implicit |
| Third-party distribution gates | Complete exact dependency/source/notices work and implement only the approved NPOI option across affected graphs | NPOI A/B/C decision, any payment/exemption, authenticated font source trust and distribution review | Actual selected dependency graph, matching notices/SBOM/source obligations, tests and human approval; marker/candidate evidence alone is insufficient |
| Production private-key custody | Validate manufacturer-only isolation, permissions, recovery procedure and support-artifact custody against approved deployment arrangements | Named custodian, secret storage, backup, rotation, recovery and support-map retention ownership | Recorded operator sign-off and safely evidenced custody/recovery controls; no private material in repo, packages, images, reports or customer deployments |
| Complete general validation | Fix all remaining parser/ImageAnalysis findings, validate current protected packages and complete required application/platform/visual/fuzz runs | Remote execution authorization, actual platform capacity and human baseline review | Exact candidate-bound green runs with full result auditing, no unexplained failures/skips, no unreviewed replacement baseline and successful cleanup |
| Final package documentation/distribution review | Inspect real final packages, XML/API docs, activation/offline examples, license references, notices, dependency closure and protected outputs | Final EULA, corresponding-source/distribution decisions and distribution-review sign-off | Reproducible package/content checks, independent local-feed consumers and approved final documentation/compliance records |

Do not mark an entire row complete after only its engineering preparation passes.
The main denominator stays 76. Closing one complete row gives 71/76 = 93.4%;
two give 72/76 = 94.7%; all six give 100%. Do not add subtasks to inflate it.

## Workstream A: Finish Parser Robustness First

### Corrections Already Present

- Validate count/types of `Tj`, `TJ`, single-quote and double-quote operands before
  consuming them or changing text state. Valid empty strings/arrays still work.
- Reject the zlib preset-dictionary flag before PDF Flate decompression. Do not
  replace this with a broad catch that hides native loading, memory or IO faults.
- Nineteen regression cases first produced 13 failures and six passes, then the
  complete Importer suite passed **696/696** with no skipped cases.
- All original 37 saved fuzz inputs replayed without unexpected exceptions:
  32 controlled `FormatException` outcomes and five completed imports. A valid
  PDF seed also imports. Preserve these fixes and input bytes.

### New Findings Still To Fix

The fresh unchanged 300-second Linux/arm64 campaign **failed with two saved
crashes and one timeout**. Do not confuse the passing old-case replay with this
new failure. The campaign uses a 2,000 ms case limit and 512 MiB managed cap.

Local evidence root:
`artifacts/native-validation-hardening/linux-fuzz-1795461140739-75fd9e83/`.

- First crash: `coverage/findings/default/crashes/id:000000,sig:02,src:000868,time:152692,execs:226039,op:havoc,rep:16`.
  Independently reproduced on macOS/x64 as `OverflowException`; input SHA256
  `61b163172f1448234f8bb3e0a50e1021d83160f842376edbe69371d929ee979f`.
- Second crash: `coverage/findings/default/crashes/id:000001,sig:02,src:001132+000911,time:285287,execs:405371,op:splice,rep:16`.
  Still requires independent classification.
- Timeout: `coverage/findings/default/hangs/id:000000,src:000861,time:71335,execs:87798,op:havoc,rep:8`.
  Reproduce under the original limit; a saved timeout is not proof of deadlock.

Required work:

1. Verify every input's bytes/hash, reproduce it in an isolated signed session
   and obtain the actual exception stack or timeout location.
2. Fix checked arithmetic/input bounds, loops or allocation budgets at the real
   parser boundary. Reject unsupported values with the existing controlled
   malformed-input/limit contracts rather than silently clamping or truncating.
3. Add small deterministic synthetic regression cases suitable for tracked CI,
   and retain valid-input/control cases and denial-before-processing behavior.
4. Replay all 37 old cases plus every new crash/timeout. Verify actual outcome,
   not merely the child exit code: `--child` reports rejected exceptions as JSON
   and can exit zero even for an unexpected exception type.
5. Run the same coverage-guided campaign again for at least 300 seconds. Require
   zero unresolved crashes and zero unresolved timeouts under unchanged limits.
   Classify new findings rather than widening the expected-exception list.

Do not upload raw fuzz inputs, change the corpus seed or overwrite failed runs.
If local artifacts are unavailable on another machine, record that fact and
request their private transfer or reproduce the campaign; do not fabricate a
replay. This machine's scratch scripts under `/private/tmp/` are not repository
tools or guaranteed handoff inputs. Use/review the committed harness and runners.

## Workstream B: Deterministic ImageAnalysis Reference And Review

Current unchanged macOS/x64 suite: **177/181 passed, four snapshot failures**.
All five quality benchmarks, controlled ambiguous-glyph cases and the real-sample
validation passed. Linux/Windows history includes additional glyph/quality and
line-ending failures; they require actual platform investigation too.

The owner explicitly chose: **establish a deterministic reference environment;
new baselines require review before approval**. This is not permission to
accept today's output, replace arbitrary expected values or weaken thresholds.

Current synthetic diagnostic report:
`artifacts/native-validation-hardening/imageanalysis-review-1795461100127/review.html`.
It retains actual inputs, overlays, JSON differences and resolved font families.
Historical overlay PNGs are unavailable: only their old digest assertions exist,
so no old-image pixel equality has been demonstrated.

1. Pin OS/image digest, architecture, SDK/runtime, managed/native Skia binaries,
   explicit font bytes/notices, locale, rendering settings and relevant runtime
   context. Family names/system fallback alone are not reproducible identities.
2. Investigate actual font resolution/native support on each platform. Do not
   assume that installing fonts or selecting a native asset package proves the
   renderer/atlas actually uses those bytes.
3. Reuse/refactor existing synthetic factories instead of silently copying them
   into a second independent implementation. Keep recognition and layout quality
   requirements intact; fix real glyph/geometry defects when demonstrated.
4. Verify repeated fresh-process rendering and recognition before proposing
   baseline candidates. Compare pixels/geometry/content and JSON separately
   from PNG compression bytes and newline encoding.
5. Produce desktop-readable candidate reports with exact context/source/output
   hashes. Clearly label expected/actual images and unavailable historical data.
6. Obtain explicit human review of the exact candidates. Record who approved
   which hashes/context and when; the implementing AI cannot approve them.
7. Only after approval, promote references and update narrowly scoped tests
   through a reviewed change. Keep native/platform behavior and historical
   failures visible; do not skip them to make CI green.
8. Rerun the full unfiltered suite and platform quality tests after the change.

The present JSON snapshot differences are a diagnostic shape count and several
confidence values; other stored content/geometry/type-count fields match locally.
That observation is not proof that all visual or cross-platform output is correct.

## Workstream C: Exact Third-Party Attribution And Approved NPOI Decision

Preserve exact original notices and all existing source/hash pins. Extend
evidence with separately scoped supplements; never overwrite history to make
a guard pass or treat a self-reported flag/regex-shaped hash as verification.

- Tesseract package payloads match the four retained wrapper Git blobs.
  Component notices are candidate associations, not a verified dependency build.
- The earlier repository-wide absence claim was corrected: the pinned upstream
  build instructions exist at the retained wrapper commit. The uncommitted
  supplement and its negative tests must be preserved. It requests static
  liblzma for x86/x64 but contains no vcpkg baseline or exact liblzma version.
- Obtain the authentic vcpkg baseline/ports, exact component revisions/patches,
  build records or SBOM binding them to the actual DLL hashes. Approximate dates,
  current ports and generic error strings cannot identify the exact build.
- Liberation Sans has a content-equivalent candidate rebuild, not byte
  reproduction. Its Debian signature remains unverified without an authenticated
  keyring. Do not bootstrap trust from an arbitrary downloaded key/fingerprint.
- Container copyright-link resolution and local macOS binary correlations are
  scoped historical evidence, not current-image, publisher or source approval.
  Inspect the actual selected release/RID graph separately where needed.
- Keep the truncated Ukrainian OCR model unresolved; no repair/replacement is
  authorized by this licensing handoff. Escalate a demonstrated release-blocking
  integrity issue for an owner decision rather than silently shipping it.

Keep all [NPOI options](PVAD.NPOI-License-Decision.md) available until Product and
Legal decide. Then implement exactly the approved option:

| Option | Work after actual approval |
| --- | --- |
| A: binary agreement | Record applicable approval/payment/exemption and obligations, then deliberately configure acceptance and test retained `.xls` behavior |
| B: reviewed source build | Pin source, build/provenance/notices reproducibly, maintain an unambiguous internal package and prevent fallback to unapproved public binaries |
| C: replacement/reduction | Implement approved replacement/retirement, validate all affected paths and document any breaking compatibility change |

Do not silently set `AcceptNPOIOSMFLicense`, pay, accept terms, remove legacy XLS
or substitute another library. Reconcile actual packages, SBOMs, notices,
container/Cloud/On-Premise/native redistribution and source obligations after
the approved choice. Source evidence and Legal approval remain distinct.

## Workstream D: Real Manufacturer Inputs And Key Custody

Maintain one concise decision table, ask for missing non-secret inputs and
continue unaffected technical work. Required fields: item, owner, decision,
date, evidence reference, verification performed and remaining blocker.

- Approved manufacturer public P-256 key, stable key ID and provenance. Provision
  only the public register through review. Keep it empty until authentic approved
  inputs arrive; synthetic test keys cannot satisfy this primary item.
- Manufacturer signing custodian, approved secret store, access policy, backup,
  rotation and recovery ownership. The owner provisions private material outside
  this chat/repository through the approved channel; do not request private keys,
  credentials, passwords or tokens in the handoff/report.
- Custody for encrypted support maps/private symbols: approved recipient,
  protected storage, retention and recovery responsibilities. A synthetic
  encrypted rehearsal does not prove actual production custody.
- Confirm publisher identities/ownership and commercial/developer/OEM terms.
  Do not infer NuGet/npm ownership from a domain or a GitHub login.
- Named qualified Legal approver and evidence for EULA, NPOI and distribution
  obligations. AI-generated summaries are not counsel approval.

Use existing issuance, revocation and recovery components. Validate manufacturer
authorization and durable audit, key-pair correctness, rotation and customer
verification-only behavior without exposing secret contents. Do not deploy
issuer mode, create production licenses, mutate live databases or generate
production replacement keys without explicit operation-specific authorization.
Customer containers/workers receive verification material only. An own second
account is not an independent reviewer for a required four-eyes control.

## Workstream E: Current-Candidate Validation And Package Acceptance

Select an explicit candidate and bind every result to commit plus working-tree
source hashes, runtime architecture, configuration, toolchain and input versions.
If code changes after testing, rerun the affected checks. Never present an older
green package job as acceptance of the changed Importer.

Required acceptance:

- Complete Solution source build with all owner-added probes. Existing
  `PvadOriginalBaseline=true` selects project references; default package mode
  needs the actual protected local feed. Do not confuse those two build scopes.
- Full relevant .NET suites, complete API/PostgreSQL audit, issuer/revocation,
  direct engine/provider admission and worker checks with isolated signed sessions.
- Original/protected package graphs and independent local-feed consumers on
  Windows, Linux and macOS, including real OCR, CodeWorker, serializers, migrations,
  native dependencies, public surfaces and document equality.
- Strict visual/platform, PDF Trust and roundtrip regression gates. Report-only
  runs, selected tests or unreviewed images do not satisfy enforced acceptance.
- Designer Jest, typecheck/build; affected Admin/Account and Documentation tests
  and production builds; authenticated workflow smoke tests where affected.
- Original 37 plus all new parser cases, fresh bounded fuzzing, and the reviewed
  deterministic ImageAnalysis contract on its actual required platforms.
- Real final package inspection: no private keys/licenses, sources, embedded
  sources, SourceLink references or private symbols; no changed third-party
  binaries; preserved APIs and complete dependencies/notices/docs.

Audit TRX discovery/result rows/counters, JSON outcomes, failures, skips,
timeouts/aborts and checksums, not only process exit status or summary text.
Capture all failing attempts and cleanup; isolated PostgreSQL means a real
temporary database/container, not a production DB or in-memory substitution.

Remote validation needs a new explicit owner authorization for the candidate's
commit/push/PR/workflow actions. Prepared workflows:

- `.github/workflows/native-library-licensing.yml`
- `.github/workflows/visual-parity-platforms.yml`
- `.github/workflows/pdf-trust-fuzz.yml`
- `.github/workflows/ci.yml`

Inspect their current triggers. General CI currently needs an authorized PR;
do not invent a dispatch event, weaken branch rules or impersonate a reviewer.
Record overall workflow status: an ImageAnalysis failure means a red workflow
even when protected-package jobs passed.

## Existing Evidence: Reuse Only Within Its Scope

| Evidence | Actual result and limitation |
| --- | --- |
| Full working-tree Solution source build | Passed Debug, SDK 10.0.113 macOS/x64, 0 errors/178 warnings; `artifacts/native-solution-source-build/1795459309475-e3e790c5/`; predates the latest parser changes |
| Current parser regression suite | 696/696 Importer tests; `artifacts/native-engine-tests/1795460785422-d5ca1e6a/`; no all-platform/protected-package claim |
| Original saved-input replay | 37/37 safe outcomes plus valid seed; `artifacts/native-validation-hardening/replay-1795460869695/`; not fresh fuzz acceptance |
| Fresh coverage campaign | Failed, two crashes/one timeout; `artifacts/native-validation-hardening/linux-fuzz-1795461140739-75fd9e83/` |
| One new crash replay | Unexpected OverflowException; `artifacts/native-validation-hardening/replay-1795461426003/` |
| ImageAnalysis | 177 passed/four failed; `artifacts/native-engine-tests/1795460697854-ae4de7c5/`; candidate diagnostics are not baseline approval |
| Native/version/release tooling | 250 tests passed after the upstream-instruction supplement, including 39 runtime-source tests; revalidate after your tooling changes |
| Designer | Prior 1,052 tests/93 suites/one snapshot, typecheck and build passed; historical, not your fresh run |
| Documentation | Prior 73 tests and production build passed; existing Vite config-loader warning retained |
| Full API and issuer | Prior audited 1,527 API cases and 54 issuer cases; original raw TRX paths are in the reports |
| Protected local graph | Prior 54 assemblies/52 libraries, 654 original and 5,850 protected checks; historical package acceptance, not the changed candidate |
| Remote native workflow 45052686167 | Three protected-package jobs passed, overall workflow failed ImageAnalysis; never label it green |

The old JSON naming audit's 475 findings are a separate recorded issue. Do not
rewrite audit originals/allowlists to clear them or expand this assignment into
another repository rename. Record impact if an existing gate blocks the final
run and obtain a separate owner decision about genuine necessary remediation.

## Commands And Execution Safety

Read each runner's prerequisites and current options first. Representative local
commands, not claims they have been executed for your candidate:

```bash
git status --short
git diff --check
node tools/native-licensing/pvad-native-licensing.mjs check
node tools/native-licensing/runtime-source-evidence.mjs
node tools/native-licensing/runtime-assets.mjs
node --test tools/native-licensing/*.test.mjs tools/versioning/pvad-version.test.mjs tools/versioning/pvad-release-author.test.mjs
node tools/native-licensing/test-session.mjs exec -- dotnet build PVAD.sln --disable-build-servers -m:1 -nr:false -p:PvadOriginalBaseline=true --verbosity minimal
node tools/native-licensing/with-test-license.mjs test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj --verbosity minimal
node tools/native-licensing/with-test-license.mjs test tests/PVAD.FileImporter.ImageAnalysis.Tests/PVAD.FileImporter.ImageAnalysis.Tests.csproj --verbosity minimal
node tools/native-licensing/platform-package-check.mjs
node tools/native-licensing/regressions.mjs
node tools/dependencies/api-full-validation.mjs
PVAD_RUN_POSTGRES_TESTS=1 node tools/native-licensing/with-test-license.mjs native-issuer
node tools/native-licensing/with-test-license.mjs revocation
npm test --prefix pvad-designer -- --runInBand
npm run type-check --prefix pvad-designer
npm run build --prefix pvad-designer
npm test --prefix websites/PVAD.Documentation
npm run build --prefix websites/PVAD.Documentation
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
```

Linux coverage-guided fuzzing, inside an authorized isolated toolchain:

```bash
PVAD_PDF_FUZZ_COVERAGE_OUTPUT=artifacts/pdf-fuzz/new-candidate-run PVAD_PDF_FUZZ_COVERAGE_SECONDS=300 PVAD_PDF_FUZZ_CASE_TIMEOUT_MILLISECONDS=2000 bash tools/pdf-parser-fuzzer/run-coverage.sh
```

Use a fresh output directory per run. The Mac has x64 SDK 10.0.113 at
`/usr/local/share/dotnet-patched/10.0.113-x64`; Node is arm64. Verify rather
than inferring the .NET architecture. MSBuild/test IPC may require approved
execution outside a restricted sandbox. A blocked/aborted build is not evidence
of executed tests even if a wrapper prints a completion line.

Strip unrelated credentials from process environments while retaining required
tool paths: Rancher's Docker CLI is currently under `~/.rd/bin`. Never log full
environments. Use the existing signed-session cleanup, unique container labels,
per-process limits, bounded output and actual cleanup audits. Kill/clean only
owned test resources, never broad process matches or Docker prune. On macOS,
transport source archives with `--no-xattrs` and `COPYFILE_DISABLE=1`; preserve
tracked source bytes rather than creating `._*.cs` resource-fork artifacts.

## Final Documentation, Review And 100% Gate

Update existing guides, inventories and the primary checklist to the actual
implemented/approved state. Keep restricted issuer, recovery and custody
material outside public Documentation. Public examples contain only synthetic
data and no usable production bootstrap credentials. Review release fragments
with the existing skill; keep VERSION/published releases unchanged. Human
review is required before attesting a public security fragment as reviewed.

Write an English completion report at
`checklists/PVAD.Native-Licensing-Completion-Report.md`, preserving earlier reports.
Include:

1. Exact starting/final candidate, inherited dirty changes and your changed files.
2. Every six-row closure decision, its named owner, real evidence and remaining
   blocker; no real key/approval may be replaced with synthetic test evidence.
3. All new findings, fixes, regression cases, full/filtered commands, toolchains,
   run URLs/paths, actual counters, failures/retries and source bindings.
4. ImageAnalysis reference context, candidate hashes, repeatability evidence and
   explicit human approval before any promotion.
5. NPOI selection and implemented graph, exact source/license notices, remaining
   provenance limits, final package inspections and distribution sign-offs.
6. Trust/key/support custody evidence references without secret material.
7. Successful owned-resource cleanup and unchanged owner files, historical
   fixtures/evidence, live data, VERSION and published releases.
8. Review focus areas and final `git status`/diff check.

Completion requires all 76 primary tasks supported by actual evidence, no
unresolved required parser/quality/platform failure, no unreviewed baseline,
approved authentic manufacturer trust/custody, resolved distribution decisions
and complete package/documentation sign-off. The reviewing agent must inspect
the implementation and evidence; your self-reported green status is insufficient.

If a necessary human/external input is missing, deliver all feasible work,
leave that primary item open, give the exact next request and report the true
percentage. **Do not call the assignment 100% complete while a required gate
is blocked, failed, untested, inferred or awaiting approval.**
