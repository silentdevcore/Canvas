import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { grafanaProviderPath, grafanaReferenceInventory, prepareGrafanaInventory, validateGrafanaProvider } from './grafana-inventory.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(new URL('../../pvad-designer/package.json', import.meta.url))('js-yaml');
const source = readFileSync(new URL('../../' + grafanaProviderPath, import.meta.url), 'utf8');

test('the current Grafana provider is canonical and validating it is idempotent and read-only', () => {
  const report = validateGrafanaProvider(source);
  assert.equal(load(source).providers[0].name, 'PVAD');
  assert.equal(load(source).providers[0].folder, 'PVAD');
  assert.equal(report.sourcePath, grafanaProviderPath);
  assert.equal(report.sourceSha256, createHash('sha256').update(source).digest('hex'));
  assert.equal(report.deploymentAuthorized, false);
  assert.deepEqual(validateGrafanaProvider(source), report);
  const reserialized = dump(load(source));
  assert.equal(validateGrafanaProvider(reserialized).sourceSha256, createHash('sha256').update(reserialized).digest('hex'));
  assert.equal(readFileSync(new URL('../../' + grafanaProviderPath, import.meta.url), 'utf8'), source);
});

test('Compose mounts exactly the validated provider and no other provider generation remains', () => {
  const compose = load(readFileSync(new URL('../../docker-compose.yml', import.meta.url), 'utf8'));
  const mounts = compose.services['pvad-grafana'].volumes.filter(volume => String(volume).includes('/etc/grafana/provisioning/dashboards'));
  assert.deepEqual(mounts, [`./${grafanaProviderPath}:/etc/grafana/provisioning/dashboards/dashboards.yml:ro`]);
  const providers = execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '--', 'deploy/observability/grafana'],
    { cwd: root, encoding: 'utf8' }).split('\n').filter(path => path.endsWith('dashboards.yml') && existsSync(new URL('../../' + path, import.meta.url)));
  assert.deepEqual(providers, [grafanaProviderPath]);
});

test('unexpected Grafana provider, folder, folder UID, policy or additional provider fails closed', () => {
  const mutations = [
    config => { config.providers[0].name = 'another-provider'; },
    config => { config.providers[0].folder = 'customer-folder'; },
    config => { config.providers[0].folderUid = 'existing-folder'; },
    config => { config.providers[0].disableDeletion = false; },
    config => { config.providers[0].editable = true; },
    config => { config.providers[0].orgId = 2; },
    config => { config.providers.push(structuredClone(config.providers[0])); },
    config => { config.providers[0].options.path = '/another/path'; },
  ];
  for (const mutate of mutations) {
    const config = load(source);
    mutate(config);
    assert.throws(() => validateGrafanaProvider(dump(config)), /provisioning changed/);
  }
  assert.throws(() => validateGrafanaProvider('null'), /provisioning changed/);
  assert.throws(() => validateGrafanaProvider('providers: ['));
});

test('Grafana inventory has no apply command', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/grafana-inventory.mjs', '--apply'],
    { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No apply or deployment arguments/);
});

function syntheticInventory(mutate = () => {}) {
  const fixture = {
    sources: { datasources: [
      { uid: 'metrics', name: 'PVAD Metrics', type: 'prometheus', jsonData: { traces: { datasourceUid: 'traces' } } },
      { uid: 'traces', name: 'PVAD Traces', type: 'tempo' },
    ] },
    dashboards: [{ uid: 'overview', title: 'PVADE Overview', panels: [
      { panels: [{ targets: [{ datasource: { uid: 'metrics' } }] }] },
    ] }],
    rules: { groups: [{ name: 'pvad-platform', rules: [
      { alert: 'PvadSyntheticAlert', expr: 'up == 0', annotations: { dashboard_path: '/d/overview/platform', runbook_id: 'synthetic-runbook' } },
      { alert: 'PvadNoDashboardAlert', expr: 'up == 0' },
    ] }] },
  };
  mutate(fixture);
  return grafanaReferenceInventory(fixture.dashboards.map((dashboard, i) => ({ path: `dashboard-${i}.json`, source: JSON.stringify(dashboard) })),
    dump(fixture.sources), [{ path: 'rules.yml', source: dump(fixture.rules) }]);
}

