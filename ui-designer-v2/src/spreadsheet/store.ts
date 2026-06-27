import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import {
  SheetState, Cell, CellType, CellStyle, cellKey, emptySheet,
  Workbook, workbookFromWire, workbookToWire,
} from './types';
import { sheetEngine } from './formulaEngine';

export interface Selection { row: number; col: number; }

interface Snapshot { name: string; sheets: SheetState[]; }

interface SpreadsheetState {
  name: string;
  sheets: SheetState[];
  active: number;
  selection: Selection;
  past: Snapshot[];
  future: Snapshot[];

  // selectors
  activeSheet: () => SheetState;
  computed: (row: number, col: number) => string | number | boolean | null;
  cellAt: (row: number, col: number) => Cell | undefined;

  // actions
  select: (row: number, col: number) => void;
  setActive: (index: number) => void;
  setCellInput: (row: number, col: number, raw: string) => void;
  setCellStyle: (row: number, col: number, patch: Partial<CellStyle>) => void;
  setNumberFormat: (row: number, col: number, fmt: string | undefined) => void;
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
      sheets: [emptySheet()],
      active: 0,
      selection: { row: 0, col: 0 },
      past: [],
      future: [],

      activeSheet: () => get().sheets[get().active] ?? get().sheets[0],
      computed: (row, col) => sheetEngine.getValue(get().active, row, col),
      cellAt: (row, col) => get().activeSheet().cells[cellKey(row, col)],

      select: (row, col) => set({ selection: { row, col } }),
      setActive: (index) => set({ active: Math.max(0, Math.min(index, get().sheets.length - 1)), selection: { row: 0, col: 0 } }),

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
        const { name, sheets } = workbookFromWire(w);
        set({ name, sheets, active: 0, selection: { row: 0, col: 0 }, past: [], future: [] });
        sheetEngine.rebuild(sheets);
      },
      toWire: () => workbookToWire(get().name, get().sheets),
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
