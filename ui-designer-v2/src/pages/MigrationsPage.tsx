import React, { useState, useEffect, useRef } from 'react';
import { FiCode, FiCopy, FiDownload, FiPlay, FiRefreshCw } from 'react-icons/fi';
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
  { id: 'Syncfusion', name: 'Syncfusion PDF',    status: 'full',     description: 'Full pattern-based conversion with top-left coordinate adapter' },
  { id: 'Apryse',     name: 'Apryse (PDFTron)',  status: 'skeleton', description: 'new PDFDoc() → new PdfDocument(); PageCreate+PagePushBack → AddPage()' },
  { id: 'Aspose',     name: 'Aspose.PDF',         status: 'skeleton', description: 'new Document() → new PdfDocument(); Paragraphs.Add → DrawTextFromTop()' },
  { id: 'DsPdf',      name: 'DsPdf (GrapeCity)',  status: 'skeleton', description: 'new GcPdfDocument() → new PdfDocument(); Graphics.DrawString → DrawTextFromTop()' },
  { id: 'Spire',      name: 'Spire.PDF',          status: 'skeleton', description: 'page.Canvas.DrawString → DrawTextFromTop(); SaveToFile → Save()' },
  { id: 'GemBox',     name: 'GemBox.Pdf',         status: 'skeleton', description: 'document.Pages.Add() → document.AddPage()' },
  { id: 'iText7',     name: 'iText7',             status: 'pilot',    description: 'Roslyn-based pilot: PdfWriter+PdfDocument+Document → PdfDocument; Paragraph, PdfCanvas line/rect/text, ShowTextAligned, PageSize presets' },
  { id: 'IronPdf',    name: 'IronPDF',            status: 'skeleton', description: 'HTML-to-PDF — manual rewrite required' },
  { id: 'ActivePdf',  name: 'ActivePDF',          status: 'skeleton', description: 'API to be confirmed' },
  { id: 'Leadtools',  name: 'LEADTOOLS',          status: 'skeleton', description: 'Raster/OCR pipelines out of scope' },
  { id: 'PdfKitNet',  name: 'PDFKit.NET',         status: 'skeleton', description: 'API identity unconfirmed' },
  { id: 'Foxit',      name: 'Foxit PDF SDK',      status: 'skeleton', description: 'new PDFDoc() → new PdfDocument()' },
  { id: 'DevExpress', name: 'DevExpress PDF',     status: 'skeleton', description: 'Processor vs. generator APIs TBD' },
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

const ITEXT7_EXAMPLE = `using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

using var writer = new PdfWriter("output.pdf");
using var pdf = new PdfDocument(writer);
using var document = new Document(pdf, PageSize.A4);
document.Add(new Paragraph("Hello from iText7"));
document.ShowTextAligned(new Paragraph("Positioned text"), 40, 700, TextAlignment.LEFT);

var canvas = new PdfCanvas(pdf.GetFirstPage());
canvas.MoveTo(40, 650).LineTo(555, 650).Stroke();
canvas.Rectangle(40, 500, 515, 100).Stroke();
canvas.BeginText().MoveText(40, 580).ShowText("Canvas text").EndText();`;

const EXAMPLES: Record<string, string> = {
  Syncfusion: SYNCFUSION_EXAMPLE,
  iText7: ITEXT7_EXAMPLE,
};

const MigrationsPage: React.FC = () => {
  const [frameworks, setFrameworks] = useState<Framework[]>(FRAMEWORKS_FALLBACK);
  const [selectedId, setSelectedId] = useState('Syncfusion');
  const [sourceCode, setSourceCode] = useState('');
  const [canvasCode, setCanvasCode] = useState('');
  const [diagnostics, setDiagnostics] = useState<Diagnostic[]>([]);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [converting, setConverting] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [copyLabel, setCopyLabel] = useState('Copy');
  const [error, setError] = useState<string | null>(null);
  const prevPdfUrl = useRef<string | null>(null);

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
    setCanvasCode('');
    setDiagnostics([]);
    setPdfUrl(null);
    setError(null);
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
      const data = await res.json();
      setCanvasCode(data.canvasCode ?? '');
      setDiagnostics(data.diagnostics ?? []);
    } catch (e: any) {
      setError(e.message ?? 'Conversion failed — is the Canvas.WebApi backend running on port 5000?');
    } finally {
      setConverting(false);
    }
  };

  const handlePreview = async () => {
    if (!sourceCode.trim()) return;
    setPreviewing(true);
    setError(null);
    try {
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
      setError(e.message ?? 'Preview failed — is the Canvas.WebApi backend running on port 5000?');
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

  const infoCount = diagnostics.filter(d => d.severity === 'Info').length;
  const warnCount = diagnostics.filter(d => d.severity === 'Warning').length;

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
            <textarea
              className="mgr-source"
              value={sourceCode}
              onChange={e => setSourceCode(e.target.value)}
              placeholder={`Paste your ${current?.name ?? selectedId} code here…`}
              spellCheck={false}
            />
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
            <pre className="mgr-output">
              {canvasCode
                ? canvasCode
                : <span className="mgr-placeholder">Converted Canvas.Pdf C# code will appear here</span>
              }
            </pre>
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

        {/* Diagnostics */}
        {diagnostics.length > 0 && (
          <div className="mgr-diagnostics">
            <div className="mgr-diag-summary">
              <strong>Diagnostics</strong>
              {infoCount > 0 && <span className="mgr-diag-chip mgr-diag-chip-info">● {infoCount} info</span>}
              {warnCount > 0 && <span className="mgr-diag-chip mgr-diag-chip-warn">⚠ {warnCount} warning{warnCount > 1 ? 's' : ''}</span>}
            </div>
            <ul className="mgr-diag-list">
              {diagnostics.map((d, i) => (
                <li key={i} className={`mgr-diag-item mgr-diag-${d.severity.toLowerCase()}`}>
                  <code className="mgr-diag-code">{d.code}</code>
                  <span>{d.message}</span>
                </li>
              ))}
            </ul>
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
