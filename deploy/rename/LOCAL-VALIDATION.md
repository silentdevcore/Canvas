# Local Rename Validation

## Run

From the repository root:

```bash
node tools/configuration/rename-local-validation.mjs
```

Prerequisites: .NET 10 and restored dependencies, Docker with cached PostgreSQL
17 and Nginx 1.29 images, installed Playwright Chromium, current Company and
Designer production builds, and the Designer development server on localhost
port 5176. The browser origin may be overridden with
`PVAD_DESIGNER_SMOKE_ORIGIN`, but must remain loopback. Port 14183 must be free.
The runner does not start or restart application hosts.

Run only in an approved development environment. The runner creates isolated
synthetic databases, test key directories and ephemeral containers. Existing
databases, installed browser apps and customer keyrings are not modified.

## Twelve Checks

| Check | Evidence | Boundary |
| --- | --- | --- |
| Configuration contracts | Native manifest, locales, icons, proxy and candidate tests | Source contracts, not installed OS registration |
| Keyring and auth | Restored relocated keyring reads both payload generations; empty ring fails; CORS contracts | Fresh synthetic keys, not existing customer key access |
| Database recovery | Real dump/restore, corrupt input and target safety checks | Isolated synthetic Legal schema |
| Service cutover | Stop old writer, retain volume, switch back and verify both rows | Database container names, not full application rolling compatibility |
| Operator proxy | Canonical/legacy inputs accepted, conflict rejected | Nginx configuration validation, not live operator session |
| Website hosting | SPA/404/cache/API failure behavior | Local Company build, no public DNS or TLS |
| Native production hosting | Built Designer behind real Nginx, manifest/icons/worker, Chromium manifest and installability diagnostics | No OS install/open/uninstall or live API |
| Native intake browser | Anonymous bytes unread, explicit refresh, discard, mobile/desktop | Synthetic session responses |
| Native import browser | Both editors, one read/upload, required headers and visible content | Synthetic API results, not archive-validation evidence |
| Release contracts | Version, fragment and deployment contract tests | No registry queries or downloads |
| Version consistency | Existing package/build version consistency | Does not prepare a stable release |
| Release preview | Aggregated proposed major release with unchanged version files | No tag, package, push or deployment |

The report and step logs are written to ignored
`artifacts/rename/local-validation/`. Detailed browser and cutover reports stay
under their existing artifact directories. Reports contain scoped synthetic test
results, not release approval. A failed step makes the aggregate command fail;
later steps still run so one report records all attempted checks.

## Remaining Internal Work

Without an optional header profile the public website template still has no CSP.
The [Designer profile draft](../websites/DESIGNER-SECURITY.md) adds a baseline and
report-only resource diagnostics when mounted explicitly. The final browser run
was not approved; full resource enforcement, native PDF/print and code-editor
compatibility remain unverified. The earlier successful twelve-step report
predates this draft. Do not call that report validation of the current profile.
This remains an internal prerequisite, not an external-provider dependency.

## Separate Acceptance

- Real supported-OS PWA installation, permission, file-open and uninstall checks.
- Existing deployment key mounts, certificate access and owner-approved recovery.
- Confirmed public hosts, TLS, OAuth redirects, mail and webhook delivery.
- Registered publishers, approved major-release preparation and publication.
- Full multi-host staging with the real API and authentication flows.

The local bundle does not authorize any of these operations or close the nine
combined release/cutover gates. Overall checklist completion remains 88/97.
