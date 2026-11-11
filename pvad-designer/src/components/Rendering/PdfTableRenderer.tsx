import type { SimpleElement } from '@/types';
import { pdfFontFamily, pdfTextWidth, wrapPdfText } from './PdfTextRenderer';

export function canRenderPdfTable(element: SimpleElement) {
  const columns = element.cellData?.[0]?.length ?? 0;
  return Boolean(columns && !element.cellStyles?.length
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
  const heights = lines.map(row => Math.max(element.height / rows.length, 16,
    Math.max(1, ...row.map(cell => cell.length)) * size * 1.2 + padding * 2));
  return { rows, widths, heights, lines, size, padding, family, header: Boolean(element.headerRow || matrixHeaders.length) };
}

export function PdfTableRenderer({ element }: { element: SimpleElement }) {
  const table = layoutPdfTable(element);
  if (!table) return null;
  const style = element.style ?? {};
  const { rows, widths, heights, lines, size, padding, family, header } = table;
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
          const alignment = element.columnAlignments?.length === widths.length ? element.columnAlignments[column] : 'left';
          const cellLines = lines[row][column];
          const lineHeight = size * 1.2;
          const baseline = ys[row] + (heights[row] - cellLines.length * lineHeight) / 2 + lineHeight;
          return <g key={column} data-table-cell={`${row}:${column}`}>
            {cellLines.map((line, index) => line && <text key={index}
              x={Math.max(xs[column] + padding, alignment === 'right' ? xs[column] + width - padding - pdfTextWidth(line, size, family)
                : alignment === 'center' ? xs[column] + (width - pdfTextWidth(line, size, family)) / 2 : xs[column] + padding)}
              y={baseline + index * lineHeight} fontSize={size} fontFamily={pdfFontFamily(family)}
              fontWeight={isHeader || footer ? 'bold' : style.cellFontWeight ?? 'normal'}
              fill={style.cellColor ?? style.color ?? '#101828'}>{line}</text>)}
          </g>;
        })}
      </g>;
    })}
    <g fill="none" stroke={style.borderColor ?? '#e5e7eb'} strokeWidth={border}>
      <rect x="0" y="0" width={element.width} height={height} />
      {ys.slice(1).map(y => <line key={y} x1="0" y1={y} x2={element.width} y2={y} />)}
      {xs.slice(1).map(x => <line key={x} x1={x} y1="0" x2={x} y2={height} />)}
    </g>
  </svg>;
}
