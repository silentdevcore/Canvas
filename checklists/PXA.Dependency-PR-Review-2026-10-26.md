# Dependency PR Review - 2026-10-26

## Scope And Guardrails

Review the 26 Dependabot pull requests targeting `develop`. Perform corrections
in an isolated worktree; do not include the pending PVAD rename changes.
Keep `VERSION`, published releases, branch protection, and production unchanged.
An automated review through the owner's secondary account is not independent
human production approval.

## Shared Security Baseline

- [x] Identify the common NuGet and production npm findings blocking dependency CI.
- [x] Update XML cryptography to 10.0.13 in the three existing consumers.
- [x] Override NPOI's transitive build task without accepting its unresolved EULA.
- [x] Update the transitive SSH dependency in Testcontainers consumers only.
- [x] Refresh compatible Designer dependencies without changing the React major.
- [x] Update MCP SDK and Zod to patched versions; test protocol compatibility.
- [x] Add MCP schema default, enum and invalid-input smoke tests to CI.
- [x] Obtain human approval for the exact public security release summary.
- [x] Pass the solution restore and Release build of all 105 projects.
- [x] Pass the NuGet audit and the configured npm high/critical gate locally.
- [x] Pass 288 Designer tests, typecheck and production build.
- [x] Pass Word (3), Spreadsheet (6) and Converter (9) tests.
- [x] Pass MCP typecheck and expanded stdio smoke tests.
- [x] Pass 31 versioning/deployment tests and 3 dependency compliance tests.
- [x] Obtain successful remote CI including PostgreSQL and supply-chain checks.
- [x] Review the tested head and merge the baseline using normal protection rules.

Baseline PR #102 merged as `a4675af4227d8de5690ef47447933d084b3789ad`.
CI run `40344560266` passed every applicable job. This CI run does not replace
the explicitly recorded full local API/OCR results below.

Local API result: 280 passed, 61 skipped, 3 OCR tests failed. The isolated macOS
checkout has language data but no native OCR libraries in the test output.
Do not claim a complete local API pass; verify the Linux CI result before merge.
Persistence: 6 passed, 2 PostgreSQL tests skipped locally.

The Designer production audit still reports two moderate React Router findings.
Its compatible lockfile refresh does not fix them. A separately tested router
major upgrade is required; no advisory or existing CI rule has been suppressed.
The NPOI legal approval remains a production blocker.

## Dependency PR Queue

Only explicitly recorded merges are complete. All other rows remain pending
until the final head passes the applicable tests and receives a recorded review.
Updating a branch invalidates any earlier approval.

