/** Read one explicitly supplied legacy preference, never enumerate other users' keys. */
export function readMigratedStorage(storage, key, legacyKey) {
  const current = storage.getItem(key);
  if (current !== null) return current;
  const legacy = storage.getItem(legacyKey);
  if (legacy === null) return null;
  try {
    storage.setItem(key, legacy);
    storage.removeItem(legacyKey);
  } catch {
    // Retain the original if the canonical value cannot be persisted.
  }
  return legacy;
}
