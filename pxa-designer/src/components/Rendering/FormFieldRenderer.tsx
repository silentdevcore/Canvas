import type { SimpleElement } from '@/types';

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
    <div
      className="pxa-form-field-renderer"
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        overflow: 'hidden',
        boxSizing: 'border-box',
        fontFamily: style.fontFamily ?? 'Arial',
      }}
    >
      {label && (
        <div
          style={{
            position: 'absolute',
            left: 0,
            right: 0,
            top: 2,
            height: FORM_LABEL_OFFSET - 2,
            overflow: 'hidden',
            color: labelColor,
            fontSize,
            lineHeight: 1.2,
            whiteSpace: 'nowrap',
          }}
        >
          {label}
          {element.required && <span style={{ color: '#ef4444' }}> *</span>}
        </div>
      )}
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: FORM_LABEL_OFFSET,
          height: boxHeight,
          overflow: 'hidden',
          boxSizing: 'border-box',
          border: `1px solid ${borderColor}`,
          background,
          color: '#000000',
          fontSize,
          fontStyle: 'normal',
          lineHeight: `${Math.max(1, boxHeight - 2)}px`,
          padding: '0 6px',
          whiteSpace: 'nowrap',
        }}
      />
    </div>
  );
}
