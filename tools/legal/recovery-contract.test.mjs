import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = new URL('../../', import.meta.url);
const read = (path) => readFile(new URL(path, root), 'utf8');
const [backup, restore, verify, drill, runbook, publicDocumentation] = await Promise.all([
  read('tools/legal/backup-postgres.sh'),
  read('tools/legal/restore-postgres.sh'),
  read('tools/legal/verify-legal-recovery.sh'),
  read('tools/legal/run-backup-restore-smoke-test.sh'),
  read('operator-docs/PVAD.Legal-Backup-Restore-And-Recovery.md'),
  read('websites/PVAD.Documentation/src/main.js'),
]);

test('backup and restore scripts require protected inputs and integrity checks', () => {
  assert.match(backup, /load_database_url/);
  assert.match(backup, /--format=custom/);
  assert.match(backup, /pg_restore --list/);
  assert.match(backup, /write_sha256/);
  assert.match(restore, /RESTORE PXA DATABASE/);
  assert.match(restore, /Set PVAD_RESTORE_CONFIRM="RESTORE PVAD DATABASE"/);
  assert.match(restore, /verify_sha256/);
  assert.match(restore, /Restore target is not empty/);
  assert.doesNotMatch(backup + restore, /pxa-local|localhost:5432/);
});

test('recovery verification protects Legal relationships and fail-closed policy', () => {
  assert.match(verify, /__EFMigrationsHistory/);
  assert.match(verify, /legal_acceptance_events/);
  assert.match(verify, /ContentHash.*<>/s);
  assert.match(verify, /Current Terms and Privacy versions are not both available/);
});

test('recovery drill is isolated, synthetic, and always cleans up', () => {
  assert.match(drill, /pvad-legal-source-/);
  assert.match(drill, /pvad-legal-target-/);
  assert.match(drill, /trap cleanup EXIT INT TERM/);
  assert.match(drill, /docker rm -f -v "\$source_container" "\$target_container"/);
  assert.match(drill, /synthetic-recovery-password/);
  assert.match(drill, /POSTGRES_DB=pvad -e POSTGRES_USER=pvad/);
  assert.match(drill, /PVAD_RESTORE_CONFIRM='RESTORE PVAD DATABASE'/);
  assert.doesNotMatch(drill, /POSTGRES_(?:DB|USER)=pxa|postgresql:\/\/pxa:|psql -U pxa/);
  assert.match(drill, /verify-legal-recovery\.sh/);
});

test('recovery drill waits for the configured database and diagnoses timeouts', () => {
  assert.match(drill, /PXA_POSTGRES_READY_ATTEMPTS/);
  assert.match(drill, /PXA_POSTGRES_READY_DELAY_SECONDS/);
  assert.match(drill, /PXA_POSTGRES_STARTUP_DELAY_SECONDS/);
  assert.match(drill, /psql -U pvad -d pvad[\s\S]*SELECT 1/);
  assert.doesNotMatch(drill, /pg_isready/);
  assert.match(drill, /docker inspect --format=/);
  assert.match(drill, /docker logs --tail 100/);
  assert.match(drill, /sleep \$startup_delay; exec docker-entrypoint\.sh postgres/);
});

test('operator recovery details remain outside public Documentation', () => {
  assert.match(runbook, /Restricted operator runbook/);
  assert.match(runbook, /restore-postgres\.sh/);
  assert.match(runbook, /registration and checkout\s+disabled/i);
  assert.doesNotMatch(publicDocumentation, /restore-postgres\.sh|PXA_RESTORE_CONFIRM|pxa_recovery_database_url/);
});

const script = fileURLToPath(new URL('./restore-postgres.sh', import.meta.url));

