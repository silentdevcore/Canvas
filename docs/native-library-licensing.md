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
rewriting, the editable-document facade, Trust inspection, change-impact
evaluation, PDF signing/timestamps, redaction/audit, E-Invoice attachment/extraction,
archival conversion, built-in color processing and the combined Trust
post-processing pipeline, regeneration, recursive mapping, XObject resolution
and debug overlays now retain their admitted context through supported built-in
completion. The seven non-PDF registry importers also complete their private
parsing after a license replacement without acquiring other product rights.
The complete current public Importer surface is now classified in a versioned
inventory and checked against compiled assemblies and native probe registrations.
All ten product areas have current entry-point enforcement. Cross-product,
protected-package and worker/platform acceptance remain separate open gates.
Native HTML, Markdown, CSV, XML, SVG, ODT,
PNG, JPEG and TIFF exports now require the separate `converter` grant.
The 16 native PDF Code and 8 Spreadsheet Code provider entry points, standalone
PDF source analyses and built-in conversion/diagnostic pipelines now require
their own domain/kind/provider grants. All eight Report Designer converters now
have provider admission. Overall composite/package acceptance remains partial. Nutrient Viewer and
source-PDF Designer conversion now require separate provider-scoped grants.
Built-in Spreadsheet export, calculation and file-save composites now share
Spreadsheet-only admission; the product dependency inventory records existing
boundaries without granting a universal product bundle.
Existing server subscription checks remain additional, independent checks. This
is not yet complete commercial SDK protection.

The shipped trust register is intentionally empty until manufacturer public keys
are approved. There is no built-in evaluation key, runtime trust override,
internet call, activation bypass, or production signing-key generation.

**Breaking behavior:** with the empty source trust register, ordinary builds
cannot create/export PDFs, generate/sign DOCX, process spreadsheets, export
Converter formats, or run licensed imports/image analysis/OCR until
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

## Manufacturer Issuance And Renewal

A separate, default-disabled manufacturer backend can now issue and renew native
license artifacts. It requires explicit issuer-user allowlisting, the actual
global System Administrator role, license-management permission, and a recent
live verified-user session. Customer servers must remain verification-only;
their server offline-license configuration cannot authorize native issuance.

Signing uses a separately mounted manufacturer private key and validates every
new artifact against the native embedded public-key register before storage.
It never generates a replacement key or trusts a key supplied by a request.
Production native public-key provisioning remains an explicit release gate.

Issuance records and successful audit events commit atomically to an immutable
ledger. Idempotency keys prevent duplicate retries; concurrent renewals produce
at most one successor. Renewal extends subscription validity or perpetual update
eligibility without changing customer, model, product/provider rights, start date,
or redistribution permission. Trials cannot be renewed. Original artifacts are
not rewritten or remotely revoked.

PVADE Admin now has a separate **Native library licenses** manufacturer workspace
for catalog-driven issuance, inspection, download and renewal. It is distinct
from server offline licenses and disabled on verification-only deployments.
PVADE Account > Licenses lists native artifacts for the active organization, with
product/provider details and downloads. These read-only customer endpoints need
an active verified session and membership, not issuer mode or private keys.
Foreign IDs return 404 within an otherwise authorized workspace. Stale responses
are discarded after user or organization changes. Native downloads retain the
original bytes; they check stored integrity, while activation independently
verifies signatures and current processing rights. Neither downloading nor
renewing an artifact remotely revokes old offline copies.

The restricted manufacturer API is excluded from public OpenAPI. Operational
configuration and delivery procedures belong to the protected
[manufacturer runbook](../operator-docs/PVAD.Native-Library-License-Issuance.md),
not the public Documentation navigation. These capabilities do not provision
production keys or replace commercial and package-distribution approval.

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
and protected-package acceptance remain open prerequisites, not an
automatic action performed by this integration.

## Code Migration Provider Activation

Load an issued native license with the exact product and canonical provider,
for example `migration.pdf.code` with `aspose-pdf`:

```csharp
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var migration = new PVAD.Migration.Pdf.Code.Aspose.AsposePdfMigration();
var result = migration.Migrate(source);
string convertedCode = result.MigratedCode;
```

All 16 PDF Code and 8 Spreadsheet Code native `Migrate` implementations check
their own rights before source parsing. Calls through `ISourceMigration`, the
abstract base, factories and existing Spreadsheet wrappers reach that same
sealed provider boundary. A native PDF generation license is not a migration
license. Likewise `aspose-pdf` in `migration.pdf.code` does not authorize
`aspose-cells` in `migration.spreadsheet.code`; the same vendor family does not
merge product rights. Legacy routing aliases resolve to canonical providers, but
signed licenses use exact canonical keys, never aliases or `auto`.

Constructing services, listing providers, reading metadata and using DTOs remain
free. Converting source writes text and diagnostics; it does not execute source
or grant PDF/Spreadsheet generation rights. Running the resulting program is a
separate licensed product operation. Successful code/diagnostic algorithms and
existing migration IDs are unchanged. A failed replacement license preserves the
current snapshot; valid renewal allows a new call on existing provider objects.

