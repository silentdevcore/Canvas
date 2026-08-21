# Charts

PXA uses one versioned `ChartDefinition` for the Designer, Live Preview, API, and PDF renderer. The supported chart types are `bar`, `line`, `area`, `pie`, `doughnut`, `stackedBar`, and `combo`.

## Create and edit a chart

Add **Chart** from **Visual Elements**, select it, and use the Inspector sections:

1. **Data** edits categories and numeric values in a table.
2. **Series** adds, removes, orders, colors, and types series.
3. **Appearance** selects the chart type, title, legend, and data labels.
4. **Axes** controls value limits and grid lines.
5. **Binding** maps the chart to document data.
6. **Advanced** edits the validated version 2 JSON directly.

```json
{
  "schemaVersion": 2,
  "type": "combo",
  "title": "Quarterly performance",
  "categories": ["Q1", "Q2", "Q3"],
  "series": [
    { "id": "sales", "name": "Sales", "type": "bar", "values": [12, 18, 16], "color": "#2563eb" },
    { "id": "margin", "name": "Margin", "type": "line", "values": [3, 5, 4], "color": "#16a34a" }
  ],
  "legend": { "visible": true, "position": "bottom" },
  "dataLabels": { "visible": false }
}
```

Legacy `chartType` and `chartData` fields remain readable and are normalized to version 2. New integrations should send `chart`.

## PDF output

Core chart types are drawn as PDF vectors, so lines, text, and shapes stay sharp when zoomed or printed. A 3x raster fallback is used only when vector rendering cannot complete. Missing or invalid data produces an explicit empty state; PXA never inserts sample data into a customer document.

PXA-generated PDFs carry hashed chart metadata. Importing such a PDF restores the original editable definition with confidence `1.0` and suppresses duplicate visual primitives.

## Import charts from PDFs

The PDF importer accepts `chartRecognition=off`, `safe`, or `review`:

- `off` preserves ordinary PDF primitives and performs no heuristic recognition.
- `safe` converts only candidates with confidence `0.85` or higher.
- `review` also creates marked review candidates from confidence `0.60` through `0.849`.

Recognition runs locally. It first reads PXA metadata, then inspects vector geometry. Exact labeled bar charts can be recovered automatically; approximate vector bars and line paths require review. Uncertain content stays in its original visual form and emits a diagnostic.

PDF recognition is best effort. Raster/OCR recovery, original-asset restore for foreign PDFs, and reliable pie, doughnut, area, stacked, and combo inference remain limited in this release. Tables with uniform cells and decorative geometry are rejected by conservative structure checks.
