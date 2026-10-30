import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { auditPath, extractJsonNames, hash, scanJsonNames, trackedJsonPaths, validateAuditPin, validateJsonNaming, validateProtectedFiles } from './json-naming-audit.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const audit = JSON.parse(read(auditPath));
const baseline = path => execFileSync('git', ['show', `${audit.baseline}:${path}`],
  { cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });

test('the JSON inventory is complete and all exact baseline locations agree with Git', () => {
  assert.equal(audit.baseline, '0137a324d04d269a0b5d6a5b8bb2cf2e11686d5b');
  assert.equal(audit.filesScanned, 478);
  assert.equal(audit.technicalMatchingFiles, 82);
  assert.equal(audit.files.length, 83);
  const categories = new Set();
  let technical = 0, market = 0, renamed = 0, retained = 0;
  for (const file of audit.files) {
    const source = baseline(file.path);
    assert.equal(hash(source), file.sha256, file.path);
    const expected = extractJsonNames(source, file.path);
    assert.deepEqual(file.records.map(({ pointer, kind, valueSha256, matches }) => ({ pointer, kind, valueSha256, matches })), expected, file.path);
    for (const finding of file.records) {
      categories.add(finding.classification);
      if (finding.action === 'rename') renamed++; else retained++;
      if (finding.kind !== 'filename')
        for (const match of finding.matches) if (/pxa/i.test(match.token)) technical++; else market++;
    }
  }
  assert.equal(technical, 6169);
  assert.equal(market, 40);
  assert.equal(renamed, audit.disposition.renamedFindings);
  assert.equal(retained, audit.disposition.preservedFindings);
  assert.deepEqual([...categories].sort(), ['compatibility-contract', 'historical-evidence', 'incidental-integrity', 'rename']);
});

test('all current JSON findings are exact retained contracts or immutable evidence', () => {
  validateAuditPin(root, Buffer.from(read(auditPath)));
  const paths = [...new Set([...trackedJsonPaths(root), auditPath, 'product-metadata/release-fragments/pvad-json-naming-cleanup.json'])];
  const report = scanJsonNames(root, paths);
  assert.deepEqual(report.errors, []);
  assert.deepEqual(validateJsonNaming(report, audit), []);
  assert.deepEqual(validateProtectedFiles(root, audit), []);
  assert.ok(read('.github/workflows/ci.yml').includes('json-naming-audit.mjs --check'));
});

test('all six languages keep exact diagnostic keys and avoid old product copy in values', () => {
  let retainedKeys = 0;
  for (const language of ['en', 'de', 'fr', 'es', 'it', 'ar']) {
    for (const section of ['common', 'editor']) {
      const path = `pvad-designer/src/locales/${language}/${section}.json`;
      const current = extractJsonNames(read(path), path), original = extractJsonNames(baseline(path), path);
      assert.deepEqual(current, original);
      assert.ok(current.every(record => record.kind === 'key'));
      retainedKeys += current.length;
    }
    const jsonPaths = trackedJsonPaths(root).filter(path => path.startsWith(`pvad-designer/src/locales/${language}/`));
    for (const path of jsonPaths)
      assert.ok(extractJsonNames(read(path), path).every(record => record.kind === 'key'), path);
  }
  assert.equal(retainedKeys, 234);
});

test('OpenAPI changes only descriptions of the two intended API families', () => {
  const path = 'docs/schema/openapi.json';
  const original = JSON.parse(baseline(path)), current = JSON.parse(read(path));
  let changed = 0;
  for (const [route, item] of Object.entries(current.paths)) {
    for (const [method, operation] of Object.entries(item)) {
      if (!operation || typeof operation !== 'object') continue;
      const previous = original.paths?.[route]?.[method];
      if (operation.description === previous?.description) continue;
      assert.match(route, /^\/api\/pvad\/v1\/(?:designer\/projects|pdf\/e-invoices)\//);
      assert.equal(method, 'post');
      assert.doesNotMatch(operation.description, /pxa/i);
      operation.description = previous.description;
      changed++;
    }
  }
  assert.equal(changed, 13);
  assert.deepEqual(current, original);
});

test('updated examples preserve geometry and policy values and do not approve historical reviews', () => {
  const paths = audit.files.filter(file => file.records.some(record => record.action === 'rename')
    && /^(?:test-assets\/|checklists\/word-fidelity-samples\/|deploy\/observability\/performance-request)/.test(file.path));
  function withoutText(value) {
    if (typeof value === 'string') return '<text>';
    if (Array.isArray(value)) return value.map(withoutText);
    return value && typeof value === 'object' ? Object.fromEntries(Object.entries(value).map(([key, child]) => [key, withoutText(child)])) : value;
  }
  for (const { path } of paths)
    assert.deepEqual(withoutText(JSON.parse(read(path))), withoutText(JSON.parse(baseline(path))), path);
  for (const path of ['product-metadata/document-fidelity/reviewed-baseline.json',
    'product-metadata/document-fidelity/real-world-review-decisions.json', 'product-metadata/pvad-releases.json',
    'product-metadata/legal-documents/en/manifest.json', 'docs/reference/compatibility-identifiers.v1.json',
    'operator-docs/compatibility-identifiers.v1.json'])
    assert.equal(read(path), baseline(path), path);
});
