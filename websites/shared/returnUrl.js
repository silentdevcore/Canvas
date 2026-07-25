import { siteLinks } from './siteLinks.js';

// Only these product surfaces are valid post-login return destinations.
// PXA Admin has no entry in siteLinks.js at all, so it can never appear here.
const ALLOWED_KEYS = ['designer', 'demo', 'documentation', 'account', 'company'];

function allowedOrigins(links) {
  const origins = new Set();
  for (const key of ALLOWED_KEYS) {
    const url = links[key];
    if (url) origins.add(new URL(url).origin);
  }
  return origins;
}

/**
 * Returns the given value if it is a safe, absolute http(s) URL pointing at
 * an allowlisted PXA product origin (Designer, Demo, Documentation, Company,
 * or Account itself) in the active runtime environment.
 * Rejects everything else — protocol-relative ("//host/..."), external
 * hosts, non-http(s) schemes, and relative paths — and returns null so
 * callers can fall back to a default destination without ever reflecting
 * the rejected value back to the user.
 */
export function sanitizeReturnUrl(rawValue, links = siteLinks) {
  if (typeof rawValue !== 'string') return null;
  const trimmed = rawValue.trim();
  if (!trimmed || !/^https?:\/\//i.test(trimmed)) return null;

  let parsed;
  try {
    parsed = new URL(trimmed);
  } catch {
    return null;
  }
  if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return null;
  if (!allowedOrigins(links).has(parsed.origin)) return null;

  return parsed.toString();
}
