import { isOptionListForm, optionListMarker, PdfOptionListRenderer } from '@/components/Rendering/PdfOptionListRenderer';
import type { SimpleElement } from '@/types';

const element = (extra: Partial<SimpleElement> = {}): SimpleElement => ({
  id: 'choices', type: 'optionlist', x: 0, y: 0, width: 200, height: 80,
  options: ['Alpha', 'Beta'], ...extra,
});

describe('shared PDF option-list contract', () => {
  test('plain and explicitly styled lists remain static', () => {
    expect(isOptionListForm(element())).toBe(false);
    expect(isOptionListForm(element({ ordered: true, listStyle: 'upper-roman' }))).toBe(false);
  });

  test.each([
    { fieldName: 'regions' }, { fieldLabel: 'Regions' }, { selectedValue: '' },
    { multiSelect: false }, { multiSelect: true }, { accessibility: { role: 'Form' as const } },
  ])('explicit form intent creates a choice widget: %j', extra => {
    expect(isOptionListForm(element(extra))).toBe(true);
  });

  test.each([
    ['decimal', 4, '4. '], ['lower-alpha', 27, 'aa. '], ['upper-alpha', 28, 'AB. '],
    ['lower-roman', 14, 'xiv. '], ['upper-roman', 49, 'XLIX. '],
    ['none', 1, ''], ['disc', 1, '\u2022 '], ['square', 1, '\u25a0 '],
    ['circle', 1, 'o '], ['dash', 1, '- '], ['asterisk', 1, '* '],
  ])('preserves %s markers at %s', (style, number, expected) => {
    expect(optionListMarker(String(style), Number(number))).toBe(expected);
  });

  test('uses a stable SVG surface and clips rows beyond the element', () => {
    const result = PdfOptionListRenderer({ element: element({ height: 15 }) });
    expect(result.type).toBe('svg');
    expect(result.props.viewBox).toBe('0 0 200 15');
    expect(result.props.children[1].filter(Boolean)).toHaveLength(1);
  });
});
