#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
readonly SCRIPT="$ROOT/tools/pdfa/validate-pdfa-secondary.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

printf '%%PDF-1.7\n' >"$work/fixture.pdf"

cat >"$work/pdfvalidator-pass" <<'SH'
#!/usr/bin/env bash
if [[ $# -eq 0 ]]; then
  echo "3-Heights PDF Validator Shell test 1.0"
  exit 3
fi
echo "The document conforms to the requested standard."
exit 0
SH
chmod +x "$work/pdfvalidator-pass"

PXA_PDFA_SECONDARY_VALIDATOR="$work/pdfvalidator-pass" \
PXA_PDFA_SECONDARY_REPORT_DIR="$work/reports" \
  "$SCRIPT" 2b "$work/fixture.pdf"

grep -q 'document conforms' "$work/reports/fixture-2b-3heights.txt"
grep -q '^profile=PDF/A-2b$' "$work/reports/fixture-2b-3heights-evidence.txt"
grep -q '^exitCode=0$' "$work/reports/fixture-2b-3heights-evidence.txt"

cat >"$work/pdfvalidator-fail" <<'SH'
#!/usr/bin/env bash
[[ $# -eq 0 ]] && exit 3
echo "The document does not conform to the requested standard."
exit 4
SH
chmod +x "$work/pdfvalidator-fail"

set +e
PXA_PDFA_SECONDARY_VALIDATOR="$work/pdfvalidator-fail" \
PXA_PDFA_SECONDARY_REPORT_DIR="$work/failure-reports" \
  "$SCRIPT" 2b "$work/fixture.pdf" >/dev/null 2>&1
status=$?
set -e
[[ "$status" -eq 1 ]]
grep -q '^exitCode=4$' "$work/failure-reports/fixture-2b-3heights-evidence.txt"

cat >"$work/pdfvalidator-license" <<'SH'
#!/usr/bin/env bash
[[ $# -eq 0 ]] && exit 3
echo "License error"
exit 10
SH
chmod +x "$work/pdfvalidator-license"

set +e
PXA_PDFA_SECONDARY_VALIDATOR="$work/pdfvalidator-license" \
PXA_PDFA_SECONDARY_REPORT_DIR="$work/license-reports" \
  "$SCRIPT" 3b "$work/fixture.pdf" >/dev/null 2>&1
status=$?
set -e
[[ "$status" -eq 78 ]]

echo "Secondary PDF/A validator wrapper tests passed."
