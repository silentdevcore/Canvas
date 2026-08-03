import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  validateBuildInfo,
  verifyBuildDirectories,
  verifyContainerVersion,
  writeBuildInfo,
} from './pxa-build-consistency.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'pxa-build-consistency-'));
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
    /cannot read pxa-build-info.json/,
  );

  const stale = join(root, 'stale');
  await mkdir(stale);
  await writeFile(join(stale, 'pxa-build-info.json'), JSON.stringify({
    product: 'PXA',
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
      product: 'PXA',
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

test('all shipped frontends emit build information and display the shared version', async () => {
  const viteConfigs = [
    'pxa-designer/vite.config.ts',
    'websites/PXA.Account/vite.config.js',
    'websites/PXA.Admin/vite.config.js',
    'websites/PXA.Company/vite.config.js',
    'websites/PXA.Demo/vite.config.js',
    'websites/PXA.Documentation/vite.config.js',
  ];
  for (const relativePath of viteConfigs) {
    const source = await readFile(resolve(repoRoot, relativePath), 'utf8');
    assert.match(source, /pxaBuildInfoPlugin/);
  }
  assert.match(
    await readFile(resolve(repoRoot, 'websites/shared/footer.js'), 'utf8'),
    /PXA \$\{pxaVersion\}/,
  );
  assert.match(
    await readFile(resolve(repoRoot, 'pxa-designer/src/components/Layout/DesignerUserMenu.tsx'), 'utf8'),
    /designerVersion/,
  );
  assert.match(
    await readFile(resolve(repoRoot, 'websites/PXA.Admin/src/main.js'), 'utf8'),
    /PXA \$\{escapeHtml\(pxaVersion\)\}/,
  );
});

test('stable release workflow verifies versioned artifacts and immutable release identity', async () => {
  const workflow = await readFile(resolve(repoRoot, '.github/workflows/release.yml'), 'utf8');
  assert.match(workflow, /pxa-build-consistency\.mjs verify/);
  assert.match(workflow, /pxa-build-consistency\.mjs verify-container/);
  assert.match(workflow, /git tag --annotate "v\$VERSION"/);
  assert.match(workflow, /--title "PXA \$VERSION"/);
  assert.match(workflow, /artifacts\/pxa-\*-"\$VERSION"\.tar\.gz/);
});
