import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FiCheck, FiCode, FiCopy, FiDownload, FiX } from 'react-icons/fi';
import type { Page, PageSettings, SimpleElement } from '@/types';
import type { Template } from '../../store';
import { generateJSONExport } from '../../services/CodeGenerator';
import { jsonToCode } from '@/utils/jsonToCode';

const BACKEND_URL = 'http://localhost:5086';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  template: Template;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings: PageSettings;
}

type Tab = 'json' | 'csharp';
type ExportState = 'idle' | 'loading' | 'error';

const CodeViewer: React.FC<Props> = ({ isOpen, onClose, template, pages, sharedElements, pageSettings }) => {
  const [activeTab, setActiveTab] = useState<Tab>('json');
  const [copied, setCopied] = useState(false);
  const [exportState, setExportState] = useState<ExportState>('idle');
  const [exportError, setExportError] = useState<string | null>(null);

  const jsonCode = useMemo(
    () => generateJSONExport(template, pages, sharedElements, pageSettings),
    [template, pages, sharedElements, pageSettings],
  );

  const csharpCode = useMemo(
    () => jsonToCode({
      id: template.id,
      name: template.name,
      pages: pages.map(p => ({ id: p.id, elements: p.elements })),
      sharedElements,
      pageSettings: { width: pageSettings.width, height: pageSettings.height },
    }),
    [template, pages, sharedElements, pageSettings],
  );

  const code = activeTab === 'json' ? jsonCode : csharpCode;

  const handleCopy = useCallback(async () => {
    await navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [code]);

  const handleExportPdf = useCallback(async () => {
    setExportState('loading');
    setExportError(null);
    try {
      const payload = generateJSONExport(template, pages, sharedElements, pageSettings);
      const res = await fetch(`${BACKEND_URL}/api/templates/render-design`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: payload,
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }));
        throw new Error(err.error ?? res.statusText);
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${template.name.toLowerCase().replace(/\s+/g, '-')}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
      setExportState('idle');
    } catch (err) {
      setExportError(err instanceof Error ? err.message : 'Export failed');
      setExportState('error');
    }
  }, [template, pages, sharedElements, pageSettings]);

  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <>
      <div className="code-panel-backdrop" onClick={onClose} />
      <aside className="code-panel">
        <header className="code-panel-header">
          <div className="code-panel-title">
            <FiCode />
            <span>Export code</span>
          </div>
          <div className="code-panel-header-actions">
            <button
              className={`code-panel-export-btn${exportState === 'loading' ? ' is-loading' : ''}${exportState === 'error' ? ' is-error' : ''}`}
              onClick={handleExportPdf}
              disabled={exportState === 'loading'}
              title="Render PDF via backend (localhost:5241)"
            >
              <FiDownload />
              {exportState === 'loading' ? 'Generating…' : exportState === 'error' ? 'Retry' : 'Export PDF'}
            </button>
            <button className="editor-icon-button" onClick={onClose} aria-label="Close code panel">
              <FiX />
            </button>
          </div>
        </header>

        {exportState === 'error' && exportError && (
          <div className="code-panel-error">
            Backend error: {exportError}. Make sure Canvas.WebApi is running on port 5086.
          </div>
        )}

        <div className="code-panel-tabs">
          <button
            className={`code-panel-tab${activeTab === 'json' ? ' is-active' : ''}`}
            onClick={() => setActiveTab('json')}
          >
            JSON
          </button>
          <button
            className={`code-panel-tab${activeTab === 'csharp' ? ' is-active' : ''}`}
            onClick={() => setActiveTab('csharp')}
          >
            C# Code
          </button>
        </div>

        <div className="code-panel-description">
          {activeTab === 'json'
            ? 'Full template data as JSON — sent to the backend when you click Export PDF.'
            : 'C# code using Canvas.Pdf — paste into the Code Editor to run and preview.'}
        </div>

        <div className="code-panel-body">
          <button className="code-panel-copy" onClick={handleCopy} title="Copy to clipboard">
            {copied ? <FiCheck /> : <FiCopy />}
            {copied ? 'Copied!' : 'Copy'}
          </button>
          <pre className="code-panel-pre"><code>{code}</code></pre>
        </div>
      </aside>
    </>
  );
};

export default CodeViewer;
