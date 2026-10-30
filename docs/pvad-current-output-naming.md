# Current Output Naming

New document output uses technical PVAD names while previous-generation inputs
remain readable. This follow-up does not rotate authentication, change monitoring
resources or alter original evidence.

## Chart Recognition

Embedded editable charts use `pvadMetadata` as the recognition source. The central
backend and Designer normalizers also accept the earlier spelling. Vector and
raster sources remain `pdfVector` and `pdfRaster`. Normalization keeps the typed
recognition source and its optional style projection consistent without changing
series, confidence, geometry or the original draft.

## Project Audit Details

Project audit actions and the existing `formatId` are stable operational
contracts, independent of the downloaded package generation. Newly written
details additionally contain `canonicalFormatId`: `pvadp` for PDF projects and
`pvads` for Spreadsheet projects. Consumers should use this additive field for
current format presentation while retaining historical action filters. Older
rows can omit the new field and must remain readable. Existing rows are never
backfilled by this source change.

## Search Boundaries

The residual guard reads tracked regular files only. It excludes private IDE
paths, untracked notes, symlinks and ignored outputs. Known source extensions are
still scanned when they contain literal null bytes; binary assets are reviewed
separately. An exact reviewed path and hash are required for retained findings.

The [follow-up checklist](../checklists/PVAD.Repository-Naming-Followup.md) records
implementation and validation separately from historical release gates.
