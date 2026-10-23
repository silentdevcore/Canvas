# PVAD Technical Rename Migration Notes

Implementation in progress. This is not a released upgrade announcement.

PVAD is the technical identity. PVADE is the public platform and Designer brand.
PVADP and PVADS identify the PDF and Spreadsheet products respectively.

## Compose Defaults And Compatibility

Fresh defaults now use PVAD for service/resource keys, secret mounts and local
database credentials. WebApi and EF fallbacks target `pvad`, not an old `pxa`
database. Existing installations require the explicit root/API legacy overlays
and verified external resource IDs. They preserve old data and optional DNS
aliases without creating a second database or changing stored key bytes.
Configuration contracts pass; no stack was restarted. Keep the existing project
name and stop old writers before any approved rollout. Existing host-run APIs
must explicitly select their old connection, and custom secret-mount consumers
must be updated. See the [binding and rollback requirements](pvad-infrastructure-mapping.md).

## Existing Data Protection Directory

Before starting a renamed deployment, explicitly configure
`DataProtection:KeysDirectory` (environment form `DataProtection__KeysDirectory`)
to the existing protected keyring mount. The fallback is `App_Data/data-protection-keys`
relative to the application's content root. A renamed installation directory can
therefore point that fallback at a new empty location even though the retained
`PowerDoxAutomation` application discriminator is unchanged.

Preserve the existing mount, permissions and backup; do not delete, regenerate or
blindly rename production keys. Validate recovery with the deployment owner before
cutover. Local restart tests use isolated synthetic keyrings and do not approve
access to real customer key material.

## Retained Files And Worker Paths

For an existing installation, pin `Storage:RootPath` and
`PdfViewer:AnnotationStoragePath` to the original object and annotation mounts.
Their environment forms are `Storage__RootPath` and
`PdfViewer__AnnotationStoragePath`. Relative/default paths resolve against the
application content root; renaming that directory does not discover or move
old files automatically. Keep stored object keys, annotation document IDs and
customer filenames/content unchanged. Do not run missing-object reconciliation
against an accidentally empty replacement mount.

The API Compose definition deliberately retains `pxa-api-data` at `/app/data`,
and its configured objects, annotation store, keyring and PDF Trust cache all
resolve inside that mount. Preserve the explicit installed Compose project name
as well: a different project can select a different physical volume despite an
unchanged volume key. Existing licensing secret names are also retained.

Code execution uses the packaged `code-worker` directory with
`PVAD.CodeWorker.dll`; OCR uses `PVAD.FileImporter.ImageOcr.Worker.dll` inside
`ocr-worker`. Update explicit worker paths together with their deployed binaries.
These are executable packages, not customer-data directories or legacy worker
fallbacks. Code execution still requires its configured enablement/hardening;
the rename does not enable it.

## .NET Projects And Namespaces

All 115 first-party project files now use the PVAD prefix. Update project paths,
assembly references and C# namespace imports, for example:

```csharp
using PVAD.Core.Contracts;
using PVAD.Generator;
```

The main solution is PVAD.sln; the existing focused solution is PVAD.slnx.
The PDF engine remains PVAD.Pdf, the facade remains PVAD.Generator and migration
providers retain their domain/kind/provider suffixes under PVAD.Migration.
The OCR worker and code worker retain their architectural responsibilities.

This is a source and assembly compatibility change requiring a Major release.
Existing applications must recompile against the renamed libraries. No binary
type-forwarding compatibility is promised by this first implementation stage.

## Transition Status

Projects, namespaces and owned symbols now use PVAD/Pvad. The Designer is in
`pvad-designer`, websites are in `websites/PVAD.<Site>`, and MCP is in
`tools/PVAD.Mcp`. npm packages use pvad-prefixed technical names; public platform
headers use PVADE. A central brand definition supplies the confirmed identities.

Session names and five claim families now use PVAD with compatibility readers;
mail/cache protection contexts retain legacy readers. Remaining diagnostics,
retained-job encryption and production cutover still require dedicated work.
Their old names are not evidence that the complete rename is delivered.
Native writers now use PVADP/PVADS and `minimumPvadVersion`;
legacy envelopes and `minimumPxaVersion` remain readable. Conflicting version
aliases are rejected. See [native project compatibility](native-designer-project-formats.md).

