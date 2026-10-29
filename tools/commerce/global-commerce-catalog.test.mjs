import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { validateGlobalCommerceCatalog } from './validate-global-commerce-catalog.mjs';

test('global commerce proposal remains non-public and fail-closed', () => {
  const { catalog, errors } = validateGlobalCommerceCatalog();

  assert.deepEqual(errors, []);
  assert.equal(catalog.status, 'proposed');
  assert.equal(catalog.productionApproved, false);
  assert.equal(catalog.publicPricingEnabled, false);
  assert.equal(catalog.consumerCheckoutEnabled, false);
  assert.equal(catalog.merchantOfRecord.recommendedCandidate, 'Paddle');
  assert.equal(catalog.launchRecommendation.status, 'proposed');
  assert.equal(catalog.launchRecommendation.salesMode, 'b2b-first');
  assert.equal(catalog.launchRecommendation.firstOperatorCountry, 'CH');
  assert.equal(catalog.launchRecommendation.firstSalesRegion, 'Switzerland');
  assert.equal(catalog.launchRecommendation.automaticTrialConversion, false);
  assert.ok(catalog.marketGroups.every(group => group.b2cStatus === 'review-required'));
  assert.deepEqual(catalog.approvalOwners, ['Product', 'Finance', 'Tax', 'Legal']);
});

test('catalog exposes the proposed plans and stable initial price-book currencies', () => {
  const { catalog } = validateGlobalCommerceCatalog();
  assert.deepEqual(catalog.priceBooks.map(book => book.currency), ['USD', 'EUR', 'GBP', 'CHF']);
  assert.ok(catalog.plans.some(plan => plan.id === 'premium-individual'));
  assert.ok(catalog.plans.some(plan => plan.id === 'premium-company'));
  assert.ok(catalog.plans.some(plan => plan.visibility === 'internal-qualification'));
});

test('command-line validation reports disabled Production commerce', () => {
  const validator = fileURLToPath(new URL('./validate-global-commerce-catalog.mjs', import.meta.url));
  const output = execFileSync(process.execPath, [validator], { encoding: 'utf8' });
  assert.match(output, /Validated 6 proposed plans, 4 currencies, and 36 country candidates/);
  assert.match(output, /Production commerce remains disabled/);
});
