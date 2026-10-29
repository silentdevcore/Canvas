# Rename And Develop Integration

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
