# Native Library Licensing And Protection

## Implementation Status

The standalone .NET 10 verifier, admission API, and offline manufacturer
preparation tool are implemented. Native PDF construction and serialization now
enforce the `pdf` grant, including direct engine calls, the Generator facade,
mapper rendering, and bytes/file/stream output. C# PDF execution in the CodeWorker
also requires the host's signed native license. Native DOCX export and signing
now enforce the separate `word` grant, including direct exporter/service calls.
Native Spreadsheet authoring, calculation, data operations, and workbook IO
also require the separate `spreadsheet` grant. The eight built-in file importers
and high-level PDF load/save entry points now enforce `importer`. Image analysis,
OCR engines, and isolated OCR workers also enforce Importer rights. Generic
cross-product dependency and continuation coverage remains partial. Image
analysis, in-process OCR, typed PDF loading, full/incremental PDF saves and
PDF-to-Designer DTO/analysis mapping, bounded page editing/composition, content
rewriting and the editable-document facade now retain their admitted context
through supported built-in completion. Trust, advanced regeneration and other composite
native pipelines still need migration. Importer enforcement
is therefore still marked partial. Converter and Migration
product integrations also remain open.
Existing server subscription checks remain additional, independent checks. This
is not yet complete commercial SDK protection.

The shipped trust register is intentionally empty until manufacturer public keys
are approved. There is no built-in evaluation key, runtime trust override,
internet call, activation bypass, or production signing-key generation.

**Breaking behavior:** with the empty source trust register, ordinary builds
cannot create/export PDFs, generate/sign DOCX, process spreadsheets, or run
licensed imports/image analysis/OCR until
manufacturer trust is provisioned and the respective native product grant is loaded. Development tests
use isolated synthetic builds, not a runtime exemption or an embedded trial key.

## Activation API

```csharp
using PVAD.Licensing;

// Requires a manufacturer-signed artifact and its approved embedded public key.
var status = PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
Console.WriteLine(status.State);

// Capture admission before writing output, not after finishing the operation.
var admission = PvadLicense.Require(PvadLicenseProducts.Pdf);
```

`LoadFromStream` reads from the current position, limits input to 64 KiB, and
leaves the stream open. Failed replacement does not discard the active license.
Status contains no full license, customer name, key material, or filesystem path.
An admitted operation may finish after subscription expiry; new operations
must obtain fresh admission.

PDF construction admits creation; the shared internal writer separately admits
each serialization. Replacing the active license with one that lacks `pdf`
prevents subsequent `ToBytes` and `Save` calls on existing documents. Rejection
happens before an implicit empty page is added or an output destination is
written. No additional Converter grant is required for this PDF operation.
Pure DTOs, save options, and document metadata objects remain usable without a
license.

Word generation uses the same explicit activation API with a signed `word`
grant. `PVAD.Generator.Word.Export`, both `WordDocumentExporter.Export`
overloads, and interface calls converge on an admission check before package
creation or document traversal. `DigitalSigningService.SignDocx` checks Word
rights before loading the signing certificate or consuming the input stream.
Missing/wrong-product rights leave the source design and signing stream
unchanged. Each new call rechecks admission, even on a previously used exporter;
renewal requires no exporter reconstruction or process restart.

A Word-only grant is sufficient for Word generation and signing. No PDF or
Converter grant is required. Exporter construction, format/capability inspection,
and design/options DTOs stay free. These checks protect PVAD entry points, not
direct usage of third-party Open XML APIs outside PVAD.

For existing native Word applications, load the issued Word-capable license
before the first export or signing call:

```csharp
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
byte[] docx = PVAD.Generator.Word.Export(design);
```

Server deployments use the same `NativeLicensing:LicensePath` setting as PDF
but need `word` in the issued grants for DOCX work. A PDF-only license is not a
Word license. Export and signing failures propagate to the existing HTTP 503
Problem Details handler; they must not be treated as bad document content or a
user-subscription denial. Do not edit the signed payload to add grants: obtain
an authorized replacement and load it explicitly. Manufacturer key provisioning
and native-license Admin/Account issuance remain open prerequisites, not an
automatic action performed by this integration.

## Spreadsheet Activation And Migration

Load an issued `spreadsheet` grant before creating a `PvadWorkbook`, generating
cells/styles, calculating formulas, or using direct workbook IO:

```csharp
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var workbook = PVAD.Generator.Spreadsheet.CreateWorkbook("Budget");
workbook.AddSheet("Data").Cell("A1").Value(42).Style(style => style.Bold());
byte[] xlsx = workbook.ToXlsx();
```

Protected native entry points include both XLSX exporters, XLSX/XLS import,
CSV/TSV import/export, formula calculation, sorting, find/replace, row-to-sheet
generation, data binding, and conversion to a document design. The shared
ClosedXML builder admits export/calculation before engine creation. Import
checks run before reading a stream. File saves admit before generating bytes
or opening an output path, including empty CSV/TSV workbooks.

Each authoring method checks again, including retained worksheet, range, cell,
column, and style handles after a license replacement. A denied method does not
invoke a style callback or change models, existing file contents, or import
stream position/ownership. Earlier successful fluent calls are not rolled back
when a later, separate call is denied. Valid renewal restores processing on
existing builders without a restart.

