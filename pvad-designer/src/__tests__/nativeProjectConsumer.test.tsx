/** @jest-environment jsdom */
import { act, StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter, useLocation } from 'react-router-dom';
import NativeProjectConsumer from '@/native/NativeProjectConsumer';
import { clearNativeProjects, stageNativeProject } from '@/native/nativeProjectLaunch';
import { designerPermissions } from '@/auth/designerPermissions';
import type { DesignerUser } from '@/auth/designerAuth';
import { notify } from '@/notifications/toast';
import { useProjectImportFlow } from '@/hooks/useProjectImportFlow';

let mockUser: DesignerUser;
jest.mock('@/auth/DesignerAuthGate', () => ({ useDesignerAuth: () => ({ user: mockUser }) }));
jest.mock('@/notifications/toast', () => ({ notify: { error: jest.fn() } }));
jest.mock('react-i18next', () => {
  const t = (key: string) => key;
  return { useTranslation: () => ({ t }) };
});

test('starts after parent StrictMode import cleanup instead of consuming an aborted transfer', async () => {
  const id = stageNativeProject(new File(['synthetic'], 'project.pvads'), mockUser);
  const signals: AbortSignal[] = [];
  function Parent() {
    const flow = useProjectImportFlow();
    return <NativeProjectConsumer kind="spreadsheet" onFile={async () => {
      const signal = flow.begin();
      signals.push(signal);
      await Promise.resolve();
      expect(signal.aborted).toBe(false);
      flow.finish();
    }} />;
  }
  const root = createRoot(document.createElement('div'));
  try {
    await act(async () => root.render(<StrictMode><MemoryRouter initialEntries={[`/spreadsheet/create?nativeLaunch=${id}`]}>
      <Parent />
    </MemoryRouter></StrictMode>));
    expect(signals).toHaveLength(1);
    expect(signals[0].aborted).toBe(false);
  } finally { await act(async () => root.unmount()); }
});
beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  mockUser = { id: 'synthetic', activeOrganizationId: 'org', displayName: 'Synthetic',
    username: 'synthetic', email: 'synthetic@example.test', organizations: [], lastLoginAt: null,
    roles: [], permissions: Object.values(designerPermissions) };
  jest.clearAllMocks();
});
afterEach(clearNativeProjects);

test.each([false, true])('consumes once in StrictMode and rejects changed organization: %s', async changed => {
  const file = new File(['synthetic'], 'project.pvadp');
  const id = stageNativeProject(file, mockUser);
  if (changed) mockUser = { ...mockUser, activeOrganizationId: 'other' };
  const onFile = jest.fn(async () => {});
  let search = '';
  function Probe() { search = useLocation().search; return null; }
  const container = document.createElement('div'), root = createRoot(container);
  try {
    await act(async () => root.render(<StrictMode><MemoryRouter initialEntries={[`/pdf/create?nativeLaunch=${id}&keep=1`]}>
      <NativeProjectConsumer kind="pdf" onFile={onFile} /><Probe />
    </MemoryRouter></StrictMode>));
    expect(onFile).toHaveBeenCalledTimes(changed ? 0 : 1);
    if (!changed) expect(onFile).toHaveBeenCalledWith(file);
    expect(search).toBe('?keep=1');
    expect(notify.error).toHaveBeenCalledTimes(changed ? 1 : 0);
  } finally { await act(async () => root.unmount()); }
});
