import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function validateGlobalCommerceCatalog(repositoryRoot = root) {
  const catalog = JSON.parse(readFileSync(
    join(repositoryRoot, 'product-metadata/global-commerce-catalog.json'), 'utf8'));
  const errors = [];
  const requiredCurrencies = ['USD', 'EUR', 'GBP'];
  const requiredOwners = ['Product', 'Finance', 'Tax', 'Legal'];
  const planIds = new Set();

  for (const plan of catalog.plans ?? []) {
    if (planIds.has(plan.id)) errors.push(`Duplicate plan id: ${plan.id}`);
    planIds.add(plan.id);
    if (plan.edition === 'Trial' && plan.trialDays !== 30)
      errors.push('The proposed Trial must remain 30 days until a new commercial decision is approved.');
    if (plan.visibility === 'public' && !catalog.publicPricingEnabled)
      errors.push(`Plan ${plan.id} cannot be public while public pricing is disabled.`);
  }

  const currencies = new Set();
  const priceBookIds = new Set();
  for (const book of catalog.priceBooks ?? []) {
    if (priceBookIds.has(book.id)) errors.push(`Duplicate price-book id: ${book.id}`);
    priceBookIds.add(book.id);
    if (currencies.has(book.currency)) errors.push(`Duplicate active proposal currency: ${book.currency}`);
    currencies.add(book.currency);
    if (book.status === 'approved' && !book.effectiveAt)
      errors.push(`Approved price book ${book.id} requires an effective date.`);

    const pricedPlans = new Set();
    for (const price of book.prices ?? []) {
      if (!planIds.has(price.planId)) errors.push(`Unknown plan ${price.planId} in ${book.id}`);
      if (pricedPlans.has(price.planId)) errors.push(`Duplicate plan ${price.planId} in ${book.id}`);
      pricedPlans.add(price.planId);
      if (price.monthlyAmountMinor != null && price.annualAmountMinor != null &&
          price.annualAmountMinor > price.monthlyAmountMinor * 12)
        errors.push(`Annual price exceeds twelve monthly payments for ${price.planId} in ${book.id}.`);
    }
  }
  for (const currency of requiredCurrencies) {
    if (!currencies.has(currency)) errors.push(`Missing initial price-book currency: ${currency}`);
  }

  const countryCodes = new Set();
  for (const market of catalog.marketGroups ?? []) {
    for (const countryCode of market.countryCodes ?? []) {
      if (!/^[A-Z]{2}$/.test(countryCode)) errors.push(`Invalid ISO country code: ${countryCode}`);
      if (countryCodes.has(countryCode)) errors.push(`Country appears in multiple market groups: ${countryCode}`);
      countryCodes.add(countryCode);
    }
  }

  for (const owner of requiredOwners) {
    if (!catalog.approvalOwners?.includes(owner)) errors.push(`Missing commercial approval owner: ${owner}`);
  }
  if (!catalog.merchantOfRecord?.candidates?.includes(catalog.merchantOfRecord?.recommendedCandidate))
    errors.push('The recommended Merchant of Record must be included in the candidate list.');
  if (catalog.launchRecommendation?.automaticTrialConversion)
    errors.push('Automatic Trial conversion must remain disabled until a separate Consumer checkout decision is approved.');
  if (catalog.operationMetric?.failedPlatformOperationsBillable)
    errors.push('Failed platform operations must not be billable.');
  if (!catalog.operationMetric?.retryIdempotencyRequired)
    errors.push('Retry idempotency must remain required for billable usage.');

  if (catalog.productionApproved) {
    if (catalog.status !== 'approved') errors.push('Production approval requires approved catalog status.');
    if (catalog.merchantOfRecord?.status !== 'approved') errors.push('Production approval requires an approved billing owner.');
    if (catalog.launchRecommendation?.status !== 'approved') errors.push('Production approval requires an approved launch recommendation.');
    if (catalog.restrictionPolicy?.status !== 'approved') errors.push('Production approval requires an approved restriction policy.');
    if (catalog.priceBooks?.some(book => book.status !== 'approved')) errors.push('Production approval requires approved price books.');
    if (catalog.marketGroups?.some(group => group.selfServiceStatus !== 'approved')) errors.push('Production approval requires approved self-service markets.');
  } else if (catalog.publicPricingEnabled || catalog.consumerCheckoutEnabled) {
    errors.push('Unapproved commerce catalog cannot enable public pricing or Consumer checkout.');
  }

  return { catalog, errors, countries: countryCodes.size, currencies: currencies.size };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = validateGlobalCommerceCatalog();
  if (result.errors.length) {
    console.error(result.errors.map(error => `- ${error}`).join('\n'));
    process.exitCode = 1;
  } else {
    console.log(`Validated ${result.catalog.plans.length} proposed plans, ${result.currencies} currencies, and ${result.countries} country candidates; Production commerce remains disabled.`);
  }
}
