/**
 * Every element type the designer can place. This runtime array is the authoritative list — the
 * `ElementType` union is derived from it, and the documentation element catalog is drift-guarded
 * against it (see src/docs/elementCatalog.ts and its test).
 */
export const ELEMENT_TYPES = [
  'text',
  'image',
  'shape',
  'table',
  'line',
  'qrcode',
  'barcode',
  'signature',
  'richtext',
  'field',
  'textarea',
  'checkbox',
  'rect',
  'circle',
  'chart',
  'subsection',
  'area',
  'button',
  'dropdown',
  'optionlist',
  'radio',
  'watermark',
  'note',
  'arrow',
  'draw',
  'date',
  'highlight',
  'checkmark',
  'pageboundary',
  'pagenumber',
  'link',
  'number',
  'toc',
  // Word-only elements
  'footnote',
  'endnote',
  'bookmark',
  'comment',
  'contentcontrol',
] as const;

export type ElementType = (typeof ELEMENT_TYPES)[number];

export interface CellBorderSide {
  color?: string;
  width?: number;
}

/** Per-cell table styling. Sparse: addressed by row/col; unset properties fall back to table defaults. */
export interface CellStyle {
  row: number;
  col: number;
  backgroundColor?: string;
  textAlign?: 'left' | 'center' | 'right';
  borderColor?: string;
  borderWidth?: number;
  borderTop?: CellBorderSide;
  borderRight?: CellBorderSide;
  borderBottom?: CellBorderSide;
  borderLeft?: CellBorderSide;
  padding?: number;
  fontFamily?: string;
  fontSize?: number;
  bold?: boolean;
  italic?: boolean;
  color?: string;
}

