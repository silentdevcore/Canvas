import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [subscriptionSource, usageSource] = await Promise.all([
  readFile(new URL('../src/pages/subscription.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/pages/usage.ts', import.meta.url), 'utf8'),
]);

test('subscription presents lifecycle dates and a truthful commercial path', () => {
  assert.match(subscriptionSource, /cancellationEffectiveAt/);
  assert.match(subscriptionSource, /gracePeriodEndsAt/);
  assert.match(subscriptionSource, /currentPeriodEndsAt/);
  assert.match(subscriptionSource, /companyPage\('pricing'\)/);
  assert.match(subscriptionSource, /companyPage\('contact'\)/);
  assert.match(subscriptionSource, /Online checkout is not enabled yet/);
  assert.doesNotMatch(subscriptionSource, /createCheckout|paymentMethod|cardNumber|credit-card/i);
});

test('usage combines metering with effective subscription limits', () => {
  assert.match(usageSource, /getAccountSubscriptionUsage/);
  assert.match(usageSource, /getAccountSubscription/);
  assert.match(usageSource, /usageByCapability/);
  assert.match(usageSource, /<progress/);
  assert.match(usageSource, /remaining/);
  assert.match(usageSource, /Unlimited for this subscription period/);
});
