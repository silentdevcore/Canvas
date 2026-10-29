import { ELEMENT_CATALOG } from '../docs/elementCatalog';
import { ELEMENT_GROUP_ORDER, ELEMENT_REGISTRY, ELEMENT_TYPES } from '../data/elementRegistry';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

describe('Designer element registry', () => {
  it('contains every element exactly once and every category is ordered', () => {
    expect(new Set(ELEMENT_TYPES).size).toBe(ELEMENT_TYPES.length);
    expect(ELEMENT_REGISTRY.every(entry => ELEMENT_GROUP_ORDER.includes(entry.group))).toBe(true);
  });

  it('matches the documentation element catalog', () => {
    expect([...ELEMENT_TYPES].sort()).toEqual(ELEMENT_CATALOG.map(entry => entry.type).sort());
  });

  it('matches the Designer tool factories and backend validation catalog', () => {
    const surface = readFileSync(join(process.cwd(), 'src/components/Editor/SimplePvadSurface.tsx'), 'utf8');
    const toolSection = surface.slice(surface.indexOf('const tools: Tool[]'), surface.indexOf('const toolsById'));
    const toolTypes = [...toolSection.matchAll(/\n    \{\n      id: '([^']+)'/g)].map(match => match[1]);
    const backend = readFileSync(join(process.cwd(), '../PVAD.WebApi/Application/Designer/PvadDesignerElementCatalog.cs'), 'utf8');
    const backendTypes = [...backend.slice(backend.indexOf('ElementTypes =')).matchAll(/"([a-z]+)"/g)].map(match => match[1]);

    expect(toolTypes.sort()).toEqual([...ELEMENT_TYPES].sort());
    expect(backendTypes.sort()).toEqual([...ELEMENT_TYPES].sort());
  });

  it('limits Word-only elements to Word mode', () => {
    const wordOnly = ELEMENT_REGISTRY.filter(entry => entry.group === 'wordElements');
    expect(wordOnly.length).toBeGreaterThan(0);
    expect(wordOnly.every(entry => entry.supportedModes.length === 1 && entry.supportedModes[0] === 'word')).toBe(true);
  });
});