Constructing DTOs or exporter services, reading an existing model, inspecting
format/capability metadata, editing workbook/sheet names, and structural
`SpreadsheetValidator` checks remain free of native licensing. Application
authentication and authorization still apply independently. Free DTO access is
not a way to bypass protected processing or export.

A Spreadsheet-only license covers these native operations without additional
Importer or Converter rights. Converting a sheet to `DesignExportDto` does not
render it: subsequently invoking the separate PDF or Word renderer still checks
that renderer's grant. Intended product bundles and the remaining central
dependency-rights work must not be confused with an unrestricted cross-product
license bypass.

Existing server installations need `spreadsheet` in their issued native grants.
Spreadsheet processing routes report missing product rights through the common
HTTP 503 Problem Details contract, not as invalid workbook content. Structural
validation remains native-license-free; even empty CSV/TSV exports require
processing rights. The test suite replaces and restores signed licenses in an
explicitly non-parallel collection to avoid changing another test's process-wide
license snapshot.

## Importer Entry-Point Milestone

Load an issued `importer` grant before calling built-in file import or the
high-level existing-PDF reader/save APIs:

```csharp
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
await using var input = File.OpenRead("source.pdf");
var document = await PVAD.Importer.Pdf.LoadAsync(input);
```

The current admission boundary covers all eight `FileImporterRegistry`
implementations (DOC, DOCX, image, Markdown, ODT, PDF, PPTX, SVG), including
static entry points, the PDF facade and `PdfImporter.LoadAsync` overloads,
`PdfDocumentSaver.Save`, `PdfFullRewriteWriter`, and
`PdfIncrementalUpdateWriter.Append`. Checks occur before stream consumption,
package/image decoding, Markdown asset resolution, or update enumeration.
Registry creation, extensions, options, and object-model DTOs remain usable
without a native license.

These tested imports and byte-level save strategies work with an Importer-only
license; they do not require PDF, Word, Spreadsheet, or Converter rights. This
does not grant arbitrary document generation: ordinary `PVAD.Pdf.PdfDocument`
construction still requires `pdf`. No ambient all-products scope, runtime trust
override, or user-visible server-mode exemption was introduced.

Document import routes propagate native failures as HTTP 503 Problem Details,
not invalid-file Bad Request responses. Upload validation, cancellation, input
bounds, and application authorization remain separate and unchanged.

**Importer enforcement is partial.** The direct-engine and color/font boundaries
described below are now guarded, together with the editing boundaries.
The Trust/security/signing/redaction/e-invoice boundaries and the regeneration/
debug-overlay boundaries are also guarded as described below. Standard
regeneration uses restricted internal PDF dependencies rather than requiring
a separate PDF grant. Image analysis and OCR/worker boundaries are covered by
the later milestone below. Generic cross-product dependency rights and
admitted-operation lifetime remain open. The operation
matrix keeps `importer.enforced` false and lists the remaining work explicitly.

The local milestone passed 111 isolated admission checks and 860 .NET tests,
including the complete FileImporter and Importer suites. See the
[Importer checkpoint](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-entry-point-milestone)
for evidence, fixture limits, and unfinished engine/dependency-rights work.

### Direct Engine Admission

Direct calls require `importer` for token reading/seeking, all public object
parsing methods, cross-reference parsing, stream-registry decoding and its five
built-in decoders, content parsing, document building, graphics interpretation,
simple-font/CMap parsing, and the processing methods of the Analysis engines.
The executable inventory originally covered 41 public processing methods and
now covers 50 with the color/font milestone below. It inventories the selected
types, not every Importer public type.

Each new public processing call checks the current license before consuming
data, invoking resolvers/decoders, enumerating inputs, or mutating derived
bounds/classifications. Retaining an engine or constructing a default tokenizer
does not bypass admission. A valid Importer-only license is sufficient for these
operations. Custom decoders reached through the registry inherit its admission;
third-party implementations remain responsible for their own direct calls.

Constructing engine configuration, reading token positions, inspecting filter
support/operator metadata, looking up existing object-graph values, working with
DTOs, and general matrix/rectangle arithmetic remain free. These are not PDF
parsing or decoding entry points. The next section distinguishes color/font
processing from metadata and model construction.

Restricted internal dependency rights and snapshot propagation through a whole
admitted composite operation remain separate work. This milestone checks each
public invocation; it does not yet promise completion of a nested pipeline
after concurrent license replacement or expiration.

The local direct-engine checkpoint passed 290 distinct admission/content checks
and all 860 selected .NET regression cases. Evidence and remaining work are
recorded in the
[direct-engine milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-direct-engine-milestone).

### Color And Retained Font Admission

An Importer grant also protects `PdfSrgbColorConverter.Resolve/ToSrgb`,
`PdfFunction.TryCreate/Evaluate`, and `PdfContentColorRewriter.Rewrite`.
Internal image-color conversion and native ICC transformation check admission
before processing. These are Importer operations, not separately purchased PDF
generation or Converter operations.

`PdfFontResource.Decode/GetGlyphCodes`, `PdfEncodingMap.Decode`, and
`PdfToUnicodeMap.Decode` check each call, including calls on fonts, encodings,
and functions retained from before a license replacement. Replacing the license
with one without Importer rights blocks subsequent processing; valid renewal
restores it without reconstructing the model or restarting the process.

Existing glyph-width lookup, font attributes, function domain/range/output
metadata, color-space convertibility inspection, and construction of mapping
entries remain free. In particular, `PdfToUnicodeMap.Add` builds model data;
using that data to decode glyphs requires admission. Color-space convertibility
may inspect profile support, but does not perform a color transform.

