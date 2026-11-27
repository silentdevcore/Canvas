import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { inspectManufacturer } from './manufacturer-preflight.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => JSON.parse(readFileSync(join(root, path), 'utf8'));
const config = () => read('PVAD.WebApi/configuration/native-manufacturer-free-start.example.json');
const readiness = () => read('operator-docs/native-manufacturer-readiness.example.json');
const emptyTrust = { schemaVersion: 1, keys: [] };
const id = 'ab111111-2222-3333-4444-555555555555';
const script = join(root, 'tools/native-licensing/manufacturer-preflight.mjs');

function fixture() {
  const settings = config(), record = readiness();
  Object.assign(settings.NativeLicenseIssuer, { Enabled: true, KeyId: 'fixture-approved-key',
    PrivateKeyPath: '/not-provisioned/not-read/native.pem', AllowedUserIds: [id] });
  Object.assign(settings.NativeLicenseAutomation, { Enabled: true, IssuerUserId: id });
  for (const field of ['hostingProvider', 'secretStorage', 'keyCustodian', 'backupCustodian', 'recoveryReviewer']) record[field] = 'Fixture-only role or system';
  for (const field of Object.keys(record.evidence)) record.evidence[field] = 'Fixture-only evidence reference';
  const { publicKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1', publicKeyEncoding: { type: 'spki', format: 'pem' } });
  return { settings, record, trust: { schemaVersion: 1, keys: [{ keyId: 'fixture-approved-key', publicKeyPem: publicKey }] } };
}

test('checked-in preparation stays disabled and exposes every unfulfilled production prerequisite', () => {
  const result = inspectManufacturer(config(), readiness(), emptyTrust);
  assert.equal(result.staticChecksPassed, true);
  assert.equal(result.readyForLiveChecks, false);
  assert.equal(result.productionReady, false);
  for (const code of ['ISSUER_ID_REQUIRED', 'KEY_ID_REQUIRED', 'PRIVATE_KEY_MOUNT_PATH_REQUIRED',
    'ISSUER_ALLOWLIST_MATCH_REQUIRED', 'HOSTINGPROVIDER_REQUIRED', 'SECRETSTORAGE_REQUIRED',
    'KEYCUSTODIAN_REQUIRED', 'BACKUPCUSTODIAN_REQUIRED', 'RECOVERYREVIEWER_REQUIRED',
    'BACKUPRECOVERYDRILL_REQUIRED', 'EMBEDDED_PUBLIC_KEY_REQUIRED']) assert.ok(result.pending.includes(code));
  const selected = read('PVAD.WebApi/configuration/native-license-free-three-months.example.json');
  assert.deepEqual(config().NativeLicenseAutomation, selected.NativeLicenseAutomation);
});

test('deployment rejects placeholders and disabled issuance instead of treating preparation as acceptance', () => {
  const result = inspectManufacturer(config(), readiness(), emptyTrust, 'deployment');
  assert.equal(result.staticChecksPassed, false);
  assert.ok(result.errors.includes('DEPLOYMENT_FLAGS_DISABLED'));
  assert.ok(result.pending.length > 0);
});

test('static success never claims production approval or reads the configured private-key file', () => {
  const { settings, record, trust } = fixture();
  const result = inspectManufacturer(settings, record, trust, 'deployment');
  assert.equal(result.staticChecksPassed, true);
  assert.equal(result.readyForLiveChecks, true);
  assert.equal(result.productionReady, false);
  assert.equal(result.liveVerificationRequired, true);
  assert.deepEqual(result.pending, []);
  settings.NativeLicenseIssuer.AllowedUserIds = [id.toUpperCase()];
  assert.equal(inspectManufacturer(settings, record, trust, 'deployment').staticChecksPassed, true);
});

test('wrong policy, mismatched operator, malformed IDs and unsafe configuration shapes fail', () => {
  const mutations = [
    c => { c.NativeLicenseAutomation.ValidityMonths = 6; },
    c => { c.NativeLicenseAutomation.ValidityDays = 90; },
    c => { c.NativeLicenseAutomation.FreeRenewalEnabled = false; },
    c => { c.NativeLicenseAutomation.Model = 'Perpetual'; },
    c => { c.NativeLicenseAutomation.UpdatesUntil = '2027-01-01'; },
    c => { c.NativeLicenseAutomation.PolicyVersion = ''; },
    c => { c.NativeLicenseAutomation.IssuerUserId = 'bad-guid'; },
    c => { c.NativeLicenseIssuer.AllowedUserIds = ['cd111111-2222-3333-4444-555555555555']; },
    c => { c.NativeLicenseIssuer.AllowedUserIds = [id, id.toUpperCase()]; },
    c => { c.NativeLicenseIssuer.PrivateKeyPath = 'relative/native.pem'; },
    c => { c.NativeLicenseIssuer.Enabled = 'false'; },
    c => { c.NativeLicenseIssuer.PrivateKey = 'DO_NOT_ECHO'; },
    c => { c.ConnectionStrings = { Default: 'DO_NOT_ECHO' }; },
  ];
  for (const mutate of mutations) {
    const { settings, record, trust } = fixture(); mutate(settings);
    const result = inspectManufacturer(settings, record, trust, 'deployment');
    assert.equal(result.staticChecksPassed, false);
    assert.ok(!JSON.stringify(result).includes('DO_NOT_ECHO'));
  }
});

test('readiness omissions, unknown fields and missing or invalid public trust block live-check readiness', () => {
  const { settings, record, trust } = fixture();
  for (const field of Object.keys(record.evidence)) {
    const missing = structuredClone(record); missing.evidence[field] = null;
    assert.equal(inspectManufacturer(settings, missing, trust, 'deployment').staticChecksPassed, false);
  }
  assert.equal(inspectManufacturer(settings, record, emptyTrust, 'deployment').staticChecksPassed, false);
  const badTrust = structuredClone(trust); badTrust.keys[0].publicKeyPem = 'malformed DO_NOT_ECHO';
  const report = inspectManufacturer(settings, record, badTrust, 'deployment');
  assert.ok(report.errors.includes('PUBLIC_TRUST_INVALID'));
  assert.ok(!JSON.stringify(report).includes('DO_NOT_ECHO'));
  const unknown = { ...record, password: 'DO_NOT_ECHO' };
  assert.equal(inspectManufacturer(settings, unknown, trust, 'deployment').staticChecksPassed, false);
  for (const value of [null, [], 5, 'text', {}]) assert.equal(inspectManufacturer(value, value, emptyTrust).staticChecksPassed, false);
});

test('private material and known temporary key IDs are rejected without disclosure', () => {
  const { settings, record, trust } = fixture();
  settings.NativeLicenseIssuer.KeyId = trust.keys[0].keyId = 'ephemeral-native-engine-test';
  assert.ok(inspectManufacturer(settings, record, trust, 'deployment').errors.includes('TEST_KEY_FORBIDDEN'));
  record.secretStorage = '-----BEGIN PRIVATE KEY----- DO_NOT_ECHO';
  const report = inspectManufacturer(settings, record, trust, 'deployment');
  assert.ok(report.errors.includes('KEY_MATERIAL_FORBIDDEN'));
  assert.ok(!JSON.stringify(report).includes('DO_NOT_ECHO'));
});

test('CLI is read-only, succeeds for preparation and fails safely for malformed input and missing deployment data', () => {
  const before = readFileSync(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'));
  const prepared = spawnSync(process.execPath, [script], { cwd: root, encoding: 'utf8' });
  assert.equal(prepared.status, 0); assert.equal(JSON.parse(prepared.stdout).productionReady, false);
  const blocked = spawnSync(process.execPath, [script, '--mode', 'deployment'], { cwd: root, encoding: 'utf8' });
  assert.equal(blocked.status, 1); assert.equal(JSON.parse(blocked.stdout).readyForLiveChecks, false);
  const scratch = mkdtempSync(join(tmpdir(), 'pvad-preflight-'));
  try {
    const invalid = join(scratch, 'DO_NOT_ECHO.json'); writeFileSync(invalid, 'malformed DO_NOT_ECHO');
    for (const args of [['--config', invalid], ['--unknown', 'DO_NOT_ECHO'], ['--mode', 'wrong'], ['--mode']]) {
      const result = spawnSync(process.execPath, [script, ...args], { cwd: root, encoding: 'utf8' });
      assert.equal(result.status, 1); assert.equal(JSON.parse(result.stderr).code, 'INPUT_INVALID');
      assert.ok(!(result.stdout + result.stderr).includes('DO_NOT_ECHO'));
    }
    const { settings, record, trust } = fixture();
    for (const [name, data] of [['config', settings], ['readiness', record], ['trust', trust]])
      writeFileSync(join(scratch, name + '.json'), JSON.stringify(data));
    const checked = spawnSync(process.execPath, [script, '--mode', 'deployment', '--config', join(scratch, 'config.json'),
      '--readiness', join(scratch, 'readiness.json'), '--trust', join(scratch, 'trust.json')], { cwd: root, encoding: 'utf8' });
    assert.equal(checked.status, 0); assert.equal(JSON.parse(checked.stdout).productionReady, false);
  } finally { rmSync(scratch, { recursive: true, force: true }); }
  assert.deepEqual(readFileSync(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json')), before);
});
