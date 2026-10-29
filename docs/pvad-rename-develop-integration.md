# Rename And Develop Integration

The initial candidate sections below record validation before the authorized
transfer. The PR delivery checkpoint records subsequent commits and actual
GitHub checks; local test results are not substituted for those checks.

## Scope

This is an isolated integration candidate, not a release or deployment approval.
It combines rename commit `ff5b40138b2bf7710ae6d52e84e3f9839ef02021` with
`origin/develop` at `151267abf70c0671c912dd28e5f6fdf50fc07bb3`. The common ancestor
is `79ff91b65a40d806ce436b59051056d4cce27097`: 119 rename-only commits, 27 develop-only
commits and 77 initially conflicted paths.

Candidate branch: `integration/rename-develop-20261029`.
The original `feature/rename-pxa-to-pvad` worktree is unchanged. Its four pending
rename documents and pending policy fragment were backed up and verified by
SHA-256. Their uncommitted contents are not silently included in this candidate.
No editor configuration was inspected/copied. No remote branches, protections,
approvals, tags, versions, published manifests or deployments were changed.

## Reviewed Resolutions

- Keep technical PVAD and visible PVADE names, newer chart rendering, typed Code
  Worker, remote MCP/OAuth, packaged CLI, paid-checkout readiness and PDF Trust.
- Preserve stable diagnostics, legacy API/CLI readers, signed license material
  and published history. Do not replace functional changes with cosmetic renames.
- Merge manifests structurally and regenerate lockfiles; retain rename-only
  scripts and remote-server dependencies. Keep React 18, stable Grid 6.0.3,
  Node 22, OpenAPI.NET 2.x and coordinated managed/native Skia 3.119.2.
- Remove duplicate merged CI jobs/imports. Keep Trust, MCP beta and visual-parity
  jobs alongside incoming artifact checks. Retain fail-closed publication and
  environment controls. Normal version proposals stay paused.
