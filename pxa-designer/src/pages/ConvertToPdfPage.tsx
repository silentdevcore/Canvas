import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  FiFile,
  FiFileText,
  FiImage,
  FiLayout,
  FiPenTool,
  FiUpload,
  FiChevronRight,
  FiZap,
  FiEye,
  FiDownload,
} from 'react-icons/fi';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';
import ExportService from '@/services/ExportService';

interface PageSizeOption {
  id: string;
  widthPt?: number;
  heightPt?: number;
}

const PAGE_SIZES: PageSizeOption[] = [
  { id: 'original' },
  { id: 'a4',     widthPt: 595, heightPt: 842 },
  { id: 'a5',     widthPt: 420, heightPt: 595 },
  { id: 'a3',     widthPt: 842, heightPt: 1191 },
  { id: 'letter', widthPt: 612, heightPt: 792 },
];

interface FormatCard {
  id: string;
  extDisplay: string;
  accept: string;
  Icon: React.ElementType;
  supportsPageSize?: boolean;
}

const FORMATS: FormatCard[] = [
  {
    id: 'pdf',
    extDisplay: '.pdf',
    accept: '.pdf,application/pdf',
    Icon: FiFileText,
  },
  {
    id: 'docx',
    extDisplay: '.docx',
    accept: '.docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    Icon: FiFile,
  },
  {
    id: 'pptx',
    extDisplay: '.pptx',
    accept: '.pptx,application/vnd.openxmlformats-officedocument.presentationml.presentation',
    Icon: FiLayout,
  },
  {
    id: 'doc',
    extDisplay: '.doc',
    accept: '.doc,application/msword',
    Icon: FiFile,
  },
  {
    id: 'odt',
    extDisplay: '.odt',
    accept: '.odt,application/vnd.oasis.opendocument.text',
    Icon: FiFile,
  },
  {
    id: 'svg',
    extDisplay: '.svg',
    accept: '.svg,image/svg+xml',
    Icon: FiPenTool,
  },
  {
    id: 'image',
    extDisplay: '.png .jpg .gif .webp .bmp .tiff',
    accept: '.png,.jpg,.jpeg,.gif,.webp,.bmp,.tiff,.tif,image/png,image/jpeg,image/gif,image/webp,image/bmp,image/tiff',
    Icon: FiImage,
  },
  {
    id: 'image-analysis',
    extDisplay: '.png .jpg .jpeg',
    accept: '.png,.jpg,.jpeg,image/png,image/jpeg',
    Icon: FiZap,
    supportsPageSize: true,
  },
];

// OCR languages whose Tesseract data files (tessdata_fast) ship with the app. The codes are
// combinable — selecting several joins them with '+', e.g. 'deu+eng+fra'.
const OCR_LANGUAGE_CODES = [
  'eng', 'deu', 'fra', 'spa', 'ita', 'por', 'nld', 'pol', 'swe', 'dan',
  'nor', 'fin', 'ces', 'rus', 'ukr', 'ell', 'chi_sim', 'jpn', 'kor', 'ara',
];

const OCR_ACCEPT = '.png,.jpg,.jpeg,.webp,.bmp,.tiff,.tif,image/png,image/jpeg,image/webp,image/bmp,image/tiff';

