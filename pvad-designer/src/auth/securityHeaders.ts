/** Normalize mixed-version callers before fetching a new CSRF token. */
export function normalizeSecurityHeaders(headers: Headers): void {
  for (const suffix of ['Application', 'CSRF', 'API-Key']) {
    const canonical = `X-PVAD-${suffix}`;
    const legacy = `X-PXA-${suffix}`;
    const oldValue = headers.get(legacy);
    if (oldValue === null) continue;
    const current = headers.get(canonical);
    const matches = suffix === 'Application'
      ? current?.toLowerCase() === oldValue.toLowerCase()
      : current === oldValue;
    if (current !== null && !matches)
      throw new Error(`Conflicting security headers: ${canonical} and ${legacy}.`);
    if (current === null) headers.set(canonical, oldValue);
    headers.delete(legacy);
  }
}
