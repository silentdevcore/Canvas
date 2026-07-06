const id = () => `el-${Math.random().toString(36).slice(2, 9)}`;

export const STARTER_TEMPLATES: Record<string, object> = {
  blank: {
    id: 'code-blank',
    name: 'Untitled',
    pages: [{ id: 'page-1', elements: [] }],
    sharedElements: [],
    pageSettings: { width: 595, height: 842, orientation: 'portrait' },
  },

  hello: {
    id: 'code-hello',
    name: 'Hello World',
    pages: [
      {
        id: 'page-1',
        elements: [
          {
            id: id(),
            type: 'rect',
            x: 48,
            y: 48,
            width: 499,
            height: 120,
            style: { backgroundColor: '#eff6ff', borderColor: '#3b82f6', borderWidth: 1.5, borderRadius: 8 },
          },
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 72,
            width: 451,
            height: 48,
            content: 'Hello, Canvas PDF!',
            style: { fontSize: 28, fontWeight: 'bold', color: '#1e3a8a', textAlign: 'center' },
          },
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 130,
            width: 451,
            height: 24,
            content: 'Edit this JSON on the left to see live changes here →',
            style: { fontSize: 13, color: '#64748b', textAlign: 'center' },
          },
          {
            id: id(),
            type: 'line',
            x: 48,
            y: 200,
            width: 499,
            height: 2,
            style: { color: '#e2e8f0', strokeWidth: 1.5 },
          },
          {
            id: id(),
            type: 'richtext',
            x: 72,
            y: 220,
            width: 451,
            height: 120,
            htmlContent:
              '<p>This is a <strong>rich text</strong> element. It supports <em>italic</em>, <u>underline</u>, and <strong><em>combined</em></strong> styles.</p><p>Add more elements below by editing the JSON.</p>',
            style: { fontSize: 13, color: '#374151', lineHeight: 1.6 },
          },
          {
            id: id(),
            type: 'rect',
            x: 72,
            y: 370,
            width: 130,
            height: 44,
            style: { backgroundColor: '#3b82f6', borderRadius: 6 },
          },
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 378,
            width: 130,
            height: 28,
            content: 'Export PDF →',
            style: { fontSize: 13, fontWeight: 'bold', color: '#ffffff', textAlign: 'center' },
          },
        ],
      },
    ],
    sharedElements: [],
    pageSettings: { width: 595, height: 842, orientation: 'portrait' },
  },

  invoice: {
    id: 'code-invoice',
    name: 'Invoice',
    pages: [
      {
        id: 'page-1',
        elements: [
          {
            id: id(),
            type: 'text',
            x: 48,
            y: 48,
            width: 280,
            height: 36,
            content: 'INVOICE',
            style: { fontSize: 26, fontWeight: 'bold', color: '#0f172a' },
          },
          {
            id: id(),
            type: 'text',
            x: 48,
            y: 90,
            width: 280,
            height: 20,
            content: '#INV-2024-001',
            style: { fontSize: 13, color: '#64748b' },
          },
          {
            id: id(),
            type: 'text',
            x: 380,
            y: 48,
            width: 167,
            height: 20,
            content: 'Acme Corp',
            style: { fontSize: 13, fontWeight: 'bold', color: '#0f172a', textAlign: 'right' },
          },
          {
            id: id(),
            type: 'text',
            x: 380,
            y: 68,
            width: 167,
            height: 40,
            content: '123 Business St\nCity, 12345',
            style: { fontSize: 11, color: '#64748b', textAlign: 'right' },
          },
          {
            id: id(),
            type: 'line',
            x: 48,
            y: 130,
            width: 499,
            height: 1,
            style: { color: '#e2e8f0', strokeWidth: 1 },
          },
          {
            id: id(),
            type: 'field',
            x: 48,
            y: 148,
            width: 220,
            height: 52,
            fieldLabel: 'Bill To',
            fieldName: 'Client Name',
            style: { fontSize: 12, color: '#374151', borderColor: '#d1d5db' },
          },
          {
            id: id(),
            type: 'field',
            x: 380,
            y: 148,
            width: 167,
            height: 52,
            fieldLabel: 'Due Date',
            fieldName: '30 days from issue',
            style: { fontSize: 12, color: '#374151', borderColor: '#d1d5db' },
          },
          {
            id: id(),
            type: 'table',
            x: 48,
            y: 230,
            width: 499,
            height: 180,
            headerRow: true,
            headerBgColor: '#f1f5f9',
            zebraEnabled: true,
            zebraColor: '#f8fafc',
            cellData: [
              ['Description', 'Qty', 'Unit Price', 'Total'],
              ['Website Design', '1', '$1,200.00', '$1,200.00'],
              ['SEO Setup', '1', '$400.00', '$400.00'],
              ['Hosting (annual)', '1', '$120.00', '$120.00'],
            ],
            columnWidths: [220, 60, 110, 109],
            columnAlignments: ['left', 'center', 'right', 'right'],
            style: { fontSize: 11, borderColor: '#e2e8f0', borderWidth: 0.75 },
          },
          {
            id: id(),
            type: 'text',
            x: 380,
            y: 430,
            width: 167,
            height: 20,
            content: 'Total: $1,720.00',
            style: { fontSize: 14, fontWeight: 'bold', color: '#0f172a', textAlign: 'right' },
          },
          {
            id: id(),
            type: 'signature',
            x: 48,
            y: 700,
            width: 200,
            height: 80,
            signatureLabel: 'Authorized Signature',
            style: { borderColor: '#94a3b8', color: '#6b7280' },
          },
          {
            id: id(),
            type: 'pagenumber',
            x: 48,
            y: 808,
            width: 499,
            height: 20,
            numberingFormat: 'pageOfTotal',
            style: { fontSize: 10, color: '#94a3b8', textAlign: 'center' },
          },
        ],
      },
    ],
    sharedElements: [],
    pageSettings: { width: 595, height: 842, orientation: 'portrait' },
  },

  multipage: {
    id: 'code-multipage',
    name: 'Multi-Page Document',
    pages: [
      {
        id: 'page-1',
        elements: [
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 72,
            width: 451,
            height: 48,
            content: 'Document Title',
            style: { fontSize: 28, fontWeight: 'bold', color: '#0f172a', textAlign: 'center' },
          },
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 128,
            width: 451,
            height: 24,
            content: 'Page 1 — Introduction',
            style: { fontSize: 15, color: '#64748b', textAlign: 'center' },
          },
          {
            id: id(),
            type: 'richtext',
            x: 72,
            y: 180,
            width: 451,
            height: 200,
            htmlContent:
              '<p>This is the first page of a multi-page document. Scroll the preview to see all pages.</p><p>The page number element below is scoped to <strong>all</strong> pages, so it appears on every page automatically.</p>',
            style: { fontSize: 13, color: '#374151', lineHeight: 1.6 },
          },
        ],
      },
      {
        id: 'page-2',
        elements: [
          {
            id: id(),
            type: 'text',
            x: 72,
            y: 72,
            width: 451,
            height: 36,
            content: 'Page 2 — Content',
            style: { fontSize: 22, fontWeight: 'bold', color: '#0f172a' },
          },
          {
            id: id(),
            type: 'note',
            x: 72,
            y: 130,
            width: 451,
            height: 100,
            noteTitle: 'Developer Note',
            noteBody: 'Add more pages by duplicating the page objects in the JSON array above.',
            noteAuthor: 'Canvas',
            style: { backgroundColor: '#fef9c3', borderColor: '#fbbf24' },
          },
        ],
      },
    ],
    sharedElements: [
      {
        id: 'shared-pagenumber',
        type: 'pagenumber',
        x: 48,
        y: 808,
        width: 499,
        height: 20,
        numberingFormat: 'pageOfTotal',
        pageScope: 'all',
        style: { fontSize: 10, color: '#94a3b8', textAlign: 'center' },
      },
      {
        id: 'shared-watermark',
        type: 'watermark',
        x: 100,
        y: 300,
        width: 395,
        height: 200,
        content: 'DRAFT',
        pageScope: 'all',
        style: { fontSize: 72, color: '#e2e8f0', rotation: 45, opacity: 0.3 },
      },
    ],
    pageSettings: { width: 595, height: 842, orientation: 'portrait' },
  },
};

