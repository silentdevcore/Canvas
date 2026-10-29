import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, mkdtempSync, writeFileSync, rmSync, mkdirSync, copyFileSync, symlinkSync, existsSync, realpathSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { prepareContainerWorkflows, reviewProtectedEnvironments } from './rename-container-workflows.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const read = name => load(readFileSync(resolve(root, `.github/workflows/${name}.yml`), 'utf8'));

test('offline candidate changes image producers/consumers together and preserves protected workflow semantics', () => {
  const release = read('release'), deployment = read('deployment-validation');
  const before = JSON.stringify({ release, deployment });
  const result = prepareContainerWorkflows(release, deployment);
  assert.equal(result.deploymentAuthorized, false);
  assert.ok(result.blockers.includes('registry-ownership-and-access-unverified'));
  assert.equal(result.containerPublisher.configuredNamespace, 'ghcr.io/silentdevcore');
  assert.equal(JSON.stringify({ release, deployment }), before);
  for (const [current, candidate] of [[release, result.releaseWorkflow], [deployment, result.deploymentWorkflow]]) {
    assert.deepEqual(candidate.concurrency, current.concurrency);
    assert.deepEqual(candidate.permissions, current.permissions);
    assert.deepEqual(load(dump(candidate)), candidate);
    for (const [jobName, job] of Object.entries(candidate.jobs)) {
      assert.deepEqual(job.environment, current.jobs[jobName].environment);
      assert.deepEqual(job.steps.map(step => step.uses), current.jobs[jobName].steps.map(step => step.uses));
      for (const step of job.steps.filter(step => step.run)) {
        const syntax = spawnSync('bash', ['-n'], { encoding: 'utf8',
          input: step.run.replace(/\$\{\{[\s\S]*?\}\}/g, 'synthetic-expression') });
        assert.equal(syntax.status, 0, syntax.stderr);
      }
    }
  }
  for (const component of ['webapi', 'observability-webhook-relay']) {
    const steps = result.releaseWorkflow.jobs.release.steps;
    const tags = steps.find(step => step.with?.tags?.includes(`/pvad-${component}:`)).with.tags;
    assert.equal(tags.trim().split('\n').length, 3);
    assert.ok(!tags.includes(`/pxa-${component}:`));
    const consumer = steps.find(step => step.run?.includes(`docker pull ghcr.io/\${{ env.PVAD_CONTAINER_PUBLISHER }}/pvad-${component}:`));
    assert.ok(consumer);
  }
  assert.deepEqual(result.deploymentWorkflow.on.workflow_dispatch.inputs.container_identity.options, ['PXA', 'PVAD']);
  assert.equal(result.deploymentWorkflow.on.workflow_dispatch.inputs.container_identity.default, 'PXA');
});

