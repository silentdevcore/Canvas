#!/usr/bin/env bash
set -euo pipefail


readonly REPORT_PATH="${PVAD_PDF_FUZZ_REPORT_PATH:-artifacts/pdf-fuzz/parser-fuzz-report.json}"

mkdir -p "$(dirname "$REPORT_PATH")"
dotnet run \
  --project tools/pdf-parser-fuzzer/PVAD.PdfParserFuzzer.csproj \
  --configuration Release \
  --no-restore \
  --no-build \
  -- \
  --report "$REPORT_PATH"
node tools/pdf-parser-fuzzer/report.mjs "$REPORT_PATH"
