export function readPvadDocumentHeader(headers: Pick<Headers, 'get'>, suffix: string): string | null {
  const canonical = headers.get(`PVAD-${suffix}`);
  const legacy = headers.get(`PXA-${suffix}`);
  if (canonical !== null && legacy !== null && canonical !== legacy) {
    throw new Error(`Conflicting PVAD-${suffix} response header aliases.`);
  }
  return canonical ?? legacy;
}
