/**
 * @jest-environment node
 */
/**
 * EINV-05: the Designer's typed invoice bindings, maintained templates, localized validation and
 * the canonical values it sends to the backend.
 */

import fs from 'node:fs';
import path from 'node:path';
import {
  EINVOICE_BINDINGS,
  EINVOICE_BINDING_PATHS,
  EINVOICE_PROFILES,
  EINVOICE_VERSIONS,
  attachmentFileName,
  createEmptyInvoice,
  createEmptyLine,
  defaultSettings,
  toGenerationRequest,
  withStandard,
  type EInvoiceDocument,
} from '../features/e-invoice/invoiceModel';
import {
  INVOICE_TEMPLATES,
  invoiceTemplate,
  templateBindings,
  templateLabelKeys,
  templatesForStandard,
} from '../features/e-invoice/invoiceTemplates';
import {
  conformanceStateFor,
  hasBlockingFindings,
  validateInvoice,
  validateSettings,
} from '../features/e-invoice/invoiceValidation';
import { calculateInvoice, createBindingContext } from '../features/e-invoice/invoiceTotals';
import { toFixed } from '../features/e-invoice/exactDecimal';
import { readEInvoiceDocument } from '../features/e-invoice/designerDocument';

const LANGUAGES = ['en', 'de', 'fr', 'es', 'it', 'ar'] as const;
const LOCALES_ROOT = path.join(process.cwd(), 'src', 'locales');

function catalog(language: string): Record<string, unknown> {
  return JSON.parse(
    fs.readFileSync(path.join(LOCALES_ROOT, language, 'eInvoice.json'), 'utf8'),
  ) as Record<string, unknown>;
}

function lookup(source: Record<string, unknown>, key: string): unknown {
  return key.split('.').reduce<unknown>(
    (value, part) =>
      value && typeof value === 'object' ? (value as Record<string, unknown>)[part] : undefined,
    source,
  );
}

function completeInvoice(): EInvoiceDocument {
  return {
    ...createEmptyInvoice('2026-09-27'),
    invoiceNumber: 'DES-1',
    dueDate: '2026-10-27',
    currency: 'EUR',
    buyerReference: '04011000-12345-34',
    seller: {
      name: 'Seller GmbH',
      taxIdentifier: 'DE123456789',
      address: { street: 'Main Street 1', city: 'Berlin', postalCode: '10115', countryCode: 'DE' },
    },
    buyer: {
      name: 'Buyer AG',
      address: { street: 'Buyer Street 2', city: 'Munich', postalCode: '80331', countryCode: 'DE' },
    },
    lines: [
      { identifier: '1', description: 'Consulting', quantity: 2, unitCode: 'HUR', unitPrice: 90, taxRate: 19, taxCategoryCode: 'S' },
      { identifier: '2', description: 'Handbook', quantity: 1, unitCode: 'C62', unitPrice: 40, taxRate: 7, taxCategoryCode: 'S' },
    ],
  };
}

