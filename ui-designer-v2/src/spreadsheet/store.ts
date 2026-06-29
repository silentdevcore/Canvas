import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import {
  SheetState, Cell, CellType, CellStyle, cellKey, emptySheet,
  Workbook, workbookFromWire, workbookToWire, toA1Range, parseA1Range,
} from './types';
import { sheetEngine } from './formulaEngine';

export interface Selection { row: number; col: number; }
/** Inclusive selected rectangle (0-based). */
export interface SelRange { r0: number; c0: number; r1: number; c1: number; }

interface Snapshot { name: string; sheets: SheetState[]; }

interface SpreadsheetState {
  name: string;
  schemaVersion: string;
  definedNames: { name: string; refersTo: string }[];
  sheets: SheetState[];
  active: number;
  selection: Selection;
  range: SelRange | null;
  past: Snapshot[];
  future: Snapshot[];

  // selectors
  activeSheet: () => SheetState;
  computed: (row: number, col: number) => string | number | boolean | null;
  cellAt: (row: number, col: number) => Cell | undefined;
  /** Sum / average / count over the numeric computed cells in the current range (or active cell). */
  selectionStats: () => { count: number; sum: number; avg: number | null };

  // actions
  select: (row: number, col: number) => void;
  selectRange: (r: SelRange | null) => void;
  setActive: (index: number) => void;
  setCellInput: (row: number, col: number, raw: string) => void;
  setCellStyle: (row: number, col: number, patch: Partial<CellStyle>) => void;
  /** Merge a style patch into every cell of the current range (or the active cell). */
  applyStyle: (patch: Partial<CellStyle>) => void;
  /** Set a number format on every cell of the current range (or the active cell). */
  applyNumberFormat: (fmt: string | undefined) => void;
  setNumberFormat: (row: number, col: number, fmt: string | undefined) => void;
  insertRow: (at: number) => void;
  deleteRow: (at: number) => void;
  insertCol: (at: number) => void;
  deleteCol: (at: number) => void;
  setColWidth: (col: number, width: number) => void;
  /** Paste a block of raw values starting at (row, col) — one undo step. */
  pasteValues: (row: number, col: number, values: readonly (readonly string[])[]) => void;
  /** Clear the content of the current range (keeps styling), one undo step. */
  clearRange: () => void;
  /** Merge / unmerge the current range; freeze panes. (Round-trip to .xlsx; the editor renders horizontal
   *  merges + frozen columns — vertical merges / frozen rows still export correctly.) */
  mergeSelection: () => void;
  unmergeSelection: () => void;
  setFrozen: (rows: number, cols: number) => void;
  addSheet: () => void;
  renameSheet: (index: number, name: string) => void;
  deleteSheet: (index: number) => void;
  loadWorkbook: (w: Workbook) => void;
  toWire: () => Workbook;
  rebuildEngine: () => void;
  undo: () => void;
  redo: () => void;
}

const clone = (sheets: SheetState[]): SheetState[] => JSON.parse(JSON.stringify(sheets));

/** Shift a sheet's cells (+ column widths) for a row/column insert or delete at `at`. */
function shiftCellsForRowCol(sheet: SheetState, axis: 'row' | 'col', at: number, insert: boolean): void {
  const cells: Record<string, Cell> = {};
  for (const cell of Object.values(sheet.cells)) {
    let { row, col } = cell;
    const idx = axis === 'row' ? row : col;
    if (insert) {
      if (idx >= at) { if (axis === 'row') row++; else col++; }
    } else {
      if (idx === at) continue;            // drop cells on the deleted line
      if (idx > at) { if (axis === 'row') row--; else col--; }
    }
    cells[cellKey(row, col)] = { ...cell, row, col };
  }
  sheet.cells = cells;

  if (axis === 'col') {
    const widths: Record<number, number> = {};
    for (const [k, width] of Object.entries(sheet.colWidths)) {
      let ci = Number(k);
      if (insert) { if (ci >= at) ci++; } else { if (ci === at) continue; if (ci > at) ci--; }
      widths[ci] = width;
    }
    sheet.colWidths = widths;
  }
}

/** Insert/delete a row or column: shift the store + HyperFormula (which re-points A1 refs), then pull the
 *  shifted formula sources back so the model and the engine stay consistent. */
