import { readFileSync, mkdirSync, writeFileSync, lstatSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
export const grafanaProviderPath = 'deploy/observability/grafana/profiles/current/dashboards.yml';
export const grafanaDatasourcePath = 'deploy/observability/grafana/provisioning/datasources/datasources.yml';
const digest = value => createHash('sha256').update(value).digest('hex');
const expectedProvider = Object.freeze({
  apiVersion: 1,
  providers: [{ name: 'PVAD', orgId: 1, folder: 'PVAD', type: 'file',
    disableDeletion: true, editable: false, updateIntervalSeconds: 30,
    options: { path: '/var/lib/grafana/dashboards' } }],
});

function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === 'object') return Object.fromEntries(
    Object.keys(value).sort().map(key => [key, canonical(value[key])]));
  return value;
}

// The provider must match the reviewed current profile exactly; any other organization, folder,
// folder UID, provisioning policy or additional provider needs a new review. Validation never edits.
export function validateGrafanaProvider(source) {
  const config = load(source);
  if (JSON.stringify(canonical(config)) !== JSON.stringify(canonical(expectedProvider)))
    throw new Error('Grafana provisioning changed; review placement and permissions before preparation.');
  return { schemaVersion: 1, deploymentAuthorized: false, sourcePath: grafanaProviderPath,
    sourceSha256: digest(source), provider: 'PVAD', folder: 'PVAD' };
}

export function grafanaReferenceInventory(dashboards, datasourceSource, ruleFiles) {
  const config = load(datasourceSource);
  const sources = config?.datasources;
  if (!Array.isArray(sources) || sources.length === 0 || !dashboards.length || !ruleFiles.length)
    throw new Error('Monitoring inventory is incomplete.');
  const identifier = value => typeof value === 'string' && /^[a-zA-Z0-9_-]{1,40}$/.test(value);
  const datasourceIds = new Set(), datasourceNames = new Set(), dashboardIds = new Set();
  for (const source of sources) {
    if (!identifier(source.uid) || typeof source.name !== 'string' || !source.name.trim()
        || typeof source.type !== 'string' || !source.type || (source.orgId !== undefined && source.orgId !== 1)
        || datasourceIds.has(source.uid) || datasourceNames.has(source.name))
      throw new Error('Invalid or duplicate datasource identity.');
    datasourceIds.add(source.uid);
    datasourceNames.add(source.name);
  }
  const deleted = config.deleteDatasources ?? [];
  if (!Array.isArray(deleted) || deleted.some(source => datasourceNames.has(source.name)))
    throw new Error('Provisioning would delete an active datasource.');

  function references(value) {
    const found = new Set();
    const visited = new WeakSet();
    function visit(node) {
      if (!node || typeof node !== 'object' || visited.has(node)) return;
      visited.add(node);
      if (Object.hasOwn(node, 'datasourceUid')) found.add(node.datasourceUid);
      if (node.datasource && typeof node.datasource === 'object') found.add(node.datasource.uid);
      if (typeof node.datasource === 'string') found.add(node.datasource);
      for (const child of Object.values(node)) visit(child);
    }
    visit(value);
    for (const uid of found)
      if (!datasourceIds.has(uid)) throw new Error('Unresolved datasource reference; review the monitoring contract.');
    return [...found].sort();
  }

  const datasourceInventory = sources.map(source => ({
    uid: source.uid, name: source.name, type: source.type,
    datasourceReferences: references(source),
  })).sort((a, b) => a.uid.localeCompare(b.uid, 'en'));
  const dashboardInventory = dashboards.map(({ path, source }) => {
    const dashboard = JSON.parse(source);
    if (!identifier(dashboard.uid) || typeof dashboard.title !== 'string' || !dashboard.title.trim()
        || dashboardIds.has(dashboard.uid)) throw new Error('Invalid or duplicate dashboard identity.');
    dashboardIds.add(dashboard.uid);
    return { path, uid: dashboard.uid, title: dashboard.title, datasourceReferences: references(dashboard) };
  }).sort((a, b) => a.path.localeCompare(b.path, 'en'));

  const alertIds = new Set(), alerts = [];
  for (const { path, source } of ruleFiles) {
    const groups = load(source)?.groups;
    if (!Array.isArray(groups) || groups.length === 0) throw new Error('Invalid alert rule groups.');
    for (const group of groups) {
      if (!Array.isArray(group.rules) || !group.rules.length || typeof group.name !== 'string')
        throw new Error('Invalid alert rule group.');
      for (const rule of group.rules) {
        if (typeof rule.alert !== 'string' || !rule.alert || alertIds.has(rule.alert))
          throw new Error('Invalid or duplicate alert identity.');
        alertIds.add(rule.alert);
        const link = rule.annotations?.dashboard_path;
        const runbookId = rule.annotations?.runbook_id ?? null;
        if (runbookId !== null && (typeof runbookId !== 'string' || !/^[a-zA-Z0-9_-]{1,80}$/.test(runbookId)))
          throw new Error('Invalid runbook reference.');
        let dashboardUid = null;
        if (link !== undefined) {
          if (typeof link !== 'string' || !link.startsWith('/d/')) throw new Error('Invalid alert dashboard link.');
          const url = new URL(link, 'https://grafana.invalid');
          dashboardUid = url.pathname.split('/')[2];
          if (url.origin !== 'https://grafana.invalid' || url.search || url.hash || !dashboardIds.has(dashboardUid))
            throw new Error('Unresolved alert dashboard link.');
        }
        alerts.push({ path, group: group.name, alert: rule.alert, dashboardUid, runbookId });
      }
    }
  }
  return {
    schemaVersion: 1, deploymentAuthorized: false, liveStateInspected: false,
    dashboards: dashboardInventory, datasources: datasourceInventory,
    alerts: alerts.sort((a, b) => a.alert.localeCompare(b.alert, 'en')),
    limits: ['source-reference-validation-only', 'not-promql-or-logql-evaluation',
      'not-live-permission-or-backup-evidence', 'operator-identities-and-saved-links-remain-unchanged'],
  };
}

