import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function validateCountryReadiness(repositoryRoot = root) {
  const matrix = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/country-readiness.json'), 'utf8'));
  const commerce = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/global-commerce-catalog.json'), 'utf8'));
  const errors = [];
  const requirementIds = new Set();
  const coveredCountries = new Set();

  for (const requirement of matrix.requirements ?? []) {
    if (requirementIds.has(requirement.id)) errors.push(`Duplicate requirement id: ${requirement.id}`);
    requirementIds.add(requirement.id);
    if (!requirement.source?.startsWith('https://')) errors.push(`Requirement ${requirement.id} lacks an HTTPS source.`);
  }

  for (const region of matrix.regions ?? []) {
    for (const requirementId of region.requirementIds ?? []) {
      if (!requirementIds.has(requirementId)) errors.push(`Unknown requirement ${requirementId} in ${region.id}`);
    }
    for (const countryCode of region.countryCodes ?? []) {
      if (coveredCountries.has(countryCode)) errors.push(`Country appears in multiple readiness regions: ${countryCode}`);
      coveredCountries.add(countryCode);
    }
    if (region.b2cStatus === 'approved' && region.blockers?.length)
      errors.push(`B2C region ${region.id} cannot be approved with unresolved blockers.`);
    if (!commerce.priceBooks.some(book => book.currency === region.priceBookCurrency))
      errors.push(`Region ${region.id} references a missing ${region.priceBookCurrency} price book.`);
  }

  for (const override of matrix.countryOverrides ?? []) {
    if (!coveredCountries.has(override.countryCode)) errors.push(`Override country is not covered by a region: ${override.countryCode}`);
    for (const requirementId of override.requirementIds ?? []) {
      if (!requirementIds.has(requirementId)) errors.push(`Unknown override requirement ${requirementId} for ${override.countryCode}`);
    }
    if (override.status === 'approved' && override.blockers?.length)
      errors.push(`Country override ${override.countryCode} cannot be approved with blockers.`);
  }

  const commerceCountries = new Set((commerce.marketGroups ?? []).flatMap(group => group.countryCodes ?? []));
  for (const countryCode of commerceCountries) {
    if (!coveredCountries.has(countryCode)) errors.push(`Commerce candidate lacks country readiness: ${countryCode}`);
  }
  for (const countryCode of coveredCountries) {
    if (!commerceCountries.has(countryCode)) errors.push(`Country readiness is not a commerce candidate: ${countryCode}`);
  }

  if (matrix.productionApproved) {
    if (!commerce.productionApproved) errors.push('Country readiness cannot be approved before the commerce catalog.');
    if (matrix.regions.some(region => region.b2bStatus !== 'approved' && region.b2cStatus !== 'approved'))
      errors.push('Production approval requires at least one approved sales mode in every included region.');
  }

  return { matrix, errors, coveredCountries: coveredCountries.size, requirements: requirementIds.size };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = validateCountryReadiness();
  if (result.errors.length) {
    console.error(result.errors.map(error => `- ${error}`).join('\n'));
    process.exitCode = 1;
  } else {
    console.log(`Validated ${result.coveredCountries} country candidates across ${result.matrix.regions.length} regions with ${result.requirements} sourced requirements; no market is approved.`);
  }
}
