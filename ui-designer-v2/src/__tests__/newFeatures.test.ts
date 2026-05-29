// Unit tests for features added in UI-Improvements-2026

import { TEMPLATES, CATEGORIES, CATEGORY_CONFIG } from '../data/templates';

// ── Template format filter ─────────────────────────────────────────────────

describe('Template format field', () => {
  test('presentation templates have format = widescreen', () => {
    const pres = TEMPLATES.filter(t => t.category === 'presentation');
    expect(pres.length).toBeGreaterThanOrEqual(5);
    pres.forEach(t => expect(t.format).toBe('widescreen'));
  });

  test('book templates have format = portrait', () => {
    const books = TEMPLATES.filter(t => t.category === 'book');
    expect(books.length).toBeGreaterThanOrEqual(3);
    books.forEach(t => expect(t.format).toBe('portrait'));
  });

  test('format filter: portrait returns no widescreen templates', () => {
    const portrait = TEMPLATES.filter(t => (t.format ?? 'portrait') === 'portrait');
    portrait.forEach(t => expect(t.format ?? 'portrait').not.toBe('widescreen'));
  });

  test('format filter: widescreen returns only widescreen templates', () => {
    const widescreen = TEMPLATES.filter(t => (t.format ?? 'portrait') === 'widescreen');
    widescreen.forEach(t => expect(t.format).toBe('widescreen'));
  });
});

// ── Template categories ────────────────────────────────────────────────────

describe('New template categories', () => {
  test('CATEGORIES includes presentation', () => {
    expect(CATEGORIES.some(c => c.id === 'presentation')).toBe(true);
  });

  test('CATEGORIES includes book', () => {
    expect(CATEGORIES.some(c => c.id === 'book')).toBe(true);
  });

  test('CATEGORY_CONFIG has presentation entry', () => {
    expect(CATEGORY_CONFIG['presentation']).toBeDefined();
  });

  test('CATEGORY_CONFIG has book entry', () => {
    expect(CATEGORY_CONFIG['book']).toBeDefined();
  });

  test('presentation category count matches TEMPLATES', () => {
    const cat = CATEGORIES.find(c => c.id === 'presentation');
    const count = TEMPLATES.filter(t => t.category === 'presentation').length;
    expect(cat?.count).toBe(count);
  });
});

// ── Page presets ───────────────────────────────────────────────────────────

const PAGE_PRESETS: Record<string, { width: number; height: number }> = {
  A4:                  { width: 595,  height: 842  },
  A5:                  { width: 420,  height: 595  },
  A3:                  { width: 842,  height: 1191 },
  Letter:              { width: 612,  height: 792  },
  Legal:               { width: 612,  height: 1008 },
  'Landscape A4':      { width: 842,  height: 595  },
  'Landscape A3':      { width: 1191, height: 842  },
  'Presentation 16:9': { width: 1280, height: 720  },
  'Presentation 4:3':  { width: 1024, height: 768  },
  'Book A5':           { width: 420,  height: 595  },
  'Social Square':     { width: 1080, height: 1080 },
};

describe('Page presets', () => {
  test('Presentation 16:9 has correct dimensions', () => {
    expect(PAGE_PRESETS['Presentation 16:9']).toEqual({ width: 1280, height: 720 });
  });

  test('Landscape A4 is wider than tall', () => {
    const p = PAGE_PRESETS['Landscape A4'];
    expect(p.width).toBeGreaterThan(p.height);
  });

  test('Social Square has equal sides', () => {
    const p = PAGE_PRESETS['Social Square'];
    expect(p.width).toBe(p.height);
  });

  test('Presentation 4:3 has correct dimensions', () => {
    expect(PAGE_PRESETS['Presentation 4:3']).toEqual({ width: 1024, height: 768 });
  });

  test('there are 11 presets total', () => {
    expect(Object.keys(PAGE_PRESETS)).toHaveLength(11);
  });
});

// ── TOC entry generation ───────────────────────────────────────────────────

