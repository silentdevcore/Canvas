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
  assert.match(ci, /--tag pvad-webapi:sbom/);
  assert.match(ci, /verify-container-metadata pvad-webapi:sbom "\$GITHUB_SHA" "\$PVAD_BUILD_TIME"/);
  assert.match(ci, /--build-arg PVAD_BUILD_TIME="\$PVAD_BUILD_TIME"/);
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
  assert.match(source, /WEBAPI="ghcr.io\/\$OWNER\/pxa-webapi:\$VERSION"/);
  assert.match(read('release'), /ghcr.io\/\$\{\{ github.repository_owner \}\}\/pxa-webapi:/);
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
  assert.match(mapping, /Publisher accounts remain unselected\/unregistered/);
  assert.match(read('job-binary-cutover'), /# PXA identifies the pinned historical binary/);
});
