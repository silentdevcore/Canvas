import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import JsonEditorPane from './JsonEditorPane';
import CodePreviewPane, { type ParsedDesign, type ValidationResult } from './CodePreviewPane';
import { readMigratedStorage } from '@/utils/storageMigration';
import { ExportService } from '@/services/ExportService';
import {
  applyCodeDraft,
  compareCodeDraft,
  convertCodeDraft,
  executeCodeDraft,
  getCodeWorkspace,
  saveCodeDraft,
  validateCodeDraft,
  type CodeConversionResult,
  type CodeDiagnostic,
  type CodeLanguage,
  type CodeWorkspaceComparison,
  type DraftStatus,
} from '@/services/codeWorkspaceApi';

const LEGACY_STORAGE_KEY = 'pvad-code-editor-draft-v2';
const LANGUAGES: CodeLanguage[] = ['json', 'csharpModel', 'csharpPdf', 'csharpBase64'];

const languageLabelKey = (language: CodeLanguage) => language === 'json'
  ? 'lang.json'
  : language === 'csharpModel'
    ? 'lang.csharpDto'
    : language === 'csharpPdf'
      ? 'lang.csharpCode'
      : 'lang.csharpBase64';
const EMPTY_DRAFTS: Record<CodeLanguage, string> = { json: '', csharpModel: '', csharpPdf: '', csharpBase64: '' };
const EMPTY_STATUS: Record<CodeLanguage, DraftStatus> = { json: 'Saved', csharpModel: 'Outdated', csharpPdf: 'Outdated', csharpBase64: 'Outdated' };

export type EditorLanguage = CodeLanguage;

interface Props {
  onBack: () => void;
  templateId?: string;
  templateRevision?: number;
  initialDesign: ParsedDesign;
  onApply: (design: ParsedDesign, templateRevision: number) => void;
  onBeforeCompare?: () => Promise<void>;
}

function parseAndValidate(raw: string, t: TFunction): { validation: ValidationResult; parsed: ParsedDesign | null } {
  if (!raw.trim()) return { validation: { valid: false, errors: [] }, parsed: null };
  try {
    const obj = JSON.parse(raw);
    const errors: string[] = [];
    if (!Array.isArray(obj.pages) || obj.pages.length === 0) errors.push(t('errors.pagesMustHaveOne'));
    else obj.pages.forEach((page: any, pageIndex: number) => {
      if (!page.id) errors.push(t('errors.pageIdRequired', { i: pageIndex }));
      if (!Array.isArray(page.elements)) errors.push(t('errors.pageElementsMustBeArray', { i: pageIndex }));
      else page.elements.forEach((element: any, elementIndex: number) => {
        if (!element.id) errors.push(t('errors.elementIdRequired', { i: pageIndex, j: elementIndex }));
        if (!element.type) errors.push(t('errors.elementTypeRequired', { i: pageIndex, j: elementIndex }));
        for (const property of ['x', 'y', 'width', 'height']) {
          if (typeof element[property] !== 'number') errors.push(`${page.id}/${element.id}: ${property} must be a number.`);
        }
      });
    });
    return { validation: { valid: errors.length === 0, errors }, parsed: errors.length ? null : obj as ParsedDesign };
  } catch (error) {
    return { validation: { valid: false, errors: [t('errors.syntaxError', { message: error instanceof Error ? error.message : String(error) })] }, parsed: null };
  }
}

function designDiff(before: ParsedDesign | null, after: ParsedDesign | null) {
  const elements = (value: ParsedDesign | null) => new Map((value?.pages ?? []).flatMap(page =>
    (page.elements ?? []).map(element => [`${page.id}/${element.id}`, JSON.stringify(element)])));
  const left = elements(before); const right = elements(after);
  const fields = (left: Record<string, unknown>, right: Record<string, unknown>) =>
    [...new Set([...Object.keys(left), ...Object.keys(right)])].filter(key => JSON.stringify(left[key]) !== JSON.stringify(right[key]));
  const { pages: beforePages = [], ...beforeSettings } = before ?? {};
  const { pages: afterPages = [], ...afterSettings } = after ?? {};
  const pageSettings = (pages: typeof beforePages) => pages.map(({ elements: _elements, ...page }) => page);
  return {
    added: [...right.keys()].filter(id => !left.has(id)).length,
    removed: [...left.keys()].filter(id => !right.has(id)).length,
    changed: [...right.keys()].filter(id => left.has(id) && left.get(id) !== right.get(id)).length,
    settings: fields(beforeSettings, afterSettings),
    pagesChanged: JSON.stringify(pageSettings(beforePages)) !== JSON.stringify(pageSettings(afterPages)),
  };
}

