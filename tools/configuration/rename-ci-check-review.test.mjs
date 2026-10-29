import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import { reviewCiChecks } from './rename-ci-check-review.mjs';

const { load } = createRequire(new URL('../../pvad-designer/package.json', import.meta.url))('js-yaml');
const fixture = () => ({ on: { pull_request: { branches: ['main', 'develop'] } },
  jobs: { policy: {}, build: { needs: 'policy' }, scan: { needs: ['policy'] }, snapshot: { if: 'push-only' } } });
const checks = () => ({ strict: true, contexts: ['policy', 'build'],
  checks: [{ context: 'policy', app_id: 123 }, { context: 'build', app_id: null }] });
const options = { branch: 'main', proposed: ['scan'] };

test('static check review preserves source bindings and inputs without approving settings or execution', () => {
  const workflow = fixture(), metadata = checks(), before = structuredClone({ workflow, metadata });
  const result = reviewCiChecks(workflow, metadata, options);
  assert.deepEqual({ workflow, metadata }, before);
  assert.equal(result.requiredChecksResolve, true);
  assert.deepEqual(result.required.map(row => row.appId), [123, null]);
  assert.deepEqual(result.proposed[0].dependencies, ['policy']);
  for (const key of ['deploymentAuthorized', 'protectionChangeAuthorized', 'runtimeChecksVerified']) assert.equal(result[key], false);
  assert.ok(result.nonRequiredJobs.some(row => row.context === 'snapshot' && row.conditional));
});

test('unknown or duplicate display names never resolve a required check', () => {
  const workflow = fixture(); workflow.jobs.build.name = 'renamed';
  assert.equal(reviewCiChecks(workflow, checks(), options).requiredChecksResolve, false);
  workflow.jobs.other = { name: 'policy' };
  assert.ok(reviewCiChecks(workflow, checks(), options).required[0].blockers.includes('ambiguous-job-name'));
});

test('conditional, matrix, tolerated-error and reusable jobs need explicit review', () => {
  for (const change of [job => { job.if = 'always()'; }, job => { job.strategy = { matrix: { os: ['linux'] } }; },
    job => { job['continue-on-error'] = true; }, job => { job.uses = 'vendor/workflow'; },
    job => { job.name = '${{ matrix.os }}'; }]) {
    const workflow = fixture(); change(workflow.jobs.build);
    assert.equal(reviewCiChecks(workflow, checks(), options).requiredChecksResolve, false);
  }
});

test('conditional dependencies, missing dependencies and cycles cannot silently count as covered', () => {
  for (const change of [workflow => { workflow.jobs.policy.if = 'push-only'; },
    workflow => { workflow.jobs.build.needs = 'missing'; }, workflow => { workflow.jobs.policy.needs = 'build'; }]) {
    const workflow = fixture(); change(workflow);
    assert.equal(reviewCiChecks(workflow, checks(), options).requiredChecksResolve, false);
  }
});

test('unmatched PR branches, path/event filters and absent PR triggers block static approval', () => {
  for (const trigger of [undefined, { branches: ['develop'] }, { paths: ['src/**'] },
    { 'branches-ignore': ['main'] }, { types: ['opened'] }, [], 'unexpected']) {
    const workflow = fixture();
    if (trigger === undefined) delete workflow.on.pull_request; else workflow.on.pull_request = trigger;
    assert.equal(reviewCiChecks(workflow, checks(), options).requiredChecksResolve, false);
  }
  const workflow = fixture(); workflow.on.pull_request = null;
  assert.equal(reviewCiChecks(workflow, checks(), options).requiredChecksResolve, true);
});

test('missing metadata, inconsistent sources and duplicate contexts cannot manufacture protection', () => {
  assert.equal(reviewCiChecks(fixture(), null, options).requiredChecksResolve, false);
  for (const change of [value => { value.contexts.push('policy'); }, value => { value.checks.pop(); },
    value => { value.checks[0].app_id = 'unknown'; }, value => { value.checks.push(value.checks[0]); }]) {
    const value = checks(); change(value);
    assert.throws(() => reviewCiChecks(fixture(), value, options));
  }
  assert.throws(() => reviewCiChecks(fixture(), checks(), { branch: 'unknown' }));
  assert.throws(() => reviewCiChecks(fixture(), checks(), { ...options, proposed: ['scan', 'scan'] }));
});

