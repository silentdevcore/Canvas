# PVAD HTTP And Migration Compatibility

This is an in-progress rename contract, not a deployment completion statement.

## Common API Diagnostic Aliases

The 18 common API diagnostics keep their existing `code` values `PXAAPI001`
through `PXAAPI018`. Legacy routes retain their existing generated problem type
URLs; canonical routes now use the selected PVAD documentation domain. Responses
created or completed through `PvadApiProblems` add `canonicalCode`, explicitly mapping each identifier
to `PVADAPI001` through `PVADAPI018` without changing its numeric meaning.
Existing clients can continue inspecting `code`; new clients may display or index
`canonicalCode` and fall back to `code` when absent.

The same explicit mapping covers `PXAPDFTRUSTAPI001` through `003`, `101` through
`106`, `111` through `114`, `121` through `126`, and `901`, plus
`PXAPDFACONVERTAPI001` through `006`, adding corresponding `PVADPDF...` aliases.
It applies when Problem Details passes through shared creation/completion,
including ASP.NET Core's configured Problem Details writer. Provider reports and
other embedded diagnostics are not rewritten by that writer.

The shared, bounded catalog in `PVAD.Core.Contracts.PvadDiagnosticAliases` now
contains 494 reviewed IDs, including current code-workspace/worker, offline-license,
Designer handoff/feature/asset, subscription and entitlement diagnostics. It is an
exact allowlist, not a general prefix substitution. Unknown future IDs, Roslyn
errors and CANMIG identifiers receive no invented alias.
No historical audit, notification or job-diagnostic rows are rewritten.

The PDF diagnostic projection also covers redaction findings, PDF/A writer
preflight, PDF/UA accessibility preflight, archival conversion findings, parser
diagnostics and cross-reference diagnostics. Their legacy code, capability,
severity, offsets, object/page positions and localization metadata are preserved.
The catalog adds 110 exact IDs across these families, including redaction API
Problem Details. No prefix-wide rewrite is performed. Ordinary engine JSON and
retained evidence do not gain alias fields; only configured API serialization does.
Viewer displays prefer a supplied canonical code and retain the original as a
tooltip. Unknown findings keep their original identifier. This is an identity
migration, not a change to compliance, parsing or redaction guarantees.

The catalog also includes 13 attachment IDs (PXAEINVATTACH), six layout IDs
(PXALAYOUT) and 13 pipeline IDs (PXAPDFPIPELINE). Attachment findings reuse the
existing compliance-conversion projection. Layout ImportDiagnosticDto values gain
an alias only during API serialization, including nested design/workbook results;
their ordinary persisted representation and normalization rules remain unchanged.
Unknown import/provider diagnostics receive no alias. API JSON exports can therefore
include this derived field while retained engine JSON does not; it is not a new
canonical document property or an input authority.

Pipeline IDs are registered for consumers of structured diagnostics and shared
Problem Details, but pipeline exceptions themselves are not renamed, reclassified
or newly exposed. This catalog addition does not create a public pipeline endpoint
or alter global exception handling. Original exception text and retained job data
remain unchanged.

The three Viewer redaction errors (PXAPDFREDACTVIEWER001 through 003) now add
canonicalCode to their existing anonymous JSON responses. Their 409/503 status,
error wording and preflight/verification payloads are unchanged; failures still
return no PDF. The browser's RedactionBlockedError preserves its legacy code and
optionally exposes canonicalCode without changing refusal handling.

ChartRenderResult retains its original Diagnostic string and ordinary engine JSON.
Configured API serialization adds canonicalCode only for strings beginning with
the exact reviewed prefixes `PXACHART001: ` or `PXACHART002: `. Unknown codes,
embedded mentions and successful results gain no alias. This does not introduce
a chart diagnostic endpoint or claim that existing exports surface renderer
diagnostics where they previously did not.

## Authentication, Audit And Usage Diagnostics

The remaining symbolic result identifiers now have exact aliases:
PXA_AUTHENTICATION_REQUIRED, PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE,
PXA_USAGE_INVALID, PXA_USAGE_ALREADY_RECORDED and PXA_USAGE_RECORDED.
Product-access middleware retains its 401 status and authoritative legacy code;
the response adds canonicalCode without changing gate ordering or permissions.

