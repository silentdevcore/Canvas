# Rename PXA To PVAD

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Status And Naming Authority

**Source rename: provisionally complete for the reviewed scope. Broad validation:
partly passed; the API suite remains blocked by local database authentication.**
This is not release readiness or proof that all features work on the final source
revision. Further changes start only from a concrete naming finding or the later
validation results, not from a target percentage.

Current scope includes the local build/test follow-up recorded below. Production
deployment, live multi-host/browser validation and functional expansion remain
deferred. Earlier entries retain their point-in-time scopes and counts; they do
not establish validation of every later change. The earlier request included focused Compose
configuration verification, recorded below. Earlier source-only batches rename ten
Observability files and their references, canonical mail-template names with
legacy aliases, and internal Java/Maven/JAR/local-image identities. Results and
index counts below refer to earlier checkpoints, not validation of these batches.
No additional completed-test percentage is claimed.

Production-link follow-up (2026-10-23): shared defaults use `pvade.com` and the
selected docs/demos/designer/account/operator subdomains. The Designer Account
fallback matches. Local ports, current/legacy configuration overrides and URL
validation remain unchanged. Old origins are not implicitly trusted in the
frontend return allowlist. Backend authentication allowlists, mail sender and
cookie scope are not changed; deployment must configure approved origins before
traffic cutover. Targets remain unverified until DNS/TLS/routing tests pass.
205 Node contracts, all Website tests/builds, Account typecheck and Designer
typecheck/729 tests/build pass. Patch fragment: `pvad-production-link-defaults`.
VERSION stays 1.1.0; the previously recorded API-test blocker remains open.

Backend-origin follow-up (2026-10-23): registration return defaults now match the
five canonical production product origins. Designer authorization permits the
canonical Designer origin plus the unchanged two local origins. Legacy domains
are not automatically trusted; reviewed deployments can explicitly override the
arrays. Admin/Operator destinations and wildcard domains are excluded. Mail,
CORS, cookies and per-client OAuth registrations are unchanged. 22 isolated .NET
security/configuration tests and 205 Node contracts pass. These tests do not start
the API or use PostgreSQL; the broad API integration blocker remains open.
Override documentation explains indexed array merging. Patch fragment:
`pvad-backend-origin-defaults`; VERSION stays 1.1.0. No deployment occurred.

Schema-domain follow-up (2026-10-23): URL/auth defaults were committed separately
as `fc223d97`. Thirteen maintained schema IDs now use the selected domain and
retain explicit old-ID aliases in the fourteen-entry offline registry. The legacy
native manifest remains a distinct entry; all three legacy schema files and the
native fixtures keep their bytes. The resolver now supports alias definition
fragments, not only root aliases. MCP packaging follows the registry and copies
schema definitions only, not metadata documents or review records. Constraint-hash
tests verify the thirteen ID-only migrations. 207 Node contracts, 27 MCP tests,
MCP typecheck/build/distribution smoke, Designer typecheck/729 tests/build and
Documentation 58 tests/build pass. Patch fragment: `pvad-schema-domain-aliases`.
No schema hosting, live authentication, database change or API readiness claim;
VERSION remains 1.1.0 and prior integration blockers remain open.

Container/workflow preparation (2026-10-23): schema work was committed separately
as `6875a258`. An offline generator now creates paired release/deployment YAML
candidates in ignored artifacts. New publication, pull and metadata-check names
are coordinated in the candidate; historical inspection selects its exact image
family explicitly. Active workflow YAML, concurrency groups, protected environment
bindings, action pins and permissions remain unchanged. The evidence verifier
accepts either complete family from the repository-owner GHCR namespace and
rejects mixed, duplicate, mutable-tag or foreign-publisher inputs. New evidence
records container identity separately from archive identity, without relabeling
historical records. 212 Node contracts and version/fragment checks pass. No Docker,
registry, GitHub or deployment operation was executed. Publisher ownership and
immutable-version image mapping remain external gates. Patch fragment:
`pvad-container-workflow-preparation`; VERSION remains 1.1.0.

Grafana preparation (2026-10-24): the remaining dashboard provider and folder
labels have a tested offline PVAD candidate, original provisioning copy and
source-hash report. Active provisioning and installed data remain untouched.
Unexpected configuration shapes fail preparation. The
[cutover guide](../docs/pvad-grafana-provider-cutover.md) requires folder/access
inventory, state backup and restore, placement validation and staging rollback;
restoring YAML alone is not state recovery. All 215 Node contracts pass without
skips; version/fragment checks pass. This is preparation, not an activated rename
or runtime Grafana validation. Patch fragment: `pvad-grafana-rename-preparation`.
Prior API/database and external publication gates remain open. VERSION is 1.1.0.

