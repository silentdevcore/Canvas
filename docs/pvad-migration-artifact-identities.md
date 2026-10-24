# PVAD Migration Artifact Identities

## Current Writers

The provider catalog and current `/api/pvad/v1/migrations` responses use:

| Domain / kind | Artifact ID | Media type | Extension |
| --- | --- | --- | --- |
| PDF / code | `pvad-csharp` | `text/x-csharp` | `.cs` |
| Spreadsheet / code | `pvad-spreadsheet-csharp` | `text/x-csharp` | `.cs` |
| PDF / viewer | `pvad-viewer-package` | `application/vnd.pvad.viewer+json` | `.pvadviewer.json` |

Viewer packages retain schema version 1 and use `kind: pvad.pdf-viewer-package`.
The existing `canonicalKind`, `canonicalId`, `canonicalMediaType` and
`canonicalFileExtension` projections remain available. Code content, provider
keys, permissions, CANMIG diagnostics and conversion behavior are not renamed
by changing artifact identities. Neutral `design` JSON remains unchanged.

## Compatibility Boundary

For one major release, `/api/pxa/v1/migrations` adapters retain the original
primary artifact tuples and Viewer `kind: pxa.pdf-viewer-package`. They include
the canonical projections. The middleware's internal request marker preserves
this behavior after routing to the canonical path; no client header can request
that internal marker. The static provider registry remains canonical and is not
mutated when a legacy request is served.

The shared resolver accepts only complete known old or current tuples. It does
not infer support from a plausible ID or a supplied `canonicalId`. Mixed Viewer
IDs, media types and extensions remain unrecognized. Unknown and neutral
artifacts are not silently reclassified. Existing saved packages and source
fixtures are not rewritten.

## Designer Downloads And Imports

PDF and Spreadsheet Code Migration select the correct code artifact by domain,
ID, media type and extension, not its position in the result array. Complete
legacy code tuples remain accepted. The selected C# text is displayed and
downloaded verbatim as one `migration.cs` file with `text/x-csharp`. Invalid or
cross-domain output shows the existing localized conversion-failure message.

Report Designer and Nutrient Viewer-to-Designer migration still consume a neutral
`design` artifact. Their Open in Designer and PDF-preview workflows are unchanged.
The low-level Viewer package contains settings, an integration module or
annotation/form payloads, not the original PDF. It is not an editable design JSON
file. There is no standalone Viewer-package upload in the current Designer;
use the existing original-PDF-plus-source migration for an editable design.
This rename does not introduce an unused importer or restore a separate UI.

## Checksums And Release

Checksums retain their original meaning: source, normalized data and generated
content, not a signature over the complete response envelope. Instant JSON
sidecars already contain a per-conversion `exportedAt` timestamp; two independent
conversions may therefore have different normalized/generated hashes. Identity
projection itself does not rewrite those fields or their checksum calculations.

The release fragment declares Major impact because exact-match consumers of
current primary identities must migrate. Existing route adapters remain
compatible. `VERSION` and the published release manifest are unchanged.

## Validation Scope

- Isolated controller and serializer tests cover every provider's catalog,
  current/legacy code and Viewer output, aliases and rewritten legacy routing.
- Strict resolver tests reject mixed, neutral and unknown tuples.
- Designer tests cover domain selection, old/current code tuples and actual
  download names, media types, contents and single-download behavior.
- Documentation contracts and production builds verify the published guidance.
- Full hosted API/PostgreSQL, authenticated browser and deployed SDK consumer
  checks remain separate gates; isolated tests are not production approval.

See [HTTP compatibility](pvad-http-compatibility.md) and
[rename tracker](../checklists/Rename-PXA-To-PVAD.md).
