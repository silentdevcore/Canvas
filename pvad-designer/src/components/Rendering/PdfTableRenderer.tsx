import type { CellStyle, SimpleElement } from '@/types';
import { pdfFontFamily, pdfTextWidth, wrapPdfText } from './PdfTextRenderer';

export function canRenderPdfTable(element: SimpleElement) {
  const columns = element.cellData?.[0]?.length ?? 0;
  return Boolean(columns
    && !(element.columnWidths?.length === columns
      && element.columnWidths.some(value => !Number.isFinite(value) || value <= 0)));
}

export function layoutPdfTable(element: SimpleElement) {
  const style = element.style ?? {};
  const source = element.cellData ?? [];
  const columns = source[0]?.length ?? 0;
  if (!source.length || !columns) return null;
  const matrixHeaders = ['rdlTablixColumnHierarchy', 'rdlTablixRowHierarchy'].flatMap(key =>
    Array.isArray(style[key]) ? style[key].flatMap((member: { headerText?: string; groupName?: string }) => {
      const text = member.headerText || member.groupName;
      return text?.trim() ? [text] : [];
    }) : []);
  const rows = [...matrixHeaders.map(text => [text, ...Array<string>(columns - 1).fill('')]), ...source];
  const size = Number(style.cellFontSize ?? style.fontSize ?? 11);
  const padding = Number(style.cellPadding ?? 4);
  const family = style.cellFontFamily ?? style.fontFamily;
  const weights = element.columnWidths?.length === columns ? element.columnWidths : Array<number>(columns).fill(1);
  if (weights.some(value => !Number.isFinite(value) || value <= 0)) return null;
  const total = weights.reduce((sum, value) => sum + value, 0);
  const widths = weights.map(value => element.width * value / total);
  const lines = rows.map(row => widths.map((width, index) => {
    const content = row[index] ?? '';
    return width <= padding * 2 ? [content] : wrapPdfText(content, width - padding * 2, size, family);
  }));
  const overrides = new Map<string, CellStyle>();
  for (const cell of element.cellStyles ?? []) {
    if (cell.row < 0 || cell.row >= source.length || cell.col < 0 || cell.col >= columns) continue;
    const key = `${cell.row + matrixHeaders.length}:${cell.col}`;
    if (!overrides.has(key)) overrides.set(key, cell);
  }
  const header = Boolean(element.headerRow || matrixHeaders.length);
  const cells = rows.map((row, r) => widths.map((width, c) => {
    const cell = overrides.get(`${r}:${c}`);
    const fontSize = cell?.fontSize ?? size;
    const cellPadding = cell?.padding ?? padding;
    const fontFamily = cell?.fontFamily ?? family;
    const text = row[c] ?? '';
    return { cell, size: fontSize, padding: cellPadding, family: fontFamily,
      bold: cell?.bold ?? ((header && r === 0) || (element.footerRow && r === rows.length - 1)
        || ['bold', '600', '700'].includes(String(style.cellFontWeight))),
      italic: cell?.italic ?? false,
      color: cell?.color ?? style.cellColor ?? style.color ?? '#101828',
      alignment: cell?.textAlign ?? (element.columnAlignments?.length === columns ? element.columnAlignments[c] : 'left'),
      lines: width <= cellPadding * 2 ? [text] : wrapPdfText(text, width - cellPadding * 2, fontSize, fontFamily) };
  }));
  const heights = overrides.size ? cells.map(row => Math.max(element.height / rows.length, 16,
    ...row.map(cell => cell.lines.length * cell.size * 1.2 + cell.padding * 2))) : lines.map(row => Math.max(element.height / rows.length, 16,
    Math.max(1, ...row.map(cell => cell.length)) * size * 1.2 + padding * 2));
  return { rows, widths, heights, lines, cells, overrides, size, padding, family, header };
}

