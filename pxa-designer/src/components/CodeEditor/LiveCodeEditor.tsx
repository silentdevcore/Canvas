import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import JsonEditorPane from './JsonEditorPane';
import CodePreviewPane, { type ParsedDesign, type ValidationResult } from './CodePreviewPane';
import { STARTER_TEMPLATES, CSHARP_CODE_STARTER } from './starterTemplates';
import { ExportService } from '@/services/ExportService';
import { jsonToCSharp } from '@/utils/jsonToCSharp';
import { jsonToCode } from '@/utils/jsonToCode';

const STORAGE_KEY = 'pxa-code-editor-draft-v2';
const STORAGE_LANG_KEY = 'pxa-code-editor-lang-v2';
const LEGACY_STORAGE_KEY = 'canvas-code-editor-draft-v2';
const LEGACY_STORAGE_LANG_KEY = 'canvas-code-editor-lang-v2';
const DEFAULT_JSON = JSON.stringify(STARTER_TEMPLATES.hello, null, 2);
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5086/api';

export type EditorLanguage = 'json' | 'csharp-code' | 'csharp-dto';

interface Props {
  onBack: () => void;
}

function parseAndValidate(raw: string, t: TFunction): { validation: ValidationResult; parsed: ParsedDesign | null } {
  if (raw.trim() === '') return { validation: { valid: false, errors: [] }, parsed: null };

  let obj: any;
  try { obj = JSON.parse(raw); }
  catch (e: any) {
    return { validation: { valid: false, errors: [t('errors.syntaxError', { message: e.message })] }, parsed: null };
  }

  const errors: string[] = [];
  if (!obj.pages || !Array.isArray(obj.pages)) {
    errors.push(t('errors.pagesMustBeArray'));
  } else if (obj.pages.length === 0) {
    errors.push(t('errors.pagesMustHaveOne'));
  } else {
    obj.pages.forEach((p: any, i: number) => {
      if (typeof p.id !== 'string' || !p.id) errors.push(t('errors.pageIdRequired', { i }));
      if (!Array.isArray(p.elements))        errors.push(t('errors.pageElementsMustBeArray', { i }));
      else p.elements.forEach((el: any, j: number) => {
        if (!el.id)                       errors.push(t('errors.elementIdRequired', { i, j }));
        if (!el.type)                     errors.push(t('errors.elementTypeRequired', { i, j }));
        if (typeof el.x !== 'number')     errors.push(t('errors.elementXMustBeNumber', { i, j }));
        if (typeof el.y !== 'number')     errors.push(t('errors.elementYMustBeNumber', { i, j }));
        if (typeof el.width !== 'number') errors.push(t('errors.elementWidthMustBeNumber', { i, j }));
        if (typeof el.height !== 'number') errors.push(t('errors.elementHeightMustBeNumber', { i, j }));
      });
    });
  }
  const ps = obj.pageSettings;
  if (ps) {
    if (ps.width !== undefined && typeof ps.width !== 'number')  errors.push(t('errors.pageSettingsWidthMustBeNumber'));
    if (ps.height !== undefined && typeof ps.height !== 'number') errors.push(t('errors.pageSettingsHeightMustBeNumber'));
  }
  return {
    validation: { valid: errors.length === 0, errors },
    parsed: errors.length === 0 ? (obj as ParsedDesign) : null,
  };
}

