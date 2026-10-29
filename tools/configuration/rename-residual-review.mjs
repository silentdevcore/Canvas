import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, readFileSync, readlinkSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const ledgerPath = 'docs/pvad-rename-reviewed-exceptions.json';
export const hasLegacyIdentity = value => /pxa|power\s?dox|canvas/i.test(value);
export const reasons = Object.freeze({
  'drawing-api': 'Neutral drawing surfaces, HTML canvas, vendor APIs or third-party dependency identities, not product branding.',
  'compatibility-contract': 'Retained diagnostics, persisted or cryptographic identifiers, legacy readers, alias adapters and their explanatory documentation. See the linked boundary guides.',
  'historical-checklist': 'Original implementation history retained with an explicit Naming history notice; task status is not renamed or reapproved.',
  'historical-document': 'Historical migration/removal/design notes or rename audit evidence, not current product naming instructions.',
  'test-contract': 'Synthetic legacy inputs, expected diagnostics, compatibility assertions or neutral drawing APIs in regression tests and test tooling.',
  'retained-evidence': 'Original source fixtures, signed/hashed evidence, accepted legal content or published release history; changing their text would alter the evidence.',
  'infrastructure-binding': 'Retained physical service, volume, dashboard, rule, OAuth, schema, registry or host identity pending coordinated external cutover; canonical environment aliases are tested separately.',
  'incidental-token': 'An incidental substring inside vendor data or integrity-checked encoded bytes; not a first-party product identifier.',
});

export function residualFiles(root) {
  const files = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '-z'],
    { cwd: root, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 }).split('\0').filter(Boolean))].sort();
  return files.flatMap(path => {
    if (path === ledgerPath || !existsSync(resolve(root, path))) return [];
    const absolute = resolve(root, path);
    const stat = lstatSync(absolute);
    if (!stat.isFile() && !stat.isSymbolicLink()) return [];
    const bytes = stat.isSymbolicLink() ? Buffer.from(readlinkSync(absolute)) : readFileSync(absolute);
    if (bytes.includes(0)) return [];
    if (!hasLegacyIdentity(path + '\n' + bytes.toString('utf8'))) return [];
    return [{ path, sha256: createHash('sha256').update(bytes).digest('hex') }];
  });
}

export function validateReview(files, ledger) {
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
  failures.push(...recorded.keys());
  return failures.sort();
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const ledger = JSON.parse(readFileSync(resolve(root, ledgerPath), 'utf8'));
  const failures = validateReview(residualFiles(root), ledger);
  for (const entry of ledger.files) {
    if (typeof entry.boundaryGuide !== 'string' || !existsSync(resolve(root, entry.boundaryGuide)))
      failures.push(`${entry.path}: missing boundary guide`);
  }
  if (failures.length) {
    console.error(JSON.stringify({ reviewRequired: failures }));
    process.exitCode = 1;
  } else console.log('All text residual files match the reviewed path/hash/reason ledger. Binary evidence is reviewed separately.');
}
