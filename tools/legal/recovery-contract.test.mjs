import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../', import.meta.url);
const read = (path) => readFile(new URL(path, root), 'utf8');
const [backup, restore, verify, drill, runbook, publicDocumentation] = await Promise.all([
  read('tools/legal/backup-postgres.sh'),
  read('tools/legal/restore-postgres.sh'),
  read('tools/legal/verify-legal-recovery.sh'),
  read('tools/legal/run-backup-restore-smoke-test.sh'),
  read('operator-docs/PXA.Legal-Backup-Restore-And-Recovery.md'),
  read('websites/PXA.Documentation/src/main.js'),
]);

test('backup and restore scripts require protected inputs and integrity checks', () => {
  assert.match(backup, /load_database_url/);
  assert.match(backup, /--format=custom/);
  assert.match(backup, /pg_restore --list/);
  assert.match(backup, /write_sha256/);
  assert.match(restore, /RESTORE PXA DATABASE/);
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
  assert.match(drill, /pxa-legal-source-/);
  assert.match(drill, /pxa-legal-target-/);
  assert.match(drill, /trap cleanup EXIT INT TERM/);
  assert.match(drill, /synthetic-recovery-password/);
  assert.match(drill, /verify-legal-recovery\.sh/);
});

test('operator recovery details remain outside public Documentation', () => {
  assert.match(runbook, /Restricted operator runbook/);
  assert.match(runbook, /restore-postgres\.sh/);
  assert.match(runbook, /registration and checkout\s+disabled/i);
  assert.doesNotMatch(publicDocumentation, /restore-postgres\.sh|PXA_RESTORE_CONFIRM|pxa_recovery_database_url/);
});
