import type { SimpleElement } from '@/types';
import { pdfFontFamily } from './PdfTextRenderer';

export function PdfWatermarkRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const size = Number(style.fontSize ?? 48) * Math.max(Number(style.scale ?? 1), 0.1);
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} width="100%" height="100%"
      preserveAspectRatio="none" style={{ display: 'block', overflow: 'visible', opacity: style.opacity ?? 0.18 }}>
      {element.watermarkMode === 'image'
        ? <image href={element.content ?? ''} width={element.width} height={element.height} preserveAspectRatio="xMidYMid meet" />
        : <text x={element.width / 2} y={element.height / 2 + size * 0.22} textAnchor="middle"
          fontFamily={pdfFontFamily(style.fontFamily)} fontSize={size}
          fontWeight={style.fontWeight ?? 'normal'} fontStyle={style.fontStyle ?? 'normal'}
          fill={style.color ?? '#64748b'}>{element.content ?? ''}</text>}
    </svg>
  );
}

export function PdfTableOfContentsRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const size = Number(style.fontSize ?? 12);
  const titleSize = size * 1.4;
  const entries = (element.tocEntries ?? []).filter(entry =>
    entry.level >= (element.tocMinLevel ?? 1) && entry.level <= (element.tocMaxLevel ?? 3) && entry.text.trim());
  if (!entries.length) return null;
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} width="100%" height="100%"
      preserveAspectRatio="none" style={{ display: 'block', overflow: 'hidden' }}
      fontFamily={pdfFontFamily(style.fontFamily)} fill={style.color ?? '#1f2937'}>
      <text x="0" y={titleSize * 0.72} fontSize={titleSize} fontWeight="700">
        {element.tocTitle?.trim() || 'Table of Contents'}
      </text>
      {entries.map((entry, index) => {
        const top = titleSize + 8 + index * size * 1.6;
        if (top + size * 1.6 > element.height) return null;
        const indent = (entry.level - 1) * 12;
        const showPages = element.tocShowPageNumbers !== false;
        const textWidth = showPages ? Math.max(element.width - indent - 34, 10) : Math.max(element.width - indent, 10);
        return <g key={index} fontSize={size}>
          <text x={indent} y={top + size * 0.72}>{entry.text}</text>
          {showPages && element.tocShowLeaderDots !== false && (
            <text x={indent + textWidth} y={top + size * 0.72}>.</text>
          )}
          {showPages && <text x={element.width} y={top + size * 0.72} textAnchor="end">{entry.page}</text>}
        </g>;
      })}
    </svg>
  );
}
