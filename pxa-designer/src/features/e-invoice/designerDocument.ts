/**
 * The bridge between the canonical invoice model and the Designer document.
 *
 * There is exactly one copy of every invoice value: `template.eInvoice.invoice`. The page
 * elements the canvas, the Live Preview and the exported PDF render are a *projection* of
 * that one object - regenerated from it whenever it changes, never edited independently and
 * never read back into it. Each projected element keeps its `binding` attribute, so the
 * canonical path a value came from stays visible in the document.
 *
 * The XML and the PDF are produced by the backend from the same `invoice` object, so no
 * surface can disagree with another about a figure.
 */

import type { Page, SimpleElement, TemplateEInvoiceState } from '@/types';
import {
  EINVOICE_PROFILES,
  EINVOICE_VERSIONS,
  createEmptyInvoice,
  createEmptyLine,
  defaultSettings,
  withStandard,
  type EInvoiceDocument,
  type EInvoiceSettings,
  type EInvoiceStandard,
} from './invoiceModel';
import { createBindingContext } from './invoiceTotals';
import { planInvoicePages } from './invoicePagination';
import type { InvoiceTemplateDefinition } from './invoiceTemplates';

export interface EInvoiceDesignerDocument {
  invoice: EInvoiceDocument;
  settings: EInvoiceSettings;
  templateId?: string;
  lineIds: string[];
}

/** Translates an i18n key from a template label. */
export type TemplateTranslator = (key: string) => string;

const LABEL_PATTERN = /^\{\{t:([^}]+)\}\}$/;

function isStandard(value: unknown): value is EInvoiceStandard {
  return value === 'FacturX' || value === 'ZUGFeRD' || value === 'XRechnung';
}

/**
 * Reads the invoice a stored template carries, repairing anything an older or hand-edited
 * draft may be missing. It returns null only when the template is not an invoice at all, so
 * the caller can tell "no invoice yet" from "an invoice that needs defaults".
 */
export function readEInvoiceDocument(
  state: TemplateEInvoiceState | undefined,
): EInvoiceDesignerDocument | null {
  if (!state || typeof state !== 'object' || !state.invoice) return null;

  const stored = state.invoice as Partial<EInvoiceDocument>;
  const storedIssueDate = typeof stored.issueDate === 'string' ? stored.issueDate : undefined;
  const empty = createEmptyInvoice(storedIssueDate);
  const standard = isStandard(state.settings?.standard) ? state.settings.standard : 'FacturX';
  const storedProfile = state.settings?.profile;
  const settings: EInvoiceSettings = {
    standard,
    // The maintained version is ours, not the draft's: a stored draft must never pin PXA to
    // a version this build no longer supports.
    version: EINVOICE_VERSIONS[standard],
    profile: EINVOICE_PROFILES[standard].includes(storedProfile ?? '')
      ? storedProfile!
      : defaultSettings(standard).profile,
  };

  const lines = Array.isArray(stored.lines)
    ? stored.lines.map((line, index) => normalizeLine(line, index + 1))
    : empty.lines;

  return {
    invoice: {
      ...empty,
      ...stored,
      invoiceNumber: text(stored.invoiceNumber, empty.invoiceNumber),
      issueDate: text(stored.issueDate, empty.issueDate),
      dueDate: optionalText(stored.dueDate),
      currency: text(stored.currency, empty.currency),
      buyerReference: optionalText(stored.buyerReference),
      paymentReference: optionalText(stored.paymentReference),
      seller: normalizeParty(stored.seller, empty.seller),
      buyer: normalizeParty(stored.buyer, empty.buyer),
      lines,
      currencyDecimalPlaces: Number.isInteger(stored.currencyDecimalPlaces)
        && stored.currencyDecimalPlaces! >= 0
        && stored.currencyDecimalPlaces! <= 4
        ? stored.currencyDecimalPlaces!
        : empty.currencyDecimalPlaces,
    },
    settings,
    templateId: state.templateId,
    lineIds: normalizeLineIds(state.lineIds, lines.length),
  };
}

function normalizeLineIds(value: unknown, count: number): string[] {
  const stored = Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string' && /^line-\d+$/.test(item))
    : [];
  const used = new Set<string>();
  const result: string[] = [];
  let next = 1;
  for (let index = 0; index < count; index += 1) {
    const candidate = stored[index];
    if (candidate && !used.has(candidate)) {
      used.add(candidate);
      result.push(candidate);
      continue;
    }
    while (used.has(`line-${next}`)) next += 1;
    const generated = `line-${next++}`;
    used.add(generated);
    result.push(generated);
  }
  return result;
}

