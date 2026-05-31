import React, { useRef, useState } from 'react';
import {
  FiFile,
  FiFileText,
  FiImage,
  FiLayout,
  FiPenTool,
  FiUpload,
  FiChevronRight,
} from 'react-icons/fi';
import AppHeader from '@/components/Layout/AppHeader';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';

interface FormatCard {
  id: string;
  label: string;
  extDisplay: string;
  accept: string;
  description: string;
  Icon: React.ElementType;
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
];

const ImporterPage: React.FC = () => {
  const { loadFromFile } = useTemplateLoader();
  const inputRef = useRef<HTMLInputElement>(null);
  const [accept, setAccept] = useState('');
  const [activeId, setActiveId] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState('');

  const handleCardClick = (fmt: FormatCard) => {
    setError('');
    setActiveId(fmt.id);
    setAccept(fmt.accept);
    // Allow React to flush the accept update before triggering click
    setTimeout(() => inputRef.current?.click(), 0);
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setImporting(true);
    setError('');
    try {
      await loadFromFile(file);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed. Please check the file and try again.');
      setImporting(false);
      setActiveId(null);
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
            const Icon = fmt.Icon;
            const isActive = activeId === fmt.id && importing;
            return (
              <button
                key={fmt.id}
                className={`importer-card${isActive ? ' is-loading' : ''}`}
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
            );
          })}
        </div>

        {error && (
          <div className="importer-error" role="alert">
            {error}
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
