import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const read = name => readFileSync(new URL(`../../.github/workflows/${name}.yml`, import.meta.url), 'utf8');
const root = fileURLToPath(new URL('../../', import.meta.url));
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

test('all workflow YAML parses with valid job dependencies, existing working directories and shell syntax', () => {
  const workflow = load(read('ci'));
  assert.equal(workflow.name, 'PVADE CI');
  assert.deepEqual(workflow.on.push.branches, ['develop']);
  assert.deepEqual(workflow.on.pull_request.branches, ['develop', 'main']);
  assert.equal(workflow.permissions.contents, 'read');
  const files = readdirSync(resolve(root, '.github/workflows')).filter(name => name.endsWith('.yml'));
  assert.ok(files.includes('ci.yml'));
  for (const filename of files) {
    const workflow = load(read(filename.slice(0, -4)));
    assert.ok(workflow.jobs, filename);
    for (const [name, job] of Object.entries(workflow.jobs)) {
      for (const dependency of typeof job.needs === 'string' ? [job.needs] : job.needs ?? [])
        assert.ok(workflow.jobs[dependency], `${name}: unknown dependency ${dependency}`);
      for (const step of job.steps) {
        const directory = resolve(root, step['working-directory'] ?? '.');
        assert.ok(existsSync(directory), `${name}: missing working directory`);
        if (step.uses) assert.match(step.uses, /@[a-f0-9]{40}$/);
        if (!step.run) continue;
        const syntax = spawnSync('bash', ['-n'], { encoding: 'utf8',
          input: step.run.replace(/\$\{\{[\s\S]*?\}\}/g, 'pvad-ci-expression') });
        assert.equal(syntax.status, 0, `${name}/${step.name}: ${syntax.stderr}`);
        for (const match of step.run.matchAll(/\b(?:dotnet (?:restore|build|publish|test)) ([\w./-]+\.(?:csproj|sln))\b/g))
          assert.ok(existsSync(resolve(directory, match[1])), `${name}: missing ${match[1]}`);
      }
    }
  }
});

