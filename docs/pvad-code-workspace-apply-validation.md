# Code Workspace Apply Validation

Date: 2026-10-25. Source base: `4f51ac1d` plus the uncommitted corrections described
here. This is scoped local evidence, not a clean release-candidate approval.

## Resolved Behavior

The earlier complete customer flow failed with `PXACODE123` after saving a native
project, reopening it and advancing its visual template revision. The workspace
still referred to its older base. The revision rejection itself was correct.

- Add a tenant-filtered, apply-permission-protected `POST /compare` operation.
  It returns both reviewed revisions, the old base, the current visual document
  and the converted code result. It changes neither drafts nor the base revision.
- Require explicit confirmation in a keyboard-accessible native dialog. Review
  element additions/removals/changes, page order/layout and document-level fields;
  expand either complete document when needed. Cancel preserves every draft.
- Send the reviewed revisions and canonical checksum with the exact reviewed
  source on confirmation. Reject newer template/workspace revisions or a changed
  execution result. Keep historical diagnostics stable.
- Roll back all Apply writes, including the success audit event, if a database
  concurrency token changes during conversion/persistence.
- Wait for visual autosave before comparison and acknowledge a successful Apply
  as already saved. Do not generate a redundant visual save immediately afterward.
- Preserve edits made during an earlier code-draft save. Generated drafts also
  use the active-language autosave without overwriting another language.
- Translate review controls and notices in all six Designer languages.

No schema migration or existing workspace rewrite is required. Existing direct
Apply clients remain revision-protected; the new checksum field is optional for
compatibility. The Designer always supplies it. This is explicit replacement of a
reviewed visual document, not an automatic three-way merge.

## Validation Results

| Check | Result |
| --- | --- |
| Complete Designer Jest suite | 752 passed in 84 suites |
| Designer typecheck and production build | Passed |
| Focused conversion/workspace/template .NET selection | 59 passed, one opt-in PostgreSQL skip; executed separately below |
| Workspace and authorization contract selection with PostgreSQL enabled | 75 passed, zero skipped |
| Real PostgreSQL concurrent template/workspace writes during Apply | Passed, with rollback of the other row and success audit |
| Documentation tests and production build | 59 passed; build passed |
| Real browser selection behind repository Nginx/CSP profile | 5/5 passed |
| Configuration/legal/deployment/versioning/shared-link contracts | 239 passed |

The .NET selections overlap; their counts are not additive. The browser selection
contains three real API/PostgreSQL/SMTP cases and two separately mocked
denial/layout cases. The complete real customer case includes registration, email
verification, PKCE handoff, visual edits/autosave, backend PDF download and print
request, native project save/open, four-language conversion, explicit stale-base
Apply, a concurrent-editor conflict, local recovery, and tenant-bound handoff.
Additional real migration checks use the maintained Syncfusion PDF, DevExpress
report and ClosedXML examples. Code/design artifact downloads are verified; PDF
previews load in the actual PDF.js Viewer, their text layer contains the expected
content, and Viewer downloads match the original preview SHA-256. Report output
also opens and autosaves in the visual Designer. Spreadsheet remains a separate
HTML grid preview containing the expected cells, with no PDF Viewer action.

The rendering fixture loads all four local PVADE Helvetica font faces, verifies
a decoded local PNG in Canvas and Preview, renders a real backend PDF, and checks
that JSON export preserves the binding path and placeholder. This proves binding
preservation, not runtime data substitution in the direct-design renderer. No
production binding or migration behavior was changed to make these tests pass.

Expanded browser run: `artifacts/rename/authenticated-final/946ac88ab10c/report.json`.
Desktop/mobile comparison screenshots and the native Chromium PDF screenshot are
under that run's `playwright/` directory. Only synthetic accounts and documents
were used. Test containers and services were removed; existing apps and stores
were not reused. Nginx baseline CSP was enforced; its stricter resource policy
remains report-only, exactly as configured in the repository.

The rerun also found and corrected missing legacy routes in the Designer Nginx
allowlist and an incorrect MIME type for PDF.js `.mjs` workers. A scoped asset
location serves module workers as JavaScript with `nosniff` and a real 404 for
missing assets, without relaxing CSP. A TypeScript-AST contract checks every registered absolute app
route while preserving real 404 responses for unknown paths. Hosting guidance
now correctly omits a trailing slash from the API upstream origin, retaining the
`/api/` request prefix.

Earlier failed attempts remain evidence: `c1a52daf4fe0` passed code Apply but
encountered an obsolete unowned-handoff test; `5d1461eba989` stopped at an
incorrect test upstream URL; `9286017463c4` exposed the missing Nginx legacy
routes. No production authorization or tenant check was relaxed to pass them.
The first scoped Nginx replay `3cb3292bb4bd` passed before migration coverage was
extended. Subsequent attempts exposed a decorative-arrow test selector, a parity-
only image selector, and then the real worker MIME defect (`1be310286c69`); the
final expanded run passed after those corrections. Test assumptions were corrected
without bypassing authentication, binding substitution or actual PDF rendering.

## Remaining Acceptance Boundaries

- Record a manual native OS print-dialog and printed-output check. An intercepted
  print request and PDF screenshot do not certify the OS dialog or printer.
- Review and rerun the remaining aggregate final-candidate checks on the final
  recorded source snapshot. The prior full Solution result is not relabelled as
  a new full Solution run for these corrections.
- Retain existing production-host, registry ownership, deployment/cutover and
  operator approvals as separate gates. Existing dependency advisories remain.
- The representative migration examples do not certify every provider input.
  Binding substitution and complete visual typography parity remain distinct
  functional scopes, not claims inferred from these rename regression checks.

To repeat the expanded local browser check after generating the current schema:

```bash
PVAD_RUN_RENAME_BROWSER=1 PVAD_RENAME_NGINX=1 \
  PVAD_RENAME_SCHEMA=/tmp/pvad-final-schema.sql \
  node tools/configuration/rename-authenticated-browser.mjs
```

These are supporting subtasks of the existing rename acceptance criterion:
**88/98 (89.8%) complete; 10/98 (10.2%) open**. The successful subset does not close
that aggregate criterion. VERSION remains 1.1.0; published release history, real
customer stores, package publishing and Git remotes remain unchanged.

See [user workflow and API](code-designer-workspace.md),
[earlier candidate audit](pvad-final-candidate-validation.md) and
[rename checklist](../checklists/Rename-PXA-To-PVAD.md).
