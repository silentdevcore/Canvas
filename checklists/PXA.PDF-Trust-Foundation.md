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
- [ ] Preserve unknown objects and byte ranges when an operation does not need to
  modify them.
- [ ] Support classic xref tables, xref streams, object streams, hybrid-reference
  files, inherited page properties, rotated pages, and malformed-but-recoverable
  inputs with diagnostics.
- [ ] Add bounded parsing for recursion, object count, stream size, decompression
  ratio, page count, nesting, and cancellation.
- [ ] Reject ambiguous or dangerous duplicate-object and shadow-object structures
  rather than validating a different revision than a PDF viewer displays.

### Save Strategies

- [ ] Implement full rewrite and incremental update as explicit save strategies.
- [x] Centralize the existing append paths in a deterministic incremental-update
  writer that validates the source revision chain, preserves original bytes, and
  reparses the saved result before returning it.
- [x] Reject incremental mutation of encrypted PDFs until appended objects can be
  serialized through the active security handler.
- [x] Use incremental updates for signatures and preservation-sensitive operations.
- [ ] Rebuild xref data, `/Prev`, `/Size`, object generations, stream lengths, and
  document IDs deterministically.
- [x] Add a change-impact analyzer showing which existing signatures and compliance
  claims a planned mutation will affect.
- [ ] Make post-save parse and validation mandatory before returning trusted output.

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

- [ ] Add typed signing options for certificate source, signature field, page and
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
- [x] Produce interoperable PAdES Baseline B-B signatures validated independently by
  OpenSSL CAdES verification and Poppler `pdfsig`.
- [x] Produce PAdES Baseline B-T signatures with a bounded, HTTPS-only RFC 3161 TSA
  client, validate nonce, message imprint, TSA EKU, token signature, and configured
  host trust, and expose B-T only when a server-side TSA is configured.
- [x] Add independent RFC 3161 document timestamps that protect the complete current
  PDF revision and remain separately visible in trust reports and the Viewer.
- [x] Support multiple sequential signatures without invalidating earlier revisions.

### Signature Validation

- [ ] Validate CMS integrity, signed byte ranges, certificate chain, key usage,
  signing time, timestamp token, revocation evidence, PAdES profile, and post-sign
  changes separately.
- [x] Validate detached CMS integrity, SHA-256/384/512 document digest, signed byte
  ranges, complete revision boundaries, SigningCertificateV2 binding, RSA signature,
  signing time, host certificate chain, and PAdES B-B profile as separate statuses.
- [x] Validate embedded RFC 3161 signature timestamps separately from approval
  signature integrity and report timestamp time, TSA identity, and B-T profile.
- [ ] Detect unsigned bytes, overlapping ranges, wrapping/shadow attacks, invalid
  incremental revisions, and disallowed changes after certification signatures.
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
- [ ] Make network retrieval of OCSP, CRL, AIA, and TSA data explicit, bounded,
  allowlisted, cached, and disableable for isolated installations.
  - [x] Retrieve certificate-declared CRLs only through exact-host HTTPS allowlists,
    without redirects, with bounded responses and a Data-Protection-encrypted cache.
  - [x] Add application-owned OCSP POST requests and certificate-specific encrypted
    response caching under the same HTTPS allowlist and response limits.
  - [ ] Add bounded AIA issuer retrieval under the same policy.
- [x] Keep certificate downloads disabled by default and make online revocation an
  explicit server policy with bounded retrieval, exact-host CRL allowlists, and a
  validated Data-Protection-encrypted CRL and certificate-specific OCSP cache.
- [x] Add `POST /api/pxa/v1/pdf/signatures/sign` and
  `POST /api/pxa/v1/pdf/signatures/validate` with streaming binary responses and
  structured validation reports.
- [ ] Add separate permissions for signature inspection, signing, trust-policy
  management, and use of organization key providers.
- [x] Add separate permissions for signature inspection, validation, and signing;
  trust-policy and organization key-provider permissions remain open.

## P0 - Secure and Irreversible Redaction

- [ ] Distinguish redaction marks, preview overlays, and applied irreversible
  redaction in models, APIs, UI, diagnostics, and documentation.
