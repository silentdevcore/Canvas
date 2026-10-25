# PVAD Final Candidate Validation

## Committed Candidate: Local Acceptance Passed

Date: 2026-10-25. All automated checks below ran against the same commit,
`16c4264eb192e2cebbe3d03c9aafcc0803ad36ec`, with no tracked source changes before
or after the run. Subsequent evidence-document edits are not application changes
and were not present during this run. Unrelated untracked editor settings remain
outside the reviewed change.

| Area | Current committed-candidate result |
| --- | --- |
| Complete Solution build | Passed, zero errors and 196 compiler/dependency/license warnings |
| Complete unfiltered Solution tests | 53 TRX files; 3,513 passed, zero failed; one explicit binary case skipped |
| Separate paired-binary execution | That same skipped case passed 1/1 using the immutable old source and current worker; not additional unique coverage |
| API within the full Solution run | 1,247 passed, zero failed, one binary skip as above; disposable migrated PostgreSQL with PostgreSQL opt-ins enabled |
| Designer | Typecheck, 752 tests in 84 suites, production build passed |
| Websites | Company 58, Documentation 59, Demo 4, Admin 39, Account 53 tests passed; all five production builds and Account typecheck passed |
| MCP | Typecheck, 27 tests, build and packaged-distribution smoke passed |
| Combined Node contracts | 239 passed, zero failed or skipped; selections overlap website coverage |
| Frontend host matrix | 7/7 passed using synthetic transport responses; not real authentication evidence |
| Authenticated Nginx/CSP browser selection | 5/5 passed: three real API/PostgreSQL/SMTP cases and two separately mocked denial/layout cases |
| DocFX metadata/site generation | Passed with 21 known dependency/MSBuild warnings and zero errors |
| Generated DocFX links | 582 HTML files, 8,046 checked references, zero failures |
| API and separately published OCR/Code workers | All three dependency identities are version 1.1.0; runtime configuration files and four bundled Liberation Sans faces exist; no App_Data file is packaged |
| Seven API/frontend build-info records | All identify version 1.1.0 and the exact source commit above |
| Release preparation dry run | 128 fragments, Major impact, proposed 2.0.0; VERSION and the published manifest are unchanged |
| Residual-name ledger and whitespace | Passed before evidence edits; evidence hashes and whitespace are reviewed again afterward |
| Native Chrome print workflow | User reported the requested test completed and working on 2026-10-25; separate manual acceptance, not an automated result |

TRX skipped outcomes are counted from individual `UnitTestResult` entries. The
current test runner reports zero in its `notExecuted` summary attribute despite
the explicit skipped binary result. Do not report that as a skip-free full run.

Local evidence directory: `artifacts/rename/committed-final/da5f3fffee91/`.
It contains per-step logs, the generated schema, 53 TRX files, `report.json`,
`summary.json`, the isolated Release package and `release-dry-run.json`.
Browser evidence is recorded separately at
`artifacts/rename/authenticated-final/2a19b45622a3/report.json`; it records
`uncommittedTrackedChanges: false` and the repository Nginx image identity.
These local artifacts are not published or immutable release evidence.
Representative native-PDF, mobile comparison and report-to-Viewer screenshots
from that run were visually inspected; this is not approval of all visual baselines.

The real customer case covers reviewed stale-base Apply, concurrent-editor
conflicts, native project compatibility, local fonts/images, binding preservation,
representative PDF/Report/Spreadsheet migrations, Viewer handoff and artifact
downloads. It does not certify runtime binding substitution or every provider.
The stricter resource CSP remains report-only as in the repository; the baseline
CSP is enforced. The automated print check intercepts the print request and
therefore cannot approve the native dialog or saved/printed output.

### Manual Chrome Acceptance

The user requested a separate disposable test environment with a synthetic,
email-verified account and a two-page sample. This is temporary local tooling,
not an application or authentication-policy change. Use a Chrome Incognito
window so cookies for other localhost applications are not replaced.

