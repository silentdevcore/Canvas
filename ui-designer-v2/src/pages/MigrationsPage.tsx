import React, { useState, useEffect, useRef, useCallback } from 'react';
import { FiCode, FiCopy, FiDownload, FiPlay, FiRefreshCw } from 'react-icons/fi';
import Editor, { type OnMount } from '@monaco-editor/react';
import AppHeader from '@/components/Layout/AppHeader';

interface Framework {
  id: string;
  name: string;
  status: string;
  description: string;
}

interface Diagnostic {
  code: string;
  severity: string;
  message: string;
}

const API_BASE = '/api/migration';

const FRAMEWORKS_FALLBACK: Framework[] = [
  { id: 'Syncfusion', name: 'Syncfusion PDF',    status: 'full',    description: 'Full pattern-based conversion with top-left coordinate adapter. Covers document/page/text/line/rectangle/image/save.' },
  { id: 'iText7',     name: 'iText7',            status: 'full',    description: 'Roslyn-based conversion: PdfWriter+PdfDocument+Document → PdfDocument; Paragraph (with SetFontSize) → DrawTextFromTop; ShowTextAligned → DrawText; PdfCanvas line/rect/text; Close/SetMargins removed.' },
  { id: 'Apryse',     name: 'Apryse (PDFTron)',  status: 'full',    description: 'Roslyn-based conversion: PDFDoc → PdfDocument, PageCreate+PagePushBack → AddPage(), doc.Save() → document.Save().' },
  { id: 'Aspose',     name: 'Aspose.PDF',        status: 'full',    description: 'Roslyn-based conversion: Document → PdfDocument, Pages.Add → AddPage, TextFragment/TextBuilder with Position → DrawText/DrawTextFromTop.' },
  { id: 'DsPdf',      name: 'DsPdf (GrapeCity)', status: 'full',    description: 'Roslyn-based conversion: GcPdfDocument → PdfDocument; doc.NewPage() → AddPage(); page.Graphics.DrawString/DrawLine/DrawRectangle/FillRectangle → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save() preserved.' },
  { id: 'Foxit',      name: 'Foxit PDF SDK',     status: 'full',    description: 'Roslyn-based conversion: PDFDoc → PdfDocument; InsertPage/CreatePage → AddPage; Library.Initialize + GetGraphics/GenerateContent removed; graphics.DrawText/DrawLine/DrawRect/FillRect → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save/SaveAs → document.Save().' },
  { id: 'DevExpress', name: 'DevExpress PDF',    status: 'full',    description: 'Roslyn-based conversion: PdfDocumentProcessor → PdfDocument, RenderNewPage → AddPage, draw calls repositioned, SaveDocument → Save. Forms/signatures/report export produce warnings.' },
  { id: 'IronPdf',    name: 'IronPDF',           status: 'pilot',   description: 'Roslyn-based pilot: ChromePdfRenderer → PdfDocument + AddPage scaffold; SaveAs → document.Save(); HTML/URL/Razor rendering calls replaced with diagnostics for manual Canvas draw call migration.' },
  { id: 'Spire',      name: 'Spire.PDF',         status: 'pilot',   description: 'Roslyn-based pilot: PdfDocument/Pages.Add/page.Canvas.DrawString/DrawLine/DrawRectangle/SaveToFile converted; manual diagnostics for images, tables, forms, annotations, and security.' },
  { id: 'GemBox',     name: 'GemBox.Pdf',        status: 'pilot',   description: 'Roslyn-based pilot: PdfDocument/Pages.Add/Content.DrawText(text, PdfPoint)/Save converted; manual diagnostics for images, shapes, forms, annotations, encryption, and existing-PDF editing.' },
  { id: 'ActivePdf',  name: 'ActivePDF',         status: 'skeleton', description: 'API to be confirmed' },
  { id: 'Leadtools',  name: 'LEADTOOLS',         status: 'skeleton', description: 'Raster/OCR pipelines out of scope' },
  { id: 'PdfKitNet',  name: 'PDFKit.NET',        status: 'skeleton', description: 'API identity unconfirmed' },
];

const SYNCFUSION_EXAMPLE = `using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

using var document = new PdfDocument();
var page = document.Pages.Add();
page.Graphics.DrawString(
    "Hello from Syncfusion",
    new PdfStandardFont(PdfFontFamily.Helvetica, 14),
    PdfBrushes.Black,
    40, 40);
document.Save("output.pdf");`;

