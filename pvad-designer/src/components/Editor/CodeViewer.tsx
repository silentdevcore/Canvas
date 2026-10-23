import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FiCheck, FiCode, FiCopy, FiDownload, FiX } from 'react-icons/fi';
import type { Page, PageSettings, SimpleElement } from '@/types';
import type { Template } from '../../store';
import { generateJSONExport } from '../../services/CodeGenerator';
import { convertCodeDraft, type CodeDiagnostic } from '@/services/codeWorkspaceApi';
import ExportService from '@/services/ExportService';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  template: Template;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings: PageSettings;
  currentPreviewLanguage?: string;
}

type Tab = 'json' | 'csharp';
type ExportState = 'idle' | 'loading' | 'error';
type CSharpResult = {
  sourceJson: string;
  code: string;
  diagnostics: CodeDiagnostic[];
};

const CodeViewer: React.FC<Props> = ({
  isOpen,
  onClose,
  template,
  pages,
  sharedElements,
  pageSettings,
  currentPreviewLanguage,
}) => {
  const [activeTab, setActiveTab] = useState<Tab>('json');
  const [copied, setCopied] = useState(false);
  const [exportState, setExportState] = useState<ExportState>('idle');
  const [exportError, setExportError] = useState<string | null>(null);
  const [csharpResult, setCsharpResult] = useState<CSharpResult | null>(null);
  const [csharpLoading, setCsharpLoading] = useState(false);
  const [csharpError, setCsharpError] = useState<string | null>(null);

  const sysLang = navigator.language.split('-')[0];
  const targetLang = currentPreviewLanguage || sysLang;

  const jsonCode = useMemo(
    () => generateJSONExport(template, pages, sharedElements, pageSettings, targetLang),
    [template, pages, sharedElements, pageSettings, targetLang],
  );

  const csharpCode = csharpResult?.sourceJson === jsonCode ? csharpResult.code : '';
  const code = activeTab === 'json' ? jsonCode : csharpCode;

  useEffect(() => {
    if (!isOpen || activeTab !== 'csharp') return;
    if (csharpResult?.sourceJson === jsonCode) return;
    const templateId = template.persistence?.id;
    if (!templateId) {
      setCsharpResult(null);
      setCsharpError('Save the document before generating exact C# PDF code.');
      return;
    }

    let cancelled = false;
    setCsharpLoading(true);
    setCsharpError(null);
    void convertCodeDraft(templateId, 'json', 'csharpPdf', jsonCode)
      .then(result => {
        if (cancelled) return;
        const errors = result.diagnostics.filter(item => item.severity === 'error');
        if (errors.length > 0 || !result.generatedSource) {
          setCsharpResult(null);
          setCsharpError(errors.map(item => `${item.code}: ${item.message}`).join('\n') || 'C# code could not be generated.');
          return;
        }
        setCsharpResult({
          sourceJson: jsonCode,
          code: result.generatedSource,
          diagnostics: result.diagnostics,
        });
      })
      .catch(error => {
        if (!cancelled) {
          setCsharpResult(null);
          setCsharpError(error instanceof Error ? error.message : 'C# code could not be generated.');
        }
      })
      .finally(() => {
        if (!cancelled) setCsharpLoading(false);
      });
    return () => { cancelled = true; };
  }, [activeTab, csharpResult?.sourceJson, isOpen, jsonCode, template.persistence?.id]);

  const handleCopy = useCallback(async () => {
    if (!code) return;
    await navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [code]);

  const handleExportPdf = useCallback(async () => {
    setExportState('loading');
    setExportError(null);
    try {
      await ExportService.exportToPDF(
        template,
        pages,
        sharedElements,
        { ...pageSettings, targetLanguage: targetLang },
      );
      setExportState('idle');
    } catch (err) {
      setExportError(err instanceof Error ? err.message : 'Export failed');
      setExportState('error');
    }
  }, [template, pages, sharedElements, pageSettings, targetLang]);

  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const langLabel = targetLang.toUpperCase();

  return (
    <>
      <div className="code-panel-backdrop" onClick={onClose} />
      <aside className="code-panel">
        <header className="code-panel-header">
          <div className="code-panel-title">
            <FiCode />
            <span>Export code</span>
            {(pageSettings.activeLanguages?.length ?? 0) > 0 && (
              <span style={{ fontSize: 11, padding: '2px 6px', borderRadius: 4, background: '#ede9fe', color: '#4c1d95', marginLeft: 6 }}>
                {langLabel}
              </span>
            )}
          </div>
          <div className="code-panel-header-actions">
            <button
              className={`code-panel-export-btn${exportState === 'loading' ? ' is-loading' : ''}${exportState === 'error' ? ' is-error' : ''}`}
              onClick={handleExportPdf}
              disabled={exportState === 'loading'}
              title="Render PDF via backend (localhost:5086)"
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
            Backend error: {exportError}. Make sure the PVADE backend is running on port 5086.
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
            ? 'Complete canonical DesignExportDto used by Designer and backend exports.'
            : 'Exact PVADE C# PDF builder code generated from the canonical JSON by the backend.'}
        </div>

        <div className="code-panel-body">
          <button className="code-panel-copy" onClick={handleCopy} disabled={!code || csharpLoading} title="Copy to clipboard">
            {copied ? <FiCheck /> : <FiCopy />}
            {copied ? 'Copied!' : 'Copy'}
          </button>
          {activeTab === 'csharp' && csharpLoading ? (
            <div className="code-panel-error">Generating exact C# code…</div>
          ) : activeTab === 'csharp' && csharpError ? (
            <div className="code-panel-error" style={{ whiteSpace: 'pre-wrap' }}>{csharpError}</div>
          ) : (
            <>
              {activeTab === 'csharp' && csharpResult && csharpResult.diagnostics.length > 0 && (
                <div className="code-panel-error" style={{ whiteSpace: 'pre-wrap' }}>
                  {csharpResult.diagnostics.map(item => `${item.code}: ${item.message}`).join('\n')}
                </div>
              )}
              <pre className="code-panel-pre"><code>{code}</code></pre>
            </>
          )}
        </div>
      </aside>
    </>
  );
};

export default CodeViewer;
