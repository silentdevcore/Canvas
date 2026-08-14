import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function inspectLegalLaunchReadiness(repositoryRoot = root) {
  const inventory = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/data-processing-inventory.json'),
    'utf8'));
  const browserStorage = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/browser-storage.json'),
    'utf8'));
  const companyLegalSource = readFileSync(
    join(repositoryRoot, 'websites/PXA.Company/src/main.js'),
    'utf8');
  const productionSettings = JSON.parse(readFileSync(
    join(repositoryRoot, 'PXA.WebApi/appsettings.Production.json'),
    'utf8'));
  const commerce = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/global-commerce-catalog.json'),
    'utf8'));
  const countryReadiness = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/country-readiness.json'),
    'utf8'));

  const blockers = [];
  const violations = [];

  if (!inventory.controllerIdentity || /pending|placeholder|tbd/i.test(inventory.controllerIdentity)) {
    blockers.push('Verified operator identity is missing.');
  }
  if (inventory.productionApproved !== true) {
    blockers.push('The data-processing inventory is not approved for Production.');
  }

  const pendingRetention = (inventory.activities ?? [])
    .filter(activity => activity.retention?.approvalStatus !== 'approved')
    .map(activity => activity.id);
  if (pendingRetention.length) {
    blockers.push(`${pendingRetention.length} retention decisions still require approval: ${pendingRetention.join(', ')}.`);
  }

  if (/\b(?:draft|placeholder|launch blocker|requires counsel-approved wording)\b/i.test(companyLegalSource)) {
    blockers.push('Company Legal pages still contain draft or launch-blocking copy.');
  }
  if (/\[(?:Legal company|Registered address|Commercial register)/i.test(companyLegalSource)) {
    blockers.push('Company Imprint still contains bracketed operator placeholders.');
  }

  const obsoleteOdrPattern = /https?:\/\/(?:ec\.)?europa\.eu\/consumers\/odr/i;
  if (obsoleteOdrPattern.test(companyLegalSource)) {
    violations.push('Company Legal content contains an obsolete EU ODR platform URL.');
  }

  const optionalEntries = (browserStorage.entries ?? []).filter(entry => entry.optional === true);
  if (browserStorage.optionalStorageEnabled === false && optionalEntries.length) {
    violations.push('Optional browser storage is inventoried while the launch policy disables it.');
  }
  if (browserStorage.optionalStorageEnabled === true) {
    blockers.push('Optional browser storage is enabled; Consent Center readiness requires separate verification.');
  }

  if (productionSettings.ConsumerCheckout?.Enabled === true) {
    blockers.push('Consumer checkout is enabled; the complete B2C and electronic-withdrawal workflow requires verified sign-off.');
  }
  if (commerce.productionApproved !== true) {
    blockers.push('The global commerce catalog is not approved for Production.');
  }
  if (countryReadiness.productionApproved !== true ||
      !countryReadiness.regions?.some(region => region.b2bStatus === 'approved' || region.b2cStatus === 'approved')) {
    blockers.push('No country market has an approved B2B or B2C sales mode.');
  }

  return { blockers, violations };
}

function printReport(result) {
  console.log('PXA Legal launch readiness');
  console.log(`Structural violations: ${result.violations.length}`);
  console.log(`Launch blockers: ${result.blockers.length}`);
  for (const message of result.violations) console.log(`- VIOLATION: ${message}`);
  for (const message of result.blockers) console.log(`- BLOCKER: ${message}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const production = process.argv.includes('--production');
  const result = inspectLegalLaunchReadiness();
  printReport(result);

  if (result.violations.length || (production && result.blockers.length)) {
    process.exitCode = 1;
  } else if (result.blockers.length) {
    console.log('Development validation passed; Production remains fail-closed.');
  } else {
    console.log('Legal launch-readiness validation passed.');
  }
}
