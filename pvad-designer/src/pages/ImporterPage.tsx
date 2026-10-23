import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FiFileText, FiUpload, FiChevronRight } from 'react-icons/fi';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';

const ACCEPT = '.pdf,application/pdf';

const ImporterPage: React.FC = () => {
  const { t } = useTranslation('importer');
  const { loadFromFile } = useTemplateLoader();
  const inputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState('');
  const [status, setStatus] = useState('');

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    e.target.value = '';
    setImporting(true);
    setError('');
    setStatus(t('uploadingFile'));
    try {
      await loadFromFile(file, 'pdf');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('importFailed'));
      setImporting(false);
      setStatus('');
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

        <div className="importer-grid importer-grid--single">
          <button
            className={`importer-card${importing ? ' is-loading' : ''}`}
            onClick={() => inputRef.current?.click()}
            disabled={importing}
            aria-label={t('chooseFile')}
          >
            <span className="importer-card-icon">
              <FiFileText size={24} />
            </span>
            <strong className="importer-card-label">{t('heading')}</strong>
            <small className="importer-card-desc">{t('subheading')}</small>
            <span className="importer-card-action">
              {importing ? t('importing') : <>{t('chooseFile')} <FiChevronRight size={14} /></>}
            </span>
          </button>
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
        accept={ACCEPT}
        style={{ display: 'none' }}
        onChange={handleFileChange}
      />
    </div>
  );
};

export default ImporterPage;
