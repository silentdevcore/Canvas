/**
 * @jest-environment jsdom
 */
import React from 'react';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import App from '@/App';
import { DEFAULT_PAGE_SETTINGS, useEditorStore } from '@/store';
import { ExportService } from '@/services/ExportService';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('framer-motion', () => {
  const passthrough = ({ children, ...props }: { children?: React.ReactNode }) => (
    <div {...props}>{children}</div>
  );

  return {
    AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
    motion: new Proxy({}, { get: () => passthrough }),
  };
});

jest.mock('@monaco-editor/react', () => {
  const Editor = ({
    value,
    onChange,
    options,
  }: {
    value?: string;
    onChange?: (value?: string) => void;
    options?: { readOnly?: boolean };
  }) => (
    <textarea
      aria-label={options?.readOnly ? 'Converted output' : 'Source editor'}
      readOnly={options?.readOnly}
      value={value ?? ''}
      onChange={event => onChange?.(event.currentTarget.value)}
    />
  );

  const DiffEditor = ({ modified }: { modified?: string }) => (
    <textarea aria-label="Diff output" readOnly value={modified ?? ''} />
  );

  return { __esModule: true, default: Editor, DiffEditor };
});

jest.mock('@/components/Editor/SimpleCanvas', () => ({
  __esModule: true,
  default: ({
    template,
    onPreview,
  }: {
    template: { name: string };
    onPreview: () => void;
  }) => (
    <section aria-label="Designer smoke">
      <h1>Designer: {template.name}</h1>
      <button type="button" onClick={onPreview}>Preview</button>
    </section>
  ),
}));

jest.mock('@/components/Preview/LivePreview', () => ({
  __esModule: true,
  default: ({
    template,
    onExport,
  }: {
    template: { name: string };
    onExport: () => void;
  }) => (
    <section aria-label="Preview smoke">
      <h1>Preview: {template.name}</h1>
      <button type="button" onClick={onExport}>Export</button>
    </section>
  ),
}));

jest.mock('@/components/CodeEditor/LiveCodeEditor', () => ({
  __esModule: true,
  default: () => <section aria-label="Code editor smoke">Code editor</section>,
}));

jest.mock('@/pages/SpreadsheetImportPage', () => ({
  __esModule: true,
  default: () => <section>Spreadsheet import smoke</section>,
}));

jest.mock('@/features/pdf-viewer/PdfViewerPage', () => ({
  __esModule: true,
  default: () => <section>Open PDF</section>,
}));

jest.mock('@/services/ExportService', () => ({
  __esModule: true,
  ExportService: {
    exportToJSON: jest.fn(),
  },
  default: {
    exportToJSON: jest.fn(),
  },
}));

interface MockPdfDocument {
  numPages: number;
  getPage: () => Promise<{
    getViewport: () => { width: number; height: number };
  }>;
}

jest.mock('react-pdf', () => ({
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
  Document: ({
    children,
    onLoadSuccess,
  }: {
    children: React.ReactNode;
    onLoadSuccess?: (document: MockPdfDocument) => void;
  }) => {
    React.useEffect(() => {
      onLoadSuccess?.({
        numPages: 1,
        getPage: async () => ({
          getViewport: () => ({ width: 612, height: 792 }),
        }),
      });
    }, [onLoadSuccess]);

    return <div data-testid="mock-pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid={`mock-pdf-page-${pageNumber}`}>Page {pageNumber}</div>
  ),
}));

const renderRoute = async (container: HTMLElement, route: string) => {
  const root = createRoot(container);
  await act(async () => {
    root.render(
      <MemoryRouter initialEntries={[route]}>
        <App />
      </MemoryRouter>,
    );
  });
  return root;
};

const flush = async () => {
  await act(async () => {
    await new Promise(resolve => window.setTimeout(resolve, 0));
  });
};

const waitUntil = async (assertion: () => void) => {
  let lastError: unknown;
  for (let i = 0; i < 20; i++) {
    try {
      assertion();
      return;
    } catch (error) {
      lastError = error;
      await flush();
    }
  }
  throw lastError;
};

const getByText = (container: HTMLElement, text: string | RegExp): HTMLElement => {
  const matcher = typeof text === 'string'
    ? (value: string) => value.trim() === text
    : (value: string) => text.test(value);
  const element = Array.from(container.querySelectorAll('*'))
    .find(candidate => matcher(candidate.textContent ?? '')) as HTMLElement | undefined;
  if (!element) throw new Error(`Could not find text ${String(text)}`);
  return element;
};

const getButtonByText = (container: HTMLElement, text: string | RegExp): HTMLButtonElement => {
  const matcher = typeof text === 'string'
    ? (value: string) => value.trim() === text
    : (value: string) => text.test(value);
  const button = Array.from(container.querySelectorAll('button'))
    .find(candidate => matcher(candidate.textContent ?? '')) as HTMLButtonElement | undefined;
  if (!button) throw new Error(`Could not find button ${String(text)}`);
  return button;
};

const click = async (element: HTMLElement) => {
  await act(async () => {
    element.dispatchEvent(new MouseEvent('click', { bubbles: true }));
  });
};

const resetEditorStore = () => {
  useEditorStore.setState({
    currentTemplate: null,
    currentPageIndex: 0,
    selectedElementId: null,
    pageSettings: DEFAULT_PAGE_SETTINGS,
    settingsModifiedSinceExport: false,
    templates: [],
  });
};

describe('app route smoke tests', () => {
  let container: HTMLDivElement;
  let root: Root | null;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = null;
    jest.clearAllMocks();
    localStorage.clear();
    sessionStorage.clear();
    resetEditorStore();
    global.fetch = jest.fn();
  });

  afterEach(() => {
    if (root) {
      act(() => {
        root!.unmount();
      });
    }
    container.remove();
  });

  test('opens the migrations landing page', async () => {
    root = await renderRoute(container, '/migrations');

    expect(getByText(container, 'Migrations')).toBeTruthy();
    expect(getByText(container, 'PDF Migration')).toBeTruthy();
    expect(getByText(container, 'Spreadsheet Migration')).toBeTruthy();
  });

  test('converts a report design, opens it in the designer, previews it, and exports JSON', async () => {
    const fetchMock = global.fetch as jest.Mock;
    fetchMock.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        design: {
          id: 'booking-receipt',
          name: 'Booking Receipt',
          pages: [{ id: 'page-1', elements: [] }],
          sharedElements: [],
          data: {},
          pageSettings: { width: 612, height: 792 },
        },
        diagnostics: [],
      }),
    });

    root = await renderRoute(container, '/migrations/pdf/designer');

    await click(getButtonByText(container, /load example/i));
    await click(getButtonByText(container, /convert/i));

    await waitUntil(() => {
      expect(getButtonByText(container, /open in designer/i).disabled).toBe(false);
    });

    await click(getButtonByText(container, /open in designer/i));

    await waitUntil(() => {
      expect(getByText(container, 'Designer: Booking Receipt')).toBeTruthy();
    });

    await click(getButtonByText(container, 'Preview'));
    expect(getByText(container, 'Preview: Booking Receipt')).toBeTruthy();

    await click(getButtonByText(container, 'Export'));
    expect(ExportService.exportToJSON).toHaveBeenCalledTimes(1);
  });

  test('opens the PDF viewer route', async () => {
    root = await renderRoute(container, '/pdf-viewer');

    await waitUntil(() => {
      expect(getByText(container, 'Open PDF')).toBeTruthy();
    });
  });
});
