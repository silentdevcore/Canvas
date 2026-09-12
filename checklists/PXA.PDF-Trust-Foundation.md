# PXA PDF Trust Foundation

## Purpose

This checklist defines the first competitive-readiness milestone for trusted PDF
workflows. It covers native PDF security, signatures, compliance, accessibility,
irreversible redaction, Viewer workflows, provider migration, APIs, diagnostics,
documentation, and independent interoperability validation.

The milestone must not claim conformance or trust from file metadata alone. Every
claim must be backed by deterministic validation, external interoperability tests,
and an explicit report that distinguishes `valid`, `invalid`, `indeterminate`, and
`unsupported` results.

## Standards Baseline

- [ ] License and archive the normative standards required for implementation and
  verification, including the applicable ISO 32000, ISO 19005, ISO 14289, and ETSI
  PAdES publications.
- [ ] Record the exact standard edition, corrigenda, validator version, and policy
  profile used by every conformance result.
- [ ] Treat PDF/A as the ISO 19005 family and PDF/UA as the ISO 14289 family; do not
  use generic `PDF/A` or `PDF/UA` claims without a profile and conformance level.
- [ ] Treat PAdES Baseline B-B, B-T, B-LT, and B-LTA as separate capabilities with
  separate validation outcomes.
- [ ] Add a standards-support matrix documenting legal combinations and conflicts,
  including the prohibition of encryption in PDF/A output.
- [ ] Track published errata and approved resolutions without silently changing a
  released validator policy.

Reference material:

- PDF standards overview: https://pdfa.org/pdf-standards/
- PDF/A family: https://pdfa.org/resource/iso-19005-pdfa/
- ETSI PAdES Baseline: https://www.etsi.org/deliver/etsi_EN/319100_319199/31914201/01.02.01_60/en_31914201v010201p.pdf
- Adobe PDF standards resources: https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/

## Delivery Principles

- [ ] Keep parsing, validation, mutation, signing, and serialization as separate
  services with typed result models.
- [ ] Never report success after only writing a dictionary flag, metadata value,
  visual overlay, or signature appearance.
- [ ] Preserve the original document until a mutation has passed post-save
  validation.
- [ ] Fail closed for signing, applied redaction, and compliance conversion when a
  required primitive is unsupported.
- [ ] Preserve existing valid signatures unless the requested operation explicitly
  invalidates them and the user confirms the consequence.
- [ ] Return stable `PXAPDFTRUST...` diagnostics with severity, page/object context,
  remediation, standard/profile, and evidence.
- [ ] Keep cryptographic keys, passwords, certificate secrets, TSA credentials,
  document content, and redacted values out of logs, telemetry, and audit details.
- [ ] Add one customer-facing `minor` release fragment only after the first supported
  production profile passes all acceptance gates.

## P0 - Existing PDF and Trust Report Foundation

### Typed PDF Object Model

- [ ] Define the boundary between `PXA.Importer`, `PXA.Pdf`, and
  `PXA.Infrastructure.Pdf` for existing-document parsing and mutation.
- [ ] Introduce a typed low-level object model for dictionaries, arrays, names,
  strings, numbers, references, streams, cross-reference tables/streams, trailers,
  object streams, and incremental revisions.
- [x] Preserve unknown objects and byte ranges when an operation does not need to
  modify them.
  - [x] Carry objects of unknown type, their dictionary entries, and their stream
    payloads through a full rewrite unchanged, recomputing only `/Length`.
  - [x] Keep every object reachable from the trailer, including unknown extension entries,
    while pruning orphaned payloads that no conforming reader resolves.
- [x] Support classic xref tables, xref streams, object streams, hybrid-reference
  files, inherited page properties, rotated pages, and malformed-but-recoverable
  inputs with diagnostics.
  - [x] Read the trailer `/XRefStm` of a hybrid-reference file and prefer its entries,
    as a conforming PDF 1.5+ reader does, instead of reading only the classic table.
  - [x] Report `PXAPDFXREF010` when the classic table and the `/XRefStm` claim
    different locations for the same in-use object.
  - [x] Report `PXAPDFXREF008` when a document has no usable cross-reference data and
    objects were recovered by scanning.
- [x] Add bounded parsing for recursion, object count, stream size, decompression
  ratio, page count, nesting, and cancellation.
  - [x] Add a typed `PdfParseLimits` model on `PdfImporterOptions` covering nesting
    depth, array and dictionary sizes, indirect object count, object-stream entry
    count, decoded stream bytes, decompression ratio, predictor row length, reference
    resolution depth, and page count.
  - [x] Enforce decompression bounds during decoding rather than after it, with a
    floor so that small streams which legitimately expand are not rejected.
  - [x] Break cyclic indirect-reference chains during resolution.
  - [x] Thread `CancellationToken` through document graph parsing.
  - [x] Surface `PXAPDFPARSE` diagnostics in the trust report and refuse a `valid`
    structure status for any document that tripped a limit.
  - [x] See `PXA.PDF-Parser-Hardening.md` for the confirmed defects and corpus.
- [x] Reject ambiguous or dangerous duplicate-object and shadow-object structures
  rather than validating a different revision than a PDF viewer displays.
  - [x] Restrict the raw object scan to documents with no usable cross-reference data,
    so a well-formed document is never judged by objects a reader cannot resolve.
  - [x] Report `PXAPDFTRUST150` for any object definition that no cross-reference
    revision references, excluding candidates that fall inside parsed stream bytes.
  - [x] Refuse a `valid` structure status for a document containing such definitions.

### Save Strategies

- [x] Implement full rewrite and incremental update as explicit save strategies.
  - [x] Add `PdfDocumentSaver` so a caller states the strategy it needs, and share one
    object serializer between both writers.
  - [x] Refuse a full rewrite of a signed document unless the caller opts in, and report
    which signatures the rewrite invalidates.
- [x] Centralize the existing append paths in a deterministic incremental-update
  writer that validates the source revision chain, preserves original bytes, and
  reparses the saved result before returning it.
- [x] Reject incremental mutation of encrypted PDFs until appended objects can be
  serialized through the active security handler.
- [x] Use incremental updates for signatures and preservation-sensitive operations.
- [x] Rebuild xref data, `/Prev`, `/Size`, object generations, stream lengths, and
  document IDs deterministically.
  - [x] Renumber retained objects compactly to generation 0 and rebuild the classic
    cross-reference table, `/Size`, and `/Root`.
  - [x] Derive `/ID` from the written body rather than randomly, so that rewriting the
    same document twice produces identical bytes.
  - [x] Drop cross-reference and object-stream containers, whose contents are re-emitted
    as plain indirect objects.
- [x] Add a change-impact analyzer showing which existing signatures and compliance
  claims a planned mutation will affect.
- [x] Make post-save parse and validation mandatory before returning trusted output.
  - [x] Reparse a full rewrite and assert exactly one revision, the expected object
    count, a resolvable catalog, and no parser or cross-reference diagnostics.

### Unified Trust Report

- [x] Add `IPdfTrustInspector` returning document structure, encryption, signatures,
  timestamps, revisions, PDF/A, PDF/UA, tagging, redaction evidence, risky actions,
  attachments, and parser warnings.
