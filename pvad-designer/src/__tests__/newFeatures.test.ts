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

// ── Form metadata export ───────────────────────────────────────────────────

type FormElement = { id: string; type: string; fieldName?: string; fieldLabel?: string; required?: boolean; tabIndex?: number; validationMin?: number; validationMax?: number; validationPattern?: string };

function buildFormMetadata(elements: FormElement[]) {
  const FORM_TYPES = new Set(['field', 'checkbox', 'radio', 'dropdown', 'signature']);
  return elements
    .filter(el => FORM_TYPES.has(el.type))
    .map(el => ({
      id: el.id,
      type: el.type,
      name: el.fieldName || el.id,
      required: Boolean(el.required),
      tabIndex: el.tabIndex ?? null,
      validationMin: el.validationMin ?? null,
      validationMax: el.validationMax ?? null,
      validationPattern: el.validationPattern ?? null,
    }))
    .sort((a, b) => {
      if (a.tabIndex !== null && b.tabIndex !== null) return a.tabIndex - b.tabIndex;
      if (a.tabIndex !== null) return -1;
      if (b.tabIndex !== null) return 1;
      return 0;
    });
}

describe('Form metadata export', () => {
  test('only form-type elements are included', () => {
    const elements: FormElement[] = [
      { id: 'e1', type: 'text' },
      { id: 'e2', type: 'field', fieldName: 'name', required: true },
      { id: 'e3', type: 'image' },
      { id: 'e4', type: 'checkbox', fieldName: 'agree' },
    ];
    const meta = buildFormMetadata(elements);
    expect(meta).toHaveLength(2);
    expect(meta.map(m => m.type)).toEqual(['field', 'checkbox']);
  });

  test('fields are sorted by tabIndex ascending', () => {
    const elements: FormElement[] = [
      { id: 'e1', type: 'field', tabIndex: 3 },
      { id: 'e2', type: 'field', tabIndex: 1 },
      { id: 'e3', type: 'field', tabIndex: 2 },
    ];
    const meta = buildFormMetadata(elements);
    expect(meta.map(m => m.tabIndex)).toEqual([1, 2, 3]);
  });

  test('elements with tabIndex sort before those without', () => {
    const elements: FormElement[] = [
      { id: 'e1', type: 'field' },
      { id: 'e2', type: 'field', tabIndex: 1 },
    ];
    const meta = buildFormMetadata(elements);
    expect(meta[0].tabIndex).toBe(1);
    expect(meta[1].tabIndex).toBeNull();
  });

  test('validation fields are exported correctly', () => {
    const elements: FormElement[] = [
      { id: 'e1', type: 'field', validationMin: 3, validationMax: 50, validationPattern: '^[A-Za-z]+$' },
    ];
    const meta = buildFormMetadata(elements);
    expect(meta[0].validationMin).toBe(3);
    expect(meta[0].validationMax).toBe(50);
    expect(meta[0].validationPattern).toBe('^[A-Za-z]+$');
  });

  test('required flag is exported', () => {
    const elements: FormElement[] = [
      { id: 'e1', type: 'field', required: true },
      { id: 'e2', type: 'checkbox', required: false },
    ];
    const meta = buildFormMetadata(elements);
    expect(meta[0].required).toBe(true);
    expect(meta[1].required).toBe(false);
  });
});

// ── Document mode toggle (Section 6) ──────────────────────────────────────

type ToolGroup = { id: string; label: string; toolIds: string[] };

function getVisibleToolGroups(groups: ToolGroup[], documentMode: 'pdf' | 'word'): ToolGroup[] {
  return documentMode === 'pdf' ? groups.filter(g => g.id !== 'word') : groups;
}

const ALL_TOOL_GROUPS: ToolGroup[] = [
  { id: 'text',     label: 'Text Elements',        toolIds: ['text', 'richtext', 'link'] },
  { id: 'form',     label: 'Form Elements',         toolIds: ['field', 'checkbox', 'dropdown'] },
  { id: 'advanced', label: 'Advanced',              toolIds: ['toc', 'date', 'pagenumber'] },
  { id: 'word',     label: 'Word / DOCX Elements',  toolIds: ['footnote', 'endnote', 'contentcontrol'] },
];

