# Existing PDF Ownership Boundaries

This document defines which PXA component owns each stage of an existing-PDF
workflow. It is a product contract, not an implementation suggestion.

## Component Responsibilities

### `PXA.Importer`

`PXA.Importer` owns reading and changing existing PDF object graphs. Its typed
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

The importer must not silently flatten an existing PDF into Designer elements
and describe the result as an exact edit. Scene-graph reconstruction is a
separate, potentially lossy conversion with explicit fidelity diagnostics.

### `PXA.Pdf`

`PXA.Pdf` owns new-document generation. Its public API creates pages, content,
forms, annotations, navigation, accessibility structures, encryption, and
compliance output from typed PXA inputs.

It is responsible for:

- deterministic serialization of newly generated documents;
- generator-side PDF/A and PDF/UA preflight;
- new-document encryption and signature preparation;
- public drawing, layout, form, annotation, and document APIs.

`PXA.Pdf` does not parse arbitrary existing object graphs. Existing-document
editing reaches the generator only through an explicit bridge that reports any
semantic loss.

### `PXA.Infrastructure.Pdf`

`PXA.Infrastructure.Pdf` adapts application-level design documents to
`PXA.Pdf`. It owns rendering orchestration, font and asset resolution, chart
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
  `PXA.Infrastructure.Pdf` and `PXA.Pdf`. It is never an exact existing-PDF edit
  unless the conversion report says `exact`.

No component may choose a weaker strategy after a requested strategy fails.
Signing, redaction, and compliance conversion fail closed.

## Data Flow

```text
Existing PDF bytes
  -> PXA.Importer parse and trust inspection
  -> typed object graph
  -> bounded mutation
  -> incremental update or full rewrite
  -> reparse and post-save validation
  -> output bytes and typed evidence

DesignExportDto
  -> PXA.Infrastructure.Pdf planning and resource resolution
  -> PXA.Pdf generation
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