test('explicit static job display names match contexts without mistaking job IDs for emitted names', () => {
  const workflow = fixture(); workflow.jobs.build.name = 'Compile';
  const metadata = checks(); metadata.contexts[1] = 'Compile'; metadata.checks[1].context = 'Compile';
  const result = reviewCiChecks(workflow, metadata, options);
  assert.equal(result.requiredChecksResolve, true);
  assert.equal(result.required[1].jobId, 'build');
  assert.ok(!result.nonRequiredJobs.some(row => row.jobId === 'build'));
});

test('unavailable proposals and empty protection are separate from valid required name resolution', () => {
  const result = reviewCiChecks(fixture(), checks(), { branch: 'main', proposed: ['missing'] });
  assert.equal(result.requiredChecksResolve, true);
  assert.equal(result.proposedChecksResolve, false);
  assert.equal(reviewCiChecks(fixture(), { strict: true, contexts: [], checks: [] }, options).requiredChecksResolve, false);
});

test('the current CI keeps all existing required names and exposes optional candidates separately', () => {
  const workflow = load(readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8'));
  const contexts = ['version-policy', 'dotnet', 'frontend'];
  for (const branch of ['main', 'develop']) {
    const result = reviewCiChecks(workflow, { strict: true, contexts,
      checks: contexts.map(context => ({ context, app_id: null })) },
    { branch, proposed: ['dependency-security', 'mcp-beta', 'visual-parity', 'supply-chain'] });
    assert.equal(result.requiredChecksResolve, true);
    assert.ok(result.proposed.every(row => row.blockers.length === 0));
    assert.ok(result.nonRequiredJobs.some(row => row.jobId === 'snapshot' && row.conditional));
  }
});

test('the dated closeout candidate preserves strict source bindings and does not authorize activation', () => {
  const candidate = JSON.parse(readFileSync(new URL('../../docs/pvad-github-required-checks-candidate.json', import.meta.url), 'utf8'));
  const workflow = load(readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8'));
  const existing = ['version-policy', 'dotnet', 'frontend'];
  const additions = ['dependency-security', 'mcp-beta', 'visual-parity', 'supply-chain'];
  assert.equal(candidate.schemaVersion, 1);
  assert.equal(candidate.protectionChangeAuthorized, false);
  assert.equal(candidate.deploymentAuthorized, false);
  assert.equal(candidate.negativeEnforcementVerified, false);
  assert.equal(candidate.method, 'PATCH');
  assert.equal(candidate.endpointTemplate, `repos/${candidate.repository}/branches/{branch}/protection/required_status_checks`);
  assert.deepEqual(candidate.rollbackBody, candidate.observedProtection);
  assert.deepEqual(candidate.rollbackBody.checks.map(check => check.context), existing);
  assert.deepEqual(candidate.candidateBody.checks.map(check => check.context), [...existing, ...additions]);
  for (const body of [candidate.candidateBody, candidate.rollbackBody]) {
    assert.equal(body.strict, true);
    assert.deepEqual(Object.keys(body).sort(), ['checks', 'strict']);
    assert.ok(body.checks.every(check => check.app_id === 15368));
  }
  const evidence = candidate.positivePrEvidence;
  assert.equal(evidence.baseRef, 'develop');
  assert.equal(evidence.event, 'pull_request');
  assert.equal(evidence.conclusion, 'success');
  assert.match(evidence.headSha, /^[a-f0-9]{40}$/);
  assert.deepEqual(evidence.checks.map(({ context, app_id }) => ({ context, app_id })), candidate.candidateBody.checks);
  assert.equal(new Set(evidence.checks.map(check => check.jobId)).size, 7);
  assert.ok(evidence.checks.every(check => check.status === 'completed' && check.conclusion === 'success'));
  const result = reviewCiChecks(workflow, {
    ...candidate.observedProtection, contexts: existing,
  }, { branch: 'develop', proposed: additions });
  assert.equal(result.requiredChecksResolve, true);
  assert.equal(result.proposedChecksResolve, true);
  assert.equal(candidate.branches.develop.targetJobsPresent, true);
  assert.equal(candidate.branches.develop.commit, evidence.mergeCommit);
  assert.deepEqual(candidate.branches.main.missingTargetJobs, ['mcp-beta', 'visual-parity']);
  for (const branch of ['develop', 'main']) {
    assert.equal(candidate.branches[branch].readyToActivate, false);
    assert.ok(candidate.branches[branch].blockers.includes('operator-activation-approval'));
    assert.ok(candidate.branches[branch].blockers.includes('negative-required-check-enforcement'));
  }
});
