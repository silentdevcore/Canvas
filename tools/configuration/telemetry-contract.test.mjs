import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { dashboardQueryRules } from './pvad-telemetry-contract.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

test('OCR writes canonical spans while the API subscribes to both source generations', async () => {
  const source = await readFile(resolve(root,
    'src/Importing/PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs'), 'utf8');
  const registration = await readFile(resolve(root,
    'PVAD.WebApi/Observability/PvadObservabilityExtensions.cs'), 'utf8');
  assert.match(source, /ActivitySourceName = "PVAD\.ImageOcr"/);
  assert.match(source, /LegacyActivitySourceName = "PXA\.ImageOcr"/);
  assert.match(source, /"pvad\.ocr\.worker\.execute"/);
  assert.doesNotMatch(source, /"pxa\.ocr\.worker\.execute"|\$"pxa-image-ocr-/);
  assert.match(registration, /AddSource\(ProcessIsolatedTesseractOcrEngine\.ActivitySourceName\)/);
  assert.match(registration, /AddSource\(ProcessIsolatedTesseractOcrEngine\.LegacyActivitySourceName\)/);
  const worker = await readFile(resolve(root,
    'src/Importing/PVAD.FileImporter.ImageOcr.Worker/Program.cs'), 'utf8');
  assert.match(worker, /new Activity\("pvad\.ocr\.worker\.process"\)/);
  assert.doesNotMatch(worker, /pxa\.ocr\.worker\.process/);
});

test('all managed dashboard metric queries support both metric generations', async () => {
  const document = await dashboardQueryRules();
  const rules = document.groups[0].rules;
  assert.ok(rules.length >= 40);
  assert.equal(new Set(rules.map(rule => rule.record)).size, rules.length);
  for (const rule of rules)
    assert.doesNotMatch(rule.expr, /\b(?:pxa|pvad)_[a-z][a-z0-9_]*/,
      'Use a dual-name selector instead of hiding old or new measurements.');
  assert.ok(rules.some(rule => rule.expr.includes('__name__=~"(pvad|pxa)_')));
});

test('dashboard and datasource identities stay stable while visible titles and log filters migrate', async () => {
  const directory = resolve(root, 'deploy/observability/grafana/dashboards');
  let logQueries = 0;
  for (const file of await readdir(directory)) {
    if (!file.endsWith('.json')) continue;
    const dashboard = JSON.parse(await readFile(resolve(directory, file), 'utf8'));
    assert.match(dashboard.uid, /^pxa-/);
    assert.match(dashboard.title, /^PVADE/);
    for (const panel of dashboard.panels ?? []) {
      if (panel.datasource?.type !== 'loki') continue;
      for (const target of panel.targets ?? []) {
        if (target.expr?.includes('service_name')) {
          assert.ok(target.expr.includes('service_name=~"(pvad|pxa)-webapi"'));
          logQueries++;
        }
      }
    }
  }
  assert.ok(logQueries >= 2);
});
