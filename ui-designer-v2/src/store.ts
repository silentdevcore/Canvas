import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { SimpleElement, ElementType, LayerDirection, PageSettings, Page, Template as BaseTemplate, LocalizedProperty } from '@/types';

export type { ElementType, SimpleElement };

export interface Template extends BaseTemplate {
  pages: Page[];
  sharedElements: SimpleElement[];  // header/footer elements visible on every page
  data: Record<string, any>;
}

export { PageSettings };

export const DEFAULT_PAGE_SETTINGS: PageSettings = {
  width: 595,
  height: 842,
  orientation: 'portrait',
  backgroundColor: '#ffffff',
  backgroundImage: '',
  backgroundImageFit: 'cover',
  margins: { top: 48, right: 48, bottom: 48, left: 48 },
  headerEnabled: false,
  headerHeight: 60,
  headerFirstPageDifferent: false,
  headerOddEvenDifferent: false,
  footerEnabled: false,
  footerHeight: 40,
  footerFirstPageDifferent: false,
  footerOddEvenDifferent: false,
  bleedSize: 0,
  gridVisible: true,
  snapToGrid: false,
  gridSize: 24,
  unit: 'px',
  showMarginGuide: true,
  showSafeArea: false,
  pagination: {
    autoBreaks: true,
    repeatTableHeader: true,
    keepWithNext: true,
    sectionStartBehavior: 'continue',
    orphanLines: 2,
    widowLines: 2,
  },
  metadata: { title: '', author: '', subject: '', keywords: '' },
  pageNumbering: {
    enabled: false,
    format: 'pageOfTotal',
    startNumber: 1,
    prefix: '',
    suffix: '',
    showOnFirstPage: true,
    placement: 'bottom-center',
  },
  globalWatermark: {
    enabled: false,
    mode: 'text',
    content: '',
    opacity: 0.18,
    rotation: -24,
    scale: 1,
    pageScope: 'all',
    pageRange: '',
    color: '#64748b',
    fontSize: 42,
  },
  cropMarks: false,
  exportDefaults: {
    quality: 'printer',
    embedFonts: true,
    compressImages: true,
    accessibilityTagged: false,
  },
  namedStyles: [],
  customProperties: [],
  trackChanges: false,
  activeLanguages: [],
  localizedProperties: [],
};

interface EditorState {
  templates: Template[];
  currentTemplate: Template | null;
  currentPageIndex: number;
  selectedElementId: string | null;
  jsonData: Record<string, any>;
  generatedCode: string;
  backgroundPdf: File | null;
  pageSettings: PageSettings;
  settingsModifiedSinceExport: boolean;
  helpModalOpen: boolean;
  setHelpModalOpen: (open: boolean) => void;
  documentMode: 'pdf' | 'word';
  setDocumentMode: (mode: 'pdf' | 'word') => void;
  // Current preview language (ephemeral — not persisted)
  currentPreviewLanguage: string;
  setCurrentPreviewLanguage: (lang: string) => void;
  // Localized property helpers
  upsertLocalizedProperty: (prop: LocalizedProperty) => void;
  deleteLocalizedProperty: (key: string) => void;
  // Undo/redo (not persisted)
  undoStack: Template[];
  redoStack: Template[];
  snapshotHistory: () => void;
  undo: () => void;
  redo: () => void;
  addTemplate: (template: Template) => void;
  setCurrentTemplate: (template: Template | null) => void;
  addElement: (element: SimpleElement) => void;
  updateElement: (id: string, updates: Partial<SimpleElement>) => void;
  deleteElement: (id: string) => void;
  reorderElement: (id: string, direction: LayerDirection) => void;
  setSelectedElementId: (id: string | null) => void;
  updateJsonData: (data: Record<string, any>) => void;
  setGeneratedCode: (code: string) => void;
  setBackgroundPdf: (file: File | null) => void;
  updatePageSettings: (updates: Partial<PageSettings>) => void;
  markExported: () => void;
  // Page operations
  addPage: () => void;
  deletePage: (index: number) => void;
  duplicatePage: (index: number) => void;
  setCurrentPage: (index: number) => void;
  movePageTo: (fromIndex: number, toIndex: number) => void;
  // Shared header/footer elements (appear on all pages)
  addSharedElement: (element: SimpleElement) => void;
  updateSharedElement: (id: string, updates: Partial<SimpleElement>) => void;
  deleteSharedElement: (id: string) => void;
  bulkReplaceContent: (pages: Page[], sharedElements: SimpleElement[]) => void;
}

