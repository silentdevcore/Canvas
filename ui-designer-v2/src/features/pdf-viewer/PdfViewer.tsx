import React, { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { PDFDocument as PdfLibDocument } from 'pdf-lib';
import { Document, Page, pdfjs } from 'react-pdf';
import type { PDFDocumentProxy, TextItem, TextMarkedContent } from 'pdfjs-dist/types/src/display/api';
import {
  FiChevronLeft,
  FiChevronRight,
  FiColumns,
  FiDownload,
  FiFileText,
  FiLink,
  FiMaximize2,
  FiPrinter,
  FiSearch,
  FiSidebar,
  FiUpload,
  FiZoomIn,
  FiZoomOut,
} from 'react-icons/fi';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

type FitMode = 'page' | 'width' | 'custom';
type PdfSourceKind = 'file' | 'url';
type PrintMode = 'all' | 'current' | 'range';

export interface PdfSource {
  file: File | string;
  kind: PdfSourceKind;
  name: string;
  url?: string;
}

interface PdfViewerProps {
  initialSource?: PdfSource | null;
}

interface SearchResult {
  id: string;
  pageNumber: number;
  matchIndex: number;
  snippet: string;
}

interface ViewerEvent {
  id: number;
  label: string;
}

const clamp = (value: number, min: number, max: number): number => Math.min(max, Math.max(min, value));

const textItemToString = (item: TextItem | TextMarkedContent): string => ('str' in item ? item.str : '');

const buildSnippet = (text: string, index: number, queryLength: number): string => {
  const start = Math.max(0, index - 54);
  const end = Math.min(text.length, index + queryLength + 72);
  const prefix = start > 0 ? '...' : '';
  const suffix = end < text.length ? '...' : '';
  return `${prefix}${text.slice(start, end).replace(/\s+/g, ' ').trim()}${suffix}`;
};

const escapeHtml = (value: string): string => value
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#039;');

const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const sourceNameFromUrl = (url: string): string => {
  try {
    const parsed = new URL(url);
    const lastSegment = parsed.pathname.split('/').filter(Boolean).pop();
    return lastSegment || parsed.hostname || 'Remote PDF';
  } catch {
    return 'Remote PDF';
  }
};

const parsePageRange = (range: string, maxPage: number): number[] => {
  const pages = new Set<number>();
  const parts = range.split(',').map(part => part.trim()).filter(Boolean);

  for (const part of parts) {
    const [rawStart, rawEnd] = part.split('-').map(value => Number.parseInt(value.trim(), 10));
    if (Number.isNaN(rawStart)) {
      continue;
    }

    const start = clamp(rawStart, 1, maxPage);
    const end = Number.isNaN(rawEnd) ? start : clamp(rawEnd, 1, maxPage);
    const lower = Math.min(start, end);
    const upper = Math.max(start, end);

    for (let page = lower; page <= upper; page += 1) {
      pages.add(page);
    }
  }

  return Array.from(pages).sort((a, b) => a - b);
};

const PdfViewer: React.FC<PdfViewerProps> = ({ initialSource = null }) => {
  const [source, setSource] = useState<PdfSource | null>(initialSource);
  const [urlInput, setUrlInput] = useState(initialSource?.url ?? '');
  const [pdfDoc, setPdfDoc] = useState<PDFDocumentProxy | null>(null);
  const [numPages, setNumPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageInput, setPageInput] = useState('1');
  const [zoom, setZoom] = useState(1);
  const [fitMode, setFitMode] = useState<FitMode>('page');
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [searchPanelOpen, setSearchPanelOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [caseSensitive, setCaseSensitive] = useState(false);
  const [searchResults, setSearchResults] = useState<SearchResult[]>([]);
  const [selectedResultIndex, setSelectedResultIndex] = useState(0);
  const [isSearching, setIsSearching] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [viewerEvents, setViewerEvents] = useState<ViewerEvent[]>([]);
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [printDialogOpen, setPrintDialogOpen] = useState(false);
  const [printMode, setPrintMode] = useState<PrintMode>('all');
  const [printRange, setPrintRange] = useState('1');
  const [printError, setPrintError] = useState<string | null>(null);
  const stageRef = useRef<HTMLDivElement | null>(null);
  const eventIdRef = useRef(0);

  const currentResult = searchResults[selectedResultIndex] ?? null;

  const emitViewerEvent = useCallback((label: string, detail: Record<string, unknown> = {}) => {
    eventIdRef.current += 1;
    const event = { id: eventIdRef.current, label };
    setViewerEvents(previous => [event, ...previous].slice(0, 6));
    window.dispatchEvent(new CustomEvent('pdf-viewer:event', { detail: { label, ...detail } }));
  }, []);

  useEffect(() => {
    setSource(initialSource);
    setUrlInput(initialSource?.url ?? '');
    if (initialSource) {
      emitViewerEvent('open:initial', { name: initialSource.name });
    }
  }, [emitViewerEvent, initialSource]);

  useEffect(() => {
    if (source?.kind !== 'file' || !(source.file instanceof File)) {
      setObjectUrl(null);
      return undefined;
    }

    const nextObjectUrl = URL.createObjectURL(source.file);
    setObjectUrl(nextObjectUrl);
    return () => URL.revokeObjectURL(nextObjectUrl);
  }, [source]);

  useEffect(() => {
    setPageInput(String(currentPage));
    setPrintRange(String(currentPage));
    emitViewerEvent('page:changed', { currentPage });
  }, [currentPage, emitViewerEvent]);

  useEffect(() => {
    emitViewerEvent('zoom:changed', { zoom: Number(zoom.toFixed(2)), fitMode });
  }, [fitMode, zoom, emitViewerEvent]);

  const applyFitMode = useCallback(async (mode: FitMode) => {
    if (!pdfDoc || mode === 'custom') {
      return;
    }

    const container = stageRef.current;
    if (!container) {
      return;
    }

    const page = await pdfDoc.getPage(currentPage);
    const viewport = page.getViewport({ scale: 1 });
    const widthRatio = (container.clientWidth - 48) / viewport.width;
    const heightRatio = (container.clientHeight - 48) / viewport.height;
    const nextZoom = mode === 'width' ? widthRatio : Math.min(widthRatio, heightRatio);
    setZoom(clamp(nextZoom, 0.35, 2.5));
  }, [currentPage, pdfDoc]);

  useEffect(() => {
    void applyFitMode(fitMode);
  }, [applyFitMode, fitMode, numPages]);

  const openLocalFile = (file: File | null) => {
    if (!file) {
      return;
    }

    setSource({ file, kind: 'file', name: file.name || 'Local PDF' });
    setLoadError(null);
    setSearchResults([]);
    setSelectedResultIndex(0);
    emitViewerEvent('open:file', { name: file.name });
  };

  const openUrl = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedUrl = urlInput.trim();
    if (!trimmedUrl) {
      return;
    }

    setSource({ file: trimmedUrl, kind: 'url', name: sourceNameFromUrl(trimmedUrl), url: trimmedUrl });
    setLoadError(null);
    setSearchResults([]);
    setSelectedResultIndex(0);
    emitViewerEvent('open:url', { url: trimmedUrl });
  };

  const onDocumentLoaded = (document: PDFDocumentProxy) => {
    setPdfDoc(document);
    setNumPages(document.numPages);
    setCurrentPage(1);
    setLoadError(null);
    emitViewerEvent('document:loaded', { pages: document.numPages });
  };

  const onDocumentError = (error: Error) => {
    setPdfDoc(null);
    setNumPages(0);
    setLoadError(error.message || 'PDF could not be loaded.');
    emitViewerEvent('document:error', { message: error.message });
  };

  const goToPage = (pageNumber: number) => {
    if (!numPages) {
      return;
    }

    setCurrentPage(clamp(pageNumber, 1, numPages));
  };

  const commitPageInput = () => {
    const parsed = Number.parseInt(pageInput, 10);
    if (Number.isNaN(parsed)) {
      setPageInput(String(currentPage));
      return;
    }

    goToPage(parsed);
  };

  const changeZoom = (delta: number) => {
    setFitMode('custom');
    setZoom(previous => clamp(Number((previous + delta).toFixed(2)), 0.35, 2.5));
  };

  const runSearch = useCallback(async () => {
    const query = searchQuery.trim();
    if (!pdfDoc || !query) {
      setSearchResults([]);
      setSelectedResultIndex(0);
      return;
    }

    setIsSearching(true);
    const normalizedQuery = caseSensitive ? query : query.toLowerCase();
    const nextResults: SearchResult[] = [];

    try {
      for (let pageNumber = 1; pageNumber <= pdfDoc.numPages; pageNumber += 1) {
        const page = await pdfDoc.getPage(pageNumber);
        const content = await page.getTextContent();
        const pageText = content.items.map(textItemToString).join(' ');
        const haystack = caseSensitive ? pageText : pageText.toLowerCase();
        let matchIndex = haystack.indexOf(normalizedQuery);

        while (matchIndex >= 0) {
          nextResults.push({
            id: `${pageNumber}-${matchIndex}-${nextResults.length}`,
            pageNumber,
            matchIndex,
            snippet: buildSnippet(pageText, matchIndex, query.length),
          });
          matchIndex = haystack.indexOf(normalizedQuery, matchIndex + normalizedQuery.length);
        }
      }

      setSearchResults(nextResults);
      setSelectedResultIndex(0);
      if (nextResults[0]) {
        setCurrentPage(nextResults[0].pageNumber);
      }
      emitViewerEvent('search:completed', { query, count: nextResults.length });
    } finally {
      setIsSearching(false);
    }
  }, [caseSensitive, emitViewerEvent, pdfDoc, searchQuery]);

  const selectResult = (index: number) => {
    const nextIndex = clamp(index, 0, Math.max(0, searchResults.length - 1));
    const result = searchResults[nextIndex];
    if (!result) {
      return;
    }

    setSelectedResultIndex(nextIndex);
    setCurrentPage(result.pageNumber);
    emitViewerEvent('search:selected', { pageNumber: result.pageNumber, result: nextIndex + 1 });
  };

  const getSourceBytes = async (): Promise<ArrayBuffer | null> => {
    if (!source) {
      return null;
    }

    if (source.kind === 'file' && source.file instanceof File) {
      return source.file.arrayBuffer();
    }

    const href = source.url || (typeof source.file === 'string' ? source.file : '');
    if (!href) {
      return null;
    }

    const response = await fetch(href);
    if (!response.ok) {
      throw new Error(`PDF could not be fetched (${response.status}).`);
    }

    return response.arrayBuffer();
  };

  const buildSubsetPdfUrl = async (pages: number[]): Promise<string> => {
    const bytes = await getSourceBytes();
    if (!bytes) {
      throw new Error('No PDF source is available.');
    }

    const sourcePdf = await PdfLibDocument.load(bytes);
    const outputPdf = await PdfLibDocument.create();
    const pageIndexes = pages.map(page => page - 1);
    const copiedPages = await outputPdf.copyPages(sourcePdf, pageIndexes);
    copiedPages.forEach(page => outputPdf.addPage(page));
    const subsetBytes = await outputPdf.save();
    const subsetBuffer = new ArrayBuffer(subsetBytes.byteLength);
    new Uint8Array(subsetBuffer).set(subsetBytes);
    const blob = new Blob([subsetBuffer], { type: 'application/pdf' });
    return URL.createObjectURL(blob);
  };

  const downloadCurrentPdf = () => {
    if (!source) {
      return;
    }

    const href = source.kind === 'file' ? objectUrl : source.url;
    if (!href) {
      return;
    }

    const link = document.createElement('a');
    link.href = href;
    link.download = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    emitViewerEvent('download:started', { name: source.name });
  };

  const printHref = (href: string, label: string, revokeAfterPrint = false) => {
    const printWindow = window.open(href, '_blank', 'noopener,noreferrer');
    if (!printWindow) {
      if (revokeAfterPrint) {
        URL.revokeObjectURL(href);
      }
      emitViewerEvent('print:blocked', { name: source?.name, mode: label });
      return;
    }

    emitViewerEvent('print:opened', { name: source?.name, mode: label });
    window.setTimeout(() => {
      printWindow.print();
      if (revokeAfterPrint) {
        window.setTimeout(() => URL.revokeObjectURL(href), 2000);
      }
    }, 700);
  };

  const printCurrentPdf = async () => {
    if (!source) {
      return;
    }

    setPrintError(null);

    try {
      if (printMode === 'all') {
        const href = source.kind === 'file' ? objectUrl : source.url;
        if (!href) {
          throw new Error('No printable PDF URL is available.');
        }

        printHref(href, 'all');
        setPrintDialogOpen(false);
        return;
      }

      const pages = printMode === 'current'
        ? [currentPage]
        : parsePageRange(printRange, numPages);

      if (pages.length === 0) {
        throw new Error('Enter a valid page range, for example 1-3 or 2,4,6.');
      }

      const subsetUrl = await buildSubsetPdfUrl(pages);
      printHref(subsetUrl, printMode, true);
      setPrintDialogOpen(false);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Printing failed.';
      setPrintError(message);
      emitViewerEvent('print:failed', { message });
    }
  };

  const highlightedTextRenderer = useCallback((textItem: TextItem & { pageNumber: number }): string => {
    const query = searchQuery.trim();
    const escapedText = escapeHtml(textItem.str);
    if (!query) {
      return escapedText;
    }

    const flags = caseSensitive ? 'g' : 'gi';
    const matcher = new RegExp(escapeRegExp(query), flags);
    return escapedText.replace(matcher, match => `<mark class="pdfv-text-match">${match}</mark>`);
  }, [caseSensitive, searchQuery]);

  const resultSummary = useMemo(() => {
    if (!searchQuery.trim()) {
      return 'No search query';
    }

    if (isSearching) {
      return 'Searching...';
    }

    return `${searchResults.length} result${searchResults.length === 1 ? '' : 's'}`;
  }, [isSearching, searchQuery, searchResults.length]);

  return (
    <main className="pdfv-shell">
        <section className="pdfv-toolbar" aria-label="PDF viewer toolbar">
          <div className="pdfv-source-group">
            <label className="pdfv-button pdfv-button-primary">
              <FiUpload />
              <span>Open PDF</span>
              <input
                className="sr-only"
                type="file"
                accept="application/pdf,.pdf"
                onChange={event => openLocalFile(event.target.files?.[0] ?? null)}
              />
            </label>

            <form className="pdfv-url-form" onSubmit={openUrl}>
              <FiLink aria-hidden="true" />
              <input
                type="url"
                value={urlInput}
                placeholder="https://example.com/file.pdf"
                onChange={event => setUrlInput(event.target.value)}
                aria-label="PDF URL"
              />
              <button type="submit" className="pdfv-button">Open URL</button>
            </form>
          </div>

          <div className="pdfv-tool-group" aria-label="Viewer controls">
            <button className="pdfv-icon-button" type="button" onClick={() => setSidebarOpen(value => !value)} title="Thumbnails">
              <FiSidebar />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => setSearchPanelOpen(value => !value)} title="Search">
              <FiSearch />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => goToPage(currentPage - 1)} disabled={currentPage <= 1} title="Previous page">
              <FiChevronLeft />
            </button>
            <label className="pdfv-page-jump">
              <span className="sr-only">Page number</span>
              <input
                value={pageInput}
                inputMode="numeric"
                onChange={event => setPageInput(event.target.value)}
                onBlur={commitPageInput}
                onKeyDown={event => {
                  if (event.key === 'Enter') {
                    commitPageInput();
                  }
                }}
                disabled={!numPages}
              />
              <span>/ {numPages || '-'}</span>
            </label>
            <button className="pdfv-icon-button" type="button" onClick={() => goToPage(currentPage + 1)} disabled={!numPages || currentPage >= numPages} title="Next page">
              <FiChevronRight />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => changeZoom(-0.1)} disabled={!source} title="Zoom out">
              <FiZoomOut />
            </button>
            <span className="pdfv-zoom-value">{Math.round(zoom * 100)}%</span>
            <button className="pdfv-icon-button" type="button" onClick={() => changeZoom(0.1)} disabled={!source} title="Zoom in">
              <FiZoomIn />
            </button>
            <button className={fitMode === 'page' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFitMode('page')} disabled={!source}>
              <FiMaximize2 />
              <span>Fit page</span>
            </button>
            <button className={fitMode === 'width' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFitMode('width')} disabled={!source}>
              <FiColumns />
              <span>Fit width</span>
            </button>
            <button className="pdfv-icon-button" type="button" onClick={downloadCurrentPdf} disabled={!source} title="Download">
              <FiDownload />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => setPrintDialogOpen(true)} disabled={!source} title="Print">
              <FiPrinter />
            </button>
          </div>
        </section>

        {searchPanelOpen && (
          <section className="pdfv-search-panel" aria-label="PDF text search">
            <form
              className="pdfv-search-form"
              onSubmit={event => {
                event.preventDefault();
                void runSearch();
              }}
            >
              <FiSearch aria-hidden="true" />
              <input
                value={searchQuery}
                placeholder="Search document text"
                onChange={event => setSearchQuery(event.target.value)}
                disabled={!pdfDoc}
              />
              <label className="pdfv-checkbox">
                <input
                  type="checkbox"
                  checked={caseSensitive}
                  onChange={event => setCaseSensitive(event.target.checked)}
                  disabled={!pdfDoc}
                />
                <span>Case sensitive</span>
              </label>
              <button className="pdfv-button pdfv-button-primary" type="submit" disabled={!pdfDoc || isSearching}>
                Search
              </button>
              <span className="pdfv-result-summary">{resultSummary}</span>
            </form>

            {searchResults.length > 0 && (
              <div className="pdfv-result-controls">
                <button className="pdfv-button" type="button" onClick={() => selectResult(selectedResultIndex - 1)} disabled={selectedResultIndex <= 0}>
                  Previous result
                </button>
                <strong>{selectedResultIndex + 1} / {searchResults.length}</strong>
                <button className="pdfv-button" type="button" onClick={() => selectResult(selectedResultIndex + 1)} disabled={selectedResultIndex >= searchResults.length - 1}>
                  Next result
                </button>
              </div>
            )}
          </section>
        )}

        {printDialogOpen && (
          <section className="pdfv-print-panel" aria-label="Print options">
            <div className="pdfv-print-options">
              <strong>Print</strong>
              <label className={printMode === 'all' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'all'} onChange={() => setPrintMode('all')} />
                <span>All pages</span>
              </label>
              <label className={printMode === 'current' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'current'} onChange={() => setPrintMode('current')} />
                <span>Current page</span>
              </label>
              <label className={printMode === 'range' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'range'} onChange={() => setPrintMode('range')} />
                <span>Range</span>
              </label>
              <input
                className="pdfv-range-input"
                value={printRange}
                onChange={event => {
                  setPrintMode('range');
                  setPrintRange(event.target.value);
                }}
                placeholder="1-3,5"
                aria-label="Page range"
              />
            </div>
            <div className="pdfv-print-actions">
              {printError && <span className="pdfv-print-error">{printError}</span>}
              <button className="pdfv-button" type="button" onClick={() => setPrintDialogOpen(false)}>Cancel</button>
              <button className="pdfv-button pdfv-button-primary" type="button" onClick={() => void printCurrentPdf()}>Print</button>
            </div>
          </section>
        )}

        <section className="pdfv-workspace">
          {!source && (
            <div className="pdfv-empty">
              <FiFileText />
              <h1>PDF Viewer</h1>
              <p>Open a local PDF or use a backend-served PDF URL to inspect pages, search text, print, or download.</p>
            </div>
          )}

          {source && (
            <Document
              className="pdfv-document"
              file={source.file}
              onLoadSuccess={onDocumentLoaded}
              onLoadError={onDocumentError}
              loading={<div className="pdfv-state">Loading PDF...</div>}
              error={<div className="pdfv-state is-error">{loadError || 'PDF could not be loaded.'}</div>}
            >
              {sidebarOpen && (
                <aside className="pdfv-sidebar" aria-label="Page thumbnails">
                  <div className="pdfv-sidebar-header">
                    <strong>{source.name}</strong>
                    <span>{numPages || '-'} pages</span>
                  </div>
                  <div className="pdfv-thumbnails">
                    {Array.from({ length: numPages }, (_, index) => {
                      const pageNumber = index + 1;
                      return (
                        <button
                          key={pageNumber}
                          className={pageNumber === currentPage ? 'pdfv-thumbnail is-active' : 'pdfv-thumbnail'}
                          type="button"
                          onClick={() => goToPage(pageNumber)}
                        >
                          <Page
                            pageNumber={pageNumber}
                            width={104}
                            renderTextLayer={false}
                            renderAnnotationLayer={false}
                            loading={<span className="pdfv-thumb-loading">...</span>}
                          />
                          <span>{pageNumber}</span>
                        </button>
                      );
                    })}
                  </div>
                </aside>
              )}

              <div className="pdfv-stage" ref={stageRef}>
                {currentResult && currentResult.pageNumber === currentPage && (
                  <div className="pdfv-match-banner">
                    <strong>Search hit on page {currentResult.pageNumber}</strong>
                    <span>{currentResult.snippet}</span>
                  </div>
                )}

                <div className="pdfv-page-frame">
                  <Page
                    pageNumber={currentPage}
                    scale={zoom}
                    customTextRenderer={highlightedTextRenderer}
                    renderTextLayer
                    renderAnnotationLayer
                    loading={<div className="pdfv-state">Rendering page...</div>}
                  />
                </div>
              </div>
            </Document>
          )}

          {source && viewerEvents.length > 0 && (
            <aside className="pdfv-events" aria-label="Viewer events">
              <strong>Events</strong>
              {viewerEvents.map(event => (
                <span key={event.id}>{event.label}</span>
              ))}
            </aside>
          )}
        </section>
    </main>
  );
};

export default PdfViewer;