`CSharpPdfTrustEvidenceScanner.Scan` and `PdfEInvoiceMigrationCatalog.Analyze`
also require the matching PDF Code provider grant. Catalog listing, profiles and
pure contracts remain free. Public calls always admit fresh work. Built-in
internal continuations bind product, canonical provider and originating verifier;
they cannot switch providers, omit provider identity, survive their closed scope
or obtain private PDF dependencies. Caller-triggered native calls still check
current rights, even during an admitted analysis. Original callback failures are
not converted into successful migration results.

`PdfMigrationProviders.Migrate` shares admission through conversion and Trust
evidence. `SpreadsheetMigrationProviders.Migrate` returns code and diagnostics
from one native conversion. The API uses that result instead of converting twice.
Only first-party internal implementations use admitted dispatch; custom converter
overrides keep their public dispatch and fresh native calls.

Built-in Spreadsheet preview is a replay of recognized calls into an HTML grid,
not a real workbook calculation or file export. Its Code grant is sufficient;
no additional Spreadsheet, PDF or Converter grant is introduced. PDF preview
creates a real PDF: Code admission covers conversion only. The public PDF
constructor and writer independently check current `pdf` rights. Admission does
not reserve PDF permission for a later stage. If those rights change before
serialization, preview fails with the native diagnostic and returns no partial
download. No code execution, complete preview fidelity or new rendering behavior
is implied by these license checks.

Report Designer uses a separate provider-bound grant. Nutrient Viewer and
PDF Designer are covered by the following separate admission boundaries.
Local tests and prepared CI do not replace protected-package, cross-platform,
manufacturer issuance or complete commercial-release acceptance.

See the [Code provider entry milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-code-migration-provider-entry-milestone)
for current evidence and remaining boundaries.

