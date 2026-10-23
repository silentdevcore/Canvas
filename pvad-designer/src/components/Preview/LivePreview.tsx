import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiCheck, FiChevronDown, FiDownload, FiExternalLink, FiFileText, FiLayers, FiPrinter } from 'react-icons/fi';
import ExportService, { DesignerAccessibilityPreflightError } from '../../services/ExportService';
import ExportModal from '../Editor/ExportModal';
import type { Template, SimpleElement, PageSettings, Page } from '@/types';
import { normalizePageSettings } from '@/store';
import { blobToDataUrl, writePdfViewerHandoff } from '@/features/pdf-viewer/handoff';
import { installImportedFontFaces } from '@/utils/importedFonts';
import { effectivePageSettings } from '@/utils/pageLayout';
import PdfContractChartRenderer from '@/chart/PdfContractChartRenderer';
import FormFieldRenderer from '@/components/Rendering/FormFieldRenderer';
import LineRenderer from '@/components/Rendering/LineRenderer';
import BackendSymbolRenderer from '@/components/Rendering/BackendSymbolRenderer';
import ArrowRenderer from '@/components/Rendering/ArrowRenderer';
import HighlightRenderer from '@/components/Rendering/HighlightRenderer';
import {
  formatNumberForPdf,
  formatPageNumberForPdf,
  pdfFontFamily,
  PdfRichTextRenderer,
  PdfSingleLineText,
  PdfTextBlockRenderer,
} from '@/components/Rendering/PdfTextRenderer';
import {
  PdfButtonRenderer,
  PdfCheckboxRenderer,
  PdfCheckmarkRenderer,
  PdfDropdownRenderer,
  PdfSignatureRenderer,
} from '@/components/Rendering/PdfControlRenderer';
import { PdfCircleRenderer, PdfDrawingRenderer, PdfShapeRenderer } from '@/components/Rendering/PdfGraphicsRenderer';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { designerPermissions, hasDesignerPermission } from '@/auth/designerPermissions';
import {
  PdfCommentRenderer,
  PdfContentControlRenderer,
  PdfLayoutBoundaryRenderer,
  PdfNoteRenderer,
} from '@/components/Rendering/PdfStructureRenderer';

