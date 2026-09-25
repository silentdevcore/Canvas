# PXA Nutrient Code and Viewer Migration

## Goal

Deliver first-class migration paths from Nutrient document-processing code and
Nutrient Web SDK applications into PXA. Preserve historical product identities
such as PSPDFKit and GdPicture.NET so customers can migrate both current and
legacy implementations.

This milestone covers migration analysis, deterministic conversion, diagnostics,
PXA Viewer configuration, annotation and form-data conversion, UI integration,
documentation, and tests. It does not implement the separate PDF Trust
Foundation. Unsupported PXA capabilities must remain explicit instead of being
represented as successfully migrated.

## Implementation Status (2026-09-24)

- [x] Added the first Roslyn-based Nutrient .NET/GdPicture code provider with
  document creation, pages, standard fonts, measurement units, coordinate
  origin, text, save, disposal, and explicit gap diagnostics.
- [x] Registered canonical code provider `nutrient`, GdPicture aliases, WebApi
  converter metadata, and provider-specific Designer authorization.
- [x] Added the first isolated Nutrient Viewer project and canonical
  `POST /api/pxa/v1/migrations/pdf/viewer/convert` endpoint.
- [x] Added typed PSPDFKit Instant JSON v1 conversion for the currently
  supported PXA annotation families, page geometry, rotation, CropBox offsets,
  ID remapping, and redaction-mark warnings.
- [x] Added both implementation projects and both test projects to `PXA.sln`.
- [x] Added a comprehensive synthetic Beta example spanning GdPicture C#,
  TypeScript Viewer configuration, Viewer events, Instant JSON annotations,
  forms, rotated/CropBox page geometry, and explicit trust/security gaps.
- [x] Added non-executing Esprima AST analysis for JavaScript
  `NutrientViewer.load` and `PSPDFKit.load` bootstrap/configuration source,
  including aliases, literal view state, secure source handling, checksums,
  diagnostics, source map, normalized settings, and a generated PXA module.
- [x] Added the localized Designer Viewer Migration workflow for JavaScript,
  TypeScript, and Instant JSON plus the typed one-time Viewer handoff for
  initial page and fit.
- [x] Expanded Instant JSON conversion with independent ink strokes, markup
  quads, line caps, normalized opacity, safe metadata, lock state, supported
  form fields, and form-value payloads.
- [x] Added public Nutrient Viewer migration guidance, mapping tables,
  diagnostics, and a Documentation contract test.
- [x] Verified the completed Beta scope with 11 Nutrient .NET tests, 24 Viewer
  converter tests, 19 PDF provider tests, targeted API/security/feature-gate
  tests, all 395 Designer tests, all 12 Documentation tests, both production
  builds, release validation, and `git diff --check`.
- [x] Added a TypeScript Compiler API boundary in Designer. The API receives the
  original TypeScript and compiler-produced JavaScript, analyzes the latter
  without execution, and checksums the original source.
- [x] Expanded the .NET hardening scope with official GdPicture mappings for
  static page selection, rotation, text boxes, lines, rectangles, file-image
  resources, web links, text and checkbox fields, sticky notes, and stateful
  line/fill appearance. Right-side origins are rejected with review diagnostics
  instead of producing incorrect geometry. Generated comprehensive output is
  compiled against `PXA.Pdf` in tests.
- [x] Added the first exact existing-document editing path: `LoadFromFile`,
  `MovePage`, and `DeletePage` migrate to the typed `PXA.Importer` page editor,
  preserve reachable page content and resources, and save through a full
  rewrite instead of reconstructing the document through Designer elements.
- [x] Added fail-closed page-deletion rules for the last remaining page and for
  pages still reachable through bookmarks, destinations, links, annotations,
  or other PDF objects.
- [x] Added `PdfEditableDocument` and collision-safe page graph composition for
  `LoadFromStream`, runtime status checks, conditional workflows, `ClonePage`,
  blank `InsertPage`, path-based `Merge2Documents`, and loaded-document-array
  `MergeDocuments` without flattening pages through the Designer model.
- [x] Added path-array `MergeDocuments`; attachment-aware merge options remain
  fail-closed with `CANMIGNUTNET069` because copying or excluding catalog-level
  embedded files is not yet semantically equivalent.
