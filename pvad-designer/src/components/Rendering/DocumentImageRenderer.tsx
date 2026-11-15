import { useEffect, useState } from 'react';
import type { SimpleElement } from '@/types';
import { pdfFontFamily } from './PdfTextRenderer';

export default function DocumentImageRenderer({ element, label }: { element: SimpleElement; label: string }) {
  const [failedSource, setFailedSource] = useState<string | null>(null);
  const source = element.content ?? '';
  const fit = element.preserveAspectRatio || !['contain', 'cover', 'fill', 'none'].includes(element.fitMode ?? '')
    ? 'contain' : element.fitMode;
  const focus = (value?: number) => Number.isFinite(value) ? Math.max(0, Math.min(100, value!)) : 50;
  useEffect(() => { setFailedSource(null); }, [source]);
  if (!source || source === failedSource) return (
    <svg role="img" aria-label={label} data-image-placeholder="true" viewBox={`0 0 ${element.width} ${element.height}`}
      width="100%" height="100%" preserveAspectRatio="none" style={{ display: 'block' }}>
      <rect x="0" y="0" width={element.width} height={element.height}
        fill="none" stroke="#d1d5db" strokeWidth="0.5" strokeDasharray="3 3" />
      {element.height > 14 && <text x="4" y={element.height / 2 + Math.min(10, element.height * 0.35) * 0.3}
        fontFamily={pdfFontFamily()} fontSize={Math.min(10, element.height * 0.35)} fill="#9ca3af">[Image]</text>}
    </svg>
  );
  return <div style={{ overflow: 'hidden', width: '100%', height: '100%', position: 'relative' }}>
    <img src={source} alt={label} draggable={false} onError={() => setFailedSource(source)} style={{
      display: 'block', width: '100%', height: '100%',
      objectFit: fit,
      objectPosition: `${focus(element.focalX)}% ${focus(element.focalY)}%`,
    }} />
  </div>;
}
