import React, { useEffect, useRef, useState } from 'react';
import { FiX, FiZap, FiCommand, FiGrid, FiHelpCircle, FiExternalLink } from 'react-icons/fi';
import type { ElementType } from '@/types';
import { ELEMENT_CATALOG } from '@/docs/elementCatalog';

interface Props {
  selectedElementType: ElementType | null;
  onClose: () => void;
}

type Tab = 'quickstart' | 'shortcuts' | 'elements' | 'faq';

const SHORTCUTS = [
  { keys: '⌘ Z',       action: 'Undo' },
  { keys: '⌘ ⇧ Z',    action: 'Redo' },
  { keys: '⌘ A',       action: 'Select all elements' },
  { keys: '⌘ C',       action: 'Copy selected element' },
  { keys: '⌘ V',       action: 'Paste element' },
  { keys: '⌘ D',       action: 'Duplicate element' },
  { keys: 'Delete',    action: 'Delete selected element' },
  { keys: '← ↑ → ↓',  action: 'Move element 1 px' },
  { keys: '⇧ + Arrow', action: 'Move element 10 px' },
  { keys: '⌘ =',       action: 'Zoom in' },
  { keys: '⌘ −',       action: 'Zoom out' },
  { keys: '⌘ 0',       action: 'Reset zoom to 100 %' },
  { keys: 'Esc',       action: 'Deselect / cancel drawing' },
  { keys: 'F1',        action: 'Open this help dialog' },
];

// Derived from the single source of truth so the Help dialog never drifts from the docs/catalog.
const ELEMENTS: Array<{ type: ElementType; label: string; description: string; pdf: boolean; word: boolean }> =
  ELEMENT_CATALOG.map((e) => ({
    type: e.type,
    label: e.label,
    description: e.description,
    pdf: e.formatSupport.pdf,
    word: e.formatSupport.word,
  }));

const QUICK_STEPS = [
  { step: 1, title: 'Open a template',        desc: 'Browse the template gallery and click "Use this template" to start.' },
  { step: 2, title: 'Add elements',           desc: 'Click any tool in the left toolbar to place it on the canvas, or drag it to a specific position.' },
  { step: 3, title: 'Edit properties',        desc: 'Select an element and adjust its content, style, and layout in the right inspector panel.' },
  { step: 4, title: 'Preview your document',  desc: 'Click "Preview" in the top bar to see a live render of your template with sample data.' },
  { step: 5, title: 'Export',                 desc: 'Click the export icon to download as PDF, Word, JSON, image, or other formats.' },
];

const FAQ: Array<{ q: string; a: string }> = [
  { q: 'How do I export a PDF?',              a: 'Click the export icon (⬆) in the top bar, select "PDF" from the format list, then click Download.' },
  { q: 'How do I add a background image?',    a: 'Click the page settings icon (⚙) when nothing is selected, then set "Background Image" in the inspector.' },
  { q: 'How do I use template variables?',    a: 'Type {{VARIABLE_NAME}} inside any text element. Variables are filled at render time from your JSON data.' },
  { q: 'How do I add a second page?',         a: 'Click the + button in the page strip at the bottom of the canvas area.' },
  { q: 'How do I set up multi-language?',     a: 'In Page Settings, add languages under the "Languages" section. A tab per language appears above the canvas.' },
  { q: 'How do I draw a line or arrow?',      a: 'Click the Line, Arrow, or Draw tool in the toolbar — a blue badge appears. Then drag on the canvas to place the element.' },
  { q: 'How do I generate a Table of Contents?', a: 'Add a "Table of Contents" element, then set Heading Level on your text elements. Click "Update TOC" in the inspector.' },
  { q: 'How do I insert an address form?',    a: 'Click "Insert Form Block" at the bottom of the left toolbar and choose Address Block.' },
];

const HelpModal: React.FC<Props> = ({ selectedElementType, onClose }) => {
  const initialTab: Tab = selectedElementType ? 'elements' : 'quickstart';
  const [activeTab, setActiveTab] = useState<Tab>(initialTab);
  const elementRef = useRef<HTMLTableRowElement | null>(null);
  const dialogRef = useRef<HTMLDivElement | null>(null);

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
    { id: 'quickstart', label: 'Quick Start',    icon: <FiZap size={14} /> },
    { id: 'shortcuts',  label: 'Shortcuts',      icon: <FiCommand size={14} /> },
    { id: 'elements',   label: 'Elements',       icon: <FiGrid size={14} /> },
    { id: 'faq',        label: 'FAQ',            icon: <FiHelpCircle size={14} /> },
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
        aria-label="Help"
      >
        {/* Header */}
        <div className="modal-header">
          <strong>Help &amp; Reference</strong>
          <button className="modal-close" onClick={onClose} aria-label="Close"><FiX /></button>
        </div>

        {/* Tab bar */}
        <div className="help-modal-tabs">
          {TABS.map(t => (
            <button
              key={t.id}
              className={`help-modal-tab${activeTab === t.id ? ' is-active' : ''}`}
              onClick={() => setActiveTab(t.id)}
            >
              {t.icon} {t.label}
            </button>
          ))}
        </div>

        {/* Body */}
        <div className="help-modal-body">
          {/* Quick Start */}
          {activeTab === 'quickstart' && (
            <div className="help-steps">
              {QUICK_STEPS.map(s => (
                <div key={s.step} className="help-step">
                  <div className="help-step-num">{s.step}</div>
                  <div>
                    <div className="help-step-title">{s.title}</div>
                    <div className="help-step-desc">{s.desc}</div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Shortcuts */}
          {activeTab === 'shortcuts' && (
            <table className="help-shortcuts-table">
              <thead><tr><th>Shortcut</th><th>Action</th></tr></thead>
              <tbody>
                {SHORTCUTS.map(s => (
                  <tr key={s.keys}>
                    <td><kbd>{s.keys}</kbd></td>
                    <td>{s.action}</td>
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
                  <th>Element</th>
                  <th>Description</th>
                  <th style={{ textAlign: 'center' }}>PDF</th>
                  <th style={{ textAlign: 'center' }}>Word</th>
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
              {FAQ.map((item, i) => (
                <details key={i} className="help-faq-item">
                  <summary className="help-faq-q">{item.q}</summary>
                  <p className="help-faq-a">{item.a}</p>
                </details>
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="modal-footer">
          <a href="/docs" target="_blank" rel="noopener noreferrer" className="help-docs-link">
            <FiExternalLink size={13} /> Open full documentation
          </a>
          <button className="modal-confirm-btn" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  );
};

export default HelpModal;
