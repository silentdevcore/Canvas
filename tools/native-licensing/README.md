# Native License Preparation And Protection

This tooling is a local preparation foundation, not complete native product
enforcement or an authenticated licensing service. The production trust register
is intentionally empty until manufacturer keys are approved.

```bash
node tools/native-licensing/pvad-native-licensing.mjs check
node --test tools/native-licensing/pvad-native-licensing.test.mjs
dotnet test tests/PVAD.Licensing.Tests/PVAD.Licensing.Tests.csproj
node tools/native-licensing/pvad-native-licensing.mjs rehearse
node tools/native-licensing/with-test-license.mjs pdf
node tools/native-licensing/with-test-license.mjs word
node tools/native-licensing/with-test-license.mjs spreadsheet
node tools/native-licensing/with-test-license.mjs importer
node tools/native-licensing/with-test-license.mjs test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj
```

The rehearsal restores pinned free tools, creates an ephemeral synthetic key,
builds an isolated license library with its public key, obfuscates internal names,
compares the entire public API, inspects type visibility with ILSpy, packs the
protected DLL, and activates it through an independent local-feed consumer.

The consumer tests missing-license rejection, an unlicensed product, and invalid
replacement without discarding the valid license. It verifies the packaged DLL
hash and rejects sources, private symbols, and key material in package entries.

Temporary private keys, license files, and package caches are removed in a
`finally` block. Generated assemblies, maps, and synthetic packages remain under
ignored `artifacts/native-licensing/` for local inspection. **Do not publish
these packages or upload mapping files.** CI uploads only the report and test
results, not libraries, licenses, keys, or mappings. ILSpy update checks are
disabled during automated inspection.

The rehearsal requires .NET 10, Node.js 22+, and network access for initial tool
restoration. It does not call a licensing server or publish packages. The
verification library itself has no network dependency.

`report.json` distinguishes licensing-core evidence from product enforcement and
records the actual .NET runtime architecture separately from Node.js. A prepared CI workflow is not a successful
cross-platform run; those acceptance items remain open until executed.

See the [guide](../../docs/native-library-licensing.md) and
[checklist](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md).

## PDF Enforcement Tests

Native PDF creation and serialization now require a signed `pdf` grant. The
`with-test-license.mjs` runner creates temporary signed fixtures and a separate
synthetic-trust build. It never changes regular build directories or embeds a
test key into the source register. It removes all test licenses and candidate
binaries in `finally`; reports and TRX results remain under
`artifacts/native-engine-tests/`. No synthetic package is published.

Use `test <repository-test-project.csproj> [dotnet test options]` for an existing
suite. The runner owns restore/build, trust properties, and artifacts placement;
do not pass `--no-build`, `--no-restore`, or alternate build properties. Native
verification-core tests are excluded from test auto-activation so their
unlicensed-state assertions remain meaningful. The `pdf` probe starts unlicensed
and tests the public engine/facade without any module initializer.

## Word Enforcement Tests

The `word` probe starts unlicensed in its own process and checks direct
`WordDocumentExporter` overloads, the Generator facade, and DOCX signing. A
Word-only signed license must succeed without PDF/Converter rights. Missing,
expired, and wrong-product licenses must be rejected before input consumption;
reused exporters must recheck after replacement, and valid renewal must restore
operation without restarting. The probe also checks actual DOCX content and a
synthetically signed OPC package, not Microsoft Office signature acceptance.

General test sessions and `test` runs now activate a signed fixture containing
only `pdf`, `word`, `spreadsheet`, and the partial `importer` entry-point grant. Separate
product-only fixtures remain for negative tests. No other product grants are preemptively
enabled. The key stays in memory, source trust stays empty, and the runner
removes synthetic fixtures and binaries in `finally`. The dedicated native CI
workflow includes both probes and Word/API regressions; its actual platform
runs remain a separate acceptance gate.

The local Word milestone passed 34 probe checks, 299 Word/Generator/core/API
regression cases, the existing 20 PDF admission checks, and 45 tooling contracts.
The [Word checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-word-generation-and-signing-milestone)
lists final reports and verified cleanup. These local runs do not replace full
solution, coverage, platform, or Office signature acceptance.

## Spreadsheet Enforcement Tests

