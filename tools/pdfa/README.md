# PXA PDF/A Validation

`validate-pdfa.sh` runs the pinned official veraPDF CLI container and stores one
machine-readable XML report per input file.

```bash
PXA_PDFA_REPORT_DIR=/tmp/pxa-pdfa-reports \
  tools/pdfa/validate-pdfa.sh 2b document.pdf

tools/pdfa/validate-pdfa.sh 3b generated.pdf converted.pdf
```

The command exits unsuccessfully when a file is missing, the profile is not `2b` or
`3b`, Docker fails, or veraPDF reports a non-conforming document. Override the pinned
container only for an intentional validator upgrade:

```bash
PXA_VERAPDF_IMAGE=ghcr.io/verapdf/cli:v1.31.166 \
  tools/pdfa/validate-pdfa.sh 3b document.pdf
```

veraPDF is the automated independent validator. Production conformance approval also
requires a recorded pass from a second standards-based or commercial validator. Adobe
Acrobat Preflight is available for that manual gate, but merely opening a PDF in Acrobat
is not validation evidence.

## Secondary validator

`validate-pdfa-secondary.sh` runs a licensed 3-Heights PDF Validator Shell executable,
retains its detailed report, and records the profile, input SHA-256, validator version,
UTC validation time, and exit code as separate evidence.

```bash
PXA_PDFA_SECONDARY_VALIDATOR=/opt/pdf-tools.com/bin/x64/pdfvalidator \
PXA_PDFA_SECONDARY_REPORT_DIR=/tmp/pxa-pdfa-secondary \
  tools/pdfa/validate-pdfa-secondary.sh 2b document.pdf
```

The wrapper accepts only PDF/A-2b and PDF/A-3b. It distinguishes non-conformance from
license failure and never treats a missing validator, missing license, or processing error
as a pass. The validator is a separately licensed commercial dependency; it is not bundled
with PXA.
