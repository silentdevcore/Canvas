import { create } from 'zustand';
import { DesignTemplate } from './domain/repositories/ITemplateRepository';

// Debounce utility function
function debounce<T extends (...args: any[]) => any>(
  func: T,
  wait: number
): (...args: Parameters<T>) => void {
  let timeout: number;
  return (...args: Parameters<T>) => {
    clearTimeout(timeout);
    timeout = setTimeout(() => func(...args), wait) as any;
  };
}

export type ElementType = 'Text' | 'Column' | 'Table' | 'Image' | 'Rectangle' | 'Circle' | 'Line' | 'Link' | 'List' | 'PageBreak' | 'Grid' | 'Spacer' | 'Button' | 'Checkbox' | 'Radio' | 'QRCode' | 'Barcode' | 'Signature' | 'RichText';

export type PageSize = 'A4' | 'A5' | 'A6' | 'Letter' | 'Legal' | 'Custom';
export type PageOrientation = 'Portrait' | 'Landscape';
export type PreviewMode = 'design' | 'data' | 'error';

export interface PageSettings {
  size: PageSize;
  orientation: PageOrientation;
  width: number; // in pixels
  height: number; // in pixels
  backgroundColor: string;
  margins: {
    top: number;
    right: number;
    bottom: number;
    left: number;
  };
  title: string;
  description: string;
}

export interface BindingConfig {
  dataPath?: string;
  fallbackValue?: any;
  required?: boolean;
  requiredMessage?: string;
  valueType?: 'string' | 'number' | 'boolean' | 'date' | 'image-url';
  bindingScope?: 'root' | 'loop-item' | 'parent';
}

export interface ExpressionConfig {
  visibleWhen?: string;
  enabledWhen?: string;
  valueExpression?: string;
  styleExpression?: Record<string, string>;
  safeExpressionMode?: boolean;
  validationErrors?: string[];
}

export interface RepeatConfig {
  repeatSource?: string;
  itemAlias?: string;
  indexAlias?: string;
  emptyBehavior?: 'hide' | 'show-placeholder' | 'keep-template';
  maxItems?: number;
  pageBreakBetweenItems?: boolean;
  rowTemplateMode?: boolean;
}

export interface OverflowConfig {
  textOverflow?: 'wrap' | 'clip' | 'ellipsis' | 'shrink';
  maxLines?: number;
  lineClamp?: boolean;
  keepTogether?: boolean;
  avoidPageBreakInside?: boolean;
  anchor?: 'top-left' | 'top-center' | 'top-right' | 'middle-left' | 'middle-center' | 'middle-right' | 'bottom-left' | 'bottom-center' | 'bottom-right';
  verticalAlign?: 'top' | 'middle' | 'bottom';
  horizontalAlign?: 'left' | 'center' | 'right';
}

export interface ImageConfig {
  imageFit?: 'contain' | 'cover' | 'fill' | 'none' | 'scale-down';
  crop?: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  focalPoint?: {
    x: number; // 0-1 relative to image width
    y: number; // 0-1 relative to image height
  };
  remoteFetchPolicy?: {
    allowlist?: string[];
    timeout?: number;
    retryCount?: number;
    retryDelay?: number;
  };
  placeholder?: {
    type: 'none' | 'color' | 'icon' | 'text';
    value?: string;
    backgroundColor?: string;
  };
  fallbackImage?: string;
  preserveAspectRatio?: boolean;
  quality?: number; // 1-100
  format?: 'auto' | 'webp' | 'png' | 'jpg';
}

export interface TableColumnConfig {
  header?: string;
  dataPath?: string;
  width?: number;
  formatter?: string;
  formatterArgs?: Record<string, any>;
  alignment?: 'left' | 'center' | 'right';
  styleExpression?: Record<string, string>;
}

export interface TableConfig {
  tableDataPath?: string;
  columns?: TableColumnConfig[];
  headerRepeatOnPageBreak?: boolean;
  rowStriping?: {
    enabled?: boolean;
    evenRowStyle?: Record<string, any>;
    oddRowStyle?: Record<string, any>;
  };
  conditionalRowStyles?: Array<{
    condition: string;
    style: Record<string, any>;
  }>;
  emptyRowsPolicy?: 'hide-table' | 'show-empty-row' | 'show-placeholder-text';
  emptyRowText?: string;
  minRows?: number;
  maxRows?: number;
  showHeader?: boolean;
  headerStyle?: Record<string, any>;
  rowStyle?: Record<string, any>;
  alternateRowStyle?: Record<string, any>;
}

