import type { UserInfo } from './api';

type ResetAccountState = () => void;

const resetters = new Set<ResetAccountState>();
let activeContextKey: string | null = null;

function contextKey(user: UserInfo | null): string | null {
  return user ? `${user.id}:${user.activeOrganizationId ?? 'none'}` : null;
}

export function registerAccountStateReset(reset: ResetAccountState): () => void {
  resetters.add(reset);
  return () => resetters.delete(reset);
}

export function updateAccountContext(user: UserInfo | null): void {
  const nextKey = contextKey(user);
  if (activeContextKey !== null && activeContextKey !== nextKey)
    resetters.forEach((reset) => reset());
  activeContextKey = nextKey;
}

export function clearAccountContext(): void {
  if (activeContextKey !== null)
    resetters.forEach((reset) => reset());
  activeContextKey = null;
}
