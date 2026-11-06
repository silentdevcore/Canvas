# PVAD Visible Product Identity

## Scope

Current first-party application copy uses PVADE for the product and PVAD for
technical display names. The single general naming-history notice is at Company
`/about.html#rename-history`, linked from the Designer user menu.

This is presentation cleanup, not removal of legacy readers. User document text,
organization names, signed licenses, published Legal versions, immutable fixtures,
legacy schema downloads and raw API/audit records must not be rewritten.
Developer tools and explicitly opened original evidence may still show historical
identifiers; this checklist does not promise their deletion or redaction.

## Implementation

- [x] Add one shared diagnostic-code presentation adapter without changing raw API values or authorization comparisons.
- [x] Present canonical codes in Designer status, code, project import, accessibility, Markdown, migration, PDF Trust and invoice diagnostics, including tooltips and status-copy actions.
- [x] Present Account/Admin validation and access-denied codes using the same adapter.
- [x] Project historical release titles, summaries and changes in Designer, Admin and Documentation without editing the published manifest.
- [x] Keep one About naming-history notice; remove repeated general naming explanations from current Company copy.
- [x] Add the About action in all six Designer languages with a validated, configurable Company destination.
- [x] Clean current public guide prose and examples; label diagnostic aliases as display values, not changed machine contracts.
- [x] Keep ordinary customer strings outside broad brand replacement.
- [x] Resize the status-page wordmark so PVADE fits on mobile.
- [x] Add a Patch release fragment without changing VERSION or published release history.

## Validation

- [x] Pass all 809 Designer tests, including six-language rendered diagnostic checks and manifest immutability assertions.
- [x] Pass Designer and Account TypeScript checks.
- [x] Pass Company, Account, Admin, Documentation and Demo test suites.
- [x] Pass all six application production builds.
- [x] Verify production bundles across 12 desktop/mobile application cases and 12 localized Designer denial cases with synthetic API responses.
- [x] Check the real Designer user menu, historical-release drawer and unclipped wordmark in browser coverage.
- [x] Add static copy/download checks and wire the browser regression into the existing frontend CI job with synthetic screenshot artifacts.
- [x] Verify the reviewed residual-name ledger in an isolated worktree and run git diff --check.

Browser evidence is under `artifacts/rename/visible-branding/`. These are frontend
bundle tests, not real login, deployed multi-host or backend acceptance tests.
The initial browser attempt was blocked by the local Chromium sandbox; the
authorized run completed outside that sandbox. A fixture initially used an
incorrect administrator role spelling; it was corrected without changing access
checks. Existing Vite configuration/chunk warnings remain visible.

No production deployment, publication, database migration, commit or push is
included. Earlier uncommitted release-rehearsal changes are retained. The broader
[rename checklist](PVAD.Rename-Plan.md) remains 90/98 complete; this supporting
UI acceptance does not close the eight release/operational gates.
