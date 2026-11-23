# PVAD Native Licensing Remaining Work Report

Completion report for the
[remaining-work handoff](PVAD.Native-Licensing-Remaining-Work-Handoff.md), written
for the reviewing agent and the owner. Execution date: 2026-11-22/23.

## 1. Baseline, Candidate And Changed Files

- Branch `feature/pvad-native-licensing-and-protection`.
- Baseline `f783ae9a0a520f48ec7e799ce581129d84c9db9b`, unchanged as HEAD.
- Candidate: the uncommitted working tree. Nothing was committed, pushed or
  started remotely.
- `PVAD.sln` (51 lines) and `.vscode/` are user-owned. They were not touched.
- The four owner lines in the main plan that link this handoff are carried along,
  as instructed.

| File | Change |
| --- | --- |
| `tools/native-licensing/runtime-source-evidence.mjs` | New functions, each with a CLI command: (1) strict ustar/GNU notice-archive parser and archive-internal link resolver (`resolve-container-links`, `verify-container-links`); (2) native component marker scan (`observe-native-components`); (3) Mach-O byte-difference explanation (`explain-macos`); (4) Debian descriptor checksum check and read-only OpenPGP issuer reader, run inside `verify-downloads`. `validateEvidence` validates the three new records. |
| `tools/native-licensing/runtime-source-evidence.test.mjs` | Eleven new tests, 12 → 23, covering success and rejection cases. |
| `assets/licenses/runtime-assets/evidence/container-notice-links.v1.json` | New supplemental record: five package mappings and three license links. |
| `assets/licenses/runtime-assets/evidence/native-components.v1.json` | New supplemental record: 8 packages, 45 payloads, 14 component observations. |
| `assets/licenses/runtime-assets/evidence/macos-build-differences.v1.json` | New supplemental record: 15 of 15 differences explained. |
| `assets/licenses/runtime-assets/source-evidence.v1.json` | Adds the three observations, the `fonts.descriptor` result and three limitations. The original pins are unchanged. |
| `assets/licenses/runtime-assets/README.md` | Dated supplements and the new commands. Original paragraphs are retained. |
| `checklists/PVAD.Native-Library-Licensing-And-Protection.md` | Owner's four handoff lines, plus the new section "Remaining-Work Execution (2026-11-22)". |
| `checklists/PVAD.Native-Licensing-Remaining-Work-Report.md` | This report. |

No product source, test project, runtime asset, notice bundle, trust file,
`VERSION`, release manifest or fixture/baseline changed.

## 2. Handoff Work Items

