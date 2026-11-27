# PVAD Account Roles And Workflow Review

Date: 2026-11-27. Base commit: `05c68813`. Review of current local source and
the selected private/company account policy. Cloud selection remains deferred.
This is a findings phase: no application behavior or account rights were changed.

## Findings

### P1 — Preview quick export bypasses the private JSON capability restriction

`pvad-designer/src/components/Preview/LivePreview.tsx:39` derives `canExport`
only from the broad Designer PDF-export role permission. Its menu at line 355
uses that one flag for PDF, image and JSON, and its handler at line 115 repeats
only that check. The JSON branch at line 142 calls `exportToJSON` directly.

A private fixture has the actual policy-derived allowed-capability list, without
`export.json`, and broad Designer role permissions. On desktop, open
`/pdf/create`, Preview, Export, Export JSON: Chromium downloads the JSON file.
This is a real browser-created download, not a mocked JSON-export API response.
The larger `ExportModal` correctly hides JSON for the same private capabilities;
the alternate menu bypasses that UI restriction. JSON remains undecided in the
recorded policy and therefore has no positive grant. This finding does not
change that decision or imply access to another customer's document.

The image entry is also offered to private users. Actual HTTP PNG export is
denied with 403, so this review established a misleading action, not an image
export enforcement bypass. Proposed correction: intersect both menu visibility
and handler admission with each format's account capability, retaining separate
print permission. Replace the characterization probe with denial regression
assertions when implementing the correction. Keep JSON's policy undecided.

Evidence: the desktop `review evidence: private quick JSON export...` case,
`private-preview-quick-exports.png`, and the attached downloaded filename in
`artifacts/account-policy-review/browser-results/` and `browser-results.json`.
Phone-width visual editing is intentionally blocked by **Desktop required**;
the final phone case verifies that gate and does not claim a mobile JSON download.

### P2 — Spreadsheet import advertises deferred XLS for both account kinds

`pvad-designer/src/pages/SpreadsheetImportPage.tsx:80` includes `.xls` in the
file chooser, and the surrounding text advertises XLS support. The account
policy defers `import.xls` for both company and private accounts. Actual HTTP
tests confirm both are denied before the XLS parser runs. The browser probe
confirms the chooser and upload path still accept a synthetic `.xls` selection.

Proposed correction: filter advertised/selectable formats using the current
account capabilities, and reject a deliberately selected deferred file locally
with a clear explanation. This must not decide or implement XLS support or accept
NPOI terms. Keep the backend denial as the enforcement boundary.

### P2 — Account restrictions are presented as missing roles or generic HTTP errors

`pvad-designer/src/services/SpreadsheetService.ts:107` discards the returned
Problem Details code/message and produces **Import failed (403)**. The real
browser renders that text for the XLS fixture despite a supplied
`PVAD_ACCOUNT_CAPABILITY_DENIED` response. The failed action does not explain
which account/format restriction applies.

`ImporterPage.tsx:52` disables PDF import for private users, but its main panel
still invites them to choose a PDF. The sidebar labels it **Permission required**.
`RequireDesignerPermission.tsx:22` uses the generic organization-role explanation
for a missing Migration account approval, even when the fixture carries the
relevant broad role permissions. This can incorrectly send users to change roles
instead of identifying account restrictions or separate Migration approval.

Proposed correction: distinguish missing role permission, account capability,
deferred format and required Migration approval in user-facing messages. Do not
change the enforcement decision to make an action appear usable.

## Verified Scope

