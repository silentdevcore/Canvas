/**
 * The three validation states, shown as three separate things.
 *
 * Structural recognition, PVADE business validation and official conformance never share a badge
 * and never share a word. A clean structural result says "recognised"; only a pinned official
 * artifact set can produce "validated", and the artifact-set identity is shown with it so the
 * claim is checkable.
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { FiAlertTriangle, FiCheck, FiHelpCircle, FiX } from 'react-icons/fi';
import type { EInvoiceValidationReport } from './eInvoiceDesignerApi';
import type { DesignerConformanceState, InvoiceFinding } from './invoiceValidation';

type Tone = 'neutral' | 'ok' | 'warn' | 'error';

const CONFORMANCE_TONE: Record<DesignerConformanceState, Tone> = {
  draft: 'neutral',
  structurallyValid: 'neutral',
  notEvaluated: 'neutral',
  unavailable: 'warn',
  validated: 'ok',
  invalid: 'error',
};

function ToneIcon({ tone }: { tone: Tone }): React.ReactElement {
  if (tone === 'ok') return <FiCheck aria-hidden="true" />;
  if (tone === 'error') return <FiX aria-hidden="true" />;
  if (tone === 'warn') return <FiAlertTriangle aria-hidden="true" />;
  return <FiHelpCircle aria-hidden="true" />;
}

interface StateRowProps {
  label: string;
  value: string;
  tone: Tone;
  detail?: string;
  testId: string;
}

function StateRow({ label, value, tone, detail, testId }: StateRowProps): React.ReactElement {
  return (
    <div className="einv-state" data-testid={testId} data-tone={tone}>
      <span className="einv-state-label">{label}</span>
      <span className={`einv-state-value is-${tone}`}>
        <ToneIcon tone={tone} />
        <span>{value}</span>
      </span>
      {detail && <span className="einv-state-detail">{detail}</span>}
    </div>
  );
}

export interface InvoiceValidationStatesProps {
  findings: readonly InvoiceFinding[];
  conformance: DesignerConformanceState;
  report: EInvoiceValidationReport | null;
}

const InvoiceValidationStates: React.FC<InvoiceValidationStatesProps> = ({
  findings,
  conformance,
  report,
}) => {
  const { t } = useTranslation('eInvoice');
  const errors = findings.filter(finding => finding.severity === 'error').length;
  const warnings = findings.filter(finding => finding.severity === 'warning').length;

  const structuralValue = !report
    ? t('structural.pending')
    : report.isStructurallyValid
      ? t('structural.valid', { syntax: report.syntax ?? '-' })
      : t('structural.invalid');
  const structuralTone: Tone = !report ? 'neutral' : report.isStructurallyValid ? 'ok' : 'error';

  const businessValue = errors > 0
    ? t('business.errors', { count: errors })
    : warnings > 0
      ? t('business.warnings', { count: warnings })
      : t('business.ok');
  const businessTone: Tone = errors > 0 ? 'error' : warnings > 0 ? 'warn' : 'ok';

  return (
    <div className="einv-states">
      <StateRow
        testId="einvoice-state-structural"
        label={t('structural.label')}
        value={structuralValue}
        tone={structuralTone}
      />
      <StateRow
        testId="einvoice-state-business"
        label={t('business.label')}
        value={businessValue}
        tone={businessTone}
      />
      <StateRow
        testId="einvoice-state-conformance"
        label={t('conformance.label')}
        value={t(`conformance.${conformance}`)}
        tone={CONFORMANCE_TONE[conformance]}
        detail={report?.validatorArtifactSetId
          ? `${t('conformance.artifactSet')}: ${report.validatorArtifactSetId}`
          : undefined}
      />
      <p className="einv-states-note">{t('conformance.explanation')}</p>
    </div>
  );
};

export default InvoiceValidationStates;
