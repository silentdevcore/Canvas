import React, { useRef, useState } from 'react';
import {
  FiFile,
  FiFileText,
  FiImage,
  FiLayout,
  FiPenTool,
  FiUpload,
  FiChevronRight,
  FiDownload,
  FiEye,
  FiZap,
} from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';
import ExportService from '@/services/ExportService';

interface PageSizeOption {
  id: string;
  label: string;
  widthPt?: number;
  heightPt?: number;
}

const PAGE_SIZES: PageSizeOption[] = [
  { id: 'original', label: 'Keep original' },
  { id: 'a4',       label: 'A4 (210×297 mm)',  widthPt: 595, heightPt: 842 },
  { id: 'a5',       label: 'A5 (148×210 mm)',  widthPt: 420, heightPt: 595 },
  { id: 'a3',       label: 'A3 (297×420 mm)',  widthPt: 842, heightPt: 1191 },
  { id: 'letter',   label: 'Letter (8.5×11 in)', widthPt: 612, heightPt: 792 },
];

interface FormatCard {
  id: string;
  label: string;
  extDisplay: string;
  accept: string;
  description: string;
  Icon: React.ElementType;
  supportsPageSize?: boolean;
  mode?: 'import' | 'ocr';
}

const FORMATS: FormatCard[] = [
  {
    id: 'pdf',
    label: 'PDF',
    extDisplay: '.pdf',
    accept: '.pdf,application/pdf',
    description: 'Text, shapes, and images extracted from any PDF file.',
    Icon: FiFileText,
  },
  {
    id: 'docx',
    label: 'Word DOCX',
    extDisplay: '.docx',
    accept: '.docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    description: 'Paragraphs, tables, and inline images from Word Open XML.',
    Icon: FiFile,
  },
  {
    id: 'pptx',
    label: 'PowerPoint',
    extDisplay: '.pptx',
    accept: '.pptx,application/vnd.openxmlformats-officedocument.presentationml.presentation',
    description: 'Each slide becomes a Canvas page with shapes, text, and images.',
    Icon: FiLayout,
  },
  {
    id: 'doc',
    label: 'Word DOC',
    extDisplay: '.doc',
    accept: '.doc,application/msword',
    description: 'Legacy Word 97-2003 binary format — text extracted as editable elements.',
    Icon: FiFile,
  },
  {
    id: 'odt',
    label: 'OpenDocument',
    extDisplay: '.odt',
    accept: '.odt,application/vnd.oasis.opendocument.text',
    description: 'ODF text with paragraph styles, headings, and embedded images.',
    Icon: FiFile,
  },
  {
    id: 'svg',
    label: 'SVG',
    extDisplay: '.svg',
    accept: '.svg,image/svg+xml',
    description: 'Full vector fidelity — paths, groups, text, and embedded images.',
    Icon: FiPenTool,
  },
  {
    id: 'image',
    label: 'Image',
    extDisplay: '.png .jpg .gif .webp .bmp .tiff',
    accept: '.png,.jpg,.jpeg,.gif,.webp,.bmp,.tiff,.tif,image/png,image/jpeg,image/gif,image/webp,image/bmp,image/tiff',
    description: 'Raster image placed as a full-page Canvas design.',
    Icon: FiImage,
  },
  {
    id: 'image-analysis',
    label: 'Image (Smart)',
    extDisplay: '.png .jpg .jpeg',
    accept: '.png,.jpg,.jpeg,image/png,image/jpeg',
    description: 'Custom OCR engine — recognises text, shapes, and colours as individual editable elements.',
    Icon: FiZap,
    supportsPageSize: true,
  },
  {
    id: 'image-ocr',
    label: 'Image OCR to PDF',
    extDisplay: '.png .jpg .jpeg .webp .bmp .tiff',
    accept: '.png,.jpg,.jpeg,.webp,.bmp,.tiff,.tif,image/png,image/jpeg,image/webp,image/bmp,image/tiff',
    description: 'Embedded Tesseract OCR with editable text and an optional original image layer.',
    Icon: FiEye,
    supportsPageSize: true,
    mode: 'ocr',
  },
];

