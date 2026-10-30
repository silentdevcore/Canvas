import assert from 'node:assert/strict';

const names = values => {
  assert.ok(Array.isArray(values) && values.every(value => typeof value === 'string' && value.trim().length > 0));
  assert.equal(new Set(values).size, values.length, 'Duplicate check names require review.');
  return values;
};

// A static name match is supporting evidence, never proof that a check ran or blocked a merge.
export function reviewCiChecks(workflow, requiredStatusChecks, { branch, proposed = [] } = {}) {
  assert.ok(['main', 'develop'].includes(branch), 'An explicit protected target branch is required.');
  assert.ok(workflow?.jobs && typeof workflow.jobs === 'object' && !Array.isArray(workflow.jobs));
  names(proposed);
  const result = { branch, deploymentAuthorized: false, protectionChangeAuthorized: false,
    runtimeChecksVerified: false, scope: 'Static CI name and dependency review only; not effective ruleset or merge-enforcement verification.',
    blockers: [], required: [], proposed: [], nonRequiredJobs: [] };
  const trigger = workflow.on?.pull_request;
  if (!Object.hasOwn(workflow.on ?? {}, 'pull_request') || Array.isArray(trigger) || (trigger !== null && typeof trigger !== 'object'))
    result.blockers.push('pull-request-trigger-review-required');
  else if (trigger && (Object.keys(trigger).some(key => key !== 'branches') ||
    (trigger.branches !== undefined && (!Array.isArray(trigger.branches) || !trigger.branches.includes(branch)))))
    result.blockers.push('pull-request-filter-review-required');

  if (!requiredStatusChecks) {
    result.blockers.push('required-check-metadata-unavailable');
    result.requiredChecksResolve = false;
    result.proposedChecksResolve = false;
    return result;
  }
  assert.equal(typeof requiredStatusChecks.strict, 'boolean');
  const required = names(requiredStatusChecks.contexts);
  assert.ok(Array.isArray(requiredStatusChecks.checks), 'Explicit check-source metadata is required.');
  const sources = new Map();
  for (const check of requiredStatusChecks.checks) {
    assert.ok(check && typeof check.context === 'string' && !sources.has(check.context));
    assert.ok(check.app_id === null || (Number.isSafeInteger(check.app_id) && check.app_id > 0));
    sources.set(check.context, check.app_id);
  }
  assert.deepEqual([...sources.keys()].sort(), [...required].sort(), 'Context and source inventories differ.');
  if (required.length === 0) result.blockers.push('no-required-checks');
  result.strict = requiredStatusChecks.strict;
  const jobs = Object.entries(workflow.jobs);
  const contexts = new Map();
  for (const [id, job] of jobs) {
    const context = job.name ?? id;
    assert.equal(typeof context, 'string');
    contexts.set(context, [...(contexts.get(context) ?? []), id]);
  }
  function inspect(context) {
    const matches = contexts.get(context) ?? [];
    const row = { context, jobId: matches.length === 1 ? matches[0] : null, blockers: [], dependencies: [] };
    if (matches.length !== 1) {
      row.blockers.push(matches.length ? 'ambiguous-job-name' : 'check-name-not-found');
      return row;
    }
    const visited = new Set();
    function walk(id, stack) {
      if (stack.has(id)) { row.blockers.push('dependency-cycle'); return; }
      if (visited.has(id)) return;
      visited.add(id);
      const job = workflow.jobs[id];
      if (!job) { row.blockers.push('unknown-dependency'); return; }
      if (job.name?.includes('${{') || job.strategy?.matrix) row.blockers.push('dynamic-check-name-review-required');
      if (job.if !== undefined) row.blockers.push('conditional-job-review-required');
      if (job['continue-on-error'] !== undefined && job['continue-on-error'] !== false)
        row.blockers.push('continue-on-error-review-required');
      if (job.uses) row.blockers.push('reusable-workflow-review-required');
      const dependencies = typeof job.needs === 'string' ? [job.needs] : job.needs ?? [];
      names(dependencies);
      for (const dependency of dependencies) {
        if (!row.dependencies.includes(dependency)) row.dependencies.push(dependency);
        walk(dependency, new Set([...stack, id]));
      }
    }
    walk(matches[0], new Set());
    row.dependencies.sort();
    row.blockers = [...new Set(row.blockers)].sort();
    return row;
  }
  result.required = required.map(context => ({ ...inspect(context), appId: sources.get(context) }));
  result.proposed = proposed.map(inspect);
  result.nonRequiredJobs = jobs.filter(([id, job]) => !required.includes(job.name ?? id))
    .map(([id, job]) => ({ jobId: id, context: job.name ?? id, conditional: job.if !== undefined }));
  result.requiredChecksResolve = result.blockers.length === 0 && result.required.every(row => row.blockers.length === 0);
  result.proposedChecksResolve = result.blockers.length === 0 && result.proposed.every(row => row.blockers.length === 0);
  return result;
}

