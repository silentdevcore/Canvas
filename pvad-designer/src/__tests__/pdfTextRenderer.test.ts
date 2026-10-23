import { formatNumberForPdf, formatPageNumberForPdf, parseRichTextRuns } from '@/components/Rendering/PdfTextRenderer';
import type { SimpleElement } from '@/types';

const element = (overrides: Partial<SimpleElement>): SimpleElement => ({
  id: 'value',
  type: 'number',
  x: 0,
  y: 0,
  width: 120,
  height: 24,
  ...overrides,
});

describe('PDF text contract', () => {
  test('uses the backend page-of-total format', () => {
    expect(formatPageNumberForPdf(element({
      type: 'pagenumber',
      numberingFormat: 'pageOfTotal',
      startNumber: 3,
      prefix: 'Page ',
    }), 1, 8)).toBe('Page 4 / 8');
  });

  test('uses the backend Swiss currency representation', () => {
    expect(formatNumberForPdf(element({
      numberValue: -12345.67,
      numberStyle: 'currency',
      numberDecimals: 2,
      numberCurrency: 'CHF',
      numberLocale: 'de-CH',
    }))).toBe('CHF-12’345.67');
  });

  test('positions rich-text runs with the PDF word-width contract', () => {
    const paragraphs = parseRichTextRuns(
      '<p><b>Rich text</b> with <i>emphasis</i><br/>and a second line.</p>', 13, '#101828');
    const runs = paragraphs.flat();

    expect(paragraphs).toHaveLength(2);
    expect(runs.find(run => run.text.includes('Rich'))?.bold).toBe(true);
    expect(runs.find(run => run.text.includes('emphasis'))?.italic).toBe(true);
    expect(runs.some(run => run.text.includes('and a second line.'))).toBe(true);
  });
});
