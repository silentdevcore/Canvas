import React, { useEffect, useRef } from 'react';
import { FiBell, FiCheck, FiExternalLink, FiFlag, FiX } from 'react-icons/fi';
import { useTranslation } from 'react-i18next';
import {
  designerBuildTime,
  designerCommit,
  designerDocumentationUrl,
  designerVersion,
} from './productMetadata';
import FeatureBadge from './FeatureBadge';
import { useProductExperience, type ProductPanel } from './ProductExperienceProvider';

interface ProductExperiencePanelProps {
  panel: ProductPanel;
  selectedVersion?: string;
  onSelectVersion: (version: string) => void;
}

const changeLabels = {
  added: 'Added',
  improved: 'Improved',
  fixed: 'Fixed',
  security: 'Security',
  deprecated: 'Deprecated',
  breaking: 'Breaking',
} as const;

const ProductExperiencePanel: React.FC<ProductExperiencePanelProps> = ({
  panel,
  selectedVersion,
  onSelectVersion,
}) => {
  const { t } = useTranslation('common');
  const {
    closePanel,
    dismissNotification,
    features,
    markAllRead,
    markNotificationRead,
    markReleaseRead,
    notifications,
    openPanel,
    readVersions,
    releases,
    setFeaturePreference,
  } = useProductExperience();
  const panelRef = useRef<HTMLElement>(null);
  const release = releases.find(item => item.version === selectedVersion) ?? releases[0];

  useEffect(() => {
    panelRef.current?.focus();
    const handleKeyboard = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closePanel();
      if (event.key !== 'Tab' || !panelRef.current) return;
      const focusable = [...panelRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )];
      if (focusable.length === 0) {
        event.preventDefault();
        panelRef.current.focus();
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', handleKeyboard);
    return () => document.removeEventListener('keydown', handleKeyboard);
  }, [closePanel]);

  useEffect(() => {
    if (panel === 'releases' && release)
      void markReleaseRead(release.version);
  }, [markReleaseRead, panel, release]);

  const title = panel === 'releases'
    ? t('productExperience.whatsNew')
    : panel === 'features'
      ? t('productExperience.experimentalFeatures')
      : t('productExperience.notifications');

  return (
    <div className="pxa-product-overlay" role="presentation" onMouseDown={event => {
      if (event.target === event.currentTarget) closePanel();
    }}>
      <aside
        className="pxa-product-panel"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        ref={panelRef}
      >
        <header>
          <span className="pxa-product-panel-icon" aria-hidden="true">
            {panel === 'releases' ? <FiFlag /> : <FiBell />}
          </span>
          <div><small>PXA {designerVersion}</small><h2>{title}</h2></div>
          <button type="button" onClick={closePanel} aria-label={t('productExperience.close')}>
            <FiX />
          </button>
        </header>

        {panel === 'releases' && release && (
          <div className="pxa-product-panel-body">
            <div className="pxa-release-selector" role="tablist" aria-label="Designer releases">
              {releases.map(item => (
                <button
                  type="button"
                  role="tab"
                  aria-selected={item.version === release.version}
                  className={item.version === release.version ? 'is-active' : ''}
                  key={item.version}
                  onClick={() => onSelectVersion(item.version)}
                >
                  <span>v{item.version}</span>
                  {!readVersions.has(item.version) && <i aria-label="Unread release" />}
                </button>
              ))}
            </div>
            <article className="pxa-release-notes">
              <div className="pxa-release-heading">
                <span className={`pxa-release-channel is-${release.channel}`}>{release.channel}</span>
                <time dateTime={release.publishedAt}>{release.publishedAt}</time>
              </div>
              <h3>{release.title}</h3>
              <p>{release.summary}</p>
              {Object.entries(release.changes).map(([category, entries]) =>
                entries.length > 0 && (
                  <section key={category}>
                    <h4>{changeLabels[category as keyof typeof changeLabels]}</h4>
                    <ul>{entries.map(entry => <li key={entry}>{entry}</li>)}</ul>
                  </section>
                ))}
              <a
                href={`${designerDocumentationUrl}${release.documentationPath}`}
                target="_blank"
                rel="noreferrer"
              >
                {t('productExperience.openDocumentation')} <FiExternalLink />
              </a>
              <footer>
                Build {designerCommit} · {new Date(designerBuildTime).toLocaleString()}
              </footer>
            </article>
          </div>
        )}

        {panel === 'features' && (
          <div className="pxa-product-panel-list">
            <p className="pxa-panel-intro">{t('productExperience.featuresIntro')}</p>
            {features.map(feature => (
              <article key={feature.id} className="pxa-feature-setting">
                <div>
                  <span className="pxa-feature-title">
                    <strong>{feature.fallbackTitle}</strong>
                    <FeatureBadge feature={feature} />
                  </span>
                  <p>{feature.fallbackDescription}</p>
                  {!feature.enabled && <small>{feature.decisionReason}</small>}
                </div>
                {feature.maturity === 'alpha' && (
                  <label className="pxa-feature-toggle">
                    <input
                      type="checkbox"
                      checked={feature.enabled}
                      disabled={feature.decisionCode === 'PXA_DESIGNER_ALPHA_NOT_ALLOWED'}
                      onChange={event => void setFeaturePreference(feature.id, event.target.checked)}
                    />
                    <span>{feature.enabled ? 'On' : 'Off'}</span>
                  </label>
                )}
              </article>
            ))}
          </div>
        )}

        {panel === 'notifications' && (
          <div className="pxa-product-panel-list">
            <div className="pxa-notification-actions">
              <button type="button" onClick={() => void markAllRead()}>
                <FiCheck /> {t('productExperience.markAllRead')}
              </button>
            </div>
            {releases.filter(item => !readVersions.has(item.version)).map(item => (
              <article className="pxa-notification-item is-unread" key={`release-${item.version}`}>
                <span className="is-release"><FiFlag /></span>
                <div>
                  <small>Release</small>
                  <button type="button" onClick={() => {
                    openPanel('releases', item.version);
                  }}>{item.title}</button>
                  <p>{item.summary}</p>
                </div>
              </article>
            ))}
            {notifications.map(item => (
              <article className={`pxa-notification-item${item.read ? '' : ' is-unread'}`} key={item.id}>
                <span className={`is-${item.severity.toLowerCase()}`}><FiBell /></span>
                <div>
                  <small>{item.category.replace(/([a-z])([A-Z])/g, '$1 $2')}</small>
                  <button type="button" onClick={() => void markNotificationRead(item.id)}>
                    {item.title}
                  </button>
                  <p>{item.message}</p>
                  <div className="pxa-notification-item-actions">
                    {item.actionUrl && item.actionLabel && (
                      <a href={item.actionUrl}>{item.actionLabel}</a>
                    )}
                    {!item.read && (
                      <button type="button" onClick={() => void markNotificationRead(item.id)}>
                        {t('productExperience.markRead')}
                      </button>
                    )}
                    {item.dismissible && (
                      <button type="button" onClick={() => void dismissNotification(item.id)}>
                        {t('productExperience.dismiss')}
                      </button>
                    )}
                  </div>
                </div>
              </article>
            ))}
            {notifications.length === 0 && releases.every(item => readVersions.has(item.version)) && (
              <div className="pxa-notification-empty">
                <FiCheck />
                <h3>{t('productExperience.allCaughtUp')}</h3>
              </div>
            )}
          </div>
        )}
      </aside>
    </div>
  );
};

export default ProductExperiencePanel;
