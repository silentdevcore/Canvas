/**
 * Tax groups and totals, as read-only output.
 *
 * Every figure comes from `calculateInvoice`, the exact-decimal arithmetic pinned against the
 * generator by `test-assets/e-invoicing/totals-parity.json`. None of it is an input, because a
 * total the user could overtype would be a second source of truth.
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { toFixed, toPlainString } from './exactDecimal';
import type { CalculatedInvoice } from './invoiceTotals';

export interface InvoiceTotalsPanelProps {
  totals: CalculatedInvoice;
  currency: string;
}

const InvoiceTotalsPanel: React.FC<InvoiceTotalsPanelProps> = ({ totals, currency }) => {
  const { t } = useTranslation('eInvoice');
  const places = totals.decimalPlaces;
  const code = currency.trim().toUpperCase();

  return (
    <div className="einv-totals">
      <dl className="einv-totals-list">
        <div className="einv-totals-row">
          <dt>{t('totals.netTotal')}</dt>
          <dd>
            <output data-testid="einvoice-total-net">
              {`${toFixed(totals.lineNetTotal, places)} ${code}`}
            </output>
          </dd>
        </div>

        {totals.taxes.map(tax => (
          <div className="einv-totals-row" key={`${tax.categoryCode}-${toPlainString(tax.rate)}`}>
            <dt>
              {t('totals.taxGroup', { rate: toPlainString(tax.rate) })}
              <span className="einv-totals-category">{tax.categoryCode}</span>
            </dt>
            <dd>
              <output
                data-testid={`einvoice-tax-${tax.categoryCode}-${toPlainString(tax.rate)}`}
                title={`${t('totals.taxBasis')}: ${toFixed(tax.basis, places)} ${code}`}
              >
                {`${toFixed(tax.amount, places)} ${code}`}
              </output>
            </dd>
          </div>
        ))}

        <div className="einv-totals-row">
          <dt>{t('totals.taxTotal')}</dt>
          <dd>
            <output data-testid="einvoice-total-tax">
              {`${toFixed(totals.taxTotal, places)} ${code}`}
            </output>
          </dd>
        </div>

        <div className="einv-totals-row is-grand">
          <dt>{t('totals.grandTotal')}</dt>
          <dd>
            <output data-testid="einvoice-total-grand">
              {`${toFixed(totals.grandTotal, places)} ${code}`}
            </output>
          </dd>
        </div>
      </dl>
      <p className="einv-hint">{t('totals.calculated')}</p>
    </div>
  );
};

export default InvoiceTotalsPanel;
