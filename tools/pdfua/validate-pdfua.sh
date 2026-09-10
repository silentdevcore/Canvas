#!/usr/bin/env bash
set -euo pipefail

readonly IMAGE="${PXA_VERAPDF_IMAGE:-ghcr.io/verapdf/cli:v1.31.166}"
readonly REPORT_DIRECTORY="${PXA_PDFUA_REPORT_DIR:-artifacts/pdfua}"

usage() {
  echo "Usage: $0 <pdf> [pdf ...]" >&2
}

if [[ $# -lt 1 ]]; then
  usage
  exit 64
fi

mkdir -p "$REPORT_DIRECTORY"
report_root="$(cd "$REPORT_DIRECTORY" && pwd -P)"

failed=0
for input in "$@"; do
  if [[ ! -f "$input" ]]; then
    echo "PDF does not exist: $input" >&2
    failed=1
    continue
  fi

  input_directory="$(cd "$(dirname "$input")" && pwd -P)"
  input_name="$(basename "$input")"
  report_name="${input_name%.pdf}-ua1-verapdf.xml"
  report_path="$report_root/$report_name"

  docker run --rm --platform linux/amd64 \
    -v "$input_directory:/input:ro" \
    "$IMAGE" \
    --format xml \
    --flavour ua1 \
    "/input/$input_name" > "$report_path"

  if grep -q 'isCompliant="true"' "$report_path" &&
     ! grep -q 'isCompliant="false"' "$report_path"; then
    echo "PASS $input PDF/UA-1 ($report_path)"
  else
    echo "FAIL $input PDF/UA-1 ($report_path)" >&2
    failed=1
  fi
done

exit "$failed"
