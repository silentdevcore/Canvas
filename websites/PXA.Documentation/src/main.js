import './site.css';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';

const editorSections = [
  {
    title: 'Designer',
    status: 'Ready',
    text: 'Create and inspect PXA document templates in the visual designer, then preview output and review JSON.',
  },
  {
    title: 'Templates',
    status: 'Preview',
    text: 'Understand pages, shared elements, bindings, repeats, and template structure before connecting data.',
  },
  {
    title: 'Elements',
    status: 'Preview',
    text: 'Use text, tables, charts, images, forms, shapes, and layout primitives consistently across templates.',
  },
  {
    title: 'PDF Viewer',
    status: 'Preview',
    text: 'Review PDFs, forms, annotations, and browser-side inspection workflows connected to demos.',
  },
  {
    title: 'Spreadsheet',
    status: 'Preview',
    text: 'Import, map, edit, and export workbook-driven data flows for document automation.',
  },
  {
    title: 'Importer',
    status: 'Preview',
    text: 'Normalize incoming PDF, Office, image, and document files before migration or generation.',
  },
  {
    title: 'Export',
    status: 'Preview',
    text: 'Generate final outputs, downloadable artifacts, JSON, and code-oriented handoff files.',
  },
];

const editorDocs = [
  {
    title: 'Designer',
    status: 'Ready',
    purpose:
      'Use PXA Designer as the visual workspace for creating, inspecting, and validating document templates before they are generated or migrated into application workflows.',
    whenToUse: [
      'You need to create or inspect a document template visually.',
      'You want to preview migrated report output before committing it to code.',
      'You need JSON inspection for handoff between design and implementation.',
    ],
    concepts: ['Template canvas', 'Pages and margins', 'Preview output', 'Design JSON', 'Migration handoff'],
    tasks: ['Open the live designer', 'Create or load a template', 'Preview output', 'Inspect JSON', 'Open a migrated report in the designer'],
    related: [
      { label: 'Live designer', href: siteLinks.designer },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Master-detail demo', href: `${siteLinks.demo}#demo/master-detail-report` },
    ],
  },
  {
    title: 'Templates',
    status: 'Preview',
    purpose:
      'Templates define the reusable document model: page setup, shared content, bindings, variables, repeats, and validation expectations.',
    whenToUse: [
      'You are building reusable layouts for invoices, reports, statements, or receipts.',
      'You need to connect structured data to document output.',
      'You want shared headers, footers, or repeated sections across pages.',
    ],
    concepts: ['Page settings', 'Margins', 'Shared elements', 'Bindings', 'Repeats', 'Validation'],
    tasks: ['Define page size and margins', 'Add shared header or footer content', 'Bind fields to data', 'Validate required template structure'],
    related: [
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'Booking receipt demo', href: `${siteLinks.demo}#demo/booking-receipt` },
      { label: 'Open designer', href: siteLinks.designer },
    ],
  },
  {
    title: 'Elements',
    status: 'Preview',
    purpose:
      'Elements are the building blocks of a PXA template: text, images, tables, charts, shapes, lines, form controls, and layout primitives.',
    whenToUse: [
      'You need to place content precisely on a document page.',
      'You are mapping report items from another designer into PXA.',
      'You want consistent styling and layout behavior across generated output.',
    ],
    concepts: ['Text elements', 'Images', 'Tables', 'Charts', 'Shapes and lines', 'Forms', 'Absolute positioning'],
    tasks: ['Add and position elements', 'Style text and tables', 'Map charts from report designers', 'Use lines and shapes for report fidelity'],
    related: [
      { label: 'Element Reference', href: '#element-reference' },
      { label: 'Text element', href: '#text-block-element' },
      { label: 'Chart report demo', href: `${siteLinks.demo}#demo/chart-report` },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Report migration guide', href: '#report-designer-migration' },
    ],
  },
  {
    title: 'PDF Viewer',
    status: 'Preview',
    purpose:
      'The PDF Viewer supports browser-side review workflows for generated or imported PDFs, including forms, annotations, and inspection scenarios.',
    whenToUse: [
      'You need to review generated PDF output before release.',
      'You want to inspect forms and annotations in a browser workflow.',
      'You are tracking viewer parity against established PDF viewer products.',
    ],
    concepts: ['PDF preview', 'Forms', 'Annotations', 'Review tools', 'Viewer parity'],
    tasks: ['Open a generated PDF preview', 'Inspect form fields', 'Review annotation workflows', 'Track viewer feature gaps'],
    related: [
      { label: 'PDF Viewer product page', href: companyPage('products/pdf-viewer') },
      { label: 'Viewer demo', href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms` },
      { label: 'Feature gaps checklist', href: '../../checklists/PdfTools-WebViewer-Feature-Gaps.md' },
    ],
  },
  {
    title: 'Spreadsheet',
    status: 'Preview',
    purpose:
      'Spreadsheet workflows connect workbook data, sheets, formulas, and mappings to document automation and export scenarios.',
    whenToUse: [
      'Your document output depends on workbook data.',
      'You need to import, map, or inspect spreadsheet structures.',
      'You are planning spreadsheet provider migration or export workflows.',
    ],
    concepts: ['Workbook import', 'Sheets', 'Cells', 'Formulas', 'Data mapping', 'Export flows'],
    tasks: ['Import workbook data', 'Inspect mapped sheets', 'Plan formula handling', 'Connect workbook data to document output'],
    related: [
      { label: 'Spreadsheet product page', href: companyPage('products/spreadsheet') },
      { label: 'Spreadsheet demo', href: `${siteLinks.demo}#demo/spreadsheet-import-export` },
      { label: 'Migration guide', href: '#spreadsheet-code-migration' },
    ],
  },
  {
    title: 'Importer',
    status: 'Preview',
    purpose:
      'Importer workflows normalize incoming files so PDF, Office, image, and document inputs can enter designer, migration, or generation flows.',
    whenToUse: [
      'You need to accept existing customer or internal files.',
      'You want diagnostics for files that cannot map cleanly.',
      'You are preparing imported content for designer or migration handoff.',
    ],
    concepts: ['File detection', 'Normalization', 'Importer diagnostics', 'Format-specific importers', 'Designer handoff'],
    tasks: ['Choose an input format', 'Normalize the file', 'Review import diagnostics', 'Send imported content to Designer or Migration'],
    related: [
      { label: 'Importer product page', href: companyPage('products/importer') },
      { label: 'Importer demo', href: `${siteLinks.demo}#demo/file-importer-flow` },
      { label: 'PXA.Importer', href: '#pxa-importer' },
    ],
  },
  {
    title: 'Export',
    status: 'Preview',
    purpose:
      'Export workflows turn designs, migrated reports, and generated document models into JSON, PDF output, demo artifacts, or code-oriented handoff files.',
    whenToUse: [
      'You need to download generated output or design JSON.',
      'You want to compare input, output, and source artifacts from demos.',
      'You need handoff artifacts for implementation or review.',
    ],
    concepts: ['Design JSON', 'PDF output', 'Demo artifacts', 'Code handoff', 'Download flows'],
    tasks: ['Export design JSON', 'Download generated output', 'Compare demo input and output', 'Use exported artifacts for implementation handoff'],
    related: [
      { label: 'Demo examples', href: '#demo-examples' },
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'API Reference', href: '#api-reference' },
    ],
  },
];

const commonElementAttributes = [
  ['id', 'string', 'Unique id of the element inside the page. Generated by the designer.'],
  ['type', 'string', 'Element kind, for example "text", "image", "table", "chart", or "field".'],
  ['x / y', 'number', 'Position in page units from the top-left page corner. Defaults to points.'],
  ['width / height', 'number', 'Element box size in page units.'],
  ['name', 'string', 'Optional human-readable label for selection, search, and handoff.'],
  ['hidden', 'boolean', 'Keeps the element in the design but hides it from output.'],
  ['locked', 'boolean', 'Prevents accidental movement or editing in the designer.'],
  ['binding', 'string', 'Data path used to replace the value at render time.'],
  ['expression', 'string', 'Template expression evaluated during rendering.'],
  ['visibleExpression', 'string', 'Condition that decides whether the element is rendered.'],
  ['style', 'object', 'CSS-like style map. Unsupported keys are ignored by renderers.'],
];

const commonStyleAttributes = [
  ['fontSize', 'number', 'Font size in points.'],
  ['fontFamily', 'string', 'Font family name, for example Arial or Times New Roman.'],
  ['fontWeight', 'string', 'normal or bold.'],
  ['fontStyle', 'string', 'normal or italic.'],
  ['color', 'string', 'Text or foreground color as hex, rgb(), hsl(), or CSS color name.'],
  ['textAlign', 'string', 'left, center, right, or justify where supported.'],
  ['backgroundColor', 'string', 'Element fill/background color.'],
  ['borderColor / borderWidth', 'string / number', 'Border color and thickness.'],
  ['borderRadius', 'number', 'Corner radius in points where supported.'],
  ['padding', 'number', 'Inner spacing in points.'],
  ['lineHeight', 'number', 'Line height multiplier for text-like elements.'],
  ['opacity', 'number', 'Opacity from 0 to 1 where supported.'],
  ['rotation', 'number', 'Rotation in degrees.'],
];