function rowColOp(
  get: () => SpreadsheetState,
  set: (partial: Partial<SpreadsheetState>) => void,
  axis: 'row' | 'col',
  at: number,
  insert: boolean,
): void {
  const { sheets, active } = get();
  const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
  const next = clone(sheets);
  const sheet = next[active];

  shiftCellsForRowCol(sheet, axis, at, insert);
  if (axis === 'row') sheet.rowCount = Math.max(1, sheet.rowCount + (insert ? 1 : -1));
  else sheet.colCount = Math.max(1, sheet.colCount + (insert ? 1 : -1));

  if (axis === 'row') insert ? sheetEngine.addRows(active, at) : sheetEngine.removeRows(active, at);
  else insert ? sheetEngine.addColumns(active, at) : sheetEngine.removeColumns(active, at);

  for (const cell of Object.values(sheet.cells)) {
    if (cell.type === 'formula') {
      const f = sheetEngine.getFormula(active, cell.row, cell.col);
      if (f) cell.formula = f;
      cell.value = sheetEngine.getValue(active, cell.row, cell.col);
    }
  }
  set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
}

/** Apply a mutation to every cell of the current range (or the active cell), with an undo snapshot. */
function mutateRange(
  get: () => SpreadsheetState,
  set: (partial: Partial<SpreadsheetState>) => void,
  mutate: (cell: Cell) => void,
): void {
  const { sheets, active, selection } = get();
  const r = get().range ?? { r0: selection.row, c0: selection.col, r1: selection.row, c1: selection.col };
  const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
  const next = clone(sheets);
  const nextSheet = next[active];
  for (let row = Math.min(r.r0, r.r1); row <= Math.max(r.r0, r.r1); row++) {
    for (let col = Math.min(r.c0, r.c1); col <= Math.max(r.c0, r.c1); col++) {
      const key = cellKey(row, col);
      const cell: Cell = nextSheet.cells[key] ?? { row, col, type: 'empty', value: null };
      mutate(cell);
      nextSheet.cells[key] = cell;
    }
  }
  set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
}

/** Parse raw cell input into a typed cell payload (formula vs number/boolean/text). */
export function parseInput(raw: string): { type: CellType; value?: Cell['value']; formula?: string } {
  if (raw === '') return { type: 'empty', value: null };
  if (raw.startsWith('=')) return { type: 'formula', formula: raw, value: null };
  const trimmed = raw.trim();
  const n = Number(trimmed);
  if (trimmed !== '' && !Number.isNaN(n) && /^-?\d*\.?\d+(e[-+]?\d+)?$/i.test(trimmed)) return { type: 'number', value: n };
  if (trimmed === 'true' || trimmed === 'false') return { type: 'boolean', value: trimmed === 'true' };
  return { type: 'text', value: raw };
}

const MAX_HISTORY = 50;

