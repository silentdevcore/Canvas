import type { SimpleElement } from '@/types';

export function resizeTable(element: SimpleElement, rows: number, columns: number): Partial<SimpleElement> | null {
  if (!Number.isSafeInteger(rows) || !Number.isSafeInteger(columns) || rows < 1 || columns < 1) return null;
  return {
    style: { ...element.style, rows, columns,
      selectedCellRow: Math.min(Number(element.style?.selectedCellRow ?? 0), rows - 1),
      selectedCellCol: Math.min(Number(element.style?.selectedCellCol ?? 0), columns - 1) },
    cellData: Array.from({ length: rows }, (_, row) =>
      Array.from({ length: columns }, (_, column) => element.cellData?.[row]?.[column] ?? '')),
    columnWidths: element.columnWidths && Array.from({ length: columns }, (_, column) => element.columnWidths?.[column] ?? 1),
    columnAlignments: element.columnAlignments && Array.from({ length: columns }, (_, column) => element.columnAlignments?.[column] ?? 'left'),
    cellStyles: element.cellStyles?.filter(cell => cell.row < rows && cell.col < columns),
  };
}