export function reviewNegativeCheckEnforcement(pr, failedJob, {
  expectedHead, expectedBase, requiredChecks, failingContext, intentionalStep,
}) {
  names(requiredChecks.map(check => check.context));
  assert.ok(requiredChecks.some(check => check.context === failingContext));
  assert.match(expectedHead, /^[a-f0-9]{40}$/);
  assert.match(expectedBase, /^[a-f0-9]{40}$/);
  const blockers = [];
  if (pr.state !== 'OPEN' || pr.isDraft !== false || pr.autoMergeRequest !== null)
    blockers.push('probe-not-open-non-draft-without-auto-merge');
  if (pr.headRefOid !== expectedHead || pr.baseRefOid !== expectedBase || pr.baseRefName !== 'develop')
    blockers.push('probe-revision-or-base-mismatch');
  if (pr.reviewDecision !== 'APPROVED') blockers.push('probe-review-not-approved');
  if (pr.mergeStateStatus !== 'BLOCKED') blockers.push('probe-not-merge-blocked');
  if (pr.mergeable !== 'MERGEABLE') blockers.push('probe-conflict-state-not-clear');
  if (pr.reviewThreads?.pageInfo?.hasNextPage !== false ||
      !Array.isArray(pr.reviewThreads?.nodes) || pr.reviewThreads.nodes.some(thread => thread.isResolved !== true))
    blockers.push('probe-review-conversations-not-clear');
  const commits = pr.commits?.nodes ?? [];
  const commit = commits.length === 1 ? commits[0].commit : null;
  const inventory = commit?.statusCheckRollup?.contexts;
  if (commit?.oid !== expectedHead || inventory?.pageInfo?.hasNextPage !== false)
    blockers.push('probe-check-inventory-incomplete');
  const runs = (inventory?.nodes ?? []).filter(check => check.__typename === 'CheckRun');
  if ((inventory?.nodes ?? []).some(check => check.__typename !== 'CheckRun'))
    blockers.push('non-check-run-context-review-required');
  for (const expected of requiredChecks) {
    const matches = runs.filter(check => check.name === expected.context);
    if (matches.length !== 1) { blockers.push(`missing-or-duplicate-check:${expected.context}`); continue; }
    const check = matches[0];
    if (!Number.isSafeInteger(expected.app_id) || expected.app_id < 1 ||
        check.checkSuite?.app?.databaseId !== expected.app_id || check.isRequired !== true)
      blockers.push(`required-source-mismatch:${expected.context}`);
    const conclusion = expected.context === failingContext ? 'FAILURE' : 'SUCCESS';
    if (check.status !== 'COMPLETED' || check.conclusion !== conclusion)
      blockers.push(`unexpected-check-result:${expected.context}`);
  }
  if (runs.some(check => check.isRequired && !requiredChecks.some(expected => expected.context === check.name)))
    blockers.push('unreviewed-additional-required-check');
  const failure = runs.find(check => check.name === failingContext);
  if (failedJob.name !== failingContext || failedJob.status !== 'completed' ||
      failedJob.conclusion !== 'failure' || !failedJob.url || failedJob.url !== failure?.detailsUrl)
    blockers.push('intentional-failure-job-mismatch');
  const steps = failedJob.steps ?? [];
  const failed = steps.filter(step => step.conclusion === 'failure');
  const index = steps.findIndex(step => step.name === intentionalStep);
  if (index < 1 || failed.length !== 1 || failed[0].name !== intentionalStep ||
      steps.some(step => step.status !== 'completed') ||
      steps.slice(0, index).some(step => step.conclusion !== 'success'))
    blockers.push('failure-not-isolated-to-intentional-step');
  return {
    verified: blockers.length === 0,
    blockers: [...new Set(blockers)].sort(),
    scope: 'Read-only controlled PR rejection evidence; no merge attempt, bypass or production approval.',
  };
}