type ApplyReview = CodeWorkspaceComparison & { language: CodeLanguage; source: string };

function CodeApplyReview({ review, busy, onConfirm, onCancel }: {
  review: ApplyReview; busy: boolean; onConfirm: () => void; onCancel: () => void;
}) {
  const { t } = useTranslation('codeEditor');
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { dialog.current?.showModal(); }, []);
  const diff = designDiff(review.templateDesign, review.conversion.canonicalDesign);
  return <dialog ref={dialog} className="code-apply-review" aria-labelledby="code-apply-title"
    onCancel={event => { event.preventDefault(); if (!busy) onCancel(); }}>
    <h2 id="code-apply-title">{t('applyReview.title')}</h2>
    {review.baseTemplateRevision !== review.templateRevision && <p role="status">{t('applyReview.staleBase', {
      base: review.baseTemplateRevision, current: review.templateRevision,
    })}</p>}
    <p>{t('applyReview.replaceWarning')}</p>
    <p>{t('workspace.documentFidelity')}: {review.conversion.documentFidelity}</p>
    <p>{t('workspace.sourcePreservation')}: {review.conversion.sourcePreservation}</p>
    <dl>
      <dt>{t('workspace.added')}</dt><dd>{diff.added}</dd>
      <dt>{t('workspace.changed')}</dt><dd>{diff.changed}</dd>
      <dt>{t('workspace.removed')}</dt><dd>{diff.removed}</dd>
      <dt>{t('applyReview.pageChanges')}</dt><dd>{t(diff.pagesChanged ? 'applyReview.yes' : 'applyReview.no')}</dd>
      <dt>{t('applyReview.settingsChanges')}</dt><dd>{diff.settings.join(', ') || t('applyReview.none')}</dd>
    </dl>
    <details><summary>{t('applyReview.currentDesigner')}</summary><pre>{JSON.stringify(review.templateDesign, null, 2)}</pre></details>
    <details><summary>{t('applyReview.codeResult')}</summary><pre>{JSON.stringify(review.conversion.canonicalDesign, null, 2)}</pre></details>
    <div className="code-apply-actions">
      <button type="button" className="code-editor-btn" autoFocus disabled={busy} onClick={onCancel}>{t('actions.cancel')}</button>
      <button type="button" className="live-code-export-btn" disabled={busy} onClick={onConfirm}>{t('applyReview.confirm')}</button>
    </div>
  </dialog>;
}

function pdfUrl(base64?: string) {
  if (!base64) return null;
  const binary = atob(base64); const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
  return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
}

