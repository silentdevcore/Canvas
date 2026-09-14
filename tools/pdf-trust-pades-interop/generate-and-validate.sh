#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOL_DIRECTORY="$ROOT/tools/pdf-trust-pades-interop"
OUTPUT_DIRECTORY="${1:-$ROOT/artifacts/pdf-trust-pades-interop}"
IMAGE="pxa-dss-validator:6.4"

mkdir -p "$OUTPUT_DIRECTORY"
OUTPUT_DIRECTORY="$(cd "$OUTPUT_DIRECTORY" && pwd)"
dotnet run --project "$TOOL_DIRECTORY/PXA.PdfTrustPadesCorpus.csproj" -- "$OUTPUT_DIRECTORY"
docker build --tag "$IMAGE" "$TOOL_DIRECTORY/dss-validator"

for profile in b-t b-lt b-lta; do
  case "$profile" in
    b-t) expected_profile="PAdES-BASELINE-T"; crl="/corpus/root-current.crl" ;;
    b-lt) expected_profile="PAdES-BASELINE-LT"; crl="-" ;;
    b-lta) expected_profile="PAdES-BASELINE-LTA"; crl="-" ;;
  esac
  docker run --rm \
    --network none \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=64m \
    --volume "$OUTPUT_DIRECTORY:/corpus" \
    "$IMAGE" \
    "/corpus/pxa-pades-$profile.pdf" \
    "/corpus/root-trust-anchor.cer" \
    "$crl" \
    "/corpus"

  simple_report="$OUTPUT_DIRECTORY/pxa-pades-$profile-dss-simple.xml"
  diagnostic_report="$OUTPUT_DIRECTORY/pxa-pades-$profile-dss-diagnostic.xml"
  grep -q "SignatureFormat=\"$expected_profile\"" "$simple_report"
  grep -q '<Indication>TOTAL_PASSED</Indication>' "$simple_report"
  grep -q "<SignatureFormat>$expected_profile</SignatureFormat>" "$diagnostic_report"
  grep -q '<StructuralValidation valid="true"/>' "$diagnostic_report"
done

negative_cases=(
  "revoked|root-trust-anchor.cer|root-signer-revoked.crl|REVOKED_NO_POE"
  "untrusted|untrusted-root.cer|-|NO_CERTIFICATE_CHAIN_FOUND"
  "revocation-unavailable|root-trust-anchor.cer|-|CERTIFICATE_CHAIN_GENERAL_FAILURE"
  "expired|expired-root.cer|expired-root-current.crl|OUT_OF_BOUNDS_NOT_REVOKED"
  "tampered-lt|root-trust-anchor.cer|-|CERTIFICATE_CHAIN_GENERAL_FAILURE"
  "tampered-lta|root-trust-anchor.cer|-|SIG_CRYPTO_FAILURE"
)

for definition in "${negative_cases[@]}"; do
  IFS='|' read -r scenario anchor crl expected_failure <<< "$definition"
  crl_argument="$crl"
  if [[ "$crl" != "-" ]]; then
    crl_argument="/corpus/$crl"
  fi
  docker run --rm \
    --network none \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=64m \
    --volume "$OUTPUT_DIRECTORY:/corpus" \
    "$IMAGE" \
    "/corpus/pxa-pades-$scenario.pdf" \
    "/corpus/$anchor" \
    "$crl_argument" \
    "/corpus"

  simple_report="$OUTPUT_DIRECTORY/pxa-pades-$scenario-dss-simple.xml"
  grep -q "<SubIndication>$expected_failure</SubIndication>" "$simple_report"
  if [[ "$scenario" == "tampered-lta" ]]; then
    grep -q '<SignatureValid>false</SignatureValid>' \
      "$OUTPUT_DIRECTORY/pxa-pades-$scenario-dss-diagnostic.xml"
  else
    ! grep -q '<Indication>TOTAL_PASSED</Indication>' "$simple_report"
  fi
done

node "$TOOL_DIRECTORY/build-negative-manifest.mjs" "$OUTPUT_DIRECTORY"

echo "PXA PAdES profiles and negative trust cases validated with EU DSS 6.4 in $OUTPUT_DIRECTORY."
