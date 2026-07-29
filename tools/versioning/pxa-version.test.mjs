import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import test from 'node:test';
import {
  bumpVersion,
  checkRepository,
  compareVersions,
  prepareRelease,
  renderReleaseNotes,
  synchronizeVersions,
  validateNextVersion,
  validateReleasePullRequest,
} from './pxa-version.mjs';

const packagePaths = [
  'pxa-designer/package.json',
  'websites/PXA.Account/package.json',
  'websites/PXA.Admin/package.json',
  'websites/PXA.Company/package.json',
  'websites/PXA.Demo/package.json',
  'websites/PXA.Documentation/package.json',
  'tools/PXA.Mcp/package.json',
];
const lockPaths = [
  'pxa-designer/package-lock.json',
  'websites/PXA.Admin/package-lock.json',
  'tools/PXA.Mcp/package-lock.json',
];
const dockerPaths = [
  'PXA.WebApi/Dockerfile',
  'src/Observability/PXA.Observability.WebhookRelay/Dockerfile',
];
const composePaths = ['deploy/api/docker-compose.api.yml'];

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'pxa-version-'));
  await writeFile(join(root, 'VERSION'), '1.0.0\n');
  for (const path of packagePaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), '{"name":"test","version":"0.1.0"}\n');
  }
  for (const path of lockPaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), '{"version":"0.1.0","packages":{"":{"version":"0.1.0"}}}\n');
  }
  for (const path of dockerPaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), 'ARG PXA_VERSION=0.1.0\n');
  }
  for (const path of composePaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), 'PXA_VERSION: ${PXA_VERSION:-0.1.0}\n');
  }
  await mkdir(join(root, 'product-metadata'), { recursive: true });
  await writeFile(join(root, 'product-metadata/pxa-releases.json'), JSON.stringify({
    schemaVersion: 1,
    product: 'PXA',
    releases: [{
      version: '1.0.0',
      publishedAt: '2026-07-28',
      channel: 'stable',
      title: 'PXA 1.0.0',
      summary: 'Baseline release.',
      documentationPath: '/#release-notes-1-0-0',
      components: ['api'],
      featureIds: [],
      changes: {
        added: ['Baseline.'],
        improved: [],
        fixed: [],
        security: [],
        deprecated: [],
        breaking: [],
      },
    }],
  }));
  return root;
}

test('calculates strict stable Semantic Version bumps', () => {
  assert.equal(bumpVersion('1.2.3', 'patch'), '1.2.4');
  assert.equal(bumpVersion('1.2.3', 'minor'), '1.3.0');
  assert.equal(bumpVersion('1.2.3', 'major'), '2.0.0');
  assert.equal(compareVersions('2.0.0', '1.99.99') > 0, true);
  assert.throws(() => bumpVersion('1.2.3-beta.1', 'patch'), /Invalid stable/);
});

test('synchronizes package manifests and lockfiles', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await checkRepository(root), '1.0.0');
  const lock = JSON.parse(await readFile(join(root, lockPaths[0]), 'utf8'));
  assert.equal(lock.version, '1.0.0');
  assert.equal(lock.packages[''].version, '1.0.0');
  assert.match(await readFile(join(root, dockerPaths[0]), 'utf8'), /PXA_VERSION=1\.0\.0/);
  assert.match(await readFile(join(root, composePaths[0]), 'utf8'), /PXA_VERSION:-1\.0\.0/);
});

test('prepares the exact next version and requires curated release content', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await prepareRelease(root, 'minor', '2026-08-01'), '1.1.0');
  await assert.rejects(checkRepository(root), /TODO content/);
});

test('validates one matching release label and allowed source branch', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  await validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major'],
    headRef: 'develop',
  });
  await assert.rejects(validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major', 'release:patch'],
    headRef: 'develop',
  }), /exactly one/);
  await assert.rejects(validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major'],
    headRef: 'feature/example',
  }), /develop or hotfix/);
});

test('renders curated GitHub release notes from the shared manifest', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  const notes = await renderReleaseNotes(root, '1.0.0');
  assert.match(notes, /^# PXA 1\.0\.0/m);
  assert.match(notes, /## Added/);
  assert.match(notes, /Baseline\./);
});

test('accepts only one forward stable release step', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await validateNextVersion(root, '0.9.0'), '1.0.0');
  await assert.rejects(validateNextVersion(root, '1.0.0'), /not a single/);
  await assert.rejects(validateNextVersion(root, '2.0.0'), /not a single/);
});
