#!/usr/bin/env bash
set -euo pipefail

. "$(dirname "$0")/../configuration/environment.sh"
pvad_environment_alias PVAD_OWNER_PASSWORD PXA_OWNER_PASSWORD "${PVAD_OWNER_PASSWORD+x}" "${PVAD_OWNER_PASSWORD-}" "${PXA_OWNER_PASSWORD+x}" "${PXA_OWNER_PASSWORD-}"
pvad_environment_alias PVAD_USER_PASSWORD PXA_USER_PASSWORD "${PVAD_USER_PASSWORD+x}" "${PVAD_USER_PASSWORD-}" "${PXA_USER_PASSWORD+x}" "${PXA_USER_PASSWORD-}"

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
readonly TOOL_DIRECTORY="$ROOT/tools/pdf-trust-external-corpus"
readonly SOURCE="$TOOL_DIRECTORY/sources/trust-profile.html"
readonly FIXTURE_DIRECTORY="$ROOT/tests/PVAD.Importer.Tests/Fixtures/PdfTrustExternal"
readonly BASE_PDF="$ROOT/tests/PVAD.Importer.Tests/Fixtures/RedactionRecovery/chromium-encoded-glyphs.pdf"
readonly IMAGE="pvad-pdf-trust-external-tools:1"
readonly USER_PASSWORD="PXA-reader-ä漢字"
readonly OWNER_PASSWORD="PXA-owner-ßЖ"

for command in soffice docker dotnet; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Required command is missing: $command" >&2
    exit 69
  fi
done

if ! soffice --version | grep --quiet 'LibreOffice 26.2.3.2'; then
  echo "Expected LibreOffice 26.2.3.2 for pinned corpus generation." >&2
  exit 65
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$FIXTURE_DIRECTORY" "$work/pdfa" "$work/pdfua" "$work/crypto"

cp "$SOURCE" "$work/pdfa/libreoffice-pdfa2b.html"
cp "$SOURCE" "$work/pdfua/libreoffice-pdfua1.html"

soffice --headless \
  --convert-to 'pdf:writer_pdf_Export:{"SelectPdfVersion":{"type":"long","value":"2"},"UseTaggedPDF":{"type":"boolean","value":"true"}}' \
  --outdir "$work/pdfa" \
  "$work/pdfa/libreoffice-pdfa2b.html"

soffice --headless \
  --convert-to 'pdf:writer_pdf_Export:{"PDFUACompliance":{"type":"boolean","value":"true"},"UseTaggedPDF":{"type":"boolean","value":"true"}}' \
  --outdir "$work/pdfua" \
  "$work/pdfua/libreoffice-pdfua1.html"

cp "$work/pdfa/libreoffice-pdfa2b.pdf" "$FIXTURE_DIRECTORY/libreoffice-pdfa2b.pdf"
cp "$work/pdfua/libreoffice-pdfua1.pdf" "$FIXTURE_DIRECTORY/libreoffice-pdfua1.pdf"

docker build --tag "$IMAGE" "$TOOL_DIRECTORY"
docker run --rm \
  --volume "$ROOT:/work" \
  --volume "$work/crypto:/crypto" \
  --env "PVAD_USER_PASSWORD=$USER_PASSWORD" \
  --env "PVAD_OWNER_PASSWORD=$OWNER_PASSWORD" \
  "$IMAGE" \
  sh -ec 'qpdf --password-mode=unicode \
      --encrypt --user-password="$PVAD_USER_PASSWORD" --owner-password="$PVAD_OWNER_PASSWORD" \
      --bits=256 --modify=none --print=full -- \
      /work/tests/PVAD.Importer.Tests/Fixtures/RedactionRecovery/chromium-encoded-glyphs.pdf \
      /crypto/qpdf-aes256-unicode.pdf
    qpdf --password="$PVAD_USER_PASSWORD" --check /crypto/qpdf-aes256-unicode.pdf \
      > /crypto/qpdf-aes256-unicode-qpdf.txt 2>&1
    qpdf --password="$PVAD_OWNER_PASSWORD" --show-encryption /crypto/qpdf-aes256-unicode.pdf \
      >> /crypto/qpdf-aes256-unicode-qpdf.txt
    sed -i "s/[[:space:]]*$//" /crypto/qpdf-aes256-unicode-qpdf.txt'

docker run --rm \
  --volume "$ROOT:/work" \
  --volume "$work/crypto:/crypto" \
  "$IMAGE" \
  sh -ec 'openssl req -x509 -newkey rsa:2048 -nodes -days 30 \
      -subj "/CN=PXA External Corpus Signer/O=PXA Synthetic Tests/C=CH" \
      -keyout /crypto/signer.key.pem -out /crypto/signer.cert.pem >/dev/null 2>&1
    pyhanko sign addsig --field "1/40,40,240,100/SyntheticSignature" --use-pades \
      pemder --no-pass --key /crypto/signer.key.pem --cert /crypto/signer.cert.pem \
      /work/tests/PVAD.Importer.Tests/Fixtures/RedactionRecovery/chromium-encoded-glyphs.pdf \
      /crypto/pyhanko-pades-bb.pdf
    pyhanko sign validate --executive-summary --validation-time claimed \
      --no-revocation-check --trust /crypto/signer.cert.pem \
      /crypto/pyhanko-pades-bb.pdf > /crypto/pyhanko-pades-bb-validation.txt'

cp "$work/crypto/qpdf-aes256-unicode.pdf" "$FIXTURE_DIRECTORY/qpdf-aes256-unicode.pdf"
cp "$work/crypto/qpdf-aes256-unicode-qpdf.txt" "$FIXTURE_DIRECTORY/qpdf-aes256-unicode-qpdf.txt"
cp "$work/crypto/pyhanko-pades-bb.pdf" "$FIXTURE_DIRECTORY/pyhanko-pades-bb.pdf"
cp "$work/crypto/pyhanko-pades-bb-validation.txt" "$FIXTURE_DIRECTORY/pyhanko-pades-bb-validation.txt"

PVAD_VERAPDF_IMAGE="ghcr.io/verapdf/cli:v1.31.166" \
PVAD_PDFA_REPORT_DIR="$FIXTURE_DIRECTORY" \
  "$ROOT/tools/pdfa/validate-pdfa.sh" 2b "$FIXTURE_DIRECTORY/libreoffice-pdfa2b.pdf"
PVAD_VERAPDF_IMAGE="ghcr.io/verapdf/cli:v1.31.166" \
PVAD_PDFUA_REPORT_DIR="$FIXTURE_DIRECTORY" \
  "$ROOT/tools/pdfua/validate-pdfua.sh" "$FIXTURE_DIRECTORY/libreoffice-pdfua1.pdf"

dotnet run --project "$TOOL_DIRECTORY/PVAD.PdfTrustExternalCorpus.csproj" --no-restore

echo "External PDF Trust profiles generated. Review all changed binaries and reports."
