# PXA PDF Specialized Features Roadmap

## Purpose

This checklist is the central source of truth for specialized PDF domains that
are not covered completely by the PDF Trust, provider-gap, Viewer, Importer, or
Designer roadmaps. It prevents newly discovered PDF capabilities from being
tracked as disconnected tasks in several files.

Detailed implementation remains in focused feature checklists. This roadmap
owns priority, lifecycle state, cross-checklist references, acceptance evidence,
and duplicate-cleanup decisions.

## Status Model

- [ ] Use only `discovered`, `planned`, `inProgress`, `beta`, `validated`,
  `complete`, or `deferred` as roadmap states.
- [ ] Record a stable feature ID, owner areas, business audience, dependencies,
  implementation checklist, Documentation page, release fragment, fixtures, and
  evidence for every promoted feature.
- [ ] Distinguish generated-PDF support, existing-PDF preservation, semantic
  editing, Viewer support, and independent validation.
- [ ] Never infer standards compliance from metadata, a file extension, or a
  successful render.
- [ ] Keep unsupported executable content disabled by default.

## Sol and Luna Delivery Model

- [ ] Sol owns architecture, public contracts, standards interpretation, security
  boundaries, compliance-sensitive implementation, integration review, and final
  acceptance.
- [ ] Luna receives bounded work packages only after Sol has fixed the model,
  interfaces, diagnostics, invariants, and acceptance criteria.
- [ ] Luna owns suitable UI wiring, localization, Documentation, synthetic
  fixtures, migration mappings, coverage matrices, API clients, and repetitive
  unit or contract tests.
- [ ] Sol reviews every Luna change against the original task, runs the relevant
  security and architecture checks, corrects gaps, and approves it before commit.
- [ ] Use separate branches or worktrees for concurrent Sol and Luna work; never
  let both edit the same files concurrently.
- [ ] Keep one reviewed commit per bounded work package and record its tests and
  evidence in the owning feature checklist.

| Workstream | Primary owner | Luna contribution | Sol acceptance focus |
| --- | --- | --- | --- |
| Electronic invoicing | Sol | UI, examples, fixtures, Documentation, contract tests | Profiles, XML validation, PDF/A-3 semantics, API boundaries |
| Legal PDF workflows | Sol | Viewer workflow, fixtures, export presentation | Audit integrity, evidence model, redaction and signature safety |
| Advanced forms and signing | Sol | UI, localization, non-security test coverage | AcroForm semantics, XFA boundary, identity and signing security |
| Document capture | Luna after Sol design | Capture stages, UI, fixtures, batch tests | Resource limits, worker isolation, provenance and fidelity |
| PDF/X and print production | Sol | Designer controls, fixtures, Documentation | Color, preflight, conformance, independent evidence |
| Optimization and delivery | Sol | Benchmark harness, fixture corpus, reports | Rewrite safety, signature impact, deterministic output |
| PDF/VT | Sol | Fixtures and performance harness after contracts exist | Standard model, record boundaries, high-volume correctness |
| GeoPDF | Sol | Viewer tools and synthetic maps | Coordinate semantics, preservation, measurement accuracy |
| PDF/E and 3D | Sol | Safe metadata UI and fixtures | Parser safety, preservation, proven third-party integration |
| Rich media | Sol for security | Safe display, metadata UI, fixtures | No implicit execution, sandbox and sanitization policy |
| Content provenance | Sol | Viewer presentation, Documentation, evidence fixtures | Cryptographic meaning, privacy, audit integration |

## P0 - Commercial PDF Domains

### PXA-PDF-EINV - Electronic Invoicing

Current state: `inProgress`. The focused implementation tracker is
[PXA.E-Invoicing.md](PXA.E-Invoicing.md).

- [x] Create a focused implementation checklist for ZUGFeRD, Factur-X, and
  XRechnung.
- [x] Define the first maintained product targets as Factur-X 1.09.2, ZUGFeRD
  2.5.2, and XRechnung 3.0.2 without claiming generic conformance.
- [x] Generate deterministic CII and UBL invoice XML from typed business data,
  calculate taxes and totals, and run secure structural self-recognition.
- [x] Expose authenticated typed XML generation and visible PDF/A-3 hybrid-invoice
  creation with dedicated permissions, product entitlement, bounded input, and
  output limits.
- [ ] Validate generated invoice XML against pinned profile-specific XSD,
  Schematron, code lists, totals, and business rules.
