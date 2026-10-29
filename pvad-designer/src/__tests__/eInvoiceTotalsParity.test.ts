/**
 * Asserts the Designer's invoice arithmetic against the shared parity corpus in
 * `test-assets/e-invoicing/totals-parity.json`. `tests/PVAD.Generator.Tests/
 * EInvoiceTotalsParityTests.cs` asserts the same file, so changing one calculation without the
 * other fails on both sides.
 */

import { readFileSync } from 'fs';
import { join } from 'path';
import {
  calculateInvoice,
  createBindingContext,
} from '../features/e-invoice/invoiceTotals';
import { createEmptyInvoice, type EInvoiceDocument } from '../features/e-invoice/invoiceModel';
import { fromString, round, toFixed, toPlainString } from '../features/e-invoice/exactDecimal';

interface ParityLine {
  identifier: string;
  description: string;
  quantity: number;
  unitCode: string;
  unitPrice: number;
  taxRate: number;
  taxCategoryCode: string;
}

interface ParityCase {
  id: string;
  description: string;
  currencyDecimalPlaces: number;
  lines: ParityLine[];
  expected: {
    lineNetAmounts: string[];
    taxes: Array<{ categoryCode: string; rate: string; basis: string; amount: string }>;
    lineNetTotal: string;
    taxTotal: string;
    grandTotal: string;
  };
}

const corpus: { schema: string; cases: ParityCase[] } = JSON.parse(
  readFileSync(join(__dirname, '../../../test-assets/e-invoicing/totals-parity.json'), 'utf8'),
);

function invoiceFor(testCase: ParityCase): EInvoiceDocument {
  return {
    ...createEmptyInvoice('2026-09-27'),
    invoiceNumber: `PARITY-${testCase.id}`,
    currency: 'EUR',
    currencyDecimalPlaces: testCase.currencyDecimalPlaces,
    seller: {
      name: 'Seller GmbH',
      address: { street: 'Main Street 1', city: 'Berlin', postalCode: '10115', countryCode: 'DE' },
    },
    buyer: {
      name: 'Buyer AG',
      address: { street: 'Buyer Street 2', city: 'Munich', postalCode: '80331', countryCode: 'DE' },
    },
    lines: testCase.lines,
  };
}

describe('invoice totals parity with PVAD.Generator', () => {
  it('loads the shared corpus', () => {
    expect(corpus.schema).toBe('pxa.e-invoice.totals-parity/1');
    expect(corpus.cases.length).toBeGreaterThanOrEqual(10);
    // Large enough that the Designer paginates it, so the totals are proven across page breaks.
    const multiPage = corpus.cases.find(testCase => testCase.id === 'multi-page-projection');
    expect(multiPage?.lines.length ?? 0).toBeGreaterThanOrEqual(60);
  });

  it.each(corpus.cases.map(testCase => [testCase.id, testCase] as const))(
    'reproduces the pinned totals for %s',
    (_id, testCase) => {
      const places = testCase.currencyDecimalPlaces;
      const calculated = calculateInvoice(invoiceFor(testCase));

      expect(calculated.lines.map(line => toFixed(line.netAmount, places))).toEqual(
        testCase.expected.lineNetAmounts,
      );
      expect(
        calculated.taxes.map(tax => ({
          categoryCode: tax.categoryCode,
          rate: toPlainString(tax.rate),
          basis: toFixed(tax.basis, places),
          amount: toFixed(tax.amount, places),
        })),
      ).toEqual(testCase.expected.taxes);
      expect(toFixed(calculated.lineNetTotal, places)).toBe(testCase.expected.lineNetTotal);
      expect(toFixed(calculated.taxTotal, places)).toBe(testCase.expected.taxTotal);
      expect(toFixed(calculated.grandTotal, places)).toBe(testCase.expected.grandTotal);
    },
  );

  it('exposes the same totals through the preview binding context', () => {
    const testCase = corpus.cases.find(item => item.id === 'multiple-rates-ordered-by-rate')!;
    const invoice = invoiceFor(testCase);
    const context = createBindingContext(invoice);

    expect(context.totals.lineNetTotalText).toBe(testCase.expected.lineNetTotal);
    expect(context.totals.taxTotalText).toBe(testCase.expected.taxTotal);
    expect(context.totals.grandTotalText).toBe(testCase.expected.grandTotal);
    expect(context.lines.map(line => line.netAmountText)).toEqual(
      testCase.expected.lineNetAmounts,
    );
    expect(context.totals.taxes.map(tax => tax.amountText)).toEqual(
      testCase.expected.taxes.map(tax => tax.amount),
    );
    // The preview binds the very object that is sent to the backend, so there is no second
    // copy of an invoice value that could disagree with the XML or the PDF.
    expect(context.invoice).toBe(invoice);
  });
});

