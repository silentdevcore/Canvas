# PVAD Identity And Protected-Data Compatibility

This rename separates public branding from persisted security contracts. It does
not rotate customer credentials, reset sessions, replace signing keys or rename
physical database/storage resources.

## Implemented In This Stage

- Neutral, canonical unversioned PVAD and historical PXA routes resolve to the same
  product entitlement, Designer permission and feature gate. Versioned PXA routes
  resolve to their PVAD counterparts. Near-prefix paths are not treated as aliases.
- New service-account API keys use `pvad_`. Existing `pxa_` keys remain valid until
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
| Mail outbox | `PVAD.Mail.Outbox.Payload.v1` | `PXA.Mail.Outbox.Payload.v1` |
| PDF revocation evidence cache | `PVAD.PdfTrust.RevocationEvidence.v1` | `PXA.PdfTrust.RevocationEvidence.v1` |

## Browser Cookie Transition

| Purpose | New production cookie | Legacy reader |
| --- | --- | --- |
| Account/Admin session | `__Host-PVAD.Session` | `__Host-PXA.Session` |
| Designer session | `__Host-PVAD.Designer.Session` | `__Host-PXA.Designer.Session` |
| CSRF | `__Host-PVAD.Antiforgery` | `__Host-PXA.Antiforgery` |

Development uses `PVAD.Session.Development`, `PVAD.Designer.Session.Development`
and `PVAD.Antiforgery.Development`, with their `PXA.*` counterparts as legacy readers.
Production and development names are not interchangeable. Cookie Domain remains
unset, Path is explicitly `/`, HttpOnly and SameSite Strict remain mandatory, and
production cookies require Secure. Account/Admin and Designer retain separate
ticket protection schemes; an Account ticket cannot authenticate Designer.

Legacy session tickets undergo the existing expiry, security-stamp, persistent
session, organization and operator validation before renewal. Only a validated
legacy ticket is renewed under the canonical cookie name. The cookie manager
supports the existing chunked format and removes legacy chunks during renewal;
logout removes both name generations. Authentication schemes and their ticket
protection purposes are deliberately retained so the old ciphertext remains usable.

A canonical cookie never falls back to a different legacy value, including when
the canonical value is empty or invalid. Duplicate managed cookies/chunks are
rejected. CSRF aliases do not bypass token validation: old cookie values are
mapped to the canonical name, and both header generations still pass the real
antiforgery validator. Matching aliases are accepted. Conflicts clear both CSRF
cookies and return `PXAAPI008`; fetch a new CSRF token and retry. Malformed headers
fail closed. Token and ticket values are never included in errors.

## Security Identities Deliberately Retained

### Claims And Delegation Transition

New principals and token claims use `pvad_permission`, `pvad_organization`,
`pvad_active_organization`, `pvad_service_account` and `pvad_session`. The five
historical `pxa_*` claim families are normalized before persistent-cookie
validation, permission expansion and authorization-code exchange. Values,
issuers, identity ownership and claim metadata are preserved. Singleton ambiguity
and unequal old/new claim sets fail closed before any partial normalization.
Legacy cookie claims request renewal only after normal session validation succeeds.

MCP's JWT reader defaults to `pvad_organization` and accepts the old organization
claim. A configured custom claim retains its own meaning. Conflicting aliases,
empty canonical values and inconsistent `scope`/`scp` fields are rejected. The
three known read-only MCP scopes have explicit `pxa.*`/`pvad.*` equivalence; unknown
scope prefixes are not automatically rewritten. Internal bearer middleware accepts
both generations, while the access-context tool reports canonical scope names.

New MCP-to-API delegations use `PVAD-MCP`, issuer `pvad-mcp-server`, audience
`pvad-api` and canonical read-only scopes. Receivers accept exactly these identity
combinations, not arbitrary mixtures:

| Authorization prefix | Signed issuer | Signed audience | Purpose |
| --- | --- | --- | --- |
| `PVAD-MCP` | `pvad-mcp-server` | `pvad-api` | Canonical output |
| `PXA-MCP` | `pxa-mcp-server` | `pxa-api` | Original legacy output |
| `PXA-MCP` | `pvad-mcp-server` | `pxa-api` | Explicit historical transition adapter |

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
| Authentication schemes | `PXA.Combined`, `PXA.ApiKey`, `PXA.McpDelegation`, `PXA.Designer.Cookie` | Scheme changes can change ticket protection purposes |
| Legacy claim values | Existing user, organization, service-account and session IDs | Canonical claim names do not replace the identifiers themselves |
| OAuth registrations/scopes | Canonical twelve `pvad.*` scopes with known `pxa.*` aliases | Apply the opt-in alias migration to each retained store and verify client login before switching requests |
| OAuth API resource setting | New default `pvad-api`; retained overrides may use `pxa-api` | This reserved option is not used to expand token audiences. Actual issuance/remote validation still uses the exact configured MCP resource URI. |
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

Old binaries also do not read PVAD cookie names. A rollback must retain the cookie
adapter or require a new login and fresh CSRF tokens. Test reverse-proxy forwarding,
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
[rename roadmap](../checklists/Rename-PXA-To-PVAD.md) and
[test report](../checklists/PVAD.Rename-Test-Report.md).
