import fs from 'node:fs';
import path from 'node:path';
import { ELEMENT_CATALOG } from '../docs/elementCatalog';
import { DOCUMENT_RENDERED_ELEMENT_TYPES } from '../components/Rendering/DocumentElementRenderer';
import { DOCUMENT_LOCALIZED_FIELDS } from '../components/Rendering/documentContent';

type Fixture = { pages: Array<{ elements: Array<{ type: string }> }> };

describe('visual parity coverage contract', () => {
  const root = path.resolve(__dirname, '../../..');
  const fixture = JSON.parse(fs.readFileSync(
    path.join(root, 'test-assets/visual-parity/all-elements.json'), 'utf8')) as Fixture;
  const fixtureTypes = new Set(fixture.pages.flatMap(page => page.elements.map(element => element.type)));
  const catalogTypes = new Set(ELEMENT_CATALOG.map(element => element.type));
  const canvasSource = fs.readFileSync(
    path.join(root, 'pvad-designer/src/components/Editor/SimplePvadSurface.tsx'), 'utf8');
  const previewSource = fs.readFileSync(
    path.join(root, 'pvad-designer/src/components/Preview/LivePreview.tsx'), 'utf8');
  const backendSource = fs.readFileSync(
    path.join(root, 'PVAD.WebApi/Infrastructure/DesignJsonMapper.cs'), 'utf8');

  test('fixture and Designer catalog contain the same element types', () => {
    expect([...fixtureTypes].sort()).toEqual([...catalogTypes].sort());
    expect([...DOCUMENT_RENDERED_ELEMENT_TYPES].sort()).toEqual([...catalogTypes].sort());
  });

  test('Canvas and Preview delegate printable content to the exhaustive shared renderer', () => {
    for (const source of [canvasSource, previewSource]) {
      expect(source).toContain('<DocumentElementRenderer');
      expect(source.includes('<PdfTextBlockRenderer')).toBe(false);
      expect(source.includes('<PdfTableRenderer')).toBe(false);
      expect(source.includes('const tdStyle')).toBe(false);
      expect(source.includes('const tdSt')).toBe(false);
      expect(source.includes('via.placeholder.com')).toBe(false);
    }
  });

  test('localized display fields remain synchronized with the backend substitution contract', () => {
    const backendFields = [...backendSource.matchAll(/el\.(\w+)\s*=\s*Substitute\(/g)]
      .map(match => match[1][0].toLowerCase() + match[1].slice(1));
    expect([...DOCUMENT_LOCALIZED_FIELDS].sort()).toEqual(backendFields.sort());
  });

  test.each([...catalogTypes].sort())('%s is represented by Canvas, Preview, and PDF renderers', type => {
    expect({
      canvas: canvasSource.includes('<DocumentElementRenderer') && DOCUMENT_RENDERED_ELEMENT_TYPES.includes(type),
      preview: previewSource.includes('<DocumentElementRenderer') && DOCUMENT_RENDERED_ELEMENT_TYPES.includes(type),
      backend: backendSource.includes(`case "${type}":`),
    }).toEqual({ canvas: true, preview: true, backend: true });
  });
});
