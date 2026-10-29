# Nutrient Java and Document Engine Inventory

Status: research inventory for the N3/N4 preparation track. This document does
not claim that Java migration is implemented.

## Scope and Classification

The inventory separates three products and integration styles:

1. **Nutrient Java SDK** - an in-process Java document-processing SDK using
   the `io.nutrient.sdk` package family.
2. **Nutrient Web SDK** - a browser/frontend SDK with JavaScript APIs,
   annotations, forms, Instant JSON, and optional server integration.
3. **Nutrient Document Engine** - a separate headless document server with
   REST, deployment, file-management, and synchronization concepts.

Only the first and third areas are the subject of the Java N3/N4 inventory.
Web SDK and Instant JSON are recorded where they affect boundaries or fixture
interoperability; they are not treated as Java APIs.

The labels in this document have precise meanings:

- **Verified** means the current public Nutrient documentation explicitly
  shows the package, class, method, product, or workflow.
- **Planned mapping** means a proposed PVAD migration target that still needs a
  parser contract, implementation, and tests.
- **Unknown or unsupported** means the public sources reviewed here do not
  establish a reliable mapping. It must not be presented as supported.

## Provenance

| Field | Value |
| --- | --- |
| Inventory date | 2026-09-25 |
| Repository track | Nutrient N3/N4 preparation |
| Primary language | English |
| Source policy | Official `nutrient.io` documentation and API reference only |
| Version policy | Record the version shown by the source; do not infer compatibility from a URL alone |
| Fixture policy | Synthetic source only; no customer projects or proprietary binaries |
| License policy | No Nutrient binaries, license keys, private certificates, or licensed sample payloads are committed |
| Review state | Research inventory; no Java provider implementation is implied |

The source links below are the provenance record. They should be rechecked
before implementation because API pages and product documentation can change.

## Product and API Generation Distinctions

### Current Java SDK identity

The current Java getting-started documentation identifies the Maven Central
dependency as:

```gradle
implementation group: 'io.nutrient', name: 'nutrient-java-sdk', version: '2.1.4'
```

The same guide imports `io.nutrient.sdk.Document` and opens a document with
`Document.open(...)`. The version above is a source snapshot, not a PVAD
dependency recommendation or a compatibility guarantee.

The current Java product pages describe one SDK covering document opening,
editing, conversion, OCR/extraction, annotations, forms, signatures, and
redaction. Individual pages expose more detailed APIs and may use different
examples than the high-level product page.

### Legacy Java material

The current Java guide links to legacy Java SDK guide PDFs identified as v1.8.
That is a historical documentation generation, not proof that v1.8 and the
current `io.nutrient.sdk` API are source compatible. N4 must identify the
generation from the input project before applying a mapping.

### Related product names

- `Nutrient Java SDK` is the current Java product name in the reviewed sources.
- `Nutrient Web SDK` is a separate JavaScript product. Its public APIs include
  `NutrientViewer.load`, `exportInstantJSON`, and `applyInstantJson`.
- `PSPDFKit` appears in historical Web SDK identifiers and the Instant JSON
  format URL. It is not a Java package alias and must not be converted into a
  Java import automatically.
- `Nutrient Document Engine` is a server product. It is not the Java SDK and
  its REST resources must not be inferred from Java method names.

### Generation detection policy

The future Java migrator should classify an input using, in order:

1. declared Maven or Gradle coordinates;
2. imported package families and class names;
3. documented API signatures and exception types;
4. an explicit user-selected generation when evidence is incomplete.

If those signals conflict, the result should be `reviewRequired` and preserve
the source. It must not silently apply a current-API mapping to a legacy API.

## Verified Java Package and API Families

The following table records only families shown by official current Java pages
or API reference pages. Method names are examples of observed public surface,
not a complete SDK inventory.