export interface SimpleElement {
  id: string;
  type: ElementType;
  name?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  content?: string;
  style?: Record<string, any>;
  // Data binding (template engine)
  binding?: string;
  expression?: string;
  formatter?: string;
  repeat?: { dataPath: string; templateId: string };
  // QR code
  qrValue?: string;
  qrSize?: number;
  // Barcode
  barcodeValue?: string;
  barcodeType?: string;
  // Signature
  signatureLabel?: string;
  // Rich text
  htmlContent?: string;
  // Form fields
  fieldLabel?: string;
  fieldName?: string;
  placeholder?: string;
  required?: boolean;
  // Chart
  chartType?: 'bar' | 'line' | 'pie';
  chartData?: Record<string, any>;
  // Select / list / radio
  options?: string[];
  selectedValue?: string;
  multiSelect?: boolean;
  ordered?: boolean;
  listStyle?: string;
  // Link
  href?: string;
  linkTarget?: '_blank' | '_self';
  // Button action
  buttonAction?: string;
  // Number
  numberValue?: number;
  numberStyle?: 'decimal' | 'currency' | 'percent' | 'scientific' | 'ordinal';
  numberDecimals?: number;
  numberCurrency?: string;
  numberLocale?: string;
  // Image
  fitMode?: 'contain' | 'cover' | 'fill' | 'none';
  cropX?: number;
  cropY?: number;
  cropWidth?: number;
  cropHeight?: number;
  focalX?: number;
  focalY?: number;
  preserveAspectRatio?: boolean;
  // Advanced document elements
  watermarkMode?: 'text' | 'image';
  pageScope?: 'current' | 'all' | 'first' | 'last' | 'range' | 'odd' | 'even';
  pageRange?: string;
  visibleExpression?: string;
  locked?: boolean;
  hidden?: boolean;
  noteTitle?: string;
  noteBody?: string;
  noteAuthor?: string;
  noteCollapsed?: boolean;
  arrowMode?: 'straight' | 'elbow' | 'curved';
  arrowDirection?: 'right' | 'left' | 'up' | 'down';
  arrowRotation?: number;
  startMarker?: 'none' | 'filled' | 'open' | 'dot' | 'diamond' | 'square' | 'circle' | 'arrow';
  endMarker?: 'none' | 'filled' | 'open' | 'dot' | 'diamond' | 'square' | 'circle' | 'arrow';
  drawTool?: 'pen' | 'highlighter' | 'eraser';
  pathData?: string;
  language?: string;          // BCP-47 tag: "ar", "zh", "en", "he", etc.
  textDirection?: 'ltr' | 'rtl';
  elementLanguage?: string;   // undefined = visible in all language tabs; set to BCP-47 tag = own element for that language only
  elementGroup?: string;      // shared ID between an element and its language mirrors — used to find/delete all siblings
  langOverrides?: Record<string, { x?: number; y?: number; width?: number; height?: number; rotation?: number }>; // per-language position/rotation overrides
  dateMode?: 'static' | 'render' | 'binding';
  dateFormat?: string;
  locale?: string;
  timezone?: string;
  fallbackText?: string;
  markMode?: 'rectangle' | 'text';
  checkState?: 'checked' | 'cross' | 'dot' | 'empty';
  pageBoundaryMode?: 'start' | 'end';
  numberingFormat?: 'current' | 'total' | 'pageOfTotal' | 'roman' | 'alphabetic';
  startNumber?: number;
  prefix?: string;
  suffix?: string;
  // Table
  headerRow?: boolean;
  footerRow?: boolean;
  headerBgColor?: string;
  zebraEnabled?: boolean;
  zebraColor?: string;
  columnWidths?: number[];
  cellData?: string[][];
  columnAlignments?: ('left' | 'center' | 'right')[];
  cellStyles?: CellStyle[];
  // Named style reference
  styleName?: string;
  characterStyle?: string;
  // Footnote / endnote
  footnoteText?: string;
  footnoteRef?: string;
  // Bookmark
  bookmarkName?: string;
  bookmarkTarget?: string;
  // Word-native comment
  commentAuthor?: string;
  commentDate?: string;
  commentText?: string;
  commentId?: string;
  // Content control
  contentControlType?: 'richText' | 'plainText' | 'datePicker' | 'comboBox' | 'picture';
  contentControlTag?: string;
  contentControlTitle?: string;
  contentControlPlaceholder?: string;
  // Track changes revision
  revisionType?: 'insert' | 'delete' | 'format';
  revisionAuthor?: string;
  revisionDate?: string;
  revisionId?: string;
  // Auto-hyphenation
  autoHyphenation?: boolean;
  // Heading level (for TOC generation)
  headingLevel?: 1 | 2 | 3 | null;
  // Form accessibility / ordering
  tabIndex?: number;
  // Per-field validation
  validationMin?: number;
  validationMax?: number;
  validationPattern?: string;
  // Table of contents element config
  tocEntries?: Array<{ text: string; level: 1 | 2 | 3; page: number }>;
  tocTitle?: string;
  tocShowPageNumbers?: boolean;
  tocShowLeaderDots?: boolean;
  tocMinLevel?: 1 | 2 | 3;
  tocMaxLevel?: 1 | 2 | 3;
}

export interface Page {
  id: string;
  elements: SimpleElement[];
}

export interface Template {
  id: string;
  name: string;
  category: string;
  thumbnail?: string;
  description: string;
  data?: Record<string, any>;
}

export type LayerDirection = 'front' | 'forward' | 'backward' | 'back';

// ── Named Style System ───────────────────────────────────────────────────────

export type StyleType = 'paragraph' | 'character' | 'list' | 'table';

export interface NamedStyle {
  id: string;
  name: string;
  type: StyleType;
  basedOn?: string;
  nextStyle?: string;
  style: Record<string, any>;
}

// ── Document Protection ──────────────────────────────────────────────────────

export interface DocumentProtection {
  enabled: boolean;
  mode: 'readOnly' | 'comments' | 'trackedChanges' | 'formFields';
  passwordHash?: string;
}

// ── PDF Encryption (PXA-compatible Standard Security Handler) ────────────────

