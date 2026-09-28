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
 *
 * Geometry is declared as named, non-overlapping columns. Overlapping boxes were a real defect: a
 * right-aligned totals label drew over the amount beside it, and the per-element visual comparison
 * then measured two strings at once.
 */

import type { SimpleElement } from '../../types';
import { EINVOICE_BINDINGS, type EInvoiceStandard } from './invoiceModel';

/** Where each block sits, and how a continuation page re-establishes the line table. */
export interface InvoiceTemplateLayout {
  /** First line row on page one, below the address block and the column headings. */
  readonly tableTop: number;
  /** First line row on a continuation page, which carries no address block. */
  readonly continuationTableTop: number;
  /** Column headings sit this far above their table top. */
  readonly columnHeaderOffset: number;
  /** The heading rule sits this far above its table top. */
  readonly headerRuleOffset: number;
  /** Height of a line row whose description occupies a single line. */
  readonly rowHeight: number;
  /** Vertical advance of one wrapped description line. */
  readonly lineHeight: number;
  /** The lowest y a Designer line row may occupy. */
  readonly rowLimit: number;
  /** The totals block needs this much vertical room. */
  readonly totalsHeight: number;
  /** The lowest y the totals block may start at. */
  readonly totalsLimit: number;
  /** The anchor the totals elements are positioned relative to in `elements`. */
  readonly totalsAnchor: number;
  /** Vertical advance of one tax-group row inside the totals block. */
  readonly taxRowHeight: number;
  /** Ids rendered on page one only. */
  readonly headerIds: readonly string[];
  /** Ids rendered at the top of every page that carries line rows. */
  readonly columnHeaderIds: readonly string[];
  /** Ids rendered once per invoice line. */
  readonly lineRowIds: readonly string[];
  /** Ids rendered once per tax group. */
  readonly taxRowIds: readonly string[];
  /** Ids of the totals block, positioned after the final line. */
  readonly totalsIds: readonly string[];
  /** Ids rendered at the foot of every page. */
  readonly footerIds: readonly string[];
  /** The id of the element whose text may wrap onto several lines. */
  readonly wrappingElementId: string;
}

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
  layout: InvoiceTemplateLayout;
}

/**
 * The typeface every maintained invoice layout uses.
 *
 * Naming the PDF engine's Helvetica slot puts all three surfaces on one face: the backend embeds
 * Liberation Sans for it, and the Canvas and Live Preview resolve it through `pdfFontFamily` to the
 * `PXA Helvetica` web font, which `src/styles/editor.css` loads from those very same files. That is
 * what lets all three break a long description at the same word and land right-aligned text on the
 * same pixel. Note the bare family name: a list ending in `sans-serif` would work in a browser, but
 * the slot lookup and the backend's `ParseFontFamily` both read the string, so keep it unambiguous.
 */
const TEMPLATE_FONT_FAMILY = 'Helvetica';

const A4_WIDTH = 595;
const A4_HEIGHT = 842;

/** Non-overlapping columns, left to right: `x + width` never reaches the next column's `x`. */
const COL = {
  position: { x: 42, width: 26 },
  description: { x: 72, width: 208 },
  quantity: { x: 284, width: 52 },
  unitPrice: { x: 340, width: 68 },
  taxRate: { x: 412, width: 42 },
  net: { x: 458, width: 62 },
  /** The currency code of the payable total, in its own column so it cannot overprint digits. */
  currency: { x: 524, width: 29 },
  /** Totals labels are right-aligned and stop short of the amount column. */
  totalsLabel: { x: 284, width: 166 },
} as const;

const LEFT = COL.position.x;
const RIGHT = 553;
const TOTALS_ANCHOR = 640;
const TAX_ROW_HEIGHT = 16;

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
      fontFamily: TEMPLATE_FONT_FAMILY,
      fontSize: options.size ?? 9,
      fontWeight: options.bold ? 700 : 400,
      textAlign: options.align ?? 'left',
      color: options.muted ? '#64748b' : '#0f172a',
      // Pinned so the Canvas cannot inherit a leading the PDF paragraph engine does not use.
      lineHeight: 1,
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
      fontFamily: TEMPLATE_FONT_FAMILY,
      fontSize: options.size ?? 9,
      fontWeight: options.bold ? 700 : 400,
      textAlign: options.align ?? 'left',
      color: '#0f172a',
      lineHeight: 1,
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

const HEADER_IDS = [
  'title', 'invoiceNumber', 'headerRule',
  'sellerHeading', 'sellerName', 'sellerStreet', 'sellerPostalCode', 'sellerCity',
  'sellerCountryCode', 'sellerTaxIdentifier',
  'buyerHeading', 'buyerName', 'buyerStreet', 'buyerPostalCode', 'buyerCity',
  'buyerCountryCode', 'buyerTaxIdentifier',
  'issueDateLabel', 'issueDate', 'dueDateLabel', 'dueDate',
  'buyerReferenceLabel', 'buyerReference', 'paymentReferenceLabel', 'paymentReference',
] as const;

