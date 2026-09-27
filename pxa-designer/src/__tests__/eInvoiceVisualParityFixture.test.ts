/**
 * @jest-environment node
 */
/**
 * Generates and drift-guards the electronic-invoice visual-parity fixture.
 *
 * `test-assets/visual-parity/e-invoice.json` has to be a checked-in file, because the C# artifact
 * test and `tools/visual-parity/run.mjs` both read it. It also has to be provably the projection
 * of one canonical invoice, or comparing the Canvas against the backend PDF would only prove that
 * two renderers agree about an arbitrary page. This test builds it from the invoice below and
 * rewrites the file when it drifts, so the two can never diverge silently.
 */

import fs from 'node:fs';
import path from 'node:path';
import {
  projectInvoiceElements,
  resetProjectionIds,
} from '../features/e-invoice/designerDocument';
import { invoiceTemplate } from '../features/e-invoice/invoiceTemplates';
import { createEmptyInvoice, type EInvoiceDocument } from '../features/e-invoice/invoiceModel';
import { calculateInvoice } from '../features/e-invoice/invoiceTotals';
import { toFixed } from '../features/e-invoice/exactDecimal';

const FIXTURE_PATH = path.join(
  process.cwd(), '..', 'test-assets', 'visual-parity', 'e-invoice.json',
);

/** The English labels the projection resolves, so the fixture is language-stable. */
const EN_LABELS: Record<string, string> = JSON.parse(
  fs.readFileSync(path.join(process.cwd(), 'src', 'locales', 'en', 'eInvoice.json'), 'utf8'),
).template;

const translate = (key: string): string => {
  const label = EN_LABELS[key.replace(/^template\./, '')];
  if (!label) throw new Error(`The fixture needs a translation for ${key}`);
  return label;
};

/**
 * The invoice the fixture renders. It deliberately covers what the parity comparison has to
 * survive: two tax rates, a two-decimal currency, a description long enough to wrap, German and
 * French diacritics, and a right-to-left Arabic line.
 */
function parityInvoice(): EInvoiceDocument {
  return {
    ...createEmptyInvoice('2026-09-27'),
    invoiceNumber: 'PARITY-EINV-2026-0042',
    issueDate: '2026-09-27',
    dueDate: '2026-10-27',
    currency: 'EUR',
    currencyDecimalPlaces: 2,
    buyerReference: '04011000-12345-34',
    paymentReference: 'RF18 5390 0754 7034',
    seller: {
      name: 'Groß & Söhne Bürotechnik GmbH',
      taxIdentifier: 'DE123456789',
      address: {
        street: 'Hauptstraße 118a',
        city: 'Berlin',
        postalCode: '10115',
        countryCode: 'DE',
      },
    },
    buyer: {
      name: 'Société Générale de Facturation',
      taxIdentifier: 'FR40303265045',
      address: {
        street: "12 Rue de l'Église",
        city: 'Genève',
        postalCode: '75008',
        countryCode: 'FR',
      },
    },
    lines: [
      {
        identifier: '1',
        // Long enough to exercise wrapping in every surface.
        description: 'Consulting engagement covering the migration assessment, the implementation plan and two workshops',
        quantity: 7.25,
        unitCode: 'HUR',
        unitPrice: 93.33,
        taxRate: 19,
        taxCategoryCode: 'S',
      },
      {
        identifier: '2',
        description: 'Handbuch „Elektronische Rechnungsstellung“ (gebundene Ausgabe)',
        quantity: 3,
        unitCode: 'C62',
        unitPrice: 24.9,
        taxRate: 7,
        taxCategoryCode: 'S',
      },
      {
        identifier: '3',
        // Right-to-left content inside an otherwise left-to-right document.
        description: 'خدمات استشارية للتحول الرقمي',
        quantity: 2,
        unitCode: 'C62',
        unitPrice: 150,
        taxRate: 19,
        taxCategoryCode: 'S',
      },
    ],
  };
}

