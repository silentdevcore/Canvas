// Spreadsheet model — mirrors the backend SpreadsheetDto (src/PXA.Core/Contracts/SpreadsheetDto.cs),
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
  comment?: string;        // cell note
  hyperlink?: string;      // URL or internal "Sheet!A1"
}

export interface SheetColumn { index: number; width?: number; hidden?: boolean; outlineLevel?: number; }
export interface SheetRow { index: number; height?: number; hidden?: boolean; outlineLevel?: number; }

// ── advanced sheet features (Phase-2 backend parity; carried through losslessly) ──────────────────────
export interface Margins { top?: number; right?: number; bottom?: number; left?: number; }
export interface PageSetup {
  orientation?: string; paperSize?: string; printArea?: string; header?: string; footer?: string;
  fitToWidth?: number; fitToHeight?: number; scale?: number; margins?: Margins;
  rowPageBreaks?: number[]; colPageBreaks?: number[];
}
export interface Protection { protected: boolean; password?: string; }
export interface ConditionalFormat { range: string; type: string; operator?: string; value?: string; value2?: string; color?: string; }
export interface DataValidation { range: string; type: string; operator?: string; value1?: string; value2?: string; listSource?: string; }

export interface DefinedName { name: string; refersTo: string; }
export interface SpreadsheetImage {
  id: string;
  assetId?: string;
  fileName?: string;
  contentType?: string;
  data?: string;
  contentUrl?: string;
  row: number;
  col: number;
  width: number;
  height: number;
  altText?: string;
}

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
  autoFilterRange?: string;
  pageSetup?: PageSetup;
  protection?: Protection;
  conditionalFormats?: ConditionalFormat[];
  dataValidations?: DataValidation[];
  images?: SpreadsheetImage[];
}

export interface Workbook {
  $schema?: string;
  schemaVersion?: string;
  id: string;
  name: string;
  sheets: SheetWire[];
  definedNames: DefinedName[];
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

/** Working sheet — cells indexed by "row:col" for O(1) grid access. The editor does not yet surface every
 *  advanced feature, so raw column/row metadata and Phase-2 fields are kept here as passthrough and
 *  re-emitted by sheetToWire (lossless JSON round-trip). */
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
  // passthrough (not edited by the grid yet)
  columns?: SheetColumn[];
  rows?: SheetRow[];
  autoFilterRange?: string;
  pageSetup?: PageSetup;
  protection?: Protection;
  conditionalFormats?: ConditionalFormat[];
  dataValidations?: DataValidation[];
  images: SpreadsheetImage[];
}

const uid = () => Math.random().toString(36).slice(2, 10);

export function emptySheet(name = 'Sheet1'): SheetState {
  return { id: uid(), name, rowCount: 100, colCount: 26, colWidths: {}, cells: {}, merges: [], frozenRows: 0, frozenCols: 0, images: [] };
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
    columns: w.columns ?? [],
    rows: w.rows ?? [],
    autoFilterRange: w.autoFilterRange,
    pageSetup: w.pageSetup,
    protection: w.protection,
    conditionalFormats: w.conditionalFormats,
    dataValidations: w.dataValidations,
    images: w.images ?? [],
  };
}

export function sheetToWire(s: SheetState): SheetWire {
  // Merge preserved column metadata (hidden / outlineLevel) with widths edited in the grid.
  const colByIndex = new Map<number, SheetColumn>();
  for (const col of s.columns ?? []) colByIndex.set(col.index, { ...col });
  for (const [index, width] of Object.entries(s.colWidths)) {
    const i = Number(index);
    colByIndex.set(i, { ...(colByIndex.get(i) ?? { index: i }), index: i, width });
  }
  return {
    id: s.id,
    name: s.name,
    rowCount: s.rowCount,
    colCount: s.colCount,
    columns: [...colByIndex.values()].sort((a, b) => a.index - b.index),
    rows: s.rows ?? [],
    cells: Object.values(s.cells).filter((c) => c.type !== 'empty' || c.style || c.comment || c.hyperlink),
    merges: s.merges,
    frozenRows: s.frozenRows,
    frozenCols: s.frozenCols,
    autoFilterRange: s.autoFilterRange,
    pageSetup: s.pageSetup,
    protection: s.protection,
    conditionalFormats: s.conditionalFormats,
    dataValidations: s.dataValidations,
    images: s.images,
  };
}

export function workbookFromWire(w: Workbook): { name: string; sheets: SheetState[]; definedNames: DefinedName[]; schemaVersion: string } {
  const sheets = (w.sheets ?? []).map(sheetFromWire);
  return {
    name: w.name || 'Workbook',
    sheets: sheets.length ? sheets : [emptySheet()],
    definedNames: w.definedNames ?? [],
    schemaVersion: w.schemaVersion || '1.0',
  };
}

export function workbookToWire(name: string, sheets: SheetState[], definedNames: DefinedName[] = [], schemaVersion = '1.0'): Workbook {
  return { schemaVersion, id: uid(), name, sheets: sheets.map(sheetToWire), definedNames };
}
