# Visual parity tools

The visual parity tools use deterministic local fixtures and pinned rendering
settings. `run.mjs` compares Designer surfaces with generated PDF output.

Canvas and Live Preview delegate all 38 element types to
`pvad-designer/src/components/Rendering/DocumentElementRenderer.tsx`. Surface
placement and editor interaction controls stay separate from printable content.
The coverage contract checks that both consumers use the shared dispatcher and
that its types match the catalog, fixture pack and backend. Display-field
localization is also checked against the backend substitution list; stable IDs,
form names and binding paths are not localized.

Run the additional localized pack with:

```bash
PVAD_VISUAL_PARITY_FIXTURE=test-assets/visual-parity/shared-renderer.json node tools/visual-parity/run.mjs
```

The pack uses a French target language, synthetic properties, text/rich text,
form and signature labels, a note, and a local contained image. It exercises
normal PDF and real C# execution as well as both browser surfaces. Rendering
must leave the canonical fixture and its checksum unchanged. This is scoped
coverage, not certification of every image crop or arbitrary date format; see
`docs/guides/PVAD.Designer-Preview.md` for remaining limits.

The parity run uses packaged Liberation Sans faces and Noto Sans Arabic, not
network-loaded fonts. PDF and generated C# output retain Arabic logical text
through shaped glyphs and ActualText. In addition to aggregate pixel metrics,
the runner rejects missing/clipped RTL SVG text and verifies Arabic words in
both PDFs through `pdftotext`. Poppler's `pdftoppm`, `pdftotext`, and `pdfimages`,
plus ImageMagick, must therefore be available on PATH.

Footnote and endnote references remain at their element position; their body
uses the effective page height and the backend footer offset. The browser run
also rejects footer text clipped by an element wrapper or another ancestor.
Standard tables use the same cell matrix, relative column weights, paragraph
wrapping, minimum row heights, text positioning and single grid as the PDF.
Tables with complete canonical cell matrices use this shared renderer for
individual cell-style overrides as well. Sparse overrides preserve defaults;
explicit false bold/italic and zero padding remain meaningful. A shared edge
is painted once: explicit overrides beat defaults, wider explicit borders win,
and equal widths use the right/bottom cell. An explicit zero width suppresses
an adjacent default border. Incomplete legacy cell matrices retain their
fallback rendering and are not covered by this table parity guarantee.
Table dimension edits resize the cell matrix and preserve retained values,
column settings and in-range cell overrides in the same update.

Run the additional cell-style pack with:

```bash
PVAD_VISUAL_PARITY_FIXTURE=test-assets/visual-parity/styled-tables.json node tools/visual-parity/run.mjs
```

The pack covers cell typography, fills, padding, automatic row heights,
individual borders and inserted RDL matrix headers. It uses bundled sans-serif
faces. Serif and monospace families no longer fall back silently to Sans;
matching Liberation Serif/Mono files may be supplied, otherwise the PDF uses
native standard fonts. Cross-platform raster equivalence for these native
fonts is not certified by this pack. PDF/A font embedding also requires actual
matching font files. Per-cell styles are supported on explicit pages through
`PdfPage.DrawSimpleTable` and in automatic `PdfFlowContext.AddSimpleTable`
pagination. Flow measures the original rows before planning page breaks, maps
styles and row metrics into each chunk, repeats headers, and honors repeated
or final-only footers. Continuation markers reserve their own space and
alternating row fills retain their data-row parity. Rows stay whole; a row
that cannot fit a fresh page with its required repeated content is rejected
before any table chunk is painted.

After building `PVAD.Api.Tests`, generate the dedicated three-page flow versus
independently planned explicit-page comparison with:

```bash
node tools/visual-parity/flow-tables.mjs
```

This command runs the synthetic artifact test, rasterizes both PDFs at 72 DPI,
and writes `artifacts/flow-table-parity/report.html` and JSON alongside overlays
and pixel diffs. It requires exact zero changed pixels, not a relaxed threshold.
The test also requires byte-identical PDFs for its fixed explicit-page fixture.
The native C# workflow and limits are documented in
`docs/guides/PVAD.Styled-Table-Pagination.md`; Designer tables remain explicitly
positioned elements rather than acquiring implicit page creation.

