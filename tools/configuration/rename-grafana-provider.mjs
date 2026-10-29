import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const sourcePath = 'deploy/observability/grafana/provisioning/dashboards/dashboards.yml';

export function prepareGrafanaProvider(source) {
  const config = load(source);
  const expected = {
    apiVersion: 1,
    providers: [{ name: 'PXA', orgId: 1, folder: 'PXA', type: 'file',
      disableDeletion: true, editable: false, updateIntervalSeconds: 30,
      options: { path: '/var/lib/grafana/dashboards' } }],
  };
  // A changed folder UID or provisioning policy requires a new migration review.
  function canonical(value) {
    if (Array.isArray(value)) return value.map(canonical);
    if (value && typeof value === 'object') return Object.fromEntries(
      Object.keys(value).sort().map(key => [key, canonical(value[key])]));
    return value;
  }
  if (JSON.stringify(canonical(config)) !== JSON.stringify(canonical(expected)))
    throw new Error('Grafana provisioning changed; review placement and permissions before preparation.');
  const candidate = structuredClone(config);
  candidate.providers[0].name = 'PVAD';
  candidate.providers[0].folder = 'PVAD';
  return {
    candidate,
    originalSource: source,
    report: {
      schemaVersion: 1,
      deploymentAuthorized: false,
      sourcePath,
      sourceSha256: createHash('sha256').update(source).digest('hex'),
      changes: ['provider-name', 'dashboard-folder-name'],
      retained: ['organization-id', 'dashboard-uids', 'datasource-uids', 'provisioning-policy'],
      blockers: ['grafana-state-backup-and-restore-drill', 'folder-permission-inventory-and-approval',
        'dashboard-placement-and-link-validation', 'staging-cutover-and-rollback-test'],
      warning: 'Original provisioning is not a database backup. Restoring it does not restore folder permissions or persisted dashboard placement.',
    },
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length !== 2) throw new Error('No apply or deployment arguments are supported.');
    const result = prepareGrafanaProvider(readFileSync(resolve(root, sourcePath), 'utf8'));
    const output = resolve(root, 'artifacts/rename/grafana-provider');
    mkdirSync(output, { recursive: true });
    writeFileSync(resolve(output, 'dashboards.candidate.yml'), dump(result.candidate, { noRefs: true }));
    writeFileSync(resolve(output, 'dashboards.original.yml'), result.originalSource);
    writeFileSync(resolve(output, 'report.json'), JSON.stringify(result.report, null, 2) + '\n');
    console.log(JSON.stringify({ ...result.report, output }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