## Coordinated Upgrade Checklist

| Boundary | Required action and evidence |
| --- | --- |
| Packages and generated code | Recompile PVAD references and deploy API/code worker together; see [code compatibility](pvad-code-generation-compatibility.md) |
| HTTP and client contracts | Prefer canonical routes; keep legacy adapters for the documented window; see [HTTP](pvad-http-compatibility.md) and [schemas](pvad-schema-and-client-contracts.md) |
| Configuration and URLs | Resolve aliases before startup, install Nginx hook/template together, verify actual host allowlists; see [hosting](pvad-frontend-host-validation.md) |
| Sessions and protected data | Keep keyrings and legacy readers, preserve host-only boundaries; see [identity](pvad-identity-compatibility.md) |
| Native project files | Keep originals and explicit legacy readers; new writers do not imply old-binary forward compatibility |
| Jobs and licenses | Stop old workers, preserve leases/history and use explicit license successors; see [license/job notes](pvad-license-and-job-compatibility.md) |
| Telemetry and retained names | Accept both generations where documented; do not rename persisted dashboards/storage; see [observability](pvad-observability-identity.md) |
| Data and rollback | Obtain verified backups and owner approval before changes; follow the [rollback runbook](pvad-rename-rollback.md) |

The [current inventory and exception rules](pvad-rename-exceptions.md) distinguish
active source from compatibility and immutable evidence. The
[test report](../checklists/PVAD.Rename-Test-Report.md) records what has actually
run. Deployment authorization, registry/domain ownership, real customer rollout
and production restoration remain open prerequisites, not completed work.

## Release Tooling And Build Information

Use `tools/versioning/pvad-version.mjs` and
`tools/versioning/pvad-build-consistency.mjs`. Release history is now read from
`product-metadata/pvad-releases.json`; existing release entries are unchanged.
New frontend builds and API artifact packaging use `pvad-build-info.json`, whose
technical product identity is `PVAD`. Designer, Admin and Documentation read the
same release source. CI and deployment consumers use the new paths.

VERSION remains 1.1.0. No release is prepared, tagged, deployed or published by
this rename. Legacy build environment variables and Docker arguments are retained
as validated compatibility inputs alongside canonical PVAD names. Repository skill instructions
now use `pvad-release-author` and the renamed versioning commands.

## API Prefix Compatibility

The canonical prefix is `/api/pvad/v1`. First-party frontends, MCP API clients,
the checked-in OpenAPI schema and generated OpenAPI paths use this prefix.
Operation identifiers are retained; this is a product-name migration, not a new
API contract version.

Requests to `/api/pxa/v1` are internally mapped before routing to the same
controller and authorization, feature-gate, entitlement, rate-limit and audit
pipeline. The adapter preserves method, body, query and authentication headers;
it does not redirect clients or replay requests. Legacy responses include
`Deprecation: true` and a `Link` to their canonical successor path.
Neutral routes such as `/api/export` remain unchanged.

The legacy branded prefix is retained throughout the first PVAD product major
release (2.x); its planned removal version is 3.0.0. A dated Sunset header must
only be announced when an actual retirement date has been approved. No date is
invented by this source migration. Cookie identities, diagnostic IDs and signed
contracts remain subject to their separate compatibility work.

Cookies and protected payloads now have explicit legacy readers and canonical
PVAD writes; authentication schemes, keyring identity and EF migration IDs retain
their values. Legacy PXAP/PXAS envelopes retain their validated reader;
new saves produce a separate PVADP/PVADS download after an explicit legacy review.
Remaining compatibility gates must finish before a complete renamed release is published.

No production resource, domain, registry, signing key, database name or volume
has been migrated by this source-level change.

## Configuration Transition In Progress

The following environment keys now accept both canonical PVAD names and their
PXA aliases:

- PVAD_BUILD_COMMIT and PVAD_BUILD_TIME in frontend builds, packaged build-info
  generation and WebApi version metadata.
- VITE_PVAD_DOCUMENTATION_URL in the Designer build configuration.
- PVAD_DATABASE_CONNECTION in the EF design-time context factory only.
- PVAD_BOOTSTRAP_ADMIN_EMAIL and PVAD_BOOTSTRAP_ADMIN_PASSWORD in development
  identity bootstrap.