Normal widget appearances, stored radio selection, default-family bold/italic
styles, TOC titles, and single-rotation watermark rendering have focused tests
in `VisualRenderingRegressionTests.cs` and `pdfVisualRendering.test.tsx`.
The same tests cover form-label offsets, required markers, note wrapping,
footnote position, centered shape borders, dashed lines, table resizing and
colored PDF text decorations. P2 findings remain visible in the reports;
matching aggregate thresholds does not make different glyph shapes, widget
baselines or underline extents automatically equivalent at 200% inspection.

After building `PVAD.Api.Tests`, inspect the two residual Latin bold-text crops
at a newly rendered 200% scale with:

```bash
node tools/visual-parity/bold-text-review.mjs
node --test tools/visual-parity/text-metrics.test.mjs tools/visual-parity/bold-text-approval.test.mjs
```

The command writes `artifacts/bold-text-review/report.html` and JSON. Browser
screenshots use two device pixels per PDF point; PDF vectors are rerasterized
at 144 DPI after supersampling, not enlarged from the 72-DPI reports. It also
checks the exact loaded font bytes, size/style, each glyph offset and advance,
and the existing 0.1-point text-layout guard. This deliberately covers plain
Latin text only, not shaped scripts or arbitrary ligature clusters.

The repository owner explicitly approved both current comparisons on
2026-11-13. The reviewed images and fixture/font hashes are stored in
`test-assets/visual-parity/approvals/2026-11-13-bold-text/`. The review runner
recognizes that approval only for the exact matching images, fixture hashes,
font, platform, browser version and scale. Changed evidence remains pending;
the runner never approves a new image or updates these pins. P2 measurements
and normal CI image thresholds remain unchanged. This approval is not a
cross-platform or universal-font rendering certification.
The archived PNG byte hashes protect the original review evidence. Repeat-run
approval uses exact decoded RGBA16 pixels plus dimensions, excluding only file
encoding metadata such as timestamps. A one-pixel change requires renewed
approval; there is no perceptual tolerance in that approval check.

The artifact generator also reads `text-metrics.json` back from the real PDF.
For unrotated, left-aligned non-RTL Text and Link runs, the browser comparison
independently checks the baseline, start position and actual unkerned advance
within 0.1 point. The JSON report retains both sets of measurements and the
embedded font's hash. This is an additional layout guard, not a replacement for
pixel comparisons or an increase of the approved image thresholds. Reused
artifacts must include the current metrics sidecar; regenerate older outputs.

Choice widgets have explicit normal appearances, clipped to their inner boxes,
and embed the packaged regular font when a loader is configured. The existing
PDF CID-width serialization remains unchanged to preserve pinned invoice evidence.
Form filling invalidates the old choice appearance so readers do not display
the old value. Link underlines use explicit PDF-positioned strokes, rather than
browser-specific decoration metrics. QR images use normal downsampling;
barcode image rendering remains pixelated and original symbol pixels remain
verified in both PDF outputs.
Aggregate pixel metrics alone are not a semantic correctness guarantee; review
the per-element images as well. Thresholds and approved Trust references must
not be changed just to conceal a rendering failure.

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

Native renderer baselines are platform-specific. The original
`test-assets/pdf-trust/visual-baseline.json` remains the macOS reference.
`visual-baseline.linux.json` is a separate owner-approved Ubuntu/Poppler
reference, not a tolerance increase or a replacement of the original images.
The source PDF hashes, page geometry, page count, and exact PNG hashes are
still enforced. Unknown platforms fail closed. A change to native font
substitution requires an explicit image review, not an automatic baseline update.

The same platform selection applies to operations: macOS uses the unchanged
`operation-visual-baseline.json`; Linux uses the separately owner-approved
`operation-visual-baseline.linux.json`. Both source and operation-output PNG
hashes are exact. The Linux signature and redaction images were reviewed
separately; the embedded-font PDF/A and PDF/UA pairs remain identical across
platforms. Every report records the reference file it used.

The operation runner additionally verifies that the real service outputs keep
the source page count and geometry, and protects their rasterized output
against checked-in baselines. For every page it writes a side-by-side comparison,
a 50/50 review overlay, a red pixel-diff mask and pixel-difference metrics below
`artifacts/pdf-trust-operations/<operation>/review/`. These artifacts are
report-only evidence: intentional changes such as removed redaction content
must be reviewed by a human and are not automatically treated as regressions.
