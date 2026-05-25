import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FiCheck, FiCode, FiCopy, FiDownload, FiX } from 'react-icons/fi';
import type { Page, PageSettings, SimpleElement, LocalizedProperty } from '@/types';
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
  currentPreviewLanguage?: string;
}

type Tab = 'json' | 'csharp';
type ExportState = 'idle' | 'loading' | 'error';

function resolvePropertyMap(
  props: LocalizedProperty[],
  targetLang: string,
  sysLang: string,
): Record<string, string> {
  const map: Record<string, string> = {};
  for (const p of props) {
    if (p.scope === 'own') {
      if (p.ownerLanguage === targetLang) {
        map[p.key] = p.localizedValues[p.ownerLanguage] ?? '';
      }
    } else {
      map[p.key] = p.localizedValues[targetLang]
        ?? p.localizedValues[sysLang]
        ?? '';
    }
  }
  return map;
}

function applyProps(content: string | undefined, map: Record<string, string>): string {
  if (!content || !content.includes('{{')) return content ?? '';
  return content.replace(/\{\{(\w+)\}\}/g, (_, key) => map[key] ?? `{{${key}}}`);
}

function resolveElements(elements: SimpleElement[], map: Record<string, string>): SimpleElement[] {
  if (Object.keys(map).length === 0) return elements;
  return elements.map(el => ({
    ...el,
    content: applyProps(el.content, map),
    htmlContent: applyProps(el.htmlContent, map),
  }));
}

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

  const sysLang = navigator.language.split('-')[0];
  const targetLang = currentPreviewLanguage || sysLang;
  const propMap = useMemo(
    () => resolvePropertyMap(pageSettings.localizedProperties ?? [], targetLang, sysLang),
    [pageSettings.localizedProperties, targetLang, sysLang],
  );

  const jsonCode = useMemo(
    () => generateJSONExport(template, pages, sharedElements, pageSettings, targetLang),
    [template, pages, sharedElements, pageSettings, targetLang],
  );

  const csharpCode = useMemo(() => {
    const resolvedPages = pages.map(p => ({
      id: p.id,
      elements: resolveElements(p.elements, propMap),
    }));
    const resolvedShared = resolveElements(sharedElements, propMap);
    return jsonToCode({
      id: template.id,
      name: template.name,
      pages: resolvedPages,
      sharedElements: resolvedShared,
      pageSettings: { width: pageSettings.width, height: pageSettings.height },
    });
  }, [template, pages, sharedElements, pageSettings, propMap]);

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
      const payload = generateJSONExport(template, pages, sharedElements, pageSettings, targetLang);
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
      a.download = `${template.name.toLowerCase().replace(/\s+/g, '-')}-${targetLang}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
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
            ? `Full template data as JSON for language: ${langLabel}`
            : `C# code using Canvas.Pdf — placeholders resolved for ${langLabel}.`}
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
