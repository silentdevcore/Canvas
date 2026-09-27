/**
 * Maintained invoice templates.
 *
 * Each template is a plain arrangement of `SimpleElement`s whose every dynamic value is a
 * canonical binding from `EINVOICE_BINDINGS`. Nothing here holds a literal amount, party or date:
 * the layout is the only thing a template owns, so the figures on the page can only ever be the
 * ones the invoice carries - the same ones the XML and a later Viewer inspection report.
 *
 * Labels are i18n keys resolved at render time, which is what keeps one template usable in all six
 * Designer languages instead of needing a translated copy per language.
 */

import type { SimpleElement } from '../../types';
import { EINVOICE_BINDINGS, type EInvoiceStandard } from './invoiceModel';

export interface InvoiceTemplateDefinition {
  id: string;
  /** i18n key under the `eInvoice` namespace. */
  nameKey: string;
  descriptionKey: string;
  /** Standards this layout is maintained for. */
  standards: readonly EInvoiceStandard[];
  pageWidth: number;
  pageHeight: number;
  elements: SimpleElement[];
  /** The element that repeats once per invoice line. */
  lineTemplateId: string;
  /** The element that repeats once per tax group. */
  taxTemplateId: string;
}

const A4_WIDTH = 595;
const A4_HEIGHT = 842;

/** i18n-keyed static label. */
function label(
  id: string,
  key: string,
  x: number,
  y: number,
  width: number,
  options: { size?: number; bold?: boolean; align?: 'left' | 'right'; muted?: boolean } = {},
): SimpleElement {
  return {
    id,
    type: 'text',
    // The content is a key; the renderer translates it. Never a hard-coded language.
    content: `{{t:${key}}}`,
    x,
    y,
    width,
    height: 14,
    style: {
      fontSize: options.size ?? 9,
      fontWeight: options.bold ? 700 : 400,
      textAlign: options.align ?? 'left',
      color: options.muted ? '#64748b' : '#0f172a',
    },
  };
}

/** Bound value, resolved from the canonical invoice. */
function bound(
  id: string,
  binding: string,
  x: number,
  y: number,
  width: number,
  options: { size?: number; bold?: boolean; align?: 'left' | 'right'; formatter?: string } = {},
): SimpleElement {
  return {
    id,
    type: 'text',
    binding,
    formatter: options.formatter,
    x,
    y,
    width,
    height: 14,
    style: {
      fontSize: options.size ?? 9,
      fontWeight: options.bold ? 700 : 400,
      textAlign: options.align ?? 'left',
      color: '#0f172a',
    },
  };
}

function rule(id: string, x: number, y: number, width: number): SimpleElement {
  return {
    id,
    type: 'line',
    x,
    y,
    width,
    height: 1,
    style: { backgroundColor: '#22313f' },
  };
}

/**
 * The standard layout: seller and buyer blocks, document references, a line table, a tax
 * breakdown and the payable total. It matches the arrangement `EInvoice.CreatePdf` renders, so a
 * PDF produced from this template and one produced by the backend show the same figures in the
 * same order.
 */
