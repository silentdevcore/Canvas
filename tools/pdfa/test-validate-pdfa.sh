#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "$0")/../.." && pwd -P)"
readonly SCRIPT="$ROOT/tools/pdfa/validate-pdfa.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir -p "$work/bin"
printf '%%PDF-1.7\n' > "$work/pass.pdf"
printf '%%PDF-1.7\n' > "$work/fail.pdf"

cat > "$work/bin/docker" <<'SH'
#!/usr/bin/env bash
input="${@: -1}"
if [[ "$input" == */pass.pdf ]]; then
  printf '<report><validationReport isCompliant="true"/></report>\n'
else
  printf '<report><validationReport isCompliant="false"/></report>\n'
fi
SH
chmod +x "$work/bin/docker"

PATH="$work/bin:$PATH" PVAD_PDFA_REPORT_DIR="$work/reports" \
  "$SCRIPT" 2b "$work/pass.pdf"
grep -q 'isCompliant="true"' "$work/reports/pass-2b-verapdf.xml"

set +e
PATH="$work/bin:$PATH" PVAD_PDFA_REPORT_DIR="$work/failure-reports" \
  "$SCRIPT" 3b "$work/fail.pdf"
status=$?
set -e
[[ "$status" -ne 0 ]]
grep -q 'isCompliant="false"' "$work/failure-reports/fail-3b-verapdf.xml"

set +e
PATH="$work/bin:$PATH" PVAD_PDFA_REPORT_DIR="$work/invalid-reports" \
  "$SCRIPT" 4u "$work/pass.pdf"
status=$?
set -e
[[ "$status" -eq 64 ]]

echo "PDF/A validator wrapper tests passed."