const ITEXT7_EXAMPLE = `using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

using var writer = new PdfWriter(outputPath);
using var pdf = new PdfDocument(writer);
using var document = new Document(pdf, PageSize.A4);
document.SetMargins(72, 72, 72, 72);

document.ShowTextAligned(new Paragraph("Invoice #2024").SetFontSize(18), 72, 760, TextAlignment.LEFT);
document.ShowTextAligned(new Paragraph("Thank you for your order."), 72, 720, TextAlignment.LEFT);
document.ShowTextAligned(new Paragraph("Total: $150.00"), 400, 100, TextAlignment.LEFT);

var canvas = new PdfCanvas(pdf.GetFirstPage());
canvas.MoveTo(72, 700).LineTo(524, 700).Stroke();
canvas.Rectangle(72, 600, 452, 60).Fill();
canvas.BeginText();
canvas.MoveText(80, 630);
canvas.ShowText("Item Details");
canvas.EndText();

document.Close();`;

const APRYSE_EXAMPLE = `using pdftron;
using pdftron.PDF;
using pdftron.SDF;

// Initialise the Apryse SDK (not required by Canvas.Pdf)
PDFNet.Initialize(licenseKey);

// Create a new PDF document with two pages
using var doc = new PDFDoc();

var page1 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page1);

var page2 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page2);

// Write text via ElementBuilder / ElementWriter
var builder = new ElementBuilder();
var writer  = new ElementWriter();

writer.Begin(page1);
var font = Font.Create(doc, Font.StandardType1Font.e_helvetica);
var element = builder.CreateTextBegin(font, 14);
writer.WriteElement(element);
element = builder.CreateTextRun("Hello from Apryse SDK");
element.SetTextMatrix(1, 0, 0, 1, 40, 740);
writer.WriteElement(element);
writer.WriteElement(builder.CreateTextEnd());
writer.End();

// Save with linearisation
doc.Save(outputPath, SDFDoc.SaveOptions.e_linearized);`;

const ASPOSE_EXAMPLE = `using Aspose.Pdf;
using Aspose.Pdf.Text;

var document = new Document();
var page = document.Pages.Add();

// Positioned heading via TextFragment + Position
var heading = new TextFragment("Invoice #1042");
heading.Position = new Position(40, 750);
heading.TextState.FontSize = 18;
page.Paragraphs.Add(heading);

// Simple paragraph text (no position — uses starter coordinates)
page.Paragraphs.Add(new TextFragment("Thank you for your order."));

// TextBuilder flow
var builder = new TextBuilder(page);
var note = new TextFragment("Payment due within 30 days.");
note.Position = new Position(40, 650);
builder.AppendText(note);

document.Save(outputPath);`;

const DSPDF_EXAMPLE = `using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Drawing;

var doc = new GcPdfDocument();
var page = doc.NewPage();
page.Graphics.DrawString("Invoice #2024", new TextFormat { FontSize = 18 }, new PointF(72, 72));
page.Graphics.DrawLine(pen, 72, 100, 540, 100);
page.Graphics.DrawString("Thank you for your order.", new TextFormat { FontSize = 12 }, new PointF(72, 130));
page.Graphics.DrawRectangle(pen, new RectangleF(72, 200, 468, 300));
page.Graphics.FillRectangle(brush, new RectangleF(72, 200, 468, 20));
doc.Save(outputPath);`;

const FOXIT_EXAMPLE = `using foxit;
using foxit.pdf;

Library.Initialize(licenseKey);
using var doc = new PDFDoc();
var page = doc.InsertPage(0, PageSize.e_SizeA4);
var graphics = page.GetGraphics();
graphics.DrawText("Invoice #2024", font18, 72, 72);
graphics.DrawLine(pen, 72, 100, 540, 100);
graphics.DrawText("Thank you for your order.", font12, 72, 130);
graphics.DrawRect(pen, 72, 200, 468, 300);
graphics.FillRect(brush, 72, 200, 468, 20);
page.GenerateContent();
doc.SaveAs(outputPath);`;

