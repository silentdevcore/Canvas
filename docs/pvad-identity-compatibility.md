# PVAD Identity And Protected-Data Compatibility

This rename separates public branding from persisted security contracts. It does
not rotate customer credentials, reset sessions, replace signing keys or rename
physical database/storage resources.

## Implemented In This Stage

- Neutral, canonical unversioned PVAD and historical PVAD routes resolve to the same
  product entitlement, Designer permission and feature gate. Versioned PVAD routes
  resolve to their PVAD counterparts. Near-prefix paths are not treated as aliases.
- New service-account API keys use `pvad_`. Existing [compatibility entry `compat-5efd4896aaff`](../operator-docs/compatibility-identifiers.v1.json) keys remain valid until
  normal expiry or revocation. Both work through Bearer authentication and the
  canonical API-key header; the historical header is normalized before auth.
- Key lookup continues hashing the complete original secret. Stored hashes and
  key IDs are not rewritten. Normal rotation issues a new key, not a renamed secret.
- New mail payloads and PDF revocation-cache entries use PVAD Data Protection
  purposes. Readers first try the current purpose, then the matching historical
  purpose on cryptographic failure only. Mail and evidence remain isolated.
- Reading old ciphertext does not re-encrypt the retained record. Existing keys
  and the application discriminator `PowerDoxAutomation` are retained. Tests cover
  a provider restart using a synthetic file-backed keyring and wrong-app rejection.

| Protected content | New writes | Legacy reader |
| --- | --- | --- |
| Mail outbox | `PVAD.Mail.Outbox.Payload.v1` | [compatibility entry `compat-a3b338ef72e2`](../operator-docs/compatibility-identifiers.v1.json) |
| PDF revocation evidence cache | `PVAD.PdfTrust.RevocationEvidence.v1` | [compatibility entry `compat-fade525144b5`](../operator-docs/compatibility-identifiers.v1.json) |

## Browser Cookie Transition

| Purpose | New production cookie | Legacy reader |
| --- | --- | --- |
| Account/Admin session | `__Host-PVAD.Session` | [compatibility entry `compat-ebff574f1d7e`](../operator-docs/compatibility-identifiers.v1.json) |
| Designer session | `__Host-PVAD.Designer.Session` | [compatibility entry `compat-df6474192568`](../operator-docs/compatibility-identifiers.v1.json) |
| CSRF | `__Host-PVAD.Antiforgery` | [compatibility entry `compat-f5dc771391bd`](../operator-docs/compatibility-identifiers.v1.json) |

Development uses `PVAD.Session.Development`, `PVAD.Designer.Session.Development`
and `PVAD.Antiforgery.Development`, with their [compatibility entry `compat-dc1f0686d317`](../operator-docs/compatibility-identifiers.v1.json) counterparts as legacy readers.
Production and development names are not interchangeable. Cookie Domain remains
unset, Path is explicitly `/`, HttpOnly and SameSite Strict remain mandatory, and
production cookies require Secure. Account/Admin and Designer retain separate
ticket protection schemes; an Account ticket cannot authenticate Designer.

Legacy session tickets undergo the existing expiry, security-stamp, persistent
session, organization and operator validation before renewal. Only a validated
legacy ticket is renewed under the canonical cookie name. The cookie manager
supports the existing chunked format and removes legacy chunks during renewal;
logout removes both name generations.

New authentication uses `PVAD.Combined`, `PVAD.ApiKey`, `PVAD.McpDelegation` and
`PVAD.Designer.Cookie`. The four historical scheme names forward to these handlers
for the compatibility window; they do not create a second legacy cookie writer.
Account/Admin keeps the framework's `Identity.Application` scheme unchanged.
The Designer ticket reader first tries the current protection purpose, then only
the exact historical Designer purpose. A decoded ticket must also carry the
matching serialized scheme. Account tickets, wrong-purpose ciphertext and
tampered payloads cannot enter through this fallback.

Earlier Designer tickets are normalized in memory and marked for renewal. That
marker is consumed by the existing validation events; only an active, verified,
unlocked user with a matching security stamp, live persistent session and valid
organization context can receive a renewed ticket. New writes always use the
current Designer scheme and protection purpose, even if callers use an old alias.
Neither migration marker nor legacy scheme is written into the renewed ticket.
The persisted session ID, revocation state, expiry and keyring discriminator are
not migrated. Existing entitlement and permission checks still apply separately.

A canonical cookie never falls back to a different legacy value, including when
the canonical value is empty or invalid. Duplicate managed cookies/chunks are
rejected. CSRF aliases do not bypass token validation: old cookie values are
mapped to the canonical name, and both header generations still pass the real
antiforgery validator. Matching aliases are accepted. Conflicts clear both CSRF
cookies and return [compatibility entry `compat-0d6278d6b59d`](../operator-docs/compatibility-identifiers.v1.json); fetch a new CSRF token and retry. Malformed headers
fail closed. Token and ticket values are never included in errors.

