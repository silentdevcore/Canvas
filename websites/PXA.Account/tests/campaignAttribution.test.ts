import assert from 'node:assert/strict';
import test from 'node:test';
import { appendCampaignParams, extractCampaignContext } from '../../shared/campaignAttribution.js';

test('appendCampaignParams appends only allowlisted params from the given search string', () => {
  const url = appendCampaignParams(
    'http://localhost:5178/register',
    '?utm_source=newsletter&utm_campaign=spring&password=smuggled',
  );
  const parsed = new URL(url);
  assert.equal(parsed.searchParams.get('utm_source'), 'newsletter');
  assert.equal(parsed.searchParams.get('utm_campaign'), 'spring');
  assert.equal(parsed.searchParams.has('password'), false);
});

test('appendCampaignParams returns the original url unchanged when no campaign params are present', () => {
  const url = appendCampaignParams('http://localhost:5178/register', '');
  assert.equal(url, 'http://localhost:5178/register');
});

test('appendCampaignParams appends with & when the url already has a query string', () => {
  const url = appendCampaignParams('http://localhost:5178/register?foo=bar', '?utm_source=ads');
  assert.equal(url, 'http://localhost:5178/register?foo=bar&utm_source=ads');
});

test('extractCampaignContext returns only allowlisted keys as a plain object', () => {
  const context = extractCampaignContext('?utm_source=newsletter&utm_medium=email&other=1');
  assert.deepEqual(context, { utm_source: 'newsletter', utm_medium: 'email' });
});

test('extractCampaignContext returns null when no campaign params are present', () => {
  assert.equal(extractCampaignContext('?foo=bar'), null);
  assert.equal(extractCampaignContext(''), null);
});
