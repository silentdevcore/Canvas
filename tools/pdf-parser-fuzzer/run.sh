#!/usr/bin/env bash
set -euo pipefail

readonly REPORT_PATH="${PXA_PDF_FUZZ_REPORT_PATH:-artifacts/pdf-fuzz/parser-fuzz-report.json}"

mkdir -p "$(dirname "$REPORT_PATH")"
dotnet run \
  --project tools/pdf-parser-fuzzer/PXA.PdfParserFuzzer.csproj \
  --configuration Release \
  --no-restore \
  --no-build \
  -- \
  --report "$REPORT_PATH"