export default function LiveCodeEditor({ onBack }: Props) {
  const { t } = useTranslation('codeEditor');
  const [language, setLanguage] = useState<EditorLanguage>(() => {
    try {
      const stored = localStorage.getItem(STORAGE_LANG_KEY) ?? localStorage.getItem(LEGACY_STORAGE_LANG_KEY);
      return (stored as EditorLanguage) || 'json';
    }
    catch { return 'json'; }
  });

  const [raw, setRaw] = useState<string>(() => {
    try { return localStorage.getItem(STORAGE_KEY) ?? localStorage.getItem(LEGACY_STORAGE_KEY) ?? DEFAULT_JSON; }
    catch { return DEFAULT_JSON; }
  });

  // JSON / C# DTO preview state
  const [validation, setValidation] = useState<ValidationResult>({ valid: true, errors: [] });
  const [parsed, setParsed] = useState<ParsedDesign | null>(null);

  // C# Code PDF preview state
  const [pdfBlobUrl, setPdfBlobUrl] = useState<string | null>(null);

  // Shared operation state
  const [isExporting, setIsExporting] = useState(false);
  const [isConverting, setIsConverting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [convertError, setConvertError] = useState<string | null>(null);

  // Forces Monaco editor remount after async conversion so it shows new content
  const [editorKey, setEditorKey] = useState(0);

  // Splitter
  const [splitPct, setSplitPct] = useState(50);
  const dragging = useRef(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Revoke old blob URL when a new one is set
  const setPdfUrl = useCallback((url: string | null) => {
    setPdfBlobUrl(prev => {
      if (prev) URL.revokeObjectURL(prev);
      return url;
    });
  }, []);

  useEffect(() => {
    if (language === 'json') {
      const { validation: v, parsed: p } = parseAndValidate(raw, t);
      setValidation(v);
      setParsed(p);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Cleanup blob URL on unmount
  useEffect(() => () => { if (pdfBlobUrl) URL.revokeObjectURL(pdfBlobUrl); }, [pdfBlobUrl]);

  const handleChange = useCallback((value: string) => {
    setRaw(value);
    try { localStorage.setItem(STORAGE_KEY, value); } catch {}
    setExportError(null);
    setConvertError(null);
    if (language === 'json') {
      const { validation: v, parsed: p } = parseAndValidate(value, t);
      setValidation(v);
      setParsed(p);
    }
  }, [language, t]);

  // ── Language switching ────────────────────────────────────────

  const switchTo = useCallback((lang: EditorLanguage) => {
    if (lang === language) return;

    const finish = (content: string, saveKey = STORAGE_KEY) => {
      setRaw(content);
      try { localStorage.setItem(saveKey, content); } catch {}
      setEditorKey(k => k + 1);
    };

    const finishJson = (content: string) => {
      finish(content);
      const { validation: v, parsed: p } = parseAndValidate(content, t);
      setValidation(v);
      setParsed(p);
    };

    const fetchJson = (endpoint: string, onJson: (json: string) => void) => {
      setIsConverting(true);
      setConvertError(null);
      fetch(`${API_BASE}/templates/${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: raw }),
      })
        .then(r => r.json().then(d => ({ ok: r.ok, d })))
        .then(({ ok, d }) => {
          if (!ok) throw new Error(Array.isArray(d.details) ? d.details.join('\n') : (d.details || d.error || t('errors.generic')));
          onJson(JSON.stringify(d, null, 2));
        })
        .catch(e => setConvertError(e.message ?? t('errors.conversionFailed')))
        .finally(() => setIsConverting(false));
    };

    // JSON → C# DTO
    if (lang === 'csharp-dto' && language === 'json') {
      finish(parsed ? jsonToCSharp(parsed) : jsonToCSharp(STARTER_TEMPLATES.hello as ParsedDesign));
    }

    // JSON → C# Code
    else if (lang === 'csharp-code' && language === 'json') {
      finish(parsed ? jsonToCode(parsed) : CSHARP_CODE_STARTER);
    }

    // C# DTO → JSON
    else if (lang === 'json' && language === 'csharp-dto') {
      fetchJson('csharp-to-json', json => finishJson(json));
    }

    // C# Code → JSON
    else if (lang === 'json' && language === 'csharp-code') {
      fetchJson('csharp-code-to-json', json => finishJson(json));
    }

    // C# Code → C# DTO (execute code → JSON → DTO)
    else if (lang === 'csharp-dto' && language === 'csharp-code') {
      fetchJson('csharp-code-to-json', json => finish(jsonToCSharp(JSON.parse(json) as ParsedDesign)));
    }

    // C# DTO → C# Code (execute DTO → JSON → code)
    else if (lang === 'csharp-code' && language === 'csharp-dto') {
      fetchJson('csharp-to-json', json => finish(jsonToCode(JSON.parse(json) as ParsedDesign)));
    }

    setPdfUrl(null);
    setLanguage(lang);
    try { localStorage.setItem(STORAGE_LANG_KEY, lang); } catch {}
  }, [language, raw, parsed, setPdfUrl, t]);

  // ── C# Code: run script → PDF ────────────────────────────────

  const handleCsharpCodeRun = useCallback(async (code: string) => {
    setIsConverting(true);
    setConvertError(null);
    try {
      const res = await fetch(`${API_BASE}/templates/csharp-code-to-pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
        const msg = Array.isArray(d.details) ? d.details.join('\n') : (d.details || d.error || `HTTP ${res.status}`);
        throw new Error(msg);
      }
      const blob = await res.blob();
      setPdfUrl(URL.createObjectURL(blob));
    } catch (e: any) {
      setConvertError(e.message ?? t('errors.runFailed'));
    } finally {
      setIsConverting(false);
    }
  }, [setPdfUrl, t]);

  // ── C# DTO: convert → JSON preview ───────────────────────────

  const handleCsharpDtoConvert = useCallback(async (code: string) => {
    setIsConverting(true);
    setConvertError(null);
    try {
      const res = await fetch(`${API_BASE}/templates/csharp-to-json`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code }),
      });
      const data = await res.json();
      if (!res.ok) {
        const msg = Array.isArray(data.details) ? data.details.join('\n') : (data.error ?? `HTTP ${res.status}`);
        throw new Error(msg);
      }
      const { validation: v, parsed: p } = parseAndValidate(JSON.stringify(data), t);
      setValidation(v);
      setParsed(p);
    } catch (e: any) {
      setConvertError(e.message ?? t('errors.conversionFailed'));
      setValidation({ valid: false, errors: [e.message ?? t('errors.conversionFailed')] });
      setParsed(null);
    } finally {
      setIsConverting(false);
    }
  }, [t]);

  const handleCsharpConvert = language === 'csharp-code' ? handleCsharpCodeRun : handleCsharpDtoConvert;

  // ── Export ────────────────────────────────────────────────────

  const handleExport = async () => {
    setExportError(null);

    if (language === 'csharp-code') {
      // Re-run → download the generated PDF
      setIsExporting(true);
      try {
        const res = await fetch(`${API_BASE}/templates/csharp-code-to-pdf`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: raw }),
        });
        if (!res.ok) {
          const d = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
          throw new Error(d.error ?? `HTTP ${res.status}`);
        }
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'pxa-code.pdf';
        document.body.appendChild(a); a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      } catch (e: any) {
        setExportError(e.message ?? t('errors.exportFailed'));
      } finally {
        setIsExporting(false);
      }
      return;
    }

    if (language === 'csharp-dto') {
      setIsExporting(true);
      try {
        const res = await fetch(`${API_BASE}/templates/csharp-to-json`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: raw }),
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.error ?? `HTTP ${res.status}`);
        await ExportService.exportJsonToPDF(data, data.name ?? 'document');
      } catch (e: any) {
        setExportError(e.message ?? t('errors.exportFailed'));
      } finally {
        setIsExporting(false);
      }
      return;
    }

    if (!validation.valid || !parsed) return;
    setIsExporting(true);
    try {
      await ExportService.exportJsonToPDF(JSON.parse(raw), parsed.name ?? 'document');
    } catch (e: any) {
      setExportError(e.message ?? t('errors.exportFailed'));
    } finally {
      setIsExporting(false);
    }
  };

  // ── Splitter ──────────────────────────────────────────────────

  const onMouseDown = (e: React.MouseEvent) => { e.preventDefault(); dragging.current = true; };

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current || !containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      setSplitPct(Math.min(70, Math.max(30, ((e.clientX - rect.left) / rect.width) * 100)));
    };
    const onUp = () => { dragging.current = false; };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); };
  }, []);

  const canExport = !isExporting && !isConverting && (language !== 'json' || (validation.valid && !!parsed));

  return (
    <div className="live-code-editor">
      {/* Top bar */}
      <div className="live-code-editor-topbar">
        <button className="live-code-topbar-back" onClick={onBack}>{t('back')}</button>
        <span className="live-code-topbar-title">{t('title')}</span>

        {/* 3-way language toggle */}
        <div className="live-code-lang-toggle">
          <button className={`live-code-lang-btn${language === 'json' ? ' is-active' : ''}`}
            onClick={() => switchTo('json')} disabled={isConverting}>{t('lang.json')}</button>
          <button className={`live-code-lang-btn${language === 'csharp-code' ? ' is-active' : ''}`}
            onClick={() => switchTo('csharp-code')} disabled={isConverting}>{t('lang.csharpCode')}</button>
          <button className={`live-code-lang-btn${language === 'csharp-dto' ? ' is-active' : ''}`}
            onClick={() => switchTo('csharp-dto')} disabled={isConverting}>{t('lang.csharpDto')}</button>
        </div>

        <div className="live-code-topbar-right">
          {(exportError || convertError) && (
            <span className="live-code-export-error" title={exportError || convertError || ''}>
              {(exportError || convertError || '').split('\n')[0]}
            </span>
          )}
          {isConverting && <span className="live-code-converting">{t('converting')}</span>}
          <button
            className={`live-code-export-btn${!canExport ? ' is-disabled' : ''}`}
            onClick={handleExport}
            disabled={!canExport}
          >
            {isExporting ? t('generating') : t('exportPdf')}
          </button>
        </div>
      </div>

      {/* Split layout */}
      <div className="live-code-editor-body" ref={containerRef}>
        <div className="live-code-editor-left" style={{ width: `${splitPct}%` }}>
          <JsonEditorPane
            key={`${language}-${editorKey}`}
            value={raw}
            language={language}
            onChange={handleChange}
            onCsharpConvert={handleCsharpConvert}
          />
        </div>

        <div className="live-code-splitter" onMouseDown={onMouseDown}>
          <div className="live-code-splitter-handle" />
        </div>

        <div className="live-code-editor-right" style={{ width: `${100 - splitPct}%` }}>
          <CodePreviewPane
            raw={language === 'json' ? raw : ''}
            language={language}
            validation={validation}
            parsed={parsed}
            pdfBlobUrl={pdfBlobUrl}
            isConverting={isConverting}
            convertError={convertError}
            onExport={handleExport}
            isExporting={isExporting}
          />
        </div>
      </div>
    </div>
  );
}