export interface PdfEncryptionPermissions {
  print: boolean;
  modify: boolean;
  copy: boolean;
  annotate: boolean;
  fillForms: boolean;
  extractAccessibility: boolean;
  assemble: boolean;
  printHighResolution: boolean;
}

export interface PdfEncryption {
  enabled: boolean;
  userPassword: string;   // open-document password (empty = opens without a prompt)
  ownerPassword: string;  // permissions password (empty = uses the user password)
  algorithm: 'Rc4_128' | 'Aes128';
  permissions: PdfEncryptionPermissions;
}

// ── Custom Document Properties ───────────────────────────────────────────────

export interface CustomDocumentProperty {
  name: string;
  value: string;
  type: 'text' | 'number' | 'boolean' | 'date';
}

// ── Localized Properties ─────────────────────────────────────────────────────

export interface LocalizedProperty {
  key: string;           // template variable name without {{ }}, e.g. "SUBJECT"
  scope: 'global' | 'own';
  // 'global': placeholder appears in ALL language PDFs; each language fills its own value via localizedValues
  // 'own':    placeholder exists ONLY in the language identified by ownerLanguage
  ownerLanguage?: string;                   // set only when scope === 'own'
  localizedValues: Record<string, string>;  // { de: "Hallo Welt", ar: "مرحبا" }
}

export interface PageSettings {
  width: number;
  height: number;
  orientation: 'portrait' | 'landscape';
  backgroundColor: string;
  backgroundImage: string;
  backgroundImageFit: 'contain' | 'cover' | 'fill' | 'tile';
  margins: { top: number; right: number; bottom: number; left: number };
  headerEnabled: boolean;
  headerHeight: number;
  headerFirstPageDifferent: boolean;
  headerOddEvenDifferent: boolean;
  footerEnabled: boolean;
  footerHeight: number;
  footerFirstPageDifferent: boolean;
  footerOddEvenDifferent: boolean;
  bleedSize: number;
  gridVisible: boolean;
  snapToGrid: boolean;
  gridSize: number;
  unit: 'px' | 'pt' | 'mm' | 'cm' | 'in';
  showMarginGuide: boolean;
  showSafeArea: boolean;
  pagination: {
    autoBreaks: boolean;
    repeatTableHeader: boolean;
    keepWithNext: boolean;
    sectionStartBehavior: 'continue' | 'new-page' | 'odd-page' | 'even-page';
    orphanLines: number;
    widowLines: number;
  };
  metadata: { title: string; author: string; subject: string; keywords: string };
  pageNumbering: {
    enabled: boolean;
    format: 'current' | 'total' | 'pageOfTotal' | 'roman' | 'alphabetic';
    startNumber: number;
    prefix: string;
    suffix: string;
    showOnFirstPage: boolean;
    placement: 'none' | 'top-left' | 'top-center' | 'top-right' | 'bottom-left' | 'bottom-center' | 'bottom-right';
  };
  globalWatermark: {
    enabled: boolean;
    mode: 'text' | 'image';
    content: string;
    opacity: number;
    rotation: number;
    scale: number;
    pageScope: 'all' | 'first' | 'last' | 'range' | 'odd' | 'even';
    pageRange: string;
    color: string;
    fontSize: number;
  };
  cropMarks: boolean;
  exportDefaults: {
    quality: 'screen' | 'ebook' | 'printer' | 'prepress';
    embedFonts: boolean;
    compressImages: boolean;
    accessibilityTagged: boolean;
  };
  // Named style system
  namedStyles?: NamedStyle[];
  // Document protection
  protection?: DocumentProtection;
  // PDF encryption (password protection + permissions)
  encryption?: PdfEncryption;
  // Custom document properties
  customProperties?: CustomDocumentProperty[];
  // Track changes
  trackChanges?: boolean;
  // Multi-language localization
  systemLanguage?: string;                // source/default language for fallback resolution
  activeLanguages?: string[];           // user-selected active BCP-47 language tags
  localizedProperties?: LocalizedProperty[];
  targetLanguage?: string;                // selected export/preview language
}