| Item | Status | Reason or evidence |
| --- | --- | --- |
| A: resolve the five ARM64 mappings | Done | All five resolve through archive-internal links (section 5). |
| A: distinguish missing, link, empty and original text | Done | Statuses `missing-notice`, `empty-member`, `original-text`, `original-text-through-link` and `directory-not-notice`, with tests for each. |
| A: safe link resolution | Done | Parses header metadata from the retained bytes and never extracts. Header checksum and the ustar/GNU magic are verified. It rejects: traversal, absolute paths, paths outside the notice roots, hard links and devices, duplicates, oversized or truncated members, a missing end marker, absolute, escaping or root-leaving links, cycles and over-long chains. |
| A: provenance per mapping | Done | Package version, source version, link chain, archive member, bytes and SHA-256, all bound to the historical record. |
| A: keep the historical observation unchanged | Done | The `container-local-os.v1.json` hash `63596ce4…` is unchanged. The new record is supplemental and re-derivable with `verify-container-links`. |
| A: native component/source coverage | Partial | Marker scan completed, not exhaustive native attribution. Tesseract component notices and source/distribution review remain unresolved (section 5). |
| A: fifteen macOS libraries | Done (technical) | All differences are explained as load-command rewrites. Publisher authentication is not achieved. |
| A: Liberation Sans signature | **Blocked** | No authenticated Debian keyring; no key was imported. Checksums are verified; the signer fingerprint is recorded but untrusted. |
| A: reproducible build and counsel questions | Documented | FontForge is not installed, so a rebuild is not possible. The questions for counsel are in section 7. |
| A: third-party/EULA review, no NPOI change | Partial review; decision open | The separate NPOI checklist retains options A, B and C until the owner/counsel selects one. `AcceptNPOIOSMFLicense` was not changed. |
| B: production/test separation and secret exclusion | Done (audit) | Existing tests cover ephemeral keys, trust restoration, package private-key markers, issuer allowlists and customer separation. No genuine untested requirement was found that is in scope. |
| B: keep `TrustedKeys.json` empty | Done | `keys: []`, unchanged. |
| B: consolidate owner inputs | Done | Section 7. |
| B: test custody and recovery mechanics with disposable material | Done | `native-issuer` with real PostgreSQL: 54/54. `revocation`: 13 checks. Licensing tests cover key-file permissions and known-key rotation. |
| B: issuer configuration and documentation links | Verified | The runbook stays restricted. A public-navigation exclusion test exists. |
| C: validation matrix | Done | Section 4. |
| C: execute local suites | Partial | Listed native/API suites executed. ImageAnalysis still fails; Visual Parity passed only in report-only mode on rerun. Full Solution build and Designer Jest, typecheck and production build were not executed in this run. |
| C: bounded fuzz campaign | **Not run** | The AFL++/SharpFuzz 2.2.0 toolchain is absent. The workflow is Linux-only. |
| C: remote workflows | **Not run** | Not authorized. Prepared commands are in section 4. |
| C: discovery, counters and cleanup | Done | Section 4. |
| D: update checklist and guides | Done | Main plan section and runtime asset README. |
| D: package guidance audit | Done (no gaps fixed) | The activation guide check and the platform package check pass. No guidance change was needed for this work. |
| D: EULA and legal sources unchanged | Done | Not touched. |
| D: release fragment | Covered | This is internal-only evidence. The existing `native-runtime-source-evidence` fragment (`impact: none`) still describes it accurately, so no new fragment was added. |
| D: reconcile the six items | Done | Section 3. |

## 3. The Six Primary Items

Primary status: **70/76 complete (92.1%)**. No checkbox changed, and the
denominator stays 76.

| Primary item (original wording, shortened) | Engineering this run | Still required |
| --- | --- | --- |
| Provision manufacturer public verification keys | Trust stays empty. The import path is unchanged and covered by existing tests. | An authentic public key, key ID and provenance from the owner; a reviewed build change. |
| Keep NuGet publisher, publication, prices and legal approval separate | Publication remains disabled (`publicationReady: false` everywhere). | Publisher accounts, prices and commercial terms from the owner; legal approval from counsel. |
| Resolve third-party distribution gates, including NPOI | Historical container copyright mapping is complete, not distribution approval. The Tesseract component gap is identified. Descriptor checksums are verified. | The owner's NPOI option A, B or C; Tesseract component notices; Liberation Sans signature and corresponding source; counsel review. |
| Keep private keys out of packages, images, logs and source control | Separation audited; disposable issuer and revocation runs pass. | A named custodian, approved secret storage, and backup, rotation and recovery policy. |
| Execute complete general CI, visual/platform matrices and fuzz campaigns | The listed local signed suites ran; this is not complete application validation. | Full Solution and Designer checks, deferred ImageAnalysis failures, remote workflow authorization, a Linux fuzz run and human visual acceptance. |
| Supply EULA references, notices, XML docs and offline examples | Notice evidence extended. | Final EULA, corresponding-source and distribution approval by an authorized human review. |

## 4. Validation Matrix

Host: macOS 27.2 on an Apple M4 Max (arm64). Node 22.14.0 runs as arm64. .NET SDK
10.0.113 and runtime 10.0.13 run as `osx-x64` under Rosetta
(`/usr/local/share/dotnet-patched/10.0.113-x64`). All runs used the baseline
source plus this working tree. Logs and the run summary are retained in ignored
`artifacts/native-licensing-remaining-work/2026-11-22-validation/`.

