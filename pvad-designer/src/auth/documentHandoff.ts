export const documentHandoffKeys = {
  migration: 'pvad_migration_designer_handoff',
  pdfViewer: 'pvad_pdf_viewer_handoff',
} as const;

type HandoffKey = typeof documentHandoffKeys[keyof typeof documentHandoffKeys];
interface Owner { userId: string; organizationId: string; }
interface Envelope { schemaVersion: 1; owner: Owner; expiresAt: number; payload: unknown; }
let owner: Owner | null = null;
const pending = new Map<HandoffKey, Envelope>();
const lifetimeMs = 5 * 60_000;

export function setDocumentHandoffOwner(userId: string | null, organizationId: string | null): void {
  const next = userId && organizationId ? { userId, organizationId } : null;
  if (owner?.userId !== next?.userId || owner?.organizationId !== next?.organizationId) pending.clear();
  owner = next;
}

export function writeDocumentHandoff(key: HandoffKey, payload: unknown): void {
  if (!owner) throw new Error('An authenticated document workspace is required.');
  const envelope: Envelope = { schemaVersion: 1, owner: { ...owner }, expiresAt: Date.now() + lifetimeMs, payload };
  pending.set(key, envelope);
  try {
    sessionStorage.setItem(key, JSON.stringify(envelope));
  } catch {
    // Keep same-tab transfers usable, but do not leave an older stored transfer.
    try { sessionStorage.removeItem(key); } catch { /* Storage is unavailable. */ }
  }
}

export function readDocumentHandoff<T>(key: HandoffKey): T | null {
  if (!owner) return null;
  try {
    const envelope = pending.get(key) ?? JSON.parse(sessionStorage.getItem(key) ?? 'null');
    if (envelope?.schemaVersion !== 1 || envelope.owner?.userId !== owner.userId
      || envelope.owner?.organizationId !== owner.organizationId
      || !Number.isFinite(envelope.expiresAt) || envelope.expiresAt <= Date.now()
      || envelope.expiresAt > Date.now() + lifetimeMs) return null;
    return envelope.payload as T;
  } catch {
    return null;
  }
}

export function consumeDocumentHandoff(key: HandoffKey): void {
  pending.delete(key);
  try { sessionStorage.removeItem(key); } catch { /* Storage is unavailable. */ }
}

export function clearDocumentHandoffs(): void {
  for (const key of Object.values(documentHandoffKeys)) consumeDocumentHandoff(key);
  owner = null;
}
