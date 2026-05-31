import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  FiCheck,
  FiChevronRight,
  FiCopy,
} from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';

// ─── Copy Button ──────────────────────────────────────────────────────────────

const CopyButton: React.FC<{ code: string }> = ({ code }) => {
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };
  return (
    <button className="docs-copy-btn" onClick={copy} aria-label="Copy code">
      {copied ? <FiCheck size={14} /> : <FiCopy size={14} />}
      {copied ? 'Copied' : 'Copy'}
    </button>
  );
};

// ─── Code Block ───────────────────────────────────────────────────────────────

const Code: React.FC<{ lang?: string; children: string }> = ({ lang, children }) => (
  <div className="docs-code-wrap">
    {lang && <span className="docs-code-lang">{lang}</span>}
    <CopyButton code={children.trim()} />
    <pre className="docs-code-block"><code>{children.trim()}</code></pre>
  </div>
);

// ─── Section heading ──────────────────────────────────────────────────────────

const H2: React.FC<{ id: string; children: React.ReactNode }> = ({ id, children }) => (
  <h2 id={id} className="docs-h2">{children}</h2>
);

const H3: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <h3 className="docs-h3">{children}</h3>
);

// ─── Element reference card ───────────────────────────────────────────────────

interface ElemRow {
  type: string;
  name: string;
  desc: string;
  props: string;
  pdf: '✅' | '⚠️' | '❌';
  word: '✅' | '⚠️' | '❌';
}

const ELEMENTS: ElemRow[] = [
  { type: 'text',          name: 'Text',             desc: 'Static single-style text block.',                                                                           props: 'content, style.fontSize, style.color, style.fontWeight, style.textAlign, style.fontFamily',  pdf: '✅', word: '✅' },
  { type: 'richtext',      name: 'Rich Text',         desc: 'HTML-formatted content — bold, italic, lists, inline colours.',                                             props: 'htmlContent',                                                                               pdf: '✅', word: '✅' },
  { type: 'link',          name: 'Hyperlink',         desc: 'Clickable text link with optional href URL.',                                                               props: 'content, href, style.color',                                                                pdf: '✅', word: '✅' },
  { type: 'table',         name: 'Table',             desc: 'Data grid with optional header/footer rows and zebra striping.',                                            props: 'style.rows, style.columns, headerRow, footerRow, headerBgColor, zebraEnabled, cellData[][]',  pdf: '✅', word: '✅' },
  { type: 'image',         name: 'Image',             desc: 'Embedded image from URL or data URI. PDF supports data: URIs only; http/https images require Word export.', props: 'content (URL), fitMode (contain|cover|fill), focalX, focalY',                               pdf: '⚠️', word: '✅' },
  { type: 'qrcode',        name: 'QR Code',           desc: 'Scannable QR code generated server-side.',                                                                  props: 'qrValue, qrSize, style.color, style.backgroundColor',                                        pdf: '✅', word: '✅' },
  { type: 'barcode',       name: 'Barcode',           desc: '1-D barcode in CODE128, EAN-13, UPC-A and other formats.',                                                  props: 'barcodeValue, barcodeType',                                                                 pdf: '✅', word: '✅' },
  { type: 'chart',         name: 'Chart',             desc: 'Bar, line or pie chart with custom data. Renders as a placeholder in export.',                               props: 'chartType (bar|line|pie), chartData.labels[], chartData.datasets[]',                         pdf: '⚠️', word: '❌' },
  { type: 'field',         name: 'Form Field',        desc: 'Labelled text-input placeholder for fillable PDFs.',                                                        props: 'fieldLabel, fieldName, required, placeholder',                                               pdf: '✅', word: '✅' },
  { type: 'checkbox',      name: 'Checkbox',          desc: 'Checkbox with an inline label.',                                                                            props: 'fieldLabel, fieldName, required',                                                            pdf: '✅', word: '✅' },
  { type: 'radio',         name: 'Radio / List',      desc: 'Bullet or numbered list with selectable items.',                                                            props: 'fieldLabel, options[]',                                                                     pdf: '✅', word: '✅' },
  { type: 'dropdown',      name: 'Dropdown',          desc: 'Select box with a list of options.',                                                                        props: 'fieldLabel, fieldName, options[]',                                                           pdf: '✅', word: '✅' },
  { type: 'optionlist',    name: 'Option List',       desc: 'Numbered or bulleted list of items.',                                                                       props: 'options[], style.listStyle',                                                                pdf: '✅', word: '✅' },
  { type: 'button',        name: 'Button',            desc: 'Styled action button — rendered as a rounded rectangle in PDF.',                                            props: 'content, style.backgroundColor, style.color, style.borderRadius',                           pdf: '✅', word: '✅' },
  { type: 'signature',     name: 'Signature Line',    desc: 'Printable signature block with label and underline.',                                                       props: 'signatureLabel',                                                                            pdf: '✅', word: '✅' },
  { type: 'number',        name: 'Number',            desc: 'Formatted numeric value — decimal, currency, percent, ordinal, or scientific.',                             props: 'numberValue, numberStyle, numberDecimals, numberLocale',                                    pdf: '✅', word: '✅' },
  { type: 'shape',         name: 'Shape / Rect',      desc: 'Filled or stroked rectangle, optionally rounded.',                                                          props: 'style.backgroundColor, style.borderColor, style.borderWidth, style.borderRadius',            pdf: '✅', word: '⚠️' },
  { type: 'circle',        name: 'Circle / Ellipse',  desc: 'Circle or ellipse shape.',                                                                                  props: 'style.backgroundColor, style.borderColor, style.borderWidth',                               pdf: '✅', word: '⚠️' },
  { type: 'line',          name: 'Line',              desc: 'Horizontal, vertical, or diagonal rule with optional dash styles.',                                          props: 'style.backgroundColor, style.strokeDashArray, height',                                     pdf: '✅', word: '⚠️' },
  { type: 'arrow',         name: 'Arrow',             desc: 'Line with configurable start/end arrowhead markers.',                                                       props: 'arrowDirection, style.color, style.strokeWidth',                                            pdf: '✅', word: '⚠️' },
  { type: 'draw',          name: 'Freehand Draw',     desc: 'SVG path drawn with the mouse. Rendered via Bézier curves in PDF.',                                         props: 'pathData, style.color, style.strokeWidth',                                                  pdf: '✅', word: '⚠️' },
  { type: 'watermark',     name: 'Watermark',         desc: 'Diagonal text overlay — e.g. DRAFT, CONFIDENTIAL. Skipped in Word.',                                        props: 'content, style.color, style.fontSize, style.rotation',                                      pdf: '✅', word: '⚠️' },
  { type: 'highlight',     name: 'Highlight',         desc: 'Translucent colour overlay. Skipped in Word.',                                                              props: 'style.backgroundColor, style.opacity',                                                      pdf: '✅', word: '⚠️' },
  { type: 'checkmark',     name: 'Checkmark',         desc: 'Stand-alone check/cross/tick icon. Skipped in Word.',                                                       props: 'style.color, style.fontSize',                                                               pdf: '✅', word: '⚠️' },
  { type: 'note',          name: 'Callout Note',      desc: 'Highlighted info/warning box with title and body text.',                                                    props: 'content, noteTitle, noteType (info|warning|error|success)',                                  pdf: '✅', word: '✅' },
  { type: 'date',          name: 'Auto Date',         desc: 'Inserts today\'s date at render time with timezone and locale support.',                                    props: 'dateMode (auto|static), timezone, locale, dateFormat',                                      pdf: '✅', word: '✅' },
  { type: 'pagenumber',    name: 'Page Number',       desc: 'Inserts the current page number, total, or a custom format.',                                               props: 'numberingFormat, prefix, suffix, startNumber',                                              pdf: '✅', word: '✅' },
  { type: 'toc',           name: 'Table of Contents', desc: 'Auto-generated TOC with clickable page links. Scans heading-level text elements across all pages.',         props: 'tocTitle, tocMinLevel, tocMaxLevel, tocEntries[]',                                          pdf: '✅', word: '✅' },
  { type: 'pageboundary',  name: 'Page Boundary',     desc: 'Explicit page-break marker. Acts as a layout hint — no visible output.',                                   props: '(none)',                                                                                    pdf: '⚠️', word: '⚠️' },
  { type: 'subsection',    name: 'Subsection',        desc: 'Layout container with a dashed outline in PDF. Acts as a visual grouping aid.',                             props: 'style.borderColor',                                                                         pdf: '⚠️', word: '⚠️' },
  { type: 'area',          name: 'Area',              desc: 'Non-printing layout area. Renders a dashed outline in PDF for design guidance only.',                       props: 'style.borderColor',                                                                         pdf: '⚠️', word: '⚠️' },
  { type: 'footnote',      name: 'Footnote',          desc: 'DOCX footnote reference. PDF: superscript marker + text at page bottom. Word: native footnotes.xml.',      props: 'footnoteText',                                                                              pdf: '⚠️', word: '✅' },
  { type: 'endnote',       name: 'Endnote',           desc: 'DOCX endnote reference. PDF: superscript marker. Word: native endnotes.xml.',                              props: 'footnoteText',                                                                              pdf: '⚠️', word: '✅' },
  { type: 'bookmark',      name: 'Bookmark',          desc: 'Named anchor. PDF: named destination. Word: native bookmark.',                                              props: 'bookmarkName, bookmarkTarget',                                                              pdf: '✅', word: '✅' },
  { type: 'comment',       name: 'Word Comment',      desc: 'Margin annotation. PDF: yellow box with author. Word: native comments.xml.',                               props: 'commentText, commentAuthor, commentDate, commentId',                                        pdf: '✅', word: '✅' },
  { type: 'contentcontrol',name: 'Content Control',   desc: 'OOXML structured content control. PDF: bordered box with label. Word: native SDT.',                        props: 'contentControlType, contentControlTitle, contentControlTag, contentControlPlaceholder',     pdf: '⚠️', word: '✅' },
];

