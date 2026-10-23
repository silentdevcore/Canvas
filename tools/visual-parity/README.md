# Visual parity tools

The visual parity tools use deterministic local fixtures and pinned rendering
settings. `run.mjs` compares Designer surfaces with generated PDF output.

`pdf-trust-golden.mjs` covers the current PDF Trust visual contract:

- `signed`: digital signature appearance and page geometry
- `redaction-risk`: redaction-related content and safe rendering of risky input
- `archived`: PDF/A-2b output
- `accessible`: PDF/UA-1 output

`pdf-trust-operations.mjs` executes the real Trust services against deterministic
test documents and covers the operation outputs for:

- visible signature appearance
- applied redaction
- PDF/A-2b conversion
- PDF/UA-1 generation

Run the report locally with:

```bash
node tools/visual-parity/pdf-trust-golden.mjs
```

The default is report-only. CI can enforce the checked-in pixel baselines with:

```bash
PVAD_PDF_TRUST_VISUAL_ENFORCE=true node tools/visual-parity/pdf-trust-golden.mjs
```

Run operation output baselines with:

```bash
PVAD_PDF_TRUST_OPERATION_ENFORCE=true node tools/visual-parity/pdf-trust-operations.mjs
```

The command writes raster pages and a JSON report below
`artifacts/pdf-trust-visual/`. It never writes customer documents or trust
secrets. Updating a baseline requires an explicit reviewed change to
`test-assets/pdf-trust/visual-baseline.json`.

The golden runner protects the visual rendering of the checked-in Trust corpus.
The operation runner adds real service outputs for redaction, signing, PDF/A,
and PDF/UA. Reviewed reference images and visual diff masks remain a follow-up
milestone.

The operation runner additionally verifies that the real service outputs keep
the source page count and geometry, and protects their rasterized output
against checked-in baselines. For every page it writes a side-by-side comparison,
a 50/50 review overlay, a red pixel-diff mask and pixel-difference metrics below
`artifacts/pdf-trust-operations/<operation>/review/`. These artifacts are
report-only evidence: intentional changes such as removed redaction content
must be reviewed by a human and are not automatically treated as regressions.