export interface ValidationConfig {
  elementValidationMode?: 'strict' | 'warn' | 'ignore';
  customErrorMessage?: string;
  debugLabel?: string;
  diagnosticId?: string;
  preflightStatus?: {
    hasMissingPaths?: boolean;
    hasTypeErrors?: boolean;
    hasExpressionErrors?: boolean;
    lastValidated?: string;
  };
}

export interface TemplateMetadata {
  id?: string;
  name?: string;
  description?: string;
  category?: string;
  tags?: string[];
  version?: string;
  schemaVersion?: string;
  createdBy?: string;
  updatedBy?: string;
  createdAt?: string;
  updatedAt?: string;
  locale?: string;
  currency?: string;
  timezone?: string;
  formattingProfile?: {
    dateFormat?: string;
    timeFormat?: string;
    numberFormat?: string;
    currencyFormat?: string;
  };
  migrationHints?: Record<string, any>;
  isPublic?: boolean;
  isArchived?: boolean;
}

export interface DesignerElement {
  id: string;
  type: ElementType;
  props: Record<string, any>;
  binding?: BindingConfig;
  expression?: ExpressionConfig;
  repeat?: RepeatConfig;
  overflow?: OverflowConfig;
  image?: ImageConfig;
  table?: TableConfig;
  validation?: ValidationConfig;
  children?: string[];
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  isGroup?: boolean;
  groupId?: string;
  locked?: boolean;
}

interface DesignerStateSnapshot {
  elements: Record<string, DesignerElement>;
  rootIds: string[];
  selectedIds: string[];
  snapToGrid: boolean;
  gridSize: number;
  gridColor: string;
  gridOpacity: number;
  zoom: number;
  minZoom: number;
  maxZoom: number;
}

interface DesignerState extends DesignerStateSnapshot {
  undoStack: DesignerStateSnapshot[];
  redoStack: DesignerStateSnapshot[];
  copiedElements: DesignerElement[];
  showTooltips: boolean;
  virtualScrolling: boolean;
  pageSettings: PageSettings;
  templateMetadata: TemplateMetadata;
  samplePayload: Record<string, any>;
  previewMode: PreviewMode;
  previewErrors: Array<{ elementId: string; message: string; severity: 'error' | 'warning' }>;
  addElement: (type: ElementType, parentId?: string) => void;
  selectElement: (id: string | null, multiSelect?: boolean) => void;
  updateElementProps: (id: string, props: Record<string, any>) => void;
  updateElementBinding: (id: string, binding: BindingConfig) => void;
  updateElementExpression: (id: string, expression: ExpressionConfig) => void;
  updateElementRepeat: (id: string, repeat: RepeatConfig) => void;
  updateElementOverflow: (id: string, overflow: OverflowConfig) => void;
  updateElementImage: (id: string, image: ImageConfig) => void;
  updateElementTable: (id: string, table: TableConfig) => void;
  updateElementValidation: (id: string, validation: ValidationConfig) => void;
  updateTemplateMetadata: (metadata: Partial<TemplateMetadata>) => void;
  updateElementPosition: (id: string, x: number, y: number) => void;
  updateElementSize: (id: string, width: number, height: number) => void;
  deleteElement: (id: string) => void;
  groupElements: (elementIds: string[]) => void;
  ungroupElements: (groupId: string) => void;
  toggleSnapToGrid: () => void;
  setGridSize: (size: number) => void;
  undo: () => void;
  redo: () => void;
  canUndo: boolean;
  canRedo: boolean;
  copyElements: () => void;
  pasteElements: () => void;
  canPaste: boolean;
  selectAll: () => void;
  toggleTooltips: () => void;
  toggleElementLock: (id: string) => void;
  toggleVirtualScrolling: () => void;
  updatePageSettings: (settings: Partial<PageSettings>) => void;
  setPageSize: (size: PageSize) => void;
  setPageOrientation: (orientation: PageOrientation) => void;
  setPageBackgroundColor: (color: string) => void;
  setGridColor: (color: string) => void;
  setGridOpacity: (opacity: number) => void;
  updateSamplePayload: (payload: Record<string, any>) => void;
  loadTemplate: (template: DesignTemplate) => void;
  setPreviewMode: (mode: PreviewMode) => void;
  validateTemplate: () => void;
  exportToPNG: () => Promise<string>;
  exportToSVG: () => string;
  exportToPDF: () => Promise<string>;
  zoomIn: () => void;
  zoomOut: () => void;
  zoomToFit: () => void;
  resetZoom: () => void;
  setZoom: (zoom: number) => void;
  uploadImage: (file: File) => Promise<string>;
  toasts: Array<{ id: string; message: string; type: 'success' | 'error' | 'warning' | 'info'; duration?: number }>;
  addToast: (message: string, type?: 'success' | 'error' | 'warning' | 'info', duration?: number) => void;
  removeToast: (id: string) => void;
  alignmentGuides: {
    vertical: Array<{ x: number; label?: string }>;
    horizontal: Array<{ y: number; label?: string }>;
    distances: Array<{ x: number; y: number; text: string; vertical?: boolean }>;
  };
  setAlignmentGuides: (guides: { vertical: Array<{ x: number; label?: string }>; horizontal: Array<{ y: number; label?: string }>; distances: Array<{ x: number; y: number; text: string; vertical?: boolean }> }) => void;
  clearAlignmentGuides: () => void;
}