describe('typed invoice bindings', () => {
  it('keeps one canonical path per invoice value', () => {
    expect(new Set(EINVOICE_BINDING_PATHS).size).toBe(EINVOICE_BINDING_PATHS.length);
    expect(EINVOICE_BINDINGS.grandTotal).toBe('totals.grandTotal');
    expect(EINVOICE_BINDINGS.lineNetAmount).toBe('line.netAmount');
  });

  it('keeps version and profile consistent when the standard changes', () => {
    const facturX = defaultSettings('FacturX');
    expect(facturX.version).toBe(EINVOICE_VERSIONS.FacturX);
    expect(EINVOICE_PROFILES.FacturX).toContain(facturX.profile);

    const xrechnung = withStandard(facturX, 'XRechnung');
    expect(xrechnung.version).toBe(EINVOICE_VERSIONS.XRechnung);
    expect(xrechnung.profile).toBe('XRECHNUNG');

    // A profile only the previous standard had must not survive the switch.
    const extended = withStandard({ ...facturX, profile: 'EXTENDED' }, 'XRechnung');
    expect(extended.profile).toBe('XRECHNUNG');
    // A shared profile does survive it.
    expect(withStandard({ ...facturX, profile: 'BASIC' }, 'ZUGFeRD').profile).toBe('BASIC');
  });

  it('uses the filename each standard reserves', () => {
    expect(attachmentFileName('FacturX')).toBe('factur-x.xml');
    expect(attachmentFileName('ZUGFeRD')).toBe('factur-x.xml');
    expect(attachmentFileName('XRechnung')).toBe('xrechnung.xml');
  });

  it('sends the invoice unchanged, with no recomputed amounts in the request', () => {
    const invoice = completeInvoice();
    const request = toGenerationRequest(invoice, defaultSettings('FacturX'));

    expect(request.invoice).toBe(invoice);
    expect(request.standard).toBe('FacturX');
    expect(request.version).toBe('1.09.2');
    // Totals are the backend's to compute; the request carries only the source data.
    expect(Object.keys(request)).toEqual(['standard', 'version', 'profile', 'invoice']);
    expect(JSON.parse(JSON.stringify(request)).invoice).not.toHaveProperty('totals');
  });

  it('round-trips through JSON without changing a single value', () => {
    const invoice = completeInvoice();
    const restored: EInvoiceDocument = JSON.parse(JSON.stringify(invoice));

    expect(restored).toEqual(invoice);
    expect(toFixed(calculateInvoice(restored).grandTotal, 2)).toBe(
      toFixed(calculateInvoice(invoice).grandTotal, 2),
    );
  });

  it('creates line identifiers that do not collide', () => {
    const lines = [1, 2, 3].map(createEmptyLine);
    expect(new Set(lines.map(line => line.identifier)).size).toBe(3);
  });

  it('repairs malformed persisted values before arithmetic or rendering', () => {
    const restored = readEInvoiceDocument({
      schemaVersion: 1,
      settings: { standard: 'FacturX', version: 'old', profile: 'missing' },
      invoice: {
        invoiceNumber: 42,
        issueDate: 20260927,
        currency: null,
        currencyDecimalPlaces: 999,
        seller: { name: ['invalid'], address: null },
        buyer: null,
        lines: [{ identifier: 1, quantity: 'many', unitPrice: null, taxRate: 19 }],
      },
    });

    expect(restored).not.toBeNull();
    expect(restored!.settings.version).toBe(EINVOICE_VERSIONS.FacturX);
    expect(restored!.settings.profile).toBe(defaultSettings('FacturX').profile);
    expect(restored!.invoice.currencyDecimalPlaces).toBe(2);
    expect(restored!.invoice.issueDate).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(restored!.invoice.lines[0]).toEqual({ ...createEmptyLine(1), taxRate: 19 });
    expect(() => calculateInvoice(restored!.invoice)).not.toThrow();
  });
});

