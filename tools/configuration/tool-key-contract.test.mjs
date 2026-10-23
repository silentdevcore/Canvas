import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

test('visual parity producers emit canonical names for both npm and backend test inputs', async () => {
  const pkg = JSON.parse(await readFile(resolve(root, 'pvad-designer/package.json'), 'utf8'));
  assert.match(pkg.scripts['test:page-layout-parity'], /^PVAD_VISUAL_PARITY_FIXTURE=/);
  assert.match(pkg.scripts['test:visual-parity:ci'], /^PVAD_VISUAL_PARITY_ENFORCE=/);
  const runner = await readFile(resolve(root, 'tools/visual-parity/run.mjs'), 'utf8');
  assert.match(runner, /PVAD_VISUAL_PARITY_ARTIFACTS: raw/);
  assert.match(runner, /PVAD_VISUAL_PARITY_FIXTURE: fixturePath/);
  assert.doesNotMatch(runner, /\bPXA_VISUAL_PARITY_[A-Z_]+:/);
  const receiver = await readFile(resolve(root, 'tests/PVAD.Api.Tests/VisualParityArtifactTests.cs'), 'utf8');
  for (const key of ['PVAD_VISUAL_PARITY_FIXTURE', 'PVAD_VISUAL_PARITY_ARTIFACTS']) {
    assert.ok(receiver.includes(`PvadTestEnvironment.Read("${key}")`), key);
  }
});

test('CI and fuzz workflows publish canonical environment names', async () => {
  for (const relative of ['.github/workflows/ci.yml', '.github/workflows/pdf-trust-fuzz.yml'])
    assert.doesNotMatch(await readFile(resolve(root, relative), 'utf8'), /^\s+PXA_[A-Z0-9_]+:/m);
});

test('AES-256 local fallback image matches its current producer and documentation', async () => {
  const read = relative => readFile(resolve(root, relative), 'utf8');
  const generator = await read('tools/pdf-trust-external-corpus/generate-external-profiles.sh');
  const runner = await read('tools/pdf-security/aes256-interop.mjs');
  const documentation = await read('tools/pdf-security/README.md');
  const image = generator.match(/^readonly IMAGE="([^"]+)"$/m)?.[1];
  assert.equal(image, 'pvad-pdf-trust-external-tools:1');
  assert.ok(runner.includes(`readPvadEnvironment('PVAD_AES256_QPDF_IMAGE') ?? '${image}'`));
  assert.ok(documentation.includes(`\`${image}\``));
  assert.doesNotMatch(runner, /pxa-pdf-trust-external-tools:1/);
  assert.ok(documentation.includes('PXA_AES256_QPDF_IMAGE'));
});

test('JavaScript tools do not bypass the configuration alias resolver', async () => {
  async function visit(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      if (['node_modules', 'dist', 'bin', 'obj', 'configuration'].includes(entry.name)) continue;
      const path = resolve(directory, entry.name);
      if (entry.isDirectory()) await visit(path);
      else if (/\.(?:mjs|js|ts)$/.test(entry.name) && !entry.name.includes('.test.'))
        assert.doesNotMatch(await readFile(path, 'utf8'), /process\.env\.PXA_[A-Z0-9_]+/,
          `Use readPvadEnvironment in ${entry.name}.`);
    }
  }
  await visit(resolve(root, 'tools'));
});
