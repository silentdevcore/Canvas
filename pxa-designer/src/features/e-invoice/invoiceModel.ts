/**
 * The typed invoice the Designer edits.
 *
 * Every field mirrors `PxaEInvoiceDocument` in PXA.Generator one-for-one, so what the Designer
 * shows, what `/e-invoices/generate` turns into XML, what `/e-invoices/create` renders, and what
 * the Viewer reads back out of the PDF are all the same values.
 *
 * Tax and billing rules deliberately do not live here. This module carries invoice *data* and the
 * arithmetic the standards define (line net, tax per group, totals); rate selection, exemption
 * reasons, ledger postings and filing stay in the caller's domain, outside PXA.Pdf.
 */

export type EInvoiceStandard = 'FacturX' | 'ZUGFeRD' | 'XRechnung';

/** Profiles PXA maintains, per standard. */
export const EINVOICE_PROFILES: Readonly<Record<EInvoiceStandard, readonly string[]>> = {
  FacturX: ['MINIMUM', 'BASIC WL', 'BASIC', 'EN 16931', 'EXTENDED'],
  ZUGFeRD: ['MINIMUM', 'BASIC WL', 'BASIC', 'EN 16931', 'EXTENDED'],
  XRechnung: ['XRECHNUNG'],
};

/** The one version PXA currently maintains per standard. */
export const EINVOICE_VERSIONS: Readonly<Record<EInvoiceStandard, string>> = {
  FacturX: '1.09.2',
  ZUGFeRD: '2.5.2',
  XRechnung: '3.0.2',
};

export interface EInvoiceAddress {
  street: string;
  city: string;
  postalCode: string;
  countryCode: string;
  additionalLine?: string;
}

export interface EInvoiceBusinessParty {
  name: string;
  identifier?: string;
  taxIdentifier?: string;
  endpointIdentifier?: string;
  address?: EInvoiceAddress;
}

export interface EInvoiceDocumentLine {
  identifier: string;
  description: string;
  quantity: number;
  unitCode: string;
  unitPrice: number;
  taxRate: number;
  /** UNCL5305 category code. "S" is standard rate. */
  taxCategoryCode: string;
}

export interface EInvoiceDocument {
  invoiceNumber: string;
  /** ISO date, `YYYY-MM-DD`, matching the backend's `DateOnly`. */
  issueDate: string;
  dueDate?: string;
  currency: string;
  seller: EInvoiceBusinessParty;
  buyer: EInvoiceBusinessParty;
  lines: EInvoiceDocumentLine[];
  buyerReference?: string;
  paymentReference?: string;
  currencyDecimalPlaces: number;
}

export interface EInvoiceSettings {
  standard: EInvoiceStandard;
  version: string;
  profile: string;
}

/**
 * Canonical binding paths. Template elements reference these strings, and the preview resolves
 * them against the same object that is sent to the backend - there is no second copy of an
 * invoice value anywhere in the Designer.
 */
export const EINVOICE_BINDINGS = {
  invoiceNumber: 'invoice.invoiceNumber',
  issueDate: 'invoice.issueDate',
  dueDate: 'invoice.dueDate',
  currency: 'invoice.currency',
  buyerReference: 'invoice.buyerReference',
  paymentReference: 'invoice.paymentReference',
  sellerName: 'invoice.seller.name',
  sellerIdentifier: 'invoice.seller.identifier',
  sellerTaxIdentifier: 'invoice.seller.taxIdentifier',
  sellerStreet: 'invoice.seller.address.street',
  sellerCity: 'invoice.seller.address.city',
  sellerPostalCode: 'invoice.seller.address.postalCode',
  sellerCountryCode: 'invoice.seller.address.countryCode',
  buyerName: 'invoice.buyer.name',
  buyerIdentifier: 'invoice.buyer.identifier',
  buyerTaxIdentifier: 'invoice.buyer.taxIdentifier',
  buyerStreet: 'invoice.buyer.address.street',
  buyerCity: 'invoice.buyer.address.city',
  buyerPostalCode: 'invoice.buyer.address.postalCode',
  buyerCountryCode: 'invoice.buyer.address.countryCode',
  lines: 'invoice.lines',
  lineIdentifier: 'line.identifier',
  lineDescription: 'line.description',
  lineQuantity: 'line.quantity',
  lineUnitCode: 'line.unitCode',
  lineUnitPrice: 'line.unitPrice',
  lineTaxRate: 'line.taxRate',
  lineNetAmount: 'line.netAmount',
  taxGroups: 'totals.taxes',
  taxGroupRate: 'tax.rate',
  taxGroupBasis: 'tax.basis',
  taxGroupAmount: 'tax.amount',
  lineNetTotal: 'totals.lineNetTotal',
  taxTotal: 'totals.taxTotal',
  grandTotal: 'totals.grandTotal',
} as const;

export type EInvoiceBindingKey = keyof typeof EINVOICE_BINDINGS;

/** Every canonical path, for drift-guarding templates and documentation against this module. */
export const EINVOICE_BINDING_PATHS: readonly string[] = Object.freeze(
  Object.values(EINVOICE_BINDINGS),
);

export function createEmptyInvoice(issueDate = todayIso()): EInvoiceDocument {
  return {
    invoiceNumber: '',
    issueDate,
    currency: 'EUR',
    seller: { name: '', address: { street: '', city: '', postalCode: '', countryCode: '' } },
    buyer: { name: '', address: { street: '', city: '', postalCode: '', countryCode: '' } },
    lines: [createEmptyLine(1)],
    currencyDecimalPlaces: 2,
  };
}

export function createEmptyLine(position: number): EInvoiceDocumentLine {
  return {
    identifier: String(position),
    description: '',
    quantity: 1,
    unitCode: 'C62',
    unitPrice: 0,
    taxRate: 0,
    taxCategoryCode: 'S',
  };
}

export function defaultSettings(standard: EInvoiceStandard = 'FacturX'): EInvoiceSettings {
  return {
    standard,
    version: EINVOICE_VERSIONS[standard],
    profile: standard === 'XRechnung' ? 'XRECHNUNG' : 'EN 16931',
  };
}

/**
 * Keeps version and profile consistent when the standard changes, so the Designer can never send
 * a combination the backend will reject.
 */
export function withStandard(settings: EInvoiceSettings, standard: EInvoiceStandard): EInvoiceSettings {
  const profiles = EINVOICE_PROFILES[standard];
  return {
    standard,
    version: EINVOICE_VERSIONS[standard],
    profile: profiles.includes(settings.profile) ? settings.profile : profiles[profiles.length - 1],
  };
}

/** The XML filename each standard reserves, mirroring `EInvoice.GetDefaultFileName`. */
export function attachmentFileName(standard: EInvoiceStandard): string {
  return standard === 'XRechnung' ? 'xrechnung.xml' : 'factur-x.xml';
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/** The request body `/e-invoices/generate` and `/e-invoices/create` expect. */
export function toGenerationRequest(invoice: EInvoiceDocument, settings: EInvoiceSettings) {
  return {
    standard: settings.standard,
    version: settings.version,
    profile: settings.profile,
    invoice,
  };
}