The `spreadsheet` probe begins unlicensed, activates a Spreadsheet-only signed
license, and later replaces it with a PDF-only license before renewing. It
checks the direct XLSX/XLS/CSV/TSV, calculator, operations, data-binding, design
conversion, and fluent-builder boundaries. File-content, absent-file,
import-stream, callback, and model assertions verify non-destructive rejection,
including empty CSV/TSV workbooks and retained cell/style/column/range handles.

A reflection inventory compares public Spreadsheet processing types, methods,
constructors, and properties against explicit protected/free classifications.
Metadata/validation types are explicitly classified as free. New processing
overloads must be assigned and exercised rather than silently excluded. DTOs,
metadata, and structural validation remain free; actual workbook processing does
not. The fixture grants no unrelated native products.

API regressions run real Spreadsheet routes against signed replacement licenses
in a non-parallel test collection. The original integrated license is restored
in `finally`. These tests retain the existing application authorization setup;
they do not introduce a native test bypass or change production trust.

The local Spreadsheet milestone passed 145 probe checks across 40 protected
entries, 370 selected .NET cases, and 45 tooling contracts, plus all 20 PDF-only
and 34 Word-only checks. Reports and verified cleanup are linked from the
[Spreadsheet checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-spreadsheet-enforcement-milestone).
Earlier full API and coverage runs remain separate historical checkpoints.

## Importer Entry-Point Tests

The `importer` probe starts unlicensed, exercises the eight registered file
importers and their static/optional overloads, then checks PDF facade/direct
load and high-level rewrite/incremental save entry points. Valid operations run
with an Importer-only signed fixture. Wrong-product replacements must reject
reused readers/writers without reading or closing input, enumerating updates,
changing source bytes, or invoking the fake Markdown asset resolver.

All assets are synthetic and local; the resolver records calls without fetching
network data. The DOC fixture exercises the parser's bounded text-fallback path,
not general Word interoperability. Password/certificate overload admission uses
a plain PDF fixture; encrypted-input compatibility is covered separately by
the existing full Importer suite. No real keys, customer files, or external OCR
service are used.

Probe output explicitly records `completeImporterEnforcement: false`. The
operation matrix keeps the product's `enforced` flag false until
generic bundle dependency-rights/lifetime are implemented and tested. Image
analysis and OCR-worker admission are covered by the later milestone below.
CI now prepares this probe and complete
file/PDF importer regressions; the outer job budget is 30 minutes for the added
workload, not evidence that a cross-platform run has succeeded.

The local macOS/x64 checkpoint passed 111 distinct probe checks across 31
entries, 860 .NET tests (23 FileImporter, 661 Importer, 80 licensing-core, and
96 targeted API cases), and 46 tooling/version/release-author contracts.
Result files, the initial corrected test-double failure, cleanup evidence, and
remaining work are recorded in the
[Importer checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-entry-point-milestone).

`DirectEngineChecks` originally inventoried 41 and now exercises all 50 public
processing methods on the selected tokenizer/parser/decoder/document/graphics/
font-parser/Analysis and color/retained-font types. Missing and wrong-product licenses must fail before
input enumeration, font resolution, decoder dispatch, token-position changes,
or derived geometry/classification mutation. Valid and renewed Importer-only
licenses verify decoded bytes and parsed PDF text; default/retained tokenizer
calls are checked separately. Constructors, DTO reads, object-graph lookups,
decoder capability inspection, and general geometry remain free. The inventory
does not claim to cover every Importer type or third-party custom decoder.

`ColorFontChecks` registers the nine additional public processing methods for
color conversion, PDF functions, color-content rewriting, and font/glyph
decoding. It checks lazy component access and resolver/encoding/evaluation
callbacks, then retains real parsed fonts and all five built-in function forms
across wrong-product replacement and renewal. Nested sampled, PostScript, and
ICC decoding must propagate a real license denial. Mapping construction,
glyph-width lookup, and capability/model inspection remain free.

The ICC case uses the existing synthetic profile factory and verifies a real
LittleCMS transform when available. Set `PVAD_REQUIRE_LCMS2=1` to require it;
without that setting, an unavailable native library must fail closed and the
probe reports that limitation explicitly. This is not a new ICC algorithm or
an independent conformance validation.

