import assert from 'node:assert/strict';
import { verify, createPublicKey } from 'node:crypto';
import { mkdtempSync, readFileSync, readdirSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { createTestLicenses, main, migrationCodeTestGrants, migrationReportTestGrant } from './with-test-license.mjs';

test('synthetic artifacts are signed with one ephemeral P-256 key and no private key is written', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-native-fixture-contract-'));
  try {
    const now = Date.parse('2026-11-15T12:00:00Z');
    const paths = createTestLicenses(directory, now);
    const trust = JSON.parse(readFileSync(paths.trustPath));
    for (const name of ['pdf', 'word', 'spreadsheet', 'importer', 'converter', 'integrated', 'expired', 'migration-code', 'migration-viewer', 'migration-designer', 'migration-report']) {
      const envelope = JSON.parse(readFileSync(paths[name]));
      const bytes = Buffer.from(envelope.payload, 'base64');
      assert.equal(envelope.keyId, trust.keys[0].keyId);
      assert.ok(verify('sha256', bytes, { key: trust.keys[0].publicKeyPem, dsaEncoding: 'ieee-p1363' }, Buffer.from(envelope.signature, 'base64')));
      const payload = JSON.parse(bytes);
      const expected = (name === 'integrated' ? ['pdf', 'word', 'spreadsheet', 'importer', 'converter'] : [name === 'expired' ? 'pdf' : name])
        .map(product => ({ product, providers: [] }));
      if (name === 'integrated') expected.push(...migrationCodeTestGrants());
      if (name === 'integrated') expected.push(...['viewer', 'designer'].map(kind => ({ product: `migration.pdf.${kind}`, providers: ['nutrient-viewer'] })));
      if (name === 'integrated') expected.push(migrationReportTestGrant());
      const actualExpected = name === 'migration-code' ? migrationCodeTestGrants()
        : /^migration-(viewer|designer)$/.test(name) ? [{ product: `migration.pdf.${name.slice(10)}`, providers: ['nutrient-viewer'] }] : expected;
      assert.deepEqual(payload.grants, name === 'migration-report' ? [migrationReportTestGrant()] : actualExpected);
      assert.equal(Date.parse(payload.validUntil) - Date.parse(payload.validFrom), 30 * 86400_000);
      assert.equal(Date.parse(payload.validUntil) < now, name === 'expired');
      assert.equal(payload.redistributionAllowed, false);
    }
    assert.equal(readdirSync(directory).length, 12);
    assert.ok(!readFileSync(paths.trustPath, 'utf8').includes('PRIVATE'));
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('code-provider probes receive explicit single-provider grants without broadening product-only fixtures', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-code-license-contract-'));
  try {
    const paths = createTestLicenses(directory, Date.now(), true);
    const trust = JSON.parse(readFileSync(paths.trustPath));
    const grants = migrationCodeTestGrants();
    assert.deepEqual(grants.map(grant => grant.providers.length), [16, 8]);
    for (const [name, path] of Object.entries(paths).filter(([name]) => name.startsWith('migration.'))) {
      const envelope = JSON.parse(readFileSync(path));
      const bytes = Buffer.from(envelope.payload, 'base64');
      assert.ok(verify('sha256', bytes, { key: trust.keys[0].publicKeyPem, dsaEncoding: 'ieee-p1363' }, Buffer.from(envelope.signature, 'base64')));
      const payload = JSON.parse(bytes);
      assert.equal(payload.grants.length, 1);
      assert.equal(payload.grants[0].providers.length, 1);
      assert.equal(name, `${payload.grants[0].product}-${payload.grants[0].providers[0]}`);
    }
    assert.equal(readdirSync(directory).length, 36);
    const pdf = JSON.parse(Buffer.from(JSON.parse(readFileSync(paths.pdf)).payload, 'base64'));
    assert.deepEqual(pdf.grants, [{ product: 'pdf', providers: [] }]);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('manufacturer signer tests alone opt into an ephemeral owner-only private key matching synthetic trust', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-issuer-fixture-contract-'));
  try {
    const paths = createTestLicenses(directory, Date.now(), false, false, true);
    const trust = JSON.parse(readFileSync(paths.trustPath));
    assert.equal(createPublicKey(readFileSync(paths.issuerPrivateKey)).export({ type: 'spki', format: 'pem' }), trust.keys[0].publicKeyPem);
    if (process.platform !== 'win32') assert.equal(statSync(paths.issuerPrivateKey).mode & 0o777, 0o600);
    assert.equal(readdirSync(directory).length, 13);
    assert.ok(!readFileSync(paths.trustPath, 'utf8').includes('PRIVATE'));
    assert.throws(() => main(['native-issuer', '--no-build']));
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('isolated runner rejects foreign projects and build-directory overrides before building', () => {
  assert.throws(() => main(['test', '../foreign.csproj']));
  assert.throws(() => main(['test', 'tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj', '--no-build']));
  assert.throws(() => main(['test', 'tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj', '-p:ArtifactsPath=elsewhere']));
});

test('revocation fixtures are domain-separated signed cumulative metadata and never export a private key', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-revocation-fixture-contract-'));
  try {
    const paths = createTestLicenses(directory, Date.now(), false, false, false, true);
    const envelope = JSON.parse(readFileSync(paths.revocations));
    const trust = JSON.parse(readFileSync(paths.trustPath));
    const bytes = Buffer.from(envelope.payload, 'base64');
    assert.ok(verify('sha256', bytes, { key: trust.keys[0].publicKeyPem, dsaEncoding: 'ieee-p1363' }, Buffer.from(envelope.signature, 'base64')));
    const payload = JSON.parse(bytes);
    assert.equal(envelope.kind, 'PVAD.NativeLicenseRevocations');
    assert.equal(payload.purpose, envelope.kind); assert.equal(payload.sequence, 1);
    assert.deepEqual(payload.revokedLicenseIds, ['pdf', 'importer'].map(name => JSON.parse(Buffer.from(JSON.parse(readFileSync(paths[name])).payload, 'base64')).licenseId));
    assert.equal(readdirSync(directory).length, 13);
    assert.equal(paths.issuerPrivateKey, undefined);
    assert.throws(() => main(['revocation', '--foreign-build']));
  } finally { rmSync(directory, { recursive: true, force: true }); }
});
