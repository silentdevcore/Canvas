#!/usr/bin/env node
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const manifestPath = path.resolve(process.argv[2] ?? path.join(
  root, 'product-metadata/pdf-trust/external-evidence.json'));
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const errors = [];
const ids = new Set();
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');

if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.entries))
  errors.push('Manifest must use schemaVersion 1 and contain entries.');

for (const entry of manifest.entries ?? []) {
  if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(entry.id ?? '')) errors.push(`Invalid evidence id '${entry.id}'.`);
  if (ids.has(entry.id)) errors.push(`Duplicate evidence id '${entry.id}'.`);
  ids.add(entry.id);
  if (!['pdfa', 'pdfua', 'aes256', 'signature', 'assistive-technology'].includes(entry.kind))
    errors.push(`${entry.id} has an unsupported kind.`);
  if (!['pass', 'fail', 'blocked'].includes(entry.outcome)) errors.push(`${entry.id} has an invalid outcome.`);
  if (entry.synthetic !== true) errors.push(`${entry.id} must explicitly identify synthetic evidence.`);
  for (const [property, expectedHash] of [['input', entry.inputSha256], ['evidence', entry.evidenceSha256]]) {
    const relative = entry[property];
    if (typeof relative !== 'string' || path.isAbsolute(relative) || relative.includes('..')) {
      errors.push(`${entry.id} has an unsafe ${property} path.`);
      continue;
    }
    const file = path.join(root, relative);
    if (!fs.existsSync(file)) errors.push(`${entry.id} ${property} is missing: ${relative}`);
    else if (hash(file) !== expectedHash) errors.push(`${entry.id} ${property} hash does not match.`);
  }

  if (entry.outcome === 'pass' && entry.kind === 'pdfa') {
    const content = fs.readFileSync(path.join(root, entry.evidence), 'utf8');
    if (!content.includes('isCompliant="true"')) errors.push(`${entry.id} does not contain a passing validator result.`);
  }
  if (entry.outcome === 'pass' && entry.kind === 'pdfua') {
    const content = fs.readFileSync(path.join(root, entry.evidence), 'utf8');
    if (!content.includes('isCompliant="true"')) errors.push(`${entry.id} does not contain a passing validator result.`);
  }
}

if (errors.length) {
  for (const error of errors) console.error(`PDF Trust evidence: ${error}`);
  process.exit(1);
}
console.log(`PDF Trust evidence is valid: ${manifest.entries.length} pinned entries.`);