- [x] Added exact existing-PDF `CropBox` and page-range extraction support.
  `SetPageBox(PdfPageBoxCropBox, left, top, right, bottom)`,
  `GetPageBox(PdfPageBoxCropBox, ref left, ref top, ref right, ref bottom)`,
  and `RemovePageBox(PdfPageBoxCropBox)` preserve the existing page dictionary,
  resources, and content instead of reconstructing through Designer. The
  supported `ClonePages(FromPDF, PageRange)` overload and the typed split API
  use one-based page ranges and retain/remap the complete reachable graph.
- [x] Preserve Nutrient page-box coordinate order and the active point,
  millimeter, centimeter, or inch measurement unit symmetrically for both
  `SetPageBox` and `GetPageBox`.
- [x] Added stable diagnostics `CANMIGNUTNET070` for exact `CropBox` mapping and
  `CANMIGNUTNET071` for exact page-range append mapping. Unsupported page
  boxes, encrypted sources, and non-range split modes remain explicit review
  cases. `ClonePages` appends the selected pages to an existing target in source
  range order and selects the last appended page, matching Nutrient semantics.
- [x] Added focused importer and Nutrient tests for CropBox mutation and reads,
  page-range split/extraction, graph preservation, invalid ranges, duplicate
  selections, encrypted boundaries, and compilable generated code.
- [x] Preserve Nutrient stream ownership, one-based page positions, operation
  order, repeated loads, multiple document variables, and `GdPictureStatus.OK`
  control flow in generated PXA code.
- [ ] Next: expand .NET semantic coverage to additional annotations and form
  families, conversion, OCR service handoff, and async/cancellation patterns.
- [x] Replaced generated `TODO` statements for advanced operations with stable,
  actionable migration-review entries. OCR is not confused with image-to-design
  OCR, TWAIN remains an external hardware boundary, irreversible redaction points
  to the PXA Trust pipeline, and signing never copies certificate secrets.
- [x] Mapped the supported two-password Nutrient `Encrypt(owner, user)` pattern
  to `PdfSaveOptions.Encryption` with AES-256, runtime secret lookups, compile
  coverage, and a fail-closed PDF/A-plus-encryption conflict diagnostic.
- [ ] Next: validate against representative customer-approved fixtures and
  broaden semantic Viewer event/configuration mappings.

## Product and API Families

- [x] Inventory current Nutrient .NET SDK packages, namespaces, and public API
  patterns from official documentation and representative licensed samples.
- [x] Inventory legacy GdPicture.NET package and namespace variants still found
  in customer applications.
- [ ] Inventory current Nutrient Java SDK packages and the `io.nutrient.sdk`
  surface.
- [x] Inventory current `NutrientViewer` Web SDK imports, configuration, instance
  methods, events, annotations, forms, and document operations.
- [x] Inventory historical `PSPDFKit` Web SDK package names, global names,
  configuration objects, Instant JSON, and deprecated PSPDFKit Server patterns.
- [ ] Inventory Document Engine REST and synchronization integration separately
  from client-only Web SDK usage.
- [ ] Store the dated API inventory and fixture provenance without committing
  Nutrient binaries, license keys, proprietary source, or customer code.

Official reference baseline:

- https://www.nutrient.io/guides/dotnet/
- https://www.nutrient.io/guides/java/
- https://www.nutrient.io/guides/web/intro/
- https://www.nutrient.io/guides/document-engine/intro/
- https://www.nutrient.io/guides/web/importing-exporting/instant-json/

## Migration Taxonomy and Projects

- [x] Extend the migration taxonomy with `Viewer` as a valid migration kind:
  `PXA.Migration.<Domain>.Viewer.<Provider>`.
- [x] Add `PXA.Migration.Pdf.Code.Nutrient` under `src/Migrations/PDF`.
- [x] Add `PXA.Migration.Pdf.Viewer.Nutrient` under `src/Migrations/PDF`.
- [x] Add `PXA.Migration.Pdf.Code.Nutrient.Tests` under `tests`.
- [x] Add `PXA.Migration.Pdf.Viewer.Nutrient.Tests` under `tests`.
- [x] Reference both projects from `PXA.sln` and the appropriate PDF migration
  aggregator.
- [x] Use canonical provider name `Nutrient` and technical provider key
  `nutrient` for code migration.
- [x] Use technical provider key `nutrient-viewer` for Viewer migration.
- [x] Accept documented input aliases such as `pspdfkit`, `pspdfkit-web`, and
  `gdpicture` while returning canonical provider metadata.