function standardLayout(): SimpleElement[] {
  const left = 42;
  const right = 553;
  const amountColumn = right - 90;

  return [
    label('title', 'template.title', left, 30, 240, { size: 20, bold: true }),
    bound('invoiceNumber', EINVOICE_BINDINGS.invoiceNumber, 350, 32, 203, { size: 12, bold: true, align: 'right' }),
    rule('headerRule', left, 62, right - left),

    label('sellerHeading', 'template.seller', left, 78, 220, { bold: true, muted: true }),
    bound('sellerName', EINVOICE_BINDINGS.sellerName, left, 94, 220, { bold: true }),
    bound('sellerStreet', EINVOICE_BINDINGS.sellerStreet, left, 110, 220),
    bound('sellerPostalCode', EINVOICE_BINDINGS.sellerPostalCode, left, 126, 60),
    bound('sellerCity', EINVOICE_BINDINGS.sellerCity, left + 64, 126, 156),
    bound('sellerCountryCode', EINVOICE_BINDINGS.sellerCountryCode, left, 142, 60),
    bound('sellerTaxIdentifier', EINVOICE_BINDINGS.sellerTaxIdentifier, left, 158, 220, { size: 8 }),

    label('buyerHeading', 'template.buyer', 310, 78, 243, { bold: true, muted: true }),
    bound('buyerName', EINVOICE_BINDINGS.buyerName, 310, 94, 243, { bold: true }),
    bound('buyerStreet', EINVOICE_BINDINGS.buyerStreet, 310, 110, 243),
    bound('buyerPostalCode', EINVOICE_BINDINGS.buyerPostalCode, 310, 126, 60),
    bound('buyerCity', EINVOICE_BINDINGS.buyerCity, 374, 126, 179),
    bound('buyerCountryCode', EINVOICE_BINDINGS.buyerCountryCode, 310, 142, 60),
    bound('buyerTaxIdentifier', EINVOICE_BINDINGS.buyerTaxIdentifier, 310, 158, 243, { size: 8 }),

    label('issueDateLabel', 'template.issueDate', left, 190, 90, { muted: true }),
    bound('issueDate', EINVOICE_BINDINGS.issueDate, left + 94, 190, 90),
    label('dueDateLabel', 'template.dueDate', left, 206, 90, { muted: true }),
    bound('dueDate', EINVOICE_BINDINGS.dueDate, left + 94, 206, 90),
    label('buyerReferenceLabel', 'template.buyerReference', 310, 190, 110, { muted: true }),
    bound('buyerReference', EINVOICE_BINDINGS.buyerReference, 424, 190, 129),
    label('paymentReferenceLabel', 'template.paymentReference', 310, 206, 110, { muted: true }),
    bound('paymentReference', EINVOICE_BINDINGS.paymentReference, 424, 206, 129),

    label('lineNumberHeading', 'template.linePosition', left, 250, 30, { bold: true, muted: true }),
    label('lineDescriptionHeading', 'template.lineDescription', left + 34, 250, 200, { bold: true, muted: true }),
    label('lineQuantityHeading', 'template.lineQuantity', 300, 250, 60, { bold: true, muted: true, align: 'right' }),
    label('lineUnitPriceHeading', 'template.lineUnitPrice', 366, 250, 70, { bold: true, muted: true, align: 'right' }),
    label('lineTaxRateHeading', 'template.lineTaxRate', 442, 250, 40, { bold: true, muted: true, align: 'right' }),
    label('lineNetHeading', 'template.lineNet', amountColumn, 250, 90, { bold: true, muted: true, align: 'right' }),
    rule('tableHeaderRule', left, 266, right - left),

    // One instance per invoice line, bound to the canonical line paths.
    {
      id: 'lineRow',
      type: 'area',
      x: left,
      y: 272,
      width: right - left,
      height: 18,
      repeat: { dataPath: EINVOICE_BINDINGS.lines, templateId: 'lineRow' },
      style: {},
    },
    bound('lineIdentifier', EINVOICE_BINDINGS.lineIdentifier, left, 272, 30),
    bound('lineDescription', EINVOICE_BINDINGS.lineDescription, left + 34, 272, 200),
    bound('lineQuantity', EINVOICE_BINDINGS.lineQuantity, 300, 272, 60, { align: 'right' }),
    bound('lineUnitPrice', EINVOICE_BINDINGS.lineUnitPrice, 366, 272, 70, { align: 'right', formatter: 'money' }),
    bound('lineTaxRate', EINVOICE_BINDINGS.lineTaxRate, 442, 272, 40, { align: 'right', formatter: 'percent' }),
    bound('lineNetAmount', EINVOICE_BINDINGS.lineNetAmount, amountColumn, 272, 90, { align: 'right', formatter: 'money' }),

    label('netTotalLabel', 'template.netTotal', 366, 640, 116, { align: 'right' }),
    bound('netTotal', EINVOICE_BINDINGS.lineNetTotal, amountColumn, 640, 90, { align: 'right', formatter: 'money' }),

    // One instance per tax group, in the backend's order: by rate, then category code.
    {
      id: 'taxRow',
      type: 'area',
      x: 366,
      y: 658,
      width: right - 366,
      height: 16,
      repeat: { dataPath: EINVOICE_BINDINGS.taxGroups, templateId: 'taxRow' },
      style: {},
    },
    bound('taxRate', EINVOICE_BINDINGS.taxGroupRate, 366, 658, 116, { align: 'right', formatter: 'percent' }),
    bound('taxAmount', EINVOICE_BINDINGS.taxGroupAmount, amountColumn, 658, 90, { align: 'right', formatter: 'money' }),

    rule('totalsRule', 366, 690, right - 366),
    label('payableLabel', 'template.amountPayable', 366, 700, 116, { size: 11, bold: true, align: 'right' }),
    bound('payableAmount', EINVOICE_BINDINGS.grandTotal, amountColumn, 700, 90, { size: 11, bold: true, align: 'right', formatter: 'money' }),
    bound('payableCurrency', EINVOICE_BINDINGS.currency, right - 24, 700, 24, { size: 11, bold: true }),

    label('conformanceNote', 'template.conformanceNote', left, 790, right - left, { size: 7, muted: true }),
  ];
}