Problem Details also derives canonicalTitle when its unchanged title is itself a
reviewed diagnostic identifier. Audit-export edition denial keeps its historical
PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE title and PXAAPI002 code, adding separate
PVAD_AUDIT_EXPORT_REQUIRES_ENTERPRISE and PVADAPI002 aliases. Completion removes a
stale canonicalTitle if the final title is unknown or ordinary prose. Existing
explicit problem type URLs remain unchanged. Generated types follow the
[route-aware identity contract](pvad-api-problem-and-health-identities.md), with
separate `canonicalType` and `legacyType` references.

PvadUsageResult receives canonicalCode only under configured API serialization.
Recorded/duplicate flags, event ID, reason and legacy code remain authoritative.
The usage service, database events, uniqueness rules, quantities and concurrency
handling are unchanged. This projection does not create a new usage endpoint or
rewrite retained results; normal service serialization remains unchanged.

The production inventory guard classifies every current numeric branded diagnostic
and symbolic PXA identifier in src and WebApi. Six explicit password-configuration
and telemetry-retention names are configuration/constants, not diagnostics. Future
unclassified identifiers fail the guard and require review; they are never renamed
by a catch-all prefix rule. Historical CANMIG IDs and external validator IDs remain
unchanged, as do stored evidence and audit/job history.

The compiler warning PXA0001 remains an explicit legacy contract for the existing
Obsolete attribute, pragma directives and customer NoWarn configuration. No fictitious
PVAD0001 HTTP alias is emitted: a C# compiler warning supports only one DiagnosticId,
so changing it requires a separately announced compiler-contract migration.

Worker diagnostic DTOs derive `canonicalCode` from the authoritative legacy `code`;
legacy payloads remain readable and an incoming alias cannot override that code.
Account/Admin license-validation responses, entitlement decisions and Designer
handoff results expose the same additive field. Designer feature catalog entries
use `canonicalDecisionCode` alongside their existing `decisionCode`.

Designer feature/product denial middleware uses shared Problem Details completion
without changing permission checks or status decisions. The workspace concurrency
response also adds an alias while retaining its current revision document. License
envelopes and signatures are not part of these response projections and remain
byte-for-byte untouched. Browser clients may continue branching on legacy IDs.

MVC completes diagnostics in its always-run result filter after caller extensions
have been applied. The earlier factory customization must not pre-populate `code`:
ControllerBase.Problem adds caller extension keys afterwards, which can otherwise
throw on duplicates and turn an expected access denial into a 500 response.
Exception-handler responses still use shared completion. Completion removes a stale
canonical alias when the final authoritative code is not in the reviewed catalog.

## Native Project And Template Diagnostics

`PXADESIGNER001` (template revision conflict), `PXAPROJECT001` through `016`,
and the nine current named project validation/upgrade diagnostics have explicit
PVAD aliases. Template conflict JSON and project rejection Problem Details add
`canonicalCode` without changing the original code, revision, HTTP status or audit ID.

Project-open compatibility diagnostics use a dedicated API response projection:
each diagnostic keeps code, severity, message and source, and adds canonicalCode.
The embedded manifest still uses the archive diagnostic model without that field.
Neither native manifests nor stored version-assessment/upgrade metadata acquire
new serialized properties as a side effect of this alias stage. Existing hashes,
legacy fixture bytes and archive schema versions are not rewritten.

Typed WebApi consumers should use `PvadProjectDiagnosticResponse` for the
compatibility diagnostics collection. The JSON change is additive. Consumers of
the manifest continue using `PvadProjectDiagnostic`. Unknown provider diagnostics
remain intact and receive no invented alias.

## Migration Diagnostics And Artifact Identities

`PXAMIG001` through `PXAMIG011` retain their existing code values and gain
corresponding `PVADMIG...` aliases through Problem Details and migration diagnostic
response projections. CANMIG provider diagnostics remain unchanged and have no
invented aliases. Input limits, provider keys, capabilities and permission checks
are unaffected.

Catalog artifact descriptors and converted artifacts add `canonicalId`,
`canonicalMediaType` and `canonicalFileExtension` for three reviewed identities:

| Legacy artifact | Canonical artifact | Canonical media type | Canonical extension |
| --- | --- | --- | --- |
| pxa-csharp | pvad-csharp | text/x-csharp | .cs |
| pxa-spreadsheet-csharp | pvad-spreadsheet-csharp | text/x-csharp | .cs |
| pxa-viewer-package | pvad-viewer-package | application/vnd.pvad.viewer+json | .pvadviewer.json |

