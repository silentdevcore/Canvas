import React, { useEffect, useMemo, useRef, useState } from 'react';
import type { Template, SimpleElement, LayerDirection, PageSettings, Page, PdfEncryption, PdfEncryptionPermissions } from '@/types';
import { useEditorStore, DEFAULT_PAGE_SETTINGS } from '@/store';
import { toDisplay, fromDisplay } from '@/utils/units';
import { getPageSettingsWarnings } from '@/utils/pageValidation';
import { installImportedFontFaces } from '@/utils/importedFonts';
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


interface SimpleCanvasProps {
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
  isRtlCanvas?: boolean;
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
const RTL_LANGS = new Set(['ar', 'he', 'fa', 'ur', 'yi', 'dv']);
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
  'Noto Sans Arabic', 'Noto Sans Hebrew', 'Noto Sans SC', 'Noto Sans TC',
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
  { tag: 'he', label: '🇮🇱 עברית', rtl: true },
  { tag: 'fa', label: '🇮🇷 فارسی', rtl: true },
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

const SimpleCanvas: React.FC<SimpleCanvasProps> = ({
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
}) => {
  const [selectedElementId, setSelectedElementId] = useState<string | null>(null);
  const [selectedElementIds, setSelectedElementIds] = useState<Set<string>>(new Set());
  const [dragState, setDragState] = useState<DragState | null>(null);
  const [resizeState, setResizeState] = useState<ResizeState | null>(null);
  const [rotateState, setRotateState] = useState<RotateState | null>(null);
  const [draggingPageIndex, setDraggingPageIndex] = useState<number | null>(null);
  const [dragOverPageIndex, setDragOverPageIndex] = useState<number | null>(null);
  const [isDragOverCanvas, setIsDragOverCanvas] = useState(false);
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
  const [topbarToast, setTopbarToast] = useState('');
  const [extractingPage, setExtractingPage] = useState<number | null>(null);
  // Language Scope UI selection: 'lang' = current tab selected, 'all' = All selected
  const [scopeShowAll, setScopeShowAll] = useState(false);
  const pageContentRef = useRef<HTMLDivElement | null>(null);
  const contentInputRef = useRef<HTMLInputElement>(null);

  const showTopbarToast = (msg: string) => {
    setTopbarToast(msg);
    setTimeout(() => setTopbarToast(''), 3000);
  };

  useEffect(() => {
    installImportedFontFaces(
      'canvas-imported-font-faces-editor',
      [...pages.flatMap(page => page.elements), ...sharedElements]
    );
  }, [pages, sharedElements]);


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
      showTopbarToast('Design cloned with new ID');
    } catch (err) {
      showTopbarToast(err instanceof Error ? err.message : 'Clone failed');
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
      showTopbarToast(`Page ${pageIndex + 1} extracted`);
    } catch (err) {
      showTopbarToast(err instanceof Error ? err.message : 'Extract failed');
    } finally {
      setExtractingPage(null);
    }
  };

  const { pageSettings, updatePageSettings, settingsModifiedSinceExport, snapshotHistory, undo, redo, bulkReplaceContent, currentPreviewLanguage, setCurrentPreviewLanguage, helpModalOpen, setHelpModalOpen, documentMode, setDocumentMode } = useEditorStore();
  const pageWidth = pageSettings.width;
  const pageHeight = pageSettings.height;
  const isCurrentRtl = RTL_LANGS.has((currentPreviewLanguage || '').split('-')[0]);
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
      label: 'Text',
      hint: 'Single line text block',
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
      label: 'QR Code',
      hint: 'Scannable link block',
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
      label: 'Barcode',
      hint: 'Product or order code',
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
      label: 'Signature',
      hint: 'Approval line',
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
      label: 'Rich Text',
      hint: 'Formatted HTML copy',
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
      label: 'Text Field',
      hint: 'Fillable form input',
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
      label: 'Text Area',
      hint: 'Multi-line fillable text input',
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
      label: 'Checkbox',
      hint: 'Single-choice field',
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
      label: 'Image',
      hint: 'Image placeholder',
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
      label: 'Shape',
      hint: 'Generic shape block',
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
      label: 'Table',
      hint: 'Tabular content area',
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
      label: 'Line',
      hint: 'Divider element',
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
      label: 'Rectangle',
      hint: 'Filled rectangle',
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
      label: 'Circle',
      hint: 'Circular shape',
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
      label: 'Chart',
      hint: 'Data chart block',
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
      label: 'Subsection',
      hint: 'Nested content section',
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
      label: 'Area',
      hint: 'Layout area container',
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
      label: 'Button',
      hint: 'Action button',
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
      label: 'Dropdown',
      hint: 'Select input',
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
      label: 'Option List',
      hint: 'List of choices',
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
      label: 'Radio Group',
      hint: 'Single-select options',
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
      label: 'Watermark',
      hint: 'Global text or image mark',
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
      label: 'Notiz',
      hint: 'Document annotation',
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
      label: 'Arrow',
      hint: 'Direction marker',
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
      label: 'Draw',
      hint: 'Freehand vector stroke',
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
      label: 'Date',
      hint: 'Static or dynamic date',
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
      label: 'Highlight',
      hint: 'Transparent highlight',
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
      label: 'Checkmark',
      hint: 'Check, cross or dot mark',
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
      label: 'Page Start/End',
      hint: 'Page boundary marker',
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
      label: 'Nummerierung',
      hint: 'Page number placeholder',
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
      label: 'Table of Contents',
      hint: 'Auto-generated TOC from headings',
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
      label: 'Link',
      hint: 'Hyperlink element',
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
      label: 'Number',
      hint: 'Formatted number value',
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
      label: 'Footnote',
      hint: 'DOCX footnote reference',
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
      label: 'Endnote',
      hint: 'DOCX endnote reference',
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
      label: 'Bookmark',
      hint: 'Named anchor for cross-references',
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
      label: 'Comment',
      hint: 'Word-native margin comment',
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
      label: 'Content Control',
      hint: 'Structured Word content control (SDT)',
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
      label: 'Text Elements',
      toolIds: ['text', 'richtext', 'link']
    },
    {
      id: 'form',
      label: 'Form Elements',
      toolIds: ['field', 'textarea', 'checkbox', 'button', 'dropdown', 'optionlist', 'radio', 'signature', 'number']
    },
    {
      id: 'visual',
      label: 'Visual Elements',
      toolIds: ['image', 'qrcode', 'barcode', 'chart']
    },
    {
      id: 'layout',
      label: 'Shapes and Layout',
      toolIds: ['shape', 'rect', 'circle', 'line', 'table', 'subsection', 'area']
    },
    {
      id: 'advanced',
      label: 'Advanced Document Elements',
      toolIds: ['watermark', 'note', 'arrow', 'draw', 'date', 'highlight', 'checkmark', 'pageboundary', 'pagenumber', 'toc']
    },
    {
      id: 'word',
      label: 'Word / DOCX Elements',
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

  const wordElementsOnCanvas = useMemo(
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

  const getCanvasPoint = (clientX: number, clientY: number) => {
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
      const point = getCanvasPoint(event.clientX, event.clientY);

      if (dragState.multi && dragState.multi.length > 1) {
        const dx = point.x - dragState.startPointerX;
        const dy = point.y - dragState.startPointerY;
        const dxStored = dragState.isRtlCanvas ? -dx : dx;
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
        if (dragState.isRtlCanvas) {
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

      const point = getCanvasPoint(event.clientX, event.clientY);
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
      const point = getCanvasPoint(event.clientX, event.clientY);
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
      const point = getCanvasPoint(event.clientX, event.clientY);
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
      const point = getCanvasPoint(event.clientX, event.clientY);
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
    const point = getCanvasPoint(event.clientX, event.clientY);
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
    const point = getCanvasPoint(event.clientX, event.clientY);
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
    const point = getCanvasPoint(clientX, clientY);
    setDrawGhost({
      startX: point.x,
      startY: point.y,
      currentX: point.x,
      currentY: point.y,
      pathPoints: drawingMode === 'draw' ? `M ${point.x} ${point.y}` : undefined,
    });
  };

  const handleCanvasPointerDown = (event: React.PointerEvent) => {
    if (event.button !== 0) return;
    closeContextMenu();

    if (drawingMode) {
      event.stopPropagation();
      startDrawGhost(event.clientX, event.clientY);
      return;
    }

    if (event.target !== event.currentTarget) return;

    if (!event.shiftKey && !event.metaKey && !event.ctrlKey) clearSelection();
    const point = getCanvasPoint(event.clientX, event.clientY);
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
    const point = getCanvasPoint(clientX, clientY);
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

  const handleCanvasDrop = (event: React.DragEvent) => {
    event.preventDefault();
    setIsDragOverCanvas(false);

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
    const point = getCanvasPoint(event.clientX, event.clientY);

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
      isRtlCanvas: isCurrentRtl,
      langKey,
    });
  };

  const handleElementContextMenu = (event: React.MouseEvent, element: SimpleElement) => {
    event.preventDefault();
    event.stopPropagation();
    selectOne(element.id);
    setContextMenu({ x: event.clientX, y: event.clientY, elementId: element.id });
  };

  const handleCanvasContextMenu = (event: React.MouseEvent) => {
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
      warnings.push('Selected range needs a page range value.');
    }

    if (element.type === 'date' && element.dateMode === 'binding' && !element.binding?.trim()) {
      warnings.push('Binding mode needs a data binding path.');
    }

    if (element.type === 'draw' && !element.pathData?.trim()) {
      warnings.push('Draw element needs SVG path data.');
    }

    if (element.style?.opacity !== undefined && (element.style.opacity < 0 || element.style.opacity > 1)) {
      warnings.push('Opacity should be between 0 and 1.');
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
          <span>QR Code</span>
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
          <span>Barcode</span>
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
          <small>Signature Line</small>
        </div>
      );
    }

    if (element.type === 'richtext') {
      return (
        <div
          className="editor-richtext"
          dangerouslySetInnerHTML={{ __html: element.htmlContent || '' }}
        />
      );
    }

    if (element.type === 'field') {
      return (
        <div className="editor-form-field">
          <span>
            {resolveContent(element.fieldLabel)}
            {element.required && <span className="editor-field-required-badge" title="Required field">*</span>}
          </span>
          <strong>{element.required ? 'Required' : 'Optional'}</strong>
        </div>
      );
    }

    if (element.type === 'textarea') {
      return (
        <div className="editor-form-field editor-form-field--textarea">
          <span>
            {resolveContent(element.fieldLabel)}
            {element.required && <span className="editor-field-required-badge" title="Required field">*</span>}
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
      return (
        <div className="editor-checkbox-field">
          <FiCheckSquare />
          <span>{resolveContent(element.fieldLabel)}</span>
        </div>
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
            alt="Image"
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
        <ListTag style={{ ...baseStyle, listStyleType: style, paddingLeft: '20px', margin: 0 }}>
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
              {firstDataset?.label || 'Chart'}
            </span>
            <small style={{ fontSize: 10, color: '#475569' }}>{element.chartType || 'bar'}</small>
          </div>

          {!hasValues && (
            <div className="editor-placeholder editor-placeholder-wide" style={{ flex: 1 }}>
              <FiLayers className="editor-placeholder-icon" />
              <span>No chart data</span>
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
          <h2>Desktop required</h2>
          <p>The editor is designed for larger screens. Please open it on a desktop or tablet.</p>
        </div>
      </div>

      {topbarToast && (
        <div className="editor-toast" role="status" aria-live="polite">{topbarToast}</div>
      )}

      <header className="editor-topbar">
        <div className="editor-brand">
          <button className="editor-icon-button" onClick={onBack} aria-label="Back to gallery" title="Back to gallery">
            <FiArrowLeft />
          </button>
          <div>
            <div className="editor-kicker">UI Designer</div>
            <h1>{template.name}</h1>
          </div>
          <div className="editor-brand-menu-wrap">
            <button
              className="editor-icon-button"
              title="Design actions"
              onClick={() => setTopbarMenuOpen(v => !v)}
              aria-label="Design actions"
            >
              <FiMoreVertical size={15} />
            </button>
            {topbarMenuOpen && (
              <>
                <div className="editor-brand-menu-backdrop" onClick={() => setTopbarMenuOpen(false)} />
                <div className="editor-brand-menu">
                  <button onClick={handleCloneDesign}>
                    <FiCopy size={13} />
                    Clone design
                  </button>
                </div>
              </>
            )}
          </div>
        </div>

        <div className="editor-topbar-actions">
          <div className="editor-status-pill">
            <FiMonitor />
            <span>{pageWidth} × {pageHeight} px</span>
          </div>
          <div className="editor-undo-redo">
            <button
              className="editor-icon-button"
              title="Undo (⌘Z)"
              onClick={undo}
            >
              <FiRefreshCw style={{ transform: 'scaleX(-1)' }} />
            </button>
            <button
              className="editor-icon-button"
              title="Redo (⌘⇧Z)"
              onClick={redo}
            >
              <FiRefreshCw />
            </button>
          </div>
          <motion.button
            className={`editor-icon-button ${settingsModifiedSinceExport ? 'editor-icon-button--pending' : ''}`}
            title={settingsModifiedSinceExport ? 'Page Settings — changes not yet exported' : 'Page Settings'}
            onClick={() => setSelectedElementId(null)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiSettings />
            {settingsModifiedSinceExport && <span className="editor-pending-dot" aria-hidden="true" />}
          </motion.button>
          <motion.button
            className="editor-icon-button"
            title="Find &amp; Replace"
            onClick={() => setFindReplaceOpen(true)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiSearch />
          </motion.button>
          <motion.button
            className="editor-icon-button"
            title="Export code (JSON / C#)"
            onClick={() => setCodeViewerOpen(true)}
            whileHover={{ y: -1 }}
            whileTap={{ scale: 0.98 }}
          >
            <FiCode />
          </motion.button>
          <div className="editor-doc-mode-toggle" title="Output mode: affects which elements are shown in the toolbar">
            <button
              className={`editor-doc-mode-btn${documentMode === 'pdf' ? ' editor-doc-mode-btn--active' : ''}`}
              onClick={() => setDocumentMode('pdf')}
            >PDF</button>
            <button
              className={`editor-doc-mode-btn${documentMode === 'word' ? ' editor-doc-mode-btn--active' : ''}`}
              onClick={() => setDocumentMode('word')}
            >Word</button>
          </div>
          <motion.button
            className="editor-icon-button"
            title="Help (F1)"
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
            <span>Preview</span>
          </motion.button>
        </div>
      </header>

      <main className="editor-workspace">
        <aside className="editor-panel editor-tool-panel" aria-label="Element tools">
          <div className="editor-panel-heading">
            <FiPlus />
            <span>Add elements</span>
          </div>

          {documentMode === 'pdf' && wordElementsOnCanvas && (
            <div className="editor-doc-mode-warning">
              Some elements on the canvas are Word-only and will not render in PDF export.
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
            title="Insert a pre-built group of form fields (address, contact, etc.)"
          >
            <FiGrid size={14} />
            Insert Form Block
          </button>

          <div className="editor-layer-summary">
            <div>
              <FiLayers />
              <span>Layers</span>
            </div>
            <strong>{elements.length}</strong>
          </div>
        </aside>

        <section className="editor-stage" aria-label="Document canvas">
          <LanguageTabBar />
          <div className="editor-stage-header">
            <div>
              <span>Page {currentPageIndex + 1} / {pages.length}</span>
              <strong>{pageWidth} × {pageHeight} px</strong>
              {(() => {
                const a4 = PAGE_PRESETS['A4'];
                const isA4 = pageWidth === a4.width && pageHeight === a4.height;
                if (isA4) return null;
                const preset = Object.entries(PAGE_PRESETS).find(([, p]) => p.width === pageWidth && p.height === pageHeight);
                return (
                  <span className="editor-page-size-badge">{preset ? preset[0] : 'Custom'}</span>
                );
              })()}
            </div>
            <div className="editor-stage-zoom">
              <button
                className="editor-zoom-btn"
                title="Zoom out (⌘−)"
                onClick={() => setZoomLevel(z => Math.max(0.25, parseFloat((z - 0.25).toFixed(2))))}
              >
                <FiZoomOut />
              </button>
              <span>{Math.round(zoomLevel * 100)}%</span>
              <button
                className="editor-zoom-btn"
                title="Zoom in (⌘+)"
                onClick={() => setZoomLevel(z => Math.min(2, parseFloat((z + 0.25).toFixed(2))))}
              >
                <FiZoomIn />
              </button>
            </div>
          </div>

          {drawingMode && (
            <div className="editor-draw-badge">
              {drawingMode === 'line' ? 'Drawing line' : drawingMode === 'arrow' ? 'Drawing arrow' : 'Freehand drawing'} — click and drag on the canvas &nbsp;·&nbsp; <kbd>Esc</kbd> to cancel
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'center', minHeight: pageHeight * zoomLevel + 48 }}>
            <div
              className={`editor-page ${isDragOverCanvas ? 'is-drag-over' : ''}`}
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
                transformOrigin: 'top center',
                flexShrink: 0,
                alignSelf: 'flex-start',
                cursor: drawingMode ? 'crosshair' : undefined,
              }}
              onPointerDown={handleCanvasPointerDown}
              onContextMenu={handleCanvasContextMenu}
              onDragOver={(event) => {
                event.preventDefault();
                event.dataTransfer.dropEffect = 'copy';
                setIsDragOverCanvas(true);
              }}
              onDragLeave={(event) => {
                if (event.currentTarget === event.target) {
                  setIsDragOverCanvas(false);
                }
              }}
              onDrop={handleCanvasDrop}
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
                  <span className="editor-guide-label">Header</span>
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
                  <span className="editor-guide-label">Footer</span>
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
                      alt="Watermark"
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
                  <h2>Start with an element</h2>
                  <p>Drag a tool from the left onto the PDF page, or click a tool to add it automatically.</p>
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

          {/* Page navigation strip */}
          <div className={`editor-page-strip${pageWidth / pageHeight > 1.5 && pages.length > 1 ? ' editor-page-strip--widescreen' : ''}`}>
            {pages.map((page, index) => (
              <div
                key={page.id}
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
                  title={`Page ${index + 1}`}
                >
                  <span className="editor-page-thumb-num">{index + 1}</span>
                </button>
                <div className="editor-page-thumb-actions">
                  <button title="Duplicate page" onClick={() => onPageDuplicate(index)}><FiCopy size={10} /></button>
                  <button
                    title="Extract page to JSON"
                    onClick={() => handleExtractPage(index)}
                    disabled={extractingPage === index}
                  >
                    {extractingPage === index ? '…' : <FiScissors size={10} />}
                  </button>
                  {pages.length > 1 && (
                    <button title="Delete page" onClick={() => {
                      if (window.confirm(`Delete page ${index + 1}?`)) onPageDelete(index);
                    }}>×</button>
                  )}
                </div>
              </div>
            ))}
            <button className="editor-page-add-btn" onClick={onPageAdd} title="Add page">
              <FiPlus size={14} />
            </button>
          </div>
        </section>

        <aside className="editor-panel editor-inspector-panel" aria-label="Element properties">
          <div className="editor-inspector-tabs">
            <button
              className={`editor-inspector-tab${inspectorTab === 'inspector' ? ' active' : ''}`}
              onClick={() => setInspectorTab('inspector')}
            >
              {selectedElement ? <FiMousePointer size={12} /> : <FiSettings size={12} />}
              {selectedElement ? 'Inspector' : 'Page Settings'}
              {!selectedElement && JSON.stringify(pageSettings) !== JSON.stringify(DEFAULT_PAGE_SETTINGS) && (
                <span className="editor-settings-badge">●</span>
              )}
            </button>
            <button
              className={`editor-inspector-tab${inspectorTab === 'layers' ? ' active' : ''}`}
              onClick={() => setInspectorTab('layers')}
            >
              <FiLayers size={12} /> Layers
              {elements.length > 0 && <span className="editor-layer-count">{elements.length}</span>}
            </button>
            {(pageSettings.activeLanguages ?? []).length >= 1 && (
              <button
                className={`editor-inspector-tab${inspectorTab === 'properties' ? ' active' : ''}`}
                onClick={() => setInspectorTab('properties')}
              >
                <FiGlobe size={12} /> Properties
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
                    <FiLink size={10} /> All pages (header / footer)
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
                          <button title={el.hidden ? 'Show' : 'Hide'}
                            className={`editor-layer-icon-btn${el.hidden ? ' dimmed' : ''}`}
                            onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { hidden: !el.hidden }); }}>
                            <FiEye size={12} /></button>
                          <button title={el.locked ? 'Unlock' : 'Lock'}
                            className={`editor-layer-icon-btn${el.locked ? ' dimmed' : ''}`}
                            onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { locked: !el.locked }); }}>
                            {el.locked ? <FiLock size={12} /> : <FiUnlock size={12} />}</button>
                        </div>
                      </div>
                    );
                  })}
                  <div className="editor-layers-section-header" style={{ marginTop: 4 }}>
                    <FiFileText size={10} /> Page {currentPageIndex + 1}
                  </div>
                </>
              )}
              {elements.length === 0 && sharedElements.length === 0 && (
                <p className="editor-layers-empty">No elements yet. Add one from the toolbar.</p>
              )}
              {elements.length === 0 && sharedElements.length > 0 && (
                <p className="editor-layers-empty" style={{ fontSize: 10 }}>No elements on this page yet.</p>
              )}
              {[...elements].reverse().map((el, i) => {
                const isPrimary = el.id === selectedElementId;
                const isInMulti = selectedElementIds.has(el.id);
                return (
                  <div
                    key={el.id}
                    className={`editor-layer-row${isPrimary ? ' is-primary' : isInMulti ? ' is-multi' : ''}`}
                    onClick={(e) => e.shiftKey ? toggleMultiSelect(el.id) : selectOne(el.id)}
                    title="Shift+click to multi-select"
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
                        title={el.hidden ? 'Show' : 'Hide'}
                        className={`editor-layer-icon-btn${el.hidden ? ' dimmed' : ''}`}
                        onClick={(e) => { e.stopPropagation(); updateElementById(el.id, { hidden: !el.hidden }); }}
                      ><FiEye size={12} /></button>
                      <button
                        title={el.locked ? 'Unlock' : 'Lock'}
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

                {imageAnalysisDiagnostics && (
                  <div className="editor-settings-section">
                    <div className="editor-settings-heading">
                      <FiSliders />
                      <span>Image Analysis</span>
                    </div>
                    <div className="editor-image-analysis-panel">
                      <div className="editor-image-analysis-summary">
                        <div>
                          <strong>{formatNumber(imageAnalysisDiagnostics.elementCount)}</strong>
                          <span>Elements</span>
                        </div>
                        <div>
                          <strong>{formatNumber(imageAnalysisDiagnostics.glyphCount)}</strong>
                          <span>Glyphs</span>
                        </div>
                        <div>
                          <strong>{formatPercent(lowConfidenceShare)}</strong>
                          <span>Low confidence</span>
                        </div>
                      </div>
                      <div className="editor-image-analysis-grid">
                        <span>Source</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.sourceWidthPx)} x {formatNumber(imageAnalysisDiagnostics.sourceHeightPx)} px</strong>
                        <span>Working</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.workingWidthPx)} x {formatNumber(imageAnalysisDiagnostics.workingHeightPx)} px</strong>
                        <span>Scale</span>
                        <strong>{Number(imageAnalysisDiagnostics.scaleFactor ?? 1).toFixed(3)}</strong>
                        <span>Regions</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.colorRegionCount)}</strong>
                        <span>Shapes</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.shapeCount)}</strong>
                        <span>Text lines</span>
                        <strong>{formatNumber(imageAnalysisDiagnostics.textLineCount)}</strong>
                        <span>Words</span>
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
                      <span>Image OCR</span>
                    </div>
                    <div className="editor-image-analysis-panel">
                      <div className="editor-image-analysis-summary">
                        <div>
                          <strong>{formatNumber(imageOcrDiagnostics.wordCount)}</strong>
                          <span>Words</span>
                        </div>
                        <div>
                          <strong>{formatNumber(imageOcrDiagnostics.lineCount)}</strong>
                          <span>Lines</span>
                        </div>
                        <div>
                          <strong>{formatPercent(imageOcrDiagnostics.averageConfidence)}</strong>
                          <span>Confidence</span>
                        </div>
                      </div>
                      <div className="editor-image-analysis-grid">
                        <span>Source</span>
                        <strong>{formatNumber(imageOcrDiagnostics.sourceWidthPx)} x {formatNumber(imageOcrDiagnostics.sourceHeightPx)} px</strong>
                        <span>Pages</span>
                        <strong>{formatNumber(imageOcrDiagnostics.pageCount)}</strong>
                        <span>Languages</span>
                        <strong>{imageOcrDiagnostics.languages ?? 'deu+eng'}</strong>
                        <span>Engine</span>
                        <strong>{imageOcrDiagnostics.ocrEngine ?? 'OCR'} {imageOcrDiagnostics.ocrEngineVersion ?? ''}</strong>
                        <span>Low confidence</span>
                        <strong>{formatPercent(lowConfidenceWordShare)}</strong>
                        <span>Runtime</span>
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
                    <span>Paper</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>Size</span>
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
                        {currentPreset === 'Custom' && <option value="Custom">Custom</option>}
                      </select>
                    </label>
                    <label>
                      <span>Unit</span>
                      <select
                        value={pageSettings.unit}
                        onChange={(e) => updatePageSettings({ unit: e.target.value as PageSettings['unit'] })}
                      >
                        <option value="px">px</option>
                        <option value="pt">pt</option>
                        <option value="mm">mm</option>
                        <option value="cm">cm</option>
                        <option value="in">inch</option>
                      </select>
                    </label>
                    <div className="editor-form-grid">
                      <label>
                        <span>Width ({pageSettings.unit})</span>
                        <input
                          type="number"
                          value={toDisplay(pageSettings.width, pageSettings.unit)}
                          min={toDisplay(100, pageSettings.unit)}
                          step={pageSettings.unit === 'px' || pageSettings.unit === 'pt' ? 1 : 0.1}
                          onChange={(e) => updatePageSettings({ width: Math.max(100, fromDisplay(Number(e.target.value), pageSettings.unit)) })}
                        />
                      </label>
                      <label>
                        <span>Height ({pageSettings.unit})</span>
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
                      >Portrait</button>
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
                      >Landscape</button>
                    </div>
                  </div>
                </div>

                {/* Background */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiDroplet />
                    <span>Background</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>Color</span>
                        <input
                          type="color"
                          value={pageSettings.backgroundColor}
                          onChange={(e) => updatePageSettings({ backgroundColor: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>Fit</span>
                        <select
                          value={pageSettings.backgroundImageFit}
                          onChange={(e) => updatePageSettings({ backgroundImageFit: e.target.value as PageSettings['backgroundImageFit'] })}
                        >
                          <option value="cover">Cover</option>
                          <option value="contain">Contain</option>
                          <option value="fill">Stretch</option>
                          <option value="tile">Tile</option>
                        </select>
                      </label>
                    </div>
                    <label>
                      <span>Image URL</span>
                      <input
                        type="url"
                        placeholder="https://…"
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
                        Remove image
                      </button>
                    )}
                  </div>
                </div>

                {/* Margins */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMaximize2 />
                    <span>Margins</span>
                    <button
                      className={`editor-link-btn ${linkedMargins ? 'is-linked' : ''}`}
                      title={linkedMargins ? 'Unlink margins' : 'Link margins'}
                      onClick={() => setLinkedMargins(l => !l)}
                    >
                      {linkedMargins ? <FiLink /> : <FiLink2 />}
                    </button>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-preset-buttons">
                      {([['None', 0], ['Narrow', 24], ['Normal', 48], ['Wide', 72]] as [string, number][]).map(([label, value]) => (
                        <button
                          key={label}
                          className={`editor-preset-btn ${Object.values(pageSettings.margins).every(v => v === value) ? 'is-active' : ''}`}
                          onClick={() => updatePageSettings({ margins: { top: value, right: value, bottom: value, left: value } })}
                        >
                          {label}
                        </button>
                      ))}
                    </div>
                    <div className="editor-form-grid">
                      {(['top', 'right', 'bottom', 'left'] as const).map(side => (
                        <label key={side}>
                          <span>{side.charAt(0).toUpperCase() + side.slice(1)} ({pageSettings.unit})</span>
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

                {/* Canvas */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiMonitor />
                    <span>Canvas</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.gridVisible}
                        onChange={(e) => updatePageSettings({ gridVisible: e.target.checked })}
                      />
                      <span>Show grid</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.snapToGrid}
                        onChange={(e) => updatePageSettings({ snapToGrid: e.target.checked })}
                      />
                      <span>Snap to grid</span>
                    </label>
                    <label>
                      <span>Grid size (px)</span>
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
                      <span>Show margin guide</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.showSafeArea}
                        onChange={(e) => updatePageSettings({ showSafeArea: e.target.checked })}
                      />
                      <span>Show safe area guide</span>
                    </label>
                  </div>
                </div>

                {/* Header & Footer */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiLayers />
                    <span>Header &amp; Footer</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.headerEnabled}
                        onChange={(e) => updatePageSettings({ headerEnabled: e.target.checked })}
                      />
                      <span>Enable header</span>
                    </label>
                    {pageSettings.headerEnabled && (
                      <>
                        <label>
                          <span>Header height (px)</span>
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
                          <span>Different header on first page</span>
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.headerOddEvenDifferent}
                            onChange={(e) => updatePageSettings({ headerOddEvenDifferent: e.target.checked })}
                          />
                          <span>Different header on odd/even pages</span>
                        </label>
                        <div style={{ borderTop: '1px solid #e2e8f0', paddingTop: 8, marginTop: 2 }}>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>Insert into header</span>
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'text')}>Text</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'pagenumber')}>Page №</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'date')}>Date</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('header', 'image')}>Logo</button>
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
                      <span>Enable footer</span>
                    </label>
                    {pageSettings.footerEnabled && (
                      <>
                        <label>
                          <span>Footer height (px)</span>
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
                          <span>Different footer on first page</span>
                        </label>
                        <label className="editor-checkbox-control">
                          <input
                            type="checkbox"
                            checked={pageSettings.footerOddEvenDifferent}
                            onChange={(e) => updatePageSettings({ footerOddEvenDifferent: e.target.checked })}
                          />
                          <span>Different footer on odd/even pages</span>
                        </label>
                        <div style={{ borderTop: '1px solid #e2e8f0', paddingTop: 8, marginTop: 2 }}>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>Insert into footer</span>
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'text')}>Text</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'pagenumber')}>Page №</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'date')}>Date</button>
                            <button className="editor-secondary-button" onClick={() => insertIntoZone('footer', 'image')}>Logo</button>
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
                    <span>Bleed</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>Bleed size (px) — 0 = off</span>
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
                        Red trim line shows where paper will be cut. Extend background elements to the page edge.
                      </p>
                    )}
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.cropMarks}
                        onChange={(e) => updatePageSettings({ cropMarks: e.target.checked })}
                      />
                      <span>Show crop marks</span>
                    </label>
                  </div>
                </div>

                {/* Global Watermark */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiEyeOff />
                    <span>Global Watermark</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.globalWatermark.enabled}
                        onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, enabled: e.target.checked } })}
                      />
                      <span>Enable watermark</span>
                    </label>
                    {pageSettings.globalWatermark.enabled && (
                      <>
                        <div className="editor-orientation-toggle">
                          <button
                            className={`editor-orient-btn ${pageSettings.globalWatermark.mode === 'text' ? 'is-active' : ''}`}
                            onClick={() => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, mode: 'text' } })}
                          >Text</button>
                          <button
                            className={`editor-orient-btn ${pageSettings.globalWatermark.mode === 'image' ? 'is-active' : ''}`}
                            onClick={() => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, mode: 'image' } })}
                          >Image URL</button>
                        </div>
                        <label>
                          <span>{pageSettings.globalWatermark.mode === 'text' ? 'Text' : 'Image URL'}</span>
                          <input
                            type="text"
                            value={pageSettings.globalWatermark.content}
                            placeholder={pageSettings.globalWatermark.mode === 'text' ? 'e.g. DRAFT' : 'https://...'}
                            onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, content: e.target.value } })}
                          />
                        </label>
                        <div className="editor-form-grid">
                          <label>
                            <span>Opacity (0–1)</span>
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
                            <span>Rotation (°)</span>
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
                                <span>Font size (px)</span>
                                <input
                                  type="number"
                                  min={12}
                                  max={200}
                                  value={pageSettings.globalWatermark.fontSize}
                                  onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, fontSize: Math.max(12, Number(e.target.value)) } })}
                                />
                              </label>
                              <label>
                                <span>Color</span>
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
                          <span>Page scope</span>
                          <select
                            value={pageSettings.globalWatermark.pageScope}
                            onChange={(e) => updatePageSettings({ globalWatermark: { ...pageSettings.globalWatermark, pageScope: e.target.value as PageSettings['globalWatermark']['pageScope'] } })}
                          >
                            <option value="all">All pages</option>
                            <option value="first">First page only</option>
                            <option value="odd">Odd pages</option>
                            <option value="even">Even pages</option>
                            <option value="range">Page range</option>
                          </select>
                        </label>
                        {pageSettings.globalWatermark.pageScope === 'range' && (
                          <label>
                            <span>Page range (e.g. 2-5)</span>
                            <input
                              type="text"
                              value={pageSettings.globalWatermark.pageRange}
                              placeholder="e.g. 2-5"
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
                    <span>Page Numbering</span>
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
                      <span>Enable page numbers</span>
                    </label>
                    {pageSettings.pageNumbering.enabled && (
                      <>
                        <label>
                          <span>Format</span>
                          <select
                            value={pageSettings.pageNumbering.format}
                            onChange={(e) => updatePageSettings({
                              pageNumbering: { ...pageSettings.pageNumbering, format: e.target.value as PageSettings['pageNumbering']['format'] }
                            })}
                          >
                            <option value="pageOfTotal">Page X of Y</option>
                            <option value="current">Page number only</option>
                            <option value="total">Total pages only</option>
                            <option value="roman">Roman numerals</option>
                            <option value="alphabetic">Alphabetic</option>
                          </select>
                        </label>
                        <div className="editor-form-grid">
                          <label>
                            <span>Start at</span>
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
                            <span>Prefix</span>
                            <input
                              type="text"
                              value={pageSettings.pageNumbering.prefix}
                              placeholder="e.g. Page "
                              onChange={(e) => updatePageSettings({
                                pageNumbering: { ...pageSettings.pageNumbering, prefix: e.target.value }
                              })}
                            />
                          </label>
                          <label>
                            <span>Suffix</span>
                            <input
                              type="text"
                              value={pageSettings.pageNumbering.suffix}
                              placeholder="e.g.  | Draft"
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
                          <span>Show on first page</span>
                        </label>
                        <div>
                          <span style={{ fontSize: 11, color: '#64748b', display: 'block', marginBottom: 6 }}>Placement</span>
                          <div className="editor-placement-grid">
                            {(['top-left', 'top-center', 'top-right', 'bottom-left', 'bottom-center', 'bottom-right'] as const).map(pos => (
                              <button
                                key={pos}
                                type="button"
                                className={`editor-placement-btn ${pageSettings.pageNumbering.placement === pos ? 'is-active' : ''}`}
                                title={pos.replace(/-/g, ' ')}
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
                          Place on canvas
                        </button>
                      </>
                    )}
                  </div>
                </div>

                {/* Export Metadata */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiFileText />
                    <span>Export Metadata</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(['title', 'author', 'subject', 'keywords'] as const).map(key => (
                      <label key={key}>
                        <span>{key.charAt(0).toUpperCase() + key.slice(1)}</span>
                        <input
                          type="text"
                          value={pageSettings.metadata[key]}
                          placeholder={key === 'keywords' ? 'comma-separated' : ''}
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
                    <span>Export Defaults</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>PDF quality</span>
                      <select
                        value={pageSettings.exportDefaults.quality}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, quality: e.target.value as PageSettings['exportDefaults']['quality'] } })}
                      >
                        <option value="screen">Screen (72 dpi)</option>
                        <option value="ebook">eBook (150 dpi)</option>
                        <option value="printer">Printer (300 dpi)</option>
                        <option value="prepress">Prepress (400 dpi)</option>
                      </select>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.embedFonts}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, embedFonts: e.target.checked } })}
                      />
                      <span>Embed fonts</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.compressImages}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, compressImages: e.target.checked } })}
                      />
                      <span>Compress images</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.exportDefaults.accessibilityTagged}
                        onChange={(e) => updatePageSettings({ exportDefaults: { ...pageSettings.exportDefaults, accessibilityTagged: e.target.checked } })}
                      />
                      <span>Accessibility (tagged PDF)</span>
                    </label>
                  </div>
                </div>

                {/* Pagination Behavior */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiList />
                    <span>Pagination</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>Page breaks</span>
                      <select
                        value={pageSettings.pagination.autoBreaks ? 'auto' : 'manual'}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, autoBreaks: e.target.value === 'auto' } })}
                      >
                        <option value="auto">Automatic</option>
                        <option value="manual">Manual only (page boundary elements)</option>
                      </select>
                    </label>
                    <label>
                      <span>Section start</span>
                      <select
                        value={pageSettings.pagination.sectionStartBehavior}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, sectionStartBehavior: e.target.value as PageSettings['pagination']['sectionStartBehavior'] } })}
                      >
                        <option value="continue">Continue on same page</option>
                        <option value="new-page">Start on new page</option>
                        <option value="odd-page">Start on odd page</option>
                        <option value="even-page">Start on even page</option>
                      </select>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.pagination.repeatTableHeader}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, repeatTableHeader: e.target.checked } })}
                      />
                      <span>Repeat table header on each page</span>
                    </label>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.pagination.keepWithNext}
                        onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, keepWithNext: e.target.checked } })}
                      />
                      <span>Keep headings with following content</span>
                    </label>
                    <div className="editor-form-grid">
                      <label>
                        <span>Orphan lines (min)</span>
                        <input
                          type="number"
                          min={1}
                          max={5}
                          value={pageSettings.pagination.orphanLines}
                          onChange={(e) => updatePageSettings({ pagination: { ...pageSettings.pagination, orphanLines: Math.max(1, Number(e.target.value)) } })}
                        />
                      </label>
                      <label>
                        <span>Widow lines (min)</span>
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
                    <span>Track Changes</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label className="editor-checkbox-control">
                      <input
                        type="checkbox"
                        checked={pageSettings.trackChanges ?? false}
                        onChange={(e) => updatePageSettings({ trackChanges: e.target.checked })}
                      />
                      <span>Enable revision tracking in DOCX export</span>
                    </label>
                  </div>
                </div>

                {/* Document Protection */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiLock />
                    <span>Document Protection</span>
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
                      <span>Enable protection</span>
                    </label>
                    {pageSettings.protection?.enabled && (
                      <>
                        <label>
                          <span>Restriction mode</span>
                          <select
                            value={pageSettings.protection.mode}
                            onChange={(e) => updatePageSettings({
                              protection: { ...pageSettings.protection!, mode: e.target.value as any },
                            })}
                          >
                            <option value="readOnly">Read-only</option>
                            <option value="comments">Comments only</option>
                            <option value="trackedChanges">Tracked changes only</option>
                            <option value="formFields">Form fields only</option>
                          </select>
                        </label>
                        <label>
                          <span>Password hash (optional)</span>
                          <input
                            type="text"
                            placeholder="Leave blank for no password"
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
                    <span>PDF Encryption</span>
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
                      <span>Encrypt PDF with a password</span>
                    </label>
                    {pageSettings.encryption?.enabled && (
                      <>
                        <label>
                          <span>User password (to open)</span>
                          <input
                            type="password"
                            placeholder="Leave blank to open without a prompt"
                            value={pageSettings.encryption.userPassword}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, userPassword: e.target.value },
                            })}
                          />
                        </label>
                        <label>
                          <span>Owner password (permissions)</span>
                          <input
                            type="password"
                            placeholder="Defaults to the user password"
                            value={pageSettings.encryption.ownerPassword}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, ownerPassword: e.target.value },
                            })}
                          />
                        </label>
                        <label>
                          <span>Algorithm</span>
                          <select
                            value={pageSettings.encryption.algorithm}
                            onChange={(e) => updatePageSettings({
                              encryption: { ...pageSettings.encryption!, algorithm: e.target.value as PdfEncryption['algorithm'] },
                            })}
                          >
                            <option value="Rc4_128">RC4 128-bit</option>
                            <option value="Aes128" disabled>AES 128-bit (coming soon)</option>
                          </select>
                        </label>
                        <div className="editor-settings-subheading" style={{ marginTop: 4 }}>Permissions</div>
                        {([
                          ['print', 'Printing'],
                          ['copy', 'Copy / extract text'],
                          ['modify', 'Modify contents'],
                          ['annotate', 'Annotate & fill forms'],
                          ['fillForms', 'Fill form fields only'],
                          ['extractAccessibility', 'Extract for accessibility'],
                          ['assemble', 'Assemble (insert/rotate/delete pages)'],
                          ['printHighResolution', 'High-resolution printing'],
                        ] as [keyof PdfEncryptionPermissions, string][]).map(([key, label]) => (
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
                            <span>{label}</span>
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
                    <span>Custom Properties</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(pageSettings.customProperties ?? []).map((prop, i) => (
                      <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 80px 28px', gap: 4, alignItems: 'center' }}>
                        <input
                          type="text"
                          placeholder="Name"
                          value={prop.name}
                          onChange={(e) => {
                            const next = [...(pageSettings.customProperties ?? [])];
                            next[i] = { ...next[i], name: e.target.value };
                            updatePageSettings({ customProperties: next });
                          }}
                        />
                        <input
                          type="text"
                          placeholder="Value"
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
                          <option value="text">text</option>
                          <option value="number">number</option>
                          <option value="boolean">boolean</option>
                          <option value="date">date</option>
                        </select>
                        <button
                          className="editor-icon-button"
                          title="Remove"
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
                      <FiPlus size={13} /> Add property
                    </button>
                  </div>
                </div>

                {/* Languages */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiGlobe />
                    <span>Languages</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div style={{ fontSize: 11, color: '#64748b', marginBottom: 6 }}>
                      System language: <strong>{navigator.language}</strong> (auto-detected, used as fallback)
                    </div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                      {LOCALIZATION_LANGUAGES.map(({ tag, label }) => {
                        const active = (pageSettings.activeLanguages ?? []).includes(tag);
                        return (
                          <label key={tag} style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, cursor: 'pointer' }}>
                            <input
                              type="checkbox"
                              checked={active}
                              onChange={(e) => {
                                const current = pageSettings.activeLanguages ?? [];
                                if (e.target.checked) {
                                  updatePageSettings({ activeLanguages: [...current, tag] });
                                  if (!currentPreviewLanguage || currentPreviewLanguage === navigator.language.split('-')[0])
                                    setCurrentPreviewLanguage(tag);
                                } else {
                                  const next = current.filter(l => l !== tag);
                                  updatePageSettings({ activeLanguages: next });
                                  if (currentPreviewLanguage === tag)
                                    setCurrentPreviewLanguage(next[0] ?? navigator.language.split('-')[0]);
                                }
                              }}
                            />
                            {label}
                          </label>
                        );
                      })}
                    </div>
                  </div>
                </div>

                {/* Named Styles */}
                <div className="editor-settings-section">
                  <div className="editor-settings-heading">
                    <FiType />
                    <span>Named Styles</span>
                  </div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    {(pageSettings.namedStyles ?? []).map((ns, i) => (
                      <div key={i} style={{ border: '1px solid var(--editor-border)', borderRadius: 6, padding: 8, marginBottom: 4 }}>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 28px', gap: 4, marginBottom: 4 }}>
                          <input
                            type="text"
                            placeholder="ID"
                            value={ns.id}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], id: e.target.value };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                          <input
                            type="text"
                            placeholder="Display name"
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
                            <option value="paragraph">paragraph</option>
                            <option value="character">character</option>
                            <option value="list">list</option>
                            <option value="table">table</option>
                          </select>
                          <button
                            className="editor-icon-button"
                            title="Remove"
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
                            placeholder="Based on (ID)"
                            value={ns.basedOn ?? ''}
                            onChange={(e) => {
                              const next = [...(pageSettings.namedStyles ?? [])];
                              next[i] = { ...next[i], basedOn: e.target.value || undefined };
                              updatePageSettings({ namedStyles: next });
                            }}
                          />
                          <input
                            type="text"
                            placeholder="Next style (ID)"
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
                      <FiPlus size={13} /> Add style
                    </button>
                  </div>
                </div>

                {/* Reset */}
                <button
                  className="editor-danger-button"
                  onClick={() => updatePageSettings(DEFAULT_PAGE_SETTINGS)}
                >
                  <FiRefreshCw />
                  Reset to defaults
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
                  placeholder="Element name…"
                  value={selectedElement.name ?? ''}
                  onChange={(e) => updateSelectedElement({ name: e.target.value })}
                />
              </div>

              {isOutsideMargins(selectedElement) && (
                <div className="editor-validation-panel">
                  <span>Element is outside the margin safe area.</span>
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
                      Layer {idx + 1} of {elements.length}
                    </span>
                    <div className="editor-layer-buttons">
                      <button className="editor-layer-btn" title="Send to back" disabled={isBottom} onClick={() => onElementReorder(selectedElement.id, 'back')}><FiChevronsDown /></button>
                      <button className="editor-layer-btn" title="Send backward" disabled={isBottom} onClick={() => onElementReorder(selectedElement.id, 'backward')}><FiArrowDown /></button>
                      <button className="editor-layer-btn" title="Bring forward" disabled={isTop} onClick={() => onElementReorder(selectedElement.id, 'forward')}><FiArrowUp /></button>
                      <button className="editor-layer-btn" title="Bring to front" disabled={isTop} onClick={() => onElementReorder(selectedElement.id, 'front')}><FiChevronsUp /></button>
                    </div>
                  </div>
                );
              })()}

              <div className="editor-layer-controls">
                <span className="editor-layer-label">
                  {selectedElement.locked ? <FiLock /> : <FiUnlock />}
                  {selectedElement.locked ? 'Locked' : 'Editable'}
                </span>
                <div className="editor-layer-buttons">
                  <button
                    className="editor-layer-btn"
                    title="Duplicate"
                    onClick={() => duplicateElement(selectedElement)}
                  >
                    <FiCopy />
                  </button>
                  <button
                    className="editor-layer-btn"
                    title={selectedElement.locked ? 'Unlock' : 'Lock'}
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
                    <span>Language Scope</span>
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
                          ? `Position edits apply to ${currentPreviewLanguage.toUpperCase()} only`
                          : `Switch to ${currentPreviewLanguage.toUpperCase()}-only editing`}
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
                        title={scopeShowAll ? 'Position edits apply to all language tabs' : 'Switch to all-languages editing'}
                      >
                        All
                      </button>
                    </div>
                  </div>
                </div>
              )}

              <div className="editor-form-grid">
                <label>
                  <span>X</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).x}
                    onChange={(event) => updateLayoutValue('x', event.target.value)}
                  />
                </label>
                <label>
                  <span>Y</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).y}
                    onChange={(event) => updateLayoutValue('y', event.target.value)}
                  />
                </label>
                <label>
                  <span>Width</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).width}
                    onChange={(event) => updateLayoutValue('width', event.target.value)}
                  />
                </label>
                <label>
                  <span>Height</span>
                  <input
                    type="number"
                    value={getEffectivePos(selectedElement).height}
                    onChange={(event) => updateLayoutValue('height', event.target.value)}
                  />
                </label>
                <label>
                  <span>Rotation °</span>
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
                    title="Reset rotation"
                    onClick={() => {
                      const langKey = isMultilingual && !scopeShowAll && currentPreviewLanguage ? currentPreviewLanguage : undefined;
                      if (langKey) {
                        applyPosUpdate(selectedElement.id, { rotation: 0 }, langKey);
                      } else {
                        updateSelectedElement({ style: { ...selectedElement.style, rotation: 0 } });
                      }
                    }}
                  >
                    <FiRotateCw size={13} /> Reset
                  </button>
                </label>
              </div>

              {/* Alignment toolbar */}
              <div className="editor-align-toolbar">
                <span className="editor-align-label">Align</span>
                <div className="editor-align-buttons">
                  <button title="Align left edges to margin" onClick={() => alignSelected('left')}><FiAlignLeft size={14} /></button>
                  <button title="Align horizontal centers" onClick={() => alignSelected('hcenter')}><FiAlignCenter size={14} /></button>
                  <button title="Align right edges to margin" onClick={() => alignSelected('right')}><FiAlignRight size={14} /></button>
                  <button title="Align top edges to margin" onClick={() => alignSelected('top')}><FiAlignLeft size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                  <button title="Align vertical centers" onClick={() => alignSelected('vcenter')}><FiAlignJustify size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                  <button title="Align bottom edges to margin" onClick={() => alignSelected('bottom')}><FiAlignRight size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                </div>
                {selectedElementIds.size >= 3 && (
                  <>
                    <span className="editor-align-label" style={{ marginLeft: 4 }}>Distribute</span>
                    <div className="editor-align-buttons">
                      <button title="Distribute horizontally" onClick={() => distributeSelected('horizontal')}><FiAlignJustify size={14} /></button>
                      <button title="Distribute vertically" onClick={() => distributeSelected('vertical')}><FiAlignJustify size={14} style={{ transform: 'rotate(90deg)' }} /></button>
                    </div>
                  </>
                )}
              </div>

              {/* ── Heading Level (text / richtext) ── */}
              {(selectedElement.type === 'text' || selectedElement.type === 'richtext') && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiBookOpen /><span>Heading Level</span></div>
                  <div className="editor-form-stack" style={{ padding: '8px 12px' }}>
                    <select
                      value={selectedElement.headingLevel ?? ''}
                      onChange={e => updateSelectedElement({ headingLevel: e.target.value === '' ? null : Number(e.target.value) as 1 | 2 | 3 })}
                    >
                      <option value="">None (body text)</option>
                      <option value="1">Heading 1</option>
                      <option value="2">Heading 2</option>
                      <option value="3">Heading 3</option>
                    </select>
                    <small style={{ color: '#64748b', fontSize: 11 }}>Headings are included in the Table of Contents element.</small>
                  </div>
                </div>
              )}

              {/* ── Form field: tab order + validation ── */}
              {(['field', 'checkbox', 'radio', 'dropdown', 'optionlist', 'signature'] as const).includes(selectedElement.type as any) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiGrid /><span>Form & Validation</span></div>
                  <div className="editor-form-stack" style={{ padding: '8px 12px', gap: 6 }}>
                    <label className="editor-prop-row">
                      <span>Tab index</span>
                      <input type="number" min={0} step={1}
                        value={selectedElement.tabIndex ?? ''}
                        placeholder="auto"
                        onChange={e => updateSelectedElement({ tabIndex: e.target.value === '' ? undefined : Number(e.target.value) })}
                      />
                    </label>
                    {(selectedElement.type === 'field' || selectedElement.type === 'textarea') && (
                      <>
                        <label className="editor-prop-row">
                          <span>Min length</span>
                          <input type="number" min={0} step={1}
                            value={selectedElement.validationMin ?? ''}
                            placeholder="—"
                            onChange={e => updateSelectedElement({ validationMin: e.target.value === '' ? undefined : Number(e.target.value) })}
                          />
                        </label>
                        <label className="editor-prop-row">
                          <span>Max length</span>
                          <input type="number" min={0} step={1}
                            value={selectedElement.validationMax ?? ''}
                            placeholder="—"
                            onChange={e => updateSelectedElement({ validationMax: e.target.value === '' ? undefined : Number(e.target.value) })}
                          />
                        </label>
                        <label className="editor-prop-row">
                          <span>Pattern (regex)</span>
                          <input type="text"
                            value={selectedElement.validationPattern ?? ''}
                            placeholder="e.g. \\d{5}"
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
                    <div className="editor-settings-heading"><FiBookOpen /><span>Table of Contents</span></div>
                    <div className="editor-form-stack" style={{ padding: '8px 12px', gap: 10 }}>

                      {/* Title */}
                      <label className="editor-label">
                        <span>Title</span>
                        <input
                          className="editor-input"
                          type="text"
                          value={selectedElement.tocTitle ?? 'Table of Contents'}
                          onChange={e => updateSelectedElement({ tocTitle: e.target.value })}
                          placeholder="Table of Contents"
                        />
                      </label>

                      {/* Heading level range */}
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        <label className="editor-label">
                          <span>Min level</span>
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
                          <span>Max level</span>
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
                          Show page numbers
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
                          <input
                            type="checkbox"
                            checked={selectedElement.tocShowLeaderDots ?? true}
                            onChange={e => updateSelectedElement({ tocShowLeaderDots: e.target.checked })}
                          />
                          Show leader dots
                        </label>
                      </div>

                      {/* Status */}
                      {!hasHeadings ? (
                        <div className="editor-toc-warning">
                          No heading-level elements found. Select a text element and set a Heading Level in the inspector.
                        </div>
                      ) : (
                        <small style={{ color: '#64748b', fontSize: 11 }}>
                          {filteredCount} entr{filteredCount !== 1 ? 'ies' : 'y'} (H{minLevel}–H{maxLevel}) across {pages.length} page{pages.length !== 1 ? 's' : ''}
                          {(selectedElement.tocEntries?.length ?? 0) > 0 && (
                            <> · last updated: {selectedElement.tocEntries!.length} entries</>
                          )}
                        </small>
                      )}

                      {/* Update button */}
                      <button
                        className="editor-toc-update-btn"
                        onClick={updateToc}
                        disabled={!hasHeadings}
                        title={!hasHeadings ? 'Assign heading levels to text elements first' : 'Scan all pages and rebuild the TOC entry list'}
                      >
                        <FiBookOpen size={14} /> Update TOC
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
                    <div className="editor-settings-heading"><FiType /><span>Content</span></div>
                    <div className="editor-form-stack" style={{ padding: 12 }}>
                      <input
                        ref={contentInputRef}
                        type="text"
                        placeholder="Text content or {{KEY}}"
                        value={selectedElement.content || ''}
                        onChange={(e) => updateSelectedElement({ content: e.target.value })}
                      />
                      {(globalProps.length > 0 || ownProps.length > 0) && (
                        <div style={{ marginTop: 6 }}>
                          {globalProps.length > 0 && (
                            <div style={{ marginBottom: 4 }}>
                              <div style={{ fontSize: 10, color: '#64748b', marginBottom: 3, fontWeight: 600, letterSpacing: '0.04em' }}>GLOBAL</div>
                              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                                {globalProps.map(p => (
                                  <button
                                    key={p.key}
                                    onClick={() => insertProperty(p.key)}
                                    title={`Insert {{${p.key}}} — global property`}
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
                                OWN · {curLang.toUpperCase()}
                              </div>
                              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                                {ownProps.map(p => (
                                  <button
                                    key={p.key}
                                    onClick={() => insertProperty(p.key)}
                                    title={`Insert {{${p.key}}} — own property for ${curLang}`}
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
                  <div className="editor-settings-heading"><FiSliders /><span>Image Analysis Glyphs</span></div>
                  <div className="editor-glyph-debug-list">
                    {getGlyphDiagnostics(selectedElement).map((glyph, index) => {
                      const weights = topGlyphWeights(glyph.decisionWeights);
                      return (
                        <div className="editor-glyph-debug-row" key={`${glyph.value ?? '?'}-${index}`}>
                          <div className="editor-glyph-debug-main">
                            <span className="editor-glyph-debug-char">{glyph.value || '?'}</span>
                            <div>
                              <strong>{glyph.method || 'unknown'}</strong>
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
                    <span>HTML content</span>
                    <textarea
                      rows={5}
                      value={selectedElement.htmlContent || ''}
                      onChange={(event) => updateSelectedElement({ htmlContent: event.target.value })}
                      placeholder="<p>Your <strong>rich</strong> text here</p>"
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'field' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Field label</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder="Label text or {{key}}"
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>Use {'{{key}}'} for localized values</small>
                  </label>
                  <label>
                    <span>Field name</span>
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
                    <span>Required field</span>
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={(selectedElement.style?.backgroundColor ?? '#ffffff') !== 'transparent'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.checked ? '#ffffff' : 'transparent' } })}
                    />
                    <span>Fill background</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'textarea' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Field label</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder="Label text or {{key}}"
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>Use {'{{key}}'} for localized values</small>
                  </label>
                  <label>
                    <span>Field name</span>
                    <input
                      type="text"
                      value={selectedElement.fieldName || ''}
                      onChange={(event) => updateSelectedElement({ fieldName: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>Placeholder text</span>
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
                    <span>Required field</span>
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={(selectedElement.style?.backgroundColor ?? '#ffffff') !== 'transparent'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.checked ? '#ffffff' : 'transparent' } })}
                    />
                    <span>Fill background</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'checkbox' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Checkbox label</span>
                    <input
                      type="text"
                      value={selectedElement.fieldLabel || ''}
                      onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })}
                      placeholder="Label or {{key}}"
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>Use {'{{key}}'} for localized values</small>
                  </label>
                  <label>
                    <span>Field name</span>
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
                    <span>Required field</span>
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
                    <span>Label</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ content: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>Action type</span>
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
                      <option value="none">— None —</option>
                      <option value="url">Open URL</option>
                      <option value="page">Go to page</option>
                      <option value="submit">Submit form</option>
                      <option value="reset">Reset form</option>
                    </select>
                  </label>
                  {actionType === 'url' && (
                    <label>
                      <span>URL</span>
                      <input
                        type="text"
                        value={selectedElement.buttonAction || ''}
                        onChange={(event) => updateSelectedElement({ buttonAction: event.target.value })}
                        placeholder="https://example.com"
                      />
                    </label>
                  )}
                  {actionType === 'page' && (
                    <label>
                      <span>Page number</span>
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
                      <span>Background</span>
                      <input
                        type="color"
                        value={selectedElement.style?.backgroundColor || '#3b82f6'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>Text color</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#ffffff'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>Font size</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Radius</span>
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
                    <span>Options (one per line)</span>
                    <textarea
                      rows={4}
                      value={(selectedElement.options || []).join('\n')}
                      onChange={(event) => updateSelectedElement({ options: event.target.value.split('\n').filter(Boolean) })}
                      placeholder={'Option 1\nOption 2\nOption 3'}
                    />
                  </label>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={Boolean(selectedElement.multiSelect)}
                      onChange={(event) => updateSelectedElement({ multiSelect: event.target.checked })}
                    />
                    <span>Multi-select</span>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Font size</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Text color</span>
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
                    <span>List style</span>
                    <select
                      value={selectedElement.listStyle || (selectedElement.ordered ? 'decimal' : 'disc')}
                      onChange={(e) => updateSelectedElement({
                        listStyle: e.target.value,
                        ordered: ['decimal', 'lower-alpha', 'upper-alpha', 'lower-roman', 'upper-roman'].includes(e.target.value),
                      })}
                    >
                      <option value="disc">• Bullet (disc)</option>
                      <option value="circle">○ Circle</option>
                      <option value="square">▪ Square</option>
                      <option value="dash">– Dash</option>
                      <option value="asterisk">* Asterisk</option>
                      <option value="none">No marker</option>
                      <option value="decimal">1. Decimal</option>
                      <option value="lower-alpha">a. Lowercase alpha</option>
                      <option value="upper-alpha">A. Uppercase alpha</option>
                      <option value="lower-roman">i. Lowercase roman</option>
                      <option value="upper-roman">I. Uppercase roman</option>
                    </select>
                  </label>
                  <label>
                    <span>Items (one per line)</span>
                    <textarea
                      rows={4}
                      value={(selectedElement.options || []).join('\n')}
                      onChange={(event) => updateSelectedElement({ options: event.target.value.split('\n').filter(Boolean) })}
                      placeholder={'Item 1\nItem 2\nItem 3'}
                    />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Font size</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Text color</span>
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
                  <span className="editor-form-label">Options</span>
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
                        placeholder={`Option ${idx + 1}`}
                      />
                      <button
                        className="editor-option-remove"
                        title="Remove option"
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
                      options: [...(selectedElement.options || []), `Option ${(selectedElement.options || []).length + 1}`]
                    })}
                  >
                    + Add option
                  </button>
                  <div className="editor-form-grid">
                    <label>
                      <span>Font size</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 14}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Text color</span>
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
                    <span>Label</span>
                    <input type="text" value={selectedElement.fieldLabel || ''} onChange={(event) => updateSelectedElement({ fieldLabel: event.target.value })} placeholder="Label or {{key}}" />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>Use {'{{key}}'} for localized values</small>
                  </label>
                  <label>
                    <span>State</span>
                    <select value={selectedElement.checkState || 'checked'} onChange={(event) => updateSelectedElement({ checkState: event.target.value as SimpleElement['checkState'] })}>
                      <option value="checked">Checked</option>
                      <option value="cross">Cross</option>
                      <option value="dot">Dot</option>
                      <option value="empty">Empty</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Mark color</span>
                      <input type="color" value={selectedElement.style?.color || '#16a34a'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>Stroke</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 3} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'watermark' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Mode</span>
                    <select
                      value={selectedElement.watermarkMode || 'text'}
                      onChange={(event) => updateSelectedElement({ watermarkMode: event.target.value as 'text' | 'image' })}
                    >
                      <option value="text">Text</option>
                      <option value="image">Image</option>
                    </select>
                  </label>
                  <label>
                    <span>{selectedElement.watermarkMode === 'image' ? 'Image URL' : 'Text'}</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ content: event.target.value })}
                    />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Color</span>
                      <input
                        type="color"
                        value={selectedElement.style?.color || '#64748b'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>Opacity</span>
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
                      <span>Rotation</span>
                      <input
                        type="number"
                        value={selectedElement.style?.rotation ?? -24}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, rotation: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Scale</span>
                      <input
                        type="number"
                        min="0.1"
                        step="0.1"
                        value={selectedElement.style?.scale ?? 1}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, scale: Number(event.target.value) } })}
                      />
                    </label>
                    <label>
                      <span>Font size</span>
                      <input
                        type="number"
                        value={selectedElement.style?.fontSize || 42}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(event.target.value) } })}
                      />
                    </label>
                  </div>
                  <label>
                    <span>Page scope</span>
                    <select
                      value={selectedElement.pageScope || 'all'}
                      onChange={(event) => updateSelectedElement({ pageScope: event.target.value as SimpleElement['pageScope'] })}
                    >
                      <option value="all">All pages</option>
                      <option value="current">Current page</option>
                      <option value="first">First page only</option>
                      <option value="last">Last page only</option>
                      <option value="range">Selected range</option>
                    </select>
                  </label>
                  {selectedElement.pageScope === 'range' && (
                    <label>
                      <span>Page range</span>
                      <input
                        type="text"
                        value={selectedElement.pageRange || ''}
                        onChange={(event) => updateSelectedElement({ pageRange: event.target.value })}
                        placeholder="1-3, 5"
                      />
                    </label>
                  )}
                </div>
              )}

              {selectedElement.type === 'note' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Title</span>
                    <input type="text" value={selectedElement.noteTitle || ''} onChange={(event) => updateSelectedElement({ noteTitle: event.target.value })} />
                  </label>
                  <label>
                    <span>Body</span>
                    <textarea rows={4} value={selectedElement.noteBody || ''} onChange={(event) => updateSelectedElement({ noteBody: event.target.value })} />
                  </label>
                  <label>
                    <span>Author</span>
                    <input type="text" value={selectedElement.noteAuthor || ''} onChange={(event) => updateSelectedElement({ noteAuthor: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Note color</span>
                      <input
                        type="color"
                        value={selectedElement.style?.backgroundColor || '#fef3c7'}
                        onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                      />
                    </label>
                    <label>
                      <span>Text color</span>
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
                    <span>Collapsed note</span>
                  </label>
                </div>
              )}

              {selectedElement.type === 'date' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Mode</span>
                    <select value={selectedElement.dateMode || 'static'} onChange={(event) => updateSelectedElement({ dateMode: event.target.value as SimpleElement['dateMode'] })}>
                      <option value="static">Static date</option>
                      <option value="render">Render date</option>
                      <option value="binding">Data binding</option>
                    </select>
                  </label>
                  <label>
                    <span>Static value / fallback</span>
                    <input type="text" value={selectedElement.content || ''} onChange={(event) => updateSelectedElement({ content: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Locale</span>
                      <input type="text" value={selectedElement.locale || 'de-DE'} onChange={(event) => updateSelectedElement({ locale: event.target.value })} />
                    </label>
                    <label>
                      <span>Timezone</span>
                      <input type="text" value={selectedElement.timezone || 'Europe/Berlin'} onChange={(event) => updateSelectedElement({ timezone: event.target.value })} />
                    </label>
                    <label>
                      <span>Format</span>
                      <input type="text" value={selectedElement.dateFormat || 'yyyy-MM-dd'} onChange={(event) => updateSelectedElement({ dateFormat: event.target.value })} />
                    </label>
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.color || '#111827'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'pagenumber' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Format</span>
                    <select value={selectedElement.numberingFormat || 'pageOfTotal'} onChange={(event) => updateSelectedElement({ numberingFormat: event.target.value as SimpleElement['numberingFormat'] })}>
                      <option value="current">Current page</option>
                      <option value="total">Total pages</option>
                      <option value="pageOfTotal">Page X of Y</option>
                      <option value="roman">Roman</option>
                      <option value="alphabetic">Alphabetic</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Start</span>
                      <input type="number" min="1" value={selectedElement.startNumber || 1} onChange={(event) => updateSelectedElement({ startNumber: Number(event.target.value) || 1 })} />
                    </label>
                    <label>
                      <span>Page scope</span>
                      <select value={selectedElement.pageScope || 'all'} onChange={(event) => updateSelectedElement({ pageScope: event.target.value as SimpleElement['pageScope'] })}>
                        <option value="all">All</option>
                        <option value="current">Current</option>
                        <option value="first">First</option>
                        <option value="last">Last</option>
                        <option value="odd">Odd</option>
                        <option value="even">Even</option>
                        <option value="range">Range</option>
                      </select>
                    </label>
                    {selectedElement.pageScope === 'range' && (
                      <label>
                        <span>Page range</span>
                        <input type="text" value={selectedElement.pageRange || ''} onChange={(event) => updateSelectedElement({ pageRange: event.target.value })} placeholder="1-3, 5" />
                      </label>
                    )}
                    <label>
                      <span>Prefix</span>
                      <input type="text" value={selectedElement.prefix || ''} onChange={(event) => updateSelectedElement({ prefix: event.target.value })} />
                    </label>
                    <label>
                      <span>Suffix</span>
                      <input type="text" value={selectedElement.suffix || ''} onChange={(event) => updateSelectedElement({ suffix: event.target.value })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'arrow' && (
                <div className="editor-form-stack">
                  <span className="editor-form-label">Direction</span>
                  <div className="editor-arrow-direction-grid">
                    {(['up', 'left', 'right', 'down'] as const).map(dir => (
                      <button
                        key={dir}
                        className={`editor-arrow-dir-btn${(selectedElement.arrowDirection || 'right') === dir ? ' is-active' : ''}`}
                        onClick={() => updateSelectedElement({ arrowDirection: dir })}
                        title={dir.charAt(0).toUpperCase() + dir.slice(1)}
                      >
                        {dir === 'up' ? '↑' : dir === 'down' ? '↓' : dir === 'left' ? '←' : '→'}
                      </button>
                    ))}
                  </div>
                  <label>
                    <span>Rotation (°)</span>
                    <input
                      type="number"
                      value={selectedElement.arrowRotation ?? 0}
                      onChange={(e) => updateSelectedElement({ arrowRotation: Number(e.target.value) })}
                    />
                  </label>
                  <label>
                    <span>Arrow mode</span>
                    <select value={selectedElement.arrowMode || 'straight'} onChange={(event) => updateSelectedElement({ arrowMode: event.target.value as SimpleElement['arrowMode'] })}>
                      <option value="straight">Straight</option>
                      <option value="elbow">Elbow</option>
                      <option value="curved">Curved</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Start head</span>
                      <select value={selectedElement.startMarker || 'none'} onChange={(event) => updateSelectedElement({ startMarker: event.target.value as SimpleElement['startMarker'] })}>
                        <option value="none">None</option>
                        <option value="filled">▶ Filled</option>
                        <option value="open">▷ Open</option>
                        <option value="dot">● Dot</option>
                        <option value="diamond">◆ Diamond</option>
                        <option value="square">■ Square</option>
                        <option value="circle">○ Circle</option>
                      </select>
                    </label>
                    <label>
                      <span>End head</span>
                      <select value={selectedElement.endMarker || 'filled'} onChange={(event) => updateSelectedElement({ endMarker: event.target.value as SimpleElement['endMarker'] })}>
                        <option value="none">None</option>
                        <option value="filled">▶ Filled</option>
                        <option value="open">▷ Open</option>
                        <option value="dot">● Dot</option>
                        <option value="diamond">◆ Diamond</option>
                        <option value="square">■ Square</option>
                        <option value="circle">○ Circle</option>
                      </select>
                    </label>
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.color || '#dc2626'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>Stroke</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                  </div>
                  <label>
                    <span>Dash style</span>
                    <select value={selectedElement.style?.dashStyle || 'solid'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, dashStyle: event.target.value } })}>
                      <option value="solid">Solid</option>
                      <option value="dashed">Dashed</option>
                      <option value="dotted">Dotted</option>
                    </select>
                  </label>
                </div>
              )}

              {/* ── Shared: Typography ── */}
              {TYPOGRAPHY_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiType /><span>Typography</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <label>
                      <span>Font family</span>
                      <select
                        value={selectedElement.style?.fontFamily || 'Arial'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontFamily: e.target.value } })}
                      >
                        {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>Language</span>
                      <select
                        value={selectedElement.language || ''}
                        onChange={(e) => {
                          const lang = e.target.value;
                          const rtlLangs = new Set(['ar', 'he', 'fa', 'ur', 'yi', 'dv']);
                          const dir = rtlLangs.has(lang.split('-')[0]) ? 'rtl' : 'ltr';
                          updateSelectedElement({
                            language: lang || undefined,
                            textDirection: lang ? dir : undefined,
                          });
                        }}
                      >
                        <option value="">(none)</option>
                        <option value="en">English</option>
                        <option value="de">German</option>
                        <option value="fr">French</option>
                        <option value="es">Spanish</option>
                        <option value="it">Italian</option>
                        <option value="pt">Portuguese</option>
                        <option value="ru">Russian</option>
                        <option value="el">Greek</option>
                        <option value="ar">Arabic (RTL)</option>
                        <option value="he">Hebrew (RTL)</option>
                        <option value="fa">Persian (RTL)</option>
                        <option value="zh-CN">Chinese (Simplified)</option>
                        <option value="zh-TW">Chinese (Traditional)</option>
                        <option value="ja">Japanese</option>
                        <option value="ko">Korean</option>
                        <option value="hi">Hindi</option>
                        <option value="th">Thai</option>
                      </select>
                    </label>
                    <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                      <span style={{ fontSize: 11, color: '#64748b', minWidth: 72 }}>Direction</span>
                      {(['ltr', 'rtl'] as const).map(dir => (
                        <button
                          key={dir}
                          className={`editor-toggle-btn${(selectedElement.textDirection || 'ltr') === dir ? ' active' : ''}`}
                          style={{ flex: 1, fontFamily: 'monospace', fontSize: 11 }}
                          title={dir === 'ltr' ? 'Left to right' : 'Right to left'}
                          onClick={() => updateSelectedElement({ textDirection: dir })}
                        >{dir.toUpperCase()}</button>
                      ))}
                    </div>
                    <div className="editor-form-grid">
                      <label>
                        <span>Font size</span>
                        <input
                          type="number"
                          min={6}
                          max={400}
                          value={selectedElement.style?.fontSize || 14}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })}
                        />
                      </label>
                      <label>
                        <span>Color</span>
                        <input
                          type="color"
                          value={selectedElement.style?.color || '#111827'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>Line height</span>
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
                        <span>Letter spacing</span>
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
                        title="Bold"
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, fontWeight: selectedElement.style?.fontWeight === 'bold' ? 'normal' : 'bold' } })}
                      ><FiBold size={14} /></button>
                      <button
                        className={`editor-toggle-btn${selectedElement.style?.fontStyle === 'italic' ? ' active' : ''}`}
                        title="Italic"
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, fontStyle: selectedElement.style?.fontStyle === 'italic' ? 'normal' : 'italic' } })}
                      ><FiItalic size={14} /></button>
                      <button
                        className={`editor-toggle-btn${selectedElement.style?.textDecoration === 'underline' ? ' active' : ''}`}
                        title="Underline"
                        onClick={() => updateSelectedElement({ style: { ...selectedElement.style, textDecoration: selectedElement.style?.textDecoration === 'underline' ? 'none' : 'underline' } })}
                      ><FiUnderline size={14} /></button>
                      <div className="editor-toggle-separator" />
                      {(['left', 'center', 'right', 'justify'] as const).map((align, i) => {
                        const Icon = [FiAlignLeft, FiAlignCenter, FiAlignRight, FiAlignJustify][i];
                        return (
                          <button
                            key={align}
                            className={`editor-toggle-btn${selectedElement.style?.textAlign === align ? ' active' : ''}`}
                            title={`Align ${align}`}
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
                  <div className="editor-settings-heading"><FiDroplet /><span>Background</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>Color</span>
                        <input
                          type="color"
                          value={selectedElement.style?.backgroundColor && selectedElement.style.backgroundColor !== 'transparent' ? selectedElement.style.backgroundColor : '#ffffff'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>Opacity %</span>
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
                      <span>Transparent (no background)</span>
                    </label>
                  </div>
                </div>
              )}

              {/* ── Shared: Border ── */}
              {BORDER_TYPES.has(selectedElement.type) && (
                <div className="editor-settings-section">
                  <div className="editor-settings-heading"><FiBox /><span>Border</span></div>
                  <div className="editor-form-stack" style={{ padding: 12 }}>
                    <div className="editor-form-grid">
                      <label>
                        <span>Width</span>
                        <input
                          type="number"
                          min={0}
                          max={20}
                          value={selectedElement.style?.borderWidth ?? 0}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderWidth: Number(e.target.value) } })}
                        />
                      </label>
                      <label>
                        <span>Color</span>
                        <input
                          type="color"
                          value={selectedElement.style?.borderColor || '#000000'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderColor: e.target.value } })}
                        />
                      </label>
                      <label>
                        <span>Style</span>
                        <select
                          value={selectedElement.style?.borderStyle || 'solid'}
                          onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderStyle: e.target.value } })}
                        >
                          <option value="none">None</option>
                          <option value="solid">Solid</option>
                          <option value="dashed">Dashed</option>
                          <option value="dotted">Dotted</option>
                          <option value="double">Double</option>
                        </select>
                      </label>
                      <label>
                        <span>Radius</span>
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
                    <FiMaximize2 /><span>Padding</span>
                    <button
                      className={`editor-link-btn${linkedPadding ? ' active' : ''}`}
                      title={linkedPadding ? 'Unlink sides' : 'Link all sides'}
                      onClick={() => setLinkedPadding(p => !p)}
                      style={{ marginLeft: 'auto' }}
                    >
                      {linkedPadding ? <FiLink size={12} /> : <FiLink2 size={12} />}
                    </button>
                  </div>
                  <div className="editor-form-grid" style={{ padding: 12 }}>
                    {linkedPadding ? (
                      <label style={{ gridColumn: '1 / -1' }}>
                        <span>All sides</span>
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
                        <label><span>Top</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingTop ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingTop: Number(e.target.value) } })} />
                        </label>
                        <label><span>Right</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingRight ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingRight: Number(e.target.value) } })} />
                        </label>
                        <label><span>Bottom</span>
                          <input type="number" min={0} max={200} value={selectedElement.style?.paddingBottom ?? 0}
                            onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, paddingBottom: Number(e.target.value) } })} />
                        </label>
                        <label><span>Left</span>
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
                    <span>QR code value</span>
                    <input
                      type="text"
                      value={selectedElement.qrValue || ''}
                      onChange={(event) => updateSelectedElement({ qrValue: event.target.value })}
                      placeholder="https://example.com"
                    />
                  </label>
                  <label>
                    <span>Size</span>
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
                    <span>Barcode value</span>
                    <input
                      type="text"
                      value={selectedElement.barcodeValue || ''}
                      onChange={(event) => updateSelectedElement({ barcodeValue: event.target.value })}
                      placeholder="123456789012"
                    />
                  </label>
                  <label>
                    <span>Type</span>
                    <select
                      value={selectedElement.barcodeType || 'CODE128'}
                      onChange={(event) => updateSelectedElement({ barcodeType: event.target.value })}
                    >
                      <option value="CODE128">Code 128</option>
                      <option value="CODE39">Code 39</option>
                      <option value="EAN13">EAN-13</option>
                      <option value="UPC">UPC</option>
                    </select>
                  </label>
                </div>
              )}

              {selectedElement.type === 'signature' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Signature label</span>
                    <input
                      type="text"
                      value={selectedElement.signatureLabel || ''}
                      onChange={(event) => updateSelectedElement({ signatureLabel: event.target.value })}
                      placeholder="Signature or {{key}}"
                    />
                    <small style={{ color: '#6b7280', fontSize: 10 }}>Use {'{{key}}'} for localized values</small>
                  </label>
                </div>
              )}

              {selectedElement.type === 'image' && (
                <div className="editor-form-stack">
                  <div
                    className="editor-image-dropzone"
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => {
                      e.preventDefault();
                      const file = e.dataTransfer.files[0];
                      if (!file || !file.type.startsWith('image/')) return;
                      const reader = new FileReader();
                      reader.onload = (ev) => updateSelectedElement({ content: ev.target?.result as string });
                      reader.readAsDataURL(file);
                    }}
                  >
                    <span>Drop image here or</span>
                    <label className="editor-image-upload-btn">
                      Browse
                      <input
                        type="file"
                        accept="image/*"
                        style={{ display: 'none' }}
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (!file) return;
                          const reader = new FileReader();
                          reader.onload = (ev) => updateSelectedElement({ content: ev.target?.result as string });
                          reader.readAsDataURL(file);
                        }}
                      />
                    </label>
                  </div>
                  <label>
                    <span>Source URL</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(event) => updateSelectedElement({ content: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>Fit Mode</span>
                    <select
                      value={selectedElement.fitMode || 'contain'}
                      onChange={(event) => updateSelectedElement({ fitMode: event.target.value as 'contain' | 'cover' | 'fill' | 'none' })}
                    >
                      <option value="contain">Contain</option>
                      <option value="cover">Cover</option>
                      <option value="fill">Fill</option>
                      <option value="none">None</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Crop X</span>
                      <input
                        type="number"
                        value={selectedElement.cropX || 0}
                        onChange={(event) => updateSelectedElement({ cropX: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>Crop Y</span>
                      <input
                        type="number"
                        value={selectedElement.cropY || 0}
                        onChange={(event) => updateSelectedElement({ cropY: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>Crop Width</span>
                      <input
                        type="number"
                        value={selectedElement.cropWidth || 0}
                        onChange={(event) => updateSelectedElement({ cropWidth: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>Crop Height</span>
                      <input
                        type="number"
                        value={selectedElement.cropHeight || 0}
                        onChange={(event) => updateSelectedElement({ cropHeight: Number(event.target.value) })}
                      />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>Focal X (%)</span>
                      <input
                        type="number"
                        value={selectedElement.focalX || 50}
                        onChange={(event) => updateSelectedElement({ focalX: Number(event.target.value) })}
                      />
                    </label>
                    <label>
                      <span>Focal Y (%)</span>
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
                    <span>Preserve Aspect Ratio</span>
                  </label>
                </div>
              )}


              {selectedElement.type === 'table' && (() => {
                const cols = selectedElement.style?.columns ?? 3;
                const rows = selectedElement.style?.rows ?? 3;
                const colAligns = selectedElement.columnAlignments ?? Array.from({ length: cols }, () => 'left' as const);
                const colWidths = selectedElement.columnWidths ?? Array.from({ length: cols }, () => 0);
                return (
                  <div className="editor-form-stack">
                    <label>
                      <span>Rows</span>
                      <input type="number" min="1" value={rows}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, rows: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Columns</span>
                      <input type="number" min="1" value={cols}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, columns: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Border Width</span>
                      <input type="number" min="0" value={selectedElement.style?.borderWidth ?? 1}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderWidth: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Border Color</span>
                      <input type="color" value={selectedElement.style?.borderColor || '#000000'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, borderColor: e.target.value } })} />
                    </label>
                    <label>
                      <span>Cell Padding</span>
                      <input type="number" min="0" value={selectedElement.style?.cellPadding ?? 5}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellPadding: Number(e.target.value) } })} />
                    </label>

                    <label>
                      <span>Cell Font Size</span>
                      <input type="number" min="1" value={selectedElement.style?.cellFontSize ?? 10}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Cell Font</span>
                      <select value={selectedElement.style?.cellFontFamily || 'Arial'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontFamily: e.target.value } })}>
                        {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>Cell Text Color</span>
                      <input type="color" value={selectedElement.style?.cellColor || '#555555'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellColor: e.target.value } })} />
                    </label>
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.style?.cellFontWeight === 'bold'}
                        onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, cellFontWeight: e.target.checked ? 'bold' : 'normal' } })} />
                      <span>Bold cell text</span>
                    </label>

                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.headerRow ?? false}
                        onChange={(e) => updateSelectedElement({ headerRow: e.target.checked })} />
                      <span>Header row</span>
                    </label>
                    {(selectedElement.headerRow) && (
                      <label>
                        <span>Header Background</span>
                        <input type="color" value={selectedElement.headerBgColor || '#f1f5f9'}
                          onChange={(e) => updateSelectedElement({ headerBgColor: e.target.value })} />
                      </label>
                    )}
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.footerRow ?? false}
                        onChange={(e) => updateSelectedElement({ footerRow: e.target.checked })} />
                      <span>Footer row</span>
                    </label>
                    <label style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <input type="checkbox" checked={selectedElement.zebraEnabled ?? false}
                        onChange={(e) => updateSelectedElement({ zebraEnabled: e.target.checked })} />
                      <span>Alternating rows</span>
                    </label>
                    {(selectedElement.zebraEnabled) && (
                      <label>
                        <span>Even Row Color</span>
                        <input type="color" value={selectedElement.zebraColor || '#f9fafb'}
                          onChange={(e) => updateSelectedElement({ zebraColor: e.target.value })} />
                      </label>
                    )}

                    <div className="editor-form-group">
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>Column Alignment</span>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 4 }}>
                        {Array.from({ length: cols }).map((_, i) => (
                          <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                            <span style={{ fontSize: 10, color: '#94a3b8', minWidth: 42 }}>Col {i + 1}</span>
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
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>Column Widths (px, 0 = auto)</span>
                      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginTop: 4 }}>
                        {Array.from({ length: cols }).map((_, i) => (
                          <input key={i} type="number" min="0" placeholder="Auto" style={{ width: 52 }}
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
                      <span style={{ fontSize: 11, color: '#64748b', fontWeight: 600 }}>Cell Content</span>
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
                  </div>
                );
              })()}

              {selectedElement.type === 'chart' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Chart Type</span>
                    <select
                      value={selectedElement.chartType || 'bar'}
                      onChange={(event) => updateSelectedElement({ chartType: event.target.value as 'bar' | 'line' | 'pie' })}
                    >
                      <option value="bar">Bar</option>
                      <option value="line">Line</option>
                      <option value="pie">Pie</option>
                    </select>
                  </label>
                  <label>
                    <span>Chart Data (JSON)</span>
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
                    <span>Color</span>
                    <input
                      type="color"
                      value={selectedElement.style?.backgroundColor || '#9ca3af'}
                      onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })}
                    />
                  </label>
                  <label>
                    <span>Thickness (px)</span>
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
                    <span>Display text</span>
                    <input
                      type="text"
                      value={selectedElement.content || ''}
                      onChange={(e) => updateSelectedElement({ content: e.target.value })}
                      placeholder="Click here"
                    />
                  </label>
                  <label>
                    <span>URL (href)</span>
                    <input
                      type="text"
                      value={selectedElement.href || ''}
                      onChange={(e) => updateSelectedElement({ href: e.target.value })}
                      placeholder="https://example.com"
                    />
                  </label>
                  <label>
                    <span>Target</span>
                    <select
                      value={selectedElement.linkTarget || '_blank'}
                      onChange={(e) => updateSelectedElement({ linkTarget: e.target.value as '_blank' | '_self' })}
                    >
                      <option value="_blank">New tab (_blank)</option>
                      <option value="_self">Same tab (_self)</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Font size</span>
                      <input type="number" value={selectedElement.style?.fontSize || 14} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.color || '#2563eb'} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'number' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Value</span>
                    <input
                      type="number"
                      step="any"
                      value={selectedElement.numberValue ?? 0}
                      onChange={(e) => updateSelectedElement({ numberValue: Number(e.target.value) })}
                    />
                  </label>
                  <label>
                    <span>Style</span>
                    <select
                      value={selectedElement.numberStyle || 'decimal'}
                      onChange={(e) => updateSelectedElement({ numberStyle: e.target.value as SimpleElement['numberStyle'] })}
                    >
                      <option value="decimal">Decimal (1,234.56)</option>
                      <option value="currency">Currency (€ 1.234,56)</option>
                      <option value="percent">Percent (12.5 %)</option>
                      <option value="scientific">Scientific (1.23e+3)</option>
                      <option value="ordinal">Ordinal (1st, 2nd…)</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Decimals</span>
                      <input type="number" min="0" max="10" value={selectedElement.numberDecimals ?? 2} onChange={(e) => updateSelectedElement({ numberDecimals: Number(e.target.value) })} />
                    </label>
                    <label>
                      <span>Locale</span>
                      <input type="text" value={selectedElement.numberLocale || 'de-DE'} onChange={(e) => updateSelectedElement({ numberLocale: e.target.value })} placeholder="de-DE" />
                    </label>
                    {selectedElement.numberStyle === 'currency' && (
                      <label>
                        <span>Currency</span>
                        <input type="text" value={selectedElement.numberCurrency || 'EUR'} onChange={(e) => updateSelectedElement({ numberCurrency: e.target.value })} placeholder="EUR" />
                      </label>
                    )}
                    <label>
                      <span>Font size</span>
                      <input type="number" value={selectedElement.style?.fontSize || 18} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, fontSize: Number(e.target.value) } })} />
                    </label>
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.color || '#111827'} onChange={(e) => updateSelectedElement({ style: { ...selectedElement.style, color: e.target.value } })} />
                    </label>
                  </div>
                  <div className="editor-form-grid">
                    <label>
                      <span>Prefix</span>
                      <input type="text" value={selectedElement.prefix || ''} onChange={(e) => updateSelectedElement({ prefix: e.target.value })} placeholder="e.g. ~" />
                    </label>
                    <label>
                      <span>Suffix</span>
                      <input type="text" value={selectedElement.suffix || ''} onChange={(e) => updateSelectedElement({ suffix: e.target.value })} placeholder="e.g. pts" />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'draw' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Tool</span>
                    <select value={selectedElement.drawTool || 'pen'} onChange={(event) => updateSelectedElement({ drawTool: event.target.value as SimpleElement['drawTool'] })}>
                      <option value="pen">Pen</option>
                      <option value="highlighter">Highlighter</option>
                      <option value="eraser">Eraser</option>
                    </select>
                  </label>
                  <label>
                    <span>Path data</span>
                    <textarea rows={3} value={selectedElement.pathData || ''} onChange={(event) => updateSelectedElement({ pathData: event.target.value })} />
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.color || '#1d4ed8'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                    </label>
                    <label>
                      <span>Stroke</span>
                      <input type="number" min="1" value={selectedElement.style?.strokeWidth || 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, strokeWidth: Number(event.target.value) } })} />
                    </label>
                    <label>
                      <span>Opacity</span>
                      <input type="number" min="0" max="1" step="0.05" value={selectedElement.style?.opacity ?? 1} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, opacity: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'highlight' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Mode</span>
                    <select value={selectedElement.markMode || 'rectangle'} onChange={(event) => updateSelectedElement({ markMode: event.target.value as SimpleElement['markMode'] })}>
                      <option value="rectangle">Rectangle</option>
                      <option value="text">Text marker</option>
                    </select>
                  </label>
                  <div className="editor-form-grid">
                    <label>
                      <span>Color</span>
                      <input type="color" value={selectedElement.style?.backgroundColor || '#fde047'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, backgroundColor: event.target.value } })} />
                    </label>
                    <label>
                      <span>Opacity</span>
                      <input type="number" min="0" max="1" step="0.05" value={selectedElement.style?.opacity ?? 0.45} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, opacity: Number(event.target.value) } })} />
                    </label>
                    <label>
                      <span>Radius</span>
                      <input type="number" min="0" value={selectedElement.style?.borderRadius ?? 4} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, borderRadius: Number(event.target.value) } })} />
                    </label>
                  </div>
                </div>
              )}

              {selectedElement.type === 'pageboundary' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Boundary</span>
                    <select value={selectedElement.pageBoundaryMode || 'start'} onChange={(event) => updateSelectedElement({ pageBoundaryMode: event.target.value as SimpleElement['pageBoundaryMode'] })}>
                      <option value="start">Page start</option>
                      <option value="end">Page end</option>
                    </select>
                  </label>
                  <label>
                    <span>Label</span>
                    <input type="text" value={selectedElement.content || ''} onChange={(event) => updateSelectedElement({ content: event.target.value })} />
                  </label>
                  <label>
                    <span>Color</span>
                    <input type="color" value={selectedElement.style?.color || '#7c3aed'} onChange={(event) => updateSelectedElement({ style: { ...selectedElement.style, color: event.target.value } })} />
                  </label>
                </div>
              )}

              {(selectedElement.type === 'footnote' || selectedElement.type === 'endnote') && (
                <div className="editor-form-stack">
                  <label>
                    <span>{selectedElement.type === 'footnote' ? 'Footnote' : 'Endnote'} text</span>
                    <textarea
                      rows={4}
                      value={selectedElement.footnoteText || ''}
                      onChange={(e) => updateSelectedElement({ footnoteText: e.target.value })}
                      placeholder="Note text…"
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'bookmark' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Bookmark name</span>
                    <input
                      type="text"
                      value={selectedElement.bookmarkName || ''}
                      onChange={(e) => updateSelectedElement({ bookmarkName: e.target.value })}
                      placeholder="e.g. section-intro"
                    />
                  </label>
                  <label>
                    <span>Link target (optional)</span>
                    <input
                      type="text"
                      value={selectedElement.bookmarkTarget || ''}
                      onChange={(e) => updateSelectedElement({ bookmarkTarget: e.target.value })}
                      placeholder="Bookmark name to link to"
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'comment' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Comment text</span>
                    <textarea
                      rows={4}
                      value={selectedElement.commentText || ''}
                      onChange={(e) => updateSelectedElement({ commentText: e.target.value })}
                      placeholder="Comment…"
                    />
                  </label>
                  <label>
                    <span>Author</span>
                    <input
                      type="text"
                      value={selectedElement.commentAuthor || ''}
                      onChange={(e) => updateSelectedElement({ commentAuthor: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>Date</span>
                    <input
                      type="date"
                      value={selectedElement.commentDate || ''}
                      onChange={(e) => updateSelectedElement({ commentDate: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>Comment ID</span>
                    <input
                      type="text"
                      value={selectedElement.commentId || ''}
                      onChange={(e) => updateSelectedElement({ commentId: e.target.value })}
                      placeholder="Auto-generated if blank"
                    />
                  </label>
                </div>
              )}

              {selectedElement.type === 'contentcontrol' && (
                <div className="editor-form-stack">
                  <label>
                    <span>Control type</span>
                    <select
                      value={selectedElement.contentControlType || 'richText'}
                      onChange={(e) => updateSelectedElement({ contentControlType: e.target.value as any })}
                    >
                      <option value="richText">Rich text</option>
                      <option value="plainText">Plain text</option>
                      <option value="date">Date picker</option>
                      <option value="comboBox">Combo box</option>
                      <option value="picture">Picture</option>
                    </select>
                  </label>
                  <label>
                    <span>Title</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlTitle || ''}
                      onChange={(e) => updateSelectedElement({ contentControlTitle: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>Tag</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlTag || ''}
                      onChange={(e) => updateSelectedElement({ contentControlTag: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>Placeholder text</span>
                    <input
                      type="text"
                      value={selectedElement.contentControlPlaceholder || ''}
                      onChange={(e) => updateSelectedElement({ contentControlPlaceholder: e.target.value })}
                    />
                  </label>
                  <label>
                    <span>Default content</span>
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
                  <span>Visibility</span>
                </div>
                <div className="editor-form-stack" style={{ padding: 12 }}>
                  <label className="editor-checkbox-control">
                    <input
                      type="checkbox"
                      checked={!selectedElement.hidden}
                      onChange={(e) => updateSelectedElement({ hidden: !e.target.checked })}
                    />
                    <span>Visible in output</span>
                  </label>
                  <label>
                    <span>Visible expression</span>
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
                  <span>Word / DOCX</span>
                </div>
                <div className="editor-form-stack" style={{ padding: 12 }}>
                  <label>
                    <span>Paragraph style</span>
                    <select
                      value={selectedElement.styleName ?? ''}
                      onChange={(e) => updateSelectedElement({ styleName: e.target.value || undefined })}
                    >
                      <option value="">— None —</option>
                      {(pageSettings.namedStyles ?? [])
                        .filter(s => s.type === 'paragraph' || s.type === 'list')
                        .map(s => <option key={s.id} value={s.id}>{s.name || s.id}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>Character style</span>
                    <select
                      value={selectedElement.characterStyle ?? ''}
                      onChange={(e) => updateSelectedElement({ characterStyle: e.target.value || undefined })}
                    >
                      <option value="">— None —</option>
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
                    <span>Auto-hyphenation</span>
                  </label>
                  <label>
                    <span>Revision type</span>
                    <select
                      value={selectedElement.revisionType ?? ''}
                      onChange={(e) => updateSelectedElement({ revisionType: e.target.value as any || undefined })}
                    >
                      <option value="">— None —</option>
                      <option value="insert">Insert</option>
                      <option value="delete">Delete</option>
                      <option value="format">Format change</option>
                    </select>
                  </label>
                  {selectedElement.revisionType && (
                    <>
                      <label>
                        <span>Revision author</span>
                        <input
                          type="text"
                          value={selectedElement.revisionAuthor ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionAuthor: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>Revision date</span>
                        <input
                          type="date"
                          value={selectedElement.revisionDate ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionDate: e.target.value })}
                        />
                      </label>
                      <label>
                        <span>Revision ID</span>
                        <input
                          type="text"
                          value={selectedElement.revisionId ?? ''}
                          onChange={(e) => updateSelectedElement({ revisionId: e.target.value })}
                          placeholder="Auto-generated if blank"
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
                <span>Delete element</span>
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
                    Copy<span className="editor-context-menu-shortcut">⌘C</span>
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('duplicate')}>
                    Duplicate<span className="editor-context-menu-shortcut">⌘D</span>
                  </button>
                  <button
                    className={`editor-context-menu-item${!clipboard ? ' disabled' : ''}`}
                    onClick={() => contextMenuAction('paste')}
                  >
                    Paste<span className="editor-context-menu-shortcut">⌘V</span>
                  </button>
                  <div className="editor-context-menu-separator" />
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('lock')}>
                    {el?.locked ? 'Unlock' : 'Lock'}
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('hide')}>
                    {el?.hidden ? 'Show' : 'Hide'}
                  </button>
                  <div className="editor-context-menu-separator" />
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('front')}>
                    Bring to Front
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('forward')}>
                    Bring Forward
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('backward')}>
                    Send Backward
                  </button>
                  <button className="editor-context-menu-item" onClick={() => contextMenuAction('back')}>
                    Send to Back
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
                      title="Delete this element from all language tabs"
                    >
                      Delete from all languages<span className="editor-context-menu-shortcut">Del</span>
                    </button>
                  )}
                  <button className="editor-context-menu-item danger" onClick={() => contextMenuAction('delete')}>
                    Delete<span className="editor-context-menu-shortcut">Del</span>
                  </button>
                </>
              );
            })() : (
              <>
                <button
                  className={`editor-context-menu-item${!clipboard ? ' disabled' : ''}`}
                  onClick={() => contextMenuAction('paste')}
                >
                  Paste<span className="editor-context-menu-shortcut">⌘V</span>
                </button>
                <button className="editor-context-menu-item" onClick={() => contextMenuAction('selectAll')}>
                  Select All<span className="editor-context-menu-shortcut">⌘A</span>
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

export default SimpleCanvas;
