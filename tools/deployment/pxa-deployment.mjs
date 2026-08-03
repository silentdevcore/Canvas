#!/usr/bin/env node

import { execFile } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const scriptRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export const DEPLOYMENT_ENVIRONMENTS = Object.freeze(['staging', 'production']);
export const DEPLOYMENT_OPERATIONS = Object.freeze(['deploy', 'retry', 'rollback']);
export const DEPLOYMENT_STATUSES = Object.freeze(['validated', 'succeeded', 'failed']);

export function expectedReleaseAssets(version) {
  parseStableVersion(version);
  return [
    `pxa-account-${version}.tar.gz`,
    `pxa-admin-${version}.tar.gz`,
    `pxa-company-${version}.tar.gz`,
    `pxa-demo-${version}.tar.gz`,
    `pxa-designer-${version}.tar.gz`,
    `pxa-documentation-${version}.tar.gz`,
    `pxa-webapi-${version}.tar.gz`,
  ];
}

export function parseStableVersion(value) {
  if (!/^\d+\.\d+\.\d+$/.test(value ?? ''))
    throw new Error('Deployment version must be a stable Semantic Version such as 1.1.0.');
  return value;
}

function requireChoice(value, choices, label) {
  if (!choices.includes(value)) throw new Error(`${label} must be one of: ${choices.join(', ')}.`);
  return value;
}

function requireText(value, label) {
  if (typeof value !== 'string' || value.trim() === '') throw new Error(`${label} is required.`);
  return value.trim();
}

function normalizeRunId(value, required, label) {
  const normalized = value == null || value === '' ? null : String(value);
  if (required && normalized == null) throw new Error(`${label} is required for retry and rollback.`);
  if (normalized != null && !/^\d+$/.test(normalized)) throw new Error(`${label} must be numeric.`);
  return normalized;
}

export function validateDeploymentRequest({ version, environment, operation, sourceRunId }) {
  const normalizedOperation = requireChoice(operation, DEPLOYMENT_OPERATIONS, 'Operation');
  const normalizedSourceRunId = normalizeRunId(
    sourceRunId,
    normalizedOperation !== 'deploy',
    'Source workflow run ID',
  );
  if (normalizedOperation === 'deploy' && normalizedSourceRunId != null)
    throw new Error('A new deployment must not reference a source workflow run.');
  return {
    version: parseStableVersion(version),
    environment: requireChoice(environment, DEPLOYMENT_ENVIRONMENTS, 'Environment'),
    operation: normalizedOperation,
    sourceRunId: normalizedSourceRunId,
  };
}

export function verifyReleaseContract({
  version,
  repositoryVersion,
  releaseMetadata,
  releaseManifest,
  tagCommit,
  checkoutCommit,
}) {
  parseStableVersion(version);
  if (repositoryVersion !== version)
    throw new Error(`Release tag contains VERSION ${repositoryVersion}; expected ${version}.`);
  if (releaseMetadata.tagName !== `v${version}`)
    throw new Error(`GitHub Release tag must be v${version}.`);
  if (releaseMetadata.isDraft || releaseMetadata.isPrerelease)
    throw new Error('Deployment requires a published stable GitHub Release.');
  if (tagCommit !== checkoutCommit)
    throw new Error('The checked-out commit does not match the immutable release tag.');
  const published = releaseManifest.releases?.find(release => release.version === version);
  if (!published || published.channel !== 'stable')
    throw new Error(`Shared release metadata has no stable PXA ${version} entry.`);

  const assetsByName = new Map((releaseMetadata.assets ?? []).map(asset => [asset.name, asset]));
  const assets = expectedReleaseAssets(version).map(name => {
    const asset = assetsByName.get(name);
    if (!asset) throw new Error(`GitHub Release is missing required asset ${name}.`);
    if (!/^sha256:[a-f0-9]{64}$/.test(asset.digest ?? ''))
      throw new Error(`Release asset ${name} has no valid SHA-256 digest.`);
    if (!Number.isSafeInteger(asset.size) || asset.size <= 0)
      throw new Error(`Release asset ${name} has an invalid size.`);
    return { name, digest: asset.digest, size: asset.size };
  });

  const releaseUrl = requireText(releaseMetadata.url, 'GitHub Release URL');
  const expectedReleaseUrl = new RegExp(
    `^https://github\\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/releases/tag/v${version.replaceAll('.', '\\.')}$`,
  );
  if (!expectedReleaseUrl.test(releaseUrl))
    throw new Error('GitHub Release URL must point to the requested immutable tag.');

  return {
    schemaVersion: 1,
    product: 'PXA',
    version,
    tag: `v${version}`,
    releaseCommit: tagCommit,
    releaseUrl,
    assets,
  };
}

