import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, readFileSync, readlinkSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { canonicalPath } from './rename-file-pairs.mjs';
import { readGitBlobs } from './git-review-blobs.mjs';
export { parseBatch } from './git-review-blobs.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 256 * 1024 * 1024 });
const readWorkingFile = path => lstatSync(path).isSymbolicLink()
  ? Buffer.from(readlinkSync(path)) : readFileSync(path);

export function compareContent(before, after) {
  if (before.equals(after)) return 'byte-identical';
  if (before.includes(0) || after.includes(0)) return 'binary-content-review-required';
  const decoder = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });
  try {
    const source = decoder.decode(before);
    decoder.decode(after);
    const renamed = source.replaceAll('PXA', 'PVAD').replaceAll('Pxa', 'Pvad').replaceAll('pxa', 'pvad');
    return Buffer.from(renamed).equals(after)
      ? 'identity-substitution-only-review-required' : 'additional-content-review-required';
  } catch {
    return 'binary-content-review-required';
  }
}

export function reviewContent() {
  const baseline = git('rev-parse', 'HEAD').toString().trim();
  const entries = git('ls-tree', '-r', '-z', baseline).toString().split('\0').filter(Boolean).map(value => {
    const tab = value.indexOf('\t');
    const [mode, type, oid] = value.slice(0, tab).split(' ');
    return { mode, type, oid, source: value.slice(tab + 1) };
  }).filter(entry => entry.type === 'blob');
  const ids = [...new Set(entries.map(entry => entry.oid))];
  const blobs = readGitBlobs(root, ids);
  const targets = new Set();
  const files = entries.map(entry => {
    const canonical = canonicalPath(entry.source);
    const evidence = entry.source.replace('websites/PXA.Documentation/', 'websites/PVAD.Documentation/');
    const candidates = [entry.source, canonical];
    if (/^websites\/PXA\.Documentation\/public\/downloads\/pdf-trust\/pades\/(?:negative\/)?pxa-pades-[\w-]+\.(?:pdf|xml)$/.test(entry.source))
      candidates.push(evidence);
    const target = candidates.find(path => existsSync(resolve(root, path)));
    const before = blobs.get(entry.oid);
    if (!before) throw new Error('Missing baseline blob');
    if (!target) return { source: entry.source, sourceSha256: hash(before), classification: 'missing-target' };
    targets.add(target);
    const after = readWorkingFile(resolve(root, target));
    return { source: entry.source, target, sourceSha256: hash(before), targetSha256: hash(after),
      classification: compareContent(before, after) };
  });
  const current = [...new Set(git('ls-files', '-co', '--exclude-standard', '-z').toString().split('\0').filter(Boolean))];
  for (const path of current) {
    if (targets.has(path) || !existsSync(resolve(root, path))) continue;
    files.push({ target: path, targetSha256: hash(readWorkingFile(resolve(root, path))), classification: 'new-file-review-required' });
  }
  const counts = {};
  for (const file of files) counts[file.classification] = (counts[file.classification] ?? 0) + 1;
  const report = { schemaVersion: 1, baseline, scope: 'HEAD versus current working files, including staged and untracked files',
    approval: 'not-approved', warning: 'Identity substitution can change wire contracts; no category is semantic approval.', counts, files };
  mkdirSync(resolve(root, 'artifacts/rename'), { recursive: true });
  writeFileSync(resolve(root, 'artifacts/rename/content-review.json'), `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify({ baseline, counts }));
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) reviewContent();
