#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [[ -z "${PVAD_NATIVE_TEST_SESSION:-}" ]]; then
  exec node "$ROOT/tools/native-licensing/test-session.mjs" exec -- bash "$ROOT/tools/pdf-parser-fuzzer/run.sh" "$@"
fi
cd "$ROOT"

readonly REPORT_PATH="${PVAD_PDF_FUZZ_REPORT_PATH:-artifacts/pdf-fuzz/parser-fuzz-report.json}"

mkdir -p "$(dirname "$REPORT_PATH")"
dotnet build tools/pdf-parser-fuzzer/PVAD.PdfParserFuzzer.csproj --configuration Release --disable-build-servers -m:1
dotnet run \
  --project tools/pdf-parser-fuzzer/PVAD.PdfParserFuzzer.csproj \
  --configuration Release \
  --no-restore \
  --no-build \
  -- \
  --report "$REPORT_PATH"
node tools/pdf-parser-fuzzer/report.mjs "$REPORT_PATH"
