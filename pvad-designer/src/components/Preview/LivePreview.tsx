import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiCheck, FiChevronDown, FiDownload, FiExternalLink, FiFileText, FiLayers, FiPrinter } from 'react-icons/fi';
import ExportService, { DesignerAccessibilityPreflightError } from '../../services/ExportService';
import ExportModal from '../Editor/ExportModal';
import type { Template, SimpleElement, PageSettings, Page } from '@/types';
import { normalizePageSettings, useEditorStore } from '@/store';
import { blobToDataUrl, writePdfViewerHandoff } from '@/features/pdf-viewer/handoff';
import { installImportedFontFaces } from '@/utils/importedFonts';
import { effectivePageSettings } from '@/utils/pageLayout';
import { DocumentElementRenderer } from '@/components/Rendering/DocumentElementRenderer';
import { createDocumentContentResolver, effectiveDocumentElement, elementMatchesDocumentLanguage } from '@/components/Rendering/documentContent';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { designerPermissions, hasDesignerPermission } from '@/auth/designerPermissions';

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


const LivePreview: React.FC<LivePreviewProps> = ({ template, pages, sharedElements = [], pageSettings, onBack, onExport, hideBackButton, exportLabel, visualParityMode = false, autoFitWidth = false }) => {
  const { t } = useTranslation('preview');
  const navigate = useNavigate();
  const { user } = useDesignerAuth();
  const canExport = hasDesignerPermission(user, designerPermissions.pdfExport);
  const canPrint = hasDesignerPermission(user, designerPermissions.pdfPrint) && canExport;
  const [zoom, setZoom] = useState(1);
  const previewPagesRef = useRef<HTMLDivElement>(null);
  const documentPageSettings = React.useMemo(() => normalizePageSettings(pageSettings), [pageSettings]);
  const currentPreviewLanguage = useEditorStore(state => state.currentPreviewLanguage);
  const previewLanguage = documentPageSettings.targetLanguage || currentPreviewLanguage || documentPageSettings.systemLanguage || navigator.language;
  const resolveContent = React.useMemo(() => createDocumentContentResolver(
    documentPageSettings.localizedProperties ?? [], previewLanguage,
    documentPageSettings.systemLanguage || navigator.language, documentPageSettings.customProperties),
    [documentPageSettings.localizedProperties, documentPageSettings.customProperties, documentPageSettings.systemLanguage, previewLanguage]);
  const renderInstant = React.useMemo(() => new Date(), [pages, sharedElements, documentPageSettings, previewLanguage]);
  const exportPageSettings = React.useMemo(() => ({ ...documentPageSettings, targetLanguage: previewLanguage }),
    [documentPageSettings, previewLanguage]);
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
        await ExportService.printToPDF(template, pages, sharedElements, exportPageSettings, {
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
      ExportService.exportToJSON(template, pages, sharedElements, exportPageSettings);
      setExportDone('json');
      onExport();
      setTimeout(() => setExportDone(null), 2500);
      exportLockRef.current = false;
      return;
    }

    if (format === 'image') {
      try {
        setExportingFormat('image');
        await ExportService.exportViaBackend('png', template, pages, sharedElements, exportPageSettings);
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
      const blob = await ExportService.exportToPDF(template, pages, sharedElements, exportPageSettings);
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


  // Positioning belongs to the surface; printable styling belongs to the shared renderer.
  const wrapperStyle = (element: SimpleElement): React.CSSProperties => ({
    position: 'absolute', left: element.x * zoom, top: element.y * zoom,
    width: element.width * zoom, height: element.height * zoom,
    transform: element.style?.rotation ? `rotate(${element.style.rotation}deg)` : undefined,
    transformOrigin: 'center center', boxSizing: 'border-box',
    overflow: ['contentcontrol', 'footnote', 'endnote', 'text', 'field', 'textarea', 'rect', 'shape', 'circle', 'note', 'table'].includes(element.type) ? 'visible' : 'hidden',
  });

  const renderElement = (element: SimpleElement, pageIndex = 0) => (
    <DocumentElementRenderer element={element} pageIndex={pageIndex} pageCount={pages.length}
      pageHeight={effectivePageSettings(documentPageSettings, pages[pageIndex]).height}
      resolveContent={resolveContent} zoom={zoom} now={renderInstant} />
  );

  const renderPage = (page: Page, pageIndex: number) => {
    const layout = effectivePageSettings(documentPageSettings, page);
    const visiblePageElements = page.elements.filter(el => !el.hidden && elementMatchesDocumentLanguage(el, previewLanguage))
      .map(element => effectiveDocumentElement(element, previewLanguage));
    const visibleShared = sharedElements.filter(el => !el.hidden && elementMatchesDocumentLanguage(el, previewLanguage))
      .map(element => effectiveDocumentElement(element, previewLanguage));
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
        pageSettings={exportPageSettings}
        onClose={() => setExportModalOpen(false)}
      />
    )}
    </>
  );
};

export default LivePreview;
