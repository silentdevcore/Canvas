// HyperFormula wrapper. HyperFormula owns the A1 dependency graph + ~390 Excel functions; the Zustand
// store owns the raw model (formulas/values/styles). This keeps an HF instance in sync so the grid can
// read live computed values. (HyperFormula is GPLv3-or-commercial — license key 'gpl-v3'.)
import { HyperFormula } from 'hyperformula';
import { SheetState, cellKey } from './types';

type RawValue = string | number | boolean | null;

export class SheetEngine {
  private hf = HyperFormula.buildEmpty({ licenseKey: 'gpl-v3' });
  private ids: number[] = []; // store sheet index → HF sheet id

  /** Rebuild the whole engine from the model (on load / undo / import). */
  rebuild(sheets: SheetState[]): void {
    this.hf = HyperFormula.buildEmpty({ licenseKey: 'gpl-v3' });
    this.ids = [];
    sheets.forEach((s, i) => {
      const name = s.name || `Sheet${i + 1}`;
      this.hf.addSheet(name);
      const id = this.hf.getSheetId(name);
      this.ids[i] = id ?? i;
      this.hf.setSheetContent(this.ids[i], this.toGrid(s));
    });
  }

  private toGrid(s: SheetState): RawValue[][] {
    const grid: RawValue[][] = [];
    for (let r = 0; r < s.rowCount; r++) {
      const row: RawValue[] = [];
      for (let c = 0; c < s.colCount; c++) {
        const cell = s.cells[cellKey(r, c)];
        row.push(cell ? (cell.type === 'formula' ? (cell.formula ?? '') : (cell.value ?? null)) : null);
      }
      grid.push(row);
    }
    return grid;
  }

  /** Push a raw cell edit (a value or "=formula") into the engine. */
  setCell(sheetIndex: number, row: number, col: number, raw: string): void {
    const sheet = this.ids[sheetIndex] ?? sheetIndex;
    try { this.hf.setCellContents({ sheet, row, col }, [[raw === '' ? null : raw]]); } catch { /* noop */ }
  }

  // Structural ops — HyperFormula shifts all dependent A1 formula references automatically.
  addRows(sheetIndex: number, at: number, count = 1): void { try { this.hf.addRows(this.sid(sheetIndex), [at, count]); } catch { /* noop */ } }
  removeRows(sheetIndex: number, at: number, count = 1): void { try { this.hf.removeRows(this.sid(sheetIndex), [at, count]); } catch { /* noop */ } }
  addColumns(sheetIndex: number, at: number, count = 1): void { try { this.hf.addColumns(this.sid(sheetIndex), [at, count]); } catch { /* noop */ } }
  removeColumns(sheetIndex: number, at: number, count = 1): void { try { this.hf.removeColumns(this.sid(sheetIndex), [at, count]); } catch { /* noop */ } }

  /** The (possibly ref-shifted) formula source at a cell, including the leading '=' — or null. */
  getFormula(sheetIndex: number, row: number, col: number): string | null {
    try { return this.hf.getCellFormula({ sheet: this.sid(sheetIndex), row, col }) ?? null; } catch { return null; }
  }

  private sid(sheetIndex: number): number { return this.ids[sheetIndex] ?? sheetIndex; }

  /** Live computed display value (resolves formulas); errors come back as their #CODE string. */
  getValue(sheetIndex: number, row: number, col: number): RawValue {
    const sheet = this.ids[sheetIndex] ?? sheetIndex;
    try {
      const v = this.hf.getCellValue({ sheet, row, col });
      if (v != null && typeof v === 'object' && 'value' in (v as object)) return String((v as { value: unknown }).value);
      return v as RawValue;
    } catch {
      return null;
    }
  }
}

export const sheetEngine = new SheetEngine();