- [x] Represent every check as `valid`, `invalid`, `indeterminate`, `unsupported`, or
  `notApplicable`; never collapse unknown into valid.
- [x] Include document and revision SHA-256 hashes so audit events can be correlated
  without storing document content.
- [x] Expose `POST /api/pxa/v1/pdf/trust/inspect` as an authenticated, entitled,
  rate-limited streaming upload endpoint.
- [ ] Enforce tenant-scoped jobs, file limits, cancellation, short retention, and
  audit events for trust inspections.
- [ ] Add synthetic examples for clean, malformed, encrypted, signed, archived,
  accessible, and redaction-risk PDFs.

## P0 - Digital Signatures and Validation

### Signing Model

- [x] Add typed signing options for certificate source, signature field, page and
  appearance, reason, location, contact, signing time, digest, PAdES profile, TSA,
  revocation data, and validation policy.
- [x] Support PKCS#12/PFX input through secret streams and platform/remote key
  providers through an `IPdfSigningKey` abstraction.
- [x] Never persist raw private-key bytes or passwords in template JSON, jobs, logs,
  telemetry, or audit payloads.
- [x] Support visible and invisible signature fields and preserve existing AcroForm
  fields and appearances.
- [x] Preserve existing AcroForm fields and support standards-compatible invisible
  signature widgets and visible signature appearances on a selected page.
- [x] Implement detached CMS signatures with correct `/ByteRange`, `/Contents`,
  digest calculation, placeholder sizing, and incremental save.
- [x] Support SHA-256 or stronger digests only for new signatures.
- [ ] Produce PAdES B-B, B-T, B-LT, and B-LTA as independently verified profiles.
  - [x] Produce PAdES B-B signatures with signed attributes and incremental updates.
  - [x] Produce PAdES B-T signatures with a validated RFC 3161 timestamp token.
  - [x] Produce PAdES B-LT signatures with a bounded, deduplicated DSS and
    signature-specific VRI revision assembled from server-owned trust and revocation
    policy; fail closed unless the completed document validates back as B-LT.
  - [x] Produce PAdES B-LTA signatures by extending a validated B-LT revision with
    a trusted RFC 3161 document timestamp; fail closed unless the final document
    validates back as B-LTA.
  - [ ] Validate B-T, B-LT, and B-LTA profile classification with an independent
    licensed or standards-backed validator and retain its machine-readable report.
    OpenSSL and Poppler already corroborate CMS/PDF integrity, but do not independently
    classify every PAdES long-term profile.
- [x] Produce interoperable PAdES Baseline B-B signatures validated independently by
  OpenSSL CAdES verification and Poppler `pdfsig`.
- [x] Produce PAdES Baseline B-T signatures with a bounded, HTTPS-only RFC 3161 TSA
  client, validate nonce, message imprint, TSA EKU, token signature, and configured
  host trust, and expose B-T only when a server-side TSA is configured.
- [x] Add independent RFC 3161 document timestamps that protect the complete current
  PDF revision and remain separately visible in trust reports and the Viewer.
- [x] Support multiple sequential signatures without invalidating earlier revisions.

### Signature Validation

- [x] Validate CMS integrity, signed byte ranges, certificate chain, key usage,
  signing time, timestamp token, revocation evidence, PAdES profile, and post-sign
  changes separately.
- [x] Validate detached CMS integrity, SHA-256/384/512 document digest, signed byte
  ranges, complete revision boundaries, SigningCertificateV2 binding, RSA signature,
  signing time, host certificate chain, and PAdES B-B profile as separate statuses.
- [x] Validate embedded RFC 3161 signature timestamps separately from approval
  signature integrity and report timestamp time, TSA identity, and B-T profile.
- [x] Detect unsigned bytes, overlapping ranges, wrapping/shadow attacks, invalid
  incremental revisions, and disallowed changes after certification signatures.
  - [x] Reject non-zero, negative, overlapping, overflowing, out-of-file, malformed,
    and non-revision-aligned signature byte ranges.
  - [x] Bind the unsigned byte-range gap to one valid hexadecimal `/Contents` value
    and reject non-whitespace bytes after the final complete PDF revision.
  - [x] Reject cyclic, out-of-range, non-integer, and unparseable incremental revision
    links through stable cross-reference diagnostics.
  - [x] Detect object-shadowing and signature-wrapping attacks across valid-looking
    incremental revisions.
  - [x] Enforce DocMDP/FieldMDP certification permissions for post-signature changes.
    - [x] Bind the DocMDP transform to the catalog `/Perms` entry and require the
      certification signature to cover the first signed revision.
    - [x] Classify every object changed after the certified revision and reject changes
      that the declared permission level does not allow, keeping document timestamps and
      DSS long-term validation material permitted at every level.
    - [x] Enforce FieldMDP `/All`, `/Include`, and `/Exclude` field locks as a status
      separate from the DocMDP permission level.
    - [x] Produce certified signatures with DocMDP and FieldMDP transforms so the
      enforcement is verified end to end.
- [x] Return signer identity and certificate metadata without treating a valid
  cryptographic signature as a trusted identity unless chain policy succeeds.
- [x] Support configurable system and custom PEM/DER trust anchors plus intermediate
  certificate stores for Cloud and On-Premise deployments.
- [x] Support offline validation with supplied trust anchors, CRLs, OCSP responses,
  and embedded LTV material.
  - [x] Validate server-supplied and application-cached CRLs offline, including CRL
    issuer, signature, validity window, and certificate serial status.
  - [x] Validate supplied and cached OCSP responses, including CertID binding,
    responder authorization, response signature, status, and freshness.
  - [x] Validate embedded PAdES DSS/VRI evidence.
    - [x] Discover, decode, bound, and deduplicate embedded certificates, CRLs, and
      OCSP responses from document-level DSS arrays and signature-related VRI entries.
    - [x] Bind each valid VRI key to the exact trimmed CMS signature value and expose
      the signature-specific certificate, CRL, and OCSP inventory separately.
    - [x] Validate every non-root certificate in the trusted signer and TSA chains
      at the RFC 3161 reference time, including later evidence and revocation times,
      without treating discovery or VRI binding alone as conformance.
- [x] Support explicit `NoCheck`, platform-cache `Offline`, and opt-in `Online`
  revocation modes with separate signer and TSA revocation outcomes, including
  supplied CRL/OCSP evidence and signature-bound embedded LTV material.
- [x] Make network retrieval of OCSP, CRL, AIA, and TSA data explicit, bounded,
  allowlisted, cached, and disableable for isolated installations.
  - [x] Retrieve certificate-declared CRLs only through exact-host HTTPS allowlists,
    without redirects, with bounded responses and a Data-Protection-encrypted cache.
  - [x] Add application-owned OCSP POST requests and certificate-specific encrypted
    response caching under the same HTTPS allowlist and response limits.
  - [x] Add bounded AIA issuer retrieval under the same policy, including exact-host
    HTTPS allowlisting, response validation, encrypted caching, and disabled platform
    certificate downloads.
- [x] Keep certificate downloads disabled by default and make online revocation an
  explicit server policy with bounded retrieval, exact-host CRL allowlists, and a
  validated Data-Protection-encrypted CRL and certificate-specific OCSP cache.
