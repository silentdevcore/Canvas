# PVAD Account Roles And Workflow Review

Date: 2026-11-27. Base commit: `05c68813`. Review of current local source and
the selected private/company account policy. Cloud selection remains deferred.
This is a findings phase: no application behavior or account rights were changed.

**Follow-up status, 2026-11-27:** The owner subsequently authorized correction
of all three findings. The historical findings below describe the reviewed base;
see [the correction checkpoint](#correction-checkpoint-2026-11-27) for the local
implementation and regression evidence. The recorded account policy remains
unchanged, including undecided JSON rights and deferred XLS.

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

## Correction Checkpoint (2026-11-27)

Authorized after review commit `a9d1f5d6`; changes remain local and uncommitted.

- Preview menu visibility and action handlers now both check the selected
  format capability, in addition to role permissions. Printing retains its
  separate role check and PDF capability. Private JSON/image shortcuts are
  absent; explicitly permitted company JSON remains downloadable. The handler
  test also removes a JSON grant after rendering the button and confirms that
  its download is refused. No account rights were expanded to make a test pass.
- Spreadsheet import derives XLSX/CSV/TSV choices and descriptive text from
  account capabilities. Native Workbook JSON remains available for editing.
  Deferred XLS is not advertised or selected; an injected XLS file is refused
  locally before the service call. Native edit mode still accepts JSON only.
- Route and sidebar messages distinguish missing role permission, account
  restriction and separate Migration approval. PDF import uses the same
  capability gate at the route, rather than presenting a misleading inactive
  upload panel. A standalone import panel also explains its disabled state.
  Spreadsheet service failures preserve structured denial categories without
  displaying raw HTTP codes or arbitrary server detail. The upload page renders
  localized alerts. The new messages and dynamic format descriptions are present
  in English, German, French, Spanish, Italian and Arabic.

The existing browser profile now asserts corrected behavior rather than
successful defect reproduction. It covers both account kinds, explicit XLS
rejection without an upload, a server capability denial on XLSX, approval wording,
company JSON success and private JSON/image absence. Phone preview remains
behind the existing Desktop-required overlay; no mobile editor expansion is
claimed. Browser APIs are synthetic, as in the original review. Backend/native
licensing and database source were not changed, and the prior 85-test backend
profile was not rerun for this UI change.

Final validation: **147/147** component/route/renderer/permission/import/locale
tests and **20/20** Chromium desktop/phone cases pass, with no skipped or flaky
browser cases. Typecheck and production build pass (existing bundler warnings
remain). Tooling/version/release-author checks pass **38/38** and fragments are
valid. Screenshots were inspected. Browser APIs are still synthetic; no connected
browser/backend acceptance is implied. The component checks include narrow
account format lists, native JSON-only editing, structured/unknown/non-JSON
server failures and role/account/approval message selection. Unknown failures do
not falsely identify a role problem. New keys/placeholders match across all six
locale catalogs; this is not independent professional translation approval.

Logs, audited browser results, screenshots and source hashes are recorded under
`artifacts/account-policy-fixes/20261127/`.
No deployment, production key, live migration, paid-phase policy change or
commit is part of this correction. The release fragment declares **Patch / fixed**
for Designer and documentation; primary release acceptance remains 70/76.

## Connected local browser and backend — 2026-11-27

**Passed: both account types, 22 completed workflow checkpoints.** Chromium drove
the real Account and Designer sites through a real loopback Kestrel API and a
fresh PostgreSQL database. No API responses were intercepted or substituted.
The test host and executable live only in test tooling; application source and
the selected launch policy were not changed.

| Scope | Verified result |
| --- | --- |
| Registration and identity | Private and company registration, actual rendered verification message from the development mail outbox, email verification, login and Designer handoff succeed. |
| Documents and templates | Rename and add a text element; read back the saved draft, create a version and publish. Two persisted templates, two versions and two published records. |
| Code | Convert to C# PDF, type a comment into the visible Monaco editor, read it back from the API, and execute through the real development CodeWorker. Both executions succeed and return PDF bytes. Two persisted workspaces contain the authored edit. |
| Format processing | Markdown, CSV and TSV imports; PDF, Word/DOCX, ODT and XLSX exports. Browser PDF downloads have a PDF signature; XLSX outputs have a ZIP signature. These checks do not establish document fidelity for every supported format. |
| Restrictions | PDF import and PNG export return the account-capability denial for private accounts and succeed for company accounts. Deferred XLS import and unapproved Migration return account-capability denials for both. Private quick JSON/image exports are absent; PDF restriction and Migration approval screens are shown. |
| Native ownership | Personal licenses have a user owner and no organization owner; company licenses have an organization owner and no personal owner. |
| Native lifecycle | Browser downloads of the initial and renewed files have valid real signatures and the expected account scope. All four files allow offline PDF processing before expiry and deny it exactly at expiry, with zero grace and three calendar months of validity. |
| Renewal | Disabled before expiry, enabled at expiry, then produces a new signed file. Repeating the original renewal returns the same replacement; original download bytes remain unchanged. Two renewal audit events and two completed registration issuance jobs. |

Evidence is in `artifacts/connected-account-browser/1795816254945-cb419e45/`:
`report.json`, `cleanup.json`, build logs and four inspected screenshots.
The database audit confirms **2 customers, 2 templates, 2 versions, 2 published
templates, 2 authored code workspaces, 4 native licenses, 2 renewal events and
2 completed automatic-registration jobs**. All four native license IDs are
distinct. The two UI response counters are 75 and 70; they are response counts,
not test counts. The isolated build passes with 0 errors and 86 existing warnings.
The accompanying tooling/version/release checks pass **31/31**, with no skips.

Six initial development runs failed and are retained as failures. Harness fixes
wait for persisted autosave, the final legal-policy form and client readiness;
edit the visible editor instead of an inactive Monaco model or a version-specific
textarea; and use the platform's actual end-of-file shortcut. These runs are not
added to the passing count.

**Observed UI issue at this checkpoint (corrected below on 2026-11-28):** success toasts can cover the Preview export
button. Hovering that area pauses the toast timer, so a pointer left there can
keep the button covered. The failed company screenshot and diagnostics in
`artifacts/connected-account-browser/1795815834952-8d5f51be/` record this. The
passing scenario moves the pointer away and waits for normal dismissal; it does
not force a click, hide the toast or change product CSS. A later UI correction
should prevent success notifications from covering primary controls.

Only the issuer clock is advanced; authentication, the cloud trial and machine
time remain real. Mail and issuance processors execute their real implementations
on explicit fixture commands. SMTP delivery, autonomous background scheduling,
production CodeWorker hardening, cloud key storage, paid-phase sharing detection,
phone editing, printing, API/service-account management and full-format/visual
fidelity are outside this run. Monaco uses its configured CDN. Synthetic legal
documents are fixture data, not publication or legal approval.

Cleanup confirms temporary keys, isolated assemblies and owned containers were
removed; source trust and regular licensing outputs are unchanged. No production
key, deployment or commit was made. The existing owner changes to `PVAD.sln` and
`.vscode/` were left alone. Primary release acceptance stays **70/76**. The new
release fragment is **None / improved** for internal test tooling and evidence.

## Nonblocking notification correction — 2026-11-28

The success-toast overlap finding is corrected. Designer notifications now appear
at the bottom right with the device's bottom safe-area inset. Passive notices
let pointer events reach underlying controls, so moving over them does not pause
their expiry timer. Explicit notification action buttons retain pointer events,
keyboard activation and the existing dismissal behavior. Status/alert live
regions and notification durations are preserved.

The browser regression keeps a publication notice visible while opening the
actual Preview export menu, then clicks a control positioned underneath the
notice. It also checks automatic dismissal with the pointer over a passive notice
and exercises an action button by click and Enter. The generic notification
checks run on desktop and mobile; the actual visual-editor export check runs on
desktop because mobile retains its existing visual-editing gate. These tests use
synthetic account API responses and the real notification component.

Validation: **22/22 Chromium browser cases**, no skips or flaky outcomes;
the two notification cases also pass a targeted repeat after tightening the
visible-hover probe.
Designer typecheck and production build pass, with existing bundler warnings.
Version/release-author tests pass **29/29** and the release fragment validates.
The first expanded run had two failures because the test positioned its probe
while the notice was still animating into view. Its evidence is retained; the
final test waits for the notice to be inside the viewport and uses ordinary
clicks, without forced interaction or notification suppression. Screenshots were
inspected. Evidence: `artifacts/toast-controls/20261128/`.

The connected PostgreSQL/native-signing workflow was not repeated for this
presentation-only correction; the preceding connected result remains its
separate evidence. Release impact is **Patch / fixed** for Designer and
documentation. No commit, deployment or production-key change is included.
Primary licensing release acceptance remains **70/76**.
