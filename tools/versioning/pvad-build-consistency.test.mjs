import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { pvadBuildInfoPlugin, pvadVersionDefines, readPvadBuildInfo } from '../../websites/shared/vitePvadVersion.js';
import {
  validateBuildInfo,
  verifyBuildDirectories,
  verifyContainerVersion,
  verifyContainerMetadata,
  writeBuildInfo,
} from './pvad-build-consistency.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

test('frontend build plugin emits canonical PVAD identity and matching compiler constants', () => {
  const info = readPvadBuildInfo();
  assert.equal(info.product, 'PVAD');
  const emitted = [];
  pvadBuildInfoPlugin(info).generateBundle.call({ emitFile: asset => emitted.push(asset) });
  assert.equal(emitted.length, 1);
  assert.equal(emitted[0].fileName, 'pvad-build-info.json');
  assert.deepEqual(JSON.parse(emitted[0].source), info);
  assert.deepEqual(pvadVersionDefines(info), {
    __PVAD_VERSION__: JSON.stringify(info.productVersion),
    __PVAD_BUILD_COMMIT__: JSON.stringify(info.commitId),
    __PVAD_BUILD_TIME__: JSON.stringify(info.buildTime),
  });
  assert.throws(() => validateBuildInfo({ ...info, product: 'PXA' }, info.productVersion),
    /product must be PVAD/);
});

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'pvad-build-consistency-'));
  await writeFile(join(root, 'VERSION'), '2.3.4\n');
  return root;
}

test('writes and verifies traceable build information', async () => {
  const root = await fixture();
  const output = join(root, 'dist');
  const info = await writeBuildInfo(output, {
    repoRoot: root,
    commitId: '0123456789ab',
    buildTime: '2026-08-03T12:00:00Z',
  });
  assert.equal(info.productVersion, '2.3.4');
  assert.equal(await verifyBuildDirectories([output], { repoRoot: root }), '2.3.4');
});

test('rejects missing, stale, and incomplete build information', async () => {
  const root = await fixture();
  const missing = join(root, 'missing');
  await assert.rejects(
    verifyBuildDirectories([missing], { repoRoot: root }),
    /cannot read pvad-build-info.json/,
  );

  const stale = join(root, 'stale');
  await mkdir(stale);
  await writeFile(join(stale, 'pvad-build-info.json'), JSON.stringify({
    product: 'PVAD',
    productVersion: '2.3.3',
    commitId: '0123456789ab',
    buildTime: '2026-08-03T12:00:00Z',
  }));
  await assert.rejects(
    verifyBuildDirectories([stale], { repoRoot: root }),
    /productVersion is 2.3.3; expected 2.3.4/,
  );
  assert.throws(
    () => validateBuildInfo({
      product: 'PVAD',
      productVersion: '2.3.4',
      commitId: '',
      buildTime: '2026-08-03T12:00:00Z',
    }, '2.3.4'),
    /commitId.*non-empty/,
  );
});

test('verifies the immutable version label on a built container', async () => {
  const root = await fixture();
  const inspect = async (command, args) => {
    assert.equal(command, 'docker');
    assert.deepEqual(args.slice(0, 2), ['inspect', '--format']);
    return { stdout: '2.3.4\n' };
  };
  assert.equal(
    await verifyContainerVersion('pxa-webapi:test', { repoRoot: root, inspect }),
    '2.3.4',
  );
  await assert.rejects(
    verifyContainerVersion('pxa-webapi:stale', {
      repoRoot: root,
      inspect: async () => ({ stdout: '2.3.3\n' }),
    }),
    /container version is 2.3.3; expected 2.3.4/,
  );
});

test('container release metadata rejects unknown, stale and dirty provenance', async () => {
  const repoRoot = await fixture();
  const commit = 'a'.repeat(40);
  const time = '2026-10-21T08:00:00Z';
  const labels = { 'org.opencontainers.image.version': '2.3.4',
    'org.opencontainers.image.revision': commit, 'org.opencontainers.image.created': time };
  const check = (overrides = {}, options = {}) => verifyContainerMetadata('pvad-api:test', {
    repoRoot, expectedCommit: commit, expectedBuildTime: time,
    inspect: async () => ({ stdout: JSON.stringify({ ...labels, ...overrides }) }), ...options,
  });
  assert.equal((await check()).commitId, commit);
  await assert.rejects(check({ 'org.opencontainers.image.revision': 'unknown' }), /invalid revision/);
  await assert.rejects(check({ 'org.opencontainers.image.revision': 'b'.repeat(40) }), /revision mismatch/);
  await assert.rejects(check({ 'org.opencontainers.image.created': 'unknown' }), /timestamp mismatch/);
  await assert.rejects(check({ 'org.opencontainers.image.version': '2.3.3' }), /version/);
  await assert.rejects(check({ 'org.opencontainers.image.revision': `${commit}-dirty` }, { expectedCommit: `${commit}-dirty` }), /dirty images/);
  assert.equal((await check({ 'org.opencontainers.image.revision': `${commit}-dirty` }, {
    expectedCommit: `${commit}-dirty`, allowDirty: true,
  })).commitId, `${commit}-dirty`);
});

test('all shipped frontends emit build information and display the shared version', async () => {
  const viteConfigs = [
    'pvad-designer/vite.config.ts',
    'websites/PVAD.Account/vite.config.js',
    'websites/PVAD.Admin/vite.config.js',
    'websites/PVAD.Company/vite.config.js',
    'websites/PVAD.Demo/vite.config.js',
    'websites/PVAD.Documentation/vite.config.js',
  ];
  for (const relativePath of viteConfigs) {
    const source = await readFile(resolve(repoRoot, relativePath), 'utf8');
    assert.match(source, /pvadBuildInfoPlugin/);
  }
  assert.match(
    await readFile(resolve(repoRoot, 'websites/shared/footer.js'), 'utf8'),
    /\$\{pvadBrand\.displayName\} \$\{pvadVersion\}/,
  );
  assert.match(
    await readFile(resolve(repoRoot, 'pvad-designer/src/components/Layout/DesignerUserMenu.tsx'), 'utf8'),
    /designerVersion/,
  );
  assert.match(
    await readFile(resolve(repoRoot, 'websites/PVAD.Admin/src/main.js'), 'utf8'),
    /PVADE \$\{escapeHtml\(pvadVersion\)\}/,
  );
});

test('stable release workflow verifies versioned artifacts and immutable release identity', async () => {
  const workflow = await readFile(resolve(repoRoot, '.github/workflows/release.yml'), 'utf8');
  assert.match(workflow, /pvad-build-consistency\.mjs verify/);
  assert.match(workflow, /pvad-build-consistency\.mjs verify-container/);
  assert.match(workflow, /git tag --annotate "v\$VERSION"/);
  assert.match(workflow, /--title "PVADE \$VERSION"/);
  assert.match(workflow, /artifacts\/pvad-\*-"\$VERSION"\.tar\.gz/);
  assert.match(workflow, /verify-container-metadata/);
});
