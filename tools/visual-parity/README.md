# Visual parity tools

The visual parity tools use deterministic local fixtures and pinned rendering
settings. `run.mjs` compares Designer surfaces with generated PDF output.

`pdf-trust-golden.mjs` covers the current PDF Trust visual contract:

- `signed`: digital signature appearance and page geometry
- `redaction-risk`: redaction-related content and safe rendering of risky input
- `archived`: PDF/A-2b output
- `accessible`: PDF/UA-1 output

Run the report locally with:

```bash
node tools/visual-parity/pdf-trust-golden.mjs
```

The default is report-only. CI can enforce the checked-in pixel baselines with:

```bash
PXA_PDF_TRUST_VISUAL_ENFORCE=true node tools/visual-parity/pdf-trust-golden.mjs
```

The command writes raster pages and a JSON report below
`artifacts/pdf-trust-visual/`. It never writes customer documents or trust
secrets. Updating a baseline requires an explicit reviewed change to
`test-assets/pdf-trust/visual-baseline.json`.

This runner currently protects the visual rendering of the checked-in Trust
corpus. It does not claim that an applied redaction or a PDF/A/PDF/UA
conversion is visually equivalent yet; those operation-specific fixtures and
reviewed baseline images remain a follow-up milestone.
