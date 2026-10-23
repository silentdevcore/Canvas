/** @jest-environment jsdom */
import { NativeProjectInbox, NativeProjectError, nativeProjectKind, nativePackageLimit,
  stageNativeProject, takeNativeProject, clearNativeProjects, canImportNativeProject } from '@/native/nativeProjectLaunch';
import type { DesignerUser } from '@/auth/designerAuth';
import { designerPermissions } from '@/auth/designerPermissions';

const user = (id = 'user', organization = 'org'): DesignerUser => ({
  id, activeOrganizationId: organization, username: 'synthetic', email: 'synthetic@example.test', displayName: 'Synthetic',
  roles: [], permissions: Object.values(designerPermissions), organizations: [], lastLoginAt: null,
});
const file = (name = 'sample.pvadp') => new File(['synthetic package'], name);

afterEach(() => { clearNativeProjects(); jest.useRealTimers(); });

test('identifies only canonical project names and rejects paths and unsupported extensions', () => {
  expect(nativeProjectKind('Report.PVADP')).toBe('pdf');
  expect(nativeProjectKind('Budget.pvads')).toBe('spreadsheet');
  for (const name of ['file.zip', 'file.pdf', '../file.pvadp', 'c:\\file.pvads', 'file.pvadp\n', 'a'.repeat(256) + '.pvads'])
    expect(() => nativeProjectKind(name)).toThrow(NativeProjectError);
});

test('receiving a launch does not read file bytes and does not overwrite a waiting file', async () => {
  const inbox = new NativeProjectInbox();
  const selected = file(), getFile = jest.fn(async () => selected);
  inbox.enqueue([{ kind: 'file', name: selected.name, getFile }]);
  expect(getFile).not.toHaveBeenCalled();
  inbox.enqueue([{ kind: 'file', name: 'other.pvads', getFile }]);
  expect(inbox.getSnapshot()).toEqual({ name: selected.name, error: 'busy' });
  expect(await inbox.read()).toBe(selected);
  expect(getFile).toHaveBeenCalledTimes(1);
});

test('multiple files, expiry, cancellation during reads and size limits fail closed', async () => {
  let now = 0;
  const inbox = new NativeProjectInbox(() => now);
  inbox.enqueue([]);
  expect(inbox.getSnapshot().error).toBe('multiple');
  inbox.enqueue([{ kind: 'file', name: 'sample.pvadp', getFile: async () => file() }]);
  now = 300_001;
  await expect(inbox.read()).rejects.toMatchObject({ reason: 'expired' });
  const big = file(); Object.defineProperty(big, 'size', { value: nativePackageLimit + 1 });
  inbox.enqueue([{ kind: 'file', name: big.name, getFile: async () => big }]);
  await expect(inbox.read()).rejects.toMatchObject({ reason: 'tooLarge' });
  inbox.clear();
  let complete!: (value: File) => void;
  inbox.enqueue([{ kind: 'file', name: 'sample.pvadp', getFile: () => new Promise(resolve => { complete = resolve; }) }]);
  const pending = inbox.read(); inbox.clear(); complete(file());
  await expect(pending).rejects.toMatchObject({ reason: 'expired' });
});

test('transfers are one-time, typed, time-bounded and bound to both user and organization', () => {
  jest.useFakeTimers().setSystemTime(new Date('2026-10-22'));
  const selected = file();
  const id = stageNativeProject(selected, user());
  expect(takeNativeProject(id, user('other'), 'pdf')).toBeNull();
  expect(takeNativeProject(id, user(), 'pdf')).toBeNull();
  const organization = stageNativeProject(selected, user());
  expect(takeNativeProject(organization, user('user', 'other'), 'pdf')).toBeNull();
  const wrongKind = stageNativeProject(selected, user());
  expect(takeNativeProject(wrongKind, user(), 'spreadsheet')).toBeNull();
  const expired = stageNativeProject(selected, user()); jest.advanceTimersByTime(300_001);
  expect(takeNativeProject(expired, user(), 'pdf')).toBeNull();
  const valid = stageNativeProject(selected, user());
  expect(takeNativeProject(valid, user(), 'pdf')).toBe(selected);
  expect(takeNativeProject(valid, user(), 'pdf')).toBeNull();
});

test('import permission and active organization are required, not just create permission', () => {
  const denied = user(); denied.permissions = ['designer.pdf.create'];
  expect(canImportNativeProject(denied, 'pdf')).toBe(false);
  expect(() => stageNativeProject(file(), denied)).toThrow(NativeProjectError);
  const absent = user(); absent.activeOrganizationId = null;
  expect(canImportNativeProject(absent, 'spreadsheet')).toBe(false);
});
