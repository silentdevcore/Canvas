import assert from 'node:assert/strict';
import { readFile, mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { execFileSync } from 'node:child_process';
import {
  createDeploymentEvidence,
  expectedReleaseAssets,
  validateDeploymentRequest,
  verifyReleaseContract,
  readReleaseManifest,
} from './pvad-deployment.mjs';

const version = '1.1.0';
const commit = '30de5ea7d55d009dfc32a378e5bcc08d1f3ae8d1';
const digest = `sha256:${'a'.repeat(64)}`;

function releaseMetadata(overrides = {}) {
  return {
    tagName: `v${version}`,
    isDraft: false,
    isPrerelease: false,
    url: `https://github.com/silentdevcore/Canvas/releases/tag/v${version}`,
    assets: expectedReleaseAssets(version).map(name => ({ name, digest, size: 100 })),
    ...overrides,
  };
}

function verifiedRelease() {
  return verifyReleaseContract({
    version,
    repositoryVersion: version,
    releaseMetadata: releaseMetadata(),
    releaseManifest: { releases: [{ version, channel: 'stable' }] },
    tagCommit: commit,
    checkoutCommit: commit,
  });
}

const containers = [
  { image: `ghcr.io/silentdevcore/pxa-webapi:${version}`, digest },
  { image: `ghcr.io/silentdevcore/pxa-observability-webhook-relay:${version}`, digest },
];

test('release checkout reader accepts either manifest generation but rejects missing or ambiguous inputs', async t => {
  const root = await mkdtemp(join(tmpdir(), 'pvad-release-reader-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const metadata = join(root, 'product-metadata');
  await mkdir(metadata);
  const manifest = { releases: [{ version, channel: 'stable' }] };
  await assert.rejects(readReleaseManifest(root), /exactly one/);
  for (const name of ['pvad-releases.json', 'pxa-releases.json']) {
    const path = join(metadata, name);
    await writeFile(path, JSON.stringify(manifest));
    assert.deepEqual(await readReleaseManifest(root), manifest);
    await rm(path);
  }
  await writeFile(join(metadata, 'pvad-releases.json'), JSON.stringify(manifest));
  await writeFile(join(metadata, 'pxa-releases.json'), JSON.stringify(manifest));
  await assert.rejects(readReleaseManifest(root), /exactly one/);
  await rm(join(metadata, 'pxa-releases.json'));
  await writeFile(join(metadata, 'pvad-releases.json'), 'invalid JSON');
  await assert.rejects(readReleaseManifest(root), SyntaxError);
});

test('verify CLI validates real tagged temporary checkouts from both release generations', async t => {
  const root = await mkdtemp(join(tmpdir(), 'pvad-release-cli-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  for (const identity of ['PVAD', 'PXA']) {
    const checkout = join(root, identity);
    await mkdir(join(checkout, 'product-metadata'), { recursive: true });
    await writeFile(join(checkout, 'VERSION'), version + '\n');
    await writeFile(join(checkout, 'product-metadata', `${identity.toLowerCase()}-releases.json`),
      JSON.stringify({ releases: [{ version, channel: 'stable' }] }));
    const git = (...args) => execFileSync('git', [
      '-c', 'core.hooksPath=/dev/null', '-c', 'commit.gpgsign=false', '-c', 'tag.gpgSign=false',
      '-c', 'user.name=PVAD Release Test', '-c', 'user.email=release-fixture@example.test', ...args,
    ], { cwd: checkout, encoding: 'utf8', stdio: 'pipe' });
    git('init', '--quiet');
    git('add', 'VERSION', 'product-metadata');
    git('commit', '--quiet', '-m', 'Synthetic release fixture');
    git('tag', `v${version}`);
    const metadata = join(root, `${identity}-metadata.json`);
    const output = join(root, `${identity}-verified.json`);
    await writeFile(metadata, JSON.stringify(releaseMetadata({
      assets: expectedReleaseAssets(version, identity).map(name => ({ name, digest, size: 100 })),
    })));
    execFileSync(process.execPath, [new URL('./pvad-deployment.mjs', import.meta.url).pathname,
      'verify', '--version', version, '--release-root', checkout, '--metadata', metadata,
      '--output', output], { encoding: 'utf8' });
    const result = JSON.parse(await readFile(output, 'utf8'));
    assert.equal(result.artifactIdentity, identity);
    assert.equal(result.releaseCommit, git('rev-parse', 'HEAD').trim());
    assert.deepEqual(result.assets.map(asset => asset.name), expectedReleaseAssets(version, identity));
  }
});

test('verifies an immutable stable release and every required artifact digest', () => {
  const release = verifiedRelease();
  assert.equal(release.version, version);
  assert.equal(release.releaseCommit, commit);
  assert.equal(release.assets.length, 7);
  assert.equal(release.product, 'PVAD');
  assert.equal(release.artifactIdentity, 'PVAD');
  assert.ok(release.assets.every(asset => asset.name.startsWith('pvad-')));
});

test('historical releases retain their complete PXA artifact set without mixed-name fallback', () => {
  const args = {
    version, repositoryVersion: version,
    releaseManifest: { releases: [{ version, channel: 'stable' }] },
    tagCommit: commit, checkoutCommit: commit,
  };
  const historical = expectedReleaseAssets(version, 'PXA').map(name => ({ name, digest, size: 100 }));
  const release = verifyReleaseContract({ ...args, releaseMetadata: releaseMetadata({ assets: historical }) });
  assert.equal(release.artifactIdentity, 'PXA');
  assert.deepEqual(release.assets.map(asset => asset.name), historical.map(asset => asset.name));
  const canonical = releaseMetadata().assets;
  for (const assets of [
    [...canonical, historical[0]],
    [...canonical.slice(1), historical[0]],
    [...canonical, ...historical],
    [...canonical, canonical[0]],
  ]) assert.throws(() => verifyReleaseContract({ ...args, releaseMetadata: releaseMetadata({ assets }) }),
    /mixed|ambiguous|duplicate/);
});

test('old command delegates to canonical validation without changing versions or deploying', () => {
  for (const name of ['pvad', 'pxa']) {
    const output = execFileSync(process.execPath, [new URL(`./${name}-deployment.mjs`, import.meta.url).pathname,
      'request', '--version', version, '--environment', 'staging', '--operation', 'deploy'], { encoding: 'utf8' });
    assert.match(output, /Validated deploy request for PVAD 1\.1\.0 in staging/);
  }
});

test('release archive contract matches the canonical producer while registry names stay stable', async () => {
  const workflow = await readFile(new URL('../../.github/workflows/release.yml', import.meta.url), 'utf8');
  assert.match(workflow, /artifacts\/pvad-webapi-\$\{\{/);
  assert.match(workflow, /artifacts\/pvad-designer-\$\{\{/);
  assert.match(workflow, /for site in PVAD\.Account PVAD\.Admin PVAD\.Company PVAD\.Demo PVAD\.Documentation/);
  assert.match(workflow, /ghcr\.io\/\$\{\{ github\.repository_owner \}\}\/pxa-webapi:/);
  assert.deepEqual(expectedReleaseAssets(version).sort(),
    ['webapi', 'designer', 'account', 'admin', 'company', 'demo', 'documentation']
      .map(component => `pvad-${component}-${version}.tar.gz`).sort());
});

test('rejects drafts, tag mismatches, missing assets, and invalid digests', () => {
  const base = {
    version,
    repositoryVersion: version,
    releaseManifest: { releases: [{ version, channel: 'stable' }] },
    tagCommit: commit,
    checkoutCommit: commit,
  };
  assert.throws(() => verifyReleaseContract({
    ...base,
    releaseMetadata: releaseMetadata({ isDraft: true }),
  }), /published stable/);
  assert.throws(() => verifyReleaseContract({
    ...base,
    releaseMetadata: releaseMetadata({ tagName: 'v1.0.0' }),
  }), /tag must be/);
  assert.throws(() => verifyReleaseContract({
    ...base,
    releaseMetadata: releaseMetadata({ assets: [] }),
  }), /missing required asset/);
  const assets = releaseMetadata().assets;
  assets[0] = { ...assets[0], digest: null };
  assert.throws(() => verifyReleaseContract({
    ...base,
    releaseMetadata: releaseMetadata({ assets }),
  }), /SHA-256/);
  assert.throws(() => verifyReleaseContract({
    ...base,
    releaseMetadata: releaseMetadata({ url: 'https://example.com/v1.1.0' }),
  }), /must point to the requested immutable tag/);
});

test('requires source run evidence for retry and rollback but never changes version', () => {
  assert.deepEqual(validateDeploymentRequest({
    version,
    environment: 'staging',
    operation: 'deploy',
  }), { version, environment: 'staging', operation: 'deploy', sourceRunId: null });
  for (const operation of ['retry', 'rollback']) {
    assert.throws(() => validateDeploymentRequest({
      version,
      environment: 'production',
      operation,
    }), /Source workflow run ID is required/);
    assert.equal(validateDeploymentRequest({
      version,
      environment: 'production',
      operation,
      sourceRunId: '1234',
    }).version, version);
  }
});

test('records validated, successful, failed, retry, and rollback evidence immutably', () => {
  const release = verifiedRelease();
  const validated = createDeploymentEvidence({
    release,
    environment: 'staging',
    operation: 'deploy',
    workflowRunId: '7001',
    workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7001',
    actor: 'release-operator',
    repository: 'silentdevcore/Canvas',
    status: 'validated',
    startedAt: '2026-08-03T12:00:00Z',
    completedAt: '2026-08-03T12:01:00Z',
    containers,
  });
  assert.equal(validated.version, version);
  assert.equal(validated.adapter, 'unconfigured');

  for (const status of ['succeeded', 'failed']) {
    const evidence = createDeploymentEvidence({
      release,
      environment: 'staging',
      operation: 'deploy',
      workflowRunId: '7001',
      workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7001',
      actor: 'release-operator',
      repository: 'silentdevcore/Canvas',
      status,
      startedAt: '2026-08-03T12:00:00Z',
      completedAt: '2026-08-03T12:01:00Z',
      containers,
      adapter: 'test-target',
    });
    assert.equal(evidence.version, version);
    assert.equal(evidence.status, status);
    assert.equal(evidence.adapter, 'test-target');
  }
  assert.throws(() => createDeploymentEvidence({
    ...validated,
    release,
    status: 'succeeded',
    workflowRunId: '7003',
    workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7003',
    repository: 'silentdevcore/Canvas',
    containers,
  }), /configured target adapter/);
  assert.throws(() => createDeploymentEvidence({
    release,
    environment: 'staging',
    operation: 'deploy',
    workflowRunId: '7004',
    workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7004',
    actor: 'release-operator',
    repository: 'silentdevcore/Canvas',
    status: 'validated',
    startedAt: '2026-08-03T12:00:00Z',
    completedAt: '2026-08-03T12:01:00Z',
    containers: [containers[0], containers[0]],
  }), /complete immutable container family/);
  for (const operation of ['retry', 'rollback']) {
    const evidence = createDeploymentEvidence({
      release,
      environment: 'production',
      operation,
      sourceRunId: '6999',
      workflowRunId: '7002',
      workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7002',
      actor: 'release-operator',
      repository: 'silentdevcore/Canvas',
      status: 'validated',
      startedAt: '2026-08-03T12:00:00Z',
      completedAt: '2026-08-03T12:01:00Z',
      containers,
    });
    assert.equal(evidence.operation, operation);
    assert.equal(evidence.sourceRunId, '6999');
    assert.equal(evidence.version, version);
  }
});

test('container generations remain independent of archive names and preserve immutable evidence', () => {
  const release = verifiedRelease();
  const args = {
    release, environment: 'staging', operation: 'deploy', workflowRunId: '7005',
    workflowRunUrl: 'https://github.com/silentdevcore/Canvas/actions/runs/7005',
    actor: 'test-operator', repository: 'silentdevcore/Canvas', status: 'validated',
    startedAt: '2026-08-03T12:00:00Z', completedAt: '2026-08-03T12:01:00Z',
  };
  const canonical = containers.map(value => ({ ...value, image: value.image.replace('/pxa-', '/pvad-') }));
  for (const [identity, images] of [['PVAD', canonical], ['PXA', containers]]) {
    const before = JSON.stringify({ release, images });
    for (const operation of ['deploy', 'retry', 'rollback']) {
      const evidence = createDeploymentEvidence({ ...args, operation,
        sourceRunId: operation === 'deploy' ? undefined : '6999', containers: images });
      assert.equal(evidence.containerIdentity, identity);
      assert.equal(evidence.artifactIdentity, 'PVAD');
      assert.deepEqual(evidence.containers, images);
    }
    assert.equal(JSON.stringify({ release, images }), before);
  }
  for (const images of [
    [canonical[0], containers[1]], [canonical[0], canonical[0]],
    canonical.map(value => ({ ...value, image: value.image.replace('ghcr.io/silentdevcore', 'other.example/silentdevcore') })),
    canonical.map(value => ({ ...value, image: value.image.replace('/silentdevcore/', '/other-owner/') })),
    canonical.map(value => ({ ...value, image: value.image.replace(':1.1.0', ':latest') })),
    canonical.map(value => ({ ...value, image: value.image.replace(':1.1.0', ':1.1.1') })),
  ]) assert.throws(() => createDeploymentEvidence({ ...args, containers: images }), /complete immutable container family/);
});

test('workflow binds immutable tags to protected serialized environments without deploying', async () => {
  const workflow = await readFile(new URL('../../.github/workflows/deployment-validation.yml', import.meta.url), 'utf8');
  assert.match(workflow, /environment:\s*[\s\S]*name: pxa-\$\{\{ inputs\.environment \}\}/);
  assert.match(workflow, /group: pxa-deployment-\$\{\{ inputs\.environment \}\}/);
  assert.match(workflow, /ref: v\$\{\{ inputs\.version \}\}/);
  assert.match(workflow, /gh release view "v\$VERSION"/);
  assert.match(workflow, /docker buildx imagetools inspect/);
  assert.match(workflow, /Deployment validation requires explicit confirmation/);
  assert.match(workflow, /pvad-deployment\.mjs request/);
  assert.match(workflow, /status validated/);
  assert.match(workflow, /No target adapter executed/);
  assert.match(workflow, /appendFileSync\(process\.env\.GITHUB_OUTPUT/);
  assert.doesNotMatch(workflow, /release_url=\$\(node/);
  assert.doesNotMatch(workflow, /kubectl apply|docker compose up|ssh /);
});
