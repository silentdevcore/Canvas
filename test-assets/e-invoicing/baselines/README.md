# Electronic-Invoice Runtime Baselines

The active fixture manifest is [../pdfa-fixtures.json](../pdfa-fixtures.json).
Its .NET 10.0.13 hashes were adopted only after validating all seven newly
generated PDFs. Production rendering code and the product version did not change.

## Preserved Evidence

- [dotnet-10.0.5-pdfa-fixtures.json](dotnet-10.0.5-pdfa-fixtures.json) is the
  byte-identical previous manifest from commit `c4285d8a`. Its historical schema,
  provenance and license strings are retained without modification.
- [dotnet-10.0.13-validation.json](dotnet-10.0.13-validation.json) records the old
  and new manifest/document hashes, immutable validator image, tool versions,
  exact report hashes, and comparison outcomes.
- Raw reports and checksum-verified old/new PDFs are retained locally under
  `artifacts/dependencies/crypto-baselines-2026-10-25/`. They are not bundled in
  the repository. The JSON record is a review record, not a signed attestation.

veraPDF 1.31.166 confirmed six PDF/A-3b examples and rejected the deliberately
non-PDF/A control. Poppler 26.04.0 produced identical raster pages at 144 DPI for
all 11 pages, identical extracted text, and byte-identical extracted attachments.
The independent results apply to these exact input hashes only. They do not
establish business-invoice conformance or complete the separate secondary
validator/commercial approval gates.

This review ran on macOS with the x64 SDK 10.0.113 and runtime 10.0.13. It does
not certify other platform or native-library combinations.

## Future Reviews

The fixture test always writes a candidate manifest beside its generated PDFs.
It never rewrites the approved source manifest, including on mismatch or when
the source manifest is missing. Do not fix a failed test by rerunning it to
implicitly accept new hashes.

Use a fresh evidence directory and the intended patched runtime:

```bash
PVAD_EINVOICE_PDFA_OUTPUT_DIRECTORY="$PWD/artifacts/invoice-baseline-review/new-run" \
  dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj \
  --filter FullyQualifiedName~EInvoicePdfaFixtureTests
```

On mismatch, review `candidate-pdfa-fixtures.json`. Preserve previous inputs and
reports before proceeding. Validate every candidate using the independent
validator, including the negative control, and compare pages, text and attachments
with the previous checksum-verified inputs. The reviewed validator image is:

```text
ghcr.io/verapdf/cli@sha256:b334d330037bba9b641ff3f8b1acb29beadee9060b3028aa297d0b0f9393e17a
```

For example, validate a candidate without changing the active manifest:

```bash
PVAD_VERAPDF_IMAGE=ghcr.io/verapdf/cli@sha256:b334d330037bba9b641ff3f8b1acb29beadee9060b3028aa297d0b0f9393e17a \
PVAD_PDFA_REPORT_DIR="$PWD/artifacts/invoice-baseline-review/new-run/reports" \
  tools/pdfa/validate-pdfa.sh 3b \
  "$PWD/artifacts/invoice-baseline-review/new-run/factur-x-single-page.pdf"
```

Repeat for every fixture. The control must have a genuine non-compliant XML
verdict; a missing report or a validator/container failure is not a successful
negative control. Compare each old/new pair using the same Poppler version:
`pdftoppm -r 144` for raster pages, `pdftotext -layout` for text, and
`pdfdetach -saveall` for attachments. Check page counts and compare bytes or
SHA-256 values of every corresponding output. Do not compare only screenshots of
the first page.

Only then explicitly update the active hashes and corresponding review record.
The tests bind that record to both manifests and every old/new document hash, so
editing the active manifest alone is insufficient. Re-run the full regression
suite afterward. A servicing-release update may change compressed PDF bytes;
neither stable product versioning nor visual similarity alone approves new
validation evidence. The old runtime is not recommended for deployment.