- PVAD_API_URL, PVAD_API_KEY and the PVAD_MCP_* settings consumed by MCP.

Identical duplicate values are accepted; contradictory values fail with key
names only. No secret values appear in the conflict message. Explicit empty
values are not silently replaced by legacy secrets. Bootstrap remains restricted
to development. The existing physical database and user are not renamed.

The .NET sections now use `Pvad:EInvoices:*` and
`ConnectionStrings:PvadDatabase`. Old `Pxa:*` and `PxaDatabase` aliases are
resolved once at startup, before options are consumed. Normal configuration
provider precedence is retained: an environment setting may override a JSON
default regardless of which spelling it uses. Conflicting aliases in the same
provider fail without exposing values. Restart to apply changed aliases; runtime
hot reload is not part of this transition.

The API Compose file uses canonical PVAD variables. Run it through
`node tools/configuration/pvad-compose.mjs` to accept old PXA environment names
and check conflicts. Use an explicit `--env-file` for files outside the working
directory. Only literal dotenv values are supported by this launcher; unresolved
`${...}` interpolation is rejected. Raw Docker Compose bypasses this validation.

WebApi and WebhookRelay build arguments are PVAD_VERSION, PVAD_COMMIT and
PVAD_BUILD_TIME; legacy PXA arguments remain fallback inputs with conflict guards.
The publish command now supplies PvadBuildMetadata, matching Directory.Build.props.
The fallback mechanism follows the [Dockerfile environment replacement rules](https://docs.docker.com/reference/dockerfile/#environment-replacement).
Full production image builds have not yet been validated.

Other frontend/tool variables, Compose service names and telemetry identities
remain pending. Existing physical database/user names, Compose volume keys and
license-secret mount names remain unchanged. Do not change the Compose project
name during this transition, or new project-scoped volumes would be selected.
OAuth scopes, claim names and cryptographic purposes are not environment aliases
and retain their existing compatibility contracts.

## Telemetry Transition

New WebApi instruments and spans use `pvad.*`; MCP Prometheus metrics use
`pvad_mcp_*`. Default resource identity is service `pvad-webapi`, namespace `PVAD`.
Configured customer resource names are not forcibly rewritten. External metric
consumers must update their queries when upgrading; measurements are not emitted
twice under both names.

Managed Grafana queries and alert expressions use dual-name selectors such as
`{__name__=~"(pvad|pxa)_jobs_processed_total"}`. Managed log filters accept both
`pvad-webapi` and `pxa-webapi`, so existing retained data remains queryable.
No stored logs, traces or metrics have been rewritten or deleted.

Grafana dashboard/datasource UIDs, provisioning filenames, physical service and
dependency identifiers, existing runbook IDs and signed webhook headers remain
transition exceptions until their dedicated compatibility migration. Visible
dashboard titles use PVADE. The collector privacy pipeline is prefix-neutral;
its protections remain unchanged.

Generate a reproducible parser contract for all managed dashboard queries:

```bash
node tools/configuration/pvad-telemetry-contract.mjs artifacts/rename-pvad/observability/dashboard-queries.json
```

Validate that JSON file with `promtool check rules`, and run `promtool test rules`
against `deploy/observability/prometheus/tests/pvad-alerts.test.yml`. The fixture
keeps legacy scenarios and adds canonical-prefix scenarios. A deployed live
collector/Grafana cutover and real incident/recovery exercises are still pending.

## Tool And CI Environment Names

CI and fuzz workflows now publish canonical PVAD environment names. JavaScript
tools read them through the shared resolver; .NET test projects use a linked
test-only resolver that does not add a production dependency. Examples include
PVAD_RUN_POSTGRES_TESTS, PVAD_PDFA_ARTIFACT_DIR,
PVAD_SPREADSHEET_FIDELITY_ARTIFACTS and PVAD_VISUAL_PARITY_FIXTURE.

PDF/A, PDF/UA, fuzzing, redaction-recovery and Legal backup/recovery shell entry
points accept the corresponding legacy PXA names. Their POSIX adapter rejects
conflicts without printing values and exports canonical names only to child
processes. It uses no eval and preserves quoted/multiline values literally.
Legacy aliases are removed from the child environment after normalization.

Configurable AES-256 interop passwords accept renamed keys, but existing synthetic
passwords, reference PDFs, signed evidence and their hashes are not rewritten.
Node syntax checks and validator-wrapper tests are regression checks, not a new
PDF/A or PDF/UA conformance certification. Real PostgreSQL-backed checks still
require the explicit test opt-in and an isolated test database.

## References

- [Rename plan](../checklists/Rename-PXA-To-PVAD.md)
- [Repository audit](../checklists/PVAD.Rename-Audit.md)
## Workbook Browser Storage

New local workbook drafts are scoped to the authenticated user and active
organization using `pvad-spreadsheet:v1:<encoded-user>:<encoded-org>`.
The Designer resets workbook memory before loading another scope. Signing out
does not delete scoped drafts; another account does not load them. Clearing site
data removes them. Browser storage is not encrypted storage or server authorization.

Unscoped `pxa-spreadsheet` and `canvas-spreadsheet` drafts are not adopted
automatically, because their owner cannot be established. They remain untouched
by workbook startup; the existing sign-out/organization-switch cleanup can remove
them. In the Spreadsheet editor, select "Find old local drafts" to list recovery
downloads. Download only your own workbook and open it through the existing Import
workflow. The export preserves the original storage entry and leaves the current
workbook unchanged. Malformed or oversized recovery data produces an error instead
of a download. This is not automatic ownership assignment.

## Temporary Document Transfers

New Migration-to-Designer and PDF-viewer transfers use
`pvad_migration_designer_handoff` and `pvad_pdf_viewer_handoff`. Each versioned
envelope binds the payload to the authenticated user and active organization,
expires after five minutes and is removed when consumed. Same-tab memory fallback
supports storage-quota failures and is discarded on owner changes or cleanup.

Old unassigned transfer payloads are not trusted or automatically migrated. If
an older tab contains a pending transfer, repeat the conversion or preview in the
updated Designer. Browser envelope checks are workflow isolation, not cryptographic access
control; server authorization still applies to all protected resources.

## Sign-In And Browser Telemetry

New sign-ins store PKCE state under `pvad.designer.auth-transaction`. An unfinished
`pxa.designer.auth-transaction` may be consumed once only when no canonical
transaction exists. Both copies are consumed before exchange; the original state,
verifier and ten-minute validity remain mandatory. Malformed, expired, future-dated
or mismatched state never reaches the exchange API. Callback parameters are removed
from history immediately. Return destinations must remain on the Designer origin.

The browser telemetry bridge is `__pvadBrowserTelemetry`; the previous name remains
an alias to the same collector for compatibility. Internal API/Account events use
`pvad:*`. Export preferences write `pvad_export_format` and migrate an old preference
without overwriting an existing canonical value. Authentication cookies and runtime
deployment rollout verification remain separate acceptance gates.

Security-header migration is now implemented with server-side compatibility:
clients write `X-PVAD-Application`, `X-PVAD-CSRF` and `X-PVAD-API-Key`; the previous
spellings normalize before authentication. See
[HTTP and website compatibility](pvad-http-compatibility.md) for conflict rules,
response aliases, website environment inputs and preserved diagnostic contracts.

See the [browser identifier audit](../checklists/PVAD.Browser-Identifier-Audit.md)
for the complete distinction between canonical writes and retained legacy readers.

New API keys now use `pvad_`, with unchanged validation for existing `pxa_` keys.
Mail outbox and PDF revocation-cache readers handle both protection generations;
new writes use PVAD purposes without changing the keyring application identity.
See [license and job compatibility](pvad-license-and-job-compatibility.md) and
[identity compatibility](pvad-identity-compatibility.md) before deploying or
rolling back mixed-version consumers. Session/CSRF cookie migration is implemented;
live deployment verification and the OAuth grant/client migration remain open.
Claims now write `pvad_*` with conflict-checked historical readers, and internal MCP
delegation uses canonical identities with two explicit legacy readers. Existing
OAuth client IDs, public scope requests and stored grants remain unchanged.

## Designer Runtime Configuration

Load a deployment-owned script before the Designer application bundle:

```html
<script>
  window.__PVAD_CONFIG__ = {
    accountUrl: 'https://accounts.example/customer/'
  };
</script>
```

`accountUrl` must be an absolute HTTP(S) base URL without credentials, query or
fragment. A missing trailing slash is normalized; a customer base path is retained.
HTTP remains available for local and customer-managed deployments; use HTTPS in
production. Do not put secrets in browser configuration.

An old `__PXA_CONFIG__.accountUrl` is accepted when the new field is absent. If
both exist they must normalize to the same URL; conflicting values fail closed.
Explicit blank values preserve default-host behavior but conflict with a nonblank
alias. Invalid object/field types are rejected. Unknown future fields are ignored.
Error messages contain key names only, never URL values.

Configuration is checked before authentication bootstrap. Invalid settings show
a retryable configuration state rather than issuing authentication requests.
The existing localhost and production Account defaults remain unchanged when no
Account URL is supplied. No deployment host, cookie, CORS or server-side origin
allowlist is changed by this browser resolver; configure those separately.

## Code Element Markers

New C# Model and C# PDF source uses `// pvad-element-id: <id>`. Both the API
source-map reader and the worker readers accept historical `pxa-element-id:`
comments, preserving IDs in existing drafts. Element IDs themselves are unchanged.
The shared reader retains the 200-character ID bound and case-insensitive marker
matching. Empty and oversized IDs are ignored as before.

Deploy the matching PVAD API and code worker together: historical worker binaries
do not understand the new marker. This does not certify mixed-version execution.

## Migration Code Result Compatibility

Migration service callers use `MigrationResult.PvadCode`. The read-only obsolete
`PxaCode` property remains a compatibility alias through 2.x; removal is planned
for 3.0.0. Positional record construction remains unchanged, but source callers
using the old named constructor argument must change it to `PvadCode`.

The compatibility conversion HTTP response adds `pvadCode` and retains `pxaCode`
with identical content. Unified v1 migration responses still use their neutral
artifact `content` field; no artifact IDs or provider diagnostic IDs change.
The Designer consumes artifact content under the canonical internal name.

New background code-migration result files contain both `PvadCode` and `PxaCode`
(Pascal case, as in historical job-result JSON). Existing stored objects are not
rewritten. Consumers reading historical results must still accept `PxaCode`;
new consumers should prefer `PvadCode`. This is an additive output transition,
not a migration of retained object bytes or a change to job-result download APIs.

## Account, Notifications And Export Names

Current Account authentication, verification and Designer-access messages use
PVADE in all six supported languages. Access decisions continue to recognize the
existing diagnostic identifiers; display branding does not change authorization.

New Legal publication notifications refer to PVADE Account. Previously stored
notification bodies and published release titles retain their original wording,
remain readable and can still be reopened through the current notification panel.
No bulk rewrite of retained notification or legal evidence data is required.

New Admin audit exports use `pvad-audit-...` filenames and Legal acceptance
exports use `pvad-legal-acceptance-...`. Frontend fallbacks use the same technical
prefix. Server-provided customer filenames are preserved rather than rebranded.
New PDF Creator metadata uses `PVAD PDF Renderer`; customer title, author,
subject and keyword values are unchanged.

## XML Export Envelope

The major rename changes the default XML export root from `PxaDocument` to
`PvadDocument`. Child elements and the existing XML format version remain the
same; this is not a new full-fidelity template serialization format. Update XPath,
XSD root declarations and external import consumers before adopting the new
export. The XML is not a replacement for native PVADP/PVADS packages.

Embedded .NET integrations that temporarily require the old root can call:

```csharp
var exporter = new PVAD.Infrastructure.Converters.XmlDocumentExporter();
byte[] currentXml = exporter.Export(design);
byte[] legacyXml = exporter.ExportLegacy(design);
```

The HTTP XML exporter uses the canonical root, including through compatibility
API routes. A legacy URL is not a request for the old XML envelope. Applications
requiring the historical envelope must explicitly adapt their integration; no
new HTTP format identifier or silent root auto-detection is introduced.

Document names, text, IDs and user metadata are preserved in both modes. Existing
export files are not modified. Release publication remains a separate approval.