export function prepareGrafanaInventory(repositoryRoot = root) {
  const tracked = execFileSync('git', ['ls-files', '-z', '--',
    'deploy/observability/grafana/dashboards', 'deploy/observability/prometheus/rules'],
  { cwd: repositoryRoot, encoding: 'utf8' }).split('\0').filter(Boolean).sort();
  const dashboardPaths = tracked.filter(path => path.startsWith('deploy/observability/grafana/dashboards/') && /\.json$/i.test(path));
  const rulePaths = tracked.filter(path => path.startsWith('deploy/observability/prometheus/rules/') && /\.ya?ml$/i.test(path));
  const boundaries = [grafanaProviderPath, grafanaDatasourcePath,
    'deploy/observability/prometheus/prometheus.yml',
    'deploy/observability/alertmanager/alertmanager.yml',
    'deploy/observability/alertmanager/alertmanager.production.yml.example',
    'deploy/observability/operator-gateway/templates/default.conf.template',
    'deploy/observability/operator-gateway/15-pvad-operator.envsh',
    'PVAD.WebApi/Controllers/AdminOperatorAccessController.cs',
    'PVAD.WebApi/Infrastructure/PvadResponseHeaders.cs'];
  const paths = [...new Set([...boundaries, ...dashboardPaths, ...rulePaths])].sort();
  const files = new Map(paths.map(path => {
    const file = resolve(repositoryRoot, path);
    if (!lstatSync(file).isFile()) throw new Error('Monitoring inventory requires regular source files.');
    return [path, readFileSync(file)];
  }));
  validateGrafanaProvider(files.get(grafanaProviderPath).toString('utf8'));
  const input = paths => paths.map(path => ({ path, source: files.get(path).toString('utf8') }));
  return {
    ...grafanaReferenceInventory(input(dashboardPaths), files.get(grafanaDatasourcePath).toString('utf8'), input(rulePaths)),
    sources: paths.map(path => ({ path, sha256: digest(files.get(path)) })),
  };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length !== 2) throw new Error('No apply or deployment arguments are supported.');
    const source = readFileSync(resolve(root, grafanaProviderPath));
    const report = validateGrafanaProvider(source.toString('utf8'));
    const inventory = JSON.stringify(prepareGrafanaInventory(root), null, 2) + '\n';
    if (digest(readFileSync(resolve(root, grafanaProviderPath))) !== report.sourceSha256)
      throw new Error('Provisioning changed during preparation; rerun after reviewing the source.');
    report.inventoryPath = 'inventory.json';
    report.inventorySha256 = digest(inventory);
    const output = resolve(root, 'artifacts/grafana-inventory');
    mkdirSync(output, { recursive: true });
    writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    writeFileSync(resolve(output, 'inventory.json'), inventory);
    console.log(JSON.stringify({ ...report, output }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
