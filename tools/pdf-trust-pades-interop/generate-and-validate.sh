#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOL_DIRECTORY="$ROOT/tools/pdf-trust-pades-interop"
OUTPUT_DIRECTORY="${1:-$ROOT/artifacts/pdf-trust-pades-interop}"
IMAGE="pxa-dss-validator:6.4"

mkdir -p "$OUTPUT_DIRECTORY"
dotnet run --project "$TOOL_DIRECTORY/PXA.PdfTrustPadesCorpus.csproj" -- "$OUTPUT_DIRECTORY"
docker build --tag "$IMAGE" "$TOOL_DIRECTORY/dss-validator"

for profile in b-t b-lt b-lta; do
  case "$profile" in
    b-t) expected_profile="PAdES-BASELINE-T" ;;
    b-lt) expected_profile="PAdES-BASELINE-LT" ;;
    b-lta) expected_profile="PAdES-BASELINE-LTA" ;;
  esac
  docker run --rm \
    --network none \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=64m \
    --volume "$OUTPUT_DIRECTORY:/corpus" \
    "$IMAGE" \
    "/corpus/pxa-pades-$profile.pdf" \
    "/corpus/root-trust-anchor.cer" \
    "/corpus/root-current.crl" \
    "/corpus"

  simple_report="$OUTPUT_DIRECTORY/pxa-pades-$profile-dss-simple.xml"
  diagnostic_report="$OUTPUT_DIRECTORY/pxa-pades-$profile-dss-diagnostic.xml"
  grep -q "SignatureFormat=\"$expected_profile\"" "$simple_report"
  grep -q '<Indication>TOTAL_PASSED</Indication>' "$simple_report"
  grep -q "<SignatureFormat>$expected_profile</SignatureFormat>" "$diagnostic_report"
  grep -q '<StructuralValidation valid="true"/>' "$diagnostic_report"
done

echo "PXA PAdES profiles generated and validated with EU DSS 6.4 in $OUTPUT_DIRECTORY."
