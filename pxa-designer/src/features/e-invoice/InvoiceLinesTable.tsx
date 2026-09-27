/**
 * The invoice lines, as a real table.
 *
 * Proper table semantics matter here beyond tidiness: a screen reader announces the column when
 * the user lands in a cell, which is the only thing that makes a grid of unlabelled number inputs
 * usable. Every input therefore has its own label, and the net amount is output, not input - it is
 * computed by the same exact-decimal arithmetic the generator uses and cannot be typed over.
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { FiArrowDown, FiArrowUp, FiPlus, FiTrash2 } from 'react-icons/fi';
import { toFixed } from './exactDecimal';
import type { EInvoiceDocumentLine } from './invoiceModel';
import type { CalculatedInvoice } from './invoiceTotals';
import type { InvoiceFinding } from './invoiceValidation';

export interface InvoiceLinesTableProps {
  lines: readonly EInvoiceDocumentLine[];
  totals: CalculatedInvoice;
  findings: readonly InvoiceFinding[];
  onAdd: () => void;
  onRemove: (index: number) => void;
  onMove: (index: number, direction: -1 | 1) => void;
  onChange: (index: number, change: Partial<EInvoiceDocumentLine>) => void;
}

/** UNCL5305 categories PXA accepts. Choosing one is a billing decision, not a PXA rule. */
const TAX_CATEGORIES = ['S', 'Z', 'E', 'AE', 'K', 'G', 'O', 'L', 'M'] as const;

