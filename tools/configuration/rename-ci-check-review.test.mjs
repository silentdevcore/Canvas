import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import { reviewCiChecks, reviewNegativeCheckEnforcement } from './rename-ci-check-review.mjs';

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
  assert.equal(candidate.postMergeEvidence.headSha, evidence.mergeCommit);
  assert.notEqual(candidate.postMergeEvidence.headSha, evidence.headSha);
  assert.equal(candidate.postMergeEvidence.event, 'push');
  assert.equal(candidate.postMergeEvidence.conclusion, 'success');
  assert.notEqual(candidate.postMergeEvidence.runId, evidence.runId);
  assert.deepEqual(candidate.branches.main.missingTargetJobs, ['mcp-beta', 'visual-parity']);
  for (const branch of ['develop', 'main']) {
    assert.equal(candidate.branches[branch].readyToActivate, false);
    assert.ok(candidate.branches[branch].blockers.includes('operator-activation-approval'));
    assert.ok(candidate.branches[branch].blockers.includes('negative-required-check-enforcement'));
  }
});

const rejectionFixture = () => {
  const requiredChecks = [{ context: 'build', app_id: 123 }, { context: 'visual', app_id: 123 }];
  const options = { expectedHead: 'a'.repeat(40), expectedBase: 'b'.repeat(40),
    requiredChecks, failingContext: 'visual', intentionalStep: 'Intentional acceptance failure' };
  const nodes = requiredChecks.map(check => ({ __typename: 'CheckRun', name: check.context,
    status: 'COMPLETED', conclusion: check.context === 'visual' ? 'FAILURE' : 'SUCCESS',
    isRequired: true, checkSuite: { app: { databaseId: 123 } }, detailsUrl: `https://example.test/job/${check.context}` }));
  const pr = { state: 'OPEN', isDraft: false, autoMergeRequest: null, reviewDecision: 'APPROVED',
    headRefOid: options.expectedHead, baseRefOid: options.expectedBase, baseRefName: 'develop',
    mergeStateStatus: 'BLOCKED', mergeable: 'MERGEABLE',
    reviewThreads: { pageInfo: { hasNextPage: false }, nodes: [] },
    commits: { nodes: [{ commit: { oid: options.expectedHead,
      statusCheckRollup: { contexts: { pageInfo: { hasNextPage: false }, nodes } } } }] } };
  const job = { name: 'visual', status: 'completed', conclusion: 'failure', url: nodes[1].detailsUrl,
    steps: [{ name: 'Real validation', status: 'completed', conclusion: 'success' },
      { name: options.intentionalStep, status: 'completed', conclusion: 'failure' }] };
  return { pr, job, options, nodes };
};

test('negative enforcement requires an approved exact-head PR, failed required check and successful remaining checks', () => {
  const { pr, job, options } = rejectionFixture();
  const before = structuredClone({ pr, job, options });
  assert.equal(reviewNegativeCheckEnforcement(pr, job, options).verified, true);
  assert.deepEqual({ pr, job, options }, before);
});

test('a generic blocked state cannot hide absent review, stale commits, draft or auto-merge', () => {
  for (const change of [pr => { pr.reviewDecision = 'REVIEW_REQUIRED'; },
    pr => { pr.headRefOid = 'c'.repeat(40); }, pr => { pr.baseRefOid = 'c'.repeat(40); },
    pr => { pr.baseRefName = 'main'; }, pr => { pr.state = 'CLOSED'; },
    pr => { pr.isDraft = true; }, pr => { pr.autoMergeRequest = {}; },
    pr => { pr.mergeStateStatus = 'CLEAN'; }, pr => { pr.mergeable = 'CONFLICTING'; },
    pr => { pr.reviewThreads.nodes.push({ isResolved: false }); },
    pr => { pr.reviewThreads.pageInfo.hasNextPage = true; }]) {
    const { pr, job, options } = rejectionFixture(); change(pr);
    assert.equal(reviewNegativeCheckEnforcement(pr, job, options).verified, false);
  }
});

