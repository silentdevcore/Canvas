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
| #99 | download-artifact 8.0.2 | Official pin verified; real CI artifact handoff added with nested paths, spaces, empty files and binary hashes; 11 local contract tests passed; final CI/review/merge pending. |
| #91 | build-push-action 7.4.0 | Official pin verified; fragment and compliance corrected; final base integration and actual action runtime test pending. |
| #90 | setup-buildx-action 4.4.1 | Official pin verified; fragment/compliance corrected; non-publishing builder bootstrap smoke added; final CI/review/merge pending. |
| #100 | Admin Vite 8.3.4 | Fragment added; 26 local Admin tests, production build and full npm audit passed; final base integration/review/merge pending. |
| #97 | MCP Node types 26.6.4 | Test MCP types and stdio smoke. |
| #95 | MCP SDK 1.32.0 | Closed as superseded by merged baseline PR #102 using 1.32.2; do not downgrade. |
| #94 | MCP tsx 4.23.15 | Test types and stdio smoke. |
| #89 | Zod 4.6.5 | Closed with replacement explanation: this target remains affected by GHSA-mrhx-c7xc-m3w5; merged baseline PR #102 uses 4.6.7. |
| #78 | EF Core Design 10.0.11 | Check runtime/design patch alignment and database tests. |
| #75 | Roslyn Scripting 5.9.0 | Check the full Roslyn package graph and code execution tests. |
| #74 | Roslyn CSharp 5.9.0 | Coordinate with scripting/workspace dependencies; test migrations. |
| #73 | ASP.NET OpenAPI 10.0.11 | Check API schema compatibility and API tests. |
| #72 | ASP.NET MVC Testing 10.0.11 | Run API and relay test suites. |
| #71 | Identity EF 10.0.11 | Check EF package graph, identity and PostgreSQL tests. |
| #70 | dotnet-ef 10.0.11 | Check tool restore and migration compatibility. |
| #45 | Markdig 1.3.2 | Major dependency update: run Markdown importer regressions. |
| #43 | coverlet 10.0.1 | Major dependency update across test projects: verify test SDK/collector compatibility. |
| #41 | ClosedXML 0.105.1 | Run Spreadsheet import/export regressions. |
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
