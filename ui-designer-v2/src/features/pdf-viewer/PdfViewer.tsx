import React, { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { PDFDocument as PdfLibDocument } from 'pdf-lib';
import { Document, Page } from 'react-pdf';
import type { PDFDocumentProxy, TextItem, TextMarkedContent } from 'pdfjs-dist/types/src/display/api';
import {
  FiChevronLeft,
  FiChevronRight,
  FiColumns,
  FiDownload,
  FiEdit3,
  FiEyeOff,
  FiFileText,
  FiImage,
  FiLink,
  FiLock,
  FiMaximize2,
  FiMessageSquare,
  FiMinus,
  FiPenTool,
  FiPrinter,
  FiCircle,
  FiSearch,
  FiSidebar,
  FiSquare,
  FiUnderline,
  FiTag,
  FiTrash2,
  FiType,
  FiUnlock,
  FiUpload,
  FiZoomIn,
  FiZoomOut,
} from 'react-icons/fi';
import {
  STAMP_LABELS,
  annotationTypeFromTool,
  createAnnotationSidecar,
  parseAnnotationSidecar,
  stampColor,
  type InkPoint,
  type LineEnding,
  type MarkupQuadPoint,
  type PdfAnnotation,
  type ReviewTool,
  type StampLabel,
} from './annotations';
import { applyRedactions, deleteSavedAnnotations, embedAnnotations, extractNativeAnnotations, flattenAnnotations, loadAnnotations, saveAnnotations } from './annotationApi';
import { pdfViewerLabels, resolvePdfViewerLocale, type PdfViewerLocale } from './i18n';
import { fillPdfFormFields, readPdfFormFields, sameFormValue, type PdfFormFieldInfo, type PdfFormFieldValue } from './pdfForms';
import { configurePdfWorker } from './pdfWorker';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

configurePdfWorker();

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

interface AnnotationInteraction {
  id: string;
  mode: 'move' | 'resize';
  startClientX: number;
  startClientY: number;
  startX: number;
  startY: number;
  startWidth: number;
  startHeight: number;
}

const clamp = (value: number, min: number, max: number): number => Math.min(max, Math.max(min, value));

const isEditableTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
};

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

const documentIdFromSource = (source: PdfSource | null): string => {
  if (!source) {
    return 'unsaved-document';
  }

  const identity = source.url || source.name || 'document';
  return identity
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 120) || 'document';
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

const defaultStrokeWidthForType = (type: PdfAnnotation['type']): number => (
  type === 'highlight' || type === 'redaction' ? 1 : type === 'underline' || type === 'strikeout' ? 2 : type === 'ink' ? 2 : 3
);

const defaultOpacityForType = (type: PdfAnnotation['type']): number => (
  type === 'highlight' ? 45 : type === 'redaction' ? 88 : type === 'note' ? 96 : 100
);

const supportsStrokeWidth = (type: PdfAnnotation['type']): boolean => (
  ['ink', 'line', 'rectangle', 'circle', 'highlight', 'underline', 'strikeout', 'redaction'].includes(type)
);

const supportsOpacity = (type: PdfAnnotation['type']): boolean => (
  ['ink', 'line', 'rectangle', 'circle', 'highlight', 'underline', 'strikeout', 'redaction', 'note', 'freeText', 'stamp', 'image'].includes(type)
);

const supportsFill = (type: PdfAnnotation['type']): boolean => type === 'rectangle' || type === 'circle';

const supportsLineEndings = (type: PdfAnnotation['type']): boolean => type === 'line';

const annotationStrokeWidth = (annotation: PdfAnnotation): number => annotation.strokeWidth ?? defaultStrokeWidthForType(annotation.type);

const annotationOpacity = (annotation: PdfAnnotation): number => annotation.opacity ?? defaultOpacityForType(annotation.type);

const isTextMarkupTool = (tool: ReviewTool): boolean => tool === 'highlight' || tool === 'underline' || tool === 'strikeout';

const isTextMarkupAnnotation = (annotation: PdfAnnotation): boolean => (
  annotation.type === 'highlight' || annotation.type === 'underline' || annotation.type === 'strikeout'
);