| Capability | Verified package/import family | Verified symbols or operations | Planned PVAD mapping | Current state |
| --- | --- | --- | --- | --- |
| Open and lifecycle | `io.nutrient.sdk` | `Document`, `Document.open(path)`, try-with-resources / `AutoCloseable` usage | `PdfEditableDocument.LoadFromFile` or `LoadFromStream` with explicit source and lifetime diagnostics | Verified source; mapping planned |
| Save and export | `io.nutrient.sdk`, `io.nutrient.sdk.exporters` | `save()`, `saveAs(path)`, `export(path, new PdfExporter())`, `exportAsPdf(path)`, `exportAsImage(path)` | Typed PVAD save/export operations with output format and overwrite policy | Verified source; mapping planned |
| PDF editor | `io.nutrient.sdk.editors` | `PdfEditor.edit(document)`, `appendDocument`, editor close | `PdfEditableDocument` and explicit composition operations | Verified source; mapping planned |
| Pages | `io.nutrient.sdk.editors.pdf.pages` | `PdfPage`, `PdfPageCollection`, `getPage(index)`, `getFirst()`, page collection add/custom page examples | `PdfPageEditor` / `PdfPageComposer`, with one-based PVAD page diagnostics | Partially verified; index conversion planned |
| Page order and merge | `io.nutrient.sdk.editors`, `io.nutrient.sdk.editors.pdf.pages` | page management and `appendDocument` are documented; merge guide uses multiple `Document` instances | `AppendPages`, `ClonePages`, `ExtractPages` with source-graph validation | Verified workflow; edge semantics unknown |
| Annotations | `io.nutrient.sdk.editors.pdf.annotations` | `PdfAnnotationCollection`, `PdfTextAnnotation`, `PdfHighlightAnnotation`, `PdfUnderlineAnnotation`, `PdfStrikeOutAnnotation`, `PdfSquigglyAnnotation`, `PdfStampAnnotation`, `PdfLinkAnnotation`, `PdfLineAnnotation`, `PdfCircleAnnotation`, `PdfSquareAnnotation`, `PdfFreeTextAnnotation`, `PdfRedactAnnotation` | PVAD annotation DTOs and `PdfPageEditor` operations | Verified families; complete property parity unknown |
| Annotation collection | `io.nutrient.sdk.editors.pdf.annotations` | add, iterate, `get`, `size`/`getCount`, `removeAt` | Stable IDs, page-relative geometry, and explicit unsupported-property diagnostics | Verified examples; mapping planned |
| Forms | `io.nutrient.sdk.editors.pdf.formfields` | `PdfFormFieldCollection`, `PdfFormField`, `PdfTextField`, `PdfCheckBoxField`, `PdfRadioButtonField`, `PdfComboBoxField`, `PdfListBoxField`, `findByFullName`, field type/value operations | PVAD form field model and typed form mutations | Verified families; full hierarchy and appearance parity unknown |
| Vision and OCR | `io.nutrient.sdk` and `io.nutrient.sdk.enums` | `Vision`, `Vision.set(document)`, `VisionEngine.Ocr`, `getSettings().getVisionSettings().setEngine(...)`, `extractContent()` | PVAD OCR job/service with bounded output and source-location diagnostics | Verified current example; mapping planned |
| OCR/ICR/extraction | `io.nutrient.sdk` | current product documentation describes OCR, ICR, VLM, structured extraction, and word-level boxes | PVAD OCR/analysis handoff, never a fabricated local equivalent | Product-level capability verified; exact Java API surface incomplete |
| Redaction | `io.nutrient.sdk.editors.pdf.annotations` | `PdfRedactAnnotation`, `PdfAnnotationCollection.addRedact(...)`; official guide states save/apply removes underlying content | PVAD redaction preflight, apply, verification, and audit workflow | Verified redaction family; apply/save semantics require fixture validation |
| Signatures | `io.nutrient.sdk.signing` | official Java signing page shows `PdfSigner`, `DigitalSignatureOptions`, `SignatureAppearance`, `sign()`, and `signField()` | PVAD Trust signing workflow with runtime-only credentials and post-save validation | Verified current family; target equivalence planned |
| Signature key sources | `io.nutrient.sdk.enums` | `SignatureKeySource`: `Pkcs12`, `WindowsCertificateStore`, `SmartCard`, `Pkcs11` | PVAD credential-provider policy; never copy secret material into generated code | Enum verified; policy mapping planned |
| Security settings | `io.nutrient.sdk.settings`, `io.nutrient.sdk.enums` | official API reference exposes settings such as `AccessibilitySettings`; save preferences include `PreserveAnnotations` | PVAD compliance/security profiles with explicit unsupported diagnostics | Partial public inventory; encryption/permission API surface unknown |
| Conversion | `io.nutrient.sdk`, `io.nutrient.sdk.exporters` | current Java pages document PDF, Word, Excel, PowerPoint, HTML, Markdown conversion; examples use `exportAsPdf` and exporters | PVAD conversion provider handoff with source and target format evidence | Capability verified; format-specific API matrix planned |
| Exceptions | `io.nutrient.sdk.exceptions` | official examples import `NutrientException`; OCR documentation also shows `VisionException` | Stable `CANMIGNUTJAVA...` diagnostics preserving exception category without secrets | Exception family verified; complete hierarchy unknown |
| Types and colors | `io.nutrient.sdk.types` | official examples use `Color.fromArgb(...)` | PVAD color/value normalization | Verified example; full type inventory planned |

