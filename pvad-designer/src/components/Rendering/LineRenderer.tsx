import type { SimpleElement } from '@/types';

export default function LineRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const color = style.color ?? style.borderColor ?? '#374151';
  const strokeWidth = Math.max(Number(style.strokeWidth ?? 1), 0.5);
  const horizontal = element.width >= element.height;
  const dashStyle = style.dashStyle ?? style.borderStyle;
  const dash = dashStyle === 'dashed' ? '8 4'
    : dashStyle === 'dotted' ? `${strokeWidth} ${strokeWidth * 2}` : undefined;

  return (
    <svg data-pdf-overflow="true" viewBox={`0 0 ${element.width} ${element.height}`}
      width="100%" height="100%" preserveAspectRatio="none" style={{ display: 'block', overflow: 'visible' }}>
      <line x1={horizontal ? 0 : element.width / 2} y1={horizontal ? element.height / 2 : 0}
        x2={horizontal ? element.width : element.width / 2} y2={horizontal ? element.height / 2 : element.height}
        stroke={color} strokeWidth={strokeWidth} strokeDasharray={dash} />
    </svg>
  );
}