const annotationBoundsFromQuads = (quadPoints: MarkupQuadPoint[]): Pick<PdfAnnotation, 'xPct' | 'yPct' | 'widthPct' | 'heightPct'> | null => {
  if (quadPoints.length === 0) {
    return null;
  }

  const xs = quadPoints.flatMap(point => [point.x1Pct, point.x2Pct, point.x3Pct, point.x4Pct]);
  const ys = quadPoints.flatMap(point => [point.y1Pct, point.y2Pct, point.y3Pct, point.y4Pct]);
  const minX = clamp(Math.min(...xs), 0, 100);
  const maxX = clamp(Math.max(...xs), 0, 100);
  const minY = clamp(Math.min(...ys), 0, 100);
  const maxY = clamp(Math.max(...ys), 0, 100);

  return {
    xPct: minX,
    yPct: minY,
    widthPct: Math.max(0.5, maxX - minX),
    heightPct: Math.max(0.5, maxY - minY),
  };
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
  const [reviewPanelOpen, setReviewPanelOpen] = useState(false);
  const [reviewTool, setReviewTool] = useState<ReviewTool>('view');
  const [annotations, setAnnotations] = useState<PdfAnnotation[]>([]);
  const [selectedAnnotationId, setSelectedAnnotationId] = useState<string | null>(null);
  const [reviewAuthor, setReviewAuthor] = useState('Reviewer');
  const [selectedStamp, setSelectedStamp] = useState<StampLabel>('Draft');
  const [customStampText, setCustomStampText] = useState('');
  const [pendingImageDataUrl, setPendingImageDataUrl] = useState<string | null>(null);
  const [viewerLocale, setViewerLocale] = useState<PdfViewerLocale>(() => resolvePdfViewerLocale());
  const [annotationApiStatus, setAnnotationApiStatus] = useState<string | null>(null);
  const [formPanelOpen, setFormPanelOpen] = useState(false);
  const [formFields, setFormFields] = useState<PdfFormFieldInfo[]>([]);
  const [formStatus, setFormStatus] = useState<string | null>(null);
  const [flattenFormFields, setFlattenFormFields] = useState(false);
  const stageRef = useRef<HTMLDivElement | null>(null);
  const pageStackRef = useRef<HTMLDivElement | null>(null);
  const eventIdRef = useRef(0);
  const annotationInteractionRef = useRef<AnnotationInteraction | null>(null);
  const suppressNextPageClickRef = useRef(false);

  const currentResult = searchResults[selectedResultIndex] ?? null;
  const currentPageAnnotations = annotations.filter(annotation => annotation.pageNumber === currentPage);
  const currentPageInkAnnotations = currentPageAnnotations.filter(annotation => annotation.type === 'ink');
  const currentPageBoxAnnotations = currentPageAnnotations.filter(annotation => annotation.type !== 'ink');
  const selectedAnnotation = annotations.find(annotation => annotation.id === selectedAnnotationId) ?? null;
  const documentId = useMemo(() => documentIdFromSource(source), [source]);
  const labels = pdfViewerLabels[viewerLocale];
  const changedFormFields = formFields.filter(field => !sameFormValue(field.value, field.originalValue));
  const redactionAnnotations = annotations.filter(annotation => annotation.type === 'redaction');

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

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (isEditableTarget(event.target)) {
        return;
      }

      if (event.key === 'ArrowLeft') {
        event.preventDefault();
        goToPage(currentPage - 1);
        emitViewerEvent('keyboard:page-previous', { currentPage });
        return;
      }

      if (event.key === 'ArrowRight') {
        event.preventDefault();
        goToPage(currentPage + 1);
        emitViewerEvent('keyboard:page-next', { currentPage });
        return;
      }

      if (event.key === '+' || event.key === '=') {
        event.preventDefault();
        changeZoom(0.1);
        emitViewerEvent('keyboard:zoom-in');
        return;
      }

      if (event.key === '-' || event.key === '_') {
        event.preventDefault();
        changeZoom(-0.1);
        emitViewerEvent('keyboard:zoom-out');
        return;
      }

      if (event.key === '/') {
        event.preventDefault();
        setSearchPanelOpen(true);
        window.setTimeout(() => {
          document.querySelector<HTMLInputElement>('.pdfv-search-form input')?.focus();
        }, 0);
        emitViewerEvent('keyboard:search-focus');
        return;
      }

      if (event.key === 'Escape') {
        event.preventDefault();
        setSelectedAnnotationId(null);
        setPrintDialogOpen(false);
        setSearchPanelOpen(false);
        emitViewerEvent('keyboard:escape');
        return;
      }

      if (event.key === 'Delete' || event.key === 'Backspace') {
        if (!selectedAnnotationId) {
          return;
        }

        event.preventDefault();
        deleteAnnotation(selectedAnnotationId);
        emitViewerEvent('keyboard:annotation-delete', { id: selectedAnnotationId });
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [annotations, currentPage, emitViewerEvent, numPages, selectedAnnotationId]);

  const openLocalFile = (file: File | null) => {
    if (!file) {
      return;
    }

    setSource({ file, kind: 'file', name: file.name || 'Local PDF' });
    setLoadError(null);
    setSearchResults([]);
    setAnnotations([]);
    setFormFields([]);
    setFormStatus(null);
    setPendingImageDataUrl(null);
    setSelectedAnnotationId(null);
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
    setAnnotations([]);
    setFormFields([]);
    setFormStatus(null);
    setPendingImageDataUrl(null);
    setSelectedAnnotationId(null);
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

  const getSourceBytes = useCallback(async (): Promise<ArrayBuffer | null> => {
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
  }, [source]);

  useEffect(() => {
    let cancelled = false;

    const loadFormFields = async () => {
      if (!source) {
        setFormFields([]);
        setFormStatus(null);
        return;
      }

      setFormStatus(labels.formLoading);
      try {
        const bytes = await getSourceBytes();
        if (!bytes || cancelled) {
          return;
        }

        const fields = await readPdfFormFields(bytes);
        if (cancelled) {
          return;
        }

        setFormFields(fields);
        setFormStatus(fields.length > 0 ? null : labels.formNone);
        emitViewerEvent('forms:loaded', { count: fields.length });
      } catch (error) {
        if (cancelled) {
          return;
        }

        const message = error instanceof Error ? error.message : labels.formLoadFailed;
        setFormFields([]);
        setFormStatus(message);
        emitViewerEvent('forms:load-failed', { message });
      }
    };

    void loadFormFields();

    return () => {
      cancelled = true;
    };
  }, [emitViewerEvent, getSourceBytes, labels.formLoadFailed, labels.formLoading, labels.formNone, source]);

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

  const updateFormFieldValue = (name: string, value: PdfFormFieldValue) => {
    setFormFields(previous => previous.map(field => (
      field.name === name ? { ...field, value } : field
    )));
  };

  const resetFormFields = () => {
    setFormFields(previous => previous.map(field => ({
      ...field,
      value: Array.isArray(field.originalValue) ? [...field.originalValue] : field.originalValue,
    })));
    setFormStatus(null);
    emitViewerEvent('forms:reset');
  };

  const downloadFilledFormPdf = async () => {
    if (!source || formFields.length === 0) {
      return;
    }

    setFormStatus(labels.formSaving);
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error('No PDF source is available.');
      }

      const filledBytes = await fillPdfFormFields(bytes, formFields, flattenFormFields);
      const filledBuffer = new ArrayBuffer(filledBytes.byteLength);
      new Uint8Array(filledBuffer).set(filledBytes);
      const blob = new Blob([filledBuffer], { type: 'application/pdf' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
      link.href = url;
      link.download = sourceName.replace(/\.pdf$/i, flattenFormFields ? '-flattened-form.pdf' : '-filled.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setFormStatus(flattenFormFields ? labels.formFlattened : labels.formDownloaded);
      emitViewerEvent('forms:downloaded', { count: changedFormFields.length, flattened: flattenFormFields });
    } catch (error) {
      const message = error instanceof Error ? error.message : labels.formSaveFailed;
      setFormStatus(message);
      emitViewerEvent('forms:download-failed', { message });
    }
  };

  const eraseInkAnnotation = useCallback((id: string) => {
    setAnnotations(previous => {
      const target = previous.find(annotation => annotation.id === id);
      if (!target || target.type !== 'ink' || target.locked) {
        return previous;
      }

      emitViewerEvent('annotation:ink-erased', { id });
      return previous.filter(annotation => annotation.id !== id);
    });
    setSelectedAnnotationId(current => (current === id ? null : current));
  }, [emitViewerEvent]);

  const selectImageAnnotationFile = async (file: File | null) => {
    if (!file) {
      return;
    }

    if (!file.type.startsWith('image/')) {
      emitViewerEvent('annotation:image-rejected', { name: file.name });
      return;
    }

    const dataUrl = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(new Error('Image could not be read.'));
      reader.readAsDataURL(file);
    });

    setPendingImageDataUrl(dataUrl);
    setReviewTool('image');
    emitViewerEvent('annotation:image-selected', { name: file.name });
  };

  const createTextMarkupFromSelection = () => {
    if (!source || !isTextMarkupTool(reviewTool)) {
      return;
    }

    const selection = window.getSelection();
    const pageBounds = pageStackRef.current?.getBoundingClientRect();
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed || !pageBounds) {
      return;
    }

    const selectionText = selection.toString().replace(/\s+/g, ' ').trim();
    const quadPoints: MarkupQuadPoint[] = [];
    for (let index = 0; index < selection.rangeCount; index += 1) {
      const range = selection.getRangeAt(index);
      for (const rect of Array.from(range.getClientRects())) {
        const left = clamp(Math.max(rect.left, pageBounds.left), pageBounds.left, pageBounds.right);
        const right = clamp(Math.min(rect.right, pageBounds.right), pageBounds.left, pageBounds.right);
        const top = clamp(Math.max(rect.top, pageBounds.top), pageBounds.top, pageBounds.bottom);
        const bottom = clamp(Math.min(rect.bottom, pageBounds.bottom), pageBounds.top, pageBounds.bottom);
        if (right - left < 1 || bottom - top < 1) {
          continue;
        }

        quadPoints.push({
          x1Pct: ((left - pageBounds.left) / pageBounds.width) * 100,
          y1Pct: ((top - pageBounds.top) / pageBounds.height) * 100,
          x2Pct: ((right - pageBounds.left) / pageBounds.width) * 100,
          y2Pct: ((top - pageBounds.top) / pageBounds.height) * 100,
          x3Pct: ((left - pageBounds.left) / pageBounds.width) * 100,
          y3Pct: ((bottom - pageBounds.top) / pageBounds.height) * 100,
          x4Pct: ((right - pageBounds.left) / pageBounds.width) * 100,
          y4Pct: ((bottom - pageBounds.top) / pageBounds.height) * 100,
        });
      }
    }

    const bounds = annotationBoundsFromQuads(quadPoints);
    if (!bounds) {
      return;
    }

    const type = annotationTypeFromTool(reviewTool);
    const nextAnnotation: PdfAnnotation = {
      id: `annotation-${Date.now()}-${Math.round(Math.random() * 1000)}`,
      type,
      pageNumber: currentPage,
      ...bounds,
      text: selectionText,
      author: reviewAuthor.trim() || 'Reviewer',
      createdAt: new Date().toISOString(),
      color: type === 'highlight' ? '#fef08a' : type === 'underline' ? '#2563eb' : '#dc2626',
      locked: false,
      opacity: defaultOpacityForType(type),
      strokeWidth: defaultStrokeWidthForType(type),
      lineEndingStart: 'none',
      lineEndingEnd: 'none',
      quadPoints,
    };

    suppressNextPageClickRef.current = true;
    selection.removeAllRanges();
    setAnnotations(previous => [...previous, nextAnnotation]);
    setSelectedAnnotationId(nextAnnotation.id);
    emitViewerEvent('annotation:text-selection-created', {
      type: nextAnnotation.type,
      pageNumber: currentPage,
      quads: quadPoints.length,
    });
  };

  const addAnnotationAtPoint = (event: React.MouseEvent<HTMLDivElement>) => {
    if (suppressNextPageClickRef.current) {
      suppressNextPageClickRef.current = false;
      return;
    }

    if (reviewTool === 'view' || reviewTool === 'ink' || reviewTool === 'inkEraser' || !source) {
      return;
    }

    const bounds = pageStackRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    const xPct = clamp(((event.clientX - bounds.left) / bounds.width) * 100, 0, 96);
    const yPct = clamp(((event.clientY - bounds.top) / bounds.height) * 100, 0, 96);
    const type = annotationTypeFromTool(reviewTool);
    if (type === 'image' && !pendingImageDataUrl) {
      return;
    }
    const stampText = type === 'stamp' && customStampText.trim()
      ? customStampText.trim().slice(0, 48)
      : selectedStamp;
    const nextAnnotation: PdfAnnotation = {
      id: `annotation-${Date.now()}-${Math.round(Math.random() * 1000)}`,
      type,
      pageNumber: currentPage,
      xPct,
      yPct,
      widthPct: type === 'highlight' || type === 'underline' || type === 'strikeout' ? 26 : type === 'redaction' ? 28 : type === 'line' ? 26 : type === 'stamp' ? 24 : type === 'image' ? 24 : type === 'note' ? 18 : type === 'circle' ? 16 : type === 'rectangle' ? 22 : 28,
      heightPct: type === 'highlight' || type === 'underline' || type === 'strikeout' ? 4 : type === 'redaction' ? 6 : type === 'line' ? 4 : type === 'stamp' ? 9 : type === 'image' ? 16 : type === 'note' ? 12 : type === 'circle' ? 16 : type === 'rectangle' ? 12 : 10,
      text: type === 'stamp' ? stampText : type === 'redaction' ? 'Redaction mark' : type === 'note' ? 'New note' : type === 'freeText' ? 'Text annotation' : '',
      author: reviewAuthor.trim() || 'Reviewer',
      createdAt: new Date().toISOString(),
      color: type === 'highlight' ? '#fef08a' : type === 'underline' ? '#2563eb' : type === 'strikeout' ? '#dc2626' : type === 'redaction' ? '#111827' : type === 'stamp' ? stampColor(selectedStamp) : type === 'image' ? '#0e7490' : type === 'note' ? '#facc15' : type === 'freeText' ? '#38bdf8' : '#ef4444',
      locked: false,
      imageDataUrl: type === 'image' ? pendingImageDataUrl ?? undefined : undefined,
      opacity: defaultOpacityForType(type),
      strokeWidth: defaultStrokeWidthForType(type),
      fillColor: type === 'rectangle' || type === 'circle' ? '#ffffff' : null,
      fillEnabled: false,
      lineEndingStart: 'none',
      lineEndingEnd: type === 'line' ? 'arrow' : 'none',
    };

    setAnnotations(previous => [...previous, nextAnnotation]);
    setSelectedAnnotationId(nextAnnotation.id);
    emitViewerEvent('annotation:created', { type: nextAnnotation.type, pageNumber: currentPage });
  };

  const updateAnnotationText = (id: string, text: string) => {
    setAnnotations(previous => previous.map(annotation => (
      annotation.id === id && !annotation.locked ? { ...annotation, text } : annotation
    )));
  };

  const beginInkDrawing = (event: React.MouseEvent<HTMLDivElement>) => {
    if (reviewTool !== 'ink' || !source || event.button !== 0) {
      return;
    }

    const bounds = pageStackRef.current?.getBoundingClientRect();
    if (!bounds) {
      return;
    }

    event.preventDefault();
    const pointFromEvent = (pointerEvent: MouseEvent | React.MouseEvent): InkPoint => ({
      xPct: clamp(((pointerEvent.clientX - bounds.left) / bounds.width) * 100, 0, 100),
      yPct: clamp(((pointerEvent.clientY - bounds.top) / bounds.height) * 100, 0, 100),
    });
    const id = `annotation-${Date.now()}-${Math.round(Math.random() * 1000)}`;
    const firstPoint = pointFromEvent(event);
    const nextAnnotation: PdfAnnotation = {
      id,
      type: 'ink',
      pageNumber: currentPage,
      xPct: 0,
      yPct: 0,
      widthPct: 100,
      heightPct: 100,
      text: '',
      author: reviewAuthor.trim() || 'Reviewer',
      createdAt: new Date().toISOString(),
      color: '#ef4444',
      locked: false,
      opacity: defaultOpacityForType('ink'),
      strokeWidth: defaultStrokeWidthForType('ink'),
      lineEndingStart: 'none',
      lineEndingEnd: 'none',
      points: [firstPoint],
    };

    setAnnotations(previous => [...previous, nextAnnotation]);
    setSelectedAnnotationId(id);

    const handleMove = (moveEvent: MouseEvent) => {
      const nextPoint = pointFromEvent(moveEvent);
      setAnnotations(previous => previous.map(annotation => {
        if (annotation.id !== id || annotation.locked) {
          return annotation;
        }

        const points = annotation.points ?? [];
        const lastPoint = points[points.length - 1];
        if (lastPoint && Math.abs(lastPoint.xPct - nextPoint.xPct) < 0.2 && Math.abs(lastPoint.yPct - nextPoint.yPct) < 0.2) {
          return annotation;
        }

        return { ...annotation, points: [...points, nextPoint] };
      }));
    };

    const handleUp = () => {
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseup', handleUp);
      emitViewerEvent('annotation:ink-created', { id, pageNumber: currentPage });
    };

    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseup', handleUp);
  };

  const updateAnnotation = (id: string, updates: Partial<PdfAnnotation>) => {
    setAnnotations(previous => previous.map(annotation => (
      annotation.id === id && (!annotation.locked || Object.keys(updates).every(key => key === 'locked'))
        ? { ...annotation, ...updates }
        : annotation
    )));
  };

  const beginAnnotationInteraction = (
    event: React.MouseEvent,
    annotation: PdfAnnotation,
    mode: AnnotationInteraction['mode'],
  ) => {
    if (annotation.locked) {
      return;
    }

    event.stopPropagation();
    event.preventDefault();
    setSelectedAnnotationId(annotation.id);
    annotationInteractionRef.current = {
      id: annotation.id,
      mode,
      startClientX: event.clientX,
      startClientY: event.clientY,
      startX: annotation.xPct,
      startY: annotation.yPct,
      startWidth: annotation.widthPct,
      startHeight: annotation.heightPct,
    };

    const handleMove = (moveEvent: MouseEvent) => {
      const interaction = annotationInteractionRef.current;
      const bounds = pageStackRef.current?.getBoundingClientRect();
      if (!interaction || !bounds) {
        return;
      }

      const deltaXPct = ((moveEvent.clientX - interaction.startClientX) / bounds.width) * 100;
      const deltaYPct = ((moveEvent.clientY - interaction.startClientY) / bounds.height) * 100;
      if (interaction.mode === 'move') {
        updateAnnotation(interaction.id, {
          xPct: clamp(interaction.startX + deltaXPct, 0, 100 - interaction.startWidth),
          yPct: clamp(interaction.startY + deltaYPct, 0, 100 - interaction.startHeight),
        });
        return;
      }

      updateAnnotation(interaction.id, {
        widthPct: clamp(interaction.startWidth + deltaXPct, 8, 100 - interaction.startX),
        heightPct: clamp(interaction.startHeight + deltaYPct, 6, 100 - interaction.startY),
      });
    };

    const handleUp = () => {
      const interaction = annotationInteractionRef.current;
      annotationInteractionRef.current = null;
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseup', handleUp);
      if (interaction) {
        emitViewerEvent(`annotation:${interaction.mode}`, { id: interaction.id });
      }
    };

    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseup', handleUp);
  };

  const deleteAnnotation = (id: string) => {
    const target = annotations.find(annotation => annotation.id === id);
    if (target?.locked) {
      emitViewerEvent('annotation:delete-blocked', { id });
      return;
    }

    setAnnotations(previous => previous.filter(annotation => annotation.id !== id));
    setSelectedAnnotationId(current => (current === id ? null : current));
    emitViewerEvent('annotation:deleted', { id });
  };

  const downloadAnnotationSidecar = () => {
    const payload = createAnnotationSidecar(source?.name ?? null, annotations);
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const sourceName = source?.name?.replace(/\.pdf$/i, '') || 'document';
    link.href = url;
    link.download = `${sourceName}-annotations.json`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    emitViewerEvent('annotations:exported', { count: annotations.length });
  };

  const downloadFlattenedPdf = async () => {
    if (!source || annotations.length === 0) {
      return;
    }

    setAnnotationApiStatus('Flattening PDF...');
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error('No PDF source is available.');
      }

      const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
      const pdfFile = source.file instanceof File
        ? source.file
        : new File([bytes], sourceName, { type: 'application/pdf' });
      const blob = await flattenAnnotations(pdfFile, createAnnotationSidecar(source?.name ?? null, annotations));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = sourceName.replace(/\.pdf$/i, '-reviewed.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setAnnotationApiStatus(`Downloaded flattened PDF with ${annotations.length} annotation${annotations.length === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:flattened', { count: annotations.length });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Flatten failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:flatten-failed', { message });
    }
  };

  const downloadRedactedPdf = async () => {
    if (!source || redactionAnnotations.length === 0) {
      return;
    }

    setAnnotationApiStatus('Applying redactions...');
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error('No PDF source is available.');
      }

      const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
      const pdfFile = source.file instanceof File
        ? source.file
        : new File([bytes], sourceName, { type: 'application/pdf' });
      const blob = await applyRedactions(pdfFile, createAnnotationSidecar(source?.name ?? null, redactionAnnotations));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = sourceName.replace(/\.pdf$/i, '-redacted.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setAnnotationApiStatus(`Downloaded redacted PDF with ${redactionAnnotations.length} redaction mark${redactionAnnotations.length === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:redacted', { count: redactionAnnotations.length });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Redaction failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:redaction-failed', { message });
    }
  };

  const downloadEmbeddedAnnotationsPdf = async () => {
    if (!source || annotations.length === 0) {
      return;
    }

    setAnnotationApiStatus('Embedding annotations...');
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error('No PDF source is available.');
      }

      const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
      const pdfFile = source.file instanceof File
        ? source.file
        : new File([bytes], sourceName, { type: 'application/pdf' });
      const blob = await embedAnnotations(pdfFile, createAnnotationSidecar(source?.name ?? null, annotations));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = sourceName.replace(/\.pdf$/i, '-annotated.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setAnnotationApiStatus(`Downloaded PDF with ${annotations.length} embedded annotation${annotations.length === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:embedded', { count: annotations.length });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Embedding annotations failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:embed-failed', { message });
    }
  };

  const saveAnnotationSidecar = async () => {
    setAnnotationApiStatus('Saving...');
    try {
      const response = await saveAnnotations(documentId, createAnnotationSidecar(source?.name ?? null, annotations));
      setAnnotationApiStatus(`Saved ${response.annotationCount} annotation${response.annotationCount === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:saved', { documentId, count: response.annotationCount });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Save failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:save-failed', { documentId, message });
    }
  };

  const loadAnnotationSidecar = async () => {
    setAnnotationApiStatus('Loading...');
    try {
      const response = await loadAnnotations(documentId);
      if (!response) {
        setAnnotationApiStatus('No saved annotations found.');
        emitViewerEvent('annotations:load-empty', { documentId });
        return;
      }

      setAnnotations(response.annotations);
      setSelectedAnnotationId(null);
      setAnnotationApiStatus(`Loaded ${response.annotationCount} annotation${response.annotationCount === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:loaded', { documentId, count: response.annotationCount });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Load failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:load-failed', { documentId, message });
    }
  };

  const deleteSavedAnnotationSidecar = async () => {
    setAnnotationApiStatus('Deleting...');
    try {
      await deleteSavedAnnotations(documentId);
      setAnnotationApiStatus('Saved annotations deleted.');
      emitViewerEvent('annotations:saved-deleted', { documentId });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Delete failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:delete-saved-failed', { documentId, message });
    }
  };

  const importAnnotationSidecar = async (file: File | null) => {
    if (!file) {
      return;
    }

    try {
      const raw = await file.text();
      const imported = parseAnnotationSidecar(raw);

      setAnnotations(imported);
      setSelectedAnnotationId(null);
      emitViewerEvent('annotations:imported', { count: imported.length });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Annotation sidecar import failed.';
      emitViewerEvent('annotations:import-failed', { message });
    }
  };

  const importNativeAnnotationsFromPdf = async () => {
    if (!source) {
      return;
    }

    setAnnotationApiStatus('Importing PDF annotations...');
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error('No PDF source is available.');
      }

      const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
      const pdfFile = source.file instanceof File
        ? source.file
        : new File([bytes], sourceName, { type: 'application/pdf' });
      const sidecar = await extractNativeAnnotations(pdfFile);
      const imported = sidecar.annotations;
      setAnnotations(previous => [
        ...previous.filter(existing => !imported.some(annotation => annotation.id === existing.id)),
        ...imported,
      ]);
      setSelectedAnnotationId(null);
      setAnnotationApiStatus(`Imported ${imported.length} PDF annotation${imported.length === 1 ? '' : 's'}.`);
      emitViewerEvent('annotations:native-imported', { count: imported.length });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'PDF annotation import failed.';
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:native-import-failed', { message });
    }
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
      return labels.noSearchQuery;
    }

    if (isSearching) {
      return labels.searching;
    }

    return `${searchResults.length} ${searchResults.length === 1 ? labels.result : labels.results}`;
  }, [isSearching, labels, searchQuery, searchResults.length]);

  return (
    <main className="pdfv-shell">
        <section className="pdfv-toolbar" aria-label="PDF viewer toolbar">
          <div className="pdfv-source-group">
            <label className="pdfv-button pdfv-button-primary">
              <FiUpload />
              <span>{labels.openPdf}</span>
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
                aria-label={labels.pdfUrl}
              />
              <button type="submit" className="pdfv-button">{labels.openUrl}</button>
            </form>
          </div>

          <div className="pdfv-tool-group" aria-label="Viewer controls">
            <button className="pdfv-icon-button" type="button" onClick={() => setSidebarOpen(value => !value)} title={labels.thumbnails}>
              <FiSidebar />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => setSearchPanelOpen(value => !value)} title={labels.search}>
              <FiSearch />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => goToPage(currentPage - 1)} disabled={currentPage <= 1} title={labels.previousPage}>
              <FiChevronLeft />
            </button>
            <label className="pdfv-page-jump">
              <span className="sr-only">{labels.pageNumber}</span>
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
            <button className="pdfv-icon-button" type="button" onClick={() => goToPage(currentPage + 1)} disabled={!numPages || currentPage >= numPages} title={labels.nextPage}>
              <FiChevronRight />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => changeZoom(-0.1)} disabled={!source} title={labels.zoomOut}>
              <FiZoomOut />
            </button>
            <span className="pdfv-zoom-value">{Math.round(zoom * 100)}%</span>
            <button className="pdfv-icon-button" type="button" onClick={() => changeZoom(0.1)} disabled={!source} title={labels.zoomIn}>
              <FiZoomIn />
            </button>
            <button className={fitMode === 'page' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFitMode('page')} disabled={!source}>
              <FiMaximize2 />
              <span>{labels.fitPage}</span>
            </button>
            <button className={fitMode === 'width' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFitMode('width')} disabled={!source}>
              <FiColumns />
              <span>{labels.fitWidth}</span>
            </button>
            <button className="pdfv-icon-button" type="button" onClick={downloadCurrentPdf} disabled={!source} title={labels.download}>
              <FiDownload />
            </button>
            <button className="pdfv-icon-button" type="button" onClick={() => setPrintDialogOpen(true)} disabled={!source} title={labels.print}>
              <FiPrinter />
            </button>
            <button className={reviewPanelOpen ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewPanelOpen(value => !value)} disabled={!source}>
              <FiEdit3 />
              <span>{labels.review}</span>
            </button>
            <button className={formPanelOpen ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFormPanelOpen(value => !value)} disabled={!source}>
              <FiFileText />
              <span>{labels.forms}</span>
            </button>
            <label className="pdfv-stamp-select">
              <span>{labels.language}</span>
              <select value={viewerLocale} onChange={event => setViewerLocale(event.target.value as PdfViewerLocale)}>
                <option value="en">EN</option>
                <option value="de">DE</option>
              </select>
            </label>
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
                placeholder={labels.searchDocumentText}
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
                <span>{labels.caseSensitive}</span>
              </label>
              <button className="pdfv-button pdfv-button-primary" type="submit" disabled={!pdfDoc || isSearching}>
                {labels.search}
              </button>
              <span className="pdfv-result-summary">{resultSummary}</span>
            </form>

            {searchResults.length > 0 && (
              <div className="pdfv-result-controls">
                <button className="pdfv-button" type="button" onClick={() => selectResult(selectedResultIndex - 1)} disabled={selectedResultIndex <= 0}>
                  {labels.previousResult}
                </button>
                <strong>{selectedResultIndex + 1} / {searchResults.length}</strong>
                <button className="pdfv-button" type="button" onClick={() => selectResult(selectedResultIndex + 1)} disabled={selectedResultIndex >= searchResults.length - 1}>
                  {labels.nextResult}
                </button>
              </div>
            )}
          </section>
        )}

        {printDialogOpen && (
          <section className="pdfv-print-panel" aria-label="Print options">
            <div className="pdfv-print-options">
              <strong>{labels.print}</strong>
              <label className={printMode === 'all' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'all'} onChange={() => setPrintMode('all')} />
                <span>{labels.printAllPages}</span>
              </label>
              <label className={printMode === 'current' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'current'} onChange={() => setPrintMode('current')} />
                <span>{labels.printCurrentPage}</span>
              </label>
              <label className={printMode === 'range' ? 'pdfv-radio is-active' : 'pdfv-radio'}>
                <input type="radio" checked={printMode === 'range'} onChange={() => setPrintMode('range')} />
                <span>{labels.printRange}</span>
              </label>
              <input
                className="pdfv-range-input"
                value={printRange}
                onChange={event => {
                  setPrintMode('range');
                  setPrintRange(event.target.value);
                }}
                placeholder="1-3,5"
                aria-label={labels.pageRange}
              />
            </div>
            <div className="pdfv-print-actions">
              {printError && <span className="pdfv-print-error">{printError}</span>}
              <button className="pdfv-button" type="button" onClick={() => setPrintDialogOpen(false)}>{labels.cancel}</button>
              <button className="pdfv-button pdfv-button-primary" type="button" onClick={() => void printCurrentPdf()}>{labels.print}</button>
            </div>
          </section>
        )}

        {formPanelOpen && (
          <section className="pdfv-form-panel" aria-label={labels.formFields}>
            <div className="pdfv-form-header">
              <strong>{labels.formFields}</strong>
              <span className="pdfv-result-summary">
                {formFields.length} {formFields.length === 1 ? labels.formField : labels.formFieldsPlural}
                {changedFormFields.length > 0 ? `, ${changedFormFields.length} ${labels.formChanged}` : ''}
              </span>
            </div>

            {formFields.length > 0 ? (
              <div className="pdfv-form-fields">
                {formFields.map(field => (
                  <label key={field.name} className="pdfv-form-field">
                    <span>
                      <strong>{field.name}</strong>
                      <small>{field.kind}{field.multiline ? `, ${labels.multiline}` : ''}</small>
                    </span>
                    {field.kind === 'text' && field.multiline ? (
                      <textarea
                        value={String(field.value)}
                        onChange={event => updateFormFieldValue(field.name, event.target.value)}
                      />
                    ) : field.kind === 'text' ? (
                      <input
                        value={String(field.value)}
                        onChange={event => updateFormFieldValue(field.name, event.target.value)}
                      />
                    ) : field.kind === 'checkbox' ? (
                      <input
                        type="checkbox"
                        checked={field.value === true}
                        onChange={event => updateFormFieldValue(field.name, event.target.checked)}
                      />
                    ) : field.kind === 'radio' || field.kind === 'dropdown' ? (
                      <select
                        value={String(field.value)}
                        onChange={event => updateFormFieldValue(field.name, event.target.value)}
                      >
                        <option value="">-</option>
                        {field.options.map(option => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </select>
                    ) : field.kind === 'list' ? (
                      <select
                        multiple
                        value={Array.isArray(field.value) ? field.value : []}
                        onChange={event => updateFormFieldValue(
                          field.name,
                          Array.from(event.target.selectedOptions).map(option => option.value),
                        )}
                      >
                        {field.options.map(option => (
                          <option key={option} value={option}>{option}</option>
                        ))}
                      </select>
                    ) : (
                      <em>{labels.unsupportedField}</em>
                    )}
                  </label>
                ))}
              </div>
            ) : (
              <span className="pdfv-form-empty">{formStatus || labels.formNoFields}</span>
            )}

            <div className="pdfv-form-actions">
              <label className="pdfv-checkbox">
                <input
                  type="checkbox"
                  checked={flattenFormFields}
                  onChange={event => setFlattenFormFields(event.target.checked)}
                  disabled={formFields.length === 0}
                />
                <span>{labels.flattenFields}</span>
              </label>
              <button className="pdfv-button" type="button" onClick={resetFormFields} disabled={formFields.length === 0 || changedFormFields.length === 0}>
                {labels.resetForms}
              </button>
              <button className="pdfv-button pdfv-button-primary" type="button" onClick={() => void downloadFilledFormPdf()} disabled={formFields.length === 0}>
                <FiDownload />
                <span>{labels.downloadFilledPdf}</span>
              </button>
              {formStatus && formFields.length > 0 && <span className="pdfv-api-status">{formStatus}</span>}
            </div>
          </section>
        )}

        {reviewPanelOpen && (
          <section className="pdfv-review-panel" aria-label="Review annotations">
            <div className="pdfv-review-tools">
              <strong>{labels.review}</strong>
              <button className={reviewTool === 'view' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('view')}>
                {labels.view}
              </button>
              <button className={reviewTool === 'note' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('note')}>
                <FiMessageSquare />
                <span>{labels.note}</span>
              </button>
              <button className={reviewTool === 'freeText' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('freeText')}>
                <FiType />
                <span>{labels.text}</span>
              </button>
              <button className={reviewTool === 'stamp' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('stamp')}>
                <FiTag />
                <span>{labels.stamp}</span>
              </button>
              <label className={reviewTool === 'image' ? 'pdfv-button is-active' : 'pdfv-button'}>
                <FiImage />
                <span>{labels.image}</span>
                <input
                  className="sr-only"
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  onChange={event => {
                    void selectImageAnnotationFile(event.target.files?.[0] ?? null);
                    event.target.value = '';
                  }}
                />
              </label>
              <button className={reviewTool === 'line' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('line')}>
                <FiMinus />
                <span>{labels.line}</span>
              </button>
              <button className={reviewTool === 'rectangle' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('rectangle')}>
                <FiSquare />
                <span>{labels.rect}</span>
              </button>
              <button className={reviewTool === 'circle' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('circle')}>
                <FiCircle />
                <span>{labels.circle}</span>
              </button>
              <button className={reviewTool === 'ink' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('ink')}>
                <FiPenTool />
                <span>{labels.ink}</span>
              </button>
              <button className={reviewTool === 'inkEraser' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('inkEraser')}>
                <FiTrash2 />
                <span>{labels.eraser}</span>
              </button>
              <button className={reviewTool === 'highlight' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('highlight')}>
                <FiEdit3 />
                <span>{labels.highlight}</span>
              </button>
              <button className={reviewTool === 'underline' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('underline')}>
                <FiUnderline />
                <span>{labels.underline}</span>
              </button>
              <button className={reviewTool === 'strikeout' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('strikeout')}>
                <FiMinus />
                <span>{labels.strike}</span>
              </button>
              <button className={reviewTool === 'redaction' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('redaction')}>
                <FiEyeOff />
                <span>{labels.redact}</span>
              </button>
              {reviewTool === 'stamp' && (
                <>
                  <label className="pdfv-stamp-select">
                    <span>{labels.stamp}</span>
                    <select
                      value={selectedStamp}
                      onChange={event => setSelectedStamp(event.target.value as StampLabel)}
                    >
                      {STAMP_LABELS.map(label => (
                        <option key={label} value={label}>{label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="pdfv-author-input pdfv-stamp-text-input">
                    <span>{labels.custom}</span>
                    <input
                      value={customStampText}
                      maxLength={48}
                      placeholder={selectedStamp}
                      onChange={event => setCustomStampText(event.target.value)}
                    />
                  </label>
                </>
              )}
              <label className="pdfv-author-input">
                <span>{labels.author}</span>
                <input value={reviewAuthor} onChange={event => setReviewAuthor(event.target.value)} />
              </label>
              <span className="pdfv-result-summary">{annotations.length} {annotations.length === 1 ? labels.annotation : labels.annotations}</span>
            </div>
            {selectedAnnotation && (
              <div className="pdfv-annotation-controls">
                <strong>{labels.selected}</strong>
                <label className="pdfv-color-input">
                  <span>{labels.color}</span>
                  <input
                    type="color"
                    value={selectedAnnotation.color}
                    disabled={selectedAnnotation.locked}
                    onChange={event => updateAnnotation(selectedAnnotation.id, { color: event.target.value })}
                  />
                </label>
                {supportsOpacity(selectedAnnotation.type) && (
                  <label className="pdfv-size-input pdfv-slider-input">
                    <span>{labels.opacity}</span>
                    <input
                      type="range"
                      min={10}
                      max={100}
                      value={annotationOpacity(selectedAnnotation)}
                      disabled={selectedAnnotation.locked}
                      onChange={event => updateAnnotation(selectedAnnotation.id, {
                        opacity: clamp(Number(event.target.value), 10, 100),
                      })}
                    />
                    <small>{annotationOpacity(selectedAnnotation)}%</small>
                  </label>
                )}
                {supportsStrokeWidth(selectedAnnotation.type) && (
                  <label className="pdfv-size-input">
                    <span>{labels.stroke}</span>
                    <input
                      type="number"
                      min={1}
                      max={16}
                      value={annotationStrokeWidth(selectedAnnotation)}
                      disabled={selectedAnnotation.locked}
                      onChange={event => updateAnnotation(selectedAnnotation.id, {
                        strokeWidth: clamp(Number(event.target.value), 1, 16),
                      })}
                    />
                  </label>
                )}
                {supportsFill(selectedAnnotation.type) && (
                  <>
                    <label className="pdfv-checkbox">
                      <input
                        type="checkbox"
                        checked={Boolean(selectedAnnotation.fillEnabled)}
                        disabled={selectedAnnotation.locked}
                        onChange={event => updateAnnotation(selectedAnnotation.id, { fillEnabled: event.target.checked })}
                      />
                      <span>{labels.fill}</span>
                    </label>
                    <label className="pdfv-color-input">
                      <span>{labels.fillColor}</span>
                      <input
                        type="color"
                        value={selectedAnnotation.fillColor ?? '#ffffff'}
                        disabled={selectedAnnotation.locked || !selectedAnnotation.fillEnabled}
                        onChange={event => updateAnnotation(selectedAnnotation.id, { fillColor: event.target.value })}
                      />
                    </label>
                  </>
                )}
                {supportsLineEndings(selectedAnnotation.type) && (
                  <>
                    <label className="pdfv-stamp-select">
                      <span>{labels.start}</span>
                      <select
                        value={selectedAnnotation.lineEndingStart ?? 'none'}
                        disabled={selectedAnnotation.locked}
                        onChange={event => updateAnnotation(selectedAnnotation.id, { lineEndingStart: event.target.value as LineEnding })}
                      >
                        <option value="none">{labels.none}</option>
                        <option value="arrow">{labels.arrow}</option>
                        <option value="circle">{labels.circle}</option>
                        <option value="square">{labels.square}</option>
                      </select>
                    </label>
                    <label className="pdfv-stamp-select">
                      <span>{labels.end}</span>
                      <select
                        value={selectedAnnotation.lineEndingEnd ?? 'arrow'}
                        disabled={selectedAnnotation.locked}
                        onChange={event => updateAnnotation(selectedAnnotation.id, { lineEndingEnd: event.target.value as LineEnding })}
                      >
                        <option value="none">{labels.none}</option>
                        <option value="arrow">{labels.arrow}</option>
                        <option value="circle">{labels.circle}</option>
                        <option value="square">{labels.square}</option>
                      </select>
                    </label>
                  </>
                )}
                {selectedAnnotation.type === 'stamp' && (
                  <label className="pdfv-author-input pdfv-stamp-text-input">
                    <span>{labels.text}</span>
                    <input
                      value={selectedAnnotation.text}
                      maxLength={48}
                      disabled={selectedAnnotation.locked}
                      onChange={event => updateAnnotationText(selectedAnnotation.id, event.target.value)}
                    />
                  </label>
                )}
                {selectedAnnotation.type !== 'ink' && (
                  <>
                    <label className="pdfv-size-input">
                      <span>{labels.width}</span>
                      <input
                        type="number"
                        min={8}
                        max={90}
                        value={Math.round(selectedAnnotation.widthPct)}
                        disabled={selectedAnnotation.locked}
                        onChange={event => updateAnnotation(selectedAnnotation.id, {
                          widthPct: clamp(Number(event.target.value), 8, 100 - selectedAnnotation.xPct),
                        })}
                      />
                    </label>
                    <label className="pdfv-size-input">
                      <span>{labels.height}</span>
                      <input
                        type="number"
                        min={6}
                        max={90}
                        value={Math.round(selectedAnnotation.heightPct)}
                        disabled={selectedAnnotation.locked}
                        onChange={event => updateAnnotation(selectedAnnotation.id, {
                          heightPct: clamp(Number(event.target.value), 6, 100 - selectedAnnotation.yPct),
                        })}
                      />
                    </label>
                  </>
                )}
                <button
                  className={selectedAnnotation.locked ? 'pdfv-button is-active' : 'pdfv-button'}
                  type="button"
                  onClick={() => {
                    updateAnnotation(selectedAnnotation.id, { locked: !selectedAnnotation.locked });
                    emitViewerEvent(selectedAnnotation.locked ? 'annotation:unlocked' : 'annotation:locked', { id: selectedAnnotation.id });
                  }}
                >
                  {selectedAnnotation.locked ? <FiUnlock /> : <FiLock />}
                  <span>{selectedAnnotation.locked ? labels.unlock : labels.lock}</span>
                </button>
                <button
                  className="pdfv-button"
                  type="button"
                  disabled={selectedAnnotation.locked}
                  onClick={() => deleteAnnotation(selectedAnnotation.id)}
                >
                  <FiTrash2 />
                  <span>{labels.delete}</span>
                </button>
              </div>
            )}
            <div className="pdfv-review-actions">
              <label className="pdfv-button">
                <FiUpload />
                <span>{labels.importSidecar}</span>
                <input
                  className="sr-only"
                  type="file"
                  accept="application/json,.json"
                  onChange={event => {
                    void importAnnotationSidecar(event.target.files?.[0] ?? null);
                    event.target.value = '';
                  }}
                />
              </label>
              <button className="pdfv-button" type="button" onClick={() => void importNativeAnnotationsFromPdf()} disabled={!source}>
                <FiUpload />
                <span>{labels.importNativeAnnotations}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={downloadAnnotationSidecar} disabled={annotations.length === 0}>
                <FiDownload />
                <span>{labels.exportSidecar}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void downloadFlattenedPdf()} disabled={!source || annotations.length === 0}>
                <FiFileText />
                <span>{labels.flattenPdf}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void downloadEmbeddedAnnotationsPdf()} disabled={!source || annotations.length === 0}>
                <FiFileText />
                <span>{labels.embedAnnotations}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void downloadRedactedPdf()} disabled={!source || redactionAnnotations.length === 0}>
                <FiEyeOff />
                <span>{labels.applyRedactions}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void saveAnnotationSidecar()} disabled={annotations.length === 0}>
                <FiDownload />
                <span>{labels.save}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void loadAnnotationSidecar()}>
                <FiUpload />
                <span>{labels.loadSaved}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void deleteSavedAnnotationSidecar()}>
                <FiTrash2 />
                <span>{labels.deleteSaved}</span>
              </button>
              {annotationApiStatus && <span className="pdfv-api-status">{annotationApiStatus}</span>}
            </div>
          </section>
        )}

        <section className="pdfv-workspace">
          {!source && (
            <div className="pdfv-empty">
              <FiFileText />
              <h1>{labels.emptyTitle}</h1>
              <p>{labels.emptyDescription}</p>
            </div>
          )}

          {source && (
            <Document
              className="pdfv-document"
              file={source.file}
              onLoadSuccess={onDocumentLoaded}
              onLoadError={onDocumentError}
              loading={<div className="pdfv-state">{labels.loadingPdf}</div>}
              error={<div className="pdfv-state is-error">{loadError || 'PDF could not be loaded.'}</div>}
            >
              {sidebarOpen && (
                <aside className="pdfv-sidebar" aria-label="Page thumbnails">
                  <div className="pdfv-sidebar-header">
                    <strong>{source.name}</strong>
                    <span>{numPages || '-'} {labels.pages}</span>
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
                  <div
                    className={reviewTool === 'view' ? 'pdfv-page-stack' : 'pdfv-page-stack is-annotating'}
                    ref={pageStackRef}
                    onClick={addAnnotationAtPoint}
                    onMouseDown={beginInkDrawing}
                    onMouseUp={createTextMarkupFromSelection}
                  >
                  <Page
                    pageNumber={currentPage}
                    scale={zoom}
                    customTextRenderer={highlightedTextRenderer}
                    renderTextLayer
                    renderAnnotationLayer
                    loading={<div className="pdfv-state">{labels.renderingPage}</div>}
                  />
                    <div className="pdfv-annotation-layer" aria-label="Annotation sidecar layer">
                      {currentPageInkAnnotations.length > 0 && (
                        <svg className="pdfv-ink-layer" viewBox="0 0 100 100" preserveAspectRatio="none">
                          {currentPageInkAnnotations.map(annotation => (
                            <polyline
                              key={annotation.id}
                              className={[
                                selectedAnnotationId === annotation.id ? 'is-selected' : '',
                                reviewTool === 'inkEraser' && !annotation.locked ? 'is-erasable' : '',
                                annotation.locked ? 'is-locked' : '',
                              ].filter(Boolean).join(' ')}
                              points={(annotation.points ?? []).map(point => `${point.xPct},${point.yPct}`).join(' ')}
                              stroke={annotation.color}
                              strokeOpacity={annotationOpacity(annotation) / 100}
                              strokeWidth={annotationStrokeWidth(annotation)}
                              onClick={event => {
                                event.stopPropagation();
                                if (reviewTool === 'inkEraser') {
                                  eraseInkAnnotation(annotation.id);
                                  return;
                                }

                                setSelectedAnnotationId(annotation.id);
                              }}
                              onPointerEnter={() => {
                                if (reviewTool === 'inkEraser') {
                                  eraseInkAnnotation(annotation.id);
                                }
                              }}
                            />
                          ))}
                        </svg>
                      )}
                      {currentPageBoxAnnotations.map(annotation => (
                        <div
                          key={annotation.id}
                          className={[
                            'pdfv-annotation',
                            `pdfv-annotation-${annotation.type}`,
                            selectedAnnotationId === annotation.id ? 'is-selected' : '',
                            annotation.locked ? 'is-locked' : '',
                          ].filter(Boolean).join(' ')}
                          style={{
                            left: `${annotation.xPct}%`,
                            top: `${annotation.yPct}%`,
                            width: `${annotation.widthPct}%`,
                            minHeight: `${annotation.heightPct}%`,
                            borderColor: annotation.color,
                            color: annotation.color,
                            opacity: annotationOpacity(annotation) / 100,
                            ['--pdfv-stroke-width' as string]: `${annotationStrokeWidth(annotation)}px`,
                            ['--pdfv-fill-color' as string]: annotation.fillEnabled ? (annotation.fillColor ?? annotation.color) : 'transparent',
                          }}
                          onClick={event => {
                            event.stopPropagation();
                            setSelectedAnnotationId(annotation.id);
                          }}
                        >
                          <button
                            className="pdfv-annotation-drag"
                            type="button"
                            aria-label="Move annotation"
                            disabled={annotation.locked}
                            onMouseDown={event => beginAnnotationInteraction(event, annotation, 'move')}
                          />
                          {annotation.type === 'image'
                            ? (
                              <img
                                className="pdfv-image-content"
                                src={annotation.imageDataUrl}
                                alt={annotation.text || 'Image annotation'}
                                draggable={false}
                              />
                            )
                            : annotation.type === 'stamp'
                            ? <strong className="pdfv-stamp-label">{annotation.text}</strong>
                            : annotation.type === 'line'
                              ? (
                                <svg className="pdfv-shape-line" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
                                  <defs>
                                    <marker id={`line-start-${annotation.id}`} markerWidth="10" markerHeight="10" refX="2" refY="5" orient="auto" markerUnits="strokeWidth">
                                      {annotation.lineEndingStart === 'arrow' && <path d="M10 0 L0 5 L10 10 z" fill="currentColor" />}
                                      {annotation.lineEndingStart === 'circle' && <circle cx="5" cy="5" r="4" fill="currentColor" />}
                                      {annotation.lineEndingStart === 'square' && <rect x="1.5" y="1.5" width="7" height="7" fill="currentColor" />}
                                    </marker>
                                    <marker id={`line-end-${annotation.id}`} markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto" markerUnits="strokeWidth">
                                      {annotation.lineEndingEnd === 'arrow' && <path d="M0 0 L10 5 L0 10 z" fill="currentColor" />}
                                      {annotation.lineEndingEnd === 'circle' && <circle cx="5" cy="5" r="4" fill="currentColor" />}
                                      {annotation.lineEndingEnd === 'square' && <rect x="1.5" y="1.5" width="7" height="7" fill="currentColor" />}
                                    </marker>
                                  </defs>
                                  <line
                                    x1="2"
                                    y1="50"
                                    x2="98"
                                    y2="50"
                                    markerStart={annotation.lineEndingStart && annotation.lineEndingStart !== 'none' ? `url(#line-start-${annotation.id})` : undefined}
                                    markerEnd={annotation.lineEndingEnd && annotation.lineEndingEnd !== 'none' ? `url(#line-end-${annotation.id})` : undefined}
                                  />
                                </svg>
                              )
                              : annotation.type === 'highlight'
                                ? (
                                  annotation.quadPoints && annotation.quadPoints.length > 0
                                    ? (
                                      <span className="pdfv-markup-quads" aria-hidden="true">
                                        {annotation.quadPoints.map((quad, index) => (
                                          <span
                                            key={`${annotation.id}-quad-${index}`}
                                            className="pdfv-markup-highlight"
                                            style={{
                                              left: `${((Math.min(quad.x1Pct, quad.x3Pct) - annotation.xPct) / annotation.widthPct) * 100}%`,
                                              top: `${((Math.min(quad.y1Pct, quad.y2Pct) - annotation.yPct) / annotation.heightPct) * 100}%`,
                                              width: `${((Math.max(quad.x2Pct, quad.x4Pct) - Math.min(quad.x1Pct, quad.x3Pct)) / annotation.widthPct) * 100}%`,
                                              height: `${((Math.max(quad.y3Pct, quad.y4Pct) - Math.min(quad.y1Pct, quad.y2Pct)) / annotation.heightPct) * 100}%`,
                                            }}
                                          />
                                        ))}
                                      </span>
                                    )
                                    : <span className="pdfv-markup-highlight" />
                                )
                                : annotation.type === 'redaction'
                                  ? <span className="pdfv-markup-redaction" />
                                : annotation.type === 'underline'
                                  ? (
                                    annotation.quadPoints && annotation.quadPoints.length > 0
                                      ? (
                                        <span className="pdfv-markup-quads" aria-hidden="true">
                                          {annotation.quadPoints.map((quad, index) => (
                                            <span
                                              key={`${annotation.id}-quad-${index}`}
                                              className="pdfv-markup-underline"
                                              style={{
                                                left: `${((Math.min(quad.x1Pct, quad.x3Pct) - annotation.xPct) / annotation.widthPct) * 100}%`,
                                                top: `${((Math.max(quad.y3Pct, quad.y4Pct) - annotation.yPct) / annotation.heightPct) * 100}%`,
                                                width: `${((Math.max(quad.x2Pct, quad.x4Pct) - Math.min(quad.x1Pct, quad.x3Pct)) / annotation.widthPct) * 100}%`,
                                              }}
                                            />
                                          ))}
                                        </span>
                                      )
                                      : <span className="pdfv-markup-underline" />
                                  )
                                  : annotation.type === 'strikeout'
                                    ? (
                                      annotation.quadPoints && annotation.quadPoints.length > 0
                                        ? (
                                          <span className="pdfv-markup-quads" aria-hidden="true">
                                            {annotation.quadPoints.map((quad, index) => (
                                              <span
                                                key={`${annotation.id}-quad-${index}`}
                                                className="pdfv-markup-strikeout"
                                                style={{
                                                  left: `${((Math.min(quad.x1Pct, quad.x3Pct) - annotation.xPct) / annotation.widthPct) * 100}%`,
                                                  top: `${((((Math.min(quad.y1Pct, quad.y2Pct) + Math.max(quad.y3Pct, quad.y4Pct)) / 2) - annotation.yPct) / annotation.heightPct) * 100}%`,
                                                  width: `${((Math.max(quad.x2Pct, quad.x4Pct) - Math.min(quad.x1Pct, quad.x3Pct)) / annotation.widthPct) * 100}%`,
                                                }}
                                              />
                                            ))}
                                          </span>
                                        )
                                        : <span className="pdfv-markup-strikeout" />
                                    )
                              : annotation.type === 'rectangle'
                                ? <span className="pdfv-shape-rectangle" />
                                : annotation.type === 'circle'
                                  ? <span className="pdfv-shape-circle" />
                            : (
                              <textarea
                                value={annotation.text}
                                aria-label={`${annotation.type} annotation`}
                                disabled={annotation.locked}
                                onChange={event => updateAnnotationText(annotation.id, event.target.value)}
                              />
                            )}
                          <div className="pdfv-annotation-meta">
                            <span>{annotation.author}</span>
                            {annotation.locked && <FiLock aria-label="Locked annotation" />}
                            <button
                              type="button"
                              aria-label="Delete annotation"
                              disabled={annotation.locked}
                              onClick={event => {
                                event.stopPropagation();
                                deleteAnnotation(annotation.id);
                              }}
                            >
                              <FiTrash2 />
                            </button>
                          </div>
                          <button
                            className="pdfv-annotation-resize"
                            type="button"
                            aria-label="Resize annotation"
                            disabled={annotation.locked}
                            onMouseDown={event => beginAnnotationInteraction(event, annotation, 'resize')}
                          />
                        </div>
                      ))}
                    </div>
                  </div>
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
