import { companyPage } from '../../../shared/siteLinks.js';
import type { AccountProfileResponse } from '../api';
import { escapeHtml } from '../shell';

const legalPublications = [
  { key: 'terms', label: 'Terms and Conditions', path: 'terms' },
  { key: 'privacy', label: 'Privacy Notice', path: 'privacy' },
  { key: 'cookie-storage', label: 'Cookie and Storage Policy', path: 'cookie-storage' },
  { key: 'imprint', label: 'Imprint', path: 'imprint' },
  { key: 'withdrawal', label: 'Consumer Withdrawal Information', path: 'withdrawal' },
  { key: 'dpa', label: 'Data Processing Agreement', path: 'dpa' },
  { key: 'license', label: 'License Agreement', path: 'license' },
] as const;

function versionUrl(kind: 'terms' | 'privacy', version: string): string {
  return `${companyPage(kind)}?version=${encodeURIComponent(version)}`;
}

function publication(
  kind: 'terms' | 'privacy',
  title: string,
  currentVersion: string,
  recordedVersion: string | null,
  previousVersion: string | null,
  changeSummary: string | null,
  actionLabel: string,
): string {
  const current = recordedVersion === currentVersion;
  return `
    <article class="account-legal-update">
      <header>
        <div>
          <p class="pxa-kicker">${escapeHtml(actionLabel)}</p>
          <h2>${escapeHtml(title)}</h2>
        </div>
        <span class="account-status">${current ? 'Current' : 'Action required'}</span>
      </header>
      <dl>
        <div><dt>Published version</dt><dd>${escapeHtml(currentVersion)}</dd></div>
        <div><dt>Your recorded version</dt><dd>${escapeHtml(recordedVersion || 'None')}</dd></div>
      </dl>
      <p>${escapeHtml(changeSummary || 'No public change summary was provided for this version.')}</p>
      <div class="account-actions">
        <a class="pxa-button pxa-button--secondary" href="${companyPage(kind)}" target="_blank" rel="noopener">Read current version</a>
        ${previousVersion ? `<a href="${versionUrl(kind, previousVersion)}" target="_blank" rel="noopener">Read previous version ${escapeHtml(previousVersion)}</a>` : '<span class="account-legal-first-version">First recorded publication</span>'}
      </div>
    </article>`;
}

export function legalUpdatesPage(profile: AccountProfileResponse): string {
  const selectedDocument = new URLSearchParams(window.location.search).get('document');
  const publications = legalPublications.map(document => {
    const selected = document.key === selectedDocument;
    return `
      <li${selected ? ' class="is-selected"' : ''}>
        <a href="${companyPage(document.path)}" target="_blank" rel="noopener"${selected ? ' aria-current="true"' : ''}>
          <span>${escapeHtml(document.label)}</span>
          <small>${selected ? 'Selected update - open publication and version history' : 'Open publication and version history'}</small>
        </a>
      </li>`;
  }).join('');

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Legal</p>
        <h1>Legal updates</h1>
        <p>Review current publications, public change summaries, and your recorded legal status.</p>
      </div>
    </header>
    <section class="account-legal-explanation" aria-labelledby="legal-status-model">
      <h2 id="legal-status-model">Acceptance and acknowledgement are different</h2>
      <p>Updated Terms require acceptance only when the publication explicitly requests it. A Privacy Notice update is acknowledged as received and is never treated as marketing consent.</p>
    </section>
    <div class="account-legal-updates">
      ${publication(
        'terms',
        'Terms and Conditions',
        profile.currentTermsVersion,
        profile.termsAcceptedVersion,
        profile.previousTermsVersion,
        profile.currentTermsChangeSummary,
        'Acceptance',
      )}
      ${publication(
        'privacy',
        'Privacy Notice',
        profile.currentPrivacyVersion,
        profile.privacyAcknowledgedVersion,
        profile.previousPrivacyVersion,
        profile.currentPrivacyChangeSummary,
        'Acknowledgement',
      )}
    </div>
    <section class="account-legal-publications" aria-labelledby="all-legal-publications">
      <div>
        <p class="pxa-kicker">Publication library</p>
        <h2 id="all-legal-publications">All Legal publications</h2>
        <p>Each public document includes its current text and available version history.</p>
      </div>
      <ul>${publications}</ul>
    </section>`;
}