- [x] Add `POST /api/pxa/v1/pdf/signatures/sign` and
  `POST /api/pxa/v1/pdf/signatures/validate` with streaming binary responses and
  structured validation reports.
- [x] Add separate permissions for signature inspection, signing, trust-policy
  management, and use of organization key providers.
- [x] Add separate permissions for signature inspection, validation, signing,
  trust-policy management, and organization key-provider use. Only administrator
  role aggregates receive the two policy/provider permissions by default.

## P0 - Secure and Irreversible Redaction

- [x] Distinguish redaction marks, preview overlays, and applied irreversible
  redaction in models, APIs, UI, diagnostics, and documentation.
  - [x] Separate preflight, apply, and verify in the backend model, the API surface, the
    permissions, and the documentation.
  - [x] Route the Viewer redaction endpoint through the gated applier and remove the
    former ungated regenerate-the-whole-document path, so one implementation remains.
  - [x] Add `POST /api/pdf-viewer/annotations/redact/preflight` so the Viewer can show
    coverage before a redaction is committed.
  - [x] Present preflight, apply, and verify as distinct steps in the Viewer UI itself.
    - [x] Add a preflight action that reports coverage and findings without changing the
      document, and keep apply disabled until preflight reports the marks as removable.
    - [x] Surface a refusal as the structured preflight report rather than a message
      string, so the user is told which content blocked the redaction.
    - [x] Show the verification outcome and whether the audit record was signed.
    - [x] Localize the workflow in all six Designer languages.
- [x] Build a content-intersection analyzer for text glyphs, paths, images, masks,
  clipping paths, patterns, nested Form XObjects, transparency groups, annotations,
  AcroForm appearances, optional content, and rotated/cropped pages.
  - [x] Classify text, path, image, shading, and nested group elements as not
    intersecting, partially overlapping, or fully contained; supported text runs are split
    at glyph boundaries while unsupported partial elements remain `reviewRequired`.
  - [x] Report overlapping annotations, form-field widgets, and optional content, which
    are not interpreted scene-graph content.
  - [x] Extend to masks, clipping paths, patterns, and transparency groups as distinct
    intersection cases rather than through their containing element.
    - [x] Narrow coverage by the clipping paths in force, including those inherited from
      enclosing groups, so a clipped element is not reported as covered where it never
      paints.
    - [x] Report soft and stencil masks when a partially covered image would require pixel
      reconstruction, plus soft masks set in the graphics state, tiling and shading pattern
      resources, and page or form transparency groups. Unused image resources do not block.
    - [x] Resolve selected tiling and shading pattern paint operations and attached image-mask
      pixels per region instead of blocking on every declared resource. Remove an intersecting
      pattern-painted element as one safe paint unit, prune its stream after the last use, retain
      uses outside the region, and keep malformed selected patterns `reviewRequired`.
- [x] Remove covered text operators and associated extractable text, not only paint
  a black rectangle.
  - [x] Reserialize only the pages carrying a region through the content-stream rewriter,
    leaving every other page and object byte-faithful.
  - [x] Empty any additional content streams on a rewritten page so no original operator
    survives elsewhere in that page.
- [x] Remove or irreversibly transform covered image pixels and reusable resources
  without leaking the hidden original through an unused object or alternate stream.
  - [x] Remove optional content, non-normal appearance streams, and image alternates, so
    a viewer cannot reveal covered content the rendered page does not show.
  - [x] Remove page thumbnails.
  - [x] Zero covered pixels inside supported image XObjects, cloning a shared XObject before editing
    so a redaction on one page cannot alter or leak through another.
    - [x] Support bounded 1-, 2-, 4-, 8-, and 16-bit samples with resolvable named or
      custom colour spaces in raw, Flate, LZW, RunLength, DCT/JPEG, and safely delimited
      inline images.
    - [x] Remove fully contained image operations and prune their now-unreachable payloads.
    - [x] Re-decode the result and verify that every covered sample is zero; never trust the
      internal redaction marker by itself.
  - [x] Add deterministic JPX and JBIG2 decoding. PXA now uses the bundled, managed
    CoreJ2K and JBig2Decoder.NETStandard codecs, validates encoded dimensions and bounded
    input before decoding, resolves bounded `/JBIG2Globals`, requires embedded JBIG2
    organization, and rewrites covered grayscale/RGB output as lossless Flate data.
    Unsupported component layouts, malformed segments, mismatched dimensions, colour-key
    masks, and oversized inputs continue to fail closed.
  - [x] Reconstruct bounded, decodable soft-mask and explicit-mask streams together with the
    main image; preserve colour-key arrays; and securely rewrite stencil images, 1/2/4/16-bit
    samples, and named or custom colour spaces without changing uncovered sample values.
- [x] Sanitize metadata, XMP, attachments, embedded files, comments, form values,
  actions, JavaScript, thumbnails, previous revisions, and incremental history when
  requested by the redaction policy.
  - [x] Add a typed sanitization policy covering document metadata and XMP, embedded
    files, actions and JavaScript, overlapping annotations and field values, and page
    thumbnails, with every option defaulting to on.
  - [x] Clear removed annotation objects themselves, not only their page references.
  - [x] Prune objects made unreachable by sanitization during the full rewrite so
    detached XMP, attachment, annotation, and resource payloads do not remain in the file.
  - [x] Traverse and sanitize actions, comments, form values, and alternate carriers
    outside the catalog and overlapping page annotations.
    - [x] Traverse direct dictionaries, arrays, and stream dictionaries recursively with
      a bounded depth so nested carriers cannot bypass sanitization.
    - [x] Clear `/A`, `/AA`, and `/OpenAction` wherever they occur, not only on the catalog.
    - [x] Clear `/V`, `/DV`, and `/RV` on every object declaring a field type, including
      fields that do not overlap a region.
    - [x] Clear `/Alt`, `/ActualText`, `/TU`, `/Alternates`, and `/AlternatePresentations`.
- [x] Force a full rewrite when prior revisions could retain redacted content.
  - [x] Save every applied redaction through `PdfFullRewriteWriter`, and verify the result
    contains exactly one revision.
- [x] Detect content that cannot be confidently removed and block `Apply redaction`
  instead of returning a visually redacted file.
  - [x] Detect and report pages that carry content streams but produced no interpreted
    content, deferred stream filters, encrypted input, and unparseable documents.
  - [x] Block the apply stage on a `reviewRequired` or `unsupported` preflight result,
    treating only document-level carriers that sanitization actually removes as resolved
    and always blocking on geometric ambiguity.
- [x] Add preflight, apply, and verify stages with separate reports and hashes.
  - [x] Add a preflight stage that mutates nothing and reports per-region coverage with
    the document hash.
  - [x] Add an apply stage that returns the preflight report, the rewritten pages, and
    source and result hashes.
  - [x] Add a verify stage that reparses the result and fails the operation rather than
    returning an unverified file.