| PR | Update | Review requirement / current status |
| --- | --- | --- |
| #101 | upload-artifact 7.0.2 | Merged as `8e58ba9bc39dfb7ddf82b7e559c4b8858ccef9e1`; official pin, fragment, comments and compliance corrected; all applicable CI passed including artifact upload. |
| #98 | sbom-action 0.24.3 | Merged as `e7fe6f2aaf9c84c569530d3af453c21ccd776a02`; official annotated tag verified; fragment/compliance corrected; all applicable CI passed including actual container SPDX generation. |
| #99 | download-artifact 8.0.2 | Merged as `7ab074a10b53a0c182ee939dbb73fe116a4000bb`; all applicable CI passed, including real artifact handoff with nested paths, spaces, empty files and binary SHA-256 checks. |
| #91 | build-push-action 7.4.0 | Merged as `66253cb311589cdb12aa734c7176f8b6d72425b2`; all applicable CI passed, including an actual action build with publishing disabled, image-version verification and container SPDX generation. |
| #90 | setup-buildx-action 4.4.1 | Merged as `90c8bf74ae9239d23bb21988e3e15e4e7b6886a2`; all applicable CI passed including actual builder setup/bootstrap, container build and SBOM. |
| #100 | Admin Vite 8.3.4 | Fragment added; 26 local Admin tests, production build and full npm audit passed. Final tested head, CI, review and merge are recorded in [PR #100](https://github.com/silentdevcore/Canvas/pull/100). |
| #97 | MCP Node types 26.6.5 | Closed as superseded by merged PR #105 (`168e84a319a8ee3bf40a39ec27267f9f76d6d736`): Node 22 declarations match the validated runtime; the Node 26 proposal was not approved. |
| #95 | MCP SDK 1.32.0 | Closed as superseded by merged baseline PR #102 using 1.32.2; do not downgrade. |
| #94 | MCP tsx 4.23.15 | Closed automatically after merged PR #105; exact-head CI, tooling contracts, typecheck and all 14 stdio assertions passed. |
| #89 | Zod 4.6.5 | Closed with replacement explanation: this target remains affected by GHSA-mrhx-c7xc-m3w5; merged baseline PR #102 uses 4.6.7. |
| #78 | EF Core Design 10.0.11 | Superseded by merged platform PR #103 (`865007bd7283540867d716b1dddcc058425d7fa0`), with exact-head CI and disclosed technical review. |
| #75 | Roslyn Scripting 5.9.0 | Closed as superseded by merged PR #104 (`9903b202400127420ec3d3eb9455e682319f6d28`), with full graph alignment and exact-head CI. |
| #74 | Roslyn CSharp 5.9.0 | Closed as superseded by merged PR #104; all 556 migration tests and 375 PostgreSQL-enabled API tests passed. |
| #73 | ASP.NET OpenAPI 10.0.11 | Superseded by merged platform PR #103; API contracts and PostgreSQL tests passed. |
| #72 | ASP.NET MVC Testing 10.0.11 | Superseded by merged platform PR #103; API and relay tests passed. |
| #71 | Identity EF 10.0.11 | Superseded by merged platform PR #103; EF graph, identity and PostgreSQL tests passed. |
| #70 | dotnet-ef 10.0.11 | Superseded by merged platform PR #103; local tool restore and unchanged model confirmed. |
| #45 | Markdig 1.3.2 | Major dependency update: run Markdown importer regressions. |
| #43 | coverlet 10.0.1 | Major dependency update across test projects: verify test SDK/collector compatibility. |
| #41 | ClosedXML 0.105.1 | Closed automatically after merged PR #106 (`c4c62ae110442a08a87c6e56a51f35c040ce0080`); all 38 workbook/export cases and 375 PostgreSQL-enabled API cases passed, followed by exact-head CI. |
| #40 | Recharts 3.10.1 | Run Designer tests and chart rendering smoke tests. |
| #39 | React 19 | Changes requested on `728054e1`: React 19 with React DOM/types 18; current react-beautiful-dnd peers exclude 19. Requires a coordinated migration. |
| #38 | React DOM 19 | Changes requested on `0cfc53f0`: React DOM/types 19 with React/types 18; same unsupported drag-and-drop peers. Do not bypass peer validation. |
| #37 | jest-dom 7 | Major test dependency update: run the full Designer test suite. |
| #36 | react-icons 5.7 | Run Designer typecheck and build. |
| #31 | TypeScript 7 | Major compiler upgrade: verify SDK/compiler configuration and MCP smoke. |

## Completion

- [ ] Update and retest every safe PR against current `develop`.
- [ ] Record approvals against exact tested commit IDs.
- [ ] Merge safe PRs without administrator bypass or force pushes.
- [ ] Leave incompatible or superseded PRs with an explicit disposition.
- [ ] Record merged, pending and blocked counts after the final review.

