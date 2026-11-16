import assert from 'node:assert/strict';
import { verify } from 'node:crypto';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { createTestLicenses, main } from './with-test-license.mjs';

test('synthetic artifacts are signed with one ephemeral P-256 key and no private key is written', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-native-fixture-contract-'));
  try {
    const now = Date.parse('2026-11-15T12:00:00Z');
    const paths = createTestLicenses(directory, now);
    const trust = JSON.parse(readFileSync(paths.trustPath));
    for (const name of ['pdf', 'word', 'spreadsheet', 'importer', 'integrated', 'expired']) {
      const envelope = JSON.parse(readFileSync(paths[name]));
      const bytes = Buffer.from(envelope.payload, 'base64');
      assert.equal(envelope.keyId, trust.keys[0].keyId);
      assert.ok(verify('sha256', bytes, { key: trust.keys[0].publicKeyPem, dsaEncoding: 'ieee-p1363' }, Buffer.from(envelope.signature, 'base64')));
      const payload = JSON.parse(bytes);
      assert.deepEqual(payload.grants, (name === 'integrated' ? ['pdf', 'word', 'spreadsheet', 'importer'] : [name === 'expired' ? 'pdf' : name])
        .map(product => ({ product, providers: [] })));
      assert.equal(Date.parse(payload.validUntil) - Date.parse(payload.validFrom), 30 * 86400_000);
      assert.equal(Date.parse(payload.validUntil) < now, name === 'expired');
      assert.equal(payload.redistributionAllowed, false);
    }
    assert.equal(readdirSync(directory).length, 7);
    assert.ok(!readFileSync(paths.trustPath, 'utf8').includes('PRIVATE'));
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('isolated runner rejects foreign projects and build-directory overrides before building', () => {
  assert.throws(() => main(['test', '../foreign.csproj']));
  assert.throws(() => main(['test', 'tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj', '--no-build']));
  assert.throws(() => main(['test', 'tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj', '-p:ArtifactsPath=elsewhere']));
});
