import type { SimpleElement } from '@/types';

export function PdfShapeRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const fill = style.backgroundColor ?? style.fill ?? 'none';
  const strokeWidth = Number(style.borderWidth ?? 0);
  const radius = Math.min(Number(style.borderRadius ?? 0), element.width / 2, element.height / 2);
  return (
    <svg data-pdf-overflow="true" viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block', overflow: 'visible' }}>
      <rect
        x={0}
        y={0}
        width={element.width}
        height={element.height}
        rx={radius}
        fill={fill === 'transparent' ? 'none' : fill}
        stroke={strokeWidth > 0 ? style.borderColor ?? '#000000' : 'none'}
        strokeWidth={strokeWidth}
      />
    </svg>
  );
}

export function PdfCircleRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const strokeWidth = Math.max(Number(style.borderWidth ?? 1), 0.5);
  return (
    <svg data-pdf-overflow="true" viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block', overflow: 'visible' }}>
      <ellipse
        cx={element.width / 2}
        cy={element.height / 2}
        rx={element.width / 2}
        ry={element.height / 2}
        fill={(style.backgroundColor ?? '#f3f4f6') === 'transparent' ? 'none' : style.backgroundColor ?? '#f3f4f6'}
        stroke={style.borderColor ?? '#d1d5db'}
        strokeWidth={strokeWidth}
      />
    </svg>
  );
}

export function PdfDrawingRenderer({ element }: { element: SimpleElement }) {
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <path
        d={element.pathData ?? ''}
        fill="none"
        stroke={element.style?.color ?? '#1d4ed8'}
        strokeWidth={element.style?.strokeWidth ?? 2}
      />
    </svg>
  );
}
