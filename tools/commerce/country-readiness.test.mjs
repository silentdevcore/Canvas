import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { validateCountryReadiness } from './validate-country-readiness.mjs';

test('every commerce country is covered exactly once and remains gated', () => {
  const { matrix, errors, coveredCountries } = validateCountryReadiness();
  assert.deepEqual(errors, []);
  assert.equal(coveredCountries, 36);
  assert.equal(matrix.productionApproved, false);
  assert.ok(matrix.regions.every(region => region.b2bStatus !== 'approved'));
  assert.ok(matrix.regions.every(region => region.b2cStatus !== 'approved'));
});

test('Switzerland, EU, non-EU EEA, UK, and US requirements remain distinct', () => {
  const { matrix } = validateCountryReadiness();
  const switzerland = matrix.regions.find(region => region.id === 'switzerland');
  const eu = matrix.regions.find(region => region.id === 'eu-27');
  const eea = matrix.regions.find(region => region.id === 'eea-non-eu');
  const uk = matrix.regions.find(region => region.id === 'united-kingdom');
  const us = matrix.regions.find(region => region.id === 'united-states');

  assert.ok(switzerland.requirementIds.includes('ch-online-contracts'));
  assert.ok(switzerland.requirementIds.includes('ch-fadp-and-transfers'));
  assert.equal(switzerland.priceBookCurrency, 'CHF');
  assert.ok(eu.requirementIds.includes('eu-vat-oss'));
  assert.ok(eea.requirementIds.includes('eea-local-vat'));
  assert.ok(!eea.requirementIds.includes('eu-vat-oss'));
  assert.ok(uk.requirementIds.includes('uk-subscription-contracts'));
  assert.ok(us.requirementIds.includes('us-sales-tax-nexus'));
  assert.ok(us.requirementIds.includes('us-export-and-sanctions'));
});

test('Australia, Canada, and New Zealand have separate tax, privacy, and consumer gates', () => {
  const { matrix } = validateCountryReadiness();
  const australia = matrix.regions.find(region => region.id === 'australia');
  const canada = matrix.regions.find(region => region.id === 'canada');
  const newZealand = matrix.regions.find(region => region.id === 'new-zealand');

  assert.ok(australia.requirementIds.includes('au-gst-digital-services'));
  assert.ok(australia.requirementIds.includes('au-privacy-cross-border'));
  assert.ok(canada.requirementIds.includes('ca-gst-hst-digital-services'));
  assert.ok(canada.requirementIds.includes('ca-privacy-federal-provincial'));
  assert.equal(canada.localizationStatus, 'insufficient');
  assert.ok(newZealand.requirementIds.includes('nz-gst-remote-services'));
  assert.ok(newZealand.requirementIds.includes('nz-privacy-cross-border'));
});

test('command-line validation reports that no market is approved', () => {
  const validator = fileURLToPath(new URL('./validate-country-readiness.mjs', import.meta.url));
  const output = execFileSync(process.execPath, [validator], { encoding: 'utf8' });
  assert.match(output, /Validated 36 country candidates across 8 regions with 26 sourced requirements/);
  assert.match(output, /no market is approved/);
});
