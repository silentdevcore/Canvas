# PVAD Native Licensing Final Work Report

This is the round-two completion report for the
[remaining-work handoff](PVAD.Native-Licensing-Remaining-Work-Handoff.md), written for
the reviewing agent and the owner. The
[Round One report](PVAD.Native-Licensing-Remaining-Work-Report.md) and its review
history are preserved unchanged. Execution date: 2026-11-23.

## 1. Baseline, Scope And Changed Files

- Branch `feature/pvad-native-licensing-and-protection`; reviewed baseline and HEAD
  `b0faa29a0c9c2d37a8ecbcaf95ce04b2720b83d1`.
- Candidate: HEAD plus the uncommitted working tree below. Nothing was committed,
  pushed, dispatched, published or deployed.
- Not touched: the user-owned `PVAD.sln` addition and `.vscode/`. The owner's
  handoff revision is also left as is.

| File | Change |
| --- | --- |
| `tools/native-licensing/runtime-source-evidence.mjs` | Adds `validateComponentSources` and `validateFontBuild`. `validateEvidence` registers the new font-build observation and the new `componentSources` index. |
| `tools/native-licensing/runtime-source-evidence.test.mjs` | Two new tests with 23 negative mutations (28 → 30 runtime-source tests). |
| `assets/licenses/runtime-assets/component-sources/tesseract-5.2.0/component-sources.v1.json` | New: Tesseract payload identity and component source candidates. |
| `assets/licenses/runtime-assets/component-sources/tesseract-5.2.0/notices/**` | New: seven byte-exact upstream notice files (see section 4). |
| `assets/licenses/runtime-assets/evidence/liberation-sans-candidate-build.v1.json` | New: candidate rebuild comparison. |
| `assets/licenses/runtime-assets/source-evidence.v1.json` | Adds the font-build observation, `componentSources` and two limitations. Existing pins are unchanged. |
| `assets/licenses/runtime-assets/README.md` | Two dated supplements. Original text is retained. |
| `checklists/PVAD.Native-Library-Licensing-And-Protection.md` | New section "Remaining-Work Round Two (2026-11-23)" and one new Later Findings row (fuzz crashes). |
| `checklists/PVAD.Native-Licensing-Final-Work-Report.md` | This report. |

Release fragments: the existing internal-only fragments
`native-runtime-source-evidence` and `native-runtime-evidence-validation`
(`impact: none`) describe this evidence-only work, so no fragment was added.
No product source, test project, runtime asset, notice bundle, trust file or
`VERSION` changed.

## 2. Work Items

| Item | Status | Reason or evidence |
| --- | --- | --- |
| P1 full Solution build through the signed session | **Blocked** | The working-tree `PVAD.sln` addition includes `tools/native-licensing/GeneratorPackageProbe`. It references the protected `PVAD.Generator` package, which exists only in the joint rehearsal's local feed: `NU1101` on nuget.org, after 11 s. The project was not removed or excluded. Owner decision needed (section 7). |
| P1 supplemental: committed Solution | Done, different scope | `git archive HEAD` copy, built through `test-session.mjs exec`: **Build succeeded, 0 errors, 177 warnings**, 511 s. This does not include the working-tree `PVAD.sln`. The copy was deleted. |
| P1 Designer Jest | Done | 93/93 suites, **1,052/1,052** tests, 1 snapshot, exit 0, `--runInBand`. |
| P1 Designer type check | Done | `tsc --noEmit` exit 0. |
| P1 Designer production build | Done | exit 0. The Vite `import.meta`, config-loader and >500 kB chunk warnings remain visible. |
| P1 roundtrip/visual reconciliation | Done (analysis) | This round changes no product code, renderer, font or fixture. Round One's report-only visual run (39 matches, 1 P2; first attempt timed out) remains the latest visual evidence. It is not enforced acceptance or a human baseline approval, and no image was approved. |
| P2 Tesseract components | Partial | Payload identity is verified. Byte-exact notices are stored as candidates for five components. The Tesseract version is ambiguous, and **liblzma is unresolved** (section 4). |
| P2 version labeling | Done | The validator requires the candidate tag to equal the observed version and keeps inferred and ambiguous states distinct. A nearby release or a notice for an unknown version is rejected by tests. |
| P2 Liberation Sans build evidence | Done (candidate) | Content-equivalent rebuild, not a byte reproduction (section 4). |
| P2 Debian signature | **Blocked** | No authenticated keyring. The build used `dpkg-source --no-check`, and nothing was trusted. |
| P2 static/transitive reconciliation | Partial | See section 4. SkiaSharp is covered by its own notice. HarfBuzzSharp and ZXing show no markers, and absence is not proof. Tesseract is as above. |
| P3 bounded fuzz campaign | **Ran, failed** | 37 crashes and 0 hangs in 300 s (section 3). Classified and deferred. |
| P3 remote preparation | Prepared, not run | Section 7. |