| Command | Result | Raw evidence |
| --- | --- | --- |
| `node --test tools/native-licensing/*.test.mjs` plus the two version tests | 234/234; baseline was 223 | console |
| `with-test-license.mjs test tests/PVAD.Licensing.Tests/...` | 118/118 | `artifacts/native-engine-tests/1795381543351-16f4fbbe` |
| `activation-guide-check.mjs` | passed | `artifacts/native-activation-guide/1795381568844-85fa468a` |
| `webapi-protection-rehearsal.mjs` | passed | `artifacts/native-webapi-protection/1795381626225-a2c789e2` |
| `regressions.mjs` | **failed** at ImageAnalysis (177/181 passed, 4 snapshot failures). It stopped afterwards; the earlier steps passed: Licensing 118, Generator 141, Pdf 76, Importer 677, FileImporter 29. | `artifacts/native-test-sessions/f0e5545f-feb2-4a8f-9f35-a6e03237757f` |
| Remaining regression steps, run separately through `with-test-license.mjs test` with the runner's exact filter | ImageOcr 104/104, Converters 20/20, Word 6/6, Spreadsheet 13/13. Filtered API: 447 passed and 2 skipped without the PostgreSQL flag; those 2 then passed with `PVAD_RUN_POSTGRES_TESTS=1`, so 449/449. | `artifacts/native-engine-tests/1795384116149-53b56d2f`, `…5315981-a66c877b`, `…5349888-8f353977`, `…5374313-f230adfa`, `…5407793-b6ca59ff`, `…5452508490-5ed58e03` |
| `platform-package-check.mjs`, including the joint rehearsal | passed: 54 assemblies, 654 original and 5,850 protected checks; cleanup confirmed | `artifacts/native-platform-packages/1795382199593-f06f255f`, `artifacts/native-joint-packages/1795382202082-f7b64a71` |
| `api-full-validation.mjs` | passed, 1,527 | `artifacts/api-full-validation/2026-11-22T21-28-22.731Z-713395c3-86e4-458a-be5b-657289db567e` |
| `with-test-license.mjs native-issuer` (real PostgreSQL) | 54/54 | `artifacts/native-engine-tests/1795385913563-c0ab2c15` |
| `with-test-license.mjs revocation` | 13 checks passed | `artifacts/native-engine-tests/1795386290634-fccc602c` |
| `visual-parity/run.mjs` | attempt 1 **failed**: Playwright element screenshot timeout ("waiting for element to be stable"). Attempt 2 passed in report-only mode: 4 pages, 40 elements, 39 matches, 1 P2. | `artifacts/visual-parity/visual-parity-report.json` (attempt 2; attempt 1 survives only as a log) |
| Documentation `npm test` and `npm run build` | 73/73; build ok; Vite config-loader warning still visible | console |
| `pvad-native-licensing.mjs check`, `runtime-assets.mjs`, `runtime-source-evidence.mjs`, `verify-downloads`, `verify-container-links` | all pass | console |
| Former-name guard, Markdown `--check`, version check, fragments, `git diff --check` | all pass | console |

**ImageAnalysis classification.** The failing tests are two debug-overlay snapshot
digests (PNG bytes 11172 vs 11171 and 6612 vs 6539) and two recognition snapshots
(confidence 0.687 vs 0.688; shape count 13 vs 14). These are the known, deferred
glyph/quality and snapshot-portability issues, not native licensing. No
algorithm, expectation, threshold, fixture or snapshot was changed.

**Not run:**
- Full `PVAD.sln` solution build, Designer Jest, Designer typecheck and Designer
  production build. The listed selected/native/API runs do not replace them.
- The fuzz campaign: AFL++ and SharpFuzz are absent, and the workflow is
  Linux-only.
- All remote workflows, which were not authorized. The following are prepared
  commands only; commit, push and workflow execution each require owner approval:
  - `gh workflow run native-library-licensing.yml --ref feature/pvad-native-licensing-and-protection`
  - `gh workflow run visual-parity-platforms.yml --ref feature/pvad-native-licensing-and-protection`
  - `gh workflow run pdf-trust-fuzz.yml --ref feature/pvad-native-licensing-and-protection`
  - `ci.yml` has no `workflow_dispatch`. It runs only on a push to `develop` or on
    a PR to `develop` or `main`, so it needs an authorized PR. The trigger was not
    changed.

Earlier native workflow run `45052686167` remains historical evidence: its
package jobs passed, and ImageAnalysis failed on all three platforms.

**Cleanup:**
- Every signed session reported removal of synthetic keys, licenses and isolated
  builds.
