import React, { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FiUpload, FiGrid } from 'react-icons/fi';
import { SpreadsheetService } from '@/services/SpreadsheetService';
import { useSpreadsheetStore } from '@/spreadsheet/store';
import { jsonToWorkbook } from '@/spreadsheet/io';
import '@/styles/migrations.css';

interface SpreadsheetImportPageProps {
  /**
   * 'import' (default): accepts .xlsx/.xls/.csv/.tsv/.json, matches the
   * "Import Spreadsheet" sidebar item. 'edit': narrows to the native PXA
   * Workbook .json format, matching "Edit Spreadsheet" — the closest existing
   * equivalent to Edit PDF's paste-code-then-adjust flow, since no live
   * code editor exists for spreadsheets yet.
   */
  variant?: 'import' | 'edit';
}

/**
 * Imports a spreadsheet file (.xlsx/.xls/.csv/.tsv via the backend, or a PXA
 * Workbook .json) into the spreadsheet model and opens it in the editor.
 */
const SpreadsheetImportPage: React.FC<SpreadsheetImportPageProps> = ({ variant = 'import' }) => {
  const navigate = useNavigate();
  const loadWorkbook = useSpreadsheetStore((s) => s.loadWorkbook);
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isEdit = variant === 'edit';

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const ext = file.name.split('.').pop()?.toLowerCase();
      // .xlsx/.xls/.csv/.tsv are dispatched by extension on the backend; .json is the native workbook format.
      const workbook = ext === 'json'
        ? jsonToWorkbook(await file.text())
        : await SpreadsheetService.importXlsx(file);
      loadWorkbook(workbook);
      navigate('/spreadsheet/create');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Import failed. Please check the file and try again.');
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
              <h1>{isEdit ? 'Edit a Workbook JSON' : 'Import Spreadsheet'}</h1>
              {isEdit ? (
                <p>Open a PXA Workbook <code>.json</code> file to edit it in the spreadsheet grid.</p>
              ) : (
                <p>Import an Excel or CSV file (<code>.xlsx</code>, <code>.xls</code>, <code>.csv</code>,
                  {' '}<code>.tsv</code>) — or a PXA Workbook <code>.json</code> — into the spreadsheet editor,
                  preserving formulas, typed values, styles, and merges.</p>
              )}
            </div>
          </div>
        </div>

        <div className="mgr-import-drop">
          <FiUpload size={28} />
          <p>{busy ? 'Importing…' : isEdit ? 'Choose a Workbook JSON file' : 'Choose a spreadsheet file to import'}</p>
          <button className="mgr-import-btn" disabled={busy} onClick={() => fileInput.current?.click()}>
            Choose file
          </button>
          <input
            ref={fileInput}
            type="file"
            accept={isEdit ? '.json' : '.xlsx,.xls,.csv,.tsv,.json'}
            hidden
            onChange={(e) => onFile(e.target.files?.[0])}
          />
          {error && <p className="mgr-import-error">{error}</p>}
        </div>
      </main>
    </div>
  );
};

export default SpreadsheetImportPage;