## Security Identities Deliberately Retained

### Claims And Delegation Transition

New principals and token claims use `pvad_permission`, `pvad_organization`,
`pvad_active_organization`, `pvad_service_account` and `pvad_session`. The five
historical [compatibility entry `compat-3597927317fd`](../operator-docs/compatibility-identifiers.v1.json) claim families are normalized before persistent-cookie
validation, permission expansion and authorization-code exchange. Values,
issuers, identity ownership and claim metadata are preserved. Singleton ambiguity
and unequal old/new claim sets fail closed before any partial normalization.
Legacy cookie claims request renewal only after normal session validation succeeds.

MCP's JWT reader defaults to `pvad_organization` and accepts the old organization
claim. A configured custom claim retains its own meaning. Conflicting aliases,
empty canonical values and inconsistent `scope`/`scp` fields are rejected. The
three known read-only MCP scopes have explicit [compatibility entry `compat-fb5db5457234`](../operator-docs/compatibility-identifiers.v1.json)/`pvad.*` equivalence; unknown
scope prefixes are not automatically rewritten. Internal bearer middleware accepts
both generations, while the access-context tool reports canonical scope names.

New MCP-to-API delegations use `PVAD-MCP`, issuer `pvad-mcp-server`, audience
`pvad-api` and canonical read-only scopes. Receivers accept exactly these identity
combinations, not arbitrary mixtures:

| Authorization prefix | Signed issuer | Signed audience | Purpose |
| --- | --- | --- | --- |
| `PVAD-MCP` | `pvad-mcp-server` | `pvad-api` | Canonical output |
| [compatibility entry `compat-b1cad1347fb6`](../operator-docs/compatibility-identifiers.v1.json) | [compatibility entry `compat-b93898a80e92`](../operator-docs/compatibility-identifiers.v1.json) | [compatibility entry `compat-ca1eb4ce6067`](../operator-docs/compatibility-identifiers.v1.json) | Original legacy output |
| [compatibility entry `compat-b1cad1347fb6`](../operator-docs/compatibility-identifiers.v1.json) | `pvad-mcp-server` | [compatibility entry `compat-ca1eb4ce6067`](../operator-docs/compatibility-identifiers.v1.json) | Explicit historical transition adapter |

The last combination was already produced by an earlier rename stage; its reader
was missing and is now covered. HMAC verification, tenant/user identifiers, bounded
size/lifetime and read-only scope checks remain mandatory. Duplicate JSON fields,
tampering, expired/future/overlong envelopes and write scopes are rejected. These
short-lived read-only delegations remain stateless; this stage does not add a
persistent nonce-consumption store or claim replay prevention within their lifetime.

### OAuth Grants Are A Separate Phase

The server registers both generations of the twelve known scopes. Newly created
public, dynamically registered and service-account clients receive both aliases
of their three read-only scopes, never additional write capabilities. Token
validation and template-entitlement checks recognize either spelling. Issuance
retains the scopes actually requested; it does not silently rewrite grants.

Existing registrations and grants remain unchanged by default. The opt-in
`OAuth:MigrateLegacyScopeAliases` maintenance step adds canonical aliases only
where the matching known historical scope is already present. It does not create
new clients, rotate secrets or rewrite issued tokens. Canonical discovery alone
does not upgrade an existing client: without the migration it may still reject
canonical requests. Keep legacy login examples until all target stores are migrated.
The internal HMAC audience transition does not itself rename OAuth clients or
issuer URLs. The later [client transition](pvad-oauth-client-transition.md) now
uses canonical built-in, new service-account and dynamic-registration IDs, plus
canonical default/discovered scopes. Retained clients and issued tokens keep their
IDs. Issuer/MCP resource URLs and token audiences are not widened by this rename.

### Scope Migration Procedure

1. Back up the database and retain compatible binaries and signing keys.
2. Stop OAuth traffic and all other API/MCP replicas. Use a dedicated service
   scope: this migration must not share a context with pending business changes.
3. Resolve `PvadOAuthScopeMigration` through `ActivatorUtilities.CreateInstance`
   in that scope and call `RunAsync()` for a read-only plan. The result contains
   client/grant counts only, not secrets, subjects or scope payloads.
4. After reviewing the plan, set `OAuth:MigrateLegacyScopeAliases=true` on one
   maintenance instance. Startup applies the same operation before serving traffic.
   The default is false. A cancelled or failed run may have committed earlier
   records; re-running is idempotent. This is not an all-or-nothing transaction.
