import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import test from 'node:test';
import { applyReviewedJsonTransitions, hash, validateJsonNaming } from './json-naming-audit.mjs';

const root = resolve(import.meta.dirname, '../..');
const read = path => readFileSync(resolve(root, path), 'utf8');
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

test('dashboard display titles and tags are canonical while physical links remain stable', () => {
  for (const name of ['browser-health', 'infrastructure', 'platform-overview', 'document-operations', 'operations']) {
    const dashboard = JSON.parse(read(`deploy/observability/grafana/dashboards/pvad-${name}.json`));
    assert.match(dashboard.title, /^PVADE /);
    assert.ok(dashboard.tags.includes('PVAD'));
    assert.ok(!dashboard.tags.some(tag => /pxa/i.test(tag)));
    assert.equal(dashboard.uid, `pxa-${name}`);
    for (const panel of dashboard.panels)
      if (panel.datasource?.uid) assert.match(panel.datasource.uid, /^pxa-(?:prometheus|loki)$/);
  }
  const provider = load(read('deploy/observability/grafana/provisioning/dashboards/dashboards.yml')).providers[0];
  assert.equal(provider.name, 'PXA');
  assert.equal(provider.folder, 'PXA');
  assert.equal(provider.disableDeletion, true);
  const sources = load(read('deploy/observability/grafana/provisioning/datasources/datasources.yml')).datasources;
  for (const source of sources) {
    assert.match(source.name, /^PVAD /);
    assert.match(source.uid, /^pxa-(?:prometheus|loki|tempo)$/);
  }
});

const path = 'deploy/example.json';
const transition = { path, pointer: '/tags/0', previousValueSha256: hash('pxa'), currentValue: 'PVAD',
  reason: 'Canonical display tag only; datasource and dashboard identities remain unchanged.' };
const audit = { schemaVersion: 1, baseline: 'a'.repeat(40), files: [{ path, sha256: 'a'.repeat(64), preserveBytes: false,
  records: [{ pointer: '/tags/0', kind: 'value', valueSha256: hash('pxa'), matches: [{ offset: 0, token: 'pxa' }],
    classification: 'compatibility-contract', action: 'preserve' }] }] };

test('precise JSON transitions preserve the original audit and require the exact current display value', () => {
  const before = JSON.stringify(audit);
  const updated = applyReviewedJsonTransitions(audit, [transition], () => '{"tags":["PVAD"]}');
  assert.equal(updated.files[0].records.length, 0);
  assert.equal(JSON.stringify(audit), before);
  const report = { errors: [], files: [] };
  assert.equal(validateJsonNaming(report, updated).length, 0);
  assert.equal(validateJsonNaming(report, audit).length, 1);
  for (const source of ['{"tags":["pxa"]}', '{"tags":["other"]}', '{"tags":[]}'])
    assert.throws(() => applyReviewedJsonTransitions(audit, [transition], () => source));
});

test('JSON transition review cannot replace protected evidence, unknown findings or a different former hash', () => {
  const source = () => '{"tags":["PVAD"]}';
  for (const mutate of [
    value => value.files[0].preserveBytes = true,
    value => value.protectedFiles = [{ path }],
    value => value.files[0].records[0].classification = 'historical-evidence',
    value => value.files[0].records[0].valueSha256 = '0'.repeat(64),
    value => value.files[0].records = [],
  ]) {
    const invalid = structuredClone(audit); mutate(invalid);
    assert.throws(() => applyReviewedJsonTransitions(invalid, [transition], source));
  }
  for (const update of [{ path: '../private.json' }, { pointer: '/tags/1' }, { currentValue: 'PXA' },
    { reason: '' }, { path: '.vscode/settings.json' }])
    assert.throws(() => applyReviewedJsonTransitions(audit, [{ ...transition, ...update }], source));
  assert.throws(() => applyReviewedJsonTransitions(audit, [transition, transition], source));
});

test('only the two reviewed display-tag pointers differ from the original JSON audit', () => {
  const original = JSON.parse(read('checklists/PVAD.JSON-Naming-Audit.json'));
  const review = JSON.parse(read('docs/pvad-rename-reviewed-exceptions.json'));
  assert.deepEqual(review.jsonTransitions.map(value => [value.path, value.pointer]), [
    ['deploy/observability/grafana/dashboards/pvad-browser-health.json', '/tags/0'],
    ['deploy/observability/grafana/dashboards/pvad-platform-overview.json', '/tags/0'],
  ]);
  const updated = applyReviewedJsonTransitions(original, review.jsonTransitions, read);
  assert.equal(original.files.reduce((total, file) => total + file.records.length, 0) -
    updated.files.reduce((total, file) => total + file.records.length, 0), 2);
});
