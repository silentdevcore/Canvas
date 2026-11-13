import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { expectedElementBounds } from './fixture-layout.mjs';

const element = { x: 20, y: 30, width: 160, height: 120,
  langOverrides: { fr: { x: 0, width: 140, height: 100 }, 'fr-CH': { y: 0, width: 130 } } };

test('expected PDF crops use canonical target-language geometry, never measured browser bounds', () => {
  assert.deepEqual(expectedElementBounds(element, { targetLanguage: 'fr', systemLanguage: 'en' }),
    { x: 0, y: 30, width: 140, height: 100 });
  assert.deepEqual(expectedElementBounds(element, { targetLanguage: 'fr-CA' }),
    { x: 0, y: 30, width: 140, height: 100 });
});
test('exact overrides win; absent coordinates and languages retain canonical defaults', () => {
  assert.deepEqual(expectedElementBounds(element, { targetLanguage: 'fr-CH' }),
    { x: 20, y: 0, width: 130, height: 120 });
  assert.deepEqual(expectedElementBounds(element, { targetLanguage: 'de' }),
    { x: 20, y: 30, width: 160, height: 120 });
  assert.deepEqual(expectedElementBounds(element, { systemLanguage: 'fr' }),
    { x: 0, y: 30, width: 140, height: 100 });
  assert.equal(element.x, 20);
  assert.equal(element.width, 160);
});

test('CI enforces the image fixture and archives it before the full catalog overwrites the working report', () => {
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const image = workflow.indexOf('- name: Enforce image fit and focus parity');
  const archive = workflow.indexOf('- name: Preserve image parity evidence before the catalog run');
  const catalog = workflow.indexOf('- name: Enforce approved visual parity');
  assert.ok(image > 0 && image < archive && archive < catalog);
  assert.match(workflow.slice(image, archive), /PVAD_VISUAL_PARITY_FIXTURE: test-assets\/visual-parity\/image-fit.json/);
  assert.match(workflow.slice(image, archive), /PVAD_VISUAL_PARITY_ENFORCE: true/);
  assert.match(workflow.slice(archive, catalog), /if: always\(\)/);
  assert.match(workflow, /path: artifacts\/image-fit-parity/);
});
