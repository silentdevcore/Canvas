export type ElementType =
  | 'text'
  | 'image'
  | 'shape'
  | 'table'
  | 'line'
  | 'qrcode'
  | 'barcode'
  | 'signature'
  | 'richtext'
  | 'field'
  | 'checkbox'
  | 'rect'
  | 'circle'
  | 'chart'
  | 'subsection'
  | 'area'
  | 'button'
  | 'dropdown'
  | 'optionlist'
  | 'radio'
  | 'watermark'
  | 'note'
  | 'arrow'
  | 'draw'
  | 'date'
  | 'highlight'
  | 'checkmark'
  | 'pageboundary'
  | 'pagenumber'
  | 'link'
  | 'number'
  // New elements
  | 'footnote'
  | 'endnote'
  | 'bookmark'
  | 'comment'
  | 'contentcontrol';

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
  pageScope?: 'current' | 'all' | 'first' | 'range' | 'odd' | 'even';
  pageRange?: string;
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

// ── Custom Document Properties ───────────────────────────────────────────────

export interface CustomDocumentProperty {
  name: string;
  value: string;
  type: 'text' | 'number' | 'boolean' | 'date';
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
    pageScope: 'all' | 'first' | 'range' | 'odd' | 'even';
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
  // Custom document properties
  customProperties?: CustomDocumentProperty[];
  // Track changes
  trackChanges?: boolean;
}
