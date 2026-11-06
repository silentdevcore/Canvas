import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const ledgerPath = 'docs/pvad-rename-reviewed-exceptions.json';
export const hasLegacyIdentity = value => /pxa|power\s?dox|canvas/i.test(value);
export const reasons = Object.freeze({
  'drawing-api': 'Neutral drawing surfaces, HTML canvas, vendor APIs or third-party dependency identities, not product branding.',
  'compatibility-contract': 'Retained diagnostics, persisted or cryptographic identifiers, legacy readers, alias adapters and their explanatory documentation. See the linked boundary guides.',
  'historical-checklist': 'Retained checklist execution evidence, original audit context or compatibility references; editorial names may be normalized without changing task status or reapproving results.',
  'historical-document': 'Historical migration/removal/design notes or rename audit evidence, not current product naming instructions.',
  'test-contract': 'Synthetic legacy inputs, expected diagnostics, compatibility assertions or neutral drawing APIs in regression tests and test tooling.',
  'retained-evidence': 'Original source fixtures, signed/hashed evidence, accepted legal content or published release history; changing their text would alter the evidence.',
  'infrastructure-binding': 'Retained physical service, volume, dashboard, rule, OAuth, schema, registry or host identity pending coordinated external cutover; canonical environment aliases are tested separately.',
  'incidental-token': 'An incidental substring inside vendor data or integrity-checked encoded bytes; not a first-party product identifier.',
});

export function residualFiles(root) {
  const files = [...new Set(execFileSync('git', ['ls-files', '-z'],
    { cwd: root, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 }).split('\0').filter(Boolean))].sort();
  return files.flatMap(path => {
    if (path === ledgerPath || /(?:^|\/)(?:\.vscode|\.git|\.aws|\.codex)(?:\/|$)/i.test(path)) return [];
    const absolute = resolve(root, path);
    if (!lstatSync(absolute, { throwIfNoEntry: false })?.isFile()) return [];
    const bytes = readFileSync(absolute);
    // Source text can contain literal null bytes inside synthetic fixture strings.
    if (bytes.includes(0) && !/\.(?:cs|csproj|sln|ts|tsx|js|jsx|mjs|cjs|mts|cts|json|jsonc|json5|md|xml|html|css|scss|ya?ml|sh|sql|conf|txt|tsv)$/i.test(path)) return [];
    if (!hasLegacyIdentity(path + '\n' + bytes.toString('utf8'))) return [];
    return [{ path, sha256: createHash('sha256').update(bytes).digest('hex') }];
  });
}

export function resolvedReviewFiles(root, ledger) {
  const entries = ledger.files.filter(entry => entry.reviewStatus === 'resolved');
  if (!entries.length) return [];
  const tracked = new Set(execFileSync('git', ['ls-files', '-z'],
    { cwd: root, encoding: 'utf8' }).split('\0'));
  return entries.flatMap(entry => {
    if (!tracked.has(entry.path) || /(?:^|\/)(?:\.vscode|\.git|\.aws|\.codex)(?:\/|$)/i.test(entry.path)) return [];
    const absolute = resolve(root, entry.path);
    if (!lstatSync(absolute, { throwIfNoEntry: false })?.isFile()) return [];
    const bytes = readFileSync(absolute);
    if (hasLegacyIdentity(entry.path + '\n' + bytes.toString('utf8'))) return [];
    return [{ path: entry.path, sha256: createHash('sha256').update(bytes).digest('hex') }];
  });
}

export function validateReview(files, ledger, resolvedFiles = []) {
  if (ledger.schemaVersion !== 1 || !Array.isArray(ledger.files)) throw new Error('Invalid residual review schema.');
  const recorded = new Map();
  for (const entry of ledger.files) {
    if (typeof entry.path !== 'string' || !/^[a-f0-9]{64}$/.test(entry.sha256)
      || !Array.isArray(entry.reasons) || !entry.reasons.length
      || entry.reasons.some(reason => !Object.hasOwn(reasons, reason)) || recorded.has(entry.path))
      throw new Error('Invalid, duplicate or unexplained residual review entry.');
    recorded.set(entry.path, entry);
  }
  const failures = [];
  for (const file of files) {
    const entry = recorded.get(file.path);
    if (!entry || entry.sha256 !== file.sha256 || entry.reviewStatus !== 'reviewed'
      || typeof entry.reviewNotes !== 'string' || entry.reviewNotes.trim().length < 20) failures.push(file.path);
    recorded.delete(file.path);
  }
  const resolved = new Map(resolvedFiles.map(file => [file.path, file.sha256]));
  for (const entry of recorded.values()) {
    if (entry.reviewStatus !== 'resolved' || resolved.get(entry.path) !== entry.sha256 ||
        typeof entry.reviewNotes !== 'string' || entry.reviewNotes.trim().length < 20)
      failures.push(entry.path);
  }
  return failures.sort();
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const ledger = JSON.parse(readFileSync(resolve(root, ledgerPath), 'utf8'));
  const failures = validateReview(residualFiles(root), ledger, resolvedReviewFiles(root, ledger));
  for (const entry of ledger.files) {
    if (typeof entry.boundaryGuide !== 'string' || !existsSync(resolve(root, entry.boundaryGuide)))
      failures.push(`${entry.path}: missing boundary guide`);
  }
  if (failures.length) {
    console.error(JSON.stringify({ reviewRequired: failures }));
    process.exitCode = 1;
  } else console.log('All text residual files match the reviewed path/hash/reason ledger. Binary evidence is reviewed separately.');
}
