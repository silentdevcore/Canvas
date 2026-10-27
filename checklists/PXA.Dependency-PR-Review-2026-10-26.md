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
| #45 | Markdig 1.3.2 | Closed by Dependabot without merge; replacement #114 proposes 1.5.0 and still requires Markdown importer regressions. |
| #43 | coverlet 10.0.1 | Closed by Dependabot without merge; replacement #112 proposes 10.1.0 and still requires collector compatibility tests. |
| #41 | ClosedXML 0.105.1 | Closed automatically after merged PR #106 (`c4c62ae110442a08a87c6e56a51f35c040ce0080`); all 38 workbook/export cases and 375 PostgreSQL-enabled API cases passed, followed by exact-head CI. |
| #40 | Recharts 3.10.1 | Closed automatically after merged PR #123 (`4aa7efa47fc2a3d7b6072c6f5b64408fc358ee40`); 295 tests, 60 Preview cases, six unchanged Canvas images and exact-head CI passed. |
| #39 | React 19 | Changes requested on `728054e1`: React 19 with React DOM/types 18; current react-beautiful-dnd peers exclude 19. Requires a coordinated migration. |
| #38 | React DOM 19 | Changes requested on `0cfc53f0`: React DOM/types 19 with React/types 18; same unsupported drag-and-drop peers. Do not bypass peer validation. |
| #37 | jest-dom 7 | Closed automatically after merged PR #122 (`2084e3a4f2faf89ec7d8649760bbbde438028cd3`); explicit DOM peer, 295 tests, typecheck, build and exact-head CI passed. |
| #36 | react-icons 5.7 | Closed automatically after merged PR #107 (`afeac1b3f8ee7ba590b23423539f7f5bf1374318`); 290 Designer cases, unchanged 97-icon SVG fingerprint, typecheck, build and exact-head CI passed. |
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
- [x] Validate the committed release fragment against `origin/develop`.
- [x] Obtain exact-head green CI and a disclosed technical review.
- [x] Merge normally and close original #36 as superseded with replacement evidence.