- Update the structured MCP response schema for Zod 4. Add minimal fast-uri
  4.2.3 and qs 6.16.1 corrections and executable regressions. Sources:
  [fast-uri advisory](https://github.com/advisories/GHSA-w74x-v6f9-j3xg) and
  [qs advisory](https://github.com/advisories/GHSA-6pq8-jhm2-2vcw).
- Align both extra Nutrient hosts with the other private VSTest tools. Expand
  the collector matrix to 52 hosts; do not remove projects to retain the old 50.
- Give stateless API fixtures an explicit Testing host and unusable database
  address. Testing initializes OAuth stores explicitly after migrations; normal
  Development/Production startup still requires a valid persistent store.
- Run OpenAPI tests with a dedicated development fixture, workers removed and
  automatic OAuth disabled. Do not enable development endpoints in production.
- Preserve synthetic seed credentials and stable license error codes in imported
  tests. Keep the reviewed 97-icon baseline independently of new source imports;
  all current icons still receive geometry/accessibility checks.
- Adapt browser fixtures to current permissions, auth context, favorites, shared
  error pages and the PDF-contract chart renderer, without restoring old UI.

## Validation

Evidence: `artifacts/rename-integration/validation` in the isolated worktree.
Every recorded invocation has a command, exit status and retained output.
Initial failures remain separate from corrected runs.

| Area | Observed result |
| --- | --- |
| Full Solution Release build | Passed; existing warnings remain |
| Designer TypeScript and separate chart fixture project | Passed |
| Designer Jest | 780 tests / 90 suites passed; original icon snapshot retained |
| Designer and all five website production builds | Passed |
| Documentation / Admin contracts | 59 / 39 passed |
| Company / Account / Demo contracts | 58 / 53 / 4 passed |
| Rename configuration / version-tool tests | 215 / 27 passed |
| Coverage and dependency/artifact contracts | 16 passed |
| Complete collector smoke | 945 passed across 52 hosts, real nonempty production coverage |
| Persistence with disposable PostgreSQL | 13 passed, zero skipped |
| Full PDF / Word / Spreadsheet infrastructure suites | 57 / 6 / 13 passed |
| Collector-free webhook relay | 5 passed |
| MCP runtime / toolchain contracts | 29 / 7 passed |
| MCP typecheck, bundle and packaged stdio smoke | Passed |
| Designer browser | Desktop/mobile development/preview passed, including spreadsheet editing/history |
| Chart browser | 60 Preview / 6 Canvas fixtures passed: finite geometry, labels, negative axes and visible pixels |
| Admin browser | Four variants and both protected-handbook smokes passed |
| npm audit | Designer production and full MCP graph: zero known findings |
| Designer development audit | Five previously documented low findings remain |
| Full authenticated API/PostgreSQL | 1,319 passed, zero failures, one documented old-binary scenario skipped; final run `api-postgres-accepted` |

Browser fixtures use synthetic transport, not real authentication or deployed
host acceptance. The API maintenance matrix retains the documented native
`ImageConversionControllerTests` exclusion. The old-binary cutover test requires
its separately supplied pinned binary; a skip is not a pass. NPOI remains an
explicit production-license blocker; no EULA was accepted by these tests.
Old Recharts screenshots are not an unchanged baseline for the newer renderer.

## Delivery And Remaining Gates

No commit, push, remote merge, tag, version bump or deployment is included.
Preserve the original pending work until a reviewed transfer/commit is requested.
The final residual-name review records individually inspected changed files;
original signed evidence and unrelated review entries are not rewritten.
The candidate patch, tree identifier and file hashes are retained separately
from the original-worktree backup. None of these local results proves that
the combined Linux/GitHub Actions workflow has executed; that remains a PR gate.

Integration supports existing criteria; it does not add score-bearing tasks.
Rename progress remains **90/98 (91.8%) complete, 8/98 (8.2%) open**. Publisher
selection, independent production approval, retained-key recovery, external
host/mail validation and authorized cutover/release remain subject to the
existing approval register.

## PR Delivery Checkpoint: 2026-10-29

- [x] Commit the tested integration as `c5492a81` and transfer it by fast-forward
  to the rename branch without committing the four pending policy documents.
- [x] Push the rename branch and open [PR 210](https://github.com/silentdevcore/Canvas/pull/210)
  against `develop`, with `impact:major`. No stable release, deployment, approval
  or protection bypass is included.
- [x] Check the entire committed diff and correct existing CRLF/trailing-line
  formatting in the renamed examples and historical brand checklist (`46bb6023`).
- [x] Replace an optional search-binary dependency in documentation tests with
  native Node file enumeration (`804e90c1`), retaining all legacy-name assertions.
- [x] Supply every registered offline schema to the MCP container build, use
  absolute test-host artifact directories, support native ImageMagick 6/7 command
  names, and test Swiss currency layout without hard-coding a host ICU separator
  (`62104688`). No production number formatter or visual threshold was changed.
- [ ] Pass every required check on the final PR head; do not merge while blocked.

Actual GitHub run [40934298779](https://github.com/silentdevcore/Canvas/actions/runs/40934298779)
tested `62104688`. Version policy, impact synchronization, registry login,
artifact contracts, dependency security, and the complete frontend job passed.
The following failures remain explicit evidence, not waived checks:

| Check | Observed failure | Required follow-up |
| --- | --- | --- |
| MCP beta | Scan referenced a nonexistent fixed Compose-generated image name after the isolated TLS test passed | Retain the exact tested image under an explicit local scan tag, then rerun the scanner |
| .NET | The configured external validator image `ghcr.io/verapdf/cli:v1.31.166` returned `manifest unknown` after the fidelity fixture stages passed | Restore a reproducible approved validator source or review a replacement version; do not silently substitute a validator or skip PDF/UA and PDF/A checks |
| Visual parity | `optionlist` exceeded the existing P0 threshold: Canvas/Preview show a bulleted list, while the rasterized backend PDF shows a selected list-box appearance | Resolve the element's intended appearance and align both renderers, with regression coverage; do not change the baseline to conceal this difference |

The observed option-list perceptual RMSE is approximately 0.315, geometry drift
is zero, and backend versus generated C# PDF is exact for that element. The
downloaded report and all crops are retained under the isolated worktree's
`artifacts/rename-integration/github-run-40934298779/visual-parity` directory.
This is a real appearance difference, not a missing-file or geometry error.

Follow-up local checks passed: 219 configuration/image-tool tests, 27
version/release-author tests, five targeted text-renderer cases, MCP container
typecheck/build with 29 runtime tests, and all four PDF Trust visual fixtures.
These results do not close the two rendering/validator blockers or replace a
successful final Linux PR run. The NPOI decision and independent production
approval remain outside this integration's authority.

## Validator And Option-List Follow-Up

- [x] Recover the original manufacturer installer for veraPDF 1.31.166, verify
  SHA-256 `25cc4d87daf1c6eb9efd40487141a03f23bb65a92ba59e49e0f33a01343038da`,
  and build a non-root local validator using a digest-pinned Java 11 runtime.
  The actual `--version` result is 1.31.166. CI explicitly selects this local
  image; no registry publication or historical evidence rewrite is performed.
- [x] Validate the independent LibreOffice PDF/UA reference, four generated
  PDF/UA fixtures, and the current Designer table/link/form fixture successfully
  with that same validator version.
- [x] Share the option-list renderer between Canvas and Preview. Plain lists
  remain static; a field name, label, selected value, explicit multi-select flag,
  or Form accessibility role selects the PDF list-box representation. Retain
  list markers and start numbers outside form mode.
- [x] Avoid duplicate body text underneath interactive list boxes and make
  list-box widgets printable. Keep the existing field identities and values.
- [x] Pass 801 Designer tests in 91 suites, 14 focused Backend rendering and
  accessibility export tests, 16 PDF/UA generation tests, Designer typecheck,
  and 246 configuration/validator/version/release-author tests locally.
- [ ] Verify the final Linux PR run, including the previously skipped
  supply-chain job, before declaring this integration merge-ready.

The first corrected visual run has zero P0/P1 findings. Option-list perceptual
RMSE dropped from approximately 0.315 to 0.029, with zero geometry drift and
exact backend/C# raster equivalence. A static Roman-numbered list is added as
an additional regression fixture. Existing thresholds and approved baselines
are unchanged. See [the validator build instructions](../tools/pdf-trust/verapdf/README.md).

The Linux run at `41a5f53a` passed the Designer parity matrix and all independent
PDF/UA and PDF/A checks. It then exposed two later validation issues: native
Helvetica substitution differed from the original Mac PNGs, and a PostgreSQL
invariance test captured the pre-roundtrip 100 ns value rather than the stored
microsecond timestamp. The owner explicitly approved separate strict Linux
corpus references after the side-by-side review; original Mac files remain
unchanged. The migration test now reloads its baseline token after the initial
save and uses a deterministic submicrosecond fixture, without excluding the
expiry field or weakening the final equality assertion. All three migration
tests, including the real PostgreSQL variant, pass locally.

Run [40944792373](https://github.com/silentdevcore/Canvas/actions/runs/40944792373)
then passed the strict Linux corpus references, Designer parity matrix, frontend,
dependency-security and MCP jobs. Its later operation-image check exposed the
same native Helvetica substitution in the signature and redaction pairs;
embedded-font PDF/A and PDF/UA pairs already matched exactly. After a separate
side-by-side review, the owner explicitly approved additional strict Linux
operation references. They retain both input/output hashes and page geometry;
neither original Mac reference file nor any tolerance is changed. A successful
full final-head run, including supply-chain, is still required before merging.

The same run subsequently reached retained-license compatibility and exposed a
production replacement-path defect: its new `IssuedAt` was signed with 100 ns
precision, but PostgreSQL stores microseconds. New issuance already normalized
before signing; replacement now uses the same normalization with the existing
registered `TimeProvider`. The PostgreSQL regression supplies a deterministic
submicrosecond clock and asserts exact persisted/envelope timestamp equality.
Signature validation and existing signed history remain unchanged.

Local follow-up validation passed all 29 replacement, strict-validator and
retained-key compatibility cases, including real PostgreSQL rollback and
concurrent-replacement tests. All four original macOS operation pairs still
match, and all eight added Linux operation PNG hashes match the downloaded CI
artifacts exactly. These local results do not replace final-head Linux CI.
