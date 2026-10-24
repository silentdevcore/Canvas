import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { prepareContainerWorkflows } from './rename-container-workflows.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const read = name => load(readFileSync(resolve(root, `.github/workflows/${name}.yml`), 'utf8'));

test('offline candidate changes image producers/consumers together and preserves protected workflow semantics', () => {
  const release = read('release'), deployment = read('deployment-validation');
  const before = JSON.stringify({ release, deployment });
  const result = prepareContainerWorkflows(release, deployment);
  assert.equal(result.deploymentAuthorized, false);
  assert.ok(result.blockers.includes('publisher-ownership-and-repositories-unapproved'));
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
    const consumer = steps.find(step => step.run?.includes(`docker pull ghcr.io/\${{ github.repository_owner }}/pvad-${component}:`));
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