- [x] Keep the Apryse/PDFTron provider completely separate; Apryse and Nutrient
  are unrelated migration targets.
- [x] Update `Migration-Namespace-Taxonomy.md` only when implementation begins.

## Shared Migration Contract

- [x] Define one structured Nutrient Viewer migration result shared by API and UI.
- [x] Consolidate the user-facing Viewer result into one versioned, importable
  `pxa-viewer-package` instead of exposing disconnected generated files.
- [x] Support package download and re-import in Designer.
- [x] Preview migrated settings, annotations, and form values in the real PXA
  PDF Viewer, using an attached PDF or generated geometry-only preview pages.
- [x] Integrate the customer-facing Nutrient workflow into PDF Designer Migration.
- [x] Require the original PDF, import it into `DesignExportDto`, and overlay
  supported Nutrient annotations and matching form values as editable content.
- [x] Return one editable design with backend preview and Open-in-Designer instead
  of exposing the Viewer package as the primary UI result.
- [x] Keep the lower-level Viewer package endpoint as a compatibility and SDK
  contract while redirecting the old Designer route to the integrated workflow.
- [x] Return the detected product family, API generation, source language,
  generated target artifacts, and canonical provider identity.
- [x] Return `documentFidelity` as `exact`, `compatible`, `reviewRequired`, or
  `unsupported`.
- [x] Return `sourcePreservation` as `preserved`, `regenerated`, or
  `structureLost`.
- [ ] Return stable diagnostics with code, severity, source location, affected
  API, PXA replacement, manual action, and Documentation link.
- [x] Return a source map between recognized Nutrient calls and generated PXA
  statements or Viewer settings.
- [x] Return a capability report containing migrated, approximated, omitted,
  and blocked features.
- [x] Never report `exact` when the source uses behavior unavailable in PXA.
- [ ] Preserve unknown but serializable metadata in a bounded, versioned
  extension object where this is safe.
- [x] Add deterministic checksums for source, normalized migration model, and
  generated artifacts.

## Track A: Nutrient .NET Code Migration

### Detection and Analysis

- [ ] Detect current Nutrient .NET SDK usage and legacy GdPicture.NET usage by
  package, namespace, type, and invocation symbols.
- [ ] Use Roslyn syntax and semantic analysis rather than regex-only rewriting.
- [ ] Detect aliases, fully qualified names, extension methods, async calls,
  helper wrappers, and dependency-injected Nutrient services.
- [x] Detect hard-coded license keys and emit a security diagnostic without
  copying the secret into generated code or logs.
- [ ] Classify every recognized call into document lifecycle, pages, content,
  annotations, forms, signatures, security, redaction, OCR, conversion,
  optimization, extraction, barcode, scanning, printing, or Viewer integration.

### Deterministic Mappings

- [x] Map supported document creation and disposal plus file-based existing-PDF
  load/save patterns to the appropriate PXA generator or importer APIs.
- [ ] Map all page add, insert, remove, reorder, rotate, crop, merge, and split
  patterns where an equivalent PXA operation exists. Static creation, blank
  insertion, rotation, remove, reorder, cross-document clone, supported merge
  overloads, `CropBox` set/read/reset, and explicit page-range extraction are
  covered. Other page-box kinds, bookmark/barcode/repeating-content split modes,
  and encrypted source workflows remain open.
- [ ] Map text, image, shape, link, bookmark, metadata, and basic page-content
  operations where semantic equivalence can be proven.
- [ ] Map supported annotations to PXA note, free text, stamp, image, line,
  rectangle, circle, ink, highlight, underline, and strikeout contracts.
- [ ] Map supported AcroForm text, checkbox, radio, dropdown, and list fields.
- [ ] Map PDF/image conversion and OCR calls only to existing PXA importer,
  export, and OCR application services with equivalent options.
- [ ] Map barcode operations only where PXA supports the same barcode type,
  encoding, error correction, and output semantics.
- [ ] Preserve cancellation and async behavior where the target PXA API supports
  it.
- [x] Generate compilable PXA code with stable formatting and no Nutrient
  package references for automatically migrated operations.

### Honest Gap Diagnostics

- [x] Mark PDF digital signatures, PAdES, timestamping, and validation as
  `reviewRequired` or `unsupported` until the PDF Trust Foundation is delivered.