## 3. Commands, Platforms And Raw Evidence

Host: macOS 27.2 on an Apple M4 Max. Node 22.14.0 runs as arm64; .NET SDK 10.0.113
runs as `osx-x64` (Rosetta). The Docker VM has 2 CPUs. Run logs and the summary are
in ignored `artifacts/native-licensing-final-work/2026-11-23-validation/`.

| Command | Platform | Result |
| --- | --- | --- |
| `node tools/native-licensing/test-session.mjs exec -- dotnet build PVAD.sln --disable-build-servers -m:1 -nr:false` (working tree) | macOS, osx-x64 | **exit 1**, `NU1101` for `PVAD.Generator` |
| Same command in a `git archive HEAD` copy | macOS, osx-x64 | exit 0, 0 errors, 177 warnings (`solution-build-head-archive.log`) |
| `npm test --prefix pvad-designer -- --runInBand` | macOS, Node arm64 | exit 0, 1,052/1,052 |
| `npm run type-check --prefix pvad-designer` | same | exit 0 |
| `npm run build --prefix pvad-designer` | same | exit 0 |
| Debian rebuild: `dpkg-source --no-check -x`, then `dpkg-buildpackage -b -uc -us` | `debian@sha256:268fb24c…dd343ff8`, linux/arm64 | exit 0; first attempt exit 3 (missing `build-essential`) |
| Font comparison (fontTools, read-only mounts) | same image | exit 0 |
| `tools/pdf-parser-fuzzer/run-coverage.sh`, `PVAD_PDF_FUZZ_COVERAGE_SECONDS=300`, `PVAD_PDF_FUZZ_CASE_TIMEOUT_MILLISECONDS=2000`, `AFL_I_DONT_CARE_ABOUT_MISSING_CRASHES=1` | same image; AFL++ 4.04c, SharpFuzz 2.2.0, .NET 10.0.113 linux-arm64, Node 22.14.0 | **exit 1**: 1,305 new corpus items, 5.42% coverage, **37 crashes**, 0 hangs. First attempt rejected by Docker (`--cpus 4` > 2 CPUs) before starting. |
| Crash replay with the harness `--child` mode in a signed session | macOS, osx-x64 | 37/37 reproduce as unexpected exception types |
| Stack probe (scratch project outside the repo, session license) | macOS, osx-x64 | 32 `IndexOutOfRangeException` in `PdfGraphicsInterpreter.InterpretAdmitted`; 5 `ZLibException` in `PdfStreamBudget.ReadBounded` via `FlateDecodeStreamDecoder`; all during `PdfImporter.LoadAsync`. A first probe run failed with `PvadLicenseException` until it loaded the session license. |
| `node --test tools/native-licensing/*.test.mjs` plus the version tests | macOS | **241/241**, including 30 runtime-source tests |
| `runtime-source-evidence.mjs`, `verify-container-links`, `verify-downloads`, `pvad-native-licensing.mjs check`, `runtime-assets.mjs` | macOS | all exit 0 |
| Documentation tests and build, guards, version/fragments, `git diff --check` | macOS | section 6 |