const ConvertToPdfPage: React.FC = () => {
  const { t } = useTranslation('convert');
  const { loadFromFile } = useTemplateLoader();

  // ── Section A: convert a file into a PXA design (moved from the old Import PDF page) ──
  const importInputRef = useRef<HTMLInputElement>(null);
  const [importAccept, setImportAccept] = useState('');
  const [importActiveId, setImportActiveId] = useState<string | null>(null);
  const [importConfiguring, setImportConfiguring] = useState<string | null>(null);
  const [importSelectedPageSize, setImportSelectedPageSize] = useState<string>('a4');
  const [includeDiagnostics, setIncludeDiagnostics] = useState(true);
  const [includeDebugOverlay, setIncludeDebugOverlay] = useState(true);
  const [includeFallbackLayer, setIncludeFallbackLayer] = useState(false);
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState('');
  const [importStatus, setImportStatus] = useState('');

  const handleFormatCardClick = (fmt: FormatCard) => {
    setImportError('');
    if (fmt.supportsPageSize) {
      // Show inline configuration panel instead of immediately opening file picker
      setImportConfiguring(fmt.id);
      setImportActiveId(fmt.id);
      setImportAccept(fmt.accept);
    } else {
      setImportConfiguring(null);
      setImportActiveId(fmt.id);
      setImportAccept(fmt.accept);
      setTimeout(() => importInputRef.current?.click(), 0);
    }
  };

  const handleConfirmImportConfig = () => {
    setImportConfiguring(null);
    setTimeout(() => importInputRef.current?.click(), 0);
  };

  const handleImportFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setImporting(true);
    setImportError('');
    setImportStatus(t('sections.import.uploadingFile'));
    try {
      const pageOpt = PAGE_SIZES.find(p => p.id === importSelectedPageSize);
      await loadFromFile(
        file,
        importActiveId ?? undefined,
        pageOpt?.widthPt,
        pageOpt?.heightPt,
        {
          includeImageAnalysisDiagnostics: importActiveId === 'image-analysis' && includeDiagnostics,
          includeImageAnalysisDebugOverlay: importActiveId === 'image-analysis' && includeDebugOverlay,
          includeImageAnalysisFallbackLayer: importActiveId === 'image-analysis' && includeFallbackLayer,
        },
      );
    } catch (err) {
      setImportError(err instanceof Error ? err.message : t('sections.import.importFailed'));
      setImporting(false);
      setImportActiveId(null);
      setImportStatus('');
    }
  };

  // ── Section B: image → searchable PDF via OCR (existing, unchanged behavior) ──
  const ocrInputRef = useRef<HTMLInputElement>(null);
  const [ocrPageSize, setOcrPageSize] = useState<string>('a4');
  const [ocrLanguages, setOcrLanguages] = useState('deu+eng');
  const [enableOcrPreprocessing, setEnableOcrPreprocessing] = useState(true);
  const [ocrLowConfidenceThreshold, setOcrLowConfidenceThreshold] = useState(0.5);
  const [ocrLayoutMode, setOcrLayoutMode] = useState<'text-background' | 'text-only'>('text-background');
  const [converting, setConverting] = useState(false);
  const [ocrError, setOcrError] = useState('');
  const [ocrStatus, setOcrStatus] = useState('');

  const handleOcrFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setConverting(true);
    setOcrError('');
    setOcrStatus(t('sections.ocr.runningOcr'));
    try {
      const pageOpt = PAGE_SIZES.find(p => p.id === ocrPageSize);
      await ExportService.downloadImageOcrPdf(
        file,
        pageOpt?.widthPt,
        pageOpt?.heightPt,
        {
          languages: ocrLanguages,
          includeBackgroundImage: false,
          enablePreprocessing: enableOcrPreprocessing,
          lowConfidenceThreshold: ocrLowConfidenceThreshold,
          layoutMode: ocrLayoutMode,
        },
      );
      setOcrStatus(t('sections.ocr.downloadStarted'));
    } catch (err) {
      setOcrError(err instanceof Error ? err.message : t('sections.ocr.conversionFailed'));
      setOcrStatus('');
    } finally {
      setConverting(false);
    }
  };

  return (
    <div className="importer-page">
      <main className="importer-main">
        <header className="importer-header">
          <span className="importer-eyebrow">
            <FiUpload size={14} />
            {t('eyebrow')}
          </span>
          <h1>{t('heading')}</h1>
          <p>{t('subheading')}</p>
        </header>

        <section className="importer-section">
          <header className="importer-section-header">
            <h2>{t('sections.import.heading')}</h2>
            <p>{t('sections.import.subheading')}</p>
          </header>

          <div className="importer-grid">
            {FORMATS.map(fmt => {
              const Icon      = fmt.Icon;
              const label     = t(`sections.import.formats.${fmt.id}.label`);
              const isActive  = importActiveId === fmt.id && importing;
              const isConfig  = importConfiguring === fmt.id;
              return (
                <div key={fmt.id} className={`importer-card-wrap${isConfig ? ' is-configuring' : ''}`}>
                  <button
                    className={`importer-card${isActive ? ' is-loading' : ''}${isConfig ? ' is-selected' : ''}`}
                    onClick={() => handleFormatCardClick(fmt)}
                    disabled={importing}
                    aria-label={t('sections.import.importAriaLabel', { label })}
                  >
                    <span className="importer-card-icon">
                      <Icon size={24} />
                    </span>
                    <strong className="importer-card-label">{label}</strong>
                    <small className="importer-card-desc">{t(`sections.import.formats.${fmt.id}.description`)}</small>
                    <span className="importer-card-ext">{fmt.extDisplay}</span>
                    <span className="importer-card-action">
                      {isActive ? t('sections.import.importing') : <>{t('sections.import.chooseFile')} <FiChevronRight size={14} /></>}
                    </span>
                  </button>

                  {isConfig && (
                    <div className="importer-config-panel">
                      <div className="importer-config-field">
                        <label className="importer-config-label" htmlFor={`${fmt.id}-page-size-select`}>
                          {t('sections.import.config.pageSize')}
                        </label>
                        <select
                          id={`${fmt.id}-page-size-select`}
                          className="importer-config-select"
                          value={importSelectedPageSize}
                          onChange={e => setImportSelectedPageSize(e.target.value)}
                        >
                          {PAGE_SIZES.map(p => (
                            <option key={p.id} value={p.id}>{t(`pageSizes.${p.id}`)}</option>
                          ))}
                        </select>
                      </div>

                      <label className="importer-config-check">
                        <input
                          type="checkbox"
                          checked={includeDiagnostics}
                          onChange={e => setIncludeDiagnostics(e.target.checked)}
                        />
                        <span>{t('sections.import.config.diagnostics')}</span>
                      </label>
                      <label className="importer-config-check">
                        <input
                          type="checkbox"
                          checked={includeDebugOverlay}
                          onChange={e => setIncludeDebugOverlay(e.target.checked)}
                        />
                        <span>{t('sections.import.config.debugOverlay')}</span>
                      </label>
                      <label className="importer-config-check">
                        <input
                          type="checkbox"
                          checked={includeFallbackLayer}
                          onChange={e => setIncludeFallbackLayer(e.target.checked)}
                        />
                        <span>{t('sections.import.config.fallbackLayer')}</span>
                      </label>

                      <div className="importer-config-actions">
                        <button
                          className="importer-config-cancel"
                          onClick={() => setImportConfiguring(null)}
                        >
                          {t('sections.import.config.cancel')}
                        </button>
                        <button
                          className="importer-config-confirm"
                          onClick={() => handleConfirmImportConfig()}
                        >
                          {t('sections.import.config.chooseFile')} <FiChevronRight size={13} />
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {importError && (
            <div className="importer-error" role="alert">
              {importError}
            </div>
          )}

          {importStatus && (
            <div className="importer-status" role="status" aria-live="polite">
              <span className={importing ? 'importer-status-spinner' : 'importer-status-dot'} />
              {importStatus}
            </div>
          )}

          <input
            ref={importInputRef}
            type="file"
            accept={importAccept}
            style={{ display: 'none' }}
            onChange={handleImportFileChange}
          />
        </section>

        <section className="importer-section">
          <header className="importer-section-header">
            <span className="importer-eyebrow">
              <FiEye size={14} />
              {t('sections.ocr.eyebrow')}
            </span>
            <h2>{t('sections.ocr.heading')}</h2>
            <p>{t('sections.ocr.subheading')}</p>
          </header>

          <div className="importer-config-panel importer-config-panel--standalone">
            <div className="importer-config-field">
              <label className="importer-config-label" htmlFor="convert-page-size-select">{t('sections.ocr.pageSize')}</label>
              <select
                id="convert-page-size-select"
                className="importer-config-select"
                value={ocrPageSize}
                onChange={e => setOcrPageSize(e.target.value)}
              >
                {PAGE_SIZES.map(p => (
                  <option key={p.id} value={p.id}>{t(`pageSizes.${p.id}`)}</option>
                ))}
              </select>
            </div>

            <div className="importer-config-field">
              <label className="importer-config-label" htmlFor="convert-layout-select">{t('sections.ocr.layout')}</label>
              <select
                id="convert-layout-select"
                className="importer-config-select"
                value={ocrLayoutMode}
                onChange={e => setOcrLayoutMode(e.target.value as 'text-background' | 'text-only')}
              >
                <option value="text-background">{t('sections.ocr.layoutFull')}</option>
                <option value="text-only">{t('sections.ocr.layoutTextOnly')}</option>
              </select>
            </div>

            <div className="importer-config-field">
              <label className="importer-config-label" htmlFor="convert-language-select">{t('sections.ocr.ocrLanguageLabel')}</label>
              <select
                id="convert-language-select"
                className="importer-config-select"
                multiple
                size={6}
                value={ocrLanguages.split('+').filter(Boolean)}
                onChange={e => {
                  const codes = Array.from(e.target.selectedOptions, o => o.value);
                  setOcrLanguages(codes.join('+') || 'eng');
                }}
              >
                {OCR_LANGUAGE_CODES.map(code => (
                  <option key={code} value={code}>{t(`sections.ocr.ocrLanguages.${code}`)}</option>
                ))}
              </select>
              <span className="importer-config-hint">
                {t('sections.ocr.ocrLanguageHint')}
              </span>
            </div>

            <label className="importer-config-check">
              <input
                type="checkbox"
                checked={enableOcrPreprocessing}
                onChange={e => setEnableOcrPreprocessing(e.target.checked)}
              />
              <span>{t('sections.ocr.preprocessImage')}</span>
            </label>

            <div className="importer-config-field">
              <label className="importer-config-label" htmlFor="convert-confidence-input">{t('sections.ocr.lowConfidence')}</label>
              <input
                id="convert-confidence-input"
                className="importer-config-input"
                type="number"
                min="0"
                max="1"
                step="0.05"
                value={ocrLowConfidenceThreshold}
                onChange={e => setOcrLowConfidenceThreshold(Number(e.target.value))}
              />
            </div>

            <div className="importer-config-actions">
              <button
                className="importer-config-download"
                style={{ flex: 1 }}
                onClick={() => ocrInputRef.current?.click()}
                disabled={converting}
              >
                <FiDownload size={13} /> {converting ? t('sections.ocr.converting') : t('sections.ocr.chooseImageAndConvert')}
              </button>
            </div>
          </div>

          {ocrError && (
            <div className="importer-error" role="alert">
              {ocrError}
            </div>
          )}

          {ocrStatus && (
            <div className="importer-status" role="status" aria-live="polite">
              <span className={converting ? 'importer-status-spinner' : 'importer-status-dot'} />
              {ocrStatus}
            </div>
          )}

          <input
            ref={ocrInputRef}
            type="file"
            accept={OCR_ACCEPT}
            style={{ display: 'none' }}
            onChange={handleOcrFileChange}
          />
        </section>
      </main>
    </div>
  );
};

export default ConvertToPdfPage;
