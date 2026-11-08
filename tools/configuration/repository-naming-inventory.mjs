import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { findFormerShortNameMatches } from './rename-artifact-audit.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const privatePath = path => /(?:^|\/)(?:\.vscode|\.git|\.aws|\.codex)(?:\/|$)/i.test(path);
const textSource = path => /\.(?:cs|csproj|sln|slnx|ts|tsx|js|jsx|mjs|cjs|mts|cts|json|jsonc|json5|md|xml|html|css|scss|ya?ml|sh|sql|conf|txt|tsv|tmpl|template|envsh|rdl|props|targets)$/i.test(path);
export const inventoryPath = 'artifacts/rename/repository-name-inventory-current.json';

function regularTrackedFile(root, path) {
  let current = root;
  for (const component of path.split('/')) {
    current = join(current, component);
    const stat = lstatSync(current, { throwIfNoEntry: false });
    if (!stat || stat.isSymbolicLink()) return false;
  }
  return lstatSync(current).isFile();
}

export function scanRepositoryNames(root) {
  const git = args => execFileSync('git', args, {
    cwd: root, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024,
  }).split('\0').filter(Boolean);
  const paths = [...new Set(git(['ls-files', '-z']))].sort();
  const ignored = new Set(git(['ls-files', '--cached', '--ignored', '--exclude-standard', '-z']));
  const excluded = { private: 0, ignored: 0, nonRegularOrMissing: 0 };
  const counts = { scannedFiles: 0, textFiles: 0, binaryFiles: 0, matchingFiles: 0,
    textOccurrences: 0, binaryOccurrences: 0, pathOccurrences: 0 };
  const files = [];
  for (const path of paths) {
    if (privatePath(path)) { excluded.private++; continue; }
    if (ignored.has(path)) { excluded.ignored++; continue; }
    if (!regularTrackedFile(root, path)) { excluded.nonRegularOrMissing++; continue; }
    const bytes = readFileSync(resolve(root, path));
    counts.scannedFiles++;
    let source;
    const pdf = bytes.subarray(0, 5).toString('ascii') === '%PDF-';
    if (!pdf && (!bytes.includes(0) || textSource(path))) {
      try { source = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes); }
      catch { /* Non-UTF-8 payloads are reported as binary evidence. */ }
    }
    const kind = source === undefined ? 'binary' : 'text';
    counts[kind === 'text' ? 'textFiles' : 'binaryFiles']++;
    const pathMatches = findFormerShortNameMatches(path);
    let records;
    if (kind === 'text') {
      records = source.split('\n').flatMap((line, index) => {
        const matches = findFormerShortNameMatches(line);
        return matches.length ? [{ line: index + 1, lineSha256: hash(line),
          matches: matches.map(match => ({ column: match.offset + 1, token: match.token })) }] : [];
      });
    } else {
      records = findFormerShortNameMatches(bytes.toString('latin1'));
    }
    const occurrences = kind === 'text'
      ? records.reduce((sum, row) => sum + row.matches.length, 0) : records.length;
    counts[kind === 'text' ? 'textOccurrences' : 'binaryOccurrences'] += occurrences;
    counts.pathOccurrences += pathMatches.length;
    if (!occurrences && !pathMatches.length) continue;
    files.push({ path, kind, sha256: hash(bytes), bytes: bytes.length,
      containsNullBytes: bytes.includes(0), pathMatches, occurrences, records });
  }
  counts.matchingFiles = files.length;
  return { schemaVersion: 1,
    scope: 'Tracked regular files only, excluding private IDE paths, symlinks, untracked files and paths matching ignore rules, even if tracked. Text source containing null bytes remains scanned.',
    limitation: 'Inventory only, not an allowlist, semantic approval or completion percentage. Binary offsets are ASCII-byte matches, not evidence of visible branding. Source lines and matching suffix values are never copied.',
    counts, excluded, files };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const args = process.argv.slice(2);
  if (args.length > 1 || args.some(value => value !== '--write-report'))
    throw new Error('Only --write-report is supported.');
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const report = { baseline: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
    ...scanRepositoryNames(root) };
  if (args.includes('--write-report')) {
    mkdirSync(resolve(root, 'artifacts/rename'), { recursive: true });
    writeFileSync(resolve(root, inventoryPath), JSON.stringify(report, null, 2) + '\n');
  }
  console.log(JSON.stringify({ ...report.counts, excluded: report.excluded,
    ...(args.includes('--write-report') ? { report: inventoryPath } : {}) }));
}