export const useSpreadsheetStore = create<SpreadsheetState>()(
  persist(
    (set, get) => ({
      name: 'Workbook',
      schemaVersion: '1.0',
      definedNames: [],
      sheets: [emptySheet()],
      active: 0,
      selection: { row: 0, col: 0 },
      range: null,
      past: [],
      future: [],

      activeSheet: () => get().sheets[get().active] ?? get().sheets[0],
      computed: (row, col) => sheetEngine.getValue(get().active, row, col),
      cellAt: (row, col) => get().activeSheet().cells[cellKey(row, col)],

      selectionStats: () => {
        const { active } = get();
        const r = get().range ?? { r0: get().selection.row, c0: get().selection.col, r1: get().selection.row, c1: get().selection.col };
        let count = 0;
        let sum = 0;
        for (let row = Math.min(r.r0, r.r1); row <= Math.max(r.r0, r.r1); row++) {
          for (let col = Math.min(r.c0, r.c1); col <= Math.max(r.c0, r.c1); col++) {
            const v = sheetEngine.getValue(active, row, col);
            if (typeof v === 'number' && !Number.isNaN(v)) { count++; sum += v; }
          }
        }
        return { count, sum, avg: count ? sum / count : null };
      },

      select: (row, col) => set({ selection: { row, col } }),
      selectRange: (r) => set({ range: r }),
      setActive: (index) => set({ active: Math.max(0, Math.min(index, get().sheets.length - 1)), selection: { row: 0, col: 0 }, range: null }),

      setCellInput: (row, col, raw) => {
        const { sheets, active } = get();
        const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
        const sheet = sheets[active];
        const key = cellKey(row, col);
        const existing = sheet.cells[key];
        const parsed = parseInput(raw);

        const next = clone(sheets);
        const nextSheet = next[active];
        if (parsed.type === 'empty' && !existing?.style && !existing?.numberFormat) {
          delete nextSheet.cells[key];
        } else {
          nextSheet.cells[key] = {
            row, col,
            type: parsed.type,
            value: parsed.value,
            formula: parsed.formula,
            numberFormat: existing?.numberFormat,
            style: existing?.style,
          };
        }
        sheetEngine.setCell(active, row, col, raw);
        if (parsed.type === 'formula' && nextSheet.cells[key]) nextSheet.cells[key].value = sheetEngine.getValue(active, row, col);

        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      setCellStyle: (row, col, patch) => {
        const { sheets, active } = get();
        const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
        const next = clone(sheets);
        const nextSheet = next[active];
        const key = cellKey(row, col);
        const cell: Cell = nextSheet.cells[key] ?? { row, col, type: 'empty', value: null };
        cell.style = { ...cell.style, ...patch };
        nextSheet.cells[key] = cell;
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      setNumberFormat: (row, col, fmt) => {
        const { sheets, active } = get();
        const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
        const next = clone(sheets);
        const nextSheet = next[active];
        const key = cellKey(row, col);
        const cell: Cell = nextSheet.cells[key] ?? { row, col, type: 'empty', value: null };
        cell.numberFormat = fmt;
        nextSheet.cells[key] = cell;
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      applyStyle: (patch) => mutateRange(get, set, (cell) => { cell.style = { ...cell.style, ...patch }; }),
      applyNumberFormat: (fmt) => mutateRange(get, set, (cell) => { cell.numberFormat = fmt; }),

      insertRow: (at) => rowColOp(get, set, 'row', at, true),
      deleteRow: (at) => rowColOp(get, set, 'row', at, false),
      insertCol: (at) => rowColOp(get, set, 'col', at, true),
      deleteCol: (at) => rowColOp(get, set, 'col', at, false),

      setColWidth: (col, width) => {
        const next = clone(get().sheets);
        next[get().active].colWidths[col] = width;
        set({ sheets: next }); // not snapshotted — resize is cheap/continuous
      },

      pasteValues: (startRow, startCol, values) => {
        const { sheets, active } = get();
        const snapshot: Snapshot = { name: get().name, sheets: clone(sheets) };
        const next = clone(sheets);
        const sheet = next[active];
        values.forEach((rowVals, dr) =>
          rowVals.forEach((raw, dc) => {
            const row = startRow + dr;
            const col = startCol + dc;
            const key = cellKey(row, col);
            const existing = sheet.cells[key];
            const parsed = parseInput(raw);
            if (parsed.type === 'empty' && !existing?.style && !existing?.numberFormat) delete sheet.cells[key];
            else sheet.cells[key] = { row, col, type: parsed.type, value: parsed.value, formula: parsed.formula, numberFormat: existing?.numberFormat, style: existing?.style };
            sheetEngine.setCell(active, row, col, raw);
          }),
        );
        for (const cell of Object.values(sheet.cells)) if (cell.type === 'formula') cell.value = sheetEngine.getValue(active, cell.row, cell.col);
        sheet.rowCount = Math.max(sheet.rowCount, startRow + values.length);
        sheet.colCount = Math.max(sheet.colCount, startCol + Math.max(0, ...values.map((v) => v.length)));
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      clearRange: () => {
        const { active, selection } = get();
        const r = get().range ?? { r0: selection.row, c0: selection.col, r1: selection.row, c1: selection.col };
        const snapshot: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        const next = clone(get().sheets);
        const sheet = next[active];
        for (let row = Math.min(r.r0, r.r1); row <= Math.max(r.r0, r.r1); row++) {
          for (let col = Math.min(r.c0, r.c1); col <= Math.max(r.c0, r.c1); col++) {
            const key = cellKey(row, col);
            const ex = sheet.cells[key];
            if (ex) {
              if (ex.style || ex.numberFormat) { ex.type = 'empty'; ex.value = null; ex.formula = undefined; }
              else delete sheet.cells[key];
            }
            sheetEngine.setCell(active, row, col, '');
          }
        }
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      mergeSelection: () => {
        const r = get().range;
        if (!r || (r.r0 === r.r1 && r.c0 === r.c1)) return; // need a multi-cell range
        const a1 = toA1Range(r.r0, r.c0, r.r1, r.c1);
        const snapshot: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        const next = clone(get().sheets);
        const sheet = next[get().active];
        if (!sheet.merges.includes(a1)) sheet.merges.push(a1);
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      unmergeSelection: () => {
        const { selection } = get();
        const r = get().range ?? { r0: selection.row, c0: selection.col, r1: selection.row, c1: selection.col };
        const snapshot: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        const next = clone(get().sheets);
        const sheet = next[get().active];
        const intersects = (m: { r0: number; c0: number; r1: number; c1: number }) =>
          Math.min(r.r0, r.r1) <= m.r1 && Math.max(r.r0, r.r1) >= m.r0 && Math.min(r.c0, r.c1) <= m.c1 && Math.max(r.c0, r.c1) >= m.c0;
        sheet.merges = sheet.merges.filter((s) => !intersects(parseA1Range(s)));
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      setFrozen: (rows, cols) => {
        const snapshot: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        const next = clone(get().sheets);
        next[get().active].frozenRows = Math.max(0, rows);
        next[get().active].frozenCols = Math.max(0, cols);
        set({ sheets: next, past: [...get().past, snapshot].slice(-MAX_HISTORY), future: [] });
      },

      addSheet: () => {
        const { sheets } = get();
        const name = `Sheet${sheets.length + 1}`;
        const next = [...sheets, emptySheet(name)];
        set({ sheets: next, active: next.length - 1, selection: { row: 0, col: 0 } });
        sheetEngine.rebuild(next);
      },
      renameSheet: (index, name) => {
        const next = clone(get().sheets);
        if (next[index]) next[index].name = name || next[index].name;
        set({ sheets: next });
        sheetEngine.rebuild(next);
      },
      deleteSheet: (index) => {
        const { sheets } = get();
        if (sheets.length <= 1) return;
        const next = sheets.filter((_, i) => i !== index);
        set({ sheets: next, active: Math.max(0, Math.min(get().active, next.length - 1)) });
        sheetEngine.rebuild(next);
      },

      loadWorkbook: (w) => {
        const { name, sheets, definedNames, schemaVersion } = workbookFromWire(w);
        set({ name, schemaVersion, definedNames, sheets, active: 0, selection: { row: 0, col: 0 }, past: [], future: [] });
        sheetEngine.rebuild(sheets);
      },
      toWire: () => workbookToWire(get().name, get().sheets, get().definedNames, get().schemaVersion),
      rebuildEngine: () => sheetEngine.rebuild(get().sheets),

      undo: () => {
        const { past } = get();
        if (past.length === 0) return;
        const prev = past[past.length - 1];
        const current: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        set({ name: prev.name, sheets: prev.sheets, past: past.slice(0, -1), future: [current, ...get().future] });
        sheetEngine.rebuild(prev.sheets);
      },
      redo: () => {
        const { future } = get();
        if (future.length === 0) return;
        const nextSnap = future[0];
        const current: Snapshot = { name: get().name, sheets: clone(get().sheets) };
        set({ name: nextSnap.name, sheets: nextSnap.sheets, future: future.slice(1), past: [...get().past, current] });
        sheetEngine.rebuild(nextSnap.sheets);
      },
    }),
    {
      name: 'canvas-spreadsheet',
      partialize: (s) => ({ name: s.name, sheets: s.sheets, active: s.active }),
      onRehydrateStorage: () => (state) => { if (state) sheetEngine.rebuild(state.sheets); },
    },
  ),
);

// Initialize the engine for the default (fresh) state.
sheetEngine.rebuild(useSpreadsheetStore.getState().sheets);
