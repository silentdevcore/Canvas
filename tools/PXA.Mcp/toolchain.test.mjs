import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readJson = (path) => JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8'));
const manifest = readJson('./package.json');
const lock = readJson('./package-lock.json');
const nodeMajor = 22;

test('Node declarations target the validated Node 22 runtime, not a newer API surface', () => {
  assert.ok(Number(process.versions.node.split('.')[0]) >= nodeMajor,
    'Run the MCP validation suite on Node 22 or newer.');
  assert.ok(manifest.devDependencies['@types/node'].startsWith(`^${nodeMajor}.`));
  assert.equal(Number(lock.packages['node_modules/@types/node'].version.split('.')[0]), nodeMajor);
});

test('declared, locked and installed MCP tooling agree', () => {
  assert.deepEqual(lock.packages[''].devDependencies, manifest.devDependencies);
  for (const name of ['@types/node', 'tsx', 'typescript']) {
    const installed = readJson(`./node_modules/${name}/package.json`);
    assert.equal(installed.version, lock.packages[`node_modules/${name}`].version, name);
  }
});

test('locked packages retain registry origins and integrity metadata', () => {
  for (const [name, entry] of Object.entries(lock.packages)) {
    if (!name || entry.link) continue;
    const resolved = new URL(entry.resolved);
    assert.equal(resolved.origin, 'https://registry.npmjs.org', name);
    assert.equal(resolved.username, '', name);
    assert.equal(resolved.password, '', name);
    assert.match(entry.integrity, /^(sha256|sha384|sha512)-/, name);
  }
});