test('candidate inspection shell selects exact immutable families without invoking a real registry', () => {
  const result = prepareContainerWorkflows(read('release'), read('deployment-validation'));
  const step = result.deploymentWorkflow.jobs.preflight.steps.find(step => step.name === 'Verify immutable container tags');
  const prefix = step.run.split('docker buildx imagetools inspect')[0];
  const directory = mkdtempSync(resolve(tmpdir(), 'pvad-image-candidate-'));
  try {
    // If future candidate setup attempts a registry command, fail rather than run Docker.
    writeFileSync(resolve(directory, 'docker'), '#!/bin/sh\nexit 91\n', { mode: 0o700 });
    for (const identity of ['PVAD', 'PXA']) {
      const run = spawnSync('bash', ['-e', '-c', prefix + '\nprintf "%s\\n%s\\n" "$WEBAPI" "$RELAY"'], {
        encoding: 'utf8', env: { PATH: `${directory}:${process.env.PATH}`, OWNER: 'Synthetic-Owner', VERSION: '1.2.3', CONTAINER_IDENTITY: identity },
      });
      assert.equal(run.status, 0, run.stderr);
      assert.deepEqual(run.stdout.trim().split('\n'), [
        `ghcr.io/synthetic-owner/${identity.toLowerCase()}-webapi:1.2.3`,
        `ghcr.io/synthetic-owner/${identity.toLowerCase()}-observability-webhook-relay:1.2.3`,
      ]);
    }
    const invalid = spawnSync('bash', ['-e', '-c', prefix], {
      encoding: 'utf8', env: { PATH: `${directory}:${process.env.PATH}`, OWNER: 'synthetic-owner', VERSION: '1.2.3', CONTAINER_IDENTITY: 'mixed' },
    });
    assert.notEqual(invalid.status, 0);
    assert.match(invalid.stderr, /Unknown container identity/);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('the active deployment inspection emits the exact selected immutable image pair using only a Docker stub', () => {
  const step = read('deployment-validation').jobs.preflight.steps.find(value => value.name === 'Verify immutable container tags');
  const parent = mkdtempSync(resolve(tmpdir(), 'pvad-container-inspection-'));
  try {
    const bin = resolve(parent, 'bin'); mkdirSync(bin);
    for (const [name, source] of [['node', process.execPath], ['tr', '/usr/bin/tr'], ['awk', '/usr/bin/awk']])
      symlinkSync(source, resolve(bin, name));
    writeFileSync(resolve(bin, 'docker'), '#!/usr/bin/env node\n'
      + 'const fs = require("node:fs"); const args = process.argv.slice(2);\n'
      + 'if (args.slice(0, 3).join(" ") !== "buildx imagetools inspect") process.exit(91);\n'
      + 'fs.appendFileSync("docker-requests.jsonl", JSON.stringify(args) + "\\n");\n'
      + 'console.log("Digest: sha256:" + "a".repeat(64));\n', { mode: 0o700 });
    for (const identity of ['PVAD', 'PXA', 'unknown', '']) {
      const directory = resolve(parent, identity || 'missing');
      mkdirSync(resolve(directory, 'artifacts/deployment'), { recursive: true });
      const run = spawnSync('/bin/bash', ['-e', '-c',
        'PATH="$TEST_BIN"\nreadonly PATH\nhash -r\n'
          + 'if [ "$(command -v docker)" != "$EXPECTED_DOCKER" ]; then echo "Docker stub was not selected; test refused to run." >&2; exit 91; fi\n' + step.run],
        { encoding: 'utf8', cwd: directory,
          env: { PATH: bin, TEST_BIN: bin, EXPECTED_DOCKER: resolve(bin, 'docker'), OWNER: 'silentdevcore', VERSION: '1.2.3', CONTAINER_IDENTITY: identity } });
      const valid = ['PVAD', 'PXA'].includes(identity);
      assert.equal(run.status === 0, valid, run.stderr);
      if (valid) {
        const images = [`ghcr.io/silentdevcore/${identity.toLowerCase()}-webapi:1.2.3`,
          `ghcr.io/silentdevcore/${identity.toLowerCase()}-observability-webhook-relay:1.2.3`];
        const requests = readFileSync(resolve(directory, 'docker-requests.jsonl'), 'utf8').trim().split('\n').map(line => JSON.parse(line));
        assert.deepEqual(requests.map(value => value[3]), images);
        assert.deepEqual(JSON.parse(readFileSync(resolve(directory, 'artifacts/deployment/containers.json'))),
          images.map(image => ({ image, digest: `sha256:${'a'.repeat(64)}` })));
      } else {
        assert.equal(existsSync(resolve(directory, 'docker-requests.jsonl')), false);
        assert.equal(existsSync(resolve(directory, 'artifacts/deployment/containers.json')), false);
      }
    }
  } finally { rmSync(parent, { recursive: true, force: true }); }
});

test('candidate generation rejects source drift instead of silently omitting consumers', () => {
  const release = read('release');
  release.jobs.release.steps = release.jobs.release.steps.filter(step => !step.run?.includes('docker pull'));
  assert.throws(() => prepareContainerWorkflows(release, read('deployment-validation')), /verification step/);
  const duplicate = read('release');
  duplicate.jobs.release.steps.push(duplicate.jobs.release.steps.find(step => step.with?.file === 'PVAD.WebApi/Dockerfile'));
  assert.throws(() => prepareContainerWorkflows(duplicate, read('deployment-validation')), /one publishing step/);
  const extraConsumer = read('release');
  extraConsumer.jobs.release.steps.push({ run: 'echo ghcr.io/example/pxa-webapi:1.2.3' });
  assert.throws(() => prepareContainerWorkflows(extraConsumer, read('deployment-validation')), /Unmapped image consumer/);
  const duplicateInspection = read('deployment-validation');
  duplicateInspection.jobs.preflight.steps.push(duplicateInspection.jobs.preflight.steps.find(step => step.name === 'Verify immutable container tags'));
  assert.throws(() => prepareContainerWorkflows(read('release'), duplicateInspection), /duplicate immutable container inspection/);
  const deployment = read('deployment-validation');
  deployment.jobs.preflight.steps.find(step => step.name === 'Verify immutable container tags').run = 'echo changed';
  assert.throws(() => prepareContainerWorkflows(read('release'), deployment), /shape changed/);
});

test('candidate CLI offers no publication or application command', () => {
  const result = spawnSync(process.execPath, [resolve(root, 'tools/configuration/rename-container-workflows.mjs'), '--publish'],
    { encoding: 'utf8' });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /No deployment or publishing arguments/);
});

function legacyWorkflowSources() {
  const release = read('release'), deployment = read('deployment-validation');
  delete release.env; delete deployment.env;
  for (const workflow of [release, deployment]) {
    for (const job of Object.values(workflow.jobs))
      job.steps = job.steps.filter(step => step.name !== 'Require approved container publisher');
  }
  for (const step of release.jobs.release.steps) {
    for (const component of ['webapi', 'observability-webhook-relay']) {
      const current = `ghcr.io/\${{ env.PVAD_CONTAINER_PUBLISHER }}/pvad-${component}:`;
      const legacy = `ghcr.io/\${{ github.repository_owner }}/pxa-${component}:`;
      if (step.with?.tags) step.with.tags = step.with.tags.replaceAll(current, legacy);
      if (step.run) step.run = step.run.replaceAll(current, legacy);
    }
  }
  delete deployment.on.workflow_dispatch.inputs.container_identity;
  const inspection = deployment.jobs.preflight.steps.find(step => step.name === 'Verify immutable container tags');
  inspection.env.OWNER = '${{ github.repository_owner }}';
  delete inspection.env.CONTAINER_IDENTITY;
  const start = inspection.run.indexOf('case "$CONTAINER_IDENTITY"'), end = inspection.run.indexOf('docker buildx imagetools inspect');
  assert.ok(start >= 0 && end > start);
  inspection.run = inspection.run.slice(0, start)
    + 'WEBAPI="ghcr.io/$OWNER/pxa-webapi:$VERSION"\nRELAY="ghcr.io/$OWNER/pxa-observability-webhook-relay:$VERSION"\n'
    + inspection.run.slice(end);
  return { release, deployment };
}

test('candidate preparation accepts the canonical workflows unchanged and is idempotent', () => {
  const release = read('release'), deployment = read('deployment-validation');
  const result = prepareContainerWorkflows(release, deployment);
  assert.deepEqual(result.releaseWorkflow, release);
  assert.deepEqual(result.deploymentWorkflow, deployment);
  const repeated = prepareContainerWorkflows(result.releaseWorkflow, result.deploymentWorkflow);
  assert.deepEqual(repeated.releaseWorkflow, release);
  assert.deepEqual(repeated.deploymentWorkflow, deployment);
});

test('older workflow models remain supported as offline candidates without granting publisher approval', () => {
  const { release, deployment } = legacyWorkflowSources();
  const before = structuredClone({ release, deployment });
  const result = prepareContainerWorkflows(release, deployment);
  assert.deepEqual({ release, deployment }, before);
  assert.equal(result.containerPublisher.configuredNamespace, null);
  assert.ok(result.blockers.includes('publisher-ownership-and-repositories-unapproved'));
  for (const component of ['webapi', 'observability-webhook-relay']) {
    const prefix = `ghcr.io/\${{ github.repository_owner }}/pvad-${component}:`;
    assert.ok(result.releaseWorkflow.jobs.release.steps.some(step => step.with?.tags?.includes(prefix)));
  }
  const repeated = prepareContainerWorkflows(result.releaseWorkflow, result.deploymentWorkflow);
  assert.deepEqual(repeated.releaseWorkflow, result.releaseWorkflow);
  assert.deepEqual(repeated.deploymentWorkflow, result.deploymentWorkflow);
});

test('mixed publishers, mixed image families, incomplete tags and missing metadata checks require review', () => {
  for (const change of [
    ({ release }) => { release.env.PVAD_CONTAINER_PUBLISHER = 'unselected-owner'; },
    ({ deployment }) => { deployment.env.PVAD_CONTAINER_PUBLISHER = 'different-owner'; },
    ({ release }) => { release.jobs.release.steps.find(step => step.with?.tags).with.tags += 'ghcr.io/foreign/pvad-webapi:latest\n'; },
    ({ release }) => { const build = release.jobs.release.steps.find(step => step.with?.tags); build.with.tags = build.with.tags.trim().split('\n').slice(0, 2).join('\n'); },
    ({ release }) => { const step = release.jobs.release.steps.find(step => step.run?.includes('docker pull')); step.run = step.run.replace('verify-container-metadata', 'omitted-verification'); },
    ({ release }) => {
      for (const step of release.jobs.release.steps) {
        if (step.with?.tags) step.with.tags = step.with.tags.replaceAll('/pvad-observability-webhook-relay:', '/pxa-observability-webhook-relay:');
        if (step.run) step.run = step.run.replaceAll('/pvad-observability-webhook-relay:', '/pxa-observability-webhook-relay:');
      }
    },
  ]) {
    const value = { release: read('release'), deployment: read('deployment-validation') }; change(value);
    assert.throws(() => prepareContainerWorkflows(value.release, value.deployment));
  }
});

const capturedAt = '2026-10-26T09:00:00.000Z';
const reviewOptions = { repository: 'synthetic-owner/project', now: Date.parse(capturedAt) + 60_000 };
function protectedEnvironment(name) {
  return { name, canAdminsBypass: false,
    protectionRules: [
      { id: 1, type: 'required_reviewers', prevent_self_review: true,
        reviewers: [{ type: 'User', reviewer: { id: 11 } }, { type: 'Team', reviewer: { id: 12 } }] },
      { id: 2, type: 'wait_timer', wait_timer: 10 }, { id: 3, type: 'branch_policy' },
    ], deploymentBranchPolicy: { protected_branches: false, custom_branch_policies: true },
    deploymentBranchPolicies: [{ type: 'branch', name: 'develop' }, { type: 'tag', name: 'v*' }],
    secretNames: ['SYNTHETIC_SECRET'], variableNames: ['SYNTHETIC_SETTING'] };
}
function snapshot() {
  return { schemaVersion: 1, repository: reviewOptions.repository, capturedAt, paginationComplete: true,
    workflowRuns: [{ id: 123, status: 'completed' }],
    environments: ['pxa-staging', 'pvad-staging', 'pxa-production', 'pvad-production'].map(protectedEnvironment) };
}

test('missing environment evidence keeps the source-only candidate blocked', () => {
  const result = prepareContainerWorkflows(read('release'), read('deployment-validation'));
  assert.equal(result.protectedEnvironmentReview.protectionComparisonPassed, false);
  assert.deepEqual(result.protectedEnvironmentReview.blockers, ['protected-environment-snapshot-required']);
});

test('matching controls are order-independent, ignore rule IDs and never authorize activation', () => {
  const input = snapshot(), before = structuredClone(input);
  const target = input.environments[1];
  target.protectionRules.reverse();
  target.protectionRules.forEach(value => { value.id += 100; });
  target.protectionRules.find(rule => rule.type === 'required_reviewers').reviewers.reverse();
  target.deploymentBranchPolicies.reverse();
  const original = structuredClone(input);
  const result = reviewProtectedEnvironments(input, reviewOptions);
  assert.equal(result.protectionComparisonPassed, true);
  assert.equal(result.deploymentAuthorized, false);
  assert.equal(result.configurationValuesVerified, false);
  assert.equal(result.operatorApprovalRequired, true);
  assert.deepEqual(input, original);
  assert.equal(result.environments[0].sourceControlHash, result.environments[0].targetControlHash);
  assert.notDeepEqual(input, before);
  const serialized = JSON.stringify(result);
  assert.ok(!serialized.includes('SYNTHETIC_SECRET') && !serialized.includes('SYNTHETIC_SETTING'));
});

for (const [name, change] of [
  ['reviewers removed', value => { value.protectionRules = value.protectionRules.filter(rule => rule.type !== 'required_reviewers'); }],
  ['reviewer changed', value => { value.protectionRules[0].reviewers[0].reviewer.id = 99; }],
  ['self-review allowed', value => { value.protectionRules[0].prevent_self_review = false; }],
  ['wait removed', value => { value.protectionRules[1].wait_timer = 0; }],
  ['administrator bypass allowed', value => { value.canAdminsBypass = true; }],
  ['branch pattern widened', value => { value.deploymentBranchPolicies[0].name = '*'; }],
  ['branch changed to tag', value => { value.deploymentBranchPolicies[0].type = 'tag'; }],
  ['branch protection removed', value => { value.deploymentBranchPolicy = null; value.deploymentBranchPolicies = []; }],
]) test(`environment comparison blocks ${name}`, () => {
  const input = snapshot(); change(input.environments[1]);
  const result = reviewProtectedEnvironments(input, reviewOptions);
  assert.equal(result.protectionComparisonPassed, false);
  assert.ok(result.environments[0].blockers.includes('protection-controls-differ'));
});

test('different secret and variable inventories block comparison without exposing their names', () => {
  const input = snapshot(); input.environments[1].secretNames = []; input.environments[3].variableNames = [];
  const result = reviewProtectedEnvironments(input, reviewOptions);
  assert.equal(result.protectionComparisonPassed, false);
  assert.deepEqual(result.environments[0].blockers, ['secret-inventory-differs']);
  assert.deepEqual(result.environments[1].blockers, ['variable-inventory-differs']);
});

for (const status of ['queued', 'in_progress', 'waiting', 'requested', 'pending', 'action_required', 'unknown'])
  test(`workflow status ${status} does not count as a drained generation`, () => {
    const input = snapshot(); input.workflowRuns[0].status = status;
    const result = reviewProtectedEnvironments(input, reviewOptions);
    assert.equal(result.activeRunCount, 1);
    assert.equal(result.protectionComparisonPassed, false);
    assert.ok(result.blockers.includes('workflow-generations-not-drained'));
  });

test('incomplete pagination, stale or future evidence and missing targets cannot pass', () => {
  for (const change of [value => { value.paginationComplete = false; },
    value => { value.capturedAt = '2026-10-24T09:00:00.000Z'; },
    value => { value.capturedAt = '2026-10-27T09:00:00.000Z'; },
    value => { value.environments = value.environments.filter(environment => !environment.name.startsWith('pvad-')); }]) {
    const input = snapshot(); change(input);
    assert.equal(reviewProtectedEnvironments(input, reviewOptions).protectionComparisonPassed, false);
  }
});

test('foreign repositories, duplicate environments and unsupported rules fail closed', () => {
  for (const change of [value => { value.repository = 'foreign/project'; },
    value => { value.environments.push(structuredClone(value.environments[0])); },
    value => { value.environments[0].protectionRules.push({ type: 'unknown-rule' }); },
    value => { value.environments[0].protectionRules[0].futureControl = true; },
    value => { value.environments[0].protectionRules[0].reviewers[0].reviewer.email = 'synthetic@example.test'; },
    value => { value.environments[0].secretValues = { SYNTHETIC_SECRET: 'must-not-be-read' }; },
    value => { value.environments[0].deploymentBranchPolicy.custom_branch_policies = false; }]) {
    const input = snapshot(); change(input);
    assert.throws(() => reviewProtectedEnvironments(input, reviewOptions));
  }
  assert.throws(() => reviewProtectedEnvironments(snapshot()), /expected repository/);
});

test('environment review does not edit the active or ordinary candidate environment binding', () => {
  const deployment = read('deployment-validation');
  const result = prepareContainerWorkflows(read('release'), deployment, snapshot(), reviewOptions);
  assert.equal(result.protectedEnvironmentReview.protectionComparisonPassed, true);
  assert.equal(result.deploymentWorkflow.jobs['validate-environment'].environment.name, 'pxa-${{ inputs.environment }}');
  assert.equal(deployment.jobs['validate-environment'].environment.name, 'pxa-${{ inputs.environment }}');
});

test('the offline CLI binds receipts to files, scopes environment candidates and removes stale output after failure', () => {
  const directory = realpathSync(mkdtempSync(resolve(tmpdir(), 'pvad-workflow-preparation-')));
  const tool = resolve(directory, 'tools/configuration/rename-container-workflows.mjs');
  const output = resolve(directory, 'artifacts/rename/container-workflows');
  const digest = path => createHash('sha256').update(readFileSync(path)).digest('hex');
  const execute = (command, args) => spawnSync(command, args, { cwd: directory, encoding: 'utf8' });
  try {
    mkdirSync(resolve(directory, 'tools/configuration'), { recursive: true });
    mkdirSync(resolve(directory, '.github/workflows'), { recursive: true });
    symlinkSync(resolve(root, 'pvad-designer'), resolve(directory, 'pvad-designer'), 'dir');
    copyFileSync(resolve(root, 'tools/configuration/rename-container-workflows.mjs'), tool);
    for (const name of ['release', 'deployment-validation'])
      copyFileSync(resolve(root, `.github/workflows/${name}.yml`), resolve(directory, `.github/workflows/${name}.yml`));
    assert.equal(execute('git', ['init', '--quiet']).status, 0);
    assert.equal(execute('git', ['remote', 'add', 'origin', 'https://github.com/synthetic-owner/project.git']).status, 0);
    const input = snapshot(); input.capturedAt = new Date().toISOString();
    const snapshotPath = resolve(directory, 'snapshot.json');
    const writeSnapshot = () => writeFileSync(snapshotPath, JSON.stringify(input));
    const candidatePath = resolve(output, 'deployment-validation.environments.candidate.yml');
    const report = () => JSON.parse(readFileSync(resolve(output, 'report.json')));
    const generate = () => execute(process.execPath, [tool, '--environment-snapshot', snapshotPath]);
    writeSnapshot();
    const run = generate();
    assert.equal(run.status, 0, run.stderr);
    assert.equal(report().status, 'prepared-not-authorized');
    assert.equal(report().deploymentAuthorized, false);
    assert.equal(report().protectedEnvironmentReview.protectionComparisonPassed, true);
    assert.equal(report().sourceHashes.environmentSnapshot, digest(snapshotPath));
    assert.equal(report().sourceHashes.release, digest(resolve(directory, '.github/workflows/release.yml')));
    assert.equal(report().sourceHashes.deployment, digest(resolve(directory, '.github/workflows/deployment-validation.yml')));
    for (const [name, hash] of Object.entries(report().candidateHashes)) assert.equal(hash, digest(resolve(output, name)));
    const candidate = load(readFileSync(candidatePath, 'utf8'));
    const ordinary = load(readFileSync(resolve(output, 'deployment-validation.candidate.yml'), 'utf8'));
    const expected = structuredClone(ordinary);
    expected.jobs['validate-environment'].environment.name = 'pvad-${{ inputs.environment }}';
    assert.deepEqual(candidate, expected);
    for (const name of ['release', 'deployment-validation'])
      assert.equal(readFileSync(resolve(directory, `.github/workflows/${name}.yml`), 'utf8'), readFileSync(resolve(root, `.github/workflows/${name}.yml`), 'utf8'));

    assert.equal(execute(process.execPath, [tool]).status, 0);
    assert.equal(existsSync(candidatePath), false);
    assert.equal(report().sourceHashes.environmentSnapshot, null);
    assert.equal(generate().status, 0);
    assert.equal(existsSync(candidatePath), true);
    input.environments[0].secretValues = { SYNTHETIC_SECRET: 'DO-NOT-ECHO-SYNTHETIC-VALUE' };
    writeSnapshot();
    const failure = generate();
    assert.notEqual(failure.status, 0);
    assert.ok(!failure.stderr.includes('DO-NOT-ECHO-SYNTHETIC-VALUE'));
    assert.equal(report().status, 'failed');
    assert.equal(report().deploymentAuthorized, false);
    assert.equal(existsSync(candidatePath), false);
    assert.equal(existsSync(resolve(output, 'release.candidate.yml')), false);
    delete input.environments[0].secretValues;
    input.repository = 'foreign/project';
    writeSnapshot();
    assert.notEqual(generate().status, 0);
    assert.equal(report().status, 'failed');
    assert.equal(existsSync(candidatePath), false);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});