const InvoiceLinesTable: React.FC<InvoiceLinesTableProps> = ({
  lines,
  totals,
  findings,
  onAdd,
  onRemove,
  onMove,
  onChange,
}) => {
  const { t } = useTranslation('eInvoice');

  const fieldError = (index: number, field: string): InvoiceFinding | undefined =>
    findings.find(finding =>
      finding.severity === 'error' && finding.field === `lines[${index}].${field}`);

  return (
    <div className="einv-lines">
      <table className="einv-lines-table">
        <caption className="einv-visually-hidden">{t('lines.caption')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('fields.lineIdentifier')}</th>
            <th scope="col">{t('fields.lineDescription')}</th>
            <th scope="col" className="is-numeric">{t('fields.lineQuantity')}</th>
            <th scope="col">{t('fields.lineUnitCode')}</th>
            <th scope="col" className="is-numeric">{t('fields.lineUnitPrice')}</th>
            <th scope="col">{t('fields.lineTaxCategory')}</th>
            <th scope="col" className="is-numeric">{t('fields.lineTaxRate')}</th>
            <th scope="col" className="is-numeric">{t('fields.lineNetAmount')}</th>
            <th scope="col"><span className="einv-visually-hidden">{t('sections.lines')}</span></th>
          </tr>
        </thead>
        <tbody>
          {lines.length === 0 && (
            <tr>
              <td colSpan={9} className="einv-lines-empty">{t('lines.empty')}</td>
            </tr>
          )}
          {lines.map((line, index) => {
            const net = totals.lines[index]?.netAmount;
            const rowLabel = t('lines.position', { position: index + 1 });
            return (
              <tr key={`line-${index}`} data-testid={`einvoice-line-${index}`}>
                <th scope="row" className="einv-lines-rowhead">
                  <label
                    className="einv-visually-hidden"
                    htmlFor={`einv-line-${index}-identifier`}
                  >
                    {`${rowLabel} ${t('fields.lineIdentifier')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-identifier`}
                    className="einv-input is-narrow"
                    value={line.identifier}
                    aria-invalid={Boolean(fieldError(index, 'identifier'))}
                    onChange={event => onChange(index, { identifier: event.target.value })}
                  />
                </th>
                <td>
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-description`}>
                    {`${rowLabel} ${t('fields.lineDescription')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-description`}
                    className="einv-input"
                    value={line.description}
                    aria-invalid={Boolean(fieldError(index, 'description'))}
                    onChange={event => onChange(index, { description: event.target.value })}
                  />
                </td>
                <td className="is-numeric">
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-quantity`}>
                    {`${rowLabel} ${t('fields.lineQuantity')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-quantity`}
                    className="einv-input is-numeric is-narrow"
                    type="number"
                    step="any"
                    min="0"
                    value={line.quantity}
                    aria-invalid={Boolean(fieldError(index, 'quantity'))}
                    onChange={event => onChange(index, { quantity: Number(event.target.value) })}
                  />
                </td>
                <td>
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-unitCode`}>
                    {`${rowLabel} ${t('fields.lineUnitCode')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-unitCode`}
                    className="einv-input is-narrow"
                    value={line.unitCode}
                    aria-invalid={Boolean(fieldError(index, 'unitCode'))}
                    onChange={event => onChange(index, { unitCode: event.target.value })}
                  />
                </td>
                <td className="is-numeric">
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-unitPrice`}>
                    {`${rowLabel} ${t('fields.lineUnitPrice')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-unitPrice`}
                    className="einv-input is-numeric"
                    type="number"
                    step="any"
                    min="0"
                    value={line.unitPrice}
                    aria-invalid={Boolean(fieldError(index, 'unitPrice'))}
                    onChange={event => onChange(index, { unitPrice: Number(event.target.value) })}
                  />
                </td>
                <td>
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-taxCategory`}>
                    {`${rowLabel} ${t('fields.lineTaxCategory')}`}
                  </label>
                  <select
                    id={`einv-line-${index}-taxCategory`}
                    className="einv-select is-narrow"
                    value={line.taxCategoryCode}
                    onChange={event => onChange(index, { taxCategoryCode: event.target.value })}
                  >
                    {TAX_CATEGORIES.map(category => (
                      <option key={category} value={category}>{category}</option>
                    ))}
                  </select>
                </td>
                <td className="is-numeric">
                  <label className="einv-visually-hidden" htmlFor={`einv-line-${index}-taxRate`}>
                    {`${rowLabel} ${t('fields.lineTaxRate')}`}
                  </label>
                  <input
                    id={`einv-line-${index}-taxRate`}
                    className="einv-input is-numeric is-narrow"
                    type="number"
                    step="any"
                    min="0"
                    max="100"
                    value={line.taxRate}
                    aria-invalid={Boolean(fieldError(index, 'taxRate'))}
                    onChange={event => onChange(index, { taxRate: Number(event.target.value) })}
                  />
                </td>
                <td className="is-numeric">
                  {/* Output, not input: the net amount is derived and must not be editable. */}
                  <output
                    className="einv-derived"
                    data-testid={`einvoice-line-${index}-net`}
                    title={t('lines.netCalculated')}
                  >
                    {net ? toFixed(net, totals.decimalPlaces) : ''}
                  </output>
                </td>
                <td className="einv-lines-actions">
                  <button
                    type="button"
                    className="einv-icon-btn"
                    aria-label={`${t('actions.moveLineUp')}: ${rowLabel}`}
                    disabled={index === 0}
                    onClick={() => onMove(index, -1)}
                  >
                    <FiArrowUp aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="einv-icon-btn"
                    aria-label={`${t('actions.moveLineDown')}: ${rowLabel}`}
                    disabled={index === lines.length - 1}
                    onClick={() => onMove(index, 1)}
                  >
                    <FiArrowDown aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="einv-icon-btn is-danger"
                    aria-label={`${t('actions.removeLine')}: ${rowLabel}`}
                    onClick={() => onRemove(index)}
                  >
                    <FiTrash2 aria-hidden="true" />
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <button type="button" className="einv-btn einv-btn-secondary" onClick={onAdd}>
        <FiPlus aria-hidden="true" />
        {t('actions.addLine')}
      </button>
    </div>
  );
};

export default InvoiceLinesTable;
