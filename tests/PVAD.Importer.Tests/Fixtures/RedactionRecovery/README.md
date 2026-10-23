# Independent PDF Redaction Recovery Corpus

These immutable fixtures are produced outside PXA and are consumed as hostile input by
the redaction recovery tests. Tests must never regenerate them through a PXA writer.

- `chromium-encoded-glyphs.pdf` was printed by Google Chrome from the matching HTML
  source. It exercises subset fonts, encoded glyphs, Flate-compressed content, and a
  transformed clipping group. The current safe result is `block`, not a redaction claim.
- `libreoffice-reusable-resources.pdf` was exported by LibreOffice from the matching HTML
  source. It exercises a second producer, repeated graphics, fonts, and compressed streams.
  PXA blocks it because the reusable resource cannot yet be removed with certainty.
- `malformed-compressed-stream.pdf` is a byte-offset-preserving copy of the Chromium
  fixture whose first 614-byte Flate payload was replaced with invalid data. PXA must block the
  undecodable compressed stream rather than issue a redacted file.

`corpus.json` records exact SHA-256 values, producers, source files, secrets used only for
recovery assertions, PXA-model regions, and expected outcomes.
Updating a fixture requires an explicit review of its source, producer version, hash,
region, and expected result. Do not include customer files or personal data.

The opt-in external suite requires `pdftotext`, `pdfimages`, and POSIX `strings`. Run it with:

```bash
PVAD_RUN_PDF_RECOVERY_TESTS=1 dotnet test tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj \
  --filter FullyQualifiedName~PdfRedactionExternalRecoveryTests
```
