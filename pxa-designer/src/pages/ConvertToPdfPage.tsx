import React, { useRef, useState } from 'react';
import { FiEye, FiDownload } from 'react-icons/fi';
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

// OCR languages whose Tesseract data files (tessdata_fast) ship with the app. The codes are
// combinable — selecting several joins them with '+', e.g. 'deu+eng+fra'.
const OCR_LANGUAGES: { code: string; label: string }[] = [
  { code: 'eng',     label: 'English' },
  { code: 'deu',     label: 'German' },
  { code: 'fra',     label: 'French' },
  { code: 'spa',     label: 'Spanish' },
  { code: 'ita',     label: 'Italian' },
  { code: 'por',     label: 'Portuguese' },
  { code: 'nld',     label: 'Dutch' },
  { code: 'pol',     label: 'Polish' },
  { code: 'swe',     label: 'Swedish' },
  { code: 'dan',     label: 'Danish' },
  { code: 'nor',     label: 'Norwegian' },
  { code: 'fin',     label: 'Finnish' },
  { code: 'ces',     label: 'Czech' },
  { code: 'rus',     label: 'Russian' },
  { code: 'ukr',     label: 'Ukrainian' },
  { code: 'ell',     label: 'Greek' },
  { code: 'chi_sim', label: 'Chinese (Simplified)' },
  { code: 'jpn',     label: 'Japanese' },
  { code: 'kor',     label: 'Korean' },
  { code: 'ara',     label: 'Arabic' },
];

const ACCEPT = '.png,.jpg,.jpeg,.webp,.bmp,.tiff,.tif,image/png,image/jpeg,image/webp,image/bmp,image/tiff';

const ConvertToPdfPage: React.FC = () => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [selectedPageSize, setSelectedPageSize] = useState<string>('a4');
  const [ocrLanguages, setOcrLanguages] = useState('deu+eng');
  const [enableOcrPreprocessing, setEnableOcrPreprocessing] = useState(true);
  const [ocrLowConfidenceThreshold, setOcrLowConfidenceThreshold] = useState(0.5);
  const [ocrLayoutMode, setOcrLayoutMode] = useState<'text-background' | 'text-only'>('text-background');
  const [converting, setConverting] = useState(false);
  const [error, setError] = useState('');
  const [status, setStatus] = useState('');

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setConverting(true);
    setError('');
    setStatus('Running OCR and generating PDF…');
    try {
      const pageOpt = PAGE_SIZES.find(p => p.id === selectedPageSize);
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
      setStatus('PDF download started.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Conversion failed. Please check the file and try again.');
      setStatus('');
    } finally {
      setConverting(false);
    }
  };

  return (
    <div className="importer-page">
      <main className="importer-main">
        <header className="importer-header">
          <span className="importer-eyebrow">
            <FiEye size={14} />
            Image to PDF
          </span>
          <h1>Convert an image to PDF</h1>
          <p>Embedded Tesseract OCR turns a scanned or photographed page into a searchable PDF.</p>
        </header>

        <div className="importer-config-panel importer-config-panel--standalone">
          <div className="importer-config-field">
            <label className="importer-config-label" htmlFor="convert-page-size-select">Page size</label>
            <select
              id="convert-page-size-select"
              className="importer-config-select"
              value={selectedPageSize}
              onChange={e => setSelectedPageSize(e.target.value)}
            >
              {PAGE_SIZES.map(p => (
                <option key={p.id} value={p.id}>{p.label}</option>
              ))}
            </select>
          </div>

          <div className="importer-config-field">
            <label className="importer-config-label" htmlFor="convert-layout-select">Layout</label>
            <select
              id="convert-layout-select"
              className="importer-config-select"
              value={ocrLayoutMode}
              onChange={e => setOcrLayoutMode(e.target.value as 'text-background' | 'text-only')}
            >
              <option value="text-background">Full layout (text + background colors)</option>
              <option value="text-only">Text only</option>
            </select>
          </div>

          <div className="importer-config-field">
            <label className="importer-config-label" htmlFor="convert-language-select">OCR language(s)</label>
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
              {OCR_LANGUAGES.map(l => (
                <option key={l.code} value={l.code}>{l.label}</option>
              ))}
            </select>
            <span className="importer-config-hint">
              Hold ⌘/Ctrl to select several — they’re combined (e.g. German + English).
            </span>
          </div>

          <label className="importer-config-check">
            <input
              type="checkbox"
              checked={enableOcrPreprocessing}
              onChange={e => setEnableOcrPreprocessing(e.target.checked)}
            />
            <span>Preprocess image</span>
          </label>

          <div className="importer-config-field">
            <label className="importer-config-label" htmlFor="convert-confidence-input">Low confidence</label>
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
              onClick={() => inputRef.current?.click()}
              disabled={converting}
            >
              <FiDownload size={13} /> {converting ? 'Converting…' : 'Choose image and convert'}
            </button>
          </div>
        </div>

        {error && (
          <div className="importer-error" role="alert">
            {error}
          </div>
        )}

        {status && (
          <div className="importer-status" role="status" aria-live="polite">
            <span className={converting ? 'importer-status-spinner' : 'importer-status-dot'} />
            {status}
          </div>
        )}
      </main>

      <input
        ref={inputRef}
        type="file"
        accept={ACCEPT}
        style={{ display: 'none' }}
        onChange={handleFileChange}
      />
    </div>
  );
};

export default ConvertToPdfPage;