- [x] Mark irreversible redaction as unsupported when PXA can only create a
  visual overlay.
- [x] Mark unsupported PDF/A or PDF/UA conversion and validation explicitly.
- [x] Mark AES-256, certificate security, advanced permissions, linearization,
  and advanced optimization according to actual PXA support.
- [x] Mark TWAIN/WIA scanning, native printing, proprietary OCR options,
  hyper-compression, and unsupported image processing for manual replacement.
- [x] Never replace an unsupported security operation with a no-op.
- [x] Never silently remove a signature, redaction, encryption, retention, or
  compliance operation.

## Track B: Nutrient Java Code Migration

- [ ] Treat Java as a separate compiler-language track rather than passing it
  through the Roslyn converter.
- [ ] Select and document a maintained Java parser with symbol-resolution
  support before implementation.
- [ ] Detect `io.nutrient.sdk` imports, `Document.open`, export, conversion,
  editing, extraction, OCR, annotation, form, signature, and redaction patterns.
- [ ] Normalize recognized Java operations into the same language-neutral
  migration model used by the .NET converter.
- [ ] Generate C# PXA code first; generating a future PXA Java SDK target remains
  blocked until that SDK is published.
- [ ] Preserve source locations and diagnostics across Java AST to C# output.
- [ ] Keep Java-specific resource handling, exceptions, concurrency, and stream
  semantics visible in the migration report.
- [ ] Deliver Java support after the C#/.NET mapping contract and fixture suite
  are stable.

## Track C: Nutrient and PSPDFKit Web Viewer Migration

### Input and Output

- [x] Accept JavaScript Viewer integration source through a non-executing AST
  parser.
- [x] Accept TypeScript-specific Viewer integration source through the Designer
  TypeScript Compiler API boundary and analyze only its non-executed output.
- [ ] Accept an optional structured Viewer configuration object separately from
  source code.
- [x] Accept optional Instant JSON as a dedicated typed input, not as executable
  JavaScript.
- [ ] Add XFDF input in a later subphase after the PXA annotation interchange
  contract is finalized.
- [x] Generate a PXA Viewer integration module, normalized Viewer settings,
  annotation sidecar, form-value payload, diagnostics, and a manual follow-up
  report.
- [x] Never execute uploaded JavaScript or TypeScript during migration.

### Viewer Bootstrap and Configuration

- [x] Detect both `NutrientViewer.load(...)` and historical
  `PSPDFKit.load(...)` bootstrap calls.
- [x] Map literal container selection to the generated PXA Viewer mount
  contract.
- [ ] Map document Blob, ArrayBuffer, and application-owned URL sources to PXA
  file or handoff sources without copying arbitrary URLs blindly.
- [x] Remove Nutrient license keys and emit a secure configuration diagnostic.
- [ ] Map initial page, zoom, fit mode, scroll mode, spread/layout mode, locale,
  theme, and permitted toolbar actions where PXA has equivalents.
- [x] Keep download, print, annotate, form-fill, and redact visibility under
  server-authorized PXA capabilities rather than accepting migrated client
  flags.
- [x] Translate supported initial page and fit mode into the typed PXA Viewer
  handoff while retaining locale, theme, and toolbar requests for review.
- [ ] Translate additional download, print, annotate, form-fill, and redact UI visibility
  into server-authorized PXA capabilities rather than trusting client flags.
- [ ] Map supported search, page navigation, thumbnail, outline, and bookmark
  behavior.
- [x] Preserve custom toolbar and UI customization requirements as structured
  review items when no deterministic PXA equivalent exists.
- [ ] Mark custom renderers, custom overlays, AI Assistant, measurement tools,
  layers, comparison, collaboration, and unsupported editing modes honestly.

### Events and Application Integration

- [ ] Inventory and map document-loaded, page-change, view-state, annotation,
  form, text-selection, search, bookmark, print, and download events.
- [ ] Generate typed PXA callback stubs for supported event equivalents.
- [ ] Preserve application-owned business logic around callbacks and mark only
  the SDK-specific portion for regeneration.
- [ ] Detect direct calls to remote Document Engine or PSPDFKit Server and
  generate a deployment/data-flow review rather than embedding old endpoints.
- [x] Map authentication to existing PXA session, tenant, entitlement, and
  permission contracts; never migrate source JWTs or API keys.
- [ ] Identify real-time Instant synchronization and collaboration as a separate
  architecture gap until PXA supports equivalent semantics.

