# OCR Table Samples

Place real table regression fixtures in this folder.

## Expected failing fixture

- `failing-table-01.png`: the original real image that currently fails table detection.
- `failing-table-01.ocr.json`: captured OCR line/word output for the image.

The test `ConvertAsync_RealFailingTableFixture_WhenPresent_ProducesTableAndDiagnostics`
is inert until both files are present. Once both files exist, it verifies that the
converter produces a `table` element and exposes rule/background/table diagnostics.

## OCR JSON format

```json
[
  {
    "text": "Item Qty Price",
    "bounds": { "x": 10, "y": 10, "width": 180, "height": 12 },
    "confidence": 0.95,
    "words": [
      {
        "text": "Item",
        "bounds": { "x": 10, "y": 10, "width": 38, "height": 12 },
        "confidence": 0.95
      }
    ]
  }
]
```
