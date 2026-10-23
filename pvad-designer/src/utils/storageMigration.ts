type BrowserStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

/** Migrate only explicitly supplied, same-owner keys after a successful write. */
export function readMigratedStorage(storage: BrowserStorage, key: string, legacyKey: string): string | null {
  const current = storage.getItem(key);
  if (current !== null) return current;
  const legacy = storage.getItem(legacyKey);
  if (legacy === null) return null;
  try {
    storage.setItem(key, legacy);
    storage.removeItem(legacyKey);
  } catch {
    // Retain the original when storage is full or unavailable.
  }
  return legacy;
}
