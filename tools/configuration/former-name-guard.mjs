#!/usr/bin/env node
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const allowlistPath = 'docs/pvad-former-name-allowlist.v2.json';
export const reasons = Object.freeze({
  'published-evidence': 'Signed, hashed or published evidence and approved baselines; changing bytes would falsify them.',
  'third-party-bytes': 'Incidental byte sequences in vendor models, compressed images, samples or lockfile integrity hashes.',
  'historical-record': 'Dated audit history, immutable v1 registers, applied migrations and the former-to-current data migration.',
  'former-name-detector': 'Tooling that must know the former name to detect or reject it.',
  'rejection-or-absence-test': 'Tests that assert former names are absent, rejected or ignored.',
  'evidence-reference': 'Code that reads published evidence or historical releases by their original names.',
  'external-binding': 'Bindings to external resources that stay unchanged until the external cutover gates (G01-G08).',
});
// Pinned categories must keep their exact bytes; the others are reviewed by exact path only.
const pinned = new Set(['published-evidence', 'third-party-bytes']);
const privatePath = path => /(?:^|\/)(?:\.vscode|\.git|\.aws|\.codex)(?:\/|$)/i.test(path) || /^artifacts\//.test(path);
export const hasFormerName = text => /pxa|power\s?dox/i.test(text);
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

export function candidateFiles(root) {
  const paths = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'],
    { cwd: root, maxBuffer: 64 * 1024 * 1024 }).toString().split('\0').filter(Boolean);
  return [...new Set(paths)].sort().flatMap(path => {
    // The allowlist names retained paths by design and is reviewed as data, not as a residual.
    if (privatePath(path) || path === allowlistPath) return [];
    const stat = lstatSync(resolve(root, path), { throwIfNoEntry: false });
    if (!stat?.isFile()) return [];
    const bytes = readFileSync(resolve(root, path));
    return hasFormerName(path) || hasFormerName(bytes.toString('latin1')) ? [{ path, sha256: sha256(bytes) }] : [];
  });
}

export function validateAllowlist(files, allowlist) {
  if (allowlist.schemaVersion !== 2 || !Array.isArray(allowlist.entries)) throw new Error('Invalid former-name allowlist.');
  const entries = new Map();
  const failures = [];
  for (const entry of allowlist.entries) {
    if (typeof entry.path !== 'string' || entries.has(entry.path) || !Object.hasOwn(reasons, entry.reason)
        || (pinned.has(entry.reason) && !/^[a-f0-9]{64}$/.test(entry.sha256 ?? '')))
      failures.push(`invalid entry: ${entry.path}`);
    entries.set(entry.path, entry);
  }
  for (const file of files) {
    const entry = entries.get(file.path);
    if (!entry) failures.push(`unreviewed former name: ${file.path}`);
    else if (pinned.has(entry.reason) && entry.sha256 !== file.sha256) failures.push(`changed pinned file: ${file.path}`);
    entries.delete(file.path);
  }
  for (const path of entries.keys()) failures.push(`stale allowlist entry: ${path}`);
  return failures.sort();
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const files = candidateFiles(root);
  const allowlist = JSON.parse(readFileSync(resolve(root, allowlistPath), 'utf8'));
  const failures = validateAllowlist(files, allowlist);
  if (failures.length) {
    console.error(JSON.stringify({ failures }, null, 2));
    process.exitCode = 1;
  } else console.log(`Former name appears only in ${files.length} reviewed files.`);
}