test('CI has no old branding and PDF/A consumers match current producer filenames', () => {
  const source = read('ci');
  assert.doesNotMatch(source, /pxa|power\s?dox|canvas/i);
  const workflow = load(source);
  const step = workflow.jobs.dotnet.steps.find(step => step.name === 'Validate generated PDF/A fixtures independently');
  assert.ok(step);
  const consumers = [...step.run.matchAll(/artifacts\/pdfa\/(?:generated|conversions)\/(pvad-[\w-]+\.pdf)/g)]
    .map(match => match[1]);
  assert.equal(consumers.length, 7);
  assert.equal(new Set(consumers).size, consumers.length);
  const producers = ['PdfTrustInspectorTests.cs', 'PdfA2bConverterTests.cs', 'PdfA3bAndColorConversionTests.cs']
    .map(name => readFileSync(resolve(root, 'tests/PVAD.Importer.Tests', name), 'utf8')).join('\n');
  const outputs = [...producers.matchAll(/(?:Path\.Combine\(artifactDirectory,\s*|WriteArtifactAsync\()"([^"\n]+\.pdf)"/g)]
    .map(match => match[1]);
  assert.equal(outputs.length, 10);
  for (const filename of consumers) assert.ok(outputs.includes(filename), `No producer for ${filename}`);
  for (const filename of outputs) assert.ok(filename.startsWith('pvad-'), `Old generated output: ${filename}`);
});

test('new CI SBOM namespace IDs use the selected domain and current product names', () => {
  const workflow = load(read('ci'));
  const step = workflow.jobs['supply-chain'].steps.find(step => step.name === 'Generate WebApi and Designer SPDX SBOMs');
  assert.ok(step);
  for (const product of ['PVAD.WebApi', 'PVAD.Designer']) {
    assert.ok(step.run.includes(`-pn ${product}`));
    assert.ok(step.run.includes(`-nsb "https://pvade.com/sbom/${product}/$VERSION"`));
  }
});

test('configuration contracts install their locked dependencies before running in CI', () => {
  const source = read('ci').split('\n  release-policy:')[0];
  const install = source.indexOf('npm ci --prefix pvad-designer --ignore-scripts');
  const tests = source.indexOf('node --test tools/configuration/*.test.mjs');
  assert.ok(install >= 0 && tests > install);
});

test('ephemeral CI artifacts and local SBOM image use canonical technical identity', () => {
  const ci = read('ci');
  assert.doesNotMatch(ci, /name: pxa-|pxa-webapi:sbom/);
  const steps = load(ci).jobs['supply-chain'].steps;
  const build = steps.find(step => step.uses?.startsWith('docker/build-push-action@'));
  assert.ok(build, 'The release container builder must be exercised without publishing');
  assert.equal(build.with.tags, 'pvad-webapi:sbom');
  assert.equal(build.with.load, true);
  assert.equal(build.with.push, false);
  assert.match(ci, /verify-container-metadata pvad-webapi:sbom "\$GITHUB_SHA" "\$PVAD_BUILD_TIME"/);
  assert.match(build.with['build-args'], /^PVAD_BUILD_TIME=\$\{\{ github.event.head_commit.timestamp \|\| github.event.pull_request.updated_at \}\}$/m);
  assert.match(ci, /image: pvad-webapi:sbom/);
  for (const name of ['release-dry-run', 'pdf-trust-fuzz'])
    assert.doesNotMatch(read(name), /name: pxa-/);
  for (const name of ['prepare-release', 'open-stable-release-pr']) {
    assert.doesNotMatch(read(name), /\/tmp\/pxa-/);
    assert.match(read(name), /--body-file \/tmp\/pvad-/);
  }
});

test('deployment artifact handoff is renamed together without bypassing existing protection boundaries', () => {
  const source = read('deployment-validation');
  const names = [...source.matchAll(/name: (pvad-deployment-preflight-\$\{\{ github.run_id \}\})/g)];
  assert.equal(names.length, 2, 'Upload and download must refer to the same preflight artifact');
  assert.match(source, /group: pvad-deployment-/);
  assert.match(source, /name: pxa-\$\{\{ inputs.environment \}\}/);
  assert.match(source, /WEBAPI="ghcr.io\/\$OWNER\/\$PREFIX-webapi:\$VERSION"/);
  assert.match(source, /CONTAINER_IDENTITY: \$\{\{ inputs.container_identity \}\}/);
  assert.match(read('release'), /ghcr.io\/\$\{\{ env.PVAD_CONTAINER_PUBLISHER \}\}\/pvad-webapi:/);
});

test('canonical workflow locks retain serialization and protected environment bindings', () => {
  const mapping = readFileSync(resolve(root, 'docs/pvad-rename-release-approvals.md'), 'utf8');
  const locks = {
    release: ['pvad-stable-release', false],
    'prepare-release': ['pvad-release-preparation', false],
    'release-dry-run': ['pvad-release-dry-run', true],
    'deployment-validation': ['pvad-deployment-${{ inputs.environment }}', false],
  };
  for (const [name, [group, cancel]] of Object.entries(locks)) {
    const source = read(name);
    const workflow = load(source);
    assert.equal(workflow.concurrency.group, group);
    assert.equal(workflow.concurrency['cancel-in-progress'], cancel);
    assert.match(source, /# Canonical lock: drain legacy runs/);
    assert.ok(source.includes('docs/pvad-rename-release-approvals.md#workflow-lock-cutover'));
    assert.ok(mapping.includes(`\`${name}.yml\``));
  }
  const deployment = load(read('deployment-validation'));
  assert.equal(deployment.jobs['validate-environment'].environment.name, 'pxa-${{ inputs.environment }}');
  assert.match(read('deployment-validation'), /# Retained protected environment/);
  assert.match(mapping, /NuGet and npm publisher accounts\s+remain unselected\/unregistered/);
  assert.match(read('job-binary-cutover'), /# PXA identifies the pinned historical binary/);
});

test('both workflows use the selected GHCR namespace and reject foreign owners before registry operations', () => {
  const targets = JSON.parse(readFileSync(resolve(root, 'deploy/rename/pvade-targets.candidate.json'), 'utf8'));
  assert.equal(targets.publishers.container, 'ghcr.io/silentdevcore');
  assert.equal(targets.publishers.nuget, null);
  assert.equal(targets.publishers.npm, null);
  for (const name of ['release', 'deployment-validation']) {
    const workflow = load(read(name));
    assert.equal(`ghcr.io/${workflow.env.PVAD_CONTAINER_PUBLISHER}`, targets.publishers.container);
    const steps = workflow.jobs[name === 'release' ? 'release' : 'preflight'].steps;
    const guardIndex = steps.findIndex(step => step.name === 'Require approved container publisher');
    assert.ok(guardIndex >= 0 && guardIndex < steps.findIndex(step => step.uses?.startsWith('docker/login-action@')));
    assert.equal(steps[guardIndex].env.REPOSITORY_OWNER, '${{ github.repository_owner }}');
    for (const owner of ['silentdevcore', 'SilentDevCore', 'foreign-owner', '', 'silentdevcore.evil']) {
      const result = spawnSync('bash', ['-e', '-c', steps[guardIndex].run], { encoding: 'utf8',
        env: { PATH: process.env.PATH, PVAD_CONTAINER_PUBLISHER: workflow.env.PVAD_CONTAINER_PUBLISHER, REPOSITORY_OWNER: owner } });
      assert.equal(result.status === 0, ['silentdevcore', 'SilentDevCore'].includes(owner), `${name}: ${owner}`);
    }
  }
});

test('new release image tags and metadata checks stay paired while deployment retains explicit historical selection', () => {
  const release = load(read('release'));
  for (const component of ['webapi', 'observability-webhook-relay']) {
    const steps = release.jobs.release.steps;
    const prefix = `ghcr.io/\${{ env.PVAD_CONTAINER_PUBLISHER }}/pvad-${component}:`;
    const build = steps.find(step => step.with?.tags?.includes(prefix));
    assert.equal(build.with.push, true);
    assert.deepEqual(build.with.tags.trim().split('\n'),
      ['${{ steps.version.outputs.version }}', '${{ steps.version.outputs.minor }}', 'latest'].map(tag => prefix + tag));
    const verify = steps.find(step => step.run?.includes(`docker pull ${prefix}`));
    assert.ok(verify.run.includes('node tools/versioning/pvad-build-consistency.mjs verify-container-metadata'));
    assert.equal(verify.run.split(prefix + '${{ steps.version.outputs.version }}').length, 3);
  }
  const deployment = load(read('deployment-validation'));
  assert.deepEqual(deployment.on.workflow_dispatch.inputs.container_identity.options, ['PXA', 'PVAD']);
  assert.equal(deployment.on.workflow_dispatch.inputs.container_identity.default, 'PXA');
  const inspect = deployment.jobs.preflight.steps.find(step => step.name === 'Verify immutable container tags');
  assert.equal(inspect.env.OWNER, '${{ env.PVAD_CONTAINER_PUBLISHER }}');
  assert.equal(inspect.env.CONTAINER_IDENTITY, '${{ inputs.container_identity }}');
  assert.equal(deployment.jobs['validate-environment'].environment.name, 'pxa-${{ inputs.environment }}');
});
