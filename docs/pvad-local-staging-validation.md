# PVAD Local Staging Validation

## Scope And Status

The owner selected **localhost** on 2026-10-25. This is an isolated development
acceptance run, not a public staging deployment, a production installation or an
authorization to publish. It does not close the separate external-staging gate.
The rename count remains **90/98 (91.8%)**, with **8/98 (8.2%)** open.

The production Account and Designer builds are served through the repository
Nginx template on `http://localhost:16178` and `http://localhost:16176`. The API
uses `http://127.0.0.1:16096`, disposable PostgreSQL, Mailpit and isolated storage,
keys, license files, Viewer annotations and revocation cache. Only this run's
processes/containers are stopped and removed. No existing customer DB is used.

The API runs in **Development**, with synthetic bootstrap and registration policy.
HTTP ports on one hostname do not prove production HTTPS, host-only `__Host-`
cookie behavior across different subdomains, or public DNS/CDN security. Designer
uses an explicit test runtime Account URL rather than a claim about deployed
public-origin configuration. Strict CSP resource restrictions remain report-only;
the existing enforced Designer baseline is retained.

## Confirmed Findings

The old Account proxy allowlist omitted seven actual routes:

- `/registration-pending`
- `/accept-invitation`
- `/confirm-email`
- `/forgot-password`
- `/reset-password`
- `/legal-updates`
- `/legal-review`

They now serve the Account shell on direct navigation/reload. Authorization stays
in the application/API; serving a shell does not grant access to protected data.
Unknown routes still return a true HTTP 404. A TypeScript-AST contract compares
all declared public/portal/legal-review routes with the proxy allowlist. It failed
before the correction and passes after it.

The authenticated runner now serves **both** frontends through Nginx instead of
only Designer. Browser run `5e0cbbec8d54` passed 5/5: three real authenticated
API/PostgreSQL/SMTP flows and two explicitly mocked denial/layout cases. Real
flows include registration, email verification, PKCE handoff, template saving and
versions, desktop/mobile navigation, local assets/fonts, code/report migration,
preview, download and Viewer handoff. Mocked cases are not live authorization
evidence. Ten HTTP checks cover both entry pages, the seven Account routes and
an unknown Account route.

Final run `9d73db87f67c` passed the same 5/5 browser cases and the API recovery
checks. Both frontend proxies rejected anonymous session access with 401 and
did not grant credentialed access to an untrusted CORS origin. Stopping only the
test API produced JSON 503 responses with `PVADAPI018` on both proxies. After
restart, canonical and legacy version routes returned 200 through both frontends.
All **four templates and one immutable template version** retained identical
row-content fingerprints. This proves local stop/start continuity, not recovery
from a lost database/keyring or rollback to an older binary.

Evidence: `artifacts/rename/authenticated-final/9d73db87f67c/report.json` and
`browser.log`. The report records base commit `269a4078` plus uncommitted tracked
changes and SHA-256 hashes of the test runner, routes and Nginx template. An
independent container listing after cleanup found no container for this run.
An earlier recovery attempt (`6cfc7891f4c4`) failed because the test SQL used
unquoted `id` instead of the actual case-sensitive `Id` column. That failed
report is retained; the final run uses the corrected query.

## Validation And Repeat Command

The WebApi build succeeded with **0 errors and 57 warnings** after repeating an
inconclusive sandbox build with build servers disabled. Existing dependency
security advisories and the NPOI license warning remain separate release work;
no package upgrade or legal license acceptance was performed.

Account 53 tests and the selected 51 hosting/deployment/configuration contracts
pass. Both frontend production builds are built by the runner. The earlier full
Solution/Designer suite retains its original source-specific evidence; this run
does not relabel it as a new clean full-candidate validation.

```sh
PVAD_RUN_RENAME_BROWSER=1 \
PVAD_RENAME_NGINX=1 \
PVAD_RENAME_SCHEMA=artifacts/rename/committed-final/da5f3fffee91/schema.sql \
node tools/configuration/rename-authenticated-browser.mjs
```

Use only a schema generated from the current EF migration chain, not a customer
dump. The recorded schema matches the unchanged persistence/API sources of this
branch; regenerate it when those migrations change. The runner refuses occupied
ports, creates uniquely named resources and removes its own services in `finally`.
Reports, logs and browser artifacts are ignored under
`artifacts/rename/authenticated-final/<run-id>/`; do not commit runtime keys,
database files, browser sessions or entire artifact directories.

## Read-only GitHub Inspection

On 2026-10-25 a GET-only inspection confirmed:

- `pxa-staging` exists and permits protected branches; the returned rules contain
  a branch policy, not a required reviewer.
- Its environment-variable list is empty. No secret values were requested.
- A historical `v1.1.0` release, published 2026-08-03, contains seven `pxa-*`
  archives with advertised SHA-256 digests. These metadata were inspected; the
  archives were not downloaded or independently hashed by this check.
- The local deployment workflow records `validated`, not `succeeded`: no target
  adapter is configured. A protected environment and release metadata are not a
  running staging deployment.

Retain that historical release unchanged. It does not contain the current PVAD
rename, establish package-publisher ownership or prove customer distribution.
The earlier owner statement about no publication is dated scope information,
not evidence that GitHub contains zero historical releases. Reconcile actual
distribution/inventory before external cutover; do not invent customer installs.

## Remaining External Acceptance

Require confirmed HTTPS target hosts, deployed immutable artifact/image digests,
a target deployment adapter, approved publisher and infrastructure owners,
verified key/backup recovery, real OAuth/mail/webhook configuration and a tested
version rollback. Local SMTP verification is not Internet email deliverability;
API stop/start is not an old/new binary rollback or database restoration.

VERSION remains **1.1.0**. The Account route fix has a Patch fragment. No release,
tag, push, registry publication, DNS change or external deployment was performed.
See the [approval register](pvad-rename-release-approvals.md),
[rollback procedure](pvad-rename-rollback.md) and
[rename checklist](../checklists/Rename-PXA-To-PVAD.md).