export type StarterKey = keyof typeof STARTER_TEMPLATES;

export const STARTER_LABELS: Record<StarterKey, string> = {
  blank:     'Blank',
  hello:     'Hello World',
  invoice:   'Invoice',
  multipage: 'Multi-Page',
};

// PXA-compatible PDF API starter — script must return a PdfDocument as last expression
export const CSHARP_CODE_STARTER = `// PXA PDF Code Editor
// Write PXA-compatible PDF API code here.
// The last expression must be the PdfDocument instance.

var document = new PdfDocument();
document.Info.Title = "My Document";

var page = document.AddPage();

// Header banner
page.DrawRectangle(
    x: 48, y: 742, width: 499, height: 72,
    lineWidth: 0.5,
    fill: true,
    fillColor: new PdfColor(0.09, 0.23, 0.55),
    strokeColor: new PdfColor(0.09, 0.23, 0.55));

page.DrawText("Hello from Power Dox Automation!", x: 72, y: 768,
    new PdfDrawTextOptions
    {
        FontSize = 22,
        FontFamily = PdfFontFamily.Helvetica,
        Bold = true,
        FillColor = new PdfColor(1, 1, 1)
    });

page.DrawText("Edit this code and click ▶ Run to see the result.", x: 72, y: 748,
    new PdfDrawTextOptions
    {
        FontSize = 11,
        FontFamily = PdfFontFamily.Helvetica,
        FillColor = new PdfColor(0.8, 0.88, 1)
    });

// Body paragraph
page.DrawParagraph(
    "This is the PXA-compatible PDF API — the same engine used by the visual editor under the hood. " +
    "You have access to rectangles, text, paragraphs, tables, images, and more.",
    x: 72, y: 700, maxWidth: 451,
    new PdfParagraphOptions
    {
        FontSize = 12,
        FontFamily = PdfFontFamily.Helvetica,
        Alignment = PdfTextAlignment.Left,
        LineHeight = 17
    });

// Simple table
page.DrawSimpleTable(
    x: 72, y: 610, width: 451,
    rows: new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<string>>
    {
        new[] { "Feature",    "Supported" },
        new[] { "DrawText",   "✅" },
        new[] { "DrawRect",   "✅" },
        new[] { "Table",      "✅" },
        new[] { "Image",      "✅" },
        new[] { "Paragraph",  "✅" },
    },
    options: new PdfTableOptions
    {
        FontFamily = PdfFontFamily.Helvetica,
        FontSize = 11,
        ColumnWidths = new[] { 3.0, 1.0 },
        ColumnAlignments = new[] { PdfTextAlignment.Left, PdfTextAlignment.Center },
        HeaderFillColor = new PdfGrayColor(0.92),
        AlternateRowFillColor = new PdfGrayColor(0.97),
        DrawOuterBorder = true,
        DrawInnerHorizontalBorders = true,
        BorderColor = PdfColor.Gray,
        CellLineHeight = 14,
        CellPaddingLeft = 8,
        CellPaddingRight = 8,
        CellPaddingTop = 4,
        CellPaddingBottom = 4,
    });

// Footer line
page.DrawLine(x1: 48, y1: 32, x2: 547, y2: 32, lineWidth: 0.5, strokeColor: new PdfGrayColor(0.8));
page.DrawText("Generated with PXA PDF · PXA Code Editor", x: 48, y: 20,
    new PdfDrawTextOptions { FontSize = 9, FontFamily = PdfFontFamily.Helvetica, FillColor = new PdfGrayColor(0.55) });

document`;

export const CSHARP_DTO_STARTER = '';
