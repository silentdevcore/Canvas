import { ELEMENT_TYPES } from '../types';
import { ELEMENT_CATALOG, CATEGORY_ORDER, getElementDoc, elementsByCategory } from '../docs/elementCatalog';

// Drift guard: the documentation element catalog must stay in lock-step with the ElementType union.
// Adding a new element type (in types.ts) forces a catalog entry, and vice-versa.
describe('elementCatalog — drift guard against ElementType', () => {
  test('every ElementType has exactly one catalog entry', () => {
    const catalogTypes = ELEMENT_CATALOG.map((e) => e.type).sort();
    const missing = ELEMENT_TYPES.filter((t) => !catalogTypes.includes(t));
    expect(missing).toEqual([]);
  });

  test('every catalog entry is a valid ElementType (no orphans, no duplicates)', () => {
    const seen = new Set<string>();
    for (const entry of ELEMENT_CATALOG) {
      expect(ELEMENT_TYPES).toContain(entry.type);
      expect(seen.has(entry.type)).toBe(false); // no duplicate entries
      seen.add(entry.type);
    }
    expect(seen.size).toBe(ELEMENT_TYPES.length);
  });

  test('every entry is well-formed (category, description, format support)', () => {
    for (const e of ELEMENT_CATALOG) {
      expect(CATEGORY_ORDER).toContain(e.category);
      expect(e.label.length).toBeGreaterThan(0);
      expect(e.description.length).toBeGreaterThan(0);
      expect(typeof e.formatSupport.pdf).toBe('boolean');
      expect(e.example.type).toBe(e.type); // the example actually describes this element
    }
  });

  test('lookup + grouping helpers work', () => {
    expect(getElementDoc('text')?.label).toBe('Text Block');
    expect(getElementDoc('not-a-type' as any)).toBeUndefined();
    const grouped = elementsByCategory();
    const total = grouped.reduce((n, g) => n + g.elements.length, 0);
    expect(total).toBe(ELEMENT_CATALOG.length);
  });
});
