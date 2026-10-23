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
  projectInvoicePages,
  resetProjectionIds,
} from '../features/e-invoice/designerDocument';
import { invoiceTemplate } from '../features/e-invoice/invoiceTemplates';
import { createEmptyInvoice, type EInvoiceDocument } from '../features/e-invoice/invoiceModel';
import { calculateInvoice } from '../features/e-invoice/invoiceTotals';
import { toFixed } from '../features/e-invoice/exactDecimal';

const FIXTURE_PATH = path.join(
  process.cwd(), '..', 'test-assets', 'visual-parity', 'e-invoice.json',
);
const MULTIPAGE_FIXTURE_PATH = path.join(
  process.cwd(), '..', 'test-assets', 'visual-parity', 'e-invoice-multipage.json',
);
const LONG_DESCRIPTION_FIXTURE_PATH = path.join(
  process.cwd(), '..', 'test-assets', 'visual-parity', 'e-invoice-long-description.json',
);
const MULTI_TAX_FIXTURE_PATH = path.join(
  process.cwd(), '..', 'test-assets', 'visual-parity', 'e-invoice-multi-tax.json',
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
 * survive: two tax rates, a two-decimal currency, a description long enough to wrap, and German,
 * French and Central European diacritics. See the note on line three for why no right-to-left or
 * CJK content appears here.
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
        // Latin Extended-A, which Liberation Sans covers, so the comparison stays about layout.
        //
        // Deliberately not Arabic, Hebrew or CJK. Those need the Noto faces from `fonts/README.md`,
        // which this repository does not carry, and the PDF engine does not implement cursive
        // shaping, so an Arabic run is drawn as unjoined glyphs in visual order while a browser
        // shapes and joins it. Pixel parity is unreachable there by design, and a fixture that
        // included it would report a small anti-aliasing-shaped difference that actually hid a
        // row of missing-glyph boxes. Right-to-left content is covered functionally instead, in
        // `eInvoiceWorkspace.test.tsx` and the canonical-invoice tests.
        description: 'Roční údržba a školení — Přeprava zboží (Łódź)',
        quantity: 2,
        unitCode: 'C62',
        unitPrice: 150,
        taxRate: 19,
        taxCategoryCode: 'S',
      },
    ],
  };
}

/**
 * An invoice with more lines than one page holds, so the fixture carries real continuation pages.
 *
 * Everything else stays as plain as possible: the point of this fixture is the page break, the
 * repeated column headings and the totals block landing after the final line, not the content.
 */
function multiPageInvoice(): EInvoiceDocument {
  const rates = [7, 19, 21];
  return {
    ...parityInvoice(),
    invoiceNumber: 'PARITY-EINV-2026-0043',
    lines: Array.from({ length: 30 }, (_, index) => ({
      identifier: String(index + 1),
      description: `Position ${index + 1} - maintenance, support and licence renewal`,
      quantity: [1, 2, 3, 0.5, 7.25][index % 5],
      unitCode: 'C62',
      unitPrice: Number((10 + index * 1.37).toFixed(2)),
      taxRate: rates[index % rates.length],
      taxCategoryCode: 'S',
    })),
  };
}

/**
 * One description far longer than a row, so the fixture carries continuation fragments.
 *
 * The second line's description needs more space than a whole page has, which is what forces the
 * projection to split it and the hybrid renderer to continue it on the next page.
 */
function longDescriptionInvoice(): EInvoiceDocument {
  const phrase = 'annual maintenance, support and licence renewal for the consolidated reporting '
    + 'platform including the quarterly compliance review';
  return {
    ...parityInvoice(),
    invoiceNumber: 'PARITY-EINV-2026-0044',
    lines: [
      {
        identifier: '1',
        description: 'Implementation workshop',
        quantity: 2,
        unitCode: 'DAY',
        unitPrice: 890,
        taxRate: 19,
        taxCategoryCode: 'S',
      },
      {
        identifier: '2',
        description: Array.from({ length: 24 }, () => phrase).join(' '),
        quantity: 1,
        unitCode: 'C62',
        unitPrice: 4200,
        taxRate: 19,
        taxCategoryCode: 'S',
      },
      {
        identifier: '3',
        description: 'Closing report',
        quantity: 1,
        unitCode: 'C62',
        unitPrice: 150,
        taxRate: 7,
        taxCategoryCode: 'S',
      },
    ],
  };
}