The color/font checkpoint passed 357 distinct probe checks with
`PVAD_REQUIRE_LCMS2=1`, all 860 selected .NET cases, and 46 tooling/version
contracts. The real ICC transform, wrong-product rejection, and subsequent
renewal were verified; no unavailable-library fallback was used. See the
[color/font milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-color-and-font-milestone)
for retained evidence, cleanup, and limits.

`EditingChecks` inventories the Editing namespace and every public processing
signature on its four engines plus the content-stream rewriter. It exercises
missing/wrong/valid/renewed licenses, retained models/status, ref outputs, stream
ownership, input-list callbacks, existing and absent files, selected page and
measurement units, and invalid replacement loads. It verifies Importer-only
blank/clone/split/extract operations without unlocking general PDF generation.
Status inspection, cleanup, pure DTO mutation, and page-range validation stay
free. The internal blank-page adapter uses the shared writer but exposes no
caller-supplied document or public processing-mode switch.

The final editing checkpoint passed 528 Importer probe checks, 20 PDF-only
admission checks, and 1,096 regression cases plus two extra 70-case PDF process
runs. See the
[editing milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-editing-milestone)
for raw evidence, the retained initially unreproduced RTL failure, cleanup, and
the then-remaining regeneration/OCR boundaries; the subsequent Trust milestone is
described below. Its later font-lifetime follow-up demonstrates a deterministic
reproduction and fix; passing retries alone were not treated as a fix.

The enhanced direct-engine checkpoint passed 290 distinct probe checks and
re-ran all 860 .NET cases and 46 tooling/version contracts successfully. See the
[direct-engine milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-direct-engine-milestone)
for raw evidence and explicit remaining work. These are local macOS/x64 .NET
results, not cross-platform or full native-product acceptance.

### Trust And Security Probe

`TrustSecurityChecks` inventories 31 processing methods/constructors across
Trust, archival conversion, CRL/OCSP, password/certificate decryption, AES-256,
PKCS#12 signing keys, PAdES, RFC 3161, redaction, audit verification, invoice
attachment, and attachment extraction. All are exercised after valid admission,
wrong-product replacement, and Importer-only renewal. Four retained AES/key
methods are created only after valid admission; missing-license coverage checks
their factories instead of constructing private unlicensed objects.

The probe verifies that denied calls do not consume/close streams, change an
object graph, invoke a signing key, or dispatch HTTP. BCL-generated CA, signer,
CRL, OCSP response, and an in-memory RFC 3161 HTTP handler provide real
cryptographic operations without contacting a server. Archival/certificate-
encrypted inputs are prepared in a separate PDF-only fixture phase before
restoring Importer-only admission. Private fixtures are disposed and PFX bytes
are cleared; no certificate or key is included in the JSON evidence.

The Trust inspector is also tested when a stream callback replaces the license
mid-read. Its native failure must propagate, not become an invalid-PDF report.
DTOs, metadata/capability checks, certificate URI locators, and disposal remain
free. At that checkpoint general regeneration, image analysis, and OCR remained
open; the following milestones cover those boundaries. Composite operation
lifetime remains unfinished. See the
[Trust milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-trust-and-security-milestone)
for actual result counts and platform limitations.

The current local probe passed 655 distinct checks. The associated validation
selected 1,364 unique .NET cases, including 389 API cases. One already recorded
intermittent RTL-width failure occurred in the initial 70-case PDF run; two
fresh-process full repeats passed. The machine-readable audit retains
`unqualifiedRegressionAcceptance: false` rather than hiding that failure.
Source shaping code, assets, assertions, and thresholds were not changed.

