import React, { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import '@/i18n';
import { SUPPORTED_LANGUAGES } from '@/i18n';
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
  FiShield,
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
import { applyRedactions, deleteSavedAnnotations, embedAnnotations, extractNativeAnnotations, flattenAnnotations, loadAnnotations, preflightRedactions, saveAnnotations } from './annotationApi';
import {
  allFindings,
  isApplyAllowed,
  RedactionBlockedError,
  type RedactionPreflightReport,
  type RedactionVerification,
} from './redaction';
import { fillPdfFormFields, readPdfFormFields, sameFormValue, type PdfFormFieldInfo, type PdfFormFieldValue } from './pdfForms';
import { configurePdfWorker } from './pdfWorker';
import { assertPdfBytes, createPdfBlob, downloadPdfBytes } from './pdfArtifact';
import { convertPdfA2b, inspectPdfTrust, PdfTrustOperationError, preflightPdfA2b, signPdf, type PdfA2bConversionPreflightReport, type PdfArchiveProfile, type PdfPadesProfile, type PdfTrustFailureKind, type PdfTrustReport, type PdfTrustStatus } from './pdfTrustApi';
import { TrustStatusBadge } from './TrustStatusBadge';
import { extractEInvoice, inspectEInvoices, type EInvoiceAttachmentSummary } from './eInvoiceApi';
import { notify } from '@/notifications/toast';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

configurePdfWorker();

export type PdfViewerFitMode = 'page' | 'width' | 'custom';
type PdfSourceKind = 'file' | 'url';
type PrintMode = 'all' | 'current' | 'range';

export interface PdfSource {
  file: File | string;
  kind: PdfSourceKind;
  name: string;
  url?: string;
}

export interface PdfViewerProps {
  initialSource?: PdfSource | null;
  initialPageNumber?: number;
  initialFitMode?: Exclude<PdfViewerFitMode, 'custom'>;
  initialAnnotations?: PdfAnnotation[];
  initialFormValues?: Array<{ name: string; value: PdfFormFieldValue }>;
  canDownload?: boolean;
  canPrint?: boolean;
  canInspectTrust?: boolean;
  canSign?: boolean;
  canConvertCompliance?: boolean;
  canAnnotate?: boolean;
  canFillForms?: boolean;
  canRedact?: boolean;
}

interface SearchResult {
  id: string;
  pageNumber: number;
  matchIndex: number;
  snippet: string;
}

interface NavigablePdfDiagnostic {
  code: string;
  canonicalCode?: string | null;
  severity: 0 | 1 | 2;
  message: string;
  objectNumber?: number | null;
  generation?: number | null;
  pageNumber?: number | null;
  elementId?: string | null;
  localizationKey?: string | null;
  localizationParameters?: Record<string, string> | null;
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

const EMPTY_INITIAL_ANNOTATIONS: PdfAnnotation[] = [];
const EMPTY_INITIAL_FORM_VALUES: Array<{ name: string; value: PdfFormFieldValue }> = [];

const annotationStrokeWidth = (annotation: PdfAnnotation): number => annotation.strokeWidth ?? defaultStrokeWidthForType(annotation.type);

const annotationOpacity = (annotation: PdfAnnotation): number => annotation.opacity ?? defaultOpacityForType(annotation.type);

const isTextMarkupTool = (tool: ReviewTool): boolean => tool === 'highlight' || tool === 'underline' || tool === 'strikeout';

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

const PdfViewer: React.FC<PdfViewerProps> = ({
  initialSource = null,
  initialPageNumber = 1,
  initialFitMode = 'page',
  initialAnnotations = EMPTY_INITIAL_ANNOTATIONS,
  initialFormValues = EMPTY_INITIAL_FORM_VALUES,
  canDownload = true,
  canPrint = true,
  canInspectTrust = true,
  canSign = false,
  canConvertCompliance = true,
  canAnnotate = true,
  canFillForms = true,
  canRedact = true,
}) => {
  const [source, setSource] = useState<PdfSource | null>(initialSource);
  const [urlInput, setUrlInput] = useState(initialSource?.url ?? '');
  const [pdfDoc, setPdfDoc] = useState<PDFDocumentProxy | null>(null);
  const [numPages, setNumPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(Math.max(1, Math.trunc(initialPageNumber)));
  const [pageInput, setPageInput] = useState(String(Math.max(1, Math.trunc(initialPageNumber))));
  const [zoom, setZoom] = useState(1);
  const [fitMode, setFitMode] = useState<PdfViewerFitMode>(initialFitMode);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [searchPanelOpen, setSearchPanelOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [caseSensitive, setCaseSensitive] = useState(false);
  const [searchResults, setSearchResults] = useState<SearchResult[]>([]);
  const [selectedResultIndex, setSelectedResultIndex] = useState(0);
  const [isSearching, setIsSearching] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [viewerEvents, setViewerEvents] = useState<ViewerEvent[]>([]);
  const [printDialogOpen, setPrintDialogOpen] = useState(false);
  const [printMode, setPrintMode] = useState<PrintMode>('all');
  const [printRange, setPrintRange] = useState('1');
  const [printError, setPrintError] = useState<string | null>(null);
  const [downloadPending, setDownloadPending] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [trustPanelOpen, setTrustPanelOpen] = useState(false);
  const [trustReport, setTrustReport] = useState<PdfTrustReport | null>(null);
  const [trustLoading, setTrustLoading] = useState(false);
  const [trustError, setTrustError] = useState<{ kind: PdfTrustFailureKind; message: string } | null>(null);
  const [eInvoices, setEInvoices] = useState<EInvoiceAttachmentSummary[]>([]);
  const [eInvoiceLoading, setEInvoiceLoading] = useState(false);
  const [eInvoiceError, setEInvoiceError] = useState<string | null>(null);
  const [compliancePreflight, setCompliancePreflight] = useState<PdfA2bConversionPreflightReport | null>(null);
  const [complianceLoading, setComplianceLoading] = useState(false);
  const [complianceProfile, setComplianceProfile] = useState<PdfArchiveProfile>('PDF/A-2b');
  const [allowSignatureInvalidation, setAllowSignatureInvalidation] = useState(false);
  const [allowTrustMutation, setAllowTrustMutation] = useState(false);
  const [signingCertificate, setSigningCertificate] = useState<File | null>(null);
  const [signingPassword, setSigningPassword] = useState('');
  const [signingProfile, setSigningProfile] = useState<PdfPadesProfile>('BaselineB');
  const [signingFieldName, setSigningFieldName] = useState('Signature1');
  const [signingReason, setSigningReason] = useState('');
  const [signingVisible, setSigningVisible] = useState(false);
  const [signingBusy, setSigningBusy] = useState(false);
  const [signingStatus, setSigningStatus] = useState<string | null>(null);
  const [reviewPanelOpen, setReviewPanelOpen] = useState(false);
  const [reviewTool, setReviewTool] = useState<ReviewTool>('view');
  const [annotations, setAnnotations] = useState<PdfAnnotation[]>(initialAnnotations);
  const [selectedAnnotationId, setSelectedAnnotationId] = useState<string | null>(null);
  const [reviewAuthor, setReviewAuthor] = useState('Reviewer');
  const [selectedStamp, setSelectedStamp] = useState<StampLabel>('Draft');
  const [customStampText, setCustomStampText] = useState('');
  const [pendingImageDataUrl, setPendingImageDataUrl] = useState<string | null>(null);
  const { t, i18n } = useTranslation('pdfViewer', { useSuspense: false });
  const [annotationApiStatus, setAnnotationApiStatus] = useState<string | null>(null);
  const [redactionPreflight, setRedactionPreflight] = useState<RedactionPreflightReport | null>(null);
  const [redactionPreflightFingerprint, setRedactionPreflightFingerprint] = useState<string | null>(null);
  const [redactionVerification, setRedactionVerification] = useState<RedactionVerification | null>(null);
  const [redactionAudit, setRedactionAudit] = useState<{ record: string | null; signed: boolean } | null>(null);
  const [redactedResult, setRedactedResult] = useState<Blob | null>(null);
  const [redactionCompareOpen, setRedactionCompareOpen] = useState(false);
  const [redactionBusy, setRedactionBusy] = useState(false);
  const [formPanelOpen, setFormPanelOpen] = useState(false);
  const [formFields, setFormFields] = useState<PdfFormFieldInfo[]>([]);
  const [formStatus, setFormStatus] = useState<string | null>(null);
  const [flattenFormFields, setFlattenFormFields] = useState(false);
  const stageRef = useRef<HTMLDivElement | null>(null);
  const pageStackRef = useRef<HTMLDivElement | null>(null);
  const eventIdRef = useRef(0);
  const annotationInteractionRef = useRef<AnnotationInteraction | null>(null);
  const suppressNextPageClickRef = useRef(false);
  const downloadLockRef = useRef(false);
  const printLockRef = useRef(false);
  const redactionFingerprintRef = useRef('');
  const trustInspectionAbortRef = useRef<AbortController | null>(null);

  const currentResult = searchResults[selectedResultIndex] ?? null;
  const currentPageAnnotations = annotations.filter(annotation => annotation.pageNumber === currentPage);
  const currentPageInkAnnotations = currentPageAnnotations.filter(annotation => annotation.type === 'ink');
  const currentPageBoxAnnotations = currentPageAnnotations.filter(annotation => annotation.type !== 'ink');
  const selectedAnnotation = annotations.find(annotation => annotation.id === selectedAnnotationId) ?? null;
  const documentId = useMemo(() => documentIdFromSource(source), [source]);
  const labels = {
    openPdf: t('openPdf'),
    openUrl: t('openUrl'),
    pdfUrl: t('pdfUrl'),
    thumbnails: t('thumbnails'),
    search: t('search'),
    previousPage: t('previousPage'),
    nextPage: t('nextPage'),
    pageNumber: t('pageNumber'),
    zoomOut: t('zoomOut'),
    zoomIn: t('zoomIn'),
    fitPage: t('fitPage'),
    fitWidth: t('fitWidth'),
    download: t('download'),
    downloadFailed: t('downloadFailed'),
    invalidPdf: t('invalidPdf'),
    print: t('print'),
    printBlocked: t('printBlocked'),
    printFailed: t('printFailed'),
    preparingPrint: t('preparingPrint'),
    review: t('review'),
    forms: t('forms'),
    trust: t('trust'),
    inspectTrust: t('inspectTrust'),
    trustLoading: t('trustLoading'),
    trustEmpty: t('trustEmpty'),
    trustFailed: t('trustFailed'),
    trustCancel: t('trustCancel'),
    trustRetry: t('trustRetry'),
    pdfAPreflight: t('pdfAPreflight'),
    pdfAProfile: t('pdfAProfile'),
    pdfAConvert: t('pdfAConvert'),
    pdfAConverting: t('pdfAConverting'),
    pdfAReady: t('pdfAReady'),
    pdfAReviewRequired: t('pdfAReviewRequired'),
    pdfAUnsupported: t('pdfAUnsupported'),
    pdfAAllowSignatureInvalidation: t('pdfAAllowSignatureInvalidation'),
    pdfAConverted: t('pdfAConverted'),
    structure: t('structure'),
    encryption: t('encryption'),
    signatures: t('signatures'),
    signature: t('signature'),
    integrity: t('integrity'),
    coverage: t('coverage'),
    certificateTrust: t('certificateTrust'),
    revocation: t('revocation'),
    signer: t('signer'),
    timestamp: t('timestamp'),
    timestampAuthority: t('timestampAuthority'),
    timestampTime: t('timestampTime'),
    timestampRevocation: t('timestampRevocation'),
    historicalRevocation: t('historicalRevocation'),
    timestampHistoricalRevocation: t('timestampHistoricalRevocation'),
    documentTimestamp: t('documentTimestamp'),
    archiveTimestamp: t('archiveTimestamp'),
    archiveTimestampTime: t('archiveTimestampTime'),
    archiveTimestampAuthority: t('archiveTimestampAuthority'),
    revocationEvidence: t('revocationEvidence'),
    embeddedValidationMaterial: t('embeddedValidationMaterial'),
    certificates: t('certificates'),
    crls: t('crls'),
    ocspResponses: t('ocspResponses'),
    vriEntries: t('vriEntries'),
    vriMatched: t('vriMatched'),
    validUntil: t('validUntil'),
    wholeDocument: t('wholeDocument'),
    earlierRevision: t('earlierRevision'),
    revisions: t('revisions'),
    objects: t('objects'),
    notDetected: t('notDetected'),
    signatureObject: t('signatureObject'),
    certificateThumbprint: t('certificateThumbprint'),
    signingTime: t('signingTime'),
    signedRevision: t('signedRevision'),
    signedRevisionOffset: t('signedRevisionOffset'),
    modificationsAfterSigning: t('modificationsAfterSigning'),
    noModifications: t('noModifications'),
    laterModifications: t('laterModifications'),
    certification: t('certification'),
    lockedFields: t('lockedFields'),
    signatureField: t('signatureField'),
    signatureFieldLocation: t('signatureFieldLocation'),
    affectedObject: t('affectedObject'),
    affectedElement: t('affectedElement'),
    technicalDetails: t('technicalDetails'),
    mutationWarningTitle: t('mutationWarningTitle'),
    mutationWarningBody: t('mutationWarningBody'),
    mutationConfirmation: t('mutationConfirmation'),
    trustInspectionRequired: t('trustInspectionRequired'),
    signPdf: t('signPdf'),
    signingCertificate: t('signingCertificate'),
    signingPassword: t('signingPassword'),
    signingProfile: t('signingProfile'),
    signingFieldName: t('signingFieldName'),
    signingReason: t('signingReason'),
    signingVisible: t('signingVisible'),
    signingInProgress: t('signingInProgress'),
    signingComplete: t('signingComplete'),
    signingCertificateRequired: t('signingCertificateRequired'),
    signingPrivacy: t('signingPrivacy'),
    riskyContent: t('riskyContent'),
    javaScriptActions: t('javaScriptActions'),
    launchActions: t('launchActions'),
    embeddedFiles: t('embeddedFiles'),
    eInvoices: t('eInvoices'),
    eInvoiceInspecting: t('eInvoiceInspecting'),
    eInvoiceFailed: t('eInvoiceFailed'),
    eInvoiceStandard: t('eInvoiceStandard'),
    eInvoiceProfile: t('eInvoiceProfile'),
    eInvoiceSyntax: t('eInvoiceSyntax'),
    eInvoiceValidation: t('eInvoiceValidation'),
    eInvoiceNumber: t('eInvoiceNumber'),
    eInvoiceSeller: t('eInvoiceSeller'),
    eInvoiceBuyer: t('eInvoiceBuyer'),
    eInvoiceCurrency: t('eInvoiceCurrency'),
    eInvoicePayable: t('eInvoicePayable'),
    eInvoiceTax: t('eInvoiceTax'),
    eInvoiceLines: t('eInvoiceLines'),
    eInvoicePdfConsistency: t('eInvoicePdfConsistency'),
    eInvoiceConsistency: {
      consistent: t('eInvoiceConsistency.consistent'),
      reviewRequired: t('eInvoiceConsistency.reviewRequired'),
      notEvaluated: t('eInvoiceConsistency.notEvaluated'),
    },
    eInvoiceStructuralValid: t('eInvoiceStructuralValid'),
    eInvoiceStructuralInvalid: t('eInvoiceStructuralInvalid'),
    eInvoiceDownloadXml: t('eInvoiceDownloadXml'),
    eInvoiceConformance: {
      valid: t('eInvoiceConformance.valid'),
      invalid: t('eInvoiceConformance.invalid'),
      unavailable: t('eInvoiceConformance.unavailable'),
      notEvaluated: t('eInvoiceConformance.notEvaluated'),
    },
    fileAttachments: t('fileAttachments'),
    richMedia: t('richMedia'),
    xfaForms: t('xfaForms'),
    externalLinks: t('externalLinks'),
    language: t('language'),
    searchDocumentText: t('searchDocumentText'),
    caseSensitive: t('caseSensitive'),
    noSearchQuery: t('noSearchQuery'),
    searching: t('searching'),
    result: t('result'),
    results: t('results'),
    previousResult: t('previousResult'),
    nextResult: t('nextResult'),
    printAllPages: t('printAllPages'),
    printCurrentPage: t('printCurrentPage'),
    printRange: t('printRange'),
    pageRange: t('pageRange'),
    cancel: t('cancel'),
    view: t('view'),
    note: t('note'),
    text: t('text'),
    stamp: t('stamp'),
    image: t('image'),
    line: t('line'),
    rect: t('rect'),
    circle: t('circle'),
    ink: t('ink'),
    eraser: t('eraser'),
    highlight: t('highlight'),
    underline: t('underline'),
    strike: t('strike'),
    redact: t('redact'),
    custom: t('custom'),
    author: t('author'),
    annotation: t('annotation'),
    annotations: t('annotations'),
    selected: t('selected'),
    color: t('color'),
    opacity: t('opacity'),
    stroke: t('stroke'),
    fill: t('fill'),
    fillColor: t('fillColor'),
    start: t('start'),
    end: t('end'),
    none: t('none'),
    arrow: t('arrow'),
    square: t('square'),
    width: t('width'),
    height: t('height'),
    lock: t('lock'),
    unlock: t('unlock'),
    delete: t('delete'),
    importSidecar: t('importSidecar'),
    importNativeAnnotations: t('importNativeAnnotations'),
    exportSidecar: t('exportSidecar'),
    flattenPdf: t('flattenPdf'),
    embedAnnotations: t('embedAnnotations'),
    applyRedactions: t('applyRedactions'),
    redactionPreflight: t('redactionPreflight'),
    redactionPreflightRunning: t('redactionPreflightRunning'),
    redactionPreflightReady: t('redactionPreflightReady'),
    redactionPreflightBlocked: t('redactionPreflightBlocked'),
    redactionApplying: t('redactionApplying'),
    redactionVerified: t('redactionVerified'),
    redactionFailed: t('redactionFailed'),
    redactionStepPreflight: t('redactionStepPreflight'),
    redactionStepApply: t('redactionStepApply'),
    redactionStepVerify: t('redactionStepVerify'),
    redactionFindings: t('redactionFindings'),
    redactionAuditSigned: t('redactionAuditSigned'),
    redactionAuditUnsigned: t('redactionAuditUnsigned'),
    redactionCompare: t('redactionCompare'),
    redactionCompareTitle: t('redactionCompareTitle'),
    redactionOriginal: t('redactionOriginal'),
    redactionResult: t('redactionResult'),
    close: t('close'),
    redactionRequiresPreflight: t('redactionRequiresPreflight'),
    save: t('save'),
    loadSaved: t('loadSaved'),
    deleteSaved: t('deleteSaved'),
    formFields: t('formFields'),
    formField: t('formField'),
    formFieldsPlural: t('formFieldsPlural'),
    formChanged: t('formChanged'),
    multiline: t('multiline'),
    formNoFields: t('formNoFields'),
    formLoading: t('formLoading'),
    formNone: t('formNone'),
    formLoadFailed: t('formLoadFailed'),
    formSaving: t('formSaving'),
    formDownloaded: t('formDownloaded'),
    formFlattened: t('formFlattened'),
    formSaveFailed: t('formSaveFailed'),
    downloadFilledPdf: t('downloadFilledPdf'),
    resetForms: t('resetForms'),
    flattenFields: t('flattenFields'),
    unsupportedField: t('unsupportedField'),
    emptyTitle: t('emptyTitle'),
    emptyDescription: t('emptyDescription'),
    pages: t('pages'),
    loadingPdf: t('loadingPdf'),
    renderingPage: t('renderingPage'),
  };
  const changedFormFields = formFields.filter(field => !sameFormValue(field.value, field.originalValue));
  const redactionAnnotations = annotations.filter(annotation => annotation.type === 'redaction');
  const redactionSidecar = createAnnotationSidecar(source?.name ?? null, redactionAnnotations);
  const redactionFingerprint = JSON.stringify({
    documentId,
    sourceUrl: source?.url ?? null,
    sourceSize: source?.file instanceof File ? source.file.size : null,
    sourceLastModified: source?.file instanceof File ? source.file.lastModified : null,
    sidecar: {
      version: redactionSidecar.version,
      sourceName: redactionSidecar.sourceName,
      annotations: redactionSidecar.annotations,
    },
  });
  redactionFingerprintRef.current = redactionFingerprint;
  const effectiveRedactionPreflight = redactionPreflightFingerprint === redactionFingerprint
    ? redactionPreflight
    : null;
  const requiresTrustMutationConfirmation = Boolean(
    trustReport && (trustReport.signatures.length > 0 || trustReport.pdfA.isClaimed || trustReport.pdfUa.isClaimed),
  );
  const trustMutationAllowed = !canInspectTrust || Boolean(
    trustReport && (!requiresTrustMutationConfirmation || allowTrustMutation),
  );

  const emitViewerEvent = useCallback((label: string, detail: Record<string, unknown> = {}) => {
    eventIdRef.current += 1;
    const event = { id: eventIdRef.current, label };
    setViewerEvents(previous => [event, ...previous].slice(0, 6));
    window.dispatchEvent(new CustomEvent('pdf-viewer:event', { detail: { label, ...detail } }));
  }, []);

  useEffect(() => {
    setSource(initialSource);
    setUrlInput(initialSource?.url ?? '');
    setCurrentPage(Math.max(1, Math.trunc(initialPageNumber)));
    setFitMode(initialFitMode);
    setAnnotations(initialAnnotations);
    if (initialSource) {
      emitViewerEvent('open:initial', { name: initialSource.name });
    }
  }, [emitViewerEvent, initialAnnotations, initialFitMode, initialPageNumber, initialSource]);

  useEffect(() => {
    setPageInput(String(currentPage));
    setPrintRange(String(currentPage));
    emitViewerEvent('page:changed', { currentPage });
  }, [currentPage, emitViewerEvent]);

  useEffect(() => {
    emitViewerEvent('zoom:changed', { zoom: Number(zoom.toFixed(2)), fitMode });
  }, [fitMode, zoom, emitViewerEvent]);

  const applyFitMode = useCallback(async (mode: PdfViewerFitMode) => {
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
    setCurrentPage(previous => clamp(previous, 1, document.numPages));
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

  const trustStatusLabel = useCallback((status: PdfTrustStatus): string => {
    const keys = ['valid', 'invalid', 'indeterminate', 'unsupported', 'notApplicable'] as const;
    return t(`trustStatus.${keys[status] ?? 'indeterminate'}`);
  }, [t]);

  const trustStatusExplanation = useCallback((status: PdfTrustStatus): string => {
    const keys = ['valid', 'invalid', 'indeterminate', 'unsupported', 'notApplicable'] as const;
    return t(`trustStatusExplanation.${keys[status] ?? 'indeterminate'}`);
  }, [t]);

  const renderTrustStatus = useCallback((status: PdfTrustStatus) => (
    <TrustStatusBadge
      status={status}
      label={trustStatusLabel(status)}
      explanation={trustStatusExplanation(status)}
    />
  ), [trustStatusExplanation, trustStatusLabel]);

  const trustFailureLabel = useCallback((kind: PdfTrustFailureKind): string =>
    t(`trustFailure.${kind}`), [t]);

  const diagnosticText = useCallback((diagnostic: NavigablePdfDiagnostic): string => {
    const localizedKey = diagnostic.localizationKey
      ? `pdfViewer:${diagnostic.localizationKey}`
      : null;
    if (localizedKey && i18n.exists(localizedKey)) {
      return i18n.t(localizedKey, diagnostic.localizationParameters ?? {});
    }
    const severities = ['information', 'warning', 'error'] as const;
    return t(`diagnosticSummary.${severities[diagnostic.severity] ?? 'warning'}`, {
      code: diagnostic.code,
      ...(diagnostic.localizationParameters ?? {}),
    });
  }, [i18n, t]);

  const navigateDiagnostic = useCallback((diagnostic: NavigablePdfDiagnostic) => {
    if (diagnostic.pageNumber != null) {
      goToPage(diagnostic.pageNumber);
    }
    if (diagnostic.elementId) {
      window.dispatchEvent(new CustomEvent('pvad-designer:navigate-element', {
        detail: { elementId: diagnostic.elementId, pageNumber: diagnostic.pageNumber ?? null },
      }));
    }
    if (diagnostic.objectNumber != null) {
      window.dispatchEvent(new CustomEvent('pdf-viewer:navigate-object', {
        detail: {
          objectNumber: diagnostic.objectNumber,
          generation: diagnostic.generation ?? 0,
          pageNumber: diagnostic.pageNumber ?? null,
        },
      }));
    }
    emitViewerEvent('diagnostic:navigated', {
      code: diagnostic.code,
      pageNumber: diagnostic.pageNumber ?? null,
      objectNumber: diagnostic.objectNumber ?? null,
      elementId: diagnostic.elementId ?? null,
    });
  }, [emitViewerEvent, goToPage]);

  const renderDiagnostic = useCallback((diagnostic: NavigablePdfDiagnostic) => (
    <>
      <code title={diagnostic.code}>{diagnostic.canonicalCode ?? diagnostic.code}</code>
      <span>{diagnosticText(diagnostic)}</span>
      <span className="pdfv-diagnostic-targets">
        {diagnostic.pageNumber != null && (
          <button className="pdfv-inline-link" type="button" onClick={() => navigateDiagnostic(diagnostic)}>
            {labels.pageNumber} {diagnostic.pageNumber}
          </button>
        )}
        {diagnostic.objectNumber != null && (
          <button className="pdfv-inline-link" type="button" onClick={() => navigateDiagnostic(diagnostic)}>
            {labels.affectedObject} #{diagnostic.objectNumber} {diagnostic.generation ?? 0}
          </button>
        )}
        {diagnostic.elementId && (
          <button className="pdfv-inline-link" type="button" onClick={() => navigateDiagnostic(diagnostic)}>
            {labels.affectedElement} {diagnostic.elementId}
          </button>
        )}
      </span>
      <details className="pdfv-diagnostic-technical">
        <summary>{labels.technicalDetails}</summary>
        <span>{diagnostic.message}</span>
      </details>
    </>
  ), [diagnosticText, labels.affectedElement, labels.affectedObject, labels.pageNumber, labels.technicalDetails, navigateDiagnostic]);

  const runTrustInspection = useCallback(async () => {
    if (!source) {
      return;
    }

    const controller = new AbortController();
    trustInspectionAbortRef.current?.abort();
    trustInspectionAbortRef.current = controller;
    setTrustLoading(true);
    setTrustError(null);
    try {
      const bytes = await getSourceBytes();
      if (!bytes) {
        throw new Error(labels.invalidPdf);
      }
      const file = source.file instanceof File
        ? source.file
        : new File([bytes], source.name || 'document.pdf', { type: 'application/pdf' });
      const report = await inspectPdfTrust(file, controller.signal);
      setTrustReport(report);
      setEInvoices([]);
      setEInvoiceError(null);
      if ((report.risk?.embeddedFileCount ?? 0) > 0) {
        setEInvoiceLoading(true);
        try {
          setEInvoices(await inspectEInvoices(file, controller.signal));
        } catch (error) {
          setEInvoiceError(error instanceof Error ? error.message : labels.eInvoiceFailed);
        } finally {
          setEInvoiceLoading(false);
        }
      }
      emitViewerEvent('trust:inspected', {
        status: report.status,
        signatures: report.signatures.length,
        encrypted: report.encryption.isEncrypted,
      });
    } catch (error) {
      const kind = error instanceof PdfTrustOperationError ? error.kind : 'failed';
      const message = error instanceof Error ? error.message : labels.trustFailed;
      setTrustError({ kind, message });
      emitViewerEvent('trust:inspection-failed', { message });
    } finally {
      if (trustInspectionAbortRef.current === controller) {
        trustInspectionAbortRef.current = null;
        setTrustLoading(false);
      }
    }
  }, [emitViewerEvent, getSourceBytes, labels.eInvoiceFailed, labels.invalidPdf, labels.trustFailed, source]);

  useEffect(() => {
    if (reviewPanelOpen && canInspectTrust && source && !trustReport && !trustLoading && !trustError) {
      void runTrustInspection();
    }
  }, [canInspectTrust, reviewPanelOpen, runTrustInspection, source, trustError, trustLoading, trustReport]);

  const sourceFile = useCallback(async (): Promise<File> => {
    const bytes = await getSourceBytes();
    if (!bytes || !source) throw new Error(labels.invalidPdf);
    return source.file instanceof File
      ? source.file
      : new File([bytes], source.name || 'document.pdf', { type: 'application/pdf' });
  }, [getSourceBytes, labels.invalidPdf, source]);

  const downloadEInvoice = useCallback(async (attachment: EInvoiceAttachmentSummary) => {
    try {
      const file = await sourceFile();
      const blob = await extractEInvoice(file, attachment.fileName);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      setEInvoiceError(error instanceof Error ? error.message : labels.eInvoiceFailed);
    }
  }, [labels.eInvoiceFailed, sourceFile]);

  const runCompliancePreflight = useCallback(async () => {
    setComplianceLoading(true);
    setTrustError(null);
    try {
      const report = await preflightPdfA2b(await sourceFile(), complianceProfile);
      setCompliancePreflight(report);
      setAllowSignatureInvalidation(false);
      emitViewerEvent('compliance:preflighted', { capability: report.capability });
    } catch (error) {
      const kind = error instanceof PdfTrustOperationError ? error.kind : 'failed';
      setTrustError({ kind, message: error instanceof Error ? error.message : labels.trustFailed });
    } finally {
      setComplianceLoading(false);
    }
  }, [complianceProfile, emitViewerEvent, labels.trustFailed, sourceFile]);

  const runComplianceConversion = useCallback(async () => {
    setComplianceLoading(true);
    setTrustError(null);
    try {
      const blob = await convertPdfA2b(await sourceFile(), allowSignatureInvalidation, complianceProfile);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      const profileSuffix = complianceProfile === 'PDF/A-3b' ? 'pdfa3b' : 'pdfa2b';
      link.download = (source?.name || 'document.pdf').replace(/\.pdf$/i, `-${profileSuffix}.pdf`);
      link.click();
      URL.revokeObjectURL(url);
      notify.success(`${labels.pdfAConverted} ${complianceProfile}`, { id: 'pdf-a-converted' });
      emitViewerEvent('compliance:converted', { profile: complianceProfile });
    } catch (error) {
      const kind = error instanceof PdfTrustOperationError ? error.kind : 'failed';
      setTrustError({ kind, message: error instanceof Error ? error.message : labels.trustFailed });
    } finally {
      setComplianceLoading(false);
    }
  }, [allowSignatureInvalidation, complianceProfile, emitViewerEvent, labels.trustFailed, source, sourceFile]);

  const runPdfSigning = useCallback(async () => {
    if (!signingCertificate) {
      setSigningStatus(labels.signingCertificateRequired);
      return;
    }
    setSigningBusy(true);
    setSigningStatus(null);
    try {
      const blob = await signPdf({
        pdf: await sourceFile(),
        certificate: signingCertificate,
        certificatePassword: signingPassword,
        profile: signingProfile,
        fieldName: signingFieldName,
        reason: signingReason,
        visible: signingVisible,
        pageNumber: currentPage,
      });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = (source?.name || 'document.pdf').replace(/\.pdf$/i, '.signed.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setSigningPassword('');
      setSigningCertificate(null);
      setSigningStatus(labels.signingComplete);
      emitViewerEvent('trust:signed', { profile: signingProfile, visible: signingVisible });
    } catch (error) {
      const kind = error instanceof PdfTrustOperationError ? error.kind : 'failed';
      setSigningStatus(`${trustFailureLabel(kind)}: ${error instanceof Error ? error.message : labels.trustFailed}`);
    } finally {
      setSigningBusy(false);
    }
  }, [currentPage, emitViewerEvent, labels.signingCertificateRequired, labels.signingComplete, labels.trustFailed, signingCertificate, signingFieldName, signingPassword, signingProfile, signingReason, signingVisible, source?.name, sourceFile, trustFailureLabel]);

  useEffect(() => {
    trustInspectionAbortRef.current?.abort();
    trustInspectionAbortRef.current = null;
    setTrustReport(null);
    setTrustError(null);
    setEInvoices([]);
    setEInvoiceLoading(false);
    setEInvoiceError(null);
    setCompliancePreflight(null);
    setAllowSignatureInvalidation(false);
    setAllowTrustMutation(false);
    setSigningCertificate(null);
    setSigningPassword('');
    setSigningStatus(null);
    setRedactedResult(null);
    setRedactionCompareOpen(false);
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

        const migratedValues = new Map(initialFormValues.map(field => [field.name, field.value]));
        const mergedFields = fields.map(field => {
          if (!migratedValues.has(field.name)) return field;
          const value = migratedValues.get(field.name)!;
          return {
            ...field,
            value: Array.isArray(value) ? [...value] : value,
          };
        });
        setFormFields(mergedFields);
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
  }, [emitViewerEvent, getSourceBytes, initialFormValues, labels.formLoadFailed, labels.formLoading, labels.formNone, source]);

  const buildSubsetPdfUrl = async (pages: number[]): Promise<string> => {
    const bytes = await getSourceBytes();
    if (!bytes) {
      throw new Error('No PDF source is available.');
    }

    assertPdfBytes(bytes, labels.invalidPdf);
    const sourcePdf = await PdfLibDocument.load(bytes);
    const outputPdf = await PdfLibDocument.create();
    const pageIndexes = pages.map(page => page - 1);
    const copiedPages = await outputPdf.copyPages(sourcePdf, pageIndexes);
    copiedPages.forEach(page => outputPdf.addPage(page));
    const subsetBytes = await outputPdf.save();
    const subsetBuffer = new ArrayBuffer(subsetBytes.byteLength);
    new Uint8Array(subsetBuffer).set(subsetBytes);
    const blob = createPdfBlob(subsetBuffer, labels.invalidPdf);
    return URL.createObjectURL(blob);
  };

  const downloadCurrentPdf = async () => {
    if (!source || downloadLockRef.current) {
      return;
    }
    downloadLockRef.current = true;
    setDownloadPending(true);
    setDownloadError(null);
    emitViewerEvent('download:started', { name: source.name });
    try {
      const bytes = await getSourceBytes();
      if (!bytes) throw new Error(labels.downloadFailed);
      downloadPdfBytes(bytes, source.name, labels.invalidPdf);
      emitViewerEvent('download:completed', { name: source.name });
    } catch (error) {
      const message = error instanceof Error ? error.message : labels.downloadFailed;
      setDownloadError(message);
      emitViewerEvent('download:failed', { name: source.name, message });
    } finally {
      downloadLockRef.current = false;
      setDownloadPending(false);
    }
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

  /** Builds the File the redaction endpoints expect, without mutating viewer state. */
  const buildRedactionFile = async (): Promise<{ file: File; sourceName: string } | null> => {
    if (!source || redactionAnnotations.length === 0) {
      return null;
    }

    const bytes = await getSourceBytes();
    if (!bytes) {
      throw new Error('No PDF source is available.');
    }

    const sourceName = source.name.toLowerCase().endsWith('.pdf') ? source.name : `${source.name}.pdf`;
    const file = source.file instanceof File
      ? source.file
      : new File([bytes], sourceName, { type: 'application/pdf' });
    return { file, sourceName };
  };

  /** Step 1. Reports what would be removed. Changes nothing. */
  const preflightRedaction = async () => {
    const requestFingerprint = redactionFingerprint;
    setRedactionBusy(true);
    setRedactionVerification(null);
    setRedactionAudit(null);
    setAnnotationApiStatus(labels.redactionPreflightRunning);
    try {
      const prepared = await buildRedactionFile();
      if (!prepared) {
        return;
      }

      const report = await preflightRedactions(
        prepared.file,
        redactionSidecar,
      );
      if (redactionFingerprintRef.current !== requestFingerprint) {
        return;
      }

      setRedactionPreflight(report);
      setRedactionPreflightFingerprint(requestFingerprint);
      setAnnotationApiStatus(
        isApplyAllowed(report) ? labels.redactionPreflightReady : labels.redactionPreflightBlocked,
      );
      emitViewerEvent('annotations:redaction-preflight', { capability: report.capability });
    } catch (error) {
      const message = error instanceof Error ? error.message : labels.redactionFailed;
      setRedactionPreflight(null);
      setRedactionPreflightFingerprint(null);
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:redaction-preflight-failed', { message });
    } finally {
      setRedactionBusy(false);
    }
  };

  /** Step 2 and 3. Applies, then shows what verification found. */
  const downloadRedactedPdf = async () => {
    if (!isApplyAllowed(effectiveRedactionPreflight)) {
      setAnnotationApiStatus(labels.redactionRequiresPreflight);
      return;
    }

    setRedactionBusy(true);
    setAnnotationApiStatus(labels.redactionApplying);
    const requestFingerprint = redactionFingerprint;
    try {
      const prepared = await buildRedactionFile();
      if (!prepared) {
        return;
      }

      if (redactionFingerprintRef.current !== requestFingerprint) {
        setAnnotationApiStatus(labels.redactionRequiresPreflight);
        return;
      }

      const outcome = await applyRedactions(
        prepared.file,
        redactionSidecar,
      );
      setRedactedResult(outcome.blob);
      const url = URL.createObjectURL(outcome.blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = prepared.sourceName.replace(/\.pdf$/i, '-redacted.pdf');
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      // The backend only returns a file after its own verification passed, so reaching here
      // means every residue check succeeded.
      setRedactionVerification({
        passed: true,
        residualCoveredElementCount: 0,
        residualExtractableMatches: 0,
        findings: [],
      });
      setRedactionAudit({ record: outcome.auditRecordSha256, signed: outcome.auditSigned });
      setAnnotationApiStatus(labels.redactionVerified);
      emitViewerEvent('annotations:redacted', { count: redactionAnnotations.length });
    } catch (error) {
      if (error instanceof RedactionBlockedError) {
        setRedactionPreflight(error.preflight);
        setRedactionPreflightFingerprint(redactionFingerprint);
        setRedactionVerification(error.verification);
        setAnnotationApiStatus(error.message);
        emitViewerEvent('annotations:redaction-blocked', { code: error.code });
        return;
      }

      const message = error instanceof Error ? error.message : labels.redactionFailed;
      setAnnotationApiStatus(message);
      emitViewerEvent('annotations:redaction-failed', { message });
    } finally {
      setRedactionBusy(false);
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

  const printCurrentPdf = async () => {
    if (!source || printLockRef.current) {
      return;
    }

    setPrintError(null);
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
      setPrintError(labels.printBlocked);
      emitViewerEvent('print:blocked', { name: source.name, mode: printMode });
      return;
    }
    printWindow.opener = null;
    printWindow.document.title = `${labels.print}: ${source.name}`;
    printWindow.document.body.textContent = labels.preparingPrint;
    printLockRef.current = true;
    let printScheduled = false;

    try {
      let printUrl: string;
      if (printMode === 'all') {
        const bytes = await getSourceBytes();
        if (!bytes) throw new Error(labels.printFailed);
        printUrl = URL.createObjectURL(createPdfBlob(bytes, labels.invalidPdf));
      } else {
        const pages = printMode === 'current'
          ? [currentPage]
          : parsePageRange(printRange, numPages);

        if (pages.length === 0)
          throw new Error('Enter a valid page range, for example 1-3 or 2,4,6.');
        printUrl = await buildSubsetPdfUrl(pages);
      }

      printWindow.location.replace(printUrl);
      emitViewerEvent('print:opened', { name: source.name, mode: printMode });
      setPrintDialogOpen(false);
      printScheduled = true;
      window.setTimeout(() => {
        try {
          printWindow.print();
          emitViewerEvent('print:completed', { name: source.name, mode: printMode });
        } catch (error) {
          const message = error instanceof Error ? error.message : labels.printFailed;
          setPrintError(message);
          emitViewerEvent('print:failed', { message });
        } finally {
          printLockRef.current = false;
          window.setTimeout(() => URL.revokeObjectURL(printUrl), 60_000);
        }
      }, 900);
    } catch (error) {
      printWindow.close();
      const message = error instanceof Error ? error.message : labels.printFailed;
      setPrintError(message);
      emitViewerEvent('print:failed', { message });
    } finally {
      if (!printScheduled) {
        printLockRef.current = false;
      }
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
            {canDownload && (
              <button className="pdfv-icon-button" type="button" onClick={() => void downloadCurrentPdf()} disabled={!source || downloadPending} title={downloadError ?? labels.download} aria-busy={downloadPending}>
                <FiDownload />
              </button>
            )}
            {canPrint && (
              <button className="pdfv-icon-button" type="button" onClick={() => setPrintDialogOpen(true)} disabled={!source} title={labels.print}>
                <FiPrinter />
              </button>
            )}
            {(canAnnotate || canRedact) && (
              <button className={reviewPanelOpen ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewPanelOpen(value => !value)} disabled={!source} aria-label={labels.review} title={labels.review}>
                <FiEdit3 />
                <span>{labels.review}</span>
              </button>
            )}
            {canFillForms && (
              <button className={formPanelOpen ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setFormPanelOpen(value => !value)} disabled={!source}>
                <FiFileText />
                <span>{labels.forms}</span>
              </button>
            )}
            {canInspectTrust && (
              <button className={trustPanelOpen ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setTrustPanelOpen(value => !value)} disabled={!source} aria-label={labels.trust} title={labels.trust}>
                <FiShield />
                <span>{labels.trust}</span>
              </button>
            )}
            <label className="pdfv-stamp-select">
              <span>{labels.language}</span>
              <select value={i18n.language} onChange={event => i18n.changeLanguage(event.target.value)}>
                {SUPPORTED_LANGUAGES.map(code => (
                  <option key={code} value={code}>{code.toUpperCase()}</option>
                ))}
              </select>
            </label>
            {downloadError && <span className="pdfv-action-error" role="alert">{downloadError}</span>}
          </div>
        </section>

        {trustPanelOpen && (
          <section className="pdfv-trust-panel" aria-label={labels.trust}>
            <div className="pdfv-trust-header">
              <div>
                <strong>{labels.trust}</strong>
                <span>{trustReport ? `SHA-256 ${trustReport.sha256.slice(0, 16)}...` : labels.trustEmpty}</span>
              </div>
              <button className="pdfv-button" type="button" onClick={() => void runTrustInspection()} disabled={!source || trustLoading} aria-label={trustLoading ? labels.trustLoading : labels.inspectTrust}>
                <FiShield />
                <span>{trustLoading ? labels.trustLoading : labels.inspectTrust}</span>
              </button>
            </div>
            {trustLoading && (
              <div className="pdfv-trust-operation" role="status" aria-live="polite">
                <span>{labels.trustLoading}</span>
                <button className="pdfv-button" type="button" onClick={() => trustInspectionAbortRef.current?.abort()}>
                  {labels.trustCancel}
                </button>
              </div>
            )}
            {trustError && (
              <div className="pdfv-trust-error" role="alert" data-failure={trustError.kind}>
                <strong>{trustFailureLabel(trustError.kind)}</strong>
                <span>{trustError.message}</span>
                {trustError.kind !== 'cancelled' && (
                  <button className="pdfv-button" type="button" onClick={() => void runTrustInspection()}>
                    {labels.trustRetry}
                  </button>
                )}
              </div>
            )}
            {canSign && (
              <form
                className="pdfv-signing-panel"
                onSubmit={event => {
                  event.preventDefault();
                  void runPdfSigning();
                }}
              >
                <strong>{labels.signPdf}</strong>
                <label>
                  <span>{labels.signingCertificate}</span>
                  <input
                    key={signingCertificate?.name ?? 'empty-certificate'}
                    type="file"
                    accept=".p12,.pfx,application/x-pkcs12"
                    onChange={event => setSigningCertificate(event.target.files?.[0] ?? null)}
                    disabled={signingBusy}
                  />
                </label>
                <label>
                  <span>{labels.signingPassword}</span>
                  <input type="password" value={signingPassword} onChange={event => setSigningPassword(event.target.value)} autoComplete="off" disabled={signingBusy} />
                </label>
                <label>
                  <span>{labels.signingProfile}</span>
                  <select value={signingProfile} onChange={event => setSigningProfile(event.target.value as PdfPadesProfile)} disabled={signingBusy}>
                    <option value="BaselineB">PAdES B-B</option>
                    <option value="BaselineT">PAdES B-T</option>
                    <option value="BaselineLT">PAdES B-LT</option>
                    <option value="BaselineLTA">PAdES B-LTA</option>
                  </select>
                </label>
                <label>
                  <span>{labels.signingFieldName}</span>
                  <input value={signingFieldName} maxLength={128} onChange={event => setSigningFieldName(event.target.value)} disabled={signingBusy} />
                </label>
                <label>
                  <span>{labels.signingReason}</span>
                  <input value={signingReason} maxLength={256} onChange={event => setSigningReason(event.target.value)} disabled={signingBusy} />
                </label>
                <label className="pdfv-checkbox">
                  <input type="checkbox" checked={signingVisible} onChange={event => setSigningVisible(event.target.checked)} disabled={signingBusy} />
                  <span>{labels.signingVisible}</span>
                </label>
                <button className="pdfv-button" type="submit" disabled={!source || !signingCertificate || signingBusy} aria-label={signingBusy ? labels.signingInProgress : labels.signPdf}>
                  <FiPenTool />
                  <span>{signingBusy ? labels.signingInProgress : labels.signPdf}</span>
                </button>
                <small>{labels.signingPrivacy}</small>
                {signingStatus && <span className="pdfv-api-status" role="status">{signingStatus}</span>}
              </form>
            )}
            {canConvertCompliance && (
              <div className="pdfv-compliance-actions">
                <label>
                  <span>{labels.pdfAProfile}</span>
                  <select
                    value={complianceProfile}
                    onChange={event => {
                      setComplianceProfile(event.target.value as PdfArchiveProfile);
                      setCompliancePreflight(null);
                      setAllowSignatureInvalidation(false);
                    }}
                    disabled={complianceLoading}
                  >
                    <option value="PDF/A-2b">PDF/A-2b</option>
                    <option value="PDF/A-3b">PDF/A-3b</option>
                  </select>
                </label>
                <button className="pdfv-button" type="button" onClick={() => void runCompliancePreflight()} disabled={!source || complianceLoading} aria-label={labels.pdfAPreflight}>
                  <FiShield />
                  <span>{labels.pdfAPreflight}</span>
                </button>
                {compliancePreflight && (
                  <>
                    <span data-status={compliancePreflight.capability === 0 ? 0 : compliancePreflight.capability === 1 ? 2 : 1}>
                      {compliancePreflight.capability === 0 ? labels.pdfAReady : compliancePreflight.capability === 1 ? labels.pdfAReviewRequired : labels.pdfAUnsupported}
                    </span>
                    {compliancePreflight.invalidatesSignatures && (
                      <label>
                        <input type="checkbox" checked={allowSignatureInvalidation} onChange={event => setAllowSignatureInvalidation(event.target.checked)} />
                        {labels.pdfAAllowSignatureInvalidation}
                      </label>
                    )}
                    <button
                      className="pdfv-button"
                      type="button"
                      onClick={() => void runComplianceConversion()}
                      disabled={complianceLoading || compliancePreflight.capability === 2 || (compliancePreflight.invalidatesSignatures && !allowSignatureInvalidation)}
                      aria-label={complianceLoading ? labels.pdfAConverting : labels.pdfAConvert}
                    >
                      <FiDownload />
                      <span>{complianceLoading ? labels.pdfAConverting : labels.pdfAConvert}</span>
                    </button>
                    <ul className="pdfv-trust-diagnostics">
                      {compliancePreflight.findings.map((finding, index) => (
                        <li key={`${finding.code}-${index}`} data-severity={finding.severity}>
                          {renderDiagnostic(finding)}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>
            )}
            {trustReport && (
              <div className="pdfv-trust-content">
                <dl className="pdfv-trust-grid">
                  <div><dt>{labels.structure}</dt><dd>{renderTrustStatus(trustReport.structureStatus)}</dd></div>
                  <div><dt>{labels.encryption}</dt><dd>{trustReport.encryption.isEncrypted ? <>{trustReport.encryption.algorithm ?? labels.encryption} {renderTrustStatus(trustReport.encryption.status)}</> : renderTrustStatus(4)}</dd></div>
                  <div><dt>{labels.signatures}</dt><dd>{trustReport.signatures.length || labels.notDetected}</dd></div>
                  <div><dt>PDF/A</dt><dd>{trustReport.pdfA.isClaimed ? <>{trustReport.pdfA.profile} {renderTrustStatus(trustReport.pdfA.status)}</> : renderTrustStatus(4)}</dd></div>
                  {trustReport.pdfA.isClaimed && trustReport.pdfA.validator && (
                    <div><dt>PDF/A validator</dt><dd title={trustReport.pdfA.evidence}>{trustReport.pdfA.validator}</dd></div>
                  )}
                  <div><dt>PDF/UA</dt><dd>{trustReport.pdfUa.isClaimed ? <>{trustReport.pdfUa.profile} {renderTrustStatus(trustReport.pdfUa.status)}</> : renderTrustStatus(4)}</dd></div>
                  <div><dt>{labels.revisions}</dt><dd>{trustReport.revisionCount}</dd></div>
                  <div><dt>{labels.objects}</dt><dd>{trustReport.objectCount}</dd></div>
                  <div><dt>PDF</dt><dd>{trustReport.pdfVersion ?? labels.notDetected}</dd></div>
                  {trustReport.embeddedValidationMaterial && (
                    <div>
                      <dt>{labels.embeddedValidationMaterial}</dt>
                      <dd>
                        {labels.certificates}: {trustReport.embeddedValidationMaterial.certificateCount} · {labels.crls}: {trustReport.embeddedValidationMaterial.crlCount} · {labels.ocspResponses}: {trustReport.embeddedValidationMaterial.ocspResponseCount} · {labels.vriEntries}: {trustReport.embeddedValidationMaterial.vriEntryCount}
                        {' '}{renderTrustStatus(trustReport.embeddedValidationMaterial.status)}
                      </dd>
                    </div>
                  )}
                  {trustReport.risk && (
                    <div>
                      <dt>{labels.riskyContent}</dt>
                      <dd>{renderTrustStatus(trustReport.risk.status)}</dd>
                    </div>
                  )}
                </dl>
                {trustReport.risk && (
                  <dl className="pdfv-trust-grid" aria-label={labels.riskyContent}>
                    <div><dt>{labels.javaScriptActions}</dt><dd>{trustReport.risk.javaScriptActionCount}</dd></div>
                    <div><dt>{labels.launchActions}</dt><dd>{trustReport.risk.launchActionCount}</dd></div>
                    <div><dt>{labels.embeddedFiles}</dt><dd>{trustReport.risk.embeddedFileCount}</dd></div>
                    <div><dt>{labels.fileAttachments}</dt><dd>{trustReport.risk.fileAttachmentCount}</dd></div>
                    <div><dt>{labels.richMedia}</dt><dd>{trustReport.risk.richMediaCount}</dd></div>
                    <div><dt>{labels.xfaForms}</dt><dd>{trustReport.risk.xfaFormCount}</dd></div>
                    <div><dt>{labels.externalLinks}</dt><dd>{trustReport.risk.externalLinkCount}</dd></div>
                  </dl>
                )}
                {(eInvoiceLoading || eInvoiceError || eInvoices.some(item => item.isXml)) && (
                  <section className="pdfv-signature-list" aria-label={labels.eInvoices}>
                    <header><strong>{labels.eInvoices}</strong></header>
                    {eInvoiceLoading && <p role="status">{labels.eInvoiceInspecting}</p>}
                    {eInvoiceError && <p role="alert" className="pdfv-trust-error">{eInvoiceError}</p>}
                    {eInvoices.filter(item => item.isXml).map(attachment => (
                      <section key={attachment.fileName} className="pdfv-signature-item">
                        <header>
                          <strong>{attachment.fileName}</strong>
                          <span>{labels.eInvoiceConformance[attachment.conformanceStatus]}</span>
                        </header>
                        <dl>
                          <div><dt>{labels.eInvoiceStandard}</dt><dd>{attachment.standard ?? labels.notDetected}</dd></div>
                          <div><dt>{labels.eInvoiceProfile}</dt><dd>{attachment.profile ?? labels.notDetected}</dd></div>
                          <div><dt>{labels.eInvoiceSyntax}</dt><dd>{attachment.syntax ?? labels.notDetected}</dd></div>
                          <div><dt>{labels.eInvoiceValidation}</dt><dd>{attachment.isStructurallyValid ? labels.eInvoiceStructuralValid : labels.eInvoiceStructuralInvalid} · {labels.eInvoiceConformance[attachment.conformanceStatus]}</dd></div>
                          {attachment.semanticData && <>
                            <div><dt>{labels.eInvoiceNumber}</dt><dd>{attachment.semanticData.invoiceNumber ?? labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoiceSeller}</dt><dd>{attachment.semanticData.seller.name ?? labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoiceBuyer}</dt><dd>{attachment.semanticData.buyer.name ?? labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoiceCurrency}</dt><dd>{attachment.semanticData.currency ?? labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoicePayable}</dt><dd>{attachment.semanticData.payableAmount != null ? `${attachment.semanticData.payableAmount.toFixed(2)} ${attachment.semanticData.currency ?? ''}`.trim() : labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoiceTax}</dt><dd>{attachment.semanticData.taxTotal != null ? `${attachment.semanticData.taxTotal.toFixed(2)} ${attachment.semanticData.currency ?? ''}`.trim() : labels.notDetected}</dd></div>
                            <div><dt>{labels.eInvoiceLines}</dt><dd>{attachment.semanticData.lines.length}</dd></div>
                          </>}
                          <div><dt>{labels.eInvoicePdfConsistency}</dt><dd>{labels.eInvoiceConsistency[attachment.pdfConsistency.status]}</dd></div>
                        </dl>
                        {attachment.pdfConsistency.status === 'reviewRequired' && (
                          <ul className="pdfv-trust-diagnostics">
                            {attachment.pdfConsistency.findings.filter(finding => finding.status === 'notFound').map(finding => (
                              <li key={`${attachment.fileName}-${finding.field}`} data-severity="warning">
                                <code>{finding.field}</code> {finding.message}
                              </li>
                            ))}
                          </ul>
                        )}
                        {attachment.diagnostics.length > 0 && (
                          <ul className="pdfv-trust-diagnostics">
                            {attachment.diagnostics.map((diagnostic, index) => (
                              <li key={`${attachment.fileName}-${diagnostic.code}-${index}`} data-severity={diagnostic.severity}>
                                <code title={diagnostic.code}>{diagnostic.canonicalCode ?? diagnostic.code}</code> {diagnostic.message}
                              </li>
                            ))}
                          </ul>
                        )}
                        <button className="pdfv-button" type="button" onClick={() => void downloadEInvoice(attachment)} disabled={!attachment.sizeMatches || !attachment.checksumMatches}>
                          <FiDownload aria-hidden="true" />
                          <span>{labels.eInvoiceDownloadXml}</span>
                        </button>
                      </section>
                    ))}
                  </section>
                )}
                {trustReport.signatures.length > 0 && (
                  <div className="pdfv-signature-list">
                    {trustReport.signatures.map((signature, index) => (
                      <section key={`${signature.objectNumber}-${signature.generation}`} className="pdfv-signature-item">
                        <header>
                          <strong>{signature.isDocumentTimestamp ? labels.documentTimestamp : labels.signature} {index + 1}</strong>
                          {renderTrustStatus(signature.status)}
                        </header>
                        <dl>
                          <div><dt>{labels.integrity}</dt><dd>{renderTrustStatus(signature.integrityStatus)}</dd></div>
                          <div><dt>{labels.coverage}</dt><dd>{signature.coversWholeDocument ? labels.wholeDocument : labels.earlierRevision} {renderTrustStatus(signature.coverageStatus)}</dd></div>
                          <div><dt>{labels.certificateTrust}</dt><dd>{renderTrustStatus(signature.certificateStatus)}</dd></div>
                          <div><dt>{labels.revocation}</dt><dd>{renderTrustStatus(signature.revocationStatus)}</dd></div>
                          <div><dt>{labels.historicalRevocation}</dt><dd>{renderTrustStatus(signature.historicalRevocationStatus)}</dd></div>
                          <div><dt>{labels.signatureObject}</dt><dd>#{signature.objectNumber} {signature.generation} obj</dd></div>
                          {signature.fieldName && <div><dt>{labels.signatureField}</dt><dd>{signature.fieldName}</dd></div>}
                          {signature.fieldPageNumber != null && (
                            <div>
                              <dt>{labels.signatureFieldLocation}</dt>
                              <dd>
                                <button className="pdfv-inline-link" type="button" onClick={() => goToPage(signature.fieldPageNumber!)}>
                                  {labels.pageNumber} {signature.fieldPageNumber}
                                </button>
                                {signature.fieldRectangle?.length === 4 ? ` · [${signature.fieldRectangle.map(value => Number(value.toFixed(2))).join(', ')}]` : ''}
                              </dd>
                            </div>
                          )}
                          <div><dt>{labels.signedRevision}</dt><dd>{trustReport.revisions.find(revision => revision.endOffset === signature.signedRevisionEndOffset)?.sequenceNumber ?? labels.notDetected}</dd></div>
                          {signature.signedRevisionEndOffset != null && <div><dt>{labels.signedRevisionOffset}</dt><dd>{signature.signedRevisionEndOffset}</dd></div>}
                          <div><dt>{labels.modificationsAfterSigning}</dt><dd>{signature.coversWholeDocument ? labels.noModifications : labels.laterModifications}</dd></div>
                          {signature.revocationEvidence?.map((evidence, evidenceIndex) => (
                            <div key={`signer-evidence-${evidenceIndex}`}>
                              <dt>{labels.revocationEvidence}</dt>
                              <dd title={evidence.source}>
                                {evidence.kind === 0 ? 'CRL' : 'OCSP'} {renderTrustStatus(evidence.status)}
                                {evidence.nextUpdate ? ` · ${labels.validUntil} ${new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(evidence.nextUpdate))}` : ''}
                              </dd>
                            </div>
                          ))}
                          <div><dt>PAdES</dt><dd>{signature.padesProfile ?? labels.notDetected}</dd></div>
                          {signature.embeddedValidationMaterial && (
                            <div>
                              <dt>{labels.vriMatched}</dt>
                              <dd>
                                {labels.certificates}: {signature.embeddedValidationMaterial.certificateCount} · {labels.crls}: {signature.embeddedValidationMaterial.crlCount} · {labels.ocspResponses}: {signature.embeddedValidationMaterial.ocspResponseCount}
                                {' '}{renderTrustStatus(signature.embeddedValidationMaterial.status)}
                              </dd>
                            </div>
                          )}
                          <div><dt>Digest</dt><dd>{signature.digestAlgorithm ?? labels.notDetected}</dd></div>
                          <div><dt>{labels.signer}</dt><dd title={signature.signerSubject ?? undefined}>{signature.signerSubject ?? labels.notDetected}</dd></div>
                          {signature.signerThumbprint && <div><dt>{labels.certificateThumbprint}</dt><dd title={signature.signerThumbprint}>{signature.signerThumbprint}</dd></div>}
                          {signature.signingTime && <div><dt>{labels.signingTime}</dt><dd>{new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(signature.signingTime))}</dd></div>}
                          <div><dt>{labels.timestamp}</dt><dd>{renderTrustStatus(signature.timestampStatus)}</dd></div>
                          <div><dt>{labels.timestampRevocation}</dt><dd>{renderTrustStatus(signature.timestampRevocationStatus)}</dd></div>
                          <div><dt>{labels.timestampHistoricalRevocation}</dt><dd>{renderTrustStatus(signature.timestampHistoricalRevocationStatus)}</dd></div>
                          {signature.isCertification && <div><dt>{labels.certification}</dt><dd>{renderTrustStatus(signature.certificationStatus ?? 2)}</dd></div>}
                          {signature.certification?.lockedFields.length ? <div><dt>{labels.lockedFields}</dt><dd title={signature.certification.lockedFields.join(', ')}>{signature.certification.lockedFields.join(', ')}</dd></div> : null}
                          {signature.timestampRevocationEvidence?.map((evidence, evidenceIndex) => (
                            <div key={`timestamp-evidence-${evidenceIndex}`}>
                              <dt>{labels.revocationEvidence}</dt>
                              <dd title={evidence.source}>
                                {evidence.kind === 0 ? 'CRL' : 'OCSP'} {renderTrustStatus(evidence.status)}
                                {evidence.nextUpdate ? ` · ${labels.validUntil} ${new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(evidence.nextUpdate))}` : ''}
                              </dd>
                            </div>
                          ))}
                          {signature.timestampTime && (
                            <div><dt>{labels.timestampTime}</dt><dd>{new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(signature.timestampTime))}</dd></div>
                          )}
                          {signature.timestampAuthoritySubject && (
                            <div><dt>{labels.timestampAuthority}</dt><dd title={signature.timestampAuthoritySubject}>{signature.timestampAuthoritySubject}</dd></div>
                          )}
                          {!signature.isDocumentTimestamp && signature.archiveTimestampStatus !== 4 && (
                            <div><dt>{labels.archiveTimestamp}</dt><dd>{renderTrustStatus(signature.archiveTimestampStatus)}</dd></div>
                          )}
                          {signature.archiveTimestampTime && (
                            <div><dt>{labels.archiveTimestampTime}</dt><dd>{new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(signature.archiveTimestampTime))}</dd></div>
                          )}
                          {signature.archiveTimestampAuthoritySubject && (
                            <div><dt>{labels.archiveTimestampAuthority}</dt><dd title={signature.archiveTimestampAuthoritySubject}>{signature.archiveTimestampAuthoritySubject}</dd></div>
                          )}
                        </dl>
                      </section>
                    ))}
                  </div>
                )}
                <ul className="pdfv-trust-diagnostics">
                  {trustReport.diagnostics.map((diagnostic, index) => (
                    <li key={`${diagnostic.code}-${index}`} data-severity={diagnostic.severity}>
                      {renderDiagnostic(diagnostic)}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
        )}

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

        {canPrint && printDialogOpen && (
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

        {canFillForms && formPanelOpen && (
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

        {(canAnnotate || canRedact) && reviewPanelOpen && (
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
              {canRedact && <button className={reviewTool === 'redaction' ? 'pdfv-button is-active' : 'pdfv-button'} type="button" onClick={() => setReviewTool('redaction')} aria-label={labels.redact}>
                <FiEyeOff />
                <span>{labels.redact}</span>
              </button>}
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
            {selectedAnnotation && (selectedAnnotation.type === 'redaction' ? canRedact : canAnnotate) && (
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
            {requiresTrustMutationConfirmation && (
              <div className="pdfv-trust-mutation-warning" role="note">
                <FiShield aria-hidden="true" />
                <div>
                  <strong>{labels.mutationWarningTitle}</strong>
                  <span>{labels.mutationWarningBody}</span>
                </div>
                <label>
                  <input
                    type="checkbox"
                    checked={allowTrustMutation}
                    onChange={event => setAllowTrustMutation(event.target.checked)}
                  />
                  {labels.mutationConfirmation}
                </label>
              </div>
            )}
            {canInspectTrust && !trustReport && (
              <div className="pdfv-trust-mutation-warning" role={trustError ? 'alert' : 'status'}>
                <FiShield aria-hidden="true" />
                <div>
                  <strong>{labels.inspectTrust}</strong>
                  <span>{trustError ? `${trustFailureLabel(trustError.kind)}: ${trustError.message}` : labels.trustInspectionRequired}</span>
                </div>
                {!trustLoading && (
                  <button className="pdfv-button" type="button" onClick={() => void runTrustInspection()}>
                    {trustError ? labels.trustRetry : labels.inspectTrust}
                  </button>
                )}
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
              <button className="pdfv-button" type="button" onClick={() => void downloadFlattenedPdf()} disabled={!source || annotations.length === 0 || !trustMutationAllowed}>
                <FiFileText />
                <span>{labels.flattenPdf}</span>
              </button>
              <button className="pdfv-button" type="button" onClick={() => void downloadEmbeddedAnnotationsPdf()} disabled={!source || annotations.length === 0 || !trustMutationAllowed}>
                <FiFileText />
                <span>{labels.embedAnnotations}</span>
              </button>
              {canRedact && <button
                className="pdfv-button"
                type="button"
                onClick={() => void preflightRedaction()}
                disabled={!source || redactionAnnotations.length === 0 || redactionBusy}
                aria-label={labels.redactionPreflight}
              >
                <FiEyeOff />
                <span>{labels.redactionPreflight}</span>
              </button>}
              {canRedact && <button
                className="pdfv-button"
                type="button"
                onClick={() => void downloadRedactedPdf()}
                disabled={!source || redactionAnnotations.length === 0 || redactionBusy || !isApplyAllowed(effectiveRedactionPreflight) || !trustMutationAllowed}
                title={isApplyAllowed(effectiveRedactionPreflight) ? undefined : labels.redactionRequiresPreflight}
                aria-label={labels.applyRedactions}
              >
                <FiEyeOff />
                <span>{labels.applyRedactions}</span>
              </button>}
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
              {canRedact && effectiveRedactionPreflight && (
                <div className="pdfv-redaction-report" data-testid="redaction-report">
                  <p className="pdfv-redaction-step">
                    {labels.redactionStepPreflight}: {effectiveRedactionPreflight.capability}
                  </p>
                  {allFindings(effectiveRedactionPreflight).length > 0 && (
                    <>
                      <p className="pdfv-redaction-step">{labels.redactionFindings}</p>
                      <ul className="pdfv-redaction-findings">
                        {allFindings(effectiveRedactionPreflight).map((finding, index) => (
                          <li key={`${finding.code}-${index}`} data-code={finding.code}>
                            <code title={finding.code}>{finding.canonicalCode ?? finding.code}</code>
                            <span>{finding.message}</span>
                            {finding.pageNumber != null && (
                              <button
                                className="pdfv-button"
                                type="button"
                                onClick={() => goToPage(finding.pageNumber!)}
                              >
                                {labels.pageNumber} {finding.pageNumber}
                              </button>
                            )}
                          </li>
                        ))}
                      </ul>
                    </>
                  )}
                  {redactionVerification && (
                    <p className="pdfv-redaction-step">
                      {labels.redactionStepVerify}: {redactionVerification.passed ? '✓' : '✗'}
                    </p>
                  )}
                  {redactionAudit && (
                    <p className="pdfv-redaction-step" data-testid="redaction-audit">
                      {redactionAudit.signed ? labels.redactionAuditSigned : labels.redactionAuditUnsigned}
                      {redactionAudit.record ? ` (${redactionAudit.record.slice(0, 16)}…)` : ''}
                    </p>
                  )}
                  {redactedResult && redactionVerification?.passed && (
                    <button className="pdfv-button" type="button" onClick={() => setRedactionCompareOpen(true)} aria-label={labels.redactionCompare}>
                      <FiColumns />
                      <span>{labels.redactionCompare}</span>
                    </button>
                  )}
                </div>
              )}
            </div>
          </section>
        )}

        {redactionCompareOpen && source && redactedResult && (
          <section className="pdfv-redaction-compare" role="dialog" aria-modal="true" aria-label={labels.redactionCompareTitle}>
            <header>
              <strong>{labels.redactionCompareTitle}</strong>
              <button className="pdfv-icon-button" type="button" onClick={() => setRedactionCompareOpen(false)} title={labels.close} aria-label={labels.close}>
                <FiMinus />
              </button>
            </header>
            <div className="pdfv-redaction-compare-grid">
              <figure>
                <figcaption>{labels.redactionOriginal}</figcaption>
                <Document file={source.file} loading={labels.loadingPdf}>
                  <Page pageNumber={currentPage} width={420} renderAnnotationLayer={false} renderTextLayer={false} />
                </Document>
              </figure>
              <figure>
                <figcaption>{labels.redactionResult}</figcaption>
                <Document file={redactedResult} loading={labels.loadingPdf}>
                  <Page pageNumber={currentPage} width={420} renderAnnotationLayer={false} renderTextLayer={false} />
                </Document>
              </figure>
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
                          {currentPageInkAnnotations.flatMap(annotation => {
                            const lines = annotation.pointLines?.length ? annotation.pointLines : [annotation.points ?? []];
                            return lines.map((points, lineIndex) => (
                            <polyline
                              key={`${annotation.id}-${lineIndex}`}
                              className={[
                                selectedAnnotationId === annotation.id ? 'is-selected' : '',
                                reviewTool === 'inkEraser' && !annotation.locked ? 'is-erasable' : '',
                                annotation.locked ? 'is-locked' : '',
                              ].filter(Boolean).join(' ')}
                              points={points.map(point => `${point.xPct},${point.yPct}`).join(' ')}
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
                            ));
                          })}
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
