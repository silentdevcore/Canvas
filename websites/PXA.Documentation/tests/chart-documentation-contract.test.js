import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PXA.Documentation/src/main.js'), 'utf8');
const guide = fs.readFileSync(path.join(root, 'docs/charts.md'), 'utf8');
const schema = JSON.parse(fs.readFileSync(path.join(root, 'docs/schema/design-export.schema.json'), 'utf8'));
const openApi = JSON.parse(fs.readFileSync(path.join(root, 'docs/schema/openapi.json'), 'utf8'));

test('chart documentation uses the shared version 2 contract', () => {
  assert.match(source, /"schemaVersion": 2/);
  assert.match(source, /"type": "combo"/);
  assert.doesNotMatch(source, /Chart\.js-style shape/);
  assert.equal(schema.$defs.chartDefinition.properties.schemaVersion.const, 2);
  assert.deepEqual(schema.$defs.chartDefinition.properties.type.enum,
    ['bar', 'line', 'area', 'pie', 'doughnut', 'stackedBar', 'combo']);
  assert.equal(
    openApi.components.schemas.ElementDto.properties.chart.oneOf[1].$ref,
    '#/components/schemas/ChartDefinitionDto',
  );
  assert.deepEqual(openApi.components.schemas.ChartDefinitionDto.properties.type.enum,
    ['bar', 'line', 'area', 'pie', 'doughnut', 'stackedBar', 'combo']);
});

test('PDF recognition modes and limitations are public and explicit', () => {
  assert.match(guide, /chartRecognition=off/);
  assert.match(guide, /`safe`/);
  assert.match(guide, /`review`/);
  assert.match(guide, /best effort/i);
  assert.match(guide, /confidence `1\.0`/);
  const parameters = openApi.paths['/api/pxa/document/import-pdf-engine'].post.parameters;
  assert.deepEqual(parameters[0].schema.enum, ['off', 'safe', 'review']);
  assert.equal(parameters[0].schema.default, 'safe');
});
