import fs from 'node:fs';
import path from 'node:path';
import { ELEMENT_CATALOG } from '../docs/elementCatalog';

type Fixture = { pages: Array<{ elements: Array<{ type: string }> }> };

describe('visual parity coverage contract', () => {
  const root = path.resolve(__dirname, '../../..');
  const fixture = JSON.parse(fs.readFileSync(
    path.join(root, 'test-assets/visual-parity/all-elements.json'), 'utf8')) as Fixture;
  const fixtureTypes = new Set(fixture.pages.flatMap(page => page.elements.map(element => element.type)));
  const catalogTypes = new Set(ELEMENT_CATALOG.map(element => element.type));
  const canvasSource = fs.readFileSync(
    path.join(root, 'pxa-designer/src/components/Editor/SimplePxaSurface.tsx'), 'utf8');
  const previewSource = fs.readFileSync(
    path.join(root, 'pxa-designer/src/components/Preview/LivePreview.tsx'), 'utf8');
  const backendSource = fs.readFileSync(
    path.join(root, 'PXA.WebApi/Infrastructure/DesignJsonMapper.cs'), 'utf8');

  test('fixture and Designer catalog contain the same element types', () => {
    expect([...fixtureTypes].sort()).toEqual([...catalogTypes].sort());
  });

  test.each([...catalogTypes].sort())('%s is represented by Canvas, Preview, and PDF renderers', type => {
    const frontendToken = new RegExp(`element\\.type\\s*===\\s*['\"]${type}['\"]`);
    expect({
      canvas: frontendToken.test(canvasSource),
      preview: frontendToken.test(previewSource),
      backend: backendSource.includes(`case "${type}":`),
    }).toEqual({ canvas: true, preview: true, backend: true });
  });
});
