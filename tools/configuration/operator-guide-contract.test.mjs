import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

test('operator guidance uses current routes and mounted secret paths without approving a deployment', () => {
  const admin = read('operator-docs/PVAD.Admin-Operations.md');
  const observability = read('deploy/observability/README.md');
  const targets = JSON.parse(read('deploy/rename/pvade-targets.candidate.json'));
  assert.ok(admin.includes(targets.hosts.admin));
  assert.ok(admin.includes('not evidence of'));
  assert.ok(admin.includes('Shared production link defaults now use the selected domain'));
  assert.match(admin, /backend authentication allowlists/i);
  for (const source of [admin, observability]) {
    assert.ok(source.includes('/api/pvad/v1/admin/operator/access'));
    assert.ok(!source.includes('/api/pxa/v1/admin/operator/access'));
  }
  assert.ok(admin.includes('/api/pvad/v1/admin/system/retention/dry-run'));
  assert.ok(read('PVAD.WebApi/Controllers/AdminOperatorAccessController.cs').includes('api/pvad/v1/admin/operator'));
  assert.ok(read('PVAD.WebApi/Controllers/AdminRetentionController.cs').includes('api/pvad/v1/admin/system/retention'));
  const smtp = load(read('deploy/observability/alertmanager/alertmanager.production.yml.example'));
  const relay = load(read('deploy/observability/docker-compose.webhook.yml'));
  assert.ok(admin.includes(smtp.global.smtp_auth_password_file));
  const service = Object.values(relay.services).find(value => value.environment?.WebhookRelay__SecretFile);
  assert.ok(service);
  assert.ok(admin.includes(service.environment.WebhookRelay__SecretFile));
  assert.doesNotMatch(admin, /\/run\/secrets\/pxa_/);
  assert.doesNotMatch(observability, /example\.test\/pxa-alerts|\/absolute\/secrets\/pxa-/);
});

test('restricted recovery examples agree with the new filename producer and preserve old recovery', () => {
  const recovery = read('operator-docs/PVAD.Legal-Backup-Restore-And-Recovery.md');
  assert.doesNotMatch(recovery, /\/run\/secrets\/pxa_|\/protected\/pxa-backups/);
  assert.ok(recovery.includes('/protected/pvad-backups/pvad-UTC.dump'));
  assert.ok(recovery.includes('pvad-<timestamp>-<pid>.dump'));
  assert.ok(recovery.includes('original basename of either generation'));
  assert.ok(recovery.includes("PVAD_RESTORE_CONFIRM='RESTORE PXA DATABASE'"));
  assert.ok(recovery.includes('not permission to move installed secrets'));
  assert.ok(read('tools/legal/backup-postgres.sh').includes('/pvad-$timestamp-$$.dump'));
});

test('MCP setup describes canonical scopes and retained discovery spellings accurately', () => {
  const doc = read('tools/PVAD.Mcp/README.md');
  assert.ok(doc.includes('PVAD_API_URL=http://pvad-webapi:8080'));
  assert.doesNotMatch(doc, /http:\/\/pxa-api:/);
  for (const scope of ['docs.read', 'documents.validate', 'templates.read']) {
    assert.ok(doc.includes('`pvad.' + scope + '`'));
    assert.ok(read('tools/PVAD.Mcp/src/core/scopeCompatibility.ts').includes("'" + scope + "'"));
  }
  assert.ok(doc.includes('discovery still advertises the historical scope spellings'));
  assert.ok(read('tools/PVAD.Mcp/src/remote/config.ts').includes("['pxa.docs.read']"));
  assert.ok(read('tools/PVAD.Mcp/src/remote/auth.ts').includes('compatibleScopes(readScopes('));
});

test('production link defaults and backend allowlists use the same explicit candidate origins', async () => {
  const { siteLinkDefaults } = await import('../../websites/shared/siteLinks.js');
  const targets = JSON.parse(read('deploy/rename/pvade-targets.candidate.json'));
  for (const [role, url] of Object.entries(siteLinkDefaults.production))
    assert.equal(url, `${targets.hosts[role]}/`, role);
  const auth = read('pvad-designer/src/auth/designerAuth.ts');
  assert.ok(auth.includes("return local ? 'http://localhost:5178/' : 'https://account.pvade.com/';"));
  assert.ok(auth.includes('if (configured) return configured;'));
  assert.ok(!auth.includes('account.powerdoxautomation.com'));
  const configuration = JSON.parse(read('PVAD.WebApi/appsettings.json'));
  const names = ['company', 'documentation', 'demo', 'designer', 'account'];
  assert.deepEqual(configuration.Registration.AllowedReturnOrigins.filter(value => value.startsWith('https:')),
    names.map(name => targets.hosts[name]));
  assert.deepEqual(configuration.DesignerAuthentication.AllowedOrigins,
    ['http://localhost:5173', 'http://localhost:5176', targets.hosts.designer]);
  for (const blocked of ['admin', 'operator', 'api', 'mcp'])
    assert.ok(!configuration.Registration.AllowedReturnOrigins.includes(targets.hosts[blocked]));
  assert.equal(configuration.Mail.SenderAddress, targets.senderAddress);
  const guidance = read('websites/HOSTING.md');
  assert.ok(guidance.includes('Backend authentication allowlists now use the same exact product origins'));
  assert.ok(guidance.includes('new hosts use host-only notices'));
});

test('approved transactional sender is distinct from support and does not enable delivery or select publishers', () => {
  const targets = JSON.parse(read('deploy/rename/pvade-targets.candidate.json'));
  const configuration = JSON.parse(read('PVAD.WebApi/appsettings.json'));
  assert.equal(targets.senderAddress, 'no-reply@pvade.com');
  assert.equal(targets.supportAddress, 'support@pvade.com');
  assert.notEqual(targets.senderAddress, targets.supportAddress);
  assert.equal(configuration.Mail.SenderAddress, targets.senderAddress);
  assert.equal(configuration.Mail.Transport, 'Disabled');
  assert.equal(configuration.Mail.SenderName, 'PVADE');
  assert.deepEqual(targets.publishers, { nuget: null, npm: null, container: null });
  assert.ok(targets.unverified.includes('mail-delivery'));
  assert.ok(targets.unverified.includes('publisher-ownership'));
});

test('Grafana changes datasource labels without deleting active identities or changing cross-links', () => {
  const config = load(read('deploy/observability/grafana/provisioning/datasources/datasources.yml'));
  const sources = config.datasources;
  assert.deepEqual(sources.map(source => [source.name, source.uid, source.url]), [
    ['PVAD Prometheus', 'pxa-prometheus', 'http://pvad-prometheus:9090'],
    ['PVAD Loki', 'pxa-loki', 'http://pvad-loki:3100'],
    ['PVAD Tempo', 'pxa-tempo', 'http://pvad-tempo:3200'],
  ]);
  assert.deepEqual(config.deleteDatasources, [{ name: 'PXA Alertmanager', orgId: 1 }]);
  const targets = new Set(sources.map(source => source.uid));
  function verifyReferences(value) {
    if (!value || typeof value !== 'object') return;
    for (const [key, item] of Object.entries(value)) {
      if (key === 'datasourceUid') assert.ok(targets.has(item), item);
      else verifyReferences(item);
    }
  }
  verifyReferences(sources);
  const dashboard = load(read('deploy/observability/grafana/provisioning/dashboards/dashboards.yml'));
  assert.equal(dashboard.providers[0].name, 'PXA');
  assert.equal(dashboard.providers[0].folder, 'PXA');
  assert.equal(dashboard.providers[0].disableDeletion, true);
});
