import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

const environmentNames = ['staging', 'production'];
const hash = value => createHash('sha256').update(value).digest('hex');
const ordered = values => [...values].sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right), 'en'));
function knownKeys(value, keys) {
  assert.ok(value && typeof value === 'object' && !Array.isArray(value));
  assert.ok(Object.keys(value).every(key => keys.includes(key)), 'Unexpected snapshot fields require review.');
}

function environmentControls(environment) {
  knownKeys(environment, ['name', 'canAdminsBypass', 'protectionRules', 'deploymentBranchPolicy',
    'deploymentBranchPolicies', 'secretNames', 'variableNames']);
  assert.equal(typeof environment.canAdminsBypass, 'boolean');
  assert.ok(Array.isArray(environment.protectionRules));
  const ruleTypes = new Set();
  const rules = environment.protectionRules.map(rule => {
    knownKeys(rule, rule.type === 'required_reviewers' ? ['type', 'id', 'prevent_self_review', 'reviewers']
      : rule.type === 'wait_timer' ? ['type', 'id', 'wait_timer'] : ['type', 'id']);
    if (rule.id !== undefined) assert.ok(Number.isSafeInteger(rule.id) && rule.id > 0);
    assert.ok(!ruleTypes.has(rule.type), 'Duplicate protection rule.');
    ruleTypes.add(rule.type);
    if (rule.type === 'required_reviewers') {
      assert.equal(typeof rule.prevent_self_review, 'boolean');
      assert.ok(Array.isArray(rule.reviewers) && rule.reviewers.length > 0);
      return { type: rule.type, preventSelfReview: rule.prevent_self_review,
        reviewers: ordered(rule.reviewers.map(value => {
          knownKeys(value, ['type', 'reviewer']);
          knownKeys(value.reviewer, ['id']);
          assert.ok(['User', 'Team'].includes(value.type));
          assert.ok(Number.isSafeInteger(value.reviewer?.id) && value.reviewer.id > 0);
          return { type: value.type, id: value.reviewer.id };
        })) };
    }
    if (rule.type === 'wait_timer') {
      assert.ok(Number.isInteger(rule.wait_timer) && rule.wait_timer >= 0 && rule.wait_timer <= 43_200);
      return { type: rule.type, minutes: rule.wait_timer };
    }
    assert.equal(rule.type, 'branch_policy', 'Unsupported protection rule requires manual review.');
    return { type: rule.type };
  });
  const policy = environment.deploymentBranchPolicy;
  if (policy !== null) {
    knownKeys(policy, ['protected_branches', 'custom_branch_policies']);
    assert.equal(typeof policy?.protected_branches, 'boolean');
    assert.equal(typeof policy.custom_branch_policies, 'boolean');
    assert.notEqual(policy.protected_branches, policy.custom_branch_policies);
  }
  assert.ok(Array.isArray(environment.deploymentBranchPolicies));
  const patterns = environment.deploymentBranchPolicies.map(value => {
    knownKeys(value, ['name', 'type']);
    assert.ok(['branch', 'tag'].includes(value.type));
    assert.equal(typeof value.name, 'string');
    assert.ok(value.name.length > 0);
    return { type: value.type, name: value.name };
  });
  assert.ok(policy?.custom_branch_policies ? patterns.length > 0 : patterns.length === 0);
  for (const key of ['secretNames', 'variableNames']) {
    assert.ok(Array.isArray(environment[key]));
    assert.ok(environment[key].every(value => typeof value === 'string' && /^[A-Za-z_][A-Za-z0-9_]*$/.test(value)));
    assert.equal(new Set(environment[key]).size, environment[key].length);
  }
  return { canAdminsBypass: environment.canAdminsBypass, rules: ordered(rules),
    branchPolicy: policy === null ? null : { protectedBranches: policy.protected_branches,
      customBranchPolicies: policy.custom_branch_policies }, patterns: ordered(patterns) };
}

