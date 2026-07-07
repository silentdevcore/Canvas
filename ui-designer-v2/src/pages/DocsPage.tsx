import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  FiCheck,
  FiChevronRight,
  FiCopy,
  FiMenu,
} from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';
import {
  ELEMENT_CATALOG, COMMON_PROPERTIES, STYLE_KEYS, elementsByCategory, toDesign,
  type ElementDoc, type ElementProperty,
} from '@/docs/elementCatalog';

const BACKEND_URL = 'http://localhost:5086';

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

// ─── Element reference (catalog-driven) ───────────────────────────────────────

const PropertyTable: React.FC<{ rows: ElementProperty[] }> = ({ rows }) => (
  <div className="docs-elem-table-wrap">
    <table className="docs-elem-table">
      <thead>
        <tr><th>Property</th><th>Type</th><th>Default</th><th>Description</th></tr>
      </thead>
      <tbody>
        {rows.map(p => (
          <tr key={p.name}>
            <td><code className="docs-inline-code">{p.name}</code></td>
            <td className="docs-props-cell">{p.allowedValues ? p.allowedValues.map(v => `"${v}"`).join(' | ') : p.type}</td>
            <td>{p.default ? <code className="docs-inline-code">{p.default}</code> : '—'}</td>
            <td>{p.description}</td>
          </tr>
        ))}
      </tbody>
    </table>
  </div>
);

