import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(join(root, path), 'utf8');

test('gateway renames local helpers but retains upstream and legacy header contracts', () => {
  const gateway = read('deploy/observability/operator-gateway/templates/default.conf.template');
  assert.match(gateway, /map \$http_upgrade \$pvad_connection_upgrade/);
  assert.match(gateway, /location = \/_pvad_operator_auth/);
  assert.equal((gateway.match(/auth_request \/_pvad_operator_auth;/g) ?? []).length, 4);
  assert.match(gateway, /auth_request_set \$pvad_operator \$upstream_http_x_pxa_operator;/);
  assert.match(gateway, /proxy_set_header X-WEBAUTH-USER \$pvad_operator;/);
  assert.match(gateway, /proxy_set_header Connection \$pvad_connection_upgrade;/);
  for (const generation of ['PXA', 'PVAD']) {
    for (const key of ['API-Key', 'Application'])
      assert.ok(gateway.includes(`proxy_set_header X-${generation}-${key} "";`));
  }
  assert.doesNotMatch(gateway, /\$pxa_operator|\$pxa_connection_upgrade|\/_pxa_operator_auth/);
  for (const path of ['tests/PVAD.Api.Tests/PvadObservabilityTests.cs',
    'tests/PVAD.Api.Tests/OperatorDocumentationContractTests.cs']) {
    assert.ok(read(path).includes('auth_request /_pvad_operator_auth;'));
    assert.ok(!read(path).includes('auth_request /_pxa_operator_auth;'));
  }
});

test('backup shell flow emits current names and accepts old names using mocked database tools', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-backup-contract-'));
  try {
    // Fake tools exercise file naming and checksum logic, not PostgreSQL validity.
    writeFileSync(join(directory, 'pg_dump'), '#!/bin/sh\nfor arg do\ncase "$arg" in --file=*) printf "synthetic backup" > "${arg#--file=}" ;; esac\ndone\n', { mode: 0o700 });
    writeFileSync(join(directory, 'pg_restore'), '#!/bin/sh\nexit 0\n', { mode: 0o700 });
    writeFileSync(join(directory, 'psql'), '#!/bin/sh\nprintf "0\\n"\n', { mode: 0o700 });
    const env = { PATH: `${directory}:${process.env.PATH}`, PVAD_DATABASE_URL: 'postgresql://synthetic.invalid/unused' };
    const output = join(directory, 'backups');
    for (let index = 0; index < 2; index++) {
      const result = spawnSync('sh', [join(root, 'tools/legal/backup-postgres.sh'), output], { env, encoding: 'utf8' });
      assert.equal(result.status, 0, result.stderr);
    }
    const backups = readdirSync(output).filter(name => name.endsWith('.dump'));
    assert.equal(backups.length, 2);
    for (const name of backups) {
      assert.match(name, /^pvad-\d{8}T\d{6}Z-\d+\.dump$/);
      const backup = join(output, name);
      const bytes = readFileSync(backup);
      assert.equal(readFileSync(`${backup}.sha256`, 'utf8'), `${createHash('sha256').update(bytes).digest('hex')}  ${name}\n`);
      assert.equal(statSync(backup).mode & 0o777, 0o600);
      assert.equal(statSync(`${backup}.sha256`).mode & 0o777, 0o600);
      const canonical = spawnSync('sh', [join(root, 'tools/legal/restore-postgres.sh'), backup], {
        env: { ...env, PVAD_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }, encoding: 'utf8',
      });
      assert.equal(canonical.status, 0, canonical.stderr);
      const legacy = join(output, name.replace(/^pvad-/, 'pxa-'));
      writeFileSync(legacy, bytes);
      writeFileSync(`${legacy}.sha256`, `${createHash('sha256').update(bytes).digest('hex')}  ${legacy.split('/').at(-1)}\n`);
      const restored = spawnSync('sh', [join(root, 'tools/legal/restore-postgres.sh'), legacy], {
        env: { ...env, PXA_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }, encoding: 'utf8',
      });
      assert.equal(restored.status, 0, restored.stderr);
      assert.deepEqual(readFileSync(legacy), bytes);
    }
    assert.ok(read('tools/legal/run-backup-restore-smoke-test.sh').includes("-name 'pvad-*.dump'"));
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('ephemeral test identities and Mailpit-only addresses are consistent', () => {
  const runtime = read('deploy/observability/retention-runtime-test.sh');
  assert.ok(runtime.includes('project="pvad-retention-test-$(date +%s)-$$"'));
  for (const source of [runtime, read('deploy/observability/README.md')]) {
    assert.ok(source.includes('pvad_retention_marker'));
    assert.ok(source.includes('pvad-retention-marker'));
    assert.doesNotMatch(source, /pxa_retention_|pxa-retention-marker/);
  }
  const mail = read('deploy/observability/alertmanager-email-smoke-test.sh');
  const config = read('deploy/observability/alertmanager/alertmanager.yml');
  assert.ok(config.includes('pvad-alerts@example.test'));
  assert.ok(config.includes('pvad-operators@example.test'));
  assert.ok(mail.includes('pvad-alerts@example.test'));
  assert.ok(mail.includes('pvad-alert-email-'));
  assert.doesNotMatch(mail, /pxa-alert-email-|powerdox\.local|Service: pxa-observability/);
  assert.ok(mail.includes('PXA-OBS-TEST'));
  assert.ok(mail.includes('/d/pxa-platform-overview/pxa-platform-overview'));
  const status = read('deploy/websites/status-hosting-smoke-test.sh');
  assert.ok(status.includes('/not-a-pvad-route'));
  assert.ok(status.includes('/api/pxa/v1/version'));
  for (const path of ['tools/legal/run-backup-restore-smoke-test.sh',
    'deploy/observability/retention-runtime-test.sh',
    'deploy/observability/alertmanager-email-smoke-test.sh',
    'deploy/websites/status-hosting-smoke-test.sh']) {
    const syntax = spawnSync('bash', ['-n', join(root, path)], { encoding: 'utf8' });
    assert.equal(syntax.status, 0, syntax.stderr);
  }
});
