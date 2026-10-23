import { toDisplay, fromDisplay, UNIT_TO_PX } from '../utils/units';

describe('UNIT_TO_PX constants', () => {
  test('px and pt are 1:1', () => {
    expect(UNIT_TO_PX.px).toBe(1);
    expect(UNIT_TO_PX.pt).toBe(1);
  });

  test('1 inch = 72 px', () => {
    expect(UNIT_TO_PX.in).toBe(72);
  });
});

describe('toDisplay', () => {
  test('px: returns same value', () => {
    expect(toDisplay(595, 'px')).toBe(595);
    expect(toDisplay(842, 'px')).toBe(842);
  });

  test('pt: same as px (1:1)', () => {
    expect(toDisplay(595, 'pt')).toBe(595);
  });

  test('mm: A4 width 595 px → ~210 mm', () => {
    expect(toDisplay(595, 'mm')).toBeCloseTo(210, 0);
  });

  test('mm: A4 height 842 px → ~297 mm', () => {
    expect(toDisplay(842, 'mm')).toBeCloseTo(297, 0);
  });

  test('cm: A4 width 595 px → ~21 cm', () => {
    expect(toDisplay(595, 'cm')).toBeCloseTo(21, 0);
  });

  test('in: 72 px → 1 inch', () => {
    expect(toDisplay(72, 'in')).toBe(1);
  });

  test('in: 144 px → 2 inch', () => {
    expect(toDisplay(144, 'in')).toBe(2);
  });

  test('unknown unit falls back to px', () => {
    expect(toDisplay(100, 'em')).toBe(100);
  });
});

describe('fromDisplay', () => {
  test('px: returns same value (rounded)', () => {
    expect(fromDisplay(595, 'px')).toBe(595);
  });

  test('mm: 210 mm → 595 px', () => {
    expect(fromDisplay(210, 'mm')).toBe(595);
  });

  test('mm: 297 mm → 842 px', () => {
    expect(fromDisplay(297, 'mm')).toBe(842);
  });

  test('cm: 21 cm → 595 px', () => {
    expect(fromDisplay(21, 'cm')).toBe(595);
  });

  test('in: 1 inch → 72 px', () => {
    expect(fromDisplay(1, 'in')).toBe(72);
  });

  test('round-trip: toDisplay → fromDisplay returns original px', () => {
    const originals = [595, 842, 420, 612, 792];
    const units = ['mm', 'cm', 'in', 'pt'];
    for (const px of originals) {
      for (const unit of units) {
        const display = toDisplay(px, unit);
        const back = fromDisplay(display, unit);
        // Allow ±1 px rounding error
        expect(Math.abs(back - px)).toBeLessThanOrEqual(1);
      }
    }
  });
});
