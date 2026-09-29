/**
 * @jest-environment jsdom
 */
import React from 'react';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import App from '@/App';
import i18n from '@/i18n';
import { useEditorStore, DEFAULT_PAGE_SETTINGS } from '@/store';
import { designerPermissions } from '@/auth/designerPermissions';
import { getStatusCatalog } from '../../../websites/shared/statusPages';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

const authenticatedDesignerUser = {
  id: 'user-1',
  username: 'designer@example.test',
  email: 'designer@example.test',
  displayName: 'Designer User',
  roles: ['Editor'],
  permissions: Object.values(designerPermissions),
  organizations: [{ id: 'org-1', name: 'Test Organization', slug: 'test' }],
  activeOrganizationId: 'org-1',
  lastLoginAt: null,
};

// Same minimal mock set as appRouteSmoke.test.tsx — needed to render the full
// <App> tree (Home pulls in the editor/preview/code-editor components transitively).
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
  const Editor = ({ value, onChange, options }: { value?: string; onChange?: (value?: string) => void; options?: { readOnly?: boolean } }) => (
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
  default: () => <section aria-label="Designer smoke" />,
}));

jest.mock('@/components/Preview/LivePreview', () => ({
  __esModule: true,
  default: () => <section aria-label="Preview smoke" />,
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
  ExportService: { exportToJSON: jest.fn() },
  default: { exportToJSON: jest.fn() },
}));

const renderApp = async (container: HTMLElement, route: string) => {
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

describe('localization', () => {
  let container: HTMLDivElement;
  let root: Root | null;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = null;
    localStorage.clear();
    sessionStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
    useEditorStore.setState({
      currentTemplate: null,
      currentPageIndex: 0,
      selectedElementId: null,
      pageSettings: DEFAULT_PAGE_SETTINGS,
      settingsModifiedSinceExport: false,
      templates: [],
    });
    global.fetch = jest.fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => authenticatedDesignerUser,
      });
  });

  afterEach(async () => {
    if (root) {
      act(() => {
        root!.unmount();
      });
    }
    container.remove();
    await act(async () => {
      await i18n.changeLanguage('en');
    });
  });

  test('switching locale changes rendered Home text to a real translation', async () => {
    root = await renderApp(container, '/');
    await waitUntil(() => {
      expect(getByText(container, 'Every PDF tool you need')).toBeTruthy();
    });

    await act(async () => {
      await i18n.changeLanguage('de');
    });

    // "Home" -> "Startseite" is a real, hand-translated string in common/de.json
    // (not a fallback) — proves the switch actually re-renders with German text.
    await waitUntil(() => {
      expect(getByText(container, 'Startseite')).toBeTruthy();
    });
  });

  test('switching to Arabic sets dir="rtl" on <html>, and reverts on switching back', async () => {
    root = await renderApp(container, '/');
    await waitUntil(() => {
      expect(getByText(container, 'Every PDF tool you need')).toBeTruthy();
    });

    await act(async () => {
      await i18n.changeLanguage('ar');
    });
    await waitUntil(() => {
      expect(document.documentElement.dir).toBe('rtl');
      expect(document.documentElement.lang).toBe('ar');
    });

    await act(async () => {
      await i18n.changeLanguage('en');
    });
    await waitUntil(() => {
      expect(document.documentElement.dir).toBe('ltr');
    });
  });

  test('a deliberately-unstubbed key in a sparse locale falls back to the English value', async () => {
    // templates.json is intentionally always empty in every locale — template
    // metadata is translated via t(..., { defaultValue }) instead of a namespace
    // file, so fallbackLng should resolve this key to the defaultValue rather
    // than the raw key or "".
    await i18n.changeLanguage('fr');
    expect(i18n.t('templates:sample.name', { defaultValue: 'Sample Template' })).toBe('Sample Template');
    await i18n.changeLanguage('en');
  });

  test('product feature and notification chrome is translated in all six locales', async () => {
    const expectedFeatureTitles: Record<string, string> = {
      en: 'Code workspace',
      de: 'Code-Arbeitsbereich',
      fr: 'Espace de code',
      es: 'Espacio de código',
      it: 'Area di lavoro codice',
      ar: 'مساحة عمل التعليمات البرمجية',
    };

    for (const [language, expectedTitle] of Object.entries(expectedFeatureTitles)) {
      await i18n.changeLanguage(language);
      expect(i18n.t('common:features.codeWorkspace.title')).toBe(expectedTitle);
      expect(i18n.t('common:productExperience.changeCategories.fixed')).not.toContain('productExperience.');
      expect(i18n.t('common:productExperience.notificationCategories.ActionRequired')).not.toContain('productExperience.');
    }
  });

  test('status and recovery messages are translated in all six locales', async () => {
    const expectedNotFoundTitles: Record<string, string> = {
      en: 'Page not found',
      de: 'Seite nicht gefunden',
      fr: 'Page introuvable',
      es: 'Página no encontrada',
      it: 'Pagina non trovata',
      ar: 'الصفحة غير موجودة',
    };

    for (const [language, expectedTitle] of Object.entries(expectedNotFoundTitles)) {
      await i18n.changeLanguage(language);
      expect(i18n.t('status:states.notFound.title')).toBe(expectedTitle);
      expect(i18n.t('status:actions.retry')).not.toContain('status:');
      expect(i18n.t('status:savedWork')).not.toContain('status:');
      for (const status of getStatusCatalog()) {
        expect(i18n.getResource(language, 'status', `states.${status.statusKey}.title`)).toBeTruthy();
        expect(i18n.getResource(language, 'status', `states.${status.statusKey}.message`)).toBeTruthy();
      }
    }
  });
});