interface LivePreviewProps {
  template: Template;
  pages: Page[];
  sharedElements?: SimpleElement[];
  pageSettings?: PageSettings;
  onBack: () => void;
  onExport: () => void;
  hideBackButton?: boolean;
  exportLabel?: string;
  /** Renders only deterministic pages for the internal visual-parity harness. */
  visualParityMode?: boolean;
  /** Keeps the widest page inside the available preview width. */
  autoFitWidth?: boolean;
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

const LivePreview: React.FC<LivePreviewProps> = ({ template, pages, sharedElements = [], pageSettings, onBack, onExport, hideBackButton, exportLabel, visualParityMode = false, autoFitWidth = false }) => {
  const { t } = useTranslation('preview');
  const navigate = useNavigate();
  const { user } = useDesignerAuth();
  const canExport = hasDesignerPermission(user, designerPermissions.pdfExport);
  const canPrint = hasDesignerPermission(user, designerPermissions.pdfPrint) && canExport;
  const [zoom, setZoom] = useState(1);
  const previewPagesRef = useRef<HTMLDivElement>(null);
  const documentPageSettings = React.useMemo(() => normalizePageSettings(pageSettings), [pageSettings]);
  const pageLayouts = React.useMemo(
    () => pages.map(page => effectivePageSettings(documentPageSettings, page)),
    [documentPageSettings, pages],
  );
  const uniquePageSizes = new Set(pageLayouts.map(layout => `${layout.width}:${layout.height}`));
  const pageSizeLabel = uniquePageSizes.size > 1
    ? t('footer.mixedPageSizes', { count: uniquePageSizes.size })
    : t('footer.pageSizeValue', {
        width: pageLayouts[0]?.width ?? documentPageSettings.width,
        height: pageLayouts[0]?.height ?? documentPageSettings.height,
      });
  const hasTrustSettings = Boolean(
    pageSettings?.encryption?.enabled ||
    (pageSettings?.complianceProfile && pageSettings.complianceProfile !== 'None') ||
    (pageSettings?.accessibilityProfile && pageSettings.accessibilityProfile !== 'None'),
  );
  const [exportingFormat, setExportingFormat] = useState<ExportFormat | null>(null);

  useEffect(() => {
    if (!autoFitWidth || !previewPagesRef.current) return;

    const updateZoom = () => {
      const availableWidth = Math.max(0, previewPagesRef.current!.clientWidth - 80);
      const widestPage = Math.max(...pageLayouts.map(layout => layout.width), documentPageSettings.width);
      if (availableWidth > 0 && widestPage > 0)
        setZoom(Math.min(1, Math.max(0.25, availableWidth / widestPage)));
    };

    updateZoom();
    const observer = new ResizeObserver(updateZoom);
    observer.observe(previewPagesRef.current);
    return () => observer.disconnect();
  }, [autoFitWidth, documentPageSettings.width, pageLayouts]);

  useEffect(() => {
    installImportedFontFaces(
      'pvad-imported-font-faces-preview',
      [...pages.flatMap(page => page.elements), ...sharedElements]
    );
  }, [pages, sharedElements]);
  const [exportDone, setExportDone] = useState<ExportFormat | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [viewerPdfDataUrl, setViewerPdfDataUrl] = useState<string | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [exportModalOpen, setExportModalOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const exportLockRef = useRef(false);

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
    if ((format === 'print' && !canPrint) || (format !== 'print' && !canExport)) return;
    if (exportLockRef.current) return;
    exportLockRef.current = true;
    setMenuOpen(false);
    setExportError(null);

    if (format === 'print') {
      try {
        setExportingFormat('print');
        await ExportService.printToPDF(template, pages, sharedElements, pageSettings, {
          blocked: t('printBlocked'),
          preparing: t('printPreparing'),
          title: t('printTitle', { name: template.name }),
        });
        setExportDone('print');
        onExport();
        setTimeout(() => setExportDone(null), 2500);
      } catch (err) {
        setExportError(err instanceof Error ? err.message : t('exportFailed'));
      } finally {
        setExportingFormat(null);
        exportLockRef.current = false;
      }
      return;
    }

    if (format === 'json') {
      ExportService.exportToJSON(template, pages, sharedElements, pageSettings);
      setExportDone('json');
      onExport();
      setTimeout(() => setExportDone(null), 2500);
      exportLockRef.current = false;
      return;
    }

    if (format === 'image') {
      try {
        setExportingFormat('image');
        await ExportService.exportViaBackend('png', template, pages, sharedElements, pageSettings);
        setExportingFormat(null);
        setExportDone('image');
        onExport();
        setTimeout(() => setExportDone(null), 2500);
      } catch (err) {
        setExportingFormat(null);
        setExportError(err instanceof Error ? err.message : t('exportImageFailed'));
      } finally {
        exportLockRef.current = false;
      }
      return;
    }

    // PDF via backend
    try {
      setExportingFormat('pdf');
      const blob = await ExportService.exportToPDF(template, pages, sharedElements, pageSettings);
      setViewerPdfDataUrl(await blobToDataUrl(blob));
      setExportingFormat(null);
      setExportDone('pdf');
      onExport();
      setTimeout(() => setExportDone(null), 2500);
    } catch (err) {
      setExportingFormat(null);
      setExportError(err instanceof DesignerAccessibilityPreflightError
        ? t('editor:accessibility.exportBlocked')
        : err instanceof Error ? err.message : t('exportFailed'));
    } finally {
      exportLockRef.current = false;
    }
  };

  const handleOpenPdfViewer = () => {
    if (!viewerPdfDataUrl) return;
    writePdfViewerHandoff({
      dataUrl: viewerPdfDataUrl,
      name: `${template.name.replace(/\s+/g, '-').toLowerCase()}.pdf`,
    });
    navigate('/pdf/viewer?handoff=session');
  };

  const getDatePreview = (element: SimpleElement) => {
    if (element.dateMode === 'static') return element.content || element.fallbackText || '-';
    if (element.dateMode === 'binding') return element.binding ? `{{ ${element.binding} }}` : element.fallbackText || '-';
    return new Intl.DateTimeFormat(element.locale || 'de-DE', { dateStyle: 'medium' }).format(new Date());
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
    const rendersOwnBox = ['area', 'arrow', 'bookmark', 'checkbox', 'circle', 'comment', 'contentcontrol', 'draw', 'dropdown', 'field', 'highlight', 'line', 'note', 'pageboundary', 'rect', 'richtext', 'shape', 'subsection', 'table', 'text', 'textarea'].includes(el.type);

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
      backgroundColor: rendersOwnBox ? undefined : bgColor,
      ...(rendersOwnBox ? {} : borderStyleForZoom(s, zoom)),
      paddingTop:    !rendersOwnBox && s.paddingTop    ? s.paddingTop    * zoom : undefined,
      paddingRight:  !rendersOwnBox && s.paddingRight  ? s.paddingRight  * zoom : undefined,
      paddingBottom: !rendersOwnBox && s.paddingBottom ? s.paddingBottom * zoom : undefined,
      paddingLeft:   !rendersOwnBox && s.paddingLeft   ? s.paddingLeft   * zoom : undefined,
      boxSizing: 'border-box',
      overflow: 'hidden',
    };
  };