function buildFixture(): Record<string, unknown> {
  const definition = invoiceTemplate('invoice-standard')!;
  resetProjectionIds();
  const elements = projectInvoiceElements(definition, parityInvoice(), translate);
  return {
    id: 'e-invoice',
    name: 'Electronic invoice parity',
    // The standard and version the projected document represents. PXA makes no conformance
    // claim here; this only records which maintained layout the fixture came from.
    category: 'invoice',
    description: 'Factur-X 1.09.2 EN 16931 hybrid invoice projected from the canonical PXA invoice model.',
    pageSettings: {
      width: definition.pageWidth,
      height: definition.pageHeight,
      orientation: 'portrait',
      unit: 'pt',
      backgroundColor: '#ffffff',
      backgroundImage: '',
      backgroundImageFit: 'contain',
      margins: { top: 24, right: 24, bottom: 24, left: 24 },
      systemLanguage: 'en',
      activeLanguages: ['en', 'de', 'fr', 'ar'],
      targetLanguage: 'en',
    },
    sharedElements: [],
    pages: [{ id: 'e-invoice-page-1', elements }],
  };
}

describe('electronic invoice visual parity fixture', () => {
  it('matches the projection of the canonical invoice', () => {
    const expected = `${JSON.stringify(buildFixture(), null, 2)}\n`;
    const actual = fs.existsSync(FIXTURE_PATH)
      ? fs.readFileSync(FIXTURE_PATH, 'utf8')
      : '';

    if (actual !== expected) {
      fs.mkdirSync(path.dirname(FIXTURE_PATH), { recursive: true });
      fs.writeFileSync(FIXTURE_PATH, expected);
      throw new Error(
        `${FIXTURE_PATH} was out of date and has been regenerated. Review the diff and commit it.`,
      );
    }
  });

  it('covers everything the parity comparison has to survive', () => {
    const fixture = buildFixture();
    const elements = (fixture.pages as Array<{ elements: Array<Record<string, unknown>> }>)[0]
      .elements;
    const contents = elements.map(element => String(element.content ?? ''));
    const invoice = parityInvoice();
    const totals = calculateInvoice(invoice);

    // Seller and buyer.
    expect(contents).toContain('Groß & Söhne Bürotechnik GmbH');
    expect(contents).toContain('Société Générale de Facturation');
    // Three lines, two tax rates.
    expect(totals.lines).toHaveLength(3);
    expect(totals.taxes.map(tax => Number(tax.rate.units) / 10 ** tax.rate.scale))
      .toEqual([7, 19]);
    // A two-decimal currency, rendered with both digits everywhere.
    expect(toFixed(totals.grandTotal, 2)).toBe(contents.find(value => /^\d+\.\d{2}$/.test(value)
      && value === toFixed(totals.grandTotal, 2)));
    // Long text that has to wrap.
    expect(contents.some(value => value.length > 80)).toBe(true);
    // Localized and right-to-left content.
    expect(contents).toContain('Handbuch „Elektronische Rechnungsstellung“ (gebundene Ausgabe)');
    expect(contents.some(value => /[؀-ۿ]/.test(value))).toBe(true);
    // The Factur-X identity of the layout is recorded.
    expect(String(fixture.description)).toContain('Factur-X 1.09.2');
  });

  it('is deterministic, so a parity run compares like with like', () => {
    expect(JSON.stringify(buildFixture())).toBe(JSON.stringify(buildFixture()));
  });

  it('holds no element type the backend mapper cannot render', () => {
    const mapper = fs.readFileSync(
      path.join(process.cwd(), '..', 'PXA.WebApi', 'Infrastructure', 'DesignJsonMapper.cs'),
      'utf8',
    );
    const supported = new Set(
      [...mapper.matchAll(/case "([a-z]+)":/g)].map(match => match[1]),
    );
    const fixture = buildFixture();
    const types = new Set(
      (fixture.pages as Array<{ elements: Array<{ type: string }> }>)[0].elements
        .map(element => element.type),
    );

    for (const type of types) {
      expect(supported.has(type)).toBe(true);
    }
  });
});