const ImporterPage: React.FC = () => {
  const { loadFromFile } = useTemplateLoader();
  const inputRef = useRef<HTMLInputElement>(null);
  const [accept, setAccept] = useState('');
  const [activeId, setActiveId] = useState<string | null>(null);
  const [configuring, setConfiguring] = useState<string | null>(null);
  const [selectedPageSize, setSelectedPageSize] = useState<string>('a4');
  const [includeDiagnostics, setIncludeDiagnostics] = useState(true);
  const [includeDebugOverlay, setIncludeDebugOverlay] = useState(true);
  const [includeFallbackLayer, setIncludeFallbackLayer] = useState(false);
  const [ocrLanguages, setOcrLanguages] = useState('deu+eng');
  const [includeOcrBackgroundImage, setIncludeOcrBackgroundImage] = useState(true);
  const [includeOcrDiagnostics, setIncludeOcrDiagnostics] = useState(true);
  const [includeOcrDebugOverlay, setIncludeOcrDebugOverlay] = useState(false);
  const [enableOcrPreprocessing, setEnableOcrPreprocessing] = useState(false);
  const [ocrLowConfidenceThreshold, setOcrLowConfidenceThreshold] = useState(0.5);
  const [pendingAction, setPendingAction] = useState<'open' | 'download'>('open');
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState('');
  const [status, setStatus] = useState('');

  const handleCardClick = (fmt: FormatCard) => {
    setError('');
    if (fmt.supportsPageSize) {
      // Show inline configuration panel instead of immediately opening file picker
      setConfiguring(fmt.id);
      setActiveId(fmt.id);
      setAccept(fmt.accept);
    } else {
      setConfiguring(null);
      setActiveId(fmt.id);
      setAccept(fmt.accept);
      setTimeout(() => inputRef.current?.click(), 0);
    }
  };

  const handleConfirmConfig = (action: 'open' | 'download' = 'open') => {
    setPendingAction(action);
    setConfiguring(null);
    setTimeout(() => inputRef.current?.click(), 0);
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setImporting(true);
    setError('');
    setStatus(activeId === 'image-ocr' ? 'Uploading image for OCR…' : 'Uploading file…');
    try {
      const pageOpt = PAGE_SIZES.find(p => p.id === selectedPageSize);
      if (activeId === 'image-ocr' && pendingAction === 'download') {
        setStatus('Running OCR and generating PDF…');
        await ExportService.downloadImageOcrPdf(
          file,
          pageOpt?.widthPt,
          pageOpt?.heightPt,
          {
            languages: ocrLanguages,
            includeBackgroundImage: includeOcrBackgroundImage,
            enablePreprocessing: enableOcrPreprocessing,
            lowConfidenceThreshold: ocrLowConfidenceThreshold,
          },
        );
        setImporting(false);
        setActiveId(null);
        setStatus('PDF download started.');
        return;
      }

      if (activeId === 'image-ocr') setStatus('Running OCR and building editable design…');
      await loadFromFile(
        file,
        activeId ?? undefined,
        pageOpt?.widthPt,
        pageOpt?.heightPt,
        {
          includeImageAnalysisDiagnostics: activeId === 'image-analysis' && includeDiagnostics,
          includeImageAnalysisDebugOverlay: activeId === 'image-analysis' && includeDebugOverlay,
          includeImageAnalysisFallbackLayer: activeId === 'image-analysis' && includeFallbackLayer,
          imageOcrLanguages: activeId === 'image-ocr' ? ocrLanguages : undefined,
          includeImageOcrBackgroundImage: activeId === 'image-ocr' ? includeOcrBackgroundImage : undefined,
          includeImageOcrDiagnostics: activeId === 'image-ocr' && includeOcrDiagnostics,
          includeImageOcrDebugOverlay: activeId === 'image-ocr' && includeOcrDebugOverlay,
          enableImageOcrPreprocessing: activeId === 'image-ocr' && enableOcrPreprocessing,
          imageOcrLowConfidenceThreshold: activeId === 'image-ocr' ? ocrLowConfidenceThreshold : undefined,
        },
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed. Please check the file and try again.');
      setImporting(false);
      setActiveId(null);
      setStatus('');
    }
  };

  return (
    <div className="importer-page">
      <AppHeader activePage="importer" />

      <main className="importer-main">
        <header className="importer-header">
          <span className="importer-eyebrow">
            <FiUpload size={14} />
            Open an existing file
          </span>
          <h1>Import a file</h1>
          <p>Choose the format, then select a file — it opens as an editable Canvas design.</p>
        </header>

        <div className="importer-grid">
          {FORMATS.map(fmt => {
            const Icon      = fmt.Icon;
            const isActive  = activeId === fmt.id && importing;
            const isConfig  = configuring === fmt.id;
            return (
              <div key={fmt.id} className={`importer-card-wrap${isConfig ? ' is-configuring' : ''}`}>
                <button
                  className={`importer-card${isActive ? ' is-loading' : ''}${isConfig ? ' is-selected' : ''}`}
                  onClick={() => handleCardClick(fmt)}
                  disabled={importing}
                  aria-label={`Import ${fmt.label} file`}
                >
                  <span className="importer-card-icon">
                    <Icon size={24} />
                  </span>
                  <strong className="importer-card-label">{fmt.label}</strong>
                  <small className="importer-card-desc">{fmt.description}</small>
                  <span className="importer-card-ext">{fmt.extDisplay}</span>
                  <span className="importer-card-action">
                    {isActive ? 'Importing…' : <>Choose file <FiChevronRight size={14} /></>}
                  </span>
                </button>

                {isConfig && (
                  <div className="importer-config-panel">
                    <div className="importer-config-field">
                      <label className="importer-config-label" htmlFor={`${fmt.id}-page-size-select`}>
                        Page size
                      </label>
                      <select
                        id={`${fmt.id}-page-size-select`}
                        className="importer-config-select"
                        value={selectedPageSize}
                        onChange={e => setSelectedPageSize(e.target.value)}
                      >
                        {PAGE_SIZES.map(p => (
                          <option key={p.id} value={p.id}>{p.label}</option>
                        ))}
                      </select>
                    </div>

                    {fmt.id === 'image-ocr' ? (
                      <>
                        <div className="importer-config-field">
                          <label className="importer-config-label" htmlFor="ocr-language-select">
                            OCR language
                          </label>
                          <select
                            id="ocr-language-select"
                            className="importer-config-select"
                            value={ocrLanguages}
                            onChange={e => setOcrLanguages(e.target.value)}
                          >
                            <option value="deu+eng">German + English</option>
                            <option value="deu">German</option>
                            <option value="eng">English</option>
                          </select>
                        </div>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeOcrBackgroundImage}
                            onChange={e => setIncludeOcrBackgroundImage(e.target.checked)}
                          />
                          <span>Original image layer</span>
                        </label>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeOcrDiagnostics}
                            onChange={e => setIncludeOcrDiagnostics(e.target.checked)}
                          />
                          <span>Diagnostics</span>
                        </label>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeOcrDebugOverlay}
                            onChange={e => setIncludeOcrDebugOverlay(e.target.checked)}
                          />
                          <span>Debug overlay page</span>
                        </label>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={enableOcrPreprocessing}
                            onChange={e => setEnableOcrPreprocessing(e.target.checked)}
                          />
                          <span>Preprocess image</span>
                        </label>
                        <div className="importer-config-field">
                          <label className="importer-config-label" htmlFor="ocr-confidence-input">
                            Low confidence
                          </label>
                          <input
                            id="ocr-confidence-input"
                            className="importer-config-input"
                            type="number"
                            min="0"
                            max="1"
                            step="0.05"
                            value={ocrLowConfidenceThreshold}
                            onChange={e => setOcrLowConfidenceThreshold(Number(e.target.value))}
                          />
                        </div>
                      </>
                    ) : (
                      <>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeDiagnostics}
                            onChange={e => setIncludeDiagnostics(e.target.checked)}
                          />
                          <span>Diagnostics</span>
                        </label>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeDebugOverlay}
                            onChange={e => setIncludeDebugOverlay(e.target.checked)}
                          />
                          <span>Debug overlay page</span>
                        </label>
                        <label className="importer-config-check">
                          <input
                            type="checkbox"
                            checked={includeFallbackLayer}
                            onChange={e => setIncludeFallbackLayer(e.target.checked)}
                          />
                          <span>Fallback image layer</span>
                        </label>
                      </>
                    )}
                    <div className="importer-config-actions">
                      <button
                        className="importer-config-cancel"
                        onClick={() => setConfiguring(null)}
                      >
                        Cancel
                      </button>
                      <button
                        className="importer-config-confirm"
                        onClick={() => handleConfirmConfig('open')}
                      >
                        Choose file <FiChevronRight size={13} />
                      </button>
                      {fmt.id === 'image-ocr' && (
                        <button
                          className="importer-config-download"
                          onClick={() => handleConfirmConfig('download')}
                        >
                          <FiDownload size={13} /> PDF
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {error && (
          <div className="importer-error" role="alert">
            {error}
          </div>
        )}

        {status && (
          <div className="importer-status" role="status" aria-live="polite">
            <span className={importing ? 'importer-status-spinner' : 'importer-status-dot'} />
            {status}
          </div>
        )}
      </main>

      <input
        ref={inputRef}
        type="file"
        accept={accept}
        style={{ display: 'none' }}
        onChange={handleFileChange}
      />
    </div>
  );
};

export default ImporterPage;