describe('maintained invoice templates', () => {
  it('binds every dynamic value to a canonical path', () => {
    for (const template of INVOICE_TEMPLATES) {
      for (const binding of templateBindings(template)) {
        expect(EINVOICE_BINDING_PATHS).toContain(binding);
      }
    }
  });

  it('holds no literal invoice value', () => {
    for (const template of INVOICE_TEMPLATES) {
      for (const element of template.elements) {
        if (element.binding) {
          // A bound element must not also carry static content.
          expect(element.content).toBeUndefined();
        }
        if (element.content) {
          // Static content is always an i18n key, never a translated string or an amount.
          expect(element.content).toMatch(/^\{\{t:[a-zA-Z.]+\}\}$/);
        }
      }
    }
  });

  it('translates every template label in all six languages', () => {
    const catalogs = LANGUAGES.map(language => [language, catalog(language)] as const);
    for (const template of INVOICE_TEMPLATES) {
      for (const key of templateLabelKeys(template)) {
        for (const [language, source] of catalogs) {
          const value = lookup(source, key);
          expect(typeof value).toBe('string');
          expect(String(value).trim()).not.toBe('');
          // A missing translation that silently falls back to English is still a gap.
          if (language !== 'en') {
            expect(value).not.toBeUndefined();
          }
        }
      }
    }
  });

  it('offers a maintained layout for every supported standard', () => {
    expect(templatesForStandard('FacturX').length).toBeGreaterThan(0);
    expect(templatesForStandard('ZUGFeRD').length).toBeGreaterThan(0);
    expect(templatesForStandard('XRechnung').length).toBeGreaterThan(0);
    expect(invoiceTemplate('invoice-standard')?.standards).toEqual([
      'FacturX',
      'ZUGFeRD',
      'XRechnung',
    ]);
    expect(invoiceTemplate('missing')).toBeUndefined();
  });

  it('repeats lines and tax groups from the canonical collections', () => {
    const template = invoiceTemplate('invoice-standard')!;
    const repeats = template.elements
      .filter(element => element.repeat)
      .map(element => element.repeat!.dataPath);

    expect(repeats).toContain(EINVOICE_BINDINGS.lines);
    expect(repeats).toContain(EINVOICE_BINDINGS.taxGroups);
  });

  it('drops the per-rate breakdown in the compact layout only', () => {
    expect(templateBindings(invoiceTemplate('invoice-standard')!)).toContain(
      EINVOICE_BINDINGS.taxGroupAmount,
    );
    expect(templateBindings(invoiceTemplate('invoice-compact')!)).not.toContain(
      EINVOICE_BINDINGS.taxGroupAmount,
    );
  });
});

