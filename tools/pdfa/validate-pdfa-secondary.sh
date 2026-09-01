#!/usr/bin/env bash
set -euo pipefail

readonly VALIDATOR="${PXA_PDFA_SECONDARY_VALIDATOR:-pdfvalidator}"
readonly REPORT_DIRECTORY="${PXA_PDFA_SECONDARY_REPORT_DIR:-artifacts/pdfa-secondary}"

usage() {
  echo "Usage: $0 <2b|3b> <pdf> [pdf ...]" >&2
}

if [[ $# -lt 2 ]]; then
  usage
  exit 64
fi

profile="$1"
shift
if [[ "$profile" != "2b" && "$profile" != "3b" ]]; then
  echo "Unsupported PDF/A profile '$profile'; expected 2b or 3b." >&2
  exit 64
fi

if ! command -v "$VALIDATOR" >/dev/null 2>&1 && [[ ! -x "$VALIDATOR" ]]; then
  echo "Secondary PDF/A validator not found: $VALIDATOR" >&2
  echo "Set PXA_PDFA_SECONDARY_VALIDATOR to a licensed 3-Heights PDF Validator Shell executable." >&2
  exit 69
fi

mkdir -p "$REPORT_DIRECTORY"
report_root="$(cd "$REPORT_DIRECTORY" && pwd)"
validator_version="$($VALIDATOR 2>&1 | head -n 1 || true)"
failed=0

for input in "$@"; do
  if [[ ! -f "$input" ]]; then
    echo "PDF does not exist: $input" >&2
    failed=1
    continue
  fi

  input_path="$(cd "$(dirname "$input")" && pwd)/$(basename "$input")"
  input_name="$(basename "$input")"
  report_name="${input_name%.pdf}-${profile}-3heights.txt"
  report_path="$report_root/$report_name"
  metadata_path="${report_path%.txt}-evidence.txt"

  set +e
  "$VALIDATOR" -cl "pdfa-$profile" -rd -v "$input_path" >"$report_path" 2>&1
  status=$?
  set -e

  {
    echo "validator=3-Heights PDF Validator Shell"
    echo "validatorVersion=$validator_version"
    echo "profile=PDF/A-$profile"
    echo "input=$input_name"
    echo "inputSha256=$(shasum -a 256 "$input_path" | awk '{print $1}')"
    echo "validatedAtUtc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "exitCode=$status"
  } >"$metadata_path"

  case "$status" in
    0)
      echo "PASS $input PDF/A-$profile ($report_path)"
      ;;
    4)
      echo "FAIL $input PDF/A-$profile: document is not conforming ($report_path)" >&2
      failed=1
      ;;
    10)
      echo "BLOCKED $input PDF/A-$profile: validator license is missing or invalid ($report_path)" >&2
      exit 78
      ;;
    *)
      echo "ERROR $input PDF/A-$profile: validator exited with $status ($report_path)" >&2
      failed=1
      ;;
  esac
done

exit "$failed"