Manual setup run `02ad7f318b3e` was prepared with successful registration, email
verification, normal login/handoff and a saved two-page sample: text on the first
page and a QR code on the second. Its ignored `ready.json` records the local
addresses, source commit and two-hour expiry; credentials are not stored in this
document. The API, PostgreSQL, mail catcher, keys and document storage are separate
test resources. The free default local Account port is used because the Designer
expects it without a custom runtime bootstrap. Existing applications were not
replaced. The environment removes its own services at expiry or controlled stop.

Earlier temporary-helper attempts corrected an Account-port mismatch, an
uncontrolled browser locale, registration before legal-policy readiness and an
ambiguous gallery selector. Their services were removed. None is native-print
acceptance evidence, and no application/authentication policy was changed to
make the setup pass. The following procedure was supplied for the user's
separate Chrome check.

1. Sign in and open the prepared two-page sample in the visual Designer.
2. Open Preview, then Export and Print. Allow the dedicated PDF popup.
3. Confirm the print target contains the PDF, not the editor, navigation or panels.
4. Inspect both pages in Chrome's print preview and open the system print dialog.
5. Save a PDF or print, reopen/inspect the output, and verify both pages and their
   text/QR content without missing content or unexpected clipping.
6. Record the Chrome and OS versions, dialog result, output method, page count
   and any observed defect. Readiness to test is not a passing result.

On 2026-10-25 the user confirmed that the test was completed and working.
Record this as user-reported acceptance of the requested Chrome print workflow,
not agent-observed OS automation. Exact Chrome/OS versions, output method and
printer hardware were not provided; no hardware certification is inferred.
Separate evidence: `artifacts/rename/manual-print/02ad7f318b3e/user-acceptance.json`.
The earlier automated report retains its original pending-manual status; this
later confirmation does not retroactively change its execution evidence.

The combined final-candidate gate is now checked: **89/98 (90.8%) complete,
9/98 (9.2%) open**. This closes one existing local acceptance gate, not production approval.
Public hosts, publishers, real-store recovery, installed OS associations and
release authorization retain their separate gates. Dependency advisories and
NPOI license decisions remain separate unresolved release/security work; no
upgrade or license acceptance was performed. No tag, push or deployment occurred.

Later scoped follow-up (2026-10-25): real macOS/Chrome installation, default file
dispatch, signed-in/out imports, edited-window protection, Chrome revocation,
manual fallback and uninstall now pass. The owner selected that platform and
approved generic icons for the test. This closes the separate local association
gate for that scope and advances the tracker to **90/98 (91.8%)**. See
[the platform validation](pvad-native-project-platform-validation.md) for the
dirty-source hashes and untested manual/other-platform boundaries. It does not
change the source or results of the committed-candidate run above.

## Earlier Uncommitted Candidate (Historical)

Follow-up: the code-workspace blocker below is resolved by explicit reviewed
Apply, and the complete browser selection now passes 5/5 behind repository
Nginx/CSP. See [current scoped results and remaining boundaries](pvad-code-workspace-apply-validation.md).
The results and failed attempts below retain their original source scope.

Review date: 2026-10-25. Base source commit: `f49df8c24fad4be41e6033fc3e3ab1c61775c804`,
plus the reviewed uncommitted validation, documentation and packaging corrections.
This is not a clean committed release, deployment approval or a security certification.
The [rename tracker](../checklists/Rename-PXA-To-PVAD.md) remains at **88/98 (89.8%)**.
Its combined final-candidate gate is still open; partial results are not counted twice.

## Results

