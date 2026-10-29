# PVADE Native Project Platform Validation

## Accepted Scope

On 2026-10-25 the product owner selected **macOS with Google Chrome** as the first
installation-test scope and approved the existing generic project icons for that
test. This is not approval of final corporate logo artwork, Windows, Linux, other
browsers, a public download, or a production deployment.

The real installed-app run passed on macOS 26.6.2 with Chrome 155.0.8059.85.
Installation used Chrome's DevTools PWA domain in a separate regular profile,
not the personal Chrome profile, an incognito context or a mocked launch queue.
The local production Designer was served through repository Nginx/CSP with a
live API, isolated PostgreSQL and a development mail catcher.

## Observed Results

| Check | Result |
| --- | --- |
| Real PWA installation and macOS document-type registration | Passed for both canonical extensions and media types |
| Standalone launch without a file | Passed; no false multiple-file error |
| `.pvadp`, signed out | Passed; no upload before sign-in and explicit Open |
| `.pvadp`, signed in | Passed; actual archive imported through the authorized API |
| `.pvads`, signed out | Passed; pending selection survived sign-in in another tab |
| `.pvads`, signed in | Passed; actual archive imported through the authorized API |
| Existing edited PDF window while opening a Spreadsheet | Passed; URL and edited document name remained unchanged |
| macOS default association dispatch | Passed using `/usr/bin/open <file>`, with the test app verified as the default first |
| Chrome file-handling revocation | Passed through the real app-settings toggle; registered file handlers became empty |
| Manual project chooser after revocation | Passed for both formats through the real import API |
| Uploaded files | Six successful imports; one per explicit Open, each with Designer and CSRF headers |
| Original project archives | SHA-256 unchanged after opening |
| Uninstall | App bundle removed; both prior default-handler results restored |

The original default-handler results were empty. Chrome registered the test app
while installed and both results returned to empty after removal. The runner
refuses a pre-existing PVADE app or an existing project-type default handler; it
does not replace another installed application or call a set-default API.

The first local probes exposed two actual defects: ordinary fileless launches
were incorrectly treated as multiple-file requests, and a valid PDF project with
`pageSettings: null` failed during import. Both were corrected with regression
tests. A later probe exposed a response-observer timing issue in the test runner;
the final runner waits for the actual import response before asserting counts.
An additional registration attempt reached the normal hourly rate limit. That
limit was not weakened; the final run reused the existing synthetic test account.

## Evidence And Source

Final run: `artifacts/rename/native-os-open/865a2d9a7e7a/report.json`, with the
signed-out intake, imported PDF/Spreadsheet and revoked-permission screenshots
beside it. These ignored artifacts contain a disposable Chrome profile and
synthetic data and must not be published as a package or committed wholesale.

The report records base commit `d4f9767fbc9254815dd11ffdfced55bd334c4b76`,
`trackedSourceDirty: true` and SHA-256 hashes for the tested launcher, page-settings
normalizer and installed test runner. This is follow-up validation of those
changes, not a new claim that the entire Solution was tested on a clean commit.
The earlier full-candidate results remain point-in-time evidence for `16c4264e`.

Current focused validation: Designer **759 tests in 85 suites**, typecheck and
production build passed; the native manifest contracts and release-tool tests
also passed. VERSION stays **1.1.0**. Both behavior corrections have separate
Patch fragments; no tag, release, push or deployment was performed.

## Repeat The Test

Build the Designer against disposable, same-origin API and Account services.
Use isolated storage, PostgreSQL and a development mail catcher. The API must
allow those local Designer/Account origins and supply synthetic registration
policy documents and Trial entitlements. Never point this test at a customer DB.

From the repository root, with Google Chrome installed:

```sh
PVAD_RUN_NATIVE_INSTALL=1 \
PVAD_NATIVE_TEST_DISPOSABLE=1 \
PVAD_NATIVE_TEST_ORIGIN=http://localhost:16376 \
PVAD_NATIVE_TEST_ACCOUNT_ORIGIN=http://localhost:5178 \
PVAD_NATIVE_TEST_MAIL_ORIGIN=http://127.0.0.1:32850 \
node tools/configuration/native-project-installed-smoke.mjs
```

The ports above are the recorded disposable run's addresses, not permanent live
services. Supply the addresses of your own isolated environment. Without an
existing synthetic account, the runner registers and verifies a fresh one. For
repetition, supply `PVAD_NATIVE_TEST_EMAIL` and `PVAD_NATIVE_TEST_PASSWORD` through
your test environment; only `@example.test` accounts are accepted. Credentials
are not written to the report. Do not disable rate limits to repeat registration.

The runner performs a real user-level app installation. Run it interactively
with explicit permission, approve only the synthetic file-handling request if
Chrome asks, and allow cleanup to finish. It owns only its new Chrome profile,
synthetic archives and new PVADE app. Failures retain their reports and attempt
uninstall in `finally`; inspect `cleanupError` before another run.

## Remaining Boundaries

- Manual installation through Chrome's ordinary install UI and first-run native
  Allow/Block dialog acceptance were **not** performed. The automated installation
  and actual app-settings revocation do not certify those manual interactions.
- Windows, Linux, Safari, Firefox and other Chromium versions remain untested.
- Pre-existing competing associations, shared Chrome profiles, production
  HTTPS/hosts and deployment updates require separate tests.
- Final corporate artwork remains a separate product decision.
- The global `nativeFileOpenSupported` flag stays false: this scoped local result
  must not become an unconditional cross-platform support claim.

The rename association criterion is locally satisfied for the explicitly selected
macOS/Chrome scope only. Remote release, publisher, domain, infrastructure and
deployment gates remain open. See [project opening](pvad-native-project-opening.md)
and the [rename checklist](../checklists/Rename-PXA-To-PVAD.md).
