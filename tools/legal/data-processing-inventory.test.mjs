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

test('native license issuance records are inventoried without approving their retention', () => {
  const activities = inventory.activities.filter(activity => activity.entities.includes('NativeLibraryLicense'));
  assert.equal(activities.length, 1);
  const activity = activities[0];
  assert.equal(activity.id, 'subscriptions-usage-and-licensing');
  assert.equal(activity.role, 'controller');
  assert.ok(activity.entities.includes('OfflineLicense'));
  assert.ok(activity.dataSubjects.includes('manufacturer operators'));
  assert.ok(activity.dataCategories.includes('native license artifacts and hashes'));
  assert.ok(activity.dataCategories.includes('customer reference and issuer identifiers'));
  assert.ok(activity.dataCategories.includes('idempotency and renewal-chain references'));
  assert.ok(activity.sources.includes('src/Core/PVAD.Domain/Entities/NativeLibraryLicense.cs'));
  assert.ok(activity.sources.includes('PVAD.WebApi/Services/Licensing/PvadNativeLicenseIssuanceService.cs'));
  assert.equal(activity.retention.approvalStatus, 'pending-legal');
  assert.equal(activity.retention.legalApprovalRequired, true);
  assert.match(activity.retention.rule, /native license.*retention.*not yet approved/i);
  assert.equal(inventory.productionApproved, false);
});
