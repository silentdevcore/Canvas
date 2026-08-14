import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Template, SimpleElement, CellStyle, LayerDirection, PageSettings, Page, PdfEncryption, PdfEncryptionPermissions } from '@/types';
import { useEditorStore, DEFAULT_PAGE_SETTINGS } from '@/store';
import { toDisplay, fromDisplay } from '@/utils/units';
import { getPageSettingsWarnings } from '@/utils/pageValidation';
import { installImportedFontFaces } from '@/utils/importedFonts';
import { applyVerticalWheelToHorizontalScroll } from '@/utils/editorScrolling';
import { updateLanguageSelection } from '@/utils/languageSelection';
import { isDocumentRtlLanguage } from '@/utils/documentDirection';
import { sanitizeRichTextHtml } from '@/utils/sanitizeRichTextHtml';
import { motion } from 'framer-motion';
import { QRCodeSVG } from 'qrcode.react';
import JsBarcode from 'jsbarcode';
import {
  BarChart,
  Bar,
  LineChart,
  Line,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';
import {
  FiArrowLeft,
  FiArrowUp,
  FiArrowDown,
  FiBox,
  FiCheckSquare,
  FiSquare,
  FiCreditCard,
  FiEdit3,
  FiEye,
  FiFileText,
  FiHash,
  FiLayers,
  FiMaximize2,
  FiMonitor,
  FiMousePointer,
  FiPlus,
  FiTrash2,
  FiType,
  FiChevronDown,
  FiChevronLeft,
  FiChevronRight,
  FiX,
  FiChevronsUp,
  FiChevronsDown,
  FiList,
  FiCircle,
  FiPlay,
  FiCalendar,
  FiPenTool,
  FiBookmark,
  FiCheck,
  FiDroplet,
  FiCopy,
  FiLock,
  FiUnlock,
  FiLink,
  FiLink2,
  FiSettings,
  FiZoomIn,
  FiZoomOut,
  FiRefreshCw,
  FiSliders,
  FiEyeOff,
  FiBold,
  FiItalic,
  FiUnderline,
  FiAlignLeft,
  FiAlignCenter,
  FiAlignRight,
  FiAlignJustify,
  FiRotateCw,
  FiCode,
  FiSearch,
  FiMoreVertical,
  FiScissors,
  FiGlobe,
  FiBookOpen,
  FiHelpCircle,
  FiGrid,
} from 'react-icons/fi';
import CodeViewer from './CodeViewer';
import { ElementBoundary } from './ElementBoundary';
import FindReplaceModal from './FindReplaceModal';
import FormBlockModal from './FormBlockModal';
import HelpModal from './HelpModal';
import ExportService from '@/services/ExportService';
import { LanguageTabBar } from './LanguageTabBar';
import { LocalizedPropertiesPanel } from './LocalizedPropertiesPanel';
import type { AutosaveState } from '@/hooks/useDesignerTemplateAutosave';
import { notify } from '@/notifications/toast';
import { uploadDesignerImage } from '@/services/designerAssetApi';


interface SimplePxaSurfaceProps {
  template: Template;
  elements: SimpleElement[];
  pages: Page[];
  currentPageIndex: number;
  sharedElements: SimpleElement[];
  onElementAdd: (element: SimpleElement) => void;
  onElementUpdate: (id: string, updates: Partial<SimpleElement>) => void;
  onElementDelete: (id: string) => void;
  onElementReorder: (id: string, direction: LayerDirection) => void;
  onPreview: () => void;
  onBack: () => void;
  onPageAdd: () => void;
  onPageDelete: (index: number) => void;
  onPageDuplicate: (index: number) => void;
  onPageSelect: (index: number) => void;
  onPageMove: (fromIndex: number, toIndex: number) => void;
  onSharedElementAdd: (element: SimpleElement) => void;
  onSharedElementUpdate: (id: string, updates: Partial<SimpleElement>) => void;
  onSharedElementDelete: (id: string) => void;
  autosaveState?: AutosaveState;
  autosaveMessage?: string;
  onCreateVersion?: () => Promise<string>;
  onPublish?: () => Promise<string>;
  onArchive?: () => Promise<string>;
}

type Tool = {
  id: SimpleElement['type'];
  label: string;
  hint: string;
  icon: React.ComponentType<{ className?: string }>;
  create: () => SimpleElement;
  supportedOutputs?: ('pdf' | 'word')[];
};

type ToolGroup = {
  id: string;
  label: string;
  toolIds: SimpleElement['type'][];
};

type DragState = {
  id: string;
  pointerOffsetX: number;
  pointerOffsetY: number;
  startPointerX: number;
  startPointerY: number;
  // present when dragging a multi-selection
  multi?: { id: string; startX: number; startY: number }[];
  isRtlSurface?: boolean;
  langKey?: string; // write to langOverrides[langKey] instead of root x/y when set
};

type ResizeHandle = 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w';

type ResizeState = {
  id: string;
  handle: ResizeHandle;
  startPointerX: number;
  startPointerY: number;
  startX: number;
  startY: number;
  startWidth: number;
  startHeight: number;
  langKey?: string; // write to langOverrides[langKey] instead of root x/y/w/h when set
};

type RotateState = {
  id: string;
  centerX: number;
  centerY: number;
  startAngle: number;
  initialPointerAngle: number;
  langKey?: string;
};

const createElementId = (type: string) => `${type}-${Date.now()}`;
const MIN_ELEMENT_SIZE = 16;
const BORDER_SIDES = ['Top', 'Right', 'Bottom', 'Left'] as const;

const ELEMENT_TYPE_LABELS: Record<string, string> = {
  text:         'Text Block',
  richtext:     'Rich Text',
  image:        'Image',
  shape:        'Shape',
  rect:         'Rectangle',
  circle:       'Circle',
  line:         'Line',
  table:        'Table',
  chart:        'Chart',
  qrcode:       'QR Code',
  barcode:      'Barcode',
  signature:    'Signature',
  field:        'Text Field',
  textarea:     'Text Area',
  checkbox:     'Checkbox',
  button:       'Button',
  dropdown:     'Dropdown',
  optionlist:   'Option List',
  radio:        'Radio Group',
  subsection:   'Subsection',
  area:         'Area',
  watermark:    'Watermark',
  note:         'Note',
  arrow:        'Arrow',
  draw:         'Drawing',
  date:         'Date',
  highlight:    'Highlight',
  checkmark:    'Checkmark',
  pageboundary: 'Page Boundary',
  pagenumber:   'Page Number',
  link:           'Link',
  number:         'Number',
  toc:            'Table of Contents',
  footnote:       'Footnote',
  endnote:        'Endnote',
  bookmark:       'Bookmark',
  comment:        'Comment',
  contentcontrol: 'Content Control',
};

const FONT_FAMILIES = [
  // Sans-serif system
  'Arial', 'Arial Narrow', 'Arial Black', 'Helvetica', 'Helvetica Neue',
  'Verdana', 'Tahoma', 'Trebuchet MS', 'Geneva', 'Calibri', 'Segoe UI',
  'Gill Sans', 'Optima', 'Futura', 'Century Gothic', 'Franklin Gothic Medium',
  // Serif system
  'Times New Roman', 'Georgia', 'Garamond', 'Palatino', 'Book Antiqua',
  'Cambria', 'Constantia', 'Didot', 'Baskerville', 'Caslon',
  // Monospace
  'Courier New', 'Courier', 'Lucida Console', 'Monaco', 'Consolas',
  'Andale Mono', 'Menlo', 'Source Code Pro',
  // Display & Impact
  'Impact', 'Haettenschweiler', 'Arial Narrow', 'Copperplate', 'Rockwell',
  // Google Fonts (loaded via CDN or system fallback)
  'Roboto', 'Roboto Condensed', 'Roboto Mono', 'Roboto Slab',
  'Inter', 'Inter Tight',
  'Open Sans', 'Lato', 'Montserrat', 'Poppins', 'Nunito', 'Nunito Sans',
  'Source Sans Pro', 'Source Serif Pro', 'Source Code Pro',
  'Raleway', 'Ubuntu', 'Oswald', 'Merriweather', 'Playfair Display',
  'Noto Sans', 'Noto Serif', 'Noto Mono',
  'PT Sans', 'PT Serif', 'PT Mono',
  'DM Sans', 'DM Serif Display', 'DM Mono',
  'Work Sans', 'Mulish', 'Jost', 'Outfit', 'Plus Jakarta Sans',
  'Figtree', 'Manrope', 'Karla', 'Barlow', 'Barlow Condensed',
  'IBM Plex Sans', 'IBM Plex Serif', 'IBM Plex Mono',
  'Libre Baskerville', 'Libre Franklin', 'Crimson Text', 'Lora',
  'EB Garamond', 'Cormorant Garamond', 'Cardo', 'Spectral',
  'Josefin Sans', 'Josefin Slab', 'Cabin', 'Exo 2', 'Titillium Web',
  'Fira Sans', 'Fira Code', 'Space Grotesk', 'Space Mono',
  'Sora', 'Lexend', 'Red Hat Display', 'Red Hat Text',
  'Dancing Script', 'Pacifico', 'Lobster', 'Comfortaa', 'Righteous',
  // Noto — multi-script coverage
  'Noto Sans Arabic', 'Noto Sans SC', 'Noto Sans TC',
  'Noto Sans JP', 'Noto Sans KR', 'Noto Sans Devanagari', 'Noto Sans Thai',
];

const elementBorderStyle = (s: Record<string, any>): React.CSSProperties => {
  const sideStyle: React.CSSProperties = {};
  let hasSideBorder = false;

  BORDER_SIDES.forEach((side) => {
    const width = s[`border${side}Width`];
    if (width == null) return;

    hasSideBorder = true;
    const key = `border${side}` as keyof React.CSSProperties;
    sideStyle[key] = `${Number(width) || 0}px ${s[`border${side}Style`] || s.borderStyle || 'solid'} ${s[`border${side}Color`] || s.borderColor || '#000000'}` as never;
  });

  if (hasSideBorder) {
    return {
      ...sideStyle,
      borderRadius: s.borderRadius ?? undefined,
    };
  }

  return {
    border: s.borderWidth ? `${s.borderWidth}px ${s.borderStyle || 'solid'} ${s.borderColor || '#000000'}` : undefined,
    borderRadius: s.borderRadius ?? undefined,
  };
};

const TYPOGRAPHY_TYPES = new Set<string>([
  'text', 'richtext', 'button', 'field', 'checkbox', 'dropdown', 'optionlist',
  'radio', 'date', 'pagenumber', 'watermark', 'note', 'checkmark', 'arrow',
]);

const LOCALIZATION_LANGUAGES: { tag: string; label: string; rtl?: boolean }[] = [
  { tag: 'en', label: '🇬🇧 English' },
  { tag: 'de', label: '🇩🇪 Deutsch' },
  { tag: 'fr', label: '🇫🇷 Français' },
  { tag: 'es', label: '🇪🇸 Español' },
  { tag: 'it', label: '🇮🇹 Italiano' },
  { tag: 'pt', label: '🇧🇷 Português' },
  { tag: 'ru', label: '🇷🇺 Русский' },
  { tag: 'el', label: '🇬🇷 Ελληνικά' },
  { tag: 'ar', label: '🇸🇦 العربية', rtl: true },
  { tag: 'zh', label: '🇨🇳 中文' },
  { tag: 'ja', label: '🇯🇵 日本語' },
  { tag: 'ko', label: '🇰🇷 한국어' },
  { tag: 'hi', label: '🇮🇳 हिन्दी' },
  { tag: 'th', label: '🇹🇭 ภาษาไทย' },
];


const BACKGROUND_TYPES = new Set<string>([
  'text', 'richtext', 'image', 'shape', 'rect', 'circle', 'table', 'button',
  'field', 'checkbox', 'dropdown', 'optionlist', 'radio', 'note', 'subsection',
  'area', 'highlight', 'signature', 'date', 'pagenumber', 'checkmark',
]);

const BORDER_TYPES = new Set<string>([
  'text', 'richtext', 'image', 'shape', 'rect', 'circle', 'table', 'button',
  'field', 'checkbox', 'dropdown', 'optionlist', 'radio', 'note', 'signature',
  'date', 'pagenumber', 'checkmark', 'area', 'subsection',
]);

const PADDING_TYPES = new Set<string>([
  'text', 'richtext', 'button', 'field', 'checkbox', 'dropdown',
  'optionlist', 'radio', 'note',
]);

const PAGE_PRESETS: Record<string, { width: number; height: number }> = {
  A4:                  { width: 595,  height: 842  },
  A5:                  { width: 420,  height: 595  },
  A3:                  { width: 842,  height: 1191 },
  Letter:              { width: 612,  height: 792  },
  Legal:               { width: 612,  height: 1008 },
  'Landscape A4':      { width: 842,  height: 595  },
  'Landscape A3':      { width: 1191, height: 842  },
  'Presentation 16:9': { width: 1280, height: 720  },
  'Presentation 4:3':  { width: 1024, height: 768  },
  'Book A5':           { width: 420,  height: 595  },
  'Social Square':     { width: 1080, height: 1080 },
};

const createDefaultChartData = () => ({
  labels: ['Jan', 'Feb', 'Mar', 'Apr'],
  datasets: [
    {
      label: 'Series 1',
      data: [12, 19, 14, 22]
    }
  ]
});

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

type GlyphDiagnostic = {
  value?: string;
  confidence?: number;
  method?: string;
  score?: number;
  initialCandidate?: string;
  selectedCandidate?: string;
  signals?: Record<string, number>;
  decisionWeights?: Record<string, number>;
};

type ImageAnalysisDiagnostics = {
  sourceWidthPx?: number;
  sourceHeightPx?: number;
  workingWidthPx?: number;
  workingHeightPx?: number;
  scaleFactor?: number;
  colorRegionCount?: number;
  shapeCount?: number;
  textLineCount?: number;
  wordCount?: number;
  glyphCount?: number;
  lowConfidenceGlyphCount?: number;
  elementCount?: number;
  warnings?: string[];
};

type ImageOcrDiagnostics = {
  sourceWidthPx?: number;
  sourceHeightPx?: number;
  pageCount?: number;
  languages?: string;
  ocrEngine?: string;
  ocrEngineVersion?: string;
  wordCount?: number;
  lineCount?: number;
  averageConfidence?: number;
  lowConfidenceWordCount?: number;
  elapsedMs?: number;
  managedMemoryBytes?: number;
};

type ImportDiagnostic = {
  code?: string;
  severity?: 'info' | 'warning' | 'error';
  message?: string;
  source?: string;
};

const getGlyphDiagnostics = (element: SimpleElement): GlyphDiagnostic[] => {
  const glyphs = element.style?.imageAnalysisGlyphs;
  return Array.isArray(glyphs) ? glyphs as GlyphDiagnostic[] : [];
};

const getImageAnalysisDiagnostics = (template: Template): ImageAnalysisDiagnostics | null => {
  const diagnostics = template.data?.imageAnalysis?.diagnostics;
  return diagnostics && typeof diagnostics === 'object' ? diagnostics as ImageAnalysisDiagnostics : null;
};

const getImageOcrDiagnostics = (template: Template): ImageOcrDiagnostics | null => {
  const diagnostics = template.data?.imageOcr?.diagnostics;
  return diagnostics && typeof diagnostics === 'object' ? diagnostics as ImageOcrDiagnostics : null;
};

const getImageOcrWarnings = (template: Template): string[] => {
  const warnings = template.data?.imageOcr?.warnings;
  return Array.isArray(warnings) ? warnings.filter((w): w is string => typeof w === 'string') : [];
};

const getMarkdownImportDiagnostics = (template: Template): ImportDiagnostic[] => {
  const diagnostics = template.data?.markdownImport?.diagnostics;
  return Array.isArray(diagnostics)
    ? diagnostics.filter((diagnostic): diagnostic is ImportDiagnostic =>
        diagnostic !== null && typeof diagnostic === 'object')
    : [];
};

const formatPercent = (value: unknown) => {
  const number = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(number) ? `${Math.round(number * 100)}%` : '0%';
};

const formatNumber = (value: unknown) => {
  const number = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(number) ? new Intl.NumberFormat().format(number) : '0';
};

const topGlyphWeights = (weights?: Record<string, number>) =>
  Object.entries(weights ?? {})
    .filter(([, value]) => value > 0)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 2);

interface PersistentHorizontalScrollbarProps {
  targetRef: React.RefObject<HTMLElement | null>;
  refreshKey: string;
  label: string;
  scrollLeftLabel: string;
  scrollRightLabel: string;
}

const PersistentHorizontalScrollbar: React.FC<PersistentHorizontalScrollbarProps> = ({
  targetRef,
  refreshKey,
  label,
  scrollLeftLabel,
  scrollRightLabel,
}) => {
  const [metrics, setMetrics] = useState({ left: 0, maximum: 0 });

  useEffect(() => {
    const target = targetRef.current;
    if (!target) return;

    const sync = () => {
      setMetrics({
        left: Math.max(0, target.scrollLeft),
        maximum: Math.max(0, target.scrollWidth - target.clientWidth),
      });
    };

    sync();
    target.addEventListener('scroll', sync, { passive: true });

    const resizeObserver = typeof ResizeObserver === 'undefined'
      ? null
      : new ResizeObserver(sync);
    resizeObserver?.observe(target);
    if (target.firstElementChild instanceof HTMLElement) {
      resizeObserver?.observe(target.firstElementChild);
    }

    const mutationObserver = typeof MutationObserver === 'undefined'
      ? null
      : new MutationObserver(sync);
    mutationObserver?.observe(target, { childList: true, subtree: true });

    return () => {
      target.removeEventListener('scroll', sync);
      resizeObserver?.disconnect();
      mutationObserver?.disconnect();
    };
  }, [targetRef, refreshKey]);

  const scrollByPage = (direction: -1 | 1) => {
    const target = targetRef.current;
    if (!target) return;
    target.scrollBy({
      left: direction * Math.max(80, target.clientWidth * 0.6),
      behavior: 'smooth',
    });
  };

  const maximum = Math.max(1, metrics.maximum);
  const disabled = metrics.maximum <= 0;

  return (
    <div className={`editor-persistent-scrollbar${disabled ? ' is-disabled' : ''}`}>
      <button
        type="button"
        onClick={() => scrollByPage(-1)}
        disabled={disabled || metrics.left <= 0}
        aria-label={scrollLeftLabel}
      >
        <FiChevronLeft />
      </button>
      <input
        type="range"
        min={0}
        max={maximum}
        step={1}
        value={Math.min(metrics.left, maximum)}
        disabled={disabled}
        onChange={(event) => {
          if (targetRef.current) {
            targetRef.current.scrollLeft = Number(event.currentTarget.value);
          }
        }}
        aria-label={label}
      />
      <button
        type="button"
        onClick={() => scrollByPage(1)}
        disabled={disabled || metrics.left >= metrics.maximum}
        aria-label={scrollRightLabel}
      >
        <FiChevronRight />
      </button>
    </div>
  );
};

