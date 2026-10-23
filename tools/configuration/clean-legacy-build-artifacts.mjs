import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, realpathSync, unlinkSync, writeFileSync } from 'node:fs';
import { basename, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = realpathSync(fileURLToPath(new URL('../../', import.meta.url)));
const audit = JSON.parse(readFileSync(resolve(root, 'artifacts/rename/name-and-artifact-audit.json'), 'utf8'));
const tracked = new Set(execFileSync('git', ['ls-files', '-z'], { cwd: root }).toString().split('\0'));
const candidates = audit.generatedArtifacts.filter(item => /\/PXA\./.test(item.path)
  && !item.path.startsWith('tools/job-cutover/'));
// Validate the entire plan before removing any reproducible output.
for (const item of candidates) {
  const absolute = resolve(root, item.path);
  if (!/^(?:(?:src|tests|tools)\/.+|PVAD\.WebApi)\/(?:bin|obj)\//.test(item.path)
    || !/^PXA\..*\.(?:dll|exe|pdb|deps\.json|runtimeconfig\.json)$/.test(basename(item.path))
    || tracked.has(item.path) || lstatSync(absolute).isSymbolicLink()
    || relative(root, realpathSync(absolute)) !== item.path)
    throw new Error(`Unsafe cleanup target: ${item.path}`);
  const hash = createHash('sha256').update(readFileSync(absolute)).digest('hex');
  if (hash !== item.sha256) throw new Error(`Artifact changed since audit: ${item.path}`);
}
if (process.argv.includes('--apply')) {
  if (!candidates.length) {
    console.log('No stale artifacts found; previous cleanup evidence remains unchanged.');
    process.exit(0);
  }
  writeFileSync(resolve(root, 'artifacts/rename/legacy-build-cleanup.json'),
    `${JSON.stringify({ schemaVersion: 1, removed: candidates, pairedHarnessPreserved: true }, null, 2)}\n`);
  for (const item of candidates) unlinkSync(resolve(root, item.path));
  console.log(`Removed ${candidates.length} untracked generated PXA build artifacts; all job-cutover artifacts preserved.`);
} else console.log(`Dry run: ${candidates.length} validated untracked generated artifacts; no files removed.`);