- [x] Verify that removed text cannot be extracted, searched, copied, recovered from
  objects, or revealed by disabling overlays/layers.
  - [x] Reparse the result, re-run region intersection, and re-extract text inside every
    redacted region without treating an equal, legitimate string elsewhere as residue.
  - [x] Verify through regression fixtures that detached XMP and annotation payloads are
    absent from both the rewritten object graph and raw output bytes.
  - [x] Extend verification to image extraction, thumbnails, and layer removal as
    separate checks.
    - [x] Report residual covered images, retained thumbnails, retained optional content,
      and retained non-normal appearance streams under distinct diagnostic codes.
    - [x] Verify pixel-redacted images by decoding the rewritten stream and checking the
      mapped sample range, including inline and shared-resource cases.
    - [x] Do not use global raw-string equality as proof of residue: PDF encodings can hide
      matching text and an equal string outside the region is not a redaction failure.
    - [x] Prove each check fires on a result that still carries the residue it looks for.
  - [x] Add an independently maintained recovery corpus for encoded glyphs, compressed and
    malformed streams, reusable resources, images, and external extraction tools before
    making a broad no-recovery claim.
    - [x] Commit immutable Chrome and LibreOffice fixtures with reviewed source files,
      producer versions, expected outcomes, and SHA-256 hashes. Encoded clipping groups,
      reusable resources, and malformed Flate payloads fail closed instead of producing a
      redacted file.
    - [x] Add an opt-in synthetic recovery harness that checks partial glyph redaction and
      masked-image redaction through Poppler `pdftotext`, Poppler `pdfimages`, and POSIX
      `strings`, independently of the PXA parser.
- [x] Add a tamper-evident redaction audit record containing document hashes,
  non-sensitive regions, reason, actor, policy, timestamp, and verification result.
  - [x] Hash the record's canonical form so a later change to it is detectable, and keep
    removed content out of it.
  - [x] Sign the audit record; hashing alone detects change but does not prove origin.
    - [x] Sign the record's canonical hash with a detached CMS signature, and record the
      algorithm, signer subject, thumbprint, and signing time.
    - [x] Verify the canonical record hash, detached CMS signature, signer metadata, and
      signed timestamp, including tamper tests.
    - [x] Use a dedicated audit key configured per deployment, separate from any document
      signing key, so a compromise of one cannot forge the other.
    - [x] Leave the record hash-only when no key is configured, and say so in the response
      rather than implying provenance that is not there.
    - [x] Fail startup when an audit key is explicitly configured but missing or invalid;
      do not silently downgrade that deployment to unsigned records.
    - [x] Persist and return the complete signed audit record through an authenticated,
      bounded API. Apply stores append-only tenant evidence, returns its retrieval location,
      and `GET /api/pxa/v1/pdf/redactions/audits/{recordSha256}` verifies size, canonical
      hash, CMS signature when present, and tenant ownership before returning the record.
    - [x] Timestamp the audit signature so its validity survives certificate expiry.
      The configured RFC 3161 authority timestamps the CMS signature value; verification
      checks the token imprint, TSA signature, exclusive timestamping EKU, timestamp, and
      recorded TSA identity. A configured TSA failure returns no redacted document.
- [x] Replace the current importer-supported redaction claim with capability-specific
  evidence and retain `reviewRequired` for unsupported structures.
  - [x] Report `removable`, `reviewRequired`, or `unsupported` per region and for the
    document, deriving the document capability from the worst finding.
  - [x] Correct `PdfTools-WebViewer-Feature-Gaps.md`, which ticked `Secure redaction
    workflow`, to agree with `PxaPdf-Provider-Feature-Gaps.md`, which records it as
    missing.
- [x] Add `POST /api/pxa/v1/pdf/redactions/preflight` and
  `POST /api/pxa/v1/pdf/redactions/apply` with privileged permission checks.
  - [x] Add `POST /api/pxa/v1/pdf/redactions/preflight` behind a dedicated
    `designer.pdf.redactions.inspect` permission, rate limited and size bounded, and
    withheld from the read-only Viewer role.
  - [x] Add `POST /api/pxa/v1/pdf/redactions/apply` behind a separate
    `designer.pdf.redactions.apply` permission, withheld from the Editor role because the
    operation destroys content irreversibly.

## P1 - AES-256 and Modern PDF Security

- [x] Deprecate RC4 for new production documents while retaining explicit legacy
  read compatibility.
  - [x] The new handler offers AES-256 only; RC4 is not reachable through it.
  - [x] Deprecate `PXA.Pdf`'s RC4 default for generated documents and route generation
    through the new handler.
- [x] Implement AES-128 only as an interoperability bridge where required.
- [x] Implement the PDF 2.0 Standard Security Handler with AES-256 and the required
  revision-6 password processing, crypt filters, permissions, metadata behavior,
  random salts, and secure random generation.
  - [x] Implement Algorithms 2.A, 2.B, 8, 9, and 10, and write `/V 5 /R 6` with an
    `/AESV3` crypt filter, `/StmF`, `/StrF`, and `/EncryptMetadata`.
  - [x] Verify `/Perms` after authentication and report a mismatch rather than trusting it.
  - [x] Fail closed on `/Perms` mismatch, validate selected string, stream, and embedded-file
    crypt filters, and decrypt encrypted object streams before expanding compressed objects.
  - [x] See `PXA.PDF-AES256-Security-Handler.md`.
- [x] Use reviewed platform cryptography; do not hand-roll AES, hashing, random
  generation, or constant-time secret comparison.
- [x] Zero or dispose password/key buffers where runtime APIs permit it.
- [x] Support Unicode password normalization exactly as required by the selected PDF
  standard revision and test maximum password length behavior.
  - [x] Normalize to NFKC, encode as UTF-8, and truncate at 127 bytes after encoding.
  - [x] Implement SASLprep mapping, Unicode 3.2 unassigned/prohibited tables, NFKC,
    and bidirectional checks from RFC 4013/RFC 3454 in the shared writer/reader path.
- [x] Implement encrypted existing-PDF open, permission inspection, owner/user
  authentication, mutation, and re-save without leaking decrypted temporary files.
  - [x] Open an AES-256 document with either password, decrypt strings and streams on the
    typed model, and report the password kind and decoded permissions.
  - [x] Open AES-256 object streams through a two-stage parse/decrypt/expand pipeline.
  - [x] Decrypt in memory only; nothing is written to disk.
  - [x] Mutate and re-save password-protected documents through a one-revision full rewrite;
    require owner authentication and replacement encryption, and fail closed rather than
    emitting plaintext. Encrypted incremental append remains intentionally unsupported.
  - [x] Open legacy RC4 and AES-128 documents.
- [x] Add certificate/public-key encryption as a separate Adobe.PubSec AESV2 profile.
- [x] Update Designer Page Settings and export flows to offer safe algorithms only;
  legacy RC4 must be hidden behind an explicit compatibility option.
- [x] Add local password-strength guidance in all six Designer languages without logging
  or transmitting the password.
- [x] Validate output with Acrobat, qpdf, Poppler, and PdfPig.

## P1 - PDF/A Generation, Conversion, and Validation

- [x] Ship one narrow, independently validated profile first: PDF/A-2b for generated
  documents.
