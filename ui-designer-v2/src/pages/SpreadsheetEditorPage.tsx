import React, { useRef, useState } from 'react';
import {
  FiBold, FiItalic, FiAlignLeft, FiAlignCenter, FiAlignRight,
  FiRotateCcw, FiRotateCw, FiUpload, FiDownload, FiPlus, FiX,
} from 'react-icons/fi';
import AppHeader from '../components/Layout/AppHeader';
import { SpreadsheetGrid, colName } from '../spreadsheet/SpreadsheetGrid';
import { useSpreadsheetStore } from '../spreadsheet/store';
import { SpreadsheetService } from '../services/SpreadsheetService';
import { workbookToWire } from '../spreadsheet/types';
import { sheetToCsv, csvToSheet, workbookToJson, jsonToWorkbook, downloadText } from '../spreadsheet/io';
import '../styles/spreadsheet.css';

const NUMBER_FORMATS: { label: string; value: string | undefined }[] = [
  { label: 'General', value: undefined },
  { label: 'Number', value: '#,##0.00' },
  { label: 'Currency', value: '"€"#,##0.00' },
  { label: 'Percent', value: '0.00%' },
  { label: 'Date', value: 'dd.MM.yyyy' },
];

const FONT_FAMILIES = ['Arial', 'Helvetica', 'Times New Roman', 'Georgia', 'Courier New', 'Verdana', 'Calibri'];
const FONT_SIZES = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36];

const round = (n: number) => (Number.isInteger(n) ? n : Math.round(n * 10000) / 10000);

