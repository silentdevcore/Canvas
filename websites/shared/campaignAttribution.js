// Single shared allowlist so PXA.Company (which appends these params to its
// outgoing Account links) and PXA.Account (which reads them back at
// registration) can never drift out of sync with each other.
export const CAMPAIGN_PARAMS = ['utm_source', 'utm_medium', 'utm_campaign'];

/**
 * Appends any allowlisted campaign params found in `search` (defaults to
 * the current page's query string) onto `url`. Used by PXA.Company when
 * building outgoing links to PXA.Account, so a visitor who arrived via a
 * campaign link keeps that attribution through to registration.
 */
export function appendCampaignParams(url, search = window.location.search) {
  const incoming = new URLSearchParams(search);
  const params = new URLSearchParams();
  for (const key of CAMPAIGN_PARAMS) {
    const value = incoming.get(key);
    if (value) params.set(key, value);
  }
  const query = params.toString();
  if (!query) return url;
  return `${url}${url.includes('?') ? '&' : '?'}${query}`;
}

/**
 * Extracts the allowlisted campaign params from `search` (defaults to the
 * current page's query string) as a plain object, or null if none are
 * present. Used by PXA.Account's registration form to pass the same
 * privacy-safe context through to the backend.
 */
export function extractCampaignContext(search = window.location.search) {
  const incoming = new URLSearchParams(search);
  const context = {};
  for (const key of CAMPAIGN_PARAMS) {
    const value = incoming.get(key);
    if (value) context[key] = value;
  }
  return Object.keys(context).length > 0 ? context : null;
}
