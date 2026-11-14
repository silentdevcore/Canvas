import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
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

test('date fixture uses a test-only clock and keeps its CI evidence separate', () => {
  const runner = readFileSync(new URL('./run.mjs', import.meta.url), 'utf8');
  const generator = readFileSync(new URL('../../tests/PVAD.Api.Tests/VisualParityArtifactTests.cs', import.meta.url), 'utf8');
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const clock = JSON.parse(readFileSync(new URL('../../test-assets/visual-parity/date-format.clock.json', import.meta.url), 'utf8'));
  assert.ok(Number.isFinite(Date.parse(clock.instant)));
  assert.match(runner, /page\.clock\.setFixedTime/);
  assert.match(generator, /new FixedClock\(value\)/);
  assert.match(workflow, /PVAD_VISUAL_PARITY_FIXTURE: test-assets\/visual-parity\/date-format.json/);
  assert.match(workflow, /path: artifacts\/date-format-parity/);
  assert.match(workflow, /steps\.image-parity\.outcome != 'skipped'/);
});

test('date layout pack covers paragraph styles and overflow with isolated enforced CI evidence', () => {
  const fixture = JSON.parse(readFileSync(new URL('../../test-assets/visual-parity/date-layout.json', import.meta.url), 'utf8'));
  const clock = JSON.parse(readFileSync(new URL('../../test-assets/visual-parity/date-layout.clock.json', import.meta.url), 'utf8'));
  const elements = fixture.pages.flatMap(page => page.elements);
  assert.equal(fixture.pages.length, 3);
  assert.equal(elements.length, 20);
  assert.equal(new Set(elements.map(element => element.id)).size, elements.length);
  assert.ok(elements.every(element => element.type === 'date'));
  for (const id of ['narrow-en', 'narrow-fr', 'narrow-de', 'narrow-ar', 'center-asymmetric', 'right-padding',
    'uniform-frame', 'side-frame', 'zero-side-frame', 'rounded-frame', 'dashed-frame', 'bold-italic',
    'line-height-factor', 'line-height-points', 'explicit-lines', 'short-height', 'unbroken-date',
    'arabic-frame', 'right-narrow', 'empty-static'])
    assert.ok(elements.some(element => element.id === id), id);
  assert.equal(new Date(clock.instant).toISOString(), '2026-11-13T12:04:05.120Z');
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const run = workflow.indexOf('- name: Enforce date paragraph layout parity');
  const archive = workflow.indexOf('- name: Preserve date layout parity evidence');
  const catalog = workflow.indexOf('- name: Enforce approved visual parity');
  assert.ok(run > 0 && run < archive && archive < catalog);
  assert.match(workflow.slice(run, archive), /PVAD_VISUAL_PARITY_ENFORCE: true/);
  assert.match(workflow.slice(run, archive), /PVAD_VISUAL_PARITY_FIXTURE: test-assets\/visual-parity\/date-layout.json/);
  assert.match(workflow.slice(archive, catalog), /if: always\(\) && steps\.date-layout-parity\.outcome != 'skipped'/);
  assert.match(workflow, /path: artifacts\/date-layout-parity/);
});

test('the scoped Date owner approval preserves the exact reviewed crops, fixture and font', () => {
  const root = new URL('../../', import.meta.url);
  const directory = new URL('test-assets/visual-parity/approvals/2026-11-14-date-layout/', root);
  const approval = JSON.parse(readFileSync(new URL('approval.json', directory), 'utf8'));
  const hash = file => createHash('sha256').update(readFileSync(file)).digest('hex');
  assert.equal(approval.status, 'approved');
  assert.equal(approval.caseId, 'short-height');
  assert.equal(approval.approvalSource, 'Explicit user confirmation after viewing the 200% short-date comparison.');
  assert.equal(hash(new URL(approval.fixture, root)), approval.fixtureSha256);
  assert.equal(hash(new URL(approval.font, root)), approval.fontSha256);
  assert.deepEqual(approval.crops.map(crop => crop.surface).sort(), ['backend', 'canvas', 'csharp', 'preview']);
  for (const crop of approval.crops) assert.equal(hash(new URL(crop.file, directory)), crop.sha256);
  assert.match(approval.scope, /P2 metrics and regression thresholds remain unchanged/);
});
