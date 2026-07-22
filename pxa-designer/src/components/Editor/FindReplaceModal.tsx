import React, { useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { FiX, FiSearch, FiLoader, FiCheck, FiAlertCircle } from 'react-icons/fi';
import ExportService from '@/services/ExportService';
import type { Template, Page, SimpleElement, PageSettings } from '@/types';

interface Props {
  template: Template;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings?: PageSettings;
  onClose: () => void;
  onApply: (updatedPages: Page[], updatedShared: SimpleElement[]) => void;
}

type State = 'idle' | 'loading' | 'done' | 'error';

const FindReplaceModal: React.FC<Props> = ({
  template, pages, sharedElements, pageSettings, onClose, onApply,
}) => {
  const { t } = useTranslation('editor');
  const [find, setFind]               = useState('');
  const [replace, setReplace]         = useState('');
  const [caseSensitive, setCaseSensitive] = useState(false);
  const [wholeWord, setWholeWord]     = useState(false);
  const [useRegex, setUseRegex]       = useState(false);
  const [state, setState]             = useState<State>('idle');
  const [error, setError]             = useState('');
  const [result, setResult]           = useState<{ count: number; ids: string[] } | null>(null);

  const buildDesign = () => ({
    id: template.id,
    name: template.name,
    pages: pages.map(p => ({ id: p.id, elements: p.elements })),
    sharedElements,
    pageSettings: pageSettings ?? {},
  });

  const handleRun = useCallback(async () => {
    if (!find.trim()) return;
    setState('loading');
    setError('');
    setResult(null);

    try {
      const resp = await ExportService.findAndReplace(buildDesign(), find, replace, {
        caseSensitive, wholeWord, useRegex,
      }) as any;

      const updatedPages: Page[] = pages.map((p, i) => ({
        ...p,
        elements: resp.design.pages[i]?.elements ?? p.elements,
      }));
      const updatedShared: SimpleElement[] = resp.design.sharedElements ?? sharedElements;

      onApply(updatedPages, updatedShared);
      setResult({ count: resp.replacementCount, ids: resp.affectedElementIds });
      setState('done');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('findReplace.failed'));
      setState('error');
    }
  }, [find, replace, caseSensitive, wholeWord, useRegex, pages, sharedElements, t]);

  return (
    <div className="export-modal-backdrop" onClick={onClose}>
      <div className="export-modal" style={{ maxWidth: 480 }} onClick={e => e.stopPropagation()} role="dialog" aria-label={t('findReplace.ariaLabel')}>
        <div className="export-modal-header">
          <h2 className="export-modal-title">{t('findReplace.title')}</h2>
          <button className="export-modal-close" onClick={onClose} aria-label={t('findReplace.close')}><FiX size={18} /></button>
        </div>

        <div className="export-modal-body" style={{ padding: 20 }}>
          <div className="editor-form-stack">
            <label>
              <span>{t('findReplace.find')}</span>
              <input
                type="text"
                placeholder={useRegex ? t('findReplace.findPlaceholderRegex') : t('findReplace.findPlaceholderText')}
                value={find}
                onChange={e => { setFind(e.target.value); setState('idle'); setResult(null); }}
                autoFocus
              />
            </label>
            <label>
              <span>{t('findReplace.replaceWith')}</span>
              <input
                type="text"
                placeholder={t('findReplace.replacePlaceholder')}
                value={replace}
                onChange={e => setReplace(e.target.value)}
              />
            </label>

            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={caseSensitive} onChange={e => setCaseSensitive(e.target.checked)} />
                <span>{t('findReplace.caseSensitive')}</span>
              </label>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={wholeWord} onChange={e => setWholeWord(e.target.checked)} disabled={useRegex} />
                <span>{t('findReplace.wholeWord')}</span>
              </label>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={useRegex} onChange={e => setUseRegex(e.target.checked)} />
                <span>{t('findReplace.regex')}</span>
              </label>
            </div>

            {error && (
              <div style={{ color: 'var(--color-danger, #dc2626)', fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                <FiAlertCircle size={14} /> {error}
              </div>
            )}

            {state === 'done' && result && (
              <div style={{ color: 'var(--color-success, #16a34a)', fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                <FiCheck size={14} />
                {result.count === 0
                  ? t('findReplace.noMatches')
                  : t('findReplace.replacedSummary', { count: result.count, elementCount: result.ids.length })}
              </div>
            )}

            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 4 }}>
              <button className="editor-icon-button" onClick={onClose}>{t('findReplace.cancel')}</button>
              <button
                className="editor-primary-button"
                onClick={handleRun}
                disabled={!find.trim() || state === 'loading'}
              >
                {state === 'loading' ? <FiLoader className="spin" size={14} /> : <FiSearch size={14} />}
                {t('findReplace.replaceAll')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FindReplaceModal;
