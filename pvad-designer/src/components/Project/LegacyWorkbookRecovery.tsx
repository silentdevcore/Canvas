import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FiDownload, FiRotateCcw, FiX } from 'react-icons/fi';
import { findLegacyWorkbookDrafts, legacyWorkbookJson, type LegacyWorkbookDraft } from '@/spreadsheet/legacyRecovery';
import { downloadText } from '@/spreadsheet/io';

export default function LegacyWorkbookRecovery() {
  const { t } = useTranslation('spreadsheet');
  const [drafts, setDrafts] = useState<LegacyWorkbookDraft[] | null>(null);
  const [error, setError] = useState(false);
  const discover = () => {
    try { setDrafts(findLegacyWorkbookDrafts(localStorage)); setError(false); }
    catch { setError(true); }
  };
  const download = (draft: LegacyWorkbookDraft) => {
    try {
      downloadText(legacyWorkbookJson(draft), 'recovered-workbook.json', 'application/json');
      setError(false);
    } catch { setError(true); }
  };
  return <aside className="legacy-workbook-recovery">
    {drafts === null ? <button className="ss-tool" type="button" onClick={discover}><FiRotateCcw aria-hidden="true" />{t('legacyRecovery.find')}</button> : <>
      <header><strong>{t('legacyRecovery.title')}</strong>
        <button className="ss-tool" type="button" title={t('legacyRecovery.close')} aria-label={t('legacyRecovery.close')}
          onClick={() => { setDrafts(null); setError(false); }}><FiX /></button>
      </header>
      <p>{t('legacyRecovery.notice')}</p>
      {!drafts.length && <p>{t('legacyRecovery.empty')}</p>}
      {drafts.map((draft, index) => <button className="ss-tool" type="button" key={draft.key} onClick={() => download(draft)}>
        <FiDownload aria-hidden="true" /> {t('legacyRecovery.download', { number: index + 1 })}
      </button>)}
    </>}
    {error && <p role="alert">{t('legacyRecovery.failed')}</p>}
  </aside>;
}
