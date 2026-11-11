import type { SimpleElement } from '@/types';
import { pdfFontFamily } from './PdfTextRenderer';

export const FORM_LABEL_OFFSET = 16;
export const FORM_FIELD_MAX_HEIGHT = 24;
export const FORM_TEXTAREA_MAX_HEIGHT = 20;

interface Props {
  element: SimpleElement;
  multiline?: boolean;
  resolveContent?: (value: string | undefined) => string | undefined;
}

export default function FormFieldRenderer({ element, multiline = false, resolveContent = value => value }: Props) {
  const style = element.style ?? {};
  const fontSize = Number(style.fontSize ?? 11);
  const boxHeight = Math.min(
    Math.max(element.height - FORM_LABEL_OFFSET, 2),
    multiline ? FORM_TEXTAREA_MAX_HEIGHT : FORM_FIELD_MAX_HEIGHT,
  );
  const label = resolveContent(element.fieldLabel || element.fieldName) ?? '';
  // PDF AcroForm normal appearances do not expose a placeholder and use a black widget border.
  const borderColor = '#000000';
  const labelColor = style.color ?? '#374151';
  const background = multiline
    ? '#ffffff'
    : style.backgroundColor && style.backgroundColor !== 'transparent'
      ? style.backgroundColor
      : '#ffffff';

  return (
    <svg
      className="pvad-form-field-renderer"
      viewBox={`0 0 ${element.width} ${element.height}`}
      width="100%" height="100%" preserveAspectRatio="none"
      style={{ display: 'block', overflow: 'visible' }}
      data-pdf-overflow="true"
    >
      {label && (
        <g fontFamily={pdfFontFamily()} fontSize={fontSize}>
          <text x="0" y={2 + fontSize * 0.72} fill={labelColor}>{label}</text>
          {element.required && <text x={label.length * fontSize * 0.52}
            y={2 + fontSize * 0.72} fill="#ef4444"> *</text>}
        </g>
      )}
      <rect x="0.5" y={FORM_LABEL_OFFSET + 0.5} width={Math.max(0, element.width - 1)} height={boxHeight - 1}
        fill={background} stroke={borderColor} strokeWidth="1" />
    </svg>
  );
}
