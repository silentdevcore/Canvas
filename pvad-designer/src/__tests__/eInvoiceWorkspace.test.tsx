/**
 * @jest-environment jsdom
 */
/**
 * EINV-05: the Electronic Invoice workspace in PXA Designer.
 *
 * The page is mounted for real - store, autosave hook, projection and all - so what these tests
 * assert is the behaviour a user gets, not a mock of it. Only the network and the auth/feature
 * context are stubbed.
 */

import { act } from 'react';
import { documentHandoffKeys, setDocumentHandoffOwner } from '@/auth/documentHandoff';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import i18n from '@/i18n';
import EInvoicePage from '@/features/e-invoice/EInvoicePage';
import { DEFAULT_PAGE_SETTINGS, useEditorStore, type Template } from '@/store';
import { designerPermissions } from '@/auth/designerPermissions';
import {
  projectInvoiceElements,
  readEInvoiceDocument,
} from '@/features/e-invoice/designerDocument';
import { invoiceTemplate } from '@/features/e-invoice/invoiceTemplates';
import { EINVOICE_BINDING_PATHS } from '@/features/e-invoice/invoiceModel';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

let currentPermissions: string[] = Object.values(designerPermissions);
let featureEnabled = true;
const navigateMock = jest.fn();

jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useNavigate: () => navigateMock,
}));

jest.mock('@/auth/DesignerAuthGate', () => ({
  useDesignerAuth: () => ({
    user: {
      id: 'user-1',
      username: 'designer@example.test',
      email: 'designer@example.test',
      displayName: 'Designer User',
      roles: ['Editor'],
      permissions: currentPermissions,
      organizations: [{ id: 'org-1', name: 'Test Organization', slug: 'test' }],
      activeOrganizationId: 'org-1',
      lastLoginAt: null,
    },
  }),
}));

jest.mock('@/product/ProductExperienceProvider', () => ({
  useProductExperience: () => ({
    features: [{ id: 'designer.e-invoice', enabled: featureEnabled, maturity: 'beta' }],
  }),
}));

const updateDraftMock = jest.fn();
const createDraftMock = jest.fn();
const getDraftMock = jest.fn();

jest.mock('@/services/designerTemplateApi', () => {
  const actual = jest.requireActual('@/services/designerTemplateApi');
  return {
    ...actual,
    createDesignerTemplate: (...args: unknown[]) => createDraftMock(...args),
    getDesignerTemplate: (...args: unknown[]) => getDraftMock(...args),
    updateDesignerTemplateDraft: (...args: unknown[]) => updateDraftMock(...args),
  };
});

// ---------------------------------------------------------------- helpers

let container: HTMLDivElement;
let root: Root | null;

function invoiceTemplateDocument(): Template {
  return {
    id: 'template-1',
    name: 'Invoice draft',
    category: 'invoice',
    description: '',
    pages: [{ id: 'page-1', elements: [] }],
    sharedElements: [],
    data: {},
    persistence: { id: 'server-1', revision: 3, status: 'Draft' },
  };
}

async function render(route = '/pdf/e-invoice'): Promise<void> {
  root = createRoot(container);
  await act(async () => {
    root!.render(
      <MemoryRouter initialEntries={[route]}>
        <EInvoicePage />
      </MemoryRouter>,
    );
  });
}

const flush = () => act(async () => {
  await new Promise(resolve => window.setTimeout(resolve, 0));
});

function query<T extends Element = HTMLElement>(selector: string): T | null {
  return container.querySelector<T>(selector);
}

function testId<T extends Element = HTMLElement>(id: string): T {
  const element = container.querySelector<T>(`[data-testid="${id}"]`);
  if (!element) throw new Error(`No element with data-testid="${id}"`);
  return element;
}

function click(element: Element | null): Promise<void> {
  if (!element) throw new Error('Cannot click a missing element');
  return act(async () => {
    element.dispatchEvent(new MouseEvent('click', { bubbles: true }));
  });
}

/**
 * Sets a controlled input's value the way a user would.
 *
 * React caches the last value it rendered on the DOM node, so assigning `input.value` directly
 * makes React consider the change a no-op and skip `onChange`. Going through the prototype's
 * native setter updates the node without touching React's cache, which is what makes the
 * subsequent event look like real user input.
 */
