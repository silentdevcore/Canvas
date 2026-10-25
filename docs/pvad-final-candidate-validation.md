# PVAD Final Candidate Validation

## Status: Local Validation, Acceptance Still Open

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
