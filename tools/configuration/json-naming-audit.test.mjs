import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { auditPath, extractJsonNames, hash, scanJsonNames, validateAuditPin, validateJsonNaming, validateProtectedFiles } from './json-naming-audit.mjs';

test('JSON scanning includes keys, values, comments, escaped text, mixed case and escaped pointers', () => {
  const records = extractJsonNames('{"x/PXA~a":"\\u0050XA", "nested":["pXa","Power-Dox-Automation"] /* PXA */}', 'PXA.JSON');
  assert.equal(records.length, 6);
  assert.deepEqual(records.map(value => value.kind), ['key', 'value', 'value', 'value', 'comment', 'filename']);
  assert.equal(records[0].pointer, '/x~1PXA~0a');
  assert.equal(records[1].valueSha256, hash('PXA'));
  assert.throws(() => extractJsonNames('{"a":"x","a":"y"}'), /Duplicate/);
  assert.throws(() => extractJsonNames('{"a":'), /Invalid JSON syntax/);
  assert.throws(() => extractJsonNames('{"brand":PXA}'), /Unsupported JSON value/);
  assert.equal(extractJsonNames('{"brand":"Power  Dox Automation"}')[0].matches[0].token, 'Power  Dox Automation');
});

test('only tracked regular JSON is read, including JSONC but excluding private files and links', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-json-guard-'));
  try {
    execFileSync('git', ['init', '--quiet'], { cwd: root });
    mkdirSync(join(root, '.vscode'));
    writeFileSync(join(root, '.vscode/private.json'), '{"brand":"PXA"}');
    writeFileSync(join(root, 'data.JSON'), '{"brand":"PXA"}');
    writeFileSync(join(root, 'settings.jsonc'), '{/* PXA */"enabled":true,}');
    writeFileSync(join(root, 'local.json'), '{"brand":"PXA"}');
    symlinkSync('local.json', join(root, 'linked.json'));
    execFileSync('git', ['add', '--', 'data.JSON', 'settings.jsonc', '.vscode/private.json', 'linked.json'], { cwd: root });
    const report = scanJsonNames(root);
    assert.equal(report.filesScanned, 2);
    assert.deepEqual(report.files.map(file => file.path), ['data.JSON', 'settings.jsonc']);
    assert.deepEqual(report.errors, []);
    assert.equal(scanJsonNames(root, ['../private.json', '.vscode/private.json']).filesScanned, 0);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

function synthetic() {
  const source = '{"code":"PXAAPI001"}';
  const records = extractJsonNames(source).map(record => ({ ...record, classification: 'compatibility-contract', action: 'preserve' }));
  const file = { path: 'contract.json', sha256: hash(source), records };
  return { source, report: { files: [structuredClone(file)], errors: [] }, audit: { schemaVersion: 1, baseline: 'a'.repeat(40), files: [file] } };
}

test('exceptions are exact and reject new locations, changed values, stale entries and unfinished renames', () => {
  const { report, audit } = synthetic();
  assert.deepEqual(validateJsonNaming(report, audit), []);
  const changed = structuredClone(report);
  changed.files[0].records[0].valueSha256 = hash('PXAAPI002');
  assert.equal(validateJsonNaming(changed, audit)[0].reason, 'unreviewed-finding');
  const moved = structuredClone(report);
  moved.files[0].records[0].pointer = '/label';
  assert.equal(validateJsonNaming(moved, audit)[0].reason, 'unreviewed-finding');
  assert.equal(validateJsonNaming({ files: [], errors: [] }, audit)[0].reason, 'stale-retained-finding');
  const rename = structuredClone(audit);
  rename.files[0].records[0].classification = 'rename';
  rename.files[0].records[0].action = 'rename';
  assert.deepEqual(validateJsonNaming({ files: [], errors: [] }, rename), []);
  assert.equal(validateJsonNaming(report, rename)[0].reason, 'unreviewed-finding');
  const extra = structuredClone(report);
  extra.files.push({ ...extra.files[0], path: 'new.json' });
  assert.equal(validateJsonNaming(extra, audit)[0].reason, 'unclassified-file');
});

test('immutable evidence is protected even when an edit removes every old name', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-json-evidence-'));
  try {
    const { source } = synthetic();
    writeFileSync(join(root, 'contract.json'), source);
    const audit = { protectedFiles: [{ path: 'contract.json', sha256: hash(source) }] };
    assert.deepEqual(validateProtectedFiles(root, audit), []);
    writeFileSync(join(root, 'contract.json'), '{"code":"PVAD"}');
    assert.equal(validateProtectedFiles(root, audit)[0].reason, 'changed-protected-evidence');
    assert.throws(() => validateProtectedFiles(root, { protectedFiles: [{ path: '../outside', sha256: hash(source) }] }), /Invalid protected/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('the sole audit self-exclusion needs an explicit reviewed evidence hash', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-json-audit-pin-'));
  try {
    mkdirSync(join(root, 'docs'));
    const bytes = Buffer.from('{"source":"PXA"}');
    const file = join(root, 'docs/pvad-rename-reviewed-exceptions.json');
    writeFileSync(file, JSON.stringify({ files: [{ path: auditPath, sha256: hash(bytes), reviewStatus: 'reviewed', reasons: ['retained-evidence'] }] }));
    assert.doesNotThrow(() => validateAuditPin(root, bytes));
    assert.throws(() => validateAuditPin(root, Buffer.from('changed')), /requires an exact reviewed/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});
