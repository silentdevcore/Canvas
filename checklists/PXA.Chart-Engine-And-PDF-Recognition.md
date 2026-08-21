# PXA Professional Chart Engine and PDF Chart Recognition

## Goal

Deliver one versioned chart model and one visual contract across Designer, Live Preview,
PDF output, and PDF import. Recover supported charts from PDFs locally, preserve the
source visual, and never silently invent customer data.

## P0 - Model and Compatibility

- [x] Add the typed `ChartDefinition` version 2 contract to C#, TypeScript, OpenAPI, and JSON schemas.
- [x] Keep `chartType` and `chartData` readable through a deterministic legacy adapter.
- [x] Support bar, line, area, pie, doughnut, stacked bar, and combo charts.
- [x] Validate series, categories, axes, formatting, recognition metadata, and resource limits.
- [x] Add model and compatibility tests for legacy and version 2 documents.

## P0 - Designer and Preview

- [x] Use one shared Recharts renderer in the editing surface and Live Preview.
- [x] Render any supported number of series, negative values, null values, and empty states.
- [x] Add structured Data, Series, Appearance, Axes, Binding, and Advanced inspector sections.
- [x] Show recognition confidence, review state, source comparison, and source restore controls.
- [x] Remove the unused Chart.js dependency after a final source-reference check.
- [x] Localize all chart controls and accessibility labels in the six Designer languages.

## P0 - Backend and PDF Output

- [x] Move chart rendering out of `DesignJsonMapper` into `PXA.Infrastructure.Pdf`.
- [x] Render the seven core chart types as PDF vectors.
- [x] Keep a 3x SkiaSharp raster fallback for unsupported or failed vector cases.
- [x] Support multiple series, negative domains, nice ticks, legends, labels, and localized values.
- [x] Return an explicit empty state or diagnostic for invalid data; never render demo data.
- [x] Embed versioned PXA chart metadata and a content hash for lossless round trips.

## P0 - PDF Recognition

- [x] Add `off`, `safe`, and `review` PDF chart-recognition modes; default to `safe`.
- [x] Recover embedded PXA chart metadata before heuristic recognition.
- [ ] Detect vector bar, line, area, pie, doughnut, stacked bar, and combo candidates. Conservative bar recognition and line review candidates are implemented; the remaining foreign-PDF types require more fixtures and geometry inference.
- [ ] Use local raster analysis and OCR for embedded or rasterized chart candidates.
- [x] Apply confidence policy: automatic at 0.85+, review at 0.60-0.849, visual fallback below 0.60.
- [ ] Consume recognized primitives exactly once and retain the original visual through the asset store.
- [x] Reject uniform table-cell geometry and require chart structure before vector recognition. Expand the negative fixture corpus for logos and infographics.
- [x] Emit import diagnostics and mark PDF chart recognition as Beta in the feature catalog.

## P1 - Documentation and Delivery

- [x] Document chart creation, data editing, binding, PDF quality, import review, and limitations.
- [x] Add a customer-facing Minor release fragment without changing `VERSION`.
- [x] Add model, renderer, import, round-trip, false-positive, feature-gate, resource-limit, and
  maximum-point performance tests.
- [ ] Add golden visual-parity coverage and authenticated desktop/mobile chart screenshots.
- [x] Run the complete .NET suite, all 314 Designer tests, type-check, production build,
  Documentation tests, and Documentation build.

## Deferred

- [ ] Add native spreadsheet chart authoring and XLSX chart import/export in a separate milestone.
- [ ] Evaluate optional local model-assisted recognition after deterministic recognition is measured.

## Acceptance Criteria

- [x] The same chart has equivalent data, series order, colors, scales, and labels in every PXA surface.
- [x] Generated PDFs remain sharp at high zoom and print resolution.
- [x] PXA-generated charts re-import losslessly with confidence 1.0.
- [x] Foreign PDF charts never replace the original visual below the safe confidence threshold.
- [x] Recognition works without internet access or external customer-data transfer.
