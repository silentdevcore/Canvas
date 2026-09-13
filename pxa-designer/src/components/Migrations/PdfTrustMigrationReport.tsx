import React from 'react';
import { useTranslation } from 'react-i18next';
import { FiAlertTriangle, FiExternalLink, FiShield } from 'react-icons/fi';

export interface PdfTrustManualAction {
  actionId: string;
  capability: string;
  disposition: 'ReviewRequired' | 'Unsupported';
  sourceOperation: string;
  targetOperation?: string | null;
  documentationPath: string;
}

export interface PdfTrustMigrationResult {
  providerKey: string;
  migrationKind: string;
  overallDisposition: 'Automatic' | 'ReviewRequired' | 'Unsupported';
  manualActions: PdfTrustManualAction[];
}

export const resolveMigrationDocumentationHref = (baseUrl: string, path: string): string => {
  try {
    const url = new URL(path, baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`);
    return url.protocol === 'http:' || url.protocol === 'https:' ? url.toString() : baseUrl;
  } catch {
    return baseUrl;
  }
};

const capabilityTranslationKey = (capability: string): string =>
  `${capability.charAt(0).toLowerCase()}${capability.slice(1)}`;

interface Props {
  report: PdfTrustMigrationResult;
  documentationBaseUrl: string;
}

const PdfTrustMigrationReport: React.FC<Props> = ({ report, documentationBaseUrl }) => {
  const { t } = useTranslation('migrations');
  if (report.manualActions.length === 0) return null;

  return (
    <section className="mgr-trust-report" aria-labelledby="mgr-trust-report-title">
      <div className="mgr-trust-report-header">
        <FiShield aria-hidden="true" />
        <div>
          <h2 id="mgr-trust-report-title">{t('trust.title')}</h2>
          <p>{t('trust.description', { count: report.manualActions.length })}</p>
        </div>
      </div>
      <ul className="mgr-trust-actions">
        {report.manualActions.map(action => {
          const unsupported = action.disposition === 'Unsupported';
          return (
            <li className={`mgr-trust-action${unsupported ? ' is-unsupported' : ''}`} key={action.actionId}>
              <FiAlertTriangle aria-hidden="true" />
              <div className="mgr-trust-action-content">
                <div className="mgr-trust-action-heading">
                  <strong>{t(`trust.capabilities.${capabilityTranslationKey(action.capability)}`, action.capability)}</strong>
                  <span>{t(`trust.dispositions.${action.disposition}`)}</span>
                </div>
                <p>{t('trust.sourceOperation')}: <code>{action.sourceOperation}</code></p>
                {action.targetOperation && (
                  <p>{t('trust.targetOperation')}: <code>{action.targetOperation}</code></p>
                )}
                <a
                  href={resolveMigrationDocumentationHref(documentationBaseUrl, action.documentationPath)}
                  target="_blank"
                  rel="noreferrer"
                >
                  {t('trust.openWorkflow')} <FiExternalLink aria-hidden="true" />
                </a>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
};

export default PdfTrustMigrationReport;
