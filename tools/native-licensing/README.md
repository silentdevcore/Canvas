# Native License Preparation And Protection

This tooling provides local preparation and isolated native-license tests, not
a public licensing service. All current native entry points are guarded; broader
package/platform acceptance remains open. A separate default-disabled
manufacturer API now handles authenticated native issuance and renewal.
The production trust register is intentionally empty until manufacturer keys
are approved.

```bash
node tools/native-licensing/pvad-native-licensing.mjs check
node --test tools/native-licensing/pvad-native-licensing.test.mjs
dotnet test tests/PVAD.Licensing.Tests/PVAD.Licensing.Tests.csproj
node tools/native-licensing/pvad-native-licensing.mjs rehearse
node tools/native-licensing/with-test-license.mjs pdf
node tools/native-licensing/with-test-license.mjs word
node tools/native-licensing/with-test-license.mjs spreadsheet
node tools/native-licensing/with-test-license.mjs converter
node tools/native-licensing/with-test-license.mjs migration-code
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

## Joint WebApi Host Rehearsal

```bash
node --test tools/native-licensing/webapi-protection-rehearsal.test.mjs
node tools/native-licensing/webapi-protection-rehearsal.mjs
```

This explicit 56-assembly profile adds the real WebApi and persistence assembly
to all 54 native modules. It builds an isolated synthetic baseline and rewrites
the entire first-party graph together, including both separate workers. It is
not safe to drop protected native binaries into an unprocessed WebApi: internal
friend references must be rewritten in the same protection run.

Only WebApi and persistence type/member names are deliberately retained for
framework activation, reflection and EF metadata. Their references to renamed
native implementations are still rewritten. Native modules keep the existing
public-API and generated-code preservation rules; private implementation-name
removal is checked using ILSpy. This is native-library protection, not a claim
that the host's own implementations are concealed. Public surfaces are compared
for all 56 modules; vendor files remain unchanged. No package is published.

The executable probe starts the actual application through an in-process ASP.NET
test server. Only the authentication transport is synthetic; authorization,
Designer permissions, CSRF, tenant context, entitlements, feature policies,
audit/usage writes and the native bootstrap use product implementations. EF
InMemory replaces PostgreSQL, and hosted services (including general startup
option validation and background maintenance) are removed except the real
native bootstrap. Production startup validation remains a separate gate.
Seeded data, keys and documents are synthetic.
No production switch enables this test authentication in the real application.

The synthetic `Hardened=true` option permits the host's configured CodeWorker
client to be tested; it is not evidence of an OS sandbox. The report keeps
worker-sandbox deployment acceptance false. Unlicensed startup is tested in a
separate fresh process so a previously loaded native license cannot mask it.

The same probe runs against original and jointly processed binaries. It checks
authorized export, missing authentication/permissions/CSRF, wrong tenant,
disabled product/feature access, incorrect/revoked native rights and rejected
expired replacements without losing the previous license,
Nutrient's PDF-to-Designer friend boundary with mixed page layouts, actual
CodeWorker execution, forwarded revocations, denied source and audit/usage
tenant boundaries. Export and worker PDFs are raster-compared at 72 DPI.
The full mapping also goes through encrypted support recovery.

Only report/cleanup JSON remains after removal of licenses, archives, binaries,
data-protection material and private build work. The prepared CI job uploads
only those two reports. Actual browser/cookie/session flows, PostgreSQL
transactions and migrations, native OCR execution through this host, complete
API/visual/fuzz suites, OS sandbox deployment and cross-platform execution remain
separate acceptance gates. Existing 52-package acceptance is separate: this
profile validates a disposable host runtime, not new customer NuGet packages.

The real HTTP comparison exposed a Designer access-error media-type mismatch.
The middleware now explicitly writes `application/problem+json`, retaining
status codes, diagnostic IDs and authorization behavior. A focused API test
covers both anonymous and stale-permission early exits without invoking native
processing or downstream dependencies.

Viewer and Designer Nutrient routes now evaluate the same organization feature
policy before conversion. The HTTP comparison uses the real database-backed
feature decision for both paths, and focused route regressions cover dispatch
to that gate. The separate public release sentence is human-approved.

## Private Support Retention

`support-artifacts.mjs` prepares encrypted archives of explicit joint Obfuscar
mappings and original portable PDBs. Build identity, tool version and exact
original/protected DLL hashes bind the files to their build. Recovery requires
both the separate recipient private key and an independently recorded archive
checksum; encryption alone does not authenticate the producer. These files
never belong in customer packages, public symbols or ordinary CI uploads.

```bash
node --test tools/native-licensing/support-artifacts.test.mjs
node tools/native-licensing/support-retention-rehearsal.mjs
```

The second command builds a real isolated DLL and portable PDB, protects it,
seals its mapping/symbols with a disposable synthetic recipient, removes the
plaintext staging copy, and verifies exact recovery, tamper rejection, expiry
and cleanup. Joint-package profiles now exercise their complete mapping too;
they intentionally produce no private PDBs. Only non-secret report/cleanup JSON
is eligible for CI upload. No production key, storage backend, automatic
retention deletion or rewritten post-obfuscation symbols are supplied.

The [operator guide](../../operator-docs/PVAD.Native-Support-Artifacts.md)
describes the request contract, explicit seal/restore commands, separate key
custody, Windows ACL requirements, retention and remaining deployment gates.

## Manufacturer Issuance Tests

Use an available Docker daemon and opt into the real PostgreSQL container tests:

```bash
PVAD_RUN_POSTGRES_TESTS=1 node tools/native-licensing/with-test-license.mjs native-issuer
```

With Rancher Desktop on macOS, set `DOCKER_HOST` to its actual user socket and
`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` when required by the
local Docker configuration. Do not point the runner at a live application
database. The test harness owns and disposes its PostgreSQL containers.

Only this explicit mode writes an ephemeral owner-only private key matching its
isolated embedded trust. Ordinary test fixtures and workers do not receive that
key. The runner tests the real production signer, native manufacturer
authorization, issuance/renewal, atomic audit rollback, append-only SQL triggers,
concurrent successor rejection, CSRF, and existing server-license separation.
Database-test opt-in is mandatory; skipped PostgreSQL tests are not acceptance.
Native Admin and customer Account workflows are covered by the same selected
API suite; customer reads remain independent of manufacturer signing keys.

Temporary signed licenses, the private key, and synthetic-trust binaries are
removed in `finally`. Only synthetic TRX and cleanup/run metadata are uploaded
by the prepared Ubuntu CI job; never upload the private directory or isolated
build. Local execution is not evidence that the remote CI job has run.

See the protected [manufacturer runbook](../../operator-docs/PVAD.Native-Library-License-Issuance.md)
for deployment boundaries, configuration and safe operator procedures.

With the Admin and Account dev servers on their usual localhost ports, run:

```bash
node tools/native-licensing/license-ui-smoke.mjs
```

This Playwright rehearsal intercepts all API requests with synthetic data. It
checks issuance, renewal, exact downloads, organization switching and visible
control geometry at 1440px and 390px. It does not establish real authentication
or cryptographic acceptance; the PostgreSQL/HTTP suite does that separately.
Reports and screenshots are written to ignored `artifacts/native-license-ui/`.
Use `--capture-docs` only to refresh the protected Admin screenshots with the
same non-usable synthetic artifacts. No private key or production license enters
the browser fixture. Restore, build and VSTest run as separate phases in the
explicit manufacturer runner so a nested test build cannot silently stall.

## PDF Enforcement Tests

### Joint Generator Package Rehearsal

```bash
node tools/native-licensing/joint-package-rehearsal.mjs
```

Requires .NET 10, the pinned Obfuscar/ILSpy tools, the repository's Node parser
dependencies and `pdftoppm` (Poppler). The reviewed profile contains exactly nine
assemblies: Licensing, Core, Domain, Application, Pdf, Infrastructure.Pdf,
Infrastructure.Word, Infrastructure.Spreadsheet and Generator. A changed project
closure fails until the profile is reviewed. All nine are processed in one
Obfuscar invocation; third-party DLLs are resolution inputs, never rename targets.

The original project-reference baseline and independent package-only consumer
use different isolated output directories. The latter restores all nine packages
from a private local feed with source mapping and a separate package cache.
Only third-party packages already resolved by the original build are copied into
the second local feed. Consumer restore has no remote package source and disables
NuGet's network audit only for that offline restore; existing dependency-security
gates are not replaced by this rehearsal.

The opt-in `protected-package.targets` selects the exact protected DLL for every
package and excludes sources and private symbols. Restore/build and pack must
use the same opt-in properties. The Spreadsheet build-task pin is preserved as
a downstream package constraint, with build/analyzer assets still private; the
normal project-only build retains its existing private dependency configuration.
No NPOI supplier terms are accepted or suppressed.

The consumer checks missing/wrong/revoked rights, cross-assembly internal calls,
public reflection and friend metadata, JSON DTO serialization, DOCX content,
XLSX formulas/styles, and exact package/loaded-assembly hashes. API comparison
includes public signatures, parameter names/defaults, literal values and JSON
member attributes. ILSpy checks two known internal implementation names. PDF
pages are rasterized at 72 DPI and must match the original pixels exactly on the
same machine. Only the two newly generated XLSX workbook/sheet workspace IDs are
normalized after their GUID shape is checked; no cell/style data is discarded.

`artifacts/native-joint-packages/<run>/report.json` and `cleanup.json` retain only
non-secret evidence. Candidate packages, binaries, mapping files, local caches
and synthetic licenses are removed even on failure; source trust and regular
product build outputs must be unchanged. This does not yet establish Importer,
Migration, Worker, full rendering/DI compatibility, support-map retention,
commercial/NPOI distribution approval or cross-platform acceptance. The prepared
Linux CI job is not evidence until an actual remote run completes.

Do not replace DLLs in an existing API/worker deployment with this protected
subset. Obfuscation can rename friend-visible internal members: every consuming
first-party assembly must join the protection graph or have an explicitly
reviewed preserved boundary. This milestone proves only the package consumer
within the nine-assembly profile, not binary interchangeability with the rest
of the platform.

### Joint Importer And OCR Worker Rehearsal

```bash
node tools/native-licensing/joint-package-rehearsal.mjs --importer
```

This reviewed profile adds Importer, FileImporter, ImageAnalysis, ImageOcr and
the OCR executable to the Generator graph: 14 assemblies are protected together,
13 libraries are packaged, and the worker remains a separate runtime bundle,
not a NuGet library. Its four first-party DLLs are replaced from the same joint
output, preserving the original runtime configuration, native dependencies and
language data. Other providers, Converter, CodeWorker and WebApi are not included.

The independent package consumer exercises all eight registry formats, missing
and incorrect rights before stream reads, typed PDF load/rewrite/regeneration,
the retained reflection contracts and the anonymous PDF page-box JSON contract.
It compares page layouts and element type/content/geometry projections, not every
imported design field or provider fidelity. Generated workspace identities are
not treated as source document identities. Regenerated PDF pixels must match.

Image analysis runs with the real Skia implementation. On macOS/x64, the original
and protected OCR workers must recognize synthetic English text using the
existing app-owned native bundle, English language data and a bundled local
font; recognized words, bounds and confidence compare exactly. Other platforms
explicitly report native OCR as not executed until suitable standalone bundles
are validated. Missing, wrong-product and revoked licenses are tested against
the real worker before input reading on every platform. Typed worker diagnostics
also retain `PVADLIC014` and `PVADLIC015` through the parent engine.

The regeneration bridge resolves four internal PDF types and one page field by
name. Their narrowly scoped attributes preserve only this required metadata;
the record type annotations deliberately leave inherited/generated methods
eligible for consistent renaming. No module-wide exclusion or license bypass is
introduced. The existing writer/authority ILSpy checks still apply.

Obfuscar 2.2.50 can create short internal namespaces such as `A`. Global
single-letter source aliases with that same name can conflict in external C#
compilations. The shared synthetic fixture therefore scopes its aliases inside
`PVAD.NativeLicenseTestFixtures`, without changing fixture bytes. This known
source-compilation limitation is not hidden by the public-signature comparison;
general consumer-source compatibility remains a release acceptance gate.

`SkipGenerated=true` preserves compiler-generated type/generic-parameter identity.
A matching `CompilerGeneratedAttribute` module rule also preserves generated
methods, fields, properties and events, including generic collection helpers and
anonymous serialization shapes. Ordinary implementation types and methods remain
eligible for renaming. The runtime package consumer, not configuration inspection
alone, is the acceptance check for these rules.
The matching compiler-name rule also covers nested collection enumerators that
do not carry their own generated attribute. C# metadata generic names use
backticks, so the angle-bracket rule targets compiler-created names rather than
excluding ordinary generic product classes.

Reports retain named assertion lists and worker bundle hashes. All candidate
material is removed, including the OCR bundle and language-data copies. This is
not full Importer/Trust/fuzz/platform acceptance or approval to redistribute
native dependencies. Neither this bundle nor the protected libraries may be
mixed into an unprocessed API/CodeWorker deployment without reviewing all
remaining friend/reference boundaries.

### Joint Converter And CodeWorker Rehearsal

```bash
node tools/native-licensing/joint-package-rehearsal.mjs --execution
```

This profile extends the Importer graph with `PVAD.Infrastructure.Converters`
and `PVAD.CodeWorker`: 16 first-party modules, 14 library packages, and separate
OCR/CodeWorker bundles with four/ten matching protected first-party DLLs. Worker
executables are never packaged as libraries. The nine- and fourteen-module
profiles remain available independently. Foreign resolution DLLs remain intact.

The independent package consumer checks all nine Converter formats through 13
direct overloads and 18 interface calls. Missing/wrong rights are rejected;
Converter-only rights suffice without separately granting PDF generation.
Single/multiple-page exports compare exact text or extracted ZIP-part hashes,
including encoded image bytes. ZIP container timestamps are excluded by comparing
parts. Only ODT `meta.xml`'s generated `meta:creation-date` value is validated and
replaced in the comparison projection; other XML fields, package paths and
content stay covered. No production ODT behavior or fixture geometry is changed.

Original and protected CodeWorker processes execute synthetic C# Model, Base64,
semantic PDF Builder and legacy low-level PDF requests. Canonical models,
fidelity, diagnostics and source maps compare without normalization. The legacy
PDF is also rasterized and compared at 72 DPI across two differently sized pages.
The semantic builder returns a canonical design for trusted host rendering;
that host renderer is not brought into this dependency graph. Model/Base64 DTO
operations stay license-free. PDF requests require current PDF rights and test
missing, wrong-product, expired and revoked configurations, without returning
design/PDF content on failure. Existing forbidden-file references, compiler
errors, invalid Base64 and duplicate IDs retain stable diagnostics.
Every unsuccessful worker response clears canonical design, PDF bytes and
source maps and reports unsupported fidelity, including errors discovered only
after a model has been evaluated. Diagnostics remain available.

The harness bounds each owned worker process and removes request/response files;
it runs trusted synthetic code only and does not prove general C# sandbox
security, OS/container isolation or live WebApi/tenant behavior. The separate
Linux CI job is prepared, not evidence of a remote/platform run. Actual native
OCR retains the macOS/x64-only acceptance boundary described above. Candidate
feeds, runtime bundles, keys and maps are disposable, not commercial artifacts.

### Joint Migration Provider Rehearsal

```bash
node tools/native-licensing/joint-package-rehearsal.mjs --migrations
```

This explicit profile adds all 38 Migration projects to the execution graph:
54 first-party assemblies, 52 library packages and two separate workers. It
covers 16 PDF Code, eight Spreadsheet Code, eight Report Designer providers and
the Nutrient Viewer provider, their common/aggregate libraries and the existing
Generator/Importer/Converter tests. Namespace/project closure and consumer
NuGet dependencies are validated; no WebApi or persistence assembly is added.

The existing complete Code, Report and Viewer probes run first from project
outputs and then as independent consumers of the local protected feed. Their
public calls, wrong/missing/provider-specific rights, aliases, output checksums
and continuation behavior must agree. The Code probe also uses Roslyn to read
the first nonempty literal source fixture from each provider's test file and
compares its direct/factory result. These are trusted synthetic test inputs,
not vendor binaries or an assertion that generated code compiles for every case.
Full test bodies are not imported into the product or executed by this parser.

Historical Report root GUID and Viewer export-timestamp normalizations remain
the existing narrow probe rules. The Viewer verifies actual sidecar/generated
checksums before normalizing its timestamp. All other probe result fields and
diagnostics compare exactly. Original inventories/fixture contents are read
without modification. The package consumers compile harness code, never product
project references, and cannot reach network package sources during restore.
The offline dependency feed is the union of the main and all three original
probe restores. Every originally resolved third-party package version must
also be present in its protected probe restore; a larger combined graph must
not silently upgrade a smaller consumer's dependency.

Migration's Roslyn 5.9 and the standalone CodeWorker's Roslyn 5.3 stay in their
respective runtime bundles. Only the two reviewed CodeAnalysis/Common and CSharp
DLL resolution conflicts may differ; both hashes are recorded and original
vendor bytes remain intact. Other dependency conflicts fail. Real worker runs
verify the protected CodeWorker against its original standalone dependencies.
No product dependency version is changed to make the rehearsal pass.

The Linux job is prepared separately and uploads only report/cleanup JSON.
Check the implementation checklist for executed local evidence. This profile
does not grant production keys, publisher/legal approval, WebApi/tenant or
cross-platform acceptance. In particular, it does not cover the HTTP-hosted
Nutrient PDF Designer composition or permit mixing protected libraries with an
unprocessed WebApi, which uses friend-visible internal members.

### Signed Revocation Tests

```bash
node tools/native-licensing/with-test-license.mjs revocation
```

This isolated probe creates a signed revocation list without exporting a private
key, checks public import/status APIs and runs real C# PDF/OCR workers. It verifies
that a revoked-but-valid license is rejected, unrelated PDF execution still
works, and OCR transfers the verified list rather than inheriting an arbitrary
parent environment path. The OCR transport child validates configuration only;
the separate real OCR-worker rejection happens before input/Tesseract access.
Evidence is `revocation-admission.json` plus `run.json`; synthetic licenses,
lists and binaries are removed in `finally`. This is not manufacturer production
provisioning or a durable revocation-issuance service.

Native PDF creation and serialization now require a signed `pdf` grant. The
`with-test-license.mjs` runner creates temporary signed fixtures and a separate
synthetic-trust build. It never changes regular build directories or embeds a
test key into the source register. It removes all test licenses and candidate
binaries in `finally`; reports and TRX results remain under
`artifacts/native-engine-tests/`. No synthetic package is published.

Probe and test builds disable both compiler-server and MSBuild node reuse to
avoid inheriting stale build processes. This affects the isolated test runner,
not production compilation or licensing rules.

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
`pdf`, `word`, `spreadsheet`, `converter`, the partial `importer` entry-point grant,
and explicit provider lists for the tested PDF/Spreadsheet Code migration entries. Separate
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

The current complete public API gate is:

```bash
node tools/native-licensing/importer-surface.mjs verify
```

It compares four freshly built Importer assemblies against
[`native-importer-public-surface.v1.json`](../../product-metadata/native-importer-public-surface.v1.json),
then runs both native probes in the same signed isolated session. All 363
exported types, 5,087 declared public members and two explicit interface bindings
are tracked. All 201 protected members must match the successful probes'
`testedSurface` registration sets exactly. New public types, constructors,
methods, properties, base types or interface bindings fail until reviewed.

`capture` writes a review candidate only under the owned artifacts directory;
it cannot update or accept the source inventory. The contract includes current
free models/helpers and interface declarations without confusing declarations
with permission to execute implementations. Pure value/graph helpers are tested
without Importer rights. Image analysis has explicit/default interface tests.
The worker executable retains separate request/admission tests.

Signature registration plus a successful probe is a coverage gate, not a claim
that shape comparison can detect every behavioral change. Existing functional,
output, callback, expiry and cancellation tests remain required. Package,
cross-product and platform acceptance are still separate. The generic protected-
package API probe is unchanged; this contract adds no runtime bypass or key.
See the [public API checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#importer-public-api-contract-milestone).

The importer probe also validates the versioned
[registry entry inventory](../../product-metadata/native-file-importer-entries.v1.json)
against all exported importer implementations and their public processing
overloads. `FileImporterContinuationChecks` exercises 30 non-PDF direct,
registry and cancellation-interface call paths after a license replacement from
inside a stream read. Each path compares the document contents, geometry and
diagnostics and rejects the next public call before input consumption. Source
bytes remain unchanged and caller-owned streams remain open.

The seven non-PDF parsers need only their existing entry check: their private
work does not dispatch another protected engine. No ambient scope or extra
product entitlement is added. Yielded Markdown asset callbacks check that new
public imports still use current rights; resolver failures propagate unchanged.
All callbacks and inputs are synthetic and make no remote requests. These
checks supplement, rather than replace, the PDF-specific continuation tests.
Whole Importer acceptance remains partial. See the
[continuation checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#non-pdf-file-importer-continuation-and-stream-ownership-milestone).

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
coarse flag does not declare whole-product package/platform acceptance. The
current operation matrix now records public entry enforcement separately, with
`enforced: true` and explicit `remainingAcceptance` gates. The public API checkpoint
below supersedes the original partial-entry inventory, not its historical results.
Image analysis and OCR-worker admission are covered by the later milestone below.
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

At this historical checkpoint, replacing the license mid-read caused a nested
public parser admission to fail. That failure propagated rather than becoming
an invalid-PDF report. The later Trust continuation milestone changes the
replacement-only case to finish the admitted inspection; genuine callback
license failures still propagate.
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
outside that mapping path were separate work at that checkpoint. Editing is
covered by the subsequent milestone below. See the [Designer mapping evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#designer-pdf-import-and-analysis-continuation-milestone).

Final validation passed all 1,681 selected .NET tests and 49 tooling contracts.
Six suites completed before an interrupted OCR dependency build; OCR/API then
passed in a clean session with server reuse/shared compilation disabled. The
combined audit counts only actual passed TRX rows and verifies cleanup in both
sessions. The checklist preserves the interrupted build, corrected test-fixture
failures, source-body comparison and exact final evidence separately. No full
API/PostgreSQL or new cross-platform/package/visual acceptance is implied.

### Editing And Composition Probe

The final Importer probe adds 196 `editing-continuation:` checks (1,434 total).
It changes the license from inside a custom source-memory reader or edit/page/
range/document enumeration, then requires admitted parsing, page-tree changes,
decryption, composition, blank-page extraction and post-save validation to finish.
All 40 existing public editing entry signatures remain independently checked
against missing, wrong and renewed grants by the earlier `EditingChecks` matrix.

Fourteen memory-reader cases compare page geometry, order, text, transforms,
result counts and available operation metadata, including encrypted editing with
owner authentication and replacement encryption. Four enumeration cases cover
editing, extraction, splitting and merging. Content rewriting compares exact
stream bytes. Semantic PDF comparisons exclude variable serialization metadata
and encryption randomness; this is not a new visual-baseline approval.

Six facade cases test successful replacement, malformed input and IO failure,
each with owned and caller-owned streams. They verify retained/replaced bytes,
selected-page/unit state, original errors, denied-save side effects and renewed
blank-page insertion. Invalid ranges/duplicates, encrypted cross-document input
and existing signature-invalidation checks remain enforced. Synthetic signing
fixtures make no network calls, and temporary output is removed.

The editing purpose may request only private empty-page bytes, never public PDF
generation, regeneration or OCR rendering. Core tests additionally expire a
Trial inside an admitted editing scope and reject both new work and foreign
authority reuse. Trust/signing/redaction/e-invoice, advanced regeneration and
other composite products remain open. See the
[Editing evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-editing-and-page-composition-continuation-milestone).

The final checkpoint passed 1,434 distinct Importer probe checks, all 1,682
selected .NET tests and 50 tooling contracts. `editing-body-audit.json` confirms
unchanged processing/validation bodies after explicit admission normalization;
`editing-validation-audit.json` records raw results, input preservation, source
hashes and cleanup. This is local macOS/x64 .NET evidence, not full API/PostgreSQL,
cross-platform, protected-package or new visual-baseline acceptance.

### Trust Inspection And Change-Impact Continuations

`TrustContinuationChecks` extends the Importer probe with normal and genuinely
asynchronous stream replacement, full-report comparisons, PDF/A-2b/3b, AES-256,
valid and tampered signatures, DocMDP violations, and real synthetic embedded
CRL/OCSP evidence. It exercises all save-strategy/mutation combinations, retained
public evidence validators, custom evidence-provider callbacks, and IO, size,
cancellation and genuine callback-license failures. Only the volatile structural
validation timestamp is excluded from full-report comparisons.

The fixture phase signs with a synthetic CA and an in-memory timestamp handler;
it never sends a request to the configured synthetic hostname. The verification
phase keeps network policy unchanged. Reentrant and subsequent public work must
check the current license; neither Trust purpose grants a private PDF dependency.
Signing, redaction, e-invoice, archival conversion/post-processing and advanced
regeneration remain separate continuation work. See the
[Trust continuation evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-trust-inspection-and-change-impact-continuation-milestone).

The final Trust probe passed 1,669 distinct checks, including 235 new Trust
continuation checks. Cleanup confirms removed ephemeral grants/builds and
unchanged source trust and regular licensing outputs. The direct internal DSS
extractor retains a fresh entry for independent calls; inspection uses only its
admitted core. The existing extractor regression is preserved.

The final signed regression session passed all 1,683 selected .NET cases, 51
tooling contracts and 45 local documentation links/anchors. Its independent
`trust-validation-audit.json` verifies exact result rows, unchanged compiled
inputs, normalized processing bodies and cleanup. These are local macOS/x64
results, not full API/PostgreSQL, cross-platform or protected-package acceptance.

### PDF Signing And Timestamp Continuations

`SigningContinuationChecks` covers PAdES B/T/LT/LTA and all three supported
SHA-2 digests, visible signature appearance, document/RFC 3161 timestamps and
real synthetic CRL/OCSP evidence. It replaces the license inside source reads,
the in-memory HTTP handler and asynchronous material providers. Every successful
PDF is inspected cryptographically after restoring the test license; timestamp
tokens are independently checked with the BCL CMS verifier.

Only sealed built-in signing keys and timestamp authorities use private
continuations. Custom key/timestamp/material providers retain interface dispatch
and freshly checked public native calls. Synchronous `SignAsync` license denial,
source preservation, no general PDF dependency, actual errors and cancellation
remain covered. All HTTP is handled in memory, never by the synthetic hostname.
The failed initial probe attempted to derive a sealed key in a test; the test
was corrected to use the supported interface, without unsealing the API.

See the [signing continuation evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-signing-and-timestamp-continuation-milestone).

The native signing checkpoint passed 1,948 distinct checks, retaining all 1,669
previous checks and adding 279 signing cases. Its cleanup report verifies
removed ephemeral licenses/builds, unchanged source trust and unchanged regular
license outputs. The normalized body audit preserves all cryptographic,
timestamp/certificate and PDF validation algorithms; it does not claim byte-equal
timestamped PDFs, production TSA acceptance or cross-platform package readiness.

The final signing session passed 1,326 selected .NET result rows, 52 tooling
contracts and 47 local documentation links/anchors. Its independent
`signing-validation-audit.json` verifies exact counters, source/dispatch hashes,
all retained native checks and complete cleanup. This does not replace a full
API/PostgreSQL or new visual/platform/protected-package acceptance run.

### PDF Redaction And Audit Continuations

`RedactionContinuationChecks` compares preflight findings and exact output bytes
with licensed reference executions, then independently reopens the output. It
covers mixed-size pages, text, metadata/attachments, RGB/gray/CMYK/indexed/inline
images, masks, optional audit CMS/timestamps, record tampering, custom providers,
IO/cancellation/preflight-size failures and denied calls before source access.
It verifies one rewritten revision and no remaining metadata/attachment payload.

The scope includes bounded color-space/function construction, not general color
evaluation. The historical sampled/PostScript/ICC replacement cases now separate
admitted completion from actual resolver-native failures. Additional memory-
manager cases throw a real native license error during decoded-byte access and
verify it is not swallowed. Public conversion/evaluation stays freshly guarded.

Existing forged-marker/residual-image tests are unchanged. Their direct scanner
entry has its own guard, while apply uses the admitted core. Audit timestamps
use synthetic keys and an in-memory HTTP handler only. The
[Redaction continuation evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#pdf-redaction-and-audit-continuation-milestone)
records exact runs and limitations.

The final native redaction checkpoint passed 2,257 distinct checks, including
303 redaction cases and six extra completion/fresh-denial cases around the three
explicitly migrated historical color failures. The nine-file body audit restores
only eleven entry wrappers, built-in dispatch and admitted call names before
comparison; geometry, text removal, sanitization and resource inspection remain
byte-identical. Cleanup confirms removed synthetic grants/builds and unchanged
source trust/regular license outputs. This is not new codec, platform or package
acceptance evidence.

The final Redaction session passed all 1,683 selected .NET result rows, 53 tooling
contracts and 49 local documentation links/anchors. Its independent
`redaction-validation-audit.json` verifies exact counters, historical assertion
migrations, current source hashes, unchanged safety helpers and complete cleanup.
The original forged-marker/live-image tests are unchanged and pass. General
color conversion/evaluation was not included at that checkpoint; the following
milestone adds its built-in continuations. These results are not broad release
acceptance.

### E-Invoice And Archival Continuations

The continuation probe supplies a fixed `TimeProvider` to the attacher so
baseline and license-replacement executions use the same attachment metadata
timestamp. The complete PDF bytes still compare exactly; neither metadata nor
timestamps are stripped from results. Separate runtime tests advance the clock,
verify the parsed UTC `ModDate` and retain the original system-clock constructor.
This clock is independent of native-license expiry and Trust validation clocks.

The initial `EInvoiceContinuationChecks` milestone added 186 checks to the native probe. It compares
preflight and exact attachment output against licensed reference executions for
PDF/A-3 incremental updates, approved plain-PDF conversion and PDF/A-2 with CMYK
content. Synchronous and truly asynchronous replacement are exercised separately.
It independently extracts XML, checks declared size/checksum, preserves unrelated
attachments and verifies mixed page geometry and structural archival status.
The fixture XML is synthetic, not a validated tax invoice.

Standalone PDF/A-2b/3b conversion and associated-file extraction keep admission;
new calls after replacement remain denied. Unapproved archive conversion and
signature modification, duplicate invoice conflicts, encryption, size limits,
IO, cancellation and real callback-native errors retain their existing failure
behavior. Custom converters and Trust inspectors retain fresh native dispatch.
All five built-in function families, tint/alternate recursion, real LittleCMS
transforms and named-color rewriting are covered; custom function subclasses
are denied before their bodies when the current license lacks Importer rights.

Run with `PVAD_REQUIRE_LCMS2=1` to require the actual native color library.
The final probe at `artifacts/native-engine-tests/1794901459357-889c6c7e/`
passed 2,443 distinct checks with unchanged source trust/normal licensing outputs
and complete temporary-file cleanup. The first attempt retained its failed
report: its rewriter assertion expected an exact whitespace splice and did not
select a named color resource. Correcting that fixture did not change product
algorithms. See the
[E-Invoice continuation evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#e-invoice-and-archival-continuation-milestone)
for suite results, body comparisons and remaining acceptance boundaries.

The final signed session at
`artifacts/native-test-sessions/739f7803-836b-4619-a8b7-0262279631fd/` passed
all 1,683 selected .NET result rows. The independent validation audit reconciles
all eight TRX files, 1,089 unchanged compiled/test inputs, nine unchanged
processing bodies after fourteen explicit wrapper/dispatch normalizations and
both complete cleanup reports. All 54 tooling tests and 51 documentation
links/anchors pass. This is local macOS/x64 evidence, not new full API/PostgreSQL,
visual, platform or protected-package acceptance. Trust post-processing,
advanced regeneration/XObject/debug and other product composites remain open.

### Trust Post-Processing Continuations

`TrustPipelineContinuationChecks` adds 410 checks to the native probe. It covers
validation-only, Redaction-only, signing and combined flows across PAdES B/T/LT/LTA,
including signed/timestamped Redaction audits and asynchronously supplied CRL/OCSP
material. License replacement during source access, region inspection, timestamp
HTTP and material-provider completion must not interrupt admitted built-in stages.
New pipeline, signing, Redaction, inspector and audit calls remain denied.

The probe verifies stage order, exact final-byte hashes, independently reopened
Trust reports, signature integrity/profile, embedded evidence, mixed page geometry,
removed/retained text and untouched input. Custom stage/key/timestamp/provider
interfaces are never bypassed. Actual callback-native, IO, cancellation, encrypted
Redaction and timestamp failures propagate; all five final-policy failures retain
their codes/reports. No general PDF permission is granted by pipeline completion.

The probe at `artifacts/native-engine-tests/1794902476581-693cafab/` passed
2,853 distinct checks, retaining all prior 2,443 assertions. It required real
LittleCMS and used synthetic license/signing material with in-memory timestamp
HTTP only. Cleanup confirms deleted synthetic grants/builds, unchanged source trust
and unchanged regular licensing outputs. See the
[Trust pipeline evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#trust-post-processing-continuation-milestone)
for final regression and algorithm-comparison results. Advanced regeneration,
other products, protected packages and platform acceptance remain separate work.

The signed session at
`artifacts/native-test-sessions/0b32773c-533e-4428-b65a-a1d9c23ec7c9/` passed all
1,689 selected .NET result rows across eight suites, including 672 Importer cases
and 407 selected API cases. Six new focused cases reject unsuccessful Redaction
verification and invalid signing plans before stage dispatch. The independent
audit verifies actual TRX counters, 1,090 unchanged compiled/test inputs,
unchanged pipeline/signing algorithms after explicit entry/dispatch normalization
and complete cleanup. All 55 tooling contracts and 53 documentation links pass.
This is local macOS/x64 evidence, not broad platform or package release approval.

### Regeneration, XObject And Debug Continuations

`RegenerationContinuationChecks` adds 183 native checks. It compares exact
regenerated bytes for metadata, RGB/indexed images, compressed palettes, soft masks
and shadings after Importer rights are replaced inside data access. Both direct
regeneration and the built-in save pipeline are covered. Independent reimport
checks mixed dimensions/rotation, text, paths, image and shading counts.

Recursive object/resource mapping, form transforms/bounds and debug overlays
retain their outputs. Missing XObjects, recursion limits and empty-form bounds
retain existing behavior. Analysis/mapping grant no private renderer access.
Caller configuration still requires both current Importer and PDF rights; all
previous configuration/revocation/retained-document tests stay unchanged.

Synchronous and genuinely asynchronous output/custom-bridge callbacks verify
fresh public admission. Source/output IO, cancellation and actual callback-native
errors retain their failure types. Tests asserting untouched output throw before
writing; arbitrary streams that partially write then fail are not promised atomic
rollback. This milestone does not add form decoding or image/shading capabilities.

`artifacts/native-engine-tests/1794906763186-0dcfaf93/` passed 3,036 distinct
checks, retaining all previous 2,853 assertions. The first attempt at
`artifacts/native-engine-tests/1794906662714-e48d1c06/` retained its failed status:
the probe incorrectly expected general PDF denial after switching to a PDF-only
license. The corrected check runs under Importer-only rights; product code was
not changed for that assertion. Both cleanup reports confirm deleted synthetic
grants/builds and unchanged production trust/normal licensing outputs. See the
[regeneration evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#regeneration-xobject-and-debug-continuation-milestone)
for regression results, algorithm comparisons and remaining boundaries.

The final signed session at
`artifacts/native-test-sessions/b1c2d74f-cb1f-4b8d-810a-bfb1aa77b775/` passed
all 1,690 selected .NET result rows across eight suites, including the new
regeneration-expiry/dependency case. Independent reconciliation checks all raw
TRX counters, 1,091 unchanged compiled/test inputs, five processing-body
comparisons and both final cleanup reports. All 56 tooling contracts and 55 local
documentation links/anchors pass. This is local macOS/x64 evidence, not new
full API/PostgreSQL, visual, cross-platform or protected-package acceptance.

### Product Dependency And Spreadsheet Continuations

The product matrix links a validated dependency inventory with explicit
implemented/planned boundaries. It does not replace the runtime verifier or
introduce cross-product bundle access. Its guard rejects duplicate IDs, unknown
products/statuses, unsafe source paths and invented private PDF dependencies.

`SpreadsheetContinuationChecks` adds 157 checks to the Spreadsheet probe,
covering direct XLSX/XLS/CSV/TSV output, calculation, builder export and all four
file-save paths after a cell value replaces the current license. Reentrant
construction, export and calculation are denied while admitted processing
finishes. Independent reimport verifies text, formulas and calculated values.
Error cases cover actual native callback denial, source IO and missing-directory
failures without masking their types or writing a result before validation.
Style callback mutations remain fresh public calls.

The Spreadsheet probe at
`artifacts/native-engine-tests/1794915905592-2b3255ed/` passed 302 distinct
checks across its existing 40 protected method signatures and new continuation
cases. The unchanged Word probe at
`artifacts/native-engine-tests/1794916023581-d01ee29a/` passed 34 checks for
Word-only DOCX export/signing and independent PDF/Converter denial. Cleanup for
both probes removed synthetic licenses/builds and preserved source trust/normal
licensing outputs. NPOI distribution warnings remain an open, separately approved
license decision; tests do not accept its terms. See the
[product dependency evidence](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#product-dependency-and-spreadsheet-continuation-milestone)
for regression and algorithm-comparison results.

The signed regression session
`artifacts/native-test-sessions/fc549b8c-d959-43a7-a8c0-af2070f0eeb4/`
passed 1,710 TRX rows across ten selected suites, including 407 API cases.
`sheet-validation-audit.json` checks all result rows and process statuses, 1,092
unchanged compiled/test source inputs, native-probe cleanup and the five-file,
six-entry processing-body comparison. No tests were skipped, failed, aborted or
timed out. `sheet-doc-links.json` validates 59 local links/anchors; all 58 tooling
tests, naming guards and metadata/fragment checks passed. Temporary licenses and
isolated builds were removed. Product version, published releases, production
trusted keys and native release metadata remain unchanged. These local
macOS/x64 checks do not constitute full API/PostgreSQL, visual, cross-platform or
obfuscated-package acceptance.

### Converter Export Enforcement

`with-test-license.mjs converter` starts a separate unlicensed process and discovers
all nine concrete exporters. Its reflection inventory requires exactly thirteen
declared public export methods; another eighteen calls cover both interface
overloads, including inherited default dispatch. A Converter-only license must
produce HTML, Markdown, CSV, XML, SVG, ODT, PNG, JPEG and TIFF without other
product grants. Metadata stays free and denied exports do not traverse the input.

The probe verifies real output signatures, text, image dimensions and mixed-page
ZIP entries, including default and explicit DPI paths. Eight style-value callback
cases replace the live license during rendering; admitted multi-page work still
finishes, while reentrant and subsequent public calls check current rights.
Callback-native and source IO failures retain their types. CSV has no synthetic
style callback; verifier-core tests cover expiry without inventing such a hook.

The final local probe at
`artifacts/native-engine-tests/1794917388776-4af2568d/` passed 519 distinct
checks, with synthetic-license/build cleanup confirmed. Two earlier harness
attempts failed on SVG BOM parsing and an uninitialized synthetic style
dictionary; they are not counted as passing. Their evidence remains at
`1794917245295-83845bca` and `1794917324742-a2b3a9a2` in the same parent directory.
The final probe compiles without warnings. Product rendering algorithms are
unchanged; this is not a new cancellation, asset-network or format-fidelity
implementation. Native-license failures through the API must remain typed
Problem Details, not document downloads.

The workflow now triggers on Converter/dependency-inventory changes, runs the
probe and compatibility suite, and includes Converter API diagnostics. Its
prepared matrix is not evidence of an actual Windows/Linux run. See the
[Converter milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-converter-enforcement-milestone)
for final regressions and remaining release gates.

The signed session
`artifacts/native-test-sessions/853ca2b7-bc20-4cc1-902e-f8a045b1a95c/`
passed 596 .NET cases (92 Licensing, 10 Converters, 78 selected Export and 416
selected API), without failures/skips. Its `converter-validation-audit.json`
reconciles actual TRX rows, all four process statuses, 1,095 unchanged
compiled/test source inputs and cleanup. `converter-body-audit.json` verifies
eight processing files unchanged except admission and 300 unchanged other-product
source inputs. All 59 tooling tests and 61 local links pass. The current CI naming
guard passes; the old JSON audit's 475 pre-existing findings remain separately
recorded, with its 48 affected files/audit inputs unchanged from `HEAD`. These
tests do not accept NPOI terms or replace full API/PostgreSQL, visual, platform
or protected-package acceptance.

### Code Migration Provider Entries

`with-test-license.mjs migration-code` starts unlicensed and checks all sixteen
PDF Code and eight Spreadsheet Code native providers. The test-only
`native-code-migration-entries.v1.json` inventory is compared against the canonical
provider catalog and actual sealed classes. It is not a second runtime registry.
Direct, abstract-base, interface, factory/wrapper and PDF-aggregate calls must
reach the same provider-bound license check before source parsing.

The runner signs one combined Code-only fixture and 24 individual provider
fixtures only for this probe. Each individual license is tried against all 24
Code providers. No wildcard, vendor-family or cross-domain grant is used.
Routing aliases resolve to canonical keys; aliases are not signed-license keys.
Converted output is text only and is not executed by the native probe. Existing
provider suites separately test concrete framework constructs and diagnostics.

The initial native run at
`artifacts/native-engine-tests/1794919955149-1c186c7e/` passed 1,187 unique checks.
It records 24 output checksums and successful cleanup. The existing Apryse unused
field warning is not suppressed. Test sessions extend their integrated fixture
with only `migration.pdf.code` and `migration.spreadsheet.code`, using explicit
canonical provider lists. Product-only fixtures are unchanged; Viewer, Designer
and Report grants are not pre-enabled. No new production trust key is added.

The later composite milestone adds standalone source analysis and provider-bound
internal lifetime rules. Code products are enforced; the subsequent Viewer/Designer
and Report Designer milestones add their separate grants. Private PDF dependencies are not granted to Code
migration. Prepared CI is not an actual cross-platform or protected-package
acceptance run. See the
[Code provider milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#native-code-migration-provider-entry-milestone)
for final regression evidence and remaining work.

The signed regression session
`artifacts/native-test-sessions/2b4aee9c-5ecc-4107-8720-19f39b8a0a94/`
passed 712 cases across 30 suites, including every existing Code-provider suite
and 116 selected API cases. Actual canonical/alias/legacy routes reject PDF-only
native rights and recover after signed renewal. The validation audit reconciles
all TRX rows and process statuses without failures/skips, 1,098 unchanged
compiled/test source inputs and removed synthetic grants/builds. The body audit
verifies 24 provider implementations unchanged except admission and 436 unchanged
other-product sources. All 62 tooling tests and 63 local links pass. These are
local macOS/x64 entry-point regressions, not full API/PostgreSQL, visual,
cross-platform, protected-package or complete Migration-product acceptance.

### Code Analysis And Composite Continuations

The extended migration probe checks standalone Trust/E-Invoice source analysis
for every provider, cross-domain single-provider denial and equivalent results.
Caller-supplied Trust capability lists inject a mid-analysis license replacement:
built-in work completes from its admission while nested/public conversions and
analyses remain fresh boundaries. Source IO and native callback errors keep their
types. The immutable E-Invoice catalog is not modified to manufacture a callback;
its tests cover public admission, equivalent output, denial and renewal. Expiry,
provider omission/switching, nested scopes, foreign verifiers and private-PDF
denials are covered in the verifier core.

API tests cover Code-only HTML preview without other engine rights, real PDF
preview requiring current PDF rights, and custom-converter errors. Custom
converters never receive internal dispatch based only on a claimed framework ID.
Conversion/diagnostics share one native result. PDF construction and serialization
stay separate public processing steps; a Code continuation cannot reserve a
future PDF grant or bypass either check. The preview rendering/replay algorithms
remain unchanged. See the
[composite checkpoint](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#code-migration-analysis-and-composite-milestone)
for exact local evidence and still-open release requirements.

The final native run `artifacts/native-engine-tests/1794923471792-e3600ded/`
passed 2,039 unique checks, including 812 analysis checks and unchanged code
checksums for all 24 providers. The earlier extended attempt
`1794923256615-54cc6639` failed on an immutable-catalog test-fixture assumption;
the catalog was not loosened, and failed evidence is retained. All native attempts
removed temporary synthetic licenses/builds and preserved source trust.

The signed session
`artifacts/native-test-sessions/1a44debf-1860-4156-891a-6a35a247d8c0/`
passed 715 .NET cases across 30 suites, including 95 Licensing and 117 selected
API cases, without failures/skips. `code-composite-validation-audit.json` checks
actual TRX rows/process statuses, 1,100 unchanged compiled/test source inputs and
owned-session cleanup. The body audit verifies 24 converter bodies, two analysis
tails and two preview bodies without algorithm changes, plus 429 unchanged
other-product source inputs and an unchanged private PDF dependency switch.
All 63 tooling tests and 65 local links pass. The current CI naming guards pass;
the historical JSON-audit findings remain a separate recorded issue. Product
version, published releases, production trust and native release metadata are
unchanged. These local macOS/x64 checks do not replace complete solution,
API/PostgreSQL, visual, cross-platform or protected-package acceptance.

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

## Nutrient Viewer And PDF Designer Admission

Run the native Viewer boundary probe without production trust or licenses:

```bash
node tools/native-licensing/with-test-license.mjs migration-viewer
node tools/native-licensing/with-test-license.mjs test tests/PVAD.Migration.Pdf.Viewer.Nutrient.Tests/PVAD.Migration.Pdf.Viewer.Nutrient.Tests.csproj --verbosity minimal
node tools/native-licensing/with-test-license.mjs test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --filter FullyQualifiedName~PvadNativeViewerMigrationLicenseTests --verbosity minimal
```

The default signed session now contains 12 files: a public synthetic trust
register, the previous product/Code/integrated/expired fixtures, and separate
Viewer-only, Designer-only and Report-only fixtures. Its integrated profile contains both
explicit `nutrient-viewer` grants and all eight concrete report providers. Product-only and Code-only profiles retain
their original narrow grants. The Code-provider probe adds its 24 individual
fixtures, for 36 files total. The Report probe instead adds eight individual
provider fixtures, for 20 files total. No private key is written to disk.

The Viewer probe starts without an active license and checks all three native
entries, validation ordering, unrelated-product denial, exact provider keys,
public reentrancy, replacement during caller-owned page/diagnostic enumeration,
original callback errors and renewed output. Output comparisons validate the
real hashes before normalizing only the existing Instant JSON export timestamp
and its dependent hashes. They do not change the converter or its fidelity.

The Designer API checks use a mixed-orientation, two-page synthetic PDF.
Composed-output comparisons validate and normalize only the existing newly
generated document ID and sidecar export timestamp; page/element IDs, layout,
content and other metadata remain compared.
Designer-only admission includes private source import and Viewer mapping but
cannot be reused for independent public Importer, Viewer or PDF calls. The
private import purpose cannot gain any private PDF output dependency. Existing
route authorization and tenant/entitlement checks remain separate from native
licenses. A canonical Designer route returns native license Problem Details,
not a parse-error replacement. Tests exercise source-stream and TypeScript-list
callbacks, fresh nested calls, completion under the original admission, callback
errors, renewal and layout preservation.

See the [Viewer and Designer milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#nutrient-viewer-and-pdf-designer-milestone)
for actual local evidence and remaining package/platform acceptance. Report
Designer is covered by the subsequent milestone; these checks do not close the full licensing plan.

The final scoped session
`artifacts/native-test-sessions/2d5d551b-7f0e-43b7-8c02-99eb5764aed5/`
passed all 230 TRX rows in five suites, including 77 selected API cases. Its
authenticated Viewer fixture uses synthetic tenant/user identities, in-memory
policies and the real feature gate with a restricted synthetic Designer
entitlement. Separate requests prove that native rights do not replace the
legacy route's 401/403 authorization rules. The raw validation audit checks all
result rows, 1,337 compiled source/project inputs and completed cleanup. All
1,344 snapshotted source/test/tool inputs were unchanged during execution.

The final native probe at
`artifacts/native-engine-tests/1794936135959-a16ee8f2/` passes 60 unique checks;
all three normalized checksums match the preceding successful run. Seven exact
body/dispatch comparisons, all 64 tooling/versioning tests and 67 local links
also pass. Earlier incomplete authentication/feature-context fixtures and the
waiting build remain failed or interrupted evidence. The isolated runner now
disables parallel project builds; no runtime algorithm or test threshold was
changed to make these runs pass. Native keys/test binaries were removed and
production trust/version data remain unchanged. This is local macOS/x64
validation, not complete browser authentication, live PostgreSQL, remote CI,
Linux/Windows, visual parity or protected-package acceptance.

## Report Designer Admission

```bash
node tools/native-licensing/with-test-license.mjs migration-report
node tools/native-licensing/report-regressions.mjs
```

The Report probe starts without a license and inventories all 19 native converter
overloads, both RESX entry paths, and canonical/uppercase factory-interface
dispatch for eight providers. It validates the canonical catalog against
`native-report-migration-entries.v1.json`, admits one explicit provider at a time,
rejects other providers/products, validates admission before source parsing,
checks expired renewal and compares output after valid renewal. Only each
converter's pre-existing random root document ID is validated and normalized;
page/element IDs, content, diagnostics and geometry remain compared.

The regression runner shares one signed session across the eight native report
suites, aggregate facade suite, licensing core and selected API tests. It records
per-project logs, TRX and process status, checks unchanged source inputs, fails
on any child failure/timeout and removes synthetic licenses and binaries in its
owned-session cleanup. API cases include explicit aliases and auto detection,
legacy routes, binary/ZIP input, resource preprocessing without an extra
DevExpress grant, replacement/reentrancy during resources, retained RPX subreport
resolution, original caller errors and renewal. Existing permission tests remain
separate from native licensing checks.

Detection and generic package extraction are free input utilities, not an `auto`
license or a promise of full source compatibility. Conversion is admitted for the
resolved concrete provider before caller-owned resources are merged. Shared
in-operation RESX parsing uses that same provider; standalone DevExpress RESX
processing requires `devexpress-report`. No private PDF dependency or public
rights for other engines are granted.

See the [Report Designer milestone](../../checklists/PVAD.Native-Library-Licensing-And-Protection.md#report-designer-migration-milestone)
for actual results, interrupted attempts and remaining platform/package gates.

The local signed session
`artifacts/native-test-sessions/37f05ac5-b11b-4299-8b23-a1ebf4eb1998/`
passed all 490 TRX rows across eleven suites, with no failed/skipped/aborted cases.
Its raw-report audit verifies unchanged sources (1,349 inputs) and synthetic
cleanup. The native probe at `1795115577122-5a2e33c6` passed 703 unique checks;
the earlier waiting build at `1795114430736-b38bd13b` remains interrupted, not
passing. The successful local probe retry used `MSBUILDNOINPROCNODE=1` without
changing shipped build settings or runtime checks. All 16 converter/wrapper
algorithm bodies, resource parsing/detection/merge and private PDF dependency
policy are unchanged apart from admission/private dispatch. All 65 tooling tests
and 69 local links pass. Manufacturer trust, protected packages, full API/DB,
visual and Linux/Windows acceptance remain outside this local checkpoint.

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
