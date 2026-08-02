const SIGNAL_PARAM = 'pxa_signed_in';

/**
 * Appends a one-time, purely cosmetic "signed in" signal to a return-URL
 * target, but only when that target's origin matches the given companyOrigin.
 * PXA.Company has no session of its own and never will (see returnUrl.js) -
 * this lets Company's header show "My account" instead of "Sign in" right
 * after a return-to-Company login, without loosening the session cookie's
 * SameSite policy or adding cross-origin credentialed requests. It is not a
 * security boundary; Company must never rely on it for anything beyond
 * deciding which header links to render.
 */
export function appendSignedInSignal(url, companyOrigin) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return url;
  }
  if (parsed.origin !== companyOrigin) return url;
  parsed.searchParams.set(SIGNAL_PARAM, '1');
  return parsed.toString();
}

/**
 * Pure counterpart consumed by PXA.Company: given a location.search string,
 * returns null when no signal is present, or `{ signedIn: true, cleanedSearch }`
 * with the signal parameter stripped (and every other query parameter
 * preserved) so the caller can apply in-memory presentation and history side effects.
 */
export function consumeSignedInSignal(search) {
  const params = new URLSearchParams(search);
  if (params.get(SIGNAL_PARAM) !== '1') return null;
  params.delete(SIGNAL_PARAM);
  return { signedIn: true, cleanedSearch: params.toString() };
}
