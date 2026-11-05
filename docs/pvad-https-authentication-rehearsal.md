# Isolated HTTPS Authentication And Recovery Rehearsal

This internal release-readiness drill runs the real Account, Designer and Admin
production frontend builds against a real HTTPS Kestrel API and disposable
PostgreSQL. It is not a deployment procedure or production approval.

## Important Environment Boundary

The current data-processing inventory has outstanding retention approvals.
The actual `Production` process must exit before listening with the retention
startup diagnostic. The drill asserts this denial; it does not modify the
inventory, disable the gate or manufacture approvals.

Positive browser scenarios use `Staging`, real OAuth signing certificates and
the non-Development cookie rules. Explicit operator authorization, database
legal-document requirements and product entitlement checks remain enabled.
Published policy records in the disposable database are synthetic test
preconditions, not evidence of a real legal publication or approval workflow.

## Reproduce

Required tools are .NET 10, the repository's restored EF tool, Node/npm,
OpenSSL and a running Rancher Desktop Docker engine on macOS. Install repository dependencies
normally first. The Designer's installed Playwright package must match the
browser image version exactly.

Prepare the test images explicitly:

```bash
docker pull postgres:17-alpine
docker pull nginx:1.29-alpine
docker build --tag pvad-https-auth-browser:1.64.1 \
  tools/configuration/https-auth-browser
node tools/configuration/https-authentication-rehearsal.mjs
```

The runner pins the locally selected images by immutable image ID and never
pulls or publishes an image itself. It accepts no target names, URLs, database
credentials or deployment arguments. Do not adapt it to an installed database.

## Trust And Isolation

The private one-day CA and server keys exist only in an owned temporary
workspace. Chromium trusts the CA in its disposable Linux NSS store. The Node
request client uses the same public CA. Neither the macOS trust store nor host
DNS configuration is changed. Untrusted and wrong-host certificates must fail.
Certificate verification is never disabled on either hop.

Three Docker DNS aliases serve separate Account, Designer and Admin origins.
The Nginx configuration derives from the actual website template and retains
the Designer CSP. Nginx verifies the API's HTTPS certificate and hostname.
This exercises HTTPS on both hops, not HTTP TLS termination or a production
forwarded-header configuration. The API's loopback listener is reachable through
Rancher Desktop's host forwarding. A Linux Docker host needs a separate reviewed
networking arrangement and is not certified by this macOS-hosted drill.

Each run owns a unique labeled network, disposable PostgreSQL instance,
gateway and non-root browser container. Only loopback ports are published.
The helper accepts bounded private stdin and validates its workspace marker
and exact temporary database name. No test-control API is exposed. Passwords,
ticket bytes and callback codes remain in private memory or temporary files;
they are not report fields or command-line arguments.

## Assertions

1. Trusted-host success and real untrusted/wrong-host certificate failures.
2. Account UI login, real PKCE callback, parameter removal and Designer autosave.
3. Separate Admin UI login, operator access and denied Editor administration.
4. Secure, HttpOnly, SameSite Strict, root-path, host-only cookies and isolated
   Designer ticket protection. Account and Admin intentionally share the
   framework ticket purpose; cookie transport isolation is not a claim that
   manually copying their ticket bytes creates a cryptographic boundary.
5. CSRF, authorization-code replay, wrong PKCE verifier and unsafe return-path
   rejection.
6. Actual validated canonical tickets rewrapped with the retained legacy
   purpose/name, then renewed by the API without creating replacement sessions.
   This is reader compatibility, not login with a historical application binary.
7. Existing sessions survive a real API process restart.
8. A wrong keyring rejects tickets; restoring the retained keyring restores access.
9. A stopped PostgreSQL dump and retained keyring restore into a different owned
   database. A post-backup template mutation disappears. The same pre-backup
   protected mail/cache ciphertext remains readable with the restored keys.
10. Disabled users, suspended organizations and missing Designer entitlement
    are denied; Account remains available when only the entitlement is removed.
11. Logout revocation survives restart and copied-cookie replay is denied while
    an independent Admin session remains valid.
12. Persisted session expiry rejects an otherwise valid Secure cookie.

## Evidence And Cleanup

Ignored output is written to `artifacts/rename/https-authentication/`:

- `report.json`: result, source/binary/image and PNG checksums, browser checks and limits.
- `browser/`: synthetic desktop/mobile screenshots and bounded scenario results.
- Phase logs and bounded failure reports for local diagnosis.

The final report is successful only after source pins still match and owned
resources are removed. Cleanup checks resource ownership and does not prune
Docker globally. Temporary credentials, private keys, dumps and browser
configuration are removed. No browser storage-state archive, video or trace is
exported. Failure evidence strips URL credentials, query strings, fragments and
known test secrets. Incomplete or duplicate scenario evidence, missing images,
mocked APIs and disabled TLS validation cannot produce a successful report.
Logs and screenshots are local synthetic evidence, not public docs.

The phone-size Designer image intentionally records the current Desktop required
guard. It does not establish phone editing support. Account and Admin screenshots
show their responsive layouts. Browser interactions for login and handoff run at
desktop size; the screenshots alone are not complete mobile workflow tests.

## Verified Local Result

On 2026-11-05 the final run passed all 12 browser scenarios, produced six
checksummed PNGs and verified 30 source pins plus API/helper binary and image
identities. Production exited with controlled code 78 before listening; positive
flows used Staging. Cleanup removed all owned runtime resources and private
workspace material.

Companion validation passed 58 focused API tests, 344 configuration/naming/release
contract tests and 60 Documentation tests. All three frontend builds and the
Documentation build passed. Version synchronization, fragment validation,
Markdown/JSON naming guards and the reviewed residual ledger passed. The product
version remains unchanged; the internal-only fragment has no release impact.

## Remaining Acceptance Gates

This drill does not establish production retention approval, public DNS/TLS,
SSO, customer backup recovery, multi-replica behavior, historical-binary rolling
upgrade, native macOS/Chrome acceptance or post-backup revocation reconciliation.
A point-in-time database restore can resurrect state absent from that snapshot;
real disaster recovery must separately reconcile subsequent revocations.

See [identity compatibility](pvad-identity-compatibility.md),
[monitoring rehearsal](pvad-integrated-monitoring-rehearsal.md) and
[rename follow-up](../checklists/PVAD.Repository-Naming-Followup.md).
