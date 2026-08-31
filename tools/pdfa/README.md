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
