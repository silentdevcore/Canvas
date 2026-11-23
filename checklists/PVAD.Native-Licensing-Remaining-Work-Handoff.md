# PVAD Native Licensing Remaining Work Handoff

## Assignment And Baseline

Finish the remaining engineering work in the original native library licensing
and protection plan. Do not restart completed product integrations or turn
adjacent findings into new implementation milestones. Another agent will review
your implementation and correct defects before final acceptance.

- Branch: `feature/pvad-native-licensing-and-protection`.
- Baseline: `f783ae9a0a520f48ec7e799ce581129d84c9db9b`.
- Previous integration audit: `5a40600f0e63890a87a14821380c53dc6cebb594`.
- Primary plan: [Native Library Licensing And Protection](PVAD.Native-Library-Licensing-And-Protection.md).
- Fixed progress: **70/76 primary items complete (92.1%); six remain open**.
- Product version: `1.1.0`. Do not increase it or rewrite published releases.

Read the actual checkout before making changes. These facts describe the
baseline, not permission to discard subsequent user changes. In this workspace,
`PVAD.sln` has an unrelated 51-line addition and `.vscode/` is user-owned and
untracked. Leave both untouched and outside your commits. Do not reset, clean,
stash, replace branches or restore files to force a clean working tree.

This handoff authorizes local engineering and isolated validation only. It does
not authorize publication, deployment, production key generation, commercial
acceptance, live database changes, GitHub review/merge actions or alteration of
repository protections. Obtain a separate owner instruction before committing,
pushing or starting remote workflows. Prepare and report unavailable remote
checks rather than inventing results.

## Read First

1. [Main plan](PVAD.Native-Library-Licensing-And-Protection.md), particularly
   Current Technical Integration Audit, Cross-Platform Package Acceptance,
   Supplemental Runtime Source Evidence, Acceptance Criteria and Later Findings.
2. [Native tooling guide](../tools/native-licensing/README.md).
3. [Native licensing contract](../docs/native-library-licensing.md) and
   [customer activation guide](../docs/guides/PVAD.Native-Library-Activation.md).
4. [Restricted issuance runbook](../operator-docs/PVAD.Native-Library-License-Issuance.md).
5. [Runtime asset guide](../assets/licenses/runtime-assets/README.md), its
   [original registry](../assets/licenses/runtime-assets/manifest.v1.json), and
   [supplemental evidence](../assets/licenses/runtime-assets/source-evidence.v1.json).
6. [NPOI decision](PVAD.NPOI-License-Decision.md),
   [subscription plan](PVAD.Subscription-Licensing.md) and
   [SDK roadmap](PVAD.SDK-Roadmap.md).
7. `product-metadata/native-library-products.v1.json`,
   `product-metadata/native-library-dependencies.v1.json`, the native entry/surface
   inventories and `product-metadata/dependency-compliance.json`.
8. `.agents/skills/pvad-release-author/SKILL.md` before authoring release fragments.

## Preserve Completed Behavior

All ten native product areas have reviewed current entry/continuation coverage:
PDF, Word, Spreadsheet, Importer, Converter, PDF Code, PDF Viewer, PDF Designer,
Spreadsheet Code and Report Designer migration. The catalog has 35 entries:
34 concrete grants and one unlicensable `auto` routing entry. Reuse their
inventories and admission probes; do not replace them with a weaker smoke test.

The existing contract remains mandatory:

- Local ECDSA P-256/SHA-256 verification with an embedded known-key register;
  never trust a public key supplied in a license or by customer code.
- Explicit signed 30-day Trial; subscriptions have seven days of grace, Trials
  have none; perpetual rights use immutable release/update metadata.
- Missing, wrong-product, expired or revoked rights fail explicitly. No hidden
  watermark, silent corruption, automatic Trial or online activation requirement.
- Failed license replacement retains the previous valid snapshot. Admitted
  built-in work may finish; public/reentrant calls and worker dispatch/start
  require fresh admission. No ambient public cross-product bypass.
- Native rights, server licenses, account subscriptions, organization roles and
  product entitlements remain separate. Customer servers are verification-only.
- Obfuscar 2.2.50 and ILSpy 11.2.0.10050 stay pinned unless a separately justified
  change is authorized. Preserve public API, serialization, DI, resources,
  generated C#, document contents and exact approved rendering comparisons.

## The Six Remaining Primary Items

