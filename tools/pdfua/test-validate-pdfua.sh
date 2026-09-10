#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "$0")/../.." && pwd -P)"
readonly SCRIPT="$ROOT/tools/pdfua/validate-pdfua.sh"
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

PATH="$work/bin:$PATH" PXA_PDFUA_REPORT_DIR="$work/reports" \
  "$SCRIPT" "$work/pass.pdf"
grep -q 'isCompliant="true"' "$work/reports/pass-ua1-verapdf.xml"

set +e
PATH="$work/bin:$PATH" PXA_PDFUA_REPORT_DIR="$work/reports" \
  "$SCRIPT" "$work/fail.pdf" >/dev/null 2>&1
status=$?
set -e
[[ "$status" -eq 1 ]]
grep -q 'isCompliant="false"' "$work/reports/fail-ua1-verapdf.xml"

set +e
PATH="$work/bin:$PATH" PXA_PDFUA_REPORT_DIR="$work/reports" \
  "$SCRIPT" "$work/missing.pdf" >/dev/null 2>&1
status=$?
set -e
[[ "$status" -eq 1 ]]

echo "PDF/UA validator wrapper tests passed."