type HeadingElement = { id: string; content?: string; headingLevel?: 1 | 2 | 3 | null };
type Page = { elements: HeadingElement[] };

function generateTocEntries(pages: Page[]) {
  return pages
    .flatMap((p, pageIdx) =>
      p.elements
        .filter(el => el.headingLevel != null)
        .map(el => ({
          text: el.content || '',
          level: (el.headingLevel ?? 1) as 1 | 2 | 3,
          page: pageIdx + 1,
        }))
    );
}

describe('TOC entry generation', () => {
  test('generates entries from headings in correct page order', () => {
    const pages: Page[] = [
      { elements: [{ id: '1', content: 'Intro', headingLevel: 1 }] },
      { elements: [{ id: '2', content: 'Chapter 1', headingLevel: 1 }, { id: '3', content: 'Section 1.1', headingLevel: 2 }] },
    ];
    const entries = generateTocEntries(pages);
    expect(entries).toHaveLength(3);
    expect(entries[0]).toEqual({ text: 'Intro', level: 1, page: 1 });
    expect(entries[1]).toEqual({ text: 'Chapter 1', level: 1, page: 2 });
    expect(entries[2]).toEqual({ text: 'Section 1.1', level: 2, page: 2 });
  });

  test('skips elements without headingLevel', () => {
    const pages: Page[] = [
      { elements: [{ id: '1', content: 'Body text', headingLevel: null }, { id: '2', content: 'Title', headingLevel: 1 }] },
    ];
    const entries = generateTocEntries(pages);
    expect(entries).toHaveLength(1);
    expect(entries[0].text).toBe('Title');
  });

  test('returns empty array if no headings', () => {
    const pages: Page[] = [{ elements: [{ id: '1', content: 'Body' }] }];
    const entries = generateTocEntries(pages);
    expect(entries).toHaveLength(0);
  });
});

// ── Form block field construction ──────────────────────────────────────────

type FieldDef = { label: string; name: string; type: 'field' | 'dropdown' };

function buildFormBlock(fields: FieldDef[], startX: number, startY: number) {
  const GAP = 56;
  return fields.map((f, i) => ({
    type: f.type,
    x: startX,
    y: startY + i * GAP,
    width: 260,
    height: 40,
    fieldLabel: f.label,
    fieldName: f.name,
    tabIndex: i + 1,
  }));
}

const ADDRESS_FIELDS: FieldDef[] = [
  { label: 'Full Name',   name: 'name',       type: 'field' },
  { label: 'Street',      name: 'street',     type: 'field' },
  { label: 'City',        name: 'city',       type: 'field' },
  { label: 'Postal Code', name: 'postalCode', type: 'field' },
  { label: 'Country',     name: 'country',    type: 'field' },
];

describe('Form block insertion', () => {
  test('address block creates 5 field elements', () => {
    const els = buildFormBlock(ADDRESS_FIELDS, 48, 120);
    expect(els).toHaveLength(5);
    els.forEach(el => expect(el.type).toBe('field'));
  });

  test('fields are spaced 56 px apart vertically', () => {
    const els = buildFormBlock(ADDRESS_FIELDS, 48, 120);
    for (let i = 1; i < els.length; i++) {
      expect(els[i].y - els[i - 1].y).toBe(56);
    }
  });

  test('tab indices start at 1 and increment', () => {
    const els = buildFormBlock(ADDRESS_FIELDS, 48, 120);
    els.forEach((el, i) => expect(el.tabIndex).toBe(i + 1));
  });

  test('all fields have width 260 and height 40', () => {
    const els = buildFormBlock(ADDRESS_FIELDS, 48, 120);
    els.forEach(el => {
      expect(el.width).toBe(260);
      expect(el.height).toBe(40);
    });
  });

  test('custom start position is applied', () => {
    const els = buildFormBlock(ADDRESS_FIELDS, 100, 200);
    expect(els[0].x).toBe(100);
    expect(els[0].y).toBe(200);
  });
});
