import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import {
  createDeploymentEvidence,
  expectedReleaseAssets,
  validateDeploymentRequest,
  verifyReleaseContract,
} from './pxa-deployment.mjs';

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

test('verifies an immutable stable release and every required artifact digest', () => {
  const release = verifiedRelease();
  assert.equal(release.version, version);
  assert.equal(release.releaseCommit, commit);
  assert.equal(release.assets.length, 7);
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
  }), /missing immutable container/);
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

test('workflow binds immutable tags to protected serialized environments without deploying', async () => {
  const workflow = await readFile(new URL('../../.github/workflows/deployment-validation.yml', import.meta.url), 'utf8');
  assert.match(workflow, /environment:\s*[\s\S]*name: pxa-\$\{\{ inputs\.environment \}\}/);
  assert.match(workflow, /group: pxa-deployment-\$\{\{ inputs\.environment \}\}/);
  assert.match(workflow, /ref: v\$\{\{ inputs\.version \}\}/);
  assert.match(workflow, /gh release view "v\$VERSION"/);
  assert.match(workflow, /docker buildx imagetools inspect/);
  assert.match(workflow, /Deployment validation requires explicit confirmation/);
  assert.match(workflow, /pxa-deployment\.mjs request/);
  assert.match(workflow, /status validated/);
  assert.match(workflow, /No target adapter executed/);
  assert.doesNotMatch(workflow, /kubectl apply|docker compose up|ssh /);
});