// Helper function to create state snapshots
const createStateSnapshot = (state: DesignerState): DesignerStateSnapshot => ({
  elements: state.elements,
  rootIds: state.rootIds,
  selectedIds: state.selectedIds,
  snapToGrid: state.snapToGrid,
  gridSize: state.gridSize,
  gridColor: state.gridColor,
  gridOpacity: state.gridOpacity,
  zoom: state.zoom,
  minZoom: state.minZoom,
  maxZoom: state.maxZoom,
});

// Helper function to save state to undo stack
const saveToUndoStack = (set: any, get: any) => {
  const currentState = get();
  const snapshot = createStateSnapshot(currentState);
  set((state: DesignerState) => ({
    undoStack: [...state.undoStack, snapshot].slice(-50), // Keep last 50 states
    redoStack: [], // Clear redo stack when new action is performed
  }));
};

export const useDesignerStore = create<DesignerState>((set, get) => ({
  elements: {},
  rootIds: [],
  selectedIds: [],
  snapToGrid: true,
  gridSize: 20,
  gridColor: '#e5e7eb',
  gridOpacity: 1,
  zoom: 1,
  minZoom: 0.1,
  maxZoom: 5,
  undoStack: [],
  redoStack: [],
  copiedElements: [] as DesignerElement[],
  showTooltips: true,
  virtualScrolling: false,
  samplePayload: {
    customer: {
      name: 'John Doe',
      email: 'john@example.com',
      address: {
        street: '123 Main St',
        city: 'Anytown',
        zipCode: '12345'
      }
    },
    order: {
      id: 'ORD-12345',
      date: '2024-01-15',
      items: [
        { name: 'Widget A', quantity: 2, price: 29.99 },
        { name: 'Widget B', quantity: 1, price: 49.99 }
      ],
      total: 109.97
    }
  },
  previewMode: 'design' as PreviewMode,
  previewErrors: [] as Array<{ elementId: string; message: string; severity: 'error' | 'warning' }>,
  alignmentGuides: { vertical: [], horizontal: [], distances: [] },
  toasts: [] as Array<{ id: string; message: string; type: 'success' | 'error' | 'warning' | 'info'; duration?: number }>,
  addElement: (type, parentId) => {
    saveToUndoStack(set, get);
    set((state) => {
      const id = Date.now().toString();
      let newElement: DesignerElement;
      if (type === 'Text') {
        newElement = {
          id,
          type,
          props: { text: 'Text', fontSize: 16 },
        };
      } else if (type === 'Table') {
        newElement = {
          id,
          type,
          props: { rows: 2, columns: 2, data: [['A1','A2'],['B1','B2']] },
        };
      } else if (type === 'Image') {
        newElement = {
          id,
          type,
          props: { src: 'https://via.placeholder.com/200x100?text=Image', width: 200, height: 100, alt: 'Image' },
        };
      } else if (type === 'Rectangle') {
        newElement = {
          id,
          type,
          props: { width: 200, height: 100, fillColor: '#ffffff', strokeColor: '#000000', strokeWidth: 1, borderRadius: 0 },
        };
      } else if (type === 'Circle') {
        newElement = {
          id,
          type,
          props: { radius: 50, fillColor: '#ffffff', strokeColor: '#000000', strokeWidth: 1 },
        };
      } else if (type === 'Line') {
        newElement = {
          id,
          type,
          props: { x1: 0, y1: 0, x2: 100, y2: 100, strokeColor: '#000000', strokeWidth: 2, lineCap: 'butt' },
        };
      } else if (type === 'Link') {
        newElement = {
          id,
          type,
          props: { url: 'https://example.com', text: 'Click here', width: 100, height: 30 },
        };
      } else if (type === 'List') {
        newElement = {
          id,
          type,
          props: { items: ['Item 1', 'Item 2', 'Item 3'], ordered: false, markerStyle: 'disc' },
        };
      } else if (type === 'PageBreak') {
        newElement = {
          id,
          type,
          props: { style: 'dashed', color: '#ff6b6b' },
        };
      } else if (type === 'Grid') {
        newElement = {
          id,
          type,
          props: { rows: 2, columns: 3, gap: 10, justifyContent: 'start', alignItems: 'start' },
          children: [],
        };
      } else if (type === 'Spacer') {
        newElement = {
          id,
          type,
          props: { width: 100, height: 20, flexGrow: 0 },
        };
      } else if (type === 'Button') {
        newElement = {
          id,
          type,
          props: { text: 'Button', style: 'primary', action: 'click' },
        };
      } else if (type === 'Checkbox') {
        newElement = {
          id,
          type,
          props: { label: 'Checkbox', checked: false },
        };
      } else if (type === 'Radio') {
        newElement = {
          id,
          type,
          props: { label: 'Radio Button', checked: false, groupName: 'radio-group' },
        };
      } else if (type === 'QRCode') {
        newElement = {
          id,
          type,
          props: { value: 'https://example.com', size: 100, eccLevel: 'M', quietZone: 4 },
        };
      } else if (type === 'Barcode') {
        newElement = {
          id,
          type,
          props: { value: '123456789', symbology: 'CODE128', width: 200, height: 60, checksum: false },
        };
      } else if (type === 'Signature') {
        newElement = {
          id,
          type,
          props: { label: 'Signature', signerNamePath: '', datePath: '', imagePath: '' },
        };
      } else if (type === 'RichText') {
        newElement = {
          id,
          type,
          props: { html: '<p>Rich text content</p>', styleProfile: 'default' },
        };
      } else {
        newElement = {
          id,
          type,
          props: {},
          children: [],
        };
      }
      const elements = { ...state.elements, [id]: newElement };
      let rootIds = state.rootIds;
      if (parentId && elements[parentId]) {
        elements[parentId] = {
          ...elements[parentId],
          children: [...(elements[parentId].children || []), id],
        };
      } else {
        rootIds = [...state.rootIds, id];
      }
      return { elements, rootIds };
    });
  },
  selectElement: (id, multiSelect = false) =>
    set((state) => {
      if (!id) {
        return { selectedIds: [] };
      }
      if (multiSelect) {
        const isAlreadySelected = state.selectedIds.includes(id);
        if (isAlreadySelected) {
          return { selectedIds: state.selectedIds.filter(selectedId => selectedId !== id) };
        } else {
          return { selectedIds: [...state.selectedIds, id] };
        }
      } else {
        return { selectedIds: [id] };
      }
    }),
  updateElementProps: (id, props) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          props: { ...state.elements[id].props, ...props },
        },
      },
    }));
  },
  updateElementPosition: (id, x, y) =>
    set((state) => {
      let snappedX = x;
      let snappedY = y;

      if (state.snapToGrid) {
        snappedX = Math.round(x / state.gridSize) * state.gridSize;
        snappedY = Math.round(y / state.gridSize) * state.gridSize;
      }

      return {
        elements: {
          ...state.elements,
          [id]: {
            ...state.elements[id],
            x: snappedX,
            y: snappedY,
          },
        },
      };
    }, false), // Skip immer for performance
  updateElementSize: (id, width, height) =>
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          width,
          height,
        },
      },
    })),
  deleteElement: (id) => {
    saveToUndoStack(set, get);
    set((state) => {
      const elements = { ...state.elements };
      delete elements[id];

      // Remove from rootIds
      const rootIds = state.rootIds.filter(rootId => rootId !== id);

      // Remove from parent children arrays
      Object.keys(elements).forEach(elementId => {
        if (elements[elementId].children) {
          elements[elementId].children = elements[elementId].children!.filter(childId => childId !== id);
        }
      });

      // Remove from selectedIds if deleted element was selected
      const selectedIds = state.selectedIds.filter(selectedId => selectedId !== id);

      return { elements, rootIds, selectedIds };
    });
  },
  groupElements: (elementIds) => {
    saveToUndoStack(set, get);
    set((state) => {
      if (elementIds.length < 2) return state;

      const groupId = `group_${Date.now()}`;
      const elements = { ...state.elements };

      // Calculate group bounds
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

      elementIds.forEach(id => {
        const element = elements[id];
        if (element && element.x !== undefined && element.y !== undefined &&
            element.width !== undefined && element.height !== undefined) {
          minX = Math.min(minX, element.x);
          minY = Math.min(minY, element.y);
          maxX = Math.max(maxX, element.x + element.width);
          maxY = Math.max(maxY, element.y + element.height);
        }
      });

      // Create group element
      const groupElement: DesignerElement = {
        id: groupId,
        type: 'Column', // Use Column as group container
        props: {},
        children: elementIds,
        x: minX,
        y: minY,
        width: maxX - minX,
        height: maxY - minY,
        isGroup: true,
      };

      // Mark child elements as part of group and adjust their positions relative to group
      elementIds.forEach(id => {
        const element = elements[id];
        if (element) {
          elements[id] = {
            ...element,
            groupId,
            x: (element.x || 0) - minX,
            y: (element.y || 0) - minY,
          };
        }
      });

      // Remove grouped elements from rootIds and add group
      const rootIds = state.rootIds.filter(id => !elementIds.includes(id));
      rootIds.push(groupId);

      return {
        elements: { ...elements, [groupId]: groupElement },
        rootIds,
        selectedIds: [groupId], // Select the new group
      };
    });
  },
  ungroupElements: (groupId) => {
    saveToUndoStack(set, get);
    set((state) => {
      const elements = { ...state.elements };
      const group = elements[groupId];

      if (!group || !group.isGroup || !group.children) return state;

      const groupX = group.x || 0;
      const groupY = group.y || 0;

      // Move child elements back to absolute positions and remove group reference
      group.children.forEach(childId => {
        const child = elements[childId];
        if (child) {
          elements[childId] = {
            ...child,
            x: (child.x || 0) + groupX,
            y: (child.y || 0) + groupY,
            groupId: undefined,
          };
        }
      });

      // Remove group from elements and rootIds
      delete elements[groupId];
      const rootIds = state.rootIds.filter(id => id !== groupId);

      // Add ungrouped elements to rootIds
      rootIds.push(...group.children);

      return {
        elements,
        rootIds,
        selectedIds: group.children, // Select the ungrouped elements
      };
    });
  },
  toggleSnapToGrid: () => set((state) => ({ snapToGrid: !state.snapToGrid })),
  setGridSize: (size) => set({ gridSize: size }),
  undo: () => set((state) => {
    if (state.undoStack.length === 0) return state;

    const previousState = state.undoStack[state.undoStack.length - 1];
    const newUndoStack = state.undoStack.slice(0, -1);

    return {
      ...previousState,
      undoStack: newUndoStack,
      redoStack: [...state.redoStack, {
        elements: state.elements,
        rootIds: state.rootIds,
        selectedIds: state.selectedIds,
        snapToGrid: state.snapToGrid,
        gridSize: state.gridSize,
        gridColor: state.gridColor,
        gridOpacity: state.gridOpacity,
        zoom: state.zoom,
        minZoom: state.minZoom,
        maxZoom: state.maxZoom,
        undoStack: [],
        redoStack: [],
      }],
    };
  }),
  redo: () => set((state) => {
    if (state.redoStack.length === 0) return state;

    const nextState = state.redoStack[state.redoStack.length - 1];
    const newRedoStack = state.redoStack.slice(0, -1);

    return {
      ...nextState,
      undoStack: [...state.undoStack, {
        elements: state.elements,
        rootIds: state.rootIds,
        selectedIds: state.selectedIds,
        snapToGrid: state.snapToGrid,
        gridSize: state.gridSize,
        gridColor: state.gridColor,
        gridOpacity: state.gridOpacity,
        zoom: state.zoom,
        minZoom: state.minZoom,
        maxZoom: state.maxZoom,
        undoStack: [],
        redoStack: [],
      }],
      redoStack: newRedoStack,
    };
  }),
  canUndo: false, // Will be computed
  canRedo: false, // Will be computed
  copyElements: () => set((state) => {
    const elementsToCopy = state.selectedIds
      .map(id => state.elements[id])
      .filter(element => element && !element.isGroup); // Don't copy groups directly

    return { copiedElements: elementsToCopy };
  }),
  pasteElements: () => {
    saveToUndoStack(set, get);
    set((state) => {
      if (state.copiedElements.length === 0) return state;

      const newElements = { ...state.elements };
      const newRootIds = [...state.rootIds];
      const newSelectedIds: string[] = [];

      // Calculate offset for pasted elements (slightly offset from originals)
      const offset = 20;

      state.copiedElements.forEach((element: DesignerElement) => {
        const newId = `copy_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const newElement: DesignerElement = {
          ...element,
          id: newId,
          x: (element.x || 0) + offset,
          y: (element.y || 0) + offset,
        };

        newElements[newId] = newElement;
        newRootIds.push(newId);
        newSelectedIds.push(newId);
      });

      return {
        elements: newElements,
        rootIds: newRootIds,
        selectedIds: newSelectedIds,
      };
    });
  },
  canPaste: false, // Will be computed
  selectAll: () => set((state) => ({
    selectedIds: state.rootIds, // Select all root elements
  })),
  toggleTooltips: () => set((state) => ({ showTooltips: !state.showTooltips })),
  toggleElementLock: (id) => set((state) => ({
    elements: {
      ...state.elements,
      [id]: {
        ...state.elements[id],
        locked: !state.elements[id].locked,
      },
    },
  })),
  toggleVirtualScrolling: () => set((state) => ({ virtualScrolling: !state.virtualScrolling })),
  pageSettings: {
    size: 'A4' as PageSize,
    orientation: 'Portrait' as PageOrientation,
    width: 794,
    height: 1123,
    backgroundColor: '#ffffff',
    margins: { top: 20, right: 20, bottom: 20, left: 20 },
    title: 'Untitled Document',
    description: '',
  },
  templateMetadata: {
    id: `template_${Date.now()}`,
    name: 'Untitled Template',
    description: '',
    category: 'General',
    tags: [],
    version: '1.0.0',
    schemaVersion: '1.0.0',
    createdBy: 'User',
    updatedBy: 'User',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    locale: 'en-US',
    currency: 'USD',
    timezone: 'UTC',
    formattingProfile: {
      dateFormat: 'MM/DD/YYYY',
      timeFormat: 'HH:mm:ss',
      numberFormat: 'en-US',
      currencyFormat: 'USD'
    },
    migrationHints: {},
    isPublic: false,
    isArchived: false
  },
  updatePageSettings: (settings) => set((state) => ({
    pageSettings: { ...state.pageSettings, ...settings },
  })),
  setPageSize: (size) => set((state) => {
    const dimensions = getPageDimensions(size, state.pageSettings.orientation);
    return {
      pageSettings: {
        ...state.pageSettings,
        size,
        ...dimensions,
      },
    };
  }),
  setPageOrientation: (orientation) => set((state) => {
    const dimensions = getPageDimensions(state.pageSettings.size, orientation);
    return {
      pageSettings: {
        ...state.pageSettings,
        orientation,
        ...dimensions,
      },
    };
  }),
  setPageBackgroundColor: (color) => set((state) => ({
    pageSettings: { ...state.pageSettings, backgroundColor: color },
  })),
  setGridColor: (color) => set({ gridColor: color }),
  setGridOpacity: (opacity) => set({ gridOpacity: opacity }),
  exportToPNG: async () => {
    return new Promise<string>((resolve, reject) => {
      try {
        // Create a canvas element to render the design
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          reject(new Error('Could not get canvas context'));
          return;
        }

        const state = get();
        canvas.width = state.pageSettings.width;
        canvas.height = state.pageSettings.height;

        // Fill background
        ctx.fillStyle = state.pageSettings.backgroundColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Draw grid if enabled
        if (state.snapToGrid) {
          ctx.strokeStyle = state.gridColor;
          ctx.globalAlpha = state.gridOpacity;
          ctx.lineWidth = 1;

          // Vertical lines
          for (let x = 0; x <= canvas.width; x += state.gridSize) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, canvas.height);
            ctx.stroke();
          }

          // Horizontal lines
          for (let y = 0; y <= canvas.height; y += state.gridSize) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(canvas.width, y);
            ctx.stroke();
          }

          ctx.globalAlpha = 1; // Reset alpha
        }

        // Draw elements (simplified for now - just text elements)
        state.rootIds.forEach(id => {
          const element = state.elements[id];
          if (element && element.type === 'Text') {
            ctx.fillStyle = '#000000';
            ctx.font = `${element.props.fontSize || 16}px Arial`;
            ctx.fillText(
              element.props.text || '',
              element.x || 0,
              (element.y || 0) + (element.props.fontSize || 16)
            );
          }
        });

        // Convert to data URL
        const dataUrl = canvas.toDataURL('image/png');
        resolve(dataUrl);
      } catch (error) {
        reject(error);
      }
    });
  },
  exportToSVG: () => {
    const state = get();
    // Generate basic SVG structure
    let svgContent = `<svg width="${state.pageSettings.width}" height="${state.pageSettings.height}" xmlns="http://www.w3.org/2000/svg">`;

    // Add background
    svgContent += `<rect width="100%" height="100%" fill="${state.pageSettings.backgroundColor}"/>`;

    // Add elements (simplified for now)
    state.rootIds.forEach(id => {
      const element = state.elements[id];
      if (element && element.type === 'Text') {
        const x = element.x || 0;
        const y = element.y || 0;
        const fontSize = element.props.fontSize || 16;
        svgContent += `<text x="${x}" y="${y + fontSize}" font-size="${fontSize}" fill="#000000">${element.props.text || ''}</text>`;
      }
    });

    svgContent += '</svg>';
    return svgContent;
  },
  exportToPDF: async () => {
    // This will be implemented with a PDF library
    // For now, return a placeholder
    return new Promise((resolve) => {
      setTimeout(() => resolve('PDF export not yet implemented'), 100);
    });
  },
  zoomIn: () => set((state) => ({
    zoom: Math.min(state.zoom * 1.2, state.maxZoom),
  })),
  zoomOut: () => set((state) => ({
    zoom: Math.max(state.zoom / 1.2, state.minZoom),
  })),
  zoomToFit: () => set((state) => {
    // Calculate zoom to fit the page in the viewport
    // This is a simplified calculation - in a real app you'd consider the actual viewport size
    const pageWidth = state.pageSettings.width;
    const pageHeight = state.pageSettings.height;
    const viewportWidth = 800; // Approximate viewport width
    const viewportHeight = 600; // Approximate viewport height

    const zoomX = viewportWidth / pageWidth;
    const zoomY = viewportHeight / pageHeight;
    const fitZoom = Math.min(zoomX, zoomY) * 0.9; // Leave some margin

    return {
      zoom: Math.max(state.minZoom, Math.min(fitZoom, state.maxZoom)),
    };
  }),
  resetZoom: () => set({ zoom: 1 }),
  setZoom: (zoom) => set((state) => ({
    zoom: Math.max(state.minZoom, Math.min(zoom, state.maxZoom)),
  })),
  uploadImage: async (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      // Validate file type
      if (!file.type.startsWith('image/')) {
        reject(new Error('Please select a valid image file'));
        return;
      }

      // Validate file size (max 10MB)
      if (file.size > 10 * 1024 * 1024) {
        reject(new Error('Image file size must be less than 10MB'));
        return;
      }

      const reader = new FileReader();

      reader.onload = (e) => {
        if (e.target?.result) {
          resolve(e.target.result as string);
        } else {
          reject(new Error('Failed to read image file'));
        }
      };

      reader.onerror = () => {
        reject(new Error('Error reading image file'));
      };

      reader.readAsDataURL(file);
    });
  },
  addToast: (message, type = 'info', duration = 4000) => set((state) => ({
    toasts: [...state.toasts, {
      id: Date.now().toString(),
      message,
      type,
      duration,
    }],
  })),
  removeToast: (id) => set((state) => ({
    toasts: state.toasts.filter(toast => toast.id !== id),
  })),
  setAlignmentGuides: (guides) => set({ alignmentGuides: guides }),
  clearAlignmentGuides: () => set({ alignmentGuides: { vertical: [], horizontal: [], distances: [] } }),
  updateElementBinding: (id, binding) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          binding,
        },
      },
    }));
  },
  updateElementExpression: (id, expression) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          expression,
        },
      },
    }));
  },
  updateElementRepeat: (id, repeat) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          repeat,
        },
      },
    }));
  },
  updateElementOverflow: (id, overflow) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          overflow,
        },
      },
    }));
  },
  updateElementImage: (id, image) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          image,
        },
      },
    }));
  },
  updateElementTable: (id, table) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          table,
        },
      },
    }));
  },
  updateElementValidation: (id, validation) => {
    saveToUndoStack(set, get);
    set((state) => ({
      elements: {
        ...state.elements,
        [id]: {
          ...state.elements[id],
          validation,
        },
      },
    }));
  },
  updateTemplateMetadata: (metadata) => set((state) => ({
    templateMetadata: {
      ...state.templateMetadata,
      ...metadata,
      updatedAt: new Date().toISOString(),
      updatedBy: metadata.updatedBy || state.templateMetadata.updatedBy || 'User'
    },
  })),
  updateSamplePayload: (payload) => set({ samplePayload: payload }),
  loadTemplate: (template) => set((state) => ({
    elements: template.elements.reduce((acc, element) => {
      acc[element.id] = element;
      return acc;
    }, {} as Record<string, DesignerElement>),
    rootIds: template.elements
      .filter(element => !template.elements.some(parent => parent.children?.includes(element.id)))
      .map(element => element.id),
    selectedIds: [],
    pageSettings: template.pageSettings,
    templateMetadata: template.metadata || state.templateMetadata,
    undoStack: [],
    redoStack: [],
  })),
  setPreviewMode: (mode) => set({ previewMode: mode }),
  validateTemplate: () => set((state) => {
    const errors: Array<{ elementId: string; message: string; severity: 'error' | 'warning' }> = [];

    // Validate all elements
    Object.values(state.elements).forEach(element => {
      // Check for missing required bindings
      if (element.binding?.required && !element.binding.dataPath) {
        errors.push({
          elementId: element.id,
          message: `Required field "${element.binding.requiredMessage || 'Field'}" has no data path`,
          severity: 'error'
        });
      }

      // Check for invalid binding paths
      if (element.binding?.dataPath) {
        // Simple validation - check if path exists in sample payload
        const pathExists = checkPathExists(state.samplePayload, element.binding.dataPath);
        if (!pathExists) {
          errors.push({
            elementId: element.id,
            message: `Data path "${element.binding.dataPath}" not found in sample data`,
            severity: 'warning'
          });
        }
      }

      // Check for expression errors
      if (element.expression?.visibleWhen) {
        try {
          // Basic syntax check - in a real implementation, this would use the expression engine
          new Function('data', `return ${element.expression.visibleWhen}`);
        } catch (err) {
          errors.push({
            elementId: element.id,
            message: `Invalid visibility expression: ${element.expression.visibleWhen}`,
            severity: 'error'
          });
        }
      }

      if (element.expression?.valueExpression) {
        try {
          new Function('data', `return ${element.expression.valueExpression}`);
        } catch (err) {
          errors.push({
            elementId: element.id,
            message: `Invalid value expression: ${element.expression.valueExpression}`,
            severity: 'error'
          });
        }
      }
    });

    return { previewErrors: errors };
  }),
}));

// Page size dimensions in pixels (at 96 DPI)
const PAGE_SIZES = {
  A4: { width: 794, height: 1123 }, // 210mm x 297mm
  A5: { width: 559, height: 794 }, // 148mm x 210mm
  A6: { width: 397, height: 559 }, // 105mm x 148mm
  Letter: { width: 816, height: 1056 }, // 8.5" x 11"
  Legal: { width: 816, height: 1344 }, // 8.5" x 14"
  Custom: { width: 800, height: 600 },
};

const getPageDimensions = (size: PageSize, orientation: PageOrientation) => {
  const dimensions = PAGE_SIZES[size];
  return orientation === 'Landscape'
    ? { width: dimensions.height, height: dimensions.width }
    : dimensions;
};

// Create debounced versions for frequent updates
export const useDebouncedStore = () => {
  const store = useDesignerStore();

  return {
    ...store,
    updateElementPosition: debounce(store.updateElementPosition, 16), // ~60fps
    updateElementSize: debounce(store.updateElementSize, 16),
  };
};

// Helper function to check if a path exists in an object
const checkPathExists = (obj: any, path: string): boolean => {
  try {
    const keys = path.split('.');
    let current = obj;

    for (const key of keys) {
      if (current && typeof current === 'object' && key in current) {
        current = current[key];
      } else {
        return false;
      }
    }

    return true;
  } catch (error) {
    return false;
  }
};

// Hook for computed properties
export const useComputedStore = () => {
  const store = useDesignerStore();

  return {
    ...store,
    canUndo: store.undoStack.length > 0,
    canRedo: store.redoStack.length > 0,
    canPaste: store.copiedElements.length > 0,
  };
};