- [x] Embed invoice XML in PDF/A-3 with the correct associated-file relationship,
  MIME type, filename, metadata, and checksum.
- [ ] Import, extract, inspect, and validate invoice XML from existing PDFs.
- [x] Add bounded associated-file inspection and byte-preserving extraction for
  structurally valid PDF associated files.
- [ ] Add Designer invoice bindings, structured invoice templates, and validation
  diagnostics without coupling the PDF engine to billing logic.
- [x] Add initial Viewer presentation for invoice profile, structural and
  conformance state, diagnostics, and machine-readable download.
- [x] Add parsed invoice identifiers, dates, parties, currency, taxes, totals,
  and line items plus conservative PDF/XML consistency warnings to
  the Viewer.
- [ ] Add API, migration, Documentation, sample invoices, schema validation, and
  independent conformance evidence.
- [ ] Keep Peppol transport and national tax submission outside the PDF engine and
  document them as integration boundaries.

### PXA-PDF-LEGAL - Legal Document Workflows

Current state: `discovered`; generic comparison and audit foundations exist, but
there is no PDF legal-workflow plan.

- [ ] Create a focused implementation checklist for Bates numbering, exhibit
  stamps, production sets, comparison, and evidentiary export.
- [ ] Add configurable Bates prefixes, suffixes, ranges, padding, page selection,
  collision detection, and repeatable manifests.
- [ ] Add visual and text comparison with change regions, redline output, and
  signature/redaction awareness.
- [ ] Add case, matter, exhibit, custodian, and production-set metadata without
  embedding unnecessary confidential data.
- [ ] Produce immutable operation evidence and checksummed export manifests using
  the existing Trust audit foundation.
- [ ] Add Viewer review, compare, approve, search, download, and audit workflows.
- [ ] Validate that redaction, encryption, signatures, and retention rules remain
  intact through legal-document operations.

### PXA-PDF-FORMS - Advanced Forms and Signing Workflows

Current state: `partial`. Technical PAdES signing exists; advanced AcroForms and
business signing ceremonies remain incomplete.

- [ ] Complete text, multiline, checkbox, radio, combo, list, push button, and
  signature field creation and editing.
- [ ] Add inherited field properties, appearance regeneration, flattening,
  calculation order, bounded validation/actions, and FDF/XFDF import/export.
- [ ] Define static XFA inspection, preservation, and conversion boundaries;
  dynamic XFA authoring is not a default target.
- [ ] Add signing requests, recipient order, reminders, cancellation, expiry,
  completion state, and tenant-scoped audit trails.
- [ ] Keep cryptographic signature validity, signer identity verification,
  qualified-provider status, and legal effect as separate concepts.
- [ ] Add provider adapters only after credentials, consent, callback validation,
  data residency, and evidence retention are defined.

## P1 - Engine and Enterprise Depth

### PXA-PDF-CAPTURE - Document Capture

Current state: `partial`; OCR and image conversion exist, but a complete capture
pipeline has not been planned.

- [ ] Create a focused implementation checklist extending the existing OCR and
  image-import foundations.
- [ ] Add orientation detection, deskew, despeckle, border cleanup, contrast
  normalization, blank-page detection, and safe rotation.
- [ ] Add barcode and separator-page batch splitting with deterministic naming and
  page provenance.
- [ ] Add searchable-PDF output, OCR confidence overlays, language detection, and
  review workflows.
- [ ] Evaluate MRC compression and preserve an original-image mode for fidelity.
- [ ] Add bounded batch processing, cancellation, memory and pixel limits, worker
  isolation, and resumable job reporting.

### PXA-PDF-PRINT - Print Production and PDF/X

Current state: `discovered/deferred`; crop marks and basic color handling exist,
while PDF/X and production preflight do not.

- [ ] Create a focused implementation checklist and select one narrow PDF/X
  profile as the first supported target.
- [ ] Stage PDF/X-1a, PDF/X-3, and PDF/X-4 rather than implementing them as one
  undifferentiated claim.
- [ ] Preserve and author output intents, Separation and DeviceN colors, spot-color
  names, overprint settings, and supported transparency behavior.
- [ ] Add preflight for fonts, image resolution, ink coverage, trim/bleed boxes,
  marks, color spaces, transparency, and forbidden content.
- [ ] Add crop, bleed, registration, and color bars to Designer and backend output
  through one canonical print model.
- [ ] Add transparency-flattening and trapping only when independently validated;
  otherwise report them as unsupported.