function setNativeValue(element: HTMLInputElement | HTMLSelectElement, value: string): void {
  const prototype = element instanceof HTMLSelectElement
    ? HTMLSelectElement.prototype
    : HTMLInputElement.prototype;
  const setter = Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}

function type(selector: string, value: string): Promise<void> {
  const input = query<HTMLInputElement>(selector);
  if (!input) throw new Error(`No input matching ${selector}`);
  return act(async () => {
    setNativeValue(input, value);
    input.dispatchEvent(new Event('input', { bubbles: true }));
  });
}

function select(selector: string, value: string): Promise<void> {
  const element = query<HTMLSelectElement>(selector);
  if (!element) throw new Error(`No select matching ${selector}`);
  return act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event('change', { bubbles: true }));
  });
}

/** Fills the fields the backend requires so an invoice has no blocking findings. */
async function fillValidInvoice(): Promise<void> {
  await click(testId('einvoice-empty').querySelector('button'));
  await type('#einv-invoiceNumber', 'DES-2026-1');
  await type('#einv-seller-name', 'Seller GmbH');
  await type('#einv-seller-address-street', 'Main Street 1');
  await type('#einv-seller-address-postalCode', '10115');
  await type('#einv-seller-address-city', 'Berlin');
  await type('#einv-seller-address-countryCode', 'DE');
  await type('#einv-buyer-name', 'Buyer AG');
  await type('#einv-buyer-address-street', 'Buyer Street 2');
  await type('#einv-buyer-address-postalCode', '80331');
  await type('#einv-buyer-address-city', 'Munich');
  await type('#einv-buyer-address-countryCode', 'DE');
  await type('#einv-line-0-description', 'Consulting');
  await type('#einv-line-0-quantity', '2');
  await type('#einv-line-0-unitPrice', '90');
  await type('#einv-line-0-taxRate', '19');
}

beforeEach(async () => {
  container = document.createElement('div');
  document.body.appendChild(container);
  root = null;
  currentPermissions = Object.values(designerPermissions);
  featureEnabled = true;
  jest.clearAllMocks();
  jest.useRealTimers();
  localStorage.clear();
  sessionStorage.clear();
  setDocumentHandoffOwner('user-a', 'org-a');
  await i18n.changeLanguage('en');
  useEditorStore.setState({
    currentTemplate: invoiceTemplateDocument(),
    currentPageIndex: 0,
    selectedElementId: null,
    pageSettings: DEFAULT_PAGE_SETTINGS,
    templates: [],
    undoStack: [],
    redoStack: [],
    jsonData: {},
    documentMode: 'pdf',
  });
});

afterEach(() => {
  if (root) act(() => root!.unmount());
  container.remove();
});

// ---------------------------------------------------------------- reachability and gating

describe('reachability and gating', () => {
  it('renders the workspace and offers to start an invoice', async () => {
    await render();

    expect(testId('einvoice-empty')).toBeTruthy();
    expect(container.textContent).toContain('No invoice in this document yet');
    // The product boundary is stated before anything is generated.
    expect(container.textContent).toContain('Peppol');
  });

  it('shows the unavailable state when the feature gate is off', async () => {
    featureEnabled = false;
    useEditorStore.setState({ currentTemplate: null });

    await render();

    expect(testId('einvoice-unavailable')).toBeTruthy();
    expect(query('[data-testid="einvoice-empty"]')).toBeNull();
    expect(useEditorStore.getState().currentTemplate).toBeNull();
  });

  it('creates a blank document when the workspace is opened directly', async () => {
    useEditorStore.setState({ currentTemplate: null });

    await render();

    expect(testId('einvoice-empty')).toBeTruthy();
    expect(useEditorStore.getState().currentTemplate?.category).toBe('invoice');
    expect(useEditorStore.getState().currentTemplate?.name).toBe('Electronic invoice');
  });

  it('disables generation actions the user has no permission for', async () => {
    currentPermissions = [designerPermissions.pdfEInvoicesRead];

    await render();
    await fillValidInvoice();

    expect(testId<HTMLButtonElement>('einvoice-validate').disabled).toBe(true);
    expect(testId<HTMLButtonElement>('einvoice-generate-xml').disabled).toBe(true);
    expect(testId<HTMLButtonElement>('einvoice-create-pdf').disabled).toBe(true);
  });

  it('enables generation once the matching permissions are present', async () => {
    await render();
    await fillValidInvoice();

    expect(testId<HTMLButtonElement>('einvoice-validate').disabled).toBe(false);
    expect(testId<HTMLButtonElement>('einvoice-generate-xml').disabled).toBe(false);
    expect(testId<HTMLButtonElement>('einvoice-create-pdf').disabled).toBe(false);
  });
});

