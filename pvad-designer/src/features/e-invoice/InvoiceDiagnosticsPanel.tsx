/**
 * Findings, each linked to the field it is about.
 *
 * Two things are deliberate. The diagnostic code stays visible even though it means nothing to
 * most users: it is what makes a support conversation possible, and it is the same identifier the
 * backend reports. And every local finding carries a focusable target, so "line 3 needs a
 * description" is one click away from the input that needs it rather than a hunt.
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { FiAlertCircle, FiAlertTriangle } from 'react-icons/fi';
import type { EInvoiceValidationReport } from './eInvoiceDesignerApi';
import type { InvoiceFinding } from './invoiceValidation';

export interface InvoiceDiagnosticsPanelProps {
  findings: readonly InvoiceFinding[];
  report: EInvoiceValidationReport | null;
  /** Focuses the input a finding points at. */
  onFocusField: (field: string) => void;
}

/** Maps an invoice path onto the input id the editor renders for it. */
export function fieldInputId(field: string): string {
  const line = /^lines\[(\d+)\]\.(.+)$/.exec(field);
  if (line) return `einv-line-${line[1]}-${line[2] === 'taxCategoryCode' ? 'taxCategory' : line[2]}`;
  return `einv-${field.replace(/\./g, '-')}`;
}

const InvoiceDiagnosticsPanel: React.FC<InvoiceDiagnosticsPanelProps> = ({
  findings,
  report,
  onFocusField,
}) => {
  const { t } = useTranslation('eInvoice');
  const backendDiagnostics = (report?.diagnostics ?? []).filter(
    diagnostic => diagnostic.severity === 'error' || diagnostic.severity === 'warning',
  );

  if (findings.length === 0 && backendDiagnostics.length === 0) {
    return <p className="einv-hint" data-testid="einvoice-diagnostics-empty">{t('diagnostics.empty')}</p>;
  }

  return (
    <div className="einv-diagnostics">
      {findings.length > 0 && (
        <ul className="einv-diagnostic-list" data-testid="einvoice-diagnostics">
          {findings.map((finding, index) => (
            <li
              key={`${finding.code}-${finding.field}-${index}`}
              className={`einv-diagnostic is-${finding.severity}`}
              data-code={finding.code}
              data-field={finding.field}
            >
              <span className="einv-diagnostic-icon" aria-hidden="true">
                {finding.severity === 'error' ? <FiAlertCircle /> : <FiAlertTriangle />}
              </span>
              <span className="einv-diagnostic-body">
                <span className="einv-diagnostic-message">
                  {t(finding.messageKey, finding.params)}
                </span>
                <span className="einv-diagnostic-meta">
                  <span className="einv-visually-hidden">
                    {finding.severity === 'error'
                      ? t('diagnostics.severityError')
                      : t('diagnostics.severityWarning')}
                  </span>
                  <code className="einv-diagnostic-code" title={t('diagnostics.code')}>
                    {finding.code}
                  </code>
                  <button
                    type="button"
                    className="einv-link-btn"
                    onClick={() => onFocusField(finding.field)}
                  >
                    {t('diagnostics.goToField')}
                  </button>
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}

      {backendDiagnostics.length > 0 && (
        <>
          <h4 className="einv-subheading">{t('diagnostics.backendTitle')}</h4>
          <ul className="einv-diagnostic-list" data-testid="einvoice-backend-diagnostics">
            {backendDiagnostics.map((diagnostic, index) => (
              <li
                key={`${diagnostic.code}-${index}`}
                className={`einv-diagnostic is-${diagnostic.severity}`}
                data-code={diagnostic.code}
              >
                <span className="einv-diagnostic-body">
                  <span className="einv-diagnostic-message">{diagnostic.message}</span>
                  <span className="einv-diagnostic-meta">
                    <code className="einv-diagnostic-code" title={diagnostic.code}>{diagnostic.canonicalCode ?? diagnostic.code}</code>
                  </span>
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
};

export default InvoiceDiagnosticsPanel;
