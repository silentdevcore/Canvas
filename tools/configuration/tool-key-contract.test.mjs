import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { spawnSync } from 'node:child_process';
import { mkdtemp, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

test('corpus root failure uses the current name without changing signed fixture identity', async () => {
  const source = await readFile(resolve(root, 'tools/pdf-trust-corpus/Program.cs'), 'utf8');
  assert.ok(source.includes('Could not locate the PVAD repository root.'));
  assert.ok(!source.includes('Could not locate the PXA repository root.'));
  assert.ok(source.includes('CN=PXA Synthetic Corpus Signer'));
  assert.ok(source.includes('document.Info.Author = "PXA synthetic test corpus"'));
});

test('remaining test and PDF validation callers use canonical keys accepted by their receivers', async () => {
  const read = relative => readFile(resolve(root, relative), 'utf8');
  for (const [script, key, receiver] of [
    ['tools/legal/run-deployment-smoke-tests.sh', 'PVAD_RUN_POSTGRES_TESTS', 'tests/PVAD.Api.Tests/PostgreSqlFactAttribute.cs'],
    ['tools/pdf-redaction-recovery/run.sh', 'PVAD_RUN_PDF_RECOVERY_TESTS', 'tests/PVAD.Importer.Tests/PdfRedactionExternalRecoveryTests.cs'],
  ]) {
    const source = await read(script);
    assert.ok(source.includes(`${key}=1 dotnet test`));
    assert.ok(!source.includes(`${key.replace('PVAD_', 'PXA_')}=1`));
    assert.ok((await read(receiver)).includes(`PvadTestEnvironment.Read("${key}")`));
    assert.equal(spawnSync('bash', ['-n', resolve(root, script)], { encoding: 'utf8' }).status, 0);
  }
  const visual = await read('tools/visual-parity/pdf-trust-operations.mjs');
  assert.ok(visual.includes('PVAD_PDF_TRUST_OPERATION_ARTIFACT_DIR: artifactDirectory'));
  assert.ok(!visual.includes('PXA_PDF_TRUST_OPERATION_ARTIFACT_DIR:'));
  assert.ok((await read('tests/PVAD.Importer.Tests/PdfTrustVisualArtifactTests.cs'))
    .includes('PvadTestEnvironment.Read("PVAD_PDF_TRUST_OPERATION_ARTIFACT_DIR")'));
  const evidence = await read('tools/pdfa/einvoice-pdfa-evidence.mjs');
  const validator = await read('tools/pdfa/validate-pdfa.sh');
  for (const key of ['PVAD_PDFA_REPORT_DIR', 'PVAD_VERAPDF_IMAGE']) {
    assert.ok(evidence.includes(`${key}:`));
    assert.ok(!evidence.includes(`${key.replace('PVAD_', 'PXA_')}:`));
    assert.ok(validator.includes(`pvad_environment_alias ${key} ${key.replace('PVAD_', 'PXA_')}`));
  }
  assert.ok(evidence.includes("schema: 'pxa.e-invoice.pdfa-evidence/1'"));
  const helper = await read('tests/Shared/PvadTestEnvironment.cs');
  assert.ok(helper.includes('var legacy = "PXA_" + name[5..];'));
  assert.ok(helper.includes('return current ?? previous;'));
  assert.ok(helper.includes('Conflicting configuration keys:'));
  assert.ok((await read('tests/PVAD.Importer.Tests/Fixtures/RedactionRecovery/README.md'))
    .includes('PVAD_RUN_PDF_RECOVERY_TESTS=1 dotnet test'));
});

test('shell test launchers pass canonical flags to a fake dotnet without running real suites', async () => {
  const directory = await mkdtemp(resolve(tmpdir(), 'pvad-test-launcher-'));
  try {
    // Exit at the first test command, before downstream services or recovery tools.
    await writeFile(resolve(directory, 'dotnet'), `#!${process.execPath}\nprocess.stdout.write(JSON.stringify({postgres:process.env.PVAD_RUN_POSTGRES_TESTS,recovery:process.env.PVAD_RUN_PDF_RECOVERY_TESTS}));process.exit(37);\n`, { mode: 0o700 });
    for (const command of ['pdftotext', 'pdfimages', 'strings'])
      await writeFile(resolve(directory, command), '#!/bin/sh\nexit 99\n', { mode: 0o700 });
    for (const [script, expected] of [
      ['tools/legal/run-deployment-smoke-tests.sh', { postgres: '1' }],
      ['tools/pdf-redaction-recovery/run.sh', { recovery: '1' }],
    ]) {
      const result = spawnSync('bash', [resolve(root, script)], {
        env: { PATH: `${directory}:${process.env.PATH}` }, encoding: 'utf8', timeout: 5000,
      });
      assert.equal(result.status, 37, result.stderr);
      assert.deepEqual(JSON.parse(result.stdout), expected);
    }
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

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