| Item | Engineering Deliverable | Required Non-Agent Decision |
| --- | --- | --- |
| Manufacturer public trust | Validate the existing import/build procedure and negative cases; prepare a checksum-bound proposed public-register change only after approved input exists | Owner supplies an authentic approved manufacturer public key and key ID through an appropriate channel; reviewed provisioning is required |
| Publisher, prices and commercial/legal approval | Consolidate missing decisions and package identities into one release-blocker table; keep publication disabled | Owner selects/controls publisher accounts and approves commercial terms; qualified counsel approves legal terms |
| Third-party distribution gates | Complete technical inventory, exact source/notice mapping and approved evidence collection, including NPOI impact | Owner/counsel decides NPOI and redistribution obligations; an agent cannot accept an agreement or purchase a license |
| Production private-key custody | Review existing manufacturer/customer isolation, document custody/rotation/backup/recovery requirements and test with disposable material only | Owner names the custodian and approved secret storage, backup and recovery procedure; no production private key enters the repository or agent output |
| Complete general validation | Execute available signed local suites; prepare or, when separately authorized, run general CI, visual/platform and bounded fuzz validation with raw evidence | Remote infrastructure/access and human visual acceptance may be needed; deferred quality failures must not be waived |
| Final package documentation/distribution review | Finish remaining source/component evidence and truthful package/customer/operator guidance, then reconcile actual local artifacts | Final EULA, corresponding-source, distribution and release approval require authorized human review |

These are composite existing tasks, not six invitations to invent new scopes.
Do not close a whole item because a helper, draft, one platform or one dependency
was checked. If external inputs remain unavailable, complete the engineering
portion, leave the primary box open and identify exactly what is missing.

## Work Package A: Runtime And Dependency Evidence

Prioritize this first. Continue from `tools/native-licensing/runtime-source-evidence.mjs`
and its tests, not a new unrelated compliance system.

### Container Notice Mappings

- [ ] Resolve, where actual archive metadata permits, the five unresolved direct
  mappings in the retained ARM64 snapshot: `libgcc-s1:arm64`, `libgomp1:arm64`,
  `libncursesw6:arm64`, `libstdc++6:arm64` and `openssl`.
- [ ] Distinguish a missing notice, a directory/file symlink, an empty member and
  an actual original text. Do not infer an agreement from a package name.
- [ ] If adding link resolution, use structured archive metadata or a proven
  archive library. Reject traversal, absolute/escaping links, cycles, duplicates,
  oversized members and unsupported entry types. Do not extract arbitrary archive
  entries into host paths or follow host filesystem links.
- [ ] Preserve original bytes and link provenance. Every successful mapping must
  identify the observed package/source version, archive member and content hash.
- [ ] Keep the historical observation unchanged. Record newly derived mappings
  as supplemental evidence, with tests for both successful and rejected cases.

The current snapshot is one local Linux/ARM64 image:
`sha256:fcc0bd274ce6849fa0c6dd65962b7f1524576b783c02fdc07b6ed548859cfc63`.
It contains 162 installed packages and 176 original notice entries. It is not a
current release image, published image, amd64 check or verified build of today's
Dockerfile. Original observations and notice tar are under
`artifacts/native-runtime-provenance/1795374811152-19192c2c/`.

If that image/archive is absent, report the missing input. A new local build may
be evaluated only as a distinct observation, with its own identity and approvals;
never relabel a new build as the historical image. Do not scan full container
environment variables or expose secrets. Use the existing read-only, non-root,
network-disabled, immutable-ID observation mode. Clean only your uniquely owned
temporary containers; never run broad container pruning or deletion.

### Native And Font Sources

- [ ] Reconcile package/component/source notice coverage for the actual selected
  native dependency graph. Wrapper-package licenses do not establish all
  statically linked component rights. Use exact source revisions, package hashes
  and original notices; leave unverifiable components explicitly unresolved.
- [ ] Continue technical source evidence for the fifteen local macOS libraries.
  All correlate with installed Homebrew UUIDs/sections, but all whole-file hashes
  differ. This is not publisher authentication or a reproduced source build.
  The ignored local libraries must not be added to Git or silently replaced.
- [ ] Review the available Liberation Sans Debian source descriptor/archive
  evidence. Where feasible with an authenticated existing keyring, independently
  verify the descriptor signature and its archive checksum relationships. Missing
  trusted keys are a blocker, not permission to trust a downloaded arbitrary key.
- [ ] Document whether a reproducible build/source-bundle check is available and
  which corresponding-source decisions still require counsel. Never reclassify
  the older Sans files under the newer Serif/Mono font agreement.
- [ ] Review the remaining third-party/EULA evidence against the actual packages
  and proposed distribution channels. Do not change `AcceptNPOIOSMFLicense`,
  replace NPOI, remove `.xls` support or self-build a replacement without the
  explicit approved option in the separate NPOI checklist.

Pinned Sans source references are in `source-evidence.v1.json`:
`fonts-liberation` version `1:1.07.4-11`, with the `.dsc`, original `.tar.gz` and
Debian `.tar.xz`. They were downloaded and hashed, not signature-authenticated or
rebuilt. The original font/model registry and notice bundles remain authoritative.

