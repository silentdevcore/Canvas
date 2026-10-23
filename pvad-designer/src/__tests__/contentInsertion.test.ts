import type { Page, PageSettings, SimpleElement } from '../types';
import { DEFAULT_PAGE_SETTINGS } from '../store';
import { prepareBlockInsertion, preparePageKitInsertion } from '../utils/contentInsertion';

let sequence = 0;
Object.defineProperty(globalThis, 'crypto', {
  value: { randomUUID: () => `00000000-0000-4000-8000-${String(sequence++).padStart(12, '0')}` },
  configurable: true,
});

const element = (id: string, fieldName?: string): SimpleElement => ({
  id,
  type: fieldName ? 'field' : 'text',
  x: 0,
  y: 0,
  width: 100,
  height: 20,
  fieldName,
  binding: id,
});

describe('Content insertion', () => {
  it('remaps IDs, preserves references, and namespaces only conflicting fields', () => {
    const existing: Page[] = [{ id: 'page-existing', elements: [element('existing', 'customer')] }];
    const source = [element('source', 'customer')];
    source[0].repeat = { dataPath: 'items', templateId: 'source' };

    const [inserted] = prepareBlockInsertion(source, existing, DEFAULT_PAGE_SETTINGS, { x: 40, y: 60 });

    expect(inserted.id).not.toBe('source');
    expect(inserted.repeat?.templateId).toBe(inserted.id);
    expect(inserted.fieldName).toBe('customer_2');
    expect(inserted.x).toBe(40);
    expect(inserted.y).toBe(60);
  });

  it('fits page kits proportionally and centers them in the target page', () => {
    const source: Page[] = [{ id: 'source-page', elements: [{ ...element('title'), x: 100, y: 50, width: 200, height: 40 }] }];
    const target = { ...DEFAULT_PAGE_SETTINGS, width: 595, height: 842 } as PageSettings;

    const result = preparePageKitInsertion(source, [], { width: 1280, height: 720 }, target);

    expect(result.scaled).toBe(true);
    expect(result.pages).toHaveLength(1);
    expect(result.pages[0].id).not.toBe('source-page');
    expect(result.pages[0].elements[0].width).toBeCloseTo(92.96875);
    expect(result.pages[0].elements[0].y).toBeGreaterThan(250);
  });

  it('rejects empty blocks and page kits before mutation', () => {
    expect(() => prepareBlockInsertion([], [], DEFAULT_PAGE_SETTINGS)).toThrow('empty');
    expect(() => preparePageKitInsertion([{ id: 'empty', elements: [] }], [], { width: 595, height: 842 }, DEFAULT_PAGE_SETTINGS)).toThrow('no insertable');
  });
});