const DEVEXPRESS_EXAMPLE = `using DevExpress.Pdf;
using DevExpress.Drawing;

using var processor = new PdfDocumentProcessor();
processor.CreateEmptyDocument();
using var graphics = processor.CreateGraphics();

// Draw calls happen before RenderNewPage in DevExpress
graphics.DrawString("Invoice #2024", new DXFont("Arial", 18), DXBrushes.Black, 40, 750);
graphics.DrawLine(DXPens.Black, 40, 720, 555, 720);
graphics.DrawString("Thank you for your order.", new DXFont("Arial", 12), DXBrushes.Black, 40, 690);
graphics.DrawRectangle(DXPens.Black, 40, 620, 200, 60);

processor.RenderNewPage(PdfPaperSize.A4, graphics);
processor.SaveDocument(outputPath);`;

const GEMBOX_EXAMPLE = `using GemBox.Pdf;
using GemBox.Pdf.Content;

ComponentInfo.SetLicense("FREE-LIMITED-KEY");

var doc = new PdfDocument();
var page = doc.Pages.Add();

page.Content.DrawText("Invoice #2024", new PdfPoint(72, 72));
page.Content.DrawText("Thank you for your order.", new PdfPoint(72, 130));
page.Content.DrawText("Total: $150.00", new PdfPoint(72, 160));

doc.Save(outputPath);`;

const SPIRE_EXAMPLE = `using Spire.Pdf;
using Spire.Pdf.Graphics;

var doc = new PdfDocument();
var page = doc.Pages.Add();

// Heading
page.Canvas.DrawString(
    "Invoice #2024",
    new PdfFont(PdfFontFamily.Helvetica, 18),
    PdfBrushes.Black,
    new PointF(72, 72));

// Separator line
page.Canvas.DrawLine(pen, 72, 110, 540, 110);

// Body text
page.Canvas.DrawString(
    "Thank you for your order.",
    new PdfFont(PdfFontFamily.Helvetica, 12),
    PdfBrushes.Black,
    new PointF(72, 140));

// Table outline and header fill
page.Canvas.DrawRectangle(pen, 72, 200, 468, 200);
page.Canvas.DrawRectangle(pen, new RectangleF(72, 200, 468, 24));

doc.SaveToFile(outputPath);`;

const IRONPDF_EXAMPLE = `using IronPdf;

var renderer = new ChromePdfRenderer();
renderer.RenderingOptions.MarginTop = 20;
renderer.RenderingOptions.MarginBottom = 20;
var pdf = renderer.RenderHtmlAsPdf(@"
  <h1>Invoice #2024</h1>
  <p>Thank you for your order.</p>
  <p>Total: $150.00</p>
");
pdf.SaveAs(outputPath);`;

const EXAMPLES: Record<string, string> = {
  Syncfusion: SYNCFUSION_EXAMPLE,
  iText7: ITEXT7_EXAMPLE,
  Apryse: APRYSE_EXAMPLE,
  Aspose: ASPOSE_EXAMPLE,
  DsPdf: DSPDF_EXAMPLE,
  Foxit: FOXIT_EXAMPLE,
  DevExpress: DEVEXPRESS_EXAMPLE,
  IronPdf: IRONPDF_EXAMPLE,
  Spire: SPIRE_EXAMPLE,
  GemBox: GEMBOX_EXAMPLE,
};

interface ConversionSummary {
  convertedCount: number;
  warningCount: number;
  errorCount: number;
  totalDiagnostics: number;
}

