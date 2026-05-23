import { getPageSettingsWarnings } from '../utils/pageValidation';
import { DEFAULT_PAGE_SETTINGS } from '../store';
import type { PageSettings } from '../types';

const base = (): PageSettings => JSON.parse(JSON.stringify(DEFAULT_PAGE_SETTINGS));

describe('getPageSettingsWarnings — clean defaults', () => {
  test('default settings produce no warnings', () => {
    expect(getPageSettingsWarnings(base())).toHaveLength(0);
  });
});

describe('page size validation', () => {
  test('width < 100 triggers warning', () => {
    const ps = base();
    ps.width = 50;
    const warnings = getPageSettingsWarnings(ps);
    expect(warnings.some(w => w.key === 'size-min')).toBe(true);
  });

  test('height < 100 triggers warning', () => {
    const ps = base();
    ps.height = 80;
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'size-min')).toBe(true);
  });

  test('width > 5000 triggers warning', () => {
    const ps = base();
    ps.width = 6000;
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'size-max')).toBe(true);
  });

  test('valid custom size: no warning', () => {
    const ps = base();
    ps.width = 1000;
    ps.height = 1400;
    expect(getPageSettingsWarnings(ps)).toHaveLength(0);
  });
});

describe('margin validation', () => {
  test('top + bottom margins ≥ height triggers warning', () => {
    const ps = base();
    ps.margins = { top: 500, right: 0, bottom: 500, left: 0 };
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'margins-v')).toBe(true);
  });

  test('left + right margins ≥ width triggers warning', () => {
    const ps = base();
    ps.margins = { top: 0, right: 400, bottom: 0, left: 400 };
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'margins-h')).toBe(true);
  });

  test('normal margins: no warning', () => {
    const ps = base();
    ps.margins = { top: 48, right: 48, bottom: 48, left: 48 };
    expect(getPageSettingsWarnings(ps)).toHaveLength(0);
  });
});

describe('header + footer validation', () => {
  test('header + footer > 80% page height triggers warning', () => {
    const ps = base();
    ps.headerEnabled = true;
    ps.headerHeight = 400;
    ps.footerEnabled = true;
    ps.footerHeight = 400;
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'header-footer')).toBe(true);
  });

  test('only header enabled: no combined warning', () => {
    const ps = base();
    ps.headerEnabled = true;
    ps.headerHeight = 400;
    ps.footerEnabled = false;
    expect(getPageSettingsWarnings(ps).every(w => w.key !== 'header-footer')).toBe(true);
  });
});

describe('bleed validation', () => {
  test('bleed > 25% of min dimension triggers warning', () => {
    const ps = base(); // 595×842 → min 595/4 ≈ 148
    ps.bleedSize = 200;
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'bleed')).toBe(true);
  });

  test('reasonable bleed (9 px): no warning', () => {
    const ps = base();
    ps.bleedSize = 9;
    expect(getPageSettingsWarnings(ps)).toHaveLength(0);
  });
});

describe('pagination section-start validation', () => {
  test('odd-page section start triggers warning', () => {
    const ps = base();
    ps.pagination.sectionStartBehavior = 'odd-page';
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'pagination')).toBe(true);
  });

  test('even-page section start triggers warning', () => {
    const ps = base();
    ps.pagination.sectionStartBehavior = 'even-page';
    expect(getPageSettingsWarnings(ps).some(w => w.key === 'pagination')).toBe(true);
  });

  test('continue section start: no warning', () => {
    const ps = base();
    ps.pagination.sectionStartBehavior = 'continue';
    expect(getPageSettingsWarnings(ps)).toHaveLength(0);
  });

  test('new-page section start: no warning', () => {
    const ps = base();
    ps.pagination.sectionStartBehavior = 'new-page';
    expect(getPageSettingsWarnings(ps)).toHaveLength(0);
  });
});
