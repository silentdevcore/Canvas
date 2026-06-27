// Element catalog — the single source of truth for Canvas element documentation.
//
// One entry per frontend ElementType. Drives the in-app docs (DocsPage), the Help dialog (HelpModal),
// the AI artifacts (llms.txt / JSON examples), and a drift-guard test that keeps this in lock-step with
// the ElementType union. Grounded in the backend ElementDto (src/Canvas.Core/Contracts/DesignExportDto.cs)
// and the per-format renderer switches (Canvas.WebApi/Infrastructure/DesignJsonMapper.cs,
// src/Canvas.Infrastructure.Word/WordDocumentExporter.cs).
//
// Common element properties (id, type, x, y, width, height, name, hidden, locked, style, binding,
// expression, visibleExpression) are shared by every element and documented once in COMMON_PROPERTIES;
// each entry below lists only its TYPE-SPECIFIC properties.

import type { ElementType } from '../types';

export type ElementCategory = 'Text' | 'Form' | 'Visual' | 'Shapes & Layout' | 'Document';

export interface ElementProperty {
  name: string;
  type: string; // "string" | "number" | "boolean" | "string[]" | "enum" | "object" | "string[][]" | ...
  allowedValues?: string[];
  default?: string;
  description: string;
}

export interface FormatSupport {
  pdf: boolean;
  word: boolean;
  html: boolean;
  excel: boolean;
}

export interface ElementDoc {
  type: ElementType;
  label: string;
  category: ElementCategory;
  /** One- to two-sentence explanation of what the element is and when to use it. */
  description: string;
  formatSupport: FormatSupport;
  /** True if the element's value can come from a data binding / template expression. */
  bindable: boolean;
  /** Type-specific properties (the shared ones live in COMMON_PROPERTIES). */
  properties: ElementProperty[];
  /** A minimal ElementDto for this element; wrap with toDesign() for a complete DesignExportDto. */
  example: Record<string, unknown>;
  /** Optional equivalent using the imperative Canvas.Pdf C# API. */
  csharpExample?: string;
}

export const CATEGORY_ORDER: ElementCategory[] = ['Text', 'Form', 'Visual', 'Shapes & Layout', 'Document'];

/** Properties every element shares (from ElementDto). Documented once, not repeated per entry. */
export const COMMON_PROPERTIES: ElementProperty[] = [
  { name: 'id', type: 'string', description: 'Unique element id within the page.' },
  { name: 'type', type: 'string', description: 'The element type (one of the catalog keys).' },
  { name: 'x', type: 'number', description: 'Left position in page units (points by default).' },
  { name: 'y', type: 'number', description: 'Top position in page units (points by default).' },
  { name: 'width', type: 'number', description: 'Width in page units.' },
  { name: 'height', type: 'number', description: 'Height in page units.' },
  { name: 'name', type: 'string', description: 'Optional human label for the element.' },
  { name: 'hidden', type: 'boolean', default: 'false', description: 'Hide from render output.' },
  { name: 'locked', type: 'boolean', default: 'false', description: 'Prevent selection/edit in the designer.' },
  { name: 'style', type: 'object', description: 'CSS-like style map (see STYLE_KEYS).' },
  { name: 'binding', type: 'string', description: 'Data path whose value replaces the content at render time.' },
  { name: 'expression', type: 'string', description: 'Template expression ($iif, $sum, $concat, …) evaluated at render time.' },
  { name: 'visibleExpression', type: 'string', description: 'Boolean expression; the element renders only when it is truthy.' },
];