### Instant JSON and Annotation Conversion

- [x] Parse Instant JSON by declared format and version, including historical
  `https://pspdfkit.com/instant-json/v1` documents.
- [ ] Validate payload size, page indexes, IDs, coordinates, colors, dates, and
  annotation-specific fields before conversion.
- [x] Map notes, free text, stamps, images, redactions, lines, rectangles,
  ellipses, ink, highlights, underlines, and strikeouts to PXA annotation types.
- [x] Convert Nutrient point-based bounding boxes to PXA page-relative
  percentages using the actual CropBox, rotation, and page dimensions.
- [x] Convert line endings, stroke, fill, opacity, quad points, and ink point
  lists where PXA supports them.
- [x] Preserve stable IDs where collision-free; otherwise emit an ID remap table.
- [x] Preserve author, creation time, update time, lock state, and safe custom
  metadata.
- [ ] Map comment threads and replies only after PXA defines an equivalent
  threaded-comment contract; otherwise retain them in the report and extension
  data.
- [x] Diagnose unsupported callouts, polygons, polylines, attachments, sound,
  screen/media, measurement, and custom annotation types.
- [x] Distinguish a redaction mark from applied irreversible redaction.
- [x] Never claim that a converted visual redaction has removed source content.

### Forms and Signatures

- [x] Map supported text, multiline text, checkbox, radio, dropdown, and list
  form values and field metadata.
- [x] Preserve required, read-only, option, and default-value semantics where
  PXA supports them.
- [x] Diagnose push buttons, JavaScript actions, calculations, validation
  scripts, and unsupported widget appearances.
- [ ] Preserve signature-field placement as review metadata without claiming a
  valid migrated digital signature.
- [x] Never export private keys, certificate material, saved electronic
  signatures, biometric data, or signature-service credentials.

## API and Security

- [x] Keep `POST /api/migration/convert` compatible for Nutrient C# code
  migration through provider key `nutrient`.
- [x] Add a canonical Viewer endpoint under
  `/api/pxa/v1/migrations/pdf/viewer/convert`.
- [x] Extend `/api/migration/frameworks` and its canonical successor with
  `domain: pdf`, `migrationKind: viewer`, and `provider: Nutrient`.
- [x] Add separate permissions for Nutrient code migration and Nutrient Viewer
  migration under the existing provider-specific authorization model.
- [x] Register `designer.nutrient-viewer-migration` as a server-enforced Beta
  feature with the existing organization and entitlement precedence rules.
- [ ] Require authenticated session, active organization, migration entitlement,
  provider permission, feature gate, rate limit, and audit.
- [x] Reject arbitrary local paths, unapproved remote URLs, executable packages,
  and oversized sources.
- [ ] Strip or redact license keys, JWTs, API keys, credentials, customer URLs,
  and document contents from logs and telemetry.
- [ ] Apply tenant isolation and configured retention to source and result
  artifacts.
- [ ] Use asynchronous jobs for multi-file projects or large annotation
  payloads; keep small source conversions synchronous initially.

## Migration UI

- [x] Add Nutrient to PDF Code Migration with display name
  `Nutrient .NET (formerly GdPicture.NET)`.
- [x] Add `Nutrient Viewer` to PDF Designer Migration and remove the standalone
  Viewer Migration tab from the customer-facing Designer navigation.
- [x] Do not present Apryse and Nutrient as aliases or versions of one another.
- [ ] Add input modes for C#, Java, JavaScript/TypeScript, Viewer configuration,
  and Instant JSON according to implemented rollout phase.
- [ ] Show detected API family and generation before conversion.
- [ ] Show generated files, structural diff, source map, fidelity, migrated
  capabilities, blocked security operations, and manual actions.
- [ ] Provide side-by-side Nutrient/PXA Viewer configuration comparison.
- [x] Provide an isolated PXA Viewer preview using synthetic or user-provided
  authorized documents.
- [ ] Never show a successful status when blocked security or compliance
  operations remain.
- [x] Localize the workflow, diagnostics, and accessibility labels in all six
  Designer languages.

## Documentation

- [x] Add a public Nutrient migration overview to PXA.Documentation.
- [x] Add public guidance for Nutrient .NET/GdPicture code and Nutrient Web
  SDK/PSPDFKit Viewer migration. Keep the Java guide open until Track B exists.
