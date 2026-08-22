import type { SimpleElement } from '@/types';

const sans = 'Arial, Helvetica, sans-serif';

export function PdfCheckboxRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const size = 14;
  const top = (element.height - size) / 2;
  const color = style.borderColor ?? '#374151';
  const state = element.checkState ?? 'empty';
  const fontSize = Number(style.fontSize ?? 12);
  const label = element.fieldLabel ?? '';
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.75" y={top + 0.75} width="12.5" height="12.5" fill={style.backgroundColor ?? '#ffffff'} stroke={color} strokeWidth="1.5" />
      {state === 'checked' && (
        <path d={`M 2.5 ${top + 7.5} L 5.5 ${top + 4} L 11.5 ${top + 11}`} fill="none" stroke={color} strokeWidth="1.5" />
      )}
      {state === 'cross' && (
        <path d={`M 3 ${top + 11} L 11 ${top + 3} M 11 ${top + 11} L 3 ${top + 3}`} fill="none" stroke={color} strokeWidth="1.5" />
      )}
      {state === 'dot' && <circle cx="7" cy={top + 7} r="3" fill={color} />}
      {label && (
        <text x="22" y={(element.height - fontSize) / 2 + fontSize * 0.72} fill={style.color ?? '#101828'} fontFamily={sans} fontSize={fontSize}>
          {label}
        </text>
      )}
    </svg>
  );
}

export function PdfDropdownRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const fontSize = Number(style.fontSize ?? 12);
  const value = element.selectedValue ?? element.fieldLabel ?? element.content ?? '';
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.5" y="0.5" width={element.width - 1} height={element.height - 1} fill="#ffffff" stroke="#000000" strokeWidth="1" />
      {value && <text x="2" y={fontSize * 0.72} fill="#000000" fontFamily={sans} fontSize={fontSize}>{value}</text>}
    </svg>
  );
}

export function PdfCheckmarkRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const size = Math.min(element.height / 2, 16);
  const top = (element.height - size) / 2;
  const color = style.color ?? '#374151';
  const stroke = Number(style.borderWidth ?? 2);
  const state = element.checkState ?? 'checked';
  const fontSize = Number(style.fontSize ?? 12);
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x={stroke / 2} y={top + stroke / 2} width={size - stroke} height={size - stroke} fill="none" stroke={color} strokeWidth={stroke} />
      {state === 'checked' && <path d={`M ${size * 0.2} ${top + size / 2} L ${size * 0.45} ${top + size * 0.75} L ${size * 0.85} ${top + size * 0.2}`} fill="none" stroke={color} strokeWidth={stroke} />}
      {state === 'cross' && <path d={`M 4 ${top + size - 4} L ${size - 4} ${top + 4} M ${size - 4} ${top + size - 4} L 4 ${top + 4}`} fill="none" stroke={color} strokeWidth={stroke} />}
      {state === 'dot' && <circle cx={size / 2} cy={top + size / 2} r={size * 0.25} fill={color} />}
      {element.fieldLabel && <text x={size + 8} y={(element.height - fontSize) / 2 + fontSize * 0.72} fill={style.labelColor ?? color} fontFamily={sans} fontSize={fontSize}>{element.fieldLabel}</text>}
    </svg>
  );
}

export function PdfSignatureRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const lineY = element.height - 14;
  const color = style.borderColor ?? style.color ?? '#9ca3af';
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      {element.height > 30 && <text x={element.width / 2 - 25} y={lineY / 2} fill="#d1d5db" fontFamily={sans} fontSize="10" fontStyle="italic">Sign here</text>}
      <line x1="0" y1={lineY} x2={element.width} y2={lineY} stroke={color} strokeWidth="1" />
      <text x="0" y={element.height - 8} fill={style.labelColor ?? style.color ?? '#6b7280'} fontFamily={sans} fontSize="10" fontStyle="italic">{element.signatureLabel ?? 'Signature'}</text>
    </svg>
  );
}

export function PdfButtonRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const fontSize = Number(style.fontSize ?? 12);
  const radius = Math.min(Number(style.borderRadius ?? 6), element.width / 2, element.height / 2);
  const fill = style.backgroundColor ?? '#1d6fff';
  const baseline = (element.height - fontSize * 1.4) / 2 + fontSize * 0.72;
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <rect x="0.25" y="0.25" width={element.width - 0.5} height={element.height - 0.5} rx={radius} fill={fill} stroke={fill} strokeWidth="0.5" />
      {element.content && <text x="8" y={baseline} fill={style.color ?? '#ffffff'} fontFamily={sans} fontSize={fontSize} fontWeight="700">{element.content}</text>}
    </svg>
  );
}
