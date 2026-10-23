import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hasLegacyIdentity } from './rename-residual-review.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const output = 'checklists/PVAD.Rename-Inventory-Current.tsv';
const paths = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '-z'],
  { cwd: root, encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 }).split('\0').filter(Boolean))].sort();
const rows = ['path\tclassification\told_name_lines\tpath_match'];
const counts = {};
for (const path of paths) {
  if (path === output || !existsSync(resolve(root, path))) continue;
  let bytes;
  try { bytes = readFileSync(resolve(root, path)); } catch { continue; }
  const binary = bytes.includes(0);
  const classification = /(?:^|\/)(?:bin|obj|dist|artifacts|test-results|playwright-report)\//.test(path)
    ? 'generated-output' : binary ? 'binary-review-required'
    : /(?:legal-documents|AdminDocumentation|legal-evidence|legacy-migration-diagnostics)/i.test(path)
      ? 'legal-or-retained-contract'
    : /(?:Fixtures|test-assets|release-fragments|releases\.json|schema\/)/i.test(path) ? 'fixture-or-contract'
    : /(?:\.md$|\.tsv$|\.agents\/)/.test(path) ? 'documentation-or-history'
    : /(?:deploy\/|\.github\/|\.env|config|\.props$|Dockerfile)/i.test(path) ? 'configuration'
    : 'source';
  const matches = binary ? 0 : bytes.toString('utf8').split('\n')
    .filter(line => hasLegacyIdentity(line)).length;
  const pathMatch = hasLegacyIdentity(path);
  if (!matches && !pathMatch && !binary) continue;
  rows.push(`${path}\t${classification}\t${matches}\t${pathMatch}`);
  counts[classification] = (counts[classification] ?? 0) + 1;
}
writeFileSync(resolve(root, output), `${rows.join('\n')}\n`);
console.log(JSON.stringify({ output, files: rows.length - 1, classifications: counts }));