Icon PR [#107](https://github.com/silentdevcore/Canvas/pull/107) merged as
`afeac1b3f8ee7ba590b23423539f7f5bf1374318` after all applicable checks passed at
`123b37bbe8025fac1b6c99ec047a934eca998a98`. The technical review disclosed the
owner's secondary account, not independent human production approval.

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

## Designer Development Security Follow-Up - 2026-10-27

The previous icon batch identified six unchanged high/critical development
package entries. Correct those without forcing a router or React major update.
The baseline passes 290 Designer tests, typecheck and production build, but its
full npm audit reports two moderate, five high and one critical package entries.

| Package | Before | Updated |
| --- | --- | --- |
| Vite | 8.2.0 | 8.3.5 |
| Handlebars | 4.7.9 | 4.7.10 |
| js-yaml | 3.15.1 | 3.15.2 |
| nanoid | 3.3.16 | 3.3.20 |
| source-map-js | 1.2.1 | 1.2.2 |
| test-exclude's brace-expansion | 1.1.18 | 1.1.21 |

Vite's required development graph also updates Rolldown, native bindings, OXC
types, PostCSS and picomatch. Removed optional WASI bindings follow the upstream
Rolldown dependency declaration. All other production dependency entries remain
unchanged. Registry download origins and integrity metadata are validated.
No override, forced major upgrade or peer-policy change is introduced.

- [x] Verify advisory ranges, compatible fixes and the existing dependency paths.
- [x] Record the 290-test, typecheck, build and eight-entry full-audit baseline.
- [x] Update the six affected tools and only their required development graph.
- [x] Perform a clean install; pass all 290 cases with coverage, typecheck and production build.
- [x] Pass the full development-inclusive npm high/critical gate; only two moderate router entries remain.
- [x] Add an explicit Designer development audit to CI without removing any production gate.
- [x] Add a compliance policy and contract test for the stronger audit scope.
- [x] Pass all 30 version/release/compliance tests and the policy validator.
- [x] Add development-server and production-preview browser smoke tests with desktop/mobile screenshots.
- [x] Pass four local Chromium scenarios covering Home, SPA gallery deep links and search/reset.
- [x] Obtain human approval of the exact public security summary before adding the fragment.
- [x] Record the actual icon merge and reconcile the changed Dependabot queue.
- [x] Validate the final committed fragment and diff against `origin/develop`.
- [x] Pass exact-head Linux CI, including the new full audit and four browser scenarios.
- [x] Obtain a disclosed technical review and merge normally without bypassing protection.

Security PR [#121](https://github.com/silentdevcore/Canvas/pull/121) merged as
`84a48364465079ff28760ff924dcce9219033ebc` after all applicable checks passed at
`3ccfe2b4b57183281b4140ec0394e1cfe4d2abe7`. The owner's secondary-account
technical review was explicitly disclosed, not independent human production
approval. Eighteen other dependency PRs remained open afterwards.

Smoke evidence is generated in `artifacts/designer-tooling-smoke/`: eight viewport
screenshots plus `report.json`. API responses use synthetic users and workspaces;
external font CSS is stubbed and other external requests are rejected. This proves
frontend bundle loading and interactions, not real authentication, backend PDF
generation, font parity, Windows support or deployment readiness. The mobile
scenario closes the existing navigation drawer through its normal button before
interacting with the gallery; no forced click or application CSS change is used.
The smoke servers use loopback and ephemeral ports and are closed after testing.
Run `node tools/dependencies/designer-tooling-smoke.mjs` after installing Designer
dependencies, building the Designer, and installing Playwright Chromium.
Synthetic CSRF and telemetry responses prevent real backend traffic. Aborted
requests during navigation or development StrictMode cleanup are recorded in
the report, separately from unexpected transport errors. Existing telemetry may
classify an intentional fetch cancellation as `api_failure/network_error`; this
tooling smoke does not certify telemetry semantics. Runtime/unhandled errors,
non-abort transport errors, failed scripts/styles and unexpected API operations
remain failures.

Coverage reporting runs successfully but remains limited by the existing suite
(approximately 40% statements); it is not a claim of full application coverage.
Existing Vite configuration and chunk warnings remain; the newer bundler also
reports already-mixed static/dynamic locale imports. No warning is suppressed.
`VERSION` stays 1.1.0, NPOI legal approval stays blocked, and rename work is untouched.

Queue reconciliation: after #107, 18 dependency PRs were open: five original
proposals (#31, #37, #38, #39, #40), plus #108-#120. The new proposals have not
been implicitly approved. #108 repeats the unvalidated Node 26 types proposal;
keep runtime-aligned Node 22 declarations until a separately tested runtime change.
#109 targets Admin Vite, not Designer Vite, and is not fulfilled by this batch.

Separate follow-ups, not acceptance claims of this security batch:

- [ ] Test and migrate React Router to a corrected major version for the two moderate findings.
- [ ] Review existing browser telemetry classification of intentional fetch cancellation.
- [ ] Correct the existing mobile sidebar toggle overlap with the header logo; application CSS is unchanged here.

Primary references: [Handlebars advisory](https://github.com/advisories/GHSA-p8wg-vrv2-v86f),
[js-yaml advisory](https://github.com/advisories/GHSA-2883-xcg3-v3hh),
[source-map-js advisory](https://github.com/advisories/GHSA-68fv-2mgg-jv7q),
[Vite advisory](https://github.com/advisories/GHSA-587h-7wmj-h5pm).

## Designer Jest-DOM Compatibility Follow-Up - 2026-10-27

Review #37 on the merged security baseline. Its original head
`297ecd95cf6145cb0a5d7dc0ccc6ae39bdab6c3c` proposes 7.0.0 but omits the newly
required `@testing-library/dom` peer. The existing React Testing Library also
requires DOM 10. Use the current compatible correction 7.0.1 and explicitly
declare DOM 10.4.2; Vitest remains optional and is not installed.

Jest-DOM 7 requires Node 22. CI and local review already use Node 22; document
22.12 or newer within that major to satisfy Vite as well. Correct the obsolete
Node 18 contributor prerequisite without changing API, browser or deployed
application runtime contracts. Do not modify the existing peer-install setting
or force an incompatible installation.

The original 290 cases pass before any changes. With the DOM peer added, four
new standard matcher cases and stronger real release-history assertions pass
on Jest-DOM 6.9.1: 294 cases total. Those same 294 also pass on 7.0.1. Add one
v7-specific query-matcher case to verify that the required DOM peer is actually
used, including single/multiple matches and failure behavior: 295 cases total.
Synthetic form controls test the toolchain; they do not add product features.

- [x] Review the exact original diff and upstream Node/peer/release metadata.
- [x] Pass all 290 original tests and typecheck before the update.
- [x] Add the explicit DOM peer and pass the expanded 294-case baseline on 6.9.1.
- [x] Use Jest-DOM 7.0.1 and DOM 10.4.2 without installing optional Vitest.
- [x] Pass all 295 cases with coverage, unchanged icon snapshot, typecheck and production build.
- [x] Pass the full development-inclusive npm high/critical audit; two moderate router entries remain unchanged.
- [x] Confirm only development dependency entries change; retain the prior security fixes and CI gates.
- [x] Document the contributor toolchain and matcher setup in README and TESTING.
- [x] Record the completed #121 review and normal merge above.
- [x] Repeat the final clean install and all four desktop/mobile development/preview bundle smoke checks.
- [x] Pass all 30 release/version/compliance contracts, policy validation, version synchronization and fragment validation.
- [x] Validate the final committed fragment against `origin/develop`.
- [x] Obtain exact-head Linux CI and a disclosed technical review.
- [x] Merge normally and close original #37 as superseded with replacement evidence.

Jest-DOM PR [#122](https://github.com/silentdevcore/Canvas/pull/122) merged as
`2084e3a4f2faf89ec7d8649760bbbde438028cd3` after all applicable checks passed at
`77f5b8ce0cdd02e8538c4eaec400b760e3aea131`. The owner's secondary-account
technical review was disclosed and is not independent human production approval.
Seventeen other dependency proposals remained open afterwards.

Release impact is `none` with an explicit reason: only development dependencies,
regression tests and contributor test instructions change. The dependency's
major version does not change shipped product behavior or public SDK/API contracts.
`VERSION` remains 1.1.0. Existing coverage remains approximately 40% statements,
not complete product coverage. Browser smoke responses remain synthetic, and
the existing router, telemetry, mobile-layout and NPOI follow-ups stay open.

Primary references: [Jest-DOM 7.0.0 requirements](https://github.com/testing-library/jest-dom/releases/tag/v7.0.0),
[Jest-DOM 7.0.1 correction](https://github.com/testing-library/jest-dom/releases/tag/v7.0.1),
[DOM Testing Library](https://www.npmjs.com/package/@testing-library/dom/v/10.4.2).

## Designer Recharts Compatibility Follow-Up - 2026-10-27

Review original #40 at `21fb1f50b0a32b7e5308446983a24d703ff92ae2` against
current `develop`, preserving the completed tooling/security updates. Recharts
3.10.1 supports the existing React 18 runtime. Its required runtime graph changes
Immer 10 to 11 and Reselect 5.1.1 to 5.2.0; inspect the resolved graph rather
than copying the older lockfile from the original proposal.

The current consumers support bar, line and pie. Live Preview uses Recharts;
the normal Canvas still uses a simplified independent renderer. No seven-type
chart engine or backend PDF parity is claimed by this dependency update.

- [x] Review exact source diff, upstream releases, peers and package lifecycle scripts.
- [x] Pass the unchanged 295-case Designer suite and typecheck on Recharts 3.8.1.
- [x] Add a browser fixture importing actual Preview and Canvas components, not chart mocks.
- [x] Record 60 before-update Preview cases and six Canvas screenshots across development/production and desktop/mobile.
- [x] Update only Recharts and its required dependency graph; inspect clean-install integrity and peers.
- [x] Compare all 60 updated cases, inspect geometry/pixel changes, and verify tooltips and negative values.
- [x] Pass the full Designer suite, typecheck, build and development-inclusive vulnerability gate.
- [x] Confirm the normal production build excludes the test fixture.
- [x] Add CI chart contracts and retained screenshots without weakening existing checks.
- [x] Repeat the final browser checks after hardening test transport and Canvas comparison assertions.
- [x] Validate the final committed patch fragment against `origin/develop`.
- [x] Pass all 30 release/version/compliance contracts.
- [x] Obtain exact-head green Linux CI and a disclosed technical review.
- [x] Merge normally and close original #40 with final evidence.

Recharts PR [#123](https://github.com/silentdevcore/Canvas/pull/123) merged as
`4aa7efa47fc2a3d7b6072c6f5b64408fc358ee40` after all applicable checks passed at
`c7809664b4624457cba5c1bcf81c625e54c06c87`, including the new Linux chart test
and screenshot collection. The disclosed secondary-account technical review
is not independent human production approval. Sixteen proposals remained open.

Transient ResponsiveContainer measurement warnings already occur in the old
development build before ResizeObserver measures its parent. The test requires
the settled chart to have finite geometry and its exact allocated dimensions;
the warning is not silently treated as a new regression or claimed fixed.
Null input is converted to zero by the existing application mapping. Long labels
and narrow charts are inspected rather than claimed universally unclipped.

The test-only HTML entry is included only by an explicit isolated fixture build,
not the ordinary application build. Servers bind loopback on ephemeral ports
and close in cleanup. No real login, external font, backend PDF, or deployment
claim is made. Product `VERSION` remains 1.1.0 and rename changes stay untouched.

Clean installation resolves Recharts 3.10.1, Immer 11.2.9 and Reselect 5.2.0.
Only these required nodes and deduplication of the former nested Immer change.
All package sources and integrity fields were inspected; the registry package
has no install lifecycle hook. Its source-repository `prepare` hook is Husky.

The final comparison passes all 60 cases and all six byte-identical Canvas
screenshots, plus the four existing application tooling smoke scenarios.
Fifty-four Preview screenshots are byte-identical PNGs; the report also records
differences with a per-channel tolerance of 16. Six desktop
bar cases (standard, edge, gaps in both build modes) change bar width by one pixel
and the second series offset by one pixel. The largest changed-pixel fraction is
1.67%. Axes, labels, dimensions, colors, legends and data remain unchanged.
These actual differences were inspected, not hidden by replacing a baseline.
All 295 Designer tests, ordinary build, both source/fixture typechecks and 30
release/compliance contracts pass. The npm high/critical gate passes with the
same two existing moderate router entries.

The broad `npm ls` check exits with existing React peer-range conflicts from
`react-json-view` and its `flux` dependency. Their entries and peer declarations
are unchanged from `develop`; Recharts accepts the installed React 18 runtime.
Do not claim the entire dependency graph has clean peers. Replacing the legacy
JSON-view package belongs with the separately reviewed React migration; no new
force-install option or peer-conflict suppression was added here.

Primary references: [Recharts 3.10.0](https://github.com/recharts/recharts/releases/tag/v3.10.0),
[Recharts 3.10.1](https://github.com/recharts/recharts/releases/tag/v3.10.1).

## .NET Platform Maintenance Follow-Up - 2026-10-27

Combine the six overlapping proposals against current `develop` instead of
merging individually inconsistent dependency graphs:

| PR | Reviewed head | Scope |
| --- | --- | --- |
| #113 | `1e0e77accc9db986971904e196013b18a4f2e822` | Local dotnet-ef 10.0.14 |
| #115 | `f9b479c8998376a0a0fd84b51212eeec0ea469ac` | Identity EF 10.0.14 |
| #116 | `cc9fbb1c622ee489e33937516adf3d64958bf713` | Both MVC test hosts 10.0.14 |
| #117 | `c39e577cb40b18ba0dd521a202a835cae989f8db` | ASP.NET OpenAPI 10.0.14 and OpenAPI model 2.12.2 |
| #119 | `96e99efa1ef6941fe5d82b879d9dc2dac453b623` | Both EF Design references 10.0.14 |
| #120 | `ec0c9105f1738eeae37854f60bea12595122993d` | EF InMemory 10.0.14 |

Include EF health checks in the aligned 10.0.14 matrix. Keep Npgsql provider and
telemetry at validated 10.0.3, Roslyn at 5.9.0, the existing cryptography/build
task pins and all application versions unchanged. Build-task, Markdig and
coverage-collector proposals remain separate, not implicitly approved here.

Before changing package versions, all 375 API maintenance cases and all eight
persistence cases pass with isolated PostgreSQL and no skips. The read-only EF
model check reports no pending changes. The new actual-controller OpenAPI schema
test also passes on the old library after enabling its required generated
interceptor namespace in the test project. Its first compile failure was test
setup, not an application regression; no production setting is changed.

- [x] Inspect all six exact source diffs and upstream package dependencies.
- [x] Record the 375-case API and eight-case persistence baseline without skips.
- [x] Confirm the pre-update EF model has no pending changes.
- [x] Add and pass actual-controller OpenAPI generation and reference checks on the old version.
- [x] Align the eight project references and local EF tool; update the explicit OpenAPI compliance pin.
- [x] Restore all 105 projects and verify the local EF tool reports 10.0.14.
- [x] Add resolved OpenAPI and unchanged PostgreSQL-provider graph contracts.
- [x] Build all 105 projects and pass the full direct/transitive NuGet vulnerability gate.
- [x] Pass the expanded PostgreSQL-enabled API matrix, all persistence cases and relay tests.
- [x] Confirm the updated EF model remains unchanged without applying or generating migrations.
- [x] Pass all 30 version, release-author and dependency-compliance tests and check synchronized version metadata.
- [x] Validate both committed patch fragments against `origin/develop`.
- [x] Pass exact-head Linux CI, including API, migrations, frontend charts and container/SBOM checks.
- [x] Obtain a disclosed technical review, merge normally and close the six superseded PRs.

The OpenAPI test uses real controller metadata but a minimal isolated TestServer;
it neither starts workers nor accesses a real database or authenticates a user.
The full API matrix covers authentication, organization isolation, legal records,
subscriptions and template persistence against disposable PostgreSQL containers.
No customer database, SQL migration, rename change, release tag, version bump,
deployment, peer bypass or NPOI license acceptance is part of this batch.
The native OCR/API exclusions remain explicit; they are not silently marked fixed.

### Concurrent Draft Validation Finding

The first updated API run reports 378 passed, one failed and no skips out of
379 cases. The failure is MVC record-constructor metadata lookup during two
concurrent first draft-save requests; an isolated repeat passes. This matches
the documented upstream lazy-metadata race and is not evidence that the package
patch introduced it. Local shared ASP.NET runtime is 10.0.13; changing NuGet
packages does not itself patch that shared runtime.

- [x] Keep the original failing API/TRX evidence and reproduce the unsafe record-validation path with a deterministic guard.
- [x] Add a parameterless constructor only to the draft request, avoiding record-constructor mapping while retaining JSON fields, positional construction and deconstruction.
- [x] Pass all 24 focused contracts, including JSON with/without optional versions and 128 concurrent validations across 16 fresh metadata providers.
- [x] Repeat the complete API matrix after the targeted fix, including the original optimistic-concurrency and tenant-isolation scenario.

Final local results: all 383 API cases, eight persistence cases and five relay
cases pass with no skips. The expanded API count includes the original 375,
three additional resolved-graph contracts, one OpenAPI generation test and four
draft-validation cases. All 105 projects build after the mitigation, the NuGet
direct/transitive gate passes, both post-update EF checks report no changes, and
all 30 release/compliance contracts pass. The initial failed 378/379 run remains
recorded; it has not been overwritten or reclassified as a success.

No validation, authorization, CSRF, tenant, ETag or conflict assertion is removed.
This is a narrow mitigation for the draft DTO, not a global fix for the upstream
framework race. Track a future supported runtime fix and review other concurrent
record consumers separately. A second patch fragment describes the observed
Designer save fix independently from the compatible dependency maintenance.

Primary race references: [upstream MVC issue](https://github.com/dotnet/aspnetcore/issues/59384),
[proposed framework fix](https://github.com/dotnet/aspnetcore/pull/69888).

Primary references: [EF Design 10.0.14](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.14),
[OpenAPI model 2.12.2](https://www.nuget.org/packages/Microsoft.OpenApi/2.12.2),
[Npgsql EF 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3).

Final platform evidence: replacement [PR #124](https://github.com/silentdevcore/Canvas/pull/124)
passed CI `40562903832` and label synchronization `40562903875` at exact head
`f44074af593a0b6f78c3bec4c2a14786e1dc4e4e`. It merged normally as
`241e559e564cbc7c6dca468f9607811d8cbed823`; all six source proposals closed.
The owner's secondary-account approval was disclosed as technical review,
not independent human approval or production authorization. Snapshot was
intentionally skipped on the pull request. The isolated worktree matched the
fetched `develop` tree and the four user rename changes stayed untouched.

## Build and CI Tooling Follow-Up - 2026-10-27

Review these three proposals together without changing product versions:

| PR | Reviewed head | Scope |
| --- | --- | --- |
| #118 | `b432828505f529d47fd1d1c6a4d76c366ed8d017` | Private Git build task 10.0.403 |
| #110 | `e770439a7c1751fa607a68db1568e8a8fabc9774` | Setup Node action 7.1.0 |
| #111 | `b2c80b9f20441efb4097424ab3461896fe98e565` | Docker login action 4.7.0 |

- [x] Inspect original diffs and verify both official action tags resolve to the proposed full SHAs.
- [x] Review release notes and unchanged action inputs/runtime contracts; retain selected Node 22.
- [x] Verify official build-task metadata and retain its private build-only scope.
- [x] Update every workflow occurrence, including release/deployment workflows, without running those workflows.
- [x] Reject stale duplicate action pins rather than accepting one correct occurrence elsewhere.
- [x] Restore and build all 105 projects; pass the full NuGet vulnerability gate.
- [x] Pass 25 focused API contracts, including resolved task version, private assets and runtime exclusion.
- [x] Pass all 35 release/compliance/registry contract tests, including verified TLS, with no skips.
- [x] Add an isolated real-action TLS login CI job with synthetic credentials and no publishing.
- [ ] Pass that actual action job, invalid-password rejection, explicit logout and post-job cleanup on Linux.
- [ ] Pass every applicable exact-head CI job and review the complete diff and changed release fragment.
- [ ] Obtain a disclosed technical review, merge normally and close all three source proposals.

The SourceLink task's upstream metadata declares a development dependency and
System.IO.Hashing 10.0.14. Its resolved graph is checked rather than assuming a
patch cannot change dependencies. All 105 projects build locally with zero
errors and 65 existing warnings; no warning suppression or EULA acceptance is
added. The new task does not enter the test/API runtime manifest.

Initial loopback server unit tests were blocked by sandbox port restrictions;
the authorized retry passes. Three local Docker HTTP attempts failed because
the CLI required HTTPS; the first cleanup additionally exposed a missing-config
fixture error. These are recorded fixture failures, not successful logins.
The revised CI test uses TLS, a per-process CA and an isolated credential file,
and does not access Rancher settings or the system certificate/keychain store.
Linux action execution is still required before marking the login item done.
No production GHCR, ECR, OIDC, main release, public image or deployment is tested
or authorized by this fixture. The NPOI legal blocker remains unchanged.

Primary references: [Setup Node 7.1.0](https://github.com/actions/setup-node/releases/tag/v7.1.0),
[Docker Login 4.7.0](https://github.com/docker/login-action/releases/tag/v4.7.0),
[Git build task 10.0.403](https://www.nuget.org/packages/Microsoft.Build.Tasks.Git/10.0.403).
