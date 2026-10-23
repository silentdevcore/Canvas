import React, { useCallback, useMemo } from 'react';
import {
  DataEditor, GridCellKind, type GridColumn, type GridCell, type Item, type EditableGridCell,
  type GridSelection, type Theme,
} from '@glideapps/glide-data-grid';
import '@glideapps/glide-data-grid/dist/index.css';
import { useSpreadsheetStore } from './store';
import { cellKey, colName, parseA1Range, type CellStyle } from './types';
import { formatCellValue } from './numberFormat';

export { colName };

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

  // Horizontal merges → glide column spans. Maps every covered "row:col" to its span + origin cell.
  // (glide renders column spans only; vertical/rectangular merges still export correctly to .xlsx.)
  const spanMap = useMemo(() => {
    const map = new Map<string, { span: [number, number]; or: number; oc: number }>();
    for (const m of sheet.merges) {
      const r = parseA1Range(m);
      if (r.r0 !== r.r1) continue; // single-row only
      for (let c = r.c0; c <= r.c1; c++) map.set(cellKey(r.r0, c), { span: [r.c0, r.c1], or: r.r0, oc: r.c0 });
    }
    return map;
  }, [sheet.merges]);

  const getCellContent = useCallback(([col, row]: Item): GridCell => {
    const merged = spanMap.get(cellKey(row, col));
    const cr = merged ? merged.or : row;
    const cc = merged ? merged.oc : col;   // a merged cell renders its origin's content
    const cell = sheet.cells[cellKey(cr, cc)];
    const image = sheet.images.find(candidate => candidate.row === cr && candidate.col === cc);
    if (image) {
      const source = image.contentUrl ?? image.data;
      if (source) {
        return {
          kind: GridCellKind.Image,
          data: [source],
          displayData: [image.altText ?? image.fileName ?? 'Image'],
          allowOverlay: false,
        };
      }
    }
    const value = computed(cr, cc);
    const display = formatCellValue(value, cell?.numberFormat); // number-format-aware display
    // On edit the overlay shows the formula source (or the raw value); the grid shows the formatted result.
    const editData = cell?.type === 'formula' ? (cell.formula ?? '') : (cell?.value != null ? String(cell.value) : '');
    const style = cell?.style;
    return {
      kind: GridCellKind.Text,
      data: editData,
      displayData: display,
      allowOverlay: true,
      ...(merged ? { span: merged.span } : {}),
      contentAlign: style?.textAlign ?? (cell?.type === 'number' || cell?.type === 'formula' ? 'right' : 'left'),
      themeOverride: style ? buildTheme(style) : undefined,
    };
  }, [sheet, computed, spanMap]);

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