Raw evidence:
- Font rebuild: `artifacts/native-runtime-provenance/liberation-sans-candidate-build/1795456342-78de1d49/`, including the built package, logs, `toolchain.tsv`, `compare.json` and `compare-cmap.json`.
- Fuzz campaign: `artifacts/pdf-fuzz-local/1795456750-686a6243/` (`campaign.log`, summary, findings, `replay.tsv`, `replay-stacks.tsv`). It also holds the source archive's SHA-256 `11a04a24…bdfe2823e` with its commit. The 133 MB archive itself was deleted.
- Upstream downloads: `artifacts/native-runtime-provenance/component-source-downloads/tesseract-5.2.0/`.

The crash inputs are synthetic mutations of a tracked test fixture. They stay
local and are not uploaded.

Cleanup:
- All containers ran with `--rm` and an ownership label, and none remain.
- Signed sessions removed their synthetic keys, licenses and isolated builds.
- The scratch stack probe and the archive copy are deleted.
- No broad pruning was used.

## 4. Source And Component Mappings

**Tesseract 5.2.0 payload identity (verified).**
- All four payloads equal the Git blobs in `charlesw/tesseract` at tag 5.2.0, commit
  `2c993543f7fa66576a8890a6c4ab053c4598aaed`:
  - x64 `leptonica-1.82.0.dll`: `64f449cd…`
  - x64 `tesseract50.dll`: `92a0dd31…`
  - x86 `leptonica-1.82.0.dll`: `d244fa29…`
  - x86 `tesseract50.dll`: `3a336415…`
- That repository contains no build recipe for these DLLs.
- The wrapper license equals the stored repository copy (`cfc7749b…`).

**Component candidates.**
- Each notice was fetched at a tag-resolved commit and checked against the host's
  blob ID, and SHA-256 for GitLab.
- Status before this round: 0 of 6 components named in any retained notice.
- After: notices are stored for 5 components plus the newly found libpng; 1 is
  unresolved.

| Component | Observed (basis) | Candidate commit | Notice (bytes, SHA-256) | Status |
| --- | --- | --- | --- | --- |
| Leptonica | 1.82.0 (identification string and file name) | `f4138265b390…` | `leptonica-license.txt` 1521, `87829abb…be7d5806` | candidate association |
| Tesseract | 5.0.0 (bare string); the wrapper changelog says 5.2 | 5.0.0 `924b746e…`, 5.2.0 `5ad5325a…` | `LICENSE` 11358, `cfc7749b…bc523d30`, identical at both tags and equal to the wrapper copy | version ambiguous |
| LibTIFF | 4.4.0 (identification string) | `b6a17e567f14…` | `COPYRIGHT` 1146, `fbd6fed7…47acafd7` | candidate association |
| libjpeg-turbo | 2.1.4 (identification string) | `8162eddf041e…` | `LICENSE.md` 5213, `ee1eaf19…`; `README.ijg` 12732, `4b7b9f8c…` | candidate association |
| zlib | 1.2.13 (inflate/deflate strings) | `04f42ceca40f…` | `LICENSE` 1002, `845efc77…95753243` | candidate association |
| libpng | 1.6.37 (bare string beside libpng messages; missed by the marker table) | `a40189cf881e…` | `LICENSE` 5345, `bf5e22b9…aa14ea9b` | inferred |
| liblzma | no version | none | none | **unresolved** |

The full hashes are in the JSON record. Corrections to Round One's marker
interpretation:
- The "tesseract 3.04" marker in the Leptonica DLL is a Leptonica message.
- libpng is present but was not detected by the fixed marker table.
- The committed `native-components.v1.json` is left unchanged.

**Liberation Sans `1:1.07.4-11` candidate rebuild.**
- Image `debian@sha256:268fb24c…dd343ff8` (arm64), fontforge-nox
  `1:20230101~dfsg-1.1~deb12u1`, fonttools `4.38.0-1+deb12u1`, debhelper 13.11.4,
  dpkg-dev 1.21.23.
- Built package SHA-256: `6806fb1f…08861a094`.
- For all four Sans faces, the 681 glyph outlines, cmap subtables and all character
  mappings are identical to the shipped files.
- Only `FFTM`, the `cmap` binary encoding and `head.checkSumAdjustment` differ.
- Built hashes: Regular `dfb550b5…`, Bold `c1ad57bd…`, Italic `b4aceace…`, BoldItalic `3aa0cf84…`.
- **Not byte-reproduced.** The signature is unverified. The shipped fonts are unchanged.

