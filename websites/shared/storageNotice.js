import { companyPage } from './siteLinks.js';

const NOTICE_VERSION = '2026-07';
const COOKIE_NAME = 'PXA.StorageNotice';

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
  document.querySelector('[data-pxa-storage-notice]')?.remove();
}

function showNotice() {
  document.querySelector('[data-pxa-storage-notice]')?.remove();
  const notice = document.createElement('aside');
  notice.className = 'pxa-storage-notice';
  notice.dataset.pxaStorageNotice = '';
  notice.setAttribute('aria-labelledby', 'pxa-storage-notice-title');
  notice.innerHTML = `
    <div>
      <strong id="pxa-storage-notice-title">Necessary browser storage</strong>
      <p>
        PXA uses only storage required for security, sessions, language, and requested
        application preferences. Optional analytics and marketing storage are not used.
      </p>
    </div>
    <div class="pxa-storage-notice__actions">
      <a href="${companyPage('cookie-storage')}">Learn more</a>
      <button type="button" class="pxa-button pxa-button--primary" data-pxa-storage-understood>
        Understood
      </button>
    </div>
  `;
  document.body.append(notice);
  notice.querySelector('[data-pxa-storage-understood]')?.addEventListener('click', acknowledge);
}

export function initializeStorageNotice() {
  if (readCookie(COOKIE_NAME) !== NOTICE_VERSION)
    showNotice();
  document.addEventListener('click', (event) => {
    if (!event.target.closest('[data-pxa-storage-settings]')) return;
    event.preventDefault();
    showNotice();
  });
}
