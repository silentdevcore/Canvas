import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = new URL('../../pxa-designer/', import.meta.url);
const require = createRequire(new URL('package.json', root));
const { satisfies, major } = require('semver');
const json = path => JSON.parse(readFileSync(new URL(path, root), 'utf8'));
const manifest = json('package.json');
const lock = json('package-lock.json');
const installed = name => json(`node_modules/${name}/package.json`);

test('Designer retains the approved stable React 18 and Grid baseline', () => {
  const react = installed('react').version;
  assert.equal(react, installed('react-dom').version);
  for (const name of ['react', 'react-dom', '@types/react', '@types/react-dom']) {
    const version = installed(name).version;
    assert.equal(major(version), 18, name);
    assert.ok(satisfies(version, manifest.dependencies[name]), name);
    assert.equal(version, lock.packages[`node_modules/${name}`].version, name);
  }
  assert.equal(installed('@glideapps/glide-data-grid').version, '6.0.3');
});

test('replacement components declare compatibility with the installed runtime and each other', () => {
  for (const name of ['@hello-pangea/dnd', '@microlink/react-json-view', '@glideapps/glide-data-grid']) {
    const entry = installed(name);
    assert.equal(entry.version, lock.packages[`node_modules/${name}`].version, name);
    for (const runtime of ['react', 'react-dom']) {
      assert.ok(satisfies(installed(runtime).version, entry.peerDependencies[runtime]), `${name}/${runtime}`);
    }
  }
  const redux = installed('redux').version;
  assert.ok(satisfies(redux, installed('redux-thunk').peerDependencies.redux));
});

test('retired components, their declarations and Flux are absent from the lockfile', () => {
  for (const name of ['react-beautiful-dnd', '@types/react-beautiful-dnd', 'react-json-view', 'flux']) {
    assert.ok(!manifest.dependencies[name] && !manifest.devDependencies[name], name);
    assert.ok(!Object.keys(lock.packages).some(path => path.endsWith(`node_modules/${name}`)), name);
  }
  assert.deepEqual(lock.packages[''].dependencies, manifest.dependencies);
  assert.deepEqual(lock.packages[''].devDependencies, manifest.devDependencies);
});

test('the full installed tree exposes only the two documented pre-existing peer debts', () => {
  let output;
  try {
    output = execFileSync('npm', ['ls', '--all', '--json'], {
      cwd: fileURLToPath(root), encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
      timeout: 30_000, maxBuffer: 8 * 1024 * 1024,
    });
  } catch (error) {
    assert.equal(error.status, 1, 'Only npm peer-tree errors may be inspected');
    output = error.stdout;
  }
  const tree = JSON.parse(output);
  const problems = tree.problems ?? [];
  const allowed = [
    /^invalid: marked@18\.0\.5 .*[/\\]node_modules[/\\]marked$/,
    /^missing: monaco-editor@>= 0\.25\.0 < 1, required by @monaco-editor\/react@4\.7\.0$/,
  ];
  for (const problem of problems) {
    assert.ok(allowed.some(pattern => pattern.test(problem)), `New peer debt: ${problem}`);
  }
  // This documents debt, not a clean-tree attestation. Fixing either debt may remove it.
  assert.equal(new Set(problems).size, problems.length);
});