Reproducible evidence: payload blob equality, notice blob/hash equality,
descriptor checksums, glyph/cmap equality. Inference: tag-to-binary
associations, the libpng version and the Tesseract version. None of this is a
legal approval, and all flags stay `false`.

## 5. The Six Primary Items (70/76, 92.1%)

| Item | Technical state after this round | Remaining blockers |
| --- | --- | --- |
| Manufacturer public verification keys | `TrustedKeys.json` is empty; provisioning tests are unchanged. | An authentic key, key ID and provenance from the owner. |
| Publisher/commercial/legal | Publication is disabled everywhere. | Publisher ownership, prices, terms, counsel. |
| Third-party distribution gates | Container notices are complete. Tesseract has candidate notices; liblzma and the Tesseract version are open. Liberation Sans has a content-equivalent rebuild. NPOI A/B/C is untouched. | NPOI decision; liblzma source; counsel on all obligations; authenticated Debian keyring. |
| Private-key custody | Existing isolation tests stay green (Round One: issuer 54/54, revocation 13); no redundant reimplementation. | Custodian, storage, backup, rotation, recovery. |
| Complete general validation | Designer is complete; the committed Solution builds; the fuzz campaign ran and **failed**. | Working-tree Solution decision; fuzz crash fixes (deferred); remote workflows; ImageAnalysis (deferred); human visual acceptance. |
| Final documentation/distribution review | Notice evidence is extended; guides are updated. | Final EULA, corresponding source, distribution approval. |

No primary checkbox changed.

## 6. Unchanged Protected Inputs And Final Checks

- **Unchanged:**
  - `PVAD.sln` and `.vscode/`.
  - `TrustedKeys.json` (`keys: []`); no keys were created or requested.
  - `VERSION` and published releases.
  - Live databases (none used).
  - Original fonts, models and dylibs.
  - Historical observations (`63596ce4…`, `42cef025…`) and committed Round One evidence.
  - Approved fixtures and baselines, and the harness, its expected-exception list and thresholds.
- **Final checks:** `git diff --check`, 241/241 tooling tests, native metadata,
  runtime bundles, offline evidence and both revalidation modes,
  former-name guard, Markdown `--check`, version/fragments, and Documentation
  73/73 tests plus the build all pass.

## 7. Owner Decisions And Unexecuted Remote Commands

1. **`PVAD.sln`:** keep the three probe projects, which means a full Solution build
   needs the rehearsal feed. The alternatives are to remove them from the Solution,
   or to provide the feed or a build condition for normal builds.
2. **Fuzz crashes:** authorize a separate importer hardening change (operand bounds
   in `PdfGraphicsInterpreter`; mapping corrupt Flate data to a controlled
   rejection), then rerun the campaign.
3. **liblzma:** an authoritative source or version for the embedded XZ code. Contact
   with the wrapper maintainer may be needed.
4. **NPOI** option A, B or C; the manufacturer key, custody and publisher inputs;
   the authenticated Debian keyring; counsel review.
5. **Remote execution authorization.** Prepared, not run; for the future candidate
   commit after an approved commit and push:
   - `gh workflow run native-library-licensing.yml --ref feature/pvad-native-licensing-and-protection`
   - `gh workflow run visual-parity-platforms.yml --ref feature/pvad-native-licensing-and-protection`
   - `gh workflow run pdf-trust-fuzz.yml --ref feature/pvad-native-licensing-and-protection`
     (expect the same 37-crash class unless fixed)
   - `ci.yml`: no `workflow_dispatch`; it requires an authorized PR to `develop` or
     `main`. Triggers were not changed.

## 8. Risks And Focus Areas For Review

- `validateComponentSources`: confirm that the tag-equals-version rule, the
  inferred/ambiguous separation and the marker-presence checks cannot be bypassed.
  The libpng exception relies on the `bare-version-string-beside-component-messages`
  basis.
- The font comparison relies on fontTools in the same image. Independently rerun
  it if needed; `compare.py` is kept with the artifacts.
