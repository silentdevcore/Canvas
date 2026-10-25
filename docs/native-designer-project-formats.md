# Native Designer Project Formats

PVADE Designer uses two portable, editable project formats:

| Format | Purpose | Media type |
| --- | --- | --- |
| `.pvadp` | PVADP PDF Project | `application/vnd.pvad.pvadp+zip` |
| `.pvads` | PVADS Spreadsheet Project | `application/vnd.pvad.pvads+zip` |

These files preserve editable Designer state. They are not substitutes for a final PDF,
XLSX, CSV, or other delivery format. No generic `.pxa` project format is defined;
new exports use `.pvadp` and `.pvads`.

### Legacy Project Compatibility

Existing `.pxap` and `.pxas` files remain readable in the matching Designer.
Their original magic (`PXA-PXAP-1` / `PXA-PXAS-1`), format ID, MIME entry and
`minimumPxaVersion` are validated as a complete legacy envelope. Mixed old/new
identity fields and mismatched MIME entries are rejected. The canonical envelopes
use `PVAD-PVADP-1` / `PVAD-PVADS-1`, `pvadp` / `pvads`, and
`minimumPvadVersion`. Equal minimum-version aliases are accepted; conflicting
aliases are rejected. These application media types are not claimed as IANA registrations.

Opening never rewrites the original upload. Saving downloads a new canonical
project with freshly calculated integrity hashes. Keep the original file for
recovery. A compatibility review warns before opening a legacy envelope and lets
the user keep the current document instead. It explains that Save project produces
a new PVADP or PVADS copy; document-schema upgrades retain their existing report.

Canonical schemas are `docs/schema/pvad-project-manifest.schema.json` and
`docs/schema/pvad-code-workspace.schema.json`. Historical schemas remain available
for legacy validation. The legacy manifest cannot simply alias the new schema,
because its envelope constraints intentionally differ. Audit event names and
`PXAPROJECT...` diagnostics remain stable during this migration.

Unlike PostgreSQL template persistence, a project file is a user-controlled portable
snapshot. It has no server revision, organization ownership, autosave history, published
version state, or authority over an existing template record. JSON exports remain useful
for interoperability but do not provide the package envelope and binary asset integrity.

## Open and save

Use **Open project** and **Save project** in the PDF or Spreadsheet Designer toolbar.
The two formats are intentionally separate: PDF Designer rejects `.pvads`, and
Spreadsheet Designer rejects `.pvadp`.

Opening performs these steps before replacing the current document:

1. Upload the package to the matching authenticated project endpoint.
2. Validate its extension, package marker, manifest, versions, paths, sizes, hashes,
   document model, and image signatures.
3. Import packaged images into the active organization's Asset Store with fresh IDs.
4. Apply the validated project as one undoable Designer transaction.

The source file stays portable. It never grants access to the organization from which
it was exported and never carries a usable tenant asset ID.

## Package layout

Both formats are ZIP-compatible packages with deterministic entry ordering:

```text
mimetype
manifest.json
document.json
components/code-workspace.json          # optional, PVADP only
assets/images/<stable-package-asset-id>.png
assets/fonts/<stable-package-asset-id>.woff2
assets/previews/<stable-package-asset-id>.webp
assets/sources/<stable-package-asset-id>.pdf
assets/data/<stable-package-asset-id>.json
```

`mimetype` is the first uncompressed entry. `manifest.json` records the format and
schema versions, minimum PVAD version, document identity, locale, timestamps, asset
index, per-entry SHA-256 checksums, a canonical content hash, required Designer
capabilities, and bounded compatibility diagnostics. `document.json` contains
`DesignExportDto` for PVADP or the PVAD Workbook model for PVADS.

PVADP may include `components/code-workspace.json`. This independently versioned,
`confidential-source` component preserves JSON, C# Model, C# PDF, and Base64 drafts
plus the canonical design and source map. It is limited to 10 MiB, covered by the
manifest checksums, and validated as inert JSON. Inspecting or opening a project never
compiles or executes its source. PVADS rejects this PDF-specific component.