// ---------------------------------------------------------------- standards

describe('standard, version and profile', () => {
  it('pins the maintained version and filters profiles per standard', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    expect(testId('einvoice-version').textContent).toBe('1.09.2');
    const profileOptions = () => Array.from(
      query<HTMLSelectElement>('#einv-profile')!.options,
    ).map(option => option.value);
    expect(profileOptions()).toContain('EN 16931');

    await select('#einv-standard', 'XRechnung');
    expect(testId('einvoice-version').textContent).toBe('3.0.2');
    expect(profileOptions()).toEqual(['XRECHNUNG']);
    expect(query<HTMLSelectElement>('#einv-profile')!.value).toBe('XRECHNUNG');

    await select('#einv-standard', 'ZUGFeRD');
    expect(testId('einvoice-version').textContent).toBe('2.5.2');
    expect(profileOptions()).toContain('EXTENDED');
  });

  it('never offers the version as an editable field', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    expect(testId('einvoice-version').tagName.toLowerCase()).toBe('output');
    expect(query('input#einv-version')).toBeNull();
  });
});

// ---------------------------------------------------------------- lines and totals

describe('invoice lines', () => {
  it('adds, reorders and removes lines', async () => {
    await render();
    await fillValidInvoice();

    await click(Array.from(container.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Add line')) ?? null);
    await type('#einv-line-1-description', 'Handbook');
    await type('#einv-line-1-unitPrice', '40');
    await type('#einv-line-1-taxRate', '7');

    expect(query<HTMLInputElement>('#einv-line-0-description')!.value).toBe('Consulting');
    expect(query<HTMLInputElement>('#einv-line-1-description')!.value).toBe('Handbook');

    // Reorder: move the second line up.
    await click(container.querySelector('[data-testid="einvoice-line-1"] button[aria-label*="Move line up"]'));
    expect(query<HTMLInputElement>('#einv-line-0-description')!.value).toBe('Handbook');
    expect(query<HTMLInputElement>('#einv-line-1-description')!.value).toBe('Consulting');

    // Remove the first line.
    await click(container.querySelector('[data-testid="einvoice-line-0"] button[aria-label*="Remove line"]'));
    expect(query<HTMLInputElement>('#einv-line-0-description')!.value).toBe('Consulting');
    expect(query('[data-testid="einvoice-line-1"]')).toBeNull();
  });

  it('keeps line identifiers unique after a removal', async () => {
    await render();
    await fillValidInvoice();
    const addLine = () => click(Array.from(container.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Add line')) ?? null);

    await addLine();
    await click(container.querySelector('[data-testid="einvoice-line-0"] button[aria-label*="Remove line"]'));
    await addLine();

    const identifiers = Array.from(
      container.querySelectorAll<HTMLInputElement>('input[id$="-identifier"]'),
    ).map(input => input.value);
    expect(new Set(identifiers).size).toBe(identifiers.length);
  });

  it('computes exact totals and tax groups, and never lets them be edited', async () => {
    await render();
    await fillValidInvoice();
    await click(Array.from(container.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Add line')) ?? null);
    await type('#einv-line-1-description', 'Handbook');
    await type('#einv-line-1-unitPrice', '40');
    await type('#einv-line-1-taxRate', '7');

    // 2 x 90 = 180.00 at 19%, 1 x 40 = 40.00 at 7%.
    expect(testId('einvoice-line-0-net').textContent).toBe('180.00');
    expect(testId('einvoice-line-1-net').textContent).toBe('40.00');
    expect(testId('einvoice-total-net').textContent).toBe('220.00 EUR');
    expect(testId('einvoice-total-tax').textContent).toBe('37.00 EUR');
    expect(testId('einvoice-total-grand').textContent).toBe('257.00 EUR');
    // Ordered by rate: 7% before 19%.
    expect(testId('einvoice-tax-S-7').textContent).toBe('2.80 EUR');
    expect(testId('einvoice-tax-S-19').textContent).toBe('34.20 EUR');

    // Totals are outputs, so there is no input a user could overtype.
    for (const id of ['einvoice-total-net', 'einvoice-total-tax', 'einvoice-total-grand']) {
      expect(testId(id).tagName.toLowerCase()).toBe('output');
    }
    expect(testId('einvoice-line-0-net').tagName.toLowerCase()).toBe('output');
  });

  it('respects the currency decimal places in every derived figure', async () => {
    await render();
    await fillValidInvoice();

    await type('#einv-currencyDecimalPlaces', '0');

    expect(testId('einvoice-line-0-net').textContent).toBe('180');
    expect(testId('einvoice-total-grand').textContent).toBe('214 EUR');
  });
});

// ---------------------------------------------------------------- diagnostics and states

describe('diagnostics and validation states', () => {
  it('reports findings with their backend code and focuses the offending field', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    const diagnostics = testId('einvoice-diagnostics');
    expect(diagnostics.querySelector('[data-code="PXAEINV100"]')).toBeTruthy();
    expect(diagnostics.textContent).toContain('PXAEINV100');

    const invoiceNumberFinding = diagnostics.querySelector('[data-code="PXAEINV100"]');
    await click(invoiceNumberFinding!.querySelector('button'));
    await flush();

    expect(document.activeElement?.id).toBe('einv-invoiceNumber');
  });

  it('links a line finding to that line input', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    const lineFinding = testId('einvoice-diagnostics')
      .querySelector('[data-field="lines[0].description"]');
    await click(lineFinding!.querySelector('button'));
    await flush();

    expect(document.activeElement?.id).toBe('einv-line-0-description');
  });

  it('marks an invalid field with aria-invalid', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    expect(query('#einv-invoiceNumber')!.getAttribute('aria-invalid')).toBe('true');

    await type('#einv-invoiceNumber', 'DES-1');
    expect(query('#einv-invoiceNumber')!.getAttribute('aria-invalid')).toBeNull();
  });

  it('keeps the three validation states separate and claims nothing before validating', async () => {
    await render();
    await fillValidInvoice();

    expect(testId('einvoice-state-structural').textContent).toContain('Not checked yet');
    expect(testId('einvoice-state-business').textContent).toContain('No blocking findings');
    // A clean local check is "recognised", never "conformant".
    expect(testId('einvoice-state-conformance').textContent)
      .toContain('Structurally recognised');
    expect(testId('einvoice-state-conformance').textContent).not.toMatch(/\bvalid\b/i);
    expect(container.textContent).toContain('Structural recognition is not a conformance claim');
  });

  it('blocks generation while a blocking finding remains', async () => {
    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    expect(testId<HTMLButtonElement>('einvoice-create-pdf').disabled).toBe(true);
  });
});

// ---------------------------------------------------------------- templates and undo

describe('template insertion', () => {
  it('projects the invoice onto canonical bindings in one undoable step', async () => {
    await render();
    await fillValidInvoice();
    const undoDepthBefore = useEditorStore.getState().undoStack.length;

    await click(testId('einvoice-insert-invoice-standard'));

    const template = useEditorStore.getState().currentTemplate!;
    expect(template.eInvoice?.templateId).toBe('invoice-standard');
    expect(template.pages[0].elements.length).toBeGreaterThan(10);
    // Exactly one history entry, so one Ctrl+Z reverses the whole insertion.
    expect(useEditorStore.getState().undoStack.length).toBe(undoDepthBefore + 1);

    act(() => useEditorStore.getState().undo());
    expect(useEditorStore.getState().currentTemplate!.eInvoice?.templateId).toBeUndefined();
    expect(useEditorStore.getState().currentTemplate!.pages[0].elements).toHaveLength(0);
  });

  it('gives every projected element a fresh id and keeps its binding path', async () => {
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-insert-invoice-standard'));

    const elements = useEditorStore.getState().currentTemplate!.pages[0].elements;
    const ids = elements.map(element => element.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.every(id => id.startsWith('einv-'))).toBe(true);
    for (const element of elements) {
      if (element.binding) expect(EINVOICE_BINDING_PATHS).toContain(element.binding);
    }
  });

  it('re-projects the canvas when an invoice value changes', async () => {
    await render();
    await fillValidInvoice();
    await click(testId('einvoice-insert-invoice-standard'));

    const before = useEditorStore.getState().currentTemplate!.pages[0].elements
      .find(element => element.binding === 'totals.grandTotal');
    expect(before?.content).toBe('214.20');

    await type('#einv-line-0-quantity', '3');

    const after = useEditorStore.getState().currentTemplate!.pages[0].elements
      .find(element => element.binding === 'totals.grandTotal');
    expect(after?.content).toBe('321.30');
  });

  it('projects a row per invoice line and per tax group', async () => {
    const definition = invoiceTemplate('invoice-standard')!;
    const document = readEInvoiceDocument({
      schemaVersion: 1,
      settings: { standard: 'FacturX', version: '1.09.2', profile: 'EN 16931' },
      invoice: {
        invoiceNumber: 'P-1',
        issueDate: '2026-09-27',
        currency: 'EUR',
        currencyDecimalPlaces: 2,
        seller: { name: 'S' },
        buyer: { name: 'B' },
        lines: [
          { identifier: '1', description: 'A', quantity: 1, unitCode: 'C62', unitPrice: 100, taxRate: 19, taxCategoryCode: 'S' },
          { identifier: '2', description: 'B', quantity: 1, unitCode: 'C62', unitPrice: 50, taxRate: 7, taxCategoryCode: 'S' },
        ],
      },
    })!;

    const elements = projectInvoiceElements(definition, document.invoice, key => key);

    const lineDescriptions = elements.filter(element => element.binding === 'line.description');
    const taxAmounts = elements.filter(element => element.binding === 'tax.amount');
    expect(lineDescriptions.map(element => element.content)).toEqual(['A', 'B']);
    expect(taxAmounts.map(element => element.content)).toEqual(['3.50', '19.00']);
    // Rows are offset, not stacked on top of each other.
    expect(lineDescriptions[1].y).toBeGreaterThan(lineDescriptions[0].y);
  });

  it('embeds no literal invoice value into the reusable definition', () => {
    const definition = invoiceTemplate('invoice-standard')!;

    const serialized = JSON.stringify(definition);
    expect(serialized).not.toContain('Seller GmbH');
    expect(serialized).not.toContain('257.00');
    // Labels remain i18n keys in the definition itself.
    expect(serialized).toContain('{{t:template.amountPayable}}');
  });
});

// ---------------------------------------------------------------- persistence

describe('persistence and autosave', () => {
  it('stores the canonical invoice, the settings and the template binding on the document', async () => {
    await render();
    await fillValidInvoice();
    await click(testId('einvoice-insert-invoice-standard'));

    const stored = useEditorStore.getState().currentTemplate!.eInvoice!;
    expect(stored.schemaVersion).toBe(1);
    expect(stored.settings).toEqual({ standard: 'FacturX', version: '1.09.2', profile: 'EN 16931' });
    expect(stored.templateId).toBe('invoice-standard');
    expect(stored.invoice.invoiceNumber).toBe('DES-2026-1');
  });

  it('autosaves through the existing two-second pipeline', async () => {
    jest.useFakeTimers();
    updateDraftMock.mockResolvedValue({ id: 'server-1', revision: 4, status: 'Draft' });

    await render();
    await fillValidInvoice();

    expect(updateDraftMock).not.toHaveBeenCalled();
    await act(async () => { jest.advanceTimersByTime(2000); });
    await act(async () => { await Promise.resolve(); });

    expect(updateDraftMock).toHaveBeenCalled();
    const [, revision, snapshot] = updateDraftMock.mock.calls.at(-1)!;
    expect(revision).toBe(3);
    // The snapshot the pipeline persists carries the invoice, so no extra save path is needed.
    expect((snapshot as { template: { eInvoice?: unknown } }).template.eInvoice).toBeTruthy();
    jest.useRealTimers();
  });

  it('reproduces the exact values and totals when the stored draft is read back', async () => {
    await render();
    await fillValidInvoice();
    const stored = JSON.parse(
      JSON.stringify(useEditorStore.getState().currentTemplate!.eInvoice),
    );

    const restored = readEInvoiceDocument(stored)!;

    expect(restored.invoice).toEqual(
      readEInvoiceDocument(useEditorStore.getState().currentTemplate!.eInvoice)!.invoice,
    );
    expect(restored.invoice.lines[0].unitPrice).toBe(90);
    expect(restored.settings.version).toBe('1.09.2');
  });

  it('surfaces a revision conflict with its recovery actions', async () => {
    jest.useFakeTimers();
    const { DesignerTemplateApiError } = jest.requireActual('@/services/designerTemplateApi');
    updateDraftMock.mockRejectedValue(
      Object.assign(new DesignerTemplateApiError('Conflict', 409), { status: 409, updatedBy: 'Other User' }),
    );

    await render();
    await fillValidInvoice();
    await act(async () => { jest.advanceTimersByTime(2000); });
    await act(async () => { await Promise.resolve(); });
    await act(async () => { await Promise.resolve(); });

    expect(testId('einvoice-save-state').dataset.state).toBe('conflict');
    expect(container.textContent).toContain('A newer server draft exists');
    expect(container.textContent).toContain('Load the server draft');
    jest.useRealTimers();
  });

  it('reports the offline state instead of pretending the draft was saved', async () => {
    jest.useFakeTimers();
    const { DesignerTemplateApiError } = jest.requireActual('@/services/designerTemplateApi');
    updateDraftMock.mockRejectedValue(
      Object.assign(new DesignerTemplateApiError('Offline', 0), { status: 0, offline: true }),
    );

    await render();
    await fillValidInvoice();
    await act(async () => { jest.advanceTimersByTime(2000); });
    // The pipeline treats a dropped connection as transient and retries on a 500/1000/2000ms
    // backoff before giving up, so the offline state is only reached after the last attempt.
    for (let attempt = 0; attempt < 6; attempt += 1) {
      await act(async () => { jest.advanceTimersByTime(2000); });
      await act(async () => { await Promise.resolve(); });
    }

    expect(testId('einvoice-save-state').dataset.state).toBe('offline');
    expect(container.textContent).toContain('Offline');
    jest.useRealTimers();
  });
});

// ---------------------------------------------------------------- generation

describe('generation, download and viewer handoff', () => {
  function xmlResponse(): Response {
    return {
      ok: true,
      status: 200,
      blob: async () => new Blob(['<Invoice/>'], { type: 'application/xml' }),
    } as unknown as Response;
  }

  function jsonResponse(body: unknown): Response {
    return { ok: true, status: 200, json: async () => body } as unknown as Response;
  }

  function pdfResponse(): Response {
    return {
      ok: true,
      status: 200,
      blob: async () => new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
    } as unknown as Response;
  }

  beforeEach(() => {
    Object.defineProperty(URL, 'createObjectURL', { value: jest.fn(() => 'blob:mock'), writable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: jest.fn(), writable: true });
  });

  it('generates the XML and offers it for download', async () => {
    global.fetch = jest.fn().mockResolvedValue(xmlResponse());
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-generate-xml'));
    await flush();

    const [url, init] = (global.fetch as jest.Mock).mock.calls[0];
    expect(url).toBe('/api/pvad/v1/pdf/e-invoices/generate');
    // The request carries the canonical invoice, not a rendered projection of it.
    const body = JSON.parse((init as RequestInit).body as string);
    expect(body.invoice.lines[0].unitPrice).toBe(90);
    expect(body.invoice).not.toHaveProperty('totals');
    expect(testId('einvoice-download-xml')).toBeTruthy();

    const anchorClick = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    await click(testId('einvoice-download-xml'));
    expect(anchorClick).toHaveBeenCalled();
    anchorClick.mockRestore();
  });

  it('discards generated artifacts as soon as the canonical invoice changes', async () => {
    global.fetch = jest.fn().mockResolvedValue(xmlResponse());
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-generate-xml'));
    await flush();
    expect(testId('einvoice-download-xml')).toBeTruthy();

    await type('#einv-invoiceNumber', 'DES-2026-CHANGED');
    expect(query('[data-testid="einvoice-download-xml"]')).toBeNull();
    expect(testId('einvoice-results').textContent).toContain('Nothing generated yet');
  });

  it('ignores a response that finishes after the canonical invoice changed', async () => {
    let completeRequest!: (response: Response) => void;
    global.fetch = jest.fn().mockReturnValue(new Promise<Response>(resolve => {
      completeRequest = resolve;
    }));
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-generate-xml'));
    await type('#einv-invoiceNumber', 'DES-2026-NEW-REVISION');
    completeRequest(xmlResponse());
    await flush();

    expect(query('[data-testid="einvoice-download-xml"]')).toBeNull();
    expect(testId('einvoice-results').textContent).toContain('Nothing generated yet');
  });

  it('validates through the backend and shows all three states', async () => {
    global.fetch = jest.fn()
      .mockResolvedValueOnce(xmlResponse())
      .mockResolvedValueOnce(jsonResponse({
        isStructurallyValid: true,
        isConformanceValidated: false,
        syntax: 'CII',
        guidelineIdentifier: 'urn:factur-x.eu:1p0:en16931',
        diagnostics: [{ code: 'PXAEINV000', severity: 'information', message: 'Recognised.' }],
        conformanceStatus: 'notEvaluated',
        validatorArtifactSetId: null,
        validatorArtifactSetSha256: null,
      }));
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-validate'));
    await flush();

    expect(testId('einvoice-state-structural').textContent).toContain('Recognised as CII');
    expect(testId('einvoice-state-conformance').textContent).toContain('Conformance not evaluated');
    // notEvaluated must never read as valid or conformant.
    expect(testId('einvoice-state-conformance').textContent).not.toMatch(/conformant/i);
  });

  it('shows a conformance claim only when the backend reports a pinned artifact set', async () => {
    global.fetch = jest.fn()
      .mockResolvedValueOnce(xmlResponse())
      .mockResolvedValueOnce(jsonResponse({
        isStructurallyValid: true,
        isConformanceValidated: true,
        syntax: 'CII',
        guidelineIdentifier: 'urn:factur-x.eu:1p0:en16931',
        diagnostics: [],
        conformanceStatus: 'valid',
        validatorArtifactSetId: 'ferd-zugferd-2.5.2-en16931',
        validatorArtifactSetSha256: 'c'.repeat(64),
      }));
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-validate'));
    await flush();

    expect(testId('einvoice-state-conformance').textContent)
      .toContain('Validated against the pinned official artifacts');
    expect(testId('einvoice-state-conformance').textContent)
      .toContain('ferd-zugferd-2.5.2-en16931');
  });

  it('creates the PDF, reports PDF/XML consistency and hands off to the Viewer', async () => {
    global.fetch = jest.fn()
      .mockResolvedValueOnce(pdfResponse())
      .mockResolvedValueOnce(jsonResponse([{
        fileName: 'factur-x.xml',
        relationship: 'Alternative',
        sizeBytes: 10,
        sizeMatches: true,
        checksumMatches: true,
        isXml: true,
        standard: 'Factur-X / ZUGFeRD',
        syntax: 'CII',
        profile: 'EN 16931',
        isStructurallyValid: true,
        conformanceStatus: 'notEvaluated',
        diagnostics: [],
        pdfConsistency: { status: 'consistent', findings: [] },
      }]));
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-create-pdf'));
    await flush();
    await flush();

    expect(testId('einvoice-consistency').dataset.status).toBe('consistent');
    expect(testId('einvoice-download-pdf')).toBeTruthy();

    await click(testId('einvoice-open-viewer'));
    expect(navigateMock).toHaveBeenCalledWith('/pdf/viewer?handoff=session');
    expect(JSON.parse(sessionStorage.getItem(documentHandoffKeys.pdfViewer)!).payload.name)
      .toBe('DES-2026-1.pdf');
  });

  it('keeps a text-inaccessible result at notEvaluated rather than consistent', async () => {
    global.fetch = jest.fn()
      .mockResolvedValueOnce(pdfResponse())
      .mockResolvedValueOnce(jsonResponse([{
        fileName: 'factur-x.xml',
        relationship: 'Alternative',
        sizeBytes: 10,
        sizeMatches: true,
        checksumMatches: true,
        isXml: true,
        isStructurallyValid: true,
        conformanceStatus: 'notEvaluated',
        diagnostics: [],
        pdfConsistency: { status: 'notEvaluated', findings: [] },
      }]));
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-create-pdf'));
    await flush();
    await flush();

    expect(testId('einvoice-consistency').dataset.status).toBe('notEvaluated');
    expect(testId('einvoice-consistency').textContent).toContain('no comparison was made');
  });

  it('reports a server refusal with its stable Problem Details code', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: false,
      status: 422,
      json: async () => ({ title: 'Rejected', detail: 'Too many lines', code: 'PXAEINVAPI011' }),
    } as unknown as Response);
    await render();
    await fillValidInvoice();

    await click(testId('einvoice-create-pdf'));
    await flush();

    expect(testId('einvoice-error').textContent).toContain('PXAEINVAPI011');
  });
});

