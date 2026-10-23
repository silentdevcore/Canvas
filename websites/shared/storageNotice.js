import { companyPage } from './siteLinks.js';

const NOTICE_VERSION = '2026-08';
const COOKIE_NAME = 'PVAD.StorageNotice';
const LEGACY_COOKIE_NAME = 'PXA.StorageNotice';

function readCookie(name) {
  return document.cookie.split(';').map((value) => value.trim())
    .find((value) => value.startsWith(`${name}=`))?.slice(name.length + 1);
}
function cookieAttributes(maxAge) {
  const production = location.protocol === 'https:';
  const sharedDomain = production && (
    location.hostname === 'powerdoxautomation.com' ||
    location.hostname.endsWith('.powerdoxautomation.com')
  );
  return [
    'Path=/',
    `Max-Age=${maxAge}`,
    'SameSite=Lax',
    production ? 'Secure' : '',
    sharedDomain ? 'Domain=.powerdoxautomation.com' : '',
  ].filter(Boolean).join('; ');
}

function acknowledge() {
  document.cookie = `${COOKIE_NAME}=${NOTICE_VERSION}; ${cookieAttributes(60 * 60 * 24 * 180)}`;
  document.querySelector('[data-pvad-storage-notice]')?.remove();
}

function showNotice() {
  document.querySelector('[data-pvad-storage-notice]')?.remove();
  const notice = document.createElement('aside');
  notice.className = 'pvad-storage-notice';
  notice.dataset.pvadStorageNotice = '';
  notice.setAttribute('aria-labelledby', 'pvad-storage-notice-title');
  notice.innerHTML = `
    <div>
      <strong id="pvad-storage-notice-title">Necessary browser storage</strong>
      <p>
        PVADE uses only storage required for security, sessions, language, and requested
        application preferences. Optional analytics and marketing storage are not used.
      </p>
    </div>
    <div class="pvad-storage-notice__actions">
      <a href="${companyPage('cookie-storage')}">Learn more</a>
      <button type="button" class="pvad-button pvad-button--primary" data-pvad-storage-understood>
        Understood
      </button>
    </div>
  `;
  document.body.append(notice);
  notice.querySelector('[data-pvad-storage-understood]')?.addEventListener('click', acknowledge);
}

export function initializeStorageNotice() {
  const current = readCookie(COOKIE_NAME);
  const legacyAcknowledged = current === undefined && readCookie(LEGACY_COOKIE_NAME) === NOTICE_VERSION;
  if (legacyAcknowledged) acknowledge();
  if (current !== NOTICE_VERSION && !legacyAcknowledged)
    showNotice();
  document.addEventListener('click', (event) => {
    if (!event.target.closest('[data-pvad-storage-settings]')) return;
    event.preventDefault();
    showNotice();
  });
}
