#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const baselinePath = path.join(root, 'product-metadata/pdf-trust/standards-baseline.json');
const releaseMode = process.argv.includes('--release');
const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
const required = new Set(['iso-32000', 'iso-19005', 'iso-14289', 'etsi-en-319-142-1']);
const errors = [];

if (baseline.schemaVersion !== 1) errors.push('schemaVersion must be 1.');
if (baseline.policyId !== 'pxa-pdf-trust') errors.push('policyId must be pxa-pdf-trust.');
if (!Array.isArray(baseline.standards)) errors.push('standards must be an array.');

const ids = new Set();
for (const standard of baseline.standards ?? []) {
  if (!required.has(standard.id)) errors.push(`Unknown standard '${standard.id}'.`);
  if (ids.has(standard.id)) errors.push(`Duplicate standard '${standard.id}'.`);
  ids.add(standard.id);
  if (!Array.isArray(standard.requiredFor) || standard.requiredFor.length === 0)
    errors.push(`${standard.id} must declare requiredFor profiles.`);
  if (!Array.isArray(standard.corrigenda)) errors.push(`${standard.id} corrigenda must be an array.`);
  if (!['license-required', 'licensed-copy-archived', 'public-copy-recorded'].includes(standard.licenseStatus))
    errors.push(`${standard.id} has an invalid licenseStatus.`);
  if (releaseMode && (!standard.approved || !standard.edition || standard.licenseStatus === 'license-required'))
    errors.push(`${standard.id} is not approved with an archived exact edition.`);
}
for (const id of required) if (!ids.has(id)) errors.push(`Missing required standard '${id}'.`);

for (const review of baseline.errataReviews ?? []) {
  if (!ids.has(review.standardId)) errors.push(`Errata review references unknown standard '${review.standardId}'.`);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(review.reviewedAt ?? ''))
    errors.push(`Errata review for '${review.standardId}' requires an ISO date.`);
  if (!['accepted', 'rejected', 'not-applicable'].includes(review.decision))
    errors.push(`Errata review for '${review.standardId}' has an invalid decision.`);
}

if (releaseMode && baseline.reviewStatus !== 'approved')
  errors.push('The standards policy is not externally approved.');

if (errors.length) {
  for (const error of errors) console.error(`PDF Trust standards: ${error}`);
  process.exit(1);
}
console.log(`PDF Trust standards baseline is structurally valid${releaseMode ? ' and release-approved' : ''}.`);
