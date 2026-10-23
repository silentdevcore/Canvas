import type { SimpleElement } from '@/types';

export default function ArrowRenderer({ element }: { element: SimpleElement }) {
  const color = element.style?.color ?? '#374151';
  const strokeWidth = Math.max(Number(element.style?.strokeWidth ?? 1.5), 0.5);
  const width = element.width;
  const height = element.height;
  const middle = height / 2;
  const tipWidth = Math.min(10, width * 0.2);
  const dotRadius = tipWidth * 0.4;
  const startMarker = element.startMarker ?? 'none';
  const endMarker = element.endMarker ?? 'arrow';
  const lineStart = startMarker !== 'none' ? tipWidth : 0;
  const lineEnd = endMarker !== 'none' ? width - tipWidth : width;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} width="100%" height="100%" preserveAspectRatio="none">
      <line x1={lineStart} y1={middle} x2={lineEnd} y2={middle} stroke={color} strokeWidth={strokeWidth} />
      {(startMarker === 'arrow' || startMarker === 'filled') && (
        <polygon points={`0,${middle} ${tipWidth},${middle - tipWidth / 2} ${tipWidth},${middle + tipWidth / 2}`}
          fill={color} stroke={color} strokeWidth={0.5} />
      )}
      {startMarker === 'dot' && (
        <circle cx={dotRadius} cy={middle} r={dotRadius} fill={color} stroke={color} strokeWidth={0.5} />
      )}
      {(endMarker === 'arrow' || endMarker === 'filled') && (
        <polygon points={`${width},${middle} ${width - tipWidth},${middle - tipWidth / 2} ${width - tipWidth},${middle + tipWidth / 2}`}
          fill={color} stroke={color} strokeWidth={0.5} />
      )}
      {endMarker === 'dot' && (
        <circle cx={width - dotRadius} cy={middle} r={dotRadius} fill={color} stroke={color} strokeWidth={0.5} />
      )}
    </svg>
  );
}