/** Common keys accepted in the `style` map. Not exhaustive; renderers ignore unknown keys. */
export const STYLE_KEYS: ElementProperty[] = [
  { name: 'fontSize', type: 'number', description: 'Font size in points.' },
  { name: 'fontFamily', type: 'string', description: 'Font family name.' },
  { name: 'fontWeight', type: 'string', allowedValues: ['normal', 'bold'], description: 'Font weight.' },
  { name: 'fontStyle', type: 'string', allowedValues: ['normal', 'italic'], description: 'Font style.' },
  { name: 'color', type: 'string', description: 'Text/foreground color (#hex, rgb(), hsl(), or CSS name).' },
  { name: 'textAlign', type: 'string', allowedValues: ['left', 'center', 'right', 'justify'], description: 'Horizontal text alignment.' },
  { name: 'backgroundColor', type: 'string', description: 'Fill behind the element (#hex, rgb(), hsl(), or CSS name).' },
  { name: 'borderColor', type: 'string', description: 'Border color.' },
  { name: 'borderWidth', type: 'number', description: 'Border thickness in points.' },
  { name: 'borderRadius', type: 'number', description: 'Corner radius in points.' },
  { name: 'padding', type: 'number', description: 'Inner padding in points.' },
  { name: 'lineHeight', type: 'number', description: 'Line height multiplier.' },
  { name: 'opacity', type: 'number', description: 'Opacity 0–1 (where the format supports it).' },
  { name: 'rotation', type: 'number', description: 'Rotation in degrees.' },
];

const PDF_WORD_HTML = { pdf: true, word: true, html: true, excel: false };
const ALL = { pdf: true, word: true, html: true, excel: true };

/** Wrap an example element into a complete, minimal DesignExportDto (A4 portrait). */
export function toDesign(element: Record<string, unknown>, name = 'Example'): Record<string, unknown> {
  return {
    id: 'doc-1',
    name,
    pages: [{ id: 'p1', elements: [element] }],
    pageSettings: { width: 595, height: 842, orientation: 'portrait', unit: 'pt' },
  };
}

