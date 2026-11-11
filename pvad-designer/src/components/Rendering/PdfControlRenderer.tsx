import type { SimpleElement } from '@/types';
import { useId } from 'react';
import { pdfFontFamily } from './PdfTextRenderer';

const sans = pdfFontFamily();

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
        <path d={`M 2.5 ${top + 6.5} L 5.5 ${top + 10} L 11.5 ${top + 3}`} fill="none" stroke={color} strokeWidth="1.5" />
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
  const interactive = Boolean(element.options?.length);
  const fontSize = Math.max(interactive ? 6 : 1, Number(style.fontSize ?? 12));
  const value = interactive ? element.selectedValue || element.options![0] : element.fieldLabel ?? element.content ?? '';
  const clip = useId();
  const baseline = interactive ? (element.height - fontSize) / 2 + fontSize * 0.8
    : (element.height - fontSize * 1.4) / 2 + fontSize * 0.72;
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} preserveAspectRatio="none" width="100%" height="100%" style={{ display: 'block' }}>
      <defs><clipPath id={clip}><rect x="1" y="1" width={Math.max(0, element.width - 2)} height={Math.max(0, element.height - 2)} /></clipPath></defs>
      <rect x={interactive ? 0.5 : 0} y={interactive ? 0.5 : 0}
        width={element.width - (interactive ? 1 : 0)} height={element.height - (interactive ? 1 : 0)}
        fill="#ffffff" stroke={interactive ? '#000000' : style.borderColor ?? '#d1d5db'} strokeWidth="1" />
      {value && <text x={interactive ? 2 : 6} y={baseline} fill={interactive ? '#000000' : style.color ?? '#374151'}
        clipPath={interactive ? `url(#${clip})` : undefined} fontFamily={pdfFontFamily()} fontSize={fontSize}>{value}</text>}
      {!interactive && <text x={element.width - 14} y={baseline} fill="#9ca3af" fontFamily={pdfFontFamily()} fontSize={fontSize}>v</text>}
    </svg>
  );
}

export function PdfRadioRenderer({ element }: { element: SimpleElement }) {
  const size = Number(element.style?.fontSize ?? 11);
  const marker = Math.max(10, Math.min(14, size + 2));
  const color = element.style?.color ?? '#101828';
  return <svg viewBox={`0 0 ${element.width} ${element.height}`} width="100%" height="100%"
    preserveAspectRatio="none" style={{ display: 'block', overflow: 'hidden' }}>
    {(element.options ?? []).map((option, index) => {
      const top = index * size * 1.7;
      if (top + size > element.height) return null;
      const selected = option === element.selectedValue || (element.selectedValue == null && index === 0);
      return <g key={index} data-radio-selected={selected}>
        <circle cx={marker / 2} cy={top + marker / 2} r={marker / 2 - 0.5} fill="white" stroke={color} />
        {selected && <circle cx={marker / 2} cy={top + marker / 2} r={marker * 0.25} fill={color} />}
        <text x={marker + 6} y={top + size * 0.72} fontSize={size}
          fontFamily={pdfFontFamily(element.style?.fontFamily)} fill={color}>{option}</text>
      </g>;
    })}
  </svg>;
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
      <rect x={0} y={top} width={size} height={size} fill="none" stroke={color} strokeWidth={stroke} />
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
      <rect x="0" y="0" width={element.width} height={element.height} rx={radius} fill={fill} stroke={fill} strokeWidth="0.5" />
      {element.content && <text x="8" y={baseline} fill={style.color ?? '#ffffff'} fontFamily={sans} fontSize={fontSize} fontWeight="700">{element.content}</text>}
    </svg>
  );
}
