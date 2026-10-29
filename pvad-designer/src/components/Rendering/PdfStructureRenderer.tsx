import type { SimpleElement } from '@/types';

const sans = 'Arial, Helvetica, sans-serif';

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
    <svg viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.5" y="0.5" width={element.width - 1} height={element.height - 1} fill={element.style?.backgroundColor ?? '#fef9c3'} stroke={element.style?.borderColor ?? '#fbbf24'} strokeWidth="1" />
      {title && <text x="8" y={4 + 11 * 0.72} fill="#78350f" fontFamily={sans} fontSize="11" fontWeight="700">{title}</text>}
      {body && <text x="8" y={20 + 10 * 0.72} fill="#92400e" fontFamily={sans} fontSize="10">{body}</text>}
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
  return (
    <svg viewBox={frame(element)} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.35" y="0.35" width={element.width - 0.7} height={element.height - 0.7} fill="none" stroke="#6b7280" strokeWidth="0.7" />
      {content && <text x="4" y={fontSize + fontSize * 0.72} fill={element.style?.color ?? '#111827'} fontFamily={sans} fontSize={fontSize}>{content}</text>}
    </svg>
  );
}
