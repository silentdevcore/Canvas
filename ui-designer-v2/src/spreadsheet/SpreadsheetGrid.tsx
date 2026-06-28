import React, { useCallback } from 'react';
import {
  DataEditor, GridCellKind, type GridColumn, type GridCell, type Item, type EditableGridCell,
  type GridSelection, type Theme,
} from '@glideapps/glide-data-grid';
import '@glideapps/glide-data-grid/dist/index.css';
import { useSpreadsheetStore } from './store';
import { cellKey, type CellStyle } from './types';

/** Map a cell style to a glide per-cell theme override (font, fill, text color). */
function buildTheme(style: CellStyle): Partial<Theme> {
  const t: Partial<Theme> = {};
  if (style.bold || style.italic || style.fontSize || style.fontFamily) {
    const italic = style.italic ? 'italic ' : '';
    const weight = style.bold ? '600' : '400';
    t.baseFontStyle = `${italic}${weight} ${style.fontSize ?? 13}px ${style.fontFamily ?? 'inherit'}`;
  }
  if (style.backgroundColor) t.bgCell = style.backgroundColor;
  if (style.color) t.textDark = style.color;
  return t;
}

/** 0-based column index → spreadsheet letters ("A", "AA", …). */
export function colName(index: number): string {
  let s = '';
  let n = index + 1;
  while (n > 0) {
    const r = (n - 1) % 26;
    s = String.fromCharCode(65 + r) + s;
    n = Math.floor((n - 1) / 26);
  }
  return s;
}

/** The cell grid — renders the active sheet's computed values with inline editing (glide-data-grid). */
export const SpreadsheetGrid: React.FC = () => {
  const sheet = useSpreadsheetStore((s) => s.sheets[s.active]);
  const computed = useSpreadsheetStore((s) => s.computed);
  const setCellInput = useSpreadsheetStore((s) => s.setCellInput);
  const select = useSpreadsheetStore((s) => s.select);
  const selectRange = useSpreadsheetStore((s) => s.selectRange);
  const setColWidth = useSpreadsheetStore((s) => s.setColWidth);
  const pasteValues = useSpreadsheetStore((s) => s.pasteValues);
  const clearRange = useSpreadsheetStore((s) => s.clearRange);

  const columns: GridColumn[] = React.useMemo(
    () => Array.from({ length: sheet.colCount }, (_, c) => ({
      title: colName(c),
      id: String(c),
      width: sheet.colWidths[c] ?? 100,
    })),
    [sheet.colCount, sheet.colWidths],
  );

  const getCellContent = useCallback(([col, row]: Item): GridCell => {
    const cell = sheet.cells[cellKey(row, col)];
    const value = computed(row, col);
    const display = value == null ? '' : String(value);
    // On edit the overlay shows the formula source; the grid shows the computed result.
    const editData = cell?.type === 'formula' ? (cell.formula ?? '') : display;
    const style = cell?.style;
    return {
      kind: GridCellKind.Text,
      data: editData,
      displayData: display,
      allowOverlay: true,
      contentAlign: style?.textAlign ?? (cell?.type === 'number' || cell?.type === 'formula' ? 'right' : 'left'),
      themeOverride: style ? buildTheme(style) : undefined,
    };
  }, [sheet, computed]);

  const onCellEdited = useCallback(([col, row]: Item, newValue: EditableGridCell) => {
    if (newValue.kind !== GridCellKind.Text) return;
    setCellInput(row, col, newValue.data);
  }, [setCellInput]);

  const onGridSelectionChange = useCallback((sel: GridSelection) => {
    const cell = sel.current?.cell;
    if (cell) select(cell[1], cell[0]); // [col, row] → (row, col)
    const r = sel.current?.range;
    if (r && (r.width > 1 || r.height > 1)) {
      selectRange({ r0: r.y, c0: r.x, r1: r.y + r.height - 1, c1: r.x + r.width - 1 });
    } else {
      selectRange(null);
    }
  }, [select, selectRange]);

  return (
    <DataEditor
      columns={columns}
      rows={sheet.rowCount}
      getCellContent={getCellContent}
      onCellEdited={onCellEdited}
      onGridSelectionChange={onGridSelectionChange}
      onColumnResize={(_col, newSize, colIndex) => setColWidth(colIndex, newSize)}
      getCellsForSelection={true}
      onPaste={(target, values) => { pasteValues(target[1], target[0], values); return false; }}
      onDelete={(sel) => { clearRange(); return sel; }}
      rowMarkers="number"
      smoothScrollX
      smoothScrollY
      width="100%"
      height="100%"
      freezeColumns={sheet.frozenCols}
    />
  );
};