const SimplePxaSurface: React.FC<SimplePxaSurfaceProps> = ({
  template,
  elements,
  pages,
  currentPageIndex,
  sharedElements,
  onElementAdd,
  onElementUpdate,
  onElementDelete,
  onElementReorder,
  onPreview,
  onBack,
  onPageAdd,
  onPageDelete,
  onPageDuplicate,
  onPageSelect,
  onPageMove,
  onSharedElementAdd,
  onSharedElementUpdate,
  onSharedElementDelete,
  autosaveState = 'idle',
  autosaveMessage = '',
  onCreateVersion,
  onPublish,
  onArchive,
}) => {
  const { t } = useTranslation('editor');
  const [selectedElementId, setSelectedElementId] = useState<string | null>(null);
  const [selectedElementIds, setSelectedElementIds] = useState<Set<string>>(new Set());
  const [dragState, setDragState] = useState<DragState | null>(null);
  const [resizeState, setResizeState] = useState<ResizeState | null>(null);
  const [rotateState, setRotateState] = useState<RotateState | null>(null);
  const [draggingPageIndex, setDraggingPageIndex] = useState<number | null>(null);
  const [dragOverPageIndex, setDragOverPageIndex] = useState<number | null>(null);
  const [isDragOverSurface, setIsDragOverSurface] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<string[]>(['text', 'form', 'visual', 'layout', 'advanced']);
  const [zoomLevel, setZoomLevel] = useState(1);
  const [linkedMargins, setLinkedMargins] = useState(true);
  const [linkedPadding, setLinkedPadding] = useState(true);
  const [inspectorTab, setInspectorTab] = useState<'inspector' | 'layers' | 'properties'>('inspector');
  const [clipboard, setClipboard] = useState<SimpleElement | null>(null);
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number; elementId: string | null } | null>(null);
  const [marqueeState, setMarqueeState] = useState<{ startX: number; startY: number; currentX: number; currentY: number; additive: boolean } | null>(null);
  const [drawingMode, setDrawingMode] = useState<'line' | 'arrow' | 'draw' | null>(null);
  const [drawGhost, setDrawGhost] = useState<{ startX: number; startY: number; currentX: number; currentY: number; pathPoints?: string } | null>(null);
  const [codeViewerOpen, setCodeViewerOpen] = useState(false);
  const [findReplaceOpen, setFindReplaceOpen] = useState(false);
  const [formBlockModalOpen, setFormBlockModalOpen] = useState(false);
  const [topbarMenuOpen, setTopbarMenuOpen] = useState(false);
  const [templateActionPending, setTemplateActionPending] = useState(false);
  const [extractingPage, setExtractingPage] = useState<number | null>(null);
  // Language Scope UI selection: 'lang' = current tab selected, 'all' = All selected
  const [scopeShowAll, setScopeShowAll] = useState(false);
  const workspaceRef = useRef<HTMLElement | null>(null);
  const stageRef = useRef<HTMLElement | null>(null);
  const pageViewportRef = useRef<HTMLDivElement | null>(null);
  const pageContentRef = useRef<HTMLDivElement | null>(null);
  const pageStripRef = useRef<HTMLDivElement | null>(null);
  const activePageThumbRef = useRef<HTMLDivElement | null>(null);
  const contentInputRef = useRef<HTMLInputElement>(null);

  const runTemplateAction = async (action: () => Promise<string>) => {
    setTemplateActionPending(true);
    setTopbarMenuOpen(false);
    try {
      notify.success(await action());
    } catch (error) {
      notify.error(error instanceof Error ? error.message : 'The template action failed.');
    } finally {
      setTemplateActionPending(false);
    }
  };

  useEffect(() => {
    installImportedFontFaces(
      'pxa-imported-font-faces-editor',
      [...pages.flatMap(page => page.elements), ...sharedElements]
    );
  }, [pages, sharedElements]);

  useEffect(() => {
    activePageThumbRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'nearest',
      inline: 'nearest',
    });
  }, [currentPageIndex, pages.length]);

  useEffect(() => {
    const workspace = workspaceRef.current;
    const stage = stageRef.current;
    if (!workspace || !stage) return;

    const syncPanelHeight = () => {
      workspace.style.setProperty('--editor-stage-height', `${stage.offsetHeight}px`);
    };

    syncPanelHeight();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', syncPanelHeight);
      return () => window.removeEventListener('resize', syncPanelHeight);
    }

    const observer = new ResizeObserver(syncPanelHeight);
    observer.observe(stage);
    return () => observer.disconnect();
  }, []);

  const handlePageStripWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    if (applyVerticalWheelToHorizontalScroll(
      event.currentTarget,
      event.deltaX,
      event.deltaY,
    )) {
      event.preventDefault();
    }
  };


  const buildDesign = () => ({
    id: template.id,
    name: template.name,
    pages: pages.map(p => ({ id: p.id, elements: p.elements })),
    sharedElements,
    pageSettings: pageSettings ?? {},
  });

  const handleCloneDesign = async () => {
    setTopbarMenuOpen(false);
    try {
      const cloned = await ExportService.cloneDesign(buildDesign()) as any;
      const newPages: Page[] = cloned.pages ?? pages;
      const newShared: SimpleElement[] = cloned.sharedElements ?? sharedElements;
      bulkReplaceContent(newPages, newShared);
      notify.success(t('toasts.designCloned'));
    } catch (err) {
      notify.error(err instanceof Error ? err.message : t('toasts.cloneFailed'));
    }
  };

  const handleExtractPage = async (pageIndex: number) => {
    setExtractingPage(pageIndex);
    try {
      const extracted = await ExportService.extractPages(buildDesign(), [pageIndex + 1]) as any;
      const json = JSON.stringify(extracted, null, 2);
      const blob = new Blob([json], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${template.name.replace(/\s+/g, '-').toLowerCase()}-page${pageIndex + 1}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      notify.success(t('toasts.pageExtracted', { number: pageIndex + 1 }));
    } catch (err) {
      notify.error(err instanceof Error ? err.message : t('toasts.extractFailed'));
    } finally {
      setExtractingPage(null);
    }
  };

  const { pageSettings, updatePageSettings, settingsModifiedSinceExport, snapshotHistory, undo, redo, bulkReplaceContent, currentPreviewLanguage, setCurrentPreviewLanguage, helpModalOpen, setHelpModalOpen, documentMode, setDocumentMode } = useEditorStore();
  const pageWidth = pageSettings.width;
  const pageHeight = pageSettings.height;
  const isCurrentRtl = isDocumentRtlLanguage(currentPreviewLanguage);
  // Only route position writes to langOverrides when the document has 2+ active languages.
  const isMultilingual = (pageSettings.activeLanguages?.length ?? 0) > 1;

  // Returns the effective position/size for an element, applying lang override if one exists.
  const getEffectivePos = (el: SimpleElement) => {
    const ov = isMultilingual && currentPreviewLanguage ? el.langOverrides?.[currentPreviewLanguage] : undefined;
    return {
      x: ov?.x ?? el.x,
      y: ov?.y ?? el.y,
      width: ov?.width ?? el.width,
      height: ov?.height ?? el.height,
    };
  };

  const getEffectiveRotation = (el: SimpleElement): number => {
    const ov = isMultilingual && currentPreviewLanguage ? el.langOverrides?.[currentPreviewLanguage] : undefined;
    return ov?.rotation ?? el.style?.rotation ?? 0;
  };

  const setLanguageSelected = (tag: string, selected: boolean) => {
    const current = pageSettings.activeLanguages ?? [];
    const next = updateLanguageSelection(current, tag, selected);
    updatePageSettings({ activeLanguages: next });

    if (selected) {
      if (!currentPreviewLanguage || currentPreviewLanguage === navigator.language.split('-')[0]) {
        setCurrentPreviewLanguage(tag);
      }
    } else if (currentPreviewLanguage === tag) {
      setCurrentPreviewLanguage(next[0] ?? navigator.language.split('-')[0]);
    }
  };

  // When not in "All" mode, writes go to langOverrides[currentPreviewLanguage].
  const applyPosUpdate = (id: string, patch: { x?: number; y?: number; width?: number; height?: number; rotation?: number }, langKey?: string) => {
    if (langKey) {
      const el = [...elements, ...sharedElements].find(e => e.id === id);
      if (!el) return;
      updateElementById(id, {
        langOverrides: {
          ...(el.langOverrides ?? {}),
          [langKey]: { ...(el.langOverrides?.[langKey] ?? {}), ...patch },
        },
      });
    } else {
      updateElementById(id, patch);
    }
  };

  // When the selected element changes, reset to language mode (user clicks All explicitly to override)
  useEffect(() => {
    setScopeShowAll(false);
  }, [selectedElementId]); // eslint-disable-line react-hooks/exhaustive-deps

  // When the language tab changes, always switch back to the language button (override All)
  useEffect(() => {
    setScopeShowAll(false);
  }, [currentPreviewLanguage]);

  // Resolve {{KEY}} in content using localized property values for the current preview language.
  const resolveContent = useMemo(() => {
    const props = pageSettings.localizedProperties ?? [];
    const sysLang = navigator.language.split('-')[0];
    const target = currentPreviewLanguage || sysLang;
    const map: Record<string, string> = {};
    for (const p of props) {
      if (p.scope === 'own') {
        // Own properties are only visible when the current preview language matches the owner
        if (p.ownerLanguage === target) {
          map[p.key] = p.localizedValues[p.ownerLanguage] ?? '';
        }
      } else {
        // Global: each language fills its own value; fall back to system language value
        map[p.key] = p.localizedValues[target]
          ?? p.localizedValues[sysLang]
          ?? '';
      }
    }
    return (content: string | undefined): string => {
      if (!content || !content.includes('{{')) return content ?? '';
      return content.replace(/\{\{(\w+)\}\}/g, (_, key) => map[key] ?? `{{${key}}}`);
    };
  }, [pageSettings.localizedProperties, currentPreviewLanguage]);

  const updateMargin = (side: keyof PageSettings['margins'], displayVal: number) => {
    const px = fromDisplay(displayVal, pageSettings.unit);
    if (linkedMargins) {
      updatePageSettings({ margins: { top: px, right: px, bottom: px, left: px } });
    } else {
      updatePageSettings({ margins: { ...pageSettings.margins, [side]: px } });
    }
  };

  const snapValue = (value: number) =>
    pageSettings.snapToGrid
      ? Math.round(value / pageSettings.gridSize) * pageSettings.gridSize
      : value;

  const isOutsideMargins = (el: SimpleElement) => {
    const { margins } = pageSettings;
    return (
      el.x < margins.left ||
      el.y < margins.top ||
      el.x + el.width  > pageWidth  - margins.right ||
      el.y + el.height > pageHeight - margins.bottom
    );
  };

  const selectedElement = useMemo(
    () => [...elements, ...sharedElements].find(el => el.id === selectedElementId) ?? null,
    [elements, sharedElements, selectedElementId]
  );

  // Unified update/delete — routes to shared or page based on element identity
  const updateElementById = React.useCallback((id: string, updates: Partial<SimpleElement>) => {
    if (sharedElements.some(el => el.id === id)) onSharedElementUpdate(id, updates);
    else onElementUpdate(id, updates);
  }, [sharedElements, onSharedElementUpdate, onElementUpdate]);

  const deleteElementById = React.useCallback((id: string) => {
    if (sharedElements.some(el => el.id === id)) onSharedElementDelete(id);
    else onElementDelete(id);
  }, [sharedElements, onSharedElementDelete, onElementDelete]);

  const tools: Tool[] = [
    {
      id: 'text',
      label: t('tools.text.label'),
      hint: t('tools.text.hint'),
      icon: FiType,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('text'),
        type: 'text',
        x: 96,
        y: 112,
        width: 220,
        height: 56,
        content: 'Click to edit text',
        style: {
          fontSize: 16,
          color: '#111827',
          fontWeight: 'normal'
        }
      })
    },
    {
      id: 'qrcode',
      label: t('tools.qrcode.label'),
      hint: t('tools.qrcode.hint'),
      icon: FiHash,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('qrcode'),
        type: 'qrcode',
        x: 96,
        y: 196,
        width: 120,
        height: 120,
        qrValue: 'https://example.com',
        qrSize: 120
      })
    },
    {
      id: 'barcode',
      label: t('tools.barcode.label'),
      hint: t('tools.barcode.hint'),
      icon: FiCreditCard,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('barcode'),
        type: 'barcode',
        x: 96,
        y: 340,
        width: 240,
        height: 88,
        barcodeValue: '123456789012',
        barcodeType: 'CODE128'
      })
    },
    {
      id: 'signature',
      label: t('tools.signature.label'),
      hint: t('tools.signature.hint'),
      icon: FiEdit3,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('signature'),
        type: 'signature',
        x: 96,
        y: 464,
        width: 260,
        height: 104,
        signatureLabel: 'Signature'
      })
    },
    {
      id: 'richtext',
      label: t('tools.richtext.label'),
      hint: t('tools.richtext.hint'),
      icon: FiFileText,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('richtext'),
        type: 'richtext',
        x: 96,
        y: 604,
        width: 320,
        height: 148,
        htmlContent: '<p><strong>Rich Text</strong> with <em>formatting</em></p>'
      })
    },
    {
      id: 'field',
      label: t('tools.field.label'),
      hint: t('tools.field.hint'),
      icon: FiFileText,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('field'),
        type: 'field',
        x: 96,
        y: 124,
        width: 260,
        height: 64,
        fieldLabel: 'Full name',
        fieldName: 'full_name',
        required: true
      })
    },
    {
      id: 'textarea',
      label: t('tools.textarea.label'),
      hint: t('tools.textarea.hint'),
      icon: FiAlignLeft,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('textarea'),
        type: 'textarea' as const,
        x: 96,
        y: 200,
        width: 260,
        height: 120,
        fieldLabel: 'Comments',
        fieldName: 'comments',
        placeholder: 'Enter your text here…',
        required: false,
      })
    },
    {
      id: 'checkbox',
      label: t('tools.checkbox.label'),
      hint: t('tools.checkbox.hint'),
      icon: FiCheckSquare,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('checkbox'),
        type: 'checkbox',
        x: 96,
        y: 212,
        width: 220,
        height: 42,
        fieldLabel: 'I agree',
        fieldName: 'agreement',
        required: false
      })
    },
    {
      id: 'image',
      label: t('tools.image.label'),
      hint: t('tools.image.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('image'),
        type: 'image',
        x: 96,
        y: 276,
        width: 220,
        height: 140,
        content: 'https://via.placeholder.com/220x140'
      })
    },
    {
      id: 'shape',
      label: t('tools.shape.label'),
      hint: t('tools.shape.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('shape'),
        type: 'shape',
        x: 96,
        y: 436,
        width: 180,
        height: 100,
        style: { backgroundColor: '#e5e7eb' }
      })
    },
    {
      id: 'table',
      label: t('tools.table.label'),
      hint: t('tools.table.hint'),
      icon: FiLayers,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('table'),
        type: 'table',
        x: 96,
        y: 556,
        width: 300,
        height: 140,
        style: {
          rows: 3,
          columns: 3,
          borderWidth: 1,
          borderColor: '#000000',
          cellPadding: 5
        }
      })
    },
    {
      id: 'line',
      label: t('tools.line.label'),
      hint: t('tools.line.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('line'),
        type: 'line',
        x: 96,
        y: 716,
        width: 260,
        height: 4,
        style: { backgroundColor: '#9ca3af' }
      })
    },
    {
      id: 'rect',
      label: t('tools.rect.label'),
      hint: t('tools.rect.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('rect'),
        type: 'rect',
        x: 366,
        y: 112,
        width: 160,
        height: 100,
        style: { backgroundColor: '#dbeafe' }
      })
    },
    {
      id: 'circle',
      label: t('tools.circle.label'),
      hint: t('tools.circle.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('circle'),
        type: 'circle',
        x: 366,
        y: 232,
        width: 100,
        height: 100,
        style: { backgroundColor: '#fde68a' }
      })
    },
    {
      id: 'chart',
      label: t('tools.chart.label'),
      hint: t('tools.chart.hint'),
      icon: FiLayers,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('chart'),
        type: 'chart',
        x: 366,
        y: 352,
        width: 200,
        height: 140,
        chartType: 'bar',
        chartData: createDefaultChartData()
      })
    },
    {
      id: 'subsection',
      label: t('tools.subsection.label'),
      hint: t('tools.subsection.hint'),
      icon: FiLayers,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('subsection'),
        type: 'subsection',
        x: 366,
        y: 512,
        width: 200,
        height: 120
      })
    },
    {
      id: 'area',
      label: t('tools.area.label'),
      hint: t('tools.area.hint'),
      icon: FiBox,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('area'),
        type: 'area',
        x: 366,
        y: 652,
        width: 200,
        height: 120
      })
    },
    {
      id: 'button',
      label: t('tools.button.label'),
      hint: t('tools.button.hint'),
      icon: FiPlay,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('button'),
        type: 'button',
        x: 96,
        y: 760,
        width: 160,
        height: 44,
        content: 'Button'
      })
    },
    {
      id: 'dropdown',
      label: t('tools.dropdown.label'),
      hint: t('tools.dropdown.hint'),
      icon: FiChevronDown,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('dropdown'),
        type: 'dropdown',
        x: 276,
        y: 760,
        width: 180,
        height: 44,
        options: ['Option 1', 'Option 2']
      })
    },
    {
      id: 'optionlist',
      label: t('tools.optionlist.label'),
      hint: t('tools.optionlist.hint'),
      icon: FiList,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('optionlist'),
        type: 'optionlist',
        x: 366,
        y: 760,
        width: 200,
        height: 72,
        options: ['Item 1', 'Item 2', 'Item 3']
      })
    },
    {
      id: 'radio',
      label: t('tools.radio.label'),
      hint: t('tools.radio.hint'),
      icon: FiCircle,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('radio'),
        type: 'radio',
        x: 96,
        y: 808,
        width: 260,
        height: 52,
        options: ['Yes', 'No']
      })
    },
    {
      id: 'watermark',
      label: t('tools.watermark.label'),
      hint: t('tools.watermark.hint'),
      icon: FiDroplet,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('watermark'),
        type: 'watermark',
        x: 112,
        y: 320,
        width: 372,
        height: 96,
        content: 'CONFIDENTIAL',
        watermarkMode: 'text',
        pageScope: 'all',
        style: {
          color: '#64748b',
          opacity: 0.18,
          rotation: -24,
          fontSize: 42,
          fontWeight: 'bold'
        }
      })
    },
    {
      id: 'note',
      label: t('tools.note.label'),
      hint: t('tools.note.hint'),
      icon: FiBookmark,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('note'),
        type: 'note',
        x: 348,
        y: 96,
        width: 164,
        height: 112,
        noteTitle: 'Notiz',
        noteBody: 'Kommentar eingeben',
        noteAuthor: 'Editor',
        noteCollapsed: false,
        style: { backgroundColor: '#fef3c7', color: '#78350f' }
      })
    },
    {
      id: 'arrow',
      label: t('tools.arrow.label'),
      hint: t('tools.arrow.hint'),
      icon: FiArrowUp,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('arrow'),
        type: 'arrow',
        x: 112,
        y: 176,
        width: 208,
        height: 72,
        arrowMode: 'straight',
        arrowDirection: 'right',
        arrowRotation: 0,
        startMarker: 'none',
        endMarker: 'filled',
        style: { color: '#dc2626', strokeWidth: 4, dashStyle: 'solid' }
      })
    },
    {
      id: 'draw',
      label: t('tools.draw.label'),
      hint: t('tools.draw.hint'),
      icon: FiPenTool,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('draw'),
        type: 'draw',
        x: 96,
        y: 452,
        width: 216,
        height: 108,
        drawTool: 'pen',
        pathData: 'M 10 76 C 44 18, 78 112, 116 54 S 184 20, 206 72',
        style: { color: '#1d4ed8', strokeWidth: 4, opacity: 1 }
      })
    },
    {
      id: 'date',
      label: t('tools.date.label'),
      hint: t('tools.date.hint'),
      icon: FiCalendar,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('date'),
        type: 'date',
        x: 96,
        y: 96,
        width: 180,
        height: 40,
        content: new Date().toISOString().slice(0, 10),
        dateMode: 'static',
        dateFormat: 'yyyy-MM-dd',
        locale: 'de-DE',
        timezone: 'Europe/Berlin',
        fallbackText: '-',
        style: { color: '#111827', fontSize: 14 }
      })
    },
    {
      id: 'highlight',
      label: t('tools.highlight.label'),
      hint: t('tools.highlight.hint'),
      icon: FiEdit3,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('highlight'),
        type: 'highlight',
        x: 92,
        y: 252,
        width: 260,
        height: 34,
        markMode: 'rectangle',
        style: { backgroundColor: '#fde047', opacity: 0.45, borderRadius: 4 }
      })
    },
    {
      id: 'checkmark',
      label: t('tools.checkmark.label'),
      hint: t('tools.checkmark.hint'),
      icon: FiCheck,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('checkmark'),
        type: 'checkmark',
        x: 96,
        y: 604,
        width: 148,
        height: 42,
        fieldLabel: 'Selection',
        fieldName: 'selection',
        checkState: 'checked',
        style: { color: '#16a34a', strokeWidth: 3, fontSize: 14 }
      })
    },
    {
      id: 'pageboundary',
      label: t('tools.pageboundary.label'),
      hint: t('tools.pageboundary.hint'),
      icon: FiMaximize2,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('pageboundary'),
        type: 'pageboundary',
        x: 48,
        y: 704,
        width: 500,
        height: 34,
        pageBoundaryMode: 'start',
        content: 'Start on new page',
        style: { color: '#7c3aed', dashStyle: 'dashed' }
      })
    },
    {
      id: 'pagenumber',
      label: t('tools.pagenumber.label'),
      hint: t('tools.pagenumber.hint'),
      icon: FiHash,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('pagenumber'),
        type: 'pagenumber',
        x: 236,
        y: 792,
        width: 124,
        height: 28,
        numberingFormat: 'pageOfTotal',
        pageScope: 'all',
        startNumber: 1,
        prefix: '',
        suffix: '',
        style: { color: '#374151', fontSize: 12 }
      })
    },
    {
      id: 'toc',
      label: t('tools.toc.label'),
      hint: t('tools.toc.hint'),
      icon: FiBookOpen,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('toc'),
        type: 'toc',
        x: 48,
        y: 96,
        width: 400,
        height: 200,
        style: { color: '#1f2937', fontSize: 13 },
        tocEntries: [],
      })
    },
    {
      id: 'link',
      label: t('tools.link.label'),
      hint: t('tools.link.hint'),
      icon: FiLink,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('link'),
        type: 'link',
        x: 96,
        y: 140,
        width: 200,
        height: 32,
        content: 'Click here',
        href: 'https://example.com',
        linkTarget: '_blank',
        style: { color: '#2563eb', fontSize: 14 }
      })
    },
    {
      id: 'number',
      label: t('tools.number.label'),
      hint: t('tools.number.hint'),
      icon: FiHash,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('number'),
        type: 'number',
        x: 96,
        y: 180,
        width: 160,
        height: 40,
        numberValue: 1234.56,
        numberStyle: 'decimal',
        numberDecimals: 2,
        numberLocale: 'de-DE',
        numberCurrency: 'EUR',
        prefix: '',
        suffix: '',
        style: { color: '#111827', fontSize: 18, fontWeight: 'bold' }
      })
    },
    {
      id: 'footnote',
      label: t('tools.footnote.label'),
      hint: t('tools.footnote.hint'),
      icon: FiFileText,
      supportedOutputs: ['word'],
      create: () => ({
        id: createElementId('footnote'),
        type: 'footnote',
        x: 96,
        y: 720,
        width: 300,
        height: 32,
        footnoteText: 'Footnote text here.',
        style: {}
      })
    },
    {
      id: 'endnote',
      label: t('tools.endnote.label'),
      hint: t('tools.endnote.hint'),
      icon: FiFileText,
      supportedOutputs: ['word'],
      create: () => ({
        id: createElementId('endnote'),
        type: 'endnote',
        x: 96,
        y: 760,
        width: 300,
        height: 32,
        footnoteText: 'Endnote text here.',
        style: {}
      })
    },
    {
      id: 'bookmark',
      label: t('tools.bookmark.label'),
      hint: t('tools.bookmark.hint'),
      icon: FiBookmark,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('bookmark'),
        type: 'bookmark',
        x: 96,
        y: 96,
        width: 200,
        height: 24,
        bookmarkName: 'section-1',
        bookmarkTarget: '',
        style: {}
      })
    },
    {
      id: 'comment',
      label: t('tools.comment.label'),
      hint: t('tools.comment.hint'),
      icon: FiEdit3,
      supportedOutputs: ['pdf', 'word'] as const,
      create: () => ({
        id: createElementId('comment'),
        type: 'comment',
        x: 400,
        y: 120,
        width: 200,
        height: 60,
        commentText: 'Review comment.',
        commentAuthor: 'Author',
        commentDate: new Date().toISOString().slice(0, 10),
        commentId: createElementId('cid'),
        style: {}
      })
    },
    {
      id: 'contentcontrol',
      label: t('tools.contentcontrol.label'),
      hint: t('tools.contentcontrol.hint'),
      icon: FiCode,
      supportedOutputs: ['word'],
      create: () => ({
        id: createElementId('sdt'),
        type: 'contentcontrol',
        x: 96,
        y: 200,
        width: 300,
        height: 48,
        contentControlType: 'richText',
        contentControlTitle: 'Content Control',
        contentControlTag: '',
        contentControlPlaceholder: 'Click to edit…',
        content: '',
        style: {}
      })
    }
  ];

  const toolGroups: ToolGroup[] = [
    {
      id: 'text',
      label: t('toolGroups.text'),
      toolIds: ['text', 'richtext', 'link']
    },
    {
      id: 'form',
      label: t('toolGroups.form'),
      toolIds: ['field', 'textarea', 'checkbox', 'button', 'dropdown', 'optionlist', 'radio', 'signature', 'number']
    },
    {
      id: 'visual',
      label: t('toolGroups.visual'),
      toolIds: ['image', 'qrcode', 'barcode', 'chart']
    },
    {
      id: 'layout',
      label: t('toolGroups.layout'),
      toolIds: ['shape', 'rect', 'circle', 'line', 'table', 'subsection', 'area']
    },
    {
      id: 'advanced',
      label: t('toolGroups.advanced'),
      toolIds: ['watermark', 'note', 'arrow', 'draw', 'date', 'highlight', 'checkmark', 'pageboundary', 'pagenumber', 'toc']
    },
    {
      id: 'word',
      label: t('toolGroups.word'),
      toolIds: ['footnote', 'endnote', 'bookmark', 'comment', 'contentcontrol']
    }
  ];

  const toolsById = useMemo(
    () => Object.fromEntries(tools.map(tool => [tool.id, tool])) as Record<SimpleElement['type'], Tool>,
    [tools]
  );

  const WORD_ONLY_TYPES = new Set<SimpleElement['type']>(['footnote', 'endnote', 'contentcontrol']);

  const visibleToolGroups = useMemo(
    () => documentMode === 'pdf' ? toolGroups.filter(g => g.id !== 'word') : toolGroups,
    [documentMode, toolGroups]
  );

  const wordElementsOnSurface = useMemo(
    () => elements.some(el => WORD_ONLY_TYPES.has(el.type as SimpleElement['type'])),
    [elements]
  );

  const toggleGroup = (groupId: string) => {
    setExpandedGroups(previous => (
      previous.includes(groupId)
        ? previous.filter(id => id !== groupId)
        : [...previous, groupId]
    ));
  };

  const getSurfacePoint = (clientX: number, clientY: number) => {
    const rect = pageContentRef.current?.getBoundingClientRect();
    if (!rect) return { x: 0, y: 0 };
    // getBoundingClientRect returns visual (scaled) pixels; divide by zoomLevel to get pt coordinates.
    return {
      x: (clientX - rect.left) / zoomLevel,
      y: (clientY - rect.top)  / zoomLevel,
    };
  };

  const positionElement = (element: SimpleElement, x: number, y: number) => ({
    x: Math.round(clamp(snapValue(x), 0, pageWidth - element.width)),
    y: Math.round(clamp(snapValue(y), 0, pageHeight - element.height))
  });

  useEffect(() => {
    if (!dragState) return;

    const handlePointerMove = (event: PointerEvent) => {
      const point = getSurfacePoint(event.clientX, event.clientY);

      if (dragState.multi && dragState.multi.length > 1) {
        const dx = point.x - dragState.startPointerX;
        const dy = point.y - dragState.startPointerY;
        const dxStored = dragState.isRtlSurface ? -dx : dx;
        dragState.multi.forEach(({ id, startX, startY }) => {
          const el = [...elements, ...sharedElements].find(e => e.id === id);
          if (!el) return;
          const patch = {
            x: Math.round(clamp(startX + dxStored, 0, pageWidth - el.width)),
            y: Math.round(clamp(startY + dy, 0, pageHeight - el.height)),
          };
          applyPosUpdate(id, patch, dragState.langKey);
        });
      } else {
        const element = [...elements, ...sharedElements].find(item => item.id === dragState.id);
        if (!element) return;
        let patch: { x: number; y: number };
        if (dragState.isRtlSurface) {
          const displayX = point.x - dragState.pointerOffsetX;
          const storedX = pageWidth - displayX - element.width;
          patch = positionElement(element, storedX, point.y - dragState.pointerOffsetY);
        } else {
          patch = positionElement(element, point.x - dragState.pointerOffsetX, point.y - dragState.pointerOffsetY);
        }
        applyPosUpdate(element.id, patch, dragState.langKey);
      }
    };

    const handlePointerUp = () => setDragState(null);

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);

    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [dragState, elements, sharedElements, updateElementById]);

  useEffect(() => {
    if (!resizeState) return;

    const handlePointerMove = (event: PointerEvent) => {
      const element = [...elements, ...sharedElements].find(item => item.id === resizeState.id);
      if (!element) return;

      const point = getSurfacePoint(event.clientX, event.clientY);
      const dx = point.x - resizeState.startPointerX;
      const dy = point.y - resizeState.startPointerY;
      const { startX, startY, startWidth, startHeight } = resizeState;

      let x = startX, y = startY, width = startWidth, height = startHeight;

      switch (resizeState.handle) {
        case 'nw': width = Math.max(MIN_ELEMENT_SIZE, startWidth - dx); height = Math.max(MIN_ELEMENT_SIZE, startHeight - dy); x = startX + (startWidth - width); y = startY + (startHeight - height); break;
        case 'n':  height = Math.max(MIN_ELEMENT_SIZE, startHeight - dy); y = startY + (startHeight - height); break;
        case 'ne': width = Math.max(MIN_ELEMENT_SIZE, startWidth + dx); height = Math.max(MIN_ELEMENT_SIZE, startHeight - dy); y = startY + (startHeight - height); break;
        case 'e':  width = Math.max(MIN_ELEMENT_SIZE, startWidth + dx); break;
        case 'se': width = Math.max(MIN_ELEMENT_SIZE, startWidth + dx); height = Math.max(MIN_ELEMENT_SIZE, startHeight + dy); break;
        case 's':  height = Math.max(MIN_ELEMENT_SIZE, startHeight + dy); break;
        case 'sw': width = Math.max(MIN_ELEMENT_SIZE, startWidth - dx); height = Math.max(MIN_ELEMENT_SIZE, startHeight + dy); x = startX + (startWidth - width); break;
        case 'w':  width = Math.max(MIN_ELEMENT_SIZE, startWidth - dx); x = startX + (startWidth - width); break;
      }

      x = clamp(x, 0, pageWidth - MIN_ELEMENT_SIZE);
      y = clamp(y, 0, pageHeight - MIN_ELEMENT_SIZE);
      width = Math.min(width, pageWidth - x);
      height = Math.min(height, pageHeight - y);

      applyPosUpdate(resizeState.id, {
        x: Math.round(x), y: Math.round(y),
        width: Math.round(width), height: Math.round(height),
      }, resizeState.langKey);
    };

    const handlePointerUp = () => setResizeState(null);

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [resizeState, elements, sharedElements, updateElementById]);

  // Rotation drag
  useEffect(() => {
    if (!rotateState) return;
    const handlePointerMove = (event: PointerEvent) => {
      const point = getSurfacePoint(event.clientX, event.clientY);
      const angle = Math.atan2(point.y - rotateState.centerY, point.x - rotateState.centerX) * (180 / Math.PI);
      const delta = angle - rotateState.initialPointerAngle;
      let newRotation = Math.round((rotateState.startAngle + delta) % 360);
      if (newRotation < 0) newRotation += 360;
      const element = [...elements, ...sharedElements].find(el => el.id === rotateState.id);
      if (!element) return;
      if (rotateState.langKey) {
        applyPosUpdate(rotateState.id, { rotation: newRotation }, rotateState.langKey);
      } else {
        updateElementById(rotateState.id, { style: { ...element.style, rotation: newRotation } });
      }
    };
    const handlePointerUp = () => setRotateState(null);
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [rotateState, elements, sharedElements, updateElementById]);

  // Marquee selection
  useEffect(() => {
    if (!marqueeState) return;

    const handlePointerMove = (event: PointerEvent) => {
      const point = getSurfacePoint(event.clientX, event.clientY);
      setMarqueeState(prev => prev ? { ...prev, currentX: point.x, currentY: point.y } : null);
    };

    const handlePointerUp = () => {
      if (marqueeState) {
        const x1 = Math.min(marqueeState.startX, marqueeState.currentX);
        const y1 = Math.min(marqueeState.startY, marqueeState.currentY);
        const x2 = Math.max(marqueeState.startX, marqueeState.currentX);
        const y2 = Math.max(marqueeState.startY, marqueeState.currentY);
        if (x2 - x1 > 4 || y2 - y1 > 4) {
          const hit = [...elements, ...sharedElements].filter(el =>
            el.x < x2 && el.x + el.width > x1 && el.y < y2 && el.y + el.height > y1
          );
          if (hit.length > 0) {
            setSelectedElementIds(prev => {
              const next = marqueeState.additive ? new Set(prev) : new Set<string>();
              hit.forEach(e => next.add(e.id));
              return next;
            });
            setSelectedElementId(hit[hit.length - 1].id);
          }
        }
      }
      setMarqueeState(null);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [marqueeState, elements, sharedElements]);

  useEffect(() => {
    if (!drawGhost || !drawingMode) return;

    const handlePointerMove = (event: PointerEvent) => {
      const point = getSurfacePoint(event.clientX, event.clientY);
      setDrawGhost(prev => {
        if (!prev) return null;
        if (drawingMode === 'draw') {
          return { ...prev, currentX: point.x, currentY: point.y, pathPoints: `${prev.pathPoints} L ${point.x} ${point.y}` };
        }
        return { ...prev, currentX: point.x, currentY: point.y };
      });
    };

    const handlePointerUp = () => {
      const { startX, startY, currentX, currentY, pathPoints } = drawGhost;
      const dx = currentX - startX;
      const dy = currentY - startY;
      const dist = Math.sqrt(dx * dx + dy * dy);

      if (dist >= 5) {
        if (drawingMode === 'line') {
          const angle = Math.atan2(dy, dx) * 180 / Math.PI;
          const cx = (startX + currentX) / 2;
          const cy = (startY + currentY) / 2;
          const el = nameElement({
            id: createElementId('line'),
            type: 'line',
            x: Math.round(cx - dist / 2),
            y: Math.round(cy - 2),
            width: Math.round(dist),
            height: 4,
            style: { backgroundColor: '#9ca3af', rotation: Math.round(angle * 10) / 10 },
          });
          onElementAdd(el);
          setSelectedElementId(el.id);
        } else if (drawingMode === 'arrow') {
          const angle = Math.atan2(dy, dx) * 180 / Math.PI;
          const cx = (startX + currentX) / 2;
          const cy = (startY + currentY) / 2;
          const el = nameElement({
            id: createElementId('arrow'),
            type: 'arrow',
            x: Math.round(cx - dist / 2),
            y: Math.round(cy - 20),
            width: Math.round(dist),
            height: 40,
            arrowMode: 'straight',
            arrowDirection: 'right',
            arrowRotation: 0,
            startMarker: 'none',
            endMarker: 'filled',
            style: { color: '#dc2626', strokeWidth: 4, dashStyle: 'solid', rotation: Math.round(angle * 10) / 10 },
          });
          onElementAdd(el);
          setSelectedElementId(el.id);
        } else if (drawingMode === 'draw' && pathPoints) {
          const matches = [...pathPoints.matchAll(/(-?[\d.]+)\s+(-?[\d.]+)/g)];
          const xs = matches.map(m => parseFloat(m[1]));
          const ys = matches.map(m => parseFloat(m[2]));
          const minX = Math.min(...xs);
          const minY = Math.min(...ys);
          const w = Math.max(Math.max(...xs) - minX, 16);
          const h = Math.max(Math.max(...ys) - minY, 16);
          const localPath = pathPoints.replace(/(-?[\d.]+)\s+(-?[\d.]+)/g, (_m, px, py) =>
            `${Math.round((parseFloat(px) - minX) * 10) / 10} ${Math.round((parseFloat(py) - minY) * 10) / 10}`
          );
          const el = nameElement({
            id: createElementId('draw'),
            type: 'draw',
            x: Math.round(minX),
            y: Math.round(minY),
            width: Math.round(w),
            height: Math.round(h),
            drawTool: 'pen',
            pathData: localPath,
            style: { color: '#1d4ed8', strokeWidth: 4, opacity: 1 },
          });
          onElementAdd(el);
          setSelectedElementId(el.id);
        }
      }

      setDrawGhost(null);
      setDrawingMode(null);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [drawGhost, drawingMode, elements, sharedElements, onElementAdd]);

  const handleResizePointerDown = (
    event: React.PointerEvent,
    element: SimpleElement,
    handle: ResizeHandle
  ) => {
    event.stopPropagation();
    if (event.button !== 0) return;
    if (element.locked) return;
    snapshotHistory();
    const point = getSurfacePoint(event.clientX, event.clientY);
    const effPos = getEffectivePos(element);
    const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
    setResizeState({
      id: element.id,
      handle,
      startPointerX: point.x,
      startPointerY: point.y,
      startX: effPos.x,
      startY: effPos.y,
      startWidth: effPos.width,
      startHeight: effPos.height,
      langKey,
    });
  };

  const handleRotatePointerDown = (event: React.PointerEvent, element: SimpleElement) => {
    event.stopPropagation();
    if (event.button !== 0) return;
    if (element.locked) return;
    snapshotHistory();
    const effPos = getEffectivePos(element);
    const displayX = isCurrentRtl ? pageWidth - effPos.x - effPos.width : effPos.x;
    const centerX = displayX + effPos.width / 2;
    const centerY = effPos.y + effPos.height / 2;
    const point = getSurfacePoint(event.clientX, event.clientY);
    const initialPointerAngle = Math.atan2(point.y - centerY, point.x - centerX) * (180 / Math.PI);
    const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
    setRotateState({
      id: element.id,
      centerX,
      centerY,
      startAngle: getEffectiveRotation(element),
      initialPointerAngle,
      langKey,
    });
  };

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const tag = (event.target as HTMLElement).tagName;

      if (event.key === 'F1') {
        event.preventDefault();
        setHelpModalOpen(true);
        return;
      }

      // Zoom shortcuts work globally (even without a selected element)
      if (event.metaKey || event.ctrlKey) {
        if (event.key === '=' || event.key === '+') {
          event.preventDefault();
          setZoomLevel(z => Math.min(2, parseFloat((z + 0.25).toFixed(2))));
          return;
        }
        if (event.key === '-') {
          event.preventDefault();
          setZoomLevel(z => Math.max(0.25, parseFloat((z - 0.25).toFixed(2))));
          return;
        }
        if (event.key === '0') {
          event.preventDefault();
          setZoomLevel(1);
          return;
        }
      }

      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;

      if (event.metaKey || event.ctrlKey) {
        const k = event.key.toLowerCase();
        if (k === 'z') {
          event.preventDefault();
          if (event.shiftKey) redo(); else undo();
          return;
        }
        if (k === 'y') {
          event.preventDefault();
          redo();
          return;
        }
        if (k === 'a') {
          event.preventDefault();
          const all = [...elements, ...sharedElements];
          setSelectedElementIds(new Set(all.map(e => e.id)));
          if (all.length > 0) setSelectedElementId(all[all.length - 1].id);
          return;
        }
        if (k === 'c' && selectedElement) {
          setClipboard(selectedElement);
          return;
        }
        if (k === 'v' && clipboard) {
          event.preventDefault();
          const clone = structuredClone(clipboard);
          const pasted: SimpleElement = {
            ...clone,
            id: createElementId(clipboard.type),
            name: undefined,
            x: Math.round(clamp(clipboard.x + 16, 0, pageWidth - clipboard.width)),
            y: Math.round(clamp(clipboard.y + 16, 0, pageHeight - clipboard.height)),
          };
          const named = nameElement(pasted);
          onElementAdd(named);
          setSelectedElementId(named.id);
          return;
        }
      }

      if (!selectedElement) return;

      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'd') {
        event.preventDefault();
        duplicateElement(selectedElement);
        return;
      }

      switch (event.key) {
        case 'Delete':
        case 'Backspace':
          event.preventDefault();
          deleteElementById(selectedElement.id);
          clearSelection();
          break;

        case 'Escape':
          if (drawingMode) {
            setDrawingMode(null);
            setDrawGhost(null);
          } else {
            clearSelection();
          }
          break;

        case 'ArrowUp':
        case 'ArrowDown':
        case 'ArrowLeft':
        case 'ArrowRight': {
          event.preventDefault();
          if (selectedElement.locked) break;
          const step = event.shiftKey ? 10 : 1;
          const dx = event.key === 'ArrowLeft' ? -step : event.key === 'ArrowRight' ? step : 0;
          const dy = event.key === 'ArrowUp'   ? -step : event.key === 'ArrowDown'  ? step : 0;
          updateElementById(selectedElement.id, {
            x: clamp(selectedElement.x + dx, 0, pageWidth  - selectedElement.width),
            y: clamp(selectedElement.y + dy, 0, pageHeight - selectedElement.height)
          });
          break;
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedElement, deleteElementById, updateElementById, onElementAdd, pageWidth, pageHeight, undo, redo, elements, sharedElements, clipboard, drawingMode, setHelpModalOpen]);

  const startDrawGhost = (clientX: number, clientY: number) => {
    const point = getSurfacePoint(clientX, clientY);
    setDrawGhost({
      startX: point.x,
      startY: point.y,
      currentX: point.x,
      currentY: point.y,
      pathPoints: drawingMode === 'draw' ? `M ${point.x} ${point.y}` : undefined,
    });
  };

  const handleSurfacePointerDown = (event: React.PointerEvent) => {
    if (event.button !== 0) return;
    closeContextMenu();

    if (drawingMode) {
      event.stopPropagation();
      startDrawGhost(event.clientX, event.clientY);
      return;
    }

    if (event.target !== event.currentTarget) return;

    if (!event.shiftKey && !event.metaKey && !event.ctrlKey) clearSelection();
    const point = getSurfacePoint(event.clientX, event.clientY);
    const additive = event.shiftKey || event.metaKey || event.ctrlKey;
    setMarqueeState({ startX: point.x, startY: point.y, currentX: point.x, currentY: point.y, additive });
  };

  const nameElement = (el: SimpleElement): SimpleElement => {
    if (el.name) return el;
    const count = [...elements, ...sharedElements].filter(e => e.type === el.type).length + 1;
    const label = (ELEMENT_TYPE_LABELS[el.type] ?? el.type).replace(/\s+/g, '');
    return { ...el, name: `${label}${count}` };
  };

  const addElement = (tool: Tool) => {
    const el = nameElement(tool.create());
    onElementAdd(el);
    setSelectedElementId(el.id);
  };

  const insertIntoZone = (zone: 'header' | 'footer', type: 'text' | 'pagenumber' | 'date' | 'image') => {
    const zoneHeight = zone === 'header' ? pageSettings.headerHeight : pageSettings.footerHeight;
    const zoneTop = zone === 'header' ? 0 : pageHeight - pageSettings.footerHeight;
    const elHeight = type === 'image' ? Math.min(zoneHeight - 8, 48) : Math.min(zoneHeight - 8, 28);
    const elWidth = type === 'image' ? elHeight : 180;
    const x = Math.round((pageWidth - elWidth) / 2);
    const y = zoneTop + Math.round((zoneHeight - elHeight) / 2);

    let element: SimpleElement;
    if (type === 'text') {
      element = {
        id: createElementId('text'),
        type: 'text',
        x, y,
        width: elWidth,
        height: elHeight,
        content: zone === 'header' ? 'Header text' : 'Footer text',
        style: { fontSize: 11, color: '#374151', fontFamily: 'Arial', fontWeight: 'normal' }
      };
    } else if (type === 'pagenumber') {
      element = {
        id: createElementId('pagenumber'),
        type: 'pagenumber',
        x, y,
        width: elWidth,
        height: elHeight,
        numberingFormat: 'pageOfTotal',
        pageScope: 'all',
        startNumber: 1,
        prefix: '',
        suffix: '',
        style: { fontSize: 11, color: '#374151' }
      };
    } else if (type === 'date') {
      element = {
        id: createElementId('date'),
        type: 'date',
        x, y,
        width: elWidth,
        height: elHeight,
        content: new Date().toISOString().slice(0, 10),
        dateMode: 'static',
        dateFormat: 'yyyy-MM-dd',
        locale: 'de-DE',
        timezone: 'Europe/Berlin',
        fallbackText: '-',
        style: { fontSize: 11, color: '#374151' }
      };
    } else {
      element = {
        id: createElementId('image'),
        type: 'image',
        x, y,
        width: elWidth,
        height: elHeight,
        content: '',
        fitMode: 'contain',
        focalX: 50,
        focalY: 50
      };
    }

    const named = nameElement(element);
    onSharedElementAdd(named);
    setSelectedElementId(named.id);
  };

  const addElementAtPoint = (tool: Tool, clientX: number, clientY: number) => {
    const raw = tool.create();
    const point = getSurfacePoint(clientX, clientY);
    // On RTL canvas the drop point is mirrored: convert display x back to stored x
    const canvasX = isCurrentRtl ? pageWidth - point.x - raw.width / 2 : point.x - raw.width / 2;
    const el = nameElement({ ...raw, ...positionElement(raw, canvasX, point.y - raw.height / 2) });
    onElementAdd(el);
    setSelectedElementId(el.id);
  };

  const handleToolDragStart = (event: React.DragEvent, tool: Tool) => {
    event.dataTransfer.setData('application/x-ui-designer-tool', tool.id);
    event.dataTransfer.effectAllowed = 'copy';
  };

  const handleSurfaceDrop = (event: React.DragEvent) => {
    event.preventDefault();
    setIsDragOverSurface(false);

    const toolId = event.dataTransfer.getData('application/x-ui-designer-tool');
    const tool = tools.find(item => item.id === toolId);
    if (!tool) return;

    addElementAtPoint(tool, event.clientX, event.clientY);
  };

  const selectOne = (id: string) => {
    setSelectedElementId(id);
    setSelectedElementIds(new Set([id]));
  };

  const toggleMultiSelect = (id: string) => {
    setSelectedElementId(id);
    setSelectedElementIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) { next.delete(id); } else { next.add(id); }
      return next;
    });
  };

  const clearSelection = () => {
    setSelectedElementId(null);
    setSelectedElementIds(new Set());
  };

  const handleElementPointerDown = (event: React.PointerEvent, element: SimpleElement) => {
    if (event.button !== 0) return;

    if (drawingMode) {
      event.stopPropagation();
      startDrawGhost(event.clientX, event.clientY);
      return;
    }

    if (element.locked) {
      selectOne(element.id);
      return;
    }

    if (event.shiftKey || event.metaKey || event.ctrlKey) {
      event.stopPropagation();
      toggleMultiSelect(element.id);
      return;
    }

    snapshotHistory();
    const point = getSurfacePoint(event.clientX, event.clientY);

    const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;

    // Build multi-drag payload when this element is part of an existing multi-selection
    const isInMultiSel = selectedElementIds.size > 1 && selectedElementIds.has(element.id);
    const allEls = [...elements, ...sharedElements];
    const multi = isInMultiSel
      ? [...selectedElementIds].map(id => {
          const el = allEls.find(e => e.id === id);
          if (!el) return null;
          const pos = getEffectivePos(el);
          return { id, startX: pos.x, startY: pos.y };
        }).filter(Boolean) as { id: string; startX: number; startY: number }[]
      : undefined;

    if (!isInMultiSel) selectOne(element.id);

    const pos = getEffectivePos(element);
    const displayX = isCurrentRtl ? pageWidth - pos.x - pos.width : pos.x;
    setDragState({
      id: element.id,
      pointerOffsetX: point.x - displayX,
      pointerOffsetY: point.y - pos.y,
      startPointerX: point.x,
      startPointerY: point.y,
      multi,
      isRtlSurface: isCurrentRtl,
      langKey,
    });
  };

  const handleElementContextMenu = (event: React.MouseEvent, element: SimpleElement) => {
    event.preventDefault();
    event.stopPropagation();
    selectOne(element.id);
    setContextMenu({ x: event.clientX, y: event.clientY, elementId: element.id });
  };

  const handleSurfaceContextMenu = (event: React.MouseEvent) => {
    event.preventDefault();
    setContextMenu({ x: event.clientX, y: event.clientY, elementId: null });
  };

  const closeContextMenu = () => setContextMenu(null);

  const contextMenuAction = (action: string) => {
    closeContextMenu();
    if (!contextMenu) return;
    const el = contextMenu.elementId
      ? [...elements, ...sharedElements].find(e => e.id === contextMenu.elementId) ?? null
      : null;

    switch (action) {
      case 'copy':
        if (el) setClipboard(el);
        break;
      case 'paste':
        if (clipboard) {
          const clone = structuredClone(clipboard);
          const pasted: SimpleElement = {
            ...clone,
            id: createElementId(clipboard.type),
            name: undefined,
            x: Math.round(clamp(clipboard.x + 16, 0, pageWidth - clipboard.width)),
            y: Math.round(clamp(clipboard.y + 16, 0, pageHeight - clipboard.height)),
          };
          const named = nameElement(pasted);
          onElementAdd(named);
          setSelectedElementId(named.id);
        }
        break;
      case 'duplicate':
        if (el) duplicateElement(el);
        break;
      case 'delete':
        if (el) { deleteElementById(el.id); clearSelection(); }
        break;
      case 'lock':
        if (el) updateElementById(el.id, { locked: !el.locked });
        break;
      case 'hide':
        if (el) updateElementById(el.id, { hidden: !el.hidden });
        break;
      case 'front':
        if (el) onElementReorder(el.id, 'front');
        break;
      case 'forward':
        if (el) onElementReorder(el.id, 'forward');
        break;
      case 'backward':
        if (el) onElementReorder(el.id, 'backward');
        break;
      case 'back':
        if (el) onElementReorder(el.id, 'back');
        break;
      case 'selectAll': {
        const all = [...elements, ...sharedElements];
        setSelectedElementIds(new Set(all.map(e => e.id)));
        if (all.length > 0) setSelectedElementId(all[all.length - 1].id);
        break;
      }
    }
  };

  const alignSelected = (axis: 'left' | 'hcenter' | 'right' | 'top' | 'vcenter' | 'bottom') => {
    const ids = selectedElementIds.size > 0 ? selectedElementIds : selectedElementId ? new Set([selectedElementId]) : new Set<string>();
    ids.forEach(id => {
      const el = elements.find(e => e.id === id);
      if (!el) return;
      const m = pageSettings.margins;
      let update: Partial<SimpleElement> = {};
      if (axis === 'left')    update = { x: m.left };
      if (axis === 'hcenter') update = { x: Math.round((pageWidth - el.width) / 2) };
      if (axis === 'right')   update = { x: pageWidth - m.right - el.width };
      if (axis === 'top')     update = { y: m.top };
      if (axis === 'vcenter') update = { y: Math.round((pageHeight - el.height) / 2) };
      if (axis === 'bottom')  update = { y: pageHeight - m.bottom - el.height };
      onElementUpdate(id, update);
    });
  };

  const distributeSelected = (dir: 'horizontal' | 'vertical') => {
    const sorted = elements
      .filter(e => selectedElementIds.has(e.id))
      .sort((a, b) => dir === 'horizontal' ? a.x - b.x : a.y - b.y);
    if (sorted.length < 3) return;
    const first = sorted[0];
    const last = sorted[sorted.length - 1];
    const span = dir === 'horizontal'
      ? (last.x + last.width) - first.x
      : (last.y + last.height) - first.y;
    const totalSize = sorted.reduce((s, e) => s + (dir === 'horizontal' ? e.width : e.height), 0);
    const gap = (span - totalSize) / (sorted.length - 1);
    let pos = dir === 'horizontal' ? first.x : first.y;
    sorted.forEach((el, i) => {
      if (i === 0) { pos += dir === 'horizontal' ? el.width : el.height; return; }
      const newPos = Math.round(pos + gap);
      onElementUpdate(el.id, dir === 'horizontal' ? { x: newPos } : { y: newPos });
      pos = newPos + (dir === 'horizontal' ? el.width : el.height);
    });
  };

  const updateSelectedElement = (updates: Partial<SimpleElement>) => {
    if (!selectedElementId) return;
    updateElementById(selectedElementId, updates);
  };

  const updateLayoutValue = (key: 'x' | 'y' | 'width' | 'height', value: string) => {
    const num = Number(value) || 0;
    const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
    if (langKey && selectedElementId) {
      applyPosUpdate(selectedElementId, { [key]: num }, langKey);
    } else {
      updateSelectedElement({ [key]: num });
    }
  };

  const duplicateElement = (element: SimpleElement) => {
    const clone = structuredClone(element);
    const base: SimpleElement = {
      ...clone,
      id: createElementId(element.type),
      name: undefined,  // force auto-name so count increments
      x: Math.round(clamp(element.x + 24, 0, pageWidth - element.width)),
      y: Math.round(clamp(element.y + 24, 0, pageHeight - element.height))
    };
    const duplicatedElement = nameElement(base);
    onElementAdd(duplicatedElement);
    setSelectedElementId(duplicatedElement.id);
  };

  const getElementWarnings = (element: SimpleElement) => {
    const warnings: string[] = [];

    if (element.pageScope === 'range' && !element.pageRange?.trim()) {
      warnings.push(t('warnings.pageRangeNeeded'));
    }

    if (element.type === 'date' && element.dateMode === 'binding' && !element.binding?.trim()) {
      warnings.push(t('warnings.bindingPathNeeded'));
    }

    if (element.type === 'draw' && !element.pathData?.trim()) {
      warnings.push(t('warnings.drawPathNeeded'));
    }

    if (element.style?.opacity !== undefined && (element.style.opacity < 0 || element.style.opacity > 1)) {
      warnings.push(t('warnings.opacityRange'));
    }

    return warnings;
  };

  const getDatePreview = (element: SimpleElement) => {
    if (element.dateMode === 'static') return element.content || element.fallbackText || '-';
    if (element.dateMode === 'binding') return element.binding ? `{{ ${element.binding} }}` : element.fallbackText || '-';

    const locale = element.locale || 'de-DE';
    const date = new Date();
    if ((element.dateFormat || '').toLowerCase().includes('time')) {
      return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
    }

    return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(date);
  };

  const getPageNumberPreview = (element: SimpleElement) => {
    const start = element.startNumber || 1;
    const prefix = element.prefix || '';
    const suffix = element.suffix || '';

    switch (element.numberingFormat || 'pageOfTotal') {
      case 'current':
        return `${prefix}${start}${suffix}`;
      case 'total':
        return `${prefix}1${suffix}`;
      case 'roman':
        return `${prefix}I${suffix}`;
      case 'alphabetic':
        return `${prefix}A${suffix}`;
      case 'pageOfTotal':
      default:
        return `${prefix}Page ${start} of 1${suffix}`;
    }
  };

  const renderElement = (element: SimpleElement, isPreview: boolean = false) => {
    if (element.type === 'text') {
      const s = element.style ?? {};
      return (
        <div
          className="editor-element-text"
          dir={element.textDirection || 'ltr'}
          lang={element.language || undefined}
          style={{
            fontSize:       s.fontSize      || 16,
            fontFamily:     s.fontFamily    || 'Arial',
            color:          s.color         || '#111827',
            fontWeight:     s.fontWeight    || 'normal',
            fontStyle:      s.fontStyle     || 'normal',
            textDecoration: s.textDecoration|| 'none',
            textAlign:      (s.textAlign    || 'left') as React.CSSProperties['textAlign'],
            lineHeight:     s.lineHeight    ?? 1.4,
            letterSpacing:  s.letterSpacing != null ? `${s.letterSpacing}px` : undefined,
            whiteSpace:     s.whiteSpace    as React.CSSProperties['whiteSpace'] | undefined,
            backgroundColor:s.backgroundColor && s.backgroundColor !== 'transparent' ? s.backgroundColor : undefined,
            opacity:        s.backgroundOpacity != null ? s.backgroundOpacity : undefined,
            ...elementBorderStyle(s),
            padding:        [s.paddingTop ?? 0, s.paddingRight ?? 0, s.paddingBottom ?? 0, s.paddingLeft ?? 0].join('px ') + 'px',
          }}
        >
          {resolveContent(element.content)}
        </div>
      );
    }

    if (element.type === 'qrcode') {
      if (isPreview) {
        return (
          <QRCodeSVG
            value={element.qrValue || 'https://example.com'}
            size={element.qrSize || 120}
            level="H"
            includeMargin={true}
          />
        );
      }
      return (
        <div className="editor-placeholder">
          <FiHash className="editor-placeholder-icon" />
          <span>{t('placeholders.qrCode')}</span>
          <small>{element.qrValue}</small>
        </div>
      );
    }

    if (element.type === 'barcode') {
      if (isPreview) {
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
      return (
        <div className="editor-placeholder editor-placeholder-wide">
          <FiCreditCard className="editor-placeholder-icon" />
          <span>{t('placeholders.barcode')}</span>
          <small>{element.barcodeValue}</small>
        </div>
      );
    }

    if (element.type === 'signature') {
      return (
        <div className="editor-signature">
          <FiEdit3 className="editor-placeholder-icon" />
          <span>{resolveContent(element.signatureLabel)}</span>
          <div className="editor-signature-line" />
          <small>{t('placeholders.signatureLine')}</small>
        </div>
      );
    }

    if (element.type === 'richtext') {
      return (
        <div
          className="editor-richtext"
          dangerouslySetInnerHTML={{ __html: sanitizeRichTextHtml(element.htmlContent || '') }}
        />
      );
    }

    if (element.type === 'field') {
      return (
        <div className="editor-form-field">
          <span>
            {resolveContent(element.fieldLabel)}
            {element.required && <span className="editor-field-required-badge" title={t('placeholders.requiredField')}>*</span>}
          </span>
          <strong>{element.required ? t('placeholders.required') : t('placeholders.optional')}</strong>
        </div>
      );
    }

    if (element.type === 'textarea') {
      return (
        <div className="editor-form-field editor-form-field--textarea">
          <span>
            {resolveContent(element.fieldLabel)}
            {element.required && <span className="editor-field-required-badge" title={t('placeholders.requiredField')}>*</span>}
          </span>
          <div className="editor-textarea-preview">
            {element.placeholder && (
              <span className="editor-textarea-placeholder">{element.placeholder}</span>
            )}
          </div>
        </div>
      );
    }

    if (element.type === 'checkbox') {
      const isChecked = (element.checkState ?? 'checked') === 'checked';
      const CheckboxIcon = isChecked ? FiCheckSquare : FiSquare;
      return (
        <div className="editor-checkbox-field">
          <CheckboxIcon />
          <span>{resolveContent(element.fieldLabel)}</span>
        </div>
      );
    }

    if (element.type === 'line') {
      const color = element.style?.backgroundColor || element.style?.borderColor || element.style?.color || '#9ca3af';
      return (
        <div style={{ width: '100%', height: '100%', backgroundColor: color }} />
      );
    }

    if (element.type === 'image') {
      const imgStyle: React.CSSProperties = {
        width: '100%',
        height: '100%',
        objectFit: element.fitMode || 'contain',
        objectPosition: `${element.focalX || 50}% ${element.focalY || 50}%`
      };

      if (element.preserveAspectRatio) {
        imgStyle.objectFit = 'contain';
      }

      return (
        <div style={{
          overflow: 'hidden',
          width: '100%',
          height: '100%',
          position: 'relative'
        }}>
          <img
            src={element.content || 'https://via.placeholder.com/220x140'}
            alt={t('placeholders.image')}
            style={imgStyle}
          />
        </div>
      );
    }

    if (element.type === 'shape' || element.type === 'rect') {
      const s = element.style ?? {};
      const bg = s.backgroundColor ?? s.fill ?? 'transparent';
      const bw = s.borderWidth ?? 1;
      const bs = s.borderStyle ?? 'solid';
      const bc = s.borderColor ?? '#000000';
      return (
        <div style={{
          width: '100%', height: '100%',
          backgroundColor: bg,
          ...elementBorderStyle({ borderWidth: bw, borderStyle: bs, borderColor: bc, ...s }),
        }} />
      );
    }

    if (element.type === 'circle') {
      const s = element.style ?? {};
      const bg = s.backgroundColor ?? s.fill ?? 'transparent';
      const bw = s.borderWidth ?? 1;
      return (
        <div style={{
          width: '100%', height: '100%',
          backgroundColor: bg,
          border: bw > 0 ? `${bw}px ${s.borderStyle ?? 'solid'} ${s.borderColor ?? '#000000'}` : 'none',
          borderRadius: '50%',
        }} />
      );
    }

    if (element.type === 'table') {
      const totalRows    = element.style?.rows ?? 3;
      const columns      = element.style?.columns ?? 3;
      const bw           = element.style?.borderWidth ?? 1;
      const bc           = element.style?.borderColor || '#000000';
      const cp           = element.style?.cellPadding ?? 5;
      const hasHeader    = element.headerRow ?? false;
      const hasFooter    = element.footerRow ?? false;
      const headerBg     = element.headerBgColor || '#f1f5f9';
      const zebraOn      = element.zebraEnabled ?? false;
      const zebraColor   = element.zebraColor || '#f9fafb';
      const colWidths    = element.columnWidths ?? [];
      const cellData     = element.cellData ?? [];
      const colAligns    = element.columnAlignments ?? [];
      const bodyRows     = Math.max(1, totalRows - (hasHeader ? 1 : 0) - (hasFooter ? 1 : 0));
      const rdlColumnHeaders = Array.isArray(element.style?.rdlTablixColumnHierarchy)
        ? element.style.rdlTablixColumnHierarchy
            .map((member: any) => member?.headerText || member?.groupName)
            .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
        : [];
      const rdlRowHeaders = Array.isArray(element.style?.rdlTablixRowHierarchy)
        ? element.style.rdlTablixRowHierarchy
            .map((member: any) => member?.headerText || member?.groupName)
            .filter((value: unknown): value is string => typeof value === 'string' && value.trim().length > 0)
        : [];
      const rdlMatrixHeaders = [...rdlColumnHeaders, ...rdlRowHeaders];

      const cellFontSize   = element.style?.cellFontSize ?? 10;
      const cellFontFamily = element.style?.cellFontFamily ?? 'Arial';
      const cellColor      = element.style?.cellColor as string | undefined;
      const cellFontWeight = element.style?.cellFontWeight ?? 'normal';

      const cellStyles = element.cellStyles ?? [];
      const sideCss = (s?: { color?: string; width?: number }) =>
        s ? `${s.width ?? 1}px solid ${s.color ?? '#000000'}` : undefined;

      const tdStyle = (
        rowIdx: number,
        colIdx: number,
        kind: 'header' | 'body' | 'footer',
        dataRow: number = rowIdx
      ): React.CSSProperties => {
        const style: React.CSSProperties = {
          border: `${bw}px solid ${bc}`,
          padding: cp,
          textAlign: colAligns[colIdx] || 'left',
          fontSize: cellFontSize,
          fontFamily: cellFontFamily,
          fontWeight: kind === 'header' ? 700 : cellFontWeight,
          color: cellColor ?? (kind === 'header' ? '#1e293b' : kind === 'footer' ? '#374151' : '#555'),
          backgroundColor:
            kind === 'header' ? headerBg
            : kind === 'footer' ? '#f8fafc'
            : zebraOn && rowIdx % 2 === 1 ? zebraColor
            : 'transparent',
          width: colWidths[colIdx] ? colWidths[colIdx] : undefined,
        };

        // Sparse per-cell override (background / alignment / borders) keyed by the absolute data row.
        const cs = cellStyles.find((x) => x.row === dataRow && x.col === colIdx);
        if (cs) {
          if (cs.backgroundColor) style.backgroundColor = cs.backgroundColor;
          if (cs.textAlign) style.textAlign = cs.textAlign;
          if (cs.padding != null) style.padding = cs.padding;
          if (cs.fontFamily) style.fontFamily = cs.fontFamily;
          if (cs.fontSize != null) style.fontSize = cs.fontSize;
          if (cs.bold) style.fontWeight = 700;
          if (cs.italic) style.fontStyle = 'italic';
          if (cs.color) style.color = cs.color;
          const hasBorder = cs.borderColor != null || cs.borderWidth != null
            || cs.borderTop || cs.borderRight || cs.borderBottom || cs.borderLeft;
          if (hasBorder) {
            // Explicit per-cell borders replace the default grid border (parity with the image exporter).
            const uniform = (cs.borderColor != null || cs.borderWidth != null)
              ? `${cs.borderWidth ?? 1}px solid ${cs.borderColor ?? '#000000'}`
              : 'none';
            style.border = undefined;
            style.borderTop = sideCss(cs.borderTop) ?? uniform;
            style.borderRight = sideCss(cs.borderRight) ?? uniform;
            style.borderBottom = sideCss(cs.borderBottom) ?? uniform;
            style.borderLeft = sideCss(cs.borderLeft) ?? uniform;
          }
        }
        return style;
      };

      const cell = (r: number, c: number) => cellData[r]?.[c] ?? '';

      return (
        <table style={{
          width: '100%', height: '100%', borderCollapse: 'collapse',
          border: `${bw}px solid ${bc}`,
          tableLayout: colWidths.some(Boolean) ? 'fixed' : 'auto',
        }}>
          {colWidths.some(Boolean) && (
            <colgroup>
              {Array.from({ length: columns }).map((_, i) => (
                <col key={i} style={{ width: colWidths[i] ? colWidths[i] : undefined }} />
              ))}
            </colgroup>
          )}
          {hasHeader && (
            <thead>
              {rdlMatrixHeaders.map((header, index) => (
                <tr key={`rdl-matrix-header-${index}`}>
                  <th
                    colSpan={columns}
                    style={{
                      ...tdStyle(index, 0, 'header'),
                      textAlign: 'left',
                      backgroundColor: '#e0f2fe',
                      color: '#075985'
                    }}
                  >
                    {header}
                  </th>
                </tr>
              ))}
              <tr>
                {Array.from({ length: columns }).map((_, c) => (
                  <th key={c} style={tdStyle(0, c, 'header')}>
                    {cell(0, c) || `Header ${c + 1}`}
                  </th>
                ))}
              </tr>
            </thead>
          )}
          <tbody>
            {Array.from({ length: bodyRows }).map((_, r) => {
              const dataRow = r + (hasHeader ? 1 : 0);
              return (
                <tr key={r}>
                  {Array.from({ length: columns }).map((_, c) => (
                    <td key={c} style={tdStyle(r, c, 'body', dataRow)}>
                      {cell(dataRow, c) || 'Cell'}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
          {hasFooter && (
            <tfoot>
              <tr>
                {Array.from({ length: columns }).map((_, c) => (
                  <td key={c} style={tdStyle(0, c, 'footer', totalRows - 1)}>
                    {cell(totalRows - 1, c) || `Footer ${c + 1}`}
                  </td>
                ))}
              </tr>
            </tfoot>
          )}
        </table>
      );
    }

    if (element.type === 'button') {
      return (
        <button
          style={{
            backgroundColor: element.style?.backgroundColor || '#3b82f6',
            color: element.style?.color || '#ffffff',
            fontSize: element.style?.fontSize || 14,
            borderRadius: element.style?.borderRadius || 4,
            border: 'none',
            cursor: element.buttonAction ? 'pointer' : 'default',
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
          }}
          onClick={element.buttonAction ? () => {
            const a = element.buttonAction!;
            if (a.startsWith('http')) window.open(a, '_blank', 'noopener');
          } : undefined}
          title={element.buttonAction || undefined}
        >
          {element.content || 'Button'}
          {element.buttonAction && <FiLink2 size={12} style={{ opacity: 0.7 }} />}
        </button>
      );
    }

    if (element.type === 'dropdown') {
      return (
        <select
          style={{
            width: '100%',
            height: '100%',
            fontFamily: element.style?.fontFamily || 'sans-serif',
            fontSize: element.style?.fontSize || 14,
            color: element.style?.color || '#000000',
            backgroundColor: '#ffffff',
            border: '1px solid #d1d5db',
            borderRadius: '4px',
            padding: '0 8px'
          }}
          value={element.selectedValue || ''}
          multiple={!!element.multiSelect}
        >
          {(element.options || []).map((opt, index) => (
            <option key={index} value={opt}>{opt}</option>
          ))}
        </select>
      );
    }

    if (element.type === 'optionlist') {
      const style = element.listStyle || (element.ordered ? 'decimal' : 'disc');
      const isCustom = style === 'dash' || style === 'asterisk';
      const prefix = style === 'dash' ? '– ' : style === 'asterisk' ? '* ' : '';
      const baseStyle = {
        fontFamily: element.style?.fontFamily || 'sans-serif',
        fontSize: element.style?.fontSize || 14,
        color: element.style?.color || '#111827',
      };
      if (isCustom) {
        return (
          <div style={{ ...baseStyle, padding: '0 4px', margin: 0 }}>
            {(element.options || []).map((item, index) => (
              <div key={index} style={{ lineHeight: 1.6 }}>{prefix}{item}</div>
            ))}
          </div>
        );
      }
      const isOrdered = ['decimal', 'lower-alpha', 'upper-alpha', 'lower-roman', 'upper-roman'].includes(style);
      const ListTag = isOrdered ? 'ol' : 'ul';
      return (
        <ListTag
          start={isOrdered ? element.startNumber ?? 1 : undefined}
          style={{ ...baseStyle, listStyleType: style, paddingLeft: '20px', margin: 0 }}
        >
          {(element.options || []).map((item, index) => (
            <li key={index}>{item}</li>
          ))}
        </ListTag>
      );
    }

    if (element.type === 'radio') {
      return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          {(element.options || []).map((opt, index) => (
            <label key={index} style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input type="radio" name={element.id} defaultChecked={index === 0} />
              <span style={{
                fontFamily: element.style?.fontFamily || 'sans-serif',
                fontSize: element.style?.fontSize || 14,
                color: element.style?.color || '#000000'
              }}>{opt}</span>
            </label>
          ))}
        </div>
      );
    }

    if (element.type === 'chart') {
      const transformToRechartsData = (data: any) => {
        if (!data || !data.labels || !data.datasets) return [];
        return data.labels.map((label: string, index: number) => ({
          name: label,
          pv: data.datasets[0]?.data[index] || 0,
          uv: data.datasets[1]?.data[index] || 0
        }));
      };

      const pieColors = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2'];

      if (isPreview) {
        const chartData = transformToRechartsData(element.chartData || createDefaultChartData());
        const chartType = element.chartType || 'bar';

        return (
          <ResponsiveContainer width="100%" height="100%">
            {chartType === 'bar' && (
              <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="pv" fill="#8884d8" />
                <Bar dataKey="uv" fill="#82ca9d" />
              </BarChart>
            )}
            {chartType === 'line' && (
              <LineChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line type="monotone" dataKey="pv" stroke="#8884d8" />
                <Line type="monotone" dataKey="uv" stroke="#82ca9d" />
              </LineChart>
            )}
            {chartType === 'pie' && (
              <PieChart>
                <Pie
                  data={chartData}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  outerRadius={80}
                  fill="#8884d8"
                  dataKey="pv"
                >
                  {chartData.map((_: any, index: number) => (
                    <Cell key={`cell-${index}`} fill={pieColors[index % pieColors.length]} />
                  ))}
                </Pie>
                <Tooltip />
                <Legend />
              </PieChart>
            )}
          </ResponsiveContainer>
        );
      }

      const parsedChartData = element.chartData as
        | { labels?: unknown[]; datasets?: Array<{ data?: unknown[]; label?: string }> }
        | undefined;
      const labels = Array.isArray(parsedChartData?.labels)
        ? parsedChartData.labels.map((label) => String(label))
        : [];
      const firstDataset = Array.isArray(parsedChartData?.datasets)
        ? parsedChartData.datasets[0]
        : undefined;
      const values = Array.isArray(firstDataset?.data)
        ? firstDataset.data.map((value) => {
            const numericValue = Number(value);
            return Number.isFinite(numericValue) ? numericValue : 0;
          })
        : [];
      const hasValues = values.length > 0;
      const maxValue = hasValues ? Math.max(...values, 1) : 1;
      const totalValue = hasValues
        ? values.reduce((sum, value) => sum + Math.max(value, 0), 0)
        : 0;

      const renderPieSlicePath = (startAngle: number, endAngle: number, radius: number) => {
        const centerX = 50;
        const centerY = 50;
        const startX = centerX + radius * Math.cos(startAngle);
        const startY = centerY + radius * Math.sin(startAngle);
        const endX = centerX + radius * Math.cos(endAngle);
        const endY = centerY + radius * Math.sin(endAngle);
        const largeArcFlag = endAngle - startAngle > Math.PI ? 1 : 0;

        return `M ${centerX} ${centerY} L ${startX} ${startY} A ${radius} ${radius} 0 ${largeArcFlag} 1 ${endX} ${endY} Z`;
      };

      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            border: '1px solid #dbe7ff',
            borderRadius: 8,
            background: 'linear-gradient(180deg, #f8fbff 0%, #ffffff 100%)',
            padding: 8,
            display: 'flex',
            flexDirection: 'column',
            gap: 8
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 11, fontWeight: 700, color: '#1e3a8a' }}>
              {firstDataset?.label || t('diagnostics.chart')}
            </span>
            <small style={{ fontSize: 10, color: '#475569' }}>{element.chartType || 'bar'}</small>
          </div>

          {!hasValues && (
            <div className="editor-placeholder editor-placeholder-wide" style={{ flex: 1 }}>
              <FiLayers className="editor-placeholder-icon" />
              <span>{t('diagnostics.noChartData')}</span>
            </div>
          )}

          {hasValues && (element.chartType || 'bar') === 'bar' && (
            <div
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'flex-end',
                gap: 6,
                borderBottom: '1px solid #cbd5e1',
                paddingBottom: 4
              }}
            >
              {values.map((value, index) => {
                const heightPercent = `${(Math.max(value, 0) / maxValue) * 100}%`;
                return (
                  <div key={`${index}-${value}`} style={{ flex: 1, minWidth: 0 }}>
                    <div
                      style={{
                        width: '100%',
                        height: heightPercent,
                        minHeight: 2,
                        borderRadius: '4px 4px 0 0',
                        backgroundColor: '#3b82f6'
                      }}
                    />
                    <div
                      style={{
                        marginTop: 4,
                        fontSize: 9,
                        color: '#64748b',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis'
                      }}
                    >
                      {labels[index] || `#${index + 1}`}
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {hasValues && (element.chartType || 'bar') === 'line' && (
            <svg viewBox="0 0 100 54" style={{ flex: 1, width: '100%', height: '100%' }}>
              <line x1="0" y1="50" x2="100" y2="50" stroke="#cbd5e1" strokeWidth="1" />
              <polyline
                fill="none"
                stroke="#2563eb"
                strokeWidth="2"
                points={values
                  .map((value, index) => {
                    const x = values.length > 1 ? (index / (values.length - 1)) * 96 + 2 : 50;
                    const y = 50 - (Math.max(value, 0) / maxValue) * 42;
                    return `${x},${y}`;
                  })
                  .join(' ')}
              />
              {values.map((value, index) => {
                const x = values.length > 1 ? (index / (values.length - 1)) * 96 + 2 : 50;
                const y = 50 - (Math.max(value, 0) / maxValue) * 42;
                return <circle key={`${index}-${value}`} cx={x} cy={y} r="2.1" fill="#1d4ed8" />;
              })}
            </svg>
          )}

          {hasValues && (element.chartType || 'bar') === 'pie' && (
            <svg viewBox="0 0 100 100" style={{ flex: 1, width: '100%', height: '100%' }}>
              {values.map((value, index) => {
                const safeValue = Math.max(value, 0);
                const previousTotal = values
                  .slice(0, index)
                  .reduce((sum, item) => sum + Math.max(item, 0), 0);
                const startAngle = totalValue > 0 ? (previousTotal / totalValue) * Math.PI * 2 - Math.PI / 2 : 0;
                const endAngle = totalValue > 0
                  ? ((previousTotal + safeValue) / totalValue) * Math.PI * 2 - Math.PI / 2
                  : 0;

                return (
                  <path
                    key={`${index}-${value}`}
                    d={renderPieSlicePath(startAngle, endAngle, 36)}
                    fill={pieColors[index % pieColors.length]}
                    stroke="#ffffff"
                    strokeWidth="1"
                  />
                );
              })}
            </svg>
          )}
        </div>
      );
    }

    if (element.type === 'watermark') {
      const isImage = element.watermarkMode === 'image';
      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            overflow: 'hidden',
            opacity: element.style?.opacity ?? 0.18,
            transform: `rotate(${element.style?.rotation ?? -24}deg) scale(${element.style?.scale ?? 1})`,
            pointerEvents: 'none'
          }}
        >
          {isImage ? (
            <img
              src={element.content || 'https://via.placeholder.com/260x80'}
              alt=""
              style={{ width: '100%', height: '100%', objectFit: 'contain' }}
            />
          ) : (
            <span
              style={{
                color: element.style?.color || '#64748b',
                fontSize: element.style?.fontSize || 42,
                fontWeight: element.style?.fontWeight || 'bold',
                letterSpacing: 2,
                whiteSpace: 'nowrap',
                textTransform: 'uppercase'
              }}
            >
              {element.content || 'WATERMARK'}
            </span>
          )}
        </div>
      );
    }

    if (element.type === 'note') {
      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            padding: 10,
            background: element.style?.backgroundColor || '#fef3c7',
            color: element.style?.color || '#78350f',
            border: '1px solid rgba(146, 64, 14, 0.2)',
            borderRadius: 6,
            boxShadow: '0 8px 20px rgb(0 0 0 / 0.12)',
            overflow: 'hidden'
          }}
        >
          <strong style={{ display: 'block', fontSize: 12, marginBottom: 6 }}>{element.noteTitle || 'Notiz'}</strong>
          {!element.noteCollapsed && (
            <>
              <p style={{ margin: 0, fontSize: 11, lineHeight: 1.35 }}>{element.noteBody || 'Kommentar eingeben'}</p>
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
      const isElbow = element.arrowMode === 'elbow';
      const isCurved = element.arrowMode === 'curved';
      // Paths pulled 12 units in from each edge to leave room for markers
      const path = isCurved
        ? 'M 12 50 C 36 6, 64 94, 88 50'
        : isElbow
          ? 'M 12 78 L 50 78 L 50 22 L 88 22'
          : 'M 12 50 L 88 50';

      const dirDeg = { right: 0, left: 180, down: 90, up: -90 }[element.arrowDirection || 'right'] ?? 0;
      const totalDeg = dirDeg + (element.arrowRotation || 0);

      const eid = element.id;
      const resolveMarker = (marker: string | undefined, isStart: boolean) => {
        const side = isStart ? 's' : 'e';
        if (!marker || marker === 'none') return undefined;
        if (marker === 'filled' || marker === 'arrow') return `url(#af-${side}-${eid})`;
        if (marker === 'open')    return `url(#ao-${side}-${eid})`;
        if (marker === 'dot')     return `url(#ad-${side}-${eid})`;
        if (marker === 'diamond') return `url(#am-${side}-${eid})`;
        if (marker === 'square')  return `url(#aq-${side}-${eid})`;
        if (marker === 'circle')  return `url(#ac-${side}-${eid})`;
        return undefined;
      };

      // markerUnits="userSpaceOnUse" → sizes are in viewBox coords, not stroke-width multiples
      // orient="auto-start-reverse" on -s markers → start arrows point away from the path start
      return (
        <svg
          viewBox="0 0 100 100"
          width="100%"
          height="100%"
          preserveAspectRatio="none"
          style={{ overflow: 'visible', ...(totalDeg !== 0 ? { transform: `rotate(${totalDeg}deg)`, transformOrigin: '50% 50%' } : {}) }}
        >
          <defs>
            {(['e', 's'] as const).map(side => {
              const orient = side === 's' ? 'auto-start-reverse' : 'auto';
              return (
                <React.Fragment key={side}>
                  <marker id={`af-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient={orient}>
                    <path d="M 0 0 L 12 6 L 0 12 z" fill={color} />
                  </marker>
                  <marker id={`ao-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient={orient}>
                    <path d="M 0 0 L 12 6 L 0 12" fill="none" stroke={color} strokeWidth={Math.max(1, sw * 0.5)} />
                  </marker>
                  <marker id={`ad-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5">
                    <circle cx="5" cy="5" r="4" fill={color} />
                  </marker>
                  <marker id={`am-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="6" refY="6" orient={orient}>
                    <path d="M 0 6 L 6 0 L 12 6 L 6 12 z" fill={color} />
                  </marker>
                  <marker id={`aq-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5" orient={orient}>
                    <rect x="0" y="0" width="10" height="10" fill={color} />
                  </marker>
                  <marker id={`ac-${side}-${eid}`} markerUnits="userSpaceOnUse" markerWidth="10" markerHeight="10" refX="5" refY="5">
                    <circle cx="5" cy="5" r="4" fill="none" stroke={color} strokeWidth={Math.max(1, sw * 0.5)} />
                  </marker>
                </React.Fragment>
              );
            })}
          </defs>
          <path
            d={path}
            fill="none"
            stroke={color}
            strokeWidth={sw}
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeDasharray={dashArray}
            markerStart={resolveMarker(element.startMarker, true)}
            markerEnd={resolveMarker(element.endMarker, false)}
          />
        </svg>
      );
    }

    if (element.type === 'link') {
      return (
        <div style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          alignItems: 'center',
          color: element.style?.color || '#2563eb',
          fontSize: element.style?.fontSize || 14,
          fontFamily: element.style?.fontFamily || 'sans-serif',
          fontWeight: element.style?.fontWeight || 'normal',
          textDecoration: 'underline',
          cursor: 'pointer',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          textOverflow: 'ellipsis',
        }}>
          {element.content || element.href || 'Link text'}
        </div>
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
        <div style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          alignItems: 'center',
          color: element.style?.color || '#111827',
          fontSize: element.style?.fontSize || 18,
          fontFamily: element.style?.fontFamily || 'sans-serif',
          fontWeight: element.style?.fontWeight || 'bold',
          overflow: 'hidden',
        }}>
          {(element.prefix || '') + formatted + (element.suffix || '')}
        </div>
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
            strokeLinecap="round"
            strokeLinejoin="round"
            opacity={element.style?.opacity ?? (element.drawTool === 'highlighter' ? 0.45 : 1)}
          />
        </svg>
      );
    }

    if (element.type === 'date') {
      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            color: element.style?.color || '#111827',
            fontSize: element.style?.fontSize || 14,
            fontWeight: element.style?.fontWeight || 'normal'
          }}
        >
          {getDatePreview(element)}
        </div>
      );
    }

    if (element.type === 'highlight') {
      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            background: element.style?.backgroundColor || '#fde047',
            opacity: element.style?.opacity ?? 0.45,
            borderRadius: element.style?.borderRadius ?? 4,
            mixBlendMode: element.style?.blendMode || 'multiply'
          }}
        />
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
          padding: 8,
          color,
          fontSize: element.style?.fontSize || 12,
          background: element.style?.backgroundColor || '#f8fafc',
          border: `${element.style?.borderWidth || 1}px ${element.style?.borderStyle || 'dashed'} ${color}`,
          borderRadius: element.style?.borderRadius ?? 4,
          overflow: 'hidden',
          textAlign: 'center',
        }}>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.content || ELEMENT_TYPE_LABELS[element.type] || element.type}
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
            {state === 'cross' && <path d="M 8 8 L 18 18 M 18 8 L 8 18" fill="none" stroke={color} strokeWidth={element.style?.strokeWidth || 3} strokeLinecap="round" />}
            {state === 'dot' && <circle cx="13" cy="13" r="5" fill={color} />}
          </svg>
          <span style={{ fontSize: element.style?.fontSize || 14, color: element.style?.labelColor || '#374151' }}>
            {resolveContent(element.fieldLabel) || 'Selection'}
          </span>
        </div>
      );
    }

    if (element.type === 'pageboundary') {
      const color = element.style?.color || '#7c3aed';
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 8, color }}>
          <div style={{ flex: 1, borderTop: `2px ${element.style?.dashStyle || 'dashed'} ${color}` }} />
          <strong style={{ fontSize: 10, textTransform: 'uppercase', letterSpacing: 1 }}>
            {element.pageBoundaryMode === 'end' ? 'Page end' : 'Page start'}
          </strong>
          <div style={{ flex: 1, borderTop: `2px ${element.style?.dashStyle || 'dashed'} ${color}` }} />
        </div>
      );
    }

    if (element.type === 'pagenumber') {
      return (
        <div
          style={{
            width: '100%',
            height: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: element.style?.color || '#374151',
            fontSize: element.style?.fontSize || 12
          }}
        >
          {getPageNumberPreview(element)}
        </div>
      );
    }

    if (element.type === 'footnote' || element.type === 'endnote') {
      const label = element.type === 'footnote' ? '†' : '‡';
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', fontSize: 12, color: '#6366f1', borderLeft: '3px solid #6366f1', overflow: 'hidden' }}>
          <span style={{ fontWeight: 700 }}>{label}</span>
          <span style={{ opacity: 0.75, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.footnoteText || `${element.type === 'footnote' ? 'Footnote' : 'Endnote'}…`}
          </span>
        </div>
      );
    }

    if (element.type === 'bookmark') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', fontSize: 12, color: '#0891b2' }}>
          <FiBookmark size={12} />
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.bookmarkName || 'bookmark'}
          </span>
        </div>
      );
    }

    if (element.type === 'comment') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', padding: 6, fontSize: 11, background: '#fef9c3', border: '1px solid #fde047', borderRadius: 4, overflow: 'hidden' }}>
          <span style={{ fontWeight: 600, color: '#92400e', marginBottom: 2 }}>{element.commentAuthor || 'Comment'}</span>
          <span style={{ color: '#78350f', overflow: 'hidden', textOverflow: 'ellipsis' }}>{element.commentText || '…'}</span>
        </div>
      );
    }

    if (element.type === 'contentcontrol') {
      return (
        <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', fontSize: 12, color: '#7c3aed', border: '1px dashed #a78bfa', borderRadius: 3, overflow: 'hidden' }}>
          <FiCode size={12} />
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {element.contentControlTitle || element.contentControlTag || element.contentControlType || 'Content Control'}
          </span>
        </div>
      );
    }

    if (element.type === 'toc') {
      const fontSize = element.style?.fontSize || 13;
      const color = element.style?.color || '#1f2937';
      const entries = element.tocEntries ?? [];
      return (
        <div style={{ width: '100%', height: '100%', overflow: 'hidden', padding: '8px 10px', border: '1px dashed #94a3b8', borderRadius: 4, background: '#f8fafc' }}>
          <div style={{ fontSize: 10, fontWeight: 700, color: '#64748b', letterSpacing: '0.06em', marginBottom: 6, textTransform: 'uppercase' }}>
            Table of Contents
          </div>
          {entries.length === 0 ? (
            <div style={{ fontSize: 11, color: '#94a3b8', fontStyle: 'italic' }}>
              No headings found. Select a text element and set a Heading Level in the inspector.
            </div>
          ) : entries.map((e, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'baseline', gap: 4, marginBottom: 3,
              paddingLeft: (e.level - 1) * 12, fontSize: e.level === 1 ? fontSize : fontSize - 1,
              fontWeight: e.level === 1 ? 600 : 400, color
            }}>
              <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{e.text}</span>
              <span style={{ flexShrink: 0, borderBottom: '1px dotted #cbd5e1', flex: 1, alignSelf: 'center', margin: '0 4px' }} />
              <span style={{ flexShrink: 0 }}>{e.page}</span>
            </div>
          ))}
        </div>
      );
    }

    return (
      <div className="editor-placeholder">
        <FiBox className="editor-placeholder-icon" />
        <span>{element.type}</span>
      </div>
    );
  };

  return (
    <div className="editor-shell">
      <div className="editor-mobile-overlay" aria-hidden="true">
        <div className="editor-mobile-overlay-inner">
          <span className="editor-mobile-overlay-icon">🖥</span>
          <h2>{t('mobileOverlay.title')}</h2>
          <p>{t('mobileOverlay.body')}</p>
        </div>
      </div>

      <header className="editor-topbar">
        <div className="editor-brand">
          <button className="editor-icon-button" onClick={onBack} aria-label={t('topbar.backToGallery')} title={t('topbar.backToGallery')}>
            <FiArrowLeft />
          </button>
          <div>
            <div className="editor-kicker">{t('topbar.kicker')}</div>
            <h1>{template.name}</h1>
          </div>
          <div className="editor-brand-menu-wrap">
            <button
              className="editor-icon-button"
              title={t('topbar.designActions')}
              onClick={() => setTopbarMenuOpen(v => !v)}
              aria-label={t('topbar.designActions')}
            >
              <FiMoreVertical size={15} />
            </button>
            {topbarMenuOpen && (
              <>
                <div className="editor-brand-menu-backdrop" onClick={() => setTopbarMenuOpen(false)} />
                <div className="editor-brand-menu">
                  <button onClick={handleCloneDesign}>
                    <FiCopy size={13} />
                    {t('topbar.cloneDesign')}
                  </button>
                  {onCreateVersion && (
                    <button
                      disabled={templateActionPending || autosaveState !== 'saved'}
                      onClick={() => void runTemplateAction(onCreateVersion)}
                    >
                      <FiBookmark size={13} />
                      Create version
                    </button>
                  )}
                  {onPublish && (
                    <button
                      disabled={templateActionPending || autosaveState !== 'saved'}
                      onClick={() => void runTemplateAction(onPublish)}
                    >
                      <FiCheck size={13} />
                      Publish
                    </button>
                  )}
                  {onArchive && (
                    <button
                      disabled={templateActionPending || autosaveState !== 'saved'}
                      onClick={() => void runTemplateAction(onArchive)}
                    >
                      <FiTrash2 size={13} />
                      Archive
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        </div>

        <div className="editor-topbar-actions">
          {autosaveState !== 'idle' && (
            <div
              className={`editor-save-status is-${autosaveState}`}
              role="status"
              aria-live="polite"
              title={autosaveMessage}
            >
              <span aria-hidden="true" />
              {autosaveMessage}
            </div>
          )}
          <div className="editor-status-pill">
            <FiMonitor />
            <span>{t('topbar.pageDimensions', { width: pageWidth, height: pageHeight })}</span>
          </div>
          <div className="editor-undo-redo">
            <button
              className="editor-icon-button"
              title={t('topbar.undo')}
              onClick={undo}
            >
              <FiRefreshCw style={{ transform: 'scaleX(-1)' }} />
            </button>
            <button
              className="editor-icon-button"
              title={t('topbar.redo')}
              onClick={redo}
            >
              <FiRefreshCw />
            </button>
          </div>
          <motion.button
            className={`editor-icon-button ${settingsModifiedSinceExport ? 'editor-icon-button--pending' : ''}`}
            title={settingsModifiedSinceExport ? t('topbar.pageSettingsPending') : t('topbar.pageSettings')}
            onClick={() => setSelectedElementId(null)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiSettings />
            {settingsModifiedSinceExport && <span className="editor-pending-dot" aria-hidden="true" />}
          </motion.button>
          <motion.button
            className="editor-icon-button"
            title={t('topbar.findReplace')}
            onClick={() => setFindReplaceOpen(true)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiSearch />
          </motion.button>
          <motion.button
            className="editor-icon-button"
            title={t('topbar.exportCode')}
            onClick={() => setCodeViewerOpen(true)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiCode />
          </motion.button>
          <div className="editor-doc-mode-toggle" title={t('topbar.outputModeTitle')}>
            <button
              className={`editor-doc-mode-btn${documentMode === 'pdf' ? ' editor-doc-mode-btn--active' : ''}`}
              onClick={() => setDocumentMode('pdf')}
            >{t('topbar.pdf')}</button>
            <button
              className={`editor-doc-mode-btn${documentMode === 'word' ? ' editor-doc-mode-btn--active' : ''}`}
              onClick={() => setDocumentMode('word')}
            >{t('topbar.word')}</button>
          </div>
          <motion.button
            className="editor-icon-button"
            title={t('topbar.help')}
            onClick={() => setHelpModalOpen(true)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiHelpCircle />
          </motion.button>
          <motion.button
            className="editor-primary-button"
            onClick={onPreview}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiEye />
            <span>{t('topbar.preview')}</span>
          </motion.button>
        </div>
      </header>

      <main
        ref={workspaceRef}
        className="editor-workspace"
      >
        <aside className="editor-panel editor-tool-panel" aria-label="Element tools">
          <div className="editor-panel-heading">
            <FiPlus />
            <span>{t('toolPanel.addElements')}</span>
          </div>

          {documentMode === 'pdf' && wordElementsOnSurface && (
            <div className="editor-doc-mode-warning">
              {t('toolPanel.wordOnlyWarning')}
            </div>
          )}
          <div className="editor-tool-list">
            {visibleToolGroups.map(group => {
              const isExpanded = expandedGroups.includes(group.id);

              return (
                <section key={group.id} className="editor-tool-group">
                  <button
                    type="button"
                    className="editor-tool-group-toggle"
                    onClick={() => toggleGroup(group.id)}
                    aria-expanded={isExpanded}
                  >
                    <span className="editor-tool-group-title">{group.label}</span>
                    <span className="editor-tool-group-meta">
                      <span className="editor-tool-group-count">{group.toolIds.length}</span>
                      <span className="editor-tool-group-chevron">{isExpanded ? '−' : '+'}</span>
                    </span>
                  </button>

                  {isExpanded && (
                    <div className="editor-tool-group-body">
                      {group.toolIds.map(toolId => {
                        const tool = toolsById[toolId];
                        if (!tool) return null;

                        const Icon = tool.icon;

                        return (
                          <motion.button
                            key={tool.id}
                            className={`editor-tool-button${drawingMode === tool.id ? ' is-draw-active' : ''}`}
                            onClick={() => {
                              if (tool.id === 'line' || tool.id === 'arrow' || tool.id === 'draw') {
                                setDrawingMode(prev => (prev === tool.id ? null : tool.id as 'line' | 'arrow' | 'draw'));
                                clearSelection();
                              } else {
                                addElement(tool);
                              }
                            }}
                            draggable
                            onDragStartCapture={(event) => handleToolDragStart(event, tool)}
                            whileHover={{ x: 2 }}
                            whileTap={{ scale: 0.98 }}
                          >
                            <span className="editor-tool-icon">
                              <Icon />
                            </span>
                            <span>
                              <strong>{tool.label}</strong>
                              <small>{tool.hint}</small>
                            </span>
                          </motion.button>
                        );
                      })}
                    </div>
                  )}
                </section>
              );
            })}
          </div>

          <button
            className="editor-form-block-btn"
            onClick={() => setFormBlockModalOpen(true)}
            title={t('toolPanel.insertFormBlockTitle')}
          >
            <FiGrid size={14} />
            {t('toolPanel.insertFormBlock')}
          </button>

          <div className="editor-layer-summary">
            <div>
              <FiLayers />
              <span>{t('toolPanel.layers')}</span>
            </div>
            <strong>{elements.length}</strong>
          </div>
        </aside>

        <section ref={stageRef} className="editor-stage" aria-label={t('canvasStage.ariaLabel')}>
          <LanguageTabBar />
          <div className="editor-stage-header">
            <div>
              <span>{t('canvasStage.pageOfTotal', { current: currentPageIndex + 1, total: pages.length })}</span>
              <strong>{t('canvasStage.pageDimensions', { width: pageWidth, height: pageHeight })}</strong>
              {(() => {
                const a4 = PAGE_PRESETS['A4'];
                const isA4 = pageWidth === a4.width && pageHeight === a4.height;
                if (isA4) return null;
                const preset = Object.entries(PAGE_PRESETS).find(([, p]) => p.width === pageWidth && p.height === pageHeight);
                return (
                  <span className="editor-page-size-badge">{preset ? preset[0] : t('canvasStage.customSize')}</span>
                );
              })()}
            </div>
            <div className="editor-stage-zoom">
              <button
                className="editor-zoom-btn"
                title={t('canvasStage.zoomOut')}
                onClick={() => setZoomLevel(z => Math.max(0.25, parseFloat((z - 0.25).toFixed(2))))}
              >
                <FiZoomOut />
              </button>
              <span>{Math.round(zoomLevel * 100)}%</span>
              <button
                className="editor-zoom-btn"
                title={t('canvasStage.zoomIn')}
                onClick={() => setZoomLevel(z => Math.min(2, parseFloat((z + 0.25).toFixed(2))))}
              >
                <FiZoomIn />
              </button>
            </div>
          </div>

          {drawingMode && (
            <div className="editor-draw-badge">
              {drawingMode === 'line' ? t('canvasStage.drawingLine') : drawingMode === 'arrow' ? t('canvasStage.drawingArrow') : t('canvasStage.freehandDrawing')} {t('canvasStage.drawInstructions')} &nbsp;·&nbsp; <kbd>Esc</kbd> {t('canvasStage.cancelHint')}
            </div>
          )}

          <div
            ref={pageViewportRef}
            className="editor-page-viewport"
            tabIndex={0}
            dir="ltr"
            aria-label={t('canvasStage.ariaLabel')}
          >
            <div className="editor-page-scroll-content">
              <div
                className="editor-page-scale-frame"
                style={{
                  width: pageWidth * zoomLevel,
                  height: pageHeight * zoomLevel,
                }}
              >
                <div
                  className={`editor-page ${isDragOverSurface ? 'is-drag-over' : ''}`}
                  style={{
                    width: pageWidth,
                    height: pageHeight,
                    backgroundColor: pageSettings.backgroundColor,
                    ...(pageSettings.backgroundImage ? {
                      backgroundImage: `url(${pageSettings.backgroundImage})`,
                      backgroundRepeat: pageSettings.backgroundImageFit === 'tile' ? 'repeat' : 'no-repeat',
                      backgroundSize: pageSettings.backgroundImageFit === 'fill' ? '100% 100%'
                        : pageSettings.backgroundImageFit === 'tile' ? 'auto'
                        : pageSettings.backgroundImageFit,
                      backgroundPosition: 'center',
                    } : {}),
                    transform: `scale(${zoomLevel})`,
                    transformOrigin: 'top left',
                    cursor: drawingMode ? 'crosshair' : undefined,
                  }}
                  onPointerDown={handleSurfacePointerDown}
                  onContextMenu={handleSurfaceContextMenu}
                  onDragOver={(event) => {
                    event.preventDefault();
                    event.dataTransfer.dropEffect = 'copy';
                    setIsDragOverSurface(true);
                  }}
                  onDragLeave={(event) => {
                    if (event.currentTarget === event.target) {
                      setIsDragOverSurface(false);
                    }
                  }}
                  onDrop={handleSurfaceDrop}
                  role="presentation"
                >
            {pageSettings.gridVisible && (
              <div
                className="editor-page-grid"
                style={{ backgroundSize: `${pageSettings.gridSize}px ${pageSettings.gridSize}px` }}
              />
            )}
            <div className="editor-page-content" ref={pageContentRef}>
              {/* Margin safe-zone guide */}
              {pageSettings.showMarginGuide && (
                <div
                  className="editor-margin-guide"
                  style={{
                    top: pageSettings.margins.top,
                    left: pageSettings.margins.left,
                    right: pageSettings.margins.right,
                    bottom: pageSettings.margins.bottom,
                  }}
                />
              )}
              {/* Safe area guide (8 px inside the margin) */}
              {pageSettings.showSafeArea && (
                <div
                  className="editor-safe-area-guide"
                  style={{
                    top: pageSettings.margins.top + 8,
                    left: pageSettings.margins.left + 8,
                    right: pageSettings.margins.right + 8,
                    bottom: pageSettings.margins.bottom + 8,
                  }}
                />
              )}
              {/* Bleed trim-line guide */}
              {pageSettings.bleedSize > 0 && (
                <div
                  className="editor-bleed-guide"
                  style={{
                    top: pageSettings.bleedSize,
                    left: pageSettings.bleedSize,
                    right: pageSettings.bleedSize,
                    bottom: pageSettings.bleedSize,
                  }}
                />
              )}
              {/* Header area band */}
              {pageSettings.headerEnabled && (
                <div
                  className="editor-header-guide"
                  style={{ height: pageSettings.headerHeight }}
                >
                  <span className="editor-guide-label">{t('canvasStage.headerGuide')}</span>
                </div>
              )}
              {/* Footer area band */}
              {pageSettings.footerEnabled && (
                <div
                  className="editor-footer-guide"
                  style={{
                    height: pageSettings.footerHeight,
                    top: pageHeight - pageSettings.footerHeight,
                  }}
                >
                  <span className="editor-guide-label">{t('canvasStage.footerGuide')}</span>
                </div>
              )}
              {/* Crop marks at page corners */}
              {pageSettings.cropMarks && (
                <svg
                  style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', pointerEvents: 'none', overflow: 'visible', zIndex: 20 }}
                  viewBox={`0 0 ${pageWidth} ${pageHeight}`}
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <line x1="8" y1="0" x2="0" y2="0" stroke="#000" strokeWidth="0.6" />
                  <line x1="0" y1="0" x2="0" y2="8" stroke="#000" strokeWidth="0.6" />
                  <line x1={pageWidth - 8} y1="0" x2={pageWidth} y2="0" stroke="#000" strokeWidth="0.6" />
                  <line x1={pageWidth} y1="0" x2={pageWidth} y2="8" stroke="#000" strokeWidth="0.6" />
                  <line x1="0" y1={pageHeight - 8} x2="0" y2={pageHeight} stroke="#000" strokeWidth="0.6" />
                  <line x1="0" y1={pageHeight} x2="8" y2={pageHeight} stroke="#000" strokeWidth="0.6" />
                  <line x1={pageWidth} y1={pageHeight - 8} x2={pageWidth} y2={pageHeight} stroke="#000" strokeWidth="0.6" />
                  <line x1={pageWidth - 8} y1={pageHeight} x2={pageWidth} y2={pageHeight} stroke="#000" strokeWidth="0.6" />
                </svg>
              )}
              {/* Global watermark overlay */}
              {pageSettings.globalWatermark.enabled && pageSettings.globalWatermark.content && (
                <div
                  style={{
                    position: 'absolute',
                    inset: 0,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    pointerEvents: 'none',
                    zIndex: 19,
                    overflow: 'hidden',
                  }}
                >
                  {pageSettings.globalWatermark.mode === 'text' ? (
                    <span
                      style={{
                        fontSize: pageSettings.globalWatermark.fontSize,
                        color: pageSettings.globalWatermark.color,
                        opacity: pageSettings.globalWatermark.opacity,
                        transform: `rotate(${pageSettings.globalWatermark.rotation}deg) scale(${pageSettings.globalWatermark.scale})`,
                        userSelect: 'none',
                        whiteSpace: 'nowrap',
                        fontWeight: 700,
                        letterSpacing: 2,
                      }}
                    >
                      {pageSettings.globalWatermark.content}
                    </span>
                  ) : (
                    <img
                      src={pageSettings.globalWatermark.content}
                      alt={t('canvasStage.watermarkAlt')}
                      style={{
                        maxWidth: '60%',
                        maxHeight: '60%',
                        opacity: pageSettings.globalWatermark.opacity,
                        transform: `rotate(${pageSettings.globalWatermark.rotation}deg) scale(${pageSettings.globalWatermark.scale})`,
                        objectFit: 'contain',
                        userSelect: 'none',
                      }}
                    />
                  )}
                </div>
              )}
              {elements
                .filter(el => !el.elementLanguage || el.elementLanguage === currentPreviewLanguage)
                .map((element, index) => (
                <motion.div
                  key={element.id}
                  initial={{ opacity: 0, scale: 0.96, y: 16 }}
                  animate={{ opacity: 1, scale: 1, y: 0, rotate: getEffectiveRotation(element) }}
                  transition={{ duration: 0.24, delay: index * 0.04, rotate: { duration: 0 } }}
                  className={`editor-canvas-element ${selectedElementId === element.id ? 'is-selected' : selectedElementIds.has(element.id) ? 'is-multi-selected' : ''} ${element.locked ? 'is-locked' : ''} ${element.hidden ? 'is-hidden' : ''}`}
                  style={(() => { const ep = getEffectivePos(element); return {
                    left: isCurrentRtl ? pageWidth - ep.x - ep.width : ep.x,
                    top: ep.y, width: ep.width, height: ep.height,
                  }; })()}
                  onPointerDown={(event) => handleElementPointerDown(event, element)}
                  onContextMenu={(event) => handleElementContextMenu(event, element)}
                  onClick={(event) => {
                    event.stopPropagation();
                    setSelectedElementId(element.id);
                  }}
                >
                  <ElementBoundary name={element.name}>{renderElement(element, false)}</ElementBoundary>
                  {selectedElementId === element.id && !element.locked && (
                    <>
                      {([
                        { handle: 'nw' as ResizeHandle, style: { top: -4, left: -4, cursor: 'nw-resize' } },
                        { handle: 'n'  as ResizeHandle, style: { top: -4, left: 'calc(50% - 4px)', cursor: 'n-resize' } },
                        { handle: 'ne' as ResizeHandle, style: { top: -4, right: -4, cursor: 'ne-resize' } },
                        { handle: 'e'  as ResizeHandle, style: { top: 'calc(50% - 4px)', right: -4, cursor: 'e-resize' } },
                        { handle: 'se' as ResizeHandle, style: { bottom: -4, right: -4, cursor: 'se-resize' } },
                        { handle: 's'  as ResizeHandle, style: { bottom: -4, left: 'calc(50% - 4px)', cursor: 's-resize' } },
                        { handle: 'sw' as ResizeHandle, style: { bottom: -4, left: -4, cursor: 'sw-resize' } },
                        { handle: 'w'  as ResizeHandle, style: { top: 'calc(50% - 4px)', left: -4, cursor: 'w-resize' } },
                      ]).map(({ handle, style }) => (
                        <div
                          key={handle}
                          className="editor-resize-handle"
                          style={style}
                          onPointerDown={(event) => handleResizePointerDown(event, element, handle)}
                        />
                      ))}
                      <div style={{ position: 'absolute', top: -28, left: 'calc(50% - 1px)', width: 1, height: 24, background: 'var(--editor-accent, #6366f1)', opacity: 0.5, pointerEvents: 'none' }} />
                      <div
                        style={{ position: 'absolute', top: -40, left: 'calc(50% - 7px)', width: 14, height: 14, borderRadius: '50%', background: 'var(--editor-accent, #6366f1)', border: '2px solid white', cursor: 'crosshair', boxShadow: '0 1px 3px rgba(0,0,0,0.3)' }}
                        onPointerDown={(event) => handleRotatePointerDown(event, element)}
                      />
                    </>
                  )}
                </motion.div>
              ))}

              {/* Shared elements (header / footer — appear on all pages) */}
              {sharedElements.map((element, index) => (
                <motion.div
                  key={element.id}
                  initial={{ opacity: 0, scale: 0.96, y: 16 }}
                  animate={{ opacity: 1, scale: 1, y: 0, rotate: getEffectiveRotation(element) }}
                  transition={{ duration: 0.24, delay: index * 0.04, rotate: { duration: 0 } }}
                  className={`editor-canvas-element is-shared ${selectedElementId === element.id ? 'is-selected' : ''} ${element.locked ? 'is-locked' : ''} ${element.hidden ? 'is-hidden' : ''}`}
                  style={(() => { const ep = getEffectivePos(element); return { left: isCurrentRtl ? pageWidth - ep.x - ep.width : ep.x, top: ep.y, width: ep.width, height: ep.height }; })()}
                  onPointerDown={(event) => handleElementPointerDown(event, element)}
                  onContextMenu={(event) => handleElementContextMenu(event, element)}
                  onClick={(event) => { event.stopPropagation(); setSelectedElementId(element.id); }}
                >
                  <ElementBoundary name={element.name}>{renderElement(element, false)}</ElementBoundary>
                  {selectedElementId === element.id && !element.locked && (
                    <>
                      {([
                        { handle: 'nw' as ResizeHandle, style: { top: -4, left: -4, cursor: 'nw-resize' } },
                        { handle: 'n'  as ResizeHandle, style: { top: -4, left: 'calc(50% - 4px)', cursor: 'n-resize' } },
                        { handle: 'ne' as ResizeHandle, style: { top: -4, right: -4, cursor: 'ne-resize' } },
                        { handle: 'e'  as ResizeHandle, style: { top: 'calc(50% - 4px)', right: -4, cursor: 'e-resize' } },
                        { handle: 'se' as ResizeHandle, style: { bottom: -4, right: -4, cursor: 'se-resize' } },
                        { handle: 's'  as ResizeHandle, style: { bottom: -4, left: 'calc(50% - 4px)', cursor: 's-resize' } },
                        { handle: 'sw' as ResizeHandle, style: { bottom: -4, left: -4, cursor: 'sw-resize' } },
                        { handle: 'w'  as ResizeHandle, style: { top: 'calc(50% - 4px)', left: -4, cursor: 'w-resize' } },
                      ]).map(({ handle, style }) => (
                        <div key={handle} className="editor-resize-handle" style={style}
                          onPointerDown={(event) => handleResizePointerDown(event, element, handle)} />
                      ))}
                      <div style={{ position: 'absolute', top: -28, left: 'calc(50% - 1px)', width: 1, height: 24, background: 'var(--editor-accent, #6366f1)', opacity: 0.5, pointerEvents: 'none' }} />
                      <div
                        style={{ position: 'absolute', top: -40, left: 'calc(50% - 7px)', width: 14, height: 14, borderRadius: '50%', background: 'var(--editor-accent, #6366f1)', border: '2px solid white', cursor: 'crosshair', boxShadow: '0 1px 3px rgba(0,0,0,0.3)' }}
                        onPointerDown={(event) => handleRotatePointerDown(event, element)}
                      />
                    </>
                  )}
                </motion.div>
              ))}

              {elements.length === 0 && sharedElements.length === 0 && (
                <div className="editor-empty-state">
                  <span>
                    <FiMousePointer />
                  </span>
                  <h2>{t('emptyState.title')}</h2>
                  <p>{t('emptyState.body')}</p>
                </div>
              )}

              {/* Marquee selection rect */}
              {marqueeState && (
                <div
                  className="editor-marquee-rect"
                  style={{
                    left: Math.min(marqueeState.startX, marqueeState.currentX),
                    top: Math.min(marqueeState.startY, marqueeState.currentY),
                    width: Math.abs(marqueeState.currentX - marqueeState.startX),
                    height: Math.abs(marqueeState.currentY - marqueeState.startY),
                  }}
                />
              )}

              {/* Draw ghost preview */}
              {drawGhost && (
                <svg
                  style={{ position: 'absolute', inset: 0, pointerEvents: 'none', overflow: 'visible', width: '100%', height: '100%', zIndex: 30 }}
                >
                  {drawingMode === 'line' && (
                    <line
                      x1={drawGhost.startX} y1={drawGhost.startY}
                      x2={drawGhost.currentX} y2={drawGhost.currentY}
                      stroke="#9ca3af" strokeWidth="2" strokeDasharray="6 3"
                    />
                  )}
                  {drawingMode === 'arrow' && (
                    <>
                      <defs>
                        <marker id="draw-ghost-arrowhead" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto">
                          <polygon points="0 0, 8 3, 0 6" fill="#dc2626" />
                        </marker>
                      </defs>
                      <line
                        x1={drawGhost.startX} y1={drawGhost.startY}
                        x2={drawGhost.currentX} y2={drawGhost.currentY}
                        stroke="#dc2626" strokeWidth="2" strokeDasharray="6 3"
                        markerEnd="url(#draw-ghost-arrowhead)"
                      />
                    </>
                  )}
                  {drawingMode === 'draw' && drawGhost.pathPoints && (
                    <path d={drawGhost.pathPoints} stroke="#1d4ed8" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
                  )}
                </svg>
              )}
                </div>
              </div>
            </div>
            </div>
          </div>
          <PersistentHorizontalScrollbar
            targetRef={pageViewportRef}
            refreshKey={`${pageWidth}:${pageHeight}:${zoomLevel}`}
            label={t('pagePanel.workspaceHorizontalScroll')}
            scrollLeftLabel={t('pagePanel.scrollLeft')}
            scrollRightLabel={t('pagePanel.scrollRight')}
          />

          {/* Page navigation strip */}
          <div
            ref={pageStripRef}
            className={`editor-page-strip${pageWidth / pageHeight > 1.5 && pages.length > 1 ? ' editor-page-strip--widescreen' : ''}`}
            onWheel={handlePageStripWheel}
            tabIndex={0}
            dir="ltr"
          >
            {pages.map((page, index) => (
              <div
                key={page.id}
                ref={index === currentPageIndex ? activePageThumbRef : undefined}
                className={[
                  'editor-page-thumb',
                  index === currentPageIndex ? 'is-active' : '',
                  draggingPageIndex === index ? 'is-dragging' : '',
                  dragOverPageIndex === index && draggingPageIndex !== index ? 'is-drag-over' : '',
                ].filter(Boolean).join(' ')}
                draggable
                onDragStart={() => setDraggingPageIndex(index)}
                onDragOver={(e) => { e.preventDefault(); setDragOverPageIndex(index); }}
                onDragLeave={() => setDragOverPageIndex(null)}
                onDrop={() => {
                  if (draggingPageIndex !== null && draggingPageIndex !== index) {
                    onPageMove(draggingPageIndex, index);
                  }
                  setDraggingPageIndex(null);
                  setDragOverPageIndex(null);
                }}
                onDragEnd={() => { setDraggingPageIndex(null); setDragOverPageIndex(null); }}
              >
                <button
                  className="editor-page-thumb-btn"
                  onClick={() => onPageSelect(index)}
                  title={t('pagePanel.pageTitle', { number: index + 1 })}
                >
                  <span className="editor-page-thumb-num">{index + 1}</span>
                </button>
                <div className="editor-page-thumb-actions">
                  <button title={t('pagePanel.duplicatePage')} onClick={() => onPageDuplicate(index)}><FiCopy size={10} /></button>
                  <button
                    title={t('pagePanel.extractPageToJson')}
                    onClick={() => handleExtractPage(index)}
                    disabled={extractingPage === index}
                  >
                    {extractingPage === index ? '…' : <FiScissors size={10} />}
                  </button>
                  {pages.length > 1 && (
                    <button title={t('pagePanel.deletePage')} onClick={() => {
                      if (window.confirm(t('pagePanel.deletePageConfirm', { number: index + 1 }))) onPageDelete(index);
                    }}>×</button>
                  )}
                </div>
              </div>
            ))}
            <button className="editor-page-add-btn" onClick={onPageAdd} title={t('pagePanel.addPage')}>
              <FiPlus size={14} />
            </button>
          </div>
          <PersistentHorizontalScrollbar
            targetRef={pageStripRef}
            refreshKey={`${pages.length}:${currentPageIndex}:${pageWidth}:${pageHeight}`}
            label={t('pagePanel.navigationHorizontalScroll')}
            scrollLeftLabel={t('pagePanel.scrollLeft')}
            scrollRightLabel={t('pagePanel.scrollRight')}
          />
        </section>

        <aside className="editor-panel editor-inspector-panel" aria-label={t('inspector.ariaLabel')}>
          <div className="editor-inspector-tabs">
            <button
              className={`editor-inspector-tab${inspectorTab === 'inspector' ? ' active' : ''}`}
              onClick={() => setInspectorTab('inspector')}
            >
              {selectedElement ? <FiMousePointer size={12} /> : <FiSettings size={12} />}
              {selectedElement ? t('inspector.inspectorTab') : t('inspector.pageSettingsTab')}
              {!selectedElement && JSON.stringify(pageSettings) !== JSON.stringify(DEFAULT_PAGE_SETTINGS) && (
                <span className="editor-settings-badge">●</span>
              )}
            </button>
            <button
              className={`editor-inspector-tab${inspectorTab === 'layers' ? ' active' : ''}`}
              onClick={() => setInspectorTab('layers')}
            >
              <FiLayers size={12} /> {t('inspector.layersTab')}
              {elements.length > 0 && <span className="editor-layer-count">{elements.length}</span>}
            </button>
            {(pageSettings.activeLanguages ?? []).length >= 1 && (
              <button
                className={`editor-inspector-tab${inspectorTab === 'properties' ? ' active' : ''}`}
                onClick={() => setInspectorTab('properties')}
              >
                <FiGlobe size={12} /> {t('inspector.propertiesTab')}
                {(pageSettings.localizedProperties ?? []).length > 0 && (
                  <span className="editor-layer-count">{(pageSettings.localizedProperties ?? []).length}</span>
                )}
              </button>
            )}
          </div>

          {/* ── Layers tab ── */}
          {inspectorTab === 'layers' && (
            <div className="editor-layers-panel">
              {/* Shared header/footer elements */}
              {sharedElements.length > 0 && (
                <>
                  <div className="editor-layers-section-header">
                    <FiLink size={10} /> {t('layers.allPagesHeaderFooter')}
                  </div>
                  {[...sharedElements].reverse().map((el, i) => {
                    const isPrimary = el.id === selectedElementId;
                    return (
                      <div key={el.id}
                        className={`editor-layer-row is-shared${isPrimary ? ' is-primary' : ''}`}
                        onClick={() => selectOne(el.id)}
                      >
                        <span className="editor-layer-row-index">S{sharedElements.length - i}</span>
                        <span className="editor-layer-row-name">{el.name || ELEMENT_TYPE_LABELS[el.type] || el.type}</span>
                        <div className="editor-layer-row-actions">
                          <button title={el.hidden ? t('layers.show') : t('layers.hide')}
                            className={`editor-layer-icon-btn${el.hidden ? ' dimmed' : ''}`}
                            onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { hidden: !el.hidden }); }}>
                            <FiEye size={12} /></button>
                          <button title={el.locked ? t('layers.unlock') : t('layers.lock')}
                            className={`editor-layer-icon-btn${el.locked ? ' dimmed' : ''}`}
                            onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { locked: !el.locked }); }}>
                            {el.locked ? <FiLock size={12} /> : <FiUnlock size={12} />}</button>
                        </div>
                      </div>
                    );
                  })}
                  <div className="editor-layers-section-header" style={{ marginTop: 4 }}>
                    <FiFileText size={10} /> {t('layers.pageHeading', { number: currentPageIndex + 1 })}
                  </div>
                </>
              )}
              {elements.length === 0 && sharedElements.length === 0 && (
                <p className="editor-layers-empty">{t('layers.noElementsYet')}</p>
              )}
              {elements.length === 0 && sharedElements.length > 0 && (
                <p className="editor-layers-empty" style={{ fontSize: 10 }}>{t('layers.noElementsOnPageYet')}</p>
              )}
              {[...elements].reverse().map((el, i) => {
                const isPrimary = el.id === selectedElementId;
                const isInMulti = selectedElementIds.has(el.id);
                return (
                  <div
                    key={el.id}
                    className={`editor-layer-row${isPrimary ? ' is-primary' : isInMulti ? ' is-multi' : ''}`}
                    onClick={(e) => e.shiftKey ? toggleMultiSelect(el.id) : selectOne(el.id)}
                    title={t('layers.shiftClickMultiSelect')}
                  >
                    <span className="editor-layer-row-index">{elements.length - i}</span>
                    <span className="editor-layer-row-name">
                      {el.name || ELEMENT_TYPE_LABELS[el.type] || el.type}
                      {el.elementLanguage && (
                        <span style={{
                          marginLeft: 4, fontSize: 9, padding: '1px 4px', borderRadius: 3,
                          background: '#ede9fe', color: '#4c1d95', fontFamily: 'monospace',
                        }}>
                          {el.elementLanguage.toUpperCase()}
                        </span>
                      )}
                    </span>
                    <div className="editor-layer-row-actions">
                      <button
                        title={el.hidden ? t('layers.show') : t('layers.hide')}
                        className={`editor-layer-icon-btn${el.hidden ? ' dimmed' : ''}`}
                        onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { hidden: !el.hidden }); }}
                      ><FiEye size={12} /></button>
                      <button
                        title={el.locked ? t('layers.unlock') : t('layers.lock')}
                        className={`editor-layer-icon-btn${el.locked ? ' dimmed' : ''}`}
                        onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { locked: !el.locked }); }}
                      >{el.locked ? <FiLock size={12} /> : <FiUnlock size={12} />}</button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* ── Properties tab (localized document properties) ── */}
          {inspectorTab === 'properties' && (
            <div className="editor-inspector-content">
              <LocalizedPropertiesPanel />
            </div>
          )}

          {inspectorTab === 'inspector' && !selectedElement && (() => {
            const currentPreset = Object.entries(PAGE_PRESETS).find(
              ([, v]) => v.width === pageSettings.width && v.height === pageSettings.height
            )?.[0] ?? 'Custom';
            const imageAnalysisDiagnostics = getImageAnalysisDiagnostics(template);
            const imageOcrDiagnostics = getImageOcrDiagnostics(template);
            const imageOcrWarnings = getImageOcrWarnings(template);
            const markdownImportDiagnostics = getMarkdownImportDiagnostics(template);
            const lowConfidenceShare = imageAnalysisDiagnostics?.glyphCount
              ? (imageAnalysisDiagnostics.lowConfidenceGlyphCount ?? 0) / imageAnalysisDiagnostics.glyphCount
              : 0;
            const lowConfidenceWordShare = imageOcrDiagnostics?.wordCount
              ? (imageOcrDiagnostics.lowConfidenceWordCount ?? 0) / imageOcrDiagnostics.wordCount
              : 0;

            return (
              <div className="editor-inspector-content">

                {/* Page settings validation */}
                {(() => {
                  const warnings = getPageSettingsWarnings(pageSettings);
                  return warnings.length > 0 ? (
                    <div className="editor-validation-panel">
                      {warnings.map(w => <span key={w.key}>{w.message}</span>)}
                    </div>
                  ) : null;
                })()}

                {markdownImportDiagnostics.length > 0 && (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading">
                      <FiHash />
                      <span>{t('diagnostics.markdownImport')}</span>
                    </div>
                    <div className="editor-import-diagnostics" role="status" aria-live="polite">
                      {markdownImportDiagnostics.map((diagnostic, index) => (
                        <div
                          className={`editor-import-diagnostic is-${diagnostic.severity ?? 'warning'}`}
                          key={`${diagnostic.code ?? 'markdown'}-${index}`}
                        >
                          <strong>{diagnostic.code ?? t('diagnostics.importIssue')}</strong>
                          <span>
                            {diagnostic.code
                              ? t(`diagnostics.markdownCodes.${diagnostic.code}`, {
                                  defaultValue: diagnostic.message ?? t('diagnostics.importIssue'),
                                })
                              : diagnostic.message ?? t('diagnostics.importIssue')}
                          </span>
                          {diagnostic.source && <code>{diagnostic.source}</code>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {imageAnalysisDiagnostics && (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading">
                      <FiSliders />
                      <span>{t('diagnostics.imageAnalysis')}</span>
                    </div>
                    <div className="editor-image-analysis-panel">
                      <div className="editor-image-analysis-summary">
                        <div>
                          <strong>{formatNumber(imageAnalysisDiagnostics.elementCount)}</strong>
                          <span>{t('diagnostics.elements')}</span>
                        </div>
                        <div>
                          <strong>{formatNumber(imageAnalysisDiagnostics.glyphCount)}</strong>
                          <span>{t('diagnostics.glyphs')}</span>
                        </div>
                        <div>
                          <strong>{formatPercent(lowConfidenceShare)}</strong>
                          <span>{t('diagnostics.lowConfidence')}</span>
                        </div>
                      </div>
                      <div className="editor-image-analysis-grid">
                        <span>{t('diagnostics.source')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.sourceWidthPx)} x {formatNumber(imageAnalysisDiagnostics.sourceHeightPx)} px</strong>
                        <span>{t('diagnostics.working')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.workingWidthPx)} x {formatNumber(imageAnalysisDiagnostics.workingHeightPx)} px</strong>
                        <span>{t('diagnostics.scale')}</span>
                        <strong>{Number(imageAnalysisDiagnostics.scaleFactor ?? 1).toFixed(3)}</strong>
                        <span>{t('diagnostics.regions')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.colorRegionCount)}</strong>
                        <span>{t('diagnostics.shapes')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.shapeCount)}</strong>
                        <span>{t('diagnostics.textLines')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.textLineCount)}</strong>
                        <span>{t('diagnostics.words')}</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.wordCount)}</strong>
                      </div>
                      {(imageAnalysisDiagnostics.warnings ?? []).length > 0 && (
                        <div className="editor-image-analysis-warnings">
                          {imageAnalysisDiagnostics.warnings!.map((warning, index) => (
                            <span key={`${warning}-${index}`}>{warning}</span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {imageOcrDiagnostics && (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading">
                      <FiEye />
                      <span>{t('diagnostics.imageOcr')}</span>
                    </div>
                    <div className="editor-image-analysis-panel">
                      <div className="editor-image-analysis-summary">
                        <div>
                          <strong>{formatNumber(imageOcrDiagnostics.wordCount)}</strong>
                          <span>{t('diagnostics.words')}</span>
                        </div>
                        <div>
                          <strong>{formatNumber(imageOcrDiagnostics.lineCount)}</strong>
                          <span>{t('diagnostics.lines')}</span>
                        </div>
                        <div>
                          <strong>{formatPercent(imageOcrDiagnostics.averageConfidence)}</strong>
                          <span>{t('diagnostics.confidence')}</span>
                        </div>
                      </div>
                      <div className="editor-image-analysis-grid">
                        <span>{t('diagnostics.source')}</span>
                        <strong>{formatNumber(imageOcrDiagnostics.sourceWidthPx)} x {formatNumber(imageOcrDiagnostics.sourceHeightPx)} px</strong>
                        <span>{t('diagnostics.pages')}</span>
                        <strong>{formatNumber(imageOcrDiagnostics.pageCount)}</strong>
                        <span>{t('diagnostics.languages')}</span>
                        <strong>{imageOcrDiagnostics.languages ?? 'deu+eng'}</strong>
                        <span>{t('diagnostics.engine')}</span>
                        <strong>{imageOcrDiagnostics.ocrEngine ?? 'OCR'} {imageOcrDiagnostics.ocrEngineVersion ?? ''}</strong>
                        <span>{t('diagnostics.lowConfidence')}</span>
                        <strong>{formatPercent(lowConfidenceWordShare)}</strong>
                        <span>{t('diagnostics.runtime')}</span>
                        <strong>{formatNumber(imageOcrDiagnostics.elapsedMs)} ms</strong>
                      </div>
                      {imageOcrWarnings.length > 0 && (
                        <div className="editor-image-analysis-warnings">
                          {imageOcrWarnings.map((warning, index) => (
                            <span key={`${warning}-${index}`}>{warning}</span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {/* Paper */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiFileText />
                    <span>{t('pageSettings.paper.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>{t('pageSettings.paper.size')}</span>
                      <select
                        value={currentPreset}
                        onChange={(e) => {
                          const preset = PAGE_PRESETS[e.target.value];
                          if (!preset) return;
                          const isLandscape = pageSettings.orientation === 'landscape';
                          updatePageSettings({
                            width:  isLandscape ? Math.max(preset.width, preset.height) : Math.min(preset.width, preset.height),
                            height: isLandscape ? Math.min(preset.width, preset.height) : Math.max(preset.width, preset.height),
                          });
                        }}
                      >
                        {Object.keys(PAGE_PRESETS).map(k => <option key={k}>{k}</option>)}
                        {currentPreset === 'Custom' && <option value="Custom">{t('pageSettings.paper.customOption')}</option>}
                      </select>
                    </label>
                    <label>
                      <span>{t('pageSettings.paper.unit')}</span>
                      <select
                        value={pageSettings.unit}
                        onChange={(e) => updatePageSettings({ unit: e.target.value as PageSettings['unit'] })}
                      >
                        <option value="px">{t('pageSettings.paper.units.px')}</option>
                        <option value="pt">{t('pageSettings.paper.units.pt')}</option>
                        <option value="mm">{t('pageSettings.paper.units.mm')}</option>
                        <option value="cm">{t('pageSettings.paper.units.cm')}</option>
                        <option value="in">{t('pageSettings.paper.units.in')}</option>
                      </select>
                    </label>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('pageSettings.paper.width', { unit: pageSettings.unit })}</span>
                        <input
                          type="number"
                          value={toDisplay(pageSettings.width, pageSettings.unit)}
                          min={toDisplay(100, pageSettings.unit)}
                          step={pageSettings.unit === 'px' || pageSettings.unit === 'pt' ? 1 : 0.1}
                          onChange={(e) => updatePageSettings({ width: Math.max(100, fromDisplay(Number(e.target.value), pageSettings.unit)) })}
                        />
                      </label>
                      <label>
                        <span>{t('pageSettings.paper.height', { unit: pageSettings.unit })}</span>
                        <input
                          type="number"
                          value={toDisplay(pageSettings.height, pageSettings.unit)}
                          min={toDisplay(100, pageSettings.unit)}
                          step={pageSettings.unit === 'px' || pageSettings.unit === 'pt' ? 1 : 0.1}
                          onChange={(e) => updatePageSettings({ height: Math.max(100, fromDisplay(Number(e.target.value), pageSettings.unit)) })}
                        />
                      </label>
                    </div>
                    <div className="editor-orientation-toggle">
                      <button
                        className={`editor-orient-btn ${pageSettings.orientation === 'portrait' ? 'is-active' : ''}`}
                        onClick={() => {
                          if (pageSettings.orientation !== 'portrait') {
                            updatePageSettings({
                              orientation: 'portrait',
                              width:  Math.min(pageSettings.width, pageSettings.height),
                              height: Math.max(pageSettings.width, pageSettings.height),
                            });
                          }
                        }}
                      >{t('pageSettings.paper.portrait')}</button>
                      <button
                        className={`editor-orient-btn ${pageSettings.orientation === 'landscape' ? 'is-active' : ''}`}
                        onClick={() => {
                          if (pageSettings.orientation !== 'landscape') {
                            updatePageSettings({
                              orientation: 'landscape',
                              width:  Math.max(pageSettings.width, pageSettings.height),
                              height: Math.min(pageSettings.width, pageSettings.height),
                            });
                          }
                        }}
                      >{t('pageSettings.paper.landscape')}</button>
                    </div>
                  </div>
                </div>

                {/* Background */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiDroplet />
                    <span>{t('pageSettings.background.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('pageSettings.background.color')}</span>
                        <input
                          type="color"
                          value={pageSettings.backgroundColor}
                          onChange={(e) => updatePageSettings({ backgroundColor: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>{t('pageSettings.background.fit')}</span>
                        <select
                          value={pageSettings.backgroundImageFit}
                          onChange={(e) => updatePageSettings({ backgroundImageFit: e.target.value as PageSettings['backgroundImageFit'] })}
                        >
                          <option value="cover">{t('pageSettings.background.fitOptions.cover')}</option>
                          <option value="contain">{t('pageSettings.background.fitOptions.contain')}</option>
                          <option value="fill">{t('pageSettings.background.fitOptions.fill')}</option>
                          <option value="tile">{t('pageSettings.background.fitOptions.tile')}</option>
                        </select>
                      </label>
                    </div>
                    <label>
                      <span>{t('pageSettings.background.imageUrl')}</span>
                      <input
                        type="url"
                        placeholder={t('pageSettings.background.imageUrlPlaceholder')}
                        value={pageSettings.backgroundImage}
                        onChange={(e) => updatePageSettings({ backgroundImage: e.target.value })}
                      />
                    </label>
                    {pageSettings.backgroundImage && (
                      <button
                        className="editor-danger-button"
                        style={{ fontSize: 12, minHeight: 32 }}
                        onClick={() => updatePageSettings({ backgroundImage: '' })}
                      >
                        {t('pageSettings.background.removeImage')}
                      </button>
                    )}
                  </div>
                </div>

                {/* Margins */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMaximize2 />
                    <span>{t('pageSettings.margins.heading')}</span>
                    <button
                      className={`editor-link-btn ${linkedMargins ? 'is-linked' : ''}`}
                      title={linkedMargins ? t('pageSettings.margins.unlinkMargins') : t('pageSettings.margins.linkMargins')}
                      onClick={() => setLinkedMargins(l => !l)}
                    >
                      {linkedMargins ? <FiLink /> : <FiLink2 />}
                    </button>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-preset-buttons">
                      {([['none', 0], ['narrow', 24], ['normal', 48], ['wide', 72]] as [string, number][]).map(([labelKey, value]) => (
                        <button
                          key={labelKey}
                          className={`editor-preset-btn ${Object.values(pageSettings.margins).every(v => v === value) ? 'is-active' : ''}`}
                          onClick={() => updatePageSettings({ margins: { top: value, right: value, bottom: value, left: value } })}
                        >
                          {t(`pageSettings.margins.presets.${labelKey}`)}
                        </button>
                      ))}
                    </div>
                    <div className="editor-form-grid">
                      {(['top', 'right', 'bottom', 'left'] as const).map(side => (
                        <label key={side}>
                          <span>{t('pageSettings.margins.sideLabel', { side: t(`pageSettings.margins.sides.${side}`), unit: pageSettings.unit })}</span>
                          <input
                            type="number"
                            value={toDisplay(pageSettings.margins[side], pageSettings.unit)}
                            min={0}
                            step={pageSettings.unit === 'px' || pageSettings.unit === 'pt' ? 1 : 0.1}
                            onChange={(e) => updateMargin(side, Math.max(0, Number(e.target.value)))}
                          />
                        </label>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Workspace */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMonitor />
                    <span>{t('pageSettings.workspace.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.gridVisible}
                        onChange={(e) => updatePageSettings({ gridVisible: e.target.checked })}
                      />
                      <span>{t('pageSettings.workspace.showGrid')}</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.snapToGrid}
                        onChange={(e) => updatePageSettings({ snapToGrid: e.target.checked })}
                      />
                      <span>{t('pageSettings.workspace.snapToGrid')}</span>
                    </label>
                    <label>
                      <span>{t('pageSettings.workspace.gridSize')}</span>
                      <input
                        type="number"
                        value={pageSettings.gridSize}
                        min={4}
                        max={96}
                        onChange={(e) => updatePageSettings({ gridSize: Math.max(4, Number(e.target.value)) })}
                      />
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.showMarginGuide}
                        onChange={(e) => updatePageSettings({ showMarginGuide: e.target.checked })}
                      />
                      <span>{t('pageSettings.workspace.showMarginGuide')}</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.showSafeArea}
                        onChange={(e) => updatePageSettings({ showSafeArea: e.target.checked })}
                      />
                      <span>{t('pageSettings.workspace.showSafeAreaGuide')}</span>
                    </label>
                  </div>
                </div>

                {/* Header & Footer */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiLayers />
                    <span>{t('pageSettings.headerFooter.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.headerEnabled}
                        onChange={(e) => updatePageSettings({ headerEnabled: e.target.checked })}
                      />
                      <span>{t('pageSettings.headerFooter.enableHeader')}</span>
                    </label>
                    {pageSettings.headerEnabled && (
                      <>
                        <label>
                          <span>{t('pageSettings.headerFooter.headerHeight')}</span>
                          <input
                            type="number"
                            value={pageSettings.headerHeight}
                            min={20}
                            max={200}
                            onChange={(e) => updatePageSettings({ headerHeight: Math.max(20, Number(e.target.value)) })}
                          />
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.headerFirstPageDifferent}
                            onChange={(e) => updatePageSettings({ headerFirstPageDifferent: e.target.checked })}
                          />
                          <span>{t('pageSettings.headerFooter.headerFirstPageDifferent')}</span>
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.headerOddEvenDifferent}
                            onChange={(e) => updatePageSettings({ headerOddEvenDifferent: e.target.checked })}
                          />
                          <span>{t('pageSettings.headerFooter.headerOddEvenDifferent')}</span>
                        </label>
                        <div style={{ borderTop: '1px solid #e2e8f0', paddingTop: 8, marginTop: 2 }}>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>{t('pageSettings.headerFooter.insertIntoHeader')}</span>
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'text')}>{t('pageSettings.headerFooter.insertText')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'pagenumber')}>{t('pageSettings.headerFooter.insertPageNumber')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'date')}>{t('pageSettings.headerFooter.insertDate')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'image')}>{t('pageSettings.headerFooter.insertLogo')}</button>
                          </div>
                        </div>
                      </>
                    )}
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.footerEnabled}
                        onChange={(e) => updatePageSettings({ footerEnabled: e.target.checked })}
                      />
                      <span>{t('pageSettings.headerFooter.enableFooter')}</span>
                    </label>
                    {pageSettings.footerEnabled && (
                      <>
                        <label>
                          <span>{t('pageSettings.headerFooter.footerHeight')}</span>
                          <input
                            type="number"
                            value={pageSettings.footerHeight}
                            min={20}
                            max={200}
                            onChange={(e) => updatePageSettings({ footerHeight: Math.max(20, Number(e.target.value)) })}
                          />
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.footerFirstPageDifferent}
                            onChange={(e) => updatePageSettings({ footerFirstPageDifferent: e.target.checked })}
                          />
                          <span>{t('pageSettings.headerFooter.footerFirstPageDifferent')}</span>
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.footerOddEvenDifferent}
                            onChange={(e) => updatePageSettings({ footerOddEvenDifferent: e.target.checked })}
                          />
                          <span>{t('pageSettings.headerFooter.footerOddEvenDifferent')}</span>
                        </label>
                        <div style={{ borderTop: '1px solid #e2e8f0', paddingTop: 8, marginTop: 2 }}>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>{t('pageSettings.headerFooter.insertIntoFooter')}</span>
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'text')}>{t('pageSettings.headerFooter.insertText')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'pagenumber')}>{t('pageSettings.headerFooter.insertPageNumber')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'date')}>{t('pageSettings.headerFooter.insertDate')}</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'image')}>{t('pageSettings.headerFooter.insertLogo')}</button>
                          </div>
                        </div>
                      </>
                    )}
                  </div>
                </div>

                {/* Bleed */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMaximize2 />
                    <span>{t('pageSettings.bleed.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>{t('pageSettings.bleed.bleedSize')}</span>
                      <input
                        type="number"
                        value={pageSettings.bleedSize}
                        min={0}
                        max={72}
                        onChange={(e) => updatePageSettings({ bleedSize: Math.max(0, Number(e.target.value)) })}
                      />
                    </label>
                    {pageSettings.bleedSize > 0 && (
                      <p style={{ margin: 0, fontSize: 11, color: '#64748b', lineHeight: 1.4 }}>
                        {t('pageSettings.bleed.bleedNote')}
                      </p>
                    )}
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.cropMarks}
                        onChange={(e) => updatePageSettings({ cropMarks: e.target.checked })}
                      />
                      <span>{t('pageSettings.bleed.showCropMarks')}</span>
                    </label>
                  </div>
                </div>

                {/* Global Watermark */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiEyeOff />
                    <span>{t('pageSettings.watermark.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.globalWatermark.enabled}
                        onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, enabled: e.target.checked } })}
                      />
                      <span>{t('pageSettings.watermark.enable')}</span>
                    </label>
                    {pageSettings.globalWatermark.enabled && (
                      <>
                        <div className="editor-orientation-toggle">
                          <button
                            className={`editor-orient-btn ${pageSettings.globalWatermark.mode === 'text' ? 'is-active' : ''}`}
                            onClick={() => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, mode: 'text' } })}
                          >{t('pageSettings.watermark.modeText')}</button>
                          <button
                            className={`editor-orient-btn ${pageSettings.globalWatermark.mode === 'image' ? 'is-active' : ''}`}
                            onClick={() => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, mode: 'image' } })}
                          >{t('pageSettings.watermark.modeImageUrl')}</button>
                        </div>
                        <label>
                          <span>{pageSettings.globalWatermark.mode === 'text' ? t('pageSettings.watermark.modeText') : t('pageSettings.watermark.modeImageUrl')}</span>
                          <input
                            type="text"
                            value={pageSettings.globalWatermark.content}
                            placeholder={pageSettings.globalWatermark.mode === 'text' ? t('pageSettings.watermark.textPlaceholder') : t('pageSettings.watermark.urlPlaceholder')}
                            onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, content: e.target.value } })}
                          />
                        </label>
                        <div className="editor-form-grid">
                          <label>
                            <span>{t('pageSettings.watermark.opacity')}</span>
                            <input
                              type="number"
                              step={0.01}
                              min={0}
                              max={1}
                              value={pageSettings.globalWatermark.opacity}
                              onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, opacity: Math.min(1, Math.max(0, Number(e.target.value))) } })}
                            />
                          </label>
                          <label>
                            <span>{t('pageSettings.watermark.rotation')}</span>
                            <input
                              type="number"
                              min={-180}
                              max={180}
                              value={pageSettings.globalWatermark.rotation}
                              onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, rotation: Number(e.target.value) } })}
                            />
                          </label>
                          {pageSettings.globalWatermark.mode === 'text' && (
                            <>
                              <label>
                                <span>{t('pageSettings.watermark.fontSize')}</span>
                                <input
                                  type="number"
                                  min={12}
                                  max={200}
                                  value={pageSettings.globalWatermark.fontSize}
                                  onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, fontSize: Math.max(12, Number(e.target.value)) } })}
                                />
                              </label>
                              <label>
                                <span>{t('pageSettings.watermark.color')}</span>
                                <input
                                  type="color"
                                  value={pageSettings.globalWatermark.color}
                                  onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, color: e.target.value } })}
                                />
                              </label>
                            </>
                          )}
                        </div>
                        <label>
                          <span>{t('pageSettings.watermark.pageScope')}</span>
                          <select
                            value={pageSettings.globalWatermark.pageScope}
                            onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, pageScope: e.target.value as PageSettings['globalWatermark']['pageScope'] } })}
                          >
                            <option value="all">{t('pageSettings.watermark.scopeAll')}</option>
                            <option value="first">{t('pageSettings.watermark.scopeFirst')}</option>
                            <option value="odd">{t('pageSettings.watermark.scopeOdd')}</option>
                            <option value="even">{t('pageSettings.watermark.scopeEven')}</option>
                            <option value="range">{t('pageSettings.watermark.scopeRange')}</option>
                          </select>
                        </label>
                        {pageSettings.globalWatermark.pageScope === 'range' && (
                          <label>
                            <span>{t('pageSettings.watermark.pageRange')}</span>
                            <input
                              type="text"
                              value={pageSettings.globalWatermark.pageRange}
                              placeholder={t('pageSettings.watermark.pageRangePlaceholder')}
                              onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, pageRange: e.target.value } })}
                            />
                          </label>
                        )}
                      </>
                    )}
                  </div>
                </div>

                {/* Page Numbering */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiHash />
                    <span>{t('pageSettings.pageNumbering.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.pageNumbering.enabled}
                        onChange={(e) => updatePageSettings({
                          pageNumbering: { ...pageSettings.pageNumbering, enabled: e.target.checked }
                        })}
                      />
                      <span>{t('pageSettings.pageNumbering.enable')}</span>
                    </label>
                    {pageSettings.pageNumbering.enabled && (
                      <>
                        <label>
                          <span>{t('pageSettings.pageNumbering.format')}</span>
                          <select
                            value={pageSettings.pageNumbering.format}
                            onChange={(e) => updatePageSettings({
                              pageNumbering: { ...pageSettings.pageNumbering, format: e.target.value as PageSettings['pageNumbering']['format'] }
                            })}
                          >
                            <option value="pageOfTotal">{t('pageSettings.pageNumbering.formatPageOfTotal')}</option>
                            <option value="current">{t('pageSettings.pageNumbering.formatCurrent')}</option>
                            <option value="total">{t('pageSettings.pageNumbering.formatTotal')}</option>
                            <option value="roman">{t('pageSettings.pageNumbering.formatRoman')}</option>
                            <option value="alphabetic">{t('pageSettings.pageNumbering.formatAlphabetic')}</option>
                          </select>
                        </label>
                        <div className="editor-form-grid">
                          <label>
                            <span>{t('pageSettings.pageNumbering.startAt')}</span>
                            <input
                              type="number"
                              min={1}
                              value={pageSettings.pageNumbering.startNumber}
                              onChange={(e) => updatePageSettings({
                                pageNumbering: { ...pageSettings.pageNumbering, startNumber: Math.max(1, Number(e.target.value)) }
                              })}
                            />
                          </label>
                          <label>
                            <span>{t('pageSettings.pageNumbering.prefix')}</span>
                            <input
                              type="text"
                              value={pageSettings.pageNumbering.prefix}
                              placeholder={t('pageSettings.pageNumbering.prefixPlaceholder')}
                              onChange={(e) => updatePageSettings({
                                pageNumbering: { ...pageSettings.pageNumbering, prefix: e.target.value }
                              })}
                            />
                          </label>
                          <label>
                            <span>{t('pageSettings.pageNumbering.suffix')}</span>
                            <input
                              type="text"
                              value={pageSettings.pageNumbering.suffix}
                              placeholder={t('pageSettings.pageNumbering.suffixPlaceholder')}
                              onChange={(e) => updatePageSettings({
                                pageNumbering: { ...pageSettings.pageNumbering, suffix: e.target.value }
                              })}
                            />
                          </label>
                        </div>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.pageNumbering.showOnFirstPage}
                            onChange={(e) => updatePageSettings({
                              pageNumbering: { ...pageSettings.pageNumbering, showOnFirstPage: e.target.checked }
                            })}
                          />
                          <span>{t('pageSettings.pageNumbering.showOnFirstPage')}</span>
                        </label>
                        <div>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>{t('pageSettings.pageNumbering.placement')}</span>
                          <div className="editor-placement-grid">
                            {(['top-left', 'top-center', 'top-right', 'bottom-left', 'bottom-center', 'bottom-right'] as const).map(pos => (
                              <button
                                key={pos}
                                type="button"
                                className={`editor-placement-btn ${pageSettings.pageNumbering.placement === pos ? 'is-active' : ''}`}
                                title={t(`pageSettings.pageNumbering.placements.${pos}`)}
                                onClick={() => updatePageSettings({
                                  pageNumbering: { ...pageSettings.pageNumbering, placement: pos }
                                })}
                              >
                                {pos === 'top-left' && '↖'}
                                {pos === 'top-center' && '↑'}
                                {pos === 'top-right' && '↗'}
                                {pos === 'bottom-left' && '↙'}
                                {pos === 'bottom-center' && '↓'}
                                {pos === 'bottom-right' && '↘'}
                              </button>
                            ))}
                          </div>
                        </div>
                        <button
                          type="button"
                          className="editor-secondary-button"
                          onClick={() => {
                            const pn = pageSettings.pageNumbering;
                            const elW = 120, elH = 24;
                            const m = pageSettings.margins;
                            const isTop = pn.placement.startsWith('top');
                            const rawY = isTop
                              ? Math.max(0, m.top - elH - 4)
                              : Math.min(pageHeight - elH, pageHeight - m.bottom + 4);
                            const rawX = pn.placement.endsWith('left')
                              ? m.left
                              : pn.placement.endsWith('right')
                                ? pageWidth - m.right - elW
                                : (pageWidth - elW) / 2;

                            const globalId = 'pagenumber-global';
                            const existing = elements.find(el => el.id === globalId);
                            const updates: Partial<SimpleElement> = {
                              x: Math.round(rawX),
                              y: Math.round(rawY),
                              numberingFormat: pn.format,
                              startNumber: pn.startNumber,
                              prefix: pn.prefix,
                              suffix: pn.suffix,
                              pageScope: pn.showOnFirstPage ? 'all' : 'range',
                              pageRange: pn.showOnFirstPage ? '' : '2-',
                            };
                            if (existing) {
                              onElementUpdate(globalId, updates);
                            } else {
                              onElementAdd({
                                id: globalId,
                                type: 'pagenumber',
                                width: elW,
                                height: elH,
                                ...updates,
                              } as SimpleElement);
                            }
                            setSelectedElementId(globalId);
                          }}
                        >
                          <FiHash />
                          {t('pageSettings.pageNumbering.placeOnCanvas')}
                        </button>
                      </>
                    )}
                  </div>
                </div>

                {/* Export Metadata */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiFileText />
                    <span>{t('pageSettings.exportMetadata.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(['title', 'author', 'subject', 'keywords'] as const).map(key => (
                      <label key={key}>
                        <span>{t(`pageSettings.exportMetadata.fields.${key}`)}</span>
                        <input
                          type="text"
                          value={pageSettings.metadata[key]}
                          placeholder={key === 'keywords' ? t('pageSettings.exportMetadata.keywordsPlaceholder') : ''}
                          onChange={(e) => updatePageSettings({
                            metadata: { ...pageSettings.metadata, [key]: e.target.value }
                          })}
                        />
                      </label>
                    ))}
                  </div>
                </div>

                {/* Export Defaults */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiSliders />
                    <span>{t('pageSettings.exportDefaults.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>{t('pageSettings.exportDefaults.pdfQuality')}</span>
                      <select
                        value={pageSettings.exportDefaults.quality}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, quality: e.target.value as PageSettings['exportDefaults']['quality'] } })}
                      >
                        <option value="screen">{t('pageSettings.exportDefaults.qualityScreen')}</option>
                        <option value="ebook">{t('pageSettings.exportDefaults.qualityEbook')}</option>
                        <option value="printer">{t('pageSettings.exportDefaults.qualityPrinter')}</option>
                        <option value="prepress">{t('pageSettings.exportDefaults.qualityPrepress')}</option>
                      </select>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.embedFonts}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, embedFonts: e.target.checked } })}
                      />
                      <span>{t('pageSettings.exportDefaults.embedFonts')}</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.compressImages}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, compressImages: e.target.checked } })}
                      />
                      <span>{t('pageSettings.exportDefaults.compressImages')}</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.accessibilityTagged}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, accessibilityTagged: e.target.checked } })}
                      />
                      <span>{t('pageSettings.exportDefaults.accessibilityTagged')}</span>
                    </label>
                  </div>
                </div>

                {/* Pagination Behavior */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiList />
                    <span>{t('pageSettings.pagination.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>{t('pageSettings.pagination.pageBreaks')}</span>
                      <select
                        value={pageSettings.pagination.autoBreaks ? 'auto' : 'manual'}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, autoBreaks: e.target.value === 'auto' } })}
                      >
                        <option value="auto">{t('pageSettings.pagination.breaksAutomatic')}</option>
                        <option value="manual">{t('pageSettings.pagination.breaksManual')}</option>
                      </select>
                    </label>
                    <label>
                      <span>{t('pageSettings.pagination.sectionStart')}</span>
                      <select
                        value={pageSettings.pagination.sectionStartBehavior}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, sectionStartBehavior: e.target.value as PageSettings['pagination']['sectionStartBehavior'] } })}
                      >
                        <option value="continue">{t('pageSettings.pagination.sectionContinue')}</option>
                        <option value="new-page">{t('pageSettings.pagination.sectionNewPage')}</option>
                        <option value="odd-page">{t('pageSettings.pagination.sectionOddPage')}</option>
                        <option value="even-page">{t('pageSettings.pagination.sectionEvenPage')}</option>
                      </select>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.pagination.repeatTableHeader}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, repeatTableHeader: e.target.checked } })}
                      />
                      <span>{t('pageSettings.pagination.repeatTableHeader')}</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.pagination.keepWithNext}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, keepWithNext: e.target.checked } })}
                      />
                      <span>{t('pageSettings.pagination.keepWithNext')}</span>
                    </label>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('pageSettings.pagination.orphanLines')}</span>
                        <input
                          type="number"
                          min={1}
                          max={5}
                          value={pageSettings.pagination.orphanLines}
                          onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, orphanLines: Math.max(1, Number(e.target.value)) } })}
                        />
                      </label>
                      <label>
                        <span>{t('pageSettings.pagination.widowLines')}</span>
                        <input
                          type="number"
                          min={1}
                          max={5}
                          value={pageSettings.pagination.widowLines}
                          onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, widowLines: Math.max(1, Number(e.target.value)) } })}
                        />
                      </label>
                    </div>
                  </div>
                </div>

                {/* Track Changes */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiEdit3 />
                    <span>{t('pageSettings.trackChanges.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.trackChanges ?? false}
                        onChange={(e) => updatePageSettings({ trackChanges: e.target.checked })}
                      />
                      <span>{t('pageSettings.trackChanges.enable')}</span>
                    </label>
                  </div>
                </div>

                {/* Document Protection */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiLock />
                    <span>{t('pageSettings.protection.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.protection?.enabled ?? false}
                        onChange={(e) => updatePageSettings({
                          protection: e.target.checked
                            ? { enabled: true, mode: 'readOnly' }
                            : undefined,
                        })}
                      />
                      <span>{t('pageSettings.protection.enable')}</span>
                    </label>
                    {pageSettings.protection?.enabled && (
                      <>
                        <label>
                          <span>{t('pageSettings.protection.restrictionMode')}</span>
                          <select
                            value={pageSettings.protection.mode}
                            onChange={(e) => updatePageSettings({
                              protection: { ...pageSettings.protection!, mode: e.target.value as any },
                            })}
                          >
                            <option value="readOnly">{t('pageSettings.protection.modeReadOnly')}</option>
                            <option value="comments">{t('pageSettings.protection.modeComments')}</option>
                            <option value="trackedChanges">{t('pageSettings.protection.modeTrackedChanges')}</option>
                            <option value="formFields">{t('pageSettings.protection.modeFormFields')}</option>
                          </select>
                        </label>
                        <label>
                          <span>{t('pageSettings.protection.passwordHash')}</span>
                          <input
                            type="text"
                            placeholder={t('pageSettings.protection.passwordHashPlaceholder')}
                            value={pageSettings.protection.passwordHash ?? ''}
                            onChange={(e) => updatePageSettings({
                              protection: { ...pageSettings.protection!, passwordHash: e.target.value || undefined },
                            })}
                          />
                        </label>
                      </>
                    )}
                  </div>
                </div>

                {/* PDF Encryption */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiLock />
                    <span>{t('pageSettings.encryption.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.encryption?.enabled ?? false}
                        onChange={(e) => updatePageSettings({
                          encryption: e.target.checked
                            ? {
                                enabled: true,
                                userPassword: '',
                                ownerPassword: '',
                                algorithm: 'Rc4_128',
                                permissions: {
                                  print: true, modify: true, copy: true, annotate: true,
                                  fillForms: true, extractAccessibility: true, assemble: true, printHighResolution: true,
                                },
                              }
                            : undefined,
                        })}
                      />
                      <span>{t('pageSettings.encryption.enable')}</span>
                    </label>
                    {pageSettings.encryption?.enabled && (
                      <>
                        <label>
                          <span>{t('pageSettings.encryption.userPassword')}</span>
                          <input
                            type="password"
                            placeholder={t('pageSettings.encryption.userPasswordPlaceholder')}
                            value={pageSettings.encryption.userPassword}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, userPassword: e.target.value },
                            })}
                          />
                        </label>
                        <label>
                          <span>{t('pageSettings.encryption.ownerPassword')}</span>
                          <input
                            type="password"
                            placeholder={t('pageSettings.encryption.ownerPasswordPlaceholder')}
                            value={pageSettings.encryption.ownerPassword}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, ownerPassword: e.target.value },
                            })}
                          />
                        </label>
                        <label>
                          <span>{t('pageSettings.encryption.algorithm')}</span>
                          <select
                            value={pageSettings.encryption.algorithm}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, algorithm: e.target.value as PdfEncryption['algorithm'] },
                            })}
                          >
                            <option value="Rc4_128">{t('pageSettings.encryption.algorithmRc4')}</option>
                            <option value="Aes128" disabled>{t('pageSettings.encryption.algorithmAes128')}</option>
                          </select>
                        </label>
                        <div className="editor-settings-subheading" style={{ marginTop: 4 }}>{t('pageSettings.encryption.permissionsHeading')}</div>
                        {([
                          'print', 'copy', 'modify', 'annotate',
                          'fillForms', 'extractAccessibility', 'assemble', 'printHighResolution',
                        ] as (keyof PdfEncryptionPermissions)[]).map((key) => (
                          <label key={key} className="editor-checkbox-control">
                            <input
                              type="checkbox"
                              checked={pageSettings.encryption!.permissions[key]}
                              onChange={(e) => updatePageSettings({
                                encryption: {
                                  ...pageSettings.encryption!,
                                  permissions: { ...pageSettings.encryption!.permissions, [key]: e.target.checked },
                                },
                              })}
                            />
                            <span>{t(`pageSettings.encryption.permissions.${key}`)}</span>
                          </label>
                        ))}
                      </>
                    )}
                  </div>
                </div>

                {/* Custom Document Properties */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiSliders />
                    <span>{t('pageSettings.customProperties.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(pageSettings.customProperties ?? []).map((prop, i) => (
                      <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 80px 28px', gap: 4, alignItems: 'center' }}>
                        <input
                          type="text"
                          placeholder={t('pageSettings.customProperties.namePlaceholder')}
                          value={prop.name}
                          onChange={(e) => {
                            const next = [...(pageSettings.customProperties ?? [])];
                            next[i] = { ...next[i], name: e.target.value };
                            updatePageSettings({ customProperties: next });
                          }}
                        />
                        <input
                          type="text"
                          placeholder={t('pageSettings.customProperties.valuePlaceholder')}
                          value={prop.value}
                          onChange={(e) => {
                            const next = [...(pageSettings.customProperties ?? [])];
                            next[i] = { ...next[i], value: e.target.value };
                            updatePageSettings({ customProperties: next });
                          }}
                        />
                        <select
                          value={prop.type}
                          onChange={(e) => {
                            const next = [...(pageSettings.customProperties ?? [])];
                            next[i] = { ...next[i], type: e.target.value as any };
                            updatePageSettings({ customProperties: next });
                          }}
                        >
                          <option value="text">{t('pageSettings.customProperties.typeText')}</option>
                          <option value="number">{t('pageSettings.customProperties.typeNumber')}</option>
                          <option value="boolean">{t('pageSettings.customProperties.typeBoolean')}</option>
                          <option value="date">{t('pageSettings.customProperties.typeDate')}</option>
                        </select>
                        <button
                          className="editor-icon-button"
                          title={t('pageSettings.customProperties.remove')}
                          onClick={() => {
                            const next = (pageSettings.customProperties ?? []).filter((_, idx) => idx !== i);
                            updatePageSettings({ customProperties: next });
                          }}
                        >
                          <FiTrash2 size={13} />
                        </button>
                      </div>
                    ))}
                    <button
                      className="editor-primary-button"
                      onClick={() => updatePageSettings({
                        customProperties: [...(pageSettings.customProperties ?? []), { name: '', value: '', type: 'text' }],
                      })}
                    >
                      <FiPlus size={13} /> {t('pageSettings.customProperties.addProperty')}
                    </button>
                  </div>
                </div>

                {/* Languages */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiGlobe />
                    <span>{t('pageSettings.languages.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div style={{ fontSize: 11, color: '#64748b', marginBottom: 6 }}>
                      {t('pageSettings.languages.systemLanguage')} <strong>{navigator.language}</strong> {t('pageSettings.languages.systemLanguageNote')}
                    </div>
                    <details className="editor-language-multiselect">
                      <summary>
                        <span>
                          {(pageSettings.activeLanguages ?? []).length === 0
                            ? t('pageSettings.languages.chooseLanguages')
                            : t('pageSettings.languages.selectedCount', {
                              count: (pageSettings.activeLanguages ?? []).length,
                            })}
                        </span>
                        <FiChevronDown aria-hidden="true" />
                      </summary>
                      <div
                        className="editor-language-options"
                        role="group"
                        aria-label={t('pageSettings.languages.chooseLanguages')}
                      >
                        {LOCALIZATION_LANGUAGES.map(({ tag, label, rtl }) => {
                          const active = (pageSettings.activeLanguages ?? []).includes(tag);
                          return (
                            <label key={tag} className={active ? 'is-selected' : ''}>
                              <input
                                type="checkbox"
                                checked={active}
                                onChange={(event) => setLanguageSelected(tag, event.target.checked)}
                              />
                              <span>{label}</span>
                              {rtl && <small>RTL</small>}
                            </label>
                          );
                        })}
                      </div>
                    </details>
                    <div className="editor-selected-languages" aria-live="polite">
                      <span className="editor-selected-languages-label">
                        {t('pageSettings.languages.selectedLanguages')}
                      </span>
                      {(pageSettings.activeLanguages ?? []).length === 0 ? (
                        <span className="editor-selected-languages-empty">
                          {t('pageSettings.languages.noneSelected')}
                        </span>
                      ) : (
                        <div className="editor-selected-language-chips">
                          {(pageSettings.activeLanguages ?? []).map(tag => {
                            const language = LOCALIZATION_LANGUAGES.find(candidate => candidate.tag === tag);
                            const label = language?.label ?? tag.toUpperCase();
                            return (
                              <span key={tag} className="editor-selected-language-chip">
                                <span>{label}</span>
                                <button
                                  type="button"
                                  onClick={() => setLanguageSelected(tag, false)}
                                  title={t('pageSettings.languages.removeLanguage', { language: label })}
                                  aria-label={t('pageSettings.languages.removeLanguage', { language: label })}
                                >
                                  <FiX aria-hidden="true" />
                                </button>
                              </span>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* Named Styles */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiType />
                    <span>{t('pageSettings.namedStyles.heading')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(pageSettings.namedStyles ?? []).map((ns, i) => (
                      <div key={i} style={{ border: '1px solid var(--editor-border)', borderRadius: 6, padding: 8, marginBottom: 4 }}>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 28px', gap: 4, marginBottom: 4 }}>
                          <input
                            type="text"
                            placeholder={t('pageSettings.namedStyles.idPlaceholder')}
                            value={ns.id}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], id: e.target.value };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                          <input
                            type="text"
                            placeholder={t('pageSettings.namedStyles.displayNamePlaceholder')}
                            value={ns.name}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], name: e.target.value };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                          <select
                            value={ns.type}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], type: e.target.value as any };
                              updatePageSettings({ namedStyles: next });
                            }}
                          >
                            <option value="paragraph">{t('pageSettings.namedStyles.typeParagraph')}</option>
                            <option value="character">{t('pageSettings.namedStyles.typeCharacter')}</option>
                            <option value="list">{t('pageSettings.namedStyles.typeList')}</option>
                            <option value="table">{t('pageSettings.namedStyles.typeTable')}</option>
                          </select>
                          <button
                            className="editor-icon-button"
                            title={t('pageSettings.namedStyles.remove')}
                            onClick={() => {
                              const next = (pageSettings.namedStyles ?? []).filter((_, idx) => idx !== i);
                              updatePageSettings({ namedStyles: next });
                            }}
                          >
                            <FiTrash2 size={13} />
                          </button>
                        </div>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 4 }}>
                          <input
                            type="text"
                            placeholder={t('pageSettings.namedStyles.basedOnPlaceholder')}
                            value={ns.basedOn ?? ''}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], basedOn: e.target.value || undefined };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                          <input
                            type="text"
                            placeholder={t('pageSettings.namedStyles.nextStylePlaceholder')}
                            value={ns.nextStyle ?? ''}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], nextStyle: e.target.value || undefined };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                        </div>
                      </div>
                    ))}
                    <button
                      className="editor-primary-button"
                      onClick={() => updatePageSettings({
                        namedStyles: [...(pageSettings.namedStyles ?? []), { id: '', name: '', type: 'paragraph', style: {} }],
                      })}
                    >
                      <FiPlus size={13} /> {t('pageSettings.namedStyles.addStyle')}
                    </button>
                  </div>
                </div>

                {/* Reset */}
                <button
                  className="editor-danger-button"
                  onClick={() => updatePageSettings(DEFAULT_PAGE_SETTINGS)}
                >
                  <FiRefreshCw />
                  {t('pageSettings.reset')}
                </button>

              </div>
            );
          })()}

          {inspectorTab === 'inspector' && selectedElement && (
            <div className="editor-inspector-content">
              <div className="editor-element-identity">
                <span className="editor-element-type-label">
                  {ELEMENT_TYPE_LABELS[selectedElement.type] ?? selectedElement.type}
                </span>
                <input
                  className="editor-element-name-input"
                  type="text"
                  placeholder={t('inspector.elementNamePlaceholder')}
                  value={selectedElement.name ?? ''}
                  onChange={(e) => updateSelectedElement({ name: e.target.value })}
                />
              </div>

              {isOutsideMargins(selectedElement) && (
                <div className="editor-validation-panel">
                  <span>{t('inspector.outsideMarginWarning')}</span>
                </div>
              )}

              {getElementWarnings(selectedElement).length > 0 && (
                <div className="editor-validation-panel">
                  {getElementWarnings(selectedElement).map((warning) => (
                    <span key={warning}>{warning}</span>
                  ))}
                </div>
              )}

              {(() => {
                const idx = elements.findIndex(el => el.id === selectedElement.id);
                const isTop = idx === elements.length - 1;
                const isBottom = idx === 0;
                return (
                  <div className="editor-layer-controls">
                    <span className="editor-layer-label">
                      <FiLayers />
                      {t('inspector.layerOf', { current: idx + 1, total: elements.length })}
                    </span>
                    <div className="editor-layer-buttons">
                      <button className="editor-layer-btn" title={t('inspector.sendToBack')} disabled={isBottom} onClick={() => onElementReorder(selectedElement.id, 'back')}><FiChevronsDown /></button>
                      <button className="editor-layer-btn" title={t('inspector.sendBackward')} disabled={isBottom} onClick={() => onElementReorder(selectedElement.id, 'backward')}><FiArrowDown /></button>
                      <button className="editor-layer-btn" title={t('inspector.bringForward')} disabled={isTop} onClick={() => onElementReorder(selectedElement.id, 'forward')}><FiArrowUp /></button>
                      <button className="editor-layer-btn" title={t('inspector.bringToFront')} disabled={isTop} onClick={() => onElementReorder(selectedElement.id, 'front')}><FiChevronsUp /></button>
                    </div>
                  </div>
                );
              })()}

              <div className="editor-layer-controls">
                <span className="editor-layer-label">
                  {selectedElement.locked ? <FiLock /> : <FiUnlock />}
                  {selectedElement.locked ? t('inspector.locked') : t('inspector.editable')}
                </span>
                <div className="editor-layer-buttons">
                  <button
                    className="editor-layer-btn"
                    title={t('layers.duplicate')}
                    onClick={() => duplicateElement(selectedElement)}
                  >
                    <FiCopy />
                  </button>
                  <button
                    className="editor-layer-btn"
                    title={selectedElement.locked ? t('layers.unlock') : t('layers.lock')}
                    onClick={() => updateSelectedElement({ locked: !selectedElement.locked })}
                  >
                    {selectedElement.locked ? <FiUnlock /> : <FiLock />}
                  </button>
                </div>
              </div>

              {(pageSettings.activeLanguages ?? []).length >= 1 && currentPreviewLanguage && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiGlobe />
                    <span>{t('inspector.languageScope')}</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div style={{ display: 'flex', gap: 4 }}>
                      {/* Current tab language — selected when scopeShowAll is false */}
                      <button
                        style={{
                          padding: '4px 10px', fontSize: 11, borderRadius: 4, fontWeight: 600,
                          border: `2px solid ${!scopeShowAll ? 'var(--editor-accent, #6366f1)' : 'var(--editor-border, #e2e8f0)'}`,
                          background: !scopeShowAll ? 'var(--editor-accent, #6366f1)' : '#f1f5f9',
                          color: !scopeShowAll ? 'white' : '#374151',
                          cursor: !scopeShowAll ? 'default' : 'pointer',
                        }}
                        onClick={() => setScopeShowAll(false)}
                        title={!scopeShowAll
                          ? t('inspector.positionAppliesToLangOnly', { lang: currentPreviewLanguage.toUpperCase() })
                          : t('inspector.switchToLangOnlyEditing', { lang: currentPreviewLanguage.toUpperCase() })}
                      >
                        {currentPreviewLanguage.toUpperCase()}
                      </button>
                      {/* "All" — selected when scopeShowAll is true */}
                      <button
                        style={{
                          padding: '4px 10px', fontSize: 11, borderRadius: 4,
                          border: `2px solid ${scopeShowAll ? 'var(--editor-accent, #6366f1)' : 'var(--editor-border, #e2e8f0)'}`,
                          background: scopeShowAll ? 'var(--editor-accent, #6366f1)' : '#f1f5f9',
                          color: scopeShowAll ? 'white' : '#374151',
                          cursor: scopeShowAll ? 'default' : 'pointer',
                        }}
                        onClick={() => setScopeShowAll(true)}
                        title={scopeShowAll ? t('inspector.positionAppliesToAll') : t('inspector.switchToAllLanguagesEditing')}
                      >
                        {t('inspector.all')}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              <div className="editor-form-grid">
                <label>
                  <span>{t('inspector.x')}</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).x}
                    onChange={(event) => updateLayoutValue('x', event.target.value)}
                  />
                </label>
                <label>
                  <span>{t('inspector.y')}</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).y}
                    onChange={(event) => updateLayoutValue('y', event.target.value)}
                  />
                </label>
                <label>
                  <span>{t('inspector.width')}</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).width}
                    onChange={(event) => updateLayoutValue('width', event.target.value)}
                  />
                </label>
                <label>
                  <span>{t('inspector.height')}</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).height}
                    onChange={(event) => updateLayoutValue('height', event.target.value)}
                  />
                </label>
                <label>
                  <span>{t('inspector.rotation')}</span>
                  <input
                    type="number"
                    value={getEffectiveRotation(selectedElement)}
                    onChange={(e) => {
                      const num = Number(e.target.value);
                      const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
                      if (langKey) {
                        applyPosUpdate(selectedElement.id, { rotation: num }, langKey);
                      } else {
                        updateSelectedElement({ style: { ...selectedElement.style, rotation: num } });
                      }
                    }}
                  />
                </label>
                <label style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                  <span>&nbsp;</span>
                  <button
                    className="editor-secondary-button"
                    title={t('inspector.resetRotation')}
                    onClick={() => {
                      const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
                      if (langKey) {
                        applyPosUpdate(selectedElement.id, { rotation: 0 }, langKey);
                      } else {
                        updateSelectedElement({ style: { ...selectedElement.style, rotation: 0 } });
                      }
                    }}
                  >
                    <FiRotateCw size={13} /> {t('inspector.reset')}
                  </button>
                </label>
              </div>

              {/* Alignment toolbar */}
              <div className="editor-align-toolbar">
                <span className="editor-align-label">{t('inspector.align')}</span>
                <div className="editor-align-buttons">
                  <button title={t('inspector.alignLeftTitle')} onClick={() => alignSelected('left')}><FiAlignLeft size={14} /></button>
                  <button title={t('inspector.alignHCenterTitle')} onClick={() => alignSelected('hcenter')}><FiAlignCenter size={14} /></button>
                  <button title={t('inspector.alignRightTitle')} onClick={() => alignSelected('right')}><FiAlignRight size={14} /></button>
                  <button title={t('inspector.alignTopTitle')} onClick={() => alignSelected('top')}><FiAlignLeft size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                  <button title={t('inspector.alignVCenterTitle')} onClick={() => alignSelected('vcenter')}><FiAlignJustify size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                  <button title={t('inspector.alignBottomTitle')} onClick={() => alignSelected('bottom')}><FiAlignRight size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                </div>
                {selectedElementIds.size >= 3 && (
                  <>
                    <span className="editor-align-label" style={{ marginLeft: 4 }}>{t('inspector.distribute')}</span>
                    <div className="editor-align-buttons">
                      <button title={t('inspector.distributeHorizontally')} onClick={() => distributeSelected('horizontal')}><FiAlignJustify size={14} /></button>
                      <button title={t('inspector.distributeVertically')} onClick={() => distributeSelected('vertical')}><FiAlignJustify size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                    </div>
                  </>
                )}
              </div>

              {/* ── Heading Level (text / richtext) ── */}
              {(selectedElement.type === 'text' || selectedElement.type === 'richtext') && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiBookOpen /><span>{t('inspector.headingLevel')}</span></div>
                  <div className="editor-form-stack" style={{ padding: '8px 12px' }}>
                    <select
                      value={selectedElement.headingLevel ?? ''}
                      onChange={e => updateSelectedElement({ headingLevel: e.target.value === '' ? null : Number(e.target.value) as 1 | 2 | 3 })}
                    >
                      <option value="">{t('inspector.headingNone')}</option>
                      <option value="1">{t('inspector.heading1')}</option>
                      <option value="2">{t('inspector.heading2')}</option>
                      <option value="3">{t('inspector.heading3')}</option>
                    </select>
                    <small style={{ color: '#64748b', fontSize: 11 }}>{t('inspector.headingTocNote')}</small>
                  </div>
                </div>
              )}

              {/* ── Form field: tab order + validation ── */}
              {(['field', 'checkbox', 'radio', 'dropdown', 'optionlist', 'signature'] as const).includes(selectedElement.type as any) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiGrid /><span>{t('inspector.formValidation')}</span></div>
                  <div className="editor-form-stack" style={{ padding: '8px 12px', gap: 6 }}>
                    <label className="editor-prop-row">
                      <span>{t('inspector.tabIndex')}</span>
                      <input type="number" min={0} step={1}
                        value={selectedElement.tabIndex ?? ''}
                        placeholder={t('inspector.tabIndexPlaceholder')}
                        onChange={e => updateSelectedElement({ tabIndex: e.target.value === '' ? undefined : Number(e.target.value) })}
                      />
                    </label>
                    {(selectedElement.type === 'field' || selectedElement.type === 'textarea') && (
                      <>
                        <label className="editor-prop-row">
                          <span>{t('inspector.minLength')}</span>
                          <input type="number" min={0} step={1}
                            value={selectedElement.validationMin ?? ''}
                            placeholder={t('inspector.lengthPlaceholder')}
                            onChange={e => updateSelectedElement({ validationMin: e.target.value === '' ? undefined : Number(e.target.value) })}
                          />
                        </label>
                        <label className="editor-prop-row">
                          <span>{t('inspector.maxLength')}</span>
                          <input type="number" min={0} step={1}
                            value={selectedElement.validationMax ?? ''}
                            placeholder={t('inspector.lengthPlaceholder')}
                            onChange={e => updateSelectedElement({ validationMax: e.target.value === '' ? undefined : Number(e.target.value) })}
                          />
                        </label>
                        <label className="editor-prop-row">
                          <span>{t('inspector.patternRegex')}</span>
                          <input type="text"
                            value={selectedElement.validationPattern ?? ''}
                            placeholder={t('inspector.patternPlaceholder')}
                            onChange={e => updateSelectedElement({ validationPattern: e.target.value || undefined })}
                          />
                        </label>
                      </>
                    )}
                  </div>
                </div>
              )}

              {/* ── Table of Contents ── */}
              {selectedElement.type === 'toc' && (() => {
                const allHeadings = pages.flatMap((p, pi) =>
                  p.elements
                    .filter(el => el.headingLevel != null)
                    .map((el, idx) => ({
                      text: el.content || el.htmlContent?.replace(/<[^>]+>/g, '') || `Heading ${idx + 1}`,
                      level: (el.headingLevel ?? 1) as 1 | 2 | 3,
                      page: pi + 1,
                    }))
                );
                const hasHeadings = allHeadings.length > 0;
                const minLevel = selectedElement.tocMinLevel ?? 1;
                const maxLevel = selectedElement.tocMaxLevel ?? 3;
                const filteredCount = allHeadings.filter(h => h.level >= minLevel && h.level <= maxLevel).length;

                const updateToc = () => {
                  updateSelectedElement({ tocEntries: allHeadings });
                };

                return (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading"><FiBookOpen /><span>{t('elementInspector.toc.heading')}</span></div>
                    <div className="editor-form-stack" style={{ padding: '8px 12px', gap: 10 }}>

                      {/* Title */}
                      <label className="editor-label">
                        <span>{t('elementInspector.toc.title')}</span>
                        <input
                          className="editor-input"
                          type="text"
                          value={selectedElement.tocTitle ?? t('elementInspector.toc.titlePlaceholder')}
                          onChange={e => updateSelectedElement({ tocTitle: e.target.value })}
                          placeholder={t('elementInspector.toc.titlePlaceholder')}
                        />
                      </label>

                      {/* Heading level range */}
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        <label className="editor-label">
                          <span>{t('elementInspector.toc.minLevel')}</span>
                          <select className="editor-select"
                            value={minLevel}
                            onChange={e => updateSelectedElement({ tocMinLevel: Number(e.target.value) as 1 | 2 | 3 })}
                          >
                            <option value={1}>H1</option>
                            <option value={2}>H2</option>
                            <option value={3}>H3</option>
                          </select>
                        </label>
                        <label className="editor-label">
                          <span>{t('elementInspector.toc.maxLevel')}</span>
                          <select className="editor-select"
                            value={maxLevel}
                            onChange={e => updateSelectedElement({ tocMaxLevel: Number(e.target.value) as 1 | 2 | 3 })}
                          >
                            <option value={1}>H1</option>
                            <option value={2}>H2</option>
                            <option value={3}>H3</option>
                          </select>
                        </label>
                      </div>

                      {/* Options */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
                          <input
                            type="checkbox"
                            checked={selectedElement.tocShowPageNumbers ?? true}
                            onChange={e => updateSelectedElement({ tocShowPageNumbers: e.target.checked })}
                          />
                          {t('elementInspector.toc.showPageNumbers')}
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
                          <input
                            type="checkbox"
                            checked={selectedElement.tocShowLeaderDots ?? true}
                            onChange={e => updateSelectedElement({ tocShowLeaderDots: e.target.checked })}
                          />
                          {t('elementInspector.toc.showLeaderDots')}
                        </label>
                      </div>

                      {/* Status */}
                      {!hasHeadings ? (
                        <div className="editor-toc-warning">
                          {t('elementInspector.toc.noHeadingsWarning')}
                        </div>
                      ) : (
                        <small style={{ color: '#64748b', fontSize: 11 }}>
                          {t('elementInspector.toc.entriesSummary', { count: filteredCount, min: minLevel, max: maxLevel, pages: pages.length })}
                          {(selectedElement.tocEntries?.length ?? 0) > 0 && (
                            <> {t('elementInspector.toc.lastUpdated', { count: selectedElement.tocEntries!.length })}</>
                          )}
                        </small>
                      )}

                      {/* Update button */}
                      <button
                        className="editor-toc-update-btn"
                        onClick={updateToc}
                        disabled={!hasHeadings}
                        title={!hasHeadings ? t('elementInspector.toc.assignHeadingLevelsFirst') : t('elementInspector.toc.rebuildTocTitle')}
                      >
                        <FiBookOpen size={14} /> {t('elementInspector.toc.updateToc')}
                      </button>

                    </div>
                  </div>
                );
              })()}

              {/* ── Content (text element) — before Typography ── */}
              {selectedElement.type === 'text' && (() => {
                const allProps = pageSettings.localizedProperties ?? [];
                const sysLang = navigator.language.split('-')[0];
                const curLang = currentPreviewLanguage || sysLang;
                const globalProps = allProps.filter(p => p.scope === 'global');
                const ownProps = allProps.filter(p => p.scope === 'own' && p.ownerLanguage === curLang);
                const insertProperty = (key: string) => {
                  const input = contentInputRef.current;
                  const tag = `{{${key}}}`;
                  if (input) {
                    const start = input.selectionStart ?? (selectedElement.content || '').length;
                    const end = input.selectionEnd ?? start;
                    const cur = selectedElement.content || '';
                    updateSelectedElement({ content: cur.slice(0, start) + tag + cur.slice(end) });
                    setTimeout(() => { input.selectionStart = input.selectionEnd = start + tag.length; input.focus(); }, 0);
                  } else {
                    updateSelectedElement({ content: (selectedElement.content || '') + tag });
                  }
                };
                return (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading"><FiType /><span>{t('elementInspector.content.heading')}</span></div>
                    <div className="editor-form-stack" style={{ padding: 12 }}>
                      <input
                        ref={contentInputRef}
                        type="text"
                        placeholder={t('elementInspector.content.placeholder')}
                        value={selectedElement.content || ''}
                        onChange={(e) => updateSelectedElement({ content: e.target.value })}
                      />
                      {(globalProps.length > 0 || ownProps.length > 0) && (
                        <div style={{ marginTop: 6 }}>
                          {globalProps.length > 0 && (
                            <div style={{ marginBottom: 4 }}>
                              <div style={{ fontSize: 10, color: '#64748b', marginBottom: 3, fontWeight: 600, letterSpacing: '0.04em' }}>{t('elementInspector.content.globalBadge')}</div>
                              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                                {globalProps.map(p => (
                                  <button
                                    key={p.key}
                                    onClick={() => insertProperty(p.key)}
                                    title={t('elementInspector.content.insertGlobalPropertyTitle', { tag: `{{${p.key}}}` })}
                                    style={{
                                      fontSize: 11, padding: '2px 7px', borderRadius: 4, cursor: 'pointer',
                                      border: '1px solid #c7d2fe', background: '#ede9fe', color: '#4c1d95',
                                      fontFamily: 'monospace',
                                    }}
                                  >
                                    {`{{${p.key}}}`}
                                  </button>
                                ))}
                              </div>
                            </div>
                          )}
                          {ownProps.length > 0 && (
                            <div>
                              <div style={{ fontSize: 10, color: '#64748b', marginBottom: 3, fontWeight: 600, letterSpacing: '0.04em' }}>
                                {t('elementInspector.content.ownBadge', { lang: curLang.toUpperCase() })}
                              </div>
                              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                                {ownProps.map(p => (
                                  <button
                                    key={p.key}
                                    onClick={() => insertProperty(p.key)}
                                    title={t('elementInspector.content.insertOwnPropertyTitle', { tag: `{{${p.key}}}`, lang: curLang })}
                                    style={{
                                      fontSize: 11, padding: '2px 7px', borderRadius: 4, cursor: 'pointer',
                                      border: '1px solid #fde68a', background: '#fef3c7', color: '#92400e',
                                      fontFamily: 'monospace',
                                    }}
                                  >
                                    {`{{${p.key}}}`}
                                  </button>
                                ))}
                              </div>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })()}

              {selectedElement.type === 'text' && getGlyphDiagnostics(selectedElement).length > 0 && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiSliders /><span>{t('elementInspector.imageAnalysisGlyphs.heading')}</span></div>
                  <div className="editor-glyph-debug-list">
                    {getGlyphDiagnostics(selectedElement).map((glyph, index) => {
                      const weights = topGlyphWeights(glyph.decisionWeights);
                      return (
                        <div className="editor-glyph-debug-row" key={`${glyph.value ?? '?'}-${index}`}>
                          <div className="editor-glyph-debug-main">
                            <span className="editor-glyph-debug-char">{glyph.value || '?'}</span>
                            <div>
                              <strong>{glyph.method || t('elementInspector.imageAnalysisGlyphs.unknownMethod')}</strong>
                              <span>
                                {glyph.initialCandidate || '?'} → {glyph.selectedCandidate || glyph.value || '?'}
                              </span>
                            </div>
                          </div>
                          <div className="editor-glyph-debug-metrics">
                            <span>{formatPercent(glyph.confidence)}</span>
                            {weights.map(([name, value]) => (
                              <span key={name}>{name} {formatPercent(value)}</span>
                            ))}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* ── Type-specific primary content (before Typography) ── */}
              {selectedElement.type === 'richtext' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.richtext.htmlContent')}</span>
                    <textarea
                      rows={5}
                      value={selectedElement.htmlContent || ''}
                      onChange={(event) => updateSelectedElement({ htmlContent: event.target.value })}
                      placeholder={t('elementInspector.richtext.htmlPlaceholder')}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'field' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.field.fieldLabel')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder={t('elementInspector.field.fieldLabelPlaceholder')}
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>{t('elementInspector.field.localizedValuesHint')}</small>
                  </label>
                  <label>
                    <span>{t('elementInspector.field.fieldName')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldName || ''}
                      onChange={(event) => updateSelectedElement({ fieldName: event.target.value })}
                    />
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.required)}
                      onChange={(event) => updateSelectedElement({ required: event.target.checked })}
                    />
                    <span>{t('placeholders.requiredField')}</span>
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={(selectedElement.style?.backgroundColor ?? '#ffffff') !== 'transparent'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.checked ? '#ffffff' : 'transparent' } })}
                    />
                    <span>{t('elementInspector.field.fillBackground')}</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'textarea' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.textarea.fieldLabel')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder={t('elementInspector.textarea.fieldLabelPlaceholder')}
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>{t('elementInspector.textarea.localizedValuesHint')}</small>
                  </label>
                  <label>
                    <span>{t('elementInspector.textarea.fieldName')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldName || ''}
                      onChange={(event) => updateSelectedElement({ fieldName: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.textarea.placeholderText')}</span>
                    <input
                      type="text"
                      value={selectedElement.placeholder || ''}
                      onChange={(event) => updateSelectedElement({ placeholder: event.target.value || undefined })}
                    />
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.required)}
                      onChange={(event) => updateSelectedElement({ required: event.target.checked })}
                    />
                    <span>{t('placeholders.requiredField')}</span>
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={(selectedElement.style?.backgroundColor ?? '#ffffff') !== 'transparent'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.checked ? '#ffffff' : 'transparent' } })}
                    />
                    <span>{t('elementInspector.textarea.fillBackground')}</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'checkbox' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.checkbox.label')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder={t('elementInspector.checkbox.labelPlaceholder')}
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>{t('elementInspector.checkbox.localizedValuesHint')}</small>
                  </label>
                  <label>
                    <span>{t('elementInspector.checkbox.fieldName')}</span>
                    <input
                      type="text"
                      value={selectedElement.fieldName || ''}
                      onChange={(event) => updateSelectedElement({ fieldName: event.target.value })}
                    />
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.required)}
                      onChange={(event) => updateSelectedElement({ required: event.target.checked })}
                    />
                    <span>{t('placeholders.requiredField')}</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'button' && (() => {
                const action = selectedElement.buttonAction ?? '';
                const actionType = action.startsWith('page:') ? 'page'
                  : action === 'submit' ? 'submit'
                  : action === 'reset' ? 'reset'
                  : action.length > 0 ? 'url' : 'none';
                return (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.button.label')}</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ content: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.button.actionType')}</span>
                    <select
                      value={actionType}
                      onChange={(event) => {
                        const t = event.target.value;
                        if (t === 'none') updateSelectedElement({ buttonAction: '' });
                        else if (t === 'url') updateSelectedElement({ buttonAction: 'https://' });
                        else if (t === 'page') updateSelectedElement({ buttonAction: 'page:1' });
                        else updateSelectedElement({ buttonAction: t });
                      }}
                    >
                      <option value="none">{t('elementInspector.button.actionNone')}</option>
                      <option value="url">{t('elementInspector.button.actionOpenUrl')}</option>
                      <option value="page">{t('elementInspector.button.actionGoToPage')}</option>
                      <option value="submit">{t('elementInspector.button.actionSubmitForm')}</option>
                      <option value="reset">{t('elementInspector.button.actionResetForm')}</option>
                    </select>
                  </label>
                  {actionType === 'url' && (
                    <label>
                      <span>{t('elementInspector.button.url')}</span>
                      <input
                        type="text"
                        value={selectedElement.buttonAction || ''}
                        onChange={(event) => updateSelectedElement({ buttonAction: event.target.value })}
                        placeholder={t('elementInspector.button.urlPlaceholder')}
                      />
                    </label>
                  )}
                  {actionType === 'page' && (
                    <label>
                      <span>{t('elementInspector.button.pageNumber')}</span>
                      <input
                        type="number"
                        min="1"
                        value={parseInt(action.replace('page:', ''), 10) || 1}
                        onChange={(event) => updateSelectedElement({ buttonAction: `page:${event.target.value}` })}
                      />
                    </label>
                  )}
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.button.background')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.backgroundColor || '#3b82f6'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.textColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#ffffff'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.button.radius')}</span>
                      <input
                        type="number"
                        min="0"
                        value={selectedElement.style?.borderRadius || 4}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, borderRadius: Number(event.target.value) } })}
                      />
                    </label>
                  </div>
                </div>
                );
              })()}

              {selectedElement.type === 'dropdown' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.dropdown.options')}</span>
                    <textarea
                      rows={4}
                      value={(selectedElement.options || []).join('\n')}
                      onChange={(event) => updateSelectedElement({ options: event.target.value.split('\n').filter(Boolean) })}
                      placeholder={t('elementInspector.dropdown.optionsPlaceholder')}
                    />
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.multiSelect)}
                      onChange={(event) => updateSelectedElement({ multiSelect: event.target.checked })}
                    />
                    <span>{t('elementInspector.dropdown.multiSelect')}</span>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.textColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#000000'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'optionlist' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.optionlist.listStyle')}</span>
                    <select
                      value={selectedElement.listStyle || (selectedElement.ordered ? 'decimal' : 'disc')}
                      onChange={(e) => updateSelectedElement({
                        listStyle: e.target.value,
                        ordered: ['decimal', 'lower-alpha', 'upper-alpha', 'lower-roman', 'upper-roman'].includes(e.target.value),
                      })}
                    >
                      <option value="disc">{t('elementInspector.optionlist.styleBulletDisc')}</option>
                      <option value="circle">{t('elementInspector.optionlist.styleCircle')}</option>
                      <option value="square">{t('elementInspector.optionlist.styleSquare')}</option>
                      <option value="dash">{t('elementInspector.optionlist.styleDash')}</option>
                      <option value="asterisk">{t('elementInspector.optionlist.styleAsterisk')}</option>
                      <option value="none">{t('elementInspector.optionlist.styleNone')}</option>
                      <option value="decimal">{t('elementInspector.optionlist.styleDecimal')}</option>
                      <option value="lower-alpha">{t('elementInspector.optionlist.styleLowerAlpha')}</option>
                      <option value="upper-alpha">{t('elementInspector.optionlist.styleUpperAlpha')}</option>
                      <option value="lower-roman">{t('elementInspector.optionlist.styleLowerRoman')}</option>
                      <option value="upper-roman">{t('elementInspector.optionlist.styleUpperRoman')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.optionlist.items')}</span>
                    <textarea
                      rows={4}
                      value={(selectedElement.options || []).join('\n')}
                      onChange={(event) => updateSelectedElement({ options: event.target.value.split('\n').filter(Boolean) })}
                      placeholder={t('elementInspector.optionlist.itemsPlaceholder')}
                    />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.textColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#000000'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'radio' && (
                <div className="editor-form-stack">
                  <span className="editor-form-label">{t('elementInspector.radio.options')}</span>
                  {(selectedElement.options || ['Yes', 'No']).map((opt, idx) => (
                    <div key={idx} className="editor-option-row">
                      <input
                        type="text"
                        value={opt}
                        onChange={(e) => {
                          const next = [...(selectedElement.options || [])];
                          next[idx] = e.target.value;
                          updateSelectedElement({ options: next });
                        }}
                        placeholder={t('elementInspector.radio.optionPlaceholder', { number: idx + 1 })}
                      />
                      <button
                        className="editor-option-remove"
                        title={t('elementInspector.radio.removeOption')}
                        onClick={() => {
                          const next = (selectedElement.options || []).filter((_, i) => i !== idx);
                          if (next.length < 1) return;
                          updateSelectedElement({ options: next });
                        }}
                      >×</button>
                    </div>
                  ))}
                  <button
                    className="editor-option-add"
                    onClick={() => updateSelectedElement({
                      options: [...(selectedElement.options || []), t('elementInspector.radio.optionPlaceholder', { number: (selectedElement.options || []).length + 1 })]
                    })}
                  >
                    {t('elementInspector.radio.addOption')}
                  </button>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.textColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#000000'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'checkmark' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.checkmark.label')}</span>
                    <input type="text" value={selectedElement.fieldLabel || ''} onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })} placeholder={t('elementInspector.checkmark.labelPlaceholder')} />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>{t('elementInspector.checkmark.localizedValuesHint')}</small>
                  </label>
                  <label>
                    <span>{t('elementInspector.checkmark.state')}</span>
                    <select value={selectedElement.checkState || 'checked'} onChange={(event) => updateSelectedElement({ checkState: event.target.value as SimpleElement['checkState'] })}>
                      <option value="checked">{t('elementInspector.checkmark.stateChecked')}</option>
                      <option value="cross">{t('elementInspector.checkmark.stateCross')}</option>
                      <option value="dot">{t('elementInspector.checkmark.stateDot')}</option>
                      <option value="empty">{t('elementInspector.checkmark.stateEmpty')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.checkmark.markColor')}</span>
                      <input type="color" value={selectedElement.style?.color || '#16a34a'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.checkmark.stroke')}</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 3} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'watermark' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.watermark.mode')}</span>
                    <select
                      value={selectedElement.watermarkMode || 'text'}
                      onChange={(event) => updateSelectedElement({ watermarkMode: event.target.value as 'text' | 'image' })}
                    >
                      <option value="text">{t('elementInspector.watermark.modeText')}</option>
                      <option value="image">{t('elementInspector.watermark.modeImage')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{selectedElement.watermarkMode === 'image' ? t('elementInspector.watermark.imageUrl') : t('elementInspector.watermark.text')}</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ content: event.target.value })}
                    />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.watermark.color')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#64748b'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.watermark.opacity')}</span>
                      <input
                        type="number"
                        min="0"
                        max="1"
                        step="0.05"
                        value={selectedElement.style?.opacity ?? 0.18}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, opacity: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.watermark.rotation')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.rotation ?? -24}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, rotation: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.watermark.scale')}</span>
                      <input
                        type="number"
                        min="0.1"
                        step="0.1"
                        value={selectedElement.style?.scale ?? 1}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, scale: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 42}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                  </div>
                  <label>
                    <span>{t('elementInspector.watermark.pageScope')}</span>
                    <select
                      value={selectedElement.pageScope || 'all'}
                      onChange={(event) => updateSelectedElement({ pageScope: event.target.value as SimpleElement['pageScope'] })}
                    >
                      <option value="all">{t('elementInspector.watermark.scopeAll')}</option>
                      <option value="current">{t('elementInspector.watermark.scopeCurrent')}</option>
                      <option value="first">{t('elementInspector.watermark.scopeFirst')}</option>
                      <option value="last">{t('elementInspector.watermark.scopeLast')}</option>
                      <option value="range">{t('elementInspector.watermark.scopeRange')}</option>
                    </select>
                  </label>
                  {selectedElement.pageScope === 'range' && (
                    <label>
                      <span>{t('elementInspector.watermark.pageRange')}</span>
                      <input
                        type="text"
                        value={selectedElement.pageRange || ''}
                        onChange={(event) => updateSelectedElement({ pageRange: event.target.value })}
                        placeholder={t('elementInspector.watermark.pageRangePlaceholder')}
                      />
                    </label>
                  )}
                </div>
              )}

              {selectedElement.type === 'note' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.note.title')}</span>
                    <input type="text" value={selectedElement.noteTitle || ''} onChange={(event) => updateSelectedElement({ noteTitle: event.target.value })} />
                  </label>
                  <label>
                    <span>{t('elementInspector.note.body')}</span>
                    <textarea rows={4} value={selectedElement.noteBody || ''} onChange={(event) => updateSelectedElement({ noteBody: event.target.value })} />
                  </label>
                  <label>
                    <span>{t('elementInspector.note.author')}</span>
                    <input type="text" value={selectedElement.noteAuthor || ''} onChange={(event) => updateSelectedElement({ noteAuthor: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.note.noteColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.backgroundColor || '#fef3c7'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.common.textColor')}</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#78350f'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.noteCollapsed)}
                      onChange={(event) => updateSelectedElement({ noteCollapsed: event.target.checked })}
                    />
                    <span>{t('elementInspector.note.collapsed')}</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'date' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.date.mode')}</span>
                    <select value={selectedElement.dateMode || 'static'} onChange={(event) => updateSelectedElement({ dateMode: event.target.value as SimpleElement['dateMode'] })}>
                      <option value="static">{t('elementInspector.date.modeStatic')}</option>
                      <option value="render">{t('elementInspector.date.modeRender')}</option>
                      <option value="binding">{t('elementInspector.date.modeBinding')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.date.staticValue')}</span>
                    <input type="text" value={selectedElement.content || ''} onChange={(event) => updateSelectedElement({ content: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.date.locale')}</span>
                      <input type="text" value={selectedElement.locale || 'de-DE'} onChange={(event) => updateSelectedElement({ locale: event.target.value })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.date.timezone')}</span>
                      <input type="text" value={selectedElement.timezone || 'Europe/Berlin'} onChange={(event) => updateSelectedElement({ timezone: event.target.value })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.date.format')}</span>
                      <input type="text" value={selectedElement.dateFormat || 'yyyy-MM-dd'} onChange={(event) => updateSelectedElement({ dateFormat: event.target.value })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.date.color')}</span>
                      <input type="color" value={selectedElement.style?.color || '#111827'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'pagenumber' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.pagenumber.format')}</span>
                    <select value={selectedElement.numberingFormat || 'pageOfTotal'} onChange={(event) => updateSelectedElement({ numberingFormat: event.target.value as SimpleElement['numberingFormat'] })}>
                      <option value="current">{t('elementInspector.pagenumber.formatCurrent')}</option>
                      <option value="total">{t('elementInspector.pagenumber.formatTotal')}</option>
                      <option value="pageOfTotal">{t('elementInspector.pagenumber.formatPageOfTotal')}</option>
                      <option value="roman">{t('elementInspector.pagenumber.formatRoman')}</option>
                      <option value="alphabetic">{t('elementInspector.pagenumber.formatAlphabetic')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.pagenumber.start')}</span>
                      <input type="number" min="1" value={selectedElement.startNumber || 1} onChange={(event) => updateSelectedElement({ startNumber: Number(event.target.value) || 1 })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.pagenumber.pageScope')}</span>
                      <select value={selectedElement.pageScope || 'all'} onChange={(event) => updateSelectedElement({ pageScope: event.target.value as SimpleElement['pageScope'] })}>
                        <option value="all">{t('elementInspector.pagenumber.scopeAll')}</option>
                        <option value="current">{t('elementInspector.pagenumber.scopeCurrent')}</option>
                        <option value="first">{t('elementInspector.pagenumber.scopeFirst')}</option>
                        <option value="last">{t('elementInspector.pagenumber.scopeLast')}</option>
                        <option value="odd">{t('elementInspector.pagenumber.scopeOdd')}</option>
                        <option value="even">{t('elementInspector.pagenumber.scopeEven')}</option>
                        <option value="range">{t('elementInspector.pagenumber.scopeRange')}</option>
                      </select>
                    </label>
                    {selectedElement.pageScope === 'range' && (
                      <label>
                        <span>{t('elementInspector.pagenumber.pageRange')}</span>
                        <input type="text" value={selectedElement.pageRange || ''} onChange={(event) => updateSelectedElement({ pageRange: event.target.value })} placeholder={t('elementInspector.pagenumber.pageRangePlaceholder')} />
                      </label>
                    )}
                    <label>
                      <span>{t('elementInspector.pagenumber.prefix')}</span>
                      <input type="text" value={selectedElement.prefix || ''} onChange={(event) => updateSelectedElement({ prefix: event.target.value })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.pagenumber.suffix')}</span>
                      <input type="text" value={selectedElement.suffix || ''} onChange={(event) => updateSelectedElement({ suffix: event.target.value })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'arrow' && (
                <div className="editor-form-stack">
                  <span className="editor-form-label">{t('elementInspector.arrow.direction')}</span>
                  <div className="editor-arrow-direction-grid">
                    {(['up', 'left', 'right', 'down'] as const).map(dir => (
                      <button
                        key={dir}
                        className={`editor-arrow-dir-btn${(selectedElement.arrowDirection || 'right') === dir ? ' is-active' : ''}`}
                        onClick={() => updateSelectedElement({ arrowDirection: dir })}
                        title={t(`elementInspector.arrow.directions.${dir}`)}
                      >
                        {dir === 'up' ? '↑' : dir === 'down' ? '↓' : dir === 'left' ? '←' : '→'}
                      </button>
                    ))}
                  </div>
                  <label>
                    <span>{t('elementInspector.arrow.rotation')}</span>
                    <input
                      type="number"
                      value={selectedElement.arrowRotation ?? 0}
                      onChange={(e) => updateSelectedElement({ arrowRotation: Number(e.target.value) })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.arrow.arrowMode')}</span>
                    <select value={selectedElement.arrowMode || 'straight'} onChange={(event) => updateSelectedElement({ arrowMode: event.target.value as SimpleElement['arrowMode'] })}>
                      <option value="straight">{t('elementInspector.arrow.modeStraight')}</option>
                      <option value="elbow">{t('elementInspector.arrow.modeElbow')}</option>
                      <option value="curved">{t('elementInspector.arrow.modeCurved')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.arrow.startHead')}</span>
                      <select value={selectedElement.startMarker || 'none'} onChange={(event) => updateSelectedElement({ startMarker: event.target.value as SimpleElement['startMarker'] })}>
                        <option value="none">{t('elementInspector.arrow.markerNone')}</option>
                        <option value="filled">{t('elementInspector.arrow.markerFilled')}</option>
                        <option value="open">{t('elementInspector.arrow.markerOpen')}</option>
                        <option value="dot">{t('elementInspector.arrow.markerDot')}</option>
                        <option value="diamond">{t('elementInspector.arrow.markerDiamond')}</option>
                        <option value="square">{t('elementInspector.arrow.markerSquare')}</option>
                        <option value="circle">{t('elementInspector.arrow.markerCircle')}</option>
                      </select>
                    </label>
                    <label>
                      <span>{t('elementInspector.arrow.endHead')}</span>
                      <select value={selectedElement.endMarker || 'filled'} onChange={(event) => updateSelectedElement({ endMarker: event.target.value as SimpleElement['endMarker'] })}>
                        <option value="none">{t('elementInspector.arrow.markerNone')}</option>
                        <option value="filled">{t('elementInspector.arrow.markerFilled')}</option>
                        <option value="open">{t('elementInspector.arrow.markerOpen')}</option>
                        <option value="dot">{t('elementInspector.arrow.markerDot')}</option>
                        <option value="diamond">{t('elementInspector.arrow.markerDiamond')}</option>
                        <option value="square">{t('elementInspector.arrow.markerSquare')}</option>
                        <option value="circle">{t('elementInspector.arrow.markerCircle')}</option>
                      </select>
                    </label>
                    <label>
                      <span>{t('elementInspector.arrow.color')}</span>
                      <input type="color" value={selectedElement.style?.color || '#dc2626'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.arrow.stroke')}</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                  </div>
                  <label>
                    <span>{t('elementInspector.arrow.dashStyle')}</span>
                    <select value={selectedElement.style?.dashStyle || 'solid'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, dashStyle: event.target.value } })}>
                      <option value="solid">{t('elementInspector.arrow.dashSolid')}</option>
                      <option value="dashed">{t('elementInspector.arrow.dashDashed')}</option>
                      <option value="dotted">{t('elementInspector.arrow.dashDotted')}</option>
                    </select>
                  </label>
                </div>
              )}

              {/* ── Shared: Typography ── */}
              {TYPOGRAPHY_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiType /><span>{t('elementInspector.typography.heading')}</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>{t('elementInspector.typography.fontFamily')}</span>
                      <select
                        value={selectedElement.style?.fontFamily || 'Arial'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontFamily: e.target.value } })}
                      >
                        {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>{t('elementInspector.typography.language')}</span>
                      <select
                        value={selectedElement.language || ''}
                        onChange={(e) => {
                          const lang = e.target.value;
                          const dir = isDocumentRtlLanguage(lang) ? 'rtl' : 'ltr';
                          updateSelectedElement({
                            language: lang || undefined,
                            textDirection: lang ? dir : undefined,
                          });
                        }}
                      >
                        <option value="">{t('elementInspector.typography.languageNone')}</option>
                        <option value="en">{t('elementInspector.typography.languages.en')}</option>
                        <option value="de">{t('elementInspector.typography.languages.de')}</option>
                        <option value="fr">{t('elementInspector.typography.languages.fr')}</option>
                        <option value="es">{t('elementInspector.typography.languages.es')}</option>
                        <option value="it">{t('elementInspector.typography.languages.it')}</option>
                        <option value="pt">{t('elementInspector.typography.languages.pt')}</option>
                        <option value="ru">{t('elementInspector.typography.languages.ru')}</option>
                        <option value="el">{t('elementInspector.typography.languages.el')}</option>
                        <option value="ar">{t('elementInspector.typography.languages.ar')}</option>
                        <option value="zh-CN">{t('elementInspector.typography.languages.zh-CN')}</option>
                        <option value="zh-TW">{t('elementInspector.typography.languages.zh-TW')}</option>
                        <option value="ja">{t('elementInspector.typography.languages.ja')}</option>
                        <option value="ko">{t('elementInspector.typography.languages.ko')}</option>
                        <option value="hi">{t('elementInspector.typography.languages.hi')}</option>
                        <option value="th">{t('elementInspector.typography.languages.th')}</option>
                      </select>
                    </label>
                    <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                      <span style={{ fontSize: 11, color: '#64748b', minWidth: 72 }}>{t('elementInspector.typography.direction')}</span>
                      {(['ltr', 'rtl'] as const).map(dir => (
                        <button
                          key={dir}
                          className={`editor-toggle-btn${(selectedElement.textDirection || 'ltr') === dir ? ' active' : ''}`}
                          style={{ flex: 1, fontFamily: 'monospace', fontSize: 11 }}
                          title={dir === 'ltr' ? t('elementInspector.typography.directionLtrTitle') : t('elementInspector.typography.directionRtlTitle')}
                          onClick={() => updateSelectedElement({ textDirection: dir })}
                        >{dir.toUpperCase()}</button>
                      ))}
                    </div>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('elementInspector.common.fontSize')}</span>
                        <input
                          type="number"
                          min={6}
                          max={400}
                          value={selectedElement.style?.fontSize || 14}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.typography.color')}</span>
                        <input
                          type="color"
                          value={selectedElement.style?.color || '#111827'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.typography.lineHeight')}</span>
                        <input
                          type="number"
                          min={0.8}
                          max={4}
                          step={0.1}
                          value={selectedElement.style?.lineHeight ?? 1.4}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, lineHeight: Number(e.target.value) } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.typography.letterSpacing')}</span>
                        <input
                          type="number"
                          min={-5}
                          max={20}
                          step={0.5}
                          value={selectedElement.style?.letterSpacing ?? 0}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, letterSpacing: Number(e.target.value) } })}
                        />
                      </label>
                    </div>
                    <div className="editor-toggle-group">
                      <button
                        className={`editor-toggle-btn${selectedElement.style?.fontWeight === 'bold' ? ' active' : ''}`}
                        title={t('elementInspector.typography.bold')}
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, fontWeight: selectedElement.style?.fontWeight === 'bold' ? 'normal' : 'bold' } })}
                      ><FiBold size={14} /></button>
                      <button
                        className={`editor-toggle-btn${selectedElement.style?.fontStyle === 'italic' ? ' active' : ''}`}
                        title={t('elementInspector.typography.italic')}
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, fontStyle: selectedElement.style?.fontStyle === 'italic' ? 'normal' : 'italic' } })}
                      ><FiItalic size={14} /></button>
                      <button
                        className={`editor-toggle-btn${selectedElement.style?.textDecoration === 'underline' ? ' active' : ''}`}
                        title={t('elementInspector.typography.underline')}
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, textDecoration: selectedElement.style?.textDecoration === 'underline' ? 'none' : 'underline' } })}
                      ><FiUnderline size={14} /></button>
                      <div className="editor-toggle-separator" />
                      {(['left', 'center', 'right', 'justify'] as const).map((align, i) => {
                        const Icon = [FiAlignLeft, FiAlignCenter, FiAlignRight, FiAlignJustify][i];
                        return (
                          <button
                            key={align}
                            className={`editor-toggle-btn${selectedElement.style?.textAlign === align ? ' active' : ''}`}
                            title={t(`elementInspector.typography.align${align.charAt(0).toUpperCase()}${align.slice(1)}`)}
                            onClick={() => updateSelectedElement({ style: { ...selectedElement.style, textAlign: align } })}
                          ><Icon size={14} /></button>
                        );
                      })}
                    </div>
                  </div>
                </div>
              )}

              {/* ── Shared: Background ── */}
              {BACKGROUND_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiDroplet /><span>{t('elementInspector.background.heading')}</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('elementInspector.background.color')}</span>
                        <input
                          type="color"
                          value={selectedElement.style?.backgroundColor && selectedElement.style.backgroundColor !== 'transparent' ? selectedElement.style.backgroundColor : '#ffffff'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.background.opacityPercent')}</span>
                        <input
                          type="number"
                          min={0}
                          max={100}
                          value={Math.round((selectedElement.style?.backgroundOpacity ?? 1) * 100)}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, backgroundOpacity: Math.min(1, Math.max(0, Number(e.target.value) / 100)) } })}
                        />
                      </label>
                    </div>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={selectedElement.style?.backgroundColor === 'transparent' || selectedElement.style?.backgroundOpacity === 0}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: e.target.checked ? 'transparent' : '#ffffff', backgroundOpacity: e.target.checked ? 0 : 1 } })}
                      />
                      <span>{t('elementInspector.background.transparent')}</span>
                    </label>
                  </div>
                </div>
              )}

              {/* ── Shared: Border ── */}
              {BORDER_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiBox /><span>{t('elementInspector.border.heading')}</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>{t('elementInspector.border.width')}</span>
                        <input
                          type="number"
                          min={0}
                          max={20}
                          value={selectedElement.style?.borderWidth ?? 0}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderWidth: Number(e.target.value) } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.border.color')}</span>
                        <input
                          type="color"
                          value={selectedElement.style?.borderColor || '#000000'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderColor: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.border.style')}</span>
                        <select
                          value={selectedElement.style?.borderStyle || 'solid'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderStyle: e.target.value } })}
                        >
                          <option value="none">{t('elementInspector.border.styleNone')}</option>
                          <option value="solid">{t('elementInspector.border.styleSolid')}</option>
                          <option value="dashed">{t('elementInspector.border.styleDashed')}</option>
                          <option value="dotted">{t('elementInspector.border.styleDotted')}</option>
                          <option value="double">{t('elementInspector.border.styleDouble')}</option>
                        </select>
                      </label>
                      <label>
                        <span>{t('elementInspector.border.radius')}</span>
                        <input
                          type="number"
                          min={0}
                          max={200}
                          value={selectedElement.style?.borderRadius ?? 0}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderRadius: Number(e.target.value) } })}
                        />
                      </label>
                    </div>
                  </div>
                </div>
              )}

              {/* ── Shared: Padding ── */}
              {PADDING_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMaximize2 /><span>{t('elementInspector.padding.heading')}</span>
                    <button
                      className={`editor-link-btn${linkedPadding ? ' active' : ''}`}
                      title={linkedPadding ? t('elementInspector.padding.unlinkTitle') : t('elementInspector.padding.linkTitle')}
                      onClick={() => setLinkedPadding(p => !p)}
                      style={{ marginLeft: 'auto' }}
                    >
                      {linkedPadding ? <FiLink size={12} /> : <FiLink2 size={12} />}
                    </button>
                  </div>
                  <div className="editor-form-grid" style={{ padding: 12 }}>
                    {linkedPadding ? (
                      <label style={{ gridColumn: '1 / -1' }}>
                        <span>{t('elementInspector.padding.allSides')}</span>
                        <input
                          type="number" min={0} max={200}
                          value={selectedElement.style?.paddingTop ?? 0}
                          onChange={(e) => {
                            const v = Number(e.target.value);
                            updateSelectedElement({ style: { ...selectedElement.style, paddingTop: v, paddingRight: v, paddingBottom: v, paddingLeft: v } });
                          }}
                        />
                      </label>
                    ) : (
                      <>
                        <label><span>{t('elementInspector.padding.top')}</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingTop ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingTop: Number(e.target.value) } })} />
                        </label>
                        <label><span>{t('elementInspector.padding.right')}</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingRight ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingRight: Number(e.target.value) } })} />
                        </label>
                        <label><span>{t('elementInspector.padding.bottom')}</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingBottom ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingBottom: Number(e.target.value) } })} />
                        </label>
                        <label><span>{t('elementInspector.padding.left')}</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingLeft ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingLeft: Number(e.target.value) } })} />
                        </label>
                      </>
                    )}
                  </div>
                </div>
              )}

              {selectedElement.type === 'qrcode' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.qrcode.value')}</span>
                    <input
                      type="text"
                      value={selectedElement.qrValue || ''}
                      onChange={(event) => updateSelectedElement({ qrValue: event.target.value })}
                      placeholder={t('elementInspector.qrcode.valuePlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.qrcode.size')}</span>
                    <input
                      type="number"
                      value={selectedElement.qrSize || 100}
                      onChange={(event) => updateSelectedElement({ qrSize: Number(event.target.value) || 100 })}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'barcode' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.barcode.value')}</span>
                    <input
                      type="text"
                      value={selectedElement.barcodeValue || ''}
                      onChange={(event) => updateSelectedElement({ barcodeValue: event.target.value })}
                      placeholder={t('elementInspector.barcode.valuePlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.barcode.type')}</span>
                    <select
                      value={selectedElement.barcodeType || 'CODE128'}
                      onChange={(event) => updateSelectedElement({ barcodeType: event.target.value })}
                    >
                      <option value="CODE128">{t('elementInspector.barcode.typeCode128')}</option>
                      <option value="CODE39">{t('elementInspector.barcode.typeCode39')}</option>
                      <option value="EAN13">{t('elementInspector.barcode.typeEan13')}</option>
                      <option value="UPC">{t('elementInspector.barcode.typeUpc')}</option>
                    </select>
                  </label>
                </div>
              )}

              {selectedElement.type === 'signature' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.signature.label')}</span>
                    <input
                      type="text"
                      value={selectedElement.signatureLabel || ''}
                      onChange={(event) => updateSelectedElement({ signatureLabel: event.target.value })}
                      placeholder={t('elementInspector.signature.labelPlaceholder')}
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>{t('elementInspector.signature.localizedValuesHint')}</small>
                  </label>
                </div>
              )}

              {selectedElement.type === 'image' && (
                <div className="editor-form-stack">
                  <div
                    className="editor-image-dropzone"
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={async (e) => {
                      e.preventDefault();
                      const file = e.dataTransfer.files[0];
                      if (!file || !file.type.startsWith('image/')) return;
                      try {
                        const asset = await uploadDesignerImage(file);
                        updateSelectedElement({ assetId: asset.id, content: asset.contentUrl });
                        notify.success(t('elementInspector.image.uploaded'));
                      } catch (error) {
                        notify.error(error instanceof Error ? error.message : t('elementInspector.image.uploadFailed'));
                      }
                    }}
                  >
                    <span>{t('elementInspector.image.dropHere')}</span>
                    <label className="editor-image-upload-btn">
                      {t('elementInspector.image.browse')}
                      <input
                        type="file"
                        accept="image/png,image/jpeg"
                        style={{ display: 'none' }}
                        onChange={async (e) => {
                          const file = e.target.files?.[0];
                          if (!file) return;
                          try {
                            const asset = await uploadDesignerImage(file);
                            updateSelectedElement({ assetId: asset.id, content: asset.contentUrl });
                            notify.success(t('elementInspector.image.uploaded'));
                          } catch (error) {
                            notify.error(error instanceof Error ? error.message : t('elementInspector.image.uploadFailed'));
                          } finally {
                            e.target.value = '';
                          }
                        }}
                      />
                    </label>
                  </div>
                  <label>
                    <span>{t('elementInspector.image.sourceUrl')}</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ assetId: undefined, content: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.image.fitMode')}</span>
                    <select
                      value={selectedElement.fitMode || 'contain'}
                      onChange={(event) => updateSelectedElement({ fitMode: event.target.value as 'contain' | 'cover' | 'fill' | 'none' })}
                    >
                      <option value="contain">{t('elementInspector.image.fitContain')}</option>
                      <option value="cover">{t('elementInspector.image.fitCover')}</option>
                      <option value="fill">{t('elementInspector.image.fitFill')}</option>
                      <option value="none">{t('elementInspector.image.fitNone')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.image.cropX')}</span>
                      <input
                        type="number"
                        value={selectedElement.cropX || 0}
                        onChange={(event) => updateSelectedElement({ cropX: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.image.cropY')}</span>
                      <input
                        type="number"
                        value={selectedElement.cropY || 0}
                        onChange={(event) => updateSelectedElement({ cropY: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.image.cropWidth')}</span>
                      <input
                        type="number"
                        value={selectedElement.cropWidth || 0}
                        onChange={(event) => updateSelectedElement({ cropWidth: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.image.cropHeight')}</span>
                      <input
                        type="number"
                        value={selectedElement.cropHeight || 0}
                        onChange={(event) => updateSelectedElement({ cropHeight: Number(event.target.value) })}
                      />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.image.focalX')}</span>
                      <input
                        type="number"
                        value={selectedElement.focalX || 50}
                        onChange={(event) => updateSelectedElement({ focalX: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>{t('elementInspector.image.focalY')}</span>
                      <input
                        type="number"
                        value={selectedElement.focalY || 50}
                        onChange={(event) => updateSelectedElement({ focalY: Number(event.target.value) })}
                      />
                    </label>
                  </div>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.preserveAspectRatio)}
                      onChange={(event) => updateSelectedElement({ preserveAspectRatio: event.target.checked })}
                    />
                    <span>{t('elementInspector.image.preserveAspectRatio')}</span>
                  </label>
                </div>
              )}


              {selectedElement.type === 'table' && (() => {
                const cols = selectedElement.style?.columns ?? 3;
                const rows = selectedElement.style?.rows ?? 3;
                const colAligns = selectedElement.columnAlignments ?? Array.from({ length: cols }, () => 'left' as const);
                const colWidths = selectedElement.columnWidths ?? Array.from({ length: cols }, () => 0);
                const selectedCellRow = Math.min(Math.max(Number(selectedElement.style?.selectedCellRow ?? 0), 0), rows - 1);
                const selectedCellCol = Math.min(Math.max(Number(selectedElement.style?.selectedCellCol ?? 0), 0), cols - 1);
                const cellStyles = selectedElement.cellStyles ?? [];
                const activeCellStyle = cellStyles.find((cs) => cs.row === selectedCellRow && cs.col === selectedCellCol);
                const isMeaningfulCellStyle = (cs: CellStyle) =>
                  Boolean(cs.backgroundColor || cs.color || cs.textAlign || cs.borderColor || cs.borderWidth
                    || cs.padding || cs.fontFamily || cs.fontSize || cs.bold || cs.italic
                    || cs.borderTop || cs.borderRight || cs.borderBottom || cs.borderLeft);
                const setSelectedCell = (row: number, col: number) => {
                  updateSelectedElement({
                    style: { ...selectedElement.style, selectedCellRow: row, selectedCellCol: col }
                  });
                };
                const updateCellStyle = (updates: Partial<CellStyle>) => {
                  const next = cellStyles.filter((cs) => !(cs.row === selectedCellRow && cs.col === selectedCellCol));
                  const merged: CellStyle = { row: selectedCellRow, col: selectedCellCol, ...(activeCellStyle ?? {}), ...updates };
                  if (isMeaningfulCellStyle(merged)) next.push(merged);
                  updateSelectedElement({ cellStyles: next.length ? next : undefined });
                };
                const clearCellStyle = () => {
                  const next = cellStyles.filter((cs) => !(cs.row === selectedCellRow && cs.col === selectedCellCol));
                  updateSelectedElement({ cellStyles: next.length ? next : undefined });
                };
                return (
                  <div className="editor-form-stack">
                    <label>
                      <span>{t('elementInspector.table.rows')}</span>
                      <input type="number" min="1" value={rows}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, rows: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.table.columns')}</span>
                      <input type="number" min="1" value={cols}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, columns: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.table.borderWidth')}</span>
                      <input type="number" min="0" value={selectedElement.style?.borderWidth ?? 1}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderWidth: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.table.borderColor')}</span>
                      <input type="color" value={selectedElement.style?.borderColor || '#000000'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderColor: e.target.value } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.table.cellPadding')}</span>
                      <input type="number" min="0" value={selectedElement.style?.cellPadding ?? 5}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellPadding: Number(e.target.value) } })} />
                    </label>

                    <label>
                      <span>{t('elementInspector.table.cellFontSize')}</span>
                      <input type="number" min="1" value={selectedElement.style?.cellFontSize ?? 10}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.table.cellFont')}</span>
                      <select value={selectedElement.style?.cellFontFamily || 'Arial'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontFamily: e.target.value } })}>
                        {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>{t('elementInspector.table.cellTextColor')}</span>
                      <input type="color" value={selectedElement.style?.cellColor || '#555555'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellColor: e.target.value } })} />
                    </label>
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.style?.cellFontWeight === 'bold'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontWeight: e.target.checked ? 'bold' : 'normal' } })} />
                      <span>{t('elementInspector.table.boldCellText')}</span>
                    </label>

                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.headerRow ?? false}
                        onChange={(e) => updateSelectedElement({ headerRow: e.target.checked })} />
                      <span>{t('elementInspector.table.headerRow')}</span>
                    </label>
                    {(selectedElement.headerRow) && (
                      <label>
                        <span>{t('elementInspector.table.headerBackground')}</span>
                        <input type="color" value={selectedElement.headerBgColor || '#f1f5f9'}
                          onChange={(e) => updateSelectedElement({ headerBgColor: e.target.value })} />
                      </label>
                    )}
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.footerRow ?? false}
                        onChange={(e) => updateSelectedElement({ footerRow: e.target.checked })} />
                      <span>{t('elementInspector.table.footerRow')}</span>
                    </label>
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.zebraEnabled ?? false}
                        onChange={(e) => updateSelectedElement({ zebraEnabled: e.target.checked })} />
                      <span>{t('elementInspector.table.alternatingRows')}</span>
                    </label>
                    {(selectedElement.zebraEnabled) && (
                      <label>
                        <span>{t('elementInspector.table.evenRowColor')}</span>
                        <input type="color" value={selectedElement.zebraColor || '#f9fafb'}
                          onChange={(e) => updateSelectedElement({ zebraColor: e.target.value })} />
                      </label>
                    )}

                    <div className="editor-form-group">
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>{t('elementInspector.table.columnAlignment')}</span>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
                        {Array.from({ length: cols }).map((_, i) => (
                          <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                            <span style={{ fontSize: 10, color: '#94a3b8', minWidth: 42 }}>{t('elementInspector.table.col', { number: i + 1 })}</span>
                            <div className="editor-toggle-group">
                              {(['left', 'center', 'right'] as const).map((align) => (
                                <button key={align}
                                  className={`editor-toggle-btn${(colAligns[i] || 'left') === align ? ' active' : ''}`}
                                  onClick={() => {
                                    const next = [...colAligns];
                                    while (next.length <= i) next.push('left');
                                    next[i] = align;
                                    updateSelectedElement({ columnAlignments: next });
                                  }}>
                                  {align === 'left' ? <FiAlignLeft size={11} /> : align === 'center' ? <FiAlignCenter size={11} /> : <FiAlignRight size={11} />}
                                </button>
                              ))}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div className="editor-form-group">
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>{t('elementInspector.table.columnWidths')}</span>
                      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginTop: 4 }}>
                        {Array.from({ length: cols }).map((_, i) => (
                          <input key={i} type="number" min="0" placeholder={t('elementInspector.table.widthPlaceholder')} style={{ width: 52 }}
                            value={colWidths[i] || ''}
                            onChange={(e) => {
                              const next = [...colWidths];
                              while (next.length <= i) next.push(0);
                              next[i] = Number(e.target.value);
                              updateSelectedElement({ columnWidths: next });
                            }} />
                        ))}
                      </div>
                    </div>

                    <div className="editor-form-group">
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>{t('elementInspector.table.cellContent')}</span>
                      <div style={{ overflowX: 'auto', marginTop: 4 }}>
                        <table style={{ borderCollapse: 'collapse', width: '100%' }}>
                          <tbody>
                            {Array.from({ length: rows }).map((_, r) => (
                              <tr key={r}>
                                {Array.from({ length: cols }).map((_, c) => (
                                  <td key={c} style={{ padding: 0, border: '1px solid #e2e8f0' }}>
                                    <input type="text"
                                      value={selectedElement.cellData?.[r]?.[c] ?? ''}
                                      placeholder={r === 0 && selectedElement.headerRow ? `H${c + 1}` : r === rows - 1 && selectedElement.footerRow ? `F${c + 1}` : ''}
                                      onChange={(e) => {
                                        const data: string[][] = Array.from({ length: rows }, (_, rr) =>
                                          Array.from({ length: cols }, (__, cc) => selectedElement.cellData?.[rr]?.[cc] ?? '')
                                        );
                                        data[r][c] = e.target.value;
                                        updateSelectedElement({ cellData: data });
                                      }}
                                      style={{ width: '100%', border: 'none', padding: '2px 4px', fontSize: 10, background: 'transparent', outline: 'none' }} />
                                  </td>
                                ))}
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>

                    <div className="editor-form-group">
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>{t('elementInspector.table.cellStyle')}</span>
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6, marginTop: 4 }}>
                        <label>
                          <span>{t('elementInspector.table.row')}</span>
                          <input type="number" min="1" max={rows} value={selectedCellRow + 1}
                            onChange={(e) => setSelectedCell(Math.min(Math.max(Number(e.target.value) - 1, 0), rows - 1), selectedCellCol)} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.colLabel')}</span>
                          <input type="number" min="1" max={cols} value={selectedCellCol + 1}
                            onChange={(e) => setSelectedCell(selectedCellRow, Math.min(Math.max(Number(e.target.value) - 1, 0), cols - 1))} />
                        </label>
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginTop: 6 }}>
                        {Array.from({ length: rows }).map((_, r) => (
                          Array.from({ length: cols }).map((__, c) => {
                            const hasStyle = cellStyles.some((cs) => cs.row === r && cs.col === c);
                            const active = r === selectedCellRow && c === selectedCellCol;
                            return (
                              <button key={`${r}-${c}`} type="button"
                                className={`editor-toggle-btn${active ? ' active' : ''}`}
                                title={hasStyle ? t('elementInspector.table.cellTitleStyled', { row: r + 1, col: c + 1 }) : t('elementInspector.table.cellTitle', { row: r + 1, col: c + 1 })}
                                onClick={() => setSelectedCell(r, c)}
                                style={{ minWidth: 28, height: 24, borderColor: hasStyle ? '#7c3aed' : undefined }}>
                                {r + 1}:{c + 1}
                              </button>
                            );
                          })
                        ))}
                      </div>
                      <div className="editor-form-grid" style={{ marginTop: 8 }}>
                        <label>
                          <span>{t('elementInspector.table.background')}</span>
                          <input type="color" value={activeCellStyle?.backgroundColor || '#ffffff'}
                            onChange={(e) => updateCellStyle({ backgroundColor: e.target.value })} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.textColor')}</span>
                          <input type="color" value={activeCellStyle?.color || '#111827'}
                            onChange={(e) => updateCellStyle({ color: e.target.value })} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.fontSize')}</span>
                          <input type="number" min="1" value={activeCellStyle?.fontSize ?? ''}
                            onChange={(e) => updateCellStyle({ fontSize: e.target.value === '' ? undefined : Number(e.target.value) })} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.padding')}</span>
                          <input type="number" min="0" value={activeCellStyle?.padding ?? ''}
                            onChange={(e) => updateCellStyle({ padding: e.target.value === '' ? undefined : Number(e.target.value) })} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.borderColor')}</span>
                          <input type="color" value={activeCellStyle?.borderColor || '#e2e8f0'}
                            onChange={(e) => updateCellStyle({ borderColor: e.target.value })} />
                        </label>
                        <label>
                          <span>{t('elementInspector.table.borderWidth')}</span>
                          <input type="number" min="0" value={activeCellStyle?.borderWidth ?? ''}
                            onChange={(e) => updateCellStyle({ borderWidth: e.target.value === '' ? undefined : Number(e.target.value) })} />
                        </label>
                      </div>
                      <label style={{ marginTop: 6 }}>
                        <span>{t('elementInspector.table.fontFamily')}</span>
                        <select value={activeCellStyle?.fontFamily || ''}
                          onChange={(e) => updateCellStyle({ fontFamily: e.target.value || undefined })}>
                          <option value="">{t('elementInspector.table.fontFamilyDefault')}</option>
                          {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                        </select>
                      </label>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
                        <div className="editor-toggle-group">
                          {(['left', 'center', 'right'] as const).map((align) => (
                            <button key={align} type="button"
                              className={`editor-toggle-btn${activeCellStyle?.textAlign === align ? ' active' : ''}`}
                              title={t('elementInspector.table.alignTitle', { align })}
                              onClick={() => updateCellStyle({ textAlign: activeCellStyle?.textAlign === align ? undefined : align })}>
                              {align === 'left' ? <FiAlignLeft size={11} /> : align === 'center' ? <FiAlignCenter size={11} /> : <FiAlignRight size={11} />}
                            </button>
                          ))}
                        </div>
                        <button type="button" className={`editor-toggle-btn${activeCellStyle?.bold ? ' active' : ''}`}
                          title={t('elementInspector.table.bold')} onClick={() => updateCellStyle({ bold: activeCellStyle?.bold ? undefined : true })}>
                          <FiBold size={11} />
                        </button>
                        <button type="button" className={`editor-toggle-btn${activeCellStyle?.italic ? ' active' : ''}`}
                          title={t('elementInspector.table.italic')} onClick={() => updateCellStyle({ italic: activeCellStyle?.italic ? undefined : true })}>
                          <FiItalic size={11} />
                        </button>
                        <button type="button" className="editor-toggle-btn" title={t('elementInspector.table.clearCellStyle')} onClick={clearCellStyle}>
                          <FiTrash2 size={11} />
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })()}

              {selectedElement.type === 'chart' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.chart.chartType')}</span>
                    <select
                      value={selectedElement.chartType || 'bar'}
                      onChange={(event) => updateSelectedElement({ chartType: event.target.value as 'bar' | 'line' | 'pie' })}
                    >
                      <option value="bar">{t('elementInspector.chart.typeBar')}</option>
                      <option value="line">{t('elementInspector.chart.typeLine')}</option>
                      <option value="pie">{t('elementInspector.chart.typePie')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.chart.chartData')}</span>
                    <textarea
                      rows={5}
                      value={JSON.stringify(selectedElement.chartData || createDefaultChartData(), null, 2)}
                      onChange={(event) => {
                        try {
                          const data = JSON.parse(event.target.value);
                          updateSelectedElement({ chartData: data });
                        } catch {
                          // Invalid JSON, do nothing
                        }
                      }}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'line' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.line.color')}</span>
                    <input
                      type="color"
                      value={selectedElement.style?.backgroundColor || '#9ca3af'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.line.thickness')}</span>
                    <input
                      type="number"
                      min="1"
                      value={selectedElement.height}
                      onChange={(event) => updateSelectedElement({ height: Math.max(1, Number(event.target.value)) })}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'link' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.link.displayText')}</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(e) => updateSelectedElement({ content: e.target.value })}
                      placeholder={t('elementInspector.link.displayTextPlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.link.url')}</span>
                    <input
                      type="text"
                      value={selectedElement.href || ''}
                      onChange={(e) => updateSelectedElement({ href: e.target.value })}
                      placeholder={t('elementInspector.link.urlPlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.link.target')}</span>
                    <select
                      value={selectedElement.linkTarget || '_blank'}
                      onChange={(e) => updateSelectedElement({ linkTarget: e.target.value as '_blank' | '_self' })}
                    >
                      <option value="_blank">{t('elementInspector.link.targetBlank')}</option>
                      <option value="_self">{t('elementInspector.link.targetSelf')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input type="number" value={selectedElement.style?.fontSize || 14} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.link.color')}</span>
                      <input type="color" value={selectedElement.style?.color || '#2563eb'} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'number' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.number.value')}</span>
                    <input
                      type="number"
                      step="any"
                      value={selectedElement.numberValue ?? 0}
                      onChange={(e) => updateSelectedElement({ numberValue: Number(e.target.value) })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.number.style')}</span>
                    <select
                      value={selectedElement.numberStyle || 'decimal'}
                      onChange={(e) => updateSelectedElement({ numberStyle: e.target.value as SimpleElement['numberStyle'] })}
                    >
                      <option value="decimal">{t('elementInspector.number.styleDecimal')}</option>
                      <option value="currency">{t('elementInspector.number.styleCurrency')}</option>
                      <option value="percent">{t('elementInspector.number.stylePercent')}</option>
                      <option value="scientific">{t('elementInspector.number.styleScientific')}</option>
                      <option value="ordinal">{t('elementInspector.number.styleOrdinal')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.number.decimals')}</span>
                      <input type="number" min="0" max="10" value={selectedElement.numberDecimals ?? 2} onChange={(e) => updateSelectedElement({ numberDecimals: Number(e.target.value) })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.number.locale')}</span>
                      <input type="text" value={selectedElement.numberLocale || 'de-DE'} onChange={(e) => updateSelectedElement({ numberLocale: e.target.value })} placeholder="de-DE" />
                    </label>
                    {selectedElement.numberStyle === 'currency' && (
                      <label>
                        <span>{t('elementInspector.number.currency')}</span>
                        <input type="text" value={selectedElement.numberCurrency || 'EUR'} onChange={(e) => updateSelectedElement({ numberCurrency: e.target.value })} placeholder="EUR" />
                      </label>
                    )}
                    <label>
                      <span>{t('elementInspector.common.fontSize')}</span>
                      <input type="number" value={selectedElement.style?.fontSize || 18} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.number.color')}</span>
                      <input type="color" value={selectedElement.style?.color || '#111827'} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })} />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.number.prefix')}</span>
                      <input type="text" value={selectedElement.prefix || ''} onChange={(e) => updateSelectedElement({ prefix: e.target.value })} placeholder={t('elementInspector.number.prefixPlaceholder')} />
                    </label>
                    <label>
                      <span>{t('elementInspector.number.suffix')}</span>
                      <input type="text" value={selectedElement.suffix || ''} onChange={(e) => updateSelectedElement({ suffix: e.target.value })} placeholder={t('elementInspector.number.suffixPlaceholder')} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'draw' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.draw.tool')}</span>
                    <select value={selectedElement.drawTool || 'pen'} onChange={(event) => updateSelectedElement({ drawTool: event.target.value as SimpleElement['drawTool'] })}>
                      <option value="pen">{t('elementInspector.draw.toolPen')}</option>
                      <option value="highlighter">{t('elementInspector.draw.toolHighlighter')}</option>
                      <option value="eraser">{t('elementInspector.draw.toolEraser')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.draw.pathData')}</span>
                    <textarea rows={3} value={selectedElement.pathData || ''} onChange={(event) => updateSelectedElement({ pathData: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.draw.color')}</span>
                      <input type="color" value={selectedElement.style?.color || '#1d4ed8'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.draw.stroke')}</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.draw.opacity')}</span>
                      <input type="number" min="0" max="1" step="0.05" value={selectedElement.style?.opacity ?? 1} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, opacity: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'highlight' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.highlight.mode')}</span>
                    <select value={selectedElement.markMode || 'rectangle'} onChange={(event) => updateSelectedElement({ markMode: event.target.value as SimpleElement['markMode'] })}>
                      <option value="rectangle">{t('elementInspector.highlight.modeRectangle')}</option>
                      <option value="text">{t('elementInspector.highlight.modeText')}</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>{t('elementInspector.highlight.color')}</span>
                      <input type="color" value={selectedElement.style?.backgroundColor || '#fde047'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.highlight.opacity')}</span>
                      <input type="number" min="0" max="1" step="0.05" value={selectedElement.style?.opacity ?? 0.45} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, opacity: Number(event.target.value) } })} />
                    </label>
                    <label>
                      <span>{t('elementInspector.highlight.radius')}</span>
                      <input type="number" min="0" value={selectedElement.style?.borderRadius ?? 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, borderRadius: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'pageboundary' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.pageboundary.boundary')}</span>
                    <select value={selectedElement.pageBoundaryMode || 'start'} onChange={(event) => updateSelectedElement({ pageBoundaryMode: event.target.value as SimpleElement['pageBoundaryMode'] })}>
                      <option value="start">{t('elementInspector.pageboundary.boundaryStart')}</option>
                      <option value="end">{t('elementInspector.pageboundary.boundaryEnd')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.pageboundary.label')}</span>
                    <input type="text" value={selectedElement.content || ''} onChange={(event) => updateSelectedElement({ content: event.target.value })} />
                  </label>
                  <label>
                    <span>{t('elementInspector.pageboundary.color')}</span>
                    <input type="color" value={selectedElement.style?.color || '#7c3aed'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                  </label>
                </div>
              )}

              {(selectedElement.type === 'footnote' || selectedElement.type === 'endnote') && (
                <div className="editor-form-stack">
                  <label>
                    <span>{selectedElement.type === 'footnote' ? t('elementInspector.footnote.footnoteText') : t('elementInspector.footnote.endnoteText')}</span>
                    <textarea
                      rows={4}
                      value={selectedElement.footnoteText || ''}
                      onChange={(e) => updateSelectedElement({ footnoteText: e.target.value })}
                      placeholder={t('elementInspector.footnote.placeholder')}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'bookmark' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.bookmark.name')}</span>
                    <input
                      type="text"
                      value={selectedElement.bookmarkName || ''}
                      onChange={(e) => updateSelectedElement({ bookmarkName: e.target.value })}
                      placeholder={t('elementInspector.bookmark.namePlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.bookmark.linkTarget')}</span>
                    <input
                      type="text"
                      value={selectedElement.bookmarkTarget || ''}
                      onChange={(e) => updateSelectedElement({ bookmarkTarget: e.target.value })}
                      placeholder={t('elementInspector.bookmark.linkTargetPlaceholder')}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'comment' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.comment.text')}</span>
                    <textarea
                      rows={4}
                      value={selectedElement.commentText || ''}
                      onChange={(e) => updateSelectedElement({ commentText: e.target.value })}
                      placeholder={t('elementInspector.comment.textPlaceholder')}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.comment.author')}</span>
                    <input
                      type="text"
                      value={selectedElement.commentAuthor || ''}
                      onChange={(e) => updateSelectedElement({ commentAuthor: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.comment.date')}</span>
                    <input
                      type="date"
                      value={selectedElement.commentDate || ''}
                      onChange={(e) => updateSelectedElement({ commentDate: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.comment.id')}</span>
                    <input
                      type="text"
                      value={selectedElement.commentId || ''}
                      onChange={(e) => updateSelectedElement({ commentId: e.target.value })}
                      placeholder={t('elementInspector.comment.idPlaceholder')}
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'contentcontrol' && (
                <div className="editor-form-stack">
                  <label>
                    <span>{t('elementInspector.contentcontrol.controlType')}</span>
                    <select
                      value={selectedElement.contentControlType || 'richText'}
                      onChange={(e) => updateSelectedElement({ contentControlType: e.target.value as any })}
                    >
                      <option value="richText">{t('elementInspector.contentcontrol.typeRichText')}</option>
                      <option value="plainText">{t('elementInspector.contentcontrol.typePlainText')}</option>
                      <option value="date">{t('elementInspector.contentcontrol.typeDate')}</option>
                      <option value="comboBox">{t('elementInspector.contentcontrol.typeComboBox')}</option>
                      <option value="picture">{t('elementInspector.contentcontrol.typePicture')}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.contentcontrol.title')}</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlTitle || ''}
                      onChange={(e) => updateSelectedElement({ contentControlTitle: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.contentcontrol.tag')}</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlTag || ''}
                      onChange={(e) => updateSelectedElement({ contentControlTag: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.contentcontrol.placeholderText')}</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlPlaceholder || ''}
                      onChange={(e) => updateSelectedElement({ contentControlPlaceholder: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t('elementInspector.contentcontrol.defaultContent')}</span>
                    <textarea
                      rows={3}
                      value={selectedElement.content || ''}
                      onChange={(e) => updateSelectedElement({ content: e.target.value })}
                    />
                  </label>
                </div>
              )}

              {/* ── Visibility ── */}
              <div className="editor-settings-section">
                <div className="editor-settings-heading">
                  <FiEye />
                  <span>{t('elementInspector.visibility.heading')}</span>
                </div>
                <div className="editor-form-stack" style={{ padding: 12 }}>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={!selectedElement.hidden}
                      onChange={(e) => updateSelectedElement({ hidden: !e.target.checked })}
                    />
                    <span>{t('elementInspector.visibility.visibleInOutput')}</span>
                  </label>
                  <label>
                    <span>{t('elementInspector.visibility.visibleExpression')}</span>
                    <textarea
                      rows={2}
                      value={selectedElement.visibleExpression || ''}
                      onChange={(e) => updateSelectedElement({ visibleExpression: e.target.value || undefined })}
                    />
                  </label>
                </div>
              </div>

              {/* ── Word / DOCX metadata — always last ── */}
              <div className="editor-settings-section">
                <div className="editor-settings-heading">
                  <FiFileText />
                  <span>{t('elementInspector.wordDocx.heading')}</span>
                </div>
                <div className="editor-form-stack" style={{ padding: 12 }}>
                  <label>
                    <span>{t('elementInspector.wordDocx.paragraphStyle')}</span>
                    <select
                      value={selectedElement.styleName ?? ''}
                      onChange={(e) => updateSelectedElement({ styleName: e.target.value || undefined })}
                    >
                      <option value="">{t('elementInspector.wordDocx.none')}</option>
                      {(pageSettings.namedStyles ?? [])
                        .filter(s => s.type === 'paragraph' || s.type === 'list')
                        .map(s => <option key={s.id} value={s.id}>{s.name || s.id}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>{t('elementInspector.wordDocx.characterStyle')}</span>
                    <select
                      value={selectedElement.characterStyle ?? ''}
                      onChange={(e) => updateSelectedElement({ characterStyle: e.target.value || undefined })}
                    >
                      <option value="">{t('elementInspector.wordDocx.none')}</option>
                      {(pageSettings.namedStyles ?? [])
                        .filter(s => s.type === 'character')
                        .map(s => <option key={s.id} value={s.id}>{s.name || s.id}</option>)}
                    </select>
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={selectedElement.autoHyphenation ?? true}
                      onChange={(e) => updateSelectedElement({ autoHyphenation: e.target.checked })}
                    />
                    <span>{t('elementInspector.wordDocx.autoHyphenation')}</span>
                  </label>
                  <label>
                    <span>{t('elementInspector.wordDocx.revisionType')}</span>
                    <select
                      value={selectedElement.revisionType ?? ''}
                      onChange={(e) => updateSelectedElement({ revisionType: e.target.value as any || undefined })}
                    >
                      <option value="">{t('elementInspector.wordDocx.none')}</option>
                      <option value="insert">{t('elementInspector.wordDocx.revisionInsert')}</option>
                      <option value="delete">{t('elementInspector.wordDocx.revisionDelete')}</option>
                      <option value="format">{t('elementInspector.wordDocx.revisionFormat')}</option>
                    </select>
                  </label>
                  {selectedElement.revisionType && (
                    <>
                      <label>
                        <span>{t('elementInspector.wordDocx.revisionAuthor')}</span>
                        <input
                          type="text"
                          value={selectedElement.revisionAuthor ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionAuthor: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.wordDocx.revisionDate')}</span>
                        <input
                          type="date"
                          value={selectedElement.revisionDate ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionDate: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>{t('elementInspector.wordDocx.revisionId')}</span>
                        <input
                          type="text"
                          value={selectedElement.revisionId ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionId: e.target.value })}
                          placeholder={t('elementInspector.wordDocx.autoGenerated')}
                        />
                      </label>
                    </>
                  )}
                </div>
              </div>

              <button
                className="editor-danger-button"
                onClick={() => {
                  deleteElementById(selectedElement.id);
                  setSelectedElementId(null);
                }}
              >
                <FiTrash2 />
                <span>{t('elementInspector.deleteElement')}</span>
              </button>
            </div>
          )}
        </aside>
      </main>

      {/* Context menu */}
      {contextMenu && (
        <>
          <div className="editor-context-menu-backdrop" onClick={closeContextMenu} />
          <div
            className="editor-context-menu"
            style={{ left: contextMenu.x, top: contextMenu.y }}
          >
            {contextMenu.elementId ? (() => {
              const el = [...elements, ...sharedElements].find(e => e.id === contextMenu.elementId);
              return (
                <>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('copy')}>
                    {t('contextMenu.copy')}<span className="editor-context-menu-shortcut">⌘C</span>
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('duplicate')}>
                    {t('contextMenu.duplicate')}<span className="editor-context-menu-shortcut">⌘D</span>
                  </button>
                  <button
                    className={`editor-context-menu-item${!clipboard ? ' disabled' : ''}`}
                    onClick={() => contextMenuAction('paste')}
                  >
                    {t('contextMenu.paste')}<span className="editor-context-menu-shortcut">⌘V</span>
                  </button>
                  <div className="editor-context-menu-separator" />
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('lock')}>
                    {el?.locked ? t('contextMenu.unlock') : t('contextMenu.lock')}
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('hide')}>
                    {el?.hidden ? t('contextMenu.show') : t('contextMenu.hide')}
                  </button>
                  <div className="editor-context-menu-separator" />
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('front')}>
                    {t('contextMenu.bringToFront')}
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('forward')}>
                    {t('contextMenu.bringForward')}
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('backward')}>
                    {t('contextMenu.sendBackward')}
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('back')}>
                    {t('contextMenu.sendToBack')}
                  </button>
                  <div className="editor-context-menu-separator" />
                  {(pageSettings.activeLanguages ?? []).length >= 1 && (
                    <button
                      className="editor-context-menu-item danger"
                      onClick={() => {
                        if (!el) { closeContextMenu(); return; }
                        // Also delete any elementGroup siblings (language mirrors)
                        if (el.elementGroup) {
                          const allEls = [...elements, ...sharedElements];
                          allEls.filter(other => other.elementGroup === el.elementGroup)
                            .forEach(sib => deleteElementById(sib.id));
                        } else {
                          deleteElementById(el.id);
                        }
                        closeContextMenu();
                      }}
                      title={t('contextMenu.deleteFromAllLanguagesTitle')}
                    >
                      {t('contextMenu.deleteFromAllLanguages')}<span className="editor-context-menu-shortcut">Del</span>
                    </button>
                  )}
                  <button className="editor-context-menu-item danger" onClick={() => contextMenuAction('delete')}>
                    {t('contextMenu.delete')}<span className="editor-context-menu-shortcut">Del</span>
                  </button>
                </>
              );
            })() : (
              <>
                <button
                  className={`editor-context-menu-item${!clipboard ? ' disabled' : ''}`}
                  onClick={() => contextMenuAction('paste')}
                >
                  {t('contextMenu.paste')}<span className="editor-context-menu-shortcut">⌘V</span>
                </button>
                <button className="editor-context-menu-item" onClick={() => contextMenuAction('selectAll')}>
                  {t('contextMenu.selectAll')}<span className="editor-context-menu-shortcut">⌘A</span>
                </button>
              </>
            )}
          </div>
        </>
      )}

      <CodeViewer
        isOpen={codeViewerOpen}
        onClose={() => setCodeViewerOpen(false)}
        template={template as any}
        pages={pages}
        sharedElements={sharedElements}
        pageSettings={pageSettings}
        currentPreviewLanguage={currentPreviewLanguage}
      />

      {findReplaceOpen && (
        <FindReplaceModal
          template={template as any}
          pages={pages}
          sharedElements={sharedElements}
          pageSettings={pageSettings}
          onClose={() => setFindReplaceOpen(false)}
          onApply={(updatedPages, updatedShared) => {
            bulkReplaceContent(updatedPages, updatedShared);
            setFindReplaceOpen(false);
          }}
        />
      )}

      {formBlockModalOpen && (
        <FormBlockModal
          onClose={() => setFormBlockModalOpen(false)}
          onInsert={(newElements) => {
            snapshotHistory();
            newElements.forEach(el => onElementAdd(nameElement(el)));
            setFormBlockModalOpen(false);
          }}
        />
      )}

      {helpModalOpen && (
        <HelpModal
          selectedElementType={selectedElement?.type ?? null}
          onClose={() => setHelpModalOpen(false)}
        />
      )}
    </div>
  );
};

export default SimplePxaSurface;
