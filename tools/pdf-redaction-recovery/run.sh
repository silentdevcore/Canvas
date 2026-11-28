#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [[ -z "${PVAD_NATIVE_TEST_SESSION:-}" ]]; then
  exec node "$ROOT/tools/native-licensing/test-session.mjs" exec -- bash "$ROOT/tools/pdf-redaction-recovery/run.sh" "$@"
fi
cd "$ROOT"

for tool in pdftotext pdfimages strings; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    printf 'Required recovery tool is missing: %s\n' "$tool" >&2
    exit 1
  fi
done

PVAD_RUN_PDF_RECOVERY_TESTS=1 dotnet test \
  tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj \
  --filter 'FullyQualifiedName~PdfRedactionExternalRecoveryTests' \
  -m:1