Each asset descriptor records a stable package-local ID, kind, canonical path, safe
filename, MIME type, byte length, and SHA-256. The supported kinds are `image`, `font`,
`preview`, `source`, and `data`. Current Designer flows externalize PNG and JPEG images
instead of retaining Base64 in `document.json`. On open, PVAD validates the bytes and
creates new organization-owned Asset Store IDs. Older development packages with flat
image or font paths remain readable; all newly written packages use typed folders.

## Capabilities and review diagnostics

The writer derives stable capability IDs from the editable content instead of relying
on claims supplied by a client. Examples include `pdf.per-page-layouts`, `pdf.charts`,
`pdf.accessibility`, `spreadsheet.formulas`, and `spreadsheet.print-layout`. The open
response compares these requirements with the capabilities available in the current
application. Unknown requirements do not disappear silently: the Designer warns the
user before replacing the active document and identifies every missing capability.

The manifest also carries bounded, non-executable diagnostics. Imported Spreadsheet
external-workbook formulas, preserved macro or VBA metadata, and provider-specific
extension metadata produce review warnings. Macro and provider data remain inert
extension data; opening a PVADS never executes it. Existing importer diagnostics survive
the native project roundtrip with their code, severity, message, and source. Informational
diagnostics do not require review, while warning and error diagnostics do.

The API open response exposes this decision through `compatibility.requiresReview`,
`compatibility.missingFeatures`, and `compatibility.diagnostics`. Compatibility metadata
is advisory before apply; package integrity, schema compatibility, and security rules
remain mandatory and can still reject the file.

## Security model

The reader rejects:

- renamed or mismatched PVADP/PVADS packages;
- absolute, parent-relative, duplicate, non-normalized, or control-character paths;
- symbolic links, encrypted ZIP entries, unsupported compression, and nested archives;
- missing, undeclared, tampered, or MIME-spoofed assets;
- asset kinds that do not match their MIME type or canonical typed folder;
- invalid JSON, excessive JSON depth, future incompatible major versions, and
  document complexity above configured limits;
- tenant asset references, external image URLs, document passwords, password hashes,
  and Spreadsheet protection passwords.

Current defaults limit a compressed package to 25 MiB, expanded content to 100 MiB,
an individual entry to 20 MiB, and the archive to 256 entries. PDF pages/elements and
Spreadsheet sheets/cells/formulas have additional model limits. Raster assets are
decoded only after their headers have passed validation and are limited to 100 million
pixels each. Parsing has a 15-second processing budget and also stops when the client
cancels the request. These are defensive maximums, not recommended project sizes.

The format reader and writer preserve unknown properties in the manifest and supported
document extension locations. The Designer also carries document extensions through
editing and project export. This applies to the PDF document, page, page-settings, and
element contracts, and to Spreadsheet workbook, sheet, image, and cell contracts.
Extension data is limited to 1,024 properties and 1 MiB per validated manifest or
document area. Unknown data never overrides a known typed property and is not
interpreted as executable code.

SVG assets reject scripts, event handlers, entities, foreign objects, and external
URLs. Source PDFs and bounded JSON data are inert resources: opening a project never
executes them or resolves package-provided URLs. Executables, macros, external packages,
and nested archives are not accepted as native project assets.

Rich Text remains editable source content, but it is never trusted as active HTML.
Designer rendering and server-side HTML export apply matching allowlists for supported
formatting elements, attributes, and CSS properties. Scripts, embedded frames, objects,
event handlers, active CSS, unsafe URL schemes, and protocol-relative links are removed.
Malformed markup is displayed as encoded text instead of being interpreted. PDF and
other non-HTML exporters do not execute markup.

Document identity, PDF metadata, element content, and Rich Text have explicit length
and control-character limits. Asset filenames must be normalized single-segment names
without control or invisible format characters. Invalid metadata or filenames reject
the package with a stable diagnostic rather than being silently rewritten. These rules
also apply while writing a project, preventing PVAD from producing a package it would
later refuse to open.

