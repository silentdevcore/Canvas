import { readPvadDocumentHeader } from '../utils/pvadDocumentHeaders';

const headers = (values: Record<string, string>): Pick<Headers, 'get'> => ({
  get: name => values[name] ?? null,
});

test.each([
  [{ 'PVAD-Source-SHA256': 'value' }, 'value'],
  [{ 'PXA-Source-SHA256': 'old' }, 'old'],
  [{ 'PVAD-Source-SHA256': 'same', 'PXA-Source-SHA256': 'same' }, 'same'],
  [{ 'PVAD-Source-SHA256': '' }, ''],
  [{}, null],
] as [Record<string, string>, string | null][])('reads canonical and retained header generations', (values, expected) => {
  expect(readPvadDocumentHeader(headers(values), 'Source-SHA256')).toBe(expected);
});

test('rejects conflicting evidence aliases without disclosing values', () => {
  const read = () => readPvadDocumentHeader(headers({
    'PVAD-Source-SHA256': 'private-current', 'PXA-Source-SHA256': 'private-old',
  }), 'Source-SHA256');
  expect(read).toThrow('Conflicting PVAD-Source-SHA256 response header aliases.');
  expect(read).not.toThrow(/private-current|private-old/);
});