export function reviewProtectedEnvironments(snapshot, { repository, now = Date.now() } = {}) {
  const result = { deploymentAuthorized: false, protectionComparisonPassed: false,
    configurationValuesVerified: false, operatorApprovalRequired: true,
    scope: 'Offline comparison only; supplied metadata is not authenticated GitHub state or permission to activate bindings.',
    blockers: [], environments: [] };
  if (snapshot == null) {
    result.blockers.push('protected-environment-snapshot-required');
    return result;
  }
  knownKeys(snapshot, ['schemaVersion', 'repository', 'capturedAt', 'paginationComplete', 'workflowRuns', 'environments']);
  assert.equal(snapshot.schemaVersion, 1);
  assert.match(snapshot.repository, /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/);
  assert.equal(typeof repository, 'string', 'An expected repository is required.');
  assert.equal(snapshot.repository.toLowerCase(), repository.toLowerCase(), 'Snapshot repository does not match the target repository.');
  assert.equal(typeof snapshot.capturedAt, 'string');
  assert.match(snapshot.capturedAt, /^\d{4}-\d{2}-\d{2}T[\d:.]+Z$/);
  const age = now - Date.parse(snapshot.capturedAt);
  assert.ok(Number.isFinite(age));
  if (age < 0 || age > 86_400_000) result.blockers.push('snapshot-stale-or-future');
  assert.equal(typeof snapshot.paginationComplete, 'boolean');
  if (!snapshot.paginationComplete) result.blockers.push('snapshot-pagination-incomplete');
  assert.ok(Array.isArray(snapshot.workflowRuns));
  for (const run of snapshot.workflowRuns) knownKeys(run, ['id', 'status']);
  assert.ok(snapshot.workflowRuns.every(run => Number.isSafeInteger(run.id) && run.id > 0
    && typeof run.status === 'string' && run.status.length > 0));
  const activeRuns = snapshot.workflowRuns.filter(run => run.status !== 'completed');
  result.activeRunCount = activeRuns.length;
  if (activeRuns.length > 0) result.blockers.push('workflow-generations-not-drained');
  assert.ok(Array.isArray(snapshot.environments));
  const environments = new Map();
  for (const environment of snapshot.environments) {
    assert.ok(!environments.has(environment.name), 'Duplicate environment snapshot.');
    assert.ok(environmentNames.some(name => [`pxa-${name}`, `pvad-${name}`].includes(environment.name)),
      'Unexpected environment snapshot.');
    environments.set(environment.name, { value: environment, controls: environmentControls(environment) });
  }
  result.repository = snapshot.repository;
  result.capturedAt = snapshot.capturedAt;
  for (const name of environmentNames) {
    const source = environments.get(`pxa-${name}`), target = environments.get(`pvad-${name}`);
    const review = { source: `pxa-${name}`, target: `pvad-${name}`, blockers: [] };
    if (!source) review.blockers.push('source-snapshot-required');
    if (!target) review.blockers.push('target-snapshot-required');
    if (source && target) {
      review.sourceControlHash = hash(JSON.stringify(source.controls));
      review.targetControlHash = hash(JSON.stringify(target.controls));
      if (review.sourceControlHash !== review.targetControlHash) review.blockers.push('protection-controls-differ');
      for (const key of ['secretNames', 'variableNames']) {
        if (JSON.stringify(ordered(source.value[key])) !== JSON.stringify(ordered(target.value[key])))
          review.blockers.push(`${key === 'secretNames' ? 'secret' : 'variable'}-inventory-differs`);
      }
    }
    result.environments.push(review);
  }
  result.protectionComparisonPassed = result.blockers.length === 0 && result.environments.every(value => value.blockers.length === 0);
  return result;
}

function replaceRequired(source, from, to) {
  if (typeof source !== 'string' || !source.includes(from))
    throw new Error('Workflow shape changed; review the container cutover candidate manually.');
  return source.replaceAll(from, () => to);
}