- [x] Add PDF/A-3b for workflows requiring arbitrary embedded source documents.
  - [x] Add a target profile to the conversion options and API, defaulting to PDF/A-2b so
    existing callers are unaffected.
  - [x] Retain embedded files and attachments, write `pdfaid:part 3`, and give every file
    specification the `/AFRelationship` and catalog `/AF` association PDF/A-3 requires.
  - [x] Keep embedded files blocked for PDF/A-2b, where they are not permitted.
  - [x] Decode escaped PDF names exactly once, repair invalid attachment MIME subtypes,
    validate profile-specific attachment rules, and independently validate generated and
    converted PDF/A-3b fixtures with veraPDF.
  - [x] Expose PDF/A-3b as a typed Generator profile and as a selectable Viewer preflight
    and conversion target; attachment authoring remains in the existing-PDF conversion path.
- [ ] Add PDF/A-4 only after the PDF 2.0 writer and validator policy are production
  ready.
  - [ ] Precondition not met. The writer emits classic cross-reference tables only, and
    the PXA validator has no PDF/A-4 rules and no PDF/A-4 output has been corroborated by
    veraPDF. Emitting `pdfaid:part 4` on that basis would be a metadata-only
    conformance claim, which the Delivery Principles forbid.
- [x] Define a typed compliance profile rather than a free-form string.
- [x] Embed all required fonts and fail when embedding or Unicode mapping is not
  possible.
- [x] Add output intents and ICC profiles with deterministic color conversion for the
  documented supported paths, and fail closed for source colour that cannot be transformed
  without a valid profile or without visual loss.
  - [x] Convert DeviceCMYK and Indexed content-stream colour to sRGB, plus Separation and
    DeviceN when their tint transform and alternate space are supported. Evaluate sampled
    functions with multilinear interpolation and reject unknown Type 4 operators.
  - [x] Fail closed for unknown named spaces, unsafe inline-image and unsupported shading
    colour, malformed ICCBased profiles, and invalid CalGray, CalRGB, or Lab parameters.
  - [x] Rewrite colour operators by splicing only their byte ranges, so no other content
    is disturbed.
  - [x] Document the DeviceCMYK conversion as the ISO 32000-1 8.6.4.4 approximation:
    reproducible, not colorimetrically exact.
  - [x] Convert bounded 8-bit colour carried by losslessly decodable DeviceCMYK, Indexed,
    Separation, and DeviceN image samples to DeviceRGB. Preserve stencil and soft masks;
    block JPEG/JPX and unknown filters, colour-key masks, oversized images, unsupported bit
    depths, and colour spaces without a complete deterministic path rather than passing
    them through or rewriting encoded bytes approximately.
    - [x] Validate the converted CMYK image fixture independently as PDF/A-2b with the
      pinned official veraPDF container and retain its machine-readable XML report.
  - [x] Complete safe Pattern colour-space handling for the documented conversion scope.
    - [x] Traverse colored tiling pattern resources and convert deterministic colour in
      their content streams while preserving the page's Pattern selection semantics.
      Reject missing and malformed patterns with an object-level diagnostic instead of
      silently flattening or recoloring them.
      - [x] Validate the converted colored-tiling-pattern fixture independently as
        PDF/A-2b with the pinned official veraPDF container.
    - [x] Convert uncolored tiling patterns by safely remapping their underlying colour
      space and every `scn`/`SCN` use without changing pattern identity.
      - [x] Validate the converted uncolored-tiling-pattern fixture independently as
        PDF/A-2b with the pinned official veraPDF container.
    - [x] Handle shading patterns without making a lossy conformance claim.
      - [x] Preserve DeviceGray, DeviceRGB, valid CalGray/CalRGB/Lab, and embedded ICC
        shadings because these are already deterministic or device independent under the
        RGB output policy.
      - [x] Fail closed for DeviceCMYK, Separation, DeviceN, and mesh shading samples rather
        than changing interpolation, domains, or visual output. Native conversion remains a
        future capability expansion, not a gap in the supported PDF/A contract.
  - [x] Use LittleCMS for embedded Gray, RGB, and CMYK ICC source-profile transforms to
    sRGB. Keep the documented ISO 32000 DeviceCMYK approximation only for untagged CMYK,
    because an output-intent destination profile cannot reconstruct a missing source
    profile; never describe that path as colorimetrically exact.
  - [x] Implement complete CalGray, CalRGB, Lab, and embedded ICC profile conversion before
    advertising those colour families as converted.
    - [x] Validate required white points, black points, gamma, matrices, Lab ranges, ICC
      headers, channel counts, and native LittleCMS availability; fail closed otherwise.
    - [x] Validate a converted CalRGB fixture as PDF/A-2b with the pinned official veraPDF
      container and retain its machine-readable XML report.
    - [x] Exercise both ICC outcomes: transform through an architecture-compatible
      LittleCMS runtime, and reject conversion when the native runtime or profile cannot be
      opened. The Linux WebApi image installs `liblcms2-2`; platform-specific local builds
      must provide a native library matching the .NET process architecture.
    - [x] Run the required-runtime contract in a native Linux .NET 10 container with
      `liblcms2-2`, convert an embedded-ICC fixture, and validate the result as PDF/A-2b
      with the pinned veraPDF container.
- [x] Emit an sRGB output intent and embedded ICC profile for generated PDF/A-2b and
  deterministically convert every non-sRGB source-colour path in the documented support
  boundary.
- [x] Produce required XMP identification and document metadata and synchronize it
  with the information dictionary.
- [x] Enforce profile restrictions for encryption, actions, JavaScript, multimedia,
  annotations, transparency, attachments, and external dependencies.
  - [x] Reject encryption, JavaScript name trees and actions, additional-action
    dictionaries, forbidden action types, multimedia structures, non-printing or hidden
    annotations, profile-incompatible attachment annotations, external stream/XObject
    dependencies, and non-default transfer functions with stable object-level diagnostics.
  - [x] Keep URI, GoTo, and GoToR actions available instead of incorrectly treating every
    external hyperlink as an archival dependency.
  - [x] Traverse indirect objects, stream dictionaries, nested direct dictionaries, and
    nested arrays with bounded depth so forbidden structures cannot bypass preflight.
  - [x] Complete the ISO 19005-2/3 structural rule matrix for annotation appearances,
    optional content, transparency, soft masks, blend modes, blend colour, and rendering
    intents within the supported PDF/A-2b/3b policy.
    - [x] Require valid annotation subtypes, flags, normal appearances, button appearance
      states, and static AcroForm/XFA behavior with stable `PXAPDFTRUST333`-`335`
      diagnostics.
    - [x] Require named, uniquely identified optional-content configurations, reject `/AS`,
      and verify that `/Order` includes every OCG reference with stable
      `PXAPDFTRUST336`-`339` diagnostics.
    - [x] Validate dictionary and content-stream rendering intents and extended graphics
      state blend modes against the ISO 32000-1 value sets with stable
      `PXAPDFTRUST340`-`341` diagnostics.
    - [x] Preserve PDF/A-2/3 transparency and soft masks instead of applying a false
      blanket prohibition. Traverse their Form content, require the document RGB output
      intent as the default blending colour space, and fail closed when a relevant content
      stream cannot be decoded for structural inspection (`PXAPDFTRUST343`).
    - [x] Keep the PXA result labelled as a bounded structural validator. The pinned
      veraPDF profile remains the standards-level release evidence rather than treating
      this first-party matrix as a replacement for all 144 external rules.