// ---------------------------------------------------------------- localization

describe('localization and accessibility', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
    document.documentElement.dir = 'ltr';
  });

  it.each([
    ['en', 'Electronic invoice'],
    ['de', 'Elektronische Rechnung'],
    ['fr', 'Facture électronique'],
    ['es', 'Factura electrónica'],
    ['it', 'Fattura elettronica'],
    ['ar', 'فاتورة إلكترونية'],
  ])('renders the workspace in %s', async (language, heading) => {
    await act(async () => { await i18n.changeLanguage(language); });

    await render();
    await click(testId('einvoice-empty').querySelector('button'));

    expect(container.querySelector('h1')?.textContent).toBe(heading);
    // No untranslated key leaks into the rendered page.
    expect(container.textContent).not.toMatch(/\b(page|sections|fields|actions)\.[a-zA-Z]/);
  });

  it('mirrors for Arabic and keeps the table semantics', async () => {
    await act(async () => { await i18n.changeLanguage('ar'); });
    document.documentElement.dir = 'rtl';

    await render();
    await fillValidInvoice();

    expect(document.documentElement.dir).toBe('rtl');
    const table = query('table.einv-lines-table')!;
    expect(table.querySelector('caption')).toBeTruthy();
    expect(table.querySelectorAll('thead th[scope="col"]').length).toBeGreaterThan(5);
    expect(table.querySelector('tbody th[scope="row"]')).toBeTruthy();
    // The line caption is translated, not an English fallback.
    expect(table.querySelector('caption')!.textContent).not.toContain('Invoice lines with');
  });

  it('labels every input and keeps it keyboard reachable', async () => {
    await render();
    await fillValidInvoice();

    const controls = Array.from(
      container.querySelectorAll<HTMLElement>('input, select, textarea'),
    );
    expect(controls.length).toBeGreaterThan(15);
    for (const control of controls) {
      const id = control.getAttribute('id');
      expect(id).toBeTruthy();
      const label = container.querySelector(`label[for="${id}"]`);
      expect(label?.textContent?.trim()).toBeTruthy();
      // Nothing is removed from the tab order.
      expect(control.getAttribute('tabindex')).not.toBe('-1');
      expect((control as HTMLInputElement).disabled).toBe(false);
    }
  });

  it('announces an asynchronous result in a live region', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      blob: async () => new Blob(['<Invoice/>'], { type: 'application/xml' }),
    } as unknown as Response);
    await render();
    await fillValidInvoice();

    const liveRegion = container.querySelector('[aria-live="polite"]')!;
    expect(liveRegion.textContent).toBe('');

    await click(testId('einvoice-generate-xml'));
    await flush();

    expect(liveRegion.textContent).toBe('Invoice XML generated.');
  });

  it('gives every icon-only button an accessible name', async () => {
    await render();
    await fillValidInvoice();

    const iconButtons = Array.from(container.querySelectorAll<HTMLButtonElement>('.einv-icon-btn'));
    expect(iconButtons.length).toBeGreaterThan(0);
    for (const button of iconButtons) {
      expect(button.getAttribute('aria-label')?.trim()).toBeTruthy();
    }
  });
});

