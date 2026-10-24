# PVAD Offline Cutover Preparation

This directory prepares the remaining internal rename work. It does not deploy,
register packages, install OS associations, send mail, inspect real keys or alter
the current application defaults. Current completion remains 88/97 (90.7%);
partial work does not close the nine combined release/cutover gates.

## Confirmed And Proposed Values

The owner selected `pvade.com`, support address `support@pvade.com`, and technical
sender `no-reply@pvade.com` (2026-10-24). DNS/TLS/mailbox ownership
and delivery are not verified. Subdomains in `pvade-targets.candidate.json` are
**proposals**, not confirmed deployed hosts. NuGet, npm and container publishers
remain unselected. No registry ownership is inferred from a proposed name.

## Read-only Candidate

```bash
node tools/configuration/rename-cutover-candidate.mjs
node tools/configuration/rename-cutover-candidate.mjs --require-ready
```

The first command prints a deterministic offline candidate. The second exits 2
while prerequisites remain unmet. Neither command has deployment, installation,
network access or file-write operations. The candidate contains:

- Matching frontend site links and Designer Account runtime configuration.
- Explicit API host/CORS lists, public registration return origins and isolated
  Designer origins. Admin and Operator are not public registration destinations
  and no Company-to-Admin link is added.
- Consistent transactional mail URLs and the independently selected sender.
  The support address is not implicitly used as the mail sender. Mail delivery and
  OAuth stay disabled until the existing certificate/provider requirements are
  separately met. Existing OAuth client/resource IDs are not replaced.
- Canonical image names only when a publisher is explicitly selected. Selection
  is not ownership verification or publishing authorization.
- Proposed webhook configuration with no invented callback endpoint or secret.
- Prepared Linux MIME, macOS exported-type and Windows ProgID descriptors.

`apiSettings` is an overlay proposal, **not a replacement appsettings file**.
Review and merge it with the environment's configuration through the normal
deployment process. It deliberately contains no database credentials, private
keys, SMTP credentials, approved subscription policy or production bootstrap.
Internal API service aliases and loopback hosts remain in AllowedHosts for MCP
and health probes; the public proxy still owns TLS and host routing.

All browser sites use their own same-origin `/api` proxy. Therefore the candidate
does **not** grant credentialed cross-origin access among Company, Designer,
Account, Admin and Operator. Its CORS list contains only the API's own origin.
Add a separate browser SDK origin only after reviewing the specific public API
boundary; do not expose administrative session endpoints to public-site origins.

## Configurable API CORS

The API now reads `Cors:AllowedOrigins` (environment keys
`Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.). A provided list replaces
the development defaults, never unions with them. Origins must be explicit HTTP(S)
origins without credentials, paths, query, fragment, wildcard or whitespace.
Empty/oversized lists fail. Errors name the setting, not its potentially sensitive
value. Credentialed CORS never uses a wildcard.

The existing localhost defaults remain when no list is configured. CORS is not
authentication or tenant authorization, and it does not replace host-only session
cookies, CSRF, PKCE or server-side product permissions. Production browser hosts
must use HTTPS and be switched together with proxy/CSP, return URL allowlists,
mail links and OAuth certificates/callbacks.

## Canonical Service Preparation

Current bundled Compose defaults use PVAD throughout. Existing data and legacy
DNS aliases require the explicit root/API compatibility overlays in this folder,
with verified external resource IDs from `legacy-bindings.env.example`. Keep the
installed project name and stop old writers first. The focused configuration
tests pass without starting resources; runtime cutover remains unverified.
See the [infrastructure mapping](../../docs/pvad-infrastructure-mapping.md). The
function below also accepts an older parsed model; it is not a deployment command.

The exported `canonicalComposeCandidate(document, volumeBindings)` function takes
a **parsed, self-contained** Compose model and explicit logical-to-physical volume
IDs. It returns a candidate model; it never invokes Docker.

- Service keys and depends_on references become canonical together.
- Legacy and canonical DNS aliases coexist on the same existing network keys.
  Existing IPAM/static addresses, images, secret references, storage mounts,
  monitoring selectors, rule paths and environment values are retained.
- Every persistent volume must have an explicit physical ID and becomes external.
  Missing IDs, unknown bindings, service/DNS collisions and unsupported identity
  mechanisms such as container_name, links or extends are rejected.
- A database/user/volume is never renamed or copied by string replacement.

The focused tests use synthetic resource IDs and actual `docker compose config`
only; they pass on the renamed sources. No candidate services were started by
this follow-up. Before a real cutover, verify actual volume IDs,
backups, mounts and key access and stop the old writers. **Never run two PostgreSQL
servers on one data volume or old/new workers concurrently without the separately
validated lease/cutover procedure.** Keep the verified old model for rollback.

## File Types Are Not Yet OS Open Support

`product-metadata/native-file-types.json` describes `.pvadp` and `.pvads`. Generated
descriptors do not claim `.zip`, install a default application or add shell open
commands. `nativeFileOpenSupported` remains false.

The Designer now includes a PWA launch handler and authenticated, explicitly
confirmed in-memory file transfer. See [installation and boundaries](../../docs/pvad-native-project-opening.md).
The file chooser is a fallback, not evidence of OS integration. Real macOS,
Windows and Linux installation/open/uninstall tests remain required. Do not
register a nonexistent executable or send local paths in public URLs.

## Isolated Local Service Cutover

`node tools/configuration/service-cutover-smoke.mjs` uses the already cached
PostgreSQL image and a new labeled synthetic volume. It stops the legacy-named
container before starting the canonical-named container on the same volume,
checks retained rows, and returns to the first container to verify both rows.
It publishes no ports, mounts no customer directories, and removes only resources
with its unique test label. The latest successful report is generated under
`artifacts/rename/service-cutover/report.json`. This tests service-name and data
continuity, not application-version compatibility or production recovery.

## Remaining Evidence

Use the [local validation bundle](LOCAL-VALIDATION.md) to repeat the twelve
internally executable checks. A passing bundle is not a live staging deployment.
Public Designer CSP policy validation remains an internal predeployment task;
the Operator CSP does not substitute for it.

| Gate | Internal preparation | Still required |
| --- | --- | --- |
| Publishers | Separate validated fields and canonical image naming | Registration, ownership and actual release approval |
| Data Protection | Existing old/new consumer and keyring restart tests; no new envelope invented for unencrypted stores | Real key mounts, recovery and operator sign-off |
| Native file opening | PWA launch handler, authenticated file handoff and canonical type contracts | Real per-OS install/open/uninstall tests |
| Services/images | Read-only canonical model, retained volumes/DNS and Compose config tests | Owner-selected images, stopped-writer cutover and rollback |
| Hosts/auth/mail | Shared candidate plus configurable CORS and real in-process preflight tests | Confirmed subdomains, DNS/TLS, CSP/proxy, OAuth and delivery verification |

Keep these parent tasks open until all of their acceptance evidence exists. A
passing candidate validation is not proof of an external deployment.