- [x] Add `generate`, `convert`, `validate`, and `preflight` operations with distinct
  results; conversion must never silently discard unsupported content.
- [x] Preserve the original when conversion reports `reviewRequired` or fails.
- [ ] Integrate veraPDF as one independent validator and compare against at least one
  additional commercial or standards-based validator before claiming support.
  - [x] Validate the deterministic generated fixture as PDF/A-2b with the official
    `ghcr.io/verapdf/cli:v1.31.166` image; all 144 evaluated rules pass.
  - [x] Validate unsigned and explicitly signature-stripped existing-PDF conversion
    fixtures with `ghcr.io/verapdf/cli:v1.31.166`; veraPDF reports `PASS` for profile
    `2b`, while visible signature appearances remain fail-closed.
  - [x] Validate generated and attachment-bearing converted PDF/A-3b fixtures with
    `ghcr.io/verapdf/cli:v1.31.166`; veraPDF reports `PASS` for profile `3b`.
  - [x] Validate converted colored/uncolored tiling-pattern, lossless CMYK-image, CalRGB,
    and embedded-ICC fixtures with `ghcr.io/verapdf/cli:v1.31.166`; veraPDF reports `PASS`
    for profile `2b` and retains XML evidence for each fixture.
  - [x] Add `tools/pdfa/validate-pdfa.sh` as a pinned, reproducible validator wrapper that
    accepts only `2b` or `3b`, fails on non-conformance, and retains machine-readable XML
    evidence for local and CI runs.
  - [x] Add `tools/pdfa/validate-pdfa-secondary.sh` for a licensed 3-Heights PDF Validator
    Shell run, retaining the detailed report plus validator version, profile, input hash,
    timestamp, and exit code. Missing binaries and license exit code 10 fail closed.
  - [ ] Add a second commercial or standards-based validator before the production
    support claim. The installed Adobe application reports `Reader:Full`, so its Pro-only
    Preflight API is unavailable. 3-Heights requires a separately supplied executable and
    valid license; a recorded pass from either product is still required.
- [x] Store validator name, version, profile, rule IDs, and machine-readable evidence
  in the compliance report.
- [x] Add the structural PDF/A-2b validation path to the Viewer and expose
  validate, preflight, and conversion actions; keep a second independent validator
  and production support policy as explicit open work.
  - [x] Let the Viewer select PDF/A-2b or PDF/A-3b, display stable findings with affected
    object numbers, and report successful conversion using the selected profile in all six
    Designer languages.
- [x] Add `POST /api/pxa/v1/pdf/compliance/validate` and
  `POST /api/pxa/v1/pdf/compliance/preflight` plus
  `POST /api/pxa/v1/pdf/compliance/convert`.
  - [x] Expose `POST /api/pxa/v1/pdf/compliance/validate` as an authenticated alias
    of the structural trust inspection pipeline.
  - [x] Implement explicit conversion with loss and review diagnostics.

## P1 - PDF/UA and Tagged PDF

- [ ] Implement Tagged PDF primitives: structure tree, parent tree, marked-content
  IDs, role map, namespaces, artifacts, language, alternate text, actual text, and
  table header associations.
- [x] Emit a PDF 1.7 structure tree, parent tree, per-page `StructParents`, MCIDs,
  explicit artifacts, document/element language, alternate text, actual text, and
  table header scope/associations from the PXA generator, including validated
  parent-child hierarchies for grouped, list, and table semantics.
- [x] Add validated PDF 1.7 role maps and namespace-aware source metadata for imported
  or provider-specific semantics, including Designer inspection, collision diagnostics,
  deterministic output, and independent veraPDF PDF/UA-1 validation.
- [ ] Emit native structure namespace dictionaries only with the future PDF 2.0 object
  model and evaluated PDF/UA-2 profile; PDF/UA-1 continues to use the standard RoleMap
  fallback required by its PDF 1.7 foundation.
- [x] Add a semantic document layer so accessibility is not inferred only from final
  drawing coordinates.
- [x] Preserve reading order independently from visual z-order in the generator and
  Designer JSON contract, including one logical element spanning multiple drawing
  operations.
- [x] Map Designer text, headings, paragraphs, lists, tables, links, images, forms,
  headers/footers, page numbers, charts, and decorative elements to explicit roles.
  - [x] Add an exhaustive Designer-registry contract for all 39 element types, assign
    explicit paragraph semantics to date/number/note content, keep destination-only
    bookmarks and static check marks as artifacts, and prevent static check marks from
    leaking unstructured AcroForm widgets into PDF/UA output.
  - [x] Independently validate a Designer-generated mixed fixture covering a table, link,
    text field, checkbox, radio group, option list, signature field, and reset button with
    the pinned official veraPDF PDF/UA-1 profile.
  - [x] Treat Designer-generated and imported shared header/footer elements as repeated
    artifacts in both the document model and backend renderer.
- [x] Carry explicit Designer element roles, reading order, language, alternatives,
  artifacts, and table-header associations through `DesignExportDto` into tagged PDF;
  automatic role defaults and the Designer property editor are now available.
- [x] Add suggested roles for newly inserted elements and a one-action upgrade for
  existing documents, while preserving explicit user choices and synchronizing heading
  roles with heading-level changes.
- [x] Add a localized Accessibility Inspector for role, artifact status, logical parent,
  reading order, language, alternate/actual text, and table header associations.
- [x] Emit granular `TR`, `TH`, and `TD` semantics from Designer table cells and add
  `OBJR`, `StructParent`, parent-tree, tooltip, and structure-order support for links,
  linked buttons, text fields, text areas, checkboxes, and populated dropdowns.
- [x] Add native accessible widgets and Designer mappings for radio groups, option lists,
  signature fields, and non-link submit/reset buttons, including radio parent fields,
  multi-select list values, signature fields, tooltips, and structure-object references.
- [x] Require document title, language, meaningful link text, form labels, image/chart
  alternatives, and table semantics where the selected profile requires them.
- [x] Fail PDF/UA-1 export when title, language, complete tagging, figure alternatives,
  table-header scope/references, embedded text fonts, or supported annotation semantics
  are absent.
- [x] Add keyboard/focus-order and annotation accessibility checks for interactive
  documents, order page annotations by explicit semantic reading order under `/Tabs /S`,
  and report implicit or duplicate focus positions without misrepresenting them as a
  successful explicit order.
- [ ] Ship PDF/UA-1 generation and validation first; evaluate PDF/UA-2 after the PDF
  2.0 object model and ecosystem validators are mature in PXA.
- [x] Add an accessibility preflight report with errors, warnings, element IDs,
  page references, and actionable remediation.
- [x] Add typed `PdfAccessibilityProfile.PdfUa1`, a fail-closed structural generator
  preflight, stable `PXAPDFUA...` diagnostics with page/element references, and
  `pdfuaid:part=1` identification metadata. Independent validation remains required
  before claiming full conformance.
- [ ] Validate with an independent PDF/UA validator and manual assistive-technology
  checks; automated validation alone is not sufficient.
  - [x] Validate maintained structure and native-widget fixtures against PDF/UA-1 with
    the pinned official veraPDF container and retain its machine-readable XML reports as
    CI artifacts.
  - [ ] Complete the human-checkable Matterhorn Protocol review and record manual
    keyboard, screen-reader, reading-order, form-label, table, link, and alternative-text
    evidence on supported desktop platforms.