### Important API inconsistencies to preserve as evidence

The reviewed annotation guide and API reference use both naming styles such as
`addHighlight(...)` and `addHighlightAnnotation(...)` in examples from
different pages. This may represent documentation/API-generation drift. A
parser must record the exact source symbol and generation instead of
normalizing both names to one operation without verification.

The public pages also show both `saveAs(...)` and exporter-based `export(...)`
flows. These should remain separate source operations until their lifecycle,
format, and mutation semantics are confirmed with generation-specific fixtures.

## Planned Language-Neutral Mapping

The proposed intermediate model is intentionally smaller than the Java SDK:

```text
Java source
  -> Java AST + symbol model
  -> NutrientJavaOperation
  -> language-neutral PVAD migration operation
  -> C# PVAD output + diagnostics + source map
```

Planned operation families:

| Java operation family | PVAD target direction | Fidelity policy |
| --- | --- | --- |
| `Document.open` and close | `PdfEditableDocument.LoadFromFile` / `LoadFromStream` and scoped lifecycle | Exact only after stream, password, and disposal behavior are proven |
| `PdfEditor.edit` | existing-document editor session | Preserve an explicit edit-session boundary |
| `saveAs` / `export` | typed save/export operation | Keep output format and overwrite behavior visible |
| page access and mutation | page editor/composer operations | Convert Java zero-based indexes to PVAD one-based indexes with diagnostics |
| annotation creation and mutation | PVAD annotation operations | Unsupported appearance/property fields remain review items |
| form lookup and value mutation | PVAD form operations | Preserve full field names and field-type checks |
| `Vision` OCR | PVAD OCR service/job | Preserve engine selection and output shape; do not claim ICR/VLM parity automatically |
| redaction annotations and apply/save | PVAD Trust redaction pipeline | Never replace permanent removal with a visual overlay |
| signing and certificate selection | PVAD Trust signing pipeline | Credentials remain runtime-only and external to generated source |
| conversion/export | PVAD conversion provider | Preserve source/target format, streams, and failure behavior |
| exceptions and cleanup | PVAD diagnostics and resource scope | Keep cleanup and failure paths visible in generated code |

These are planned mappings, not delivered functionality. N4 is blocked until
the Java parser, symbol-resolution strategy, operation schema, and fixtures are
approved.

## Document Engine REST and Synchronization

Document Engine is inventoried separately because it represents deployment and
server data flow rather than in-process Java calls.

### Verified Document Engine concepts