test('inventory resolves nested dashboard, datasource and alert references without copying query or connection settings', () => {
  const result = syntheticInventory(fixture => {
    fixture.sources.datasources[0].url = 'https://must-not-be-exported.invalid';
    fixture.sources.datasources[0].secureJsonData = { password: 'synthetic-not-a-real-secret' };
  });
  assert.deepEqual(result.dashboards[0].datasourceReferences, ['metrics']);
  assert.deepEqual(result.datasources[0].datasourceReferences, ['traces']);
  assert.equal(result.alerts.find(alert => alert.alert === 'PvadSyntheticAlert').dashboardUid, 'overview');
  assert.equal(result.alerts.find(alert => alert.alert === 'PvadNoDashboardAlert').dashboardUid, null);
  assert.equal(result.deploymentAuthorized, false);
  assert.equal(result.liveStateInspected, false);
  assert.doesNotMatch(JSON.stringify(result), /must-not-be-exported|synthetic-not-a-real-secret|up == 0/);
});

test('inventory rejects duplicate identities, broken nested references, active deletions and cross-organization sources', () => {
  const mutations = [
    fixture => { fixture.sources.datasources.push(structuredClone(fixture.sources.datasources[0])); },
    fixture => { fixture.sources.datasources[1].name = fixture.sources.datasources[0].name; },
    fixture => { fixture.sources.datasources[0].orgId = 2; },
    fixture => { fixture.sources.deleteDatasources = [{ name: 'PVAD Metrics', orgId: 1 }]; },
    fixture => { fixture.sources.datasources[0].jsonData.traces.datasourceUid = 'missing'; },
    fixture => { fixture.dashboards[0].panels[0].panels[0].targets[0].datasource.uid = 'missing'; },
    fixture => { fixture.dashboards.push(structuredClone(fixture.dashboards[0])); },
    fixture => { fixture.dashboards[0].uid = ''; },
    fixture => { fixture.rules.groups[0].rules.push(structuredClone(fixture.rules.groups[0].rules[0])); },
    fixture => { fixture.rules.groups[0].rules[0].annotations.dashboard_path = '/d/missing/dashboard'; },
    fixture => { fixture.rules.groups[0].rules[0].annotations.dashboard_path = 'https://offsite.invalid/d/overview'; },
    fixture => { fixture.rules.groups[0].rules[0].annotations.dashboard_path = '/d/overview/platform?secret=synthetic'; },
    fixture => { fixture.rules.groups[0].rules[0].annotations.runbook_id = 42; },
    fixture => { fixture.rules.groups = []; },
    fixture => { fixture.sources.datasources = []; },
    fixture => { fixture.dashboards = []; },
  ];
  for (const mutate of mutations) assert.throws(() => syntheticInventory(mutate));
  assert.throws(() => grafanaReferenceInventory([], 'not: [valid', []));
});

test('repository inventory is deterministic and pins every included source without altering active configuration', () => {
  const paths = execFileSync('git', ['ls-files', '-z', '--', 'deploy/observability',
    'PVAD.WebApi/Controllers/AdminOperatorAccessController.cs', 'PVAD.WebApi/Infrastructure/PvadResponseHeaders.cs'],
  { cwd: root, encoding: 'utf8' }).split('\0').filter(path => path && existsSync(new URL('../../' + path, import.meta.url)));
  const before = new Map(paths.map(path => [path, readFileSync(new URL('../../' + path, import.meta.url))]));
  const result = prepareGrafanaInventory();
  assert.deepEqual(prepareGrafanaInventory(), result);
  assert.equal(result.dashboards.length, 5);
  assert.equal(result.datasources.length, 3);
  assert.equal(result.alerts.length, 37);
  assert.equal(result.alerts.filter(alert => alert.dashboardUid !== null).length, 34);
  assert.equal(result.alerts.filter(alert => alert.runbookId !== null).length, 34);
  for (const file of result.sources) {
    const bytes = readFileSync(new URL('../../' + file.path, import.meta.url));
    assert.equal(createHash('sha256').update(bytes).digest('hex'), file.sha256);
    assert.deepEqual(before.get(file.path), bytes);
  }
  for (const [path, bytes] of before)
    assert.deepEqual(readFileSync(new URL('../../' + path, import.meta.url)), bytes);
});

test('CLI binds its reference inventory to exact checksums without implying deployment readiness', () => {
  const run = spawnSync(process.execPath, ['tools/configuration/grafana-inventory.mjs'], { cwd: root, encoding: 'utf8' });
  assert.equal(run.status, 0, run.stderr);
  const report = JSON.parse(run.stdout);
  const output = new URL('../../artifacts/grafana-inventory/', import.meta.url);
  assert.equal(report.inventorySha256, createHash('sha256').update(readFileSync(new URL('inventory.json', output))).digest('hex'));
  assert.equal(report.sourceSha256, createHash('sha256').update(source).digest('hex'));
  assert.equal(report.deploymentAuthorized, false);
});
