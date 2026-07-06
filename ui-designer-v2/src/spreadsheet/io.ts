// Client-side import/export for lightweight formats (CSV, JSON). The .xlsx round-trip is handled by the
// backend (SpreadsheetService); these need no server and no extra dependency.
import { SheetState, Workbook, cellKey, emptySheet } from './types';

type Computed = string | number | boolean | null;

// ── CSV (RFC 4180) ───────────────────────────────────────────────────────────────────────────────────

function csvField(v: string): string {
  return /[",\n\r]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
}

/** The active sheet's *computed* values as CSV (so the file matches what's on screen). */
export function sheetToCsv(sheet: SheetState, getComputed: (row: number, col: number) => Computed): string {
  let maxRow = -1;
  let maxCol = -1;
  for (const key of Object.keys(sheet.cells)) {
    const [r, c] = key.split(':').map(Number);
    if (r > maxRow) maxRow = r;
    if (c > maxCol) maxCol = c;
  }
  if (maxRow < 0) return '';

  const lines: string[] = [];
  for (let r = 0; r <= maxRow; r++) {
    const row: string[] = [];
    for (let c = 0; c <= maxCol; c++) {
      const v = getComputed(r, c);
      row.push(csvField(v == null ? '' : String(v)));
    }
    lines.push(row.join(','));
  }
  return lines.join('\r\n');
}

/** RFC 4180-aware CSV parse → rows of string fields. */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let inQuotes = false;
  let sawAny = false;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    sawAny = true;
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; }
        else inQuotes = false;
      } else field += ch;
      continue;
    }
    if (ch === '"') inQuotes = true;
    else if (ch === ',') { row.push(field); field = ''; }
    else if (ch === '\r') { /* skip; handled by \n */ }
    else if (ch === '\n') { row.push(field); rows.push(row); row = []; field = ''; }
    else field += ch;
  }
  if (sawAny && (field !== '' || row.length > 0)) { row.push(field); rows.push(row); }
  return rows;
}

const NUMERIC = /^-?\d*\.?\d+(e[-+]?\d+)?$/i;

export function csvToSheet(text: string, name = 'Sheet1'): SheetState {
  const rows = parseCsv(text);
  const sheet = emptySheet(name);
  rows.forEach((cols, r) =>
    cols.forEach((val, c) => {
      if (val === '') return;
      const isNum = val.trim() !== '' && NUMERIC.test(val.trim());
      sheet.cells[cellKey(r, c)] = { row: r, col: c, type: isNum ? 'number' : 'text', value: isNum ? Number(val) : val };
    }),
  );
  sheet.rowCount = Math.max(100, rows.length);
  sheet.colCount = Math.max(26, ...rows.map((r) => r.length), 0);
  return sheet;
}

// ── JSON (native workbook model — lossless) ────────────────────────────────────────────────────────────

export function workbookToJson(w: Workbook): string {
  return JSON.stringify(w, null, 2);
}

/** The PXA Workbook JSON format version this client understands (matches SpreadsheetDto.CurrentSchemaVersion). */
export const CURRENT_SCHEMA_VERSION = '1.0';

const major = (v: string | undefined): number => Number((v ?? '1.0').split('.')[0]) || 0;

export function jsonToWorkbook(text: string): Workbook {
  const w = JSON.parse(text);
  if (!w || typeof w !== 'object' || !Array.isArray(w.sheets)) throw new Error('Not a valid workbook JSON (missing "sheets").');
  if (major(w.schemaVersion) > major(CURRENT_SCHEMA_VERSION))
    throw new Error(`This workbook uses PXA Workbook JSON v${w.schemaVersion}, which is newer than this app supports (v${CURRENT_SCHEMA_VERSION}). Update the app to open it without losing data.`);
  return w as Workbook;
}

// ── browser download helper ────────────────────────────────────────────────────────────────────────────

export function downloadText(content: string, fileName: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
