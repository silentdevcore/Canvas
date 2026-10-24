# PVAD Serialized Contract Inventory

## Machine-Readable Index

`node tools/configuration/serialized-contract-inventory.mjs` produces
`checklists/PVAD.Serialized-Contract-Inventory.tsv` from all checked-in JSON schemas,
OpenAPI property/operation names and the five canonical claim types. Only contract
names are recorded; no runtime tokens, customer values or configuration secrets.
The schema index includes both canonical and retained legacy schemas, not merely
the current writer. Existing schema/DTO coverage tests remain the behavior gate.

## Contract Decisions

| Boundary | Canonical writer | Compatibility decision |
| --- | --- | --- |
| Design/page/element JSON | Neutral DTO property names | No blanket rename of arbitrary customer or extension properties |
| Migration result | `pvadCode` / `PvadCode` | Retain legacy aliases and provider diagnostics as documented |
| E-invoice migration mapping | `pvadEquivalent` / `PvadEquivalent` | Obsolete `pxaEquivalent` / `PxaEquivalent` remains a read/write alias to the same value for the compatibility window. |
| Native archive manifest | PVAD format identities, `minimumPvadVersion` | Exact old tuples/version aliases remain readable; conflicts rejected |
| Permission/org/active-org/service/session claims | Five `pvad_*` names | Legacy aliases recognized with conflicting-value rejection |
| OAuth issuer and resource audience | Configured URI identity | Do not derive new domains from branding or rewrite issued tokens/grants |
| OAuth scopes/client IDs | Known canonical scope aliases and new PVAD dynamic-registration IDs; built-in/public/service-account defaults and MCP discovery still require writer cutover | Existing scope/grant migration is opt-in; client/resource identity transition and final verification remain open |
| Browser events and handoffs | Documented `pvad:` events/envelopes | Owner-bound legacy recovery where specified; no new cross-tenant adoption |
| Jobs and worker messages | Neutral typed JSON/protocol fields | Preserve job IDs, payload fields and result aliases; deploy paired worker/API |
| Legal/license/audit content | Existing immutable/versioned contracts | No in-place reserialization of accepted/signed retained evidence |

## Serialized Type Names

`PdfEInvoiceProviderCapability` and `PdfEInvoiceMigrationFinding` expose the
canonical mapping property and serialize both names with equal values. Tests
exercise each input generation separately, preserving diagnostic IDs and source
markers. This is not a conflicting-dual-input rejection guarantee: normal JSON
property order determines the last assigned alias. These mapping DTOs do not
select tenants or authorization context. The current OpenAPI snapshot contains
neither typed mapping schema, so this addition does not require hand-editing it.

The owned runtime scan found no `TypeNameHandling`, `JsonDerivedType` or
`AssemblyQualifiedName` persistence scheme in `src`/`PVAD.WebApi`. CLR namespaces
are not a reason to rewrite neutral JSON. `exception.GetType().FullName` is used
for the existing bounded log exception-type field, not polymorphic document
deserialization. This source inventory does not inspect arbitrary uploaded files
or customer databases; those require their own migration assessment.

OAuth URI values remain deployment inputs; production allowlists, issuer/audience
validation and existing client credentials must be checked at cutover. See
[identity](pvad-identity-compatibility.md), [HTTP](pvad-http-compatibility.md),
[schema/client](pvad-schema-and-client-contracts.md) and
[rollback](pvad-rename-rollback.md) documentation for the behavioral boundaries.