  const renderElement = (element: SimpleElement, pageIndex: number = 0) => {
    if (element.type === 'text') {
      return <PdfTextBlockRenderer element={element} text={element.content ?? ''} />;
    }

    if (element.type === 'qrcode') {
      return <BackendSymbolRenderer element={element} type="qrcode" />;
    }

    if (element.type === 'barcode') {
      return <BackendSymbolRenderer element={element} type="barcode" />;
    }

    if (element.type === 'signature') {
      return <PdfSignatureRenderer element={element} />;
    }

    if (element.type === 'richtext') {
      return <PdfRichTextRenderer element={element} />;
    }

    if (element.type === 'field') {
      return <FormFieldRenderer element={element} />;
    }

    if (element.type === 'textarea') {
      return <FormFieldRenderer element={element} multiline />;
    }

    if (element.type === 'checkbox') {
      return <PdfCheckboxRenderer element={element} />;
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
      return <PdfShapeRenderer element={element} />;
    }

    if (element.type === 'circle') {
      return <PdfCircleRenderer element={element} />;
    }

    if (element.type === 'line') {
      return <LineRenderer element={element} />;
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
      const colWidths  = element.columnWidths ?? [];
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
      const visualRowHeight = (element.height * zoom) / Math.max(1, totalRows + rdlMatrixHeaders.length);
      const cellFontSize = s.cellFontSize ?? 10;
      const cellFontFamily = s.cellFontFamily ?? 'Arial';
      const cellColor = s.cellColor as string | undefined;
      const cellFontWeight = s.cellFontWeight ?? 'normal';

      const cellStyles = element.cellStyles ?? [];
      const sideCss = (sd?: { color?: string; width?: number }) =>
        sd ? `${sd.width ?? 1}px solid ${sd.color ?? '#000000'}` : undefined;

      const tdSt = (r: number, c: number, kind: 'header' | 'body' | 'footer', dataRow: number = r): React.CSSProperties => {
        const st: React.CSSProperties = {
          border: `${bw}px solid ${bc}`,
          padding: cp,
          textAlign: (colAligns[c] || 'left') as React.CSSProperties['textAlign'],
          fontSize: cellFontSize * zoom,
          fontFamily: pdfFontFamily(cellFontFamily),
          fontWeight: kind === 'header' || kind === 'footer' ? 700 : cellFontWeight,
          color: cellColor ?? '#101828',
          backgroundColor: kind === 'header' ? headerBg : kind === 'footer' ? '#f8fafc' : zebraOn && r % 2 === 1 ? zebraColor : 'transparent',
        };
        const cs = cellStyles.find((x) => x.row === dataRow && x.col === c);
        if (cs) {
          if (cs.backgroundColor) st.backgroundColor = cs.backgroundColor;
          if (cs.textAlign) st.textAlign = cs.textAlign;
          if (cs.padding != null) st.padding = cs.padding;
          if (cs.fontFamily) st.fontFamily = cs.fontFamily;
          if (cs.fontSize != null) st.fontSize = cs.fontSize;
          if (cs.bold) st.fontWeight = 700;
          if (cs.italic) st.fontStyle = 'italic';
          if (cs.color) st.color = cs.color;
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
        <table style={{
          width: '100%', height: '100%', borderCollapse: 'collapse', border: `${bw}px solid ${bc}`,
          tableLayout: colWidths.some(Boolean) ? 'fixed' : 'auto',
        }}>
          {colWidths.some(Boolean) && (
            <colgroup>
              {Array.from({ length: columns }).map((_, index) => (
                <col key={index} style={{ width: colWidths[index] || undefined }} />
              ))}
            </colgroup>
          )}
          {hasHeader && (
            <thead>
              {rdlMatrixHeaders.map((header, index) => (
                <tr key={`rdl-matrix-header-${index}`} style={{ height: visualRowHeight }}>
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
              <tr style={{ height: visualRowHeight }}>{Array.from({ length: columns }).map((_, c) => (
                <th key={c} style={tdSt(0, c, 'header')}>{cellData[0]?.[c] ?? ''}</th>
              ))}</tr>
            </thead>
          )}
          <tbody>
            {Array.from({ length: bodyRows }).map((_, r) => (
              <tr key={r} style={{ height: visualRowHeight }}>{Array.from({ length: columns }).map((_, c) => (
                <td key={c} style={tdSt(r, c, 'body', r + (hasHeader ? 1 : 0))}>{cellData[r + (hasHeader ? 1 : 0)]?.[c] ?? ''}</td>
              ))}</tr>
            ))}
          </tbody>
          {hasFooter && (
            <tfoot>
              <tr style={{ height: visualRowHeight }}>{Array.from({ length: columns }).map((_, c) => (
                <td key={c} style={tdSt(0, c, 'footer', totalRows - 1)}>{cellData[totalRows - 1]?.[c] ?? ''}</td>
              ))}</tr>
            </tfoot>
          )}
        </table>
      );
    }

    if (element.type === 'button') {
      return <PdfButtonRenderer element={element} />;
    }

    if (element.type === 'dropdown') {
      return <PdfDropdownRenderer element={element} />;
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
        <Tag
          start={isOrdered ? element.startNumber ?? 1 : undefined}
          style={{ ...baseStyle, listStyleType: ls, paddingLeft: 20, margin: 0 }}
        >
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
      return (
        <PdfContractChartRenderer
          chart={element.chart}
          legacyType={element.chartType}
          legacyData={element.chartData}
          ariaLabel={element.name || 'Chart'}
          width={element.width}
          height={element.height}
        />
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
      return <PdfNoteRenderer element={element} />;
    }

    if (element.type === 'arrow') {
      return <ArrowRenderer element={element} />;
    }

    if (element.type === 'draw') {
      return <PdfDrawingRenderer element={element} />;
    }

    if (element.type === 'date') {
      return <PdfSingleLineText element={element} kind="date" text={getDatePreview(element)} />;
    }

    if (element.type === 'highlight') {
      return <HighlightRenderer element={element} />;
    }

    if (element.type === 'subsection' || element.type === 'area') {
      return <PdfLayoutBoundaryRenderer element={element} />;
    }

    if (element.type === 'checkmark') {
      return <PdfCheckmarkRenderer element={element} />;
    }

    if (element.type === 'pageboundary') {
      return null;
    }

    if (element.type === 'pagenumber') {
      return <PdfSingleLineText element={element} kind="pagenumber" text={formatPageNumberForPdf(element, pageIndex, pages.length)} />;
    }

    if (element.type === 'link') {
      return <PdfSingleLineText element={element} kind="link" text={element.content || element.href || ''} />;
    }

    if (element.type === 'footnote' || element.type === 'endnote') {
      const label = element.type === 'footnote' ? '†' : '‡';
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', fontSize: 12 * zoom, color: '#6366f1', borderLeft: `${3 * zoom}px solid #6366f1`, overflow: 'hidden' }}>
          <span style={{ fontWeight: 700 }}>{label}</span>
          <span style={{ opacity: 0.75, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.footnoteText || `${element.type === 'footnote' ? 'Footnote' : 'Endnote'}…`}
          </span>
        </div>
      );
    }

    if (element.type === 'bookmark') {
      return null;
    }

    if (element.type === 'comment') {
      return <PdfCommentRenderer element={element} />;
    }

    if (element.type === 'contentcontrol') {
      return <PdfContentControlRenderer element={element} />;
    }

    if (element.type === 'toc') {
      const fontSize = (element.style?.fontSize || 13) * zoom;
      const color = element.style?.color || '#1f2937';
      const entries = element.tocEntries ?? [];
      return (
        <div style={{ width: '100%', height: '100%', overflow: 'hidden', padding: `${8 * zoom}px ${10 * zoom}px`, border: `${zoom}px dashed #94a3b8`, borderRadius: 4 * zoom, background: '#f8fafc' }}>
          <div style={{ fontSize: 10 * zoom, fontWeight: 700, color: '#64748b', letterSpacing: '0.06em', marginBottom: 6 * zoom, textTransform: 'uppercase' }}>
            {element.tocTitle || 'Table of Contents'}
          </div>
          {entries.map((entry, index) => (
            <div key={index} style={{ display: 'flex', alignItems: 'baseline', gap: 4 * zoom, marginBottom: 3 * zoom, paddingLeft: (entry.level - 1) * 12 * zoom, fontSize: entry.level === 1 ? fontSize : fontSize - zoom, fontWeight: entry.level === 1 ? 600 : 400, color }}>
              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{entry.text}</span>
              {element.tocShowLeaderDots !== false && <span style={{ flex: 1, borderBottom: '1px dotted #cbd5e1' }} />}
              {element.tocShowPageNumbers !== false && <span>{entry.page}</span>}
            </div>
          ))}
        </div>
      );
    }

    if (element.type === 'number') {
      return <PdfSingleLineText element={element} kind="number" text={`${element.prefix ?? ''}${formatNumberForPdf(element)}${element.suffix ?? ''}`} />;
    }

    return (
      <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f3f4f6', border: '2px dashed #d1d5db', borderRadius: 4 }}>
        <FiLayers style={{ color: '#9ca3af', marginRight: 6 }} />
        <span style={{ fontSize: 12, color: '#6b7280', textTransform: 'capitalize' }}>{element.type}</span>
      </div>
    );
  };

  const renderPage = (page: Page, pageIndex: number) => {
    const layout = effectivePageSettings(documentPageSettings, page);
    const visiblePageElements = page.elements.filter(el => !el.hidden);
    const visibleShared = sharedElements.filter(el => !el.hidden);
    return (
      <div key={page.id} style={{ marginBottom: visualParityMode ? 0 : pages.length > 1 ? 40 : 0 }}>
        {!visualParityMode && pages.length > 1 && (
          <p style={{ textAlign: 'center', marginBottom: 8, fontSize: 12, color: '#6b7280', fontWeight: 500 }}>
            {t('pageLabel', { number: pageIndex + 1 })}
          </p>
        )}
        <div
          data-parity-page={pageIndex + 1}
          style={{
            width: `${layout.width * zoom}px`,
            height: `${layout.height * zoom}px`,
            backgroundColor: layout.backgroundColor ?? '#ffffff',
            position: 'relative',
            overflow: 'hidden',
            boxShadow: visualParityMode ? 'none' : '0 4px 24px rgba(0,0,0,0.15)',
            border: visualParityMode ? 'none' : '1px solid #d1d5db',
            margin: visualParityMode ? 0 : '0 auto',
            ...(layout.backgroundImage ? {
              backgroundImage: `url(${layout.backgroundImage})`,
              backgroundRepeat: layout.backgroundImageFit === 'tile' ? 'repeat' : 'no-repeat',
              backgroundSize: layout.backgroundImageFit === 'fill' ? '100% 100%'
                : layout.backgroundImageFit === 'tile' ? 'auto'
                : layout.backgroundImageFit,
              backgroundPosition: 'center',
            } : {}),
          }}
        >
          {/* Shared header/footer elements (same on every page) */}
          {visibleShared.map((element) => (
            <div key={element.id} data-element-id={element.id} style={wrapperStyle(element)}>
              {renderElement(element, pageIndex)}
            </div>
          ))}
          {/* Page-specific elements */}
          {visiblePageElements.map((element) => (
            <div key={element.id} data-element-id={element.id} style={wrapperStyle(element)}>
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
                <img src={pageSettings.globalWatermark.content} alt={t('watermarkAlt')} style={{
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
              <p style={{ fontSize: 14 }}>{t('noElementsOnPage')}</p>
            </div>
          )}
        </div>
      </div>
    );
  };

  const totalElements = pages.reduce((sum, p) => sum + p.elements.length, 0);

  if (visualParityMode) {
    return (
      <div className="visual-parity-surface" data-parity-surface="preview">
        {pages.map((page, index) => renderPage(page, index))}
      </div>
    );
  }

  return (
    <>
    <div style={{ minHeight: '100vh', background: '#f1f5f9' }}>
      {/* Header */}
      <header style={{ background: '#fff', borderBottom: '1px solid #e2e8f0', padding: '12px 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          {!hideBackButton && (
            <button onClick={onBack} style={{ color: '#475569', fontWeight: 500, background: 'none', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6 }}>
              {t('backToEditor')}
            </button>
          )}
          <div>
            <h1 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#0f172a' }}>{t('heading', { name: template.name })}</h1>
            <p style={{ margin: 0, fontSize: 12, color: '#64748b' }}>{t('pageCount', { count: pages.length })} · {t('elementCount', { count: totalElements })}</p>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {/* Zoom */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 8, padding: '4px 8px' }}>
            <button onClick={handleZoomOut} disabled={zoom <= 0.5} aria-label={t('zoomOut')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 18, lineHeight: 1 }}>−</button>
            <span style={{ minWidth: 48, textAlign: 'center', fontSize: 13, fontWeight: 500, color: '#374151' }}>{Math.round(zoom * 100)}%</span>
            <button onClick={handleZoomIn} disabled={zoom >= 2} aria-label={t('zoomIn')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 18, lineHeight: 1 }}>+</button>
            <button onClick={handleResetZoom} aria-label={t('resetZoom')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#475569', fontSize: 14, marginInlineStart: 4 }}>⟲</button>
          </div>
          {/* Export dropdown */}
          {(canExport || canPrint) && <div ref={menuRef} style={{ position: 'relative' }}>
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
              {exportingFormat ? t('exporting') : exportDone ? t('exported') : (exportLabel ?? t('export'))}
              {!exportingFormat && !exportDone && <FiChevronDown size={14} />}
            </button>

            {menuOpen && (
              <div style={{
                position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 50,
                background: '#fff', border: '1px solid #e2e8f0', borderRadius: 10,
                boxShadow: '0 8px 24px rgba(0,0,0,0.12)', minWidth: 200, overflow: 'hidden',
              }}>
                {([
                  { format: 'pdf'   as ExportFormat, icon: <FiDownload size={14} />, label: t('exportMenu.pdf.label'),   sub: t('exportMenu.pdf.sub') },
                  { format: 'image' as ExportFormat, icon: <FiLayers   size={14} />, label: t('exportMenu.image.label'), sub: t('exportMenu.image.sub') },
                  { format: 'json'  as ExportFormat, icon: <FiFileText size={14} />, label: t('exportMenu.json.label'),  sub: t('exportMenu.json.sub') },
                  { format: 'print' as ExportFormat, icon: <FiPrinter  size={14} />, label: t('exportMenu.print.label'), sub: t('exportMenu.print.sub') },
                ] as const)
                  .filter(({ format }) => format === 'print' ? canPrint : canExport)
                  .map(({ format, icon, label, sub }) => (
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
                {canExport && <button
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
                    <div style={{ fontSize: 13, fontWeight: 600, color: '#6366f1' }}>{t('exportMenu.more.label')}</div>
                    <div style={{ fontSize: 11, color: '#64748b', marginTop: 1 }}>{t('exportMenu.more.sub')}</div>
                  </span>
                </button>}
              </div>
            )}
          </div>}

          {viewerPdfDataUrl && (
            <button
              onClick={handleOpenPdfViewer}
              style={{
                display: 'flex', alignItems: 'center', gap: 6,
                padding: '8px 12px', borderRadius: 8, border: '1px solid #cbd5e1',
                background: '#fff', color: '#0f172a', fontWeight: 500, fontSize: 14, cursor: 'pointer',
              }}
            >
              <FiExternalLink size={15} />
              {t('openInPdfViewer')}
            </button>
          )}

          {exportError && (
            <span style={{ fontSize: 12, color: '#dc2626', maxWidth: 200 }}>{exportError}</span>
          )}
        </div>
      </header>

      {/* Preview area */}
      <div ref={previewPagesRef} id="preview-pages" style={{ padding: 40, overflow: 'auto' }}>
        {pages.map((page, i) => renderPage(page, i))}
      </div>

      {/* Info footer */}
      <div style={{ maxWidth: 700, margin: '0 auto 48px', background: '#fff', borderRadius: 12, padding: 24, boxShadow: '0 1px 8px rgba(0,0,0,0.08)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 16 }}>
          {[
            { label: t('footer.template'), value: template.name, bg: '#eff6ff', fg: '#1d4ed8' },
            { label: t('footer.pages'), value: `${pages.length}`, bg: '#f5f3ff', fg: '#6d28d9' },
            { label: t('footer.pageSize'), value: pageSizeLabel, bg: '#f0fdf4', fg: '#15803d' },
            { label: t('footer.quality'), value: pageSettings?.exportDefaults?.quality ?? t('footer.qualityDefault'), bg: '#fff7ed', fg: '#c2410c' },
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
            <strong style={{ fontSize: 13, color: '#92400e' }}>{t('footer.previewModeTitle')}</strong>
            <p style={{ margin: '4px 0 0', fontSize: 12, color: '#b45309' }}>
              {hasTrustSettings ? t('footer.trustPreviewModeBody') : t('footer.previewModeBody')}
            </p>
          </div>
        </div>
      </div>
    </div>

    {exportModalOpen && canExport && (
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
