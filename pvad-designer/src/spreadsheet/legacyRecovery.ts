import { workbookToWire, type SheetState } from './types';

export const legacyWorkbookKeys = ['pxa-spreadsheet', 'canvas-spreadsheet', 'pvad-spreadsheet'] as const;
export interface LegacyWorkbookDraft { key: string; raw: string; }

/** Explicit recovery only: never hydrate or delete an unassigned document. */
export function findLegacyWorkbookDrafts(storage: Pick<Storage, 'getItem'>): LegacyWorkbookDraft[] {
  return legacyWorkbookKeys.flatMap(key => {
    const raw = storage.getItem(key);
    return raw === null ? [] : [{ key, raw }];
  });
}

export function legacyWorkbookJson(draft: LegacyWorkbookDraft): string {
  if (draft.raw.length > 10 * 1024 * 1024) throw new Error('Recovery draft exceeds the size limit.');
  const state = JSON.parse(draft.raw)?.state;
  if (!state || typeof state.name !== 'string' || !Array.isArray(state.sheets)
    || !state.sheets.length || state.sheets.length > 1000) throw new Error('Invalid recovery draft.');
  for (const sheet of state.sheets) {
    if (!sheet || typeof sheet.name !== 'string' || !sheet.cells || Array.isArray(sheet.cells)
      || typeof sheet.cells !== 'object' || !sheet.colWidths || typeof sheet.colWidths !== 'object'
      || !Array.isArray(sheet.merges) || !Number.isInteger(sheet.rowCount) || sheet.rowCount < 1
      || !Number.isInteger(sheet.colCount) || sheet.colCount < 1) throw new Error('Invalid recovery sheet.');
    for (const cell of Object.values(sheet.cells) as Record<string, unknown>[]) {
      if (!cell || !Number.isInteger(cell.row) || Number(cell.row) < 0
        || !Number.isInteger(cell.col) || Number(cell.col) < 0
        || !['number', 'text', 'boolean', 'date', 'formula', 'empty'].includes(String(cell.type)))
        throw new Error('Invalid recovery cell.');
    }
  }
  return JSON.stringify(workbookToWire(state.name, state.sheets as SheetState[],
    state.definedNames, state.schemaVersion, state.projectId, state.extensionData), null, 2);
}
