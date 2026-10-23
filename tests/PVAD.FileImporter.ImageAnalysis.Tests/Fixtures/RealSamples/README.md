# Real ImageAnalysis Samples

Drop real validation samples into this directory as image/expectation pairs:

- `invoice-photo-01.jpg`
- `invoice-photo-01.expected.json`

Expectation files are intentionally small and coarse. Example:

```json
{
  "expectedTextFragments": ["Invoice", "Total", "25.00"],
  "minTextLineCount": 2,
  "minGlyphExactMatchRate": 0.7,
  "expectedElementCount": 12,
  "maxElementCountNoise": 1.5,
  "maxRuntimeMs": 5000
}
```

Set `IMAGE_ANALYSIS_WRITE_REAL_SAMPLE_OVERLAYS=1` to write debug overlays to
`TestResults/ImageAnalysisOverlays` during local test runs.