- [ ] Build a content-intersection analyzer for text glyphs, paths, images, masks,
  clipping paths, patterns, nested Form XObjects, transparency groups, annotations,
  AcroForm appearances, optional content, and rotated/cropped pages.
- [ ] Remove covered text operators and associated extractable text, not only paint
  a black rectangle.
- [ ] Remove or irreversibly transform covered image pixels and reusable resources
  without leaking the hidden original through an unused object or alternate stream.
- [ ] Sanitize metadata, XMP, attachments, embedded files, comments, form values,
  actions, JavaScript, thumbnails, previous revisions, and incremental history when
  requested by the redaction policy.
- [ ] Force a full rewrite when prior revisions could retain redacted content.
- [ ] Detect content that cannot be confidently removed and block `Apply redaction`
  instead of returning a visually redacted file.
- [ ] Add preflight, apply, and verify stages with separate reports and hashes.
- [ ] Verify that removed text cannot be extracted, searched, copied, recovered from
  objects, or revealed by disabling overlays/layers.
- [ ] Add a tamper-evident redaction audit record containing document hashes,
  non-sensitive regions, reason, actor, policy, timestamp, and verification result.
- [ ] Replace the current importer-supported redaction claim with capability-specific
  evidence and retain `reviewRequired` for unsupported structures.
- [ ] Add `POST /api/pxa/v1/pdf/redactions/preflight` and
  `POST /api/pxa/v1/pdf/redactions/apply` with privileged permission checks.

## P1 - AES-256 and Modern PDF Security

- [ ] Deprecate RC4 for new production documents while retaining explicit legacy
  read compatibility.
- [ ] Implement AES-128 only as an interoperability bridge where required.
- [ ] Implement the PDF 2.0 Standard Security Handler with AES-256 and the required
  revision-6 password processing, crypt filters, permissions, metadata behavior,
  random salts, and secure random generation.
- [ ] Use reviewed platform cryptography; do not hand-roll AES, hashing, random
  generation, or constant-time secret comparison.
- [ ] Zero or dispose password/key buffers where runtime APIs permit it.
- [ ] Support Unicode password normalization exactly as required by the selected PDF
  standard revision and test maximum password length behavior.
- [ ] Implement encrypted existing-PDF open, permission inspection, owner/user
  authentication, mutation, and re-save without leaking decrypted temporary files.
- [ ] Add certificate/public-key encryption as a later, separately gated profile.
- [ ] Update Designer Page Settings and export flows to offer safe algorithms only;
  legacy RC4 must be hidden behind an explicit compatibility option.
- [ ] Add password-strength guidance without logging or transmitting the password.
- [ ] Validate output with at least Acrobat, qpdf, and an independent PDF library.

## P1 - PDF/A Generation, Conversion, and Validation

- [ ] Ship one narrow, independently validated profile first: PDF/A-2b for generated
  documents.
- [ ] Add PDF/A-3b for workflows requiring arbitrary embedded source documents.
- [ ] Add PDF/A-4 only after the PDF 2.0 writer and validator policy are production
  ready.
- [ ] Define a typed compliance profile rather than a free-form string.
- [ ] Embed all required fonts and fail when embedding or Unicode mapping is not
  possible.
- [ ] Add output intents and ICC profiles with deterministic color conversion.
- [ ] Produce required XMP identification and document metadata and synchronize it
  with the information dictionary.
- [ ] Enforce profile restrictions for encryption, actions, JavaScript, multimedia,
  annotations, transparency, attachments, and external dependencies.
- [ ] Add `generate`, `convert`, `validate`, and `preflight` operations with distinct
  results; conversion must never silently discard unsupported content.
- [ ] Preserve the original when conversion reports `reviewRequired` or fails.
- [ ] Integrate veraPDF as one independent validator and compare against at least one
  additional commercial or standards-based validator before claiming support.
- [ ] Store validator name, version, profile, rule IDs, and machine-readable evidence
  in the compliance report.
- [ ] Add `POST /api/pxa/v1/pdf/compliance/validate` and
  `POST /api/pxa/v1/pdf/compliance/convert`.

## P1 - PDF/UA and Tagged PDF

