# Word vs PDF Fidelity Sample Pack

These samples are used by automated tests in tests/Canvas.Export.Tests/WordPdfFidelityTests.cs.

How it works:
- The test loads each JSON file in this folder as a DesignExportDto.
- It renders baseline PDF via DesignJsonMapper.
- It renders DOCX via WordDocumentExporter.
- If LibreOffice (soffice) is available, DOCX is converted to PDF and first-page geometry is compared.

Artifacts are written to:
- tests/Canvas.Export.Tests/Fidelity/artifacts/latest/<sample>/

Current sample set:
- text-table-basic.json
- richtext-link-list.json
- multipage-shared-elements.json
- form-elements.json
- links-and-note.json
- wide-table-report.json
- invoice-two-page.json
- contract-clauses.json
- checklist-audit.json
- dense-table-ledger.json
- image-and-caption.json
- landscape-section.json