The official Document Engine guide describes a headless PDF server that can be
self-hosted or managed. Its guides cover configuration, file management,
deployment, management, monitoring, viewer integration, PDF generation,
conversion, OCR, annotations, forms, editing, signatures, redaction,
extraction, optimization, PDF/A, rendering, and Instant JSON.

The official synchronization guidance describes Web SDK integration with
Document Engine and server-backed annotation/document continuity across
devices, sessions, or users. Instant JSON is also documented as a portable
annotation representation. The Web SDK documentation recommends removing
`pdfId` before storing Instant JSON when the source document can change.

### Separate migration boundary

Document Engine input should be treated as a deployment/data-flow migration:

- identify base URL, authentication mechanism, document/file identifiers,
  asset lifecycle, synchronization mode, and webhook/callback behavior;
- generate a review report and PVAD deployment mapping;
- never copy source JWTs, API keys, private URLs, or customer document data;
- do not interpret a Java `Document.open` call as a Document Engine REST call;
- do not interpret an HTTP Document Engine operation as an in-process Java
  operation;
- classify real-time collaboration and synchronization as separate work until
  PVAD has equivalent server contracts.

### Planned Document Engine fixture fields

Synthetic REST fixtures should contain only placeholders:

```json
{
  "baseUrl": "https://example.invalid/document-engine",
  "documentId": "synthetic-document-001",
  "auth": "placeholder-token",
  "sync": "instant-json",
  "operations": ["open", "export", "annotation-sync"]
}
```

The `.invalid` host and placeholder token are deliberate. They must never be
replaced with a live customer endpoint in a committed fixture.

## Synthetic Fixture Matrix

Fixtures should be small, deterministic Java source files or sanitized JSON.
Every fixture must declare a fixture ID, intended API generation, source
language, expected operations, expected diagnostics, and expected fidelity.

| Fixture group | Synthetic cases | Expected purpose |
| --- | --- | --- |
| Bootstrap | Maven dependency, Gradle dependency, wildcard imports, explicit imports | Detect current package and build coordinates |
| Generation | current `io.nutrient.sdk` API, legacy v1.8-marked sample, conflicting coordinate/imports | Generation classification and review-required behavior |
| Lifecycle | `Document.open` path, stream placeholder, try-with-resources, explicit close, exception path | Resource ownership and failure mapping |
| Pages | first page, indexed access, add custom page, reorder, append document, merge | Index conversion and page graph operations |
| Annotations | text, free text, highlight, underline, strikeout, squiggly, stamp, link, line, circle, square, redact | Type and geometry mapping |
| Forms | text, checkbox, radio, combo, list, signature field, hierarchical field name | Form type/value and field-name preservation |
| Trust | redaction mark/apply, visible signature, invisible signature, PAdES/timestamp placeholder, key-source enum | External credential and Trust handoff |
| OCR | `VisionEngine.Ocr`, extraction output, multilingual placeholder, ICR/VLM marker | Engine selection and bounded output mapping |
| Conversion | Word/Excel/PowerPoint/HTML/Markdown input and PDF output, exporter object | Format preservation and unsupported format diagnostics |
| Security | password placeholder, permissions placeholder, accessibility setting, preserve annotations | Security/compliance boundary detection |
| REST | file upload, document fetch, export, annotations, Instant JSON, sync placeholder | Document Engine separation and data-flow review |
| Adversarial | aliases, shadowed symbols, wrappers, async calls, DI parameters, dynamic reflection | Parser confidence and unsupported detection |
| Safety | embedded key-looking strings, customer-looking URLs, binary references | Secret and provenance redaction tests |

No fixture should contain a real license key, certificate, password, customer
URL, customer document, downloaded SDK jar, or proprietary sample.

## Provenance and Licensing Boundaries

Every future fixture and inventory update should record:

```yaml
fixtureId: nutrient-java-current-open-001
sourceKind: official-documentation-example
sourceUrl: https://www.nutrient.io/sdk/java/getting-started/
retrievedOn: 2026-09-25
product: Nutrient Java SDK
apiGeneration: current-public-docs
licenseStatus: documentation-reference-only
customerData: false
binaryIncluded: false
secretsIncluded: false
adaptation: synthetic-and-minimal
```

