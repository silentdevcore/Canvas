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