Current `/api/pvad/v1/migrations` responses write the canonical tuple in the
primary `id`, `mediaType` and `fileExtension` fields. The canonical metadata
fields remain available and equal the primary tuple for these known artifacts.
Legacy `/api/pxa/v1/migrations` adapters retain the old primary tuple, including
when the middleware rewrites the request path. Their canonical metadata still
identifies the current format. Null canonical fields do not make a neutral
artifact unsupported.
Aliases are derived only from a complete known legacy or canonical tuple; mixed
tuples, unknown provider artifacts and neutral design JSON are not reclassified.
Each conversion still returns the same number of artifacts, not duplicate files.

Current Viewer package output writes `kind: pvad.pdf-viewer-package`; legacy
routes retain `kind: pxa.pdf-viewer-package`. Both include
`canonicalKind: pvad.pdf-viewer-package`. Existing stored packages are not rewritten.
Source/normalized/generated checksums retain their existing meanings; these are
not signatures over the entire newly extended package. This identity projection
does not add a standalone Viewer-package upload workflow, new provider capabilities
or claim that Designer consumers import a new file format. The current Designer
flows continue using the neutral artifact content/design document as before.
See [migration artifact identities](pvad-migration-artifact-identities.md) for
the writer/reader boundary, downloads and validation limits.

## E-Invoice And Trust Report Projections

The catalog additionally covers 28 E-Invoice API errors, 56 E-Invoice engine codes,
93 PDF-Trust engine codes and the code-builder no-pages diagnostic. Exact reviewed
IDs are listed in PvadDiagnosticAliases; unallocated numbers receive no alias.

For the explicitly supported diagnostic types, the API JSON contracts add
a derived canonicalCode to known diagnostic objects, including nested reports.
The same modifier is configured for MVC and HTTP JSON/OpenAPI options. Ordinary
engine serialization outside the API remains unchanged, so persisted DiagnosticsJson,
signed audit strings and existing checksums do not acquire extra properties.
API-projected JSON is not byte-identical to the underlying retained evidence.

The old code, severity, source location, localization key and message remain
authoritative. Official OriginalFinding identifiers, XPath locations, validator
messages and rule expressions are retained verbatim. An incoming alias cannot
replace the code; unknown and official third-party IDs are not renamed.
This does not change conformance decisions, certificate trust or signature validity.

## Security Request Headers

| Canonical | Legacy alias | Purpose |
| --- | --- | --- |
| `X-PVAD-Application` | `X-PXA-Application` | Select the Designer or Account/Admin browser authentication context |
| `X-PVAD-CSRF` | `X-PXA-CSRF` | ASP.NET Core antiforgery request token |
| `X-PVAD-API-Key` | `X-PXA-API-Key` | Service-account API key |

Legacy names are accepted for the API compatibility major and normalized before
authentication, Designer gates and CSRF validation. All downstream consumers use
the canonical name. Matching aliases collapse to one value; conflicting aliases,
multiple values, comma-combined values and explicitly blank values return HTTP 400
Problem Details with the stable `PXAAPI005` diagnostic. Error bodies never echo
credentials or token values. Application names are `designer`, `account` or `admin`
and compare case-insensitively; token and key values compare exactly.

Header renaming does not confer permissions, add entitlements or bypass CSRF.
Both names run the same real antiforgery and API-key validation. Requests without
an Application header retain existing default authentication behavior. Browser
clients should fetch a CSRF token from `/api/pvad/v1/auth/csrf` before mutations.
Designer, Account and Admin write canonical headers; the Designer fetch adapter
also normalizes explicit headers supplied by mixed-version callers.

## Response Headers

`X-PVAD-Template-Revision`, `X-PVAD-PAdES-Profile`, `X-PVAD-PDF-Timestamp` and
`X-PVAD-Operator` are emitted alongside their `X-PXA-*` equivalents with identical
values for one compatibility major. ETag and Content-Disposition stay neutral.
The revision, PAdES and timestamp pairs are exposed by the existing CORS policy;
the internal Operator identity is not newly exposed to browser JavaScript.

The Operator pseudonymous value remains stable; changing the header name does not
rename retained accounts in an external operator application. Issued API-key
`pxa_` secrets remain valid; newly issued keys use `pvad_`. Mail and revocation-cache
readers accept old Data Protection purposes while writing the new PVAD purposes.
Session and CSRF cookies now write PVAD names with bounded legacy readers;
authentication-scheme names remain stable to preserve old ticket protection.
See [identity and protected-data compatibility](pvad-identity-compatibility.md)
for the mixed-version deployment and rollback boundaries.

