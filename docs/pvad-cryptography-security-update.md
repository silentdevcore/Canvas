# PVAD Cryptographic Dependency Update

## Scope

Reviewed on 2026-10-25. This is dependency maintenance, not production release
approval or completion of a rename gate. Product version `1.1.0` is unchanged.

Update the five explicit cryptographic package references to `10.0.13`:

| Package | Projects | Previous direct version |
| --- | --- | --- |
| System.Security.Cryptography.Pkcs | PDF engine, PDF importer | 10.0.8 |
| System.Security.Cryptography.Xml | Spreadsheet, Word, converters | 10.0.10 |

The repository compliance validator now enforces these reviewed pins for both
packages. No vulnerability exclusions, warning suppressions, or license
acceptance flags were added. NPOI remains a separate, unresolved legal decision.

## Vendor Evidence

The Microsoft advisories identify `10.0.13` as the patched .NET 10 package version:

- [PKCS advisory GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx).
- [PKCS advisory GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
- [XML advisory GHSA-r955-8q5j-3h83](https://github.com/advisories/GHSA-r955-8q5j-3h83).
- [XML advisory GHSA-7hqp-7c5m-4vq6](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6).
- [XML advisory GHSA-c399-9853-6jmj](https://github.com/advisories/GHSA-c399-9853-6jmj).

Package compatibility was checked against the official
[PKCS package](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.13)
and [XML package](https://www.nuget.org/packages/System.Security.Cryptography.Xml/10.0.13).

## Local Evidence

Generated evidence is under `artifacts/dependencies/crypto-2026-10-25/` and is
intentionally excluded from source control. These files are local evidence, not
published release artifacts or a signed SBOM.

| Check | Result | Evidence |
| --- | --- | --- |
| Before: direct and transitive NuGet audit | 111 projects; 103 finding instances across five distinct advisories | `before.json` |
| After: complete restore and online NuGet audit | 111 projects; zero known vulnerability findings | `after.json` |
| Existing CI vulnerability gate | Passed for 111 projects | `nuget-gate.log` |
| Resolved package graph | All retained PKCS/XML package entries use 10.0.13; 29 affected projects | `resolved-packages.json` |
| Full solution build, including both worker packages | Passed; zero errors, 113 other warnings; no NU1902/NU1903 warnings | `build.log`, `build-result.json` |
| Final x64 solution build, including both worker packages | Passed; zero errors, 67 other warnings; no NU1902/NU1903 warnings | `x64-build.log`, `x64-build-result.json` |
| Full patched-runtime x64 .NET regression run | 53 reports; 3,428 passed, 82 skipped, three failed baseline checks; not a green full suite | `test-summary.json`, `x64-solution-tests.log`, `x64-solution-tests/` |
| PDF importer and signing suite | 646 passed | `x64-tests/importer.trx` and full-suite reports |
| API suite with disposable PostgreSQL and current migration SQL | 1,168 passed, 80 skipped, zero failed; test database removed | `x64-solution-test-run.json` and full-suite reports |
| PDF/Word/Spreadsheet/converter infrastructure suites | 81 passed | Full-suite reports |
| Export suite | 220 passed | Full-suite reports |
| Loaded shared-framework assemblies | PKCS and XML both report 10.0.13 | `runtime-probe-x64.log` |

The original dependency-compliance regression test was run before changing the
pins and failed as expected. All four compliance contract tests and the metadata
validator pass after the correction. The NPOI production blocker remains active.

The initial arm64 run exposed missing native OCR dependencies, a timing-sensitive
image-analysis case, and API startup failures against the machine's default
database credentials. The x64 follow-up uses the architecture of the existing
developer .NET installation and native OCR libraries, a new disposable PostgreSQL
instance initialized from current generated migration SQL, and isolated storage
and key directories. No existing database was migrated or reused. PostgreSQL
opt-in tests, paired-binary cutover tests, and optional external suites were not
enabled. Skipped tests are not passing evidence. This does not certify an arm64
OCR deployment.

The OCSP test fixture now creates whole-second timestamps instead of comparing
random fractional UTC ticks through ASN.1 encoding and decoding. This removes an
observed 100 ns rounding failure without relaxing any production trust check or
test assertion. The complete importer suite passes with that change.

The owner approved the exact customer-facing security summary in
`product-metadata/release-fragments/crypto-dependency-security-patches.json` on
2026-10-25. Its impact is `patch`; the fragment does not publish a release or
approve deployment.

## Initial Runtime Baseline Findings

The initial patched-runtime suite at `c4285d8a` remained failed for these cases:

- `EInvoicePdfaFixtureTests.The_fixtures_and_their_manifest_are_written_and_pinned`:
  regenerated invoice PDFs have different size/hash values from the committed
  validation-input manifest.
- Both canonical cases of
  `PvadProjectArchiveTests.Published_reference_fixtures_are_valid_synthetic_projects`:
  rewritten `.pvadp` and `.pvads` archive bytes differ from their committed files.

Control runs of the same updated binaries on the previous runtime pass all four
invoice-fixture cases and all four published-project cases. They are diagnostic
controls, not approval to deploy the old runtime. Logs and TRX files are under
`old-runtime-controls/`, `old-runtime-generator-confirmed.log`, and `old-runtime-core.log`.

An additional read/rewrite probe on the patched runtime confirms that every ZIP
entry has identical uncompressed content and timestamps, and that repeated
writes are deterministic. Only compressed lengths/bytes differ; see
`archive-probe.log`. The original project archives are unchanged. The generated
invoice manifest was returned to its exact committed bytes by the old-runtime
control run and checked with Git; no new validation baseline was approved.

Before claiming a green patched-runtime full suite, review the archive byte
stability contract and regenerate the invoice PDFs with the patched runtime into
a separate evidence directory. Independently validate those PDFs before deciding
whether to approve new PDF/A fixture hashes. Do not suppress the
tests, replace historical validation evidence, or reuse old validator reports
for PDFs with new hashes.

## Baseline Follow-Up - 2026-10-25

The ZIP reference tests now compare exact uncompressed contents, entry order,
names, timestamps and attributes. Repeat writes must still be byte-identical on
the current runtime. Additional regression cases reject changed content,
timestamps, attributes, entry order, missing entries and extra entries, while
accepting recompression alone. Original project archives are unchanged.

The invoice fixture test now writes only an ignored candidate manifest and never
modifies the approved source manifest. Regression cases verify that mismatched
and missing approved manifests are preserved rather than implicitly accepted.
An additional contract binds the approved manifest and each fixture hash to the
review record and the archived previous manifest.

All seven patched-runtime PDFs were validated before adopting their new hashes:
veraPDF 1.31.166, pinned by image digest and run without network access, confirmed
the six expected PDF/A-3b examples and rejected the negative control. All 11 pages
are pixel-identical at 144 DPI to their checksum-verified previous versions;
extracted text and attachments also match exactly. Previous reports were not
reused or overwritten. The former approved manifest is archived byte-for-byte.

See [the baseline evidence and future review workflow](../test-assets/e-invoicing/baselines/README.md).
New local evidence is under `artifacts/dependencies/crypto-baselines-2026-10-25/`.
This is approval of these technical fixture hashes only, not business-invoice,
secondary-validator, license, production-runtime or deployment approval.

The first follow-up full run reported no failed assertions but still exited
with an interrupted Export test host. Its local 120-second hang collector
terminated the real-world corpus test, which includes a 62-page PDF and DOCX/XLSX
roundtrips. Running that test alone succeeded in about 115 seconds on emulated
x64, leaving too little headroom at the original diagnostic threshold. The final
repeat uses a local 300-second per-test collector limit and an overall
900-second process limit; CI and production limits are unchanged. The original
aborted reports are retained, and zero failed assertions alone is not sufficient
to mark a full run green.

The final full-solution repeat passed: **53 completed project reports, 3,437
passed, 82 skipped, zero failed**, process exit code **0**, and the disposable
database removed. The real-world corpus case completed in that same run; the
Export suite reports 221 passes. See `final-test-summary.json`,
`final-solution-test-run.json`, `final-solution-tests.log`, and
`final-solution-tests/` under the follow-up evidence directory. The full build
also passed, with zero errors and 110 existing warnings. The 35 release,
compliance and residual-review contracts and 15 documentation/native-project
contracts pass. Source code under `src/`, API implementation, CI rules, `VERSION`
and the published release manifest are unchanged by this follow-up.

The earlier 3,428/82/3 result remains historical evidence and is not relabeled
green; its aggregate also omitted the interrupted corpus case. The final total
includes five added regression cases and that completed corpus case. Skipped
tests remain unverified, not passes. This closes the three baseline follow-ups,
not the remaining runtime deployment, license, external validation or rename gates.

## Initial Runtime And Deployment Boundary

The system-wide developer SDK is `10.0.105`, with .NET and ASP.NET Core runtimes
`10.0.5`. It was not upgraded by this change. Tests use isolated SDKs `10.0.113`
with both runtimes at `10.0.13`. The macOS arm64 and x64 archives were downloaded from the
[official release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
and verified against their listed SHA-512 before extraction. `toolchain.json`
and `toolchain-x64.json` record the exact URLs and hashes. These toolchains are
not committed or installed globally.

With the patched SDK, the API dependency manifest omits the separate crypto
package entries because the ASP.NET shared framework supplies these assemblies.
Consequently, a clean NuGet audit does not prove that an application running
against an older shared runtime is patched. It also does not scan native or OS
libraries. Do not reuse the earlier API image as evidence for this update.

Before a production release:

- Retain the reviewed baseline contracts and rerun regression validation for the
  exact production artifacts; the green local follow-up is not deployment approval.
- Update the runtime used to run API and worker processes to the supported
  patched .NET 10 servicing release, at least `10.0.13` for these advisories.
- Rebuild deployment images with refreshed SDK and runtime base images; record
  immutable image digests and the actual runtime versions in the final images.
- Re-run the vulnerability gate, generate matching SBOMs, and scan the final
  images rather than relying on this local source-tree audit.
- Re-run deployment smoke tests on the exact release artifacts.
- Resolve the independent NPOI legal approval or replacement decision.

No runtime deployment, image publication, registry ownership, or NPOI EULA
approval is implied by these local results.

## System and Container Follow-Up - 2026-10-26

The later [runtime and container validation](pvad-runtime-container-validation.md)
records the system CLI update, fresh image builds and scans, native OCR
packaging corrections, and the unchanged Production retention gate. The initial
system-runtime description above is historical; it must not be used as the
current default-CLI inventory. This follow-up does not change the approved
invoice baselines, product version, published release history, NPOI approval or
rename percentage.
