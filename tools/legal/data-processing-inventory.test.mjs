import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/data-processing-inventory.json', import.meta.url), 'utf8'));

test('processing inventory remains a production gate until legal decisions are approved', () => {
  assert.equal(inventory.productionApproved, false);
  assert.match(inventory.controllerIdentity, /pending/i);
  assert.ok(inventory.activities.some(activity => activity.role === 'processor-for-customer-content'));
  assert.ok(inventory.activities.some(activity => activity.retention.status === 'legal-review-required'));
  assert.ok(inventory.activities.some(activity => activity.retention.approvalStatus === 'pending-legal'));
  assert.ok(inventory.providers.some(provider => provider.transferRisk === 'conditional'));
});

test('every persisted entity, provider, region, transfer, and retention rule is inventoried', () => {
  const validator = fileURLToPath(new URL('./validate-data-processing-inventory.mjs', import.meta.url));
  const output = execFileSync(process.execPath, [validator], { encoding: 'utf8' });
  assert.match(output, /Validated \d+ processing activities, \d+ persisted entities, and \d+ providers/);
});
