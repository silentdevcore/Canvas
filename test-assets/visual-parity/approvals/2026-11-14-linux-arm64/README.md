# Linux ARM64 Raster Review

The repository owner confirmed viewing and approving all thirteen Linux 200%
comparisons on 2026-11-14. The exact response was recorded in `approval.json`.

Eight cases belong to `font-families`: serif-bold, serif-bold-italic,
mono-regular, mono-bold, mono-italic, mono-bold-italic, mono-date and mono-number.
Five belong to `all-elements`: text, date, number, field and link.

Each case preserves backend, Canvas, Preview and generated C# crops with byte
and decoded-pixel hashes. Display each crop at twice its recorded width/height
to repeat the 200% review. Fixture and font hashes, original report summaries,
browser/platform identity and production input hashes are also pinned.

The original Linux backend/C# PDFs were generated from the recorded base commit.
The browser-only correction was replayed against those PDFs and subsequently
committed separately. This is not a fresh full candidate build. P2 measurements
remain P2; neither comparison thresholds nor rendering baselines are relaxed.

Approval covers only the exact preserved Linux/ARM64 evidence. It does not
approve changed pixels, remote Linux/x64 CI, Windows, native fallback variants
or customer authentication/persistence. GitHub execution was explicitly deferred
by the owner; this record is committed locally without push or pull request.

The original local comparison page remains at
`artifacts/font-linux/raster-review.html`.
