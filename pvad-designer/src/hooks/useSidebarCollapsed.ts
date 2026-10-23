import { useEffect, useState } from 'react';
import { readMigratedStorage } from '@/utils/storageMigration';

/** Persists a hub sidebar's collapsed/expanded state per storage key across navigation and reloads. */
export function useSidebarCollapsed(storageKey: string, legacyKey = storageKey): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return readMigratedStorage(localStorage, storageKey, legacyKey) === '1';
    } catch {
      return false;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(storageKey, collapsed ? '1' : '0');
    } catch {
      // Storage unavailable (private browsing, etc.) — collapsed state just won't persist.
    }
  }, [storageKey, collapsed]);

  return [collapsed, () => setCollapsed(value => !value)];
}