const COLUMN_HEADER_IDS = [
  'lineNumberHeading', 'lineDescriptionHeading', 'lineQuantityHeading',
  'lineUnitPriceHeading', 'lineTaxRateHeading', 'lineNetHeading', 'tableHeaderRule',
] as const;

const LINE_ROW_IDS = [
  'lineIdentifier', 'lineDescription', 'lineQuantity', 'lineUnitPrice',
  'lineTaxRate', 'lineNetAmount',
] as const;

const TAX_ROW_IDS = ['taxRate', 'taxAmount'] as const;

const TOTALS_IDS = [
  'netTotalLabel', 'netTotal', 'totalsRule',
  'payableLabel', 'payableAmount', 'payableCurrency',
] as const;

const FOOTER_IDS = ['conformanceNote'] as const;

/**
 * The standard layout: seller and buyer blocks, document references, a line table, a tax
 * breakdown and the payable total. It matches the arrangement `EInvoice.CreatePdf` renders, so a
 * PDF produced from this template and one produced by the backend show the same figures in the
 * same order.
 */
function standardLayout(): SimpleElement[] {
  return [
    label('title', 'template.title', LEFT, 30, 240, { size: 20, bold: true }),
    bound('invoiceNumber', EINVOICE_BINDINGS.invoiceNumber, 350, 32, 203, { size: 12, bold: true, align: 'right' }),
    rule('headerRule', LEFT, 62, RIGHT - LEFT),

    label('sellerHeading', 'template.seller', LEFT, 78, 220, { bold: true, muted: true }),
    bound('sellerName', EINVOICE_BINDINGS.sellerName, LEFT, 94, 220, { bold: true }),
    bound('sellerStreet', EINVOICE_BINDINGS.sellerStreet, LEFT, 110, 220),
    bound('sellerPostalCode', EINVOICE_BINDINGS.sellerPostalCode, LEFT, 126, 60),
    bound('sellerCity', EINVOICE_BINDINGS.sellerCity, LEFT + 64, 126, 156),
    bound('sellerCountryCode', EINVOICE_BINDINGS.sellerCountryCode, LEFT, 142, 60),
    bound('sellerTaxIdentifier', EINVOICE_BINDINGS.sellerTaxIdentifier, LEFT, 158, 220, { size: 8 }),

    label('buyerHeading', 'template.buyer', 310, 78, 243, { bold: true, muted: true }),
    bound('buyerName', EINVOICE_BINDINGS.buyerName, 310, 94, 243, { bold: true }),
    bound('buyerStreet', EINVOICE_BINDINGS.buyerStreet, 310, 110, 243),
    bound('buyerPostalCode', EINVOICE_BINDINGS.buyerPostalCode, 310, 126, 60),
    bound('buyerCity', EINVOICE_BINDINGS.buyerCity, 374, 126, 179),
    bound('buyerCountryCode', EINVOICE_BINDINGS.buyerCountryCode, 310, 142, 60),
    bound('buyerTaxIdentifier', EINVOICE_BINDINGS.buyerTaxIdentifier, 310, 158, 243, { size: 8 }),

    label('issueDateLabel', 'template.issueDate', LEFT, 190, 90, { muted: true }),
    bound('issueDate', EINVOICE_BINDINGS.issueDate, LEFT + 94, 190, 90),
    label('dueDateLabel', 'template.dueDate', LEFT, 206, 90, { muted: true }),
    bound('dueDate', EINVOICE_BINDINGS.dueDate, LEFT + 94, 206, 90),
    label('buyerReferenceLabel', 'template.buyerReference', 310, 190, 110, { muted: true }),
    bound('buyerReference', EINVOICE_BINDINGS.buyerReference, 424, 190, 129),
    label('paymentReferenceLabel', 'template.paymentReference', 310, 206, 110, { muted: true }),
    bound('paymentReference', EINVOICE_BINDINGS.paymentReference, 424, 206, 129),

    label('lineNumberHeading', 'template.linePosition', COL.position.x, 250, COL.position.width, { bold: true, muted: true }),
    label('lineDescriptionHeading', 'template.lineDescription', COL.description.x, 250, COL.description.width, { bold: true, muted: true }),
    label('lineQuantityHeading', 'template.lineQuantity', COL.quantity.x, 250, COL.quantity.width, { bold: true, muted: true, align: 'right' }),
    label('lineUnitPriceHeading', 'template.lineUnitPrice', COL.unitPrice.x, 250, COL.unitPrice.width, { bold: true, muted: true, align: 'right' }),
    label('lineTaxRateHeading', 'template.lineTaxRate', COL.taxRate.x, 250, COL.taxRate.width, { bold: true, muted: true, align: 'right' }),
    label('lineNetHeading', 'template.lineNet', COL.net.x, 250, COL.net.width, { bold: true, muted: true, align: 'right' }),
    rule('tableHeaderRule', LEFT, 266, RIGHT - LEFT),

    // One instance per invoice line, bound to the canonical line paths.
    {
      id: 'lineRow',
      type: 'area',
      x: LEFT,
      y: 272,
      width: RIGHT - LEFT,
      height: 18,
      repeat: { dataPath: EINVOICE_BINDINGS.lines, templateId: 'lineRow' },
      style: {},
    },
    bound('lineIdentifier', EINVOICE_BINDINGS.lineIdentifier, COL.position.x, 272, COL.position.width),
    bound('lineDescription', EINVOICE_BINDINGS.lineDescription, COL.description.x, 272, COL.description.width),
    bound('lineQuantity', EINVOICE_BINDINGS.lineQuantity, COL.quantity.x, 272, COL.quantity.width, { align: 'right' }),
    bound('lineUnitPrice', EINVOICE_BINDINGS.lineUnitPrice, COL.unitPrice.x, 272, COL.unitPrice.width, { align: 'right', formatter: 'money' }),
    bound('lineTaxRate', EINVOICE_BINDINGS.lineTaxRate, COL.taxRate.x, 272, COL.taxRate.width, { align: 'right', formatter: 'percent' }),
    bound('lineNetAmount', EINVOICE_BINDINGS.lineNetAmount, COL.net.x, 272, COL.net.width, { align: 'right', formatter: 'money' }),

    label('netTotalLabel', 'template.netTotal', COL.totalsLabel.x, TOTALS_ANCHOR, COL.totalsLabel.width, { align: 'right' }),
    bound('netTotal', EINVOICE_BINDINGS.lineNetTotal, COL.net.x, TOTALS_ANCHOR, COL.net.width, { align: 'right', formatter: 'money' }),

    // One instance per tax group, in the backend's order: by rate, then category code.
    {
      id: 'taxRow',
      type: 'area',
      x: COL.totalsLabel.x,
      y: TOTALS_ANCHOR + 18,
      width: COL.net.x + COL.net.width - COL.totalsLabel.x,
      height: TAX_ROW_HEIGHT,
      repeat: { dataPath: EINVOICE_BINDINGS.taxGroups, templateId: 'taxRow' },
      style: {},
    },
    bound('taxRate', EINVOICE_BINDINGS.taxGroupRate, COL.totalsLabel.x, TOTALS_ANCHOR + 18, COL.totalsLabel.width, { align: 'right', formatter: 'percent' }),
    bound('taxAmount', EINVOICE_BINDINGS.taxGroupAmount, COL.net.x, TOTALS_ANCHOR + 18, COL.net.width, { align: 'right', formatter: 'money' }),

    rule('totalsRule', COL.totalsLabel.x, TOTALS_ANCHOR + 50, COL.net.x + COL.net.width - COL.totalsLabel.x),
    label('payableLabel', 'template.amountPayable', COL.totalsLabel.x, TOTALS_ANCHOR + 60, COL.totalsLabel.width, { size: 11, bold: true, align: 'right' }),
    bound('payableAmount', EINVOICE_BINDINGS.grandTotal, COL.net.x, TOTALS_ANCHOR + 60, COL.net.width, { size: 11, bold: true, align: 'right', formatter: 'money' }),
    bound('payableCurrency', EINVOICE_BINDINGS.currency, COL.currency.x, TOTALS_ANCHOR + 60, COL.currency.width, { size: 11, bold: true }),

    label('conformanceNote', 'template.conformanceNote', LEFT, 790, RIGHT - LEFT, { size: 7, muted: true }),
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

/**
 * Designer pagination bounds: a line row may reach y 740, and the totals block needs 150 points
 * and may start no lower than 790. The dedicated hybrid-invoice renderer uses a separate layout.
 */
function layoutFor(taxRows: boolean): InvoiceTemplateLayout {
  return {
    tableTop: 272,
    continuationTableTop: 82,
    columnHeaderOffset: 22,
    headerRuleOffset: 6,
    rowHeight: 18,
    lineHeight: 11,
    rowLimit: 740,
    totalsHeight: 150,
    totalsLimit: 790,
    totalsAnchor: TOTALS_ANCHOR,
    taxRowHeight: TAX_ROW_HEIGHT,
    headerIds: HEADER_IDS,
    columnHeaderIds: COLUMN_HEADER_IDS,
    lineRowIds: LINE_ROW_IDS,
    taxRowIds: taxRows ? TAX_ROW_IDS : [],
    totalsIds: TOTALS_IDS,
    footerIds: FOOTER_IDS,
    wrappingElementId: 'lineDescription',
  };
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
    layout: layoutFor(true),
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
    layout: layoutFor(false),
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

/** The declared columns, so a test can assert that no two of them overlap. */
export const INVOICE_TEMPLATE_COLUMNS = COL;