- [x] Add Designer accessibility properties and prevent a compliance export when
  required semantic data is absent, with localized document- and element-level findings
  in Page Settings and the Export dialog.

## Viewer Experience

- [x] Add a `Trust` panel showing encryption, signatures, revisions, timestamps,
  PDF/A, PDF/UA, tagging, risky content, and trust-report diagnostics.
  - [x] Baseline panel shows structure, revisions, encryption, signature candidates,
    PDF/A/PDF/UA metadata claims, hashes, and diagnostics.
  - [x] Signature cards show cryptographic integrity, revision coverage, certificate
    trust, signer, digest, PAdES profile, timestamp status, timestamp time, and TSA
    identity plus separate signer/TSA revocation states.
  - [x] Show validated signer and TSA revocation evidence kind, status, source, and
    expiry without exposing raw CRL bytes.
  - [x] Show bounded DSS/VRI certificate, CRL, OCSP, and VRI counts plus matched
    per-signature VRI inventory and the resulting historical B-LT validation state.
  - [x] Inventory JavaScript and launch actions, embedded files, file-attachment
    annotations, rich media, XFA forms, and external links without claiming that
    every detected item is malicious.
- [x] Display `valid`, `invalid`, `indeterminate`, `unsupported`, and `notApplicable`
  states with distinct accessible icons, text, and explanations.
- [x] Show signature field location, signer/certificate details, signed revision,
  timestamp, chain status, revocation status, and modifications after signing.
  - [x] Show signer, certificate thumbprint, signing time, signed revision and byte
    offset, timestamp and revocation evidence, certification status, locked fields,
    and whether later revisions exist.
  - [x] Resolve and show the bound signature field name, page, and rectangle.
- [x] Add signature creation only through privileged backend operations; private
  keys must never enter browser storage.
- [x] Add redaction mark, preflight, apply, verification, compare-original, and
  download-result steps.
  - [x] Add the mark, preflight, apply, verification, and download-result steps.
  - [x] Add a compare-original view.
- [x] Require explicit confirmation that applying redaction creates a new document
  and may invalidate signatures.
- [x] Add compliance and accessibility issue navigation from a report item to the
  affected page, object, or Designer element.
  - [x] Navigate page-scoped redaction findings directly to the affected Viewer page.
  - [x] Add page, object, and Designer-element targets to compliance and accessibility
    report contracts and navigate to them.
- [x] Warn before an edit that invalidates an existing signature or compliance state.
- [x] Localize all trust states, warnings, diagnostics, actions, and accessibility
  labels in the six Designer languages.
  - [x] Localize all static Viewer trust states, actions, signing fields, mutation
    warnings, risk inventory, and accessibility labels in all six languages.
  - [x] Return localization keys and parameters for backend-generated diagnostics
    instead of displaying English diagnostic messages in every locale.
- [x] Add protected loading, offline, timeout, cancelled, unsupported, and policy-
  denied states instead of generic download failures.

## Designer and Generator Integration

- [ ] Extend `PdfSaveOptions` with typed signing, modern encryption, PDF/A, PDF/UA,
  redaction/sanitization, and validation policies without mixing these concerns.
  - [x] Keep modern encryption, PDF/A, and PDF/UA as typed writer options.
  - [ ] Add a separate typed post-processing orchestration contract for signing,
    redaction, sanitization, and final-byte validation; never place credentials in
    `PdfSaveOptions` or `DesignExportDto`.
- [x] Add C# Generator examples for every supported trust profile.
- [x] Extend JSON, C# Model, C# PDF Builder, and FromBase64String roundtrips for all
  trust settings and accessibility properties.
- [x] Add signing fields, accessibility metadata, compliance profile, and security
  options to Designer inspectors with permission and entitlement checks.
- [x] Prevent incompatible combinations before export and repeat the same validation
  server-side.
- [x] Keep trust operations out of preview-only rendering; Preview must clearly show
  that cryptographic validity exists only on the produced PDF bytes.
- [x] Ensure normal backend export and C# PDF worker output use the same trust services
  and produce equivalent validation reports.

Implementation evidence:

- `PdfTrustSettingsValidator` is shared by normal API export and every executable code
  representation, while the Designer performs the same preflight before transport.
- Semantic C# PDF output returns its canonical design to the WebApi and is rendered with
  `DesignJsonMapper.BuildSaveOptions`, exactly like normal Designer export.
- `docs/guides/PXA.Pdf-Trust-Generator-Examples.md` documents AES, certificate encryption,
  PDF/A, PDF/UA, PAdES, redaction, and sanitization without embedding secrets in templates.

## Migration Coverage

- [ ] Add a shared PDF Trust capability model used by all PDF code and Viewer
  migration providers.
- [ ] Inventory signature, timestamp, validation, encryption, redaction, PDF/A, and
  PDF/UA APIs for Nutrient, iText, Apryse, Aspose, Syncfusion, DevExpress, Foxit,
  DsPdf, GemBox, Spire, and Pdftools.
- [ ] Map a provider operation automatically only when the native PXA capability has
  equal security and compliance semantics.
- [ ] Emit `reviewRequired` when provider defaults, certificate stores, trust policy,
  redaction guarantees, or compliance levels cannot be inferred safely.
- [ ] Emit `unsupported` for provider profiles PXA cannot validate end to end.
- [ ] Preserve provider options as structured migration evidence instead of dropping
  them into comments only.
- [ ] Extend Nutrient code and Viewer migration after each native trust capability is
  delivered; remove deferred diagnostics only when parity tests prove the mapping.
- [ ] Add provider-specific full examples containing signing, encrypted input,
  redaction, archival conversion, and accessibility validation.
- [ ] Add migration reports linking each manual action to the matching PXA
  Documentation workflow.
- [ ] Never transform a visual redaction mark into a claim of applied secure
  redaction or an image of a signature into a valid digital signature.

## Authorization, Licensing, and Audit

- [ ] Define granular permissions for inspect, sign, validate, redact, compliance
  convert, compliance validate, trust-policy manage, and key-provider manage.
- [ ] Define product entitlements independently from permissions and roles.
- [ ] Require step-up authentication for key-provider changes, trust-policy changes,
  applied redaction, and organization-level signing operations.
- [ ] Audit actor, tenant, operation, policy, input/output hash, result, diagnostic
  codes, and key/certificate identifier without recording secrets or content.
- [ ] Make audit records append-only and correlate redaction/signature operations with
  immutable job records.
- [ ] Add Cloud and On-Premise configuration for trust stores, TSA, OCSP/CRL access,
  HSM/KMS adapters, offline mode, validator binaries, and retention.
- [ ] Fail startup or disable the affected feature when required trust dependencies
  are absent; never fall back to an insecure implementation.

## Documentation

- [ ] Add a public `PDF Trust` section to PXA.Documentation with separate Generator,
  Viewer, Migration, API, and troubleshooting paths.
- [ ] Document supported standards by exact profile, edition, validator, and known
  limitation.
- [ ] Explain cryptographic integrity, certificate trust, legal signature effect,
  and visual signature appearance as separate concepts.