// ─── Nav sections ─────────────────────────────────────────────────────────────

const SECTIONS = [
  { id: 'quick-start',     label: 'Quick Start' },
  { id: 'editor-overview', label: 'Editor Overview' },
  { id: 'elements',        label: 'Elements Reference' },
  { id: 'import-export',   label: 'Import & Export' },
  { id: 'document-ops',    label: 'Document Operations' },
  { id: 'word-features',   label: 'Word / DOCX Features' },
  { id: 'json-schema',     label: 'JSON Schema' },
  { id: 'csharp-models',   label: 'C# Models' },
  { id: 'csharp-examples', label: 'C# Code Examples' },
  { id: 'json-to-csharp',  label: 'JSON → C# Mapping' },
  { id: 'rest-api',        label: 'REST API' },
];

// ─── DocsPage ─────────────────────────────────────────────────────────────────

const DocsPage: React.FC = () => {
  const navigate = useNavigate();
  const [activeId, setActiveId] = useState('quick-start');
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const observerRef = useRef<IntersectionObserver | null>(null);

  useEffect(() => {
    observerRef.current?.disconnect();
    observerRef.current = new IntersectionObserver(
      entries => {
        for (const entry of entries) {
          if (entry.isIntersecting) setActiveId(entry.target.id);
        }
      },
      { rootMargin: '-20% 0px -60% 0px' }
    );
    SECTIONS.forEach(s => {
      const el = document.getElementById(s.id);
      if (el) observerRef.current!.observe(el);
    });
    return () => observerRef.current?.disconnect();
  }, []);

  const scrollTo = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
    setMobileNavOpen(false);
  };

  const sidebar = (
    <nav className="docs-sidebar">
      <div className="docs-sidebar-header">
        <span className="docs-sidebar-title">Documentation</span>
      </div>
      <ul className="docs-sidebar-list">
        {SECTIONS.map(s => (
          <li key={s.id}>
            <button
              className={`docs-sidebar-link${activeId === s.id ? ' is-active' : ''}`}
              onClick={() => scrollTo(s.id)}
            >
              {s.label}
            </button>
          </li>
        ))}
      </ul>
      <div className="docs-sidebar-footer">
        <button className="docs-sidebar-back" onClick={() => navigate('/')}>
          ← Back to home
        </button>
      </div>
    </nav>
  );

  return (
    <div className="docs-root">
      <AppHeader activePage="docs" />

      <div className="docs-layout">
        {/* Desktop sidebar */}
        <div className="docs-sidebar-wrap">
          {sidebar}
        </div>

        {/* Mobile nav drawer */}
        {mobileNavOpen && (
          <div className="docs-mobile-nav">
            {sidebar}
          </div>
        )}

        {/* Main content */}
        <main className="docs-main">

          {/* ── Quick Start ─────────────────────────────────────────────── */}
          <section id="quick-start" className="docs-section">
            <H2 id="quick-start">Quick Start</H2>
            <p className="docs-lead">Create a professional PDF in under two minutes — no design experience required.</p>

            <H3>Option A — Start from scratch</H3>
            <ol className="docs-steps">
              <li><strong>Open the home page</strong> and click <em>Blank canvas</em>. The editor opens with an empty A4 page.</li>
              <li><strong>Add elements</strong> using the toolbar at the top of the editor: text blocks, tables, images, QR codes, signatures, and more.</li>
              <li><strong>Resize and position</strong> each element by dragging its handles. The inspector panel on the right lets you set exact values.</li>
              <li><strong>Style each element</strong> — font, colour, border, background — in the inspector.</li>
              <li><strong>Add more pages</strong> via the page panel at the bottom. Drag pages to reorder them.</li>
              <li><strong>Preview</strong> the finished document with the Preview button in the toolbar.</li>
              <li><strong>Export</strong> as PDF (via the backend), JSON, or PNG image.</li>
            </ol>

            <H3>Option B — Start from a template</H3>
            <ol className="docs-steps">
              <li>Go to <strong>Templates</strong> and click any template card to see a live preview.</li>
              <li>Click <strong>Use this template</strong> to open it in the editor with all elements pre-loaded.</li>
              <li>Edit, remove, or add elements as needed, then export.</li>
            </ol>

            <div className="docs-callout docs-callout--tip">
              <strong>Tip:</strong> Export as JSON first to save your work. Re-import by pasting the JSON into the Code Editor (<em>{'{ }'} Code Editor</em> on the home page) and editing it live.
            </div>
          </section>

          {/* ── Editor Overview ─────────────────────────────────────────── */}
          <section id="editor-overview" className="docs-section">
            <H2 id="editor-overview">Editor Overview</H2>

            <div className="docs-overview-grid">
              {[
                { label: 'Canvas', desc: 'The central page area. Click any element to select it. Drag to move; use the blue handles at corners and edges to resize.' },
                { label: 'Toolbar', desc: 'Collapsible tool groups on the left: Basic, Word/DOCX Elements, and more. Add elements, toggle shared elements, open Find & Replace, and access Export.' },
                { label: 'Inspector Panel', desc: 'Appears on the right when an element is selected. Every property (position, size, content, style, revision, DOCX metadata) is editable here without touching JSON.' },
                { label: 'Pages Panel', desc: 'At the bottom of the editor. Add, delete, duplicate, and drag-to-reorder pages. Click a page thumbnail to navigate to it.' },
                { label: 'Shared Elements', desc: 'Elements marked as "shared" appear on every page — ideal for headers and footers. Managed from the toolbar.' },
                { label: 'Find & Replace', desc: 'Click the search icon in the toolbar to open the Find & Replace modal. Supports plain text, case-sensitive, whole-word, and regular expression modes.' },
                { label: 'Import File', desc: 'On the Templates page, click "Import file" to open an existing PDF, DOCX, DOC, or ODT as a Canvas design. The document is converted to editable elements.' },
                { label: 'Page Settings', desc: 'Gear icon in the toolbar. Controls page size, margins, background, header/footer, bleed, watermark, page numbering, metadata, Track Changes, Document Protection, Named Styles, and Custom Properties.' },
                { label: 'Keyboard Shortcuts', desc: 'Undo/Redo: ⌘Z / ⌘⇧Z  ·  Copy/Paste/Duplicate: ⌘C / ⌘V / ⌘D  ·  Select all: ⌘A  ·  Delete: Del or ⌫  ·  Deselect: Esc  ·  Move: Arrow keys (1 pt), Shift+Arrows (10 pt)  ·  Zoom: ⌘+ / ⌘– / ⌘0' },
              ].map(item => (
                <div className="docs-overview-card" key={item.label}>
                  <span className="docs-overview-label">{item.label}</span>
                  <p>{item.desc}</p>
                </div>
              ))}
            </div>
          </section>

          {/* ── Elements Reference ───────────────────────────────────────── */}
          <section id="elements" className="docs-section">
            <H2 id="elements">Elements Reference</H2>
            <p>Every element is absolutely positioned on the canvas using <code className="docs-inline-code">x</code>, <code className="docs-inline-code">y</code>, <code className="docs-inline-code">width</code>, and <code className="docs-inline-code">height</code> in points (pt). All element types are listed below.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead>
                  <tr>
                    <th>Type ID</th>
                    <th>Name</th>
                    <th>Description</th>
                    <th style={{ textAlign: 'center' }}>PDF</th>
                    <th style={{ textAlign: 'center' }}>Word</th>
                    <th>Key Properties</th>
                  </tr>
                </thead>
                <tbody>
                  {ELEMENTS.map(el => (
                    <tr key={el.type}>
                      <td><code className="docs-inline-code">{el.type}</code></td>
                      <td style={{ whiteSpace: 'nowrap' }}>{el.name}</td>
                      <td>{el.desc}</td>
                      <td style={{ textAlign: 'center', fontSize: 16 }}>{el.pdf}</td>
                      <td style={{ textAlign: 'center', fontSize: 16 }}>{el.word}</td>
                      <td className="docs-props-cell">{el.props}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p style={{ fontSize: 12, color: '#6b7280', marginTop: 8 }}>✅ Full support &nbsp;⚠️ Partial / placeholder &nbsp;❌ No handler</p>
            </div>

            <H3>Common element properties</H3>
            <Code lang="json">{`{
  "id":     "elem-1715000000000",   // unique string ID
  "type":   "text",                 // element type (see table above)
  "x":      72,                     // left offset in points from page left
  "y":      72,                     // top offset in points from page top
  "width":  420,                    // element width in points
  "height": 48,                     // element height in points
  "style": {
    "fontSize":       16,
    "color":          "#111827",
    "fontWeight":     "bold",       // "normal" | "bold"
    "fontStyle":      "normal",     // "normal" | "italic"
    "textAlign":      "left",       // "left" | "center" | "right"
    "fontFamily":     "Arial",
    "lineHeight":     1.4,
    "backgroundColor":"#ffffff",
    "borderColor":    "#e2e8f0",
    "borderWidth":    1,
    "borderRadius":   4,
    "rotation":       0             // degrees
  }
}`}</Code>

            <H3>Table element</H3>
            <Code lang="json">{`{
  "id":     "table-1715000000001",
  "type":   "table",
  "x": 72, "y": 200, "width": 451, "height": 180,
  "style": {
    "rows":         4,
    "columns":      3,
    "borderWidth":  1,
    "borderColor":  "#e2e8f0",
    "cellPadding":  6
  },
  "headerRow":      true,
  "footerRow":      false,
  "headerBgColor":  "#1d6fff",
  "zebraEnabled":   true,
  "zebraColor":     "#f8fafc",
  "columnWidths":   [240, 80, 131],
  "columnAlignments": ["left", "center", "right"],
  "cellData": [
    ["Description",  "Qty",  "Amount"],
    ["Web Design",   "1",    "€ 1,200"],
    ["Hosting",      "1",    "€ 150"],
    ["Total",        "",     "€ 1,350"]
  ]
}`}</Code>

            <H3>Chart element</H3>
            <Code lang="json">{`{
  "id":        "chart-1715000000002",
  "type":      "chart",
  "x": 72, "y": 400, "width": 420, "height": 200,
  "chartType": "bar",              // "bar" | "line" | "pie"
  "chartData": {
    "labels":   ["Q1", "Q2", "Q3", "Q4"],
    "datasets": [
      { "label": "Revenue", "data": [42000, 55000, 49000, 71000] },
      { "label": "Costs",   "data": [28000, 31000, 27000, 38000] }
    ]
  }
}`}</Code>
          </section>

          {/* ── Import & Export ─────────────────────────────────────────── */}
          <section id="import-export" className="docs-section">
            <H2 id="import-export">Import &amp; Export</H2>

            <H3>Export formats</H3>
            <p>Click <strong>Export</strong> in the editor toolbar to open the format picker. All formats send the current design to the backend at <code className="docs-inline-code">POST /api/export</code>.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Format key</th><th>Output</th><th>Notes</th></tr></thead>
                <tbody>
                  {([
                    ['pdf',  'PDF',              'Custom .NET renderer — no external PDF library'],
                    ['word', 'DOCX',             'Full OOXML with styles, footnotes, bookmarks, comments, content controls, track changes, and digital signature support'],
                    ['odt',  'ODT',              'ODF 1.3 ZIP with draw frames — pixel-accurate layout; opens in LibreOffice and Google Docs'],
                    ['excel','XLSX',             'Excel workbook via ClosedXML; tables map to worksheets'],
                    ['html', 'HTML',             'Inline-styled HTML page'],
                    ['csv',  'CSV',              'Flat comma-separated values; table elements become rows'],
                    ['md',   'Markdown',         'Text-based Markdown output'],
                    ['png',  'PNG',              'Page-by-page raster images'],
                    ['jpeg', 'JPEG',             'Page-by-page JPEG images'],
                    ['tiff', 'TIFF',             'Multi-page baseline RGB TIFF for print and archival workflows'],
                  ] as [string,string,string][]).map(([key, out, note]) => (
                    <tr key={key}>
                      <td><code className="docs-inline-code">{key}</code></td>
                      <td>{out}</td>
                      <td>{note}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <H3>Import formats</H3>
            <p>On the <strong>Templates</strong> page, click <em>Import file</em> and choose a supported file. The document is converted to a Canvas design and opened in the editor.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Extension</th><th>Source format</th><th>What is extracted</th></tr></thead>
                <tbody>
                  {([
                    ['.pdf',  'PDF',                    'Words grouped by baseline Y into Text elements; embedded images as base64 data URIs'],
                    ['.docx', 'Word Open XML',          'Paragraphs → Text; tables → Table; inline images → Image; typography from RunProperties; page size from SectionProperties'],
                    ['.doc',  'Word 97-2003 binary',    'Pure C# CFBF parser: reads WordDocument stream via FIB offsets; text stacked as paragraphs'],
                    ['.odt',  'OpenDocument Text',      'Paragraphs and headings with style resolution; draw:frame images extracted as base64'],
                    ['.png / .jpg / .jpeg / .gif / .webp / .bmp / .tiff', 'Raster image', 'Decoded via SkiaSharp; creates a single-page design whose dimensions match the image\'s native pixel size, with one full-page Image element'],
                  ] as [string,string,string][]).map(([ext, fmt, note]) => (
                    <tr key={ext}>
                      <td><code className="docs-inline-code">{ext}</code></td>
                      <td>{fmt}</td>
                      <td>{note}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="docs-callout docs-callout--tip">
              <strong>API import:</strong> You can also call the import endpoints directly — see <em>REST API → Document Operations</em> below. They accept multipart file uploads and return a <code className="docs-inline-code">DesignExportDto</code> JSON object.
            </div>
          </section>

          {/* ── Document Operations ──────────────────────────────────────── */}
          <section id="document-ops" className="docs-section">
            <H2 id="document-ops">Document Operations</H2>
            <p>These endpoints act on a full <code className="docs-inline-code">DesignExportDto</code> JSON payload and return a modified design or binary file. All are on the <code className="docs-inline-code">/api/document/</code> prefix.</p>

            <H3>Find &amp; Replace</H3>
            <p>Available from the editor toolbar (search icon) and via API. Searches all text-bearing elements across all pages and shared elements.</p>
            <Code lang="json">{`// POST /api/document/find-replace
// Request body:
{
  "design":        { /* DesignExportDto */ },
  "find":          "Acme Corp",
  "replace":       "ACME GmbH",
  "caseSensitive": false,
  "wholeWord":     false,
  "useRegex":      false
}

// Response (200 OK):
{
  "design":           { /* modified DesignExportDto */ },
  "replacementCount": 4
}`}</Code>

            <H3>Clone</H3>
            <p>Deep-copies a design with fresh IDs. Useful for duplicating a template before making changes.</p>
            <Code lang="json">{`// POST /api/document/clone
{
  "design":  { /* DesignExportDto */ },
  "newName": "Invoice Copy"         // optional
}

// Response: DesignExportDto with new id and name`}</Code>

            <H3>Extract Pages</H3>
            <p>Extracts a subset of pages (1-based) into a new standalone design.</p>
            <Code lang="json">{`// POST /api/document/extract-pages
{
  "design":      { /* DesignExportDto */ },
  "pageNumbers": [1, 3],             // 1-based page numbers
  "newName":     "Pages 1 & 3"      // optional
}

// Response: DesignExportDto containing only the requested pages`}</Code>

            <H3>Digital Signing (DOCX)</H3>
            <p>Applies an X.509 / RSA-SHA256 OOXML XML-DSig signature to an existing DOCX file. You need a PFX/P12 certificate file (containing both the certificate and private key).</p>
            <Code lang="http">{`POST http://localhost:5274/api/document/sign-docx
Content-Type: multipart/form-data

--boundary
Content-Disposition: form-data; name="docx"; filename="contract.docx"
[binary DOCX content]

--boundary
Content-Disposition: form-data; name="certificate"; filename="signing.pfx"
[binary PFX content]

--boundary
Content-Disposition: form-data; name="password"
MyPfxPassword123
--boundary--

──────────────────────────────────────────────────────
HTTP/1.1 200 OK
Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document
Content-Disposition: attachment; filename="contract_signed.docx"

[signed DOCX binary]`}</Code>

            <div className="docs-callout docs-callout--info">
              <strong>Note:</strong> The signature embeds the certificate's public key in <code className="docs-inline-code">_xmlsignatures/sig1.xml</code> inside the DOCX ZIP. Word and LibreOffice will show a "Signed" indicator. Signature validity requires the certificate chain to be trusted on the recipient's machine.
            </div>
          </section>

          {/* ── Word / DOCX Features ─────────────────────────────────────── */}
          <section id="word-features" className="docs-section">
            <H2 id="word-features">Word / DOCX Features</H2>
            <p>These features only affect DOCX export (format key <code className="docs-inline-code">word</code>). They are configured in <strong>Page Settings</strong> (gear icon) and the <strong>Inspector Panel</strong> per element.</p>

            <H3>Named Styles</H3>
            <p>Define reusable paragraph, character, list, and table styles in <em>Page Settings → Named Styles</em>. Styles support inheritance via <code className="docs-inline-code">basedOn</code> and <code className="docs-inline-code">nextStyle</code>.</p>
            <Code lang="json">{`// pageSettings.namedStyles
[
  {
    "id":        "heading1",
    "name":      "Heading 1",
    "type":      "paragraph",      // "paragraph" | "character" | "list" | "table"
    "basedOn":   "Normal",
    "nextStyle": "Normal",
    "style": { "fontSize": 20, "fontWeight": "bold", "color": "#1d2939" }
  },
  {
    "id":   "emphasis",
    "name": "Emphasis",
    "type": "character",
    "style": { "fontStyle": "italic", "color": "#1d6fff" }
  }
]

// Referencing a style on an element:
{
  "id": "title-1", "type": "text",
  "styleName":      "heading1",   // paragraph style
  "characterStyle": "emphasis"    // character style (inline)
}`}</Code>

            <H3>Track Changes / Revisions</H3>
            <p>Enable in <em>Page Settings → Track Changes</em>. Per-element revision metadata controls how changes are wrapped in the DOCX output.</p>
            <Code lang="json">{`// pageSettings.trackChanges: true

// Per-element revision fields:
{
  "revisionType":   "insert",          // "insert" | "delete" | "format" | null
  "revisionAuthor": "Jane Smith",
  "revisionDate":   "2026-05-22",
  "revisionId":     "rev-001"
}`}</Code>

            <H3>Document Protection</H3>
            <p>Enable in <em>Page Settings → Document Protection</em>. Writes <code className="docs-inline-code">{'<w:documentProtection>'}</code> to <code className="docs-inline-code">settings.xml</code>.</p>
            <Code lang="json">{`// pageSettings.protection
{
  "enabled":      true,
  "mode":         "readOnly",    // "readOnly" | "comments" | "trackedChanges" | "formFields"
  "passwordHash": ""             // SHA-512 hash of the password (optional)
}`}</Code>

            <H3>Custom Document Properties</H3>
            <p>Add custom metadata in <em>Page Settings → Custom Properties</em>. Properties are written to <code className="docs-inline-code">custom.xml</code> and visible in Word's Document Properties panel.</p>
            <Code lang="json">{`// pageSettings.customProperties
[
  { "name": "ProjectId",    "value": "PRJ-2026-001", "type": "text"    },
  { "name": "ReviewCount",  "value": "3",            "type": "number"  },
  { "name": "Approved",     "value": "true",         "type": "boolean" },
  { "name": "ApprovalDate", "value": "2026-05-22",   "type": "date"    }
]`}</Code>

            <H3>Footnotes &amp; Endnotes</H3>
            <p>Add from the <em>Word / DOCX Elements</em> toolbox group. Footnote text appears in a preview box in the editor; in the DOCX export it is placed in <code className="docs-inline-code">footnotes.xml</code> / <code className="docs-inline-code">endnotes.xml</code> with automatic numbering.</p>
            <Code lang="json">{`{ "id": "fn-1", "type": "footnote", "x": 96, "y": 720, "width": 300, "height": 32,
  "footnoteText": "See ISO 32000-2:2020 for the full PDF specification." }

{ "id": "en-1", "type": "endnote",  "x": 96, "y": 760, "width": 300, "height": 32,
  "footnoteText": "Published by Adobe Systems, 2020." }`}</Code>

            <H3>Bookmarks</H3>
            <Code lang="json">{`{ "id": "bm-1", "type": "bookmark",
  "bookmarkName":   "section-2",
  "bookmarkTarget": "#section-2"    // used for hyperlink cross-references }`}</Code>

            <H3>Word Comments</H3>
            <Code lang="json">{`{ "id": "cm-1", "type": "comment",
  "commentText":   "Please verify this clause with legal.",
  "commentAuthor": "Jane Smith",
  "commentDate":   "2026-05-22",
  "commentId":     "1" }`}</Code>

            <H3>Content Controls</H3>
            <Code lang="json">{`{ "id": "cc-1", "type": "contentcontrol",
  "contentControlType":        "richText",    // "richText" | "plainText" | "date" | "comboBox" | "picture"
  "contentControlTitle":       "Clause Body",
  "contentControlTag":         "clause-body",
  "contentControlPlaceholder": "Enter clause text here…" }`}</Code>

            <H3>Auto-Hyphenation</H3>
            <p>Set <code className="docs-inline-code">autoHyphenation: true</code> on any text or richtext element (Inspector → Word / DOCX section). When any element opts in, <code className="docs-inline-code">{'<w:autoHyphenation>'}</code> is also written to the document-level settings.</p>
          </section>

          {/* ── JSON Schema ──────────────────────────────────────────────── */}
          <section id="json-schema" className="docs-section">
            <H2 id="json-schema">JSON Schema</H2>
            <p>Exporting a document from the editor produces a single JSON file. This is the canonical data format consumed by the Canvas WebAPI to generate PDF output.</p>

            <H3>Full template structure</H3>
            <Code lang="json">{`{
  "id":          "invoice-freelancer",
  "name":        "Freelancer Invoice",
  "category":    "invoice",
  "description": "Minimal hourly invoice",
  "pages": [
    {
      "id": "page-1",
      "elements": [ /* SimpleElement[] — see Elements Reference */ ]
    },
    {
      "id": "page-2",
      "elements": []
    }
  ],
  "sharedElements": [
    /* Elements placed here appear on every page (e.g. header/footer) */
  ],
  "pageSettings": {
    "width":       595,            // A4 width in points (1 pt = 1/72 inch)
    "height":      842,            // A4 height in points
    "unit":        "pt",
    "orientation": "portrait",
    "backgroundColor": "#ffffff",
    "backgroundImage": null,
    "margins": { "top": 48, "right": 48, "bottom": 48, "left": 48 },
    "metadata": {
      "title": "My Document", "author": "Jane Smith",
      "subject": "", "keywords": ""
    },
    "namedStyles":       [],       // NamedStyleDto[] — see Word / DOCX Features
    "customProperties":  [],       // CustomDocumentPropertyDto[]
    "trackChanges":      false,    // enables revision wrapping in DOCX
    "protection": {
      "enabled":      false,
      "mode":         "readOnly", // "readOnly"|"comments"|"trackedChanges"|"formFields"
      "passwordHash": ""
    }
  },
  "data": {}                       // reserved for variable-binding data
}`}</Code>

            <H3>Page settings reference</H3>
            <div className="docs-prop-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Property</th><th>Type</th><th>Default</th><th>Description</th></tr></thead>
                <tbody>
                  {[
                    ['width', 'number', '595', 'Page width in points. 595 = A4, 612 = US Letter'],
                    ['height', 'number', '842', 'Page height in points. 842 = A4, 792 = US Letter'],
                    ['unit', 'string', '"pt"', 'Unit system. Currently only "pt" is supported'],
                    ['backgroundColor', 'string?', '"#ffffff"', 'Page background colour (hex)'],
                    ['backgroundImage', 'string?', 'null', 'URL or base64 data URI for a background image'],
                  ].map(([p, t, d, desc]) => (
                    <tr key={p as string}>
                      <td><code className="docs-inline-code">{p}</code></td>
                      <td>{t}</td>
                      <td><code className="docs-inline-code">{d}</code></td>
                      <td>{desc}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* ── C# Models ────────────────────────────────────────────────── */}
          <section id="csharp-models" className="docs-section">
            <H2 id="csharp-models">C# Models</H2>
            <p>These classes map 1-to-1 to the JSON schema. Use <code className="docs-inline-code">System.Text.Json</code> or <code className="docs-inline-code">Newtonsoft.Json</code> to deserialize the exported file.</p>

            <Code lang="csharp">{`// TemplateDocument.cs
public class TemplateDocument
{
    public string Id          { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Category    { get; set; } = "";
    public string Description { get; set; } = "";

    public List<Page>    Pages          { get; set; } = new();
    public List<Element> SharedElements { get; set; } = new();
    public PageSettings  PageSettings   { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

public class Page
{
    public string        Id       { get; set; } = "";
    public List<Element> Elements { get; set; } = new();
}

public class PageSettings
{
    public int    Width           { get; set; } = 595;
    public int    Height          { get; set; } = 842;
    public string Unit            { get; set; } = "pt";
    public string BackgroundColor { get; set; } = "#ffffff";
    public string? BackgroundImage { get; set; }
}`}</Code>

            <Code lang="csharp">{`// Element.cs
public class Element
{
    // ── Position & size ──────────────────────────────────
    public string Id     { get; set; } = "";
    public string Type   { get; set; } = "";   // "text" | "table" | "qrcode" | ...
    public int    X      { get; set; }
    public int    Y      { get; set; }
    public int    Width  { get; set; }
    public int    Height { get; set; }

    // ── Text / RichText ──────────────────────────────────
    public string?       Content     { get; set; }
    public string?       HtmlContent { get; set; }
    public ElementStyle? Style       { get; set; }

    // ── Table ────────────────────────────────────────────
    public bool?                HeaderRow        { get; set; }
    public bool?                FooterRow        { get; set; }
    public string?              HeaderBgColor    { get; set; }
    public bool?                ZebraEnabled     { get; set; }
    public string?              ZebraColor       { get; set; }
    public List<int>?           ColumnWidths     { get; set; }
    public List<string>?        ColumnAlignments { get; set; }
    public List<List<string>>?  CellData         { get; set; }

    // ── Form fields ──────────────────────────────────────
    public string? FieldLabel   { get; set; }
    public string? FieldName    { get; set; }
    public bool    Required     { get; set; }
    public string? Placeholder  { get; set; }

    // ── QR code ──────────────────────────────────────────
    public string? QrValue { get; set; }
    public int?    QrSize  { get; set; }

    // ── Barcode ──────────────────────────────────────────
    public string? BarcodeValue { get; set; }
    public string? BarcodeType  { get; set; }  // "CODE128" | "EAN13" | "UPCA"

    // ── Signature ────────────────────────────────────────
    public string? SignatureLabel { get; set; }

    // ── Chart ────────────────────────────────────────────
    public string?    ChartType { get; set; }   // "bar" | "line" | "pie"
    public ChartData? ChartData { get; set; }

    // ── Date ─────────────────────────────────────────────
    public string? DateMode { get; set; }  // "auto" | "static" | "binding"
    public string? Locale   { get; set; }  // e.g. "de-DE", "en-US"

    // ── Page number ──────────────────────────────────────
    public string? NumberingFormat { get; set; } // "pageOfTotal" | "current" | "roman"
    public string? Prefix          { get; set; }
    public string? Suffix          { get; set; }
    public int?    StartNumber     { get; set; }
}

public class ChartData
{
    public List<string>      Labels   { get; set; } = new();
    public List<ChartDataset> Datasets { get; set; } = new();
}

public class ChartDataset
{
    public string       Label { get; set; } = "";
    public List<double> Data  { get; set; } = new();
}`}</Code>

            <Code lang="csharp">{`// ElementStyle.cs
public class ElementStyle
{
    // Typography
    public int?    FontSize       { get; set; }
    public string? Color          { get; set; }
    public string? FontWeight     { get; set; }  // "normal" | "bold"
    public string? FontStyle      { get; set; }  // "normal" | "italic"
    public string? FontFamily     { get; set; }
    public string? TextAlign      { get; set; }  // "left" | "center" | "right"
    public string? TextDecoration { get; set; }
    public double? LineHeight     { get; set; }
    public double? LetterSpacing  { get; set; }

    // Background & border
    public string? BackgroundColor { get; set; }
    public double? BackgroundOpacity { get; set; }
    public string? BorderColor     { get; set; }
    public int?    BorderWidth     { get; set; }
    public string? BorderStyle     { get; set; }  // "solid" | "dashed" | "dotted"
    public int?    BorderRadius    { get; set; }

    // Shape fill (rect/circle)
    public string? Fill            { get; set; }

    // Padding
    public int? PaddingTop    { get; set; }
    public int? PaddingRight  { get; set; }
    public int? PaddingBottom { get; set; }
    public int? PaddingLeft   { get; set; }

    // Table grid dimensions (stored in style for tables)
    public int? Rows        { get; set; }
    public int? Columns     { get; set; }
    public int? CellPadding { get; set; }

    // Transform
    public double? Rotation { get; set; }  // degrees
}`}</Code>
          </section>

          {/* ── C# Code Examples ─────────────────────────────────────────── */}
          <section id="csharp-examples" className="docs-section">
            <H2 id="csharp-examples">C# Code Examples</H2>

            <H3>Deserializing an exported JSON file</H3>
            <Code lang="csharp">{`using System.Text.Json;
using System.Text.Json.Serialization;

var json = await File.ReadAllTextAsync("invoice.json");

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var template = JsonSerializer.Deserialize<TemplateDocument>(json, options)
    ?? throw new InvalidOperationException("Invalid template JSON");

Console.WriteLine($"Template: {template.Name}");
Console.WriteLine($"Pages: {template.Pages.Count}");
Console.WriteLine($"Elements on page 1: {template.Pages[0].Elements.Count}");`}</Code>

            <H3>Building a template programmatically</H3>
            <Code lang="csharp">{`var template = new TemplateDocument
{
    Id       = "invoice-001",
    Name     = "My Invoice",
    Category = "invoice",
    Pages    = new List<Page>
    {
        new Page
        {
            Id = "page-1",
            Elements = new List<Element>
            {
                new Element
                {
                    Id     = $"title-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type   = "text",
                    X      = 72,
                    Y      = 72,
                    Width  = 360,
                    Height = 48,
                    Content = "INVOICE",
                    Style  = new ElementStyle
                    {
                        FontSize   = 28,
                        Color      = "#1d6fff",
                        FontWeight = "bold"
                    }
                },
                new Element
                {
                    Id     = $"table-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Type   = "table",
                    X      = 72,
                    Y      = 180,
                    Width  = 451,
                    Height = 160,
                    Style  = new ElementStyle { Rows = 4, Columns = 3, BorderWidth = 1 },
                    HeaderRow    = true,
                    HeaderBgColor = "#1d6fff",
                    CellData     = new List<List<string>>
                    {
                        new() { "Description", "Qty", "Total" },
                        new() { "Web Design",  "1",   "€ 1,200" },
                        new() { "Hosting",     "1",   "€ 150"   },
                        new() { "Total",       "",    "€ 1,350" },
                    }
                }
            }
        }
    }
};`}</Code>

            <H3>Serializing back to JSON</H3>
            <Code lang="csharp">{`var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var json = JsonSerializer.Serialize(template, options);
await File.WriteAllTextAsync("output.json", json);`}</Code>

            <H3>Rendering to PDF via Canvas WebAPI</H3>
            <Code lang="csharp">{`using var http = new HttpClient { BaseAddress = new Uri("https://localhost:5274") };

// Serialize the template
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var payload = JsonSerializer.Serialize(new
{
    template,
    data = new Dictionary<string, string>
    {
        ["client_name"] = "Acme GmbH",
        ["invoice_date"] = "2026-05-19"
    }
}, options);

var response = await http.PostAsync(
    "/api/templates/render",
    new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
);

response.EnsureSuccessStatusCode();

var pdfBytes = await response.Content.ReadAsByteArrayAsync();
await File.WriteAllBytesAsync("invoice-rendered.pdf", pdfBytes);

Console.WriteLine($"PDF saved ({pdfBytes.Length:N0} bytes)");`}</Code>

            <H3>Iterating elements by type</H3>
            <Code lang="csharp">{`// Get all text elements across all pages
var textElements = template.Pages
    .SelectMany(p => p.Elements)
    .Where(e => e.Type == "text")
    .ToList();

// Get all form fields (required only)
var requiredFields = template.Pages
    .SelectMany(p => p.Elements)
    .Where(e => e.Type is "field" or "checkbox" && e.Required)
    .Select(e => e.FieldName)
    .ToList();

// Count tables
var tableCount = template.Pages
    .Sum(p => p.Elements.Count(e => e.Type == "table"));

// Replace all text content matching a pattern
foreach (var el in template.Pages.SelectMany(p => p.Elements))
{
    if (el.Type == "text" && el.Content?.Contains("{{client}}") == true)
        el.Content = el.Content.Replace("{{client}}", "Acme GmbH");
}`}</Code>
          </section>

          {/* ── JSON → C# Mapping ────────────────────────────────────────── */}
          <section id="json-to-csharp" className="docs-section">
            <H2 id="json-to-csharp">JSON → C# Mapping</H2>
            <p>The table below shows how each JSON property name maps to its C# counterpart. <code className="docs-inline-code">System.Text.Json</code> with <code className="docs-inline-code">PropertyNameCaseInsensitive = true</code> handles the camelCase ↔ PascalCase conversion automatically.</p>

            <div className="docs-prop-table-wrap">
              <table className="docs-elem-table">
                <thead>
                  <tr><th>JSON (camelCase)</th><th>C# (PascalCase)</th><th>Type</th><th>Notes</th></tr>
                </thead>
                <tbody>
                  {([
                    ['id',                    'Id',                 'string',                  ''],
                    ['name',                  'Name',               'string',                  ''],
                    ['category',              'Category',           'string',                  ''],
                    ['description',           'Description',        'string',                  ''],
                    ['pages',                 'Pages',              'List<Page>',              ''],
                    ['pages[].id',            'Page.Id',            'string',                  ''],
                    ['pages[].elements',      'Page.Elements',      'List<Element>',           ''],
                    ['sharedElements',        'SharedElements',     'List<Element>',           'Same type as page elements'],
                    ['pageSettings',          'PageSettings',       'PageSettings',            ''],
                    ['pageSettings.width',    'PageSettings.Width', 'int',                     '595 = A4'],
                    ['pageSettings.height',   'PageSettings.Height','int',                     '842 = A4'],
                    ['elements[].id',         'Element.Id',         'string',                  ''],
                    ['elements[].type',       'Element.Type',       'string',                  '"text" | "table" | ...'],
                    ['elements[].x',          'Element.X',          'int',                     'Points from left'],
                    ['elements[].y',          'Element.Y',          'int',                     'Points from top'],
                    ['elements[].width',      'Element.Width',      'int',                     ''],
                    ['elements[].height',     'Element.Height',     'int',                     ''],
                    ['elements[].content',    'Element.Content',    'string?',                 'Text/image URL/watermark'],
                    ['elements[].htmlContent','Element.HtmlContent','string?',                 'richtext type only'],
                    ['elements[].style',      'Element.Style',      'ElementStyle?',           ''],
                    ['elements[].cellData',   'Element.CellData',   'List<List<string>>?',     'table type only'],
                    ['elements[].headerRow',  'Element.HeaderRow',  'bool?',                   'table type only'],
                    ['elements[].qrValue',    'Element.QrValue',    'string?',                 'qrcode type only'],
                    ['elements[].barcodeValue','Element.BarcodeValue','string?',               'barcode type only'],
                    ['elements[].fieldLabel', 'Element.FieldLabel', 'string?',                 'field/checkbox types'],
                    ['elements[].fieldName',  'Element.FieldName',  'string?',                 'Machine-readable key'],
                    ['elements[].required',   'Element.Required',   'bool',                    ''],
                    ['elements[].chartType',  'Element.ChartType',  'string?',                 '"bar"|"line"|"pie"'],
                    ['elements[].chartData',  'Element.ChartData',  'ChartData?',              ''],
                    ['style.fontSize',        'ElementStyle.FontSize',  'int?',                ''],
                    ['style.color',           'ElementStyle.Color',     'string?',             'Hex colour e.g. "#111827"'],
                    ['style.fontWeight',      'ElementStyle.FontWeight','string?',             '"normal" | "bold"'],
                    ['style.backgroundColor', 'ElementStyle.BackgroundColor','string?',        ''],
                    ['style.borderWidth',     'ElementStyle.BorderWidth','int?',               'Pixels'],
                    ['style.borderColor',     'ElementStyle.BorderColor','string?',            ''],
                    ['style.borderRadius',    'ElementStyle.BorderRadius','int?',              'Points'],
                    ['style.rows',            'ElementStyle.Rows',      'int?',                'table grid rows'],
                    ['style.columns',         'ElementStyle.Columns',   'int?',                'table grid columns'],
                  ] as [string, string, string, string][]).map(([json, cs, type, note]) => (
                    <tr key={json}>
                      <td><code className="docs-inline-code">{json}</code></td>
                      <td><code className="docs-inline-code">{cs}</code></td>
                      <td>{type}</td>
                      <td className="docs-note-cell">{note}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <H3>Newtonsoft.Json alternative</H3>
            <Code lang="csharp">{`// If you prefer Newtonsoft.Json over System.Text.Json:
// Install-Package Newtonsoft.Json

using Newtonsoft.Json;

var settings = new JsonSerializerSettings
{
    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
    NullValueHandling = NullValueHandling.Ignore
};

// Deserialize
var template = JsonConvert.DeserializeObject<TemplateDocument>(json, settings);

// Serialize
var output = JsonConvert.SerializeObject(template, Formatting.Indented, settings);`}</Code>
          </section>

          {/* ── REST API ─────────────────────────────────────────────────── */}
          <section id="rest-api" className="docs-section">
            <H2 id="rest-api">REST API</H2>
            <p>The Canvas WebAPI (ASP.NET Core, <code className="docs-inline-code">Canvas.WebApi</code>) runs at <code className="docs-inline-code">http://localhost:5274</code> by default. Swagger UI is at <code className="docs-inline-code">http://localhost:5274/swagger</code>.</p>

            <H3>All endpoints</H3>
            <div className="docs-endpoint-grid">
              {[
                { method: 'POST', path: '/api/export',                     desc: 'Export a design to any format. Body: { format, design, data }. Format key: pdf | word | odt | excel | html | csv | md | png | jpeg | tiff.' },
                { method: 'GET',  path: '/api/export/formats',             desc: 'List supported export formats and their capabilities.' },
                { method: 'POST', path: '/api/document/find-replace',      desc: 'Find and replace text across all elements. Body: { design, find, replace, caseSensitive, wholeWord, useRegex }.' },
                { method: 'POST', path: '/api/document/clone',             desc: 'Deep-clone a design with new IDs. Body: { design, newName? }.' },
                { method: 'POST', path: '/api/document/extract-pages',     desc: 'Extract a page subset. Body: { design, pageNumbers: number[], newName? }.' },
                { method: 'POST', path: '/api/document/sign-docx',         desc: 'Apply X.509 digital signature to a DOCX. Multipart: docx file + certificate PFX + optional password. Returns signed DOCX.' },
                { method: 'POST', path: '/api/document/import-pdf-engine',   desc: 'Import PDF → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-docx',       desc: 'Import DOCX → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-doc',        desc: 'Import Word 97-2003 .doc → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-odt',        desc: 'Import ODT → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-image',      desc: 'Import raster image (PNG, JPG, GIF, WebP, BMP, TIFF) → DesignExportDto. Creates a single-page design with the image filling the page.' },
                { method: 'POST', path: '/api/templates/render',           desc: 'Render a template with data to PDF.' },
                { method: 'POST', path: '/api/templates/render-design',    desc: 'Render a raw DesignExportDto to PDF.' },
                { method: 'POST', path: '/api/templates',                  desc: 'Create and persist a template.' },
                { method: 'GET',  path: '/api/templates/{id}',             desc: 'Retrieve a template by ID.' },
                { method: 'POST', path: '/api/templates/validate',         desc: 'Validate a template without rendering.' },
                { method: 'POST', path: '/api/templates/csharp-code-to-pdf',  desc: 'Compile C# code that returns a DesignExportDto and render to PDF.' },
                { method: 'POST', path: '/api/templates/csharp-to-json',       desc: 'Compile C# class → DesignExportDto JSON.' },
              ].map(ep => (
                <div className="docs-endpoint-card" key={ep.path + ep.method}>
                  <span className={`docs-method docs-method--${ep.method.toLowerCase()}`}>{ep.method}</span>
                  <code className="docs-inline-code docs-endpoint-path">{ep.path}</code>
                  <p>{ep.desc}</p>
                </div>
              ))}
            </div>

            <H3>POST /api/templates/render</H3>
            <Code lang="http">{`POST https://localhost:5274/api/templates/render
Content-Type: application/json
Accept: application/pdf

{
  "id": "invoice-freelancer",
  "name": "Freelancer Invoice",
  "category": "invoice",
  "pages": [
    {
      "id": "page-1",
      "elements": [
        {
          "id": "title-1",
          "type": "text",
          "x": 72, "y": 72, "width": 360, "height": 48,
          "content": "INVOICE",
          "style": { "fontSize": 28, "color": "#1d6fff", "fontWeight": "bold" }
        }
      ]
    }
  ],
  "sharedElements": [],
  "pageSettings": { "width": 595, "height": 842, "unit": "pt" }
}

──────────────────────────────────────────────────────
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="invoice-freelancer.pdf"

[binary PDF content]`}</Code>

            <H3>Error responses</H3>
            <Code lang="json">{`// 400 Bad Request — validation failure
{
  "type":   "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title":  "Validation failed",
  "status": 400,
  "errors": {
    "pages": ["At least one page is required"]
  }
}

// 500 Internal Server Error — render failure
{
  "type":    "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title":   "Render error",
  "status":  500,
  "detail":  "PdfDocumentRenderer: unsupported element type 'unknown'"
}`}</Code>

            <H3>Register in ASP.NET Core (Program.cs)</H3>
            <Code lang="csharp">{`// Canvas.WebApi/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Core domain services
builder.Services.AddScoped<ITemplateRepository, InMemoryTemplateRepository>();
builder.Services.AddScoped<ITemplateExpander,  TemplateExpander>();
builder.Services.AddScoped<IExpressionEvaluator, ExpressionEvaluator>();

// PDF rendering backend
builder.Services.AddScoped<IDocumentRenderer, PdfDocumentRenderer>();
builder.Services.AddScoped<IOutputWriter,     FileOutputWriter>();

// Use cases
builder.Services.AddScoped<RenderTemplateUseCase>();
builder.Services.AddScoped<CreateTemplateUseCase>();
builder.Services.AddScoped<ValidateTemplateUseCase>();

// CORS for the React frontend
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000")
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();`}</Code>

            <div className="docs-callout docs-callout--info">
              <strong>Note:</strong> The WebAPI is started separately from the React app. Run <code className="docs-inline-code">dotnet run</code> inside <code className="docs-inline-code">Canvas.WebApi/</code> — it defaults to <code className="docs-inline-code">http://localhost:5274</code>. The React dev server runs on port <code className="docs-inline-code">5173</code>.
            </div>

            <H3>Minimal controller example</H3>
            <Code lang="csharp">{`// Controllers/TemplatesController.cs
[ApiController]
[Route("api/templates")]
public class TemplatesController : ControllerBase
{
    private readonly RenderTemplateUseCase _render;

    public TemplatesController(RenderTemplateUseCase render) => _render = render;

    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] TemplateDocument template)
    {
        var result = await _render.ExecuteAsync(template);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return File(result.Value, "application/pdf", $"{template.Id}.pdf");
    }
}`}</Code>

            <div className="docs-section-end">
              <p>Need more help? Open a template in the editor, export the JSON, and use it as the starting point for your own integration.</p>
              <div className="docs-end-actions">
                <button className="tpl-use-button" style={{ width: 'auto', padding: '0 24px' }} onClick={() => navigate('/template')}>
                  Browse templates <FiChevronRight size={16} />
                </button>
                <button className="pdf-outline-button" onClick={() => navigate('/')}>
                  Go to home
                </button>
              </div>
            </div>
          </section>

        </main>
      </div>
    </div>
  );
};

export default DocsPage;
