#!/usr/bin/env bash
set -euo pipefail

. "$(dirname "$0")/../configuration/environment.sh"
pvad_environment_alias PVAD_PDF_FUZZ_REPORT_PATH PXA_PDF_FUZZ_REPORT_PATH "${PVAD_PDF_FUZZ_REPORT_PATH+x}" "${PVAD_PDF_FUZZ_REPORT_PATH-}" "${PXA_PDF_FUZZ_REPORT_PATH+x}" "${PXA_PDF_FUZZ_REPORT_PATH-}"

readonly REPORT_PATH="${PVAD_PDF_FUZZ_REPORT_PATH:-artifacts/pdf-fuzz/parser-fuzz-report.json}"

mkdir -p "$(dirname "$REPORT_PATH")"
dotnet run \
  --project tools/pdf-parser-fuzzer/PVAD.PdfParserFuzzer.csproj \
  --configuration Release \
  --no-restore \
  --no-build \
  -- \
  --report "$REPORT_PATH"