The provenance record is descriptive and does not grant permission to
redistribute Nutrient software. PVAD work may refer to public API names and
documentation links, but must not package or commit Nutrient binaries, native
libraries, license files, private keys, real certificate material, customer
source, customer documents, or captured production traffic.

If a licensed sample is needed for validation, it must remain outside the
repository in a local, access-controlled test location. The committed test
should use a synthetic equivalent and document the missing external validation
explicitly.

## Unknown or Unsupported Areas

The following are intentionally not asserted as supported by this inventory:

- complete Java API coverage across every current or legacy release;
- source compatibility between the current SDK and the legacy v1.8 guides;
- exact encryption, permissions, certificate-store, HSM, and timestamp method
  signatures beyond the publicly verified families above;
- full annotation appearance, reply/thread, attachment, bookmark, and custom
  metadata parity;
- exact page-coordinate, crop-box, rotation, and unit semantics for every Java
  API generation;
- complete OCR/ICR/VLM model configuration and output schema parity;
- complete conversion format matrix and byte-level output equivalence;
- Document Engine REST endpoint authentication, deployment configuration,
  webhook contracts, and synchronization conflict semantics;
- multi-user collaboration semantics, offline conflict resolution, and
  server-side retention behavior;
- automatic execution of Java source during migration.

Unknown behavior must produce a stable diagnostic and a review action. It must
not be represented as `exact` fidelity merely because the source compiles.

## N3/N4 Readiness Gates

N4 research preparation is complete when the inventory, provenance rules,
product boundary, and synthetic matrix are reviewed. N4 implementation may
start only after these gates are satisfied:

1. Select a maintained Java parser with symbol resolution.
2. Freeze the language-neutral operation model and diagnostic family
   `CANMIGNUTJAVA...`.
3. Add generation-specific synthetic fixtures for every verified family.
4. Define a compatibility matrix for current SDK, legacy Java material, Web
   SDK/Instant JSON, and Document Engine.
5. Implement detection and source mapping before code generation.
6. Compile generated PVAD output against the repository contracts.
7. Validate Trust, OCR, conversion, and REST handoffs independently.

Until then, this document is an evidence inventory and planning aid only.

## Official Sources

- [Nutrient Java SDK overview](https://www.nutrient.io/guides/java/)
- [Nutrient Java SDK getting started](https://www.nutrient.io/sdk/java/getting-started/)
- [Java document processing](https://www.nutrient.io/sdk/java/document-processing/)
- [Java editor guides](https://www.nutrient.io/guides/java/editor/)
- [Java annotations guide](https://www.nutrient.io/guides/java/editor/add-annotations-to-pdf/)
- [Java form fields guide](https://www.nutrient.io/guides/java/editor/fill-pdf-form/)
- [Java redaction guide](https://www.nutrient.io/sdk/java/pdf-redaction/)
- [Java OCR guide](https://www.nutrient.io/guides/java/extraction/extract-data-from-image-ocr/)
- [Java signing overview](https://www.nutrient.io/sdk/java/pdf-signing/)
- [Java API: annotation collection](https://www.nutrient.io/api/java-sdk/nutrient-java-sdk/io.nutrient.sdk.editors.pdf.annotations/pdf-annotation-collection/index.html)
- [Java API: signature key sources](https://www.nutrient.io/api/java-sdk/nutrient-java-sdk/io.nutrient.sdk.enums/signature-key-source/index.html)
- [Document Engine overview](https://www.nutrient.io/guides/document-engine/)
- [Document Engine developer guides](https://www.nutrient.io/guides/document-engine/intro/)
- [Web SDK Instant JSON guide](https://www.nutrient.io/guides/web/importing-exporting/instant-json/)
- [Web SDK InstantJSON API reference](https://www.nutrient.io/api/web/interfaces/InstantJSON.html)
