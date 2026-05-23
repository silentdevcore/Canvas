import React, { useState, useCallback } from 'react';
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
      setError(err instanceof Error ? err.message : 'Find & Replace failed');
      setState('error');
    }
  }, [find, replace, caseSensitive, wholeWord, useRegex, pages, sharedElements]);

  return (
    <div className="export-modal-backdrop" onClick={onClose}>
      <div className="export-modal" style={{ maxWidth: 480 }} onClick={e => e.stopPropagation()} role="dialog" aria-label="Find and Replace">
        <div className="export-modal-header">
          <h2 className="export-modal-title">Find &amp; Replace</h2>
          <button className="export-modal-close" onClick={onClose} aria-label="Close"><FiX size={18} /></button>
        </div>

        <div className="export-modal-body" style={{ padding: 20 }}>
          <div className="editor-form-stack">
            <label>
              <span>Find</span>
              <input
                type="text"
                placeholder={useRegex ? 'Regular expression…' : 'Text to find…'}
                value={find}
                onChange={e => { setFind(e.target.value); setState('idle'); setResult(null); }}
                autoFocus
              />
            </label>
            <label>
              <span>Replace with</span>
              <input
                type="text"
                placeholder="Replacement text…"
                value={replace}
                onChange={e => setReplace(e.target.value)}
              />
            </label>

            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={caseSensitive} onChange={e => setCaseSensitive(e.target.checked)} />
                <span>Case sensitive</span>
              </label>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={wholeWord} onChange={e => setWholeWord(e.target.checked)} disabled={useRegex} />
                <span>Whole word</span>
              </label>
              <label className="editor-checkbox-control">
                <input type="checkbox" checked={useRegex} onChange={e => setUseRegex(e.target.checked)} />
                <span>Regex</span>
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
                  ? 'No matches found.'
                  : `Replaced ${result.count} occurrence${result.count !== 1 ? 's' : ''} in ${result.ids.length} element${result.ids.length !== 1 ? 's' : ''}.`}
              </div>
            )}

            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 4 }}>
              <button className="editor-icon-button" onClick={onClose}>Cancel</button>
              <button
                className="editor-primary-button"
                onClick={handleRun}
                disabled={!find.trim() || state === 'loading'}
              >
                {state === 'loading' ? <FiLoader className="spin" size={14} /> : <FiSearch size={14} />}
                Replace all
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FindReplaceModal;