- [ ] Document PAdES levels, TSA, LTV, trust stores, offline validation, and signature-
  invalidating changes with runnable examples.
- [ ] Document redaction mark versus applied redaction, preflight, sanitization,
  verification, audit evidence, and unsupported-content behavior.
- [x] Document PDF/A profiles, conversion loss risks, validation reports, attachments,
  and incompatibility with encryption.
- [ ] Document Tagged PDF, PDF/UA semantics, Designer remediation, and the limits of
  automated accessibility validation.
- [ ] Document AES-256, legacy RC4 compatibility, permissions, password handling, and
  certificate encryption status without exposing cryptographic internals useful for
  abuse.
- [ ] Add downloadable synthetic examples and expected trust reports; do not use
  customer files, real private keys, or production certificates.
- [ ] Add current screenshots for Viewer Trust, signature validation, redaction
  review, and compliance/accessibility reports.
- [ ] Update `PxaPdf-Provider-Feature-Gaps.md`, `Pdf-Encryption.md`,
  `PdfTools-WebViewer-Feature-Gaps.md`, provider migration checklists, the product
  roadmap, and the Documentation audit as capabilities ship.

## Verification Matrix

- [ ] Build a deterministic corpus containing generated and externally produced PDFs
  for every supported profile and failure state.
- [ ] Include malformed xref data, object streams, nested forms, transparency,
  rotations, clipping, layers, attachments, forms, JavaScript, incremental updates,
  multiple signatures, encrypted documents, and mixed page geometry.
- [ ] Add known-answer cryptographic tests and mutation tests that flip every signed
  region and validation-material component.
- [ ] Test PAdES B-B, B-T, B-LT, and B-LTA independently, including expired,
  untrusted, revoked, offline, and unavailable-revocation cases.
- [ ] Test AES-256 against independent readers and writers with Unicode passwords,
  all permission combinations, metadata options, wrong passwords, and tampering.
- [ ] Test PDF/A and PDF/UA through independent validators and store machine-readable
  reports as CI artifacts.
  - [x] Provide a deterministic generated PDF/A-2b fixture and verify it locally with
    veraPDF; PDF/A CI report publication remains open.
  - [x] Verify the conservative existing-PDF conversion fixture locally with veraPDF;
    CI report publication and a second validator remain open.
  - [x] Generate deterministic PDF/UA-1 structure, table, and native-widget fixtures,
    validate them with the pinned veraPDF `ua1` profile, and publish the PDFs and
    machine-readable XML reports as CI artifacts.
- [ ] Test redaction against text extraction, object inspection, image extraction,
  layer removal, incremental history, copy/paste, search, thumbnails, metadata, and
  attachments.
- [ ] Add differential fuzzing and bounded parser fuzz tests for trust-sensitive
  objects and incremental revisions.
- [ ] Add golden visual tests proving signatures, redaction results, tagged content,
  and compliance conversion do not corrupt layout.
- [ ] Run authenticated desktop and mobile Viewer smoke tests for inspect, sign,
  validate, redact, compare, and download.
- [ ] Run migration parity tests for every provider mapping before changing a manual
  diagnostic to automatic conversion.
- [ ] Run `dotnet build PXA.sln`, relevant .NET suites, Designer tests, typecheck,
  production builds, Documentation tests/build, security scanning, and
  `git diff --check`.

## Milestone Order

### Milestone 1 - Inspect and Preserve

- [ ] Deliver the typed existing-PDF object model, incremental save foundation,
  unified Trust Report, parser hardening, and Viewer Trust panel.
- [ ] Acceptance: PXA can inspect supported PDFs without mutation, preserve unknown
  objects, identify unsupported structures, and never present indeterminate results
  as valid.

### Milestone 2 - Sign and Validate

- [ ] Deliver PAdES B-B and B-T, signature validation, trust stores, signature Viewer
  UI, Generator/API integration, Documentation, and initial provider mappings.
- [ ] Acceptance: generated and external signatures pass independent validation;
  tampering and post-sign modifications are detected reliably.

### Milestone 3 - Redact and Sanitize

- [ ] Deliver redaction preflight, irreversible apply, sanitization, verification,
  audit evidence, Viewer compare flow, Documentation, and migration mappings.
- [ ] Acceptance: the supported corpus contains no recoverable covered data and PXA
  blocks unsupported redactions.

### Milestone 4 - Modern Security

- [ ] Deliver AES-256, encrypted existing-PDF processing, safe Designer controls,
  independent interoperability tests, Documentation, and provider mappings.
- [ ] Acceptance: modern encrypted files interoperate across supported readers and no
  new production workflow defaults to RC4.

### Milestone 5 - Archive and Accessibility

- [ ] Deliver PDF/A-2b generation/validation, then PDF/A-3b; deliver Tagged PDF and
  PDF/UA-1 generation/validation with Designer remediation.
- [ ] Acceptance: every claimed profile passes independent automated validation and
  the documented manual accessibility checks.

### Milestone 6 - Long-Term Trust and PDF 2.0

- [ ] Deliver PAdES B-LT/B-LTA, PDF/A-4, evaluated PDF/UA-2 support, remote/HSM key
  providers, and expanded provider migration parity.
- [ ] Acceptance: offline long-term validation and PDF 2.0 profiles pass the approved
  standards and interoperability matrix.

## Definition of Done

- [ ] PXA makes no broad `secure`, `compliant`, `accessible`, `valid`, or `redacted`
  claim without a profile-specific machine-readable report.
- [ ] Existing signatures and revisions are preserved or their invalidation is
  explicitly predicted, confirmed, and reported.
- [ ] Applied redaction is irreversible for the documented supported corpus and
  unsupported content is blocked.
- [x] AES-256 replaces RC4 as the default for new encrypted production PDFs.
- [ ] At least one PDF/A and one PDF/UA profile pass independent validation and have
  complete Generator, Designer, Viewer, API, Migration, and Documentation support.
- [ ] Customer-facing examples, known limitations, permissions, entitlements,
  diagnostics, release notes, and operational guidance are complete.
- [ ] All relevant security, interoperability, visual, migration, accessibility, and
  performance gates pass in CI.

## Explicitly Deferred

- [ ] Qualified electronic signature service-provider certification and legal
  conclusions about signature enforceability.
- [ ] Running a public timestamp authority, certificate authority, OCSP responder, or
  trust-list service.
- [ ] Universal repair of arbitrary malformed or hostile PDFs.
- [ ] Claiming full accessibility from automated PDF/UA validation alone.
- [ ] PDF/X, PDF/E, PDF/VT, healthcare-specific signatures, and country-specific trust
  schemes unless a customer-backed requirement promotes them.

## Assumptions

- PXA remains an offline-capable product; all network-dependent trust operations have
  explicit offline behavior.
- Private-key operations are abstracted so Cloud can use managed KMS/HSM services and
  On-Premise can use customer-controlled providers.
- Production support is introduced profile by profile, not as one all-or-nothing PDF
  compliance claim.
- Existing Viewer redaction is considered partial until the new preflight,
  sanitization, verification, and corpus gates pass.
- `VERSION` and the published release manifest remain unchanged until the normal
  release-preparation workflow.