export const ELEMENT_CATALOG: ElementDoc[] = [
  // ── Text ────────────────────────────────────────────────────────────────────────────────────────
  {
    type: 'text', label: 'Text Block', category: 'Text',
    description: 'Single- or multi-line static text with full typography. Supports {{variables}} and expressions.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'content', type: 'string', description: 'The text to render; may contain {{tokens}}.' },
      { name: 'headingLevel', type: 'number', allowedValues: ['1', '2', '3'], description: 'Mark as a heading for Table-of-Contents generation.' },
    ],
    example: { id: 'el1', type: 'text', x: 40, y: 40, width: 300, height: 24, content: 'Invoice {{Number}}', style: { fontSize: 18, fontWeight: 'bold' } },
    csharpExample: 'page.DrawText("Invoice 1001", x: 40, y: 800, new PdfDrawTextOptions { FontSize = 18, Bold = true });',
  },
  {
    type: 'richtext', label: 'Rich Text', category: 'Text',
    description: 'HTML-formatted paragraph supporting bold, italic, lists, and inline styles.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [{ name: 'htmlContent', type: 'string', description: 'Inline HTML (a restricted subset) rendered as flowing rich text.' }],
    example: { id: 'el1', type: 'richtext', x: 40, y: 40, width: 360, height: 80, htmlContent: '<p>Hello <b>world</b> — <i>welcome</i>.</p>' },
  },
  {
    type: 'date', label: 'Date', category: 'Text',
    description: 'Static or render-time date with locale/timezone-aware formatting.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'dateMode', type: 'enum', allowedValues: ['static', 'render', 'binding'], default: 'static', description: 'Where the date value comes from.' },
      { name: 'dateFormat', type: 'string', default: 'dd.MM.yyyy', description: 'Format pattern.' },
      { name: 'locale', type: 'string', description: 'BCP-47 locale (e.g. "de-DE").' },
      { name: 'timezone', type: 'string', description: 'IANA timezone id (e.g. "Europe/Berlin").' },
      { name: 'fallbackText', type: 'string', description: 'Shown when no value resolves.' },
    ],
    example: { id: 'el1', type: 'date', x: 40, y: 40, width: 160, height: 20, dateMode: 'render', dateFormat: 'dd.MM.yyyy', locale: 'de-DE' },
  },
  {
    type: 'pagenumber', label: 'Page Number', category: 'Text',
    description: 'Auto-incremented page number / page-of-total placeholder.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'numberingFormat', type: 'enum', allowedValues: ['current', 'total', 'pageOfTotal', 'roman', 'alphabetic'], default: 'current', description: 'What to display.' },
      { name: 'startNumber', type: 'number', default: '1', description: 'Number assigned to the first page.' },
      { name: 'prefix', type: 'string', description: 'Text before the number.' },
      { name: 'suffix', type: 'string', description: 'Text after the number.' },
    ],
    example: { id: 'el1', type: 'pagenumber', x: 270, y: 810, width: 60, height: 16, numberingFormat: 'pageOfTotal', prefix: 'Page ' },
  },
  {
    type: 'number', label: 'Number', category: 'Text',
    description: 'A formatted numeric value (currency, percent, decimal, …) with locale-aware output.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'numberValue', type: 'number', description: 'The value to format.' },
      { name: 'numberStyle', type: 'enum', allowedValues: ['decimal', 'currency', 'percent', 'scientific', 'ordinal'], default: 'decimal', description: 'Formatting style.' },
      { name: 'numberDecimals', type: 'number', description: 'Fraction digits.' },
      { name: 'numberCurrency', type: 'string', description: 'ISO currency code (e.g. "EUR") for currency style.' },
      { name: 'numberLocale', type: 'string', description: 'BCP-47 locale for grouping/decimal separators.' },
    ],
    example: { id: 'el1', type: 'number', x: 40, y: 40, width: 120, height: 20, numberValue: 1234.5, numberStyle: 'currency', numberCurrency: 'EUR', numberLocale: 'de-DE' },
  },
  {
    type: 'toc', label: 'Table of Contents', category: 'Text',
    description: 'Auto-generated table of contents built from heading-level text elements.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'tocTitle', type: 'string', description: 'Heading shown above the entries.' },
      { name: 'tocShowPageNumbers', type: 'boolean', default: 'true', description: 'Show page numbers.' },
      { name: 'tocShowLeaderDots', type: 'boolean', default: 'true', description: 'Show dotted leaders.' },
      { name: 'tocMinLevel', type: 'number', description: 'Lowest heading level to include.' },
      { name: 'tocMaxLevel', type: 'number', description: 'Highest heading level to include.' },
      { name: 'tocPlacement', type: 'enum', allowedValues: ['beginning', 'end'], description: 'Where the generated pages are inserted.' },
    ],
    example: { id: 'el1', type: 'toc', x: 40, y: 40, width: 460, height: 200, tocTitle: 'Contents', tocShowLeaderDots: true },
  },
  {
    type: 'footnote', label: 'Footnote', category: 'Text',
    description: 'Inline footnote reference; native in DOCX, fallback-rendered in PDF.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'footnoteText', type: 'string', description: 'The footnote body.' },
      { name: 'footnoteRef', type: 'string', description: 'Reference marker.' },
    ],
    example: { id: 'el1', type: 'footnote', x: 40, y: 40, width: 200, height: 16, footnoteText: 'See appendix A.' },
  },
  {
    type: 'endnote', label: 'Endnote', category: 'Text',
    description: 'Inline endnote reference collected at the end of the document; native in DOCX.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [{ name: 'footnoteText', type: 'string', description: 'The endnote body (shares the footnote fields).' }],
    example: { id: 'el1', type: 'endnote', x: 40, y: 40, width: 200, height: 16, footnoteText: 'Source: internal data.' },
  },

  // ── Form ────────────────────────────────────────────────────────────────────────────────────────
  {
    type: 'field', label: 'Text Field', category: 'Form',
    description: 'Interactive single-line PDF/Word form field for user input.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'fieldLabel', type: 'string', description: 'Visible label.' },
      { name: 'fieldName', type: 'string', description: 'Form field identifier.' },
      { name: 'placeholder', type: 'string', description: 'Placeholder text.' },
      { name: 'required', type: 'boolean', description: 'Mark as required.' },
    ],
    example: { id: 'el1', type: 'field', x: 40, y: 40, width: 220, height: 24, fieldLabel: 'Name', fieldName: 'name', placeholder: 'Your name' },
    csharpExample: 'page.AddTextField(fieldName: "name", x: 40, y: 760, width: 220, height: 24);',
  },
  {
    type: 'textarea', label: 'Text Area', category: 'Form',
    description: 'Multi-line fillable text input for comments or descriptions.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'fieldName', type: 'string', description: 'Form field identifier.' },
      { name: 'placeholder', type: 'string', description: 'Placeholder text.' },
    ],
    example: { id: 'el1', type: 'textarea', x: 40, y: 40, width: 300, height: 90, fieldName: 'comments', placeholder: 'Notes…' },
    csharpExample: 'page.AddMultilineTextField(fieldName: "comments", x: 40, y: 700, width: 300, height: 90);',
  },
  {
    type: 'checkbox', label: 'Checkbox', category: 'Form',
    description: 'Single boolean checkbox form field.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'fieldName', type: 'string', description: 'Form field identifier.' },
      { name: 'checkState', type: 'enum', allowedValues: ['checked', 'cross', 'dot', 'empty'], default: 'empty', description: 'Initial state / glyph.' },
    ],
    example: { id: 'el1', type: 'checkbox', x: 40, y: 40, width: 16, height: 16, fieldName: 'agree', checkState: 'checked' },
    csharpExample: 'page.AddCheckBox(fieldName: "agree", x: 40, y: 780, size: 16, isChecked: true);',
  },
  {
    type: 'radio', label: 'Radio Group', category: 'Form',
    description: 'Single-select radio buttons from a list of options.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'options', type: 'string[]', description: 'Option values.' },
      { name: 'selectedValue', type: 'string', description: 'Currently selected option.' },
    ],
    example: { id: 'el1', type: 'radio', x: 40, y: 40, width: 200, height: 60, options: ['Yes', 'No', 'Maybe'], selectedValue: 'Yes' },
  },
  {
    type: 'dropdown', label: 'Dropdown', category: 'Form',
    description: 'Select dropdown (combo box) with a configurable list of options.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'options', type: 'string[]', description: 'Option values.' },
      { name: 'selectedValue', type: 'string', description: 'Currently selected option.' },
      { name: 'fieldName', type: 'string', description: 'Form field identifier.' },
    ],
    example: { id: 'el1', type: 'dropdown', x: 40, y: 40, width: 200, height: 24, fieldName: 'country', options: ['DE', 'FR', 'US'], selectedValue: 'DE' },
    csharpExample: 'page.AddComboBox(fieldName: "country", x: 40, y: 760, width: 200, height: 24, options: new[] { "DE", "FR", "US" }, selectedIndex: 0);',
  },
  {
    type: 'optionlist', label: 'Option List', category: 'Form',
    description: 'A bulleted/numbered list of selectable options (renders as a static list outside form contexts).',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'options', type: 'string[]', description: 'List items.' },
      { name: 'ordered', type: 'boolean', description: 'Numbered vs. bulleted.' },
      { name: 'listStyle', type: 'string', allowedValues: ['decimal', 'alpha', 'roman', 'disc', 'square'], description: 'Marker style.' },
    ],
    example: { id: 'el1', type: 'optionlist', x: 40, y: 40, width: 240, height: 80, options: ['First', 'Second', 'Third'], ordered: true, listStyle: 'decimal' },
  },
  {
    type: 'button', label: 'Button', category: 'Form',
    description: 'A clickable button that navigates to a URL or an internal page.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'content', type: 'string', description: 'Button caption.' },
      { name: 'buttonAction', type: 'string', description: 'A URL, or "page:N" for internal navigation.' },
    ],
    example: { id: 'el1', type: 'button', x: 40, y: 40, width: 120, height: 32, content: 'Open', buttonAction: 'https://example.com' },
  },
  {
    type: 'signature', label: 'Signature', category: 'Form',
    description: 'A signature line placeholder for hand or digital signing.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [{ name: 'signatureLabel', type: 'string', description: 'Caption under the line.' }],
    example: { id: 'el1', type: 'signature', x: 40, y: 40, width: 220, height: 50, signatureLabel: 'Authorized signature' },
  },

  // ── Visual ──────────────────────────────────────────────────────────────────────────────────────
  {
    type: 'image', label: 'Image', category: 'Visual',
    description: 'Embedded raster or vector image with crop and fit modes.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'content', type: 'string', description: 'Image URL or data URI.' },
      { name: 'fitMode', type: 'enum', allowedValues: ['contain', 'cover', 'fill', 'none'], default: 'contain', description: 'How the image fits its box.' },
      { name: 'preserveAspectRatio', type: 'boolean', description: 'Keep aspect ratio.' },
      { name: 'cropX', type: 'number', description: 'Crop bounds (with cropY/cropWidth/cropHeight).' },
    ],
    example: { id: 'el1', type: 'image', x: 40, y: 40, width: 120, height: 120, content: 'https://example.com/logo.png', fitMode: 'contain' },
    csharpExample: 'page.DrawImage("logo.png", x: 40, y: 680, width: 120, height: 120);',
  },
  {
    type: 'qrcode', label: 'QR Code', category: 'Visual',
    description: 'A QR code generated from a URL or data string.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'qrValue', type: 'string', description: 'Encoded value.' },
      { name: 'qrSize', type: 'number', description: 'Module size hint.' },
    ],
    example: { id: 'el1', type: 'qrcode', x: 40, y: 40, width: 96, height: 96, qrValue: 'https://example.com' },
  },
  {
    type: 'barcode', label: 'Barcode', category: 'Visual',
    description: 'A linear barcode (Code128, EAN, UPC, …) generated from a value.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'barcodeValue', type: 'string', description: 'Encoded value.' },
      { name: 'barcodeType', type: 'string', allowedValues: ['Code128', 'EAN13', 'UPCA', 'Code39'], description: 'Symbology.' },
    ],
    example: { id: 'el1', type: 'barcode', x: 40, y: 40, width: 200, height: 60, barcodeValue: '4006381333931', barcodeType: 'EAN13' },
  },
  {
    type: 'chart', label: 'Chart', category: 'Visual',
    description: 'A bar, line, or pie chart rendered from inline data. (PDF/HTML; not native in Word.)',
    formatSupport: { pdf: true, word: false, html: true, excel: false }, bindable: true,
    properties: [
      { name: 'chartType', type: 'enum', allowedValues: ['bar', 'line', 'pie'], default: 'bar', description: 'Chart kind.' },
      { name: 'chartData', type: 'object', description: 'Labels + datasets (Chart.js-style data object).' },
    ],
    example: { id: 'el1', type: 'chart', x: 40, y: 40, width: 320, height: 200, chartType: 'bar', chartData: { labels: ['Q1', 'Q2'], datasets: [{ label: 'Sales', data: [10, 20] }] } },
  },

  // ── Shapes & Layout ─────────────────────────────────────────────────────────────────────────────
  {
    type: 'shape', label: 'Shape', category: 'Shapes & Layout',
    description: 'A generic filled/stroked rectangle used as a background or container.',
    formatSupport: ALL, bindable: false,
    properties: [{ name: 'style.fill', type: 'string', description: 'Fill color (via style).' }],
    example: { id: 'el1', type: 'shape', x: 40, y: 40, width: 200, height: 80, style: { backgroundColor: '#eef2ff', borderColor: '#6366f1', borderWidth: 1 } },
    csharpExample: 'page.DrawRectangle(x: 40, y: 720, width: 200, height: 80, fill: true, fillColor: new PdfColor(0.93, 0.95, 1));',
  },
  {
    type: 'rect', label: 'Rectangle', category: 'Shapes & Layout',
    description: 'A filled or stroked rectangle.',
    formatSupport: ALL, bindable: false,
    properties: [],
    example: { id: 'el1', type: 'rect', x: 40, y: 40, width: 160, height: 60, style: { borderColor: '#111827', borderWidth: 1 } },
    csharpExample: 'page.DrawRectangle(x: 40, y: 740, width: 160, height: 60, lineWidth: 1);',
  },
  {
    type: 'circle', label: 'Circle', category: 'Shapes & Layout',
    description: 'A filled or stroked ellipse.',
    formatSupport: ALL, bindable: false,
    properties: [],
    example: { id: 'el1', type: 'circle', x: 40, y: 40, width: 80, height: 80, style: { backgroundColor: '#fde68a' } },
    csharpExample: 'page.DrawCircle(centerX: 80, centerY: 760, radius: 40, fill: true, fillColor: new PdfColor(0.99, 0.9, 0.52));',
  },
  {
    type: 'line', label: 'Line', category: 'Shapes & Layout',
    description: 'A horizontal or rotated divider line.',
    formatSupport: ALL, bindable: false,
    properties: [{ name: 'style.strokeWidth', type: 'number', description: 'Line thickness (via style).' }],
    example: { id: 'el1', type: 'line', x: 40, y: 40, width: 300, height: 1, style: { borderColor: '#9ca3af', borderWidth: 1 } },
    csharpExample: 'page.DrawLine(x1: 40, y1: 800, x2: 340, y2: 800, lineWidth: 1);',
  },
  {
    type: 'arrow', label: 'Arrow', category: 'Shapes & Layout',
    description: 'A directional arrow with customizable head markers.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'arrowMode', type: 'enum', allowedValues: ['straight', 'elbow', 'curved'], description: 'Path style.' },
      { name: 'startMarker', type: 'enum', allowedValues: ['none', 'filled', 'open', 'dot', 'diamond', 'square', 'circle', 'arrow'], description: 'Start marker.' },
      { name: 'endMarker', type: 'enum', allowedValues: ['none', 'filled', 'open', 'dot', 'diamond', 'square', 'circle', 'arrow'], default: 'filled', description: 'End marker.' },
    ],
    example: { id: 'el1', type: 'arrow', x: 40, y: 40, width: 160, height: 2, arrowMode: 'straight', endMarker: 'filled' },
  },
  {
    type: 'draw', label: 'Drawing', category: 'Shapes & Layout',
    description: 'A freehand SVG stroke drawn with the mouse.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'drawTool', type: 'enum', allowedValues: ['pen', 'highlighter', 'eraser'], description: 'Brush.' },
      { name: 'pathData', type: 'string', description: 'SVG path "d" string.' },
    ],
    example: { id: 'el1', type: 'draw', x: 40, y: 40, width: 120, height: 60, drawTool: 'pen', pathData: 'M0,60 C40,0 80,120 120,40' },
  },
  {
    type: 'table', label: 'Table', category: 'Shapes & Layout',
    description: 'A fixed-column table with header/footer rows, per-cell styling, and zebra striping.',
    formatSupport: ALL, bindable: true,
    properties: [
      { name: 'cellData', type: 'string[][]', description: 'Rows of cell text; cells may contain {{tokens}}.' },
      { name: 'columnWidths', type: 'number[]', description: 'Per-column widths.' },
      { name: 'columnAlignments', type: 'string[]', allowedValues: ['left', 'center', 'right'], description: 'Per-column alignment.' },
      { name: 'headerRow', type: 'boolean', description: 'Treat the first row as a header.' },
      { name: 'footerRow', type: 'boolean', description: 'Treat the last row as a footer.' },
      { name: 'zebraEnabled', type: 'boolean', description: 'Alternate row backgrounds.' },
      { name: 'cellStyles', type: 'object[]', description: 'Sparse per-cell styling (row/col addressed).' },
      { name: 'repeat', type: 'object', description: 'Bind to a dataset to repeat the row template per data row.' },
    ],
    example: { id: 'el1', type: 'table', x: 40, y: 40, width: 320, height: 80, headerRow: true, cellData: [['Item', 'Qty'], ['Coffee', '2'], ['Tea', '5']] },
    csharpExample: 'page.DrawSimpleTable(x: 40, y: 760, width: 320, rows: new[] { new[] { "Item", "Qty" }, new[] { "Coffee", "2" } });',
  },
  {
    type: 'subsection', label: 'Subsection', category: 'Shapes & Layout',
    description: 'A visual grouping guide (dotted box) for organizing the canvas; not rendered to output.',
    formatSupport: { pdf: false, word: false, html: false, excel: false }, bindable: false,
    properties: [{ name: 'content', type: 'string', description: 'Optional label.' }],
    example: { id: 'el1', type: 'subsection', x: 40, y: 40, width: 300, height: 160, content: 'Address block' },
  },
  {
    type: 'area', label: 'Area', category: 'Shapes & Layout',
    description: 'A layout region guide (dotted box) used to reserve space; not rendered to output.',
    formatSupport: { pdf: false, word: false, html: false, excel: false }, bindable: false,
    properties: [{ name: 'content', type: 'string', description: 'Optional label.' }],
    example: { id: 'el1', type: 'area', x: 40, y: 40, width: 300, height: 120, content: 'Reserved' },
  },
  {
    type: 'highlight', label: 'Highlight', category: 'Shapes & Layout',
    description: 'A translucent highlight rectangle drawn behind or over content.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [{ name: 'markMode', type: 'enum', allowedValues: ['rectangle', 'text'], description: 'Highlight a region or a text run.' }],
    example: { id: 'el1', type: 'highlight', x: 40, y: 40, width: 160, height: 18, style: { backgroundColor: 'rgba(255,235,59,0.5)' } },
  },
  {
    type: 'checkmark', label: 'Check Mark', category: 'Shapes & Layout',
    description: 'A static check/cross/dot mark (decorative, not an interactive field).',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [{ name: 'checkState', type: 'enum', allowedValues: ['checked', 'cross', 'dot', 'empty'], default: 'checked', description: 'Glyph.' }],
    example: { id: 'el1', type: 'checkmark', x: 40, y: 40, width: 18, height: 18, checkState: 'checked' },
  },

  // ── Document ────────────────────────────────────────────────────────────────────────────────────
  {
    type: 'watermark', label: 'Watermark', category: 'Document',
    description: 'A text or image watermark with configurable opacity, rotation, and page scope.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'content', type: 'string', description: 'Watermark text (text mode).' },
      { name: 'watermarkMode', type: 'enum', allowedValues: ['text', 'image'], default: 'text', description: 'Text or image watermark.' },
      { name: 'pageScope', type: 'enum', allowedValues: ['current', 'all', 'first', 'last', 'range', 'odd', 'even'], default: 'all', description: 'Which pages it appears on.' },
      { name: 'pageRange', type: 'string', description: 'Page range when pageScope = "range" (e.g. "1-3,5").' },
    ],
    example: { id: 'el1', type: 'watermark', x: 100, y: 350, width: 400, height: 120, content: 'DRAFT', watermarkMode: 'text', pageScope: 'all', style: { opacity: 0.15, rotation: -45 } },
    csharpExample: 'document.AddTextWatermark("DRAFT", new PdfWatermarkOptions { RotationDegrees = -45, Opacity = 0.15 });',
  },
  {
    type: 'note', label: 'Sticky Note', category: 'Document',
    description: 'A sticky-note / callout block with title, body, and author.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'noteTitle', type: 'string', description: 'Note heading.' },
      { name: 'noteBody', type: 'string', description: 'Note text.' },
      { name: 'noteAuthor', type: 'string', description: 'Author label.' },
    ],
    example: { id: 'el1', type: 'note', x: 40, y: 40, width: 200, height: 90, noteTitle: 'Reminder', noteBody: 'Verify totals.', noteAuthor: 'QA' },
  },
  {
    type: 'link', label: 'Link', category: 'Document',
    description: 'A hyperlink region pointing to a URL or internal target.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'content', type: 'string', description: 'Link text.' },
      { name: 'href', type: 'string', description: 'Destination URL.' },
      { name: 'linkTarget', type: 'enum', allowedValues: ['_blank', '_self'], description: 'Open target (HTML).' },
    ],
    example: { id: 'el1', type: 'link', x: 40, y: 40, width: 160, height: 18, content: 'Visit site', href: 'https://example.com' },
    csharpExample: 'page.AddWebLink(x: 40, y: 782, width: 160, height: 18, url: "https://example.com");',
  },
  {
    type: 'bookmark', label: 'Bookmark', category: 'Document',
    description: 'A named anchor for cross-references and PDF/Word navigation outlines.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'bookmarkName', type: 'string', description: 'Outline/anchor name.' },
      { name: 'bookmarkTarget', type: 'string', description: 'Target reference.' },
    ],
    example: { id: 'el1', type: 'bookmark', x: 40, y: 40, width: 10, height: 10, bookmarkName: 'chapter-1' },
    csharpExample: 'document.AddBookmark("Chapter 1", pageNumber: 1, level: 1);',
  },
  {
    type: 'comment', label: 'Comment', category: 'Document',
    description: 'A review comment; native in DOCX, fallback-rendered in PDF.',
    formatSupport: PDF_WORD_HTML, bindable: false,
    properties: [
      { name: 'commentAuthor', type: 'string', description: 'Comment author.' },
      { name: 'commentText', type: 'string', description: 'Comment body.' },
      { name: 'commentDate', type: 'string', description: 'ISO timestamp.' },
    ],
    example: { id: 'el1', type: 'comment', x: 40, y: 40, width: 180, height: 60, commentAuthor: 'Reviewer', commentText: 'Please confirm.' },
  },
  {
    type: 'contentcontrol', label: 'Content Control', category: 'Document',
    description: 'A structured Word content control (SDT) with a PDF fallback box.',
    formatSupport: PDF_WORD_HTML, bindable: true,
    properties: [
      { name: 'contentControlType', type: 'enum', allowedValues: ['richText', 'plainText', 'datePicker', 'comboBox', 'picture'], description: 'Control kind.' },
      { name: 'contentControlTag', type: 'string', description: 'Programmatic tag.' },
      { name: 'contentControlTitle', type: 'string', description: 'Title shown in Word.' },
    ],
    example: { id: 'el1', type: 'contentcontrol', x: 40, y: 40, width: 240, height: 24, contentControlType: 'plainText', contentControlTag: 'customer', contentControlTitle: 'Customer' },
  },
  {
    type: 'pageboundary', label: 'Page Boundary', category: 'Document',
    description: 'A designer guide marking page edges/margins; not rendered to output.',
    formatSupport: { pdf: false, word: false, html: false, excel: false }, bindable: false,
    properties: [],
    example: { id: 'el1', type: 'pageboundary', x: 0, y: 0, width: 595, height: 842 },
  },
];

/** Lookup a catalog entry by element type. */
export function getElementDoc(type: ElementType): ElementDoc | undefined {
  return ELEMENT_CATALOG.find((e) => e.type === type);
}

/** Catalog entries grouped by category, in CATEGORY_ORDER. */
export function elementsByCategory(): { category: ElementCategory; elements: ElementDoc[] }[] {
  return CATEGORY_ORDER.map((category) => ({
    category,
    elements: ELEMENT_CATALOG.filter((e) => e.category === category),
  }));
}
