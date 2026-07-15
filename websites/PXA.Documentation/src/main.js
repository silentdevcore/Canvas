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
            ${renderNavList(editorSections)}
            <strong>Code SDK</strong>
            ${renderNavList(codeSections)}
            <strong>Migration</strong>
            ${renderNavList(migrationGuides)}
          </nav>
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
            <div class="pxa-doc-card-grid">
              ${renderCards(editorSections)}
            </div>
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
            <div class="pxa-doc-card-grid">
              ${renderCards(migrationGuides)}
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
