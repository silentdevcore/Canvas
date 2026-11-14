import cases from '../../../test-assets/visual-parity/date-format-cases.json';
import historicalCases from '../../../test-assets/visual-parity/date-historical-cases.json';
import { formatDocumentDate } from '../components/Rendering/documentDate';
import { createDocumentContentResolver } from '../components/Rendering/documentContent';
import type { SimpleElement } from '../types';
import contract from '../../../product-metadata/designer-date-formatting.json';

const vectors = [cases, historicalCases].flatMap(group => group.cases.map(example => ({
  ...example, instant: 'instant' in example ? example.instant! : group.instant,
})));

test.each(vectors)('$id follows the shared date contract', example => {
  const element = { id: 'date', type: 'date', x: 0, y: 0, width: 300, height: 30, ...example.input } as SimpleElement;
  const original = structuredClone(element);
  expect(formatDocumentDate(element, new Date(example.instant)))
    .toEqual({ text: example.expected, warnings: example.warnings });
  expect(element).toEqual(original);
});

test('binding uses the same custom or localized property lookup without mutating content', () => {
  const resolve = createDocumentContentResolver([], 'en', 'en', [{ name: 'Invoice.Date', value: '2026-11-13T12:00:00Z' }]);
  const element = { id: 'date', type: 'date', x: 0, y: 0, width: 300, height: 30,
    dateMode: 'binding', binding: 'invoice.date', dateFormat: 'dd.MM.yyyy HH:mm', timezone: 'Europe/Berlin' } as SimpleElement;
  expect(formatDocumentDate(element, new Date(cases.instant), resolve)).toEqual({ text: '13.11.2026 13:00', warnings: [] });
  expect(element.content).toBeUndefined();
});

test('date warnings are available in every supported Designer language', () => {
  for (const language of Object.keys(contract.locales)) {
    const { warnings } = require(`../locales/${language}/editor.json`);
    for (const key of ['dateLocaleFallback', 'dateTimezoneFallback', 'dateHistoricalTimezoneFallback', 'dateFormatFallback', 'dateBindingUnresolved', 'dateLayoutOverflow'])
      expect(typeof warnings[key]).toBe('string');
  }
});

test('an explicitly empty bound property does not resurrect stale ISO content', () => {
  const resolve = createDocumentContentResolver([], 'en', 'en', [{ name: 'Invoice.Date', value: '' }]);
  const element = { type: 'date', dateMode: 'binding', binding: 'Invoice.Date', content: '2025-01-01', fallbackText: 'No date' } as SimpleElement;
  expect(formatDocumentDate(element, new Date(cases.instant), resolve)).toEqual({ text: 'No date', warnings: ['dateBindingUnresolved'] });
});

test('the host timezone does not replace the UTC document default', () => {
  const source = { id: 'date', type: 'date', dateMode: 'render', dateFormat: 'yyyy-MM-dd HH:mm zzz' } as SimpleElement;
  const original = process.env.TZ;
  try {
    process.env.TZ = 'America/Los_Angeles';
    expect(formatDocumentDate(source, new Date(cases.instant)).text).toBe('2026-11-13 12:04 +00:00');
  } finally {
    if (original === undefined) delete process.env.TZ;
    else process.env.TZ = original;
  }
});
