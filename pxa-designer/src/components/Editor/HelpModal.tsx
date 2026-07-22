import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FiX, FiZap, FiCommand, FiGrid, FiHelpCircle, FiExternalLink } from 'react-icons/fi';
import type { ElementType } from '@/types';
import { ELEMENT_CATALOG } from '@/docs/elementCatalog';

interface Props {
  selectedElementType: ElementType | null;
  onClose: () => void;
}

type Tab = 'quickstart' | 'shortcuts' | 'elements' | 'faq';

const SHORTCUT_KEYS = [
  { keys: '⌘ Z',       actionKey: 'undo' },
  { keys: '⌘ ⇧ Z',    actionKey: 'redo' },
  { keys: '⌘ A',       actionKey: 'selectAll' },
  { keys: '⌘ C',       actionKey: 'copy' },
  { keys: '⌘ V',       actionKey: 'paste' },
  { keys: '⌘ D',       actionKey: 'duplicate' },
  { keys: 'Delete',    actionKey: 'delete' },
  { keys: '← ↑ → ↓',  actionKey: 'moveOnePx' },
  { keys: '⇧ + Arrow', actionKey: 'moveTenPx' },
  { keys: '⌘ =',       actionKey: 'zoomIn' },
  { keys: '⌘ −',       actionKey: 'zoomOut' },
  { keys: '⌘ 0',       actionKey: 'zoomReset' },
  { keys: 'Esc',       actionKey: 'deselect' },
  { keys: 'F1',        actionKey: 'openHelp' },
];

const QUICK_STEP_KEYS = ['1', '2', '3', '4', '5'] as const;

const FAQ_KEYS = [
  'exportPdf', 'backgroundImage', 'templateVariables', 'secondPage',
  'multiLanguage', 'drawLine', 'tableOfContents', 'addressForm',
] as const;

