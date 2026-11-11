import type { SimpleElement } from '@/types';
import { pdfFontFamily, wrapPdfText } from './PdfTextRenderer';

const sans = pdfFontFamily();

const frame = (element: SimpleElement) => `0 0 ${element.width} ${element.height}`;

export function PdfLayoutBoundaryRenderer({ element }: { element: SimpleElement }) {
  return (
    <svg viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.25" y="0.25" width={element.width - 0.5} height={element.height - 0.5} fill="none" stroke="#d1d5db" strokeWidth="0.5" strokeDasharray="4 4" />
    </svg>
  );
}

export function PdfNoteRenderer({ element }: { element: SimpleElement }) {
  const title = element.noteTitle ?? 'Note';
  const body = element.noteBody ?? '';
  return (
    <svg data-pdf-overflow="true" viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block', overflow: 'visible' }}>
      <rect x="0" y="0" width={element.width} height={element.height} fill={element.style?.backgroundColor ?? '#fef9c3'} stroke={element.style?.borderColor ?? '#fbbf24'} strokeWidth="1" />
      {title && <text x="8" y={4 + 11 * 0.72} fill="#78350f" fontFamily={sans} fontSize="11" fontWeight="700">{title}</text>}
      {wrapPdfText(body, Math.max(1, element.width - 16), 10).map((line, index) => line &&
        <text key={index} x="8" y={20 + 10 * 0.72 + index * 12} fill="#92400e" fontFamily={sans} fontSize="10">{line}</text>)}
      {element.noteAuthor && <text x="8" y={element.height - 12} fill="#92400e" fontFamily={sans} fontSize="9" fontStyle="italic">— {element.noteAuthor}</text>}
    </svg>
  );
}

export function PdfCommentRenderer({ element }: { element: SimpleElement }) {
  const fontSize = Number(element.style?.fontSize ?? 10);
  const meta = [element.commentAuthor, element.commentDate].filter(Boolean).join('  ');
  const bodyTop = 4 + (meta ? fontSize * 1.4 : 0);
  return (
    <svg viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.25" y="0.25" width={element.width - 0.5} height={element.height - 0.5} fill="#fef9c3" stroke="#ca8a04" strokeWidth="0.5" />
      {meta && <text x="4" y={4 + fontSize * 0.85 * 0.72} fill="#78716c" fontFamily={sans} fontSize={fontSize * 0.85} fontWeight="700">{meta}</text>}
      {(element.commentText ?? element.content) && <text x="4" y={bodyTop + fontSize * 0.72} fill="#1c1917" fontFamily={sans} fontSize={fontSize}>{element.commentText ?? element.content}</text>}
    </svg>
  );
}

export function PdfContentControlRenderer({ element }: { element: SimpleElement }) {
  const fontSize = Number(element.style?.fontSize ?? 11);
  const content = element.content ?? element.contentControlPlaceholder ?? '';
  const label = element.contentControlTitle ?? element.contentControlTag ?? element.type;
  return (
    <svg data-content-control="true" viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block', overflow: 'visible' }}>
      <rect x="0" y="0" width={element.width} height={element.height} fill="none" stroke="#6b7280" strokeWidth="0.7" />
      {label && <text x="3" y={-fontSize * 0.75 * 0.28} fill="#6b7280" fontFamily={sans} fontSize={fontSize * 0.75}>{label}</text>}
      {wrapPdfText(content, Math.max(1, element.width - 8), fontSize).map((line, index) => line &&
        <text key={index} x="4" y={fontSize + fontSize * 0.72 + index * fontSize * 1.2}
          fill={element.style?.color ?? '#111827'} fontFamily={sans} fontSize={fontSize}>{line}</text>)}
    </svg>
  );
}

export function PdfFootnoteRenderer({ element, pageHeight }: { element: SimpleElement; pageHeight: number }) {
  const reference = element.footnoteRef ?? '*';
  const size = Number(element.style?.fontSize ?? 9);
  const bodySize = size * 0.9;
  const body = element.footnoteText ?? element.content ?? '';
  const baseline = pageHeight - element.y - 32 - Number(element.style?.footnoteBottomOffset ?? 0);
  return <svg data-pdf-overflow="true" viewBox={frame(element)} width="100%" height="100%"
    preserveAspectRatio="none" style={{ display: 'block', overflow: 'visible' }}>
    <text x="0" y={size * 0.72} fontFamily={sans} fontSize={size} fill={element.style?.color ?? '#374151'}>{reference}</text>
    {body.trim() && wrapPdfText(`${reference} ${body}`, element.width, bodySize).map((line, index) => line &&
      <text key={index} data-footnote-body="true" x="0" y={baseline + index * bodySize * 1.2}
        fontFamily={sans} fontSize={bodySize} fill="#6b7280">{line}</text>)}
  </svg>;
}