const makePage = (elements: SimpleElement[] = []): Page => ({
  id: `page-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
  elements,
});

const MAX_PERSISTED_TEMPLATE_CHARS = 1_500_000;

function tryMeasureJson(value: unknown): number {
  try {
    return JSON.stringify(value).length;
  } catch {
    return Number.POSITIVE_INFINITY;
  }
}

function persistableTemplate(template: Template | null): Template | null {
  if (!template) return null;
  return tryMeasureJson(template) <= MAX_PERSISTED_TEMPLATE_CHARS ? template : null;
}

function persistableTemplates(templates: Template[]): Template[] {
  return templates.filter(template => tryMeasureJson(template) <= MAX_PERSISTED_TEMPLATE_CHARS);
}

export const useEditorStore = create<EditorState>()(
  persist(
    (set, get) => ({
      templates: [],
      currentTemplate: null,
      currentPageIndex: 0,
      selectedElementId: null,
      jsonData: {},
      generatedCode: '',
      backgroundPdf: null,
      pageSettings: DEFAULT_PAGE_SETTINGS,
      settingsModifiedSinceExport: false,
      helpModalOpen: false,
      documentMode: 'pdf' as const,
      currentPreviewLanguage: navigator.language.split('-')[0],
      undoStack: [],
      redoStack: [],

      setHelpModalOpen: (open) => set({ helpModalOpen: open }),
      setDocumentMode: (mode) => set({ documentMode: mode }),
      setCurrentPreviewLanguage: (lang) => set({ currentPreviewLanguage: lang }),

      upsertLocalizedProperty: (prop) => {
        const ps = get().pageSettings;
        const existing = ps.localizedProperties ?? [];
        const idx = existing.findIndex(p => p.key === prop.key);
        const updated = idx >= 0
          ? existing.map((p, i) => i === idx ? prop : p)
          : [...existing, prop];
        set({ pageSettings: { ...ps, localizedProperties: updated } });
      },

      deleteLocalizedProperty: (key) => {
        const ps = get().pageSettings;
        set({ pageSettings: { ...ps, localizedProperties: (ps.localizedProperties ?? []).filter(p => p.key !== key) } });
      },

      snapshotHistory: () => {
        const { currentTemplate, undoStack } = get();
        if (!currentTemplate) return;
        set({
          undoStack: [...undoStack.slice(-49), currentTemplate],
          redoStack: [],
        });
      },

      undo: () => {
        const { undoStack, currentTemplate, redoStack } = get();
        if (undoStack.length === 0) return;
        const previous = undoStack[undoStack.length - 1];
        set({
          currentTemplate: previous,
          undoStack: undoStack.slice(0, -1),
          redoStack: currentTemplate ? [...redoStack, currentTemplate] : redoStack,
        });
      },

      redo: () => {
        const { redoStack, currentTemplate, undoStack } = get();
        if (redoStack.length === 0) return;
        const next = redoStack[redoStack.length - 1];
        set({
          currentTemplate: next,
          redoStack: redoStack.slice(0, -1),
          undoStack: currentTemplate ? [...undoStack, currentTemplate] : undoStack,
        });
      },

      addTemplate: (template) => set((state) => ({ templates: [...state.templates, template] })),
      setCurrentTemplate: (template) => set({ currentTemplate: template, currentPageIndex: 0, undoStack: [], redoStack: [] }),

      addElement: (element) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const pages = state.currentTemplate.pages.map((page, i) =>
            i === state.currentPageIndex
              ? { ...page, elements: [...page.elements, element] }
              : page
          );
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      updateElement: (id, updates) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const pages = state.currentTemplate.pages.map((page, i) =>
            i === state.currentPageIndex
              ? { ...page, elements: page.elements.map((el) => el.id === id ? { ...el, ...updates } : el) }
              : page
          );
          return { currentTemplate: { ...state.currentTemplate, pages } };
        }),

      deleteElement: (id) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const pages = state.currentTemplate.pages.map((page, i) =>
            i === state.currentPageIndex
              ? { ...page, elements: page.elements.filter((el) => el.id !== id) }
              : page
          );
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      reorderElement: (id, direction) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const pageIdx = state.currentPageIndex;
          const pages = state.currentTemplate.pages.map((page, i) => {
            if (i !== pageIdx) return page;
            const els = [...page.elements];
            const index = els.findIndex((el) => el.id === id);
            if (index === -1) return page;
            const [element] = els.splice(index, 1);
            switch (direction) {
              case 'front':    els.push(element); break;
              case 'forward':  els.splice(Math.min(index + 1, els.length), 0, element); break;
              case 'backward': els.splice(Math.max(index - 1, 0), 0, element); break;
              case 'back':     els.unshift(element); break;
            }
            return { ...page, elements: els };
          });
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      setSelectedElementId: (id) => set({ selectedElementId: id }),
      updateJsonData: (data) => set({ jsonData: data }),
      setGeneratedCode: (code) => set({ generatedCode: code }),
      setBackgroundPdf: (file) => set({ backgroundPdf: file }),

      updatePageSettings: (updates) =>
        set((state) => ({ pageSettings: { ...state.pageSettings, ...updates }, settingsModifiedSinceExport: true })),

      markExported: () => set({ settingsModifiedSinceExport: false }),

      addPage: () =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const newPage = makePage();
          const pages = [...state.currentTemplate.pages, newPage];
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            currentPageIndex: pages.length - 1,
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      deletePage: (index) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          if (state.currentTemplate.pages.length <= 1) return state;
          const pages = state.currentTemplate.pages.filter((_, i) => i !== index);
          const newIndex = Math.min(state.currentPageIndex, pages.length - 1);
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            currentPageIndex: newIndex,
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      duplicatePage: (index) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const source = state.currentTemplate.pages[index];
          if (!source) return state;
          const ts = Date.now();
          const newPage: Page = {
            id: `page-${ts}-copy`,
            elements: source.elements.map((el) => ({ ...el, id: `${el.id}-c${ts}` })),
          };
          const pages = [
            ...state.currentTemplate.pages.slice(0, index + 1),
            newPage,
            ...state.currentTemplate.pages.slice(index + 1),
          ];
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            currentPageIndex: index + 1,
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      setCurrentPage: (index) =>
        set((state) => ({
          currentPageIndex: Math.max(0, Math.min(index, (state.currentTemplate?.pages.length ?? 1) - 1)),
        })),

      movePageTo: (fromIndex, toIndex) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          const pages = [...state.currentTemplate.pages];
          const [page] = pages.splice(fromIndex, 1);
          pages.splice(toIndex, 0, page);
          return {
            currentTemplate: { ...state.currentTemplate, pages },
            currentPageIndex: toIndex,
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      addSharedElement: (element) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          return {
            currentTemplate: {
              ...state.currentTemplate,
              sharedElements: [...(state.currentTemplate.sharedElements ?? []), element],
            },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      updateSharedElement: (id, updates) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          return {
            currentTemplate: {
              ...state.currentTemplate,
              sharedElements: (state.currentTemplate.sharedElements ?? []).map(el =>
                el.id === id ? { ...el, ...updates } : el
              ),
            },
          };
        }),

      deleteSharedElement: (id) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          return {
            currentTemplate: {
              ...state.currentTemplate,
              sharedElements: (state.currentTemplate.sharedElements ?? []).filter(el => el.id !== id),
            },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),

      bulkReplaceContent: (pages, sharedElements) =>
        set((state) => {
          if (!state.currentTemplate) return state;
          return {
            currentTemplate: { ...state.currentTemplate, pages, sharedElements },
            undoStack: [...state.undoStack.slice(-49), state.currentTemplate],
            redoStack: [],
          };
        }),
    }),
    {
      name: 'editor-storage',
      version: 6,
      partialize: (state) => {
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const { undoStack, redoStack, snapshotHistory, undo, redo, currentPreviewLanguage, setCurrentPreviewLanguage, upsertLocalizedProperty, deleteLocalizedProperty, helpModalOpen, setHelpModalOpen, ...rest } = state;
        return {
          ...rest,
          currentTemplate: persistableTemplate(rest.currentTemplate),
          templates: persistableTemplates(rest.templates),
          backgroundPdf: null,
        };
      },
      migrate: (persisted: unknown, version: number) => {
        // v5→v6: LocalizedProperty shape: global+globalValue → scope+ownerLanguage
        const state = persisted as any;
        if (version < 6 && state?.pageSettings?.localizedProperties) {
          state.pageSettings.localizedProperties = (state.pageSettings.localizedProperties as any[]).map((p: any) => {
            if ('scope' in p) return p; // already migrated
            const { global: _g, globalValue: _gv, ...rest } = p;
            return { ...rest, scope: 'global' };
          });
        }
        // v3→v4: Template.elements → Template.pages
        if (version < 4 && state?.currentTemplate) {
          const t = state.currentTemplate;
          if (t.elements && !t.pages) {
            state.currentTemplate = {
              ...t,
              pages: [{ id: 'page-1', elements: t.elements }],
              sharedElements: [],
            };
            delete state.currentTemplate.elements;
          }
          if (state.templates) {
            state.templates = (state.templates as any[]).map((tmpl: any) => {
              if (tmpl.elements && !tmpl.pages) {
                const { elements, ...rest } = tmpl;
                return { ...rest, pages: [{ id: 'page-1', elements }], sharedElements: [] };
              }
              return tmpl;
            });
          }
        }
        // Ensure sharedElements exists on persisted templates
        if (state?.currentTemplate && !state.currentTemplate.sharedElements) {
          state.currentTemplate = { ...state.currentTemplate, sharedElements: [] };
        }
        return state;
      },
      merge: (persisted: unknown, current: EditorState): EditorState => {
        const p = (persisted ?? {}) as Partial<EditorState>;
        const ps = ((p.pageSettings ?? {}) as Partial<PageSettings>);
        return {
          ...current,
          ...p,
          currentPageIndex: (p as any).currentPageIndex ?? 0,
          pageSettings: {
            ...DEFAULT_PAGE_SETTINGS,
            ...ps,
            margins:         { ...DEFAULT_PAGE_SETTINGS.margins,         ...(ps.margins         ?? {}) },
            metadata:        { ...DEFAULT_PAGE_SETTINGS.metadata,        ...(ps.metadata        ?? {}) },
            pageNumbering:   { ...DEFAULT_PAGE_SETTINGS.pageNumbering,   ...(ps.pageNumbering   ?? {}) },
            globalWatermark: { ...DEFAULT_PAGE_SETTINGS.globalWatermark, ...(ps.globalWatermark ?? {}) },
            exportDefaults:  { ...DEFAULT_PAGE_SETTINGS.exportDefaults,  ...(ps.exportDefaults  ?? {}) },
            pagination:      { ...DEFAULT_PAGE_SETTINGS.pagination,      ...(ps.pagination      ?? {}) },
          },
        };
      },
    }
  )
);