| Area | Executed evidence | Result |
| --- | --- | --- |
| Complete Solution build | `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1` | Passed; compiler, dependency and license warnings remain |
| Complete unfiltered Solution tests | Disposable PostgreSQL base, opt-in PostgreSQL fixtures, Development environment, 53 TRX files | 3,506 passed, zero failed, one opt-in binary case skipped |
| Paired old/current worker test | `node tools/job-cutover/run.mjs`, immutable original sources and a separate disposable database | Skipped case executed separately: 1/1 passed |
| Designer | Typecheck, Jest, production build | 745 tests in 84 suites passed |
| Company | Tests and production build | 58 tests passed |
| Documentation website | Tests and production build | 59 tests passed |
| Demo | Tests and production build | 4 tests passed |
| Account | Typecheck, tests and production build | 53 tests passed |
| Admin | Tests and production build | 39 tests passed |
| MCP | Typecheck, 27 tests, build and refreshed distribution smoke | Passed |
| Configuration, legal, deployment, release and hosting contracts | Final combined Node selection | 236/236 passed |
| Local frontend host matrix | All six built frontends, proxy contracts, desktop/mobile, storage acknowledgement and safe handoff URL | 7/7 passed; transport responses in this matrix are synthetic |
| Real Account/Designer browser selection | Built frontends, actual API, separate PostgreSQL and SMTP | 4/5 passed; the complete customer workflow fails at stale code-workspace Apply |
| Local Designer fonts | Four bundled faces at desktop/mobile sizes | Passed; isolated font evidence, not complete typography parity |
| Built Designer with Nginx | Baseline CSP, report-only resource policy, native intake, blob worker and PDF download signature | Passed using synthetic intake responses, not authenticated CSP acceptance |
| Native PDF display | Full Chromium, actual backend PDF, blob signature and screenshot | Visible text, QR code, button and two-page native PDF viewer verified |
| API Release package | API and both separately published workers; dependency/runtime manifests | All three identify as 1.1.0; seven API/frontend build-info records agree |
| DocFX | 2.81.0, full metadata and HTML generation | Passed; 21 dependency/MSBuild warnings remain, no unresolved file-link or XML-cref warnings |
| Generated DocFX links | HTML parsing, local targets and anchors | 582 HTML files, 8,046 checked references, zero failures |

Website, shared Node and integration selections overlap. Do not add their counts
as unique coverage. The separately executed binary case is the same case skipped
in the general Solution run, not an additional new test.

The initial full run had eight API failures and one image-analysis timing failure.
Corrected stale mail/domain expectations and the migration catalog comparison;
the latter now permits only the explicitly reviewed legacy artifact tuple while
comparing every other field. OpenAPI is intentionally Development-only. The first
runner used Testing, which incorrectly made its default-factory OpenAPI requests
return 404. The corrected run uses a migrated disposable base database and the
intended environment. No authorization, alias policy or production API was relaxed.
The image-analysis case passed on rerun without raising its five-second limit.

## Corrections

- Publish schemas and the existing public synthetic PDF corpus as DocFX resources.
  Correct namespace links, canonical workbook schema links and API XML references.
  A new output checker validates the generated site rather than source paths alone.
- Exclude `App_Data/**/*` from WebApi default project items. A real local publish
  had copied a runtime annotation JSON file. The corrected package was generated
  in a new directory and checked for absence of runtime storage. The rejected
  package's generated data copy was removed; its contents were not inspected or
  printed, and the original runtime file was not modified or deleted. Docker's
  existing context exclusion is unchanged.
- Update browser selectors, project labels and extensions to the current UI.
  Preserve a download's real filename when uploading it: Playwright's temporary
  download path has no extension and is correctly rejected by the backend.
- Wait for the actual project response before accepting its compatibility review.
  Wait for product bootstrap before closing a new-release panel.
- Prefer canonical smoke-test environment keys; retain conflict-checked legacy
  credential-key aliases. No credentials are committed.
- Add a guarded isolated browser runner. It refuses occupied IPv4/IPv6 ports,
  uses separate SQL and mail containers and removes only its own services.

## Blocking Follow-up

### Code Workspace Base Revision

Reproduced with synthetic data:

1. Create and autosave a document.
2. Save its native project; the save path reads/initializes its code workspace.
3. Reopen that project and save or publish the changed template.
4. Open Code mode, convert through the four representations and run JSON.
5. Apply to Designer returns `PXACODE123` and the JSON tab becomes Conflict.

`LiveCodeEditor` restores `activeTemplateRevision` from the workspace's earlier
`baseTemplateRevision`. Project import/autosave has advanced the actual template.
The server's revision check correctly prevents overwriting newer visual content.
Do not fix this by removing the check, changing the test to expect success without
comparison, or silently replacing the remembered base with the latest revision.

