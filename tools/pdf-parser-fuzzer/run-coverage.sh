#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
readonly PROJECT="$ROOT/tools/pdf-parser-fuzzer/PXA.PdfParserFuzzer.csproj"
readonly OUTPUT="${PXA_PDF_FUZZ_COVERAGE_OUTPUT:-$ROOT/artifacts/pdf-fuzz/coverage}"
readonly DURATION="${PXA_PDF_FUZZ_COVERAGE_SECONDS:-60}"
readonly TIMEOUT_MILLISECONDS="${PXA_PDF_FUZZ_CASE_TIMEOUT_MILLISECONDS:-2000}"
readonly MANAGED_HEAP_MEGABYTES=512
readonly PUBLISH="$OUTPUT/publish"
readonly INPUTS="$OUTPUT/inputs"
readonly FINDINGS="$OUTPUT/findings"

for command in afl-fuzz sharpfuzz dotnet; do
  if ! command -v "$command" >/dev/null 2>&1; then
    printf 'Required coverage-fuzzing command is missing: %s\n' "$command" >&2
    exit 69
  fi
done

case "$DURATION:$TIMEOUT_MILLISECONDS" in
  *[!0-9:]*|*::*|:*|*:)
    echo 'Fuzzing limits must be positive integers.' >&2
    exit 64
    ;;
esac
if (( DURATION < 1 || TIMEOUT_MILLISECONDS < 100 )); then
  echo 'Fuzzing limits are below the enforced minimum.' >&2
  exit 64
fi

if [[ -e "$PUBLISH" || -e "$INPUTS" || -e "$FINDINGS" ]]; then
  echo "Coverage output already exists; choose a fresh PXA_PDF_FUZZ_COVERAGE_OUTPUT directory." >&2
  exit 73
fi
mkdir -p "$PUBLISH" "$INPUTS" "$FINDINGS"
cp "$ROOT/tests/PXA.Importer.Tests/Fixtures/RedactionRecovery/chromium-encoded-glyphs.pdf" "$INPUTS/seed.pdf"

dotnet publish "$PROJECT" \
  --configuration Release \
  --output "$PUBLISH" \
  --artifacts-path "$OUTPUT/build"
DOTNET_ROLL_FORWARD=Major sharpfuzz "$PUBLISH/PXA.Importer.dll"

export AFL_NO_UI=1
export AFL_SKIP_CPUFREQ=1
export AFL_SKIP_BIN_CHECK=1
export DOTNET_GCHeapHardLimit=20000000
afl-fuzz \
  -V "$DURATION" \
  -m none \
  -t "$TIMEOUT_MILLISECONDS" \
  -i "$INPUTS" \
  -o "$FINDINGS" \
  -- "$PUBLISH/PXA.PdfParserFuzzer" --afl

crashes="$(find "$FINDINGS" -path '*/crashes/id:*' -type f | wc -l | tr -d ' ')"
hangs="$(find "$FINDINGS" -path '*/hangs/id:*' -type f | wc -l | tr -d ' ')"
printf '{\n  "schemaVersion": 1,\n  "durationSeconds": %s,\n  "managedHeapMegabytes": %s,\n  "timeoutMilliseconds": %s,\n  "crashes": %s,\n  "hangs": %s\n}\n' \
  "$DURATION" "$MANAGED_HEAP_MEGABYTES" "$TIMEOUT_MILLISECONDS" "$crashes" "$hangs" \
  > "$OUTPUT/coverage-fuzz-summary.json"

if (( crashes > 0 || hangs > 0 )); then
  echo "Coverage-guided PDF fuzzing found $crashes crashes and $hangs hangs." >&2
  exit 1
fi

echo "Coverage-guided PDF fuzzing completed without crashes or hangs."