The Ukrainian OCR model is an exact truncated prefix of the pinned official
fast-data file: 2,891,776 of 3,825,102 bytes, **933,326 bytes missing**, four
component offsets beyond EOF. Preserve it and its unresolved original status.
Do not fix or replace it during this assignment; the owner deferred OCR quality
and model remediation until the original licensing plan is complete.

## Work Package B: Trust And Custody Readiness

- [ ] Audit existing production/test separation and package secret exclusions;
  add focused tests only where a genuine untested requirement exists.
- [ ] Keep `src/Licensing/PVAD.Licensing/TrustedKeys.json` empty until an approved
  authentic manufacturer public key is supplied. Never commit a synthetic key as
  production trust or add a runtime trust-override/server-mode bypass.
- [ ] Consolidate the exact owner inputs still needed: public key/key ID and
  provenance, signing custodian, approved secret storage, recovery operator,
  backup/rotation policy, publisher ownership and legal/commercial approvers.
- [ ] Test custody/recovery mechanics with the existing disposable synthetic
  tooling. Never request private-key contents, passwords, tokens or customer
  license payloads in chat, reports or examples.
- [ ] Keep issuer-only configuration restricted to the manufacturer and avoid
  public Documentation links to restricted operator runbooks.

Do not implement a new key-management product or revocation administration
system. Explicit signed offline revocation import already exists; automatic
remote revocation and a durable Admin list issuer are not this milestone.

## Work Package C: General Validation

- [ ] Produce a matrix of exact source commits, platforms, executed commands,
  raw reports and cleanup. Separate full runs from filtered tests, preparation
  from execution, local runs from remote runs, and package jobs from whole CI.
- [ ] Execute applicable solution/API, native admission, Designer, Documentation,
  roundtrip and visual regressions through the existing isolated signed sessions.
  Preserve caller files and normal build outputs; never test against live data.
- [ ] Run the existing bounded parser fuzz campaign where its toolchain exists.
  Retain corpus/crash/coverage provenance without uploading document or secret
  material. A normal unit test or installed fuzzing tool is not a campaign.
- [ ] If separately authorized, run `.github/workflows/ci.yml`,
  `.github/workflows/native-library-licensing.yml`,
  `.github/workflows/visual-parity-platforms.yml` and
  `.github/workflows/pdf-trust-fuzz.yml` at the actual candidate commit. Do not
  change protected branches, permissions or required checks to force completion.
- [ ] Confirm full test discovery and raw result counts, process exit statuses,
  timeouts/skips/failures, unchanged inputs and cleanup. Keep failed attempts.

