# PXA Electronic Invoicing

## Goal

Deliver versioned electronic-invoice generation, embedding, extraction,
validation, Viewer inspection, Designer binding, migration, and Documentation
without confusing secure XML parsing with standards conformance.

The first maintained versions are Factur-X 1.09.2, ZUGFeRD 2.5.2, and
XRechnung 3.0.2. Versioned official schemas, Schematron rules, code lists, and
independent validators remain authoritative.

## P0 - Foundation

- [x] Add generic document-level associated files to PXA.Pdf.
- [x] Emit embedded-file streams, file specifications, AFRelationship, catalog
  AF, and the embedded-files name tree.
- [x] Reject associated files in PDF/A-2b and permit them in PDF/A-3b.
- [x] Add bounded, DTD-free XML parsing and CII/UBL syntax recognition.
- [x] Distinguish structural recognition from XSD/Schematron conformance.
- [x] Add typed Factur-X, ZUGFeRD, and XRechnung options and stable PXAEINV
  diagnostics.
- [x] Add a typed business-invoice model with deterministic CII and UBL
  generation, calculated tax groups and totals, bounded input validation, and
  secure structural self-recognition.
- [x] Add the required Factur-X/ZUGFeRD XMP extension schema and profile metadata.
- [x] Add a fail-closed, hash-pinned local XSD and precompiled Schematron
  validation engine with stable conformance diagnostics.
- [ ] Pin licensed or distributable official validation artifacts with source,
  version, license, hash, and update policy.
- [ ] Validate profile-specific XSD, Schematron, code lists, totals, and business
  rules.

## P0 - API and Product Integration

- [ ] Add authenticated create, attach, extract, inspect, and validate operations
  under /api/pxa/v1/pdf/e-invoices.
- [x] Add authenticated structural validation, attachment inspection, and
  byte-preserving extraction operations under `/api/pxa/v1/pdf/e-invoices`.
- [x] Add authenticated typed XML generation and visible PDF/A-3 hybrid-invoice
  creation after XMP metadata and semantic PDF/XML consistency became available.
- [ ] Add attachment of invoice XML to an existing PDF without replacing its
  visible source pages.
- [x] Add dedicated read and validation permissions plus bounded PDF/XML uploads.
- [x] Add dedicated generate/create permissions, Generator product entitlement,
  bounded JSON input, synchronous line limits, and generated-PDF output limits.
- [x] Return stable Problem Details and never return an invoice as conformant after
  structural validation alone.
- [x] Add Viewer invoice summary, profile, validation evidence, diagnostics, and
  integrity-protected XML download.
- [x] Add parsed invoice identifiers, dates, parties, currency, taxes, totals,
  and line items plus conservative PDF/XML semantic consistency warnings to
  the Viewer.
- [ ] Add Designer invoice data binding and templates without making the PDF engine
  depend on billing-domain services.
- [ ] Add migration capability mappings for providers that expose Factur-X,
  ZUGFeRD, XRechnung, or generic PDF/A-3 invoice APIs.

## P1 - Evidence and Delivery

- [ ] Add official valid and invalid fixtures for every supported syntax/profile.
- [ ] Validate generated hybrid invoices with the official FeRD/FNFE artifacts and
  an independent PDF/A validator.
- [ ] Validate XRechnung XML with the pinned KoSIT validator configuration.
- [x] Add automated XML/PDF semantic consistency coverage for invoice identifiers,
  parties, currency, totals, taxes, dates, and line items.
- [ ] Validate semantic consistency against official and real-world multilingual
  fixtures, including scanned PDFs where comparison must remain not evaluated.
- [ ] Add public Documentation, examples, known limits, compatibility matrix, and
  downloadable machine-readable evidence.
- [ ] Add a customer-facing minor release fragment after all P0 release gates pass.

## Security and Compatibility

- [x] Reject DTDs, external entities, oversized XML, duplicate attachments, unsafe
  filenames, invalid MIME types, and ambiguous profiles.
- [ ] Preserve unknown invoice XML when explicitly requested without claiming it is
  editable or conformant.
- [ ] Keep Peppol transport, tax submission, accounting, and legal retention outside
  the PDF engine.
- [x] Version validator artifacts independently from API routes and include the
  artifact-set ID and SHA-256 fingerprint in validation reports.
- [ ] Persist historical validation reports with their artifact-set evidence.

## References

- Central roadmap:
  [PXA.PDF-Specialized-Features-Roadmap.md](PXA.PDF-Specialized-Features-Roadmap.md)
