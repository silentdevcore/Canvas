import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { QRCodeSVG } from 'qrcode.react';
import { FiEdit3, FiCheckSquare } from 'react-icons/fi';
import { getTemplateElementsLocalized } from '@/data/templateContent.i18n';
import type { SimpleElement } from '@/types';
import { sanitizeRichTextHtml } from '@/utils/sanitizeRichTextHtml';

interface Props {
  templateId: string;
}

const PAGE_W = 595;
const PAGE_H = 842;
const SCALE = 260 / PAGE_W; // ≈ 0.437

function renderMiniEl(el: SimpleElement): React.ReactNode {
  const s = el.style ?? {};

  if (el.type === 'text') {
    return (
      <div style={{
        width: '100%', height: '100%',
        fontSize:      Math.max(6, (s.fontSize || 14) * SCALE),
        fontFamily:    s.fontFamily || 'Arial, sans-serif',
        color:         s.color || '#111827',
        fontWeight:    s.fontWeight || 'normal',
        fontStyle:     s.fontStyle || 'normal',
        textAlign:    (s.textAlign || 'left') as React.CSSProperties['textAlign'],
        lineHeight:    s.lineHeight ?? 1.4,
        overflow: 'hidden',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
      }}>
        {el.content}
      </div>
    );
  }

  if (el.type === 'richtext') {
    return (
      <div
        style={{ width: '100%', height: '100%', overflow: 'hidden', fontSize: 8 }}
        dangerouslySetInnerHTML={{ __html: sanitizeRichTextHtml(el.htmlContent || '') }}
      />
    );
  }

  if (el.type === 'rect' || el.type === 'shape') {
    return (
      <div style={{
        width: '100%', height: '100%',
        backgroundColor: s.backgroundColor ?? s.fill ?? 'transparent',
        borderRadius: s.borderRadius || 0,
      }} />
    );
  }

  if (el.type === 'circle') {
    return (
      <div style={{
        width: '100%', height: '100%',
        backgroundColor: s.backgroundColor ?? s.fill ?? 'transparent',
        borderRadius: '50%',
      }} />
    );
  }

  if (el.type === 'line') {
    return <div style={{ width: '100%', height: '100%', backgroundColor: s.backgroundColor || '#9ca3af' }} />;
  }

  if (el.type === 'signature') {
    return (
      <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 2 }}>
        <FiEdit3 size={8} color="#6b7280" />
        <span style={{ fontSize: 6, color: '#374151' }}>{el.signatureLabel || 'Signature'}</span>
        <div style={{ width: '80%', borderBottom: '1px solid #111827' }} />
      </div>
    );
  }

  if (el.type === 'field') {
    return (
      <div style={{ width: '100%', height: '100%', padding: 3, border: '1px solid #93c5fd', background: '#eff6ff', borderRadius: 2 }}>
        <div style={{ fontSize: 5, fontWeight: 600, color: '#1d4ed8', marginBottom: 2 }}>
          {el.fieldLabel}{el.required ? ' *' : ''}
        </div>
        <div style={{ height: 8, background: '#fff', border: '1px solid #bfdbfe', borderRadius: 1 }} />
      </div>
    );
  }

  if (el.type === 'checkbox') {
    return (
      <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: 3 }}>
        <FiCheckSquare size={7} color="#3b82f6" />
        <span style={{ fontSize: 6, color: '#374151' }}>{el.fieldLabel}</span>
      </div>
    );
  }

  if (el.type === 'qrcode') {
    return (
      <QRCodeSVG
        value={el.qrValue || 'https://example.com'}
        size={Math.min(el.width, el.height) * SCALE}
        level="L"
        includeMargin={false}
      />
    );
  }

  if (el.type === 'image') {
    return (
      <img
        src={el.content || ''}
        alt=""
        style={{ width: '100%', height: '100%', objectFit: 'cover' }}
      />
    );
  }

  if (el.type === 'table') {
    const ts = el.style ?? {};
    const rows = ts.rows ?? 3;
    const cols = ts.columns ?? 3;
    const bw = ts.borderWidth ?? 1;
    const bc = ts.borderColor || '#000';
    const hasHeader = el.headerRow ?? false;
    const headerBg = el.headerBgColor || '#f1f5f9';
    const cellData = el.cellData ?? [];
    const bodyRows = Math.max(1, rows - (hasHeader ? 1 : 0));

    const tdStyle = (kind: 'header' | 'body'): React.CSSProperties => ({
      border: `${bw}px solid ${bc}`,
      padding: 2,
      fontSize: 5,
      fontWeight: kind === 'header' ? 700 : 'normal',
      color: kind === 'header' ? '#1e293b' : '#555',
      backgroundColor: kind === 'header' ? headerBg : 'transparent',
      whiteSpace: 'nowrap',
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      maxWidth: 0,
    });

    return (
      <table style={{ width: '100%', height: '100%', borderCollapse: 'collapse', border: `${bw}px solid ${bc}`, tableLayout: 'fixed' }}>
        {hasHeader && (
          <thead>
            <tr>{Array.from({ length: cols }).map((_, c) => (
              <th key={c} style={tdStyle('header')}>{cellData[0]?.[c] || `H${c + 1}`}</th>
            ))}</tr>
          </thead>
        )}
        <tbody>
          {Array.from({ length: bodyRows }).map((_, r) => (
            <tr key={r}>{Array.from({ length: cols }).map((_, c) => (
              <td key={c} style={tdStyle('body')}>{cellData[r + (hasHeader ? 1 : 0)]?.[c] || ''}</td>
            ))}</tr>
          ))}
        </tbody>
      </table>
    );
  }

  return null;
}

function wrapperStyle(el: SimpleElement): React.CSSProperties {
  const s = el.style ?? {};
  const bw = s.borderWidth ?? 0;

  let bg: string | undefined;
  const rawBg = s.backgroundColor ?? s.fill;
  if (rawBg && rawBg !== 'transparent') bg = rawBg;

  return {
    position: 'absolute',
    left:   el.x,
    top:    el.y,
    width:  el.width,
    height: el.height,
    backgroundColor: bg,
    border: bw > 0 ? `${bw}px ${s.borderStyle ?? 'solid'} ${s.borderColor ?? '#000'}` : undefined,
    borderRadius: s.borderRadius || undefined,
    overflow: 'hidden',
    boxSizing: 'border-box',
    transform: s.rotation ? `rotate(${s.rotation}deg)` : undefined,
    transformOrigin: 'center center',
  };
}

const TemplateMiniPreview: React.FC<Props> = ({ templateId }) => {
  const { i18n } = useTranslation();
  const elements = useMemo(() => {
    const els = getTemplateElementsLocalized(templateId, i18n.language);
    return els;
  }, [templateId, i18n.language]);

  const outerW = Math.round(PAGE_W * SCALE);
  const outerH = Math.round(PAGE_H * SCALE);

  return (
    <div style={{
      width: outerW,
      height: outerH,
      position: 'relative',
      overflow: 'hidden',
      borderRadius: 6,
      boxShadow: '0 4px 24px rgba(0,0,0,0.15)',
      flexShrink: 0,
    }}>
      <div style={{
        width: PAGE_W,
        height: PAGE_H,
        position: 'absolute',
        top: 0,
        left: 0,
        background: '#ffffff',
        transformOrigin: 'top left',
        transform: `scale(${SCALE})`,
        pointerEvents: 'none',
      }}>
        {elements.map(el => (
          <div key={el.id} style={wrapperStyle(el)}>
            {renderMiniEl(el)}
          </div>
        ))}
      </div>
    </div>
  );
};

export default TemplateMiniPreview;
