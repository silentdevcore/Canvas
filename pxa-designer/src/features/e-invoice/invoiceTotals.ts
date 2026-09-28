/**
 * Invoice arithmetic, mirroring `EInvoice.Calculate` in PXA.Generator exactly.
 *
 * The rules are the ones the standards fix, not tax policy:
 *   line net   = round(quantity * unitPrice, currencyDecimalPlaces)
 *   tax groups = lines grouped by (taxCategoryCode, taxRate), ordered by rate then category
 *   tax amount = round(groupBasis * rate / 100, currencyDecimalPlaces)
 *   lineNetTotal = sum of line nets; taxTotal = sum of group amounts; grand = net + tax
 *
 * Choosing a rate, claiming an exemption or deciding what is taxable is billing-domain work and
 * stays with the caller. `test-assets/e-invoicing/totals-parity.json` pins these results and is
 * asserted from both this module and the C# generator, so the two can never drift apart silently.
 */

import {
  type ExactDecimal,
  add,
  compare,
  fromNumber,
  multiply,
  round,
  sum,
  toFixed,
  toNumber,
  ZERO,
} from './exactDecimal';
import type { EInvoiceDocument, EInvoiceDocumentLine } from './invoiceModel';

export interface CalculatedInvoiceLine {
  readonly source: EInvoiceDocumentLine;
  /** Rounded line net amount. */
  readonly netAmount: ExactDecimal;
}

export interface CalculatedTaxGroup {
  readonly categoryCode: string;
  readonly rate: ExactDecimal;
  readonly basis: ExactDecimal;
  readonly amount: ExactDecimal;
}

export interface CalculatedInvoice {
  readonly lines: readonly CalculatedInvoiceLine[];
  readonly taxes: readonly CalculatedTaxGroup[];
  readonly lineNetTotal: ExactDecimal;
  readonly taxTotal: ExactDecimal;
  readonly grandTotal: ExactDecimal;
  readonly decimalPlaces: number;
}

export function calculateInvoice(invoice: EInvoiceDocument): CalculatedInvoice {
  const decimalPlaces = invoice.currencyDecimalPlaces;
  const lines: CalculatedInvoiceLine[] = (invoice.lines ?? []).map(line => ({
    source: line,
    netAmount: round(
      multiply(fromNumber(line.quantity), fromNumber(line.unitPrice)),
      decimalPlaces,
    ),
  }));

  // Group insertion order is irrelevant: the result is sorted below, as the backend does.
  const groups = new Map<string, { categoryCode: string; rate: ExactDecimal; basis: ExactDecimal }>();
  for (const line of lines) {
    const rate = fromNumber(line.source.taxRate);
    const categoryCode = line.source.taxCategoryCode;
    const key = `${categoryCode}\u0000${rateKey(rate)}`;
    const existing = groups.get(key);
    if (existing) {
      groups.set(key, { ...existing, basis: add(existing.basis, line.netAmount) });
    } else {
      groups.set(key, { categoryCode, rate, basis: line.netAmount });
    }
  }

  const taxes: CalculatedTaxGroup[] = [...groups.values()]
    .map(group => ({
      categoryCode: group.categoryCode,
      rate: group.rate,
      basis: group.basis,
      amount: round(divideByHundred(multiply(group.basis, group.rate)), decimalPlaces),
    }))
    .sort(
      (left, right) =>
        compare(left.rate, right.rate) || ordinalCompare(left.categoryCode, right.categoryCode),
    );

  const lineNetTotal = sum(lines.map(line => line.netAmount));
  const taxTotal = sum(taxes.map(tax => tax.amount));
  return {
    lines,
    taxes,
    lineNetTotal,
    taxTotal,
    grandTotal: add(lineNetTotal, taxTotal),
    decimalPlaces,
  };
}

/**
 * `basis * rate / 100` without a division: shifting the scale by two is exact, where dividing
 * would introduce a repeating fraction the backend's decimal arithmetic never sees.
 */
function divideByHundred(value: ExactDecimal): ExactDecimal {
  return { units: value.units, scale: value.scale + 2 };
}

function rateKey(rate: ExactDecimal): string {
  // Normalized in `exactDecimal`, so 19 and 19.00 share one key - as (categoryCode, rate)
  // grouping on .NET decimal does.
  return `${rate.units}e${rate.scale}`;
}

/** Matches `StringComparer.Ordinal`: compares UTF-16 code units, not locale collation. */
function ordinalCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}

/** The values a template binds to, resolved once from the canonical invoice. */
export interface InvoiceBindingContext {
  invoice: EInvoiceDocument;
  totals: {
    lineNetTotal: number;
    taxTotal: number;
    grandTotal: number;
    lineNetTotalText: string;
    taxTotalText: string;
    grandTotalText: string;
    taxes: Array<{
      categoryCode: string;
      rate: number;
      basis: number;
      amount: number;
      basisText: string;
      amountText: string;
    }>;
  };
  lines: Array<
    EInvoiceDocumentLine & {
      netAmount: number;
      netAmountText: string;
      unitPriceText: string;
      taxRateText: string;
    }
  >;
}

/**
 * Builds the preview's binding context. Amounts appear both as numbers (for layout maths) and as
 * fixed-point text identical to the backend's "F2" output, so the preview shows the same digits
 * the PDF and the XML will carry.
 */
export function createBindingContext(invoice: EInvoiceDocument): InvoiceBindingContext {
  const calculated = calculateInvoice(invoice);
  const places = calculated.decimalPlaces;
  return {
    invoice,
    totals: {
      lineNetTotal: toNumber(calculated.lineNetTotal),
      taxTotal: toNumber(calculated.taxTotal),
      grandTotal: toNumber(calculated.grandTotal),
      lineNetTotalText: toFixed(calculated.lineNetTotal, places),
      taxTotalText: toFixed(calculated.taxTotal, places),
      grandTotalText: toFixed(calculated.grandTotal, places),
      taxes: calculated.taxes.map(tax => ({
        categoryCode: tax.categoryCode,
        rate: toNumber(tax.rate),
        basis: toNumber(tax.basis),
        amount: toNumber(tax.amount),
        // The percentage as the page shows it. The hybrid renderer writes the same string, so the
        // Designer and `EInvoice.CreatePdf` present one tax rate in one form.
        rateText: formatRate(toNumber(tax.rate)),
        basisText: toFixed(tax.basis, places),
        amountText: toFixed(tax.amount, places),
      })),
    },
    lines: calculated.lines.map(line => ({
      ...line.source,
      netAmount: toNumber(line.netAmount),
      netAmountText: toFixed(line.netAmount, places),
      // The unit price in the currency's minor units, which is how the hybrid renderer writes it.
      // Without this the Designer showed the raw number - "24.9" where the PDF said "24.90".
      unitPriceText: toFixed(fromNumber(line.source.unitPrice), places),
      taxRateText: formatRate(line.source.taxRate),
    })),
  };
}

/**
 * A tax rate as both renderers print it: the shortest exact decimal, with a percent sign. Matches
 * `EInvoice.Decimal` in the generator, which uses the same trailing-zero-free form.
 */
export function formatRate(rate: number): string {
  return `${rate}%`;
}

/** Fixed-point text for one amount, matching the backend's money formatting. */
export function formatAmount(value: number, decimalPlaces: number): string {
  return toFixed(fromNumber(value), decimalPlaces);
}

export const EMPTY_TOTALS: CalculatedInvoice = {
  lines: [],
  taxes: [],
  lineNetTotal: ZERO,
  taxTotal: ZERO,
  grandTotal: ZERO,
  decimalPlaces: 2,
};