const HelpModal: React.FC<Props> = ({ selectedElementType, onClose }) => {
  const { t } = useTranslation('editor');
  const initialTab: Tab = selectedElementType ? 'elements' : 'quickstart';
  const [activeTab, setActiveTab] = useState<Tab>(initialTab);
  const elementRef = useRef<HTMLTableRowElement | null>(null);
  const dialogRef = useRef<HTMLDivElement | null>(null);

  // Derived from the single source of truth so the Help dialog never drifts from the docs/catalog.
  // `elementCatalog.ts` stays English-only (it also feeds llms.txt/AI artifacts and a drift-guard
  // test) — translations are looked up here by element type with the catalog text as the fallback.
  const ELEMENTS: Array<{ type: ElementType; label: string; description: string; pdf: boolean; word: boolean }> =
    ELEMENT_CATALOG.map((e) => ({
      type: e.type,
      label: t(`elements.${e.type}.label`, { defaultValue: e.label }),
      description: t(`elements.${e.type}.description`, { defaultValue: e.description }),
      pdf: e.formatSupport.pdf,
      word: e.formatSupport.word,
    }));

  // Scroll to selected element type row when tab is 'elements'
  useEffect(() => {
    if (activeTab === 'elements' && selectedElementType && elementRef.current) {
      elementRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [activeTab, selectedElementType]);

  // Focus trap
  useEffect(() => {
    const el = dialogRef.current;
    if (!el) return;
    const focusable = el.querySelectorAll<HTMLElement>('button, [href], input, select, [tabindex]:not([tabindex="-1"])');
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    first?.focus();
    const trap = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return;
      if (e.shiftKey) { if (document.activeElement === first) { e.preventDefault(); last?.focus(); } }
      else { if (document.activeElement === last) { e.preventDefault(); first?.focus(); } }
    };
    el.addEventListener('keydown', trap);
    return () => el.removeEventListener('keydown', trap);
  }, []);

  // Close on Escape
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  const TABS: Array<{ id: Tab; label: string; icon: React.ReactNode }> = [
    { id: 'quickstart', label: t('help.tabs.quickstart'), icon: <FiZap size={14} /> },
    { id: 'shortcuts',  label: t('help.tabs.shortcuts'),  icon: <FiCommand size={14} /> },
    { id: 'elements',   label: t('help.tabs.elements'),   icon: <FiGrid size={14} /> },
    { id: 'faq',        label: t('help.tabs.faq'),        icon: <FiHelpCircle size={14} /> },
  ];

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        ref={dialogRef}
        className="modal-dialog help-modal"
        style={{ maxWidth: 680, maxHeight: '85vh', display: 'flex', flexDirection: 'column' }}
        onClick={e => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label={t('help.ariaLabel')}
      >
        {/* Header */}
        <div className="modal-header">
          <strong>{t('help.title')}</strong>
          <button className="modal-close" onClick={onClose} aria-label={t('help.close')}><FiX /></button>
        </div>

        {/* Tab bar */}
        <div className="help-modal-tabs">
          {TABS.map(tab => (
            <button
              key={tab.id}
              className={`help-modal-tab${activeTab === tab.id ? ' is-active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              {tab.icon} {tab.label}
            </button>
          ))}
        </div>

        {/* Body */}
        <div className="help-modal-body">
          {/* Quick Start */}
          {activeTab === 'quickstart' && (
            <div className="help-steps">
              {QUICK_STEP_KEYS.map((key, i) => (
                <div key={key} className="help-step">
                  <div className="help-step-num">{i + 1}</div>
                  <div>
                    <div className="help-step-title">{t(`help.quickSteps.${key}.title`)}</div>
                    <div className="help-step-desc">{t(`help.quickSteps.${key}.desc`)}</div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Shortcuts */}
          {activeTab === 'shortcuts' && (
            <table className="help-shortcuts-table">
              <thead><tr><th>{t('help.shortcutsTable.shortcut')}</th><th>{t('help.shortcutsTable.action')}</th></tr></thead>
              <tbody>
                {SHORTCUT_KEYS.map(s => (
                  <tr key={s.keys}>
                    <td><kbd>{s.keys}</kbd></td>
                    <td>{t(`help.shortcuts.${s.actionKey}`)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {/* Elements Reference */}
          {activeTab === 'elements' && (
            <table className="help-elements-table">
              <thead>
                <tr>
                  <th>{t('help.elementsTable.element')}</th>
                  <th>{t('help.elementsTable.description')}</th>
                  <th style={{ textAlign: 'center' }}>{t('help.elementsTable.pdf')}</th>
                  <th style={{ textAlign: 'center' }}>{t('help.elementsTable.word')}</th>
                </tr>
              </thead>
              <tbody>
                {ELEMENTS.map(el => (
                  <tr
                    key={el.type}
                    ref={el.type === selectedElementType ? elementRef : undefined}
                    className={el.type === selectedElementType ? 'help-row-highlighted' : undefined}
                  >
                    <td><strong>{el.label}</strong></td>
                    <td>{el.description}</td>
                    <td style={{ textAlign: 'center' }}>{el.pdf ? '✓' : '—'}</td>
                    <td style={{ textAlign: 'center' }}>{el.word ? '✓' : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {/* FAQ */}
          {activeTab === 'faq' && (
            <div className="help-faq">
              {FAQ_KEYS.map((key) => (
                <details key={key} className="help-faq-item">
                  <summary className="help-faq-q">{t(`help.faq.${key}.q`)}</summary>
                  <p className="help-faq-a">{t(`help.faq.${key}.a`)}</p>
                </details>
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="modal-footer">
          <a href="/docs" target="_blank" rel="noopener noreferrer" className="help-docs-link">
            <FiExternalLink size={13} /> {t('help.docsLink')}
          </a>
          <button className="modal-confirm-btn" onClick={onClose}>{t('help.close')}</button>
        </div>
      </div>
    </div>
  );
};

export default HelpModal;
