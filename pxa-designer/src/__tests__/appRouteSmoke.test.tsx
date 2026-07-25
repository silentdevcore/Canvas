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

const authenticatedDesignerUser = {
  id: 'user-1',
  username: 'designer@example.test',
  email: 'designer@example.test',
  displayName: 'Designer User',
  roles: ['Editor'],
  organizations: [{ id: 'org-1', name: 'Test Organization', slug: 'test' }],
  activeOrganizationId: 'org-1',
  lastLoginAt: null,
};

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

jest.mock('@/components/Editor/SimplePxaSurface', () => ({
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
    global.fetch = jest.fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => authenticatedDesignerUser,
      });
  });

  afterEach(() => {
    if (root) {
      act(() => {
        root!.unmount();
      });
    }
    container.remove();
  });

  test('old /migrations bookmark redirects into the PDF hub\'s migrations view', async () => {
    const fetchMock = global.fetch as jest.Mock;
    // MigrationsPage in "code" mode fetches the frameworks list on mount.
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => [] });

    root = await renderRoute(container, '/migrations');

    await waitUntil(() => {
      expect(getByText(container, 'PDF Code Migration')).toBeTruthy();
    });
  });

  test('PDF hub sidebar lists every PDF sidebar item', async () => {
    // /pdf/import (ImporterPage) has no unmocked fetch-on-mount dependency,
    // unlike the /pdf index redirect into the real CreatePage/editor store.
    root = await renderRoute(container, '/pdf/import');

    await waitUntil(() => {
      expect(getByText(container, 'Create PDF')).toBeTruthy();
      expect(getByText(container, 'Edit PDF')).toBeTruthy();
      expect(getByText(container, 'Use Template')).toBeTruthy();
      expect(getByText(container, 'Import PDF')).toBeTruthy();
      expect(getByText(container, 'Convert to PDF')).toBeTruthy();
      expect(getByText(container, 'PDF Viewer')).toBeTruthy();
      expect(getByText(container, 'Migrations')).toBeTruthy();
    });
  });

  test('PDF sidebar switches cleanly between Create PDF and Edit PDF, back and forth', async () => {
    // Regression test: /pdf/create and /pdf/edit (-> /pdf/create?mode=code) are
    // the same matched route, so React Router doesn't remount CreatePage when
    // switching between them via the sidebar — only the `mode` query param
    // changes. Two related bugs used to happen: (1) CreatePage's `subView`
    // state was set once from the URL at mount and never re-synced, so the
    // view got stuck on whichever one loaded first; (2) the sidebar highlight
    // is driven by NavLink's pathname-only matching, which can't tell /pdf/edit
    // apart from /pdf/create (both resolve to the same pathname), so "Create
    // PDF" stayed highlighted even while viewing the Edit/code view.
    root = await renderRoute(container, '/pdf/create');

    await waitUntil(() => {
      expect(getByText(container, /Designer: Untitled document/)).toBeTruthy();
    });

    const createLink = () => container.querySelector('a[href="/pdf/create"]') as HTMLAnchorElement;
    const editLink = () => container.querySelector('a[href="/pdf/edit"]') as HTMLAnchorElement;

    expect(createLink().className).toContain('is-active');
    expect(editLink().className).not.toContain('is-active');

    await click(editLink());
    await waitUntil(() => {
      expect(getByText(container, 'Code editor')).toBeTruthy();
    });
    expect(editLink().className).toContain('is-active');
    expect(createLink().className).not.toContain('is-active');

    await click(createLink());
    await waitUntil(() => {
      expect(getByText(container, /Designer: Untitled document/)).toBeTruthy();
    });
    expect(createLink().className).toContain('is-active');
    expect(editLink().className).not.toContain('is-active');

    // And back again, to prove it isn't a one-shot fix that only works once.
    await click(editLink());
    await waitUntil(() => {
      expect(getByText(container, 'Code editor')).toBeTruthy();
    });
    expect(editLink().className).toContain('is-active');
  });

  test('Spreadsheet hub sidebar lists every spreadsheet sidebar item', async () => {
    // /spreadsheet/import (SpreadsheetImportPage) is already mocked above;
    // the /spreadsheet index redirect instead lazy-loads the real, heavy
    // SpreadsheetEditorPage, which pulls in @glideapps/glide-data-grid — a
    // package this project's Jest config doesn't transform (ESM-only, same
    // as it doesn't transform framer-motion/monaco, which is why those are
    // mocked above too).
    root = await renderRoute(container, '/spreadsheet/import');

    await waitUntil(() => {
      expect(getByText(container, 'Create Spreadsheet')).toBeTruthy();
      expect(getByText(container, 'Edit Spreadsheet')).toBeTruthy();
      expect(getByText(container, 'Import Spreadsheet')).toBeTruthy();
      // Disabled item renders "Convert to Spreadsheet" alongside a "Coming
      // soon" child <small>, so the two concatenate in textContent — match
      // the label as a substring rather than requiring an exact-text node.
      expect(getByText(container, /Convert to Spreadsheet/)).toBeTruthy();
    });
  });

  test('Home shows both the PDF tools and Spreadsheet tools sections', async () => {
    root = await renderRoute(container, '/');

    await waitUntil(() => {
      expect(getByText(container, 'Every PDF tool you need')).toBeTruthy();
      expect(getByText(container, 'Every spreadsheet tool you need')).toBeTruthy();
      expect(getByText(container, 'Edit PDF')).toBeTruthy();
      expect(getByText(container, 'Create Spreadsheet')).toBeTruthy();
      expect(getByText(container, 'Edit Spreadsheet')).toBeTruthy();
      expect(getByText(container, 'Import Spreadsheet')).toBeTruthy();
      expect(getByText(container, /Convert to Spreadsheet/)).toBeTruthy();
      expect(getByText(container, 'Migrations')).toBeTruthy();
    });
  });

  test('Home\'s "Create PDF" tool card opens a blank document in the editor', async () => {
    // Regression test: this card used to `navigate('/pdf/create')` directly with
    // no template in the store, which made CreatePage's guard bounce straight
    // back to "/" — clicking looked like it did nothing. It must go through
    // loadBlank() (like the hero "Blank document" card) so a template exists
    // before CreatePage ever mounts.
    root = await renderRoute(container, '/');

    await waitUntil(() => {
      expect(getByText(container, 'Every PDF tool you need')).toBeTruthy();
    });

    const label = Array.from(container.querySelectorAll('strong'))
      .find(el => el.textContent?.trim() === 'Create PDF');
    const card = label?.closest('.pdf-tool-card') as HTMLElement | undefined;
    if (!card) throw new Error('"Create PDF" tool card not found');
    await click(card);

    await waitUntil(() => {
      expect(getByText(container, /Designer: Untitled document/)).toBeTruthy();
    });
  });

  test('Home\'s "Edit PDF" tool card opens the code editor with a blank document', async () => {
    root = await renderRoute(container, '/');

    await waitUntil(() => {
      expect(getByText(container, 'Every PDF tool you need')).toBeTruthy();
    });

    const label = Array.from(container.querySelectorAll('strong'))
      .find(el => el.textContent?.trim() === 'Edit PDF');
    const card = label?.closest('.pdf-tool-card') as HTMLElement | undefined;
    if (!card) throw new Error('"Edit PDF" tool card not found');
    await click(card);

    await waitUntil(() => {
      expect(getByText(container, 'Code editor')).toBeTruthy();
    });
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