describe('localized invoice validation', () => {
  it('accepts a complete invoice', () => {
    const findings = validateInvoice(completeInvoice(), defaultSettings('FacturX'));
    expect(findings.filter(finding => finding.severity === 'error')).toEqual([]);
    expect(hasBlockingFindings(findings)).toBe(false);
  });

  it('reports the backend diagnostic code for each rule', () => {
    const invoice: EInvoiceDocument = {
      ...completeInvoice(),
      invoiceNumber: '',
      currency: 'EURO',
      currencyDecimalPlaces: 9,
      dueDate: '2026-01-01',
      lines: [
        { identifier: '1', description: '', quantity: 0, unitCode: '', unitPrice: -5, taxRate: 120, taxCategoryCode: '' },
        { identifier: '1', description: 'Duplicate position', quantity: 1, unitCode: 'C62', unitPrice: 1, taxRate: 19, taxCategoryCode: 'S' },
      ],
    };

    const codes = validateInvoice(invoice, defaultSettings('FacturX')).map(finding => finding.code);

    expect(codes).toContain('PXAEINV100');
    expect(codes).toContain('PXAEINV102');
    expect(codes).toContain('PXAEINV103');
    expect(codes).toContain('PXAEINV109');
    expect(codes).toContain('PXAEINV110');
    expect(codes).toContain('PXAEINV111');
    expect(codes).toContain('PXAEINV112');
    expect(codes).toContain('PXAEINV113');
    expect(codes).toContain('PXAEINV114');
    expect(codes).toContain('PXAEINV115');
    expect(codes).toContain('PXAEINV116');
  });

  it('points every finding at a focusable field', () => {
    const invoice: EInvoiceDocument = {
      ...completeInvoice(),
      seller: { name: '' },
      lines: [{ identifier: '', description: '', quantity: 1, unitCode: 'C62', unitPrice: 1, taxRate: 0, taxCategoryCode: 'S' }],
    };

    const findings = validateInvoice(invoice, defaultSettings('FacturX'));

    expect(findings.map(finding => finding.field)).toContain('seller.name');
    expect(findings.map(finding => finding.field)).toContain('lines[0].identifier');
  });

  it('warns rather than blocks when XRechnung has no buyer reference', () => {
    const findings = validateInvoice(
      { ...completeInvoice(), buyerReference: undefined },
      defaultSettings('XRechnung'),
    );

    const finding = findings.find(item => item.code === 'PXAEINV117');
    expect(finding?.severity).toBe('warning');
    expect(hasBlockingFindings(findings)).toBe(false);
  });

  it('refuses an unsupported version or profile before any request is sent', () => {
    expect(
      validateSettings({ standard: 'FacturX', version: '1.0', profile: 'EN 16931' }).map(f => f.code),
    ).toContain('PXAEINV012');
    expect(
      validateSettings({ standard: 'XRechnung', version: '3.0.2', profile: 'EXTENDED' }).map(f => f.code),
    ).toContain('PXAEINV013');
    expect(validateSettings(defaultSettings('ZUGFeRD'))).toEqual([]);
  });

  it('translates every validation message in all six languages', () => {
    const invoice: EInvoiceDocument = {
      ...createEmptyInvoice('2026-09-27'),
      currency: 'EURO',
      currencyDecimalPlaces: 9,
      dueDate: '2020-01-01',
      seller: { name: '', address: { street: '', city: '', postalCode: '', countryCode: 'DEU' } },
      buyer: { name: '', address: { street: '', city: '', postalCode: '', countryCode: '' } },
      lines: [],
    };
    const keys = new Set(
      [
        ...validateInvoice(invoice, defaultSettings('XRechnung')),
        ...validateInvoice(completeInvoice(), { standard: 'FacturX', version: 'x', profile: 'nope' }),
        ...validateInvoice(
          { ...completeInvoice(), lines: Array.from({ length: 10_001 }, (_, index) => createEmptyLine(index + 1)) },
          defaultSettings('FacturX'),
        ),
      ].map(finding => finding.messageKey),
    );

    expect(keys.size).toBeGreaterThan(10);
    for (const language of LANGUAGES) {
      const source = catalog(language);
      for (const key of keys) {
        expect(typeof lookup(source, key)).toBe('string');
      }
    }
  });

  it('never presents structural recognition as conformance', () => {
    const clean = validateInvoice(completeInvoice(), defaultSettings('FacturX'));
    const broken = validateInvoice({ ...completeInvoice(), invoiceNumber: '' }, defaultSettings('FacturX'));

    expect(conformanceStateFor(broken)).toBe('draft');
    expect(conformanceStateFor(clean)).toBe('structurallyValid');
    expect(conformanceStateFor(clean, 'notEvaluated')).toBe('notEvaluated');
    // The backend's vocabulary is `valid`, not `validated`; only that reaches `validated`.
    expect(conformanceStateFor(clean, 'valid')).toBe('validated');
    expect(conformanceStateFor(clean, 'invalid')).toBe('invalid');
    expect(conformanceStateFor(clean, 'unavailable')).toBe('unavailable');
    // An unknown or misspelled status must never be promoted to a conformance claim.
    expect(conformanceStateFor(clean, 'validated')).toBe('structurallyValid');
    expect(conformanceStateFor(clean, 'conformant')).toBe('structurallyValid');
    // A blocking error outranks any backend verdict the Designer may still be holding.
    expect(conformanceStateFor(broken, 'valid')).toBe('draft');
  });

  it('states the product boundary in every language', () => {
    for (const language of LANGUAGES) {
      const description = String(lookup(catalog(language), 'boundaries.description'));
      expect(description).toMatch(/Peppol/);
      expect(description.length).toBeGreaterThan(40);
    }
  });
});

describe('preview binding context', () => {
  it('shows the amounts the backend will write, to the digit', () => {
    const invoice = completeInvoice();
    const context = createBindingContext(invoice);

    // 2 x 90 = 180.00 at 19%, 1 x 40 = 40.00 at 7%.
    expect(context.lines.map(line => line.netAmountText)).toEqual(['180.00', '40.00']);
    expect(context.totals.taxes.map(tax => [tax.rate, tax.amountText])).toEqual([
      [7, '2.80'],
      [19, '34.20'],
    ]);
    expect(context.totals.lineNetTotalText).toBe('220.00');
    expect(context.totals.taxTotalText).toBe('37.00');
    expect(context.totals.grandTotalText).toBe('257.00');
  });

  it('reflects an edit immediately, because it holds no cached totals', () => {
    const invoice = completeInvoice();
    const before = createBindingContext(invoice).totals.grandTotalText;
    invoice.lines[0].quantity = 3;
    const after = createBindingContext(invoice).totals.grandTotalText;

    expect(before).toBe('257.00');
    expect(after).toBe('364.10');
  });
});
