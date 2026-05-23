import React, { useState, useCallback } from 'react';
import {
  FiX, FiDownload, FiLoader, FiCheck, FiAlertCircle,
  FiCode, FiFileText, FiImage, FiGrid, FiHash, FiShield,
} from 'react-icons/fi';
import ExportService, { type ExportFormat } from '@/services/ExportService';
import type { Template, Page, SimpleElement, PageSettings } from '@/types';
import SignDocxModal from './SignDocxModal';

interface Props {
  template: Template;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings?: PageSettings;
  onClose: () => void;
}

interface FormatCard {
  format: ExportFormat;
  label: string;
  ext: string;
  description: string;
  group: string;
  icon: React.ReactNode;
  serverSide: boolean;
}

const FORMAT_CARDS: FormatCard[] = [
  // Documents
  { format: 'pdf',   label: 'PDF',              ext: '.pdf',  description: 'Pixel-perfect document via backend renderer', group: 'Documents', icon: <FiFileText />, serverSide: false },
  { format: 'word',  label: 'Word (.docx)',      ext: '.docx', description: 'Editable Microsoft Word document',           group: 'Documents', icon: <FiFileText />, serverSide: true },
  { format: 'odt',   label: 'ODT',              ext: '.odt',  description: 'OpenDocument Text (LibreOffice / Google Docs)', group: 'Documents', icon: <FiFileText />, serverSide: true },
  { format: 'html',  label: 'HTML',             ext: '.html', description: 'Positioned HTML with inline CSS',            group: 'Documents', icon: <FiCode />,     serverSide: true },
  // Data
  { format: 'json',  label: 'JSON',             ext: '.json', description: 'Full template definition (client-side)',      group: 'Data',      icon: <FiCode />,     serverSide: false },
  { format: 'xml',   label: 'XML',              ext: '.xml',  description: 'Structured element data',                    group: 'Data',      icon: <FiCode />,     serverSide: true },
  { format: 'excel', label: 'Excel (.xlsx)',     ext: '.xlsx', description: 'Tables as worksheets, text as summary',      group: 'Data',      icon: <FiGrid />,     serverSide: true },
  { format: 'csv',   label: 'CSV',              ext: '.csv',  description: 'Comma-separated, Excel-compatible',          group: 'Data',      icon: <FiHash />,     serverSide: true },
  // Images
  { format: 'png',   label: 'PNG',              ext: '.png',  description: 'Raster image at 150 dpi via SkiaSharp',      group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'jpeg',  label: 'JPEG',             ext: '.jpg',  description: 'Compressed image, smaller file size',        group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'tiff',  label: 'TIFF',             ext: '.tiff', description: 'Multi-page TIFF for print and archival',     group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'svg',   label: 'SVG',              ext: '.svg',  description: 'Scalable vector, multi-page as zip',         group: 'Images',    icon: <FiImage />,    serverSide: true },
  // Text
  { format: 'md',    label: 'Markdown',         ext: '.md',   description: 'Reading-order prose, GFM tables',            group: 'Text',      icon: <FiFileText />, serverSide: true },
];

const GROUPS = ['Documents', 'Data', 'Images', 'Text'];

type ExportState = 'idle' | 'loading' | 'done' | 'error';

const LAST_FORMAT_KEY = 'canvas_export_format';

const ExportModal: React.FC<Props> = ({ template, pages, sharedElements, pageSettings, onClose }) => {
  const [states, setStates] = useState<Record<string, ExportState>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [progress, setProgress] = useState<Record<string, string>>({});
  const [lastUsed, setLastUsed] = useState<string>(
    () => { try { return localStorage.getItem(LAST_FORMAT_KEY) ?? ''; } catch { return ''; } }
  );
  const [signOpen, setSignOpen] = useState(false);

  const handleExport = useCallback(async (card: FormatCard) => {
    setStates(s => ({ ...s, [card.format]: 'loading' }));
    setErrors(e => ({ ...e, [card.format]: '' }));
    setProgress(p => ({ ...p, [card.format]: '' }));

    try {
      if (card.format === 'json') {
        ExportService.exportToJSON(template, pages, sharedElements, pageSettings);
      } else if (card.format === 'pdf') {
        await ExportService.exportToPDF(template, pages, sharedElements, pageSettings,
          msg => setProgress(p => ({ ...p, pdf: msg })));
      } else {
        await ExportService.exportViaBackend(
          card.format, template, pages, sharedElements, pageSettings,
          msg => setProgress(p => ({ ...p, [card.format]: msg })));
      }
      setStates(s => ({ ...s, [card.format]: 'done' }));
      try { localStorage.setItem(LAST_FORMAT_KEY, card.format); } catch {}
      setLastUsed(card.format);
      setTimeout(() => setStates(s => ({ ...s, [card.format]: 'idle' })), 2500);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Export failed';
      setStates(s => ({ ...s, [card.format]: 'error' }));
      setErrors(e => ({ ...e, [card.format]: msg }));
    }
  }, [template, pages, sharedElements, pageSettings]);

  return (
    <div className="export-modal-backdrop" onClick={onClose}>
      <div className="export-modal" onClick={e => e.stopPropagation()} role="dialog" aria-label="Export design">
        <div className="export-modal-header">
          <h2 className="export-modal-title">Export</h2>
          <button className="export-modal-close" onClick={onClose} aria-label="Close"><FiX size={18} /></button>
        </div>

        <div className="export-modal-body">
          {signOpen && <SignDocxModal onClose={() => setSignOpen(false)} />}
          {GROUPS.map(group => {
            const cards = FORMAT_CARDS.filter(c => c.group === group);
            return (
              <div key={group} className="export-group">
                <h3 className="export-group-label">{group}</h3>
                <div className="export-cards">
                  {cards.map(card => {
                    const state = states[card.format] ?? 'idle';
                    const err   = errors[card.format] ?? '';
                    const prog  = progress[card.format] ?? '';
                    return (
                      <div key={card.format} className={`export-card ${state === 'error' ? 'export-card--error' : ''}`}>
                        <div className="export-card-icon">{card.icon}</div>
                        <div className="export-card-info">
                          <span className="export-card-label">
                            {card.label}
                            {lastUsed === card.format && <span className="export-card-last-badge">Last used</span>}
                          </span>
                          <span className="export-card-desc">
                            {state === 'loading' && prog ? prog : err || card.description}
                          </span>
                        </div>
                        <button
                          className={`export-card-btn export-card-btn--${state}`}
                          onClick={() => handleExport(card)}
                          disabled={state === 'loading'}
                          aria-label={`Export as ${card.label}`}
                        >
                          {state === 'loading' ? <FiLoader className="spin" size={15} />
                           : state === 'done'    ? <FiCheck size={15} />
                           : state === 'error'   ? <FiAlertCircle size={15} />
                           : <FiDownload size={15} />}
                          <span>{state === 'done' ? 'Done!' : state === 'error' ? 'Retry' : 'Export'}</span>
                        </button>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>

        <div className="export-modal-footer">
          <button
            className="export-sign-btn"
            onClick={() => setSignOpen(true)}
            title="Apply an X.509 digital signature to a DOCX file"
          >
            <FiShield size={14} />
            Sign DOCX…
          </button>
        </div>
      </div>
    </div>
  );
};

export default ExportModal;