/**
 * Enough distinct tax rates that the breakdown outgrows the totals block and continues onto totals
 * pages of its own, with the net total on the first and the payable total on the last.
 */
function multiTaxInvoice(): EInvoiceDocument {
  const rates = [0, 2.1, 3, 4, 5, 5.5, 6, 7, 8, 9, 10, 12, 13, 13.5, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 27];
  return {
    ...parityInvoice(),
    invoiceNumber: 'PARITY-EINV-2026-0045',
    lines: rates.map((rate, index) => ({
      identifier: String(index + 1),
      description: `Cross-border service at ${rate}%`,
      quantity: 1,
      unitCode: 'C62',
      unitPrice: Number((25 + index * 3.5).toFixed(2)),
      taxRate: rate,
      taxCategoryCode: rate === 0 ? 'Z' : 'S',
    })),
  };
}

/** The shared envelope every fixture uses, so only the pages differ between them. */
function fixtureEnvelope(
  id: string,
  name: string,
  pages: Array<{ id: string; elements: unknown[] }>,
): Record<string, unknown> {
  const definition = invoiceTemplate('invoice-standard')!;
  return {
    id,
    name,
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
    pages,
  };
}

function buildFixture(): Record<string, unknown> {
  const definition = invoiceTemplate('invoice-standard')!;
  resetProjectionIds();
  const elements = projectInvoiceElements(definition, parityInvoice(), translate);
  return fixtureEnvelope('e-invoice', 'Electronic invoice parity', [
    { id: 'e-invoice-page-1', elements },
  ]);
}

function buildMultiPageFixture(): Record<string, unknown> {
  return buildProjectedFixture(
    'e-invoice-multipage', 'Electronic invoice parity (multi-page)', multiPageInvoice());
}

function buildProjectedFixture(
  id: string,
  name: string,
  invoice: EInvoiceDocument,
): Record<string, unknown> {
  const definition = invoiceTemplate('invoice-standard')!;
  resetProjectionIds();
  return fixtureEnvelope(id, name, projectInvoicePages(definition, invoice, translate));
}

/**
 * The canonical invoice a fixture was projected from, written beside it.
 *
 * `VisualParityArtifactTests` reads this to render the dedicated hybrid PDF with
 * `EInvoice.CreatePdf`. Without it the parity run could only compare the Designer against a
 * DesignExport PDF of the projection, which says nothing about the hybrid invoice PXA actually
 * issues.
 */
function sidecarFor(invoice: EInvoiceDocument): Record<string, unknown> {
  return {
    schema: 'pxa.e-invoice.parity-invoice/1',
    standard: 'FacturX',
    profile: 'EN 16931',
    invoice,
  };
}

/** Rewrites `file` when the generated fixture drifted, and reports that it did. */
function assertFixtureCurrent(file: string, fixture: Record<string, unknown>): void {
  const expected = `${JSON.stringify(fixture, null, 2)}\n`;
  const actual = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
  if (actual === expected) return;

  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, expected);
  throw new Error(`${file} was out of date and has been regenerated. Review the diff and commit it.`);
}