5. Disable the option, restart all compatible replicas to discard remote caches,
   and verify both scope generations, secret validation and revoked-grant denial.
6. Only then update the requesting clients to canonical scopes. Keep legacy scope
   readers and registrations during rollback; do not remove aliases automatically.

The operation uses OpenIddict stores/managers rather than replacing application
descriptors, so hashed secrets are not re-hashed as plaintext. Only permission
and active-authorization scope arrays are augmented. Revoked authorizations and
all issued token rows are untouched. Existing IDs, custom/unknown scopes, redirect
URIs, requirements, consent, status and expiration remain intact. Concurrent client
administration is unsupported during the maintenance window; concurrency failures
abort startup rather than overwriting another writer.

| Contract | Retained identity | Remaining migration work |
| --- | --- | --- |
| Authentication schemes | [compatibility entry `compat-08ab2cf7701b`](../operator-docs/compatibility-identifiers.v1.json), [compatibility entry `compat-61cd87ac9809`](../operator-docs/compatibility-identifiers.v1.json), [compatibility entry `compat-e8a6a6ca9982`](../operator-docs/compatibility-identifiers.v1.json), [compatibility entry `compat-5b314dd4e759`](../operator-docs/compatibility-identifiers.v1.json) | Historical aliases forward to current handlers; Designer has a read-only historical ticket-purpose fallback |
| Legacy claim values | Existing user, organization, service-account and session IDs | Canonical claim names do not replace the identifiers themselves |
| OAuth registrations/scopes | Canonical twelve `pvad.*` scopes with known [compatibility entry `compat-fb5db5457234`](../operator-docs/compatibility-identifiers.v1.json) aliases | Apply the opt-in alias migration to each retained store and verify client login before switching requests |
| OAuth API resource setting | New default `pvad-api`; retained overrides may use [compatibility entry `compat-ca1eb4ce6067`](../operator-docs/compatibility-identifiers.v1.json) | This reserved option is not used to expand token audiences. Actual issuance/remote validation still uses the exact configured MCP resource URI. |
| OAuth client identities | New `pvad-mcp-public`, `pvad-mcp-server`, `pvad-sa-<id>`; old counterparts remain registered | Explicit parallel built-in transition; retained service-account IDs remain exact until key rotation. No issued-token/grant rewriting. |
| Legacy signed MCP delegation | The two explicit historical combinations above | Compatibility readers retain original signed bytes |
| License signatures | Existing envelope JSON, key ID and `ECDSA_P256_SHA256` | No signed-byte replacement by rename |

Cookie Domains are not widened. Account/Admin and Designer retain their existing
host isolation, persistent session checks, CSRF and PKCE requirements. This stage
does not claim that a deployed browser or PostgreSQL session cutover has been tested.

## Deployment And Rollback Boundary

The new protected-data reader handles old and new payloads, but old binaries cannot
read newly written PVAD-purpose ciphertext. Do not run old mail consumers alongside
new writers. Deploy the compatible reader to every consumer before enabling new
traffic, or stop the old processes during a coordinated replacement. Keep the
existing keyring accessible at its existing location and under its existing
application discriminator. Do not create a fresh keyring as part of the rename.

Rollback after new ciphertext exists must retain the compatible reader. Returning
to a legacy-only binary requires an explicitly reviewed conversion or an approved
consistent data/key backup restore; it is not a source-only rollback. New `pvad_`
keys similarly require a reader that supports the new prefix. Never transform an
issued key string or signed license in place.

Old binaries also do not read PVAD cookie names or the current Designer ticket
purpose. A rollback must retain both the cookie-name adapter and compatible ticket
reader, or require a new login and fresh CSRF tokens. Keep the existing keyring;
renaming its discriminator would invalidate both generations. Stop legacy-only
writers during replacement or deploy compatible readers before switching writers.
This source change does not establish a safe mixed-binary rolling deployment.
Test reverse-proxy forwarding,
root cookie paths and host isolation before production rollout. Do not restore
parent-domain cookies or weaken SameSite as a shortcut. Native browser behavior
and live session revocation remain deployment/database acceptance gates.

Claims and delegation now also require compatible receivers. Updating an Account
token writer before the MCP claim reader, or the MCP delegation writer before the
API receiver, can interrupt access. Deploy the compatible components together or
stop traffic during replacement; this stage does not claim a zero-downtime rolling
upgrade. Rollback must retain alias-aware readers or require fresh compatible tokens.

## Boundaries And Remaining Tests