export default function LiveCodeEditor({ onBack, templateId, templateRevision = 0, initialDesign, onApply, onBeforeCompare }: Props) {
  const { t } = useTranslation('codeEditor');
  const [language, setLanguage] = useState<CodeLanguage>('json');
  const [drafts, setDrafts] = useState<Record<CodeLanguage, string>>({ ...EMPTY_DRAFTS, json: JSON.stringify(initialDesign, null, 2) });
  const [statuses, setStatuses] = useState<Record<CodeLanguage, DraftStatus>>(EMPTY_STATUS);
  const [workspaceRevision, setWorkspaceRevision] = useState(0);
  const [baseTemplateRevision, setBaseTemplateRevision] = useState(templateRevision);
  const [persisted, setPersisted] = useState(false);
  const [diagnostics, setDiagnostics] = useState<CodeDiagnostic[]>([]);
  const [previewDesign, setPreviewDesign] = useState<ParsedDesign | null>(initialDesign);
  const [previewProvenance, setPreviewProvenance] = useState<{ language: CodeLanguage; source: string } | null>(null);
  const [pdfBlobUrl, setPdfBlobUrl] = useState<string | null>(null);
  const [pdfSource, setPdfSource] = useState<string | null>(null);
  const [busy, setBusy] = useState<'loading' | 'saving' | 'validating' | 'running' | 'converting' | 'comparing' | 'applying' | null>('loading');
  const [error, setError] = useState('');
  const [targetLanguage, setTargetLanguage] = useState<CodeLanguage>('csharpModel');
  const [pendingConversion, setPendingConversion] = useState<CodeConversionResult | null>(null);
  const [pendingApply, setPendingApply] = useState<ApplyReview | null>(null);
  const [legacyDraft, setLegacyDraft] = useState<string | null>(null);
  const [splitPct, setSplitPct] = useState(50);
  const [editorKey, setEditorKey] = useState(0);
  const saving = useRef(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const dragging = useRef(false);
  const activeSource = drafts[language];
  const latestDrafts = useRef(drafts);
  latestDrafts.current = drafts;
  const jsonState = useMemo(
    () => parseAndValidate(language === 'json' ? activeSource : '', t),
    [activeSource, language, t],
  );
  const activePreviewDesign = language === 'json'
    ? jsonState.parsed
    : previewProvenance?.language === language && previewProvenance.source === activeSource
      ? previewDesign
      : null;
  const activePdfBlobUrl = language === 'csharpPdf' && pdfSource === activeSource ? pdfBlobUrl : null;
  const parsedState = language === 'json'
    ? jsonState
    : { validation: { valid: activePreviewDesign !== null, errors: [] }, parsed: activePreviewDesign };

  const replacePdfUrl = useCallback((next: string | null) => {
    setPdfBlobUrl(previous => { if (previous) URL.revokeObjectURL(previous); return next; });
  }, []);

  useEffect(() => () => { if (pdfBlobUrl) URL.revokeObjectURL(pdfBlobUrl); }, [pdfBlobUrl]);

  useEffect(() => {
    if (targetLanguage === language)
      setTargetLanguage(LANGUAGES.find(item => item !== language) ?? 'json');
  }, [language, targetLanguage]);

  useEffect(() => {
    try { setLegacyDraft(readMigratedStorage(localStorage, LEGACY_STORAGE_KEY, 'pxa-code-editor-draft-v2')); } catch { /* storage unavailable */ }
    if (!templateId) { setBusy(null); return; }
    const controller = new AbortController();
    setBusy('loading');
    getCodeWorkspace(templateId, controller.signal).then(workspace => {
      setDrafts({ json: workspace.json.source, csharpModel: workspace.cSharpModel.source, csharpPdf: workspace.cSharpPdf.source, csharpBase64: workspace.cSharpBase64.source });
      setStatuses({ json: 'Saved', csharpModel: workspace.cSharpModel.source ? 'Saved' : 'Outdated', csharpPdf: workspace.cSharpPdf.source ? 'Saved' : 'Outdated', csharpBase64: workspace.cSharpBase64.source ? 'Saved' : 'Outdated' });
      setWorkspaceRevision(workspace.revision);
      setBaseTemplateRevision(workspace.baseTemplateRevision);
      setPersisted(workspace.persisted);
      setPreviewDesign(workspace.canonicalDesign as ParsedDesign);
      setPreviewProvenance(null);
      setPdfSource(null);
      setEditorKey(value => value + 1);
    }).catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : String(cause)); })
      .finally(() => { if (!controller.signal.aborted) setBusy(null); });
    return () => controller.abort();
  }, [templateId]);

  useEffect(() => {
    if (!templateId || !['Modified', 'Generated'].includes(statuses[language]) || saving.current || busy || pendingApply) return;
    const timer = window.setTimeout(async () => {
      saving.current = true; setBusy('saving');
      try {
        const workspace = await saveCodeDraft(templateId, workspaceRevision, language, drafts[language]);
        setWorkspaceRevision(workspace.revision); setPersisted(true);
        if (latestDrafts.current[language] === drafts[language])
          setStatuses(current => ({ ...current, [language]: 'Saved' }));
      } catch (cause: any) {
        setStatuses(current => ({ ...current, [language]: cause?.status === 409 ? 'Conflict' : 'Modified' }));
        setError(cause instanceof Error ? cause.message : String(cause));
      } finally { saving.current = false; setBusy(null); }
    }, 2000);
    return () => window.clearTimeout(timer);
  }, [drafts, language, statuses, templateId, workspaceRevision, busy, pendingApply]);

  const handleChange = useCallback((value: string) => {
    setDrafts(current => ({ ...current, [language]: value }));
    setStatuses(current => ({ ...current, [language]: 'Modified' }));
    setError('');
    if (language === 'json') {
      const parsed = parseAndValidate(value, t);
      setPreviewDesign(parsed.parsed);
      setDiagnostics(parsed.validation.errors.map(message => ({ code: 'PXACODE-LOCAL', severity: 'error', message })));
    } else {
      setPreviewProvenance(current => current?.language === language ? null : current);
      if (language === 'csharpPdf') { replacePdfUrl(null); setPdfSource(null); }
    }
  }, [language, replacePdfUrl, t]);

  const requireTemplate = () => {
    if (!templateId) throw new Error('Wait until this template has been saved before running server code.');
    return templateId;
  };

  const validate = async () => {
    setBusy('validating'); setError('');
    try {
      const result = await validateCodeDraft(requireTemplate(), language, activeSource);
      const next = result.diagnostics ?? [];
      setDiagnostics(next);
      setStatuses(current => ({ ...current, [language]: next.some((item: CodeDiagnostic) => item.severity === 'error') ? 'Invalid' : current[language] }));
      if (result.canonicalDesign) {
        setPreviewDesign(result.canonicalDesign);
        setPreviewProvenance({ language, source: activeSource });
      }
    } catch (cause) { setError(cause instanceof Error ? cause.message : String(cause)); }
    finally { setBusy(null); }
  };

  const run = async () => {
    setBusy('running'); setError(''); replacePdfUrl(null); setPdfSource(null);
    try {
      const result = await executeCodeDraft(requireTemplate(), language, activeSource);
      setDiagnostics(result.diagnostics ?? []);
      if (!result.success) { setStatuses(current => ({ ...current, [language]: 'Invalid' })); return; }
      if (result.canonicalDesign) {
        setPreviewDesign(result.canonicalDesign);
        setPreviewProvenance({ language, source: activeSource });
      }
      if (language === 'csharpPdf') {
        replacePdfUrl(pdfUrl(result.pdfBytes));
        if (result.pdfBytes) setPdfSource(activeSource);
      }
    } catch (cause) { setError(cause instanceof Error ? cause.message : String(cause)); }
    finally { setBusy(null); }
  };

  const convert = async () => {
    if (language === targetLanguage) return;
    setBusy('converting'); setError('');
    try {
      const result = await convertCodeDraft(requireTemplate(), language, targetLanguage, activeSource);
      setDiagnostics(result.diagnostics);
      setPendingConversion(result);
    } catch (cause) { setError(cause instanceof Error ? cause.message : String(cause)); }
    finally { setBusy(null); }
  };

  const acceptConversion = () => {
    if (!pendingConversion) return;
    setDrafts(current => ({ ...current, [pendingConversion.targetLanguage]: pendingConversion.generatedSource }));
    setStatuses(current => ({ ...current, [pendingConversion.targetLanguage]: 'Generated' }));
    if (pendingConversion.canonicalDesign) {
      setPreviewDesign(pendingConversion.canonicalDesign);
      setPreviewProvenance({
        language: pendingConversion.targetLanguage,
        source: pendingConversion.generatedSource,
      });
    }
    if (pendingConversion.targetLanguage === 'csharpPdf') {
      replacePdfUrl(null);
      setPdfSource(null);
    }
    setLanguage(pendingConversion.targetLanguage); setPendingConversion(null); setEditorKey(value => value + 1);
  };

  const apply = async () => {
    if (saving.current || busy) return;
    setBusy('comparing'); setError('');
    try {
      try { await onBeforeCompare?.(); }
      catch { throw new Error(t('applyReview.saveRequired')); }
      const review = await compareCodeDraft(requireTemplate(), workspaceRevision, language, activeSource);
      setDiagnostics(review.conversion.diagnostics);
      if (latestDrafts.current[language] !== activeSource) throw new Error(t('applyReview.sourceChanged'));
      setPendingApply({ ...review, language, source: activeSource });
    } catch (cause: any) {
      if (cause?.status === 409) setStatuses(current => ({ ...current, [language]: 'Conflict' }));
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally { setBusy(null); }
  };

  const confirmApply = async () => {
    if (!pendingApply || busy || saving.current) return;
    const review = pendingApply;
    setBusy('applying'); setError('');
    try {
      const result = await applyCodeDraft(requireTemplate(), review.workspaceRevision, review.templateRevision,
        review.language, review.source, review.conversion.canonicalChecksum);
      setWorkspaceRevision(result.workspaceRevision); setBaseTemplateRevision(result.templateRevision);
      setPreviewDesign(result.conversion.canonicalDesign as ParsedDesign);
      setPreviewProvenance({ language: review.language, source: review.source });
      onApply(result.conversion.canonicalDesign as ParsedDesign, result.templateRevision);
      setStatuses(current => ({ ...current, [review.language]: latestDrafts.current[review.language] === review.source ? 'Saved' : 'Modified' }));
    } catch (cause: any) {
      if (cause?.status === 409) setStatuses(current => ({ ...current, [language]: 'Conflict' }));
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally { setPendingApply(null); setBusy(null); }
  };

  const restore = () => {
    if (language === 'json') setDrafts(current => ({ ...current, json: JSON.stringify(previewDesign ?? initialDesign, null, 2) }));
    else {
      setDrafts(current => ({ ...current, [language]: '' }));
      setPreviewProvenance(current => current?.language === language ? null : current);
      if (language === 'csharpPdf') { replacePdfUrl(null); setPdfSource(null); }
    }
    setStatuses(current => ({ ...current, [language]: language === 'json' ? 'Saved' : 'Outdated' }));
    setEditorKey(value => value + 1);
  };

  const importLegacy = () => {
    if (!legacyDraft) return;
    setDrafts(current => ({ ...current, json: legacyDraft }));
    setStatuses(current => ({ ...current, json: 'Modified' }));
    setLanguage('json'); setLegacyDraft(null); setEditorKey(value => value + 1);
    for (const key of [LEGACY_STORAGE_KEY, 'pxa-code-editor-draft-v2']) {
      try {
        if (localStorage.getItem(key) === legacyDraft) localStorage.removeItem(key);
      } catch { /* Preserve different or concurrently updated recovery drafts. */ }
    }
  };

  const exportPdf = async () => {
    if (language === 'csharpPdf' && activePdfBlobUrl) {
      const anchor = document.createElement('a'); anchor.href = activePdfBlobUrl; anchor.download = 'pvad-code.pdf'; anchor.click(); return;
    }
    if (activePreviewDesign) await ExportService.exportJsonToPDF(activePreviewDesign, activePreviewDesign.name ?? 'document');
  };

  useEffect(() => {
    const move = (event: MouseEvent) => {
      if (!dragging.current || !containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      setSplitPct(Math.min(70, Math.max(30, ((event.clientX - rect.left) / rect.width) * 100)));
    };
    const up = () => { dragging.current = false; };
    window.addEventListener('mousemove', move); window.addEventListener('mouseup', up);
    return () => { window.removeEventListener('mousemove', move); window.removeEventListener('mouseup', up); };
  }, []);

  const diff = pendingConversion ? designDiff(activePreviewDesign ?? previewDesign, pendingConversion.canonicalDesign as ParsedDesign) : null;

  return <div className="live-code-editor">
    <div className="live-code-editor-topbar">
      <button className="live-code-topbar-back" onClick={onBack}>{t('back')}</button>
      <span className="live-code-topbar-title">{t('title')}</span>
      <div className="live-code-lang-toggle" role="tablist">
        {LANGUAGES.map(item => <button key={item} role="tab" aria-selected={language === item}
          disabled={!!busy || !!pendingApply}
          className={`live-code-lang-btn${language === item ? ' is-active' : ''}`} onClick={() => setLanguage(item)}>
          {t(languageLabelKey(item))}
          <small className={`code-draft-status status-${statuses[item].toLowerCase()}`}>{t(`status.${statuses[item].toLowerCase()}`)}</small>
        </button>)}
      </div>
      <div className="live-code-topbar-right">
        <button className="code-editor-btn" onClick={validate} disabled={!!busy}>{t('actions.validate')}</button>
        <button className="code-editor-btn" onClick={run} disabled={!!busy}>{t('actions.run')}</button>
        <select value={targetLanguage} onChange={event => setTargetLanguage(event.target.value as CodeLanguage)} aria-label={t('workspace.target')}>
          {LANGUAGES.filter(item => item !== language).map(item => <option key={item} value={item}>{t(languageLabelKey(item))}</option>)}
        </select>
        <button className="code-editor-btn" onClick={convert} disabled={!!busy || targetLanguage === language}>{t('actions.convert')}</button>
        <button className="code-editor-btn" onClick={restore} disabled={!!busy}>{t('actions.restore')}</button>
        <button className="live-code-export-btn" onClick={apply} disabled={!!busy || !persisted}>{t('actions.apply')}</button>
        <button className="code-editor-btn" onClick={exportPdf} disabled={!!busy || !activePreviewDesign}>{t('exportPdf')}</button>
      </div>
    </div>
    {legacyDraft && <aside className="code-workspace-banner">{t('workspace.legacy')}
      <button onClick={importLegacy}>{t('actions.importLegacy')}</button><button onClick={() => setLegacyDraft(null)}>{t('actions.dismiss')}</button></aside>}
    {!templateId && <aside className="code-workspace-banner">{t('workspace.waiting')}</aside>}
    {templateRevision > baseTemplateRevision && <aside className="code-workspace-banner" role="status">{t('applyReview.staleBase', {
      base: baseTemplateRevision, current: templateRevision,
    })}</aside>}
    {pendingApply && <CodeApplyReview review={pendingApply} busy={!!busy}
      onConfirm={() => void confirmApply()} onCancel={() => setPendingApply(null)} />}
    {busy && <div className="code-workspace-progress" role="status">{busy}...</div>}
    {error && <div className="code-workspace-error" role="alert">{error}</div>}
    {pendingConversion && diff && <section className="code-conversion-review" role="dialog" aria-label="Conversion review">
      <strong>{pendingConversion.sourceLanguage} to {pendingConversion.targetLanguage}</strong>
      <span>{t('workspace.documentFidelity')}: {pendingConversion.documentFidelity ?? pendingConversion.fidelity}</span>
      <span>{t('workspace.sourcePreservation')}: {pendingConversion.sourcePreservation}</span>
      <span>{t('workspace.added')}: {diff.added}</span><span>{t('workspace.changed')}: {diff.changed}</span><span>{t('workspace.removed')}: {diff.removed}</span>
      <button onClick={acceptConversion}>{t('actions.accept')}</button><button onClick={() => setPendingConversion(null)}>{t('actions.cancel')}</button>
    </section>}
    {diagnostics.length > 0 && <aside className="code-diagnostics" aria-label="Code diagnostics">
      {diagnostics.map((item, index) => <button key={`${item.code}-${index}`} className={`diagnostic-${item.severity}`} title={item.elementId ? `Element ${item.elementId}` : undefined}>
        {item.code}{item.line ? `:${item.line}` : ''} {item.message}
      </button>)}
    </aside>}
    <div className="live-code-editor-body" ref={containerRef}>
      <div className="live-code-editor-left" style={{ width: `${splitPct}%` }}>
        <JsonEditorPane key={`${language}-${editorKey}`} value={activeSource} language={language} onChange={handleChange} onCsharpConvert={run} />
      </div>
      <div className="live-code-splitter" onMouseDown={event => { event.preventDefault(); dragging.current = true; }}><div className="live-code-splitter-handle" /></div>
      <div className="live-code-editor-right" style={{ width: `${100 - splitPct}%` }}>
        <CodePreviewPane raw={language === 'json' ? activeSource : ''} language={language}
          validation={parsedState.validation} parsed={activePreviewDesign} pdfBlobUrl={activePdfBlobUrl}
          isConverting={busy === 'running' || busy === 'converting'} convertError={error || null}
          onExport={exportPdf} isExporting={false} />
      </div>
    </div>
  </div>;
}
