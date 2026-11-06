# PVAD Rename Classification And Historical Exceptions

## Current Source Corrections - 2026-11-05

The [remaining code checklist](../checklists/PVAD.Remaining-Code-Naming-Cleanup.md)
supersedes broad earlier claims that all internal names and writers were complete.
New editable-chart metadata and redacted-image resources now use canonical names;
old keys remain input aliases. Internal step-up policy names and the MCP runtime
user are canonical, not permanent compatibility exceptions. Persisted grants,
key IDs, protection purposes and historical evidence remain separate boundaries.
The [HTTP writer review](pvad-http-compatibility.md#remaining-writer-alias-review)
records protocol and browser aliases individually and bounds temporary output
aliases to the rename compatibility major.

Historical context review: 2026-10-22. The point-in-time decisions below are
superseded where the current corrections above explicitly say so. Production hosts,
publishers, external keys and real cutover/rollback remain separate open gates.

The later [complete Markdown cleanup](../checklists/PVAD.Markdown-Naming-Cleanup.md)
supersedes historical wording exceptions for Markdown. Exact values now live in
versioned JSON registers. Every tracked Markdown file passes the strict full-text
guard; Git retains original execution text. Legal source candidates are new
version 1.1 drafts, not changes to existing published or accepted records.

## Current Inventory

### 2026-10-22 Review Queue Safeguards

The text inventory detects case-insensitive old-name matches, including lowercase
`powerdox` hostnames, uppercase legacy test flags and incidental [compatibility entry `compat-a959a4da24b7`](reference/compatibility-identifiers.v1.json)
substrings. `pvad-rename-reviewed-exceptions.json` records exact paths, hashes,
retention decisions and boundary guides. The review command fails for pending,
unexplained, new, changed or removed entries. A classification or matching hash
alone never constitutes review. The checked snapshot is a point-in-time naming
decision, not unrestricted approval of future edits to these paths.

Retained schema `$id` URLs, configured website host defaults and mail sender
defaults are operational/serialized identities, not evidence that the selected
`pvade.com` hosts or mailboxes have been provisioned. The DSS validator's Java
package/JAR/image identities remain the reproducible validation harness for the
existing signed corpus, not a new public PVAD SDK or registered publisher. Its
package, Maven main class, Docker COPY and generating script must stay together
until a separately validated harness successor is introduced. Binary and
NUL-bearing files remain in the separate evidence review.

### Final Context Decisions

| Boundary | Naming decision |
| --- | --- |
| C# and UI diagnostics, localization lookup keys and obsolete pragmas | Preserve stable rule IDs and their existing meanings; bounded canonical aliases are additive. |
| Native format readers, generated-code markers, migration artifact tuples and chart provenance | Preserve explicitly supported legacy inputs/fields; canonical writers, new package names and generated namespaces use PVAD. |
| Browser preferences, document handoffs, auth transactions and telemetry bridge | Preserve bounded legacy readers and reset paths. A cosmetic signal never creates a session or grants access. |
| Cookies, schemes, claims, OAuth registrations, Data Protection and signing key IDs | Retained persisted/cryptographic bindings cannot be globally replaced without invalidating sessions, grants or signed material. Internal step-up policy names are now canonical. |
| PostgreSQL objects/migration SQL, volume names, mounted secrets, Grafana UIDs and registry repositories | Preserve physical identities; canonical environment aliases remain conflict-checked. New monitoring job/service labels use PVAD with dual-generation query selectors and separate live cutover gates. |
| Live documentation and active metadata descriptions | Correct remaining old product prose. Preserve schema IDs, compatibility explanations, external hosts and original evidence references explicitly. |
| Checklist prose, original execution records and audit inventories | Normalize Markdown without exceptions; retain task states and results, original wording in Git, and exact identifiers in JSON registers. Do not reclassify incomplete features as delivered. |
| Authored legal candidates and published release entries | Prepare new version 1.1 source drafts; preserve existing database versions, accepted hashes and published release history. New names do not authorize publication. |
| Original PDF Trust corpora, manual review decisions and visual baseline IDs | Preserve evidence provenance, signed subjects, original result IDs and filenames. Current generators now write canonical metadata with explicit legacy readers; see the current-output guide. |
| Test source and fixture strings | Retain deliberate old inputs and expected diagnostics; do not rewrite customer/vendor text during a successful rename test. |
| HTML/Skia/vendor Canvas APIs and encoded integrity data | Neutral rendering or coincidental substrings are not first-party branding. Preserve vendor APIs and dependency integrity hashes. |

The exact per-file snapshot, not this broad table, bounds these decisions. Its
validator runs in CI; changed file bytes require a new contextual decision.

The earlier `checklists/PVAD.Rename-Inventory-Current.tsv` is retained as a
point-in-time inventory, not refreshed by the current source batches. That older
inventory included Git-visible untracked files. Current acceptance uses
`node tools/configuration/rename-residual-review.mjs`, which reads tracked regular
files only and excludes private paths, untracked notes, symlinks and ignored output.
Do not refresh historical inventories or inspect private notes merely to reduce
search counts. Resolved records require exact clean-source hashes, not a blanket
exception for future edits.

Run `node tools/configuration/rename-artifact-audit.mjs` for the separate
`artifacts/rename/name-and-artifact-audit.json` report. It records source locations
and line hashes without source text or matched suffix values. Its findings are
explicitly unapproved: this is a review queue, not an automatic exception list.
Binary entries include baseline Git-blob equality and SHA-256; generated own
assemblies/dependency metadata are inventoried separately from repository inputs.

The approved 2026-10-21 cleanup removed 4,211 stale, untracked PVAD build outputs
under bin/obj after validating the entire plan, paths, non-symlink status and
hashes. Its manifest is `artifacts/rename/legacy-build-cleanup.json`. All paired
job-cutover bin/obj legacy outputs remain intact. The Solution rebuild passed
after cleanup. This does not authorize removal of arbitrary customer files.

Classes distinguish source, configuration, documentation/history, fixtures/data
contracts, legal/retained contracts, binaries and generated outputs. These are
triage buckets, not automatic approval of every old-name occurrence. Generated
and ignored local outputs require their separate build/artifact review.

## Preservation Rules

### Generation, Importing And Infrastructure Residual Review

The recursive C# contract in `runtime-brand-contract.test.mjs` checks joined,
lowercase and standalone old names across these three source trees, including
files that an interactive search configuration might omit. It rejects unexpected
identities and stale path/literal exceptions. Stable diagnostic codes, their
localization keys and [compatibility entry `compat-ccb786033be3`](reference/compatibility-identifiers.v1.json) pragma/obsolete references are not branding.

| Exact source boundary | Retained literal | Reason |
| --- | --- | --- |
| `PVAD.Generator/EInvoicePageLayout.cs` | [compatibility entry `compat-4a00ba8a5601`](reference/compatibility-identifiers.v1.json) | Existing versioned pagination contract; changing display prose does not change its schema identity. |
| `PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs` | [compatibility entry `compat-6306f3309206`](reference/compatibility-identifiers.v1.json) | Legacy ActivitySource subscription only; new parent and worker process activity names use PVAD. |
| `PVAD.FileImporter/PdfFileImporter.cs` | [compatibility entry `compat-53477bb0d970`](reference/compatibility-identifiers.v1.json) | Previous chart provenance remains readable and is normalized to `pvadMetadata`. Both PDF metadata keys are read; conflicting payloads preserve original graphics. |
| `PVAD.Importer/Redaction/PdfImageRedactionEngine.cs` | [compatibility entry `compat-22cc5163402c`](reference/compatibility-identifiers.v1.json) | Previous marker is read for existing PDFs and removed when rewriting an image; new image/mask output uses `PVADRedacted`. Sample and residue checks still apply. |
| `PVAD.Importer/Redaction/PdfRedactionApplier.cs` | [compatibility entry `compat-1ef9fd08fea6`](reference/compatibility-identifiers.v1.json) | Historical resource suffix only; newly generated collision-checked resource names use `_PVADRedacted`. Original PDFs remain unchanged unless explicitly edited. |
| `PVAD.Infrastructure.Converters/XmlDocumentExporter.cs` | [compatibility entry `compat-ee27215b8284`](reference/compatibility-identifiers.v1.json) | Explicit `ExportLegacy` output only; normal XML output uses `PvadDocument`. |
| `PVAD.Infrastructure.Persistence/PvadDbContextFactory.cs` | Local legacy database/login defaults | Prevent design-time commands silently selecting a different existing database. These are local defaults, not production credentials. |
| `PVAD.Infrastructure.Persistence/Configuration/PvadEnvironment.cs` | [compatibility entry `compat-281230894e71`](reference/compatibility-identifiers.v1.json) | Conflict-checked legacy environment input; canonical inputs use `PVAD_`. |
| `PVAD.Infrastructure.Persistence/PersistenceServiceCollectionExtensions.cs` | [compatibility entry `compat-200b6f2c8869`](reference/compatibility-identifiers.v1.json) | Retained health/data-source label consumed by the operational status contract. |
| `PVAD.Infrastructure.Persistence/Migrations/20260927050514_AddEInvoiceValidationRecords.cs` | [compatibility entry `compat-306460adb6d0`](reference/compatibility-identifiers.v1.json) | Applied SQL session-key contract; the current purge writer uses the same key. Historical migration SQL is not rewritten. |

This closes this bounded source review, not the repository-wide checklist gates.
The WebApi, frontend, tools, binary assets and historical content still require
their own complete review evidence. No production configuration is changed here.

Authentication review confirms that retained scheme names and OAuth registered
client IDs are persisted/cryptographic integration identities. Prefix changes
for newly generated API keys do not rewrite existing secret hashes. Both input
generations use the same revocation, expiry, service-account and organization
checks; the expanded integration matrix covers all those rejection states.
This does not authorize a production OAuth or Data Protection cutover.

- [compatibility entry `compat-1d4bfbb58b89`](reference/compatibility-identifiers.v1.json) in Git history, released tags, historical release titles and authored
  third-party evidence remain original. Do not rewrite history to clean a scan.
- Published legal versions, accepted hashes, signed licenses/audits, historical
  invoices, customer names and uploaded source remain byte-preserved unless an
  explicit authorized successor/conversion workflow applies.
- Diagnostic IDs, legacy API/schema/resource aliases, cookie/ticket readers,
  database migration IDs and physical storage identities follow their individual
  compatibility contracts. They are not global text-replacement candidates.
- CSS classes and vendor syntax in converter inputs are not product branding.
  Neutral `PdfDocument`, `SpreadsheetDto`, canvas HTML/graphics APIs and .NET/PDF
  concepts must not be renamed just because they resemble the old brand.
- Third-party names such as Aspose, DevExpress, Syncfusion, Nutrient, iText,
  Telerik, GemBox and EPPlus belong to their providers. Preserve package IDs,
  namespace identifiers and original test source. Generated target code uses
  PVAD without rewriting vendor source documents or claiming vendor ownership.
- Screenshots and PDFs are separate binary review items. Text scans cannot
  approve their contents or cryptographic evidence.

## Status

### Joined Core Identifier Review

The Core C# scan now covers joined and lowercase PVAD spellings as well as
standalone names. `runtime-brand-contract.test.mjs` bounds exceptions by exact
path and literal: legacy native-format IDs/MIME/extensions/magic and minimum
version readers in PvadProjectModels, the legacy element marker, and
[compatibility entry `compat-2352f6e27484`](reference/compatibility-identifiers.v1.json) in ChartMetadataCodec. Stable quoted diagnostic IDs remain a
separate category. Unexpected old identifiers fail the contract.

[compatibility entry `compat-2352f6e27484`](reference/compatibility-identifiers.v1.json) is a serialized PDF Info dictionary key, not display branding.
Changing it without a dual-reader migration would stop existing PDF metadata
recognition. Its retention is explicit here; this review does not claim that
automatic chart metadata production or recognition is complete in every exporter.

Legacy local-data directories must also retain their ignore rules. Both old and
current App_Data, generated Company legal snapshots and Designer build outputs
are excluded. A Git check-ignore contract tests these paths without writing data.

Baseline .NET package identities/vendor imports and
neutral PdfDocument/SpreadsheetDto names have automated preservation checks in
`tools/configuration/rename-boundaries.test.mjs`. This is rename boundary coverage,
not certification of every provider feature or arbitrary customer source. The
final per-file snapshot supplements these earlier scoped rules. Repository screenshot regeneration is
recorded separately from external artwork approval. See the [tracker](../checklists/PVAD.Rename-Plan.md),
[asset inventory](pvad-brand-asset-inventory.md) and
[observability exceptions](pvad-observability-identity.md).

## PDF Metadata and Historical Validator Reports

New PDF ICC profiles use exactly four-byte `PVAD` creator/manufacturer signatures;
new output-intent and validator display names use PVAD. Stable [compatibility entry `compat-7761cb789e48`](reference/compatibility-identifiers.v1.json) diagnostic
IDs and `CANMIG...` identifiers remain unchanged. Published PDF fixtures and their
historical JSON evidence are not rewritten to replace display text. Corpus tests
clone expected JSON and alias only the exact historical structural-validator
display name at version 1.3; other versions, statuses, evidence and hashes remain
strict. This is a test comparison adapter, not a production evidence migration.

## Core, Infrastructure and Branding API Follow-up

Current Core/Infrastructure descriptions now use PVAD. New Word comments and
tracked insertions default their author to PVADE only when the source author is
null. Explicit names, including an existing `PVAD` author, are preserved verbatim.
Word export tests verify both defaults and explicitly supplied identities.

The standalone `PVAD` scan of Core/Infrastructure C# source now has exactly one
reviewed source-line exception: `PvadProjectFormat.LegacyMagic` retains
[compatibility entry `compat-bea571ba4815`](reference/compatibility-identifiers.v1.json) and [compatibility entry `compat-d1b9fec09994`](reference/compatibility-identifiers.v1.json). Existing reference archives must remain readable;
writers emit the canonical identities. `runtime-brand-contract.test.mjs` checks
this exact path and line rather than allowing old names throughout a directory.
This scoped check is not approval of every joined identifier or lowercase match.

Other reviewed categories in this scope remain distinct:

| Location | Retained contract | Rationale |
| --- | --- | --- |
| `PvadProjectModels` | Legacy MIME types, extensions and [compatibility entry `compat-c29574ca3da7`](reference/compatibility-identifiers.v1.json) reader | Open existing packages without rewriting their bytes; reject conflicting versions. |
| Core diagnostic catalog and validators | [compatibility entry `compat-7761cb789e48`](reference/compatibility-identifiers.v1.json) diagnostic IDs | Keep stored reports, client handling and diagnostic aliases stable. |
| Persistence configuration and migrations | Existing database identity and migration SQL/session keys | A source rename must not silently choose a different database or rewrite applied migration history. |
| `PvadCodeElementMarker.Legacy` | [compatibility entry `compat-d4533c00f1a8`](reference/compatibility-identifiers.v1.json) | Preserve old generated-source element mappings. |

The XML follow-up now writes `PvadDocument` by default and exposes explicit
`ExportLegacy(design)` for [compatibility entry `compat-ee27215b8284`](reference/compatibility-identifiers.v1.json) integrations. Export tests compare the
parsed trees after normalizing only their root names; document text is never
rebranded. This default-envelope change is a declared major-release change, not
an assertion that old XPath consumers remain compatible without migration. The
exhaustive repository residual-match gate remains open.

The public branding endpoint now returns PVADE, PVAD and `pvad`, with primary PDF
extension `.pvadp` and additive `nativeFileExtensions` containing `.pvadp` and
`.pvads`. Both generic and canonical API paths and the old prefix alias expose
the current identity. Explicit legacy metadata fields retain PVAD. The singular
extension is a primary-format compatibility field, not a claim that all projects
share one format.

## Importer and API Text Review

New importer diagnostics, conversion metadata and public API descriptions use
PVAD; account-facing messages use PVADE. Diagnostic IDs such as [compatibility entry `compat-8353ee18cf9e`](reference/compatibility-identifiers.v1.json),
[compatibility entry `compat-45790eef02cc`](reference/compatibility-identifiers.v1.json) and the joined trust/invoice codes are unchanged. Old files,
signed reports, user-provided text and encrypted payloads are not rewritten.

`runtime-brand-contract.test.mjs` checks all importer C# sources, allowing only
the bounded Markdown/chart diagnostic families and the exact OCR activity alias
as standalone PVAD strings. Its API check records concrete literals by file for
authentication schemes, cookie readers, Data Protection purposes, request and
response header aliases, MCP authorization, explicit legacy metadata, SMTP
correlation and the OpenAPI explanation of legacy extensions. The test rejects
new standalone branding outside those entries and rejects stale exceptions.
It does not exempt whole directories or classify arbitrary lowercase matches.

Current fidelity/parity report generators and legal readiness console headings
use PVAD. Historical generated evidence, legal placeholders and explicit backup
confirmation phrases remain original. Regenerating a report title is not
permission to modify its underlying signed or pinned evidence.

Pending release-fragment summaries are editable preparation material, not published
history: twenty-nine current summaries still using PVAD were updated to PVAD/PVADE.
The `release-1.1.0-verification` fragment explicitly records the actual historical
PVAD 1.1.0 publication and keeps that name under an exact-text regression exception.
Fragment IDs, feature
IDs, impact and security-review fields remain unchanged. The published release
manifest was not rewritten. The release dry run still requires an explicit major
confirmation and does not update VERSION, create a tag or publish anything.

## Public Metadata and Retention Follow-up

Company product-page HTML titles/descriptions and the shared Company template use
PVADE. A recursive metadata contract now covers nested pages, not only index.html.
Illustrative Demo descriptors name actual Core DTOs or explicitly use a viewer
session label; customer-provided filenames, text and sheet names are not rewritten.

Retention inventory configuration labels now use `PVAD_*`. Admin status resolves
both generations through the existing environment helper and rejects conflicts
without revealing values. Defaults, legal holds, Production approval and retention
durations are unchanged. Tests use EF InMemory for status computation, not a claim
of new PostgreSQL or deployed-backend retention validation.

See the [phase review](pvad-rename-phase-review.md) for deleted-file pairing and
36 byte-preserved external-signature evidence files. These contextual checks do
not replace review of every remaining matching line or a final staged diff.

## Workflow and Historical Release Manifest Names

`tools/deployment/pvad-deployment.mjs` reads either `pvad-releases.json` or the
historical [compatibility entry `compat-7d5159733b8b`](reference/compatibility-identifiers.v1.json) inside the explicitly selected release checkout.
Exactly one is required; ambiguity is rejected. This preserves old immutable tags
without adding a legacy manifest copy to the current source tree or rewriting
release entries. Artifact-family checks still reject incomplete or mixed archives.

Ephemeral CI artifact labels, local SBOM image tags and temporary PR-body files
now use PVAD. The four workflow concurrency groups now use canonical PVAD names
in local source, with their original cancellation rules. Their activation still
requires draining old runs across applicable branches, as described in the
[lock cutover](pvad-rename-release-approvals.md#workflow-lock-cutover).
External GHCR repositories and protected GitHub environments retain their existing
names. They are not merely display text: changing them can publish elsewhere or
bypass environment approval configuration. A point-in-time read-only inspection
does not approve remote changes or safely serialize old and new lock generations.

## Active Guides Versus Historical Evidence

Current integration prose uses PVAD/PVADE, but the code-workspace and asset guides
intentionally name old markers and extensions when explaining legacy readers.
The Nutrient Java inventory retains vendor identifiers, dates and limitations;
changing its PVAD target wording to PVAD does not certify new provider coverage.
The screenshot inventory now labels its first findings as an initial historical
audit, distinct from the subsequent completed recaptures.

Current public-guide packaging explicitly includes the chart/code guides without
copying operator mail content. Retained external-signature corpus wrappers and
reports remain evidence, not a reason to globally replace names in signed or
hash-pinned material. Their separate byte/provenance rules still apply.