- PDF Trust:
  [PXA.PDF-Trust-Foundation.md](PXA.PDF-Trust-Foundation.md)
- Provider gaps:
  [PxaPdf-Provider-Feature-Gaps.md](PxaPdf-Provider-Feature-Gaps.md)
- Official FeRD ZUGFeRD 2.5.2 package:
  https://www.ferd-net.de/publikationen-produkte/publikationen/detailseite/zugferd-252-english
- Official KoSIT XRechnung validator configuration:
  https://github.com/itplr-kosit/validator-configuration-xrechnung
- Official KoSIT XRechnung Schematron rules:
  https://github.com/itplr-kosit/xrechnung-schematron

## Remaining Work Handoff

The following items are the complete remaining scope as of this handoff. Work
should proceed in the listed order because later evidence, UI, and release work
depends on the validation foundation.

### EINV-01 - Official Validation Artifact Governance

- Confirm redistribution and runtime-use terms for every required FeRD/FNFE
  and KoSIT artifact before adding it to the repository or release image.
- Record source URL, upstream version, license, retrieval date, SHA-256,
  supported standard/profile, and update policy in the validator manifest.
- Make startup and CI fail closed when an enabled artifact is missing,
  modified, expired by policy, or inconsistent with its manifest entry.
- Completion evidence: reviewed license record, pinned artifacts, deterministic
  manifest verification tests, and no network access during invoice validation.

### EINV-02 - Complete Standards Validation

- Execute the correct XSD, Schematron, code-list, arithmetic-total, tax, and
  business-rule set for each supported Factur-X, ZUGFeRD, and XRechnung profile.
- Keep structural recognition, PXA business validation, and official
  conformance as separate statuses in every report.
- Map validator output to stable `PXAEINV...` diagnostics without discarding
  the original rule identifier, location, severity, or artifact-set evidence.
- Completion evidence: profile matrix tests covering valid documents, each rule
  family, incorrect profiles, malformed totals, and unsupported versions.

### EINV-03 - Existing-PDF Attachment Workflow

- Add an authenticated `attach` operation below
  `/api/pxa/v1/pdf/e-invoices` that embeds validated XML into an existing PDF.
- Preserve all visible source pages and supported metadata; never silently
  replace, rasterize, or redesign the source invoice.
- Preflight PDF/A-3 conversion requirements, signatures, encryption,
  conflicting attachments, XMP, output size, and semantic PDF/XML consistency.
- Require explicit approval for destructive transformations and preserve the
  original file when the operation is blocked or requires review.
- Completion evidence: byte-level attachment extraction, unchanged-page visual
  comparison, PDF/A-3 validation, authorization, limits, and negative tests.

### EINV-04 - Complete API Contract

- Mark the API umbrella complete only after create, attach, extract, inspect,
  and validate are all authenticated, documented, rate-limited, entitled, and
  covered by stable Problem Details contracts.
- Add OpenAPI examples for Factur-X, ZUGFeRD, and XRechnung and document the
  difference between structural, business-rule, and official conformance status.
- Completion evidence: endpoint contract tests, generated OpenAPI review, and
  permission tests for every role and operation.

### EINV-05 - Designer Invoice Experience

- Add typed invoice bindings and maintained invoice templates without moving
  tax, accounting, or billing-domain logic into `PXA.Pdf`.
- Support seller, buyer, dates, references, currency, lines, tax groups, and
  totals with localized validation and a visible conformance state.
- Ensure Designer preview, generated XML, visible PDF, and reopened Viewer
  inspection use the same canonical invoice values.
- Completion evidence: autosave/roundtrip tests, six-language UI coverage,
  visual parity, and successful export/reinspection fixtures.

### EINV-06 - Migration Provider Coverage

- Add explicit capability mappings for providers exposing Factur-X,
  ZUGFeRD, XRechnung, PDF/A-3 attachment, or invoice-validation APIs.
- Report `automatic`, `reviewRequired`, or `unsupported` per capability;
  never turn a source API into a silent TODO or an unsupported conformance claim.
- Link each mapping to the unified migration catalog, permission, example,
  diagnostics, and documentation.
- Completion evidence: provider coverage report and representative conversion
  tests for every mapped provider family.

### EINV-07 - Maintained Fixture Corpus

- Add licensed official valid and invalid fixtures for every maintained
  syntax/profile combination, plus PXA-generated deterministic fixtures.
- Include multilingual, multi-tax-rate, rounding, credit-note where
  supported, large-line-count, malformed, and adversarial XML cases.