export function createDeploymentEvidence({
  release,
  environment,
  operation,
  sourceRunId,
  workflowRunId,
  workflowRunUrl,
  actor,
  repository,
  status,
  startedAt,
  completedAt,
  containers,
  adapter = 'unconfigured',
}) {
  const request = validateDeploymentRequest({
    version: release.version,
    environment,
    operation,
    sourceRunId,
  });
  const normalizedStatus = requireChoice(status, DEPLOYMENT_STATUSES, 'Status');
  const normalizedAdapter = requireText(adapter, 'Adapter');
  if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(normalizedAdapter))
    throw new Error('Adapter must be a stable lowercase identifier.');
  if (normalizedStatus !== 'validated' && normalizedAdapter === 'unconfigured')
    throw new Error('Succeeded or failed evidence requires a configured target adapter.');
  const normalizedWorkflowRunId = normalizeRunId(workflowRunId, true, 'Workflow run ID');
  const normalizedContainers = (containers ?? []).map(container => {
    const image = requireText(container.image, 'Container image');
    if (!/^sha256:[a-f0-9]{64}$/.test(container.digest ?? ''))
      throw new Error(`Container ${image} has no valid immutable digest.`);
    return { image, digest: container.digest };
  });
  if (normalizedContainers.length !== 2)
    throw new Error('Deployment evidence requires WebApi and Observability Relay container digests.');
  const expectedContainerSuffixes = [
    `/pxa-webapi:${release.version}`,
    `/pxa-observability-webhook-relay:${release.version}`,
  ];
  for (const suffix of expectedContainerSuffixes) {
    if (!normalizedContainers.some(container => container.image.endsWith(suffix)))
      throw new Error(`Deployment evidence is missing immutable container ${suffix.slice(1)}.`);
  }

  const normalizedStartedAt = new Date(requireText(startedAt, 'Started timestamp'));
  const normalizedCompletedAt = new Date(requireText(completedAt, 'Completed timestamp'));
  if (Number.isNaN(normalizedStartedAt.valueOf()) || Number.isNaN(normalizedCompletedAt.valueOf()))
    throw new Error('Deployment evidence timestamps must be valid ISO dates.');
  if (normalizedCompletedAt < normalizedStartedAt)
    throw new Error('Completed timestamp must not precede started timestamp.');

  return {
    schemaVersion: 1,
    product: 'PXA',
    deploymentId: `${request.environment}-${release.version}-${normalizedWorkflowRunId}`,
    version: release.version,
    tag: release.tag,
    releaseCommit: release.releaseCommit,
    releaseUrl: release.releaseUrl,
    environment: request.environment,
    operation: request.operation,
    sourceRunId: request.sourceRunId,
    workflowRunId: normalizedWorkflowRunId,
    workflowRunUrl: requireText(workflowRunUrl, 'Workflow run URL'),
    actor: requireText(actor, 'Actor'),
    repository: requireText(repository, 'Repository'),
    status: normalizedStatus,
    adapter: normalizedAdapter,
    startedAt: normalizedStartedAt.toISOString(),
    completedAt: normalizedCompletedAt.toISOString(),
    artifacts: release.assets,
    containers: normalizedContainers,
  };
}

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
}

async function readJson(path) {
  return JSON.parse(await readFile(path, 'utf8'));
}

async function writeJson(path, value) {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
}

async function git(root, ...args) {
  return (await execFileAsync('git', args, { cwd: root })).stdout.trim();
}

async function main(args = process.argv.slice(2)) {
  const command = args.shift();
  if (command === 'request') {
    const request = validateDeploymentRequest({
      version: option(args, '--version'),
      environment: option(args, '--environment'),
      operation: option(args, '--operation'),
      sourceRunId: option(args, '--source-run-id'),
    });
    console.log(`Validated ${request.operation} request for PXA ${request.version} in ${request.environment}.`);
    return;
  }
  if (command === 'verify') {
    const version = option(args, '--version');
    const metadataPath = resolve(option(args, '--metadata'));
    const outputPath = resolve(option(args, '--output'));
    const releaseRoot = resolve(option(args, '--release-root') ?? scriptRoot);
    const release = verifyReleaseContract({
      version,
      repositoryVersion: (await readFile(resolve(releaseRoot, 'VERSION'), 'utf8')).trim(),
      releaseMetadata: await readJson(metadataPath),
      releaseManifest: await readJson(resolve(releaseRoot, 'product-metadata/pxa-releases.json')),
      tagCommit: await git(releaseRoot, 'rev-list', '-n', '1', `v${version}`),
      checkoutCommit: await git(releaseRoot, 'rev-parse', 'HEAD'),
    });
    await writeJson(outputPath, release);
    console.log(`Verified immutable PXA ${version} release ${release.releaseCommit}.`);
    return;
  }
  if (command === 'record') {
    const containers = await readJson(resolve(option(args, '--containers')));
    const evidence = createDeploymentEvidence({
      release: await readJson(resolve(option(args, '--release'))),
      environment: option(args, '--environment'),
      operation: option(args, '--operation'),
      sourceRunId: option(args, '--source-run-id'),
      workflowRunId: option(args, '--workflow-run-id'),
      workflowRunUrl: option(args, '--workflow-run-url'),
      actor: option(args, '--actor'),
      repository: option(args, '--repository'),
      status: option(args, '--status'),
      startedAt: option(args, '--started-at'),
      completedAt: option(args, '--completed-at'),
      containers,
      adapter: option(args, '--adapter') ?? 'unconfigured',
    });
    await writeJson(resolve(option(args, '--output')), evidence);
    console.log(`Recorded ${evidence.operation} validation ${evidence.deploymentId}.`);
    return;
  }
  throw new Error('Usage: pxa-deployment.mjs request|verify|record [options]');
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