const elementReferenceDocs = [
  {
    title: 'Text Block',
    type: 'text',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Single- or multi-line text for headings, labels, paragraphs, totals, and migrated report textboxes.',
    addSteps: ['Open PXA Designer.', 'In the toolbar open Text Elements.', 'Click Text.', 'Drag the element to the required position and resize the box.', 'Edit Content and typography in the properties panel.'],
    usage: ['Static label such as "Invoice".', 'Dynamic value with {{Customer.Name}} tokens.', 'Heading with headingLevel for Table of Contents generation.', 'Multi-line paragraph when the box height and lineHeight allow wrapping.'],
    attributes: [
      ['content', 'string', 'Text to render. Can contain {{tokens}}.'],
      ['headingLevel', '1 | 2 | 3', 'Marks text as a heading for the TOC element.'],
      ['style.fontSize', 'number', 'Font size in points.'],
      ['style.fontFamily', 'string', 'Font family.'],
      ['style.fontWeight', 'normal | bold', 'Text weight.'],
      ['style.fontStyle', 'normal | italic', 'Italic handling.'],
      ['style.color', 'string', 'Text color.'],
      ['style.textAlign', 'left | center | right | justify', 'Horizontal alignment inside the box.'],
      ['style.lineHeight', 'number', 'Useful for multi-line text.'],
    ],
    example: `{
  "id": "invoice-title",
  "type": "text",
  "x": 40,
  "y": 40,
  "width": 300,
  "height": 32,
  "content": "Invoice {{Number}}",
  "style": {
    "fontSize": 18,
    "fontWeight": "bold",
    "color": "#111827"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Text',
      properties: ['Content', 'Font family', 'Font size', 'Weight', 'Alignment', 'Line height'],
      preview: 'Invoice 1001',
    },
  },
  {
    title: 'Rich Text',
    type: 'richtext',
    category: 'Text Elements',
    status: 'Ready',
    description: 'HTML-formatted text for paragraphs with bold, italic, lists, and inline emphasis.',
    addSteps: ['Open Text Elements.', 'Click Rich Text.', 'Paste or edit restricted HTML content.', 'Resize the element to fit the expected text flow.'],
    usage: ['Terms paragraphs.', 'Formatted notes.', 'Imported DOCX/HTML text blocks.', 'Mixed inline styles that should stay together.'],
    attributes: [
      ['htmlContent', 'string', 'Restricted inline HTML rendered as rich text.'],
      ['style.fontSize', 'number', 'Base font size for the block.'],
      ['style.color', 'string', 'Default text color.'],
      ['style.lineHeight', 'number', 'Paragraph line height.'],
    ],
    example: `{
  "id": "payment-note",
  "type": "richtext",
  "x": 40,
  "y": 92,
  "width": 360,
  "height": 80,
  "htmlContent": "<p>Payment due within <b>30 days</b>.</p>"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Rich Text',
      properties: ['HTML content', 'Base font size', 'Color', 'Line height'],
      preview: 'Payment due within 30 days.',
    },
  },
  {
    title: 'Image',
    type: 'image',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Raster or vector image element for logos, signatures, imported images, stamps, and product visuals.',
    addSteps: ['Open Visual Elements.', 'Click Image.', 'Set the image URL or data URI in Content.', 'Choose fit mode and crop settings when needed.'],
    usage: ['Company logo in a header.', 'Imported PDF/image content.', 'Product picture in a quote.', 'Image watermark source.'],
    attributes: [
      ['content', 'string', 'Image URL or data URI.'],
      ['fitMode', 'contain | cover | fill | none', 'How the image fits the element box.'],
      ['preserveAspectRatio', 'boolean', 'Keeps image proportions when resizing.'],
      ['cropX / cropY / cropWidth / cropHeight', 'number', 'Optional crop rectangle.'],
      ['focalX / focalY', 'number', 'Focal point for cover/crop workflows.'],
    ],
    example: `{
  "id": "logo",
  "type": "image",
  "x": 40,
  "y": 28,
  "width": 120,
  "height": 48,
  "content": "https://example.com/logo.png",
  "fitMode": "contain",
  "preserveAspectRatio": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Image',
      properties: ['Image URL/data URI', 'Fit mode', 'Crop', 'Aspect ratio'],
      preview: 'Logo image box',
    },
  },
  {
    title: 'Table',
    type: 'table',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Fixed-column table with header rows, cell data, column widths, per-cell styling, and optional repeated data.',
    addSteps: ['Open Shapes & Layout.', 'Click Table.', 'Edit rows and columns through cellData.', 'Set headerRow, column widths, alignment, and zebra styling.', 'Use repeat when rows come from a dataset.'],
    usage: ['Invoice line items.', 'Report detail rows.', 'Summary totals.', 'Migrated RDL tablix/table output.'],
    attributes: [
      ['cellData', 'string[][]', 'Rows and cells. Cells may contain {{tokens}}.'],
      ['columnWidths', 'number[]', 'Per-column widths.'],
      ['columnAlignments', 'left | center | right[]', 'Per-column text alignment.'],
      ['headerRow', 'boolean', 'Treats first row as table header.'],
      ['footerRow', 'boolean', 'Treats last row as footer/total row.'],
      ['zebraEnabled / zebraColor', 'boolean / string', 'Alternating row backgrounds.'],
      ['cellStyles', 'object[]', 'Sparse cell styles addressed by row and col.'],
      ['repeat', 'object', 'Dataset repeat configuration for data-driven rows.'],
    ],
    example: `{
  "id": "items",
  "type": "table",
  "x": 40,
  "y": 180,
  "width": 500,
  "height": 120,
  "headerRow": true,
  "cellData": [
    ["Item", "Qty", "Total"],
    ["{{Name}}", "{{Qty}}", "{{Total}}"]
  ],
  "columnAlignments": ["left", "right", "right"]
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Table',
      properties: ['Rows/cells', 'Header row', 'Column widths', 'Cell styles', 'Repeat'],
      preview: 'Item | Qty | Total',
    },
  },
  {
    title: 'Chart',
    type: 'chart',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Bar, line, or pie chart rendered from inline chart data. Used heavily by report-designer migrations.',
    addSteps: ['Open Visual Elements.', 'Click Chart.', 'Select chartType.', 'Paste chartData JSON.', 'Resize the chart area and preview output.'],
    usage: ['Sales by period.', 'Report dashboard charts.', 'Migrated RDL/DevExpress/Jasper chart placeholders.', 'Small analytical visuals in generated documents.'],
    attributes: [
      ['chartType', 'bar | line | pie', 'Chart kind.'],
      ['chartData', 'object', 'Labels and datasets in Chart.js-style shape.'],
      ['style.backgroundColor', 'string', 'Optional chart background.'],
      ['style.color', 'string', 'Text/foreground hint.'],
    ],
    example: `{
  "id": "sales-chart",
  "type": "chart",
  "x": 40,
  "y": 340,
  "width": 320,
  "height": 200,
  "chartType": "bar",
  "chartData": {
    "labels": ["Q1", "Q2"],
    "datasets": [{ "label": "Sales", "data": [10, 20] }]
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Chart',
      properties: ['Chart type', 'Chart data JSON', 'Size', 'Style'],
      preview: 'Bar chart preview',
    },
  },
  {
    title: 'Line and Shapes',
    type: 'line',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Layout primitives for dividers, boxes, circles, backgrounds, report separators, and visual grouping.',
    addSteps: ['Open Shapes & Layout.', 'Choose Line, Shape, Rectangle, or Circle.', 'Place and resize on the canvas.', 'Set border, fill, opacity, and rotation.'],
    usage: ['Receipt separators.', 'Report boxes.', 'Background bands.', 'Migrated XRLine/RDL Line/FastReport LineObject output.'],
    attributes: [
      ['type', 'line | shape | rect | circle', 'Specific primitive type.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke width.'],
      ['style.backgroundColor', 'string', 'Fill color for boxes/shapes.'],
      ['style.opacity', 'number', 'Transparency from 0 to 1.'],
      ['style.rotation', 'number', 'Rotation in degrees.'],
    ],
    example: `{
  "id": "section-rule",
  "type": "line",
  "x": 40,
  "y": 156,
  "width": 500,
  "height": 1,
  "style": {
    "borderColor": "#9ca3af",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Line / Rectangle / Circle',
      properties: ['Stroke color', 'Stroke width', 'Fill', 'Opacity', 'Rotation'],
      preview: 'Horizontal divider line',
    },
  },
  {
    title: 'Form Fields',
    type: 'field',
    category: 'Form Elements',
    status: 'Preview',
    description: 'Interactive form controls: text fields, text areas, checkboxes, radio groups, dropdowns, option lists, buttons, and signatures.',
    addSteps: ['Open Form Elements.', 'Choose Field, Checkbox, Dropdown, Radio, Button, or Signature.', 'Set fieldName and visible label.', 'Configure options or required state.', 'Preview/export through PDF/Word-capable workflows.'],
    usage: ['Customer input forms.', 'Approval checklists.', 'Signature placeholders.', 'PDF viewer form review workflows.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field identifier.'],
      ['fieldLabel', 'string', 'Visible label.'],
      ['placeholder', 'string', 'Placeholder text for text input.'],
      ['required', 'boolean', 'Marks input as required.'],
      ['options', 'string[]', 'Dropdown, option list, or radio values.'],
      ['selectedValue', 'string', 'Initial selected value.'],
      ['checkState', 'checked | cross | dot | empty', 'Checkbox/checkmark visual state.'],
      ['buttonAction', 'string', 'URL or internal action for button elements.'],
      ['signatureLabel', 'string', 'Caption below signature line.'],
    ],
    example: `{
  "id": "customer-name",
  "type": "field",
  "x": 40,
  "y": 420,
  "width": 240,
  "height": 28,
  "fieldLabel": "Customer name",
  "fieldName": "customerName",
  "placeholder": "Enter name",
  "required": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Field / Checkbox / Dropdown / Signature',
      properties: ['Field name', 'Label', 'Placeholder', 'Required', 'Options'],
      preview: 'Customer name input',
    },
  },
  {
    title: 'Date',
    type: 'date',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Static, render-time, or bound date value with locale and timezone-aware formatting.',
    addSteps: ['Open Text Elements.', 'Click Date.', 'Choose dateMode.', 'Set dateFormat, locale, timezone, and fallback text as needed.'],
    usage: ['Invoice date.', 'Generated-at timestamp.', 'Bound order date.', 'Locale-aware report date.'],
    attributes: [
      ['dateMode', 'static | render | binding', 'Controls whether the value is fixed, generated at render time, or read from binding.'],
      ['dateFormat', 'string', 'Format pattern such as dd.MM.yyyy.'],
      ['locale', 'string', 'BCP-47 locale such as de-DE.'],
      ['timezone', 'string', 'IANA timezone such as Europe/Berlin.'],
      ['fallbackText', 'string', 'Shown when no value resolves.'],
    ],
    example: `{
  "id": "invoice-date",
  "type": "date",
  "x": 420,
  "y": 40,
  "width": 120,
  "height": 20,
  "dateMode": "render",
  "dateFormat": "dd.MM.yyyy",
  "locale": "de-DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Date',
      properties: ['Date mode', 'Format', 'Locale', 'Timezone', 'Fallback'],
      preview: '15.07.2026',
    },
  },
  {
    title: 'Page Number',
    type: 'pagenumber',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Automatic page number element for current page, total pages, page-of-total, roman, or alphabetic numbering.',
    addSteps: ['Open Text Elements.', 'Click Page Number.', 'Choose numberingFormat.', 'Set prefix, suffix, and startNumber when needed.'],
    usage: ['Footer page labels.', 'Page X of Y.', 'Appendix pages with roman numbering.', 'Migrated report footer page info.'],
    attributes: [
      ['numberingFormat', 'current | total | pageOfTotal | roman | alphabetic', 'What the element displays.'],
      ['startNumber', 'number', 'Number assigned to the first page.'],
      ['prefix', 'string', 'Text before the generated number.'],
      ['suffix', 'string', 'Text after the generated number.'],
    ],
    example: `{
  "id": "page-number",
  "type": "pagenumber",
  "x": 430,
  "y": 810,
  "width": 90,
  "height": 16,
  "numberingFormat": "pageOfTotal",
  "prefix": "Page "
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Page Number',
      properties: ['Format', 'Start number', 'Prefix', 'Suffix'],
      preview: 'Page 1 of 3',
    },
  },
  {
    title: 'Number',
    type: 'number',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Formatted numeric value for currency, percent, decimal, scientific, or ordinal output.',
    addSteps: ['Open Text Elements.', 'Click Number.', 'Set numberValue or binding.', 'Choose numberStyle, decimals, currency, and locale.'],
    usage: ['Currency totals.', 'Percent KPIs.', 'Localized amounts.', 'Bound numeric report fields.'],
    attributes: [
      ['numberValue', 'number', 'Value to format.'],
      ['numberStyle', 'decimal | currency | percent | scientific | ordinal', 'Formatting style.'],
      ['numberDecimals', 'number', 'Fraction digits.'],
      ['numberCurrency', 'string', 'ISO currency code such as EUR.'],
      ['numberLocale', 'string', 'Locale for grouping and decimal separators.'],
    ],
    example: `{
  "id": "total",
  "type": "number",
  "x": 430,
  "y": 300,
  "width": 110,
  "height": 20,
  "numberValue": 1234.5,
  "numberStyle": "currency",
  "numberCurrency": "EUR",
  "numberLocale": "de-DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Number',
      properties: ['Value', 'Style', 'Decimals', 'Currency', 'Locale'],
      preview: '1.234,50 EUR',
    },
  },
  {
    title: 'Table of Contents',
    type: 'toc',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Generated table of contents based on text elements that define headingLevel.',
    addSteps: ['Create heading Text elements and set headingLevel.', 'Open Text Elements.', 'Click Table of Contents.', 'Set TOC title, page numbers, leader dots, and heading level range.'],
    usage: ['Long documents.', 'Books and manuals.', 'Generated report packages.', 'Word/PDF navigation output.'],
    attributes: [
      ['tocTitle', 'string', 'Heading shown above generated entries.'],
      ['tocShowPageNumbers', 'boolean', 'Shows page numbers.'],
      ['tocShowLeaderDots', 'boolean', 'Shows dotted leaders.'],
      ['tocMinLevel / tocMaxLevel', 'number', 'Heading levels included.'],
      ['tocPlacement', 'beginning | end', 'Where generated TOC pages are inserted.'],
    ],
    example: `{
  "id": "contents",
  "type": "toc",
  "x": 40,
  "y": 80,
  "width": 460,
  "height": 220,
  "tocTitle": "Contents",
  "tocShowPageNumbers": true,
  "tocShowLeaderDots": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Table of Contents',
      properties: ['Title', 'Page numbers', 'Leader dots', 'Min/max level', 'Placement'],
      preview: 'Contents ...... 1',
    },
  },
  {
    title: 'Text Area',
    type: 'textarea',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Multi-line fillable text input for notes, comments, descriptions, and long form answers.',
    addSteps: ['Open Form Elements.', 'Click Text Area.', 'Set fieldName and placeholder.', 'Resize height for expected lines.'],
    usage: ['Comments field.', 'Customer notes.', 'Approval remarks.', 'PDF form long answer input.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable form field id.'],
      ['placeholder', 'string', 'Placeholder shown before input.'],
      ['required', 'boolean', 'Marks the field as required where supported.'],
      ['style.fontSize', 'number', 'Input text size.'],
    ],
    example: `{
  "id": "comments",
  "type": "textarea",
  "x": 40,
  "y": 460,
  "width": 300,
  "height": 90,
  "fieldName": "comments",
  "placeholder": "Notes..."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Text Area',
      properties: ['Field name', 'Placeholder', 'Required', 'Size'],
      preview: 'Notes...',
    },
  },
  {
    title: 'Checkbox',
    type: 'checkbox',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Interactive boolean checkbox field with an initial visual state.',
    addSteps: ['Open Form Elements.', 'Click Checkbox.', 'Set fieldName.', 'Choose checkState for the initial value.'],
    usage: ['Terms accepted.', 'Approval flags.', 'Checklist rows.', 'Migrated yes/no report controls.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field id.'],
      ['checkState', 'checked | cross | dot | empty', 'Initial checkbox glyph/state.'],
      ['required', 'boolean', 'Marks the checkbox as required where supported.'],
    ],
    example: `{
  "id": "approved",
  "type": "checkbox",
  "x": 40,
  "y": 560,
  "width": 16,
  "height": 16,
  "fieldName": "approved",
  "checkState": "checked"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Checkbox',
      properties: ['Field name', 'Check state', 'Required'],
      preview: '[x] Approved',
    },
  },
  {
    title: 'Radio Group',
    type: 'radio',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Single-select radio group for mutually exclusive options.',
    addSteps: ['Open Form Elements.', 'Click Radio.', 'Add options.', 'Set selectedValue if one option should be preselected.'],
    usage: ['Yes/no/maybe choices.', 'Shipping method.', 'Priority selection.', 'Survey answers.'],
    attributes: [
      ['options', 'string[]', 'Available radio values.'],
      ['selectedValue', 'string', 'Currently selected option.'],
      ['fieldName', 'string', 'Shared field id for the radio group.'],
    ],
    example: `{
  "id": "priority",
  "type": "radio",
  "x": 40,
  "y": 590,
  "width": 220,
  "height": 60,
  "fieldName": "priority",
  "options": ["Low", "Normal", "High"],
  "selectedValue": "Normal"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Radio',
      properties: ['Field name', 'Options', 'Selected value'],
      preview: '( ) Low  (o) Normal',
    },
  },
  {
    title: 'Dropdown',
    type: 'dropdown',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Combo box / dropdown field with configurable choices.',
    addSteps: ['Open Form Elements.', 'Click Dropdown.', 'Set fieldName.', 'Add options and selectedValue.'],
    usage: ['Country selection.', 'Status values.', 'Predefined categories.', 'Provider-generated choice fields.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field id.'],
      ['options', 'string[]', 'Dropdown choices.'],
      ['selectedValue', 'string', 'Initial selected value.'],
    ],
    example: `{
  "id": "country",
  "type": "dropdown",
  "x": 40,
  "y": 650,
  "width": 180,
  "height": 24,
  "fieldName": "country",
  "options": ["DE", "FR", "US"],
  "selectedValue": "DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Dropdown',
      properties: ['Field name', 'Options', 'Selected value'],
      preview: 'DE v',
    },
  },
  {
    title: 'Option List',
    type: 'optionlist',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Static or form-oriented ordered/unordered option list.',
    addSteps: ['Open Form Elements.', 'Click Option List.', 'Add options.', 'Choose ordered and listStyle.'],
    usage: ['Checklist text.', 'Selectable-looking static lists.', 'Numbered instructions.', 'Migrated option groups.'],
    attributes: [
      ['options', 'string[]', 'List entries.'],
      ['ordered', 'boolean', 'Numbered when true, bulleted when false.'],
      ['listStyle', 'decimal | alpha | roman | disc | square', 'Marker style.'],
    ],
    example: `{
  "id": "steps",
  "type": "optionlist",
  "x": 300,
  "y": 590,
  "width": 220,
  "height": 80,
  "options": ["Review", "Approve", "Archive"],
  "ordered": true,
  "listStyle": "decimal"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Option List',
      properties: ['Options', 'Ordered', 'List style'],
      preview: '1. Review',
    },
  },
  {
    title: 'Button',
    type: 'button',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Clickable button area that links to a URL or internal page action.',
    addSteps: ['Open Form Elements.', 'Click Button.', 'Set content as the caption.', 'Set buttonAction to a URL or internal page target.'],
    usage: ['Open external resource.', 'Jump to internal page.', 'Call-to-action in HTML/PDF contexts.', 'Interactive form navigation.'],
    attributes: [
      ['content', 'string', 'Button caption.'],
      ['buttonAction', 'string', 'URL or internal action such as page:2.'],
      ['style.backgroundColor', 'string', 'Button fill.'],
      ['style.color', 'string', 'Caption color.'],
    ],
    example: `{
  "id": "open-docs",
  "type": "button",
  "x": 40,
  "y": 700,
  "width": 120,
  "height": 32,
  "content": "Open docs",
  "buttonAction": "https://docs.powerdoxautomation.com"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Button',
      properties: ['Caption', 'Action', 'Fill', 'Text color'],
      preview: 'Open docs',
    },
  },
  {
    title: 'Signature',
    type: 'signature',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Signature line placeholder for manual or digital signing workflows.',
    addSteps: ['Open Form Elements.', 'Click Signature.', 'Resize the line area.', 'Set signatureLabel.'],
    usage: ['Approval forms.', 'Contracts.', 'Delivery confirmation.', 'Generated sign-off pages.'],
    attributes: [
      ['signatureLabel', 'string', 'Caption under the signature line.'],
      ['width / height', 'number', 'Signature area size.'],
      ['style.borderColor', 'string', 'Line color where supported.'],
    ],
    example: `{
  "id": "signature",
  "type": "signature",
  "x": 320,
  "y": 700,
  "width": 220,
  "height": 50,
  "signatureLabel": "Authorized signature"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Signature',
      properties: ['Signature label', 'Line size', 'Style'],
      preview: '________________',
    },
  },
  {
    title: 'QR Code',
    type: 'qrcode',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Generated QR code from a URL, id, or data string.',
    addSteps: ['Open Visual Elements.', 'Click QR Code.', 'Set qrValue.', 'Resize the square element.'],
    usage: ['Payment URL.', 'Verification link.', 'Ticket code.', 'Customer portal link.'],
    attributes: [
      ['qrValue', 'string', 'Encoded value.'],
      ['qrSize', 'number', 'Module size hint.'],
      ['width / height', 'number', 'Rendered QR box size.'],
    ],
    example: `{
  "id": "payment-qr",
  "type": "qrcode",
  "x": 430,
  "y": 360,
  "width": 96,
  "height": 96,
  "qrValue": "https://pay.example/invoice/1001"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> QR Code',
      properties: ['QR value', 'Size', 'Position'],
      preview: 'QR',
    },
  },
  {
    title: 'Barcode',
    type: 'barcode',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Linear barcode generated from a value and symbology.',
    addSteps: ['Open Visual Elements.', 'Click Barcode.', 'Set barcodeValue.', 'Choose barcodeType.', 'Resize for scanner readability.'],
    usage: ['Shipping labels.', 'SKU labels.', 'Receipt codes.', 'Inventory forms.'],
    attributes: [
      ['barcodeValue', 'string', 'Encoded value.'],
      ['barcodeType', 'Code128 | EAN13 | UPCA | Code39', 'Barcode symbology.'],
      ['width / height', 'number', 'Barcode box size.'],
    ],
    example: `{
  "id": "sku-barcode",
  "type": "barcode",
  "x": 330,
  "y": 470,
  "width": 200,
  "height": 60,
  "barcodeValue": "4006381333931",
  "barcodeType": "EAN13"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Barcode',
      properties: ['Barcode value', 'Barcode type', 'Size'],
      preview: '|||| ||| ||||',
    },
  },
  {
    title: 'Shape',
    type: 'shape',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Generic filled or stroked background/container block.',
    addSteps: ['Open Shapes & Layout.', 'Click Shape.', 'Resize the block.', 'Set fill, border, radius, opacity, and layering.'],
    usage: ['Section background.', 'Callout block.', 'Report band background.', 'Grouped visual container.'],
    attributes: [
      ['style.backgroundColor', 'string', 'Fill color.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
      ['style.borderRadius', 'number', 'Corner radius.'],
      ['style.opacity', 'number', 'Transparency.'],
    ],
    example: `{
  "id": "callout-bg",
  "type": "shape",
  "x": 40,
  "y": 120,
  "width": 500,
  "height": 80,
  "style": {
    "backgroundColor": "#f3f8ff",
    "borderColor": "#2468b2",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Shape',
      properties: ['Fill', 'Border', 'Radius', 'Opacity'],
      preview: 'Background block',
    },
  },
  {
    title: 'Rectangle',
    type: 'rect',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Specific rectangle primitive for boxes, borders, and report frames.',
    addSteps: ['Open Shapes & Layout.', 'Click Rectangle.', 'Position and resize.', 'Set border and fill style.'],
    usage: ['Table frame.', 'Input box outline.', 'Migrated rectangle/report frame.', 'Visual separator.'],
    attributes: [
      ['style.borderColor', 'string', 'Rectangle stroke.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
      ['style.backgroundColor', 'string', 'Optional fill.'],
    ],
    example: `{
  "id": "total-box",
  "type": "rect",
  "x": 380,
  "y": 300,
  "width": 160,
  "height": 52,
  "style": {
    "borderColor": "#111827",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Rectangle',
      properties: ['Border color', 'Border width', 'Fill'],
      preview: 'Rectangle frame',
    },
  },
  {
    title: 'Circle',
    type: 'circle',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Ellipse/circle primitive for badges, markers, and decorative report shapes.',
    addSteps: ['Open Shapes & Layout.', 'Click Circle.', 'Resize the ellipse.', 'Set fill and border.'],
    usage: ['Status badge.', 'Bullet marker.', 'Seal placeholder.', 'Chart legend marker.'],
    attributes: [
      ['style.backgroundColor', 'string', 'Circle fill.'],
      ['style.borderColor', 'string', 'Circle stroke.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
    ],
    example: `{
  "id": "status-dot",
  "type": "circle",
  "x": 40,
  "y": 330,
  "width": 18,
  "height": 18,
  "style": {
    "backgroundColor": "#0f766e"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Circle',
      properties: ['Fill', 'Border', 'Size'],
      preview: 'Circle marker',
    },
  },
  {
    title: 'Arrow',
    type: 'arrow',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Directional arrow with straight, elbow, or curved path and marker configuration.',
    addSteps: ['Open Shapes & Layout.', 'Click Arrow.', 'Choose arrowMode.', 'Configure startMarker and endMarker.'],
    usage: ['Process flows.', 'Callouts.', 'Diagram annotations.', 'Review explanations.'],
    attributes: [
      ['arrowMode', 'straight | elbow | curved', 'Path style.'],
      ['startMarker', 'none | filled | open | dot | diamond | square | circle | arrow', 'Start marker.'],
      ['endMarker', 'none | filled | open | dot | diamond | square | circle | arrow', 'End marker.'],
      ['style.borderColor', 'string', 'Stroke color.'],
    ],
    example: `{
  "id": "flow-arrow",
  "type": "arrow",
  "x": 80,
  "y": 240,
  "width": 180,
  "height": 2,
  "arrowMode": "straight",
  "endMarker": "filled"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Arrow',
      properties: ['Arrow mode', 'Start marker', 'End marker', 'Stroke'],
      preview: 'Start -> End',
    },
  },
  {
    title: 'Drawing',
    type: 'draw',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Freehand SVG-style stroke drawn with pen, highlighter, or eraser tools.',
    addSteps: ['Open Shapes & Layout.', 'Click Drawing.', 'Draw on the canvas.', 'Adjust drawTool, pathData, and stroke style.'],
    usage: ['Hand annotations.', 'Sketches.', 'Markup highlights.', 'Imported ink paths.'],
    attributes: [
      ['drawTool', 'pen | highlighter | eraser', 'Drawing tool.'],
      ['pathData', 'string', 'SVG path data.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
    ],
    example: `{
  "id": "ink-note",
  "type": "draw",
  "x": 40,
  "y": 500,
  "width": 120,
  "height": 60,
  "drawTool": "pen",
  "pathData": "M0,60 C40,0 80,120 120,40"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Drawing',
      properties: ['Tool', 'Path data', 'Stroke color', 'Stroke width'],
      preview: 'Freehand path',
    },
  },
  {
    title: 'Subsection',
    type: 'subsection',
    category: 'Shapes & Layout',
    status: 'Preview',
    description: 'Non-rendered designer guide used to group related content on the design surface.',
    addSteps: ['Open Shapes & Layout.', 'Click Subsection.', 'Place it around related elements.', 'Use content as a guide label.'],
    usage: ['Address block guide.', 'Report section guide.', 'Template authoring organization.', 'Non-output design notes.'],
    attributes: [
      ['content', 'string', 'Optional guide label.'],
      ['hidden', 'boolean', 'Can be hidden from the designer view.'],
      ['locked', 'boolean', 'Locks the guide.'],
    ],
    example: `{
  "id": "address-zone",
  "type": "subsection",
  "x": 40,
  "y": 120,
  "width": 300,
  "height": 160,
  "content": "Address block"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Subsection',
      properties: ['Label', 'Position', 'Size', 'Locked'],
      preview: 'Address block guide',
    },
  },
  {
    title: 'Area',
    type: 'area',
    category: 'Shapes & Layout',
    status: 'Preview',
    description: 'Non-rendered layout region that reserves or documents an intended content area.',
    addSteps: ['Open Shapes & Layout.', 'Click Area.', 'Place it where dynamic content should stay.', 'Label the area with content.'],
    usage: ['Reserved space.', 'Data repeat region.', 'Import/migration layout planning.', 'Designer handoff note.'],
    attributes: [
      ['content', 'string', 'Optional region label.'],
      ['x / y / width / height', 'number', 'Reserved region geometry.'],
      ['locked', 'boolean', 'Prevents accidental edits.'],
    ],
    example: `{
  "id": "details-area",
  "type": "area",
  "x": 40,
  "y": 220,
  "width": 500,
  "height": 280,
  "content": "Line items"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Area',
      properties: ['Label', 'Geometry', 'Locked'],
      preview: 'Reserved area',
    },
  },
  {
    title: 'Highlight',
    type: 'highlight',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Translucent rectangle or text-marker highlight for emphasizing existing content.',
    addSteps: ['Open Shapes & Layout.', 'Click Highlight.', 'Choose markMode.', 'Set translucent background color.'],
    usage: ['Review markup.', 'Important totals.', 'Imported PDF highlights.', 'Training/documentation examples.'],
    attributes: [
      ['markMode', 'rectangle | text', 'Highlight region or text-run mode.'],
      ['style.backgroundColor', 'string', 'Usually translucent highlight color.'],
      ['style.opacity', 'number', 'Transparency.'],
    ],
    example: `{
  "id": "total-highlight",
  "type": "highlight",
  "x": 380,
  "y": 300,
  "width": 160,
  "height": 22,
  "markMode": "rectangle",
  "style": {
    "backgroundColor": "rgba(255,235,59,0.5)"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Highlight',
      properties: ['Mode', 'Color', 'Opacity'],
      preview: 'Highlighted total',
    },
  },
  {
    title: 'Check Mark',
    type: 'checkmark',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Static check/cross/dot mark used as a decorative mark, not an interactive field.',
    addSteps: ['Open Shapes & Layout.', 'Click Check Mark.', 'Choose checkState.', 'Set stroke color and size.'],
    usage: ['Static approval indicator.', 'Migrated check mark glyph.', 'Status summary.', 'Checklist output.'],
    attributes: [
      ['checkState', 'checked | cross | dot | empty', 'Displayed glyph.'],
      ['style.color', 'string', 'Glyph color.'],
      ['style.strokeWidth', 'number', 'Glyph thickness where supported.'],
    ],
    example: `{
  "id": "paid-mark",
  "type": "checkmark",
  "x": 500,
  "y": 120,
  "width": 18,
  "height": 18,
  "checkState": "checked",
  "style": { "color": "#0f766e" }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Check Mark',
      properties: ['State', 'Color', 'Stroke width'],
      preview: 'Check',
    },
  },
  {
    title: 'Watermark',
    type: 'watermark',
    category: 'Document',
    status: 'Ready',
    description: 'Text or image watermark with page scope, opacity, and rotation.',
    addSteps: ['Open Document Elements.', 'Click Watermark.', 'Choose text or image mode.', 'Set pageScope, opacity, and rotation.'],
    usage: ['Draft watermark.', 'Confidential stamp.', 'Brand watermark.', 'Page range-specific marks.'],
    attributes: [
      ['content', 'string', 'Watermark text or image source depending on mode.'],
      ['watermarkMode', 'text | image', 'Watermark source type.'],
      ['pageScope', 'current | all | first | last | range | odd | even', 'Pages where the watermark appears.'],
      ['pageRange', 'string', 'Range expression for range scope.'],
      ['style.opacity / style.rotation', 'number', 'Transparency and angle.'],
    ],
    example: `{
  "id": "draft",
  "type": "watermark",
  "x": 100,
  "y": 350,
  "width": 400,
  "height": 120,
  "content": "DRAFT",
  "watermarkMode": "text",
  "pageScope": "all",
  "style": { "opacity": 0.15, "rotation": -45 }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Watermark',
      properties: ['Mode', 'Scope', 'Range', 'Opacity', 'Rotation'],
      preview: 'DRAFT',
    },
  },
  {
    title: 'Sticky Note',
    type: 'note',
    category: 'Document',
    status: 'Ready',
    description: 'Review note or callout with title, body, author, and collapsed state.',
    addSteps: ['Open Document Elements.', 'Click Sticky Note.', 'Set noteTitle, noteBody, and noteAuthor.', 'Use collapsed mode for compact review notes.'],
    usage: ['Review comment.', 'Internal instruction.', 'Migration diagnostic note.', 'Template handoff reminder.'],
    attributes: [
      ['noteTitle', 'string', 'Note heading.'],
      ['noteBody', 'string', 'Note content.'],
      ['noteAuthor', 'string', 'Author label.'],
      ['noteCollapsed', 'boolean', 'Compact/collapsed state.'],
    ],
    example: `{
  "id": "qa-note",
  "type": "note",
  "x": 360,
  "y": 120,
  "width": 180,
  "height": 90,
  "noteTitle": "QA",
  "noteBody": "Verify totals before sending.",
  "noteAuthor": "Reviewer"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Sticky Note',
      properties: ['Title', 'Body', 'Author', 'Collapsed'],
      preview: 'QA note',
    },
  },
  {
    title: 'Link',
    type: 'link',
    category: 'Document',
    status: 'Ready',
    description: 'Clickable hyperlink region with visible text and target URL.',
    addSteps: ['Open Document Elements.', 'Click Link.', 'Set content and href.', 'Choose linkTarget for HTML workflows.'],
    usage: ['External docs link.', 'Customer portal link.', 'Internal navigation target.', 'Clickable support URL.'],
    attributes: [
      ['content', 'string', 'Visible link text.'],
      ['href', 'string', 'Destination URL.'],
      ['linkTarget', '_blank | _self', 'HTML target behavior.'],
    ],
    example: `{
  "id": "portal-link",
  "type": "link",
  "x": 40,
  "y": 760,
  "width": 180,
  "height": 18,
  "content": "Open customer portal",
  "href": "https://example.com/portal",
  "linkTarget": "_blank"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Link',
      properties: ['Text', 'Href', 'Target'],
      preview: 'Open customer portal',
    },
  },
  {
    title: 'Bookmark',
    type: 'bookmark',
    category: 'Document',
    status: 'Preview',
    description: 'Named anchor for navigation outlines and cross-reference workflows.',
    addSteps: ['Open Document Elements.', 'Click Bookmark.', 'Set bookmarkName.', 'Place it near the target content.'],
    usage: ['Chapter anchor.', 'PDF outline target.', 'Cross-reference destination.', 'Word navigation marker.'],
    attributes: [
      ['bookmarkName', 'string', 'Anchor/outline name.'],
      ['bookmarkTarget', 'string', 'Optional explicit target reference.'],
      ['x / y', 'number', 'Anchor location.'],
    ],
    example: `{
  "id": "chapter-1-bookmark",
  "type": "bookmark",
  "x": 40,
  "y": 80,
  "width": 10,
  "height": 10,
  "bookmarkName": "chapter-1"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Bookmark',
      properties: ['Name', 'Target', 'Position'],
      preview: 'Bookmark anchor',
    },
  },
  {
    title: 'Comment',
    type: 'comment',
    category: 'Document',
    status: 'Preview',
    description: 'Review comment element with author, body, and timestamp metadata.',
    addSteps: ['Open Document Elements.', 'Click Comment.', 'Set commentAuthor and commentText.', 'Use commentDate for imported or audited comments.'],
    usage: ['Reviewer feedback.', 'DOCX comment migration.', 'QA notes.', 'Approval trace.'],
    attributes: [
      ['commentAuthor', 'string', 'Reviewer/author.'],
      ['commentText', 'string', 'Comment body.'],
      ['commentDate', 'string', 'ISO timestamp.'],
    ],
    example: `{
  "id": "legal-comment",
  "type": "comment",
  "x": 380,
  "y": 220,
  "width": 180,
  "height": 60,
  "commentAuthor": "Legal",
  "commentText": "Confirm this clause."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Comment',
      properties: ['Author', 'Text', 'Date'],
      preview: 'Legal comment',
    },
  },
  {
    title: 'Footnote',
    type: 'footnote',
    category: 'Document',
    status: 'Preview',
    description: 'Inline footnote reference with note body; strongest native support is in DOCX.',
    addSteps: ['Open Text or Document Elements.', 'Click Footnote.', 'Set footnoteText and optional footnoteRef.', 'Place next to referenced content.'],
    usage: ['Legal/source reference.', 'Book manuscript notes.', 'Academic-style documents.', 'DOCX import/export fidelity.'],
    attributes: [
      ['footnoteText', 'string', 'Footnote body.'],
      ['footnoteRef', 'string', 'Optional marker/reference.'],
    ],
    example: `{
  "id": "source-footnote",
  "type": "footnote",
  "x": 240,
  "y": 210,
  "width": 160,
  "height": 16,
  "footnoteText": "Source: internal data."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Footnote',
      properties: ['Text', 'Reference', 'Position'],
      preview: 'Footnote ref',
    },
  },
  {
    title: 'Endnote',
    type: 'endnote',
    category: 'Document',
    status: 'Preview',
    description: 'Inline endnote reference collected at the end of the document where supported.',
    addSteps: ['Open Text or Document Elements.', 'Click Endnote.', 'Set footnoteText as the endnote body.', 'Place next to referenced content.'],
    usage: ['Long-form references.', 'Book endnotes.', 'DOCX-native note workflows.', 'PDF fallback note rendering.'],
    attributes: [
      ['footnoteText', 'string', 'Endnote body.'],
      ['footnoteRef', 'string', 'Optional marker/reference.'],
    ],
    example: `{
  "id": "appendix-endnote",
  "type": "endnote",
  "x": 260,
  "y": 260,
  "width": 160,
  "height": 16,
  "footnoteText": "Published reference."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Endnote',
      properties: ['Text', 'Reference', 'Position'],
      preview: 'Endnote ref',
    },
  },
  {
    title: 'Content Control',
    type: 'contentcontrol',
    category: 'Document',
    status: 'Preview',
    description: 'Structured Word content control with PDF fallback box for template-driven content.',
    addSteps: ['Open Document Elements.', 'Click Content Control.', 'Choose contentControlType.', 'Set tag and title for code integration.'],
    usage: ['Word SDT fields.', 'Template placeholders.', 'Structured authoring.', 'DOCX import/export workflows.'],
    attributes: [
      ['contentControlType', 'richText | plainText | datePicker | comboBox | picture', 'Control kind.'],
      ['contentControlTag', 'string', 'Programmatic tag.'],
      ['contentControlTitle', 'string', 'Visible title in Word.'],
    ],
    example: `{
  "id": "customer-control",
  "type": "contentcontrol",
  "x": 40,
  "y": 520,
  "width": 240,
  "height": 24,
  "contentControlType": "plainText",
  "contentControlTag": "customer",
  "contentControlTitle": "Customer"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Content Control',
      properties: ['Type', 'Tag', 'Title'],
      preview: 'Customer control',
    },
  },
  {
    title: 'Page Boundary',
    type: 'pageboundary',
    category: 'Document',
    status: 'Preview',
    description: 'Designer guide for page edges or margin boundaries; not rendered to final output.',
    addSteps: ['Open Document Elements.', 'Click Page Boundary.', 'Place or resize it to mark page regions.', 'Use locked mode to keep it stable.'],
    usage: ['Margin guide.', 'Import diagnostics.', 'Layout debugging.', 'Page-edge planning.'],
    attributes: [
      ['x / y / width / height', 'number', 'Boundary geometry.'],
      ['locked', 'boolean', 'Prevents accidental movement.'],
      ['hidden', 'boolean', 'Hides the guide from designer view.'],
    ],
    example: `{
  "id": "print-boundary",
  "type": "pageboundary",
  "x": 0,
  "y": 0,
  "width": 595,
  "height": 842,
  "locked": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Page Boundary',
      properties: ['Geometry', 'Locked', 'Hidden'],
      preview: 'Page edge guide',
    },
  },
];

const codeSections = [
  {
    title: 'PXA.Generator',
    status: 'Ready',
    text: 'Generate PDFs and business documents from .NET code, structured data, and reusable layout primitives.',
  },
  {
    title: 'PXA.Migration',
    status: 'Preview',
    text: 'Convert provider-specific code and designer formats into PXA targets with diagnostics and follow-up notes.',
  },
  {
    title: 'PXA.Importer',
    status: 'Preview',
    text: 'Integrate file importers into document automation, migration, and designer workflows.',
  },
  {
    title: 'PXA.Infrastructure',
    status: 'Preview',
    text: 'Understand rendering, conversion, persistence, and integration boundaries across PXA services.',
  },
  {
    title: 'PXA.WebApi',
    status: 'Ready',
    text: 'Use HTTP endpoints for migration, import, export, rendering, and designer handoff flows.',
  },
  {
    title: 'API Reference',
    status: 'Planned',
    text: 'Connect generated DocFX and OpenAPI references to product-level integration guides.',
  },
];

const migrationGuides = [
  {
    title: 'PDF Code Migration',
    status: 'Preview',
    text: 'Map third-party PDF SDK calls into PXA code patterns and track missing API parity explicitly.',
  },
  {
    title: 'Report Designer Migration',
    status: 'Ready',
    text: 'Convert DevExpress, RDL/RDLC, ActiveReports, FastReport, Telerik, JasperReports, and Stimulsoft reports into editable PXA designs.',
  },
  {
    title: 'Spreadsheet Code Migration',
    status: 'Preview',
    text: 'Plan spreadsheet provider migrations and workbook-driven automation paths.',
  },
  {
    title: 'Provider Taxonomy',
    status: 'Ready',
    text: 'Use the PXA migration namespace taxonomy to distinguish domain, migration kind, and provider.',
  },
  {
    title: 'Migration Diagnostics',
    status: 'Ready',
    text: 'Understand severity, manual migration notes, and stable diagnostic IDs.',
  },
];

const migrationWorkflowSteps = [
  ['Choose the migration path', 'PDF Code, Report Designer, Spreadsheet Code, or Provider Taxonomy.'],
  ['Select provider/framework', 'Use the original technology, for example iText7, DevExpress XtraReport, RDL, ClosedXML, or Aspose.Cells.'],
  ['Submit source', 'Paste source code, upload a report designer file, or provide a spreadsheet migration sample.'],
  ['Review converted output', 'PDF and spreadsheet code produce PXA-compatible code; report designer migration produces editable design JSON.'],
  ['Read diagnostics', 'Warnings identify unsupported patterns, manual follow-ups, or API areas outside deterministic migration.'],
  ['Open or export result', 'Open report output in PXA Designer, copy generated code, generate preview, or download artifacts.'],
];

const pdfCodeMigrationProviders = [
  ['Syncfusion PDF', 'full', 'Document/page/text/line/rectangle/image/save with top-left coordinate adapter.', 'Complex tables, forms, signatures, existing-PDF editing.'],
  ['iText7', 'full', 'PdfWriter/PdfDocument/Document, Paragraph, ShowTextAligned, PdfCanvas text/line/rectangle.', 'Advanced layout renderer model, forms, tagged PDF, signatures.'],
  ['Apryse (PDFTron)', 'full', 'PDFDoc creation, PageCreate/PagePushBack, document save.', 'ElementBuilder rich graphics, editing workflows, annotations.'],
  ['Aspose.PDF', 'full', 'Document, Pages.Add, TextFragment/TextBuilder positioned text.', 'Advanced DOM editing, forms, compliance, optimization.'],
  ['DsPdf (GrapeCity)', 'full', 'GcPdfDocument, NewPage, DrawString/DrawLine/DrawRectangle/FillRectangle, Save.', 'Advanced graphics, AcroForms, image processing.'],
  ['Foxit PDF SDK', 'full', 'PDFDoc, InsertPage/CreatePage, graphics draw calls, Save/SaveAs.', 'Runtime/library setup, editing APIs, rich annotations.'],
  ['DevExpress PDF', 'full', 'PdfDocumentProcessor, RenderNewPage, draw calls, SaveDocument.', 'Forms/signatures/report export produce follow-up diagnostics.'],
  ['Spire.PDF', 'full', 'PdfDocument, Pages.Add, Canvas.DrawString/DrawLine/DrawRectangle/FillRectangle, SaveToFile.', 'Tables, forms, annotations produce warnings.'],
  ['GemBox.Pdf', 'full', 'PdfDocument, Pages.Add, Content.DrawText/DrawLine/DrawRectangle, ComponentInfo.SetLicense removal.', 'Forms, encryption, annotations produce warnings.'],
  ['PDFKit.NET', 'full', 'Document/NewPage/Pages.Add, DrawText/DrawString/DrawLine/DrawRectangle, Save/Render.', 'Package identity must be manually verified; forms/encryption/annotations warning path.'],
  ['LEADTOOLS', 'full', 'PDFDocument, AddPage/Pages.Add, DrawText/DrawString/DrawLine/DrawRectangle, Save/Export.', 'Raster/OCR/barcode/conversion APIs are manual.'],
  ['IronPDF', 'pilot', 'ChromePdfRenderer scaffold, SaveAs to Save.', 'HTML/URL/Razor rendering converted to manual diagnostics.'],
  ['ActivePDF', 'pilot', 'Likely Toolkit-style generation only.', 'DocConverter, WebGrabber, COM/server, printer, merge, stamp workflows are manual.'],
  ['PDFTools / Pdftools SDK', 'pilot', 'Sdk.Initialize removal and cautious processing diagnostics.', 'SDK conversion/processing workflows remain manual; Toolbox direct generation is separate.'],
  ['PDF Toolbox SDK', 'pilot', 'Document.Create/Page.Create/TextGenerator.ShowLine to PXA-compatible code.', 'Existing-PDF editing and rich styling remain manual.'],
];

const reportDesignerProviders = [
  ['DevExpress XtraReport', '.Designer.cs / C# report class', 'Bands, labels, lines, tables, charts, barcodes, images, page header/footer.', 'PXA.Migration.Report.Designer.DevExpress'],
  ['RDL/RDLC / Syncfusion / Bold Reports', '.rdl / .rdlc', 'Textbox, tablix/table, line, rectangle, image, page header/footer, field bindings.', 'PXA.Migration.Report.Designer.Rdl'],
  ['ActiveReports RPX', '.rpx', 'Section bands, labels, textboxes, line, barcode, page header/footer, DataField binding.', 'PXA.Migration.Report.Designer.Rpx'],
  ['ActiveReportsJS', 'JSON report definition', 'Designer JSON report items where supported by the converter.', 'PXA.Migration.Report.Designer.ActiveReportsJs'],
  ['FastReport', '.frx', 'Bands, TextObject, LineObject, BarcodeObject, style/font/color basics.', 'PXA.Migration.Report.Designer.FastReport'],
  ['Telerik Reporting', '.trdx', 'Sections, named styles, text boxes, barcodes, page header/footer.', 'PXA.Migration.Report.Designer.Telerik'],
  ['JasperReports', '.jrxml', 'Bands, staticText/textField, images, charts where mapping is available, $F{} bindings.', 'PXA.Migration.Report.Designer.JasperReports'],
  ['Stimulsoft Reports', '.mrt', 'Bands, text, horizontal lines, page footer/header, {Source.Field} bindings.', 'PXA.Migration.Report.Designer.Stimulsoft'],
];

const spreadsheetCodeMigrationProviders = [
  ['Aspose.Cells', 'full', 'PXA.Migration.Spreadsheet.Code.Aspose', 'Workbook, Worksheets, Cells, values, styles, formulas.'],
  ['ClosedXML', 'full', 'PXA.Migration.Spreadsheet.Code.ClosedXml', 'Reference implementation for workbook/sheet/cell/range authoring.'],
  ['EPPlus', 'full', 'PXA.Migration.Spreadsheet.Code.Epplus', 'ExcelPackage, Worksheets, Cells indexers, values, formulas, styles.'],
  ['GemBox.Spreadsheet', 'full', 'PXA.Migration.Spreadsheet.Code.GemBox', 'ExcelFile authoring and SetLicense removal.'],
  ['NPOI', 'full', 'PXA.Migration.Spreadsheet.Code.Npoi', 'Workbook/sheet/row/cell creation patterns.'],
  ['Spire.XLS', 'full', 'PXA.Migration.Spreadsheet.Code.Spire', 'Workbook/worksheet/range authoring patterns.'],
  ['SpreadsheetLight', 'full', 'PXA.Migration.Spreadsheet.Code.SpreadsheetLight', 'SLDocument cells, formulas, styles where deterministic.'],
  ['Syncfusion XlsIO', 'full', 'PXA.Migration.Spreadsheet.Code.Syncfusion', 'ExcelEngine, IWorkbook/IWorksheet/range authoring patterns.'],
];

const taxonomyRows = [
  ['PDF code migration', 'PXA.Migration.Pdf.Code.<Provider>', 'Provider-specific C# PDF SDK code to PXA PDF generator code.'],
  ['Report designer migration', 'PXA.Migration.Report.Designer.<Provider>', 'Report designer files/classes to editable PXA design JSON.'],
  ['Spreadsheet code migration', 'PXA.Migration.Spreadsheet.Code.<Provider>', 'Spreadsheet library C# code to PXA spreadsheet API code.'],
  ['Reserved spreadsheet datasource migration', 'PXA.Migration.Spreadsheet.Datasource.<Provider>', 'Reserved for concrete datasource/file migration providers; not fake-created.'],
  ['Shared abstractions', 'PXA.Migration.Abstractions', 'Common contracts and migration result shape.'],
  ['Roslyn infrastructure', 'PXA.Migration.Roslyn', 'Shared C# source rewriting infrastructure.'],
];

const diagnosticRows = [
  ['Info', 'Converted deterministic pattern, removed provider setup, or noted compatibility behavior.'],
  ['Warning', 'Output is usable, but a source pattern needs review or manual parity check.'],
  ['Error', 'Conversion could not produce a reliable target for that input.'],
  ['Migrate manually', 'The source feature is outside deterministic conversion, for example signatures, existing-PDF editing, advanced HTML rendering, or provider-specific runtime services.'],
  ['Stable diagnostic IDs', 'Existing CANMIG... IDs remain stable for compatibility while namespace names move to PXA.'],
];

const cookbook = [
  {
    title: 'PDF generation',
    status: 'Ready',
    text: 'Create business documents from structured data and reusable layout primitives.',
    tasks: ['Choose a template or code model', 'Bind data', 'Render or export output'],
    href: `${siteLinks.demo}#demo/booking-receipt`,
  },
  {
    title: 'Edit PDF',
    status: 'Planned',
    text: 'Track planned editing workflows for existing PDFs and imported document surfaces.',
    tasks: ['Import source file', 'Inspect mapped objects', 'Export edited output'],
    href: companyPage('products/pdf-viewer'),
  },
  {
    title: 'Forms',
    status: 'Preview',
    text: 'Plan form review and field workflows through the PDF Viewer and generated outputs.',
    tasks: ['Open viewer workflow', 'Inspect form fields', 'Connect to review scenarios'],
    href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms`,
  },
  {
    title: 'Annotations',
    status: 'Preview',
    text: 'Review annotation workflows and viewer feature gaps before implementation.',
    tasks: ['Open viewer demo', 'Review planned tools', 'Track parity gaps'],
    href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms`,
  },
  {
    title: 'Reports',
    status: 'Ready',
    text: 'Work with migrated report layouts, report sections, grouped data, charts, and designer handoff.',
    tasks: ['Choose report provider', 'Run designer migration', 'Open output in PXA Designer'],
    href: `${siteLinks.demo}#demo/master-detail-report`,
  },
  {
    title: 'Import/export',
    status: 'Preview',
    text: 'Normalize incoming files and export generated JSON, PDF, and demo artifacts.',
    tasks: ['Choose input format', 'Normalize or migrate', 'Download output artifacts'],
    href: `${siteLinks.demo}#demo/file-importer-flow`,
  },
];

const trackGuides = {
  editor: {
    readFirst: ['Designer', 'Templates', 'Elements'],
    tasks: ['Open a template in PXA Designer', 'Inspect page size, margins, and shared elements', 'Preview output and export JSON'],
    related: [
      { label: 'Open live designer', href: siteLinks.designer },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Master-detail demo', href: `${siteLinks.demo}#demo/master-detail-report` },
    ],
  },
  code: {
    readFirst: ['PXA.Generator', 'PXA.WebApi', 'API Reference'],
    tasks: ['Start the backend API', 'Render or export a document model', 'Use diagnostics for failed imports or migrations'],
    related: [
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'Booking receipt demo', href: `${siteLinks.demo}#demo/booking-receipt` },
      { label: 'OpenAPI schema', href: '../../docs/schema/openapi.json' },
    ],
  },
  migration: {
    readFirst: ['Report Designer Migration', 'PDF Code Migration', 'Provider Taxonomy'],
    tasks: ['Choose code or designer migration', 'Review converted output and diagnostics', 'Open report output in PXA Designer when available'],
    related: [
      { label: 'Migration product page', href: companyPage('products/migration') },
      { label: 'Provider migration demo', href: `${siteLinks.demo}#demo/provider-migration-examples` },
      { label: 'Designer migration route', href: `${siteLinks.designer}migrations/pdf/designer` },
    ],
  },
};

const docEntryPoints = [
  {
    title: 'Use the Editor',
    label: 'Editor path',
    href: '#editor-path',
    text: 'Design templates, inspect elements, preview output, and move between visual workflows and generated JSON.',
  },
  {
    title: 'Integrate with Code',
    label: 'Code path',
    href: '#code-path',
    text: 'Start from .NET integration points: Generator, Importer, Migration, Infrastructure, WebApi, and references.',
  },
  {
    title: 'Migrate to PXA',
    label: 'Migration path',
    href: '#migration',
    text: 'Convert provider-specific PDF code, report designer files, and spreadsheet workflows with diagnostics.',
  },
  {
    title: 'Explore APIs',
    label: 'Reference path',
    href: '#api-reference',
    text: 'Find generated API reference, OpenAPI artifacts, cookbook links, and endpoint-oriented guidance.',
  },
];

const demoExamples = [
  {
    title: 'Invoice / Booking Receipt',
    route: 'booking-receipt',
    docs: 'PDF generation',
    source: '/examples/booking-receipt/source.js',
    input: '/examples/booking-receipt/input.json',
    output: '/examples/booking-receipt/output.json',
  },
  {
    title: 'Master-detail report',
    route: 'master-detail-report',
    docs: 'Reports',
    source: '/examples/master-detail-report/source.js',
    input: '/examples/master-detail-report/input.json',
    output: '/examples/master-detail-report/output.json',
  },
  {
    title: 'Chart report',
    route: 'chart-report',
    docs: 'Elements',
    source: '/examples/chart-report/source.js',
    input: '/examples/chart-report/input.json',
    output: '/examples/chart-report/output.json',
  },
  {
    title: 'PDF viewer annotations/forms',
    route: 'pdf-viewer-annotations-forms',
    docs: 'PDF Viewer',
    source: '/examples/pdf-viewer-annotations-forms/source.js',
    input: '/examples/pdf-viewer-annotations-forms/input.json',
    output: '/examples/pdf-viewer-annotations-forms/output.json',
  },
  {
    title: 'Spreadsheet import/export',
    route: 'spreadsheet-import-export',
    docs: 'Spreadsheet',
    source: '/examples/spreadsheet-import-export/source.js',
    input: '/examples/spreadsheet-import-export/input.json',
    output: '/examples/spreadsheet-import-export/output.json',
  },
  {
    title: 'Provider migration examples',
    route: 'provider-migration-examples',
    docs: 'PXA.Migration',
    source: '/examples/provider-migration-examples/source.js',
    input: '/examples/provider-migration-examples/input.json',
    output: '/examples/provider-migration-examples/output.json',
  },
];

const quickstarts = [
  {
    title: 'Editor Quickstart',
    label: 'Editor',
    steps: ['Open PXA Designer', 'Choose or import a template', 'Preview output and inspect JSON'],
    command: 'cd pxa-designer && npm run dev',
  },
  {
    title: 'Code Quickstart',
    label: 'SDK',
    steps: ['Start the backend API', 'Create or load a document model', 'Render, migrate, import, or export through PXA endpoints'],
    command: 'dotnet build',
  },
  {
    title: 'Migration Quickstart',
    label: 'Migration',
    steps: ['Choose code or designer migration', 'Upload or paste provider input', 'Review diagnostics before opening in designer'],
    command: 'open /migrations/pdf/designer',
  },
];

const referenceLinks = [
  {
    title: 'DocFX API Reference',
    status: 'Planned',
    text: 'Generated .NET API reference for PXA packages.',
    href: '../../docs/api/',
  },
  {
    title: 'OpenAPI Schema',
    status: 'Ready',
    text: 'WebApi contract for migration, import, and export endpoints.',
    href: '../../docs/schema/openapi.json',
  },
  {
    title: 'C# Cookbook',
    status: 'Preview',
    text: 'Task-oriented examples for generator and integration workflows.',
    href: '../../docs/csharp-cookbook.md',
  },
];

const checklistLinks = [
  'PXA.Web-Design-System',
  'PXA.Company',
  'PXA.Documentation',
  'PXA.Demo',
  'Migration-Namespace-Taxonomy',
  'PxaPdf-Provider-Feature-Gaps',
];

function itemTitle(item) {
  return typeof item === 'string' ? item : item.title;
}

function renderNavList(items) {
  return items.map((item) => `<a href="#${slug(itemTitle(item))}">${itemTitle(item)}</a>`).join('');
}

function renderDetailNavList(items) {
  return items.map((item) => `<a href="#${slug(itemTitle(item))}-details">${itemTitle(item)}</a>`).join('');
}

function renderElementNav(items) {
  const categories = [...new Set(items.map((item) => item.category))];
  return `
    <details class="pxa-doc-nav__section" open>
      <summary>Element Reference</summary>
      <a class="pxa-doc-nav__featured" href="#element-reference">Overview</a>
      ${categories
        .map((category) => `
          <details class="pxa-doc-nav__section pxa-doc-nav__section--nested">
            <summary>${category}</summary>
            ${items
              .filter((item) => item.category === category)
              .map((item) => `<a class="pxa-doc-nav__subitem" href="#${slug(item.title)}-element">${item.title}</a>`)
              .join('')}
          </details>
        `)
        .join('')}
    </details>
  `;
}

function statusClass(status) {
  if (status === 'Ready') return 'pxa-status--ready';
  if (status === 'Preview') return 'pxa-status--preview';
  return 'pxa-status--planned';
}

function renderCards(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-card" id="${slug(itemTitle(item))}">
          <span class="pxa-status ${statusClass(item.status ?? 'Planned')}">${item.status ?? 'Planned'}</span>
          <h3>${itemTitle(item)}</h3>
          <p>${item.text ?? descriptionFor(itemTitle(item))}</p>
        </article>
      `,
    )
    .join('');
}

function renderEntryPoints(items) {
  return items
    .map(
      (item) => `
        <a class="pxa-card pxa-doc-entry" href="${item.href}">
          <span class="pxa-status pxa-status--ready">${item.label}</span>
          <h2>${item.title}</h2>
          <p>${item.text}</p>
        </a>
      `,
    )
    .join('');
}

function renderQuickstarts(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-quickstart-card">
          <span class="pxa-status pxa-status--ready">${item.label}</span>
          <h3>${item.title}</h3>
          <ol>
            ${item.steps.map((step) => `<li>${step}</li>`).join('')}
          </ol>
          <pre class="pxa-code"><code>${item.command}</code></pre>
        </article>
      `,
    )
    .join('');
}

function renderReferenceLinks(items) {
  return items
    .map(
      (item) => `
        <a class="pxa-card pxa-doc-reference-card" href="${item.href}">
          <span class="pxa-status ${statusClass(item.status ?? 'Planned')}">${item.status ?? 'Planned'}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
        </a>
      `,
    )
    .join('');
}

function renderChecklistLinks(items) {
  return items
    .map((item) => `<span class="pxa-status pxa-status--preview">${item}</span>`)
    .join('');
}

function renderCookbook(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-cookbook-card">
          <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <ul>
            ${item.tasks.map((task) => `<li>${task}</li>`).join('')}
          </ul>
          <a href="${item.href}">Open related resource</a>
        </article>
      `,
    )
    .join('');
}

function renderDemoExamples(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-demo-card">
          <span class="pxa-status pxa-status--ready">${item.docs}</span>
          <h3>${item.title}</h3>
          <div class="pxa-doc-demo-links">
            <a href="${siteLinks.demo}#demo/${item.route}">Open demo</a>
            <a href="${siteLinks.demo}${item.input.slice(1)}">Input</a>
            <a href="${siteLinks.demo}${item.output.slice(1)}">Output</a>
            <a href="${siteLinks.demo}${item.source.slice(1)}">Source</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function renderTrackGuide(guide) {
  return `
    <div class="pxa-doc-track-guide">
      <article class="pxa-card">
        <h3>Read first</h3>
        <ol>
          ${guide.readFirst.map((item) => `<li><a href="#${slug(item)}">${item}</a></li>`).join('')}
        </ol>
      </article>
      <article class="pxa-card">
        <h3>Common tasks</h3>
        <ul>
          ${guide.tasks.map((item) => `<li>${item}</li>`).join('')}
        </ul>
      </article>
      <article class="pxa-card">
        <h3>Related links</h3>
        <div class="pxa-doc-related-links">
          ${guide.related.map((item) => `<a href="${item.href}">${item.label}</a>`).join('')}
        </div>
      </article>
    </div>
  `;
}

function renderDetailedDocs(items) {
  return items
    .map(
      (item) => `
        <section class="pxa-card pxa-doc-detail" id="${slug(item.title)}-details">
          <div class="pxa-doc-detail__header">
            <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
            <h3>${item.title}</h3>
            <p>${item.purpose}</p>
          </div>
          <div class="pxa-doc-detail__grid">
            <article>
              <h4>When to use</h4>
              <ul>
                ${item.whenToUse.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
            <article>
              <h4>Core concepts</h4>
              <ul>
                ${item.concepts.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
            <article>
              <h4>Common tasks</h4>
              <ul>
                ${item.tasks.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
          </div>
          <div class="pxa-doc-related-links">
            ${item.related.map((link) => `<a href="${link.href}">${link.label}</a>`).join('')}
          </div>
        </section>
      `,
    )
    .join('');
}

function renderAttributeTable(rows) {
  return `
    <div class="pxa-doc-table-wrap">
      <table class="pxa-doc-attribute-table">
        <thead>
          <tr>
            <th>Attribute</th>
            <th>Type / values</th>
            <th>Usage</th>
          </tr>
        </thead>
        <tbody>
          ${rows.map(([name, type, usage]) => `
            <tr>
              <td><code>${name}</code></td>
              <td>${type}</td>
              <td>${usage}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function renderElementVisual(visual) {
  return `
    <aside class="pxa-doc-element-visual" aria-label="${visual.title}">
      <div class="pxa-doc-element-visual__toolbar">
        <span>Toolbar</span>
        <strong>${visual.toolbar}</strong>
      </div>
      <div class="pxa-doc-element-visual__canvas">
        <div class="pxa-doc-element-visual__page">
          <div class="pxa-doc-element-visual__selection">${visual.preview}</div>
        </div>
      </div>
      <div class="pxa-doc-element-visual__properties">
        <span>Properties panel</span>
        <ul>
          ${visual.properties.map((property) => `<li>${property}</li>`).join('')}
        </ul>
      </div>
    </aside>
  `;
}

function renderElementReference(items) {
  return `
    <section class="pxa-card pxa-doc-element-reference" id="element-reference">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Element Reference</h3>
        <p>
          This is the practical designer reference: how to add elements, which attributes matter,
          and what the generated design JSON looks like.
        </p>
        <a class="pxa-doc-show-all-elements" href="#element-reference">Show all elements</a>
      </div>

      <div class="pxa-doc-element-basics">
        <article>
          <h4>How every element is used</h4>
          <ol>
            <li>Open the live designer.</li>
            <li>Choose the element group in the toolbar.</li>
            <li>Click the element type to place it on the active page.</li>
            <li>Drag, resize, and align it on the canvas.</li>
            <li>Edit content, style, binding, and type-specific properties in the properties panel.</li>
            <li>Preview, export JSON, or open the output in the PDF workflow.</li>
          </ol>
        </article>
        <article>
          <h4>Common attributes</h4>
          ${renderAttributeTable(commonElementAttributes)}
        </article>
        <article>
          <h4>Common style keys</h4>
          ${renderAttributeTable(commonStyleAttributes)}
        </article>
      </div>

      <div class="pxa-doc-element-list">
        ${items.map((item) => `
          <section class="pxa-doc-element-card" id="${slug(item.title)}-element">
            <div class="pxa-doc-element-card__header">
              <div>
                <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
                <h4>${item.title}</h4>
                <p><code>${item.type}</code> - ${item.description}</p>
              </div>
              <span class="pxa-status pxa-status--preview">${item.category}</span>
            </div>

            <div class="pxa-doc-element-card__body">
              <div class="pxa-doc-element-card__content">
                <h5>How to use</h5>
                <ol>
                  ${item.addSteps.map((step) => `<li>${step}</li>`).join('')}
                </ol>

                <h5>Use cases</h5>
                <ul>
                  ${item.usage.map((usage) => `<li>${usage}</li>`).join('')}
                </ul>

                <h5>Attributes</h5>
                ${renderAttributeTable(item.attributes)}

                <h5>Design JSON example</h5>
                <pre class="pxa-code"><code>${escapeHtml(item.example)}</code></pre>
              </div>
              ${renderElementVisual(item.visual)}
            </div>
          </section>
        `).join('')}
      </div>
    </section>
  `;
}

function renderMigrationProviderTable(headers, rows) {
  return `
    <div class="pxa-doc-table-wrap">
      <table class="pxa-doc-attribute-table">
        <thead>
          <tr>${headers.map((header) => `<th>${header}</th>`).join('')}</tr>
        </thead>
        <tbody>
          ${rows.map((row) => `
            <tr>${row.map((cell) => `<td>${cell}</td>`).join('')}</tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function renderMigrationDetails() {
  return `
    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="pdf-code-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--preview">Preview</span>
        <h3>PDF Code Migration</h3>
        <p>Convert third-party C# PDF SDK generation code into PXA-compatible PDF generator code, then review diagnostics for unsupported provider-specific features.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have existing C# PDF generation code.</li><li>You want generated PXA code as a starting point.</li><li>The source mostly creates pages, draws text, lines, rectangles, images, and saves output.</li></ul></article>
        <article><h4>Typical output</h4><ul><li>PXA-compatible PDF code.</li><li>Previewable PDF output where deterministic.</li><li>Diagnostics for manual follow-up patterns.</li></ul></article>
        <article><h4>Manual follow-ups</h4><ul><li>Existing-PDF editing.</li><li>Digital signatures and advanced forms.</li><li>HTML/Razor/URL rendering and provider runtime services.</li></ul></article>
      </div>
      ${renderMigrationProviderTable(['Provider', 'Status', 'Mapped patterns', 'Manual / diagnostic areas'], pdfCodeMigrationProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="report-designer-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Report Designer Migration</h3>
        <p>Convert report designer formats into editable PXA design JSON so the result can be opened in PXA Designer and refined visually.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have visual report definitions, not hand-written PDF code.</li><li>You need editable PXA Designer output.</li><li>You want banded report layout flattened into page elements.</li></ul></article>
        <article><h4>Common mapping</h4><ul><li>Report bands become absolute page positions.</li><li>Labels/textboxes become Text elements.</li><li>Lines, rectangles, tables, charts, barcodes, and images map to closest PXA elements.</li></ul></article>
        <article><h4>Validation workflow</h4><ul><li>Run report-to-design migration.</li><li>Inspect diagnostics and mapping notes.</li><li>Open in Designer and compare layout fidelity.</li></ul></article>
      </div>
      ${renderMigrationProviderTable(['Provider', 'Input', 'Mapped surface', 'Namespace'], reportDesignerProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="spreadsheet-code-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--preview">Preview</span>
        <h3>Spreadsheet Code Migration</h3>
        <p>Convert C# spreadsheet library authoring code into PXA spreadsheet API code and preview workbook-like output through the spreadsheet migration flow.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have existing workbook generation code.</li><li>You need PXA spreadsheet API output.</li><li>You want provider-specific object models normalized into one workbook model.</li></ul></article>
        <article><h4>Mapped patterns</h4><ul><li>Workbook and worksheet creation.</li><li>Cell values, formulas, ranges, and basic styles.</li><li>Save/export calls where deterministic.</li></ul></article>
        <article><h4>Datasource boundary</h4><ul><li>Code migration is not datasource/file import.</li><li>Spreadsheet datasource namespace is reserved.</li><li>File import stays in spreadsheet/importer workflows.</li></ul></article>
      </div>
      ${renderMigrationProviderTable(['Provider', 'Status', 'Namespace', 'Mapped surface'], spreadsheetCodeMigrationProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="provider-taxonomy">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Provider Taxonomy</h3>
        <p>Migration namespaces identify domain, migration kind, and provider so code migration and designer migration are not confused.</p>
      </div>
      ${renderMigrationProviderTable(['Area', 'Namespace pattern', 'Meaning'], taxonomyRows)}
      <pre class="pxa-code"><code>PXA.Migration.&lt;Domain&gt;.&lt;Kind&gt;.&lt;Provider&gt;

PXA.Migration.Pdf.Code.Aspose
PXA.Migration.Report.Designer.Rdl
PXA.Migration.Spreadsheet.Code.Syncfusion</code></pre>
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="migration-diagnostics">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Migration Diagnostics</h3>
        <p>Diagnostics explain what was converted, what needs review, and which provider features must be migrated manually.</p>
      </div>
      ${renderMigrationProviderTable(['Signal', 'Meaning'], diagnosticRows)}
      <div class="pxa-doc-related-links">
        <a href="../../checklists/Migration-Namespace-Taxonomy.md">Namespace taxonomy checklist</a>
        <a href="../../checklists/Designer-Migration.md">Designer migration roadmap</a>
        <a href="../../checklists/Spreadsheet-Migration.md">Spreadsheet migration checklist</a>
      </div>
    </section>
  `;
}

function slug(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function descriptionFor(item) {
  const descriptions = {
    Designer: 'Build and inspect document templates with visual workflows.',
    Templates: 'Understand template structure, variables, repeats, and validation.',
    Elements: 'Use text, tables, charts, images, forms, shapes, and layout primitives.',
    'PDF Viewer': 'Review PDFs, forms, annotations, and viewer-side workflows.',
    Spreadsheet: 'Import, map, edit, and export workbook-driven data flows.',
    Importer: 'Normalize incoming PDF, Office, image, and document files.',
    Export: 'Generate final outputs and code-oriented artifacts.',
    'PXA.Generator': 'Generate PDFs and document output from .NET code.',
    'PXA.Migration': 'Convert provider-specific code and designer formats into PXA.',
    'PXA.Importer': 'Integrate file importers into automation flows.',
    'PXA.Infrastructure': 'Understand rendering, conversion, and persistence boundaries.',
    'PXA.WebApi': 'Use HTTP endpoints for migration, import, and export flows.',
    'API Reference': 'Open generated API docs and OpenAPI reference material.',
  };

  return descriptions[item] || 'Documentation entry planned for this product area.';
}

document.querySelector('#app').innerHTML = `
  <div class="pxa-site pxa-site--documentation">
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="/" aria-label="PXA.Documentation home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Documentation</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}">Company</a>
          <a href="${companyPage('products')}">Products</a>
          <a href="${siteLinks.documentation}" aria-current="page">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="${companyPage('pricing')}">Pricing</a>
          <a href="${companyPage('about')}">About</a>
          <a href="${companyPage('support')}">Support</a>
        </nav>
        <div class="pxa-header-actions">
          <a class="pxa-button pxa-button--secondary pxa-header-cta" href="${siteLinks.designer}">Live demo</a>
          <a class="pxa-button pxa-button--primary pxa-header-cta" href="${companyPage('contact')}">Contact sales</a>
        </div>
      </div>
    </header>

    <main class="pxa-site-main">
      <div class="pxa-page-header">
        <div class="pxa-docs-container">
          <p class="pxa-kicker">PXA Documentation</p>
          <h1 class="pxa-heading">Build with PXA from the editor, from code, or from a migration path.</h1>
          <p class="pxa-lede">
            Use this documentation as the technical map for Power Dox Automation:
            visual authoring, .NET integration, provider migration, demos, and generated references.
          </p>
        </div>
      </div>

      <div class="pxa-docs-layout">
        <aside class="pxa-docs-sidebar">
          <input class="pxa-search" type="search" placeholder="Search documentation" aria-label="Search documentation">
          <nav class="pxa-card pxa-doc-nav" aria-label="Documentation sections">
            <strong>Editor</strong>
            ${renderDetailNavList(editorSections)}
            ${renderElementNav(elementReferenceDocs)}
            <strong>Code SDK</strong>
            ${renderNavList(codeSections)}
            <strong>Migration</strong>
            ${renderNavList(migrationGuides)}
          </nav>
          <p class="pxa-doc-search-empty" hidden>No documentation entries found.</p>
        </aside>

        <article class="pxa-docs-content">
          <section class="pxa-doc-hero-grid" aria-label="Documentation entry points">
            ${renderEntryPoints(docEntryPoints)}
          </section>

          <section class="pxa-doc-section" id="overview">
            <p class="pxa-kicker">Overview</p>
            <h2 class="pxa-heading">Four ways into the platform</h2>
            <div class="pxa-feature-grid">
              <article class="pxa-card"><h3>Product map</h3><p>Understand how Generator, Migration, Importer, Designer, PDF Viewer, and Spreadsheet connect.</p></article>
              <article class="pxa-card"><h3>Local setup</h3><p>Run the backend, designer, documentation site, and demo gallery on separate local ports.</p></article>
              <article class="pxa-card"><h3>Core concepts</h3><p>Learn design JSON, pages, elements, bindings, migration diagnostics, and provider taxonomy.</p></article>
              <article class="pxa-card"><h3>Examples first</h3><p>Use demo input, output, and source links to validate workflows before deeper integration.</p></article>
            </div>
          </section>

          <section class="pxa-doc-section" id="quickstarts">
            <p class="pxa-kicker">Quickstarts</p>
            <h2 class="pxa-heading">Start with the path closest to your task</h2>
            <div class="pxa-doc-quickstart-grid">
              ${renderQuickstarts(quickstarts)}
            </div>
          </section>

          <section class="pxa-doc-section" id="editor-path">
            <p class="pxa-kicker">Editor documentation</p>
            <h2 class="pxa-heading">Product guides for visual document workflows</h2>
            ${renderTrackGuide(trackGuides.editor)}
            <div class="pxa-doc-detail-stack">
              ${renderDetailedDocs(editorDocs)}
              ${renderElementReference(elementReferenceDocs)}
            </div>
          </section>

          <section class="pxa-doc-section" id="code-path">
            <p class="pxa-kicker">Code documentation</p>
            <h2 class="pxa-heading">SDK and WebApi entry points</h2>
            ${renderTrackGuide(trackGuides.code)}
            <div class="pxa-doc-card-grid">
              ${renderCards(codeSections)}
            </div>
          </section>

          <section class="pxa-doc-section" id="migration">
            <p class="pxa-kicker">Migration guides</p>
            <h2 class="pxa-heading">Provider-oriented migration documentation</h2>
            ${renderTrackGuide(trackGuides.migration)}
            <div class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="migration-workflow">
              <div class="pxa-doc-detail__header">
                <span class="pxa-status pxa-status--ready">Ready</span>
                <h3>Common Migration Workflow</h3>
                <p>Every migration path follows the same review loop: choose the source technology, convert, inspect diagnostics, then open or export the result.</p>
              </div>
              ${renderMigrationProviderTable(['Step', 'What happens'], migrationWorkflowSteps)}
            </div>
            <div class="pxa-doc-detail-stack">
              ${renderMigrationDetails()}
            </div>
          </section>

          <section class="pxa-doc-section" id="cookbook">
            <p class="pxa-kicker">Cookbook</p>
            <h2 class="pxa-heading">Task-based examples</h2>
            <p>
              Cookbook entries explain common implementation tasks and point to the closest demo,
              product page, or reference material while the full article set is being expanded.
            </p>
            <div class="pxa-doc-cookbook-grid">
              ${renderCookbook(cookbook)}
            </div>
          </section>

          <section class="pxa-doc-section" id="demo-examples">
            <p class="pxa-kicker">Demo examples</p>
            <h2 class="pxa-heading">Runnable examples connect docs, input, output, and source</h2>
            <p>
              PXA.Demo hosts lightweight example files for every demo card. These links make
              documentation topics directly traceable to the examples used in the demo gallery.
            </p>
            <div class="pxa-doc-demo-grid">
              ${renderDemoExamples(demoExamples)}
            </div>
          </section>

          <section class="pxa-doc-section" id="api-reference">
            <p class="pxa-kicker">API reference</p>
            <h2 class="pxa-heading">Generated references stay connected</h2>
            <p>
              The existing DocFX and OpenAPI outputs remain the source for generated reference material.
              This website provides product-first entry points that link into those generated docs.
            </p>
            <div class="pxa-doc-reference-note">
              <article class="pxa-card">
                <h3>Use references after choosing a product path</h3>
                <p>Start from Generator, Migration, Importer, WebApi, or Designer guidance, then jump into generated API details.</p>
              </article>
              <article class="pxa-card">
                <h3>Keep generated docs separate</h3>
                <p>Generated DocFX and OpenAPI outputs stay authoritative for signatures and contracts; this site stays task-oriented.</p>
              </article>
            </div>
            <div class="pxa-doc-reference-grid">
              ${renderReferenceLinks(referenceLinks)}
            </div>
            <pre class="pxa-code"><code>docfx build docs/docfx.json</code></pre>
          </section>

          <section class="pxa-doc-section" id="history">
            <p class="pxa-kicker">History and planning</p>
            <h2 class="pxa-heading">Checklists stay as implementation history</h2>
            <p>
              Product documentation should describe current behavior. Checklists remain useful for
              roadmap decisions, migration status, implementation notes, and historical context.
            </p>
            <div class="pxa-company-badges">
              ${renderChecklistLinks(checklistLinks)}
            </div>
          </section>
        </article>

        <aside class="pxa-docs-toc">
          <div class="pxa-card pxa-doc-toc">
            <strong>On this page</strong>
            <a href="#overview">Overview</a>
            <a href="#quickstarts">Quickstarts</a>
            <a href="#editor-path">Editor path</a>
            <a href="#element-reference">Element reference</a>
            <a href="#code-path">Code path</a>
            <a href="#migration">Migration</a>
            <a href="#cookbook">Cookbook</a>
            <a href="#demo-examples">Demo examples</a>
            <a href="#api-reference">API reference</a>
            <a href="#history">History</a>
          </div>
        </aside>
      </div>
    </main>

    ${renderPxaFooter('PXA.Documentation')}
  </div>
`;

initDocumentationScrollSpy();
initDocumentationSearch();

function initDocumentationSearch() {
  const search = document.querySelector('.pxa-search');
  const nav = document.querySelector('.pxa-doc-nav');
  const empty = document.querySelector('.pxa-doc-search-empty');
  if (!search || !nav) return;

  const links = [...nav.querySelectorAll('a')];
  const headings = [...nav.querySelectorAll('strong')];
  const sections = [...nav.querySelectorAll('details')];

  const setFiltered = (element, hidden) => {
    element.classList.toggle('is-search-hidden', hidden);
    element.setAttribute('aria-hidden', hidden ? 'true' : 'false');
  };

  const applyFilter = () => {
    const query = search.value.trim().toLowerCase();
    let visibleLinks = 0;

    links.forEach((link) => {
      const matches = !query || link.textContent.toLowerCase().includes(query);
      setFiltered(link, !matches);
      if (matches) visibleLinks += 1;
    });

    sections.forEach((section) => {
      const visibleChildLinks = [...section.querySelectorAll('a')].some((link) => !link.classList.contains('is-search-hidden'));
      const summaryMatches = section.querySelector('summary')?.textContent.toLowerCase().includes(query) ?? false;
      const isVisible = !query || visibleChildLinks || summaryMatches;
      setFiltered(section, !isVisible);
      if (query && isVisible) section.setAttribute('open', '');
    });

    headings.forEach((heading) => {
      let next = heading.nextElementSibling;
      let hasVisibleItem = false;
      while (next && next.tagName !== 'STRONG') {
        if (!next.classList.contains('is-search-hidden')) hasVisibleItem = true;
        next = next.nextElementSibling;
      }
      setFiltered(heading, query ? !hasVisibleItem : false);
    });

    if (empty) empty.hidden = !query || visibleLinks > 0;
  };

  search.addEventListener('input', applyFilter);
  search.addEventListener('keyup', applyFilter);
  search.addEventListener('search', applyFilter);
}

function initDocumentationScrollSpy() {
  const links = [...document.querySelectorAll('.pxa-doc-nav a[href^="#"], .pxa-doc-toc a[href^="#"]')];
  const sidebarLinks = [...document.querySelectorAll('.pxa-doc-nav a[href^="#"]')];
  const elementReference = document.querySelector('.pxa-doc-element-reference');
  const elementCards = [...document.querySelectorAll('.pxa-doc-element-card')];
  const contentBlocks = [...document.querySelectorAll('.pxa-docs-content > .pxa-doc-hero-grid, .pxa-docs-content > .pxa-doc-section')];
  const targets = links
    .map((link) => document.querySelector(link.getAttribute('href')))
    .filter(Boolean);

  if (!links.length || !targets.length) return;

  let activeSidebarFocusId = '';

  const isElementCardId = (id) => elementCards.some((card) => card.id === id);
  const isSidebarTargetId = (id) => sidebarLinks.some((link) => link.getAttribute('href') === `#${id}`);

  const getFocusContainer = (id) => {
    const target = document.getElementById(id);
    if (!target) return null;
    return target.closest('.pxa-doc-element-card, .pxa-doc-detail, .pxa-doc-card, .pxa-doc-section');
  };

  const focusContainers = [...document.querySelectorAll('.pxa-doc-detail, .pxa-doc-card, .pxa-doc-element-card')];

  const applySidebarFocus = (id, enabled) => {
    const selected = enabled ? getFocusContainer(id) : null;
    const selectedContentBlock = selected?.closest('.pxa-doc-section, .pxa-doc-hero-grid') ?? null;
    activeSidebarFocusId = selected ? id : '';
    elementReference?.classList.toggle('is-filtered', Boolean(selected && isElementCardId(id)));
    contentBlocks.forEach((block) => {
      const isHidden = Boolean(selectedContentBlock && block !== selectedContentBlock);
      block.classList.toggle('is-hidden-by-filter', isHidden);
      block.setAttribute('aria-hidden', isHidden ? 'true' : 'false');
    });
    focusContainers.forEach((container) => {
      const isFocused = container === selected;
      const isAncestorOfSelected = Boolean(selected && container.contains(selected));
      const isHidden = Boolean(selected && !isFocused && !isAncestorOfSelected);
      container.classList.toggle('is-focused', isFocused);
      container.classList.toggle('is-hidden-by-filter', isHidden);
      container.setAttribute('aria-hidden', isHidden ? 'true' : 'false');
    });
  };

  const closeSiblingElementGroups = (activeLink) => {
    const activeGroup = activeLink?.closest('.pxa-doc-nav__section--nested');
    document.querySelectorAll('.pxa-doc-nav__section--nested[open]').forEach((section) => {
      if (section !== activeGroup) section.removeAttribute('open');
    });
  };

  const setActive = (id, options = {}) => {
    if (options.fromObserver && activeSidebarFocusId) {
      return;
    }

    applySidebarFocus(id, Boolean(options.fromSidebar));
    links.forEach((link) => {
      const isActive = link.getAttribute('href') === `#${id}`;
      link.classList.toggle('is-active', isActive);
      if (isActive) {
        link.closest('details')?.setAttribute('open', '');
        link.closest('details')?.parentElement?.closest('details')?.setAttribute('open', '');
        if (isElementCardId(id)) closeSiblingElementGroups(link);
      }
    });
  };

  links.forEach((link) => {
    link.addEventListener('click', () => {
      const id = link.getAttribute('href')?.slice(1);
      if (id) setActive(id, { fromSidebar: link.closest('.pxa-doc-nav') !== null });
    });
  });

  const initialId = window.location.hash.slice(1) || 'overview';
  if (document.getElementById(initialId)) setActive(initialId, { fromSidebar: isSidebarTargetId(initialId) });

  window.addEventListener('hashchange', () => {
    const id = window.location.hash.slice(1) || 'overview';
    if (document.getElementById(id)) setActive(id, { fromSidebar: isSidebarTargetId(id) });
  });

  if (!('IntersectionObserver' in window)) return;

  const observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);

      if (visible[0]?.target?.id) {
        setActive(visible[0].target.id, { fromObserver: true });
      }
    },
    {
      rootMargin: '-18% 0px -68% 0px',
      threshold: [0, 0.1, 0.25, 0.5],
    },
  );

  targets.forEach((target) => observer.observe(target));
}