- Store provenance, license, expected result, and SHA-256 for every fixture.
- Completion evidence: a machine-readable corpus manifest checked in CI.

### EINV-08 - Independent Hybrid-PDF Validation

- Validate generated Factur-X/ZUGFeRD hybrid invoices with official
  FeRD/FNFE tooling and an independent PDF/A validator such as veraPDF.
- Validate XRechnung XML with the pinned KoSIT configuration independently
  of PXA's own validator.
- Publish tool versions, commands, hashes, raw reports, and normalized
  machine-readable summaries without overstating unavailable checks.
- Completion evidence: reproducible passing evidence for every supported output
  profile and intentional failing fixtures proving the gate is effective.

### EINV-09 - Real-World Semantic Consistency

- Exercise PDF/XML comparison against licensed real-world and multilingual
  invoices, including alternative number/date formatting and rounding.
- Keep scanned or text-inaccessible PDFs at `notEvaluated` unless bounded OCR
  evidence is available; never treat missing text extraction as consistency.
- Completion evidence: reviewed corpus results with false-positive and
  false-negative thresholds and retained original fixtures.

### EINV-10 - Unknown XML Preservation

- Allow explicitly requested byte-preserving storage and extraction of
  unknown invoice XML while marking it non-editable and non-conformant.
- Apply the same size, filename, MIME, DTD/entity, checksum, and tenant
  boundaries used for recognized documents.
- Completion evidence: exact-byte roundtrip and UI/API status tests showing that
  preservation never becomes a standards claim.

### EINV-11 - Historical Validation Evidence

- Persist immutable validation reports with tenant, document checksum,
  validator artifact-set ID/hash, result, diagnostics, timestamps, and actor.
- Add bounded retention, authorization, audit, export, and revalidation
  behavior when a newer artifact set becomes available.
- Completion evidence: tenant-isolation, immutability, retention, and historical
  reproducibility tests.

### EINV-12 - Product Boundaries and Public Documentation

- Document generation, attachment, inspection, extraction, validation,
  Designer binding, migration, supported versions/profiles, and known limits.
- State clearly that Peppol transport, national tax submission, accounting,
  payment, and legal-retention services are external integrations, not PDF-engine
  capabilities.
- Publish API and C# examples plus a compatibility matrix and downloadable,
  machine-readable validation evidence.
- Completion evidence: Documentation build, link checks, runnable examples, and
  terminology parity across API, Viewer, Designer, and Migration.

### EINV-13 - Final Release Gates

- Prove generated PDF/A-3 output with PXA and independent validation.
- Prove supported XML against the pinned official XSD and Schematron sets.
- Prove invalid XML, profile mismatches, incorrect totals, unsafe payloads,
  and attachment conflicts fail closed with stable diagnostics.
- Verify Viewer, API, Designer, Migration, tests, and Documentation report the
  same standard, version, profile, syntax, and conformance state.
- Add or finalize the customer-facing `minor` release fragment only after all
  preceding P0 gates pass; do not change `VERSION` on this feature branch.
- Completion evidence: full solution/build matrix, external validation bundle,
  authenticated desktop/mobile smoke tests, and reviewed release text.

## Acceptance Criteria

- [x] A supported invoice XML can be embedded in a PDF/A-3 document and extracted
  byte-for-byte.
- [x] Typed invoice data can produce deterministic XML and a visible PDF/A-3
  hybrid invoice whose embedded semantic values match its rendered content.
- [ ] The generated PDF/A-3 structure passes PXA and independent validation.
- [ ] Supported XML passes the pinned official XSD and Schematron rules.
- [ ] Invalid XML, mismatched profiles, invalid totals, and unsafe payloads fail
  closed with stable diagnostics.
- [ ] Viewer, API, Designer, Migration, tests, and Documentation agree on standard,
  version, profile, syntax, and conformance state.

## Assumptions

- Structural recognition is useful but is never marketed as invoice conformance.
- ZUGFeRD and Factur-X share a technical hybrid format while retaining product
  identities and version labels. Current ZUGFeRD 2.5.2 output uses the harmonized
  `factur-x.xml` filename and Factur-X XMP namespace; legacy ZUGFeRD metadata is
  import compatibility only.
- XRechnung supports UBL and CII XML; embedding it in PDF/A-3 is a PXA workflow,
  not a claim that every XRechnung exchange requires a PDF.
- Official validation artifacts are pinned only after their redistribution terms
  have been reviewed.
