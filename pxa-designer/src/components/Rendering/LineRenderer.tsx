import type { SimpleElement } from '@/types';

export default function LineRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const color = style.color ?? style.borderColor ?? '#374151';
  const strokeWidth = Math.max(Number(style.strokeWidth ?? 1), 0.5);
  const horizontal = element.width >= element.height;
  const dashStyle = style.dashStyle ?? style.borderStyle;
  const background = dashStyle === 'dashed'
    ? `repeating-linear-gradient(${horizontal ? '90deg' : '0deg'}, ${color} 0 ${strokeWidth * 4}px, transparent ${strokeWidth * 4}px ${strokeWidth * 6}px)`
    : dashStyle === 'dotted'
      ? `repeating-linear-gradient(${horizontal ? '90deg' : '0deg'}, ${color} 0 ${strokeWidth}px, transparent ${strokeWidth}px ${strokeWidth * 3}px)`
      : color;

  return (
    <div style={{
      position: 'absolute',
      left: horizontal ? 0 : `calc(50% - ${strokeWidth / 2}px)`,
      top: horizontal ? `calc(50% - ${strokeWidth / 2}px)` : 0,
      width: horizontal ? '100%' : strokeWidth,
      height: horizontal ? strokeWidth : '100%',
      background,
    }} />
  );
}
