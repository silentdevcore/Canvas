import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

function replaceRequired(source, from, to) {
  if (typeof source !== 'string' || !source.includes(from))
    throw new Error('Workflow shape changed; review the container cutover candidate manually.');
  return source.replaceAll(from, () => to);
}

export function prepareContainerWorkflows(releaseSource, deploymentSource) {
  const release = structuredClone(releaseSource);
  const deployment = structuredClone(deploymentSource);
  const publisher = 'ghcr.io/${{ github.repository_owner }}';
  for (const component of ['webapi', 'observability-webhook-relay']) {
    const steps = release.jobs.release.steps;
    const builds = steps.filter(step => step.with?.file === (component === 'webapi'
      ? 'PVAD.WebApi/Dockerfile' : 'src/Observability/PVAD.Observability.WebhookRelay/Dockerfile'));
    if (builds.length !== 1 || builds[0].with.push !== true) throw new Error('Expected one publishing step per image.');
    const build = builds[0];
    const oldImage = `${publisher}/pxa-${component}:`;
    const newImage = `${publisher}/pvad-${component}:`;
    build.with.tags = replaceRequired(build.with.tags, oldImage, newImage);
    const verifications = steps.filter(step => step.run?.includes(`docker pull ${oldImage}`));
    if (verifications.length !== 1) throw new Error('Missing or duplicate matching published-image verification step.');
    verifications[0].run = replaceRequired(verifications[0].run, oldImage, newImage);
  }

  deployment.on.workflow_dispatch.inputs.container_identity = {
    description: 'Exact image family published for this immutable release; never infer it from archive names',
    required: true,
    default: 'PXA',
    type: 'choice',
    options: ['PXA', 'PVAD'],
  };
  const inspections = deployment.jobs.preflight.steps.filter(step => step.name === 'Verify immutable container tags');
  if (inspections.length !== 1) throw new Error('Missing or duplicate immutable container inspection step.');
  const inspect = inspections[0];
  inspect.env.CONTAINER_IDENTITY = '${{ inputs.container_identity }}';
  inspect.run = replaceRequired(inspect.run,
    'WEBAPI="ghcr.io/$OWNER/pxa-webapi:$VERSION"\nRELAY="ghcr.io/$OWNER/pxa-observability-webhook-relay:$VERSION"',
    'case "$CONTAINER_IDENTITY" in\n  PVAD) PREFIX=pvad ;;\n  PXA) PREFIX=pxa ;;\n  *) echo "Unknown container identity." >&2; exit 1 ;;\nesac\n'
    + 'WEBAPI="ghcr.io/$OWNER/$PREFIX-webapi:$VERSION"\nRELAY="ghcr.io/$OWNER/$PREFIX-observability-webhook-relay:$VERSION"');

  for (const workflow of [release, deployment]) {
    if (/\/pxa-(?:webapi|observability-webhook-relay):/.test(JSON.stringify(workflow)))
      throw new Error('Unmapped image consumer remains; review the container cutover candidate manually.');
  }

  return {
    schemaVersion: 1,
    scope: 'Offline source candidate only; not approval to publish, deploy or change protected GitHub configuration.',
    deploymentAuthorized: false,
    blockers: ['publisher-ownership-and-repositories-unapproved', 'immutable-version-image-family-review-required',
      'workflow-and-environment-owner-approval-required', 'registry-access-and-staging-validation-required'],
    retainedBindings: ['workflow-concurrency-groups', 'protected-environment-names', 'action-pins', 'token-permissions'],
    releaseWorkflow: release,
    deploymentWorkflow: deployment,
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length !== 2) throw new Error('No deployment or publishing arguments are supported.');
    const result = prepareContainerWorkflows(
      load(readFileSync(resolve(root, '.github/workflows/release.yml'), 'utf8')),
      load(readFileSync(resolve(root, '.github/workflows/deployment-validation.yml'), 'utf8')));
    const output = resolve(root, 'artifacts/rename/container-workflows');
    mkdirSync(output, { recursive: true });
    for (const [name, workflow] of [['release', result.releaseWorkflow], ['deployment-validation', result.deploymentWorkflow]])
      writeFileSync(resolve(output, `${name}.candidate.yml`), dump(workflow, { lineWidth: -1, noRefs: true }));
    const { releaseWorkflow, deploymentWorkflow, ...report } = result;
    writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ ...report, output }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