Primary references: [Zod advisory](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5),
[Zod migration guide](https://zod.dev/v4/changelog).

All three changed npm lockfiles were checked structurally: package download
sources use `https://registry.npmjs.org` without embedded credentials and every
resolved package has SHA-256, SHA-384 or SHA-512 integrity metadata.

## .NET Platform Alignment Follow-Up

Combine the overlapping updates from #70, #71, #72, #73 and #78 into one
compatibility-tested maintenance change. Align local `dotnet-ef`, EF Design,
Identity EF, OpenAPI, both MVC test hosts, InMemory and EF health checks at
10.0.11. Keep Npgsql at its existing compatible provider version. Roslyn is a
separate follow-up, not an implicit compiler upgrade in this batch.

Before any package change, the PostgreSQL-enabled API baseline ran 339 tests:
338 passed and one failed. The failing Admin test expected 401 for a correct
password on a disabled account; the existing implementation and dedicated Auth
contract already require 403 with `PXAAPI015`. Correct that obsolete expectation,
assert the explicit diagnostic, and retain the check that protected Admin
requests remain unauthenticated. Do not change application authorization.

The same older scenario also lacked an active organization for its non-allowlisted
legacy operator. Verify that login is denied without a membership, then add an
ordinary Viewer membership and retain the assertions that the legacy global
role is not exposed and Admin access is denied. Production authorization is
unchanged; no permission assertion is removed.

The five native image/OCR API cases are outside this platform matrix. Their
previously recorded macOS native-library limitation is not claimed as resolved.

- [x] Review the five original dependency diffs and the additional alignment pins.
- [x] Record an unchanged database model before the update.
- [x] Restore and build all 105 solution projects with aligned packages.
- [x] Pass the NuGet direct/transitive vulnerability gate for all 105 projects.
- [x] Add and pass 16 project-reference/resolved-graph alignment contracts.
- [x] Restore local EF tooling and verify no pending model changes after the update.
- [x] Add PostgreSQL-enabled API, persistence and read-only EF checks to CI.
- [x] Pass 355 updated API tests with PostgreSQL enabled and no skipped cases.
- [x] Pass all 8 persistence tests and 5 relay tests without skips.
- [x] Pass the 29 versioning, release-author and dependency compliance tests.
- [x] Record final CI against the exact maintenance commit and obtain a technical review.
- [x] Merge the maintenance change normally (`865007bd7283540867d716b1dddcc058425d7fa0`).
- [x] Close #70, #71, #72, #73 and #78 with cross-links without deleting their source branches.

Platform PR [#103](https://github.com/silentdevcore/Canvas/pull/103) passed
version-policy, .NET, frontend, dependency-security, artifact-contract, impact
label synchronization and supply-chain checks at exact head
`d90e6ace30495f83d1e3a84a6a2f7d317aa42fba` before its normal squash merge.
The `mabadev` review explicitly identifies the owner's secondary account and
is not an independent human production approval.

No database migrations, product version changes, production deployments or
changes to the uncommitted rename worktree are part of this maintenance batch.

### Linux CI Precision Regression

The first PR #103 Linux run passed build, dependency security, EF model checks
and all 8 persistence tests, but failed 2 of the 355 API tests. PostgreSQL stores
microsecond timestamps, while a Linux clock may supply sub-microsecond ticks.
Signing those extra ticks before persistence caused strict envelope comparison
to fail after reload. The same failure was reproduced locally using explicit
sub-microsecond validity dates; it is not an authorization or dependency bypass.

- [x] Reproduce the failure deterministically before changing issuance.
- [x] Normalize issuance dates to UTC microseconds before signing and persistence.
- [x] Preserve exact timestamp comparison and signature verification.
- [x] Add timestamp precision and tampering regression tests.
- [x] Pass all 363 API tests with PostgreSQL after the fix, with no skipped cases.
- [x] Confirm the EF model is unchanged and pass all 29 release/compliance tests again.
- [x] Pass exact-head Linux CI after the fix.

Existing inconsistent signed records are not silently rewritten or re-signed;
they require normal authorized license replacement. No database migration is needed.

## Roslyn Alignment Follow-Up

Combine #74 and #75 into one compatibility-tested update. Both migration
compiler references and API Scripting, CSharp Workspaces and MSBuild Workspaces
must move together to 5.9.0. EF tooling remains independently aligned at 10.0.11;
no provider behavior or sandbox policy change is planned.

Before updating Roslyn, all 556 migration tests in 34 projects passed without
skips against the platform maintenance baseline. Keep those TRX artifacts as
the before/after comparison rather than assuming a successful build proves
runtime compatibility.

- [x] Review the original two PRs and all five direct Roslyn references.
- [x] Record the complete 34-project migration baseline: 556 passed, 0 failed, 0 skipped.
- [x] Align all five direct compiler, scripting and workspace references.
- [x] Add project-reference and resolved-runtime graph contract tests.
- [x] Add the complete migration-provider regression matrix to CI.
- [x] Restore and build all 105 solution projects with Roslyn 5.9.0 (0 errors; 65 existing warnings).
- [x] Pass the NuGet direct/transitive vulnerability gate for all 105 projects.
- [x] Pass all 28 alignment contracts and 375 PostgreSQL-enabled API tests without skips.
- [x] Confirm the EF model remains unchanged with the updated compiler graph.
- [x] Rerun all 556 migration tests across 34 projects without skips.
- [x] Pass all 29 version/compliance contracts; keep product version at 1.1.0.
- [x] Validate the committed Roslyn release fragment against merged `develop`.
- [x] Obtain exact-head green CI and a disclosed technical review.
- [x] Merge normally and close #74 and #75 as superseded with replacement links.

Roslyn PR [#104](https://github.com/silentdevcore/Canvas/pull/104) merged as
`9903b202400127420ec3d3eb9455e682319f6d28` after all applicable checks passed at
`183753268599d6cd7b29dc1338cdf89c32c8a259`. The owner's secondary-account
technical review was explicitly disclosed. Both original source branches were
preserved. Eleven original dependency PRs remained open after that merge.

The Roslyn work started on the platform maintenance branch, then incorporated
the merged `develop` baseline before its own PR. Do not treat the owner's
secondary GitHub account as an independent production approver.

## MCP Toolchain Follow-Up

Review #94 (`tsx` 4.23.15) and #97 (`@types/node` 26.6.5) together. Existing
CI and local validation use Node 22; the previous declarations targeted Node
26 despite that runtime baseline. Use `@types/node` 22.20.5 instead, keeping
the API surface checked by TypeScript aligned with the runtime actually tested.
This is a development-only declaration correction, not a Node runtime upgrade
or a new minimum imposed on shipped applications. Keep TypeScript 5.9.3,
MCP SDK 1.32.2 and Zod 4.6.7 unchanged.

Before the update, typecheck and all 14 stdio smoke assertions passed on Node
22.14.0. The replacement must retain those results after a clean install.
The smoke test exercises tool discovery, examples, defaults, validation errors,
search and resource discovery. Real backend rendering, credentials and customer
data are outside this dependency test.

- [x] Inspect both exact original PR diffs and upstream npm metadata.
- [x] Record the unchanged Node 22 typecheck and stdio baseline.
- [x] Update the runner and align declarations with Node 22.
- [x] Add declaration/lockfile/installed-tooling contracts to CI.
- [x] Start the stdio server through the current Node and local `tsx`, not `npx`.
- [x] Record the completed Roslyn review and merge evidence above.
- [x] Complete a clean install, all 3 contracts, typecheck and all 14 stdio assertions on Node 22.14.0.
- [x] Pass the complete MCP npm vulnerability audit (0 findings) and inspect the lockfile diff.
- [x] Pass all 29 release/version/compliance tests and fragment validation; keep version 1.1.0.
- [x] Pass exact-head CI and obtain the disclosed technical review.
- [x] Merge the replacement normally, then close #94 and #97 with cross-links.

MCP PR [#105](https://github.com/silentdevcore/Canvas/pull/105) merged as
`168e84a319a8ee3bf40a39ec27267f9f76d6d736` after all applicable checks passed at
`e7bedc33ab43f90e0e376e28d4d66f2001444185`. The technical secondary-account
review was disclosed, not represented as independent human approval.
Nine original dependency PRs remained open afterwards.

Primary references: [DefinitelyTyped version numbers](https://github.com/DefinitelyTyped/DefinitelyTyped#version-numbers),
[tsx package](https://www.npmjs.com/package/tsx/v/4.23.15).

## ClosedXML Compatibility Follow-Up

Review #41 against current `develop`, not its older dependency graph. Apply
only ClosedXML 0.105.0 to 0.105.1; retain the existing cryptography 10.0.13
and private build-task 10.0.400 security pins. NPOI remains unchanged and its
outstanding legal approval is not granted by this update.

The upstream patch bounds SixLabors.Fonts to `[1.0.0,3.0.0)`. Before the update,
the resolved Spreadsheet graph uses Fonts 1.0.0, OpenXML 3.1.1 and Parser 2.0.0.
The API/export composition can resolve a newer compatible OpenXML package;
test both the standalone infrastructure and existing composed export paths.
This change does not add new workbook features or claim complete format fidelity.

Baseline: 6 infrastructure tests and 26 selected Spreadsheet/Excel export tests
passed without skips on 0.105.0. Six additional compatibility cases also passed
against that same old package before changing the pin. These validate real
XLSX packages with OpenXML validation and exercise culture-independent JSON
values, cross-sheet named formulas, styles, merged cells, frozen panes, print
settings, validation, protection and font measurement in design exports.

- [x] Review the exact original diff and the official upstream release metadata.
- [x] Pass the existing 32-test baseline before changing the dependency.
- [x] Add and pass six compatibility cases on the old package (12 infrastructure cases total).
- [x] Apply only the workbook patch and preserve existing security pins.
- [x] Add the focused Spreadsheet/Excel regression matrix to CI.
- [x] Record the completed MCP merge and original PR dispositions above.
- [x] Restore and build all 105 solution projects against the patched dependency (0 errors; 65 existing warnings).
- [x] Compare the inspected before/after graph and pass the vulnerability gate for all 105 projects.
- [x] Pass all 12 infrastructure and 26 selected export cases on the new package, without skips.
- [x] Pass all 375 PostgreSQL-enabled API cases, including the Excel endpoint, without skips in the established matrix.
- [x] Pass all 29 version/release/compliance contracts and fragment validation; keep product version 1.1.0.
- [x] Obtain exact-head CI and the disclosed technical review.
- [x] Merge normally and close original #41 as superseded with replacement evidence.

ClosedXML PR [#106](https://github.com/silentdevcore/Canvas/pull/106) merged as
`c4c62ae110442a08a87c6e56a51f35c040ce0080` after all applicable checks passed at
`f230cc73d5ab4f913012863edfa8b01021b1ad13`. The technical review explicitly
disclosed the owner's secondary account; it was not independent human approval.
The original #41 source branch was preserved. Eight original dependency PRs
remained open after that merge.

Primary references: [ClosedXML 0.105.1 release](https://github.com/ClosedXML/ClosedXML/releases/tag/0.105.1),
[NuGet dependency contract](https://www.nuget.org/packages/ClosedXML/0.105.1).

The five native image/OCR API cases remain outside the established maintenance
matrix and are not claimed as fixed. No existing test or security gate is weakened.

## Designer Icon Compatibility Follow-Up

Review #36 against merged `develop`. Update only `react-icons` 5.6.0 to 5.7.0,
with the matching manifest range and registry integrity. Keep React, React DOM,
types, router, charts and all other resolved packages unchanged. The existing
peer-install configuration is unchanged, not introduced to bypass this review.

The source imports Feather icons only. Add a TypeScript-AST contract that
discovers every production icon import, verifies the exports, renders them in
the DOM, and checks SVG geometry, dimensions, color and accessible titles.
Capture its aggregate SVG fingerprint on the old package first; require the
same fingerprint after the update without refreshing the snapshot.

- [x] Inspect the exact original PR diff and official release/npm metadata.
- [x] Pass the original 288 Designer tests and typecheck before the update.
- [x] Pass the expanded 290-test suite, SVG baseline and production build on 5.6.0.
- [x] Update only the icon dependency and its lockfile entries on current `develop`.
- [x] Perform a clean install and pass all 290 tests, typecheck and production build on 5.7.0.
- [x] Preserve the old-package SVG fingerprint without updating the snapshot.
- [x] Confirm no other resolved package entries changed and pass the production high/critical audit gate.
- [x] Record completed ClosedXML CI, technical review, merge and original PR closure.
- [x] Pass all 29 release/version/compliance contracts and version/fragment validation.
- [ ] Validate the committed release fragment against `origin/develop`.
- [ ] Obtain exact-head green CI and a disclosed technical review.
- [ ] Merge normally and close original #36 as superseded with replacement evidence.

The production audit still reports two moderate router findings. The complete
development-inclusive audit reports eight affected package entries: two moderate,
five high and one critical. The six additional entries are brace-expansion,
handlebars, js-yaml, nanoid, source-map-js and Vite, all unchanged by this icon
update. A separate development-tool security review is required; this batch does
not claim a clean full audit, suppress advisories or run forced major upgrades.
The Vite configuration and bundle-size warnings also predate this change.

The new checks run in the existing Designer Jest CI step. They are DOM component
tests, not a real authenticated browser/backend or production deployment test.
Keep `VERSION` at 1.1.0 and leave the uncommitted rename worktree untouched.

Primary references: [react-icons 5.7.0 release](https://github.com/react-icons/react-icons/releases/tag/v5.7.0),
[npm package](https://www.npmjs.com/package/react-icons/v/5.7.0).
