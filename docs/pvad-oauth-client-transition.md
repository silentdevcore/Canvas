# PVAD OAuth Client And Scope Transition

## New Identities

| Boundary | New default | Retained reader/registration |
| --- | --- | --- |
| Public interactive client | `pvad-mcp-public` | `pxa-mcp-public` |
| Internal confidential client | `pvad-mcp-server` | `pxa-mcp-server` |
| Service-account client | `pvad-sa-<key-id-N>` | `pxa-sa-<key-id-N>` |
| API resource option | `pvad-api` | Explicit `pxa-api` configuration override |
| OAuth scope catalog | Twelve known `pvad.*` scopes | Their exact `pxa.*` aliases |
| MCP protected-resource metadata | `pvad.docs.read`, `pvad.documents.validate` | Existing tokens with equivalent read scopes remain accepted |

New built-in/public/DCR/service-account permissions include both generations of
the three existing read scopes. No write, render, job or migration capability is
added to MCP. Unknown or case-modified scope names are not mapped by prefix.
Token issuance preserves the requested scope strings.

The reserved `ApiResource` option does not currently participate in OAuth token
issuance or validation. Renaming its default is not an audience migration.
Issued access tokens and remote JWT validation still use the exact configured
`McpResource` URI, issuer and accepted algorithms. Those URI identities, signing
keys and strict signed MCP-delegation identity tuples are not broadened.

## Retained Clients Are Not Renamed In Place

Application IDs, old client IDs, credentials, grants and token rows remain intact.
Service-account exchange accepts either strictly parsed ID prefix but still
requires an exact match to the stored API-key `OAuthClientId`, active tenant and
account, unrevoked/unexpired key and permitted read scopes. A prefix alias alone
never authenticates another client or key.

Use the Account-returned client ID rather than constructing it. Existing
service-account clients remain under their old ID. To move one to the new naming,
create a new key through the existing Account/Admin operation, install its new
credentials in the consuming workload, verify it and explicitly revoke the old
key. That deliberate rotation is not a transparent preservation of the same
secret. Revocation checks both exact ID generations for the key being revoked.
Custom and existing DCR IDs remain unchanged.

## Explicit Built-in Client Provisioning

Fresh stores create current clients as usual. If a configured canonical built-in
client is absent but its known old counterpart exists, normal startup fails
closed with a maintenance instruction. It does not silently create a differently
configured client or mutate an existing registration.

1. Back up the store, configuration and signing keys. Stop OAuth traffic and all
   replicas. Use a dedicated service scope without pending business changes.
2. Resolve `PvadOAuthClientTransition` through `ActivatorUtilities.CreateInstance`
   and call `RunAsync()` for a read-only plan. It returns a count and dry-run flag,
   not IDs, subjects, secrets, hashes, source code or token data.
3. Review retained redirect URIs, requirements and client permissions separately.
   A current counterpart copies those settings and adds only canonical aliases
   for already granted known scopes. It does not receive the broader fresh-client
   defaults. Client-type mismatch fails planning. A confidential counterpart also
   requires the configured plaintext secret to validate against the retained
   registration; no stored hash is passed through plaintext hashing.
4. After owner approval, call `RunAsync(false)` in that maintenance scope, or set
   `OAuth:MigrateLegacyClientIdentities=true` for one maintenance instance.
   Startup preflights this approval before applying optional scope migration.
5. Independently plan/apply `PvadOAuthScopeMigration` or enable
   `OAuth:MigrateLegacyScopeAliases=true` if old registrations/grants must accept
   canonical scope requests. This only augments permissions and active grants;
   revoked grants and every issued token remain untouched.
6. Disable both flags, restart all compatible replicas to discard caches, and
   verify current/legacy login, PKCE, client-credential exchange, scope denial,
   tenant isolation and revocation before resuming traffic.

The client operation creates parallel registrations only for the configured
built-ins. It never copies grants or tokens to them and does not manufacture a
second service-account registration. Current registrations already present are
not overwritten or implicitly rotated. Custom configured client IDs do not acquire
invented aliases. Each creation is committed through the manager; this is not one
transaction for the entire batch. A failed/cancelled partial run can leave earlier
creations committed, and retry is idempotent. No concurrent client administration
or mixed-replica cache safety is promised during maintenance.

## Rollback And Validation

Stop traffic, select retained `OAuth:PublicClientId` and
`OAuth:McpServerClientId`, retain the old resource/URI configuration where required,
and restart the matching binaries. Old registrations, grants, token bytes and keys
are still available. Do not delete current registrations used by new workloads or
rewrite issued token audiences/presenters to make rollback look transparent.
Newly issued current-client tokens do not become legacy-client tokens.

Tests cover fresh stores, explicit approval, dry-run, repeat application, metadata
and credential preservation, unknown scopes, type/secret guards, revoked grants,
issued token bytes, cancellation and retained-ID rollback. Actual PostgreSQL
checks cover manager-backed revocation and full client-credential HTTP flows for
both ID generations. MCP JWT tests retain exact audience/issuer validation.
This is synthetic local evidence, not execution of a customer-store migration or
the final whole-repository, multi-host release acceptance.

See [identity compatibility](pvad-identity-compatibility.md),
[client setup](mcp-client-setup.md),
[rename tracker](../checklists/Rename-PXA-To-PVAD.md) and
[release approvals](pvad-rename-release-approvals.md).
