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
  EINVOICE_BINDINGS,
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
import type { InvoiceTemplateDefinition } from './invoiceTemplates';

export interface EInvoiceDesignerDocument {
  invoice: EInvoiceDocument;
  settings: EInvoiceSettings;
  templateId?: string;
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
      lines: Array.isArray(stored.lines)
        ? stored.lines.map((line, index) => normalizeLine(line, index + 1))
        : empty.lines,
      currencyDecimalPlaces: Number.isInteger(stored.currencyDecimalPlaces)
        && stored.currencyDecimalPlaces! >= 0
        && stored.currencyDecimalPlaces! <= 4
        ? stored.currencyDecimalPlaces!
        : empty.currencyDecimalPlaces,
    },
    settings,
    templateId: state.templateId,
  };
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
    schemaVersion: 1,
  };
}

/** A fresh invoice for a document that does not have one yet. */
export function createEInvoiceDesignerDocument(
  standard: EInvoiceStandard = 'FacturX',
): EInvoiceDesignerDocument {
  return { invoice: createEmptyInvoice(), settings: defaultSettings(standard) };
}

/** Keeps version and profile valid when the standard changes. */
export function withStandardSelected(
  document: EInvoiceDesignerDocument,
  standard: EInvoiceStandard,
): EInvoiceDesignerDocument {
  return { ...document, settings: withStandard(document.settings, standard) };
}

/**
 * Retained for source compatibility with fixture builders. Projection IDs are now derived from
 * template element IDs and repeat indexes, so no mutable counter needs resetting.
 */
export function resetProjectionIds(): void {
  // Intentionally empty.
}

/**
 * Projects the invoice onto concrete page elements.
 *
 * Static labels resolve through `translate`, so one template definition serves all six
 * Designer languages instead of needing a translated copy per language. Bound elements get
 * their value from `createBindingContext`, which is the same exact-decimal arithmetic the
 * generator uses, and keep their `binding` path so the projection stays traceable.
 */
export function projectInvoiceElements(
  definition: InvoiceTemplateDefinition,
  invoice: EInvoiceDocument,
  translate: TemplateTranslator,
): SimpleElement[] {
  const context = createBindingContext(invoice);
  const projected: SimpleElement[] = [];
  const rowTemplates = new Map<string, SimpleElement[]>();
  const repeatHosts = definition.elements.filter(element => element.repeat);

  // Elements that sit inside a repeat host's row band belong to that row, not to the page.
  for (const host of repeatHosts) {
    rowTemplates.set(
      host.id,
      definition.elements.filter(element =>
        element.id !== host.id &&
        !element.repeat &&
        element.y >= host.y &&
        element.y < host.y + host.height),
    );
  }
  const consumed = new Set(
    [...rowTemplates.values()].flat().map(element => element.id),
  );

  for (const element of definition.elements) {
    if (consumed.has(element.id)) continue;

    if (element.repeat) {
      const rows = element.repeat.dataPath === EINVOICE_BINDINGS.lines
        ? context.lines
        : context.totals.taxes;
      const band = rowTemplates.get(element.id) ?? [];
      rows.forEach((row, index) => {
        const offsetY = element.y + index * element.height;
        for (const cell of band) {
          projected.push(resolveElement(cell, translate, row, offsetY - element.y, index));
        }
      });
      continue;
    }

    projected.push(resolveElement(element, translate, context, 0, 0));
  }

  return projected;
}

/**
 * Resolves one template element against a value scope. `scope` is the whole binding context
 * for a page-level element and one row for a repeated element, which is what lets a row
 * template bind to `line.netAmount` rather than to an index.
 */
function resolveElement(
  element: SimpleElement,
  translate: TemplateTranslator,
  scope: unknown,
  offsetY: number,
  occurrence: number,
): SimpleElement {
  const resolved: SimpleElement = {
    ...element,
    id: `einv-${element.id}-${occurrence + 1}`,
    y: element.y + offsetY,
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

/** Builds the pages a maintained invoice template projects onto, with fresh ids. */
export function projectInvoicePages(
  definition: InvoiceTemplateDefinition,
  invoice: EInvoiceDocument,
  translate: TemplateTranslator,
): Page[] {
  return [{
    id: `page-einv-${definition.id}`,
    elements: projectInvoiceElements(definition, invoice, translate),
  }];
}