// ---------------------------------------------------------------- single source of truth

describe('canonical invoice state', () => {
  it('keeps exactly one editable copy of every invoice value', async () => {
    await render();
    await fillValidInvoice();
    await click(testId('einvoice-insert-invoice-standard'));

    const template = useEditorStore.getState().currentTemplate!;
    // The invoice lives in one place.
    expect(template.eInvoice?.invoice.invoiceNumber).toBe('DES-2026-1');
    expect(template.data).toEqual({});
    expect(useEditorStore.getState().jsonData).toEqual({});

    // The page elements are a projection: they carry the binding path they came from, so they
    // are traceable rather than an independent second copy.
    const projected = template.pages[0].elements
      .filter(element => element.binding === 'invoice.invoiceNumber');
    expect(projected).toHaveLength(1);
    expect(projected[0].content).toBe('DES-2026-1');
    const projectedId = projected[0].id;
    const pageId = template.pages[0].id;

    // Editing the invoice moves the projection; the projection is never the source.
    await type('#einv-invoiceNumber', 'DES-2026-2');
    const after = useEditorStore.getState().currentTemplate!.pages[0].elements
      .find(element => element.binding === 'invoice.invoiceNumber');
    expect(after?.content).toBe('DES-2026-2');
    expect(after?.id).toBe(projectedId);
    expect(useEditorStore.getState().currentTemplate!.pages[0].id).toBe(pageId);
    expect(useEditorStore.getState().currentTemplate!.eInvoice?.invoice.invoiceNumber)
      .toBe('DES-2026-2');
  });
});
