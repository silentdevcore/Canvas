import { readPvadDocumentHeader } from '../utils/pvadDocumentHeaders';

const headers = (values: Record<string, string>): Pick<Headers, 'get'> => ({
  get: name => values[name] ?? null,
});

test.each([
  [{ 'PVAD-Source-SHA256': 'value' }, 'value'],
  [{ 'PVAD-Source-SHA256': '' }, ''],
  [{ 'PXA-Source-SHA256': 'former' }, null],
  [{}, null],
] as [Record<string, string>, string | null][])('reads only the canonical document header', (values, expected) => {
  expect(readPvadDocumentHeader(headers(values), 'Source-SHA256')).toBe(expected);
});
