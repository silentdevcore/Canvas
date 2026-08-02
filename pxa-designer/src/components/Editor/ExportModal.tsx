import React, { useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  FiX, FiDownload, FiLoader, FiCheck, FiAlertCircle,
  FiCode, FiFileText, FiImage, FiGrid, FiHash, FiShield, FiGlobe,
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
  ext: string;
  group: string;
  icon: React.ReactNode;
  serverSide: boolean;
}

const FORMAT_CARDS: FormatCard[] = [
  // Documents
  { format: 'pdf',   ext: '.pdf',  group: 'Documents', icon: <FiFileText />, serverSide: false },
  { format: 'word',  ext: '.docx', group: 'Documents', icon: <FiFileText />, serverSide: true },
  { format: 'odt',   ext: '.odt',  group: 'Documents', icon: <FiFileText />, serverSide: true },
  { format: 'html',  ext: '.html', group: 'Documents', icon: <FiCode />,     serverSide: true },
  // Data
  { format: 'json',  ext: '.json', group: 'Data',      icon: <FiCode />,     serverSide: false },
  { format: 'xml',   ext: '.xml',  group: 'Data',      icon: <FiCode />,     serverSide: true },
  { format: 'excel', ext: '.xlsx', group: 'Data',      icon: <FiGrid />,     serverSide: true },
  { format: 'csv',   ext: '.csv',  group: 'Data',      icon: <FiHash />,     serverSide: true },
  // Images
  { format: 'png',   ext: '.png',  group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'jpeg',  ext: '.jpg',  group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'tiff',  ext: '.tiff', group: 'Images',    icon: <FiImage />,    serverSide: true },
  { format: 'svg',   ext: '.svg',  group: 'Images',    icon: <FiImage />,    serverSide: true },
  // Text
  { format: 'md',    ext: '.md',   group: 'Text',      icon: <FiFileText />, serverSide: true },
];

const GROUPS = ['Documents', 'Data', 'Images', 'Text'];

type ExportState = 'idle' | 'loading' | 'done' | 'error';

const LAST_FORMAT_KEY = 'pxa_export_format';

const ExportModal: React.FC<Props> = ({ template, pages, sharedElements, pageSettings, onClose }) => {
  const { t } = useTranslation('editor');
  const [states, setStates] = useState<Record<string, ExportState>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [progress, setProgress] = useState<Record<string, string>>({});
  const [lastUsed, setLastUsed] = useState<string>(
    () => { try { return localStorage.getItem(LAST_FORMAT_KEY) ?? ''; } catch { return ''; } }
  );
  const [signOpen, setSignOpen] = useState(false);

  const activeLangs = pageSettings?.activeLanguages ?? [];
  const hasMultiLang = activeLangs.length > 1;
  const [multiLangState, setMultiLangState] = useState<ExportState>('idle');
  const [multiLangError, setMultiLangError] = useState('');

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
      const msg = err instanceof Error ? err.message : t('export.exportFailed');
      setStates(s => ({ ...s, [card.format]: 'error' }));
      setErrors(e => ({ ...e, [card.format]: msg }));
    }
  }, [template, pages, sharedElements, pageSettings, t]);

  const handleExportAllLanguages = useCallback(async () => {
    setMultiLangState('loading');
    setMultiLangError('');
    try {
      await ExportService.exportMultiLanguage(template, pages, sharedElements, pageSettings);
      setMultiLangState('done');
      setTimeout(() => setMultiLangState('idle'), 2500);
    } catch (err) {
      setMultiLangError(err instanceof Error ? err.message : t('export.exportFailed'));
      setMultiLangState('error');
    }
  }, [template, pages, sharedElements, pageSettings, t]);

  return (
    <div className="export-modal-backdrop" onClick={onClose}>
      <div className="export-modal" onClick={e => e.stopPropagation()} role="dialog" aria-label={t('export.ariaLabel')}>
        <div className="export-modal-header">
          <h2 className="export-modal-title">{t('export.title')}</h2>
          <button className="export-modal-close" onClick={onClose} aria-label={t('export.close')}><FiX size={18} /></button>
        </div>

        <div className="export-modal-body">
          {signOpen && <SignDocxModal onClose={() => setSignOpen(false)} />}

          {/* Multi-language export section */}
          {hasMultiLang && (
            <div className="export-group">
              <h3 className="export-group-label">{t('export.multiLanguage.sectionLabel')}</h3>
              <div className="export-cards">
                <div className={`export-card ${multiLangState === 'error' ? 'export-card--error' : ''}`}>
                  <div className="export-card-icon"><FiGlobe /></div>
                  <div className="export-card-info">
                    <span className="export-card-label">{t('export.multiLanguage.cardLabel')}</span>
                    <span className="export-card-desc">
                      {multiLangState === 'error' && multiLangError
                        ? multiLangError
                        : t('export.multiLanguage.cardDescription', { languages: activeLangs.join(', ') })}
                    </span>
                  </div>
                  <button
                    className={`export-card-btn export-card-btn--${multiLangState}`}
                    onClick={handleExportAllLanguages}
                    disabled={multiLangState === 'loading'}
                    aria-label={t('export.multiLanguage.exportZipAriaLabel')}
                  >
                    {multiLangState === 'loading' ? <FiLoader className="spin" size={15} />
                     : multiLangState === 'done'  ? <FiCheck size={15} />
                     : multiLangState === 'error' ? <FiAlertCircle size={15} />
                     : <FiDownload size={15} />}
                    <span>{multiLangState === 'done' ? t('export.done') : multiLangState === 'error' ? t('export.retry') : t('export.multiLanguage.exportZip')}</span>
                  </button>
                </div>
              </div>
            </div>
          )}

          {GROUPS.map(group => {
            const cards = FORMAT_CARDS.filter(c => c.group === group);
            return (
              <div key={group} className="export-group">
                <h3 className="export-group-label">{t(`export.groups.${group}`)}</h3>
                <div className="export-cards">
                  {cards.map(card => {
                    const state = states[card.format] ?? 'idle';
                    const err   = errors[card.format] ?? '';
                    const prog  = progress[card.format] ?? '';
                    const label = t(`export.formats.${card.format}.label`);
                    return (
                      <div key={card.format} className={`export-card ${state === 'error' ? 'export-card--error' : ''}`}>
                        <div className="export-card-icon">{card.icon}</div>
                        <div className="export-card-info">
                          <span className="export-card-label">
                            {label}
                            {lastUsed === card.format && <span className="export-card-last-badge">{t('export.lastUsed')}</span>}
                          </span>
                          <span className="export-card-desc">
                            {state === 'loading' && prog ? prog : err || t(`export.formats.${card.format}.description`)}
                          </span>
                        </div>
                        <button
                          className={`export-card-btn export-card-btn--${state}`}
                          onClick={() => handleExport(card)}
                          disabled={state === 'loading'}
                          aria-label={t('export.exportAriaLabel', { label })}
                        >
                          {state === 'loading' ? <FiLoader className="spin" size={15} />
                           : state === 'done'    ? <FiCheck size={15} />
                           : state === 'error'   ? <FiAlertCircle size={15} />
                           : <FiDownload size={15} />}
                          <span>{state === 'done' ? t('export.done') : state === 'error' ? t('export.retry') : t('export.exportAction')}</span>
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
            title={t('export.signDocx.tooltip')}
          >
            <FiShield size={14} />
            {t('export.signDocx.button')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ExportModal;
