import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, readFileSync, readlinkSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { canonicalPath } from './rename-file-pairs.mjs';
import { readGitBlobs } from './git-review-blobs.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const forbiddenPath = path => /(?:^|\/)(?:bin|obj|dist|node_modules|App_Data|artifacts)\//.test(path)
  || /(?:^|\/)\.env(?:\.(?!example$|sample$)[^/]+)?$/.test(path)
  || /\.(?:key|pfx|p12|dump)$/.test(path);

export function inspectIndex(root) {
  const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 64 * 1024 * 1024 }).toString();
  const entries = git('ls-files', '--stage', '-z').split('\0').filter(Boolean).map(record => {
    const tab = record.indexOf('\t');
    const [mode, oid, stage] = record.slice(0, tab).split(' ');
    return { path: record.slice(tab + 1), mode, oid, stage };
  });
  const baseline = git('ls-tree', '-r', '-z', 'HEAD').split('\0').filter(Boolean).map(record => {
    const tab = record.indexOf('\t');
    return { path: record.slice(tab + 1), mode: record.slice(0, tab).split(' ')[0] };
  });
  const baselineModes = new Map(baseline.map(entry => [entry.path, entry.mode]));
  const movedModes = new Map(baseline.map(entry => [canonicalPath(entry.path), entry.mode]));
  const blobs = readGitBlobs(root, entries.filter(entry => entry.mode !== '160000').map(entry => entry.oid));
  const problems = [];
  const modes = [];
  const paths = new Set(entries.map(entry => entry.path));
  for (const entry of entries) {
    if (entry.stage !== '0') { problems.push({ path: entry.path, reason: 'unmerged-index' }); continue; }
    if (!['100644', '100755', '120000'].includes(entry.mode)) {
      problems.push({ path: entry.path, reason: 'unsupported-index-mode' }); continue;
    }
    const file = resolve(root, entry.path);
    if (!existsSync(file)) { problems.push({ path: entry.path, reason: 'missing-working-file' }); continue; }
    const stat = lstatSync(file);
    const actual = stat.isSymbolicLink() ? Buffer.from(readlinkSync(file)) : readFileSync(file);
    if (hash(actual) !== hash(blobs.get(entry.oid))) problems.push({ path: entry.path, reason: 'unstaged-content' });
    if (stat.isSymbolicLink() !== (entry.mode === '120000')) problems.push({ path: entry.path, reason: 'symlink-mode-mismatch' });
    if (entry.mode !== '120000' && Boolean(stat.mode & 0o111) !== (entry.mode === '100755'))
      problems.push({ path: entry.path, reason: 'executable-mode-mismatch' });
    const previousMode = baselineModes.get(entry.path) ?? movedModes.get(entry.path);
    if (previousMode && previousMode !== entry.mode) modes.push({ path: entry.path, before: previousMode, after: entry.mode });
    if (!baselineModes.has(entry.path) && !movedModes.has(entry.path) && forbiddenPath(entry.path))
      problems.push({ path: entry.path, reason: 'new-runtime-or-secret-path' });
  }
  const untracked = git('ls-files', '--others', '--exclude-standard', '-z').split('\0').filter(Boolean);
  for (const path of untracked) problems.push({ path, reason: 'untracked-file' });
  const missingTargets = baseline.filter(entry => !paths.has(entry.path) && !paths.has(canonicalPath(entry.path)))
    .filter(entry => !(/^websites\/PXA\.Documentation\/public\/downloads\/pdf-trust\/pades\//.test(entry.path)
      && paths.has(entry.path.replace('websites/PXA.Documentation/', 'websites/PVAD.Documentation/'))));
  for (const entry of missingTargets) problems.push({ path: entry.path, reason: 'unpaired-index-deletion' });
  return { indexedFiles: entries.length, modeChanges: modes, problems };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const report = inspectIndex(fileURLToPath(new URL('../../', import.meta.url)));
  console.log(JSON.stringify(report, null, 2));
  if (report.problems.length) process.exitCode = 1;
}