- The fuzz run used `AFL_I_DONT_CARE_ABOUT_MISSING_CRASHES=1` for the container's
  core pattern; the harness and its limits were unchanged. Containers on arm64
  differ from the workflow's ubuntu x64 runner.
- The supplemental Solution build covers `HEAD`, not the working-tree Solution file.
- The fuzz crashes are a P1 robustness finding. Please review whether they should be
  prioritized before release, even though they are outside licensing.

## 9. Reviewing-Agent Corrections

The three new consistency defects were reproduced with negative tests before
correction. This review pass changes internal evidence tooling, reference data,
tests and documentation only; no importer, font, model, license or fixture is
replaced.

- A component with no observed version cannot borrow another component's
  ambiguous candidate/license to clear its unresolved state. Known bare strings
  retain ambiguity. Candidate repositories are tied to component identity, and
  directly observed versions must occur in every claimed payload.
- Notice URLs are built and compared using the exact canonical HTTPS
  repository/commit/member contract, including GitHub and GitLab paths. An
  unrelated host containing the commit text is not a source match.
- A separately retained, hash-pinned payload reference rejects changed repository
  identifiers and Git blob IDs in the candidate record. Its lengths/SHA256/blob
  IDs were independently recomputed from the four exact local package payloads.
  Offline validation uses those reference pins; it does not imply a fresh
  download or publisher authentication.
- Explicit `verify-component-payloads` reopens local DLL bytes and checks size,
  SHA256 and Git blob IDs. This was executed for all four Tesseract 5.2.0 DLLs,
  with no upstream download. Synthetic tests cover substituted bytes, wrong
  sizes/hashes, duplicate entries and unsafe paths without needing NuGet in CI.
- Font equivalence flags are strictly Boolean `true`. Glyph counts must be
  integers matching the hash-verified shipped font's bounded `maxp` table, not
  zero, a string or a guessed positive count. Table lists are duplicate-free,
  and `head` differences remain restricted to the recorded checksum field.

The original font-build and component-source observations are retained unchanged.
Their candidate/inference limitations remain real. This pass does not rerun or
approve the full Solution, Designer or fuzz campaign; the Solution feed decision
and 37 saved fuzz cases in two exception classes remain open. No primary task is
closed: **70/76 (92.1%)**.

The revised reference and tests do not provision trust, accept NPOI terms or
change VERSION, published releases, live data, user-owned Solution/IDE files,
runtime assets or approved baselines. No commit, push or deployment is performed.

Corrective validation passes **247/247** native/version/release tooling tests,
including **36** runtime-source tests, and **73/73** Documentation tests plus the
production build. The four real DLL payloads pass explicit byte revalidation;
font/model downloads and the historical container archive pass their existing
revalidation modes. Native metadata/runtime bundle guards, current naming and
tracked-Markdown guards, version/fragment checks and `git diff --check` pass.
The pre-existing Vite config-loader warning remains visible. The new internal
fragment uses `none`; no general application, platform or fuzz pass is claimed.

## 10. Current Working-Tree Solution Source Build

On 2026-11-23 the complete current working-tree `PVAD.sln`, including the
owner-added ImporterEngineProbe, WebApiProtectionProbe and GeneratorPackageProbe,
built successfully in Debug using the existing source-reference mode:

```bash
node tools/native-licensing/test-session.mjs exec -- dotnet build PVAD.sln --disable-build-servers -m:1 -nr:false -p:PvadOriginalBaseline=true --verbosity minimal
```

The executed SDK was 10.0.113, runtime 10.0.13, macOS/x64. MSBuild completed in
8 minutes 47 seconds with **0 errors and 178 warnings**; warnings were not
suppressed. All three additional probes appear as successful build outputs.
The exact SDK executable, command, source commit and timestamps are retained in
`artifacts/native-solution-source-build/1795459309475-e3e790c5/report.json`;
`build.log` beside it retains the full compiler output. These are local ignored
artifacts, not committed or published acceptance records.