- [ ] Add an optional print-output layer for N-up, booklet, poster, duplex, and
  imposition without changing the source document.
- [ ] Retain machine-readable independent validator evidence per supported profile.

### PXA-PDF-OPT - Optimization and Delivery

Current state: `partial`; stream compression exists, but optimization profiles
and linearization are incomplete.

- [ ] Create a focused optimizer checklist with lossless and controlled lossy
  profiles.
- [ ] Add linearization/Fast Web View with byte-range and progressive-load tests.
- [ ] Add object streams, duplicate-resource elimination, font subsetting, image
  downsampling, metadata cleanup, and unused-object removal.
- [ ] Report original size, optimized size, savings, changed resources, quality
  policy, and signature/compliance impact.
- [ ] Preserve signatures through incremental operations or require explicit
  invalidation confirmation before a full rewrite.
- [ ] Benchmark representative small, large, scanned, signed, encrypted, PDF/A,
  and malformed documents.

## P2 - Specialized Markets

### PXA-PDF-VT - PDF/VT and Variable-Data Printing

Current state: `deferred`; only the standard family is named in PDF Trust.

- [ ] Create a feature plan only after a customer-backed high-volume print case.
- [ ] Evaluate PDF/VT profiles, document-part metadata, reusable graphical content,
  record boundaries, and PDF/X dependencies.
- [ ] Define streaming data binding, restartable batches, deterministic record
  manifests, and high-volume performance gates.

### PXA-PDF-GEO - Geospatial PDF

Current state: `discovered`; RDL map migration is adjacent but does not provide
GeoPDF semantics.

- [ ] Create a feature plan after a GIS, construction, or public-sector use case is
  selected.
- [ ] Preserve geospatial dictionaries, coordinate reference systems, viewports,
  neatlines, map layers, and measurement calibration.
- [ ] Add Viewer coordinate display, calibrated distance/area measurement, layer
  controls, and explicit unsupported-projection diagnostics.
- [ ] Preserve unknown GIS metadata without presenting it as semantically editable.

### PXA-PDF-ENG - Engineering and 3D PDF

Current state: `deferred`; PDF/E and 3D structures are detected only as Trust
risks or unsupported content.

- [ ] Begin with inspection, preservation, extraction, and safe Viewer fallback.
- [ ] Evaluate PDF/E profiles, CAD layers, U3D/PRC streams, 3D annotations, named
  views, model trees, part metadata, sections, and measurements.
- [ ] Keep 3D authoring outside the first milestone and require a proven library or
  service before committing to native PRC/U3D generation.

### PXA-PDF-RICH - Rich Media and Interactive PDF

Current state: `partial inspection`; Trust reports detect risky multimedia and
JavaScript, but PXA does not provide a rich-media runtime.

- [ ] Preserve and inspect Sound, Movie, Screen, 3D, and RichMedia annotations
  without automatically executing them.
- [ ] Add safe metadata, attachment extraction, sanitization, and fallback poster
  rendering.
- [ ] Keep embedded PDF JavaScript disabled by default and limit compatibility work
  to static analysis, known-action migration, and explicit diagnostics.
- [ ] Require tenant policy, permissions, CSP, sandboxing, and user confirmation
  before any future interactive playback.

### PXA-PDF-PROV - Content Provenance

Current state: `discovered`; operation audits and cryptographic signatures provide
foundations, but no document-provenance product exists.

- [ ] Evaluate signed generation manifests, transformation history, source hashes,
  and content-credential standards.
- [ ] Integrate provenance with Trust reports and append-only operation evidence.
- [ ] Distinguish origin and transformation evidence from factual correctness,
  authorship, qualified signatures, and legal admissibility.
- [ ] Define privacy-preserving disclosure and selective export before storing
  source identities or tool history in customer PDFs.

## Existing Specialized Gaps Kept in Other Owners

- [ ] Keep universal existing-PDF editing and low-level object APIs in
  [PxaPdf-Provider-Feature-Gaps.md](PxaPdf-Provider-Feature-Gaps.md) and
  [Importer-New-Featuers.md](Importer-New-Featuers.md) until promoted to a
  dedicated milestone.
- [ ] Keep annotations, collaboration, Viewer state, and PDF review UX in
  [PdfTools-WebViewer-Feature-Gaps.md](PdfTools-WebViewer-Feature-Gaps.md).
