# Existing PDF Ownership Boundaries

This document defines which PVAD component owns each stage of an existing-PDF
workflow. It is a product contract, not an implementation suggestion.

## Component Responsibilities

### `PVAD.Importer`

`PVAD.Importer` owns reading and changing existing PDF object graphs. Its typed
object model represents dictionaries, arrays, names, strings, numbers,
references, streams, cross-reference data, trailers, object streams, page trees,
and revision chains.

It is responsible for:

- bounded parsing and diagnostics;
- password and certificate based decryption;
- trust inspection and signature validation;
- incremental updates when preserving signed source bytes is required;
- full rewrites for sanitization, compliance conversion, or irreversible
  redaction;
- preserving reachable unknown objects that are not affected by an operation;
- refusing an operation when the required object semantics cannot be proven.

The first public page-editing surface is `PdfPageEditor`. It accepts one-based
move and delete operations against an existing PDF, preserves the original page
dictionaries, content streams, resources, annotations, and unknown reachable
objects, and writes one fresh revision. It materializes inherited page
attributes before flattening the edited page tree. Deleting the final page or a
page that remains reachable through navigation, links, annotations, or another
PDF object is rejected rather than producing a dangling reference or retaining
hidden page content.

`PdfPageComposer` imports a selected page by cloning its complete transitive
object graph into a collision-free target object-number range. It remaps page,
resource, content, font, image, annotation, and form references, then rebuilds
the target page tree in one full rewrite. `PdfEditableDocument` adds a
stateful facade for migrated SDK workflows, including file and stream loading,
runtime status results, page move/delete, blank insertion, cloning, merging,
AES-256 output configuration, and saving.

The exact page-editing surface also supports the PDF `CropBox` on a selected
one-based page. `PdfPageEditor` can set, read, or remove that boundary while
leaving the page dictionary, resources, and content streams intact; CropBox
coordinates use the existing PDF point and bottom-left coordinate contract.
The migration facade preserves Nutrient's `Left, Top, Right, Bottom` parameter
order and converts the active point, millimeter, centimeter, or inch unit in
both directions; PDF storage remains `[left, bottom, right, top]`.
This operation is intentionally limited to non-encrypted input. The
`PdfEditableDocument` facade exposes the corresponding
`SetPageBox`/`GetPageBox`/`RemovePageBox` operations through the typed
`PdfEditablePageBox.CropBox` value.

`PdfPageComposer.Split` and `ExtractPages` create independent documents from
explicit one-based page ranges. The selected page graph is cloned and internal
cross-page references are remapped; references to pages outside the selected
set, malformed or duplicate ranges, and encrypted sources are rejected. A
signed source requires explicit acknowledgement that the operation invalidates
its signature. `AppendPages` clones all selected pages as one graph and appends
them to an existing target. Nutrient's two-argument
`ClonePages(source, pageRange)` therefore retains destination pages, appends in
range order, and selects the final appended page.

Path-based multi-document merge is supported. Nutrient's attachment-specific
merge options intentionally fail closed until catalog-level embedded-file
composition can preserve their exact semantics.

Cross-document composition rejects encrypted inputs and pages that reference a
different source page. These cases require password-aware source sessions or a
multi-page relationship remapper and must not be approximated by copying page
dictionaries or leaving dangling destinations.

The importer must not silently flatten an existing PDF into Designer elements
and describe the result as an exact edit. Scene-graph reconstruction is a
separate, potentially lossy conversion with explicit fidelity diagnostics.

### Migration evidence and Trust handoff

The Nutrient/GdPicture code migrator uses Roslyn syntax and semantic information
for the supported detection paths. An SDK symbol remains identifiable when the
call is reached through a supported alias, field, or method parameter; the
Trust evidence retains the source location of the invocation. This is evidence for
review and source navigation, not a promise that every wrapper, extension,
async, or dependency-injected pattern is understood.

Official Nutrient signature and redaction calls are externalized as bounded PVAD
Trust review evidence. The evidence records the recognized operation, source
location, Trust status, and required next action. It does not execute signing,
certificate validation, preflight, apply, or irreversible redaction as part of
the migration handoff. PFX paths, passwords, private keys, certificate
material, and removed text are never copied into generated Trust review code,
diagnostics, evidence, or logs. A visual redaction mark remains a review
item and is never reported as removed content.

This boundary deliberately separates semantic detection from Trust execution:
the migrator may point an operator to the PVAD Trust workflow, while the Trust
pipeline remains responsible for credentials, policy, validation, full-rewrite
decisions, and post-operation evidence.

### `PVAD.Pdf`

`PVAD.Pdf` owns new-document generation. Its public API creates pages, content,
forms, annotations, navigation, accessibility structures, encryption, and
compliance output from typed PVAD inputs.

It is responsible for:

- deterministic serialization of newly generated documents;
- generator-side PDF/A and PDF/UA preflight;
- new-document encryption and signature preparation;
- public drawing, layout, form, annotation, and document APIs.

`PVAD.Pdf` does not parse arbitrary existing object graphs. Existing-document
editing reaches the generator only through an explicit bridge that reports any
semantic loss.

### `PVAD.Infrastructure.Pdf`

`PVAD.Infrastructure.Pdf` adapts application-level design documents to
`PVAD.Pdf`. It owns rendering orchestration, font and asset resolution, chart
rendering, localization, data binding, page-layout planning, and export
integration.

It must not become a second existing-PDF parser or duplicate the importer's
security and revision logic.

## Save Strategy Contract

The caller must select a save strategy explicitly:

- **Incremental update** preserves original bytes and appends one validated
  revision. Use it for supported form, annotation, timestamp, and signing
  operations that must preserve existing signatures.
- **Full rewrite** emits only the validated reachable object graph. Use it for
  irreversible redaction, sanitization, encryption replacement, and compliance
  conversion. Signed input requires explicit acknowledgement of invalidation.
- **Regeneration** converts a reconstructed design through
  `PVAD.Infrastructure.Pdf` and `PVAD.Pdf`. It is never an exact existing-PDF edit
  unless the conversion report says `exact`.

No component may choose a weaker strategy after a requested strategy fails.
Signing, redaction, and compliance conversion fail closed.

## Data Flow

```text
Existing PDF bytes
  -> PVAD.Importer parse and trust inspection
  -> typed object graph
  -> bounded mutation
  -> incremental update or full rewrite
  -> reparse and post-save validation
  -> output bytes and typed evidence

DesignExportDto
  -> PVAD.Infrastructure.Pdf planning and resource resolution
  -> PVAD.Pdf generation
  -> output bytes and generator diagnostics
```

The bridge from an imported PDF to `DesignExportDto` is intentionally outside
the exact object-editing path. It carries fidelity, unsupported-feature, and
review diagnostics and retains the original source where required.

## Security Invariants

- Parsing, validation, mutation, signing, and serialization remain separate
  typed operations.
- The source remains unchanged until post-save validation succeeds.
- Unknown reachable objects survive operations that do not affect them.
- Unsupported crypt filters, content encodings, signature coverage, or
  redaction carriers block the operation.
- Passwords, keys, document content, and removed values never enter telemetry
  or audit details.
- A metadata flag or visual appearance alone never proves encryption,
  signature validity, archival conformance, accessibility, or redaction.
