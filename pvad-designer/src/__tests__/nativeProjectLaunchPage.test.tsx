/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import NativeProjectLaunchPage from '@/native/NativeProjectLaunchPage';
import { currentDesignerUser, type DesignerUser } from '@/auth/designerAuth';
import { nativeProjectInbox, clearNativeProjects, takeNativeProject } from '@/native/nativeProjectLaunch';
import { designerPermissions } from '@/auth/designerPermissions';
import labels from '@/locales/en/nativeProject.json';

const navigate = jest.fn();
jest.mock('react-router-dom', () => ({ ...jest.requireActual('react-router-dom'), useNavigate: () => navigate }));
jest.mock('@/auth/designerAuth', () => ({ currentDesignerUser: jest.fn() }));
jest.mock('react-i18next', () => {
  const t = (key: keyof typeof labels) => labels[key] ?? key;
  return { useTranslation: () => ({ t }) };
});
const user: DesignerUser = { id: 'synthetic', activeOrganizationId: 'org', username: 'test', email: 'synthetic@example.test',
  displayName: 'Synthetic User', roles: [], permissions: Object.values(designerPermissions),
  organizations: [{ id: 'org', name: 'Synthetic Company', slug: 'synthetic' }], lastLoginAt: null };
const auth = currentDesignerUser as jest.Mock;
let root: Root, container: HTMLDivElement;
beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  navigate.mockReset(); auth.mockReset(); clearNativeProjects();
  container = document.createElement('div'); document.body.appendChild(container); root = createRoot(container);
});
afterEach(() => { act(() => root.unmount()); container.remove(); clearNativeProjects(); });
const render = async () => { await act(async () => { root.render(<MemoryRouter><NativeProjectLaunchPage /></MemoryRouter>); }); };
const button = (text: string) => Array.from(container.querySelectorAll('button')).find(item => item.textContent === text)!;

test('anonymous launch keeps file bytes unread and offers a separate sign-in window', async () => {
  auth.mockRejectedValue({ status: 401 });
  const read = jest.fn(); nativeProjectInbox.enqueue([{ kind: 'file', name: 'private.pvadp', getFile: read }]);
  await render();
  expect(button(labels.open).disabled).toBe(true);
  const link = container.querySelector('a')!;
  expect(link.getAttribute('href')).toBe('/pdf/template');
  expect(link.getAttribute('rel')).toContain('noopener');
  expect(read).not.toHaveBeenCalled(); expect(navigate).not.toHaveBeenCalled();
});

test('authorized explicit opening stages one owner-bound transfer, not an uploaded filename URL', async () => {
  auth.mockResolvedValue(user);
  const file = new File(['synthetic'], 'private.pvadp'), read = jest.fn(async () => file);
  nativeProjectInbox.enqueue([{ kind: 'file', name: file.name, getFile: read }]);
  await render(); expect(read).not.toHaveBeenCalled();
  await act(async () => { button(labels.open).click(); });
  expect(read).toHaveBeenCalledTimes(1);
  const url = new URL(navigate.mock.calls[0][0], 'https://designer.example.test');
  expect(url.pathname).toBe('/pdf/create'); expect(url.href).not.toContain('private');
  expect(takeNativeProject(url.searchParams.get('nativeLaunch')!, user, 'pdf')).toBe(file);
});

test('account change or insufficient import permission prevents file reads and routing', async () => {
  auth.mockResolvedValueOnce(user).mockResolvedValue({ ...user, activeOrganizationId: 'other' });
  const read = jest.fn(); nativeProjectInbox.enqueue([{ kind: 'file', name: 'private.pvads', getFile: read }]);
  await render(); await act(async () => { button(labels.open).click(); });
  expect(container.querySelector('[role="alert"]')?.textContent).toBe(labels.accountChanged);
  expect(read).not.toHaveBeenCalled(); expect(navigate).not.toHaveBeenCalled();
});
