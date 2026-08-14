import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  FiBold, FiItalic, FiAlignLeft, FiAlignCenter, FiAlignRight,
  FiRotateCcw, FiRotateCw, FiUpload, FiDownload, FiPlus, FiX,
  FiImage,
} from 'react-icons/fi';
import { SpreadsheetGrid, colName } from '../spreadsheet/SpreadsheetGrid';
import { useSpreadsheetStore } from '../spreadsheet/store';
import { SpreadsheetService, type ValidationResult } from '../services/SpreadsheetService';
import { workbookToWire, toA1, toA1Range } from '../spreadsheet/types';
import { sheetToCsv, csvToSheet, workbookToJson, jsonToWorkbook, downloadText } from '../spreadsheet/io';
import { notify } from '@/notifications/toast';
import { uploadDesignerImage } from '@/services/designerAssetApi';
import '../styles/spreadsheet.css';

const NUMBER_FORMAT_DEFS: { key: string; value: string | undefined }[] = [
  { key: 'general', value: undefined },
  { key: 'number', value: '#,##0.00' },
  { key: 'currency', value: '"€"#,##0.00' },
  { key: 'percent', value: '0.00%' },
  { key: 'date', value: 'dd.MM.yyyy' },
];

const FONT_FAMILIES = ['Arial', 'Helvetica', 'Times New Roman', 'Georgia', 'Courier New', 'Verdana', 'Calibri'];
const FONT_SIZES = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36];
const CF_OPERATORS = ['greaterThan', 'lessThan', 'equalTo', 'between', 'contains'];
const DV_OPERATORS = ['between', 'greaterThan', 'lessThan', 'equalTo'];
const DV_TYPES = ['list', 'wholeNumber', 'decimal', 'textLength'];

const round = (n: number) => (Number.isInteger(n) ? n : Math.round(n * 10000) / 10000);