Broad validation follow-up (2026-10-23, source commit `5a0f48c4`): the complete
Solution builds with zero errors and 171 warnings. The full default test attempt
was stopped after repeated API application-start failures caused by local
PostgreSQL authentication; it is not a passing full-Solution test result. A
separate Solution run excluding API tests passes 2,264 tests with two PostgreSQL
Persistence opt-ins skipped. All 221 Export tests pass. Designer typecheck,
729 Jest tests and production build pass. All five Website builds/tests pass;
Account typecheck passes. MCP typecheck, 26 tests and build pass. The expanded
Node contract selection passes 199/199 after correcting two stale recovery-test
prefix expectations. Details, limitations and next steps are in
[the validation report](PVAD.Rename-Test-Report.md#broad-build-and-test-follow-up-2026-10-23).
No database credentials, installed resources, VERSION or public release were changed.

Internal-name follow-up (2026-10-23): Nginx auth subrequests and local variables,
new backup filenames, ephemeral recovery/retention resource names, synthetic
mail-test identities and the negative-path hosting probe now use PVAD. Existing
backups/checksums, restore confirmation, upstream auth headers, legacy API paths,
dashboard/runbook IDs and protected deployment bindings are retained. The
retention project is unique per run; its fixed ports still require serial runs.
48 focused tests pass, including mocked backup/restore flows for both filenames.
No real database, Nginx container, mail delivery or retention run was executed.
The patch fragment is `pvad-internal-operator-names`; VERSION stays 1.1.0.

Operator/MCP guide and Grafana follow-up (2026-10-23): restricted Admin/recovery
instructions match canonical API routes, mounted secret names and new backup
outputs. The proposed Admin host remains explicitly unverified. MCP examples use
the current Compose API hostname and canonical scopes while documenting retained
discovery spellings and grants. Grafana datasource display names now use PVAD;
UIDs, cross-links, provider/folder identity and permissions are not migrated.
The deletion list no longer deletes active datasources during provisioning.
67 focused tests pass; version/fragment validation passes. No actual Grafana
upgrade, remote OAuth exchange, database recovery or deployment was performed.
Patch fragment: `pvad-operator-guides-and-monitoring-labels`; VERSION stays 1.1.0.

Validation-caller follow-up (2026-10-23): Legal PostgreSQL and Redaction Recovery
launchers, Trust visual-artifact generation and the PDF/A evidence helper now
pass canonical PVAD environment keys. The Redaction Recovery command example
matches. Receiver aliases, conflict handling, evidence schema and pinned fixture
bytes remain unchanged. Six focused contracts pass, including two fake-dotnet
launches that stop before services; the combined focused selection passes 73/73.
Node syntax, version and fragment checks pass. No real .NET suite, veraPDF,
database, recovery, image comparison or baseline regeneration was run. Internal
fragment: `pvad-validation-caller-keys` (impact none); VERSION stays 1.1.0.

Final internal wording follow-up (2026-10-23): the corpus root-not-found message
uses PVAD. Synthetic signer/author identity and fixture bytes remain unchanged.
Open internal rename changes are being committed before the broader validation
run; any results recorded earlier apply only to their named source checkpoints.

Operator-example follow-up (2026-10-23): PDF/UA paths and new key/secret/directory
examples use PVAD. Hosting/Company strategy separate unverified target domains
from unchanged runtime fallbacks. The AES-256 runner now uses the canonical local
image already produced by the corpus helper. 24 focused contracts pass; fixture
credentials, backup/restore contracts and actual infrastructure remain unchanged.
No image execution, recovery operation or deployment is inferred from these checks.

Workflow boundary follow-up (2026-10-23): all nine workflow files receive local
syntax/reference coverage; 17 workflow/deployment contracts pass. Retained GHCR
targets, protected environment names and concurrency locks are explicitly
commented and recorded in the
[binding register](../docs/pvad-rename-release-approvals.md#retained-workflow-bindings).
The five edited YAML definitions are semantically unchanged. Their remote
cutover stays open pending explicit owner decisions and validation.

Ignore-rule follow-up: obsolete branding literals were replaced by bounded
patterns retaining current/legacy runtime-data protection. Environment variants
are ignored and examples/shared editor configuration remain visible. Three Git
ignore contracts pass. The 228 already tracked `designer-simples/` examples remain
unchanged; `.gitignore` does not retroactively untrack them.

CI follow-up: remaining generated PDF/A paths and their three test producers now
use PVAD, as do new SBOM namespace IDs under the selected domain. Six focused
workflow tests pass (YAML, shell syntax, paths, producer/consumer references and
metadata). Historical evidence and diagnostics remain unchanged. Full GitHub CI,
PDF generation and independent validation are not claimed by these source checks.

Compose follow-up: root/API defaults and production/webhook overlays now use
PVAD for all services, storage/network keys, secret mounts and local credential
defaults. Legacy database settings, DNS aliases and actual external storage/
networks are isolated in explicit compatibility overlays. WebApi and EF defaults
match the fresh stack. **51 focused checks passed**, including actual Compose
configuration for fresh/legacy deployments and failure on missing bindings.
No containers or data were modified; application builds and runtime cutover remain
deferred. See [the mapping](../docs/pvad-infrastructure-mapping.md).

Documentation follow-up: public PDF Trust HTML/README headings use PVADE;
example links use clearly marked target hosts. Current documentation audit paths
point to PVAD sources. Runtime host defaults, signed corpus bytes and historical
acceptance records are unchanged. Documentation build/link/browser validation
for this final naming pass remains deferred.

### Source Rename Versus Validation

| Area | Current source status | Remaining acceptance |
| --- | --- | --- |
| Technical names and visible branding | PVAD / PVADE across the reviewed active projects and UI | Repeat the deferred builds and tests on the final source revision |
| Observability source files and template names | Canonical paths and references; stable IDs and legacy aliases retained | Rule/template/runtime checks deferred |
| Internal PDF validation tools | Java package, Maven artifact, shaded main class, JAR path and local image tags renamed together | Java/container build and interop execution deferred |
| Compose defaults and legacy overlays | Canonical defaults; explicit verified external bindings for existing data | 51 focused checks passed; live DNS, mounts, stop/start and rollback remain open |
| Legacy formats, schemas, diagnostics and evidence | Intentionally retained compatibility readers or immutable provenance | Do not erase these identities as cosmetic cleanup |
| Existing infrastructure and publication | No live resource or registry changes made | Separate operator authorization and deployment evidence |

The combined 88/97 score below is the earlier acceptance checkpoint, not a
percentage for source-name replacement alone. No new build/test result is claimed
for the latest rename-only changes. Remaining old-name files are compatibility
entrypoints, legacy schemas/fixtures, archived signed evidence or historical/
neutral Canvas terminology; they are not all outstanding product renames.

### Remaining Old Names And Their Treatment

| Remaining name | Why it remains | Future action |
| --- | --- | --- |
| `tools/deployment/pxa-deployment.mjs` | Compatibility command delegating to the canonical implementation | Remove only after the agreed compatibility window |
| `docs/schema/pxa-*.schema.json`, old native fixtures | Readers and schema contracts for older documents | Keep alongside canonical schemas and fixtures |
| Archived PAdES `pxa-*` PDF/XML and corpus IDs | Hash-bound validation evidence and provenance | Do not rewrite or rename as cosmetic cleanup |
| Stable diagnostic IDs and old input aliases | Existing documents, clients and operational handlers depend on them | Keep stable; canonical aliases are separate |
| Legacy service DNS aliases, database/volume/network names and dashboard UIDs | Persisted installations, history and data bindings; service keys are canonical | Coordinated mapping/migration and later validation, never a blind replacement |
| Rename-history checklist names and neutral Canvas terms | Historical intent or ordinary drawing terminology | Keep; they are not active product branding |

The latest source-only pass corrected remaining helper console/error labels and
canonical veraPDF invocation variables. Fixture passwords, certificate subjects,
signed document metadata and historical corpus fields were deliberately retained.
This table is not a claim that a future naming scan cannot reveal another issue.

### Previous Validated Checkpoint

Planning baseline: 2026-10-17. Earlier repository review: 2026-10-22.
At that checkpoint, the context audit, exact retained-name
exception snapshot and full staged-index review are complete: **88/97 (90.7%)**.
Nine release/external cutover tasks remain open. No commit or push was performed.
Follow-up: corrected bundled font registration to match the PDF text renderer
and renamed internal product/notification CSS classes with their consumers.
At that checkpoint, 729 Designer tests passed. Isolated Chromium checks verified
all four local font faces; full Designer/backend visual parity remains separate.
Latest user clarification: technical name PVAD; market name PVADE.
Audit: [PVAD.Rename-Audit.md](PVAD.Rename-Audit.md).
Inventory: [PVAD.Rename-Inventory.tsv](PVAD.Rename-Inventory.tsv).

Bundled internal follow-up: a PWA file-launch handler, explicit authenticated
project intake, single-use owner-bound transfer and all six intake translations
are implemented. The manual chooser remains available. Browser smoke evidence
uses synthetic sessions on desktop and mobile; installed OS associations remain
unverified. See [native opening](../docs/pvad-native-project-opening.md).
An isolated PostgreSQL service-name cutover and rollback preserved both synthetic
rows, with no concurrent writers or customer mounts; owned test resources were
removed. These partial internal deliverables do not close the combined external
gates, so the honest total remains **88/97 (90.7%)**, not 95%.
The earlier complete browser launch-to-editor flow passed for both project kinds on
desktop and mobile with synthetic API responses. It verifies one file read, one
upload, Designer/CSRF headers and visible imported content. Fixed the missing
progress-upload headers and the StrictMode cancellation of Spreadsheet launches.
The earlier release dry-run proposed 2.0.0 from 99 pending fragments; VERSION and the published
manifest were verified unchanged. This is not a release or deployment approval.
The repeatable [local validation bundle](../deploy/rename/LOCAL-VALIDATION.md)
also covers relocated synthetic keyring restore, real PostgreSQL dump/restore,
Nginx-hosted production assets and Chromium installability diagnostics. Public
Designer CSP remains an internal predeployment gap, not a third-party dependency.
A Designer CSP profile is now drafted with baseline enforcement and report-only
resource diagnostics. Contract tests pass; the final browser run was not approved,
and native PDF display/print is unverified. See the
[profile evidence and follow-up](../deploy/websites/DESIGNER-SECURITY.md).

### Resume With Validation, Not More Cosmetic Replacement

When validation is authorized, first check the final staged file mappings and
remaining-name ledger, then build the affected projects and run the deferred
Observability rule/template/API and Java/Maven/container checks. Recheck the
Designer/CSP browser workflow only with the required environment permission.
Do not reuse old pass counts as acceptance of the final source revision.
Publication, publisher ownership and real infrastructure cutover retain their
separate approvals. Do not delete legacy schemas, readers, diagnostics or signed
evidence to make a text search empty.

### Naming Authority

PVAD and PVADE serve different purposes and must not be normalized into one name.
Use PVAD in namespaces, assemblies, repositories, packages, paths, CLI, config,
APIs and other technical identifiers. Use PVADE in visible platform branding,
including websites and Designer. A technically named PVAD.Company application
therefore presents the PVADE brand; pvad-designer presents PVADE Designer.
PVADE means Platform for Visualization & Automation of Documents - Engine.
The final E represents Engine. The claim is Create. Visualize. Automate.
PDF product PVADP and Spreadsheet product PVADS follow the handoff.
Optional Word/Viewer/Converter/Automation brands remain unconfirmed.

## Canonical Mapping

| Old | Target | Treatment |
|---|---|---|
| PXA / Power Dox Automation in visible platform copy | PVADE | Websites, Designer, titles, logos and marketing; legal entity names require verification |
| PXA as technical identity | PVAD | Namespaces, assemblies, packages, paths, CLI, configuration and protocols |
| PXA PDF product | PVADP | Product display name; PDF remains the technical format name |
| PXA Spreadsheet product | PVADS | Product display name; Spreadsheet remains the domain |
| PXA.* | PVAD.* | All projects, namespaces, assemblies and test projects |
| Pxa* / pxa* / PXA_* | Pvad* / pvad* / PVAD_* | Symbols and owned config keys; classify stored/wire values first |
| PXA.sln / PXA.slnx | PVAD.sln / PVAD.slnx | Preserve project IDs and intended membership |
| pxa-designer | pvad-designer | Directory, package, scripts and all references |
| websites/PXA.<Site> | websites/PVAD.<Site> | Company, Documentation, Demo, Admin, Account |
| tools/PXA.Mcp | tools/PVAD.Mcp | npm package pvad-mcp; executables pvad-mcp and pvad-mcp-server |
| PXA.Generator / PXA.Pdf | PVAD.Generator / PVAD.Pdf | Keep architecture; do not collapse into PVADP namespaces |
| PXA.Importer / PXA.FileImporter* | PVAD.Importer / PVAD.FileImporter* | Update parser/generator bridges and workers |
| PXA.Migration.<Domain>.<Kind>.<Provider> | PVAD.Migration.<Domain>.<Kind>.<Provider> | Preserve provider names and domain separation |
| tools/versioning/pxa-* | tools/versioning/pvad-* | Scripts, tests, workflows and skill references |
| product-metadata/pxa-releases.json | product-metadata/pvad-releases.json | Preserve version history and IDs; update schema/readers coherently |
| pxa-build-info.json | pvad-build-info.json | All writers, packaging checks and UI readers |
| .pxap / .pxas | .pvadp / .pvads | Proposed canonical extensions; legacy import retained |
| pxap / pxas format IDs | pvadp / pvads | Versioned reader/writer migration |
| PXA-PXAP-1 / PXA-PXAS-1 | PVAD-PVADP-1 / PVAD-PVADS-1 | Explicit new envelope identities |
| application/vnd.powerdox.pxap+zip | application/vnd.pvad.pvadp+zip | Internal media type; do not claim IANA registration |
| application/vnd.powerdox.pxas+zip | application/vnd.pvad.pvads+zip | Same policy |
| minimumPxaVersion | minimumPvadVersion | New output plus old-input adapter; reject contradictory fields |
| /api/pxa/v1 | /api/pvad/v1 | New canonical routes with bounded old-route adapters |
| PXAAPI / PXACODE / PXAPROJECT / PXA_* codes | PVADAPI / PVADCODE / PVADPROJECT / PVAD_* | Stable translation map and compatibility tests |
| __Host-PXA.* / PXA.*.Development | __Host-PVAD.* / PVAD.*.Development | Dedicated auth transition; preserve host isolation |
| PowerDoxAutomation protection context | PVAD | Migrate protected payloads before retiring legacy reader |
| pxa-* browser keys/events/cache | pvad-* | Idempotent migration; honor user/tenant boundaries |
| pxa-* Docker services/images/networks | pvad-* | Internal config rename; preserve physical persisted storage |
| PXA-named checklist/docs files | PVAD-named counterparts | Update forward/reverse links and historical attribution |
| powerdoxautomation.com URLs | Confirmed market-brand domain | PVADE branding does not imply domain ownership; deployment configuration after verification |
| pxa-release-author skill | pvad-release-author | Rename repo instructions, tests and references during implementation |

These are proposed technical targets for execution, including extensions.
A complete rename does not mean corrupting immutable historical evidence or
discarding compatibility. Such remaining old strings must be explicitly listed.

## P0 - Baseline And Decisions

- [x] Create a dedicated rename branch from the reviewed current commit.
- [x] Record commit, clean/dirty state, runtime versions and reproducible test commands.
- [x] Refresh the tracked inventory; classify source, contracts, binaries, legal evidence and generated outputs.
  Reproducible current snapshot: PVAD.Rename-Inventory-Current.tsv, generated by
  tools/configuration/rename-inventory.mjs. Original inventory remains the baseline.
  File classes are triage, not per-match approval; ignored build outputs are a
  separate review gate. No line contents or secret values are included.
- [x] Identify published packages, customers and deployed instances from confirmed records.
  Repository owner confirmed on 2026-10-21 that the product is not published
  and there are no published/customer installations in scope. Existing evidence
  covers local development/test deployments only, not a production inventory.
- [ ] Confirm target domain, public publisher scopes, registry ownership and support addresses.
  2026-10-22 preparation: deploy/rename/pvade-targets.candidate.json keeps each
  publisher explicitly null. The offline compiler validates independent publisher
  selections but never asserts ownership or authorizes publishing. Proposed
  subdomains are distinguished from the confirmed base-domain/address choice.
  Target domain pvade.com confirmed by the repository owner on 2026-10-21.
  Support address support@pvade.com also confirmed. Package publishers and
  registries are explicitly not registered/chosen yet. Mailbox/DNS ownership
  and sender configuration are not implied; this combined gate remains open.
- [x] Encode the confirmed distinction: technicalName PVAD, displayName PVADE, PDF PVADP, Spreadsheet PVADS, full name and claim from the handoff.
- [x] Ratify proposed file extensions, diagnostic mapping and API compatibility window.
  Repository owner explicitly confirmed on 2026-10-21: .pvadp and .pvads,
  stable existing diagnostic IDs and legacy API/format compatibility for one
  major release. Publication/production authorization is not implied.
- [x] Inventory credential references by key name only; never include values in reports.
- [x] Record backup, restoration and rollout owners for every persistent environment.
  Owner confirmed responsibility for the existing local development environment
  on 2026-10-21. No published/customer environment is in scope. This assigns
  responsibility only; it does not certify a production backup or authorize rollout.

## P1 - Projects And Internal Code

- [x] Rename all 115 project directories/files and all PXA assembly/namespace references to PVAD.
- [x] Rename first-party Pxa-equivalent symbols using semantic rename where possible.
- [x] Preserve third-party namespaces, vendor names and neutral Pdf/Spreadsheet identifiers.
  Baseline package IDs/vendor imports and neutral PdfDocument/SpreadsheetDto names
  pass preservation checks in rename-boundaries.test.mjs. Customer/vendor fixtures
  are not blanket-renamed; this does not assert complete provider feature fidelity.
- [x] Update both solutions, ProjectReference, build/copy targets and publish output paths.
- [x] Preserve existing GUIDs and EF migration IDs; verify SLN versus SLNX membership explicitly.
- [x] Rename WebApi, Core, Application, Domain, Infrastructure, Generation, Importing, Execution, Observability, migrations, tools and tests.
- [x] Update MSBuild PxaVersion/PxaBuildMetadata, assembly metadata, analyzer rules and InternalsVisibleTo.
- [x] Update source generators, Roslyn analyzers, sandbox allowlists, worker protocols and worker startup paths.
  Current conversion/worker scaffolding uses PVAD assemblies and imports; code-worker
  and OCR startup paths point to renamed binaries. Neutral protocol fields, language
  IDs, limits and diagnostic contracts are retained. Real-worker execution, all twelve
  directed representation conversions and sandbox regressions pass. Compiler warning
  PXA0001 remains the reviewed compatibility exception; no independent analyzer/source
  generator project was found. See [code generation compatibility](../docs/pvad-code-generation-compatibility.md).
- [x] Update code conversion, snippets, builder examples and provider-generated C#; test actual execution.
  Workspace headers, builder/model/Base64 execution and Code Workspace PDF filenames
  now use PVAD. Customer text and original source drafts remain unchanged. A registry-
  driven basic matrix covers 16 PDF and eight Spreadsheet Code providers: 23 produce
  compiled outputs; Pdftools retains its explicit manual contract. The public builder
  XML example is compiled/exported. EPPlus resource declarations were corrected.
  This closes rename-level code execution, not all provider-feature/fidelity coverage;
  diagnosed scaffolds and unsupported operations are not promoted to full support.
- [x] Build the complete solution immediately after this stage.

## P1 - Designer, Websites And MCP

- [x] Rename Designer and all five website directories/packages, plus MCP package and executables.
- [x] Update package-lock root/workspace identities through structured package tooling without dependency upgrades.
- [x] Update Vite/Jest/Playwright/TypeScript aliases, sibling node_modules paths and asset references.
- [x] Update all six locales, navigation, Account handoff messages, notifications, export metadata and download names.
  Locale stage: current string values now distinguish PVADE UI, PVAD APIs and
  PVADP/PVADS native projects in all six languages. Locale-key/placeholder and
  branding regression tests pass; historical diagnostic translation keys remain.
  New PDF Creator metadata and local recovery filename defaults are updated
  without rewriting customer metadata. Account authentication/access copy now
  uses PVADE in all languages; new Legal notifications and Admin export filenames
  use current names. Account/Admin, Designer notification/auth-route and Legal
  governance regression suites pass. Historical notification content remains
  unchanged. Real desktop/mobile browser tests passed registration, mail
  verification, login, PKCE handoff, callback cleanup, authenticated session and
  reload plus template/migration/viewer navigation without mocked auth responses.
- [x] Display PVADE for the platform and PVADE Designer, PVADP for the PDF product and PVADS for Spreadsheet.
- [x] Introduce a central brand definition with separate technicalName and displayName consumed by shared surfaces; avoid unrelated layout changes.
- [x] Inventory existing logo, favicon, app manifest, screenshot and social assets; use approved assets or accurate text branding.
  Recorded six text-branded entrypoints, 11 public workflow screenshots and
  11 protected Admin captures in [the asset inventory](../docs/pvad-brand-asset-inventory.md).
  Removed the missing Vite favicon reference; no unapproved replacement artwork
  was introduced. Actual logo approval and screenshot recapture remain separate
  release gates, not implied by inventory completion.
- [x] Update MCP tool descriptions, resource URIs, server identity, client examples and container entrypoints.
- [x] Migrate local storage/session storage/events/cache keys once with account isolation and failure recovery.
  Completed: sidebar-collapse preferences, exact-user favorite caches and locale
  preferences, element groups, recent content and status caches migrated with
  recoverable legacy reads. Account/Admin API events and content drag MIME now
  use PVAD. Document handoffs now use owner-bound, expiring PVAD envelopes.
  PKCE writes PVAD and consumes an exact legacy transaction once; telemetry uses
  one canonical bridge with a compatibility alias. Export preferences, font style
  IDs and Monaco marker ownership are PVAD. Browser identifier audit is recorded
  in [PVAD.Browser-Identifier-Audit.md](PVAD.Browser-Identifier-Audit.md).
  Unassigned code-recovery drafts now use a PVAD key but still require explicit
  import. Cleanup covers both aliases and legacy Canvas workbooks; workbook
  owner-scoped persistence is implemented and tested. Explicit legacy-workbook
  recovery exports native JSON without changing or adopting the original.
  Cookie names, API headers, runtime configuration globals and diagnostic codes
  remain separate cutover/contract tasks below; they are not implicitly renamed.
- [x] Verify auth return URLs, API proxying, all local hosts and runtime production configuration.
  Isolated six-host production-preview tests cover actual proxies, runtime Account
  handoff configuration, allowlisted return paths, cross-site links and desktop/mobile
  rendering. This complements the earlier authenticated identity E2E; it does not
  authorize production DNS or external identity-provider changes.
  Runtime reader now validates `__PVAD_CONFIG__` and its `__PXA_CONFIG__` alias,
  rejects conflicts without value disclosure and shows a safe configuration state.
  URL validation now rejects embedded credentials in return destinations and
  browser-ambiguous raw backslashes/control characters. All six development API
  proxy configurations have structural contract coverage. Local Nginx container
  smoke passed canonical/legacy outage responses, SPA statuses and cache headers.
  Website Nginx now consumes PVAD settings through an executable pre-substitution
  hook; legacy-only and matching aliases resolve identically, conflicts and
  missing/empty values fail closed. Real containers passed both input generations.
  Deploy hook and template together; template-only upgrades are unsupported.
  See [frontend host validation](../docs/pvad-frontend-host-validation.md).
  Production/local-host and reverse-proxy end-to-end verification remain open.

## P1 - API And Contract Migration

- [x] Publish /api/pvad/v1 equivalents with stable operationIds and matching auth/rate/audit policies.
- [x] Retain /api/pxa/v1 for one product major as deprecated delegates; document removal version before release.
- [x] Keep neutral routes such as /api/export where still supported; avoid duplicate controllers and split behavior.
- [x] Update OpenAPI, schema IDs, SDK paths and schema-document resolution.
  Canonical snapshots include binary native export contracts. Current writer and
  compatibility reader schemas resolve offline with retained legacy identities;
  valid identity tuples for both generations/four formats and mixed-tuple rejection
  are tested.
  MCP packages the registry and uses the canonical migration discovery alias.
  Designer/MCP are the implemented clients; public language SDKs remain planned,
  not unpublished code requiring a rename. See
  [schema and client contracts](../docs/pvad-schema-and-client-contracts.md).
- [x] Map branded diagnostic families explicitly; preserve numeric meaning and client error handling.
  A shared exact allowlist covers 494 API, PDF-Trust, E-Invoice, migration API, code-worker/workspace,
  license, Designer-access/feature/asset and entitlement/subscription IDs.
  `canonicalCode` (or `canonicalDecisionCode` on feature entries) is additive;
  old codes, permission decisions, type URLs and signed license bytes remain
  unchanged. A source contract guards the reviewed families against missing
  mappings. Template conflict and native project diagnostics now have aliases;
  project-open compatibility uses an API projection without adding properties to
  archive manifests. Historical CANMIG/provider diagnostics and retained history
  remain explicit legacy contracts and are not rewritten.
  API-only JSON projections now add aliases to E-Invoice and PDF-Trust diagnostic
  objects without changing normal engine serialization, stored diagnostics or
  official validator findings. Redaction, PDF/A/UA, archival conversion, parser
  and cross-reference diagnostics now have the same API-only projections.
  Layout import diagnostics and attachment findings now receive API-only aliases.
  Pipeline IDs are registered without changing exception text or exposing a new
  endpoint. Chart results have API-only aliases for their two exact known string
  prefixes; the three Viewer-specific redaction errors expose additive aliases.
  A production inventory contract covers all current numeric branded IDs.
  Symbolic authentication and usage results have API aliases; symbolic Problem
  Details titles additionally expose canonicalTitle without replacing their code.
  The complete production inventory guard classifies numeric and symbolic IDs,
  six configuration/retention names and the retained compiler contract PXA0001.
  Compiler pragmas/NoWarn and external validator rules remain unchanged.
  PostgreSQL checks confirm unchanged license decisions and real 403 Designer
  denials with both code fields. An MVC extension-key collision found during this
  verification is fixed and covered by factory and database regression tests.
- [x] Keep historical CANMIG codes stable unless separately approved; document them as legacy contracts.
  Verified 432 production IDs against the immutable rename baseline. The retained
  catalog is `product-metadata/legacy-migration-diagnostics.json`; the configuration
  CI suite prevents their disappearance. See the HTTP compatibility guide.
- [x] Inventory JSON property names, claims, OAuth audience/issuer, event contracts and serialized type names.
  See docs/pvad-serialized-contract-inventory.md and the reproducible 2,888-entry
  PVAD.Serialized-Contract-Inventory.tsv. Runtime secrets/customer payloads are not
  included; inventory is not completion of every subsequent contract migration.
  Claims, OAuth, signed licenses and neutral job payloads have documented reader/
  writer rules. Migration code results now write canonical `pvadCode`/`PvadCode`
  plus equal legacy aliases in compatibility HTTP responses and new job output.
  Internal service and Designer consumers use the canonical name. Existing stored
  result bytes remain untouched; this does not complete the wider event/type audit.
  Three migration artifact identities now have additive canonical ID/MIME/extension
  metadata shared by catalog and result responses. Viewer output adds canonicalKind
  while retaining the legacy kind and primary fields; stored packages are not rewritten.
- [x] Read legacy names where needed; write PVAD canonical names in new contracts.
  Security request headers are canonical with a pre-auth legacy normalizer;
  conflicting/duplicate aliases fail closed. Four response-header families write
  both names. Cookies, credentials, signed webhook envelopes and other serialized
  contracts are not covered by this completed header substage.
  Subsequent stages now cover cookies, API-key secrets, the five claim families
  and paired PDF/A, redaction and E-Invoice response headers with shared CORS
  exposure and canonical-first browser readers. SMTP writes paired outbox headers
  without modifying retained rows, delivery IDs or signed audit bytes.
  Other stages cover signed internal MCP delegation. OAuth scope readers recognize known aliases,
  and the server/new client registrations now accept known canonical scope aliases.
  An opt-in, idempotent client/grant alias migration now preserves credentials,
  metadata, revoked grants and issued tokens under InMemory and PostgreSQL tests.
  Existing stores remain unchanged unless enabled; issuance preserves requested
  scopes. New dynamic clients receive PVAD IDs. Existing-client login examples
  stay legacy until the target-store maintenance procedure is applied and verified.
  Necessary-storage acknowledgements now write PVAD.StorageNotice and migrate
  compatible legacy acknowledgements; unit/browser tests and the storage inventory
  cover this boundary. Retained signed payloads and existing OAuth grants are the
  explicitly documented exceptions, not silently rewritten canonical output.
- [x] Validate generated-code roundtrips, all migration providers, source maps and worker previews.
  New Model/PDF source writes `pvad-element-id:`. API and both worker parsing paths
  share a bounded reader for canonical and legacy markers. Deploy API and worker
  together; this does not make old worker binaries understand new generated code.
  Current focused API/provider/worker/documentation selection: 95/95 passed.
  This supplements the full migration suites and conversion tests recorded earlier;
  it does not promote providers with manual or scaffold coverage to full parity.

## P1 - Identity, Database, Licenses And Jobs

- [x] Design and test old/new cookie handling or a coordinated forced-login cutover.
  Canonical session/CSRF names, retained ticket purposes, validated renewal,
  chunk cleanup and conflict rejection are covered by real TestServer ticket/CSRF
  tests and actual application-option checks. Production-host and PostgreSQL
  session verification remain separate gates, not implied by this source migration.
- [ ] Migrate Data Protection consumers, purpose strings, key access and encrypted retained job data.
  Additional local tests cover both retained payload purposes through real
  file-keyring provider restarts, old/new tampering and unrelated-app rejection.
  Production key mounts/recovery remain unverified; migration notes now warn
  explicitly about changed content roots and empty default key directories.
  Mail outbox and PDF revocation-cache consumers now read old/write PVAD purposes,
  with keyring restart and cross-purpose tests. Retained job/object encryption,
  production key access and deployment/rollback evidence remain open. See
  [identity compatibility](../docs/pvad-identity-compatibility.md).
  Follow-up source review and local regressions confirm that job/object storage
  has no application Data Protection envelope to rename. Do not invent an
  encrypted-data migration or claim encryption at rest. Actual production key
  access and the deployment owner sign-off remain open.
- [x] Preserve host-only Account/Designer/Admin boundaries, CSRF, PKCE and active-tenant authorization.
  Source/in-process coverage includes actual cookie configuration, ticket isolation,
  real antiforgery validation, PKCE/state/tenant handoffs and conflicting claim
  rejection. Live hosts, PostgreSQL sessions and rollout remain separate gates.
- [x] Keep neutral schemas/tables; rename branded identifiers only through required, verified migrations.
  Real PXA/PVAD processes use the same migrated PostgreSQL schema. Physical column/
  table metadata and migration IDs are unchanged; neither generation reports pending
  migrations. No brand-only SQL rename is introduced.
- [x] Preserve migration history, user/organization/template IDs, roles, entitlements and subscription links.
  A pinned PXA binary creates synthetic records; fresh PVAD and PXA processes both
  verify their IDs, role/membership/subscription/entitlement links and retained
  template revision/checksum after job recovery. This is representative test-data
  evidence, not production customer backup/restore certification.
- [x] Inventory signed license envelope fields and signature-covered bytes; retain old validator and issue new-format replacements explicitly.
  Signed fields, wrapper metadata and byte-level signature rules are inventoried
  in [license/job compatibility](../docs/pvad-license-and-job-compatibility.md).
  New issuance/downloads use PVAD; historical downloads and schema 1/2 validation
  remain compatible. The protected replacement API now preserves rights/history
  and atomically records successor, Replaced state, audit and mail outbox. Real
  PostgreSQL tests cover rollback, parallel requests, schema-1 binding and unknown
  signed constraints. This is an API operation, not a new Admin UI button; offline
  artifact distribution and production cutover remain separate operational gates.
- [x] Preserve signing keys/key IDs needed by issued licenses and audit evidence.
  Existing paths/key IDs remain unchanged; incomplete pairs now fail rather than
  overwriting surviving material. Real ECDSA tests cover retained bytes, restart,
  old schemas and tampering. Production key backup/restore and multi-key rotation
  remain separate deployment work, not implied by these synthetic tests.
- [x] Do not edit published legal versions, acceptance hashes, signed audits, historical invoices or legal entity names.
  Baseline Legal source bytes remain identical; Legal governance passes 22 tests
  and real PostgreSQL rejects audit updates/deletes (1/1). No retained-customer
  data rewrite is part of this rename. Production backup/restore and comprehensive
  retained-data cutover evidence remain separate open gates.
- [x] Test in-flight/background jobs, stored object locations, queue discriminators and cancellation across cutover.
  PostgreSQL source-level tests now cover fresh-context restart, expired/active
  leases, retry exhaustion, worker shutdown/recovery, lost-claim result cleanup,
  tenant-scoped Pending cancellation and an enforced claim/cancel race. Legacy
  payload names and filesystem object locations remain usable after reopening.
  The representative paired-binary fixture now kills a real PXA process after its
  claim and resumes with PVAD, cancels an old Pending job, and reopens old/new
  results with both readers. It uses a controlled importer, not every provider or
  OCR/code worker. Stop old workers before starting PVAD; live deployment and
  mixed-version rolling safety are not certified. See
  [worker cutover](../docs/pvad-license-and-job-compatibility.md#coordinated-worker-cutover).
- [x] Back up and restore a representative database with templates, accounts, legal records and licenses.
  PvadDatabaseRestoreTests passes with real pg_dump/pg_restore between isolated
  PostgreSQL databases using the actual EF schema. IDs, template revision/JSON,
  Legal hash/acceptance, ECDSA-signed synthetic license bytes and append-only audit
  behavior survive restoration. This is not a production restore certification.

## P1 - Native Project Formats

- [x] Add PVADP/PVADS writer identities, media types, extensions and schemas together.
- [x] Read full legacy PXAP/PXAS envelopes, including magic, format ID, MIME and minimumPxaVersion.
- [x] Add explicit conversion preview and original-preserving save-as to the new format. The existing compatibility review requires confirmation for legacy envelopes and explains canonical Save project output.
- [x] Preserve document IDs, assets, workspace drafts, extension data, source maps and version semantics.
- [x] Recompute integrity hashes only for newly serialized output; never mutate old signed evidence.
- [x] Test renamed files, mismatched magic/extensions, dual version fields, hostile archives and cancellation.
  All 76 PvadProjectArchiveTests pass, including current/legacy tuples, hostile
  packages, alias conflicts, renamed formats and cancelled atomic operations.
- [x] Publish synthetic new fixtures while keeping old fixtures for backward-compatibility tests.
- [ ] Update Designer open/save filters, SDK helpers, download filenames and eventual OS associations. Designer filters, MIME checks, stream helpers and download names use PVADP/PVADS; OS associations remain open.
  2026-10-22: product-metadata/native-file-types.json and the candidate compiler
  now prepare Linux MIME, macOS exported types and Windows ProgIDs, checked
  against Core's canonical formats. No fake open command or default .zip handler
  is registered. A packaged native/PWA opener and actual per-OS tests remain open.

## P1 - Infrastructure And Operations

- [ ] Rename owned env variables, config keys, Docker ARGs, image names, services and telemetry identity.
  2026-10-22: canonicalComposeCandidate prepares canonical service keys and
  dependencies while preserving legacy DNS aliases, mounts, secrets, network
  settings and monitoring paths. Every persistent volume requires an explicit
  external physical ID. API/observability models and actual Compose config are
  tested with synthetic bindings only; no services were started or replaced.
  Follow-up: six remaining operational shell scripts, the Alertmanager overlay
  and retention Compose/Loki/Tempo inputs now use PVAD with conflict-checked
  legacy aliases. Canonical/legacy resolution and pre-execution rejection tests
  pass. Existing physical identities remain mapped exceptions pending owners.
  Webhook overlay inputs now also use PVAD; actual Compose output agrees for
  canonical and legacy inputs. Operational alert descriptions, mail subjects and
  dashboard tags use PVAD, with 37 rules validated and real template rendering.
  Physical UIDs, volumes, service names and deployed ownership remain separate.
  CI upload/download artifacts, scratch PR files and local SBOM image tags use
  PVAD; strict CI container metadata validation includes commit and build time.
  Existing registry repositories and protected workflow/environment identities
  remain explicitly retained pending external owner approval.
- [x] Accept legacy config during a documented transition; new keys win and conflicting values fail clearly.
  Shared shell/Node/.NET adapters, actual Compose resolution, Nginx canonical/legacy/
  conflict checks and the containerized MCP TLS smoke passed. Conflicts fail before
  execution; values are not evaluated as shell code or printed in diagnostics.
- [x] Bind renamed Compose services to existing physical volumes or perform tested backup/restore.
  Stateful service and logical storage names deliberately remain unchanged. Actual
  Compose tests verify equal physical volume names and mount bindings with an
  explicit existing project name for both input generations. Representative DB
  dump/restore was tested separately. Actual customer project IDs still require
  operator confirmation before deployment; no customer volume was renamed.
- [x] Plan DB/user/network/container renames with explicit mapping; preserve data and network access.
  docs/pvad-infrastructure-mapping.md maps stable physical DB/user/service/volume/
  network/secret identities. No actual customer resource or Compose project was
  renamed. Existing project/volume IDs must be confirmed before external rollout.
- [x] Update health/readiness checks, dashboards, alerts, collector rules and support diagnostics.
- [ ] Update reverse proxy, CORS/CSP, OAuth redirect allowlists, sender identities and webhook callbacks together.
  2026-10-22: API CORS is now configurable through a strictly validated explicit
  origin list; missing configuration preserves existing development defaults.
  In-process CORS and API compatibility tests pass 25/25. The shared offline
  candidate aligns site links, Designer handoff, return allowlists, mail URLs and
  proposed OAuth host values. Mail/OAuth stay disabled in the candidate, with no
  new credentials or fabricated webhook target. Real proxy/TLS/CSP/callback and
  mail-delivery cutover remain required. See deploy/rename/README.md.
  Actual API preflight tests now cover both route generations, allowed local
  origins, untrusted and near-prefix origins and null origins. Six-language
  transactional copy and sender display name now use PVADE; configured addresses,
  issued tokens and stored mail payloads are preserved. Real-host callback and
  DNS changes are not authorized by these local tests.
- [ ] Change production domains, registries, DNS, email and external systems only after explicit deployment authorization.
- [x] Preserve real secrets, customer references and signed material without string replacement.
  In the confirmed local/unpublished scope, 61 baseline PDF/certificate/signed
  XML assets and baseline Legal candidate bytes are preserved exactly. Staged
  path checks reject private runtime directories and credential files. Existing
  customer-supplied names/content remain data, not replacement targets. No real
  private keys were inspected, moved or rewritten; production key access and
  recovery remain separate open gates. No customer deployment is confirmed.
- [x] Document rollback for application, data, URLs, jobs and identity, including irreversible boundaries.
  See docs/pvad-rename-rollback.md. Production restoration and named external
  owners remain separate open authorization/evidence gates.

## P1 - Documentation, Release And Evidence

- [x] Rename owned active docs/checklist filenames and update all internal links and generated TOCs.
  Durable path mapping records 73 moves. Contract tests verify every target and all
  relative file links in the moved documents. Public generated navigation anchors
  and guide downloads pass real-browser checks; historical content remains marked.
- [x] Explain historical names accurately; do not rewrite Git history, tags or authored third-party evidence.
  Category-level rules are in docs/pvad-rename-exceptions.md; no history/tag rewrite
  was performed. Exhaustive remaining-match classification is still open below.
- [x] Update public Company, Documentation, Demo and protected Admin guide content.
  Current text uses PVADE/PVAD; public downloads use an allowlisted packaging path,
  never operator documents. Legacy contract examples remain explicitly compatible.
  Historical screenshots/legal publication bytes are excluded and remain separate.
- [x] Update repo skills/instructions, llms files, samples, cookbook and contributor/testing architecture guides.
  Root guides, active PDF guides, tool READMEs, DocFX identity and the protected
  release-author skill were updated. Historical job-cutover examples, restore
  confirmation text and mounted secret filenames remain intentional exceptions.
- [x] Regenerate first-party screenshots/PDF examples and revalidate changed golden/cryptographic evidence.
  Eleven protected Admin captures use real Admin/API and isolated PostgreSQL;
  eight Viewer and two Editor captures now use the current UI with explicitly
  synthetic API fixtures. The remaining mixed-layout illustration has no brand.
  Viewer screenshots are not cryptographic evidence. PDF metadata/ICC tests and
  importer corpus checks cover the changed runtime identity; historical signed
  fixtures remain unchanged. See the test report and asset inventory.
- [x] Rename version scripts, manifest paths, product identity and build-info outputs together.
- [x] Preserve root VERSION until release preparation; do not restart product history at 1.0.0.
- [x] Add a customer-facing major release fragment for breaking namespace/package changes.
- [x] Prepare coordinated migration notes for packages, APIs, config, sessions, files and deployment.
  docs/pvad-rename-migration-notes.md links each boundary, paired rollout and rollback
  procedures; deployment prerequisites are explicitly pending, not silently approved.
- [x] Record test results and residual exceptions in PVAD.Rename-Test-Report.md and docs/pvad-rename-migration-notes.md before rollout.

## Validation Gates

- [x] Run dotnet build PVAD.sln and independently validate PVAD.slnx's intended scope.
- [x] Run core, application, persistence, API, Generator, importer, migration and export suites.
  Full default solution run: 53 test projects, 3,294 passed, zero failed,
  78 opt-in PostgreSQL tests skipped. This closes the default suite execution
  gate only; the subsequent opt-in database results are recorded below.
- [x] Run Designer type-check, Jest, production build and authenticated desktop/mobile E2E.
  Full Jest: 80 suites/711 tests; type-check and build pass. Real synthetic Account
  desktop/mobile registration/verification/login/session/navigation E2E: 2/2 pass.
- [x] Build/test all five websites and shared modules; inspect internal links and all local host redirects.
  Company 55, Documentation 57, Demo 2, Admin 37 and Account 50 tests passed.
  All six production builds and seven isolated host/browser checks passed; test
  build origins were subsequently removed by normal production rebuilds.
- [x] Run MCP typecheck/build/tests and stdio/remote smoke tests.
  Typecheck/build, all 26 tests, authenticated local Streamable HTTP SDK test and
  packaged Stdio resource/tool smoke pass. Public TLS deployment remains separate.
- [x] Test JSON/C# Model/C# PDF/Base64 conversions and dynamically compiled migration output.
  Conversion/worker/generated-provider API filter: 76 tests pass, including twelve
  directed format roundtrips and compiled provider output. Diagnosed provider
  scaffolds/manual cases are not claimed as exact feature-complete conversion.
- [x] Test both old and new native project formats and old/new API paths.
- [x] Test persisted DB upgrade/rollback, auth, legal history, licenses, jobs, assets and tenant isolation.
  Final opt-in API selection plus three protected-document contracts: 79/79
  passed, zero skips. This includes real pg_dump/restore, OAuth/identity,
  retained Legal/license evidence, tenant isolation and the explicitly enabled
  PXA/PVAD paired-binary job/schema/storage cutover. Persistence separately
  passes 10/10 with real PostgreSQL. Production rollback, real-key/customer
  datasets and mixed-version rolling operation remain external rollout gates.
  An initial template-update HTTP 500 was not reproduced in the focused rerun,
  five repetitions or the final full selection; its cause is not claimed fixed.
- [x] Validate Docker/Compose config, worker packaging and readiness without replacing customer volumes.
  Configuration/Compose contracts pass; the local API image with both worker
  packages runs non-root/read-only against a fresh migrated PostgreSQL database.
  Readiness is 200 with the database and 503 after its deliberate removal;
  liveness remains 200. Only newly created anonymous data volumes, containers
  and a random test network were used and removed. This is local API validation,
  not multi-platform image approval or production cutover authorization.
- [x] Run renamed versioning/release tooling, validate fragments and build-info/container metadata.
  Version/fragments checks and strict metadata tests pass. Both actual .NET images
  were built and inspected with VERSION 1.1.0, full commit identity and build time.
  Their local dirty-worktree suffix is explicit; stable-release validation rejects
  dirty/unknown/stale metadata. No image was published and VERSION is unchanged.
- [x] Audit all remaining PXA/PowerDox/Canvas names by context with a reviewed exception list.
  Final 2026-10-22 decision: exact file hashes, explicit rationale and existing
  boundary guides are recorded in docs/pvad-rename-reviewed-exceptions.json;
  CI rejects new, changed or unexplained residuals. Active descriptions were
  corrected; diagnostics, legacy readers, retained evidence, drawing APIs and
  physical infrastructure bindings have separate bounded decisions.
  The following notes record the earlier incremental review chronology.
  2026-10-22: the scanner now also catches lowercase legacy domains. The draft
  path/hash/reason ledger requires explicit per-file review and existing boundary
  guides; matching hashes and classification do not imply approval. See the
  [review safeguards](../docs/pvad-rename-exceptions.md#2026-10-22-review-queue-safeguards).
  Storage follow-up verifies old object keys/content and annotation files across
  explicit-mount restarts, plus canonical worker package paths. Relative-root
  cutover hazards and required mount settings are now documented. This does not
  approve real installations or close the repository-wide gate.
  Authentication follow-up reviewed retained scheme/OAuth IDs and compared
  cookie/API-key checks against HEAD. Expanded real-handler tests cover both
  input generations and denied access states (64 focused tests, zero skips).
  Production identities and the remaining repository review remain open.
  Joined/lowercase Core identifiers now have an exact-path/literal contract,
  including native-format readers, legacy element marker and PDF chart Info key.
  This expands reviewed coverage but does not close the repository-wide gate.
  Core/Infrastructure standalone branding has an exact-path regression contract:
  only the legacy project magic remains. Current Word default authors and the
  branding API are corrected. Joined identifiers, XML wire-format compatibility
  and other source/documentation scopes still require separate review; see
  docs/pvad-rename-exceptions.md. This does not close the repository-wide gate.
  The follow-up also covers standalone API/importer branding with explicit
  literal-level exceptions. XML defaults to PvadDocument with ExportLegacy for
  the old root; consumer migration is documented as a major-release change.
  Active chart/code/asset/architecture/deployment guides have a further reviewed
  naming pass. Release-linked chart/code guide files now ship in the public build;
  DocFX source/TOC contracts include native/architecture pages and exclude operator
  navigation. This does not approve every historical-document or identifier match.
  The 2026-10-21 follow-up adds a recursive joined/lowercase identity contract
  across Generation, Importing and Infrastructure with exact path/literal
  exceptions. It found and corrected the OCR worker process activity name.
  Company return signals now write pvad_signed_in and accept the old cosmetic
  input without changing authentication. Full repository gates remain open.
- [x] Review binaries, generated output and public screenshots separately from textual source search.
  Separate byte, generated-output and OCR audits completed: 96 repository binary
  artifacts (94 unchanged baseline blobs; two new native fixtures), 4,211 stale
  untracked generated PXA files removed after an approved hash/path-checked plan,
  and 22 documentation screenshots inspected by OCR. The initial audit found old
  branding in ten Admin captures; subsequent Admin and public recaptures are
  recorded in the completed screenshot-regeneration task. Audit completion is not
  exhaustive source-name approval or external artwork approval. Paired legacy
  harness outputs remain preserved.
- [x] Run git diff --check and review each phase's staged diff before committing.
  Final 2026-10-22 review covers the entire staged rename, not only the first
  batch. Including the later cutover-preparation follow-up, the actual index
  contains 2,918 files, zero unresolved rename targets,
  zero mode changes and no untracked/unstaged files. Final staged whitespace
  validation passes after correcting text line endings, empty TSV end fields
  and EOF blank lines. See docs/pvad-rename-phase-review.md.
  The following notes describe the earlier partial snapshots, not current status.
  A first bounded ignore-protection/content-review-tool batch is staged and
  reviewed; the remainder is not yet staged or globally approved. The new
  content-review report separates identity-only candidates from substantive
  changes, explicitly without automatic semantic approval.
  Partial phase review and the limited staged batch are recorded in
  docs/pvad-rename-phase-review.md.
  All 1,984 tracked deletions have current targets; 36 external-signature evidence
  files retain original basenames and exact bytes. This is pairing evidence, not
  semantic approval of every change or completion of a staged review.
  File-pair integration tests now verify the same result before/after staging in
  an isolated temporary repository. This validates the audit tool, not approval
  of the entire index. The overall count remains 85/97 (87.6%).
- [ ] Perform externally authorized staging/deployment smoke tests before production cutover.

## Delivery And Commit Order

### Implementation Progress - 2026-10-17

- Active branch: feature/rename-pxa-to-pvad.
- First implementation stage renames all 115 project files, 110 branded project
  directories and both solution files. The five other project files are tools
  inside neutral directory names.
- All 272 ProjectReference targets resolve. Original solution membership and
  GUIDs are preserved; the focused SLNX still contains its original six projects.
- Namespace references, generated-code namespace strings, reflection type names,
  worker executable paths and consumer build references were updated together.
- The symbol mapping records 534 owned identifiers. Technical types, methods,
  frontend paths and package roots now use PVAD/Pvad/pvad. Serialized contracts
  remain explicitly subject to the compatibility stages below.
- All five website builds and the Designer build pass. Synthetic browser checks
  cover six applications at desktop and mobile sizes; these are branding smoke
  tests, not a real authenticated end-to-end or production deployment sign-off.
- Shared brand metadata distinguishes PVAD, PVADE, PVADP and PVADS. Dependency
  versions remain unchanged, and VERSION remains 1.1.0.
- Canonical API controllers and path-based policies use /api/pvad/v1. A legacy
  adapter runs before routing, retaining /api/pxa/v1 throughout 2.x with planned
  removal in 3.0.0. Neutral routes remain unchanged. The actual generated OpenAPI
  document and both-prefix authentication/response behavior are covered by tests.
- The repository release-author skill and its test catalog now use
  pvad-release-author; the wider documentation/instructions task remains open.
- Configuration transition has started for build metadata, Designer documentation
  URL, EF design-time connection, development bootstrap and MCP settings. Both
  aliases are tested; conflicting values fail without disclosing their contents.
  Docker/Compose, telemetry, .NET sections and remaining tools are not complete,
  so the broad configuration checklist tasks remain unchecked.
- .NET EInvoices and connection-string sections now use Pvad with source-order
  legacy aliases. The actual host resolves both connection-string spellings;
  resolution is deferred rather than capturing defaults before host overrides.
- API Compose now uses canonical PVAD environment inputs through the validated
  launcher. Existing service/database/user/volume/secret identities are retained.
  WebApi/WebhookRelay build arguments and MSBuild metadata use PVAD with legacy
  argument fallbacks. Full image builds and broader observability migration are
  still open; no running stack or persisted volume has been changed.
- Telemetry instruments/spans and default resource identity use PVAD. Five
  managed dashboards and all 37 alert rules accept old/new metric prefixes; log
  filters accept both service names. Health/readiness and privacy tests pass.
  Collector privacy processors do not depend on the branded metric prefix and
  remain unchanged. Persisted Grafana UIDs, physical dependency names and stable
  runbook IDs are explicit transition exceptions, not silently rewritten data.
  OCR now writes PVAD.ImageOcr / pvad.ocr.worker.execute and pvad-image-ocr
  temporary directories; API subscriptions accept both source generations.
  Structured logging defaults to pvad-json with a registered pxa-json alias.
  Worker W3C propagation/cleanup and both formatter privacy paths passed tests.
  See [observability identity decisions](../docs/pvad-observability-identity.md).
- CI/fuzz workflows now publish PVAD environment keys. JavaScript tools, .NET
  test switches and twelve shell tools accept legacy names through shared
  resolvers. Shell adapters use no eval and pass canonical names to children.
  Other frontend/deployment identities still prevent closing the broad owned-
  configuration task; external tests are not inferred from passing stub wrappers.
- [PVAD.Configuration-Key-Inventory.json](PVAD.Configuration-Key-Inventory.json)
  records 131 old-key candidates without values. It includes diagnostics and
  requires classification; it is not proof of complete secret/deployment coverage.
- Auth cookies, protection contexts, native-format identities and EF migration
  IDs retain their previous values pending their dedicated compatibility stages.
- Detailed executed checks are recorded in
  [PVAD.Rename-Test-Report.md](PVAD.Rename-Test-Report.md).

1. Baseline, mapping and backup evidence.
2. Projects, namespaces, internal symbols and worker references.
3. Frontend/tool paths, branding and storage migration.
4. API, authentication, licensing and native-format compatibility.
5. Deployment definitions and operational migration.
6. Documentation, synthetic assets and release tooling.
7. Test report, migration notes and remaining exception review.

Keep every phase reviewable and buildable where practical. If a dependency forces
a larger atomic commit, record it before execution. Commit/push/merge/deploy are
separate operations; this planning step performs none of them.

## Completion Criteria

- [x] Active platform surfaces consistently display PVADE; first-party technical identities consistently use PVAD.
  Current shared-brand/localization contracts and desktop/mobile host screenshots
  verify active surfaces. Current documentation images were regenerated as
  recorded above; retained compatibility contracts and historical evidence remain
  intentionally original and require their own contextual review.
- [x] Verify website/Designer titles and accessible logo labels use PVADE while their technical packages, folders and namespaces use PVAD.
  Entry-point, package/lock identity and public brand-link contract tests passed;
  Company/Demo accessible home labels now use PVADE rather than PVAD project names.
- [x] PDF and Spreadsheet are consistently branded PVADP/PVADS.
  Shared product identity, native writers, schema identities and localized active
  navigation use the new names; legacy readers remain covered separately.
- [x] Every tracked old-name match is migrated or has a reviewed compatibility/history rationale.
  Exact text residual decisions are hash-bound and CI-validated; binary evidence
  and current screenshots have their separately completed review above. This
  does not authorize domain, registry, keyring or production infrastructure cutover.
- [x] Existing data, authentication boundaries, legal evidence and editable projects survive cutover.
  Repository acceptance uses the retained synthetic PXA/PVAD process dataset,
  tested dump/restore, native-format compatibility, unchanged signed/legal input
  bytes, keyring restart and host-isolated session tests. The follow-up real Admin
  login/capture and 46 local API contracts pass. This is representative cutover
  evidence, not certification of an uninspected customer installation; production
  keys, backups, host access and external rollout retain their own open gates.
- [ ] Breaking package/namespace changes have a major release and actionable migration instructions.
- [ ] External systems have confirmed owners and tested cutover/rollback evidence.
- [x] Test report distinguishes passed, skipped, blocked and unrun checks.

## Related Work

- [Previous rename](Rename-To-PXA.md)
- [Migration taxonomy](Migration-Namespace-Taxonomy.md)
- [Native formats](PVAD.Native-Designer-Formats.md)
- [Brand and logo](PVAD.Brand-And-Logo-System.md)
- [Product versioning](PVAD.Application-Versioning-And-Releases.md)
- [Designer auth](PVAD.Designer-Authentication.md)
- [Template persistence](PVAD.Designer-Template-Persistence.md)
- [Legal documents](PVAD.Legal-Terms-Privacy-And-Consent.md)
- [API Docker](PVAD.Api-Docker.md)
- [Designer Docker](PVAD.Designer-Docker.md)
