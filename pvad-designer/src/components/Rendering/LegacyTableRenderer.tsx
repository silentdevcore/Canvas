import type React from 'react';
import type { SimpleElement } from '@/types';
import { pdfFontFamily } from './PdfTextRenderer';

/** Retains incomplete legacy tables; complete cell matrices use PdfTableRenderer. */
export default function LegacyTableRenderer({ element }: { element: SimpleElement }) {
  const totalRows    = element.style?.rows ?? 3;
  const columns      = element.style?.columns ?? 3;
  const bw           = element.style?.borderWidth ?? 1;
  const bc           = element.style?.borderColor || '#000000';
  const cp           = element.style?.cellPadding ?? 5;
  const hasHeader    = element.headerRow ?? false;
  const hasFooter    = element.footerRow ?? false;
  const headerBg     = element.headerBgColor || '#f1f5f9';
  const zebraOn      = element.zebraEnabled ?? false;
  const zebraColor   = element.zebraColor || '#f9fafb';
  const colWidths    = element.columnWidths ?? [];
  const cellData     = element.cellData ?? [];
  const colAligns    = element.columnAlignments ?? [];
  const bodyRows     = Math.max(0, totalRows - (hasHeader ? 1 : 0) - (hasFooter ? 1 : 0));
  const rdlColumnHeaders = Array.isArray(element.style?.rdlTablixColumnHierarchy)
    ? element.style.rdlTablixColumnHierarchy
        .map((member: any) => member?.headerText || member?.groupName)
        .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
    : [];
  const rdlRowHeaders = Array.isArray(element.style?.rdlTablixRowHierarchy)
    ? element.style.rdlTablixRowHierarchy
        .map((member: any) => member?.headerText || member?.groupName)
        .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
    : [];
  const rdlMatrixHeaders = [...rdlColumnHeaders, ...rdlRowHeaders];
  const visualRowHeight = element.height / Math.max(1, totalRows + rdlMatrixHeaders.length);

  const cellFontSize   = element.style?.cellFontSize ?? 10;
  const cellFontFamily = element.style?.cellFontFamily ?? 'Arial';
  const cellColor      = element.style?.cellColor as string | undefined;
  const cellFontWeight = element.style?.cellFontWeight ?? 'normal';

  const cellStyles = element.cellStyles ?? [];
  const sideCss = (s?: { color?: string; width?: number }) =>
    s ? `${s.width ?? 1}px solid ${s.color ?? '#000000'}` : undefined;

  const tdStyle = (
    rowIdx: number,
    colIdx: number,
    kind: 'header' | 'body' | 'footer',
    dataRow: number = rowIdx
  ): React.CSSProperties => {
    const style: React.CSSProperties = {
      border: `${bw}px solid ${bc}`,
      padding: cp,
      textAlign: colAligns[colIdx] || 'left',
      fontSize: cellFontSize,
      fontFamily: pdfFontFamily(cellFontFamily),
      fontWeight: kind === 'header' || kind === 'footer' ? 700 : cellFontWeight,
      color: cellColor ?? '#101828',
      backgroundColor:
        kind === 'header' ? headerBg
        : kind === 'footer' ? '#f8fafc'
        : zebraOn && rowIdx % 2 === 1 ? zebraColor
        : 'transparent',
      width: colWidths[colIdx] ? colWidths[colIdx] : undefined,
    };

    // Sparse per-cell override (background / alignment / borders) keyed by the absolute data row.
    const cs = cellStyles.find((x) => x.row === dataRow && x.col === colIdx);
    if (cs) {
      if (cs.backgroundColor) style.backgroundColor = cs.backgroundColor;
      if (cs.textAlign) style.textAlign = cs.textAlign;
      if (cs.padding != null) style.padding = cs.padding;
      if (cs.fontFamily) style.fontFamily = cs.fontFamily;
      if (cs.fontSize != null) style.fontSize = cs.fontSize;
      if (cs.bold != null) style.fontWeight = cs.bold ? 700 : 400;
      if (cs.italic != null) style.fontStyle = cs.italic ? 'italic' : 'normal';
      if (cs.color) style.color = cs.color;
      const hasBorder = cs.borderColor != null || cs.borderWidth != null
        || cs.borderTop || cs.borderRight || cs.borderBottom || cs.borderLeft;
      if (hasBorder) {
        // Explicit per-cell borders replace the default grid border (parity with the image exporter).
        const uniform = (cs.borderColor != null || cs.borderWidth != null)
          ? `${cs.borderWidth ?? 1}px solid ${cs.borderColor ?? '#000000'}`
          : 'none';
        style.border = undefined;
        style.borderTop = sideCss(cs.borderTop) ?? uniform;
        style.borderRight = sideCss(cs.borderRight) ?? uniform;
        style.borderBottom = sideCss(cs.borderBottom) ?? uniform;
        style.borderLeft = sideCss(cs.borderLeft) ?? uniform;
      }
    }
    return style;
  };

  const cell = (r: number, c: number) => cellData[r]?.[c] ?? '';

  return (
    <table style={{
      width: '100%', height: '100%', borderCollapse: 'collapse',
      border: `${bw}px solid ${bc}`,
      tableLayout: colWidths.some(Boolean) ? 'fixed' : 'auto',
    }}>
      {colWidths.some(Boolean) && (
        <colgroup>
          {Array.from({ length: columns }).map((_, i) => (
            <col key={i} style={{ width: colWidths[i] ? colWidths[i] : undefined }} />
          ))}
        </colgroup>
      )}
      {hasHeader && (
        <thead>
          {rdlMatrixHeaders.map((header, index) => (
            <tr key={`rdl-matrix-header-${index}`} style={{ height: visualRowHeight }}>
              <th
                colSpan={columns}
                style={{
                  ...tdStyle(index, 0, 'header'),
                  textAlign: 'left',
                  backgroundColor: '#e0f2fe',
                  color: '#075985'
                }}
              >
                {header}
              </th>
            </tr>
          ))}
          <tr style={{ height: visualRowHeight }}>
            {Array.from({ length: columns }).map((_, c) => (
              <th key={c} style={tdStyle(0, c, 'header')}>
                {cell(0, c)}
              </th>
            ))}
          </tr>
        </thead>
      )}
      <tbody>
        {Array.from({ length: bodyRows }).map((_, r) => {
          const dataRow = r + (hasHeader ? 1 : 0);
          return (
            <tr key={r} style={{ height: visualRowHeight }}>
              {Array.from({ length: columns }).map((_, c) => (
                <td key={c} style={tdStyle(r, c, 'body', dataRow)}>
                  {cell(dataRow, c)}
                </td>
              ))}
            </tr>
          );
        })}
      </tbody>
      {hasFooter && (
        <tfoot>
          <tr style={{ height: visualRowHeight }}>
            {Array.from({ length: columns }).map((_, c) => (
              <td key={c} style={tdStyle(0, c, 'footer', totalRows - 1)}>
                {cell(totalRows - 1, c)}
              </td>
            ))}
          </tr>
        </tfoot>
      )}
    </table>
  );
}