- Source trust and normal product outputs are unchanged.
- The archive observation did not start an application/container. The API and
  native-issuer tests did create disposable PostgreSQL containers through their
  existing isolated runners. Do not interpret those runs as container-free or
  as live/customer database access. Runner cleanup evidence and a fresh Docker
  inventory are separate checks; no fresh Docker inventory is claimed here.
- Read-only cross-checks: `gpg --list-packets` with a disposable empty homedir,
  which was deleted.
- Two of my own superseded derivation outputs were deleted. The final ones are
  under `artifacts/native-runtime-provenance/`: `1795380726549-fdb0653c`,
  `1795381017107-67a9f9db` and `1795381218489-40007d72`.

## 5. Notice Coverage And Source Mappings

**Container (historical image `sha256:fcc0bd27…59cfc63`, Linux/ARM64).**
- Before: 157 of 162 packages had a directly mapped original copyright.
- After: 162 of 162, the five through links.
- The archive SHA-256 `a1b897c8…5e42c259` equals the historical record (527 members).

| Package | Version | Link | Original member | Bytes | SHA-256 |
| --- | --- | --- | --- | --- | --- |
| libgcc-s1:arm64 | 14.2.0-4ubuntu2~24.04.1 | directory → `gcc-14-base` | `usr/share/doc/gcc-14-base/copyright` | 69004 | `20390f8a…7252f79` |
| libgomp1:arm64 | same | directory → `gcc-14-base` | same | 69004 | same |
| libstdc++6:arm64 | same | directory → `gcc-14-base` | same | 69004 | same |
| libncursesw6:arm64 | 6.4+20240113-1ubuntu2.2 | directory → `libtinfo6` | `usr/share/doc/libtinfo6/copyright` | 4670 | `7c9d2c8d…917e1c65` |
| openssl | 3.0.13-0ubuntu3.16 | file → `../libssl3t64/copyright` | `usr/share/doc/libssl3t64/copyright` | 2543 | `6a7da622…1ad6ef35` |

Every target belongs to an installed package with the same source and source
version. The `common-licenses` links resolve as LGPL → LGPL-3 (`e3a994d8…`),
GPL → GPL-3 (`3972dc97…`) and GFDL → GFDL-1.3 (`11053552…`).

**Native NuGet payloads (45, from the existing restore review).**
- SkiaSharp names all observed components in its retained notice: libpng 1.6.44,
  libjpeg-turbo 2.1.5.1, zlib 1.3.0.1 and expat 2.6.4.
- HarfBuzzSharp and ZXing.Net show no markers from the fixed table.
- **Tesseract 5.2.0: unresolved.** It has markers for Leptonica 1.82.0, Tesseract
  (3.04/5.0.0 strings), LibTIFF 4.4.0, libjpeg-turbo 2.1.4, zlib 1.2.13 and
  liblzma. The package has no notice file, and the repository copy contains only
  the wrapper license.

**macOS dylibs.** All 15 have equal lengths to their Homebrew candidates. Every
differing byte lies in the header and load commands: 184–661 bytes each. Neither
side is code-signed.

**Liberation Sans `1:1.07.4-11`.**
- Descriptor SHA-256, SHA-1, MD5 and sizes match both archives.
- Signer fingerprint `B8BF54137B09D35CF026FE9D091AB856069AAA1C`, read only, signature
  unverified.

**Ukrainian OCR model.** Unchanged and preserved: an exact truncated prefix,
933,326 bytes missing.

A resolved link, a copied notice, a matching prefix, a local receipt or a fetched
archive is **not** legal approval. All records keep `legalApprovalGranted: false`
and `publicationReady: false`.

## 6. Unchanged Protected Inputs

- `.vscode/` and `PVAD.sln` were not touched.
- `TrustedKeys.json` is unchanged and empty. No production key was generated or
  requested.
- `VERSION` and published releases are unchanged.
- No live database was used; the PostgreSQL tests used disposable containers from
  the existing runners.
- Original fixtures, baselines, fonts and models are unchanged, as are the historical
  observations (`42cef025…` and `63596ce4…`).
- The 475 pre-existing JSON naming findings were not repaired, and no historical
  pin was regenerated.

