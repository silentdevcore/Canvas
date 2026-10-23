export function readMigratedStorage(
  storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>,
  key: string,
  legacyKey: string,
): string | null;