- [x] Document provider detection, accepted aliases, supported API generations,
  inputs, outputs, fidelity levels, and security handling.
- [x] Publish Beta mapping tables for code APIs, Viewer configuration,
  annotations, forms, and Instant JSON. Event mappings remain open.
- [x] Document Beta unsupported and review-required capabilities, especially
  signatures, applied redaction, PDF/A, PDF/UA, collaboration, and custom UI.
- [x] Add complete before/after examples with synthetic code, response payloads,
  and documents only.
- [ ] Add a guide for replacing Document Engine deployment and authentication
  without exposing internal operator configuration.
- [x] Add troubleshooting by stable `CANMIGNUT...` diagnostic family.
- [ ] Link the relevant PXA Viewer, forms, annotations, permissions, migration,
  and deployment documentation.
- [x] Update the Documentation search index and migration provider coverage
  tables.

## Diagnostic Families

- [x] Reserve `CANMIGNUTNET...` for Nutrient .NET and GdPicture code migration.
- [ ] Reserve `CANMIGNUTJAVA...` for Nutrient Java migration.
- [x] Reserve `CANMIGNUTWEB...` for Nutrient Web SDK and PSPDFKit source
  migration.
- [x] Reserve `CANMIGNUTJSON...` for Instant JSON validation and conversion.
- [ ] Keep diagnostic identifiers stable after the first public Beta.
- [ ] Include severity, source location, affected feature, migration outcome,
  manual action, and Documentation link in every diagnostic definition.

## Fixtures and Tests

- [x] Create minimal synthetic fixtures for current Nutrient .NET and legacy
  GdPicture.NET lifecycle patterns.
- [x] Add C# fixtures for pages, content, annotations, forms, OCR, conversion,
  signatures, encryption, redaction, PDF/A, and unsupported scanning APIs.
- [ ] Add Java fixtures only after selecting the parser and freezing the
  language-neutral operation model.
- [x] Add synthetic JavaScript fixtures for `NutrientViewer.load`,
  `PSPDFKit.load`, import aliases, view state, toolbar requests, events,
  embedded secrets, unsafe paths, and dynamic values.
- [ ] Create the remaining Web fixtures for forms, custom integrations,
  Document Engine, and deprecated PSPDFKit Server.
- [x] Create Instant JSON fixtures for every mapped PXA annotation type,
  rotations, CropBoxes, comment threads, unsupported annotations, and malformed
  input.
- [x] Verify point-to-percentage coordinate conversion against portrait,
  landscape, rotated, cropped, and mixed-size pages.
- [x] Verify form field values and options without leaking signature material.
- [ ] Add compilation tests for generated C# and TypeScript artifacts.
- [ ] Add provider detection, alias, taxonomy, permission, API, tenant-isolation,
  rate-limit, retention, and audit tests.
- [ ] Add security tests for embedded secrets, malicious source, oversized JSON,
  prototype-pollution keys, path traversal, remote URLs, and executable input.
- [ ] Add fidelity and diagnostics snapshot tests so unsupported operations
  cannot silently disappear.
- [ ] Add authenticated desktop and mobile migration UI smoke tests.
- [x] Run the full migration test groups, relevant Viewer/API tests, Designer
  tests, typecheck, production build, and `git diff --check`.

## Delivery Phases

### Phase N0: Contract and Inventory

- [ ] Freeze API-family detection, taxonomy, migration result, diagnostics,
  fixtures, and security boundaries.
- [ ] Produce a mapping matrix before implementing source rewriting.

### Phase N1: Nutrient .NET Beta

- [ ] Deliver Roslyn-based C# detection and deterministic mappings for document
  lifecycle, pages, basic content, supported annotations, forms, and conversion.
- [ ] Deliver honest diagnostics for all recognized unsupported capabilities.
- [ ] Integrate API, permissions, UI, Documentation, and tests.

### Phase N2: Web Viewer Beta

- [x] Deliver JavaScript `NutrientViewer` and `PSPDFKit`
  bootstrap/configuration migration.
- [x] Deliver Instant JSON conversion for PXA-supported annotations and forms.
- [x] Deliver PXA Viewer preview, comparison, Documentation, and tests.
- [x] Deliver TypeScript Compiler API input, deterministic checksums, capability
  reporting, and the comprehensive synthetic Beta example.
- [x] Expose the comprehensive Viewer/Instant JSON and GdPicture C# examples
  through separately authorized endpoints and load them from the corresponding
  Designer workflows.

