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

New PDFs store the editable-chart envelope under `PVADChartsV2`. The importer
also reads the previous metadata key. When both keys exist, their payloads must
be identical; conflicting or malformed payloads keep the original graphics and
produce the existing invalid-metadata diagnostic instead of reconstructing a
potentially different chart. Envelope hashing and payload limits are unchanged.

## Redacted Image Resources

Newly rewritten images and their masks use the `PVADRedacted` marker. New image
resource names use the `_PVADRedacted` suffix. Rewriting a previously marked
image removes its former marker and emits the current one; existing PDFs are
not modified merely by loading them.

Verification accepts the previous marker when the canonical marker is absent.
A marker never proves successful redaction on its own: covered image and mask
samples must still be zero and all existing residue checks remain enforced.

## Internal Runtime Names

The five internal step-up authorization policies use canonical technical names;
their required permissions and recent-authentication checks are unchanged. The
Designer migration middleware recognizes the canonical versioned route after
legacy normalization, including callers with domain/kind-specific permissions.
This does not waive organization, entitlement, provider or CSRF checks.

The MCP image creates and runs as the non-root `pvad` user with matching group
and distribution-file ownership. Command aliases and protocol readers remain
separate compatibility contracts. Synthetic Admin screenshot examples use
canonical license and key labels; existing issued licenses and keys are unchanged.

MCP resource titles and descriptions use current names for both canonical and
compatibility resource listings. Previous resource URIs and schema payloads
remain exact contracts; a display-name correction does not migrate their formats.
The [source acceptance review](pvad-rename-source-acceptance.md) records runtime
evidence separately from retained identifiers and operational gates.

## Project Audit Details

Project audit actions and the existing `formatId` are stable operational
contracts, independent of the downloaded package generation. Newly written
details additionally contain `canonicalFormatId`: `pvadp` for PDF projects and
`pvads` for Spreadsheet projects. Consumers should use this additive field for
current format presentation while retaining historical action filters. Older
rows can omit the new field and must remain readable. Existing rows are never
backfilled by this source change.

## Parser Reports And Corpus Generators

New version-2 parser-fuzz reports write the `pvad` parser ID. Their read-only
reader accepts the former ID and normalizes it in memory, rejecting conflicting
aliases and incomplete outcome sets. It preserves mutation hashes, statuses,
durations, limits, diagnostic IDs and disagreement rules. Historical reports
are not rewritten.

New synthetic Trust and PAdES generation uses canonical authors, certificate
subjects, file IDs and classified-profile field names. PAdES readers accept
both generated manifest generations and preserve actual file names and hashes.
The negative-evidence builder rejects mixed or incomplete evidence for one case.
The existing signed PDFs, certificates, validation XML and approved manifests
remain original evidence. Original encrypted-fixture passwords are unchanged.

See [parser reports](../tools/pdf-parser-fuzzer/README.md) and
[PAdES corpus generation](../tools/pdf-trust-pades-interop/README.md) for commands
and the distinction between reader compatibility and independent validation.

## Monitoring Display And Deployment Defaults

Dashboard titles use PVADE and display tags use PVAD. Existing dashboard and
datasource UIDs, metric selectors, provisioned provider/folder names and folder
permissions remain operational bindings. Two display-tag transitions are
recorded at exact JSON pointers in the current review ledger; the original
JSON audit remains unchanged. The guard verifies both former audited hashes
and exact current values, and never permits transitions of protected evidence.

The local deployment workflow now defaults to the canonical image family.
Historical image families remain an explicit selection for their immutable
release; archive naming never determines the image family. Candidate generation
accepts the prior choice contract and emits the current one idempotently.
Protected environments, reviewers, secrets, publisher selection, lock groups and
token permissions are unchanged. Source tests and stubbed image inspection do
not publish images, authorize a cutover or certify a deployed environment.

## Search Boundaries

The residual guard reads tracked regular files only. It excludes private IDE
paths, untracked notes, symlinks and ignored outputs. Known source extensions are
still scanned when they contain literal null bytes; binary assets are reviewed
separately. An exact reviewed path and hash are required for retained findings.

Resolved review records keep their stable inventory positions so the historical
JSON audit pointers do not change. They no longer allow a previous identity:
the guard requires a clean tracked regular file with its exact reviewed hash.
Reintroduced names, changed bytes, missing files, private files, untracked files
and symlinks cannot satisfy a resolved record.

The [follow-up checklist](../checklists/PVAD.Repository-Naming-Followup.md) records
implementation and validation separately from historical release gates.
The [remaining code checklist](../checklists/PVAD.Remaining-Code-Naming-Cleanup.md)
tracks internal corrections, canonical writers and separately deferred protocols
and operational cutover.
