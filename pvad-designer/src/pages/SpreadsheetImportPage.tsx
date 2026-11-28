import React, { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { FiUpload, FiGrid } from 'react-icons/fi';
import { SpreadsheetImportError, SpreadsheetService } from '@/services/SpreadsheetService';
import { useOptionalDesignerAuth } from '@/auth/DesignerAuthGate';
import { formatCapability, hasAccountCapability } from '@/auth/designerPermissions';
import { useSpreadsheetStore } from '@/spreadsheet/store';
import { jsonToWorkbook } from '@/spreadsheet/io';
import '@/styles/migrations.css';

interface SpreadsheetImportPageProps {
  /**
   * 'import' (default): accepts account-allowed .xlsx/.csv/.tsv plus native .json, matches the
   * "Import Spreadsheet" sidebar item. 'edit': narrows to the native PVADE
   * Workbook .json format, matching "Edit Spreadsheet" — the closest existing
   * equivalent to Edit PDF's paste-code-then-adjust flow, since no live
   * code editor exists for spreadsheets yet.
   */
  variant?: 'import' | 'edit';
}

/**
 * Imports a spreadsheet file (.xlsx/.csv/.tsv via the backend, or a PVADE
 * Workbook .json) into the spreadsheet model and opens it in the editor.
 */
const SpreadsheetImportPage: React.FC<SpreadsheetImportPageProps> = ({ variant = 'import' }) => {
  const { t } = useTranslation('spreadsheet');
  const { t: common } = useTranslation('common');
  const auth = useOptionalDesignerAuth();
  const navigate = useNavigate();
  const loadWorkbook = useSpreadsheetStore((s) => s.loadWorkbook);
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isEdit = variant === 'edit';
  const formats = isEdit ? ['json'] : [
    ...['xlsx', 'csv', 'tsv'].filter(format => hasAccountCapability(auth?.user, formatCapability('import', format))), 'json',
  ];
  const accept = formats.map(format => `.${format}`).join(',');

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (ext === 'xls' || !formats.includes(ext)) {
      setError(common(ext === 'xls' ? 'permissions.xlsDeferred' : 'permissions.formatDenied'));
      if (fileInput.current) fileInput.current.value = '';
      return;
    }
    setBusy(true);
    setError(null);
    try {
      // External formats are dispatched by extension; .json is the native workbook format.
      const workbook = ext === 'json'
        ? jsonToWorkbook(await file.text())
        : await SpreadsheetService.importXlsx(file);
      loadWorkbook(workbook);
      navigate('/spreadsheet/create');
    } catch (err) {
      const key = err instanceof SpreadsheetImportError ? ({ account: 'accountMessage', role: 'requiredMessage',
        authentication: 'signInMessage', failed: 'importFailed' } as const)[err.kind] : 'importFailed';
      setError(common(`permissions.${key}`));
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  return (
    <div className="mgr-page">
      <main className="mgr-main">
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            <FiGrid className="mgr-heading-icon" />
            <div>
              <h1>{isEdit ? t('import.editHeading') : t('import.importHeading')}</h1>
              {isEdit ? (
                <p><Trans t={t} i18nKey="import.editSubheading" components={{ code: <code /> }} /></p>
              ) : (
                <p><Trans t={t} i18nKey="import.importSubheading" values={{ formats: formats.map(format => `.${format}`).join(', ') }} components={{ code: <code /> }} /></p>
              )}
            </div>
          </div>
        </div>

        <div className="mgr-import-drop">
          <FiUpload size={28} />
          <p>{busy ? t('import.importing') : isEdit ? t('import.chooseWorkbookJson') : t('import.chooseSpreadsheetFile')}</p>
          <button className="mgr-import-btn" disabled={busy} onClick={() => fileInput.current?.click()}>
            {t('import.chooseFile')}
          </button>
          <input
            ref={fileInput}
            type="file"
            accept={accept}
            hidden
            onChange={(e) => onFile(e.target.files?.[0])}
          />
          {error && <p className="mgr-import-error" role="alert">{error}</p>}
        </div>
      </main>
    </div>
  );
};

export default SpreadsheetImportPage;