const SpreadsheetEditorPage: React.FC = () => {
  const { t } = useTranslation('spreadsheet');
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
  const mergeSelection = useSpreadsheetStore((s) => s.mergeSelection);
  const unmergeSelection = useSpreadsheetStore((s) => s.unmergeSelection);
  const setFrozen = useSpreadsheetStore((s) => s.setFrozen);
  const addSheet = useSpreadsheetStore((s) => s.addSheet);
  const setActive = useSpreadsheetStore((s) => s.setActive);
  const renameSheet = useSpreadsheetStore((s) => s.renameSheet);
  const deleteSheet = useSpreadsheetStore((s) => s.deleteSheet);
  const loadWorkbook = useSpreadsheetStore((s) => s.loadWorkbook);
  const toWire = useSpreadsheetStore((s) => s.toWire);
  const setCellMeta = useSpreadsheetStore((s) => s.setCellMeta);
  const patchSheet = useSpreadsheetStore((s) => s.patchSheet);
  const addConditionalFormat = useSpreadsheetStore((s) => s.addConditionalFormat);
  const removeConditionalFormat = useSpreadsheetStore((s) => s.removeConditionalFormat);
  const addDataValidation = useSpreadsheetStore((s) => s.addDataValidation);
  const removeDataValidation = useSpreadsheetStore((s) => s.removeDataValidation);
  const addImage = useSpreadsheetStore((s) => s.addImage);
  const removeImage = useSpreadsheetStore((s) => s.removeImage);
  const undo = useSpreadsheetStore((s) => s.undo);
  const redo = useSpreadsheetStore((s) => s.redo);

  const fileInput = useRef<HTMLInputElement>(null);
  const imageInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [exportMenu, setExportMenu] = useState(false);
  const [cellMenu, setCellMenu] = useState(false);
  const [sheetMenu, setSheetMenu] = useState(false);
  const [rulesMenu, setRulesMenu] = useState(false);
  const [validation, setValidation] = useState<ValidationResult | null>(null);
  const [cf, setCf] = useState({ type: 'cellIs', operator: 'greaterThan', value: '', value2: '', color: '#ffeb3b' });
  const [dv, setDv] = useState({ type: 'list', operator: 'between', value1: '', value2: '', listSource: '' });

  const { row, col } = selection;
  const cell = cellAt(row, col);
  const sheet = sheets[active];
  const pageSetup = sheet?.pageSetup ?? {};
  const selRange = range ? toA1Range(range.r0, range.c0, range.r1, range.c1) : toA1(row, col);
  const stats = selectionStats(); // recomputes on sheets/selection/range change (all subscribed)
  const rangeLabel = range ? `${colName(Math.min(range.c0, range.c1))}${Math.min(range.r0, range.r1) + 1}:${colName(Math.max(range.c0, range.c1))}${Math.max(range.r0, range.r1) + 1}` : null;
  const formulaBarValue = cell?.type === 'formula' ? (cell.formula ?? '') : (cell?.value != null ? String(cell.value) : '');
  const [editing, setEditing] = useState<string | null>(null);
  const barValue = editing ?? formulaBarValue;

  const commitBar = () => {
    if (editing != null) setCellInput(row, col, editing);
    setEditing(null);
  };

  const onImageUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    setBusy(t('editor.uploadingImage'));
    try {
      const asset = await uploadDesignerImage(file);
      addImage({
        id: crypto.randomUUID(),
        assetId: asset.id,
        fileName: asset.fileName ?? file.name,
        contentType: asset.contentType,
        contentUrl: asset.contentUrl,
        row,
        col,
        width: Math.min(asset.width ?? 160, 320),
        height: Math.min(asset.height ?? 90, 180),
        altText: asset.fileName ?? file.name,
      });
      notify.success(t('editor.imageStored'));
    } catch (error) {
      notify.error(error instanceof Error ? error.message : t('editor.imageUploadFailed'));
    } finally {
      setBusy(null);
      event.target.value = '';
    }
  };

  const safeName = (name || 'workbook').replace(/[\\/:*?"<>|]/g, '_');

  const onImport = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(t('editor.importing'));
    try {
      const ext = file.name.toLowerCase().split('.').pop();
      if (ext === 'xlsx') {
        loadWorkbook(await SpreadsheetService.importXlsx(file));
      } else if (ext === 'csv') {
        const sheet = csvToSheet(await file.text(), 'Sheet1');
        loadWorkbook(workbookToWire(file.name.replace(/\.csv$/i, ''), [sheet]));
      } else if (ext === 'json') {
        loadWorkbook(await SpreadsheetService.storeEmbeddedImages(jsonToWorkbook(await file.text())));
      } else {
        notify.warning(t('editor.unsupportedFile'));
      }
    } catch (err) {
      notify.error(err instanceof Error ? err.message : t('editor.importFailed'));
    } finally {
      setBusy(null);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  const exportAs = async (fmt: 'xlsx' | 'csv' | 'json') => {
    setExportMenu(false);
    setBusy(t('editor.exporting'));
    try {
      if (fmt === 'xlsx') {
        await SpreadsheetService.exportXlsx(toWire());
      } else if (fmt === 'csv') {
        downloadText(sheetToCsv(sheets[active], computed), `${safeName}.csv`, 'text/csv;charset=utf-8');
      } else {
        downloadText(workbookToJson(toWire()), `${safeName}.json`, 'application/json');
      }
    } catch (err) {
      notify.error(err instanceof Error ? err.message : t('editor.exportFailed'));
    } finally {
      setBusy(null);
    }
  };

  const validate = async () => {
    setBusy(t('editor.validating'));
    try {
      setValidation(await SpreadsheetService.validate(toWire()));
    } catch (err) {
      notify.error(err instanceof Error ? err.message : t('editor.validationFailed'));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="spreadsheet-root">
      <div className="spreadsheet-toolbar">
        <button className="ss-tool" title={t('editor.undo')} onClick={undo}><FiRotateCcw /></button>
        <button className="ss-tool" title={t('editor.redo')} onClick={redo}><FiRotateCw /></button>
        <span className="ss-sep" />
        <select className="ss-format" title={t('editor.font')} value={cell?.style?.fontFamily ?? ''} onChange={(e) => applyStyle({ fontFamily: e.target.value || undefined })}>
          <option value="">{t('editor.font')}</option>
          {FONT_FAMILIES.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>
        <select className="ss-format ss-format--sm" title={t('editor.fontSize')} value={cell?.style?.fontSize ?? ''} onChange={(e) => applyStyle({ fontSize: e.target.value ? Number(e.target.value) : undefined })}>
          <option value="">{t('editor.size')}</option>
          {FONT_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <button className="ss-tool" title={t('editor.bold')} onClick={() => applyStyle({ bold: !cell?.style?.bold })}><FiBold /></button>
        <button className="ss-tool" title={t('editor.italic')} onClick={() => applyStyle({ italic: !cell?.style?.italic })}><FiItalic /></button>
        <label className="ss-color" title={t('editor.textColor')}><span>A</span>
          <input type="color" value={cell?.style?.color ?? '#111827'} onChange={(e) => applyStyle({ color: e.target.value })} />
        </label>
        <label className="ss-color ss-color--fill" title={t('editor.fillColor')}><span>▦</span>
          <input type="color" value={cell?.style?.backgroundColor ?? '#ffffff'} onChange={(e) => applyStyle({ backgroundColor: e.target.value })} />
        </label>
        <span className="ss-sep" />
        <button className="ss-tool" title={t('editor.alignLeft')} onClick={() => applyStyle({ textAlign: 'left' })}><FiAlignLeft /></button>
        <button className="ss-tool" title={t('editor.alignCenter')} onClick={() => applyStyle({ textAlign: 'center' })}><FiAlignCenter /></button>
        <button className="ss-tool" title={t('editor.alignRight')} onClick={() => applyStyle({ textAlign: 'right' })}><FiAlignRight /></button>
        <span className="ss-sep" />
        <select
          className="ss-format"
          title={t('editor.numberFormat')}
          value={cell?.numberFormat ?? ''}
          onChange={(e) => applyNumberFormat(e.target.value || undefined)}
        >
          {NUMBER_FORMAT_DEFS.map((f) => <option key={f.key} value={f.value ?? ''}>{t(`editor.numberFormats.${f.key}`)}</option>)}
        </select>
        <span className="ss-sep" />
        <button className="ss-tool ss-tool--text" title={t('editor.insertRowAbove')} onClick={() => insertRow(row)}>{t('editor.insertRow')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.deleteRowTitle')} onClick={() => deleteRow(row)}>{t('editor.deleteRow')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.insertColLeft')} onClick={() => insertCol(col)}>{t('editor.insertCol')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.deleteColTitle')} onClick={() => deleteCol(col)}>{t('editor.deleteCol')}</button>
        <span className="ss-sep" />
        <button className="ss-tool ss-tool--text" title={t('editor.mergeSelectedCells')} onClick={mergeSelection}>{t('editor.merge')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.unmerge')} onClick={unmergeSelection}>{t('editor.unmerge')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.freezeTitle')} onClick={() => setFrozen(row, col)}>{t('editor.freeze')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.unfreeze')} onClick={() => setFrozen(0, 0)}>{t('editor.unfreeze')}</button>
        <span className="ss-sep" />
        <div className="ss-export">
          <button className="ss-tool ss-tool--text" title={t('editor.cellMenuTitle')} onClick={() => { setCellMenu((v) => !v); setSheetMenu(false); }}>{t('editor.cellMenu')}</button>
          {cellMenu && (
            <div className="ss-menu ss-menu--panel" onMouseLeave={() => setCellMenu(false)}>
              <label className="ss-field">{t('editor.comment')}
                <textarea rows={2} value={cell?.comment ?? ''} onChange={(e) => setCellMeta(row, col, { comment: e.target.value, hyperlink: cell?.hyperlink })} />
              </label>
              <label className="ss-field">{t('editor.hyperlink')}
                <input type="text" placeholder={t('editor.hyperlinkPlaceholder')} value={cell?.hyperlink ?? ''} onChange={(e) => setCellMeta(row, col, { hyperlink: e.target.value, comment: cell?.comment })} />
              </label>
            </div>
          )}
        </div>
        <div className="ss-export">
          <button className="ss-tool ss-tool--text" title={t('editor.sheetMenuTitle')} onClick={() => { setSheetMenu((v) => !v); setCellMenu(false); }}>{t('editor.sheetMenu')}</button>
          {sheetMenu && (
            <div className="ss-menu ss-menu--panel" onMouseLeave={() => setSheetMenu(false)}>
              <label className="ss-field">{t('editor.orientation')}
                <select value={pageSetup.orientation ?? 'portrait'} onChange={(e) => patchSheet({ pageSetup: { ...pageSetup, orientation: e.target.value } })}>
                  <option value="portrait">{t('editor.portrait')}</option>
                  <option value="landscape">{t('editor.landscape')}</option>
                </select>
              </label>
              <label className="ss-field">{t('editor.header')}
                <input type="text" value={pageSetup.header ?? ''} onChange={(e) => patchSheet({ pageSetup: { ...pageSetup, header: e.target.value } })} />
              </label>
              <label className="ss-field">{t('editor.footer')}
                <input type="text" value={pageSetup.footer ?? ''} onChange={(e) => patchSheet({ pageSetup: { ...pageSetup, footer: e.target.value } })} />
              </label>
              <label className="ss-field">{t('editor.autoFilterRange')}
                <input type="text" placeholder="A1:D20" value={sheet?.autoFilterRange ?? ''} onChange={(e) => patchSheet({ autoFilterRange: e.target.value || undefined })} />
              </label>
              <label className="ss-field ss-field--row">
                <input type="checkbox" checked={sheet?.protection?.protected ?? false} onChange={(e) => patchSheet({ protection: e.target.checked ? { protected: true } : undefined })} />
                {t('editor.protectSheet')}
              </label>
            </div>
          )}
        </div>
        <div className="ss-export">
          <button className="ss-tool ss-tool--text" title={t('editor.rulesMenuTitle')} onClick={() => { setRulesMenu((v) => !v); setCellMenu(false); setSheetMenu(false); }}>{t('editor.rulesMenu')}</button>
          {rulesMenu && (
            <div className="ss-menu ss-menu--panel ss-menu--wide" onMouseLeave={() => setRulesMenu(false)}>
              <div className="ss-rule-head">{t('editor.conditionalFormatting')}</div>
              {(sheet?.conditionalFormats ?? []).map((r, i) => (
                <div key={i} className="ss-rule-row">
                  <span className="ss-rule-swatch" style={{ background: r.color ?? '#ccc' }} />
                  <span className="ss-rule-text">{r.range} · {r.type}{r.operator ? ` ${r.operator}` : ''}{r.value ? ` ${r.value}` : ''}</span>
                  <button className="ss-rule-del" title={t('editor.removeRule')} onClick={() => removeConditionalFormat(i)}><FiX /></button>
                </div>
              ))}
              <div className="ss-rule-form">
                <select value={cf.type} onChange={(e) => setCf({ ...cf, type: e.target.value })}>
                  <option value="cellIs">{t('editor.cellIs')}</option>
                  <option value="colorScale">{t('editor.colorScale')}</option>
                </select>
                {cf.type === 'cellIs' && (
                  <select value={cf.operator} onChange={(e) => setCf({ ...cf, operator: e.target.value })}>
                    {CF_OPERATORS.map((o) => <option key={o} value={o}>{t(`editor.operators.${o}`)}</option>)}
                  </select>
                )}
                {cf.type === 'cellIs' && <input type="text" placeholder={t('editor.valuePlaceholder')} value={cf.value} onChange={(e) => setCf({ ...cf, value: e.target.value })} />}
                {cf.type === 'cellIs' && cf.operator === 'between' && <input type="text" placeholder={t('editor.andPlaceholder')} value={cf.value2} onChange={(e) => setCf({ ...cf, value2: e.target.value })} />}
                <input type="color" value={cf.color} onChange={(e) => setCf({ ...cf, color: e.target.value })} />
                <button className="ss-rule-add" onClick={() => addConditionalFormat({
                  range: selRange, type: cf.type,
                  operator: cf.type === 'cellIs' ? cf.operator : undefined,
                  value: cf.type === 'cellIs' ? (cf.value || undefined) : undefined,
                  value2: cf.type === 'cellIs' && cf.operator === 'between' ? (cf.value2 || undefined) : undefined,
                  color: cf.color,
                })}>{t('editor.addRuleTo', { range: selRange })}</button>
              </div>

              <div className="ss-rule-head">{t('editor.dataValidation')}</div>
              {(sheet?.dataValidations ?? []).map((r, i) => (
                <div key={i} className="ss-rule-row">
                  <span className="ss-rule-text">{r.range} · {r.type}{r.listSource ? ` [${r.listSource}]` : r.operator ? ` ${r.operator}` : ''}</span>
                  <button className="ss-rule-del" title={t('editor.removeRule')} onClick={() => removeDataValidation(i)}><FiX /></button>
                </div>
              ))}
              <div className="ss-rule-form">
                <select value={dv.type} onChange={(e) => setDv({ ...dv, type: e.target.value })}>
                  {DV_TYPES.map((ty) => <option key={ty} value={ty}>{t(`editor.validationTypes.${ty}`)}</option>)}
                </select>
                {dv.type === 'list'
                  ? <input type="text" placeholder={t('editor.listPlaceholder')} value={dv.listSource} onChange={(e) => setDv({ ...dv, listSource: e.target.value })} />
                  : (
                    <>
                      <select value={dv.operator} onChange={(e) => setDv({ ...dv, operator: e.target.value })}>
                        {DV_OPERATORS.map((o) => <option key={o} value={o}>{t(`editor.operators.${o}`)}</option>)}
                      </select>
                      <input type="text" placeholder={t('editor.valuePlaceholder')} value={dv.value1} onChange={(e) => setDv({ ...dv, value1: e.target.value })} />
                      {dv.operator === 'between' && <input type="text" placeholder={t('editor.andPlaceholder')} value={dv.value2} onChange={(e) => setDv({ ...dv, value2: e.target.value })} />}
                    </>
                  )}
                <button className="ss-rule-add" onClick={() => addDataValidation({
                  range: selRange, type: dv.type,
                  listSource: dv.type === 'list' ? (dv.listSource || undefined) : undefined,
                  operator: dv.type !== 'list' ? dv.operator : undefined,
                  value1: dv.type !== 'list' ? (dv.value1 || undefined) : undefined,
                  value2: dv.type !== 'list' && dv.operator === 'between' ? (dv.value2 || undefined) : undefined,
                })}>{t('editor.addRuleTo', { range: selRange })}</button>
              </div>
            </div>
          )}
        </div>
        <span className="ss-spacer" />
        {busy && <span className="ss-busy">{busy}</span>}
        <button className="ss-tool ss-tool--text" title={t('editor.validateTitle')} onClick={validate}>{t('editor.validate')}</button>
        <button className="ss-tool ss-tool--text" title={t('editor.insertImageTitle')} onClick={() => imageInput.current?.click()}><FiImage /> {t('editor.insertImage')}</button>
        {sheet?.images.some(image => image.row === row && image.col === col) && (
          <button className="ss-tool ss-tool--text" title={t('editor.removeImageTitle')} onClick={() => {
            sheet.images.filter(image => image.row === row && image.col === col).forEach(image => removeImage(image.id));
          }}><FiX /> {t('editor.removeImage')}</button>
        )}
        <button className="ss-tool ss-tool--text" onClick={() => fileInput.current?.click()}><FiUpload /> {t('editor.import')}</button>
        <div className="ss-export">
          <button className="ss-tool ss-tool--text ss-tool--primary" onClick={() => setExportMenu((v) => !v)}><FiDownload /> {t('editor.export')}</button>
          {exportMenu && (
            <div className="ss-menu" onMouseLeave={() => setExportMenu(false)}>
              <button onClick={() => exportAs('xlsx')}>{t('editor.exportExcel')}</button>
              <button onClick={() => exportAs('csv')}>{t('editor.exportCsv')}</button>
              <button onClick={() => exportAs('json')}>{t('editor.exportJson')}</button>
            </div>
          )}
        </div>
        <input ref={fileInput} type="file" accept=".xlsx,.csv,.json" hidden onChange={onImport} />
        <input ref={imageInput} type="file" accept="image/png,image/jpeg" hidden onChange={onImageUpload} />
      </div>

      {validation && (
        <div className={`ss-validation${validation.valid ? ' is-valid' : ' is-invalid'}`}>
          <span className="ss-validation-summary">
            {validation.valid
              ? t('editor.workbookValid')
              : t('editor.validationSummary', {
                  errorCount: validation.issues.filter((i) => i.severity === 'error').length,
                  warningCount: validation.issues.filter((i) => i.severity === 'warning').length,
                })}
          </span>
          {!validation.valid && (
            <ul className="ss-validation-list">
              {validation.issues.slice(0, 8).map((i, k) => (
                <li key={k} className={`ss-issue ss-issue--${i.severity}`}><code>{i.path}</code> {i.message}</li>
              ))}
              {validation.issues.length > 8 && <li className="ss-issue">{t('editor.andMoreIssues', { count: validation.issues.length - 8 })}</li>}
            </ul>
          )}
          <button className="ss-validation-close" title={t('editor.dismiss')} onClick={() => setValidation(null)}><FiX /></button>
        </div>
      )}

      <div className="spreadsheet-formula-bar">
        <span className="ss-namebox">{colName(col)}{row + 1}</span>
        <input
          className="ss-formula-input"
          value={barValue}
          placeholder={t('editor.formulaPlaceholder')}
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
              onDoubleClick={() => { const n = prompt(t('editor.renameSheetPrompt'), s.name); if (n) renameSheet(i, n); }}
            >
              {s.name}
            </button>
            {sheets.length > 1 && (
              <button className="ss-tab-close" title={t('editor.deleteSheetTitle')} onClick={() => { if (confirm(t('editor.deleteSheetConfirm', { name: s.name }))) deleteSheet(i); }}><FiX /></button>
            )}
          </div>
        ))}
        <button className="ss-tab-add" title={t('editor.addSheetTitle')} onClick={addSheet}><FiPlus /></button>
        <span className="ss-spacer" />
        {stats.count > 0 && (
          <span className="ss-stats">
            {rangeLabel && <span className="ss-stats-range">{rangeLabel}</span>}
            <span>{t('editor.sum')} <strong>{round(stats.sum)}</strong></span>
            <span>{t('editor.avg')} <strong>{stats.avg != null ? round(stats.avg) : '—'}</strong></span>
            <span>{t('editor.count')} <strong>{stats.count}</strong></span>
          </span>
        )}
      </div>
    </div>
  );
};

export default SpreadsheetEditorPage;
