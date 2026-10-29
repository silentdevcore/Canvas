import type { DesignerUser } from '@/auth/designerAuth';
import { designerPermissions, hasAnyDesignerPermission } from '@/auth/designerPermissions';

export type NativeProjectKind = 'pdf' | 'spreadsheet';
export interface NativeFileHandle { kind: 'file'; name: string; getFile(): Promise<File>; }
export type NativeLaunchError = 'unsupported' | 'multiple' | 'expired' | 'tooLarge' | 'busy' | 'accountChanged';
export class NativeProjectError extends Error {
  constructor(readonly reason: NativeLaunchError) { super(reason); }
}
const lifetime = 5 * 60_000;
export const nativePackageLimit = 25 * 1024 * 1024;

export function nativeProjectKind(name: string): NativeProjectKind {
  if (!name || name.length > 255 || /[/\\\u0000-\u001f\u007f]/.test(name)) throw new NativeProjectError('unsupported');
  if (/\.pvadp$/i.test(name)) return 'pdf';
  if (/\.pvads$/i.test(name)) return 'spreadsheet';
  throw new NativeProjectError('unsupported');
}

export class NativeProjectInbox {
  private pending: { handle: NativeFileHandle; expiresAt: number } | null = null;
  private snapshot: { name: string | null; error: NativeLaunchError | null } = { name: null, error: null };
  private listeners = new Set<() => void>();
  constructor(private now = () => Date.now()) {}
  subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  getSnapshot = () => this.snapshot;
  private emit(name: string | null, error: NativeLaunchError | null) {
    this.snapshot = { name, error };
    this.listeners.forEach(listener => listener());
  }
  enqueue(handles: NativeFileHandle[]): void {
    try {
      if (this.pending && this.pending.expiresAt > this.now()) throw new NativeProjectError('busy');
      if (handles.length !== 1) throw new NativeProjectError('multiple');
      const handle = handles[0];
      if (handle.kind !== 'file' || typeof handle.getFile !== 'function') throw new NativeProjectError('unsupported');
      nativeProjectKind(handle.name);
      this.pending = { handle, expiresAt: this.now() + lifetime };
      this.emit(handle.name, null);
    } catch (error) {
      this.emit(this.pending?.handle.name ?? null, error instanceof NativeProjectError ? error.reason : 'unsupported');
    }
  }
  async read(): Promise<File> {
    const request = this.pending;
    if (!request || request.expiresAt <= this.now()) { this.clear(); throw new NativeProjectError('expired'); }
    const file = await request.handle.getFile();
    if (this.pending !== request || request.expiresAt <= this.now()) throw new NativeProjectError('expired');
    if (file.name !== request.handle.name) throw new NativeProjectError('unsupported');
    nativeProjectKind(file.name);
    if (file.size <= 0 || file.size > nativePackageLimit) throw new NativeProjectError('tooLarge');
    return file;
  }
  clear = () => { this.pending = null; this.emit(null, null); };
}

export const nativeProjectInbox = new NativeProjectInbox();
interface Owner { id: string; activeOrganizationId: string | null; }
const ownerKey = (user: Owner) => JSON.stringify([user.id, user.activeOrganizationId]);
let transfer: { id: string; owner: string; kind: NativeProjectKind; file: File; expiresAt: number } | null = null;
let expiryTimer: ReturnType<typeof setTimeout> | undefined;

export function canImportNativeProject(user: DesignerUser, kind: NativeProjectKind): boolean {
  if (!user.activeOrganizationId) return false;
  const required = kind === 'pdf'
    ? [designerPermissions.pdfAccess, designerPermissions.pdfCreate, designerPermissions.pdfImport]
    : [designerPermissions.spreadsheetAccess, designerPermissions.spreadsheetCreate, designerPermissions.spreadsheetImport];
  return required.every(permission => hasAnyDesignerPermission(user, [permission]));
}

export function stageNativeProject(file: File, user: DesignerUser): string {
  const kind = nativeProjectKind(file.name);
  if (!canImportNativeProject(user, kind)) throw new NativeProjectError('accountChanged');
  if (file.size <= 0 || file.size > nativePackageLimit) throw new NativeProjectError('tooLarge');
  const id = crypto.randomUUID();
  clearTimeout(expiryTimer);
  transfer = { id, owner: ownerKey(user), kind, file, expiresAt: Date.now() + lifetime };
  expiryTimer = setTimeout(() => { transfer = null; }, lifetime);
  return id;
}

export function takeNativeProject(id: string, user: DesignerUser, kind: NativeProjectKind): File | null {
  const request = transfer;
  if (!request || request.id !== id) return null;
  clearTimeout(expiryTimer);
  transfer = null;
  if (request.owner !== ownerKey(user) || request.kind !== kind || request.expiresAt <= Date.now()
      || !canImportNativeProject(user, kind)) return null;
  return request.file;
}

export function clearNativeProjects(): void { clearTimeout(expiryTimer); transfer = null; nativeProjectInbox.clear(); }

export function installNativeProjectLaunch(target: Window = window): boolean {
  const queue = (target as Window & { launchQueue?: { setConsumer(callback: (request: { files?: NativeFileHandle[] }) => void): void } }).launchQueue;
  if (!queue || !target.isSecureContext) return false;
  queue.setConsumer(request => {
    // Normal app launches have no files and must not disturb a pending intake.
    if (request.files?.length) nativeProjectInbox.enqueue(request.files);
  });
  return true;
}
