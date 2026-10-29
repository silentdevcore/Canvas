import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { readGitBlobs } from './git-review-blobs.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
export const canonicalPath = path => path === 'tools/pdf-trust-pades-interop/dss-validator/src/main/java/com/powerdoxautomation/tools/DssValidator.java'
  ? 'tools/pdf-trust-pades-interop/dss-validator/src/main/java/com/pvad/tools/DssValidator.java'
  : path.replaceAll('PXA', 'PVAD').replaceAll('Pxa', 'Pvad').replaceAll('pxa', 'pvad');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

export function inspectDeletedPairs(repositoryRoot = root) {
  const paths = execFileSync('git', ['diff', 'HEAD', '--no-renames', '--name-only', '--diff-filter=D', '-z'],
    { cwd: repositoryRoot, maxBuffer: 20 * 1024 * 1024 }).toString().split('\0').filter(Boolean);
  const tree = new Map(execFileSync('git', ['ls-tree', '-r', '-z', 'HEAD'],
    { cwd: repositoryRoot, maxBuffer: 20 * 1024 * 1024 }).toString().split('\0').filter(Boolean).map(entry => {
    const tab = entry.indexOf('\t');
    return [entry.slice(tab + 1), entry.slice(0, tab).split(' ')[2]];
  }));
  const blobs = readGitBlobs(repositoryRoot, paths.map(path => tree.get(path)));
  return paths.map(path => {
    const before = blobs.get(tree.get(path));
    if (!before) throw new Error('Missing deleted-file baseline blob');
    const canonical = canonicalPath(path);
    if (existsSync(resolve(repositoryRoot, canonical))) {
      const after = readFileSync(resolve(repositoryRoot, canonical));
      return { source: path, target: canonical, sourceSha256: hash(before), targetSha256: hash(after),
        status: 'paired-requires-content-review' };
    }
    const retained = path.replace('websites/PXA.Documentation/', 'websites/PVAD.Documentation/');
    // Only this pinned external-signature evidence family may retain its physical basename.
    if (/^websites\/PXA\.Documentation\/public\/downloads\/pdf-trust\/pades\/(?:negative\/)?pxa-pades-[\w-]+\.(?:pdf|xml)$/.test(path)
        && existsSync(resolve(repositoryRoot, retained))) {
      const after = readFileSync(resolve(repositoryRoot, retained));
      return { source: path, target: retained, sourceSha256: hash(before), targetSha256: hash(after),
        status: before.equals(after) ? 'retained-evidence-byte-identical' : 'retained-evidence-modified' };
    }
    return { source: path, proposedTarget: canonical, status: 'unresolved' };
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const pairs = inspectDeletedPairs();
  const failed = pairs.filter(pair => ['unresolved', 'retained-evidence-modified'].includes(pair.status));
  mkdirSync(resolve(root, 'artifacts/rename'), { recursive: true });
  writeFileSync(resolve(root, 'artifacts/rename/file-pairs.json'), JSON.stringify({
    scope: 'HEAD tracked deletions (staged or unstaged) versus current working files; pairing is not semantic approval',
    pairs,
  }, null, 2) + '\n');
  console.log(JSON.stringify({ paired: pairs.length - failed.length,
    retainedEvidence: pairs.filter(pair => pair.status === 'retained-evidence-byte-identical').length,
    unresolved: failed.length }));
  if (failed.length) process.exitCode = 1;
}