Admission precedes resolver callbacks, component reads, function evaluation,
and custom encoding dispatch from `PdfFontResource`. Third-party subclasses
remain responsible for their own directly exposed implementations; the wrapper
does not invoke a custom encoding when admission fails. License failures from
nested sampled/PostScript/ICC decoding propagate instead of being converted
to an unsupported-content result. Existing invalid-input handling and color
formulas remain unchanged.

This color/font checkpoint did not close Trust/security or general regeneration;
see their subsequent milestones below, including image analysis and OCR.
Complete-package and admitted-operation snapshot work remain open. A composite pipeline still rechecks nested
public calls and may stop if the license is replaced during processing.

The local color/font milestone passed 357 distinct probe checks, including an
actual required LittleCMS ICC transform and its retained-object denial/renewal,
plus 860 .NET regressions and 46 tooling/version contracts. See the
[color/font milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-color-and-font-milestone)
for evidence and remaining work. These are macOS/x64 .NET results, not complete
native-product or cross-platform acceptance.

### Editing And Retained Documents

`PdfEditingSession` mutations, `PdfPageEditor`, `PdfPageComposer`, both
`PdfContentStreamRewriter.Rewrite` overloads, and the processing methods on
`PdfEditableDocument` require `importer`. Admission occurs before input access,
list enumeration, status changes, mutation, stream consumption/ownership, and
file output. Retaining a session or loaded document does not bypass the check.

The status-based facade throws `PvadLicenseException` for native admission
failures instead of storing them as ordinary operation failures. Catch that
exception separately from checking `PdfPageOperationStatus.Failed`/`LastError`:
the following example assumes a source with at least two pages.

```csharp
try
{
    PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
    using var document = new PVAD.Importer.Editing.PdfEditableDocument();
    var status = document.LoadFromFile("source.pdf");
    if (status == PVAD.Importer.Editing.PdfPageOperationStatus.Success)
    {
        status = document.MovePage(1, 2);
        if (status == PVAD.Importer.Editing.PdfPageOperationStatus.Success)
            status = document.SaveToFile("reordered.pdf");
    }
}
catch (PvadLicenseException error)
{
    Console.Error.WriteLine(error.Code);
}
```

Initial license denial preserves `LastError`, loaded bytes, selected page,
measurement unit, edit models, caller reference parameters, existing files, and
stream ownership/position. File/stream loading validates a candidate before
replacing the existing document, so invalid replacement input also preserves
the old document. Normal file/argument/unsupported-operation errors retain the
facade's existing status behavior; unsupported attachment merge options remain
unsupported. Disposal, status inspection, page-range validation, and pure DTO
construction/mutation remain free. Mutable DTOs are not a protected data vault;
processing/output APIs enforce admission rather than hiding user-owned data.

Blank-page insertion, initial clones, and split/extract targets use a bounded
internal dependency adapter into the existing PDF writer. It checks Importer
rights and can return only a freshly created empty page of the requested size;
it accepts no caller document, drawing callback, or asset. There is no public
license-mode switch. Ordinary PDF construction/output still requires `pdf`,
and that editing checkpoint did not implement the general regeneration bridge.
The later regeneration milestone below adds its own internal adapter; generic
cross-product dependency rights and snapshot propagation remain separate work.

The final local editing checkpoint passed 528 Importer probe checks (40 editing
entries), 20 PDF-only admission checks, and 1,096 regression cases. The full
70-case PDF infrastructure suite also passed two additional process runs.
An initial RTL-width failure was not reproduced in those three passing runs;
its cause remains unestablished and the failed result is retained. See the
[editing milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-editing-milestone)
for exact evidence and remaining work. No visual threshold or font algorithm
was changed in response, and these local results are not platform acceptance.

## Importer Trust And Security Milestone

An `importer` grant is now required for existing-PDF Trust inspection, change
impact analysis, archival preflight/conversion, and the Trust post-processing
pipeline. The same boundary covers password/certificate decryption, AES-256
security-handler creation and retained encryption/decryption methods, CRL/OCSP
processing, PAdES signing, PKCS#12 key loading/signing, RFC 3161 timestamp
requests, document timestamps, redaction/preflight/audit verification, invoice
attachment, and associated-file extraction.

Admission precedes stream reads, graph changes, key loading, signing-key
callbacks, timestamp HTTP requests, and output production. Retaining an AES
handler, key, or service does not retain permission after a license replacement.
Pure DTOs, certificate URI locators, encryption capability flags, key certificate
metadata, and disposal remain usable without a license. A locator only reads
certificate metadata; it does not fetch remote evidence.

These operations were exercised with an Importer-only signed license and
synthetic certificates/evidence. Preparing archival or certificate-encrypted
test inputs uses a separate PDF-generation fixture phase; the actual Importer
operations do not inherit that grant. General `PdfDocument` construction
remains unavailable to the Importer-only consumer.

The corresponding API actions check native admission before reading uploads,
resolving timestamp/audit-key dependencies, or storing inspection jobs. The
background inspection executor rechecks before opening stored input/results.
Retrieving a redaction audit also verifies its cryptographic record and
therefore requires Importer admission; it is not a pure metadata read.
Authorization, tenant checks, upload limits, and XML-only invoice validation
remain separate. Native failures use HTTP 503 Problem Details with their
`PVADLIC...` diagnostic, not HTTP 400 invalid-document responses.

