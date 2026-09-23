/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import MigrationsPage from '@/pages/MigrationsPage';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import {
  canonicalMigrationProviderPermission,
  designerPermissions,
  migrationKindPermission,
  migrationProviderPermission,
} from '@/auth/designerPermissions';
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

const providers = [
  {
    providerKey: 'syncfusion-pdf', providerFamily: 'syncfusion', displayName: 'Syncfusion PDF', status: 'full', description: 'Allowed PDF', domain: 'pdf', kind: 'code', aliases: ['Syncfusion'], requiredPermission: canonicalMigrationProviderPermission('pdf', 'code', 'syncfusion'),
  },
  {
    providerKey: 'devexpress-pdf', providerFamily: 'devexpress', displayName: 'DevExpress PDF', status: 'full', description: 'Denied PDF', domain: 'pdf', kind: 'code', aliases: ['DevExpress'], requiredPermission: canonicalMigrationProviderPermission('pdf', 'code', 'devexpress'),
  },
  {
    providerKey: 'aspose-cells', providerFamily: 'aspose', displayName: 'Aspose.Cells', status: 'full', description: 'Allowed Spreadsheet', domain: 'spreadsheet', kind: 'code', aliases: ['AsposeCells'], requiredPermission: canonicalMigrationProviderPermission('spreadsheet', 'code', 'aspose'),
  },
  {
    providerKey: 'closedxml', providerFamily: 'closedxml', displayName: 'ClosedXML', status: 'full', description: 'Denied Spreadsheet', domain: 'spreadsheet', kind: 'code', aliases: ['ClosedXmlSpreadsheet'], requiredPermission: canonicalMigrationProviderPermission('spreadsheet', 'code', 'closedxml'),
  },
];

const renderPage = async (
  container: HTMLElement,
  kind: 'pdf' | 'spreadsheet',
  user: DesignerUser = limitedUser,
): Promise<Root> => {
  const root = createRoot(container);
  await act(async () => {
    root.render(
      <MemoryRouter>
        <DesignerAuthContextProvider user={user}>
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
    global.fetch = jest.fn().mockImplementation((input: RequestInfo | URL) => {
      const url = String(input);
      const domain = url.includes('domain=spreadsheet') ? 'spreadsheet' : 'pdf';
      return Promise.resolve({ ok: true, json: async () => ({ providers: providers.filter(item => item.domain === domain) }) });
    });
  });

  afterEach(() => {
    if (root) act(() => root!.unmount());
    container.remove();
  });

  it('shows only the allowed PDF provider', async () => {
    root = await renderPage(container, 'pdf');
    await waitForOptions(container, ['syncfusion-pdf']);
  });

  it('shows only the allowed Spreadsheet provider', async () => {
    root = await renderPage(container, 'spreadsheet');
    await waitForOptions(container, ['aspose-cells']);
  });

  it('supports a role containing only canonical kind and provider permissions', async () => {
    const canonicalUser: DesignerUser = {
      ...limitedUser,
      id: 'canonical-user',
      permissions: [
        designerPermissions.access,
        migrationKindPermission('pdf', 'code', 'read'),
        migrationKindPermission('pdf', 'code', 'execute'),
        canonicalMigrationProviderPermission('pdf', 'code', 'syncfusion'),
      ],
    };

    root = await renderPage(container, 'pdf', canonicalUser);
    await waitForOptions(container, ['syncfusion-pdf']);
  });
});
