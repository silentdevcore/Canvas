import React, { useEffect, useRef, useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import JsBarcode from 'jsbarcode';
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts';
import { FiCheck, FiChevronDown, FiDownload, FiEdit3, FiCheckSquare, FiFileText, FiLayers, FiPrinter } from 'react-icons/fi';
import ExportService from '../../services/ExportService';
import ExportModal from '../Editor/ExportModal';
import type { Template, SimpleElement, PageSettings, Page } from '@/types';
import { installImportedFontFaces } from '@/utils/importedFonts';

interface LivePreviewProps {
  template: Template;
  pages: Page[];
  sharedElements?: SimpleElement[];
  pageSettings?: PageSettings;
  onBack: () => void;
  onExport: () => void;
  hideBackButton?: boolean;
  exportLabel?: string;
}

type ExportFormat = 'pdf' | 'json' | 'image' | 'print';
const BORDER_SIDES = ['Top', 'Right', 'Bottom', 'Left'] as const;

const borderStyleForZoom = (s: Record<string, any>, zoom: number): React.CSSProperties => {
  const sideStyle: React.CSSProperties = {};
  let hasSideBorder = false;

  BORDER_SIDES.forEach((side) => {
    const width = s[`border${side}Width`];
    if (width == null) return;

    hasSideBorder = true;
    const key = `border${side}` as keyof React.CSSProperties;
    sideStyle[key] = `${(Number(width) || 0) * zoom}px ${s[`border${side}Style`] || s.borderStyle || 'solid'} ${s[`border${side}Color`] || s.borderColor || '#000'}` as never;
  });

  if (hasSideBorder) {
    return {
      ...sideStyle,
      borderRadius: s.borderRadius ? s.borderRadius * zoom : undefined,
    };
  }

  const bw = s.borderWidth ?? 0;
  return {
    border: bw > 0 ? `${bw * zoom}px ${s.borderStyle ?? 'solid'} ${s.borderColor ?? '#000'}` : undefined,
    borderRadius: s.borderRadius ? s.borderRadius * zoom : undefined,
  };
};

const LivePreview: React.FC<LivePreviewProps> = ({ template, pages, sharedElements = [], pageSettings, onBack, onExport, hideBackButton, exportLabel }) => {
  const [zoom, setZoom] = useState(1);
  const pageWidth  = pageSettings?.width  ?? 595;
  const pageHeight = pageSettings?.height ?? 842;
  const [exportingFormat, setExportingFormat] = useState<ExportFormat | null>(null);

  useEffect(() => {
    installImportedFontFaces(
      'canvas-imported-font-faces-preview',
      [...pages.flatMap(page => page.elements), ...sharedElements]
    );
  }, [pages, sharedElements]);
  const [exportDone, setExportDone] = useState<ExportFormat | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [exportModalOpen, setExportModalOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  const handleZoomIn  = () => setZoom(prev => Math.min(prev + 0.25, 2));
  const handleZoomOut = () => setZoom(prev => Math.max(prev - 0.25, 0.5));
  const handleResetZoom = () => setZoom(1);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const handleExport = async (format: ExportFormat) => {
    setMenuOpen(false);
    setExportError(null);

    if (format === 'print') {
      window.print();
      return;
    }

    if (format === 'json') {
      ExportService.exportToJSON(template, pages, sharedElements, pageSettings);
      setExportDone('json');
      onExport();
      setTimeout(() => setExportDone(null), 2500);
      return;
    }

    if (format === 'image') {
      try {
        setExportingFormat('image');
        await ExportService.exportToImage(
          '#preview-pages',
          template.name.toLowerCase().replace(/\s+/g, '-'),
          'png'
        );
        setExportingFormat(null);
        setExportDone('image');
        onExport();
        setTimeout(() => setExportDone(null), 2500);
      } catch (err) {
        setExportingFormat(null);
        setExportError(err instanceof Error ? err.message : 'Image export failed');
      }
      return;
    }

    // PDF via backend
    try {
      setExportingFormat('pdf');
      await ExportService.exportToPDF(template, pages, sharedElements, pageSettings);
      setExportingFormat(null);
      setExportDone('pdf');
      onExport();
      setTimeout(() => setExportDone(null), 2500);
    } catch (err) {
      setExportingFormat(null);
      setExportError(err instanceof Error ? err.message : 'Export failed');
    }
  };

  const createDefaultChartData = () => ({
    labels: ['Jan', 'Feb', 'Mar', 'Apr'],
    datasets: [{ label: 'Series 1', data: [12, 19, 14, 22] }]
  });

  const getDatePreview = (element: SimpleElement) => {
    if (element.dateMode === 'static') return element.content || element.fallbackText || '-';
    if (element.dateMode === 'binding') return element.binding ? `{{ ${element.binding} }}` : element.fallbackText || '-';
    return new Intl.DateTimeFormat(element.locale || 'de-DE', { dateStyle: 'medium' }).format(new Date());
  };

  const getPageNumberPreview = (element: SimpleElement, pageIndex: number = 0) => {
    const pageNum = (element.startNumber || 1) + pageIndex;
    const prefix = element.prefix || '';
    const suffix = element.suffix || '';
    const toRoman = (n: number): string => {
      const vals = [1000,900,500,400,100,90,50,40,10,9,5,4,1];
      const syms = ['M','CM','D','CD','C','XC','L','XL','X','IX','V','IV','I'];
      let res = '';
      vals.forEach((v, i) => { while (n >= v) { res += syms[i]; n -= v; } });
      return res;
    };
    switch (element.numberingFormat || 'pageOfTotal') {
      case 'current':    return `${prefix}${pageNum}${suffix}`;
      case 'total':      return `${prefix}${pages.length}${suffix}`;
      case 'roman':      return `${prefix}${toRoman(pageNum)}${suffix}`;
      case 'alphabetic': return `${prefix}${String.fromCharCode(64 + Math.min(pageNum, 26))}${suffix}`;
      default:           return `${prefix}Page ${pageNum} of ${pages.length}${suffix}`;
    }
  };

  // Shared border + padding style applied on the element wrapper
  const hexToRgba = (hex: string, opacity: number): string => {
    const h = hex.replace('#', '');
    const full = h.length === 3 ? h.split('').map(c => c + c).join('') : h;
    const r = parseInt(full.slice(0, 2), 16);
    const g = parseInt(full.slice(2, 4), 16);
    const b = parseInt(full.slice(4, 6), 16);
    return isNaN(r) ? 'transparent' : `rgba(${r},${g},${b},${opacity})`;
  };

  const wrapperStyle = (el: SimpleElement): React.CSSProperties => {
    const s = el.style ?? {};

    let bgColor: string | undefined;
    const rawBg = s.backgroundColor ?? s.fill;
    if (rawBg && rawBg !== 'transparent') {
      const opacity = s.backgroundOpacity ?? 1;
      bgColor = opacity < 1 ? hexToRgba(rawBg, opacity) : rawBg;
    }

    return {
      position: 'absolute',
      left:   el.x * zoom,
      top:    el.y * zoom,
      width:  el.width  * zoom,
      height: el.height * zoom,
      transform: s.rotation ? `rotate(${s.rotation}deg)` : undefined,
      transformOrigin: 'center center',
      backgroundColor: bgColor,
      ...borderStyleForZoom(s, zoom),
      paddingTop:    s.paddingTop    ? s.paddingTop    * zoom : undefined,
      paddingRight:  s.paddingRight  ? s.paddingRight  * zoom : undefined,
      paddingBottom: s.paddingBottom ? s.paddingBottom * zoom : undefined,
      paddingLeft:   s.paddingLeft   ? s.paddingLeft   * zoom : undefined,
      boxSizing: 'border-box',
      overflow: 'hidden',
    };
  };

  const renderElement = (element: SimpleElement, pageIndex: number = 0) => {
    if (element.type === 'text') {
      const s = element.style ?? {};
      return (
        <div style={{
          width: '100%', height: '100%',
          fontSize:       (s.fontSize      || 16) * zoom,
          fontFamily:      s.fontFamily    || 'Arial',
          color:           s.color         || '#111827',
          fontWeight:      s.fontWeight    || 'normal',
          fontStyle:       s.fontStyle     || 'normal',
          textDecoration:  s.textDecoration|| 'none',
          textAlign:      (s.textAlign     || 'left') as React.CSSProperties['textAlign'],
          lineHeight:      s.lineHeight    ?? 1.4,
          letterSpacing:   s.letterSpacing ? `${s.letterSpacing}px` : undefined,
          whiteSpace:      s.whiteSpace    as React.CSSProperties['whiteSpace'] | undefined,
          display: 'block',
          overflow: 'hidden',
        }}>
          {element.content}
        </div>
      );
    }

    if (element.type === 'qrcode') {
      return (
        <QRCodeSVG
          value={element.qrValue || 'https://example.com'}
          size={element.qrSize || 120}
          level="H"
          includeMargin={true}
        />
      );
    }

    if (element.type === 'barcode') {
      return (
        <canvas
          ref={(canvas) => {
            if (canvas) {
              JsBarcode(canvas, element.barcodeValue || '123456789012', {
                format: element.barcodeType || 'CODE128',
                lineColor: '#000',
                width: 2,
                height: element.height || 88,
                fontSize: 16
              });
            }
          }}
          style={{ width: '100%', height: '100%' }}
        />
      );
    }

    if (element.type === 'signature') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: 8 }}>
          <FiEdit3 style={{ marginBottom: 4, color: '#6b7280' }} />
          <span style={{ fontSize: 12, color: '#374151' }}>{element.signatureLabel}</span>
          <div style={{ width: '100%', borderBottom: '2px solid #111827', marginTop: 8 }} />
          <small style={{ marginTop: 4, fontSize: 10, color: '#9ca3af' }}>Signature Line</small>
        </div>
      );
    }

    if (element.type === 'richtext') {
      return (
        <div
          style={{ width: '100%', height: '100%', overflow: 'hidden' }}
          dangerouslySetInnerHTML={{ __html: element.htmlContent || '' }}
        />
      );
    }

    if (element.type === 'field') {
      return (
        <div style={{ width: '100%', height: '100%', padding: 8, border: '1px solid #93c5fd', background: '#eff6ff', borderRadius: 4 }}>
          <div style={{ fontSize: 11, fontWeight: 600, color: '#1d4ed8', marginBottom: 4 }}>
            {element.fieldLabel}{element.required ? ' *' : ''}
          </div>
          <div style={{ height: 28, background: '#fff', border: '1px solid #bfdbfe', borderRadius: 3 }} />
        </div>
      );
    }

    if (element.type === 'checkbox') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 8, padding: 8 }}>
          <FiCheckSquare style={{ color: '#3b82f6', flexShrink: 0 }} />
          <span style={{ fontSize: 14, color: '#374151' }}>
            {element.fieldLabel}{element.required ? ' *' : ''}
          </span>
        </div>
      );
    }

    if (element.type === 'image') {
      return (
        <img
          src={element.content || 'https://via.placeholder.com/220x140'}
          alt=""
          style={{
            width: '100%', height: '100%',
            objectFit: element.fitMode || 'contain',
            objectPosition: `${element.focalX || 50}% ${element.focalY || 50}%`
          }}
        />
      );
    }

    if (element.type === 'shape' || element.type === 'rect') {
      const s = element.style ?? {};
      return (
        <div style={{
          width: '100%', height: '100%',
          backgroundColor: s.backgroundColor ?? s.fill ?? 'transparent',
          borderRadius: s.borderRadius || 0
        }} />
      );
    }

    if (element.type === 'circle') {
      const s = element.style ?? {};
      return (
        <div style={{
          width: '100%', height: '100%',
          backgroundColor: s.backgroundColor ?? s.fill ?? 'transparent',
          borderRadius: '50%'
        }} />
      );
    }

    if (element.type === 'line') {
      return (
        <div style={{ width: '100%', height: '100%', backgroundColor: element.style?.backgroundColor || '#9ca3af' }} />
      );
    }

    if (element.type === 'table') {
      const s = element.style ?? {};
      const totalRows  = s.rows ?? 3;
      const columns    = s.columns ?? 3;
      const bw         = s.borderWidth ?? 1;
      const bc         = s.borderColor || '#000000';
      const cp         = s.cellPadding ?? 5;
      const hasHeader  = element.headerRow ?? false;
      const hasFooter  = element.footerRow ?? false;
      const headerBg   = element.headerBgColor || '#f1f5f9';
      const zebraOn    = element.zebraEnabled ?? false;
      const zebraColor = element.zebraColor || '#f9fafb';
      const colAligns  = element.columnAlignments ?? [];
      const cellData   = element.cellData ?? [];
      const bodyRows   = Math.max(1, totalRows - (hasHeader ? 1 : 0) - (hasFooter ? 1 : 0));
      const rdlColumnHeaders = Array.isArray(s.rdlTablixColumnHierarchy)
        ? s.rdlTablixColumnHierarchy
            .map((member: any) => member?.headerText || member?.groupName)
            .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
        : [];
      const rdlRowHeaders = Array.isArray(s.rdlTablixRowHierarchy)
        ? s.rdlTablixRowHierarchy
            .map((member: any) => member?.headerText || member?.groupName)
            .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
        : [];
      const rdlMatrixHeaders = [...rdlColumnHeaders, ...rdlRowHeaders];

      const cellStyles = element.cellStyles ?? [];
      const sideCss = (sd?: { color?: string; width?: number }) =>
        sd ? `${sd.width ?? 1}px solid ${sd.color ?? '#000000'}` : undefined;

      const tdSt = (r: number, c: number, kind: 'header' | 'body' | 'footer', dataRow: number = r): React.CSSProperties => {
        const st: React.CSSProperties = {
          border: `${bw}px solid ${bc}`,
          padding: cp,
          textAlign: (colAligns[c] || 'left') as React.CSSProperties['textAlign'],
          fontSize: 10 * zoom,
          fontWeight: kind === 'header' ? 700 : 'normal',
          color: kind === 'header' ? '#1e293b' : kind === 'footer' ? '#374151' : '#555',
          backgroundColor: kind === 'header' ? headerBg : kind === 'footer' ? '#f8fafc' : zebraOn && r % 2 === 1 ? zebraColor : 'transparent',
        };
        const cs = cellStyles.find((x) => x.row === dataRow && x.col === c);
        if (cs) {
          if (cs.backgroundColor) st.backgroundColor = cs.backgroundColor;
          if (cs.textAlign) st.textAlign = cs.textAlign;
          const hasBorder = cs.borderColor != null || cs.borderWidth != null
            || cs.borderTop || cs.borderRight || cs.borderBottom || cs.borderLeft;
          if (hasBorder) {
            const uniform = (cs.borderColor != null || cs.borderWidth != null)
              ? `${cs.borderWidth ?? 1}px solid ${cs.borderColor ?? '#000000'}`
              : 'none';
            st.border = undefined;
            st.borderTop = sideCss(cs.borderTop) ?? uniform;
            st.borderRight = sideCss(cs.borderRight) ?? uniform;
            st.borderBottom = sideCss(cs.borderBottom) ?? uniform;
            st.borderLeft = sideCss(cs.borderLeft) ?? uniform;
          }
        }
        return st;
      };

      return (
        <table style={{ width: '100%', height: '100%', borderCollapse: 'collapse', border: `${bw}px solid ${bc}` }}>
          {hasHeader && (
            <thead>
              {rdlMatrixHeaders.map((header, index) => (
                <tr key={`rdl-matrix-header-${index}`}>
                  <th
                    colSpan={columns}
                    style={{
                      ...tdSt(index, 0, 'header'),
                      textAlign: 'left',
                      backgroundColor: '#e0f2fe',
                      color: '#075985'
                    }}
                  >
                    {header}
                  </th>
                </tr>
              ))}
              <tr>{Array.from({ length: columns }).map((_, c) => (
                <th key={c} style={tdSt(0, c, 'header')}>{cellData[0]?.[c] || `Header ${c + 1}`}</th>
              ))}</tr>
            </thead>
          )}
          <tbody>
            {Array.from({ length: bodyRows }).map((_, r) => (
              <tr key={r}>{Array.from({ length: columns }).map((_, c) => (
                <td key={c} style={tdSt(r, c, 'body', r + (hasHeader ? 1 : 0))}>{cellData[r + (hasHeader ? 1 : 0)]?.[c] || 'Cell'}</td>
              ))}</tr>
            ))}
          </tbody>
          {hasFooter && (
            <tfoot>
              <tr>{Array.from({ length: columns }).map((_, c) => (
                <td key={c} style={tdSt(0, c, 'footer', totalRows - 1)}>{cellData[totalRows - 1]?.[c] || `Footer ${c + 1}`}</td>
              ))}</tr>
            </tfoot>
          )}
        </table>
      );
    }

    if (element.type === 'button') {
      return (
        <button
          style={{
            width: '100%', height: '100%',
            backgroundColor: element.style?.backgroundColor || '#3b82f6',
            color: element.style?.color || '#ffffff',
            fontSize: (element.style?.fontSize || 14) * zoom,
            borderRadius: element.style?.borderRadius || 4,
            border: 'none', cursor: element.buttonAction ? 'pointer' : 'default',
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
          }}
          onClick={element.buttonAction ? () => window.open(element.buttonAction, '_blank', 'noopener') : undefined}
        >
          {element.content || 'Button'}
        </button>
      );
    }

    if (element.type === 'dropdown') {
      return (
        <select style={{
          width: '100%', height: '100%',
          fontSize: (element.style?.fontSize || 14) * zoom,
          color: element.style?.color || '#000000',
          border: '1px solid #d1d5db', borderRadius: 4, padding: '0 8px'
        }} multiple={!!element.multiSelect}>
          {(element.options || []).map((opt, i) => <option key={i}>{opt}</option>)}
        </select>
      );
    }

    if (element.type === 'optionlist') {
      const ls = element.listStyle || (element.ordered ? 'decimal' : 'disc');
      const isCustom = ls === 'dash' || ls === 'asterisk';
      const prefix = ls === 'dash' ? '– ' : ls === 'asterisk' ? '* ' : '';
      const baseStyle = { fontSize: (element.style?.fontSize || 14) * zoom, color: element.style?.color || '#000' };
      if (isCustom) {
        return (
          <div style={{ ...baseStyle, padding: '0 4px', margin: 0 }}>
            {(element.options || []).map((item, i) => <div key={i} style={{ lineHeight: 1.6 }}>{prefix}{item}</div>)}
          </div>
        );
      }
      const isOrdered = ['decimal', 'lower-alpha', 'upper-alpha', 'lower-roman', 'upper-roman'].includes(ls);
      const Tag = isOrdered ? 'ol' : 'ul';
      return (
        <Tag style={{ ...baseStyle, listStyleType: ls, paddingLeft: 20, margin: 0 }}>
          {(element.options || []).map((item, i) => <li key={i}>{item}</li>)}
        </Tag>
      );
    }

    if (element.type === 'radio') {
      return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: 4 }}>
          {(element.options || []).map((opt, i) => (
            <label key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: (element.style?.fontSize || 14) * zoom, color: element.style?.color || '#000' }}>
              <input type="radio" name={element.id} defaultChecked={i === 0} readOnly />
              {opt}
            </label>
          ))}
        </div>
      );
    }

    if (element.type === 'chart') {
      const raw = element.chartData || createDefaultChartData();
      const chartData = (raw.labels || []).map((label: string, i: number) => ({
        name: label,
        pv: raw.datasets?.[0]?.data?.[i] || 0,
        uv: raw.datasets?.[1]?.data?.[i] || 0
      }));
      const pieColors = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2'];
      const chartType = element.chartType || 'bar';
      return (
        <ResponsiveContainer width="100%" height="100%">
          {chartType === 'bar' ? (
            <BarChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" /><YAxis />
              <Tooltip /><Legend />
              <Bar dataKey="pv" fill="#8884d8" /><Bar dataKey="uv" fill="#82ca9d" />
            </BarChart>
          ) : chartType === 'line' ? (
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="name" /><YAxis />
              <Tooltip /><Legend />
              <Line type="monotone" dataKey="pv" stroke="#8884d8" />
              <Line type="monotone" dataKey="uv" stroke="#82ca9d" />
            </LineChart>
          ) : (
            <PieChart>
              <Pie data={chartData} cx="50%" cy="50%" outerRadius={80} dataKey="pv">
                {chartData.map((_: unknown, i: number) => <Cell key={i} fill={pieColors[i % pieColors.length]} />)}
              </Pie>
              <Tooltip /><Legend />
            </PieChart>
          )}
        </ResponsiveContainer>
      );
    }

    if (element.type === 'watermark') {
      return (
        <div style={{
          width: '100%', height: '100%',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          opacity: element.style?.opacity ?? 0.18,
          transform: `rotate(${element.style?.rotation ?? -24}deg) scale(${element.style?.scale ?? 1})`,
          pointerEvents: 'none', overflow: 'hidden'
        }}>
          {element.watermarkMode === 'image' ? (
            <img src={element.content || 'https://via.placeholder.com/260x80'} alt="" style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
          ) : (
            <span style={{
              color: element.style?.color || '#64748b',
              fontSize: (element.style?.fontSize || 42) * zoom,
              fontWeight: 'bold',
              letterSpacing: 2,
              whiteSpace: 'nowrap',
              textTransform: 'uppercase'
            }}>
              {element.content || 'WATERMARK'}
            </span>
          )}
        </div>
      );
    }

    if (element.type === 'note') {
      return (
        <div style={{
          width: '100%', height: '100%', padding: 10,
          background: element.style?.backgroundColor || '#fef3c7',
          color: element.style?.color || '#78350f',
          borderRadius: 6,
          overflow: 'hidden'
        }}>
          <strong style={{ display: 'block', fontSize: 12 * zoom, marginBottom: 6 }}>{element.noteTitle || 'Notiz'}</strong>
          {!element.noteCollapsed && (
            <>
              <p style={{ margin: 0, fontSize: 11 * zoom, lineHeight: 1.35 }}>{element.noteBody || 'Kommentar eingeben'}</p>
              <small style={{ display: 'block', marginTop: 8, opacity: 0.72 }}>{element.noteAuthor || 'Editor'}</small>
            </>
          )}
        </div>
      );
    }

    if (element.type === 'arrow') {
      const color = element.style?.color || '#dc2626';
      const sw = element.style?.strokeWidth || 4;
      const dashArray = element.style?.dashStyle === 'dashed' ? '8 6' : element.style?.dashStyle === 'dotted' ? '2 6' : undefined;
      const path = element.arrowMode === 'curved'
        ? 'M 12 50 C 36 6, 64 94, 88 50'
        : element.arrowMode === 'elbow'
          ? 'M 12 78 L 50 78 L 50 22 L 88 22'
          : 'M 12 50 L 88 50';
      const dirDeg = ({ right: 0, left: 180, down: 90, up: -90 } as Record<string, number>)[element.arrowDirection || 'right'] ?? 0;
      const totalDeg = dirDeg + (element.arrowRotation || 0);
      const eid = element.id;
      const resolveMarker = (marker: string | undefined, isStart: boolean) => {
        const side = isStart ? 's' : 'e';
        if (!marker || marker === 'none') return undefined;
        if (marker === 'filled' || marker === 'arrow') return `url(#paf-${side}-${eid})`;
        if (marker === 'open')    return `url(#pao-${side}-${eid})`;
        if (marker === 'dot')     return `url(#pad-${side}-${eid})`;
        if (marker === 'diamond') return `url(#pam-${side}-${eid})`;
        if (marker === 'square')  return `url(#paq-${side}-${eid})`;
        if (marker === 'circle')  return `url(#pac-${side}-${eid})`;
        return undefined;
      };
      return (
        <svg viewBox="0 0 100 100" width="100%" height="100%" preserveAspectRatio="none"
          style={{ overflow: 'visible', ...(totalDeg !== 0 ? { transform: `rotate(${totalDeg}deg)`, transformOrigin: '50% 50%' } : {}) }}>
          <defs>
            {(['e', 's'] as const).map(side => {
              const orient = side === 's' ? 'auto-start-reverse' : 'auto';
              return (
                <React.Fragment key={side}>
                  <marker id={`paf-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient={orient}>
                    <path d="M 0 0 L 12 6 L 0 12 z" fill={color} />
                  </marker>
                  <marker id={`pao-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient={orient}>
                    <path d="M 0 0 L 12 6 L 0 12" fill="none" stroke={color} strokeWidth={Math.max(1, sw * 0.5)} />
                  </marker>
                  <marker id={`pad-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5">
                    <circle cx="5" cy="5" r="4" fill={color} />
                  </marker>
                  <marker id={`pam-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="6" refY="6" orient={orient}>
                    <path d="M 0 6 L 6 0 L 12 6 L 6 12 z" fill={color} />
                  </marker>
                  <marker id={`paq-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5" orient={orient}>
                    <rect x="0" y="0" width="10" height="10" fill={color} />
                  </marker>
                  <marker id={`pac-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5">
                    <circle cx="5" cy="5" r="4" fill="none" stroke={color} strokeWidth={Math.max(1, sw * 0.5)} />
                  </marker>
                </React.Fragment>
              );
            })}
          </defs>
          <path d={path} fill="none" stroke={color} strokeWidth={sw}
            strokeLinecap="round" strokeLinejoin="round" strokeDasharray={dashArray}
            markerStart={resolveMarker(element.startMarker, true)}
            markerEnd={resolveMarker(element.endMarker, false)}
          />
        </svg>
      );
    }

    if (element.type === 'draw') {
      return (
        <svg viewBox="0 0 216 108" width="100%" height="100%" preserveAspectRatio="none">
          <path
            d={element.pathData || 'M 10 76 C 44 18, 78 112, 116 54 S 184 20, 206 72'}
            fill="none"
            stroke={element.style?.color || '#1d4ed8'}
            strokeWidth={element.style?.strokeWidth || 4}
            strokeLinecap="round" strokeLinejoin="round"
            opacity={element.style?.opacity ?? (element.drawTool === 'highlighter' ? 0.45 : 1)}
          />
        </svg>
      );
    }

    if (element.type === 'date') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', color: element.style?.color || '#111827', fontSize: (element.style?.fontSize || 14) * zoom }}>
          {getDatePreview(element)}
        </div>
      );
    }

    if (element.type === 'highlight') {
      return (
        <div style={{
          width: '100%', height: '100%',
          background: element.style?.backgroundColor || '#fde047',
          opacity: element.style?.opacity ?? 0.45,
          borderRadius: element.style?.borderRadius ?? 4,
          mixBlendMode: element.style?.blendMode || 'multiply'
        }} />
      );
    }

    if (element.type === 'subsection' || element.type === 'area') {
      const color = element.style?.color || element.style?.borderColor || '#475569';
      return (
        <div style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 8 * zoom,
          color,
          fontSize: (element.style?.fontSize || 12) * zoom,
          background: element.style?.backgroundColor || '#f8fafc',
          border: `${(element.style?.borderWidth || 1) * zoom}px ${element.style?.borderStyle || 'dashed'} ${color}`,
          borderRadius: (element.style?.borderRadius ?? 4) * zoom,
          overflow: 'hidden',
          textAlign: 'center',
        }}>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.content || (element.type === 'subsection' ? 'Subsection' : 'Area')}
          </span>
        </div>
      );
    }

    if (element.type === 'checkmark') {
      const color = element.style?.color || '#16a34a';
      const state = element.checkState || 'checked';
      return (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, width: '100%', height: '100%', color }}>
          <svg width="26" height="26" viewBox="0 0 26 26" aria-hidden="true">
            <rect x="2" y="2" width="22" height="22" rx="4" fill="none" stroke={color} strokeWidth="2" />
            {state === 'checked' && <path d="M 7 13 L 11 17 L 20 8" fill="none" stroke={color} strokeWidth={element.style?.strokeWidth || 3} strokeLinecap="round" strokeLinejoin="round" />}
            {state === 'cross'   && <path d="M 8 8 L 18 18 M 18 8 L 8 18" fill="none" stroke={color} strokeWidth={element.style?.strokeWidth || 3} strokeLinecap="round" />}
            {state === 'dot'     && <circle cx="13" cy="13" r="5" fill={color} />}
          </svg>
          <span style={{ fontSize: (element.style?.fontSize || 14) * zoom, color: element.style?.labelColor || '#374151' }}>
            {element.fieldLabel || 'Auswahl'}
          </span>
        </div>
      );
    }

    if (element.type === 'pageboundary') {
      const color = element.style?.color || '#7c3aed';
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 8, color }}>
          <div style={{ flex: 1, borderTop: `2px dashed ${color}` }} />
          <strong style={{ fontSize: 10, textTransform: 'uppercase', letterSpacing: 1 }}>
            {element.pageBoundaryMode === 'end' ? 'Page end' : 'Page start'}
          </strong>
          <div style={{ flex: 1, borderTop: `2px dashed ${color}` }} />
        </div>
      );
    }

    if (element.type === 'pagenumber') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', color: element.style?.color || '#374151', fontSize: (element.style?.fontSize || 12) * zoom }}>
          {getPageNumberPreview(element, pageIndex)}
        </div>
      );
    }

    if (element.type === 'link') {
      return (
        <a
          href={element.href || '#'}
          target={element.linkTarget || '_blank'}
          rel="noopener noreferrer"
          style={{
            display: 'flex', alignItems: 'center',
            width: '100%', height: '100%',
            color: element.style?.color || '#2563eb',
            fontSize: (element.style?.fontSize || 14) * zoom,
            fontFamily: element.style?.fontFamily || 'sans-serif',
            fontWeight: element.style?.fontWeight || 'normal',
            textDecoration: 'underline',
            overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis',
          }}
        >
          {element.content || element.href || 'Link text'}
        </a>
      );
    }

    if (element.type === 'number') {
      const val = element.numberValue ?? 0;
      let formatted = '';
      try {
        const locale = element.numberLocale || 'de-DE';
        if (element.numberStyle === 'currency') {
          formatted = new Intl.NumberFormat(locale, { style: 'currency', currency: element.numberCurrency || 'EUR', minimumFractionDigits: element.numberDecimals ?? 2, maximumFractionDigits: element.numberDecimals ?? 2 }).format(val);
        } else if (element.numberStyle === 'percent') {
          formatted = new Intl.NumberFormat(locale, { style: 'percent', minimumFractionDigits: element.numberDecimals ?? 1, maximumFractionDigits: element.numberDecimals ?? 1 }).format(val / 100);
        } else if (element.numberStyle === 'scientific') {
          formatted = val.toExponential(element.numberDecimals ?? 2);
        } else if (element.numberStyle === 'ordinal') {
          const abs = Math.abs(Math.round(val));
          const s = ['th', 'st', 'nd', 'rd'];
          const v = abs % 100;
          formatted = abs + (s[(v - 20) % 10] || s[v] || s[0]);
        } else {
          formatted = new Intl.NumberFormat(locale, { minimumFractionDigits: element.numberDecimals ?? 0, maximumFractionDigits: element.numberDecimals ?? 2 }).format(val);
        }
      } catch { formatted = String(val); }
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', color: element.style?.color || '#111827', fontSize: (element.style?.fontSize || 18) * zoom, fontFamily: element.style?.fontFamily || 'sans-serif', fontWeight: element.style?.fontWeight || 'bold', overflow: 'hidden' }}>
          {(element.prefix || '') + formatted + (element.suffix || '')}
        </div>
      );
    }

    return (
      <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f3f4f6', border: '2px dashed #d1d5db', borderRadius: 4 }}>
        <FiLayers style={{ color: '#9ca3af', marginRight: 6 }} />
        <span style={{ fontSize: 12, color: '#6b7280', textTransform: 'capitalize' }}>{element.type}</span>
      </div>
    );
  };

  const renderPage = (page: Page, pageIndex: number) => {
    const visiblePageElements = page.elements.filter(el => !el.hidden);
    const visibleShared = sharedElements.filter(el => !el.hidden);
    return (
      <div key={page.id} style={{ marginBottom: pages.length > 1 ? 40 : 0 }}>
        {pages.length > 1 && (
          <p style={{ textAlign: 'center', marginBottom: 8, fontSize: 12, color: '#6b7280', fontWeight: 500 }}>
            Page {pageIndex + 1}
          </p>
        )}
        <div
          style={{
            width: `${pageWidth * zoom}px`,
            height: `${pageHeight * zoom}px`,
            backgroundColor: pageSettings?.backgroundColor ?? '#ffffff',
            position: 'relative',
            overflow: 'hidden',
            boxShadow: '0 4px 24px rgba(0,0,0,0.15)',
            border: '1px solid #d1d5db',
            margin: '0 auto',
            ...(pageSettings?.backgroundImage ? {
              backgroundImage: `url(${pageSettings.backgroundImage})`,
              backgroundRepeat: pageSettings.backgroundImageFit === 'tile' ? 'repeat' : 'no-repeat',
              backgroundSize: pageSettings.backgroundImageFit === 'fill' ? '100% 100%'
                : pageSettings.backgroundImageFit === 'tile' ? 'auto'
                : pageSettings.backgroundImageFit,
              backgroundPosition: 'center',
            } : {}),
          }}
        >
          {/* Shared header/footer elements (same on every page) */}
          {visibleShared.map((element) => (
            <div key={element.id} style={wrapperStyle(element)}>
              {renderElement(element, pageIndex)}
            </div>
          ))}
          {/* Page-specific elements */}
          {visiblePageElements.map((element) => (
            <div key={element.id} style={wrapperStyle(element)}>
              {renderElement(element, pageIndex)}
            </div>
          ))}

          {/* Global watermark overlay */}
          {pageSettings?.globalWatermark?.enabled && pageSettings.globalWatermark.content && (
            <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', pointerEvents: 'none', overflow: 'hidden' }}>
              {pageSettings.globalWatermark.mode === 'text' ? (
                <span style={{
                  fontSize: (pageSettings.globalWatermark.fontSize ?? 42) * zoom,
                  color: pageSettings.globalWatermark.color ?? '#64748b',
                  opacity: pageSettings.globalWatermark.opacity ?? 0.18,
                  transform: `rotate(${pageSettings.globalWatermark.rotation ?? -24}deg) scale(${pageSettings.globalWatermark.scale ?? 1})`,
                  userSelect: 'none', whiteSpace: 'nowrap', fontWeight: 700, letterSpacing: 2,
                }}>
                  {pageSettings.globalWatermark.content}
                </span>
              ) : (
                <img src={pageSettings.globalWatermark.content} alt="Watermark" style={{
                  maxWidth: '60%', maxHeight: '60%',
                  opacity: pageSettings.globalWatermark.opacity ?? 0.18,
                  transform: `rotate(${pageSettings.globalWatermark.rotation ?? -24}deg)`,
                  objectFit: 'contain',
                }} />
              )}
            </div>
          )}

          {visiblePageElements.length === 0 && visibleShared.length === 0 && (
            <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#9ca3af' }}>
              <p style={{ fontSize: 14 }}>No elements on this page</p>
            </div>
          )}
        </div>
      </div>
    );
  };

  const totalElements = pages.reduce((sum, p) => sum + p.elements.length, 0);

  return (
    <>
    <div style={{ minHeight: '100vh', background: '#f1f5f9' }}>
      {/* Header */}
      <header style={{ background: '#fff', borderBottom: '1px solid #e2e8f0', padding: '12px 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          {!hideBackButton && (
            <button onClick={onBack} style={{ color: '#475569', fontWeight: 500, background: 'none', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6 }}>
              ← Back to Editor
            </button>
          )}
          <div>
            <h1 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#0f172a' }}>Preview: {template.name}</h1>
            <p style={{ margin: 0, fontSize: 12, color: '#64748b' }}>{pages.length} page{pages.length !== 1 ? 's' : ''} · {totalElements} elements</p>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {/* Zoom */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 8, padding: '4px 8px' }}>
            <button onClick={handleZoomOut} disabled={zoom <= 0.5} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 18, lineHeight: 1 }}>−</button>
            <span style={{ minWidth: 48, textAlign: 'center', fontSize: 13, fontWeight: 500, color: '#374151' }}>{Math.round(zoom * 100)}%</span>
            <button onClick={handleZoomIn} disabled={zoom >= 2} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 18, lineHeight: 1 }}>+</button>
            <button onClick={handleResetZoom} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 14, marginLeft: 4 }}>⟲</button>
          </div>
          {/* Export dropdown */}
          <div ref={menuRef} style={{ position: 'relative' }}>
            <button
              onClick={() => setMenuOpen(o => !o)}
              disabled={exportingFormat !== null}
              style={{
                display: 'flex', alignItems: 'center', gap: 6,
                padding: '8px 14px', borderRadius: 8, border: 'none',
                background: exportDone ? '#16a34a' : exportingFormat ? '#94a3b8' : '#2563eb',
                color: '#fff', fontWeight: 500, fontSize: 14, cursor: exportingFormat ? 'not-allowed' : 'pointer',
              }}
            >
              {exportDone ? <FiCheck size={15} /> : exportingFormat ? null : <FiDownload size={15} />}
              {exportingFormat ? 'Exporting…' : exportDone ? 'Exported!' : (exportLabel ?? 'Export')}
              {!exportingFormat && !exportDone && <FiChevronDown size={14} />}
            </button>

            {menuOpen && (
              <div style={{
                position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 50,
                background: '#fff', border: '1px solid #e2e8f0', borderRadius: 10,
                boxShadow: '0 8px 24px rgba(0,0,0,0.12)', minWidth: 200, overflow: 'hidden',
              }}>
                {([
                  { format: 'pdf'   as ExportFormat, icon: <FiDownload size={14} />, label: 'Export PDF',   sub: 'Render via backend (localhost:5086)' },
                  { format: 'image' as ExportFormat, icon: <FiLayers   size={14} />, label: 'Export Image', sub: 'Save as PNG (all pages)' },
                  { format: 'json'  as ExportFormat, icon: <FiFileText size={14} />, label: 'Export JSON',  sub: 'Download template file' },
                  { format: 'print' as ExportFormat, icon: <FiPrinter  size={14} />, label: 'Print',        sub: 'Browser print dialog'   },
                ] as const).map(({ format, icon, label, sub }) => (
                  <button
                    key={format}
                    onClick={() => handleExport(format)}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 12, width: '100%',
                      padding: '11px 16px', background: 'none', border: 'none',
                      cursor: 'pointer', textAlign: 'left', borderBottom: format !== 'print' ? '1px solid #f1f5f9' : 'none' as string,
                    }}
                    onMouseEnter={e => (e.currentTarget.style.background = '#f8fafc')}
                    onMouseLeave={e => (e.currentTarget.style.background = 'none')}
                  >
                    <span style={{ color: '#1d6fff', flexShrink: 0 }}>{icon}</span>
                    <span>
                      <div style={{ fontSize: 13, fontWeight: 600, color: '#0f172a' }}>{label}</div>
                      <div style={{ fontSize: 11, color: '#64748b', marginTop: 1 }}>{sub}</div>
                    </span>
                  </button>
                ))}
                <button
                  onClick={() => { setMenuOpen(false); setExportModalOpen(true); }}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 12, width: '100%',
                    padding: '11px 16px', background: 'none', border: 'none',
                    borderTop: '1px solid #e0e7ff',
                    cursor: 'pointer', textAlign: 'left',
                  }}
                  onMouseEnter={e => (e.currentTarget.style.background = '#f0f4ff')}
                  onMouseLeave={e => (e.currentTarget.style.background = 'none')}
                >
                  <span style={{ color: '#6366f1', flexShrink: 0 }}><FiDownload size={14} /></span>
                  <span>
                    <div style={{ fontSize: 13, fontWeight: 600, color: '#6366f1' }}>More formats…</div>
                    <div style={{ fontSize: 11, color: '#64748b', marginTop: 1 }}>Word, Excel, HTML, XML, SVG, CSV, Markdown</div>
                  </span>
                </button>
              </div>
            )}
          </div>

          {exportError && (
            <span style={{ fontSize: 12, color: '#dc2626', maxWidth: 200 }}>{exportError}</span>
          )}
        </div>
      </header>

      {/* Preview area */}
      <div id="preview-pages" style={{ padding: 40, overflowAuto: 'auto' } as React.CSSProperties}>
        {pages.map((page, i) => renderPage(page, i))}
      </div>

      {/* Info footer */}
      <div style={{ maxWidth: 700, margin: '0 auto 48px', background: '#fff', borderRadius: 12, padding: 24, boxShadow: '0 1px 8px rgba(0,0,0,0.08)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 16 }}>
          {[
            { label: 'Template', value: template.name, bg: '#eff6ff', fg: '#1d4ed8' },
            { label: 'Pages', value: `${pages.length}`, bg: '#f5f3ff', fg: '#6d28d9' },
            { label: 'Page Size', value: `${pageWidth} × ${pageHeight} px`, bg: '#f0fdf4', fg: '#15803d' },
            { label: 'Quality', value: pageSettings?.exportDefaults?.quality ?? 'printer', bg: '#fff7ed', fg: '#c2410c' },
          ].map(({ label, value, bg, fg }) => (
            <div key={label} style={{ background: bg, borderRadius: 8, padding: '10px 14px' }}>
              <div style={{ fontSize: 11, fontWeight: 600, color: fg, marginBottom: 2 }}>{label}</div>
              <div style={{ fontSize: 13, color: fg }}>{value}</div>
            </div>
          ))}
        </div>
        <div style={{ background: '#fefce8', border: '1px solid #fde68a', borderRadius: 8, padding: '12px 16px', display: 'flex', gap: 10 }}>
          <span>⚠️</span>
          <div>
            <strong style={{ fontSize: 13, color: '#92400e' }}>Preview Mode</strong>
            <p style={{ margin: '4px 0 0', fontSize: 12, color: '#b45309' }}>
              QR codes, barcodes, and exact font rendering will be finalised in the PDF export.
            </p>
          </div>
        </div>
      </div>
    </div>

    {exportModalOpen && (
      <ExportModal
        template={template}
        pages={pages}
        sharedElements={sharedElements}
        pageSettings={pageSettings}
        onClose={() => setExportModalOpen(false)}
      />
    )}
    </>
  );
};

export default LivePreview;