The subsequent font-lifetime follow-up retains that initial failed audit but
reproduces the ownership defect under compacting GC, corrects the native buffer
lifetime, and passes a fresh 1,370-case selection. The final 76-case PDF suite
adds six regressions, including the exact mixed Arabic/currency width. All
655 Importer and 20 PDF admission checks still pass; 118 local visual cases
pass unchanged enforcement rules with five measured P2 findings. Historical
approval is not extended to changed browser pixels. See the
[follow-up evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#font-shaping-buffer-lifetime-follow-up)
for actual reports, cleanup, and separate platform/package gates.

### Regeneration And Debug Probe

`RegenerationChecks` covers seven processing signatures: both regeneration
overloads, all three map methods, pipeline save, and debug-overlay building.
Standard regeneration works with an Importer-only signed fixture. The advanced
non-null generator callback requires Importer and PDF grants explicitly; its
successful test uses the combined synthetic fixture. No per-user callback or
output stream inherits the private mapper's internal rendering rights.

The probe checks missing/wrong-product/renewed admission, unchanged output
bytes and stream position on denial, custom-pipeline dispatch, cancellation,
throwing callbacks, callback license replacement, retained generator handles,
and reentrant attempts to use the ordinary generator. A multi-page case checks
text, paths, a supported Flate image, shading, and rotated page geometry with
Importer-only rights. These tests do not certify new image filter support.

Ordinary public constructor/renderer/writer guards remain in force. Internal
regeneration maps into a fresh, private document and returns only bytes; it
does not activate an ambient all-products scope. Debug-overlay DTOs remain free.
Image-analysis debug PNGs and OCR belong to the separate image/OCR milestone,
not this overlay builder. See the
[regeneration milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-importer-regeneration-and-debug-milestone)
for current results and remaining scope.

## Image Analysis And OCR Probe

```bash
node tools/native-licensing/with-test-license.mjs image
```

`ImageEngineProbe` independently checks 43 public processing signatures. These
cover the image importer overloads, public preprocessing/color/shape/text/glyph
and atlas processing, scene assembly, PNG overlays, the converter, embedded and
isolated OCR engines, and the default process runner. DTOs and generic numeric
color-distance/correlation helpers remain free. Missing, wrong-product, failed
renewal, and post-cache/native-use revocation cases must fail before input reads,
temporary writes, native work, or custom engine/runner dispatch.

The positive path uses an Importer-only grant for analysis, debug PNG output,
and OCR orchestration. The real worker separately rejects missing, expired,
wrong-product, malformed, and tampered licenses before opening a request. The
real runner must override a deliberately wrong inherited license path with the
signed in-memory Importer snapshot, propagate stable worker diagnostic codes,
and remove the temporary envelope on both failed process start and worker error.
Only content-free check names and booleans are retained in `image-admission.json`.

The probe requires an actual blank-image Tesseract worker run on macOS with
x64 .NET, where the app-owned native bundle is available. Setting
`PVAD_REQUIRE_NATIVE_OCR=1` makes absence of that real native execution a failure.
Other runtimes explicitly report `realNativeSucceeded: false` rather than
claiming native compatibility. Their native bundles/containers and OCR accuracy
need separate validation; fake engines and a language-data rejection are not
native-success evidence. Existing OCR tests that return early when native files
are unavailable must not be counted as proof of native execution.

The API tests cover Importer-only analysis/debug output and built-in OCR PDF
output, plus HTTP 503 diagnostics for new operations. The continuation follow-up
lets already-admitted OCR output finish after a custom engine replaces the
license; the next public call is rejected before reading input or invoking that
engine. The original rejection test is retained in the earlier checkpoint, not
claimed as the new completion behavior. Ordinary PDF construction and public
mapping keep their PDF requirement. The matrix remains partial because generic
bundle rights and admitted-operation lifetime have not been closed. See the
[image/OCR milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-image-analysis-and-ocr-milestone).

### Operation Continuation Follow-Up

The image probe also replaces the license from inside a source-stream read.
Internal analysis, glyph/template, scene, and overlay work must finish, while a
reentrant public analysis call must fail. A custom OCR callback similarly
replaces the license: its admitted conversion finishes, but a new conversion
must fail without reading input or invoking the engine. The context must never
grant public PDF rights to an Importer-only caller.

Core tests use a deterministic clock to expire a Trial inside a live context.
They verify fresh public/worker admission, original internal continuation,
purpose-specific dependencies, nested restoration, 64 parallel contexts, and
rejection from captured/child contexts after completion. They also verify that
an independent test authority cannot authorize real-library continuations.
These are internal-only
continuations; public `Require` always uses the current process-wide verifier.

Starting a new OCR worker remains a fresh admission. The probe changes the
license before a built-in worker dispatch, expects rejection, and verifies
staged-image cleanup. No host timestamp/flag is accepted as a worker exemption.
Other composite product call graphs remain explicitly open. See the
[continuation milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-operation-continuation-milestone).

### Typed PDF Load/Save Probe

The Importer probe adds 127 distinct `pdf-continuation:` checks. It changes the
active license during seekable/non-seekable input reads, update enumeration,
custom decoding and font callbacks. Typed loads cover plain, Flate, AES-256,
AES-128, RC4 and certificate-protected sources, plus the typed facade. Full and
incremental saves preserve their validated output; encrypted rewrite preserves
the requested replacement encryption and updated content. Next/reentrant public
calls fail before input reads or custom work when the replacement grant lacks
Importer. Invalid updates, signed-source rewrite protection, cancellation and
read exceptions remain checked; certificate fixtures never make network calls.

Built-in continuations are internal only. External decoder/font/encoding
overrides remain dispatched, and their own protected public PVAD calls still
need a fresh grant. Five buffer cases additionally check retained stream bytes,
seekable/non-seekable ownership, repeated disposal, cancellation and read errors.
Designer `PdfFileImporter` DTO/analysis mapping, Editing, Trust, advanced
regeneration and other product composite lifetimes were separate work at that
checkpoint; the subsequent Designer mapping milestone is described below.
See the [typed PDF evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#typed-pdf-load-and-save-continuation-milestone).

The final local checkpoint passed 833 Importer, 20 ordinary PDF and 239 strict
native-image/OCR probe checks, plus all 1,680 selected .NET tests and 48 tooling
contracts. Final image evidence requires `PVAD_REQUIRE_NATIVE_OCR=1`; the separate
238-check preliminary run did execute native OCR but did not require it. The
checklist preserves exact paths, independent counter/cleanup audits and the
remaining product/package/platform boundaries. These are local macOS results
with x64 .NET, not a new full API/PostgreSQL or cross-platform acceptance run.

### Designer PDF Mapping Probe

The final Importer probe adds 405 `designer-pdf-continuation:` checks
(1,238 total checks). It exercises five public registry/instance/static paths,
six synthetic PDF fixtures and seekable/non-seekable input. During each read it
replaces Importer rights with PDF-only rights, verifies rejection of a reentrant
public analysis call, then compares the completed DTO with its unchanged-license
baseline. Only the random root ID and generated chart IDs are excluded from
comparison; geometry, content, styles, assets, layouts and diagnostics are not.

Fixtures cover text, mixed portrait/landscape pages, shared headers, image data,
safe/review chart recognition, valid chart metadata and invalid-metadata warnings.
Further enumeration callbacks verify nine nested/recursive analysis paths,
followed by fresh public denial before enumeration. Read failure, cancellation,
input ownership and lack of public PDF-generation rights remain tested. Existing
direct-engine coverage still checks all public methods against missing, wrong
and renewed licenses. The new HTTP regression checks mixed-layout PDF imports
and license changes between separate API requests.

Two additional mixed-layout cases force asynchronous reads with `Task.Yield`
on seekable and non-seekable input, compare the completed DTO and reject the
next public call before reading. These add ten checks to the earlier 1,228-check
checkpoint; its original evidence remains separate.

Editing, Trust, advanced regeneration, XObject/color/debug-overlay composites
outside this mapping path, other products and protected packages/platforms remain
open. See the [Designer mapping evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#designer-pdf-import-and-analysis-continuation-milestone).

Final validation passed all 1,681 selected .NET tests and 49 tooling contracts.
Six suites completed before an interrupted OCR dependency build; OCR/API then
passed in a clean session with server reuse/shared compilation disabled. The
combined audit counts only actual passed TRX rows and verifies cleanup in both
sessions. The checklist preserves the interrupted build, corrected test-fixture
failures, source-body comparison and exact final evidence separately. No full
API/PostgreSQL or new cross-platform/package/visual acceptance is implied.

## Shared Workflow Sessions

```bash
node tools/native-licensing/test-session.mjs exec -- dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj
node tools/native-licensing/session-rehearsal.mjs
node tools/dependencies/api-full-validation.mjs
node tools/dependencies/coverage-smoke.mjs artifacts/coverage-native-licensing-new-run
node tools/visual-parity/run.mjs
```

`test-session.mjs` creates one temporary signed license/trust set and one build
tree under ignored `artifacts/native-test-sessions/`. Nested tools reuse the
validated session. Native license/build settings are replaced by the owned
configuration, not a customer license or runtime trust override. The generic
command wrapper otherwise retains the caller's environment: run it only in a
dedicated development/CI environment without customer credentials. Full API and
browser/operator launchers retain their narrower credential-stripping allowlists.
The key
used to sign synthetic fixtures exists in memory only. The fixture files,
descriptor, and isolated binaries are removed on normal completion or failure;
only test reports and cleanup evidence remain. The command wrapper forwards
interrupts to its owned child process before cleanup. Forced process/runner loss
still requires the CI cleanup step or disposal of the ephemeral runner.

MSBuild stamps synthetic assemblies `PVAD.NativeTestArtifact=not-for-publication`.
It rejects Pack/Publish and compilation outside the isolated build root.
Manufacturer release builds and container/package jobs do not opt into this
session. The existing protected-package rehearsal remains a separate explicit
candidate-only exercise, not a publication workflow.

GitHub test jobs call `prepare-ci` after Node setup and before restore, then
`cleanup-ci` with `if: always()`. Preparation writes only to the runner-owned
environment file; application credentials are not propagated. Do not upload the
session build tree or descriptor. API full validation retains its original
unfiltered discovery/TRX audit, serial settings, and per-test hang detection.

| Launcher | Integration |
| --- | --- |
| Main .NET CI, platform parity, visual parity, scheduled fuzzing | Explicit prepare/always-cleanup steps |
| API full validation | Shared session, fresh isolated restore, actual MSBuild target lookup, PostgreSQL isolation |
| Visual parity, platform matrix, bold-text/flow-table reviews, Trust operations | Nested shared sessions; original thresholds and approved assets unchanged |
| Coverage smoke | Actual MSBuild assets/dependency paths instead of fixed bin/obj paths |
| Operator/HTTPS/monitoring rehearsal | Narrow owned-session propagation, actual API/helper paths, build-only relay harness |
| Recovery, Legal smoke, PAdES/external corpus, parser shell tools | Re-enter through the command wrapper; test tool bootstrap verifies signed licenses |

The session rehearsal checks the real API/worker tests and negative Pack,
Publish, and output-redirection behavior. Prepared workflows and launcher
contracts are not proof of complete CI, full API, cross-platform, or fuzz
campaign success; record those runs separately in the implementation checklist.

The complete local API/PostgreSQL audit was executed on 2026-11-15 and passed
all 1,467 concrete cases without failures, skips, or relaxed time limits.
Discovery/TRX matching and the assembly checksum passed. Temporary files and
synthetic build output were removed. After the automated Docker post-check was
blocked by an approval-service usage limit, the user ran
`docker ps -aq --filter label=org.testcontainers=true` locally and reported an
empty result. This closes the manually confirmed labeled-container post-check,
not an agent-executed inspection, an audit of unlabeled resources, or platform
acceptance.
See the [checkpoint and evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#complete-api-and-postgresql-acceptance).

## Coverage Instrumentation Acceptance

Use a new output directory for each coverage smoke run; existing evidence is
never overwritten. The command creates a signed session, builds the solution in
Release configuration inside that session, and executes the existing selections
in all 52 collector projects. Core licensing tests and the relay tests do not
use the collector and are not part of this 52-host inventory.

Each host must execute nonzero tests with no failures or skips, retain a single
fresh TRX/Cobertura/JSON report set, and show hits in its expected production
module. The XML and JSON module sets must agree; test/tool assemblies and
generated `obj` sources remain excluded by the existing settings. These are
instrumentation smoke checks, not a full solution functional run or a minimum
overall code-coverage claim.

After the owned session removes its synthetic files and binaries, audit the
retained reports without rebuilding or changing their contents:

```bash
node tools/dependencies/coverage-smoke.mjs --audit artifacts/coverage-native-licensing-new-run
```

Build-dependent tooling contracts must run while the session's Release output
still exists. A report audit after cleanup cannot replace those dependency and
collector-resolution checks. Record actual runs and remaining platform gates in
the [implementation checklist](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md).

The 2026-11-15 local run completed all 52 hosts and 947 passing cases with no
failures or skips. All six tooling contracts passed against the active session's
Release artifacts. The independent raw-report audit reproduced the summary
byte-for-byte, and the session cleanup removed signed fixtures and binaries.
Reports remain under `artifacts/coverage-native-licensing-20261115/`; the
[coverage checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#local-coverage-instrumentation-acceptance)
records the scope and cleanup evidence. Linux/Windows and full CI acceptance are
still separate gates.
