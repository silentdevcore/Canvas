import React, { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getStatusDefinition,
  recordStatusPageShown,
  recordStatusRecoverySelected,
  type PvadStatusKey,
} from '../../../websites/shared/statusPages';
import { accountPageUrl } from '@/auth/designerAuth';

interface StatusPageProps {
  statusKey: PvadStatusKey;
  title?: string;
  message?: string;
  diagnosticCode?: string;
  correlationId?: string;
  primaryAction?: 'back' | 'home' | 'reload' | 'retry' | 'signIn' | 'subscription' | 'support' | 'account';
}

const actionLabels: Record<string, string> = {
  back: 'Go back',
  home: 'Return to Designer',
  reload: 'Reload',
  retry: 'Try again',
  signIn: 'Sign in',
  subscription: 'View subscription',
  support: 'Contact support',
  account: 'Open PVADE Account',
};

const StatusPage: React.FC<StatusPageProps> = ({
  statusKey,
  title,
  message,
  diagnosticCode,
  correlationId,
  primaryAction,
}) => {
  const { t, i18n } = useTranslation('status');
  const headingRef = useRef<HTMLHeadingElement>(null);
  const definition = getStatusDefinition(statusKey);
  const diagnostic = [diagnosticCode, correlationId].filter(Boolean).join(' / ');

  useEffect(() => {
    headingRef.current?.focus({ preventScroll: true });
    recordStatusPageShown(statusKey, correlationId);
  }, [correlationId, statusKey]);

  const action = primaryAction ?? definition.primaryAction;
  const isEnglish = i18n.resolvedLanguage?.startsWith('en') ?? true;
  const renderedTitle = isEnglish && title
    ? title
    : t(`states.${statusKey}.title`, { defaultValue: definition.title });
  const renderedMessage = isEnglish && message
    ? message
    : t(`states.${statusKey}.message`, { defaultValue: definition.message });
  const performPrimaryAction = () => {
    recordStatusRecoverySelected(action, correlationId);
    switch (action) {
      case 'back':
        history.back();
        break;
      case 'reload':
      case 'retry':
        location.reload();
        break;
      case 'subscription':
        location.assign(accountPageUrl('/subscription'));
        break;
      case 'support':
        location.assign(accountPageUrl('/support'));
        break;
      case 'signIn':
        location.assign(accountPageUrl('/login'));
        break;
      case 'account':
        location.assign(accountPageUrl('/dashboard'));
        break;
      default:
        location.assign('/');
    }
  };

  return (
    <main className="pvad-status-page designer-status-page" data-status-key={statusKey}>
      <div className="pvad-status-page__brand" aria-label="PVADE Designer">
        <span className="pvad-status-page__mark" aria-hidden="true">PVADE</span>
        <span>PVADE <small>{t('product')}</small></span>
      </div>
      <section
        className="pvad-status-page__content"
        aria-labelledby="pvad-status-title"
        aria-live="polite"
        aria-atomic="true"
      >
        <p className="pvad-status-page__code">{definition.httpStatus ?? 'PVADE'}</p>
        <h1 id="pvad-status-title" ref={headingRef} tabIndex={-1}>{renderedTitle}</h1>
        <p className="pvad-status-page__message">{renderedMessage}</p>
        <div className="pvad-status-page__actions">
          <button className="pvad-status-primary" type="button" onClick={performPrimaryAction}>
            {t(`actions.${action}`, { defaultValue: actionLabels[action] })}
          </button>
          {statusKey === 'notFound' && <a className="pvad-status-secondary" href="/docs">{t('openHelp')}</a>}
        </div>
        {diagnostic && (
          <div className="pvad-status-page__diagnostic">
            <span>{t('diagnosticDetails')}</span>
            <code>{diagnostic}</code>
            <button type="button" onClick={() => void navigator.clipboard.writeText(diagnostic)}>{t('copy')}</button>
          </div>
        )}
      </section>
      <footer className="pvad-status-page__footer">{t('savedWork')}</footer>
    </main>
  );
};

export default StatusPage;
