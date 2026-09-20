# PXA document fidelity

This tool keeps corpus scope and measured fidelity separate. It prevents synthetic fixtures,
standards-only files, or absent evidence from inflating the official migration rate.

```bash
node tools/document-fidelity/fidelity.mjs validate
node tools/document-fidelity/fidelity.mjs evaluate --output artifacts/document-fidelity
node --test tools/document-fidelity/fidelity.test.mjs
```

Replace reviewed baseline entries with fresh tool output when available:

```bash
node tools/document-fidelity/fidelity.mjs evaluate \
  --visual-parity artifacts/visual-parity/visual-parity-report.json \
  --word-fidelity tests/PXA.Export.Tests/Fidelity/artifacts/latest/fidelity-report.json \
  --output artifacts/document-fidelity
```

Use `--enforce` only after the manifest contains reviewed, redistributable, real-world migration
fixtures marked `officiallySupported`. An empty denominator fails enforcement and is reported as
`notMeasured` during normal evaluation.

The reviewed baseline records current evidence. Live adapters for Spreadsheet and provider
migrations remain tracked in
`checklists/PXA.Proven-Document-Fidelity.md`.
