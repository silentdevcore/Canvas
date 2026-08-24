/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import MigrationsPage from '@/pages/MigrationsPage';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import { designerPermissions, migrationProviderPermission } from '@/auth/designerPermissions';
import type { DesignerUser } from '@/auth/designerAuth';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('@monaco-editor/react', () => ({
  __esModule: true,
  default: ({ value }: { value?: string }) => <textarea value={value ?? ''} readOnly />,
  DiffEditor: ({ modified }: { modified?: string }) => <textarea value={modified ?? ''} readOnly />,
}));

const limitedUser: DesignerUser = {
  id: 'limited-user',
  username: 'limited@example.test',
  email: 'limited@example.test',
  displayName: 'Limited Migration User',
  roles: ['Limited Migration Tester'],
  permissions: [
    designerPermissions.access,
    designerPermissions.migrationPdfRead,
    designerPermissions.migrationPdfExecute,
    designerPermissions.migrationSpreadsheetRead,
    designerPermissions.migrationSpreadsheetExecute,
    migrationProviderPermission('pdf', 'syncfusion-pdf'),
    migrationProviderPermission('spreadsheet', 'aspose-cells'),
  ],
  organizations: [{ id: 'org-1', name: 'Test Organization', slug: 'test' }],
  activeOrganizationId: 'org-1',
  lastLoginAt: null,
};

const frameworks = [
  {
    id: 'Syncfusion', name: 'Syncfusion PDF', status: 'full', description: 'Allowed PDF', kind: 'pdf',
    requiredPermission: migrationProviderPermission('pdf', 'syncfusion-pdf'),
  },
  {
    id: 'Aspose', name: 'Aspose.PDF', status: 'full', description: 'Blocked PDF', kind: 'pdf',
    requiredPermission: migrationProviderPermission('pdf', 'aspose-pdf'),
  },
  {
    id: 'AsposeCells', name: 'Aspose.Cells', status: 'full', description: 'Allowed Spreadsheet', kind: 'spreadsheet',
    requiredPermission: migrationProviderPermission('spreadsheet', 'aspose-cells'),
  },
  {
    id: 'ClosedXmlSpreadsheet', name: 'ClosedXML', status: 'full', description: 'Blocked Spreadsheet', kind: 'spreadsheet',
    requiredPermission: migrationProviderPermission('spreadsheet', 'closedxml'),
  },
];

const renderPage = async (container: HTMLElement, kind: 'pdf' | 'spreadsheet'): Promise<Root> => {
  const root = createRoot(container);
  await act(async () => {
    root.render(
      <MemoryRouter>
        <DesignerAuthContextProvider user={limitedUser}>
          <MigrationsPage mode="code" codeKind={kind} />
        </DesignerAuthContextProvider>
      </MemoryRouter>,
    );
    await new Promise(resolve => window.setTimeout(resolve, 0));
  });
  return root;
};

const waitForOptions = async (container: HTMLElement, expected: string[]) => {
  for (let index = 0; index < 20; index++) {
    const values = Array.from(container.querySelectorAll<HTMLSelectElement>('#mgr-fw-select option'))
      .map(option => option.value);
    if (JSON.stringify(values) === JSON.stringify(expected)) return;
    await act(async () => { await new Promise(resolve => window.setTimeout(resolve, 0)); });
  }
  throw new Error(`Framework options did not become ${expected.join(', ')}.`);
};

describe('migration provider permissions', () => {
  let container: HTMLDivElement;
  let root: Root | null;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = null;
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => frameworks,
    });
  });

  afterEach(() => {
    if (root) act(() => root!.unmount());
    container.remove();
  });

  it('shows only the allowed PDF provider', async () => {
    root = await renderPage(container, 'pdf');
    await waitForOptions(container, ['Syncfusion']);
  });

  it('shows only the allowed Spreadsheet provider', async () => {
    root = await renderPage(container, 'spreadsheet');
    await waitForOptions(container, ['AsposeCells']);
  });
});
