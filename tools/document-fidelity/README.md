# PXA document fidelity

This tool keeps corpus scope and measured fidelity separate. It prevents synthetic fixtures,
standards-only files, or absent evidence from inflating the official migration rate.

```bash
node tools/document-fidelity/fidelity.mjs validate
node tools/document-fidelity/fidelity.mjs evaluate --output artifacts/document-fidelity
node --test tools/document-fidelity/fidelity.test.mjs
node --test tools/document-fidelity/real-world-review.test.mjs
```

Replace reviewed baseline entries with fresh tool output when available:

```bash
node tools/document-fidelity/fidelity.mjs evaluate \
  --visual-parity artifacts/visual-parity/visual-parity-report.json \
  --word-fidelity tests/PXA.Export.Tests/Fidelity/artifacts/latest/fidelity-report.json \
  --spreadsheet-fidelity artifacts/spreadsheet-fidelity/fidelity-report.json \
  --migration-fidelity test-assets/document-fidelity/migration-provider-smoke.json \
  --real-world-fidelity artifacts/document-fidelity/real-world/fidelity-report.json \
  --real-world-review artifacts/document-fidelity/real-world/review/real-world-review.json \
  --output artifacts/document-fidelity
```

Use `--enforce` only after the manifest contains reviewed, redistributable, real-world migration
fixtures marked `officiallySupported`. An empty denominator fails enforcement and is reported as
`notMeasured` during normal evaluation.

The migration adapter keeps PDF code, Spreadsheet code, and Report Designer results separate. The
checked-in provider-smoke report deliberately classifies every provider as review-required: registry
and execution coverage are useful evidence, but they do not prove operation-level fidelity. Real-world
reviewed fixtures must replace those outcomes before a provider enters the official percentage.

The checked-in real-world PDF, DOCX, and XLSX corpus is imported by the production adapters in
`RealWorldCorpusFidelityTests`. Its source URLs, OGL v3 attribution, and immutable SHA-256 hashes are
recorded in `test-assets/document-fidelity/real-world/corpus-sources.json`. Successful structural
imports remain `reviewRequired` until their visual and semantic output has been reviewed.

Generate the reproducible cross-format review packet after the .NET corpus test has emitted its
roundtrip files:

```bash
node tools/document-fidelity/real-world-review.mjs
```

The command converts Office sources with LibreOffice, extracts text with Poppler, compares three
representative pages per document with ImageMagick, and writes JSON, HTML, side-by-side images, and
diff images below `artifacts/document-fidelity/real-world/review`. Human decisions are versioned in
`product-metadata/document-fidelity/real-world-review-decisions.json`.