Synthetic tests cover API-key hashes/revocation, both protected-data purposes,
tampering, cross-purpose isolation, file-backed keyring reuse, actual mail queue
and processor compatibility, permission resolution and disabled feature gates.
The mail integration uses EF InMemory, not a PostgreSQL persistence proof.
OAuth scope migration is separately tested against both EF InMemory and an
isolated PostgreSQL container, including dry run, repeat execution, unchanged
client secrets/metadata, revoked authorizations and token payloads. HTTP tests
use the application's PostgreSQL migrations for discovery, registration and PKCE.
These synthetic checks do not prove that an existing customer backup is restorable.

Authentication transition tests additionally exercise actual cookie options and
alias registration, both Designer ticket purposes, ciphertext tampering, Account
isolation and file-backed keyring reuse across restarts. An isolated TestServer
uses the real cookie-validation events and session service with EF InMemory to
verify renewal, actual CSRF validation, sign-out/replay denial and invalid user,
stamp, membership, organization and expiry states. Its synthetic issuance helper
is not a production login endpoint. A separate PostgreSQL-backed rehearsal below
exercises actual application login and handoff; neither suite replaces production
browser acceptance.

### Isolated PostgreSQL Login And Restart Rehearsal

The opt-in API integration group uses real ASP.NET Core Identity, Npgsql, the
application's migrations and a new disposable PostgreSQL container for each test.
It does not connect to an existing development or customer database. The standard
hosts in this test group use ephemeral Data Protection providers; the two restart cases use
only their own temporary file-backed keyrings, removed after host disposal.

The restart cases first authenticate through Account and exchange a real PKCE
handoff. A test-only helper re-protects that validated Designer ticket with the
historical purpose to model earlier server issuance. It does not skip login,
entitlements, persistent sessions or the real cookie-validation events.
The first host is disposed before the second reads the old ticket. Renewal must
write the current purpose without creating another session, and the next request
must not repeat migration renewal. Account-cookie substitution remains denied.
Invalid CSRF cannot sign out the session; valid sign-out persists revocation.
A third host must reject both the original and renewed cookie using that same
PostgreSQL state. Both supported incoming cookie-name generations are covered.

The existing Account, Designer and Admin tests additionally cover disabled,
locked and suspended identities, expiry, security stamps, PKCE failures and
replay, membership/entitlement changes, tenant boundaries and operator access.
The operator response must contain equal current and retained identity headers.
The protected runbook test follows the exact compatibility-register reference;
the restore tool's safety phrase itself is not changed.

To reproduce on Rancher Desktop, with its Docker engine running and the test
images available locally:

```bash
export DOCKER_HOST="$(docker context inspect --format '{{.Endpoints.docker.Host}}')"
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
PVAD_RUN_POSTGRES_TESTS=1 dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj \
  --filter 'FullyQualifiedName~AuthControllerTests|FullyQualifiedName~DesignerAuthenticationControllerTests|FullyQualifiedName~AdminSystemControllerTests' \
  -p:SkipOcrWorkerPackaging=true -p:SkipCodeWorkerPackaging=true \
  --disable-build-servers -m:1 \
  --logger 'trx;LogFileName=real-postgres-auth.trx' \
  --results-directory artifacts/rename/postgres-auth
```

The socket override is the Docker VM's socket used by the resource reaper, not
the host-side Rancher socket. Without the opt-in, PostgreSQL tests are skipped;
a skipped result is not acceptance evidence. Tests use TestServer HTTP with a
synthetic HTTPS base URL, not a TLS handshake or an Account frontend browser.
This rehearsal does not yet connect the real API to the Nginx/Grafana lab,
establish real-host cookie isolation, prove SSO, authorize deployment or certify
customer backup recovery. The separate gateway drill still uses synthetic auth.

### Retained Storage Boundaries

Retained job/object encryption is not inferred from mail Data Protection. The
filesystem object-storage adapter writes the stream supplied to it; it does not
itself implement a Data Protection envelope. This rename stage neither changes
that behavior nor certifies encryption at rest for job objects. Database backup,
key access under production identities, in-flight jobs, cookie cutover and
production-host verification remain separate acceptance gates.

The follow-up source audit confirms that explicit application Data Protection
consumers are mail queue/delivery and PDF revocation evidence. Object storage and
background-job JSON are not protected by those purposes; inventing an encrypted
job migration during a brand rename would be a separate storage/security change.
Local file-backed keyring restart, purpose isolation, object-storage byte
preservation and CORS origin boundaries are regression-tested. Production key
access is still an operator-owned gate, not established by development captures.

See [HTTP compatibility](pvad-http-compatibility.md),
[license and job compatibility](pvad-license-and-job-compatibility.md),
[rename roadmap](../checklists/PVAD.Rename-Plan.md) and
[test report](../checklists/PVAD.Rename-Test-Report.md).
