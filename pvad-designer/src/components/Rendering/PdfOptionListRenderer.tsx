import type { SimpleElement } from '@/types';
import { pdfFontFamily } from './PdfTextRenderer';

export function isOptionListForm(element: SimpleElement) {
  return Boolean(element.fieldName?.trim() || element.fieldLabel?.trim()
    || element.selectedValue != null || element.multiSelect != null
    || element.accessibility?.role?.toLowerCase() === 'form');
}

export function optionListMarker(style: string, number: number) {
  let alpha = '';
  for (let value = number; value > 0; value = Math.floor((value - 1) / 26)) {
    alpha = String.fromCharCode(97 + ((value - 1) % 26)) + alpha;
  }
  let roman = '';
  if (style === 'lower-roman' || style === 'upper-roman') {
    let value = number;
    for (const [amount, symbol] of [[1000, 'M'], [900, 'CM'], [500, 'D'], [400, 'CD'],
      [100, 'C'], [90, 'XC'], [50, 'L'], [40, 'XL'], [10, 'X'], [9, 'IX'],
      [5, 'V'], [4, 'IV'], [1, 'I']] as const) {
      const count = Math.max(0, Math.floor(value / amount));
      roman += symbol.repeat(count);
      value -= count * amount;
    }
  }
  switch (style) {
    case 'none': return '';
    case 'decimal': return `${number}. `;
    case 'lower-alpha': return `${alpha}. `;
    case 'upper-alpha': return `${alpha.toUpperCase()}. `;
    case 'lower-roman': return `${roman.toLowerCase()}. `;
    case 'upper-roman': return `${roman}. `;
    case 'dash': return '- ';
    case 'asterisk': return '* ';
    case 'circle': return 'o ';
    case 'square': return '\u25a0 ';
    default: return '\u2022 ';
  }
}

export function PdfOptionListRenderer({ element }: { element: SimpleElement }) {
  const form = isOptionListForm(element);
  const fontSize = Math.max(form ? 6 : 1, Number(element.style?.fontSize ?? 11));
  const lineHeight = fontSize * (form ? 1.1 : 1.7);
  const listStyle = element.listStyle ?? String(element.style?.markerStyle ?? (element.ordered ? 'decimal' : 'disc'));
  return (
    <svg viewBox={`0 0 ${element.width} ${element.height}`} width="100%" height="100%"
      preserveAspectRatio="none" style={{ display: 'block', overflow: 'hidden' }} aria-hidden="true">
      {form && <rect x="0.5" y="0.5" width={Math.max(0, element.width - 1)} height={Math.max(0, element.height - 1)}
        fill="#ffffff" stroke="#000000" />}
      {(element.options ?? []).map((option, index) => {
        const selected = option === element.selectedValue;
        const top = (form ? 2 : 0) + index * lineHeight;
        if (top + fontSize > element.height) return null;
        return (
          <g key={index}>
            {form && selected && <rect x="1" y={top} width={Math.max(0, element.width - 2)} height={lineHeight} fill="#000000" />}
            <text x={form ? 2 : 0} y={top + fontSize * (form ? 0.8 : 0.72)}
              fontFamily={pdfFontFamily(form ? undefined : element.style?.fontFamily)} fontSize={fontSize}
              fill={form ? selected ? '#ffffff' : '#000000' : element.style?.color ?? '#101828'}>
              {form ? option : `${optionListMarker(listStyle, (element.startNumber ?? 1) + index)}${option}`}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