The before/after hashes of all 2,122 tracked build inputs matched, including the
owner's modified Solution and the original source trust register. The isolated
session cleanup confirmed temporary files removed, source trust unchanged and
existing artifacts unchanged. No project was excluded or Solution edit made.

This supplements the earlier HEAD-only build and resolves the local source-build
question without requiring unpublished packages. `PvadOriginalBaseline=true`
selects project references; the default package-probe mode still requires its
protected rehearsal feed. This build is **not** protected-package acceptance,
execution of the compiled tests, a Release build, platform acceptance or remote
CI. Earlier package runs remain separately scoped historical evidence.

The 37 saved fuzz cases in two exception classes, existing ImageAnalysis
failures, human visual acceptance and remaining external inputs stay open.
No runtime code, fixture, threshold, production trust, VERSION, published
release, live data or owner-owned file changed. Primary status remains
**70/76 (92.1%)**. No commit, push or deployment was performed.

Follow-up validation passed all **247/247** native/version/release tooling
tests, version synchronization, fragment validation, the tracked-Markdown
zero-match guard and `git diff --check`. The historical JSON naming audit still
fails with **475 findings**, including stale retained entries and protected-file
hash mismatches. Its raw report remains at
`artifacts/rename/json-name-audit-current.json`. None of those original records
or allowlists was changed to turn that independent audit green; it is not a
Solution compiler failure or a new licensing completion claim.

## 11. Tesseract Build-Instruction Correction