See the [analysis and composite milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#code-migration-analysis-and-composite-milestone)
for the later provider-bound lifetime and preview-rights checkpoint.

## Nutrient Viewer And PDF Designer Activation

The three public native Viewer conversion methods require
`migration.pdf.viewer` with the exact provider `nutrient-viewer`:
`NutrientViewerSourceConverter.Convert`, `ConvertTranspiledTypeScript` and
`NutrientInstantJsonConverter.Convert`. Examples, DTOs and capability metadata
remain readable without a native license. These converters analyze source;
they do not execute the supplied JavaScript or TypeScript. Transpilation remains
a separate existing step, not a new native compiler entitlement.

```csharp
using PVAD.Licensing;
using PVAD.Migration.Pdf.Viewer.Nutrient;

PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var converted = new NutrientViewerSourceConverter().Convert(
    "NutrientViewer.load({ document: '/source.pdf' });");
```

Editable source-PDF conversion through
`NutrientViewerDesignMigrationService.ConvertAsync` instead requires
`migration.pdf.designer` with provider `nutrient-viewer`. That single operation
includes its own private PDF import and built-in Viewer interpretation. It does
not require separate Importer, Viewer, PDF or Converter grants to produce the
design DTO. The private source-import path is fixed to that purpose and provider,
bound to the originating verifier, and closed at the end of the operation.
It cannot create private PDF output or transfer engine rights to callbacks.

Viewer and Designer grants are not interchangeable. Neither grant authorizes
an independent public `PdfFileImporter.DoImportAsync`, PDF export or direct
Viewer call made inside a caller callback. Such public work checks the current
license afresh. A completed or foreign-authority continuation cannot be reused.
An already admitted built-in operation may finish after license expiry or
replacement; subsequent public operations require new valid admission.

Both canonical and compatibility HTTP routes preserve typed `PVADLIC...`
Problem Details with status 503. A native license error from Designer conversion
is not presented as a malformed PDF. Identity, organization, entitlement,
provider permission, feature gate and input validation remain independent
requirements. A native license alone never grants access to a server route.

See the [Viewer and Designer milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#nutrient-viewer-and-pdf-designer-milestone)
for scoped evidence and the remaining Report Designer/package acceptance work.

## Report Designer Activation

Every native Report Designer conversion requires `migration.report.designer`
and the exact canonical provider key:

| Provider | Signed Grant Key |
|---|---|
| ActiveReports JS | `activereports-js` |
| DevExpress Reports | `devexpress-report` |
| FastReport | `fastreport` |
| JasperReports | `jasperreports` |
| RDL/RDLC | `rdl` |
| ActiveReports RPX | `rpx` |
| Stimulsoft | `stimulsoft` |
| Telerik Reporting | `telerik` |

```csharp
using PVAD.Licensing;
using PVAD.Migration.Report;

PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var migration = ReportMigrationProviders.Create("rdl");
var converted = migration.Convert(File.ReadAllText("Monthly_Report.rdl"));
var design = converted.Design;
```

Direct converters in `PVAD.Migration.Report.Designer.*`, their public overloads,
aggregate wrappers and factory/interface dispatch all enforce the same provider
right. They create an editable design DTO, not a PDF. PDF generation, import,
workbook processing and other independent operations still require their own
native rights. Report conversion grants none of those products implicitly.

`ReportMigrationService` resolves explicit aliases or auto-detection to a concrete
canonical provider, then admits conversion before merging caller-owned resources.
`auto` is routing, never a signed license key. Format detection, provider metadata
and generic package utilities remain free input utilities and may run before
conversion admission; they do not authorize conversion. Existing archive limits
and supported-format detection remain unchanged.

Standalone `DevExpressReportResourceParser.ParseResx`, including its compatibility
facade, now requires `devexpress-report` rights. Inside an admitted report
conversion, shared RESX handling uses only the selected provider's report grant.
For example, RDL resource preprocessing does not unexpectedly require a separate
DevExpress license. Built-in wrapper, converter, resource and nested subreport
processing retain the same originating admission. Public calls made by a caller's
resource callback check the current license afresh; no callback or retained
context receives transferable rights.

An admitted conversion may finish after replacement or expiry; subsequent public
work is checked again. Canonical and legacy conversion endpoints preserve native
license Problem Details instead of returning partial designs or disguising the
failure as a source-format error. User, organization, permission, entitlement and
feature checks remain separate from native licensing.

See the [Report Designer milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#report-designer-migration-milestone)
for the scoped tests and remaining overall protection/package acceptance.

The Report checkpoint passed 490 tests across all eight provider suites, aggregate,
licensing and selected API suites, plus 703 native admission checks and 65 tooling
tests. These are local macOS/x64 checks with synthetic licenses. Provider fidelity
and supported features were not promoted by adding licensing. Full package,
platform and commercial acceptance remain separate gates.

## Converter Activation And Migration

Load an issued `converter` grant before using any of the nine native
`PVAD.Infrastructure.Converters` exporters:

```csharp
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var exporter = new PVAD.Infrastructure.Converters.HtmlDocumentExporter();
byte[] html = exporter.Export(design);
```

Protected formats are HTML, Markdown, CSV, XML, SVG, ODT, PNG, JPEG and TIFF.
All thirteen declared public export overloads and both `IDocumentExporter`
overloads use the same product boundary. Default interface dispatch does not
avoid admission. Exporter construction, `FormatKey`, `MimeType`, `FileExtension`,
capabilities and design/options DTOs remain usable without a native license.

Each public export admits fresh work before reading the design, allocating a
render target or traversing pages. `ConverterExport` retains that verifier-bound
admission through the sealed built-in implementation, including multi-page image
archives and TIFF's internal PNG rendering. It grants no private PDF dependency
or additional PDF, Word, Spreadsheet or Importer rights. An independently started
operation on those products still needs its own grant. Native PDF/DOCX/workbook
exports retain their own product rules and do not acquire a new Converter check.

A caller-supplied style value may run `ToString()` during export; a new native
operation inside that callback still checks current rights. Callback license and
source IO failures are not converted to success. Existing layout, formatting,
image retrieval, font links, DPI defaults, ZIP and cancellation behavior remain
unchanged. This milestone does not introduce new cancellation support or imply
that exported HTML/SVG assets are self-contained offline.

Missing, wrong-product or expired rights produce the existing typed `PVADLIC...`
diagnostics instead of an output file. Server-side native failures continue
through the central Problem Details handler, without including license paths or
turning them into corrupt document downloads. User/tenant/feature authorization
remains a separate check. Renewal enables subsequent exports without restarting
the process; a failed renewal does not discard the previous snapshot.

See the [Converter milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-converter-enforcement-milestone)
for test scope and remaining release gates.

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

**Public entry enforcement and complete release acceptance are separate.** The direct-engine and color/font boundaries
described below are now guarded, together with the editing boundaries.
The Trust/security/signing/redaction/e-invoice boundaries and the regeneration/
debug-overlay boundaries are also guarded as described below. Standard
regeneration uses restricted internal PDF dependencies rather than requiring
a separate PDF grant. Image analysis and OCR/worker boundaries are covered by
the later milestone below. The current public-surface inventory closes the entry
classification. Cross-product dependencies, protected packages and worker/platform
execution remain open. The operation matrix records `importer.enforced: true`
with a separate partial `acceptanceStatus` and `remainingAcceptance` list. This
does not declare protected packages or commercial distribution ready.

The local milestone passed 111 isolated admission checks and 860 .NET tests,
including the complete FileImporter and Importer suites. See the
[Importer checkpoint](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-entry-point-milestone)
for evidence, fixture limits, and unfinished engine/dependency-rights work.

### File Import Stream Ownership And Continuations

The caller owns the supplied stream and disposes it after import, including when
import fails. Markdown readers and ODT package readers leave that stream open;
their private readers, package objects and entry streams are still disposed.
Text-size, upload and cancellation limits remain unchanged.

The [file importer inventory](../product-metadata/native-file-importer-entries.v1.json)
lists all eight registry implementations and twenty declared public processing
overloads. The probe compares that inventory with the actual exported types and
methods. Seven non-PDF implementations are tested through their sixteen direct
overloads, seven registry paths and seven cancellation-interface paths. PDF has
separate loader, analysis and mapping continuation coverage.

Each non-PDF operation checks the current Importer grant before reading input.
Private parsing already admitted at that boundary can finish after replacement;
it needs neither an ambient grant nor another product license. New public calls
must pass admission again. The Markdown remote-image resolver remains a caller
extension: it cannot confer rights on public native calls, even across an await.
No network request is made by the synthetic resolver tests.

The local tests compare full DTOs, geometry, assets and diagnostics, normalizing
only validated generated document IDs and the image importer's generated page
and element IDs. They exercise seekable and non-seekable streams, synchronous
reads and yielded asynchronous reads, resolver failure and renewal. Synchronous
importers remain synchronous; an asynchronous wrapper does not imply they yield.
This fixture pack is not a complete upstream file-format compatibility corpus.
See the [file importer continuation checkpoint](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#non-pdf-file-importer-continuation-and-stream-ownership-milestone)
for the actual run results and remaining acceptance boundaries.

### Complete Public Importer API Contract

The [reviewed public surface](../product-metadata/native-importer-public-surface.v1.json)
covers `PVAD.Importer`, `PVAD.FileImporter`, `PVAD.FileImporter.ImageAnalysis` and
`PVAD.FileImporter.ImageOcr`: 363 exported types and 5,087 declared public members.
Eighty-one processing types expose 201 protected entries, including the signing-key
constructor. The remaining types are 236 models, 25 utilities and 21 interface
contracts. A contract declaration is not a right to call an implementation:
built-in methods and delegated interface calls still require native admission.

The compiled contract checks members, base types, implemented interfaces and
explicit interface bindings. A new type, method, constructor, property or binding
requires review. Type names are canonicalized through reflection without assembly
version strings, so a product version bump alone does not change this contract.
Every protected member must also appear in the registration set emitted by a
successfully completed Importer or image/OCR native probe. This is a contract and
test-coverage gate, not proof against arbitrary changes to method bodies.

Detached object/page data mutations, object-graph lookup, scalar geometry,
capability metadata, disposal and generic byte buffering are free. Buffering raw
bytes does not parse a PDF. Editing services, font/content decoding, rendering and
saving remain protected. The remote-image adapter is a separate bounded HTTP
utility; it does not authorize import, OCR or native execution, and its existing
SSRF restrictions remain mandatory. Synthetic API tests make no HTTP requests.

The OCR worker executable is not a public library API. It retains its independent
signed-license and request-dispatch tests, rather than being counted as another
unlicensed contract. Image analysis is additionally tested through both the
explicit `IFileImporter` implementation and its default cancellation method.

Run `node tools/native-licensing/importer-surface.mjs verify` to build the current
surface, compare the inventory, execute both native probes and require exact
protected-member coverage in an owned signed session. `capture` writes only a
review candidate under artifacts; it never accepts a new source inventory.
See the [public API checkpoint](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#importer-public-api-contract-milestone)
for actual local evidence and unfinished release acceptance.

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

Other product
composite call graphs, have not yet been converted end to end.
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
for exact tests, algorithm comparison and remaining scope. At that checkpoint,
signing, redaction, e-invoice, advanced regeneration and other composite product
chains remained open; the following milestones extend only their named subsets.
Whole Importer release acceptance remains incomplete; current public entry
classification and enforcement are recorded in the later public API checkpoint.

### PDF Trust Inspection And Change-Impact Continuations

`PdfTrustInspector.InspectAsync` and `PdfChangeImpactAnalyzer.Analyze` admit new
work against the current Importer license. Their internal continuations retain
the original verifier-bound admission through asynchronous reads, object/stream
parsing, embedded evidence, certification revision inspection, structural PDF/A
validation and change-impact enumeration. The two purposes grant no private PDF
generation, regeneration, blank-page or OCR dependency rights.

The public CRL/OCSP validation APIs remain fresh licensed entries. Only the
built-in signature-bound embedded-evidence path uses their internal historical
validation continuations. Caller-supplied revocation/issuer providers retain
their interface dispatch; any public native API they call still needs its own
current license. There is no caller-supplied admission or trust override.

Trust, integrity, revision coverage, certificate trust, timestamps, revocation,
certification permissions and structural conformance remain separate results.
No network policy, signature algorithm, size limit, error handling or validation
threshold is relaxed. A genuine callback license failure still propagates rather
than becoming an invalid-PDF report. License replacement alone no longer aborts
an already admitted inspection; the next public operation is denied if the new
license lacks the necessary right. Full rewrites still invalidate signatures;
incremental changes still require validation, not automatic preservation.

This milestone does not migrate signing execution, redaction, e-invoice,
archival conversion, post-processing or advanced regeneration end to end.
See the [Trust continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-trust-inspection-and-change-impact-continuation-milestone)
for exact evidence and remaining acceptance boundaries.

### PDF Signing And Timestamp Continuations

Public PAdES signing, PKCS#12 hash signing, document timestamping and RFC 3161
requests remain freshly licensed operations. Built-in processing retains its
original Importer admission through parsing, incremental writes, CAdES assembly,
DSS/VRI evidence writing, archive timestamps and post-sign Trust inspection.
Neither signing nor timestamp purposes grant PDF generation or any other private
product dependency. Key construction remains freshly guarded; certificate
metadata and disposal remain free.

The PKCS#12 key and RFC 3161 authority are sealed classes. Only these known
built-ins use internal continuations; custom `IPdfSigningKey` and
`IPdfTimestampAuthority` implementations retain ordinary interface dispatch.
Long-term material providers remain custom asynchronous callbacks. A public
native call made inside any custom callback checks the current license and can
fail independently of the surrounding admitted operation. No public bypass,
admission token, signing-key export or trust setter is introduced.

`SignAsync` preserves synchronous public license rejection. Its asynchronous
implementation establishes its own fresh admission before processing, so no
ambient scope is exposed to a caller while a Task is pending. New public calls
never inherit a pending signing admission. Actual cancellation, malformed PDFs,
encrypted-source restrictions, failed timestamp transport, response limits,
cryptographic/certificate checks and requested-profile validation are unchanged.

See the [signing continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-signing-and-timestamp-continuation-milestone)
for synthetic evidence and limits. These changes do not close redaction,
e-invoice, archival conversion/post-processing, advanced regeneration, other
product graphs or protected-package/platform acceptance.

### PDF Redaction And Audit Continuations

Public preflight, apply and audit-verification calls check the current Importer
license. Their built-in continuations retain the original verifier-bound
admission through parsing, scene construction, bounds, pixel decoding/removal,
content rewriting, full rewrite, residue scanning and optional audit signing or
timestamping. Direct internal scanner/image entries also establish fresh
admissions; the surrounding apply uses their admitted cores. None of the
redaction, audit or color-resolution purposes grants a private PDF dependency.

Only the sealed built-in preflight, key and timestamp authority use internal
dispatch. Custom preflight/key/authority implementations retain their interfaces;
any public native API they invoke checks current rights independently. Genuine
license failures from source, resolver or decoder callbacks propagate rather
than becoming unsupported content, empty color data or a successful audit.

The full rewrite, blocking findings, image sample clearing, mask handling,
metadata/attachment removal and residue checks keep their existing algorithms.
No new codec or removal capability is introduced. Existing preflight limits,
cancellation and failure behavior are unchanged. An empty names dictionary is
allowed after attachment removal; its embedded-file references and payloads must
be gone. Output verification still requires a single rewritten revision.

Audit verification retains canonical-hash, optional CMS and timestamp-integrity
checks. It is not a new certificate-chain trust decision or a replacement for
document-signature validation. No source content is added to the audit record.

Color-space resolution and function construction now have bounded internal
continuations, including recursive factories and stream decoding needed by image
redaction. General `ToSrgb`, function `Evaluate`, ICC conversion and content-color
rewriting were not included at that checkpoint; the following E-Invoice milestone
adds their built-in continuations. Retained functions still require a fresh
license when evaluated through their public entry.

See the [Redaction continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-redaction-and-audit-continuation-milestone)
for exact historical tests, source comparisons and scope.

### E-Invoice And Archival Conversion Continuations

Attachment metadata uses the system UTC clock by default. For reproducible
processing, `PdfEInvoiceAttacher` also accepts a `TimeProvider` as its second
constructor argument. A fixed clock makes repeated attachments byte-identical
when the source, payload, options and conversion inputs are identical. An
advancing clock updates the embedded file's UTC `ModDate`; it does not control
license validity, signature validation or archival validation. The original
one-argument constructor remains available. The native continuation probe uses
a fixed metadata clock and retains its strict full-PDF byte comparison.

Attachment preflight, attachment writing, associated-file extraction and
PDF/A-2b/3b conversion check current Importer rights at every public entry. An
admitted operation keeps its original verifier-bound admission through parsing,
bounded decoding, incremental/full writing, built-in color conversion and
post-save verification. These purposes grant no private PDF-generation rights.

Only sealed built-in archival converters and Trust inspectors use internal
continuations. Custom implementations keep their interface dispatch. Built-in
function arrays, exponential, stitching, sampled and PostScript functions can
continue internal evaluation; a caller-defined `PdfFunction` still enters its
fresh public evaluator. Resolver callbacks do not acquire rights to start new
public work. Exceptions raised by actual native calls inside callbacks are not
converted to unsupported-content results.

The existing approvals for archive conversion, replacement and signed-document
modification remain required. Original stream ownership, size limits, filter and
pixel restrictions, source hashes, extracted XML bytes, declared size/MD5 checks,
PDF/A structural validation and page-content verification are unchanged. This
does not add tax-schema validation, certify fiscal compliance or prove external
PDF/A conformance. Replacing an attachment still leaves its old bytes in previous
incremental revisions; it is not secure erasure.

Local probe coverage compares licensed references with synchronous and truly
asynchronous license replacement, verifies exact output bytes and XML, retains
unrelated attachments and mixed page geometry, and exercises real LittleCMS
conversion. Trial expiry and unavailable private dependencies are covered by
the verifier-core tests. See the
[E-Invoice continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#e-invoice-and-archival-continuation-milestone)
for exact evidence and limits. Advanced regeneration and other native graphs,
Migration enforcement, native issuance/UI,
protection, packaging and cross-platform acceptance remain separate work.

### Combined Trust Post-Processing Continuations

`PdfTrustPostProcessingPipeline.ProcessAsync` checks the current Importer
license before accessing source content or dispatching a stage. One internal,
verifier-bound admission then spans the sealed built-in Redaction applier,
PAdES signer and Trust inspector. Their existing continuations retain admission
through audit signing/timestamps, DSS/VRI evidence and final inspection. The
post-processing purpose grants no private PDF-generation dependency rights.

The sequence remains Redaction, then Signing, then Validation. Plan checks run
before any processing; a failed Redaction verification prevents signing.
The validation report describes the exact final bytes returned. Structural,
PDF/A, PDF/UA, signature and risky-content policies still fail closed using
their existing diagnostic codes. No validation threshold or supported format
changes as part of this licensing fix.

Custom Redaction, signing and inspector implementations retain normal interface
dispatch. Custom keys, timestamp authorities and material providers keep their
existing boundaries: a public native operation they start must check the current
license, even inside a running pipeline. A material provider may return data
after an asynchronous await without starting a new native operation; it cannot
inherit admission for independent public processing. Public signing retains its
synchronous license-denial behavior. A retained pipeline/result cannot start
another operation after expiry or replacement without current rights.

Local isolated probes cover validation-only, Redaction-only, signing and combined
flows with all four PAdES profiles, source/region/provider/HTTP license replacement,
mixed pages, independent final-byte inspection and content extraction. Synthetic
keys and an in-memory timestamp transport avoid network/customer dependencies.
Original input, actual callback failures, cancellation and policy rejection
remain preserved. See the
[Trust pipeline continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#trust-post-processing-continuation-milestone)
for evidence and remaining acceptance boundaries. This is not full native-product,
protected-package or cross-platform release acceptance.

### Regeneration, XObjects And Debug Continuations

Ordinary imported-PDF regeneration and `PdfGeneratorBridgePipeline.SaveAsync`
check the current Importer license, then retain one verifier-bound admission
through the sealed built-in bridge, image/palette decoding, trusted private PDF
writer and shading parse/rewrite/append processing. The writer checks the existing
`ImporterRegeneration` dependency before and after trusted population. This does
not grant general PDF construction, serialization, blank-page or OCR rights.

The configured overload remains different: a non-null `configureDocument`
callback additionally requires current PDF rights, rechecks Importer rights after
the callback and uses ordinary public PDF rendering. Removing either product
right during that callback still fails before output. Custom bridge dispatch
also remains public; its native calls admit fresh work. A caller-owned output
stream does not inherit permission to start another protected operation.

Object/graphics/resource mapping, XObject resolution and debug-overlay building
use fresh analysis admission and internal-only continuations. Recursive mapping
does not re-admit each child. Form interpretation and bounds computation use the
existing built-in parser/interpreter/bounds cores. Mapping, XObject and debug
contexts cannot use regeneration's private PDF dependency.

No content interpretation, recursion limit, filter capability, image conversion,
shading algorithm or overlay-label behavior changes. XObject form resolution
still consumes the same supplied encoded/decoded representation as before; this
is not new compressed-form support. Stream ownership and errors remain unchanged.
If the caller's output stream partially writes and then fails, atomic rollback
is not promised. Failures before writing leave its original content untouched.

The local probe compares exact regenerated bytes, reopens mixed-size/rotated
pages and verifies retained text, paths, images and shadings. It separately tests
recursive object/resource mapping, form bounds/transforms, overlay items, new-call
denials and synchronous/asynchronous output/custom-bridge callbacks. See the
[regeneration continuation milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#regeneration-xobject-and-debug-continuation-milestone)
for exact evidence and remaining product/package/platform boundaries. Selected
PDF Importer graphs are covered; other native product graphs and commercial
protection/packaging acceptance remain open.

### Product Dependencies And Spreadsheet Continuations

The [dependency inventory](../product-metadata/native-library-dependencies.v1.json)
distinguishes fresh public product operations, internal same-product continuation,
the existing private byte-only dependencies and future integrations. It is an
audited technical inventory, not pricing, edition selection or a new universal
bundle grant. Planned Migration rows remain unenforced work; the later Converter
milestone updates that product's inventory row to implemented.

| Operation | Admission | Additional product right |
| --- | --- | --- |
| Public PDF rendering | PDF | No Converter right |
| DOCX export or separately started DOCX signing | Word | No PDF/Converter right |
| Built-in XLSX/XLS/CSV/TSV save or spreadsheet calculation | Spreadsheet | No PDF/Word/Importer/Converter right |
| HTML/Markdown/CSV/XML/SVG/ODT/PNG/JPEG/TIFF document export | Converter | No PDF/Word/Spreadsheet/Importer right |
| Spreadsheet-to-design DTO conversion | Spreadsheet | No renderer right is created |
| Separately started PDF rendering of that DTO | PDF | Fresh public operation |
| Importer regeneration/blank-page/OCR byte rendering | Importer | Only its existing purpose-bound private dependency |
| Caller-configured regeneration | Importer and current PDF | Existing callback checks remain fresh |

`SpreadsheetProcessing` retains the original verifier-bound Spreadsheet
admission through built-in workbook construction, XLSX export/calculation and
file-save dispatch to XLS or delimited text. Public export, calculation, CSV and
save operations still admit fresh work. The new purpose has no private PDF
dependency permission and cannot continue as another product. Its internal entry
points are assembly-only; public APIs and caller-visible ClosedXML behavior do
not change.

Authoring/style callbacks remain independently guarded public mutations. A
custom cell value's `ToString()` can return data during an admitted export, but
any new native operation it invokes checks current rights. Actual callback-native,
source IO and filesystem failures retain their original errors. No atomic file
replacement or rollback guarantee is added; no file is written when processing
fails before the existing final write.

Existing formula, unsupported-function fallback, workbook layout, BIFF8, CSV/TSV
formatting and mutation behavior remain unchanged. XLSX/BIFF8 outputs are checked
semantically, not by byte equality, because package metadata can vary. Third-party
NPOI distribution approval remains separate and is not granted by successful tests.
See the [product dependency milestone](../checklists/PVAD.Native-Library-Licensing-And-Protection.md#product-dependency-and-spreadsheet-continuation-milestone)
for exact local evidence. That checkpoint predates the subsequent Migration and
manufacturer Admin/Account milestones described above. Remaining composite,
obfuscation, packaging and platform acceptance must be assessed separately.

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

Shared test sessions include `importer` and `converter` for the integrated entry-point milestones,
alongside `pdf`, `word`, and `spreadsheet`. This is a normally signed test grant,
not evidence that every Importer engine is protected. Product-only fixtures
remain separate. The integrated fixture now additionally carries the tested
`migration.pdf.code` and `migration.spreadsheet.code` provider lists only; Viewer,
Designer and Report grants are not enabled in advance.

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

## Signed Offline Revocation Lists

Native revocations are explicit local imports, not an online activation service.
Applications without a configured list keep their existing behavior. Import the
latest approved manufacturer artifact before activation and on every restart:

```csharp
PvadLicense.ImportRevocationsFromFile(configuration["PVAD:RevocationsPath"]!);
PvadLicense.LoadFromFile(configuration["PVAD:LicensePath"]!);
var revocations = PvadLicense.GetRevocationStatus();
```

`ImportRevocationsFromStream` reads from the current position, leaves the stream
open and enforces the same 64 KiB envelope / 32 KiB payload limits as licenses.
The separate version-one envelope adds `kind: "PVAD.NativeLicenseRevocations"`.
Its exact signed payload contains `schemaVersion`, the matching `purpose`, a
positive global `sequence`, UTC `issuedAt` / `nextUpdate`, and at most 512 unique,
non-empty `revokedLicenseIds`. It contains no customer names, document data or
private/public keys. ECDSA-P256/SHA-256/P1363 and the existing embedded manufacturer
key register are authoritative; changing the signer does not reset sequencing.

Each newer list must retain every known revoked ID and must not move its issue
date backward. Lower sequences, altered same-sequence payloads, future-dated
lists, malformed data and invalid signatures leave the accepted list untouched.
Re-signing the identical payload is an idempotent import. No public reset or
unrevoke operation exists. Correct an erroneous revocation by separately issuing
a replacement license ID, not by removing historical entries.

An active license in the list reports `Revoked` / `PVADLIC014`. New native
operations, license reloads and worker admissions fail; already admitted private
continuations may finish under their original snapshot. Loading a different
valid license never clears the revocation state. Updates and concurrent license
replacements are serialized so a successful reload cannot lose known revocations.

`nextUpdate` is a freshness warning, not a hidden mandatory Internet or renewal
requirement. An overdue list still blocks all known revoked IDs, permits otherwise
valid unrelated licenses, and reports `IsStale`. Operators must distribute new
signed lists explicitly. There is no background fetch, automatic remote kill,
trusted offline clock or guaranteed protection against an operator restoring an
old file after process restart. Protect and back up the latest approved artifact
and sequencing record. Larger lists, sharding, signer-key revocation and a durable
manufacturer revocation-management API remain separate follow-ups.

API hosts can set `NativeLicensing:RevocationListPath`; startup imports that list
before activating the license and fails on invalid configured input. C# PDF and
OCR workers receive only a snapshot of the accepted signed list, never a signing
key or source-visible bypass. OCR captures license and list atomically and removes
temporary handoff files. Customer Account downloads and manufacturer renewal
history do not themselves revoke an artifact or change a server's imported list.

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
Its `all-native-entry-points` status records the current protected entry boundaries
for all ten products, not completed package/platform acceptance. The Importer row
separately records pending cross-product, protected-package and worker/platform
acceptance. The public-surface and provider inventories prevent newly added
processing APIs from silently inheriting this status without review and tests.

| Product | Required integration boundaries |
| --- | --- |
| PDF | Native generation, byte/stream/file output, mapper rendering |
| Word | Facade, both direct Word export overloads, and DOCX signing service |
| Spreadsheet | Builder/retained-handle mutations, calculation, XLSX/XLS/CSV/TSV IO, data operations, and design conversion |
| Importer | All 201 classified public processing entries guarded, including built-in imports, PDF parse/save/edit/Trust, regeneration, image analysis and OCR; separate cross-product/package/platform acceptance remains open |
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

The separate manufacturer service now provides authenticated native-license
issuance, immutable audit records, Admin management and authorized Account
downloads, as described above. The offline tool does not provide these controls;
automated durable revocation-list management remains a separate follow-up.
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
| `PVADLIC014` | License ID revoked by an explicitly imported signed list |
| `PVADLIC015` | Future-dated, rolled-back, altered or non-cumulative revocation update |

## Protection And Packaging

The local joint Generator rehearsal now processes nine related first-party
assemblies together and builds independent NuGet consumers from the protected
output. It compares public API contracts, JSON/DOCX/XLSX semantics, license
failures and two-page PDF pixels with an unprotected baseline. See the
[rehearsal guide](../tools/native-licensing/README.md#joint-generator-package-rehearsal)
for the exact scope and prerequisites. Synthetic candidates are never published
and are removed after the run. Third-party dependencies are not obfuscated.
An additional [Importer/OCR rehearsal](../tools/native-licensing/README.md#joint-importer-and-ocr-worker-rehearsal)
extends the graph to 14 assemblies and tests 13 library packages plus the separate
OCR worker bundle. It covers the eight registry imports, PDF regeneration,
image analysis, worker license denials and actual OCR on macOS/x64. Other native
OCR platforms explicitly remain unexecuted. This does not replace complete
Importer/Trust tests or establish API, CodeWorker, Migration, visual/platform,
support-artifact or commercial distribution acceptance.

The [Converter/CodeWorker profile](../tools/native-licensing/README.md#joint-converter-and-codeworker-rehearsal)
extends the same local graph to 16 assemblies, 14 library packages and two
separate worker bundles. It compares all nine Converter formats and real
CodeWorker Model/Base64/PDF Builder/low-level PDF execution against original
binaries, including two-page PDF pixels and license/error contracts. ODT
comparison normalizes only its validated generated creation date; ZIP entries
are compared independently of ZIP timestamps. This scoped test does not bring
WebApi, migration providers or general OS sandbox acceptance into the graph.

The [Migration rehearsal](../tools/native-licensing/README.md#joint-migration-provider-rehearsal)
adds all 38 native Migration projects, producing a 54-assembly graph and 52
library packages while keeping both workers separate. Existing native provider
probes run against original assemblies and independently restored protected
packages. Representative provider fixtures, per-provider licensing, diagnostics,
aliases and normalized output checksums are compared without changing product
dependencies or original fixture contents. The HTTP-hosted PDF Designer
composition, full WebApi integration and distribution/platform gates remain
outside this native package milestone.

The [protected host rehearsal](../tools/native-licensing/README.md#joint-webapi-host-rehearsal)
adds WebApi and persistence to the joint graph so internal native references are
rewritten consistently. Host/persistence names are preserved for framework
activation; native private implementations remain eligible for renaming. The
synthetic HTTP probe compares real authorization, CSRF, entitlement, migration,
export and CodeWorker behavior against an original baseline. This isolated
test-server/EF-InMemory setup does not establish real browser-session,
PostgreSQL, production-sandbox or cross-platform acceptance.

Designer access errors explicitly use the Problem Details media type
`application/problem+json`. Authentication and authorization denials remain
distinct from native-library license failures; the host comparison checks both
without relaxing any permission, entitlement or native-license boundary.

Pinned Obfuscar and ILSpy tools protect/audit candidate output. Public APIs and
serialization metadata must survive, third-party binaries stay unchanged, and
debugging maps stay private. The [support-retention tooling](../tools/native-licensing/README.md#private-support-retention)
seals joint mappings and optional original portable symbols independently of
customer packages; synthetic restore tests cover checksums and tampering.
Production recipient keys, private storage and its access/retention policies
remain separate deployment requirements. Original symbols are not rewritten
post-obfuscation debugger symbols. Renaming internal code raises inspection effort;
it does not prevent decompilation or patching. Signatures authenticate licenses,
not customer-controlled runtime integrity.

No anti-debugger crashes, process scanning, self-modifying loader, embedded
private signing keys, or strong-name-as-security claim is permitted. Native AOT
and commercial anti-tamper tools are deferred.

See the [implementation checklist](../checklists/PVAD.Native-Library-Licensing-And-Protection.md)
for open work. Public package publishing and version bumps are separate actions.