/**
 * The compact layout omits the tax breakdown rows, for the MINIMUM and BASIC WL profiles where a
 * per-group presentation is not expected.
 */
function compactLayout(): SimpleElement[] {
  return standardLayout().filter(
    element => !['taxRow', 'taxRate', 'taxAmount'].includes(element.id),
  );
}

export const INVOICE_TEMPLATES: readonly InvoiceTemplateDefinition[] = Object.freeze([
  {
    id: 'invoice-standard',
    nameKey: 'template.standardName',
    descriptionKey: 'template.standardDescription',
    standards: ['FacturX', 'ZUGFeRD', 'XRechnung'],
    pageWidth: A4_WIDTH,
    pageHeight: A4_HEIGHT,
    elements: standardLayout(),
    lineTemplateId: 'lineRow',
    taxTemplateId: 'taxRow',
  },
  {
    id: 'invoice-compact',
    nameKey: 'template.compactName',
    descriptionKey: 'template.compactDescription',
    standards: ['FacturX', 'ZUGFeRD'],
    pageWidth: A4_WIDTH,
    pageHeight: A4_HEIGHT,
    elements: compactLayout(),
    lineTemplateId: 'lineRow',
    taxTemplateId: 'taxRow',
  },
]);

export function invoiceTemplate(id: string): InvoiceTemplateDefinition | undefined {
  return INVOICE_TEMPLATES.find(template => template.id === id);
}

export function templatesForStandard(
  standard: EInvoiceStandard,
): readonly InvoiceTemplateDefinition[] {
  return INVOICE_TEMPLATES.filter(template => template.standards.includes(standard));
}

/** Every binding a template references, for drift-guarding against `EINVOICE_BINDINGS`. */
export function templateBindings(template: InvoiceTemplateDefinition): readonly string[] {
  const bindings = template.elements
    .flatMap(element => [element.binding, element.repeat?.dataPath])
    .filter((value): value is string => typeof value === 'string' && value.length > 0);
  return [...new Set(bindings)].sort();
}

/** Every i18n key a template's labels reference. */
export function templateLabelKeys(template: InvoiceTemplateDefinition): readonly string[] {
  const keys = template.elements
    .map(element => /^\{\{t:([^}]+)\}\}$/.exec(element.content ?? '')?.[1])
    .filter((value): value is string => typeof value === 'string');
  return [...new Set([template.nameKey, template.descriptionKey, ...keys])].sort();
}