A fresh source review after commit `73a522e0` found
[the upstream build instructions](https://github.com/charlesw/tesseract/blob/2c993543f7fa66576a8890a6c4ab053c4598aaed/docs/Compling_tesseract_and_leptonica.md)
at the same wrapper commit already associated with the four NuGet DLLs.
The earlier statement that the repository contains no build recipe was too
broad. This supplement corrects it; original observations, payload references,
candidate component notices and their pins are not rewritten.

The original was retrieved through GitHub's contents API at that exact commit,
and its returned Git blob ID was independently recomputed. Retained bytes:
3,331; SHA256 `1af6caa90b3e23fa984429911030611d90ce78540790d8ed8f657ef8eb9e1470`;
Git blob `bcd3b7f397a359d5ab9b83ad05a9d58e16441a3b`. The byte-exact original and
metadata are under
`assets/licenses/runtime-assets/component-sources/tesseract-5.2.0/` as
`build-instructions.txt` and `build-instructions.v1.json`.
The acquisition receipt remains locally at
`artifacts/native-runtime-provenance/tesseract-build-recipe-review/source.json`.
The first sandboxed request failed DNS resolution; the authorized network retry
succeeded. No issue, PR, maintainer message or remote workflow was created.

The source requests six static dependencies for each of x86/x64, including
liblzma, and checks out Leptonica 1.82.0 and Tesseract 5.2.0. It does not pin the
vcpkg baseline or dependency versions; its introductory versions also differ
from the checkout commands. The local DLL string inspection found liblzma
codec/error messages, not an attributable version. These facts support a build
instruction reference, **not** an exact liblzma source or license match.

The default evidence guard checks the supplement hash, original bytes, Git blob,
repository/commit/path association, source tags and all twelve dependency
requests. Negative tests reject changed files, substituted origins, invented
versions and approval flags. They require no network or local DLL cache.
No source build, DLL replacement, license acceptance, publication, key
provisioning or product behavior change is claimed. Primary status remains
**70/76 (92.1%)**. General validation, third-party approval and owner inputs
remain open. The handoff now points to the actual current source-build result.

Validation: **250/250** native/version/release tooling tests passed, including
**39** runtime-source tests. The full TAP log is retained locally at
`artifacts/native-runtime-provenance/tesseract-build-recipe-review/tooling-tests.tap`.
Documentation passed **73/73** tests and its production build; the existing
Vite native config-loader warning was not suppressed. Runtime source/bundle
checks, native product metadata, version/fragment validation, current naming,
tracked-Markdown guard and `git diff --check` passed. The raw upstream text has
a single-file whitespace-preservation attribute, not a repository-wide waiver.

The scoped diff confirms unchanged product source, original observations,
component/payload references, VERSION and published releases. The owner-owned
Solution still has its original SHA256 and 51 added lines. Neither full product
tests nor protected-package or remote workflows were rerun for this internal
evidence-only change; their earlier results remain historical, separately
scoped evidence. No commit or push was performed.

To close the exact-build gap, obtain the following from the upstream binary
producer or an authentic retained build record; no request has been sent:

1. The vcpkg commit/baseline and triplets used for the four hash-pinned DLLs.
2. Exact liblzma/XZ version, source archive/revision, patches and build options.
3. Build logs, lockfiles or SBOM binding those inputs to the x86/x64 DLL hashes.
4. Original license/notice texts and any corresponding-source requirements for
   that exact revision, for separate distribution review.

An approximate release date, today's vcpkg port or a matching generic error
string cannot substitute for those inputs. Manufacturer trust, key custody and
NPOI decisions remain independent gates, not assumptions to fill in here.

## 12. Authorized Parser Hardening And ImageAnalysis Investigation

The owner authorized the previously deferred robustness/validation follow-up
after commit `73a522e0`. This is a separate scoped correction, not new licensing
features or permission to change baselines. The earlier failed campaign and its
37 original inputs remain unchanged. No primary checkbox is closed by these
partial validation results: **70/76 (92.1%)**.

### Parser Correction And Red/Green Evidence

`PdfMalformedContentTests` adds 19 cases for malformed text operators, valid
empty strings and text arrays, preset-dictionary zlib headers and ordinary
compressed data. Before the correction, the signed isolated run failed 13
cases and passed six. Raw TRX and cleanup:
`artifacts/native-engine-tests/1795460681558-88705f5c/`.
The initial sandboxed attempt at `1795460637459-b2ecda89` failed MSBuild IPC
before test execution and is not test evidence; the owned blocked child was
terminated and the wrapper removed its temporary license/build files.

The interpreter now checks operand count and types for all four text-showing
operators before reading or changing text state. Malformed operands produce
`FormatException` rather than an indexing crash or fabricated empty text.
Valid empty strings and numeric adjustments in text arrays remain supported.
Flate decoding rejects the zlib FDICT flag before decompression, since PDF
Flate streams cannot request an external dictionary. It does not catch all
compression/IO exceptions or hide runtime/native-library failures. The native
license checks still run before these input checks. The fuzz harness and its
expected-exception list are unchanged.

After correction the **complete Importer suite passed 696/696**, including all
19 new cases, with zero failures/skips/aborts/timeouts. The TRX result rows were
checked against every counter. Raw evidence and successful isolated cleanup:
`artifacts/native-engine-tests/1795460785422-d5ca1e6a/`.

All **37 saved crash inputs** also passed an independent two-second-per-process
replay: 32 controlled `FormatException` results and five completed imports, no
unexpected exception types or input-byte changes. A valid PDF seed imported
successfully first, so accepting only license failures cannot pass this replay.
Replay report and build log:
`artifacts/native-validation-hardening/replay-1795460869695/`.
This is saved-input regression evidence, not a new coverage-guided campaign.

### ImageAnalysis: Real Remaining Failures

The unmodified full ImageAnalysis suite was rerun on macOS/x64 with SDK
10.0.113: **177 passed, four failed, zero skipped, 181 total**. Raw TRX and
cleanup: `artifacts/native-engine-tests/1795460697854-ae4de7c5/`.
All five benchmark cases, controlled ambiguous-glyph cases and the real-sample
validation passed their unchanged quality requirements. This does not turn the
full suite green.

A temporary, isolated diagnostics executable invoked the actual unchanged
synthetic fixture factories and snapshot builder; it did not copy/reimplement
their rendering logic. Current inputs, overlays, expected/actual JSON, resolved
font-family names, run log and a field-comparison HTML report are retained at
`artifacts/native-validation-hardening/imageanalysis-review-1795461100127/`.
Its `review.html` does not claim an old-image pixel comparison: the old PNG
files are not retained, only their digest assertions.

| Case | Observed differences |
| --- | --- |
| Clean screenshot | Overlay 6,539 expected vs 6,612 actual bytes; diagnostic ShapeCount 14 vs 13; text confidences 0.639 vs 0.644 and 0.717 vs 0.716 |
| Invoice table | Overlay 11,171 expected vs 11,172 actual bytes; text confidences 0.688 vs 0.687, 0.535 vs 0.532, 0.617 vs 0.618 and 0.620 vs 0.619 |

All other JSON snapshot fields, including text content, text geometry and
returned element-type counts, match in this local diagnostic. That is not
proof of cross-platform visual equality or the cause of each difference.
The code resolves system font families both in synthetic fixture rendering and
in the lazy character atlas; the original reference environment is not pinned.
The observed current aliases include Monospace to Helvetica, sans-serif to
Helvetica and serif to Times. Missing historical font/renderer inputs prevent
a defensible attempt to force the old hashes by tuning recognition code.

### Owner-Approved Reference Direction

The owner chose a deterministic reference environment and review before new
baseline approval. This direction is recorded for the next ImageAnalysis step;
no replacement baseline has been approved or installed:

1. Pin the reference OS/container architecture and image digest, SDK/runtime,
   managed/native Skia binaries, explicit font files and their original notices.
   A family name or current system fallback is not an adequate font identity.
2. Use controlled synthetic rendering inputs and record locale, font resolution,
   raster settings and relevant runtime/CPU context. Do not alter shipped
   renderer behavior merely to reproduce a test hash.
3. Verify repeatability in independent fresh processes; retain current input,
   overlay, pixel and JSON results with source and context hashes.
4. Keep generated references in candidate state with exact hashes and a review
   report. Only an explicit human review may approve their promotion.
5. Preserve benchmark/recognition thresholds and keep old failing native runs
   visible until the replacement contract is explicitly reviewed. Linux/x64,
   Windows and macOS claims each require actual evidence, not a local inference.

No existing ImageAnalysis test, fixture, snapshot, font, quality threshold or
algorithm was changed during this investigation. The new Patch fragment
`pdf-malformed-content-reliability` covers the actual importer correction;
VERSION and published releases remain unchanged. Remote CI and publication
were not authorized or executed.

### Fresh Coverage Campaign: Acceptance Still Failed

The existing unmodified AFL++/SharpFuzz harness was run again for **300 seconds**
with a **2,000 ms** case limit and **512 MiB** managed-heap cap on local
Linux/arm64, using the same immutable Debian base and SDK 10.0.113. Tool and
source-archive metadata, complete build/campaign logs and findings are retained
at `artifacts/native-validation-hardening/linux-fuzz-1795461140739-75fd9e83/`.
The working-tree archive's hashes bind the tested parser changes; no source or
exception-list substitution was made inside the container.

The campaign **failed**: **two saved crash inputs and one saved timeout input**,
1,436 new corpus items and 5.57% reported coverage. A saved timeout means the
case exceeded the configured limit, not proof of an infinite hang. Calibration
instability warnings remain in the raw log. One new crash was independently
replayed on macOS/x64 as an unexpected `OverflowException`:
`artifacts/native-validation-hardening/replay-1795461426003/`.
The other crash and timeout still require independent replay/classification.
These new findings are not covered by the passing replay of the original 37
inputs. Do not classify the fresh campaign as passed or close general validation.

The first launch failed before starting Docker because the credential-stripped
PATH lacked the Rancher CLI. The second attempt failed before fuzzing because
macOS archive metadata created extra binary resource-fork entries during Linux
extraction. Neither is a product-test pass. A fresh source archive generated
with `--no-xattrs` and `COPYFILE_DISABLE=1` removed only those transport metadata
entries; it did not exclude source projects or weaken compilation. The failed
launch/build evidence remains in the two preceding local directories. The
completed campaign's own container was removed and the Docker post-check found
no container with its unique ownership label. The temporary source archive was
removed; original crash inputs and approved baselines were preserved.

Remaining immediate technical work: classify and fix the new parser findings,
rerun their regression tests and the same bounded campaign, then establish the
owner-approved ImageAnalysis reference context and submit baseline candidates
for actual review. Overall primary status is still **70/76 (92.1%)**. No remote
CI, current protected-package rerun, publication, commit or push is claimed.
