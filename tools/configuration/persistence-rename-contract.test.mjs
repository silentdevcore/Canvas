import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const baseline = 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a';
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 });

test('existing migration bodies, IDs and SQL remain identical after C# identity substitutions', () => {
  const prefix = 'src/Infrastructure/PXA.Infrastructure.Persistence/Migrations/';
  const paths = git('ls-tree', '-r', '--name-only', baseline, '--', prefix).trim().split('\n')
    .filter(path => path && !path.endsWith('ModelSnapshot.cs'));
  assert.ok(paths.length > 0, 'Expected a pre-rename HEAD migration baseline');
  for (const path of paths) {
    const before = git('show', `${baseline}:${path}`);
    const target = path.replaceAll('PXA', 'PVAD').replaceAll('Pxa', 'Pvad');
    const after = readFileSync(new URL(`../../${target}`, import.meta.url), 'utf8');
    // Keep lowercase SQL session variables and migration IDs untouched.
    const expected = before.replaceAll('PXA.', 'PVAD.').replaceAll('Pxa', 'Pvad');
    assert.equal(after, expected, path);
  }
});

test('retention purge writer and installed migration share the retained SQL session key', () => {
  const migration = readFileSync(new URL('../../src/Infrastructure/PVAD.Infrastructure.Persistence/Migrations/20260927050514_AddEInvoiceValidationRecords.cs', import.meta.url), 'utf8');
  const writer = readFileSync(new URL('../../PVAD.WebApi/Services/Pdf/PvadEInvoiceValidationEvidenceService.cs', import.meta.url), 'utf8');
  assert.match(migration, /current_setting\('pxa\.e_invoice_retention_purge', true\)/);
  assert.match(writer, /SET LOCAL pxa\.e_invoice_retention_purge = 'on'/);
});