A license change during stream reading propagates the native-license failure
rather than generating a misleading invalid-PDF report. This deliberately does
not promise a stable admission snapshot across a composite operation: propagation
of scoped dependency rights and admitted-operation lifetime remains unfinished.
That Trust checkpoint did not close regeneration/debug overlays; see the later
milestones below, including image analysis and OCR/worker admission. Package
acceptance and cross-platform validation remain separate work.

See the [Trust milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-trust-and-security-milestone)
for exact local evidence and remaining work.

At the initial Trust checkpoint, validation passed 655 distinct admission checks, all 389 selected API
cases, and the full Importer, FileImporter, Generator, and licensing-core suites.
The PDF infrastructure suite again exposed the previously recorded intermittent
RTL-width failure; two complete fresh-process repeats passed unchanged. This
did not establish a root-cause fix. The original failure, repeat results, and
independent TRX audit remain in the checklist. The subsequent follow-up below
reproduces and corrects the font-lifetime issue.

### Font Shaping Follow-Up

The native shaping blob now owns a copy of its font bytes rather than relying
on the lifetime of a temporarily pinned managed array. A compacting-GC test
reproduced changed bytes before the correction and passes afterward. Parallel
shaping, mixed-direction clusters, and the original example's exact width
(92.376 points at 12 pt) are covered without changing fonts, direction rules,
or tolerances.