export function pdfTableEdges(element: SimpleElement, table: NonNullable<ReturnType<typeof layoutPdfTable>>) {
  const { widths, heights, overrides } = table;
  const xs = [0], ys = [0];
  for (const width of widths) xs.push(xs[xs.length - 1] + width);
  for (const height of heights) ys.push(ys[ys.length - 1] + height);
  const edges: { x1: number; y1: number; x2: number; y2: number; width: number; color: string }[] = [];
  const side = (r: number, c: number, name: 'Top' | 'Right' | 'Bottom' | 'Left') => {
    const cell = overrides.get(`${r}:${c}`);
    const border = cell?.[`border${name}`];
    const custom = cell && (cell.borderWidth != null || cell.borderColor != null
      || cell.borderTop || cell.borderRight || cell.borderBottom || cell.borderLeft);
    return custom ? { explicit: true, width: border?.width ?? (cell.borderWidth != null || cell.borderColor != null ? cell.borderWidth ?? 1 : 0),
      color: border?.color ?? cell.borderColor ?? '#000000' }
      : { explicit: false, width: Math.max(Number(element.style?.borderWidth ?? 0.75), 0.5), color: element.style?.borderColor ?? '#e5e7eb' };
  };
  const edge = (a: ReturnType<typeof side>, b: ReturnType<typeof side>, x1: number, y1: number, x2: number, y2: number) => {
    const chosen = a.explicit !== b.explicit ? a.explicit ? a : b : a.width > b.width ? a : b;
    if (chosen.width > 0) edges.push({ x1, y1, x2, y2, width: chosen.width, color: chosen.color });
  };
  for (let r = 0; r < heights.length; r++) for (let c = 0; c <= widths.length; c++) {
    const left = side(r, c - 1, 'Right'), right = side(r, c, 'Left');
    edge(c === 0 ? right : left, c === widths.length ? left : right, xs[c], ys[r], xs[c], ys[r + 1]);
  }
  for (let r = 0; r <= heights.length; r++) for (let c = 0; c < widths.length; c++) {
    const above = side(r - 1, c, 'Bottom'), below = side(r, c, 'Top');
    edge(r === 0 ? below : above, r === heights.length ? above : below, xs[c], ys[r], xs[c + 1], ys[r]);
  }
  return edges;
}

export function PdfTableRenderer({ element }: { element: SimpleElement }) {
  const table = layoutPdfTable(element);
  if (!table) return null;
  const style = element.style ?? {};
  const { rows, widths, heights, cells, header } = table;
  const height = heights.reduce((sum, value) => sum + value, 0);
  const border = Math.max(Number(style.borderWidth ?? 0.75), 0.5);
  const offsets = (values: number[]) => values.map((_, index) => values.slice(0, index).reduce((sum, value) => sum + value, 0));
  const xs = offsets(widths);
  const ys = offsets(heights);
  return <svg data-pdf-overflow="true" viewBox={`0 0 ${element.width} ${element.height}`}
    width="100%" height="100%" preserveAspectRatio="none" style={{ display: 'block', overflow: 'visible' }}>
    {rows.map((_, row) => {
      const isHeader = header && row === 0;
      const footer = element.footerRow && row === rows.length - 1;
      const bodyIndex = row - (header ? 1 : 0);
      const fill = isHeader ? element.headerBgColor ?? '#f0f7ff'
        : footer ? style.footerBgColor ?? '#f7fafc'
        : element.zebraEnabled && bodyIndex >= 0 && bodyIndex % 2 === 1 ? element.zebraColor ?? '#f5f5f5' : undefined;
      return <g key={row} data-table-row={row}>
        {fill && <rect x="0" y={ys[row]} width={element.width} height={heights[row]} fill={fill} />}
        {widths.map((width, column) => {
          const cell = cells[row][column];
          const { size, padding, family, alignment } = cell;
          const cellLines = cell.lines;
          const lineHeight = size * 1.2;
          const baseline = ys[row] + (heights[row] - cellLines.length * lineHeight) / 2 + lineHeight;
          return <g key={column} data-table-cell={`${row}:${column}`}>
            {cell.cell?.backgroundColor && <rect x={xs[column]} y={ys[row]} width={width} height={heights[row]} fill={cell.cell.backgroundColor} />}
            {cellLines.map((line, index) => line && <text key={index}
              x={Math.max(xs[column] + padding, alignment === 'right' ? xs[column] + width - padding - pdfTextWidth(line, size, family)
                : alignment === 'center' ? xs[column] + (width - pdfTextWidth(line, size, family)) / 2 : xs[column] + padding)}
              y={baseline + index * lineHeight} fontSize={size} fontFamily={pdfFontFamily(family)}
              fontWeight={cell.bold ? 'bold' : 'normal'} fontStyle={cell.italic ? 'italic' : 'normal'}
              fill={cell.color}>{line}</text>)}
          </g>;
        })}
      </g>;
    })}
    {table.overrides.size ? <g fill="none" data-cell-borders="true">
      {pdfTableEdges(element, table).map((edge, index) => <line key={index} x1={edge.x1} y1={edge.y1} x2={edge.x2} y2={edge.y2}
        strokeWidth={edge.width} stroke={edge.color} />)}
    </g> : <g fill="none" stroke={style.borderColor ?? '#e5e7eb'} strokeWidth={border}>
      <rect x="0" y="0" width={element.width} height={height} />
      {ys.slice(1).map(y => <line key={y} x1="0" y1={y} x2={element.width} y2={y} />)}
      {xs.slice(1).map(x => <line key={x} x1={x} y1="0" x2={x} y2={height} />)}
    </g>}
  </svg>;
}