Existing native workflow run
[45052686167](https://github.com/silentdevcore/Canvas/actions/runs/45052686167)
at source commit `0006fc79c5d5050d412ae3ca03de8ee4c4dae603` passed all three
protected package jobs: Windows/x64, Linux/x64 and macOS/Intel. Each processed
54 assemblies and 52 libraries, passed 5,790 protected assertions, 62 Converter
cases, 12 CodeWorker cases, real OCR and exact ordinary/worker PDF comparisons.
The overall workflow did **not** pass: ImageAnalysis compatibility failed on
all three platforms. Do not describe the entire workflow as green or substitute
its package success for general CI acceptance.

Known ImageAnalysis glyph/quality and snapshot-portability issues are deferred.
Do not change algorithms, expected text, thresholds, fixture bytes, line endings
or snapshots, skip tests or disable a guard to clear the broad-validation item.
Reproduce and classify a failure; if it is outside native licensing, record the
blocker and stop that change. Escalate critical security/data-loss findings to
the owner immediately. Previously resolved Windows package raster concerns must
not be reopened merely because an earlier historical entry was left intact.

## Work Package D: Documentation And Closeout

- [ ] Update the main checklist and affected technical guides with actual new
  evidence. Preserve historical results, limitations and unrelated checkbox states.
- [ ] Ensure shipped package guidance matches real local-feed restore, lockfiles,
  platform requirements, activation, renewal, expiry, offline limits and dependency
  notices. Existing public XML/API and offline examples are implemented; audit
  gaps rather than regenerating unrelated documentation.
- [ ] Keep EULA/legal source versions, published documents and acceptance records
  unchanged. Draft proposed corrections separately; obtain required legal review.
- [ ] Use the release-author skill for a narrowly scoped fragment: internal-only
  evidence uses `none` with a reason; shipped corrections use an appropriate
  impact. Mandatory licensing already has a Major fragment. Do not bump VERSION,
  rewrite published releases or expose internal findings in public release text.
- [ ] Reconcile all six original open items and final acceptance criteria.
  Report completed engineering separately from still-required external inputs.
  Keep the denominator at 76 and explain every changed primary checkbox.

Do not repair the older JSON naming audit during this work. It has **475 known
pre-existing findings**, already recorded in the main plan. The current
former-name guard and tracked Markdown zero-hit check pass. Do not regenerate
historical naming pins to conceal the old audit failure. Other deferred matters
in the main plan, including operation-specific audit alignment and test-analyzer
warnings, remain deferred rather than silently becoming licensing work.

## Verification Commands

Confirm prerequisites and read each tool's guide before expensive runs. Do not
rerun every protected graph merely for an internal Markdown correction. Changes
to packing, notice copying, runtime assets or enforcement require actual
affected artifact/consumer and behavior checks, not only source-contract tests.

```bash
git status --short
git branch --show-current
git diff --check
node --test tools/native-licensing/*.test.mjs tools/versioning/pvad-version.test.mjs tools/versioning/pvad-release-author.test.mjs
node tools/native-licensing/pvad-native-licensing.mjs check
node tools/native-licensing/runtime-assets.mjs
node tools/native-licensing/runtime-source-evidence.mjs
node tools/configuration/former-name-guard.mjs
node tools/configuration/markdown-path-audit.mjs --check
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
npm test --prefix websites/PVAD.Documentation
npm run build --prefix websites/PVAD.Documentation
```

At the handoff baseline the first combined Node command passes **223 tests**
(including twelve source-evidence tests); Documentation passes **73 tests** and
its build. These are baseline results, not proof of your future changes. The
Documentation Vite config-loader warning remains visible, not suppressed.

When the pinned reference downloads exist locally:

```bash
node tools/native-licensing/runtime-source-evidence.mjs verify-downloads artifacts/native-runtime-provenance/source-downloads
```

The default evidence check does not download references or rerun historical
native/container observations. Clean CI must not require ignored local assets,
the historical container image or private source downloads.

Use these existing runners for relevant executable checks after reading their
documented prerequisites and current argument contracts:

```bash
node tools/native-licensing/with-test-license.mjs test tests/PVAD.Licensing.Tests/PVAD.Licensing.Tests.csproj
node tools/native-licensing/regressions.mjs
node tools/dependencies/api-full-validation.mjs
node tools/visual-parity/run.mjs
node tools/native-licensing/platform-package-check.mjs
node tools/native-licensing/webapi-protection-rehearsal.mjs
node tools/native-licensing/activation-guide-check.mjs
```

For a complete local native graph, the existing joint rehearsal profile is
`node tools/native-licensing/joint-package-rehearsal.mjs --migrations`; actual
platform package verification uses the platform wrapper. Do not publish its
candidate packages or upload its maps, private symbols, synthetic licenses or
test signing material. Shared signed sessions must always clean up, including
failed commands. Test-only trust and binaries must not enter normal outputs.

On the original Mac, the reviewed x64 SDK is available at
`/usr/local/share/dotnet-patched/10.0.113-x64`. Verify actual SDK/runtime/CPU
architecture on your host; never assume Node's architecture proves .NET's.
The existing runners use no-server/no-node-reuse MSBuild settings and bounded
processor counts. Do not kill unrelated user processes to recover a stalled run.

## Deliverables For The Reviewing Agent

Leave a clear, reviewable diff and an English completion report, preferably
`checklists/PVAD.Native-Licensing-Remaining-Work-Report.md`, containing:

1. Baseline/candidate commits, exact changed files and a concise explanation of
   each behavioral or evidence change.
2. Every handoff work item marked done, blocked, not run or intentionally deferred,
   with a reason; no hidden unfinished work or unsupported completion claims.
3. Every one of the six primary open items reconciled with its original wording,
   evidence and remaining human inputs. State both completed count and percentage.
4. Raw report paths, commands, platforms, actual runtime architecture, test/result
   counters, failures/skips/timeouts, source/package hashes and cleanup facts.
   Use existing machine-readable report contracts where possible.
5. Before/after notice coverage, exact resolved/unresolved source mappings,
   retained original hashes and all limitations. Never conflate a local receipt,
   fetched archive, matching prefix or copied notice with legal approval.
6. Confirmation that user-owned files, production trust/keys, VERSION, published
   releases, live data and original fixtures/baselines remain unchanged, except
   for any separately approved, explicitly documented change.
7. Questions for the owner/counsel and concrete next actions for blocked items.

Do not commit or push by default. The reviewing agent will inspect the complete
diff, rerun relevant tests, check actual artifacts and correct defects. Do not
mark this handoff or the primary plan fully complete merely because the local
engineering portion is exhausted; external approvals remain real blockers.