### Beta Scope Boundary

- [x] Freeze the first public Beta to Nutrient/GdPicture C#, JavaScript,
  TypeScript, Viewer configuration, and Instant JSON single-file conversion.
- [x] Keep Java, XFDF, multi-project jobs, Document Engine replacement, and
  native PDF Trust capabilities visible as later milestones rather than
  presenting them as Beta-complete.

### Phase N3: Hardening

- [ ] Expand semantic C# mappings and supported Viewer events.
  - [x] Map official static page selection, rotation, text boxes, primitive
    graphics, file-image resources, web links, text/checkbox fields, and sticky
    notes with origin- and unit-aware geometry.
  - [x] Compile generated comprehensive C# against `PXA.Pdf` in the test suite.
  - [ ] Add remaining annotation and form families, existing-document editing,
    OCR/conversion service handoff, and async/cancellation mappings.
- [ ] Add XFDF conversion and bounded multi-file project input.
- [ ] Validate against representative customer-approved samples.
- [ ] Publish a provider compatibility matrix by API generation.

### Phase N4: Java

- [ ] Deliver Java AST analysis and C# PXA generation after N1-N3 contracts are
  stable.
- [ ] Add Java-specific examples, diagnostics, and Documentation.

## PDF Trust Integration Follow-up

Implementation tracker: [PXA.PDF-Trust-Foundation.md](PXA.PDF-Trust-Foundation.md)

- [x] PXA now provides native Trust capabilities for signatures/PAdES,
  irreversible redaction, PDF/A, PDF/UA, and AES-256 as tracked by the linked
  PDF Trust checklist.
- [ ] Map Nutrient signature, timestamp, validation, and certificate calls to
  semantically equivalent PXA Trust workflows.
- [ ] Map Nutrient redaction calls to PXA preflight, apply, verification, and
  audit workflows without replacing removal with visual overlays.
- [ ] Map additional Nutrient PDF/A and PDF/UA profiles only where target
  generation and validation semantics are equivalent.
- [x] Map Nutrient `PDF_A_2b` generation requests to
  `PdfComplianceProfile.PdfA2B` with explicit font, regeneration, and review warnings.
- [x] Map the supported Nutrient password-encryption pattern to PXA AES-256.
- [ ] Map advanced Nutrient permissions and certificate-security options to
  equivalent PXA policies; keep unmatched options as review items.
- [ ] Connect migrated signature and compliance operations to the existing PXA
  Viewer Trust UI.

The Nutrient migrators detect and report these APIs today, but they must not
claim functional migration until each source call is mapped to an equivalent
target capability with validation, Viewer workflow, and interoperability
evidence.

## Release and Acceptance

- [x] Add a customer-facing `minor` release fragment when the first Nutrient
  Beta is complete; do not change `VERSION` outside release preparation.
- [x] PXA detects current and historical Nutrient identities without confusing
  them with Apryse.
- [x] Generated C# and Viewer integration code builds without Nutrient
  dependencies for automatically migrated features.
- [x] Instant JSON annotations retain supported geometry, appearance, identity,
  page assignment, and metadata.
- [x] Unsupported security, signature, compliance, redaction, collaboration, and
  custom-UI behavior is never silently discarded.
- [x] Every conversion returns fidelity, source preservation, diagnostics,
  capability coverage, and manual follow-up.
- [ ] Code and Viewer operations are authenticated, permissioned, entitled,
  tenant-isolated, rate-limited, and audited.
- [x] Public Documentation enables a customer to assess and perform a supported
  Nutrient migration without internal assistance.
- [ ] All contract, migration, Viewer, API, security, Documentation, and build
  validation passes before Beta publication.

## Assumptions

- Nutrient .NET/GdPicture migration is implemented before Java migration because
  the existing PXA code-migration infrastructure is Roslyn-based.
- Viewer migration targets PXA's existing responsive Web Viewer; native iOS,
  Android, Flutter, React Native, and MAUI migration is outside this milestone.
- Current and historical product names are input aliases, while `Nutrient` is
  the canonical provider identity.
- Customer documents and proprietary source are used only with explicit
  authorization and are never committed as public fixtures.
- Migration reports target equivalent PXA behavior, not textual replacement of
  every Nutrient call.
- The PDF Trust Foundation remains a separate later milestone.