describe('electronic invoice visual parity fixture', () => {
  const normalizedText = (value: unknown) => String(value ?? '').replace(/\s+/g, ' ').trim();

  it('matches the projection of the canonical invoice', () => {
    assertFixtureCurrent(FIXTURE_PATH, buildFixture());
  });

  it('keeps a multi-page fixture in step with the projection too', () => {
    assertFixtureCurrent(MULTIPAGE_FIXTURE_PATH, buildMultiPageFixture());
  });

  it('keeps a long-description fixture in step with the projection', () => {
    assertFixtureCurrent(LONG_DESCRIPTION_FIXTURE_PATH, buildProjectedFixture(
      'e-invoice-long-description',
      'Electronic invoice parity (long description)',
      longDescriptionInvoice()));
  });

  it('keeps a multi-tax fixture in step with the projection', () => {
    assertFixtureCurrent(MULTI_TAX_FIXTURE_PATH, buildProjectedFixture(
      'e-invoice-multi-tax', 'Electronic invoice parity (many tax groups)', multiTaxInvoice()));
  });

  it('writes the canonical invoice beside every fixture, for the hybrid renderer', () => {
    const sidecars: Array<[string, EInvoiceDocument]> = [
      [FIXTURE_PATH, parityInvoice()],
      [MULTIPAGE_FIXTURE_PATH, multiPageInvoice()],
      [LONG_DESCRIPTION_FIXTURE_PATH, longDescriptionInvoice()],
      [MULTI_TAX_FIXTURE_PATH, multiTaxInvoice()],
    ];
    // Rewrite every drifted sidecar before reporting, so one run brings them all up to date.
    const regenerated: string[] = [];
    for (const [fixturePath, invoice] of sidecars) {
      const file = fixturePath.replace(/\.json$/, '.invoice.json');
      const expected = `${JSON.stringify(sidecarFor(invoice), null, 2)}\n`;
      if ((fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '') === expected) continue;
      fs.writeFileSync(file, expected);
      regenerated.push(file);
    }
    if (regenerated.length > 0) {
      throw new Error(`Regenerated ${regenerated.join(', ')}. Review the diff and commit it.`);
    }
  });

  it('breaks the multi-page fixture into real continuation pages', () => {
    const fixture = buildMultiPageFixture();
    const pages = fixture.pages as Array<{ id: string; elements: Array<Record<string, unknown>> }>;
    const invoice = multiPageInvoice();

    expect(pages.length).toBeGreaterThan(1);
    expect(new Set(pages.map(page => page.id)).size).toBe(pages.length);

    // Every line, exactly once, in canonical order.
    const descriptions = pages.flatMap(page => page.elements
      .filter(element => String(element.id).startsWith('einv-lineDescription-'))
      .map(element => normalizedText(element.content)));
    expect(descriptions).toEqual(invoice.lines.map(line => line.description));

    // The address block appears once; the column headings repeat on each page that has rows.
    const addressPages = pages.filter(page =>
      page.elements.some(element => element.id === 'einv-sellerName-p1'));
    expect(addressPages).toHaveLength(1);
    for (const page of pages) {
      const rows = page.elements.filter(e => String(e.id).startsWith('einv-lineDescription-l'));
      const headings = page.elements.filter(e =>
        String(e.id).startsWith('einv-lineDescriptionHeading-'));
      expect(headings.length).toBe(rows.length > 0 ? 1 : 0);
    }

    // One page carries the totals, and it is the last one.
    const totalsPages = pages.filter(page =>
      page.elements.some(element => element.id === 'einv-payableAmount-t'));
    expect(totalsPages).toHaveLength(1);
    expect(totalsPages[0]).toBe(pages[pages.length - 1]);

    // Every tax group reached the document.
    const groups = new Set(invoice.lines.map(line => line.taxRate));
    expect(pages.flatMap(page => page.elements
      .filter(element => String(element.id).startsWith('einv-taxAmount-g')))).toHaveLength(groups.size);
  });

  it('covers everything the parity comparison has to survive', () => {
    const fixture = buildFixture();
    const elements = (fixture.pages as Array<{ elements: Array<Record<string, unknown>> }>)[0]
      .elements;
    const contents = elements.map(element => normalizedText(element.content));
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
    // Localized content, in the scripts Liberation Sans actually carries.
    expect(contents).toContain('Handbuch „Elektronische Rechnungsstellung“ (gebundene Ausgabe)');
    expect(contents.some(value => /[Ā-ſ]/.test(value))).toBe(true);
    // Scripts the PDF engine cannot yet reach parity on must stay out of a pixel fixture.
    expect(contents.some(value => /[֐-ࣿ　-鿿]/.test(value))).toBe(false);
    // The Factur-X identity of the layout is recorded.
    expect(String(fixture.description)).toContain('Factur-X 1.09.2');
  });

  it('is deterministic, so a parity run compares like with like', () => {
    expect(JSON.stringify(buildFixture())).toBe(JSON.stringify(buildFixture()));
  });

  it('holds no element type the backend mapper cannot render', () => {
    const mapper = fs.readFileSync(
      path.join(process.cwd(), '..', 'PVAD.WebApi', 'Infrastructure', 'DesignJsonMapper.cs'),
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