## 7. Owner And Counsel Inputs, And Next Actions

| Input | Who | Blocks |
| --- | --- | --- |
| Authentic manufacturer public key, key ID and provenance channel | Owner | Trust provisioning |
| Signing custodian, approved secret storage, recovery operator, and backup/rotation policy | Owner | Private-key custody |
| Publisher accounts and ownership; prices and commercial terms | Owner | Publication |
| Legal and commercial approvers for EULA and distribution | Owner, counsel | Final review |
| NPOI option A (binary EULA and fee), B (source build) or C (approved replacement/capability change), per the separate decision checklist | Owner, counsel | Spreadsheet `.xls` distribution |
| Tesseract 5.2.0 embedded components: obtain original notices for Leptonica, Tesseract, LibTIFF, libjpeg-turbo, zlib and xz/liblzma at the exact versions | Owner, counsel; engineering can collect texts once a source is approved | Third-party gates |
| An authenticated Debian keyring (for example from a trusted Debian installation) to verify fingerprint `B8BF5413…069AAA1C` | Owner | Liberation Sans signature |
| Liberation Sans corresponding-source and redistribution obligations (GPL v2 with exceptions) | Counsel | Font distribution |
| Authorization to commit, push and start the four workflows (or a PR for `ci.yml`) | Owner | Remote validation |

**Side findings, documented only (not fixed):**
- `tools/PVAD.LicenseIssuer` signs with any P-256 key under any key ID. It does not
  check that key against the embedded trust, so a mixed-up backup would only
  surface at the customer. The API issuer does check.
- The historical `macos-local-builds.v1.json` contains local Homebrew candidate
  paths.
- Visual Parity screenshot stability was intermittent on this host.

## 8. Reviewing-Agent Corrections (2026-11-23)

The initial review found five validation defects and one reporting issue.
Corrections are limited to internal evidence tooling, tests and documentation;
no native implementation, runtime asset or historical observation is replaced.

- Recorded notice chains are replayed with the same archive-internal walker as
  original archive resolution. Escaping, unreachable, duplicate and inconsistent
  hops, terminal members and file/directory link kinds are rejected. This proves
  consistency of the recorded chain; only `verify-container-links` rereads the
  actual historical archive. It does not authenticate invented link metadata.
- Mach-O header/command counts, aligned command sizes, section-table bounds,
  file-backed section placement and signature ranges are checked against the
  binary before byte differences are classified. Textual `otool` output alone
  cannot enlarge a purported metadata region.
- Component summaries must reproduce both counts and the exact sorted union of
  payload marker versions. Duplicate packages/assets/markers and unsafe asset
  paths fail rather than receiving internally consistent but false summaries.
- Each descriptor checksum algorithm must list every supplied archive exactly
  once, in one block. A missing SHA256/SHA1/MD5 row is not covered by another
  algorithm's row. Signature authentication remains blocked.
- TAR end handling requires two zero blocks and rejects all nonzero trailing
  content, including concatenated archives, while allowing zero padding.
- This report now distinguishes executed native/API checks from unexecuted
  Solution/Designer checks and correctly records disposable database containers.

Regression tests first reproduced four failures in the original implementation;
the malformed Mach-O example was reproduced during the preceding review. New
tests include consistent multi-hop resolution and malformed binary metadata.
The reviewer separately reproduced all five real container mappings and three
license links, the pinned font/model checks, and all fifteen native byte-difference
explanations without changing retained evidence. The original 1,527 API results
and 54 issuer results were independently audited from raw TRX, not newly executed.

Corrective validation passes **239/239** native/version/release tooling tests,
including **28** runtime-source tests, plus **73/73** Documentation tests and
the Documentation production build. Native metadata and runtime bundle checks,
recorded evidence, naming/Markdown guards, version/fragment validation and
`git diff --check` pass. The existing Vite config-loader warning remains visible.
No full product/Solution/Designer/platform/fuzz rerun is claimed by these
internal-tool corrections. The new release fragment is internal-only `none`.

Primary status remains **70/76 (92.1%)**. Full general/platform/fuzz acceptance,
production trust/custody and legal/commercial approval remain open. Nothing was
committed, pushed, published or deployed by this corrective pass.