const MigrationsPage: React.FC = () => {
  const [frameworks, setFrameworks] = useState<Framework[]>(FRAMEWORKS_FALLBACK);
  const [selectedId, setSelectedId] = useState('Syncfusion');
  const [sourceCode, setSourceCode] = useState('');
  const [canvasCode, setCanvasCode] = useState('');
  const [diagnostics, setDiagnostics] = useState<Diagnostic[]>([]);
  const [summary, setSummary] = useState<ConversionSummary | null>(null);
  const [hasConverted, setHasConverted] = useState(false);
  const [diagOpen, setDiagOpen] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [converting, setConverting] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [copyLabel, setCopyLabel] = useState('Copy');
  const [error, setError] = useState<string | null>(null);
  const prevPdfUrl = useRef<string | null>(null);
  const handleConvertRef = useRef<() => Promise<void>>(async () => {});

  useEffect(() => {
    fetch(`${API_BASE}/frameworks`)
      .then(r => r.json())
      .then((data: Framework[]) => setFrameworks(data))
      .catch(() => { /* use fallback */ });
    return () => { if (prevPdfUrl.current) URL.revokeObjectURL(prevPdfUrl.current); };
  }, []);

  const current = frameworks.find(f => f.id === selectedId);

  const handleFrameworkChange = (id: string) => {
    setSelectedId(id);
    setSourceCode('');
    setCanvasCode('');
    setDiagnostics([]);
    setSummary(null);
    setHasConverted(false);
    setDiagOpen(false);
    setPdfUrl(null);
    setError(null);
  };

  const applyConvertResult = (data: { canvasCode?: string; diagnostics?: Diagnostic[]; summary?: ConversionSummary }) => {
    const diags = data.diagnostics ?? [];
    setCanvasCode(data.canvasCode ?? '');
    setDiagnostics(diags);
    setSummary(data.summary ?? null);
    setHasConverted(true);
    setDiagOpen(diags.some(d => d.severity === 'Warning'));
  };

  const handleConvert = async () => {
    if (!sourceCode.trim()) return;
    setConverting(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/convert`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
      applyConvertResult(await res.json());
    } catch (e: any) {
      setError(e.message ?? 'Conversion failed — is the Canvas.WebApi backend running on port 5086?');
    } finally {
      setConverting(false);
    }
  };

  const handlePreview = async () => {
    if (!sourceCode.trim()) return;
    setPreviewing(true);
    setError(null);
    try {
      // Sync the output panel first so the user sees the converted code
      const convertRes = await fetch(`${API_BASE}/convert`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (convertRes.ok) applyConvertResult(await convertRes.json());

      const res = await fetch(`${API_BASE}/preview`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ framework: selectedId, sourceCode }),
      });
      if (!res.ok) { const e = await res.json(); throw new Error(e.error ?? `HTTP ${res.status}`); }
      const blob = await res.blob();
      if (prevPdfUrl.current) URL.revokeObjectURL(prevPdfUrl.current);
      const url = URL.createObjectURL(blob);
      prevPdfUrl.current = url;
      setPdfUrl(url);
    } catch (e: any) {
      setError(e.message ?? 'Preview failed — is the Canvas.WebApi backend running on port 5086?');
    } finally {
      setPreviewing(false);
    }
  };

  const handleCopy = async () => {
    if (!canvasCode) return;
    await navigator.clipboard.writeText(canvasCode);
    setCopyLabel('Copied!');
    setTimeout(() => setCopyLabel('Copy'), 2000);
  };

  const handleDownload = () => {
    if (!canvasCode) return;
    const blob = new Blob([canvasCode], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'migration.cs';
    a.click();
    URL.revokeObjectURL(url);
  };

  const warnCount = diagnostics.filter(d => d.severity === 'Warning').length;

  // Keep ref current so the Monaco command closure never goes stale
  handleConvertRef.current = handleConvert;

  const handleSourceMount: OnMount = useCallback((editor, monaco) => {
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => {
      void handleConvertRef.current();
    });
  }, []);

  return (
    <div className="mgr-page">
      <AppHeader activePage="migrations" />

      <main className="mgr-main">
        {/* Page heading */}
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            <FiCode className="mgr-heading-icon" />
            <div>
              <h1>Code Migrations</h1>
              <p>Paste code from another PDF library, convert it to Canvas.Pdf, and preview the result instantly.</p>
            </div>
          </div>
        </div>

        {/* Framework selector */}
        <div className="mgr-framework-bar">
          <label htmlFor="mgr-fw-select">Source framework</label>
          <select
            id="mgr-fw-select"
            value={selectedId}
            onChange={e => handleFrameworkChange(e.target.value)}
          >
            {frameworks.map(f => (
              <option key={f.id} value={f.id}>
                {f.name}{f.status === 'skeleton' ? ' (skeleton)' : f.status === 'pilot' ? ' (pilot)' : ''}
              </option>
            ))}
          </select>
          {current && (
            <span className="mgr-framework-desc">{current.description}</span>
          )}
          {current?.status === 'full' && (
            <span className="mgr-badge mgr-badge-full">Full</span>
          )}
          {current?.status === 'pilot' && (
            <span className="mgr-badge mgr-badge-pilot">Pilot</span>
          )}
          {current?.status === 'skeleton' && (
            <span className="mgr-badge mgr-badge-skeleton">Skeleton</span>
          )}
          {EXAMPLES[selectedId] && (
            <button
              className="mgr-example-btn"
              onClick={() => setSourceCode(EXAMPLES[selectedId])}
              title={`Load ${current?.name ?? selectedId} example`}
            >
              Load example
            </button>
          )}
        </div>

        {error && (
          <div className="mgr-error" role="alert">{error}</div>
        )}

        {/* Split: source | canvas */}
        <div className="mgr-split">
          <div className="mgr-pane">
            <div className="mgr-pane-header">
              <span>Source Code — {current?.name ?? selectedId}</span>
            </div>
            <div className="mgr-editor-wrapper">
              <Editor
                language="csharp"
                value={sourceCode}
                onChange={v => setSourceCode(v ?? '')}
                onMount={handleSourceMount}
                options={{
                  minimap: { enabled: false },
                  scrollBeyondLastLine: false,
                  fontSize: 13,
                  lineNumbers: 'on',
                  wordWrap: 'on',
                  renderWhitespace: 'none',
                  padding: { top: 8, bottom: 8 },
                }}
                height="100%"
              />
            </div>
            <div className="mgr-pane-footer">
              <button
                className="mgr-btn mgr-btn-primary"
                onClick={handleConvert}
                disabled={converting || !sourceCode.trim()}
              >
                {converting
                  ? <><FiRefreshCw className="mgr-spin" /> Converting…</>
                  : <>Convert <span className="mgr-arrow">→</span></>}
              </button>
            </div>
          </div>

          <div className="mgr-pane">
            <div className="mgr-pane-header">
              <span>Canvas.Pdf Code</span>
              <div className="mgr-pane-header-actions">
                <button className="mgr-icon-btn" onClick={handleCopy} disabled={!canvasCode} title="Copy to clipboard">
                  <FiCopy /> {copyLabel}
                </button>
                <button className="mgr-icon-btn" onClick={handleDownload} disabled={!canvasCode} title="Download as .cs file">
                  <FiDownload /> Download .cs
                </button>
              </div>
            </div>
            <div className="mgr-editor-wrapper">
              <Editor
                language="csharp"
                value={canvasCode}
                options={{
                  readOnly: true,
                  minimap: { enabled: false },
                  scrollBeyondLastLine: false,
                  fontSize: 13,
                  lineNumbers: 'on',
                  wordWrap: 'on',
                  renderWhitespace: 'none',
                  padding: { top: 8, bottom: 8 },
                }}
                height="100%"
              />
            </div>
            <div className="mgr-pane-footer mgr-pane-footer-right">
              <button
                className="mgr-btn mgr-btn-secondary"
                onClick={handlePreview}
                disabled={previewing || !sourceCode.trim()}
              >
                {previewing
                  ? <><FiRefreshCw className="mgr-spin" /> Generating…</>
                  : <><FiPlay /> Generate Preview</>}
              </button>
            </div>
          </div>
        </div>

        {/* Diagnostics — always visible after a conversion */}
        {hasConverted && (
          <div className="mgr-diagnostics">
            <div className="mgr-diag-summary">
              <strong>Diagnostics</strong>
              {summary && summary.convertedCount > 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-converted">✓ {summary.convertedCount} converted</span>
              )}
              {warnCount > 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-warn">⚠ {warnCount} warning{warnCount > 1 ? 's' : ''}</span>
              )}
              {diagnostics.length === 0 && (
                <span className="mgr-diag-chip mgr-diag-chip-ok">No issues</span>
              )}
              {diagnostics.length > 0 && (
                <button
                  className="mgr-diag-toggle"
                  onClick={() => setDiagOpen(o => !o)}
                  aria-expanded={diagOpen}
                >
                  {diagOpen ? '▲ Hide' : '▼ Show'} details
                </button>
              )}
            </div>
            {diagOpen && (
              <ul className="mgr-diag-list">
                {diagnostics.map((d, i) => (
                  <li key={i} className={`mgr-diag-item mgr-diag-${d.severity.toLowerCase()}`}>
                    <code className="mgr-diag-code">{d.code}</code>
                    <span>{d.message}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* PDF Preview */}
        <div className="mgr-preview">
          <div className="mgr-preview-header">
            <span>PDF Preview</span>
          </div>
          {pdfUrl
            ? <iframe className="mgr-pdf-frame" src={pdfUrl} title="PDF Preview" />
            : (
              <div className="mgr-pdf-empty">
                <FiPlay size={32} />
                <p>Click <strong>Generate Preview</strong> to render the converted PDF</p>
              </div>
            )
          }
        </div>
      </main>
    </div>
  );
};

export default MigrationsPage;