- [ ] Keep HTML/CSS/URL/Razor rendering in
  [PxaPdf-Provider-Feature-Gaps.md](PxaPdf-Provider-Feature-Gaps.md) until it
  receives its own renderer plan.
- [ ] Keep PDF/A, PDF/UA, PAdES, AES-256, redaction, and Trust validation in
  [PXA.PDF-Trust-Foundation.md](PXA.PDF-Trust-Foundation.md).
- [ ] Keep implementation-specific provider mappings in their migration
  checklists while linking their capability families here.

## Cross-Checklist Register

| Feature family | Existing checklist references | Central owner after promotion |
| --- | --- | --- |
| Electronic invoicing | No dedicated existing checklist | `PXA-PDF-EINV` |
| Legal PDF workflows | Competitive readiness; Viewer comparison and Trust audit foundations | `PXA-PDF-LEGAL` |
| Forms and signing workflow | [Provider gaps](PxaPdf-Provider-Feature-Gaps.md), [Viewer gaps](PdfTools-WebViewer-Feature-Gaps.md), [PDF Trust](PXA.PDF-Trust-Foundation.md), [Nutrient migration](PXA.Migration-Nutrient-Code-And-Viewer.md) | `PXA-PDF-FORMS` |
| Capture and OCR | [Image OCR](Image-Converter2.md), [Importer](Importer-New-Featuers.md), [provider gaps](PxaPdf-Provider-Feature-Gaps.md) | `PXA-PDF-CAPTURE` |
| PDF/X and print | [PDF Trust](PXA.PDF-Trust-Foundation.md), [Page Settings](UI-Page-Settings.md) | `PXA-PDF-PRINT` |
| Optimization | Provider gaps, Importer, PDF Trust, Nutrient migration | `PXA-PDF-OPT` |
| PDF/VT | [PDF Trust deferred list](PXA.PDF-Trust-Foundation.md) | `PXA-PDF-VT` |
| GeoPDF | No dedicated existing checklist; RDL maps are adjacent only | `PXA-PDF-GEO` |
| PDF/E and 3D | [PDF Trust deferred/risk handling](PXA.PDF-Trust-Foundation.md) | `PXA-PDF-ENG` |
| Rich media and JavaScript | PDF Trust, Viewer, Redaction, Nutrient migration | `PXA-PDF-RICH` |
| Content provenance | Trust audit and commercial evidence foundations only | `PXA-PDF-PROV` |

## Promotion, Completion, and Cleanup

- [ ] Create a dedicated implementation checklist before moving a feature to
  `inProgress`.
- [ ] Link the implementation checklist here and link back to this roadmap from
  every active overlapping checklist.
- [ ] Require applicable Generator or Importer behavior, API contracts, Viewer or
  Designer support, migration mappings, tests, Documentation, fixtures, and
  release notes before marking a feature `complete`.
- [ ] Require the declared independent tool or industry fixture evidence before
  marking a standards-sensitive feature `validated`.
- [ ] After completion, remove duplicate open tasks from active gap lists and
  replace historical tasks with one completion note linking here.
- [ ] Never delete historical design decisions, diagnostic IDs, limitations, test
  evidence, or compatibility commitments.
- [ ] Update Product Roadmap, competitive assessments, pricing implications, and
  Documentation navigation after each completed domain.

## Validation

- [ ] Verify every open PDF provider gap is represented here, retained under an
  explicit existing owner, or explicitly excluded.
- [ ] Verify every cross-checklist reference exists and is reciprocal.
- [ ] Prevent contradictory `missing`, `partial`, `complete`, and `validated`
  statuses during roadmap reviews.
- [ ] Confirm that E-Invoicing, Legal PDF, GeoPDF, capture, signing ceremony, and
  provenance are recorded as new or newly centralized domains.
- [ ] Confirm that PDF/X, PDF/E, PDF/VT, XFA, rich media, forms, and optimization
  retain their earlier context.
- [ ] Run Markdown link validation and `git diff --check` after roadmap updates.

## Assumptions

- E-Invoicing is the first recommended implementation milestone.
- PDF Trust remains a separate detailed tracker and is linked rather than copied.
- Peppol transport, qualified trust-provider certification, and legal conclusions
  remain external integration or validation concerns.
- Dynamic XFA, embedded JavaScript, rich media, and 3D content default to safe
  inspection and preservation, not execution.
- Customer demand is required before promoting PDF/VT, GeoPDF, or PDF/E/3D.
