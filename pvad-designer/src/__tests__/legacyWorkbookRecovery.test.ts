import { findLegacyWorkbookDrafts, legacyWorkbookJson } from '../spreadsheet/legacyRecovery';
import { emptySheet } from '../spreadsheet/types';

test('discovery only reads explicit legacy keys and does not mutate storage', () => {
  const values = new Map([['pxa-spreadsheet', 'old'], ['pvad-spreadsheet:v1:other:org', 'private']]);
  const getItem = jest.fn((key: string) => values.get(key) ?? null);
  expect(findLegacyWorkbookDrafts({ getItem })).toEqual([{ key: 'pxa-spreadsheet', raw: 'old' }]);
  expect(getItem).not.toHaveBeenCalledWith('pvad-spreadsheet:v1:other:org');
  expect(values.get('pxa-spreadsheet')).toBe('old');
});

test('exports an old persisted state into native workbook JSON without changing the original', () => {
  const sheet = emptySheet();
  sheet.cells['0:0'] = { row: 0, col: 0, type: 'formula', formula: '=1+2', value: 3 };
  const raw = JSON.stringify({ state: { name: 'Recovered', sheets: [sheet], projectId: 'original' }, version: 0 });
  const draft = { key: 'pxa-spreadsheet', raw };
  const result = JSON.parse(legacyWorkbookJson(draft));
  expect(result.name).toBe('Recovered');
  expect(result.id).toBe('original');
  expect(result.sheets[0].cells[0]).toMatchObject({ formula: '=1+2', value: 3 });
  expect(draft.raw).toBe(raw);
});

test.each(['broken', '{"state":{"name":"Bad","sheets":[]}}', '{"state":{"name":"Bad","sheets":[null]}}'])(
  'rejects malformed recovery data without mutating it: %s', raw => {
    const draft = { key: 'pxa-spreadsheet', raw };
    expect(() => legacyWorkbookJson(draft)).toThrow();
    expect(draft.raw).toBe(raw);
  },
);