The corrected code passed all 1,370 selected .NET cases and 655 Importer
admission checks. Five local visual packs cover 118 cases without P0/P1;
five P2 raster findings remain visible. Normal and C# PDFs are pixel-identical,
including the inspected Arabic cases. This does not assert new human approval
of changed browser raster pixels or testing on Linux/Windows. See the
[font-lifetime evidence](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#font-shaping-buffer-lifetime-follow-up)
for reproduction, final results, cleanup, and the remaining release gates.

## Importer Regeneration And Debug Overlays

`PvadPdfGeneratorBridge.RegenerateAsync` without a caller configuration callback
requires only `importer`. It creates a private, fresh generator document from
the imported model, renders through the shared writer, and returns PDF bytes.
No generator handle is handed to the caller, and no ambient product right or
public processing-mode switch is activated.

```csharp
using PVAD.Importer.Generation;
using PVAD.Licensing;

PvadLicense.LoadFromFile("native-importer-license.json");
await using var input = File.OpenRead("source.pdf");
var model = await PVAD.Importer.Pdf.LoadAsync(input);
using var output = new MemoryStream();
await new PvadPdfGeneratorBridge().RegenerateAsync(model, output);
await File.WriteAllBytesAsync("regenerated.pdf", output.ToArray());
```

Regeneration, `MapObject`, `MapGraphicsElement`, `MapResources`,
`PdfGeneratorBridgePipeline.SaveAsync`, and `PdfDebugOverlayBuilder.Build`
check Importer admission before processing. The pipeline checks before invoking
even a custom `IPdfGeneratorBridge`. Constructors, imported DTOs, direct
metadata reads, and overlay DTOs remain free. Rendering/mapping support and
fidelity limits are unchanged; this is not a new image-filter compatibility
claim.

The overload with a **non-null `Action<PVAD.Pdf.PdfDocument>` callback** requires
both Importer and PDF rights. That is caller-controlled general generator
authoring, not a hidden renderer dependency. A null callback uses the standard
Importer-only path. Change imported-model metadata before standard regeneration
when a general generator callback is not needed.

Both rights are checked before dispatching the advanced callback. Importer
admission is checked again after it returns; the ordinary renderer rechecks PDF
rights. Captured documents, reentrant output streams, and custom bridge plugins
do not inherit internal generator privileges. Ordinary `PdfDocument`
construction, `ToBytes`/`Save`, and direct renderer output still require `pdf`.
Host features that deliberately use the advanced callback, such as the current
Viewer annotation-flattening service, retain both requirements; generic bundle
rights are not closed by this milestone.

Denied admission, cancellation before processing, mapping/rendering failures,
and callback failures do not write to the caller's output stream. A stream's
own failing or partially successful `WriteAsync` remains the stream owner's
responsibility; no atomic filesystem-write guarantee is added. Nested calls
can still observe a license replacement; whole-operation lifetime propagation
is still open. See the
[regeneration milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-regeneration-and-debug-milestone)
for direct, reentrant, API, compatibility, and cleanup evidence.

## Image Analysis And OCR

Load an issued native Importer license before image analysis or OCR:

```csharp
using PVAD.Licensing;
using PVAD.FileImporter.ImageOcr;

PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var engine = new ProcessIsolatedTesseractOcrEngine(
    workerPath: configuration["PVAD:OcrWorkerPath"],
    tessDataPath: configuration["PVAD:TessDataPath"],
    nativeLibraryPath: configuration["PVAD:OcrNativeLibraryPath"]);
var converter = new ImageToPdfConverter(engine);
await using var input = File.OpenRead("scan.png");
var result = await converter.ConvertAsync(input, "scan.png",
    new ImageToPdfConversionOptions { Languages = "eng", IncludeDebugOverlay = true });
// result.Design is the editable design; result.DebugOverlayPng is an optional PNG.
```

Despite its historical name, `ImageToPdfConverter.ConvertAsync` returns a design
and diagnostics, not PDF bytes. This processing and the API's built-in
`convert-image-to-pdf` output require only Importer rights. The API renders its
own OCR result through a private byte-only adapter using the same PDF mapper;
it exposes neither a generator document nor a caller-supplied rendering callback.
Ordinary PDF construction, public design mapping, and serialization still
require PDF rights. No Converter, Word, or Spreadsheet grant is added implicitly.

`ImageAnalysisFileImporter`, public preprocessing/color/shape/text/glyph/template
processing, scene assembly, and debug PNG rendering check Importer rights before
processing. The embedded and isolated Tesseract engines check before language
data access, native engine creation, temporary input writes, or custom runner
dispatch. `DefaultOcrWorkerProcessRunner.RunAsync` also checks independently
before starting a process. Constructors, options, metadata, DTOs, disposal, and
the generic numeric `ColorDistance` and `NormalizedCrossCorrelation` helpers
remain license-free; these helpers do not run recognition or access the atlas.
Cached character templates do not authorize later calls after a license change.

The default runner captures the exact signed envelope from the same immutable
license snapshot that admits Importer processing. This internal operation is
not a public license-export or trust-injection API. A failed renewal leaves the
previous envelope and payload together. Concurrent renewal cannot mix their
bytes. Each process launch uses a new temporary file, owner-readable/writable
only on Unix, and overrides inherited `PVAD_NATIVE_LICENSE_PATH` for that child.
The file is removed after completion, failed start, timeout, or cancellation.
On Windows, filesystem ACLs remain the deployment owner's responsibility.

The worker validates that envelope with its own embedded manufacturer keys and
release metadata before reading the request or image files. Matching deployment
artifacts and compatible manufacturer trust must be shipped with the worker.
No boolean admission flag, caller-provided public key, automatic Trial, network
validation, or runtime trust override is accepted. The signed license is not a
private signing key, but must still be kept out of logs and retained artifacts.
Grace periods, expiry, update eligibility, and product checks use the same core
verifier as the host; the worker performs its own admission at execution time.

Worker license failures carry `licenseErrorCode` in the existing response and
become `PvadLicenseException` in the caller; only known native diagnostic codes
are accepted as such. Both image API routes use the existing HTTP 503 Problem
Details handler, not an invalid-image HTTP 400. Upload validation, user/tenant
authorization, cancellation, image limits, and native dependency requirements
remain separate. Custom OCR engines and runners remain responsible for their
own directly exposed implementations, but PVAD checks before dispatching them.

The subsequent continuation milestone keeps internal image-analysis and
in-process OCR work under its original admission, including the API's built-in
OCR PDF result. Other composite pipelines are not covered by that change.
A new OCR process dispatch and worker startup still require fresh admission.
Native OCR also
needs the matching Tesseract/Leptonica libraries and language data. Successful
local macOS/x64 OCR is not Windows/Linux/ARM64 native-runtime acceptance. See the
[image/OCR milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-image-analysis-and-ocr-milestone)
for exact validation evidence and remaining work.

## Admitted Operations And Internal Dependencies

An operation that passed admission can finish its supported internal work after
license expiry or renewal. This does not authorize another public operation.
The currently integrated paths are image analysis, in-process OCR conversion,
built-in embedded/isolated-engine continuations within that conversion, the
API's built-in OCR PDF completion, typed PDF loading, full/incremental PDF
saving with their built-in validation, PDF-to-Designer scene/DTO mapping, page
editing/composition, content rewriting and the editable-document facade.
Public APIs, options, and diagnostic codes
are unchanged; callers do not receive a scope or a reusable execution token.

The internal `PvadLicenseExecution` tracks an immutable original admission in
an execution-context-local slot. **Public `PvadLicense.Require` never reads that
slot.** Every public image-processing entry starts a fresh admission before
calling its internal continuation. Built-in internal calls share the admitted
context; caller-controlled streams, engines, and callbacks can only start new
public work against the current license. There is no public session/server mode,
ambient all-products grant, runtime trust setter, or caller-selectable dependency.

The context is closed on success, cancellation, and failure. A captured
execution context or descendant task cannot reuse it after completion. Nested
fresh operations restore their parent in stack order; parallel operations keep
independent admissions. Continuations are bound to the exact verifier that
admitted them; a separate authority cannot substitute its own context for the
process-wide library authority. A retained result, bitmap, engine, or exporter does not
retain the ability to start another protected public call.

The centralized internal policy currently permits these byte-only PDF
dependencies, not general public PDF access:

| Native operation | Internal dependency | Grant required at admission |
| --- | --- | --- |
| Imported-PDF regeneration | Private PDF document and writer | Importer |
| Importer blank-page creation | Fresh empty PDF page only | Importer |
| Bounded PDF editing/composition | Fresh empty helper page only | Importer |
| Built-in OCR result completion | Private mapper and PDF bytes | Importer |

These permissions are purpose-bound: an image-analysis context cannot invoke
OCR PDF completion, and an OCR context cannot request the regeneration or
blank-page permission. Ordinary PDF serialization has its own fresh PDF
admission and receives no Importer permission. Public generator callbacks still
require the explicit rights documented above. Registered internal dependencies
are not a promise of arbitrary cross-product conversion or general redistribution.

**Process boundary:** the default OCR runner checks the current signed license
at dispatch, and the worker independently checks it before accepting work. A
host-side operation is not a trusted timestamp assertion for a new process.
If the license expires or loses Importer rights before dispatch/start, that new
worker operation is denied and staged input is cleaned up. A worker that has
already admitted its OCR processing can finish it. No unsigned admission flag,
backdated start time, or caller-provided trust key extends a license.

PDF Trust and advanced regeneration chains, plus other product composite
call graphs, have not yet been converted end to end.
Their existing nested public calls can still recheck the current license.
The operation matrix retains partial status and lists these remaining areas;
the new context must not be used to claim complete native-product acceptance.
See the [continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-operation-continuation-milestone)
for exact tests and cleanup evidence.

### Typed PDF Load And Save Continuations

`PdfImporter.LoadAsync` and the typed `Pdf` facade admit one Importer operation.
Its built-in tokenizer, object/xref parsers, password/certificate decryption,
stream decoding, font parsing and graphics building use internal continuations.
`PdfDocumentSaver`, full rewrite and incremental append similarly share the
save admission through input and output validation. Each direct public parser,
decoder, font, decryptor or writer call still admits against the current license.
Neither purpose grants the private PDF-generation dependencies listed above.

Custom decoders, font parsers and encoding overrides keep their public dispatch
and behavior. If they start another protected PVAD operation, that public call
must pass current-license validation. A retained parser, font or writer cannot
reuse a completed load/save admission. Signature-invalidation checks, invalid
update rejection, stream budgets and cancellation remain effective.

Loaded stream objects own stable source bytes beyond the temporary read-buffer
lifetime. Seekable input buffers return to the pool on success or read failure;
non-pooled buffers from non-seekable streams are never returned to that pool.
Disposal is idempotent and caller-owned streams remain open. This is not a change
to PDF geometry, input formats, encryption algorithms or visual thresholds.

The original typed-load milestone did not cover the Designer
`PdfFileImporter` scene/DTO/analysis mapping chain; that is covered separately
below. See the
[typed PDF milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#typed-pdf-load-and-save-continuation-milestone)
for exact tests, cleanup and remaining scope.

### PDF-To-Designer Import Continuations

All `PdfFileImporter` instance, static and registry entry paths admit the current
Importer grant at their shared public root. The root carries that admission
through typed PDF loading, scene/primitive construction, geometry, classification,
barcode grouping, reading order, semantic/text reconstruction, chart recognition
and final DTO mapping across every page. It does not grant public PDF-generation
rights or any private regeneration, blank-page or OCR-PDF dependency.

The 19 public processing methods in the geometry, primitive, classification,
reading-order, grouping, reconstruction, scene and semantic engines remain fresh
admission boundaries. Their built-in recursive/nested calls use internal-only
continuations. Caller-controlled enumerations cannot authorize a new public
operation, and a completed or failed import does not provide a reusable grant.
Cancellation, stream ownership, existing page boxes, mixed page geometry,
text/image/font mapping and chart confidence/metadata rules are unchanged.

This is native Importer processing, not a replacement for Designer sessions,
tenant access, server entitlements or feature gates. Diagnostic/debug-overlay,
Editing, Trust and advanced regeneration composites were outside that mapping
milestone; Editing is covered by the subsequent milestone below. See the
[Designer import milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#designer-pdf-import-and-analysis-continuation-milestone)
for the fixture matrix, output comparison and exact validation evidence.

### Editing And Page Composition Continuations

Public `PdfPageEditor`, `PdfPageComposer` and `PdfContentStreamRewriter` entries
admit the current Importer license. Built-in parsing, page-tree work, decryption,
rewrite/append validation and saving continue under that original admission.
This covers move/delete/crop, clone/merge/append, split/extract and page counting
within their existing bounded capabilities; it does not add support for formats
or merge options previously reported as unsupported.

`PdfEditableDocument` starts fresh admission for each public operation. Its
internal load/count/edit/compose/save calls do not open a second public admission.
The insert-page aliases still admit before processing. `GetStatus`, `LastError`,
`IsLoaded` and disposal remain free; denied new work cannot overwrite an earlier
error, change the selected page/units, close input or create an output file.
The five atomic `PdfEditingSession` mutations retain their existing independent
public checks; they have no nested protected processing to migrate.

Split/extract and blank-page insertion use one private, byte-only empty-page
factory. The editing purpose may request that dependency, but not general PDF
generation, regeneration or OCR-PDF rendering. No caller PDF document or public
license exemption escapes the helper. Captured contexts remain closed and
verifier-bound as documented above.

Existing malformed-input, range, encryption and signature-invalidation checks
remain unchanged. Owner-password/re-encryption requirements are not bypassed.
Read failures retain the original error and loaded bytes; accepted stream
ownership is honored. Source data, page geometry, content and validation bodies
are unchanged. See the
[Editing continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-editing-and-page-composition-continuation-milestone)
for exact tests, algorithm comparison and remaining scope. Trust/signing,
redaction, e-invoice, advanced regeneration and other composite product chains
remain open, and Importer is still partially enforced.

## API And Worker Activation

Configure `NativeLicensing:LicensePath` (environment variable
`NativeLicensing__LicensePath`) with a read-only, manufacturer-signed native
license file. This is not the existing server-license public/private key path.
An explicitly configured invalid native license fails host startup. An absent
path allows Account/Admin-only startup, but does not enable native PDF, Word,
Spreadsheet, or the guarded Importer operations.
Unhandled native-license failures return HTTP 503 Problem Details with the
stable `PVADLIC...` code and operator guidance, not a user-role denial.

The API passes the absolute file path to its CodeWorker through a narrowly
allowlisted child environment. The worker verifies the file locally against its
own embedded trust before C# PDF execution. It receives no signing key, runtime
trust override, or server-mode exemption. Missing/wrong/expired licenses produce
structured worker diagnostics without document or PDF output. Scripts cannot
load licenses or inspect the host's configuration. Model/Base64-only DTO
evaluation remains separate; producing its PDF preview still requires `pdf`.

Native renewal uses explicit `PvadLicense.LoadFromFile`/`LoadFromStream` calls;
there is no new automatic file watcher or native Admin renewal API in this
milestone. Existing server-license renewal does not activate a native license.

## Isolated Signed Test Runs

```bash
node tools/native-licensing/with-test-license.mjs pdf
node tools/native-licensing/with-test-license.mjs word
node tools/native-licensing/with-test-license.mjs spreadsheet
node tools/native-licensing/with-test-license.mjs importer
node tools/native-licensing/with-test-license.mjs image
node tools/native-licensing/with-test-license.mjs test \
  tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj
node tools/native-licensing/with-test-license.mjs test \
  tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj \
  --filter FullyQualifiedName~PvadNativePdfLicenseTests
```

The runner generates an ephemeral key in memory, creates signed PDF-only,
Word-only, Spreadsheet-only, Importer-only, combined integration, and expired fixtures,
and compiles into an isolated
artifacts tree with only the temporary public key. Test activation is a
test-project-only module initializer;
it calls the normal signature verifier. The runner removes licenses, trust files,
and synthetic-trust binaries in `finally`, retaining only results. Regular
`bin`/`obj` directories and the source trust register remain unchanged. Worker
packaging follows the actual target path so isolated runs cannot accidentally
copy a stale ordinary worker.

Plain PDF/Word/Spreadsheet-processing tests without a signed native license now
fail by design.
The dedicated native workflow uses the runner. General .NET CI, visual/platform
parity, and scheduled fuzz jobs now prepare a shared signed test session before
restore and always clean it up. The full API validator, visual tools, coverage,
and browser/operator rehearsals use the same owned-session configuration and
actual MSBuild target paths. Pack/Publish are blocked for synthetic sessions;
ordinary release/container jobs remain separate.

Shared test sessions include `importer` for the integrated entry-point milestone,
alongside `pdf`, `word`, and `spreadsheet`. This is a normally signed test grant,
not evidence that every Importer engine is protected. Product-only fixtures
remain separate, and no Converter or Migration grants are enabled in advance.

See [shared workflow sessions](../tools/native-licensing/README.md#shared-workflow-sessions)
for commands and the launcher coverage table. Before Word enforcement, the
complete local unfiltered API/PostgreSQL audit passed on 2026-11-15:
1,467 concrete cases, zero failures or
skips, with strict discovery/result matching and unchanged test assembly bytes.
Temporary storage, test licenses, and isolated binaries were removed. The user
also completed the separate local Docker post-check and reported no containers
matching `org.testcontainers=true`; this was manually reported, not an
agent-executed inspection or an audit of unlabeled resources.

The [implementation checklist](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#complete-api-and-postgresql-acceptance)
records the exact evidence and limits.

The local coverage instrumentation run before Word enforcement also passed on
2026-11-15: all 52
collector hosts, 947 passing smoke cases, and six tooling contracts against the
actual isolated Release dependencies. Each host retained TRX, Cobertura, and JSON
reports with hits in the expected product module. An independent re-read
reproduced the execution summary exactly. The Licensing dependency remains in
the measured module sets; temporary signed fixtures and binaries were removed
without changing source trust. See the
[coverage checkpoint](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#local-coverage-instrumentation-acceptance)
for evidence and group counts. This verifies instrumentation on macOS, not full
functional test coverage or an overall coverage percentage. Complete CI,
platform matrices, and campaigns remain open; a prepared cross-platform workflow
is not evidence that those platforms passed.

After Word enforcement, the separate local regression run passed all 299 cases:
six Word infrastructure, 141 Generator, 80 licensing-core, and 72 targeted API
and worker tests. The native Word probe passed 34 checks using product-specific
licenses, and the existing PDF probe retained its 20 passing checks. Signed
fixtures and isolated binaries were removed. See the
[Word milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-word-generation-and-signing-milestone)
for retained evidence and limits. This targeted checkpoint does not re-certify
the earlier full API or coverage matrix for the changed source.

The subsequent Spreadsheet checkpoint passed 145 native admission/compatibility
checks across 40 protected entries, 370 selected .NET cases, and 45 tooling
contracts. It includes 15 real API denials under a PDF-only license, free
validation, and successful export after renewal. PDF-only and Word-only probes
still passed their 20 and 34 checks. See the
[Spreadsheet milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-spreadsheet-enforcement-milestone)
for reports, cleanup, and remaining platform/product boundaries. No new full
API/PostgreSQL, coverage-matrix, or visual-parity acceptance is implied.

## Terms And Offline Limits

- Trial: explicitly signed 30-day term, no grace, no redistribution.
- Subscription: seven days of grace after signed expiry; no new admissions after grace ends.
- Perpetual: no usage expiry for covered releases; the signed update cutoff is compared with embedded release metadata, not installation or file timestamps.
- Provider grants use explicit canonical keys scoped by migration domain/kind. Aliases, wildcards, roles, and Cloud editions are not native grants.
- Developer seats and redistribution rights are contractual. Offline operation cannot guarantee global seat/instance counting, immediate revocation, or a trustworthy clock.

Signed revocation-list import remains a separate milestone.

## Signature And Trust Contract

The version-one JSON envelope contains `schemaVersion`, `keyId`, `algorithm`,
`payload`, and `signature`. Algorithm is `ECDSA_P256_SHA256_P1363`.
Payload and signature are Base64; the 64-byte signature uses IEEE P1363
fixed-field concatenation. Verification covers exact decoded UTF-8 payload bytes,
never a reserialized object.

Payload size is limited to 32 KiB. Unknown/missing properties, duplicate names
(including case variants), non-UTC dates, unsupported versions/models, duplicate
grants, and malformed signature data are rejected. The embedded public-key
register is the sole production trust source; keys inside licenses are not trusted.

`LibraryRelease.json` is checked against root `VERSION` and the existing release
manifest. It is a metadata snapshot, not a new release announcement. Release
preparation must synchronize it without substituting build time for release date.

## Operation Matrix

The machine-readable inventory is
[native-library-products.v1.json](../product-metadata/native-library-products.v1.json).
Its `pdf-word-and-spreadsheet` status records the integrated PDF generation,
Word generation/signing, and native Spreadsheet boundaries. The other seven
product flags remain false.
They must only change after their own operation-level negative and positive
tests exist.

| Product | Required integration boundaries |
| --- | --- |
| PDF | Native generation, byte/stream/file output, mapper rendering |
| Word | Facade, both direct Word export overloads, and DOCX signing service |
| Spreadsheet | Builder/retained-handle mutations, calculation, XLSX/XLS/CSV/TSV IO, data operations, and design conversion |
| Importer (partial) | Built-in imports, PDF load/save, parser/color/font/Analysis, editing/content rewriting, Trust/security/signing/redaction/e-invoice, regeneration/mapping/debug overlays, image analysis, OCR and worker admission guarded; generic bundle rights/lifetime remain open |
| Converter | Each direct exporter/converter implementation |
| PDF Code migration | Every canonical PDF code provider |
| PDF Viewer migration | Every canonical PDF viewer provider |
| PDF Designer migration | Source-PDF reconstruction providers |
| Spreadsheet Code migration | Every canonical Spreadsheet code provider |
| Report Designer migration | Every canonical Report Designer provider |

Actual providers come from `MigrationProviderCatalog`, not duplicated aliases.
The `auto` detection route is not a license grant: resolve and authorize the
detected concrete provider before starting its conversion.
Contracts/metadata stay free; internal renderer dependencies must not become
unexpected extra purchases.

## Manufacturer Preparation Tool

`tools/PVAD.LicenseIssuer` is an offline operator tool, **not** an authenticated
issuance service. Private-key file access authorizes the local operator; Unix
group/other access is rejected. The tool never generates keys, overwrites an
existing license, or logs payload/key contents.

```bash
dotnet run --project tools/PVAD.LicenseIssuer -- \
  --payload /secure/approved-native-payload.json \
  --private-key /secure/manufacturer-key.pem \
  --key-id manufacturer-key-1 \
  --output /secure/new-native-license.json
```

Authenticated native-license issuance, durable native-license audit,
Admin/Account integration, and authorized native-license downloads remain open.
Existing server-license verification and issuance are now separate services;
the default customer Compose mounts only a public verification key. Manufacturer
server-license issuance requires an explicit, separately provisioned issuer mode.
It does not activate native licenses or change their embedded trust register.
See the [deployment boundary](../deploy/api/README.md#license-verification-and-manufacturer-issuance).
Do not mount manufacturer private keys into customer containers.

## Diagnostics

| Code | Meaning |
| --- | --- |
| `PVADLIC000` | Active license |
| `PVADLIC001` | No active native license |
| `PVADLIC002` | Invalid embedded trust or release metadata |
| `PVADLIC003` | Empty or oversized data |
| `PVADLIC004` | Malformed or ambiguous document |
| `PVADLIC005` | Unsupported schema or algorithm |
| `PVADLIC006` | Untrusted signing key |
| `PVADLIC007` | Invalid signature or curve |
| `PVADLIC008` | Not yet valid |
| `PVADLIC009` | Expired |
| `PVADLIC010` | Subscription grace period |
| `PVADLIC011` | Release outside perpetual update rights |
| `PVADLIC012` | Product/provider not licensed |
| `PVADLIC013` | File/stream could not be read |

## Protection And Packaging

Pinned Obfuscar and ILSpy tools protect/audit candidate output. Public APIs and
serialization metadata must survive, third-party binaries stay unchanged, and
debugging maps stay private. Renaming internal code raises inspection effort;
it does not prevent decompilation or patching. Signatures authenticate licenses,
not customer-controlled runtime integrity.

No anti-debugger crashes, process scanning, self-modifying loader, embedded
private signing keys, or strong-name-as-security claim is permitted. Native AOT
and commercial anti-tamper tools are deferred.

See the [implementation checklist](../checklists/PVAD.Native-Library-Licensing-And-Protection.md)
for open work. Public package publishing and version bumps are separate actions.