Code Workspace content is not executed while inspecting or opening a package. Native
project files do not contain authentication cookies, API keys, signed object URLs, or
server filesystem paths. Bounded forward-compatible extension data is recursively
checked for credential-like property names, tenant identifiers, network URLs, and local
filesystem locations before a package is written or accepted.

## API

All routes require a Designer session, active organization, entitlement, rate limit,
and the matching PDF or Spreadsheet permission.

| Operation | PDF | Spreadsheet |
| --- | --- | --- |
| Save | `POST /api/pvad/v1/designer/projects/pdf/export` | `POST /api/pvad/v1/designer/projects/spreadsheet/export` |
| Open | `POST /api/pvad/v1/designer/projects/pdf/open` | `POST /api/pvad/v1/designer/projects/spreadsheet/open` |
| Validate | `POST /api/pvad/v1/designer/projects/pdf/validate` | `POST /api/pvad/v1/designer/projects/spreadsheet/validate` |
| Inspect | `POST /api/pvad/v1/designer/projects/pdf/inspect` | `POST /api/pvad/v1/designer/projects/spreadsheet/inspect` |

Open, validate, and inspect accept multipart field `file`. Save accepts the matching
canonical JSON document and returns the native media type with a sanitized download
name. Rejections use Problem Details and a stable `PXAPROJECT...` diagnostic code.
PDF Designer uses `POST /api/pvad/v1/designer/projects/pdf/export-with-workspace` when
an optional Code Workspace is included. Existing clients remain compatible with the
document-only export route.
Inspect reads and validates the envelope, manifest, declared paths, sizes, versions,
and integrity metadata without expanding document or asset payloads. It does not prove
payload checksums; use Validate or Open when full content integrity is required.

Every export, open, validate, and inspect attempt writes a tenant-scoped audit event.
Successful and rejected operations use `designer.projects.<format>.<operation>` actions.
Audit details are deliberately limited to format, operation, package byte count, asset
count, review status, and a stable rejection diagnostic. They never contain the upload
filename, document title, element or cell content, asset bytes, source path, or project
package. Requests without an active organization and user are rejected before project
processing. Designer authentication, entitlement, permission, CSRF, rate-limit, and
cancellation policies remain enforced by the common API pipeline.

When a project is opened over a selected Designer target, the client sends only the
target's stable IDs. The importer preserves package IDs that do not collide and
deterministically remaps colliding document, page, element, style, sheet, and image IDs
together with their internal references. A PDF project opened over a persisted template
retains that template's persistence identity; its next autosave therefore uses the current
revision and `If-Match` contract. Concurrent server changes return `409 Conflict` instead
of silently replacing a newer draft. A project opened without a persisted target remains
tenant-neutral and is saved as a new template.

The generated OpenAPI contract assigns stable operation IDs to all eight project
operations. Open, Validate, and Inspect describe `file` as a required binary multipart
field. The two Open operations additionally accept `reservedIds` as an optional
JSON-array string. SDK and embedded-engine callers can read directly from a `Stream`;
`WritePdfAsync` and `WriteSpreadsheetAsync` write a deterministic package to a
caller-owned writable stream without closing it.
For local, CLI, and server file paths, `WritePdfFileAtomicallyAsync` and
`WriteSpreadsheetFileAtomicallyAsync` write to a same-directory temporary file, flush
it, and replace the destination only after success. Browser download APIs cannot promise
filesystem-level atomic rename, so the Designer first receives a complete validated
response blob and only then starts the download.

Small public fixtures are available under `docs/fixtures/native-projects/`. They contain
only synthetic content and include reproducible SHA-256 hashes. Regenerate them with:

```bash
dotnet run --project tools/native-project-fixtures/PVAD.NativeProjectFixtures.csproj
```

## Version compatibility

Opening and upgrading are separate operations. PVAD first validates the complete original package,
then applies registered document-schema migrations to an in-memory copy. Upgrade steps are ordered,
forward-only, deterministic, and specific to PDF or Spreadsheet projects. PVAD never downgrades a
project and never modifies the source file while opening it.