| Area | Evidence and result |
| --- | --- |
| Markdown → PDF/DOCX/ODT | Real HTTP processing succeeds for private and company fixtures (`word` is the actual DOCX export route key) |
| CSV/TSV → XLSX | Real HTTP import and nonempty XLSX ZIP output succeed for both account kinds |
| PNG/SVG/CSV/Markdown/HTML/XML export | Private HTTP requests are denied with the account-capability diagnostic; company requests produce nonempty output |
| Deferred XLS | Actual private/company HTTP upload is denied; browser still offers it, as noted above |
| PDF/image/SVG import and advanced utilities | Existing direct-admission tests reject private operations before reading inputs or validating protected utility inputs |
| Migration | Company/private HTTP requests without separate approval fail; direct tests require a non-default, unexpired Admin entitlement; queued work rechecks revoked approval |
| Account changes during queued work | Real PostgreSQL tests reject downgraded accounts, revoked Migration approval and cancelled subscriptions before processing |
| Native issuance/renewal | Real temporary manufacturer signing and embedded verification, both account owners, download, exact expiry and repeated/concurrent renewal pass |
| Personal ownership | Owner-only discovery/download and organization-independent personal ownership tests pass |
| Rendered export modal | Private choices retain PDF/DOCX/XLSX/ODT and hide the disallowed/ungranted groups, even with broad discovery responses; company choices are exposed |
| Designer entry points | Browser fixtures open the document editor on desktop and own-code workspace for both account kinds; phone visual editor retains its intended gate |
| Account portal | Desktop and phone browser fixtures perform the renewal request with CSRF, expose the successor download and prevent immediately renewing the new term |

Browser routes use synthetic API responses; these checks run real Chromium and
the real application UI, but are not a browser connected to the PostgreSQL-backed
server. The separate .NET HTTP and signing tests supply backend enforcement
evidence. Browser license-download bodies are explicitly synthetic; cryptographic
validity is established in the backend profile, not by those fixture bytes.

The code workspace/template-override component tests run editing/conversion/
application state logic with service fixtures. This round does not demonstrate
a complete live signup → template publish/version → code execution → browser
download stack, all format implementations, every role assignment or a complete
mobile editor. Printing is covered by current policy/permission review, not a
physical printer or print-dialog acceptance run. HTML/XML import implementation,
XLS, production keys, Cloud and broad release/platform gates remain outside this
review. No global end-to-end or release acceptance is claimed.

## Execution And Changes

- Backend profile: **85/85**, 0 skipped; isolated build 0 errors, 86 warnings.
  `artifacts/native-engine-tests/1795724523603-a95848a7/` retains TRX and cleanup.
  TRX selection/counters were independently audited, including both new positive
  HTTP format cases and the real embedded-trust renewal marker.
- Designer tests: **33/33** across account policy, rendered export choices,
  export reliability, code workspace and template overrides. Typecheck passes.
- Account tests: **63/63**, 0 skipped.
- Node tooling/version/release-author tests: **38/38**, 0 skipped.
- Browser: **16/16**, 0 skipped/flaky/unexpected, Chromium desktop/phone. The
  JSON characterization succeeds on desktop; phone tests check the existing
  visual-editor gate instead. The first enlarged run reproduced the desktop JSON issue but
  timed out on the intentional phone editor overlay; its result/trace are
  retained under `20261127/initial-mobile-gate/`. The final probe explicitly
  checks the phone gate instead of bypassing it. A second locator correction is
  retained under `20261127/mobile-locator-correction/`: the existing overlay is
  `aria-hidden`, so a visible-heading accessibility locator does not identify it.
  The final test checks the actual overlay DOM; no accessibility correction or
  approval of that overlay was made. Final JSON counters and all result rows
  were audited independently, and screenshots were inspected.

New changes are the review report, test-only HTTP roundtrips, rendered UI tests,
isolated Playwright review profile and `account-flows` native runner profile.
The `review evidence` probes intentionally characterize defects: passing means
reproduction succeeded, not that those behaviors are approved. Product source,
rights and the previously prepared manufacturer files are unchanged. Review
changes are uncommitted; owner-owned `PVAD.sln` and `.vscode/` stay untouched.

Recommended implementation order: correct preview format admission first,
then remove deferred XLS promises, then make account/approval messages specific.
Production readiness and the original 70/76 checklist status are unchanged.