test('negative enforcement fails closed for wrong check sources, incomplete inventories and additional failing gates', () => {
  for (const change of [value => { value.nodes[1].isRequired = false; },
    value => { value.nodes[1].checkSuite.app.databaseId = 456; },
    value => { value.nodes[0].conclusion = 'FAILURE'; }, value => { value.nodes[0].status = 'IN_PROGRESS'; },
    value => { value.nodes.push(structuredClone(value.nodes[1])); }, value => { value.nodes.pop(); },
    value => { value.nodes.push({ ...value.nodes[0], name: 'new-required-check' }); },
    value => { value.nodes.push({ __typename: 'StatusContext' }); },
    value => { value.pr.commits.nodes[0].commit.statusCheckRollup.contexts.pageInfo.hasNextPage = true; },
    value => { value.pr.commits.nodes[0].commit.oid = 'c'.repeat(40); }]) {
    const value = rejectionFixture(); change(value);
    assert.equal(reviewNegativeCheckEnforcement(value.pr, value.job, value.options).verified, false);
  }
});

test('negative enforcement does not count an unrelated job failure or skipped real validation as an intentional probe', () => {
  for (const change of [job => { job.url = 'https://example.test/other-job'; },
    job => { job.steps[0].conclusion = 'failure'; }, job => { job.steps[0].conclusion = 'skipped'; },
    job => { job.steps[1].name = 'Unexpected crash'; }, job => { job.steps = []; }]) {
    const { pr, job, options } = rejectionFixture(); change(job);
    assert.equal(reviewNegativeCheckEnforcement(pr, job, options).verified, false);
  }
});

test('the real develop acceptance record proves one required failure and never ships the temporary probe', () => {
  const record = JSON.parse(readFileSync(new URL('../../docs/pvad-develop-required-checks-acceptance.json', import.meta.url), 'utf8'));
  const candidate = JSON.parse(readFileSync(new URL('../../docs/pvad-github-required-checks-candidate.json', import.meta.url), 'utf8'));
  assert.equal(record.activation.branch, 'develop');
  assert.equal(record.activation.appliedAndReadBack, true);
  assert.equal(record.activation.strict, true);
  assert.deepEqual(record.activation.checks, candidate.candidateBody.checks);
  assert.ok(Object.values(record.unchanged).every(value => value === true));
  assert.equal(record.deploymentAuthorized, false);
  assert.equal(record.independentProductionApproval, false);
  const negative = record.negativeTest;
  assert.equal(negative.verdict.verified, true);
  assert.deepEqual(negative.verdict.blockers, []);
  assert.equal(negative.reviewDecision, 'APPROVED');
  assert.equal(negative.mergeStateStatus, 'BLOCKED');
  assert.equal(negative.mergeable, 'MERGEABLE');
  assert.equal(negative.reviewConversationsClear, true);
  assert.equal(negative.autoMergeDisabled, true);
  assert.equal(negative.mergeAttempted, false);
  assert.equal(negative.probeClosedWithoutMerge, true);
  assert.equal(negative.probeRemoteBranchRemoved, true);
  assert.equal(negative.runConclusion, 'failure');
  assert.equal(negative.expectedRunFailure, true);
  assert.deepEqual(negative.checks.map(({ context, app_id }) => ({ context, app_id })), record.activation.checks);
  for (const check of negative.checks) {
    assert.equal(check.isRequired, true);
    assert.equal(check.status, 'COMPLETED');
    assert.equal(check.conclusion, check.context === negative.failingContext ? 'FAILURE' : 'SUCCESS');
  }
  for (const [name, value] of Object.entries(record.evidence)) {
    if (name.endsWith('Sha256')) assert.match(value, /^[a-f0-9]{64}$/);
  }
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  assert.equal(workflow.includes(negative.intentionalStep), false);
});