Required next work: expose stale-base state and an explicit compare/rebase or
refresh action, preserve all source drafts, obtain both current revisions and
confirm a structural diff before Apply. Cover a genuine concurrent template edit
and workspace edit with regression tests; then rerun the complete customer flow.
Keep this within the existing [Code Designer roadmap](../checklists/PVAD.Code-Designer-Roundtrip.md).
Actions after this failed Apply, including the later legacy workbook recovery and
navigation assertions, were not reached and are not validated by this run.

### Other Acceptance Still Required

- Run real authenticated code-editor, font/image/binding, preview and export flows
  behind the exact Designer Nginx/CSP configuration, including migration conversion,
  migration preview/Viewer handoff and artifact downloads. The synthetic Nginx test and
  real Vite-preview authentication run are separate and cannot substitute for it.
- Complete and record the manual native print-dialog/output check. The automated
  test deliberately intercepts the popup's print call to avoid operating a real
  printer. It validates the actual PDF target, signature, display and one print
  request, not OS dialog completion or printed output.
- Repeat the full affected checks after the stale-workspace correction on one
  recorded source snapshot and review the final diff before closing the gate.
- Resolve existing dependency advisories and licensing decisions through their
  separately reviewed work. No dependency upgrade or license acceptance occurred.

## Repeatable Commands

The complete API suite must use a deliberately provisioned, migrated disposable
PostgreSQL base, never the installed development database. The default factories
otherwise use normal application configuration. Enable `PVAD_RUN_POSTGRES_TESTS=1`
for the additional Testcontainers cases. Do not disable OAuth or authentication
merely to make startup pass. Keep the binary test's explicit opt-in separate.

Generate SQL without applying it to any database:

```bash
dotnet ef migrations script --no-build \
  --project src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj \
  --startup-project PVAD.WebApi/PVAD.WebApi.csproj \
  --output /tmp/pvad-final-schema.sql
```

Run the isolated real-browser selection (Docker and installed Playwright Chromium
required; no existing application is reused):

```bash
PVAD_RUN_RENAME_BROWSER=1 PVAD_RENAME_SCHEMA=/tmp/pvad-final-schema.sql \
  node tools/configuration/rename-authenticated-browser.mjs
```

DocFX is installed separately, outside the repository. On a machine with mixed
ARM/x64 .NET installations, point `DOTNET_ROOT` at the installation matching the
tool host. Do not change the project's target framework to work around the host.

```bash
docfx docs/docfx.json
node tools/configuration/verify-docfx-output.mjs
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
node tools/configuration/rename-residual-review.mjs
git diff --check
```

## Evidence And Boundaries

Local diagnostics: `/tmp/pvad-final-corrected-build.log`,
`/tmp/pvad-final-corrected-tests.log`, `/tmp/pvad-final-corrected-results/`,
`/tmp/pvad-final-binary-cutover.log`, `/tmp/pvad-final-docfx-verified.log` and
`/tmp/pvad-final-dotnet-summary.json`. These are temporary logs, not immutable
published evidence. Browser runs retain separate artifact directories;
`artifacts/rename/authenticated-final/latest.json` identifies the latest one.
Run `fbacf6c349c3` reproduces the code-workspace conflict. Native PDF screenshot
evidence is also present in runs `e155e39d7a4a` and `fbacf6c349c3`.

Earlier failed browser attempts exposed stale selectors, an existing localhost
port, a PostgreSQL initialization-readiness race and missing runtime Account
configuration. They are not counted as successful authentication evidence.
Headless-shell PDF behavior is not native PDF display evidence; full Chromium
was used for the visible PDF inspection. No application responses were mocked
in the three real customer/authentication cases; two separate denial/layout
cases intentionally use mocked authorization data.

The API package is explicitly labelled with the base commit plus `-dirty`.
`VERSION` remains 1.1.0, and published release history is unchanged. Two Patch
fragments describe the documentation and packaging fixes; the combined rename
still requires its existing Major release preparation. No tag, push, deployment,
publisher registration, real-store cutover or license acceptance is authorized.
Existing `.vscode` files and installed application databases remain untouched.
The custom disposable database/mail containers and all application processes
started by the isolated browser runner were removed after the checks.
