#!/usr/bin/env bash
set -euo pipefail

for tool in pdftotext pdfimages strings; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    printf 'Required recovery tool is missing: %s\n' "$tool" >&2
    exit 1
  fi
done

PXA_RUN_PDF_RECOVERY_TESTS=1 dotnet test \
  tests/PXA.Importer.Tests/PXA.Importer.Tests.csproj \
  --filter 'FullyQualifiedName~PdfRedactionExternalRecoveryTests' \
  -m:1