When an upgrade path is used, the open response includes `upgradeReport` with the source and target
schema versions, every applied step, the original package SHA-256, loss indicators, and diagnostics.
The Designer requires review before applying an upgraded project. The original package remains the
recovery source until the user explicitly saves the upgraded project as a new PVADP or PVADS file.
Applying the imported design is one undoable transaction, so the previous active PDF or Spreadsheet
state can also be restored before save.
If no complete reviewed path exists, PVAD rejects the package instead of partially upgrading it.

The package envelope and embedded document schema have independent `major.minor`
versions. PVAD 1.1 writes envelope `1.0` and document schema `1.0`.

- A lower minor version within the current major is backward compatible and can be
  opened without rewriting the source package.
- A higher minor or major version requires a newer PVAD application and fails with
  `PXAPROJECT014` instead of being opened partially.
- A previous major version is accepted only when PVAD ships an explicit, pure upgrade
  path. Such an upgrade must produce a new package and must never overwrite or
  downgrade the source file in place.
- Schema versions use canonical `major.minor` notation. The minimum application
  version uses canonical Semantic Versioning. PVAD compares that minimum with the
  running product assembly version before reading document content; a package that
  requires a newer application fails with `PXAPROJECT014`.

The compatibility decision is centralized in `PvadProjectVersionPolicy`, so API,
Designer, future SDKs, and upgrade steps use the same rules. No major-version upgrade
is registered for schema `1.0` because there is no historical native format to migrate.
Compatibility tests cover current PDF and Spreadsheet packages, unsupported historical
schemas, future schema and application versions, malformed JSON, truncated archives,
checksum tampering, compressed-size limits, and hostile archive structures.

## Failure and recovery

The server creates the complete package in memory and returns it only after validation;
it does not expose a partially generated server file. Browser download and final rename
behavior is controlled by the host platform. An interrupted or truncated download must
be retried and will fail package validation if opened.

Open validates the complete uploaded package before replacing the active document. The
previous Designer document remains available as one Undo step after a successful open.
If package validation or image import fails, the active document remains unchanged. Keep
the original project file until an upgraded project has opened and been saved successfully.

For large projects, the Designer reports upload percentage followed by package validation,
asset storage, and apply phases. Uploads can be cancelled while data is still being sent.
Stable `PXAPROJECT...` diagnostics remain visible after a failure instead of disappearing
with a toast. Projects with unavailable features or warning diagnostics pause at a review
step: the user must explicitly open the project or keep the current document. This status
surface is keyboard accessible, scrollable, and adapts to narrow mobile viewports.

Published schema files:

- [Project manifest schema](schema/pvad-project-manifest.schema.json)
- [PDF design schema](schema/design-export.schema.json)
- [Spreadsheet workbook schema](schema/pvad-workbook.schema.json)

Contract tests roundtrip every current PDF Designer element type and a fully populated
PDF contract covering mixed page layouts, styles, bindings, localization, RTL,
accessibility, forms, charts, document settings, and forward-compatible extensions. A
separate fully populated Workbook fixture covers sparse typed cells, formulas and cached
values, styles, names, merges, filtering, frozen panes, print setup, validation,
conditional formatting, images, and extension data. These tests cover the current
canonical models; Spreadsheet tables and charts remain outside PVADS until they become
part of the canonical Workbook contract.

## Current boundaries

- Native packages currently externalize PNG and JPEG element or worksheet images.
- The envelope supports typed fonts, previews, source PDFs, and JSON data, but current
  Designer save flows do not yet discover and attach every such resource.
- PDF background images and Code Workspace snapshots are not yet package components.
- Spreadsheet external workbook links are preserved as formulas and require review;
  PVAD does not fetch or refresh the referenced workbook while opening a project.
- Macro/VBA and provider-specific extension metadata remain inert and are reported;
  executable macro packages are not accepted as assets.
- Package signing and project encryption are later milestones. Final PDF encryption
  settings may be represented, but passwords and password hashes are never saved.
- Final rendered PDF or Spreadsheet bytes are not authoritative project content.