export function prepareContainerWorkflows(releaseSource, deploymentSource, environmentSnapshot = null, environmentReviewOptions = {}) {
  const release = structuredClone(releaseSource);
  const deployment = structuredClone(deploymentSource);
  const configured = release.env?.PVAD_CONTAINER_PUBLISHER;
  if (configured !== undefined) {
    assert.equal(configured, 'silentdevcore', 'Unexpected configured container publisher.');
    assert.equal(deployment.env?.PVAD_CONTAINER_PUBLISHER, configured, 'Workflow publishers must match.');
  } else assert.equal(deployment.env?.PVAD_CONTAINER_PUBLISHER, undefined, 'Workflow publishers must match.');
  const publisher = configured ? 'ghcr.io/${{ env.PVAD_CONTAINER_PUBLISHER }}' : 'ghcr.io/${{ github.repository_owner }}';
  const sourceFamilies = new Set();
  for (const component of ['webapi', 'observability-webhook-relay']) {
    const steps = release.jobs.release.steps;
    const builds = steps.filter(step => step.with?.file === (component === 'webapi'
      ? 'PVAD.WebApi/Dockerfile' : 'src/Observability/PVAD.Observability.WebhookRelay/Dockerfile'));
    if (builds.length !== 1 || builds[0].with.push !== true) throw new Error('Expected one publishing step per image.');
    const build = builds[0];
    const oldImage = `${publisher}/pxa-${component}:`;
    const newImage = `${publisher}/pvad-${component}:`;
    assert.equal(typeof build.with.tags, 'string', 'Missing image tags.');
    const family = build.with.tags.includes(oldImage) ? 'PXA' : 'PVAD';
    const sourceImage = family === 'PXA' ? oldImage : newImage;
    sourceFamilies.add(family);
    assert.deepEqual(build.with.tags.trim().split('\n'), ['${{ steps.version.outputs.version }}', '${{ steps.version.outputs.minor }}', 'latest']
      .map(tag => `${sourceImage}${tag}`), 'Unexpected publisher, mixed image tags or incomplete version tags.');
    const verifications = steps.filter(step => step.run?.includes(`docker pull ${sourceImage}`));
    if (verifications.length !== 1) throw new Error('Missing or duplicate matching published-image verification step.');
    assert.ok(verifications[0].run.includes('node tools/versioning/pvad-build-consistency.mjs verify-container-metadata'),
      'Missing immutable metadata verification.');
    assert.equal(verifications[0].run.split(`${sourceImage}\${{ steps.version.outputs.version }}`).length, 3,
      'Pull and metadata verification must use the same immutable version.');
    if (family === 'PXA') {
      build.with.tags = replaceRequired(build.with.tags, oldImage, newImage);
      verifications[0].run = replaceRequired(verifications[0].run, oldImage, newImage);
    }
  }
  assert.equal(sourceFamilies.size, 1, 'Mixed image families require explicit review.');

  const identityInput = {
    description: 'Exact image family published for this immutable release; never infer it from archive names',
    required: true,
    default: 'PXA',
    type: 'choice',
    options: ['PXA', 'PVAD'],
  };
  if (deployment.on.workflow_dispatch.inputs.container_identity !== undefined)
    assert.deepEqual(deployment.on.workflow_dispatch.inputs.container_identity, identityInput, 'Unexpected image-family input.');
  else deployment.on.workflow_dispatch.inputs.container_identity = identityInput;
  const inspections = deployment.jobs.preflight.steps.filter(step => step.name === 'Verify immutable container tags');
  if (inspections.length !== 1) throw new Error('Missing or duplicate immutable container inspection step.');
  const inspect = inspections[0];
  const legacyInspection = 'WEBAPI="ghcr.io/$OWNER/pxa-webapi:$VERSION"\nRELAY="ghcr.io/$OWNER/pxa-observability-webhook-relay:$VERSION"';
  const canonicalInspection = 'case "$CONTAINER_IDENTITY" in\n  PVAD) PREFIX=pvad ;;\n  PXA) PREFIX=pxa ;;\n  *) echo "Unknown container identity." >&2; exit 1 ;;\nesac\n'
    + 'WEBAPI="ghcr.io/$OWNER/$PREFIX-webapi:$VERSION"\nRELAY="ghcr.io/$OWNER/$PREFIX-observability-webhook-relay:$VERSION"';
  assert.equal(inspect.env.OWNER, configured ? '${{ env.PVAD_CONTAINER_PUBLISHER }}' : '${{ github.repository_owner }}');
  if (inspect.run.includes(legacyInspection)) {
    assert.equal(inspect.env.CONTAINER_IDENTITY, undefined);
    inspect.env.CONTAINER_IDENTITY = '${{ inputs.container_identity }}';
    inspect.run = replaceRequired(inspect.run, legacyInspection, canonicalInspection);
  } else {
    assert.ok(inspect.run.includes(canonicalInspection), 'Workflow shape changed; review the container cutover candidate manually.');
    assert.equal(inspect.env.CONTAINER_IDENTITY, '${{ inputs.container_identity }}');
  }

  for (const workflow of [release, deployment]) {
    if (/\/pxa-(?:webapi|observability-webhook-relay):/.test(JSON.stringify(workflow)))
      throw new Error('Unmapped image consumer remains; review the container cutover candidate manually.');
  }

  return {
    schemaVersion: 1,
    scope: 'Offline source candidate only; not approval to publish, deploy or change protected GitHub configuration.',
    deploymentAuthorized: false,
    containerPublisher: { configuredNamespace: configured ? `ghcr.io/${configured}` : null, registryAccessVerified: false },
    blockers: [configured ? 'registry-ownership-and-access-unverified' : 'publisher-ownership-and-repositories-unapproved', 'immutable-version-image-family-review-required',
      'workflow-and-environment-owner-approval-required', 'registry-access-and-staging-validation-required'],
    retainedBindings: ['workflow-concurrency-groups', 'protected-environment-names', 'action-pins', 'token-permissions'],
    protectedEnvironmentReview: reviewProtectedEnvironments(environmentSnapshot, environmentReviewOptions),
    releaseWorkflow: release,
    deploymentWorkflow: deployment,
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const args = process.argv.slice(2);
    if (args.length !== 0 && !(args.length === 2 && args[0] === '--environment-snapshot' && !args[1].startsWith('-')))
      throw new Error('No deployment or publishing arguments are supported; only --environment-snapshot <local-json> is allowed.');
    const output = resolve(root, 'artifacts/rename/container-workflows');
    mkdirSync(output, { recursive: true });
    writeFileSync(resolve(output, 'report.json'), JSON.stringify({ status: 'failed', deploymentAuthorized: false,
      reason: 'Candidate generation has not completed.' }, null, 2) + '\n');
    for (const name of ['release.candidate.yml', 'deployment-validation.candidate.yml', 'deployment-validation.environments.candidate.yml'])
      rmSync(resolve(output, name), { force: true });
    const releaseSource = readFileSync(resolve(root, '.github/workflows/release.yml'), 'utf8');
    const deploymentSource = readFileSync(resolve(root, '.github/workflows/deployment-validation.yml'), 'utf8');
    const environmentSource = args.length ? readFileSync(resolve(args[1]), 'utf8') : null;
    let repository;
    if (environmentSource !== null) {
      const origin = execFileSync('git', ['remote', 'get-url', 'origin'], { cwd: root, encoding: 'utf8' }).trim();
      const match = origin.match(/^(?:https:\/\/github\.com\/|git@github\.com:)([A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+?)(?:\.git)?$/);
      assert.ok(match, 'Expected a GitHub origin to bind the local environment snapshot.');
      repository = match[1];
    }
    const result = prepareContainerWorkflows(
      load(releaseSource), load(deploymentSource), environmentSource === null ? null : JSON.parse(environmentSource), { repository });
    for (const [name, workflow] of [['release', result.releaseWorkflow], ['deployment-validation', result.deploymentWorkflow]])
      writeFileSync(resolve(output, `${name}.candidate.yml`), dump(workflow, { lineWidth: -1, noRefs: true }));
    const { releaseWorkflow, deploymentWorkflow, ...report } = result;
    const environmentCandidate = resolve(output, 'deployment-validation.environments.candidate.yml');
    if (report.protectedEnvironmentReview.protectionComparisonPassed) {
      const candidate = structuredClone(deploymentWorkflow);
      assert.equal(candidate.jobs['validate-environment'].environment.name, 'pxa-${{ inputs.environment }}');
      candidate.jobs['validate-environment'].environment.name = 'pvad-${{ inputs.environment }}';
      writeFileSync(environmentCandidate, dump(candidate, { lineWidth: -1, noRefs: true }));
    }
    report.status = 'prepared-not-authorized';
    report.sourceHashes = { release: hash(releaseSource), deployment: hash(deploymentSource),
      environmentSnapshot: environmentSource === null ? null : hash(environmentSource) };
    report.candidateHashes = Object.fromEntries(['release.candidate.yml', 'deployment-validation.candidate.yml',
      ...(report.protectedEnvironmentReview.protectionComparisonPassed ? ['deployment-validation.environments.candidate.yml'] : [])]
      .map(name => [name, hash(readFileSync(resolve(output, name)))]));
    writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ ...report, output }));
  } catch {
    console.error('Container workflow preparation failed; verify workflow structure and local snapshot metadata. No deployment or publishing arguments are supported.');
    process.exitCode = 1;
  }
}
