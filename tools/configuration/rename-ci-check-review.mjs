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