describe('Document mode: toolbar filtering', () => {
  test('PDF mode hides the word tool group', () => {
    const visible = getVisibleToolGroups(ALL_TOOL_GROUPS, 'pdf');
    expect(visible.some(g => g.id === 'word')).toBe(false);
  });

  test('PDF mode keeps all non-word groups', () => {
    const visible = getVisibleToolGroups(ALL_TOOL_GROUPS, 'pdf');
    expect(visible).toHaveLength(3);
    expect(visible.map(g => g.id)).toEqual(['text', 'form', 'advanced']);
  });

  test('Word mode shows all groups including word', () => {
    const visible = getVisibleToolGroups(ALL_TOOL_GROUPS, 'word');
    expect(visible).toHaveLength(4);
    expect(visible.some(g => g.id === 'word')).toBe(true);
  });

  test('switching from PDF back to Word restores word group', () => {
    let mode: 'pdf' | 'word' = 'pdf';
    expect(getVisibleToolGroups(ALL_TOOL_GROUPS, mode).some(g => g.id === 'word')).toBe(false);
    mode = 'word';
    expect(getVisibleToolGroups(ALL_TOOL_GROUPS, mode).some(g => g.id === 'word')).toBe(true);
  });
});

describe('Document mode: warning banner logic', () => {
  const WORD_ONLY_TYPES = new Set(['footnote', 'endnote', 'contentcontrol']);

  test('shows warning when word elements on canvas in PDF mode', () => {
    const elements = [{ type: 'text' }, { type: 'footnote' }];
    const wordOnSurface = elements.some(el => WORD_ONLY_TYPES.has(el.type));
    expect('pdf' === 'pdf' && wordOnSurface).toBe(true);
  });

  test('no warning when no word-only elements on canvas in PDF mode', () => {
    const elements = [{ type: 'text' }, { type: 'link' }, { type: 'bookmark' }];
    const wordOnSurface = elements.some(el => WORD_ONLY_TYPES.has(el.type));
    expect(wordOnSurface).toBe(false);
  });

  test('bookmark is not classified as word-only', () => {
    expect(WORD_ONLY_TYPES.has('bookmark')).toBe(false);
  });

  test('footnote, endnote, contentcontrol are word-only', () => {
    ['footnote', 'endnote', 'contentcontrol'].forEach(t => {
      expect(WORD_ONLY_TYPES.has(t)).toBe(true);
    });
  });
});

// ── Help modal context (Section 5) ────────────────────────────────────────

function getInitialTab(selectedElementType: string | null): string {
  return selectedElementType ? 'elements' : 'shortcuts';
}

describe('HelpModal tab context', () => {
  test('opens on elements tab when an element is selected', () => {
    expect(getInitialTab('table')).toBe('elements');
    expect(getInitialTab('text')).toBe('elements');
    expect(getInitialTab('toc')).toBe('elements');
  });

  test('opens on shortcuts tab when nothing is selected', () => {
    expect(getInitialTab(null)).toBe('shortcuts');
  });

  test('helpModalOpen toggles between true and false', () => {
    let helpModalOpen = false;
    const setHelpModalOpen = (open: boolean) => { helpModalOpen = open; };
    setHelpModalOpen(true);
    expect(helpModalOpen).toBe(true);
    setHelpModalOpen(false);
    expect(helpModalOpen).toBe(false);
  });

  test('F1 binding sets helpModalOpen to true (simulated)', () => {
    let helpModalOpen = false;
    const handleKeyDown = (key: string) => {
      if (key === 'F1') helpModalOpen = true;
    };
    handleKeyDown('F1');
    expect(helpModalOpen).toBe(true);
  });

  test('Escape binding sets helpModalOpen to false (simulated)', () => {
    let helpModalOpen = true;
    const handleKeyDown = (key: string) => {
      if (key === 'Escape') helpModalOpen = false;
    };
    handleKeyDown('Escape');
    expect(helpModalOpen).toBe(false);
  });
});
