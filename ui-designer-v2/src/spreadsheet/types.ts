// Spreadsheet model — mirrors the backend SpreadsheetDto (src/Canvas.Core/Contracts/SpreadsheetDto.cs),
// serialized camelCase. The wire shape uses a sparse `cells` array; the store keeps cells in a Record
// keyed by "row:col" for fast lookup (see store.ts).

export type CellType = 'number' | 'text' | 'boolean' | 'date' | 'formula' | 'empty';

export interface CellStyle {
  backgroundColor?: string;
  color?: string;
  bold?: boolean;
  italic?: boolean;
  textAlign?: 'left' | 'center' | 'right';
  fontFamily?: string;
  fontSize?: number;
}

export interface Cell {
  row: number;
  col: number;
  type: CellType;
  value?: string | number | boolean | null;
  formula?: string;        // "=SUM(A1:A10)" when type === "formula"
  numberFormat?: string;   // Excel number-format code
  style?: CellStyle;
}

export interface SheetColumn { index: number; width?: number; hidden?: boolean; }
export interface SheetRow { index: number; height?: number; hidden?: boolean; }

/** Wire sheet (sparse cells array) — what the backend import/export uses. */
export interface SheetWire {
  id: string;
  name: string;
  rowCount: number;
  colCount: number;
  columns: SheetColumn[];
  rows: SheetRow[];
  cells: Cell[];
  merges: string[];
  frozenRows: number;
  frozenCols: number;
}

export interface Workbook {
  id: string;
  name: string;
  sheets: SheetWire[];
  definedNames: { name: string; refersTo: string }[];
}

// ── working representation (store) ──────────────────────────────────────────────────────────────────

export const cellKey = (row: number, col: number) => `${row}:${col}`;

/** 0-based column index → spreadsheet letters ("A", "AA", …). */
export function colName(index: number): string {
  let s = '';
  let n = index + 1;
  while (n > 0) { const r = (n - 1) % 26; s = String.fromCharCode(65 + r) + s; n = Math.floor((n - 1) / 26); }
  return s;
}
const colIndex = (name: string): number => [...name.toUpperCase()].reduce((n, ch) => n * 26 + (ch.charCodeAt(0) - 64), 0) - 1;
export const toA1 = (row: number, col: number) => `${colName(col)}${row + 1}`;
export const toA1Range = (r0: number, c0: number, r1: number, c1: number) =>
  `${toA1(Math.min(r0, r1), Math.min(c0, c1))}:${toA1(Math.max(r0, r1), Math.max(c0, c1))}`;

/** Parse "A1:B2" (or "A1") → inclusive 0-based rectangle. */
export function parseA1Range(a1: string): { r0: number; c0: number; r1: number; c1: number } {
  const [a, b = a] = a1.split(':');
  const m = (s: string) => { const mm = s.replace(/\$/g, '').match(/^([A-Za-z]+)(\d+)$/)!; return { col: colIndex(mm[1]), row: Number(mm[2]) - 1 }; };
  const p = m(a); const q = m(b);
  return { r0: Math.min(p.row, q.row), c0: Math.min(p.col, q.col), r1: Math.max(p.row, q.row), c1: Math.max(p.col, q.col) };
}

/** Working sheet — cells indexed by "row:col" for O(1) grid access. */
export interface SheetState {
  id: string;
  name: string;
  rowCount: number;
  colCount: number;
  colWidths: Record<number, number>;
  cells: Record<string, Cell>;
  merges: string[];
  frozenRows: number;
  frozenCols: number;
}

const uid = () => Math.random().toString(36).slice(2, 10);

export function emptySheet(name = 'Sheet1'): SheetState {
  return { id: uid(), name, rowCount: 100, colCount: 26, colWidths: {}, cells: {}, merges: [], frozenRows: 0, frozenCols: 0 };
}

export function sheetFromWire(w: SheetWire): SheetState {
  const cells: Record<string, Cell> = {};
  for (const c of w.cells) cells[cellKey(c.row, c.col)] = c;
  const colWidths: Record<number, number> = {};
  for (const col of w.columns ?? []) if (col.width != null) colWidths[col.index] = col.width;
  return {
    id: w.id || uid(),
    name: w.name,
    rowCount: Math.max(w.rowCount || 100, 100),
    colCount: Math.max(w.colCount || 26, 26),
    colWidths,
    cells,
    merges: w.merges ?? [],
    frozenRows: w.frozenRows ?? 0,
    frozenCols: w.frozenCols ?? 0,
  };
}

export function sheetToWire(s: SheetState): SheetWire {
  return {
    id: s.id,
    name: s.name,
    rowCount: s.rowCount,
    colCount: s.colCount,
    columns: Object.entries(s.colWidths).map(([index, width]) => ({ index: Number(index), width })),
    rows: [],
    cells: Object.values(s.cells).filter((c) => c.type !== 'empty' || c.style),
    merges: s.merges,
    frozenRows: s.frozenRows,
    frozenCols: s.frozenCols,
  };
}

export function workbookFromWire(w: Workbook): { name: string; sheets: SheetState[] } {
  const sheets = (w.sheets ?? []).map(sheetFromWire);
  return { name: w.name || 'Workbook', sheets: sheets.length ? sheets : [emptySheet()] };
}

export function workbookToWire(name: string, sheets: SheetState[]): Workbook {
  return { id: uid(), name, sheets: sheets.map(sheetToWire), definedNames: [] };
}