// One per-element card: description, property table, copy-paste design JSON, optional C#, and a live
// preview rendered by the backend (POST /api/templates/render-design → PDF shown in an iframe).
const ElementCard: React.FC<{ doc: ElementDoc }> = ({ doc }) => {
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const designJson = JSON.stringify(toDesign(doc.example, doc.label), null, 2);
  const fmt = doc.formatSupport;

  const renderPreview = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${BACKEND_URL}/api/templates/render-design`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: designJson,
      });
      if (!res.ok) throw new Error(`Render failed (${res.status})`);
      const blob = await res.blob();
      setPreviewUrl(prev => {
        if (prev) URL.revokeObjectURL(prev);
        return URL.createObjectURL(blob);
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Render failed — is the backend running on :5086?');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="docs-elem-card" id={`element-${doc.type}`}>
      <div className="docs-elem-card-head">
        <h4 className="docs-elem-card-title">{doc.label} <code className="docs-inline-code">{doc.type}</code></h4>
        <span className="docs-elem-card-formats">
          {fmt.pdf && <span className="docs-badge">PDF</span>}
          {fmt.word && <span className="docs-badge">Word</span>}
          {fmt.html && <span className="docs-badge">HTML</span>}
          {fmt.excel && <span className="docs-badge">Excel</span>}
          {doc.bindable && <span className="docs-badge docs-badge--bind">Bindable</span>}
        </span>
      </div>
      <p>{doc.description}</p>
      {doc.properties.length > 0 && <PropertyTable rows={doc.properties} />}
      <Code lang="json">{designJson}</Code>
      {doc.csharpExample && <Code lang="csharp">{doc.csharpExample}</Code>}
      {fmt.pdf ? (
        <div className="docs-elem-demo">
          <button className="docs-demo-btn" onClick={renderPreview} disabled={loading}>
            {loading ? 'Rendering…' : 'Render preview'}
          </button>
          {error && <span className="docs-demo-error">{error}</span>}
          {previewUrl && <iframe className="docs-demo-frame" title={`${doc.label} preview`} src={previewUrl} />}
        </div>
      ) : (
        <p className="docs-elem-note">Designer-only guide — not rendered to output formats.</p>
      )}
    </div>
  );
};

// ─── Nav sections ─────────────────────────────────────────────────────────────

const SECTIONS = [
  { id: 'quick-start',     label: 'Quick Start' },
  { id: 'editor-overview', label: 'Editor Overview' },
  { id: 'elements',        label: 'Elements Reference' },
  { id: 'data-binding',    label: 'Data Binding & Expressions' },
  { id: 'import-export',   label: 'Import & Export' },
  { id: 'document-ops',    label: 'Document Operations' },
  { id: 'migrations',      label: 'Migrations' },
  { id: 'word-features',   label: 'Word / DOCX Features' },
  { id: 'json-schema',     label: 'JSON Schema' },
  { id: 'csharp-models',   label: 'C# Models' },
  { id: 'csharp-examples', label: 'C# Code Examples' },
  { id: 'json-to-csharp',  label: 'JSON → C# Mapping' },
  { id: 'rest-api',        label: 'REST API' },
  { id: 'ai-codegen',      label: 'AI & Codegen' },
  { id: 'spreadsheets',    label: 'Spreadsheets' },
  { id: 'documentation-map', label: 'Documentation Map' },
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
          <button
            className="docs-mobile-nav-toggle"
            onClick={() => setMobileNavOpen(true)}
            aria-label="Open documentation navigation"
          >
            <FiMenu size={16} />
            Sections
          </button>

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
                { label: 'Import File', desc: 'On the Templates page, click "Import file" to open an existing PDF, DOCX, DOC, or ODT as a PXA design. The document is converted to editable elements.' },
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
            <p>Every element is absolutely positioned on the canvas using <code className="docs-inline-code">x</code>, <code className="docs-inline-code">y</code>, <code className="docs-inline-code">width</code>, and <code className="docs-inline-code">height</code> in points (pt). Each element below lists its type-specific properties with a copy-paste design JSON, a C# equivalent where the PXA PDF API maps directly, and a <strong>live preview</strong> rendered by the backend.</p>

            <H3>Common element properties</H3>
            <p>Shared by every element type (in addition to the type-specific properties listed per element):</p>
            <PropertyTable rows={COMMON_PROPERTIES} />

            <H3>Common <code className="docs-inline-code">style</code> keys</H3>
            <p>Accepted inside the <code className="docs-inline-code">style</code> map; renderers ignore unknown keys.</p>
            <PropertyTable rows={STYLE_KEYS} />

            <H3>Support matrix</H3>
            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead>
                  <tr>
                    <th>Type ID</th><th>Name</th>
                    <th style={{ textAlign: 'center' }}>PDF</th>
                    <th style={{ textAlign: 'center' }}>Word</th>
                    <th style={{ textAlign: 'center' }}>HTML</th>
                    <th style={{ textAlign: 'center' }}>Excel</th>
                  </tr>
                </thead>
                <tbody>
                  {ELEMENT_CATALOG.map(el => (
                    <tr key={el.type}>
                      <td><a className="docs-inline-code docs-elem-link" href={`#element-${el.type}`}>{el.type}</a></td>
                      <td style={{ whiteSpace: 'nowrap' }}>{el.label}</td>
                      <td style={{ textAlign: 'center', fontSize: 15 }}>{el.formatSupport.pdf ? '✅' : '—'}</td>
                      <td style={{ textAlign: 'center', fontSize: 15 }}>{el.formatSupport.word ? '✅' : '—'}</td>
                      <td style={{ textAlign: 'center', fontSize: 15 }}>{el.formatSupport.html ? '✅' : '—'}</td>
                      <td style={{ textAlign: 'center', fontSize: 15 }}>{el.formatSupport.excel ? '✅' : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {elementsByCategory().map(group => (
              <React.Fragment key={group.category}>
                <H3>{group.category}</H3>
                {group.elements.map(doc => <ElementCard key={doc.type} doc={doc} />)}
              </React.Fragment>
            ))}
          </section>

          {/* ── Data Binding & Expressions ──────────────────────────────── */}
          <section id="data-binding" className="docs-section">
            <H2 id="data-binding">Data Binding &amp; Expressions</H2>
            <p>Elements become dynamic in three ways. At render time you POST your design plus a JSON <code className="docs-inline-code">data</code> payload; the engine resolves tokens, evaluates expressions, and expands repeats.</p>

            <H3>1. Tokens — <code className="docs-inline-code">{'{{field}}'}</code></H3>
            <p>Any text/content may contain <code className="docs-inline-code">{'{{path.to.field}}'}</code> placeholders, replaced from the data payload. Dotted paths walk nested objects.</p>
            <Code lang="json">{`// element.content
"Invoice {{invoice.number}} — {{customer.name}}"

// data payload
{ "invoice": { "number": "1001" }, "customer": { "name": "ACME" } }`}</Code>

            <H3>2. Expressions — <code className="docs-inline-code">element.expression</code></H3>
            <p>The <code className="docs-inline-code">expression</code> field is evaluated with the Canvas expression grammar (the same engine on the server and in the live preview). Helpers and operators:</p>
            <PropertyTable rows={[
              { name: '$iif(cond, a, b)', type: 'helper', description: 'Conditional; short-circuits (b is not evaluated when cond is true).' },
              { name: '$concat(...)', type: 'helper', description: 'Concatenate values into a string.' },
              { name: '$coalesce(...)', type: 'helper', description: 'First non-null argument.' },
              { name: '$switch(c1, v1, …, default?)', type: 'helper', description: 'Multi-branch selection.' },
              { name: '$sum / $avg / $min / $max', type: 'aggregate', description: 'Aggregate over a dataset (or $group); 2nd arg is a field name OR a per-row expression, e.g. $sum(Orders, "Qty * Price").' },
              { name: '$count / $first / $last', type: 'aggregate', description: 'Count / first / last over a dataset.' },
              { name: '&&  ||  ??  ==  !=  <  <=  >  >=  + - * / %', type: 'operators', description: 'Logical (short-circuiting), comparison, and arithmetic operators.' },
            ]} />
            <Code lang="json">{`"expression": "$iif(total > 1000, $concat(\\"VIP: \\", customer), customer)"`}</Code>

            <H3>3. Repeats &amp; group aggregates — <code className="docs-inline-code">element.repeat</code></H3>
            <p>Bind an element (typically a table or a group band) to a dataset to render it once per row, or once per group. Inside a group, <code className="docs-inline-code">$group</code> is the current group's rows, so a footer total scopes to the group rather than the whole dataset.</p>
            <Code lang="json">{`{
  "type": "text",
  "expression": "$sum($group, \\"Amount\\")",   // per-group subtotal
  "repeat": { "dataPath": "Sales" },
  "style": { "groupField": "Region" }            // partitions Sales by Region
}`}</Code>
            <div className="docs-callout docs-callout--tip">
              <strong>Tip:</strong> the live preview in the editor uses the same grammar, so what you see matches the exported PDF/Word output.
            </div>
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
            <p>On the <strong>Templates</strong> page, click <em>Import file</em> and choose a supported file. The document is converted to a PXA design and opened in the editor.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Extension</th><th>Source format</th><th>What is extracted</th></tr></thead>
                <tbody>
                  {([
                    ['.pdf',  'PDF',                    'PXA importer low-level parser/editor model: page tree, text, vector paths, images, clipping, colors, fonts, and regeneration bridge'],
                    ['.docx', 'Word Open XML',          'Paragraphs → Text; tables → Table; inline images → Image; typography from RunProperties; page size from SectionProperties'],
                    ['.doc',  'Word 97-2003 binary',    'Pure C# CFBF parser: reads WordDocument stream via FIB offsets; text stacked as paragraphs'],
                    ['.odt',  'OpenDocument Text',      'Paragraphs and headings with style resolution; draw:frame images extracted as base64'],
                    ['.svg',  'SVG',                    'Dedicated SVG importer maps vector-oriented content into editable PXA elements where possible'],
                    ['.pptx', 'PowerPoint',             'Slides become PXA pages; text, images, and shapes are mapped into editable elements'],
                    ['.png / .jpg / .jpeg / .gif / .webp / .bmp / .tiff', 'Raster image', 'Direct image import creates a single-page design; ImageAnalysis/OCR paths can reconstruct editable text, shapes, and diagnostics'],
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

          {/* ── Migrations ──────────────────────────────────────────────── */}
          <section id="migrations" className="docs-section">
            <H2 id="migrations">Migrations</H2>
            <p>Power Dox Automation includes developer migration tools, organized into two types on the <strong>Migrations</strong> page: <strong>Code Migration</strong> (third-party library C# → PXA-compatible code — <strong>15 PDF</strong> + <strong>4 spreadsheet</strong> libraries) and <strong>DataSource / Format Migration</strong> (a source file/format → an editable PXA design or workbook — report designers, documents, and spreadsheets). Each opens an interactive converter with diagnostics and preview.</p>

            <H3>PDF code migration</H3>
            <p>Paste C# source written for a supported PDF library and convert deterministic document-generation patterns into PXA-compatible PDF C# code. Unsupported provider APIs stay visible through diagnostics instead of being silently rewritten.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Provider family</th><th>What is migrated</th><th>Manual areas</th></tr></thead>
                <tbody>
                  {([
                    ['DevExpress PDF, Syncfusion PDF, iText 7, Aspose.PDF, DsPdf', 'Document/page creation, simple text, lines, rectangles, colors, save/export where deterministic', 'Existing-PDF editing, forms, signatures, advanced layout, compliance'],
                    ['IronPDF, ActivePDF', 'PXA-compatible PDF scaffold and save/export paths', 'HTML/CSS/URL/Razor rendering, printer/COM/server workflows'],
                    ['Apryse, Foxit, GemBox, Spire, PDFKit.NET, LEADTOOLS, PDFTools', 'Provider-specific safe subsets and diagnostics', 'Rendering/viewer/OCR/conversion, attachments, redaction, low-level editing'],
                  ] as [string,string,string][]).map(([provider, migrated, manual]) => (
                    <tr key={provider}>
                      <td>{provider}</td>
                      <td>{migrated}</td>
                      <td>{manual}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <H3>Spreadsheet code migration</H3>
            <p>Paste C# source written for a supported spreadsheet library and convert it into the PXA spreadsheet API (<code className="docs-inline-code">CanvasWorkbook</code> legacy model). Workbook/worksheet/cell/value/formula/style/save calls are rewritten via Roslyn; charts, pivots, conditional formatting, and data validation stay visible through diagnostics.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Library</th><th>Converter</th><th>Notes</th></tr></thead>
                <tbody>
                  {([
                    ['ClosedXML', 'PXA.Migration.Spreadsheet / legacy Canvas.Migration.ClosedXmlSpreadsheet', 'Reference impl; 1-based → 0-based index shift, alignment/fill colour, named ranges'],
                    ['EPPlus', 'PXA.Migration.Spreadsheet / legacy Canvas.Migration.EpplusSpreadsheet', 'Cells[..] indexer → Cell(..), Merge=true → Range(..).Merge(), alignment'],
                    ['GemBox.Spreadsheet', 'PXA.Migration.Spreadsheet / legacy Canvas.Migration.GemBoxSpreadsheet', 'Drops SetLicense, already 0-based, Font.Weight → Bold(), alignment'],
                    ['Aspose.Cells', 'PXA.Migration.Spreadsheet / legacy Canvas.Migration.AsposeCells', 'PutValue → Value, Worksheets[0] → AddSheet, SetColumnWidth → Column().Width()'],
                  ] as [string,string,string][]).map(([lib, converter, notes]) => (
                    <tr key={lib}>
                      <td>{lib}</td>
                      <td><code className="docs-inline-code">{converter}</code></td>
                      <td>{notes}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <H3>Report-to-design migration</H3>
            <p>Report converters target editable PXA design JSON, not raw PDF source. Converted reports can open directly in the visual editor.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Input</th><th>Converter</th><th>Output</th></tr></thead>
                <tbody>
                  {([
                    ['DevExpress XtraReport / REPX', 'PXA.Migration.Report / legacy Canvas.Migration.DevExpressReport', 'Band-flattened editable PXA design'],
                    ['RDL / RDLC / Syncfusion / Bold Reports', 'PXA.Migration.Report / legacy Canvas.Migration.Rdl', 'Page, header/footer, textbox, line, rectangle, image, tablix/table, barcode placeholders'],
                    ['ActiveReports / GrapeCity RPX', 'PXA.Migration.Report / legacy Canvas.Migration.Rpx', 'Section-report bands flattened into PXA elements'],
                  ] as [string,string,string][]).map(([input, converter, output]) => (
                    <tr key={input}>
                      <td>{input}</td>
                      <td><code className="docs-inline-code">{converter}</code></td>
                      <td>{output}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <H3>Migration API</H3>
            <Code lang="http">{`GET  /api/migration/frameworks
POST /api/migration/convert
POST /api/migration/report-to-design
POST /api/migration/preview`}</Code>
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
            <p>Exporting a document from the editor produces a single JSON file. This is the canonical data format consumed by the Power Dox Automation Web API to generate PDF output.</p>

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

            <H3>Rendering to PDF via Power Dox Automation Web API</H3>
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
            <p>The Power Dox Automation Web API (ASP.NET Core, currently hosted by the legacy <code className="docs-inline-code">Canvas.WebApi</code> project) runs at <code className="docs-inline-code">http://localhost:5274</code> by default. Swagger UI is at <code className="docs-inline-code">http://localhost:5274/swagger</code>.</p>

            <H3>All endpoints</H3>
            <div className="docs-endpoint-grid">
              {[
                { method: 'POST', path: '/api/export',                     desc: 'Export a design to any format. Body: { format, design, data }. Format key: pdf | word | odt | excel | html | csv | md | png | jpeg | tiff.' },
                { method: 'POST', path: '/api/export/multilanguage',        desc: 'Export one document per active language, usually returned as a ZIP when multiple languages are selected.' },
                { method: 'GET',  path: '/api/export/formats',             desc: 'List supported export formats and their capabilities.' },
                { method: 'POST', path: '/api/document/find-replace',      desc: 'Find and replace text across all elements. Body: { design, find, replace, caseSensitive, wholeWord, useRegex }.' },
                { method: 'POST', path: '/api/document/clone',             desc: 'Deep-clone a design with new IDs. Body: { design, newName? }.' },
                { method: 'POST', path: '/api/document/extract-pages',     desc: 'Extract a page subset. Body: { design, pageNumbers: number[], newName? }.' },
                { method: 'POST', path: '/api/document/sign-docx',         desc: 'Apply X.509 digital signature to a DOCX. Multipart: docx file + certificate PFX + optional password. Returns signed DOCX.' },
                { method: 'POST', path: '/api/document/convert-image-to-pdf', desc: 'Convert a raster image to PDF, optionally with OCR/debug parameters. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-pdf-engine', desc: 'Import PDF through the PXA importer facade → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/debug-pdf-engine',  desc: 'Return PDF importer diagnostics/debug output for a PDF upload.' },
                { method: 'POST', path: '/api/document/import-docx',       desc: 'Import DOCX → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-doc',        desc: 'Import Word 97-2003 .doc → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-odt',        desc: 'Import ODT → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-image',      desc: 'Import raster image (PNG, JPG, GIF, WebP, BMP, TIFF) → DesignExportDto. Creates a single-page design with the image filling the page.' },
                { method: 'POST', path: '/api/document/import-svg',        desc: 'Import SVG → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-pptx',       desc: 'Import PowerPoint .pptx → DesignExportDto. Multipart file upload.' },
                { method: 'POST', path: '/api/document/import-image-analysis', desc: 'Import raster image through the deterministic image-analysis pipeline → DesignExportDto plus optional diagnostics.' },
                { method: 'GET',  path: '/api/migration/frameworks',       desc: 'List supported PDF code migration frameworks and their status.' },
                { method: 'POST', path: '/api/migration/convert',          desc: 'Convert vendor PDF-generation C# source to PXA-compatible PDF C# with diagnostics.' },
                { method: 'POST', path: '/api/migration/report-to-design', desc: 'Convert XtraReport/REPX/RDL/RPX style report sources to editable DesignExportDto.' },
                { method: 'POST', path: '/api/migration/preview',          desc: 'Render migrated PXA-compatible PDF code to a PDF preview.' },
                { method: 'POST', path: '/api/templates/render',           desc: 'Render a template with data to PDF.' },
                { method: 'POST', path: '/api/templates/render/async',     desc: 'Start an asynchronous render job.' },
                { method: 'POST', path: '/api/templates/render-design',    desc: 'Render a raw DesignExportDto to PDF.' },
                { method: 'POST', path: '/api/templates',                  desc: 'Create and persist a template.' },
                { method: 'GET',  path: '/api/templates/{id}',             desc: 'Retrieve a template by ID.' },
                { method: 'PUT',  path: '/api/templates/{id}',             desc: 'Update an existing template.' },
                { method: 'POST', path: '/api/templates/validate',         desc: 'Validate a template without rendering.' },
                { method: 'POST', path: '/api/templates/csharp-code-to-pdf',  desc: 'Compile C# code that returns a DesignExportDto and render to PDF.' },
                { method: 'POST', path: '/api/templates/csharp-to-json',       desc: 'Compile C# class → DesignExportDto JSON.' },
                { method: 'POST', path: '/api/templates/csharp-code-to-json',  desc: 'Compile raw C# returning a PdfDocument and convert the result to JSON.' },
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
              <strong>Note:</strong> The Web API is started separately from the React app. Run <code className="docs-inline-code">dotnet run</code> inside the current backend project <code className="docs-inline-code">Canvas.WebApi/</code> — it defaults to <code className="docs-inline-code">http://localhost:5274</code>. The React dev server runs on port <code className="docs-inline-code">5173</code>.
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

          {/* ── AI & Codegen ────────────────────────────────────────────── */}
          <section id="ai-codegen" className="docs-section">
            <H2 id="ai-codegen">AI &amp; Codegen</H2>
            <p>An AI agent can generate documents in two ways. Pick the surface that fits the task, generate, validate, then render.</p>

            <H3>Two generation targets</H3>
            <ul className="docs-steps">
              <li><strong>Declarative design JSON</strong> (<code className="docs-inline-code">DesignExportDto</code>) — describe pages + elements, POST to the render/export API. Best for templates and data-driven documents.</li>
              <li><strong>Imperative C# code</strong> (PXA PDF API, with legacy <code className="docs-inline-code">Canvas.Pdf</code> compatibility) — <code className="docs-inline-code">new PdfDocument()</code> → <code className="docs-inline-code">page.DrawText(...)</code> → <code className="docs-inline-code">ToBytes()</code>. Best for programmatic generation and SDK migrations.</li>
            </ul>

            <H3>Generate → validate → render</H3>
            <ol className="docs-steps">
              <li>Generate a <code className="docs-inline-code">DesignExportDto</code> (or PXA-compatible PDF C#). Use the per-element catalog above for the exact properties.</li>
              <li>Validate the JSON against <code className="docs-inline-code">docs/schema/design-export.schema.json</code> (required: <code className="docs-inline-code">id</code>, <code className="docs-inline-code">name</code>, <code className="docs-inline-code">pages</code>; each element needs <code className="docs-inline-code">id</code>, <code className="docs-inline-code">type</code>, <code className="docs-inline-code">x</code>, <code className="docs-inline-code">y</code>, <code className="docs-inline-code">width</code>, <code className="docs-inline-code">height</code>).</li>
              <li>Render: <code className="docs-inline-code">POST /api/templates/render-design</code> (raw design → PDF) or <code className="docs-inline-code">POST /api/export</code> (<code className="docs-inline-code">{'{ design, format, data? }'}</code>). For C#: <code className="docs-inline-code">POST /api/templates/csharp-code-to-pdf</code>.</li>
            </ol>

            <H3>Machine-readable resources</H3>
            <ul className="docs-steps">
              <li><code className="docs-inline-code">llms.txt</code> / <code className="docs-inline-code">llms-full.txt</code> (repo root) — capability map + every element with properties and examples for both surfaces.</li>
              <li><code className="docs-inline-code">docs/schema/design-export.schema.json</code> — JSON Schema for design validation.</li>
              <li><code className="docs-inline-code">docs/schema/openapi.json</code> — the full HTTP API.</li>
              <li><code className="docs-inline-code">ui-designer-v2/src/docs/elementCatalog.ts</code> — the element catalog (source of truth for all of the above).</li>
            </ul>
            <div className="docs-callout docs-callout--tip">
              <strong>MCP:</strong> a Model Context Protocol server (<code className="docs-inline-code">tools/PXA.Mcp</code>) exposes these as tools/resources — <code className="docs-inline-code">list_elements</code>, <code className="docs-inline-code">get_element_schema</code>, <code className="docs-inline-code">get_example</code>, <code className="docs-inline-code">validate_design</code>, <code className="docs-inline-code">render_preview</code> — so an agent can query and verify without scraping docs.
            </div>
          </section>

          {/* ── Spreadsheets ────────────────────────────────────────────── */}
          <section id="spreadsheets" className="docs-section">
            <H2 id="spreadsheets">Spreadsheets</H2>
            <p>Power Dox Automation includes an Excel-like <strong>Spreadsheet Editor</strong> at <code className="docs-inline-code">/spreadsheet</code> — a separate surface from the document designer, with live formulas, multiple sheets, cell styling, and <code className="docs-inline-code">.xlsx</code> round-trips.</p>

            <H3>Formulas</H3>
            <p>Type a value, or a formula starting with <code className="docs-inline-code">=</code> using standard A1 references — e.g. <code className="docs-inline-code">=SUM(A1:A10)</code>, <code className="docs-inline-code">=IF(B2&gt;0, B2*1.2, 0)</code>. Recalculation is powered by HyperFormula (~390 Excel functions, dependency-graph recalc); edits update dependents live. The formula bar shows the active cell's source; the grid shows the computed result.</p>

            <H3>Import &amp; Export</H3>
            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Format</th><th>Import</th><th>Export</th><th>Notes</th></tr></thead>
                <tbody>
                  <tr><td><code className="docs-inline-code">.xlsx</code></td><td style={{ textAlign: 'center' }}>✅</td><td style={{ textAlign: 'center' }}>✅</td><td>Full fidelity — formulas, types, number formats, styles, merges (ClosedXML).</td></tr>
                  <tr><td><code className="docs-inline-code">.csv</code></td><td style={{ textAlign: 'center' }}>✅</td><td style={{ textAlign: 'center' }}>✅</td><td>Plain values; export writes the computed values (RFC 4180 quoting).</td></tr>
                  <tr><td><code className="docs-inline-code">.json</code></td><td style={{ textAlign: 'center' }}>✅</td><td style={{ textAlign: 'center' }}>✅</td><td>The native workbook model — lossless, offline.</td></tr>
                </tbody>
              </table>
            </div>
            <p>The toolbar <strong>Import</strong> button accepts any of the three (dispatched by extension); <strong>Export ▾</strong> is a format menu. <code className="docs-inline-code">.ods</code> is not yet supported.</p>

            <H3>Backend API &amp; model</H3>
            <p>Round-trip + IO:</p>
            <ul className="docs-steps">
              <li><code className="docs-inline-code">POST /api/spreadsheet/export?format=xlsx|xls|csv|tsv&amp;recalculate=</code> — a <code className="docs-inline-code">SpreadsheetDto</code> workbook → <code className="docs-inline-code">.xlsx</code> (real A1 formulas), legacy <code className="docs-inline-code">.xls</code> (NPOI), or CSV/TSV.</li>
              <li><code className="docs-inline-code">POST /api/spreadsheet/import</code> — multipart <code className="docs-inline-code">.xlsx</code> / <code className="docs-inline-code">.xls</code> / <code className="docs-inline-code">.csv</code> / <code className="docs-inline-code">.tsv</code> → <code className="docs-inline-code">SpreadsheetDto</code> (dispatched by extension; preserves formulas + cached values, types, styles, merges).</li>
            </ul>
            <H3>Backend engine (server-side)</H3>
            <p>The backend is authoritative for headless/API callers — it no longer just stores formula strings:</p>
            <ul className="docs-steps">
              <li><code className="docs-inline-code">POST /api/spreadsheet/calculate</code> — evaluates formulas server-side (ClosedXML) and writes each computed value back into the model. Chained dependencies resolve; unsupported functions degrade to <code className="docs-inline-code">#ERROR</code>.</li>
              <li><code className="docs-inline-code">POST /api/spreadsheet/render?format=pdf|html|png|jpeg</code> — renders a sheet as a gridlined document (PDF via the PXA-compatible PDF renderer; html/png/jpeg via the standard exporters).</li>
              <li><code className="docs-inline-code">POST /api/spreadsheet/sort</code> and <code className="docs-inline-code">/find-replace</code> — sort a range by a key column; find/replace across text + formula cells.</li>
              <li><code className="docs-inline-code">POST /api/spreadsheet/from-data</code> and <code className="docs-inline-code">/fill</code> — build a workbook from JSON rows (DataTable), or fill a template's <code className="docs-inline-code">{'{{token}}'}</code> placeholders from a data object.</li>
            </ul>
            <p>Rich Excel features round-trip through <code className="docs-inline-code">.xlsx</code>: page setup, sheet protection, auto-filter, row/column grouping, cell comments + hyperlinks, conditional formatting, and data validation.</p>
            <p>The model (<code className="docs-inline-code">src/Canvas.Core/Contracts/SpreadsheetDto.cs</code>) is a workbook of sheets of sparse typed cells (<code className="docs-inline-code">number</code>/<code className="docs-inline-code">text</code>/<code className="docs-inline-code">boolean</code>/<code className="docs-inline-code">date</code>/<code className="docs-inline-code">formula</code>) with number formats, styles, merges, frozen panes, and defined names — exported/imported by <code className="docs-inline-code">Canvas.Infrastructure.Spreadsheet</code>.</p>

            <H3>Workbook JSON (canonical format)</H3>
            <p><strong>PXA Workbook JSON</strong> is the canonical, portable spreadsheet format. It is the camelCase serialization of <code className="docs-inline-code">SpreadsheetDto</code> that every spreadsheet endpoint accepts and that the editor's <strong>Export ▾ → JSON</strong> produces. It is versioned (<code className="docs-inline-code">schemaVersion</code>, currently <code className="docs-inline-code">"1.0"</code>) and carries an optional <code className="docs-inline-code">$schema</code> URL. A published JSON Schema lives at <code className="docs-inline-code">docs/schema/pxa-workbook.schema.json</code> for editor/tooling validation; <code className="docs-inline-code">docs/schema/canvas-workbook.schema.json</code> remains as the legacy compatibility alias.</p>
            <p>The format is <strong>lossless</strong>: saving and reloading JSON preserves everything the backend holds — typed values, formulas + cached values, number formats, styles, merges, frozen panes, defined names, and the full feature set (page setup, protection, auto-filter, row/column grouping, conditional formatting, data validation, cell comments + hyperlinks).</p>
            <div className="docs-callout docs-callout--tip">
              <strong>Note:</strong> a workbook (spreadsheet) contains multiple <em>sheets</em>. Live editing recalculates client-side (HyperFormula, GPLv3-or-commercial); for headless/API callers <code className="docs-inline-code">/calculate</code> recomputes server-side (ClosedXML). Charts and pivot tables are not yet supported.
            </div>
          </section>

          {/* ── Documentation Map ───────────────────────────────────────── */}
          <section id="documentation-map" className="docs-section">
            <H2 id="documentation-map">Documentation Map</H2>
            <p>The in-app docs cover daily product usage. For architecture, extension points, tests, and roadmap details, use the repository documentation below.</p>

            <div className="docs-elem-table-wrap">
              <table className="docs-elem-table">
                <thead><tr><th>Topic</th><th>Document</th><th>Use it for</th></tr></thead>
                <tbody>
                  {([
                    ['Architecture', 'ARCHITECTURE.md', 'Project boundaries, dependency direction, importer/migration layers'],
                    ['Project inventory', 'PROJECT_SUMMARY.md', 'Current project groups, endpoints, feature inventory, and test groups'],
                    ['Extension patterns', 'CONTRIBUTING_RENDERERS.md', 'Adding renderers, file importers, migration providers, report converters, and document operations'],
                    ['Testing', 'TESTING.md', 'Test project matrix, commands, CI expectations, and snapshot workflow'],
                    ['PDF engine API', 'Canvas/TECHNICAL_DOCUMENTATION.md', 'PXA-compatible PDF usage, options, layout helpers, encryption, forms, diagnostics'],
                    ['PDF encryption', 'checklists/Pdf-Encryption.md', 'RC4-128 status, AES follow-ups, and security handler notes'],
                    ['PDF provider gaps', 'checklists/CanvasPdf-Provider-Feature-Gaps.md', 'PXA-compatible PDF feature gaps compared with major PDF frameworks'],
                    ['Documentation audit', 'checklists/Documentation-Audit.md', 'Source-of-truth rules and follow-up documentation tasks'],
                    ['Multi-language UI', 'ui-designer-v2/MULTILANGUAGE.md', 'Language tabs, localized properties, RTL, and export behavior'],
                    ['Migration status', 'checklists/Code-Migrations.md', 'Provider progress and migration acceptance criteria'],
                  ] as [string,string,string][]).map(([topic, doc, use]) => (
                    <tr key={doc}>
                      <td>{topic}</td>
                      <td><code className="docs-inline-code">{doc}</code></td>
                      <td>{use}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

        </main>
      </div>
    </div>
  );
};

export default DocsPage;