PDF/A conversion, irreversible redaction, Viewer redaction and E-Invoice attachment
now write both `PVAD-*` and `PXA-*` response headers with identical values. The
shared response-header catalog exposes both generations through CORS, including
hashes, compliance profile, redaction provenance/location, invoice attachment and
validation metadata. It does not expose API-key, CSRF, cookie or Operator headers.
The browser's Viewer and E-Invoice readers prefer PVAD, accept legacy-only servers
and reject conflicting evidence aliases without logging their values.

SMTP messages add `X-PVAD-Outbox-Id` while retaining `X-PXA-Outbox-Id` with the
same outbox UUID. Message IDs, recipient routing and persisted outbox records do
not change. The paired headers do not imply a new delivery or retry guarantee.

The operator reverse proxy strips both generations of application/API-key headers
at its internal authorization boundary. Signed webhook contracts remain separate
work; no signed audit/evidence bytes or retained records are rewritten here.

## Historical Migration Diagnostics

`CANMIG` is a stable migration-diagnostic contract, not the current product brand.
Do not replace this prefix with PVAD. The 432 production IDs present at rename
baseline `fce423cf8c5f5e71eff6178f712fe4e5eb5af69a` are recorded in
`product-metadata/legacy-migration-diagnostics.json`. A CI source-contract test
requires them to remain available. New codes may be added without renaming old
ones; any future replacement needs a separately approved mapping and client tests.

## Website Configuration

Website links accept `VITE_PVAD_COMPANY_URL`, `VITE_PVAD_DOCUMENTATION_URL`,
`VITE_PVAD_DEMO_URL`, `VITE_PVAD_DESIGNER_URL`, `VITE_PVAD_ACCOUNT_URL` and
`VITE_PVAD_OPERATOR_URL`. Their `VITE_PXA_*` spellings remain aliases. Both may be
set only to identical values; conflicts fail with key names, not values. Nonempty
values must be absolute HTTP(S) base URLs without credentials, query or fragment.
An empty value uses the existing default. Overrides work in development and
production; without overrides, local ports and production domains are unchanged.

Designer compile-time constants now use `__PVAD_DOCUMENTATION_URL__` and
`__PVAD_VISUAL_PARITY_ENABLED__`. The neutral `VITE_ENABLE_VISUAL_PARITY` flag is
unchanged; production exposure of the internal parity route remains opt-in.

## Schema And MCP Identities

The unversioned branded controller aliases now use `/api/pvad/` for document,
export, migration, PDF viewer, spreadsheet, system and template operations.
Their historical `/api/pxa/` counterparts delegate through the same middleware
with a successor link. Neutral routes remain available. A route can still be
deprecated independently of branding, notably the original migration API.
OpenAPI advertises canonical branded routes only; the committed snapshot is
generated from the running API contract, not edited by string replacement.

New clients use `pvad://` MCP resources and the canonical workbook schema at
`https://pvade.com/schemas/pvad-workbook.schema.json`. Historical
`pxa://` resources remain readable. `docs/schema/schema-aliases.json` maps old
schema IDs to the bundled canonical schemas without a network lookup. All
maintained schema IDs use the selected domain; old-domain IDs remain
explicit offline aliases, not remote HTTP redirects. No fixture or published
evidence is rewritten by this identifier change. Workbook validation constraints
are unchanged. Native writers now emit PVADP/PVADS while
retaining legacy-envelope validation and original-preserving Save project downloads;
see [native project formats](native-designer-project-formats.md). Legacy and
canonical manifest schemas remain separate because their envelope constraints differ.
The combined structural reader schema and implemented-client inventory are described
in [schema and client contracts](pvad-schema-and-client-contracts.md). Native export
responses now explicitly declare binary schemas and Content-Disposition in OpenAPI.

MCP examples use PVAD configuration keys and commands. New delegation writes use
the canonical PVAD tuple and known scope aliases; legacy readers remain available.
Persistent OAuth client/resource identifiers retain their reviewed compatibility
values. See [identity compatibility](pvad-identity-compatibility.md) and
[MCP client setup](mcp-client-setup.md).

### Verification Limits

Tests cover alias normalization, conflicting and duplicate headers, real
antiforgery validation, real API-key hashing/revocation/tenant claims using an
isolated EF in-memory store, anonymous protected endpoints and response aliases.
They do not replace PostgreSQL persistence, production proxy, deployed-host or
existing-cookie cutover verification. No production database or license was changed.
