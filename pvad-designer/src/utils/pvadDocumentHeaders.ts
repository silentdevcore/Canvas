export function readPvadDocumentHeader(headers: Pick<Headers, 'get'>, suffix: string): string | null {
  return headers.get(`PVAD-${suffix}`);
}