const SpreadsheetEditorPage: React.FC = () => {
  const sheets = useSpreadsheetStore((s) => s.sheets);
  const active = useSpreadsheetStore((s) => s.active);
  const name = useSpreadsheetStore((s) => s.name);
  const computed = useSpreadsheetStore((s) => s.computed);
  const selection = useSpreadsheetStore((s) => s.selection);
  const cellAt = useSpreadsheetStore((s) => s.cellAt);
  const setCellInput = useSpreadsheetStore((s) => s.setCellInput);
  const applyStyle = useSpreadsheetStore((s) => s.applyStyle);
  const applyNumberFormat = useSpreadsheetStore((s) => s.applyNumberFormat);
  const selectionStats = useSpreadsheetStore((s) => s.selectionStats);
  const range = useSpreadsheetStore((s) => s.range);
  const insertRow = useSpreadsheetStore((s) => s.insertRow);
  const deleteRow = useSpreadsheetStore((s) => s.deleteRow);
  const insertCol = useSpreadsheetStore((s) => s.insertCol);
  const deleteCol = useSpreadsheetStore((s) => s.deleteCol);
  const addSheet = useSpreadsheetStore((s) => s.addSheet);
  const setActive = useSpreadsheetStore((s) => s.setActive);
  const renameSheet = useSpreadsheetStore((s) => s.renameSheet);
  const deleteSheet = useSpreadsheetStore((s) => s.deleteSheet);
  const loadWorkbook = useSpreadsheetStore((s) => s.loadWorkbook);
  const toWire = useSpreadsheetStore((s) => s.toWire);
  const undo = useSpreadsheetStore((s) => s.undo);
  const redo = useSpreadsheetStore((s) => s.redo);

  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [exportMenu, setExportMenu] = useState(false);

  const { row, col } = selection;
  const cell = cellAt(row, col);
  const stats = selectionStats(); // recomputes on sheets/selection/range change (all subscribed)
  const rangeLabel = range ? `${colName(Math.min(range.c0, range.c1))}${Math.min(range.r0, range.r1) + 1}:${colName(Math.max(range.c0, range.c1))}${Math.max(range.r0, range.r1) + 1}` : null;
  const formulaBarValue = cell?.type === 'formula' ? (cell.formula ?? '') : (cell?.value != null ? String(cell.value) : '');
  const [editing, setEditing] = useState<string | null>(null);
  const barValue = editing ?? formulaBarValue;

  const commitBar = () => {
    if (editing != null) setCellInput(row, col, editing);
    setEditing(null);
  };

  const safeName = (name || 'workbook').replace(/[\\/:*?"<>|]/g, '_');

  const onImport = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy('Importing…');
    try {
      const ext = file.name.toLowerCase().split('.').pop();
      if (ext === 'xlsx') {
        loadWorkbook(await SpreadsheetService.importXlsx(file));
      } else if (ext === 'csv') {
        const sheet = csvToSheet(await file.text(), 'Sheet1');
        loadWorkbook(workbookToWire(file.name.replace(/\.csv$/i, ''), [sheet]));
      } else if (ext === 'json') {
        loadWorkbook(jsonToWorkbook(await file.text()));
      } else {
        alert('Unsupported file. Use .xlsx, .csv, or .json.');
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Import failed');
    } finally {
      setBusy(null);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  const exportAs = async (fmt: 'xlsx' | 'csv' | 'json') => {
    setExportMenu(false);
    setBusy('Exporting…');
    try {
      if (fmt === 'xlsx') {
        await SpreadsheetService.exportXlsx(toWire());
      } else if (fmt === 'csv') {
        downloadText(sheetToCsv(sheets[active], computed), `${safeName}.csv`, 'text/csv;charset=utf-8');
      } else {
        downloadText(workbookToJson(toWire()), `${safeName}.json`, 'application/json');
      }
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Export failed');
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="spreadsheet-root">
      <AppHeader activePage="spreadsheet" />

      <div className="spreadsheet-toolbar">
        <button className="ss-tool" title="Undo" onClick={undo}><FiRotateCcw /></button>
        <button className="ss-tool" title="Redo" onClick={redo}><FiRotateCw /></button>
        <span className="ss-sep" />
        <select className="ss-format" title="Font" value={cell?.style?.fontFamily ?? ''} onChange={(e) => applyStyle({ fontFamily: e.target.value || undefined })}>
          <option value="">Font</option>
          {FONT_FAMILIES.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>
        <select className="ss-format ss-format--sm" title="Font size" value={cell?.style?.fontSize ?? ''} onChange={(e) => applyStyle({ fontSize: e.target.value ? Number(e.target.value) : undefined })}>
          <option value="">Size</option>
          {FONT_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <button className="ss-tool" title="Bold" onClick={() => applyStyle({ bold: !cell?.style?.bold })}><FiBold /></button>
        <button className="ss-tool" title="Italic" onClick={() => applyStyle({ italic: !cell?.style?.italic })}><FiItalic /></button>
        <label className="ss-color" title="Text color"><span>A</span>
          <input type="color" value={cell?.style?.color ?? '#111827'} onChange={(e) => applyStyle({ color: e.target.value })} />
        </label>
        <label className="ss-color ss-color--fill" title="Fill color"><span>▦</span>
          <input type="color" value={cell?.style?.backgroundColor ?? '#ffffff'} onChange={(e) => applyStyle({ backgroundColor: e.target.value })} />
        </label>
        <span className="ss-sep" />
        <button className="ss-tool" title="Align left" onClick={() => applyStyle({ textAlign: 'left' })}><FiAlignLeft /></button>
        <button className="ss-tool" title="Align center" onClick={() => applyStyle({ textAlign: 'center' })}><FiAlignCenter /></button>
        <button className="ss-tool" title="Align right" onClick={() => applyStyle({ textAlign: 'right' })}><FiAlignRight /></button>
        <span className="ss-sep" />
        <select
          className="ss-format"
          title="Number format"
          value={cell?.numberFormat ?? ''}
          onChange={(e) => applyNumberFormat(e.target.value || undefined)}
        >
          {NUMBER_FORMATS.map((f) => <option key={f.label} value={f.value ?? ''}>{f.label}</option>)}
        </select>
        <span className="ss-sep" />
        <button className="ss-tool ss-tool--text" title="Insert row above" onClick={() => insertRow(row)}>+Row</button>
        <button className="ss-tool ss-tool--text" title="Delete row" onClick={() => deleteRow(row)}>−Row</button>
        <button className="ss-tool ss-tool--text" title="Insert column left" onClick={() => insertCol(col)}>+Col</button>
        <button className="ss-tool ss-tool--text" title="Delete column" onClick={() => deleteCol(col)}>−Col</button>
        <span className="ss-spacer" />
        {busy && <span className="ss-busy">{busy}</span>}
        <button className="ss-tool ss-tool--text" onClick={() => fileInput.current?.click()}><FiUpload /> Import</button>
        <div className="ss-export">
          <button className="ss-tool ss-tool--text ss-tool--primary" onClick={() => setExportMenu((v) => !v)}><FiDownload /> Export ▾</button>
          {exportMenu && (
            <div className="ss-menu" onMouseLeave={() => setExportMenu(false)}>
              <button onClick={() => exportAs('xlsx')}>Excel (.xlsx)</button>
              <button onClick={() => exportAs('csv')}>CSV (.csv)</button>
              <button onClick={() => exportAs('json')}>JSON (.json)</button>
            </div>
          )}
        </div>
        <input ref={fileInput} type="file" accept=".xlsx,.csv,.json" hidden onChange={onImport} />
      </div>

      <div className="spreadsheet-formula-bar">
        <span className="ss-namebox">{colName(col)}{row + 1}</span>
        <input
          className="ss-formula-input"
          value={barValue}
          placeholder="Enter a value or =formula"
          onChange={(e) => setEditing(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { commitBar(); (e.target as HTMLInputElement).blur(); } if (e.key === 'Escape') setEditing(null); }}
          onBlur={commitBar}
        />
      </div>

      <div className="spreadsheet-grid-wrap">
        <SpreadsheetGrid />
      </div>

      <div className="spreadsheet-tabs">
        {sheets.map((s, i) => (
          <div key={s.id} className={`ss-tab${i === active ? ' is-active' : ''}`}>
            <button
              className="ss-tab-name"
              onClick={() => setActive(i)}
              onDoubleClick={() => { const n = prompt('Rename sheet', s.name); if (n) renameSheet(i, n); }}
            >
              {s.name}
            </button>
            {sheets.length > 1 && (
              <button className="ss-tab-close" title="Delete sheet" onClick={() => { if (confirm(`Delete "${s.name}"?`)) deleteSheet(i); }}><FiX /></button>
            )}
          </div>
        ))}
        <button className="ss-tab-add" title="Add sheet" onClick={addSheet}><FiPlus /></button>
        <span className="ss-spacer" />
        {stats.count > 0 && (
          <span className="ss-stats">
            {rangeLabel && <span className="ss-stats-range">{rangeLabel}</span>}
            <span>Sum: <strong>{round(stats.sum)}</strong></span>
            <span>Avg: <strong>{stats.avg != null ? round(stats.avg) : '—'}</strong></span>
            <span>Count: <strong>{stats.count}</strong></span>
          </span>
        )}
      </div>
    </div>
  );
};

export default SpreadsheetEditorPage;