- [ ] Implement Tagged PDF primitives: structure tree, parent tree, marked-content
  IDs, role map, namespaces, artifacts, language, alternate text, actual text, and
  table header associations.
- [ ] Add a semantic document layer so accessibility is not inferred only from final
  drawing coordinates.
- [ ] Preserve reading order independently from visual z-order.
- [ ] Map Designer text, headings, paragraphs, lists, tables, links, images, forms,
  headers/footers, page numbers, charts, and decorative elements to explicit roles.
- [ ] Require document title, language, meaningful link text, form labels, image/chart
  alternatives, and table semantics where the selected profile requires them.
- [ ] Add keyboard/focus-order and annotation accessibility checks for interactive
  documents.
- [ ] Ship PDF/UA-1 generation and validation first; evaluate PDF/UA-2 after the PDF
  2.0 object model and ecosystem validators are mature in PXA.
- [ ] Add an accessibility preflight report with errors, warnings, element IDs,
  page references, and actionable remediation.
- [ ] Validate with an independent PDF/UA validator and manual assistive-technology
  checks; automated validation alone is not sufficient.
- [ ] Add Designer accessibility properties and prevent a compliance export when
  required semantic data is absent.

## Viewer Experience

- [ ] Add a `Trust` panel showing encryption, signatures, revisions, timestamps,
  PDF/A, PDF/UA, tagging, risky content, and trust-report diagnostics.
  - [x] Baseline panel shows structure, revisions, encryption, signature candidates,
    PDF/A/PDF/UA metadata claims, hashes, and diagnostics.
  - [x] Signature cards show cryptographic integrity, revision coverage, certificate
    trust, signer, digest, PAdES profile, timestamp status, timestamp time, and TSA
    identity plus separate signer/TSA revocation states; risky content and
    attachments remain open.
  - [x] Show validated signer and TSA revocation evidence kind, status, source, and
    expiry without exposing raw CRL bytes.
  - [x] Show bounded DSS/VRI certificate, CRL, OCSP, and VRI counts plus matched
    per-signature VRI inventory and the resulting historical B-LT validation state.
- [ ] Display `valid`, `invalid`, `indeterminate`, `unsupported`, and `notApplicable`
  states with distinct accessible icons, text, and explanations.
- [ ] Show signature field location, signer/certificate details, signed revision,
  timestamp, chain status, revocation status, and modifications after signing.
- [ ] Add signature creation only through privileged backend operations; private
  keys must never enter browser storage.
- [ ] Add redaction mark, preflight, apply, verification, compare-original, and
  download-result steps.
- [ ] Require explicit confirmation that applying redaction creates a new document
  and may invalidate signatures.
- [ ] Add compliance and accessibility issue navigation from a report item to the
  affected page, object, or Designer element.
- [ ] Warn before an edit that invalidates an existing signature or compliance state.
- [ ] Localize all trust states, warnings, diagnostics, actions, and accessibility
  labels in the six Designer languages.
- [ ] Add protected loading, offline, timeout, cancelled, unsupported, and policy-
  denied states instead of generic download failures.

## Designer and Generator Integration

- [ ] Extend `PdfSaveOptions` with typed signing, modern encryption, PDF/A, PDF/UA,
  redaction/sanitization, and validation policies without mixing these concerns.
- [ ] Add C# Generator examples for every supported trust profile.
- [ ] Extend JSON, C# Model, C# PDF Builder, and FromBase64String roundtrips for all
  trust settings and accessibility properties.
- [ ] Add signing fields, accessibility metadata, compliance profile, and security
  options to Designer inspectors with permission and entitlement checks.
- [ ] Prevent incompatible combinations before export and repeat the same validation
  server-side.
- [ ] Keep trust operations out of preview-only rendering; Preview must clearly show
  that cryptographic validity exists only on the produced PDF bytes.
- [ ] Ensure normal backend export and C# PDF worker output use the same trust services
  and produce equivalent validation reports.

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
- [ ] Document PDF/A profiles, conversion loss risks, validation reports, attachments,
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
- [ ] AES-256 replaces RC4 as the default for new encrypted production PDFs.
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
