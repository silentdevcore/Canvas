# Serif And Mono Raster Review

The repository owner explicitly confirmed viewing and approving all three 200%
comparisons on 2026-11-14: `serif-bold`, `mono-bold`, `mono-bold-italic`.
The exact fixture, relevant font faces and four crops per case are hash-pinned
in `approval.json`. Normal and C# PDFs have identical pixels. Minor browser/PDF
raster differences are accepted only for these preserved cases; P2 measurements
and existing regression thresholds are unchanged. No untested-platform approval
or permission to replace evidence is implied.

Each source crop is 270 x 110 points. Display at 540 x 220 pixels to repeat the
200% review. The complete generated report is available locally under
`artifacts/font-family-parity/raster-review.html`.