function runRestore(t, confirmation, options = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-restore-contract-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const backup = join(directory, options.basename ?? 'pvad-synthetic.dump');
  const bytes = Buffer.from('Synthetic PVAD backup; not a PostgreSQL archive.');
  const trace = join(directory, 'commands.log');
  writeFileSync(trace, '');
  writeFileSync(backup, bytes);
  const hash = createHash('sha256').update(bytes).digest('hex');
  if (!options.missingChecksum) {
    writeFileSync(`${backup}.sha256`, `${options.corrupt ? '0'.repeat(64) : hash}  ${backup}\n`);
  }
  // Fake database commands record invocation order without contacting a server.
  writeFileSync(join(directory, 'pg_restore'), `#!/bin/sh
set -eu
if [ "$1" = --list ]; then
  printf 'catalog\n' >> "$PVAD_TEST_TRACE"
  exit "$PVAD_TEST_INVALID_ARCHIVE"
fi
printf 'restore\n' >> "$PVAD_TEST_TRACE"
`, { mode: 0o700 });
  writeFileSync(join(directory, 'psql'), `#!/bin/sh
set -eu
printf 'tables\n' >> "$PVAD_TEST_TRACE"
printf '%s\n' "$PVAD_TEST_TABLE_COUNT"
`, { mode: 0o700 });

  const result = spawnSync('sh', [script, backup], {
    env: {
      PATH: `${directory}:${process.env.PATH}`,
      PVAD_DATABASE_URL: 'postgresql://synthetic.invalid/unused',
      PVAD_TEST_TRACE: trace,
      PVAD_TEST_TABLE_COUNT: options.tableCount ?? '0',
      PVAD_TEST_INVALID_ARCHIVE: options.invalidArchive ? '1' : '0',
      ...confirmation,
    },
    encoding: 'utf8',
  });
  assert.ifError(result.error);
  assert.deepEqual(readFileSync(backup), bytes);
  return { ...result, calls: readFileSync(trace, 'utf8').trim().split('\n').filter(Boolean) };
}

for (const [name, confirmation] of [
  ['canonical key and phrase', { PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }],
  ['canonical key with retained phrase', { PVAD_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }],
  ['retained key and phrase', { PXA_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }],
  ['retained key with canonical phrase', { PXA_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }],
  ['identical keys', { PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE', PXA_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }],
]) {
  test(`restore accepts ${name} without bypassing archive and target checks`, t => {
    const result = runRestore(t, confirmation);
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(result.calls, ['catalog', 'tables', 'restore']);
  });
}

for (const phrase of [undefined, '', 'RESTORE', 'RESTORE PVAD', 'restore pvad database', ' RESTORE PVAD DATABASE', 'RESTORE PVAD DATABASE ', 'RESTORE OTHER DATABASE']) {
  test(`restore rejects ${JSON.stringify(phrase) ?? 'missing'} confirmation before database commands`, t => {
    const result = runRestore(t, phrase === undefined ? {} : { PVAD_RESTORE_CONFIRM: phrase });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Set PVAD_RESTORE_CONFIRM="RESTORE PVAD DATABASE"/);
    assert.deepEqual(result.calls, []);
  });
}

test('conflicting confirmation aliases fail before database commands', t => {
  const result = runRestore(t, {
    PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE', PXA_RESTORE_CONFIRM: 'RESTORE PXA DATABASE',
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Conflicting configuration keys/);
  assert.deepEqual(result.calls, []);
});

for (const [name, options, message, calls] of [
  ['missing checksum', { missingChecksum: true }, /adjacent .sha256 file is required/, []],
  ['corrupt checksum', { corrupt: true }, /Backup SHA-256 verification failed/, []],
  ['invalid archive', { invalidArchive: true }, null, ['catalog']],
  ['non-empty target', { tableCount: '1' }, /Restore target is not empty/, ['catalog', 'tables']],
]) {
  test(`canonical confirmation still rejects ${name}`, t => {
    const result = runRestore(t, { PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }, options);
    assert.notEqual(result.status, 0);
    if (message) assert.match(result.stderr, message);
    assert.deepEqual(result.calls, calls);
  });
}

test('canonical confirmation restores a verified legacy-named backup without renaming it', t => {
  const result = runRestore(t, { PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }, { basename: 'pxa-retained.dump' });
  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(result.calls, ['catalog', 'tables', 'restore']);
});