describe('exact decimal arithmetic', () => {
  it('rounds half away from zero, not half up', () => {
    expect(toFixed(round(fromString('0.125'), 2), 2)).toBe('0.13');
    expect(toFixed(round(fromString('-0.125'), 2), 2)).toBe('-0.13');
    expect(toFixed(round(fromString('2.5'), 0), 0)).toBe('3');
    expect(toFixed(round(fromString('-2.5'), 0), 0)).toBe('-3');
    // Banker's rounding would give 2 and -2 here; the backend does not use it.
    expect(toFixed(round(fromString('1.005'), 2), 2)).toBe('1.01');
  });

  it('adds decimal fractions without binary drift', () => {
    const invoice: EInvoiceDocument = {
      ...createEmptyInvoice('2026-09-27'),
      seller: { name: 'S' },
      buyer: { name: 'B' },
      lines: [
        { identifier: '1', description: 'a', quantity: 1, unitCode: 'C62', unitPrice: 0.1, taxRate: 0, taxCategoryCode: 'S' },
        { identifier: '2', description: 'b', quantity: 1, unitCode: 'C62', unitPrice: 0.2, taxRate: 0, taxCategoryCode: 'S' },
      ],
    };

    expect(toFixed(calculateInvoice(invoice).lineNetTotal, 2)).toBe('0.30');
    // The naive floating-point sum this replaces.
    expect(0.1 + 0.2).not.toBe(0.3);
  });

  it('accepts exponent notation and rejects non-numeric text', () => {
    expect(toPlainString(fromString('1.5e2'))).toBe('150');
    expect(toPlainString(fromString('1.5e-2'))).toBe('0.015');
    expect(() => fromString('nineteen')).toThrow(/not a decimal number/);
    expect(() => fromString('')).toThrow(/not a decimal number/);
  });

  it('treats equal values with different trailing zeros as one tax group', () => {
    const invoice: EInvoiceDocument = {
      ...createEmptyInvoice('2026-09-27'),
      seller: { name: 'S' },
      buyer: { name: 'B' },
      lines: [
        { identifier: '1', description: 'a', quantity: 1, unitCode: 'C62', unitPrice: 100, taxRate: 19, taxCategoryCode: 'S' },
        { identifier: '2', description: 'b', quantity: 1, unitCode: 'C62', unitPrice: 100, taxRate: 19.0, taxCategoryCode: 'S' },
      ],
    };

    const calculated = calculateInvoice(invoice);
    expect(calculated.taxes).toHaveLength(1);
    expect(toFixed(calculated.taxes[0].basis, 2)).toBe('200.00');
    expect(toFixed(calculated.taxes[0].amount, 2)).toBe('38.00');
  });

  it('handles an invoice with no lines without throwing', () => {
    const calculated = calculateInvoice({
      ...createEmptyInvoice('2026-09-27'),
      seller: { name: 'S' },
      buyer: { name: 'B' },
      lines: [],
    });

    expect(calculated.taxes).toHaveLength(0);
    expect(toFixed(calculated.grandTotal, 2)).toBe('0.00');
  });
});