function text(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

function optionalText(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

function finiteNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function normalizeParty(
  value: unknown,
  fallback: EInvoiceDocument['seller'],
): EInvoiceDocument['seller'] {
  const party = value && typeof value === 'object'
    ? value as Record<string, unknown>
    : {};
  const storedAddress = party.address && typeof party.address === 'object'
    ? party.address as Record<string, unknown>
    : {};
  const fallbackAddress = fallback.address!;
  return {
    name: text(party.name, fallback.name),
    identifier: optionalText(party.identifier),
    taxIdentifier: optionalText(party.taxIdentifier),
    endpointIdentifier: optionalText(party.endpointIdentifier),
    address: {
      street: text(storedAddress.street, fallbackAddress.street),
      city: text(storedAddress.city, fallbackAddress.city),
      postalCode: text(storedAddress.postalCode, fallbackAddress.postalCode),
      countryCode: text(storedAddress.countryCode, fallbackAddress.countryCode),
      additionalLine: optionalText(storedAddress.additionalLine),
    },
  };
}

function normalizeLine(value: unknown, position: number): EInvoiceDocument['lines'][number] {
  const fallback = createEmptyLine(position);
  const line = value && typeof value === 'object'
    ? value as Record<string, unknown>
    : {};
  return {
    identifier: text(line.identifier, fallback.identifier),
    description: text(line.description, fallback.description),
    quantity: finiteNumber(line.quantity, fallback.quantity),
    unitCode: text(line.unitCode, fallback.unitCode),
    unitPrice: finiteNumber(line.unitPrice, fallback.unitPrice),
    taxRate: finiteNumber(line.taxRate, fallback.taxRate),
    taxCategoryCode: text(line.taxCategoryCode, fallback.taxCategoryCode),
  };
}

export function writeEInvoiceDocument(document: EInvoiceDesignerDocument): TemplateEInvoiceState {
  return {
    invoice: document.invoice as unknown as Record<string, unknown>,
    settings: { ...document.settings },
    templateId: document.templateId,
    lineIds: [...document.lineIds],
    schemaVersion: 1,
  };
}

/** A fresh invoice for a document that does not have one yet. */
export function createEInvoiceDesignerDocument(
  standard: EInvoiceStandard = 'FacturX',
): EInvoiceDesignerDocument {
  return {
    invoice: createEmptyInvoice(),
    settings: defaultSettings(standard),
    lineIds: ['line-1'],
  };
}

/** Keeps version and profile valid when the standard changes. */
export function withStandardSelected(
  document: EInvoiceDesignerDocument,
  standard: EInvoiceStandard,
): EInvoiceDesignerDocument {
  return { ...document, settings: withStandard(document.settings, standard) };
}

/**
 * Retained for source compatibility with fixture builders. Projection IDs are derived from template
 * element IDs, the line index and the page number, so no mutable counter needs resetting.
 */
export function resetProjectionIds(): void {
  // Intentionally empty.
}

/** A projected element plus the page it belongs to. */
interface PlacedElement {
  readonly pageIndex: number;
  readonly element: SimpleElement;
}

/**
 * Projects the invoice onto the pages the Designer shows.
 *
 * Static labels resolve through `translate`, so one template definition serves all six Designer
 * languages instead of needing a translated copy per language. Bound elements take their value from
 * `createBindingContext` - the same exact-decimal arithmetic the generator uses - and keep their
 * `binding` path so the projection stays traceable back to the canonical invoice.
 *
 * Persisted Designer line IDs are used when supplied by the workspace. Fixture and compatibility
 * callers that only have a canonical invoice fall back to deterministic positional IDs.
 */
export function projectInvoicePages(
  definition: InvoiceTemplateDefinition,
  invoice: EInvoiceDocument,
  translate: TemplateTranslator,
  lineIds?: readonly string[],
): Page[] {
  const { layout } = definition;
  const context = createBindingContext(invoice);
  const byId = new Map(definition.elements.map(element => [element.id, element]));
  const wrapping = byId.get(layout.wrappingElementId);

  const plan = planInvoicePages({
    layout,
    wrappingWidth: wrapping?.width ?? 200,
    wrappingFontSize: Number((wrapping?.style as Record<string, unknown> | undefined)?.fontSize) || 9,
    rowTexts: context.lines.map(line => line.description),
    taxGroupCount: layout.taxRowIds.length > 0 ? context.totals.taxes.length : 0,
  });

  const placed: PlacedElement[] = [];
  const emit = (
    pageIndex: number,
    templateId: string,
    top: number,
    scope: unknown,
    suffix: string,
    height?: number,
  ): void => {
    const template = byId.get(templateId);
    if (!template) return;
    placed.push({
      pageIndex,
      element: resolveElement(template, translate, scope, top, suffix, height),
    });
  };

  plan.pages.forEach((page, pageIndex) => {
    // The address block belongs to page one; a continuation page starts at its table.
    if (page.isFirst) {
      for (const id of layout.headerIds) emit(pageIndex, id, byId.get(id)!.y, context, 'p1');
    }

    // Column headings repeat above every table that actually carries rows.
    if (page.tableTop !== null && page.rows.length > 0) {
      const shift = page.tableTop - layout.tableTop;
      for (const id of layout.columnHeaderIds) {
        emit(pageIndex, id, byId.get(id)!.y + shift, context, `p${page.pageNumber}`);
      }
    }

    for (const row of page.rows) {
      const line = context.lines[row.lineIndex];
      for (const id of layout.lineRowIds) {
        const template = byId.get(id)!;
        // The wrapping cell grows within the maintained projection limit, and the next row starts
        // below that box. Maximum-length overflow remains part of the hybrid-layout acceptance gap.
        const height = id === layout.wrappingElementId
          ? Math.max(template.height, row.textLines * layout.lineHeight)
          : undefined;
        const lineId = lineIds?.[row.lineIndex]?.replace(/^line-/, '') ?? String(row.lineIndex + 1);
        emit(pageIndex, id, row.top, line, `l${lineId}`, height);
      }
    }

    if (page.totalsTop !== null) {
      const shift = page.totalsTop - layout.totalsAnchor;
      if (page.showNetTotal) {
        emit(pageIndex, 'netTotalLabel', byId.get('netTotalLabel')!.y + shift, context, 't');
        emit(pageIndex, 'netTotal', byId.get('netTotal')!.y + shift, context, 't');
      }

      const taxes = context.totals.taxes.slice(
        page.taxGroupStart,
        page.taxGroupStart + page.taxGroupCount,
      );
      const taxBase = page.showNetTotal ? page.totalsTop + 18 : page.totalsTop;
      taxes.forEach((tax, localIndex) => {
        const globalIndex = page.taxGroupStart + localIndex;
        for (const id of layout.taxRowIds) {
          const template = byId.get(id)!;
          const relativeY = template.y - (layout.totalsAnchor + 18);
          emit(
            pageIndex,
            id,
            taxBase + localIndex * layout.taxRowHeight + relativeY,
            tax,
            `g${globalIndex + 1}`,
          );
        }
      });

      if (page.showPayableTotal) {
        const ruleTop = taxBase + Math.max(taxes.length, 1) * layout.taxRowHeight;
        const payableTop = ruleTop + 10;
        emit(pageIndex, 'totalsRule', ruleTop, context, 't');
        emit(pageIndex, 'payableLabel', payableTop, context, 't');
        emit(pageIndex, 'payableAmount', payableTop, context, 't');
        emit(pageIndex, 'payableCurrency', payableTop, context, 't');
      }
    }

    for (const id of layout.footerIds) {
      emit(pageIndex, id, byId.get(id)!.y, context, `p${page.pageNumber}`);
    }
  });

  return plan.pages.map((page, pageIndex) => ({
    id: `page-einv-${definition.id}-${page.pageNumber}`,
    elements: placed
      .filter(item => item.pageIndex === pageIndex)
      .map(item => item.element),
  }));
}

/**
 * Every projected element, flattened across pages. Kept because callers that only need the element
 * set - fixture builders and binding assertions - should not have to walk the page structure.
 */
export function projectInvoiceElements(
  definition: InvoiceTemplateDefinition,
  invoice: EInvoiceDocument,
  translate: TemplateTranslator,
  lineIds?: readonly string[],
): SimpleElement[] {
  return projectInvoicePages(definition, invoice, translate, lineIds)
    .flatMap(page => page.elements);
}

/**
 * Resolves one template element against a value scope. `scope` is the whole binding context for a
 * page-level element and one row for a repeated element, which is what lets a row template bind to
 * `line.netAmount` rather than to an index.
 */
function resolveElement(
  element: SimpleElement,
  translate: TemplateTranslator,
  scope: unknown,
  top: number,
  suffix: string,
  height?: number,
): SimpleElement {
  const resolved: SimpleElement = {
    ...element,
    id: `einv-${element.id}-${suffix}`,
    y: top,
    ...(height !== undefined ? { height } : {}),
    style: element.style ? { ...element.style } : undefined,
  };

  const labelKey = LABEL_PATTERN.exec(element.content ?? '')?.[1];
  if (labelKey) {
    resolved.content = translate(labelKey);
    return resolved;
  }

  if (element.binding) {
    const value = readBinding(scope, element.binding);
    resolved.content = value ?? '';
  }

  return resolved;
}

/**
 * Reads a canonical binding path out of a value scope. Amounts resolve to the `…Text`
 * companion the binding context publishes, so the Designer shows the same fixed-point digits
 * the backend writes into the XML and the PDF rather than a re-formatted number.
 */
function readBinding(scope: unknown, binding: string): string | undefined {
  const text = lookup(scope, `${binding}Text`);
  if (typeof text === 'string') return text;
  const value = lookup(scope, binding);
  if (value === undefined || value === null || value === '') return undefined;
  return String(value);
}

/**
 * Walks a canonical path. The leading segment (`invoice`, `totals`, `line`, `tax`) is dropped
 * when the scope is already that object, which is how one row template works for both a line
 * and a tax group.
 */
function lookup(scope: unknown, path: string): unknown {
  const parts = path.split('.');
  const direct = walk(scope, parts);
  if (direct !== undefined) return direct;
  return walk(scope, parts.slice(1));
}

function walk(scope: unknown, parts: readonly string[]): unknown {
  return parts.reduce<unknown>(
    (current, part) =>
      current && typeof current === 'object'
        ? (current as Record<string, unknown>)[part]
        : undefined,
    scope,
  );
}
