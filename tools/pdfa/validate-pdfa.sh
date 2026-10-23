#!/usr/bin/env bash
set -euo pipefail

. "$(dirname "$0")/../configuration/environment.sh"
pvad_environment_alias PVAD_PDFA_REPORT_DIR PXA_PDFA_REPORT_DIR "${PVAD_PDFA_REPORT_DIR+x}" "${PVAD_PDFA_REPORT_DIR-}" "${PXA_PDFA_REPORT_DIR+x}" "${PXA_PDFA_REPORT_DIR-}"
pvad_environment_alias PVAD_VERAPDF_IMAGE PXA_VERAPDF_IMAGE "${PVAD_VERAPDF_IMAGE+x}" "${PVAD_VERAPDF_IMAGE-}" "${PXA_VERAPDF_IMAGE+x}" "${PXA_VERAPDF_IMAGE-}"

readonly IMAGE="${PVAD_VERAPDF_IMAGE:-ghcr.io/verapdf/cli:v1.31.166}"
readonly REPORT_DIRECTORY="${PVAD_PDFA_REPORT_DIR:-artifacts/pdfa}"

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

mkdir -p "$REPORT_DIRECTORY"
report_root="$(cd "$REPORT_DIRECTORY" && pwd)"

failed=0
for input in "$@"; do
  if [[ ! -f "$input" ]]; then
    echo "PDF does not exist: $input" >&2
    failed=1
    continue
  fi

  input_directory="$(cd "$(dirname "$input")" && pwd)"
  input_name="$(basename "$input")"
  report_name="${input_name%.pdf}-${profile}-verapdf.xml"
  report_path="$report_root/$report_name"

  docker run --rm --platform linux/amd64 \
    -v "$input_directory:/input:ro" \
    "$IMAGE" \
    --format xml \
    --flavour "$profile" \
    "/input/$input_name" > "$report_path"

  if grep -q 'isCompliant="true"' "$report_path" &&
     ! grep -q 'isCompliant="false"' "$report_path"; then
    echo "PASS $input $profile ($report_path)"
  else
    echo "FAIL $input $profile ($report_path)" >&2
    failed=1
  fi
done

exit "$failed"
