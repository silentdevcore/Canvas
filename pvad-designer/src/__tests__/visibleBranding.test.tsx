/** @jest-environment jsdom */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import vm from 'node:vm';
import ts from 'typescript';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import i18n from '@/i18n';
import InvoiceDiagnosticsPanel from '@/features/e-invoice/InvoiceDiagnosticsPanel';
import { displayDiagnosticCode, displaySystemMessage, presentRelease } from '../../../websites/shared/diagnosticPresentation';
import releaseManifest from '../../../product-metadata/pvad-releases.json';

test('diagnostic codes and system messages are shown exactly as issued', () => {
  for (const code of ['PVADAPI010', 'PVADCODE042', 'PVAD-PDF-CHART-102', 'CANMIG100', 'PXA-customer-document'])
    expect(displayDiagnosticCode(code)).toBe(code);
  expect(displayDiagnosticCode(null)).toBe('');
  expect(displaySystemMessage('Customer file PXA-2024.pdf')).toBe('Customer file PXA-2024.pdf');
});

test('release presentation does not rewrite the historical manifest', () => {
  const original = JSON.stringify(releaseManifest);
  for (const release of releaseManifest.releases) {
    const shown = presentRelease(release);
    expect(JSON.stringify([shown.title, shown.summary, shown.changes])).not.toMatch(/PXA|Power Dox Automation/);
    expect(shown.version).toBe(release.version);
    expect(shown.documentationPath).toBe(release.documentationPath);
  }
  expect(JSON.stringify(releaseManifest)).toBe(original);
});

test.each(['en', 'de', 'fr', 'es', 'it', 'ar'])('invoice findings show display aliases in %s while keeping raw selectors', async language => {
  (globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  await i18n.changeLanguage(language);
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  const finding = { code: 'PVADEINV100', severity: 'error' as const, field: 'invoiceNumber', messageKey: 'validation.invoiceNumberRequired' };
  try {
    await act(async () => root.render(<InvoiceDiagnosticsPanel findings={[finding]} report={null} onFocusField={() => {}} />));
    expect(container.textContent).toContain('PVADEINV100');
    expect(container.textContent).not.toContain('PXA');
    expect(container.querySelector('[data-code="PVADEINV100"]')).not.toBeNull();
    expect(finding.code).toBe('PVADEINV100');
    expect(i18n.t('common:aboutPvaDe')).toContain('PVADE');
  } finally {
    await act(async () => root.unmount());
    container.remove();
    await i18n.changeLanguage('en');
  }
});

const root = resolve(process.cwd(), '..');
const admin = 'websites/PVAD.Admin/src/main.js';
const account = 'websites/PVAD.Account/src/main.ts';

// Execute the real render/event functions without bootstrapping authentication or a network client.
function functions(file: string, names: string[], globals: Record<string, unknown> = {}) {
  const source = ts.createSourceFile(file, readFileSync(resolve(root, file), 'utf8'), ts.ScriptTarget.Latest, true);
  const declarations = names.map(name => {
    const declaration = source.statements.find(node => ts.isFunctionDeclaration(node) && node.name?.text === name);
    if (!declaration) throw new Error(`Missing production function ${file}:${name}`);
    return declaration.getText(source);
  }).join('\n');
  const output = ts.transpileModule(declarations, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  return vm.runInNewContext(`${output}\n;({${names.join(',')}})`, {
    exports: {}, document, FormData, displayDiagnosticCode, displaySystemMessage,
    ...globals,
  }) as Record<string, (...args: any[]) => any>;
}

const escapeHtml = functions(admin, ['escapeHtml']).escapeHtml;
const message = 'PVADAPI018: Unavailable <img src=x onerror=alert(1)>';
const shown = 'PVADAPI018: Unavailable <img src=x onerror=alert(1)>';
const host = (html: string) => { const element = document.createElement('div'); element.innerHTML = html; return element; };

describe('Account diagnostic rendering', () => {
  test('shared auth notices project errors but leave informational customer text unchanged', () => {
    const page = functions(account, ['escapeHtml', 'message']);
    expect(host(page.message('error', message)).querySelector('[role="alert"]')?.textContent).toBe(shown);
    expect(host(page.message('info', 'PVAD Customer Ltd.')).textContent).toBe('PVAD Customer Ltd.');
  });
  test.each([
    ['licenses', 'licensesPage', ['licenseRow'], { licenses: [], validation: {} }],
    ['subscription', 'subscriptionPage', [], { subscription: null }],
    ['usage', 'usagePage', [], { subscription: null, usage: null }],
    ['security', 'securityPage', ['sessionRow'], { sessions: [] }],
  ] as const)('%s projects escaped error text without mutating state', (name, render, helpers, values) => {
    const state = { loaded: true, loading: false, notice: null, error: message, ...values };
    const page = functions(`websites/PVAD.Account/src/pages/${name}.ts`, [...helpers, render], { state, escapeHtml });
    const element = host(page[render]());
    expect(element.querySelector('[role="alert"]')?.textContent).toBe(shown);
    expect(element.querySelector('img')).toBeNull();
    expect(state.error).toBe(message);
  });

  test('license validation errors are projected while license numbers and download URLs stay raw', () => {
    const state = { loaded: true, loading: false, error: null,
      licenses: [{ id: 'license-1', licenseNumber: 'PVAD-CUSTOMER-001', edition: 'Enterprise',
        deploymentMode: 'OnPremise', status: 'Active', instanceLimit: 1,
        validFrom: '2026-01-01', validUntil: '2027-01-01' }], validation: { 'license-1': message } };
    const page = functions('websites/PVAD.Account/src/pages/licenses.ts', ['licenseRow', 'licensesPage'], {
      state, escapeHtml, accountLicenseDownloadUrl: (id: string) => `/api/pvad/v1/account/licenses/${id}/download`,
    });
    const element = host(page.licensesPage());
    expect(element.querySelector('td:last-child div')?.textContent).toBe(shown);
    expect(element.querySelector('td')?.textContent).toContain('PVAD-CUSTOMER-001');
    expect(element.querySelector('a')?.getAttribute('href')).toBe('/api/pvad/v1/account/licenses/license-1/download');
    expect(state.validation['license-1']).toBe(message);
  });

  test.each(['PVADAPI010', 'PVADAPI015', 'PVADAPI016', 'PVADAPI018'])('login handles %s with projected text and unchanged diagnostics', async code => {
    const form = document.createElement('form');
    form.innerHTML = '<input name="identifier" value="synthetic@example.test"><input name="password" type="password" value="synthetic"><div id="form-error" hidden></div><button type="submit">Sign in</button>';
    document.body.appendChild(form);
    form.id = 'login-test';
    const failure = Object.assign(new Error(`${code}: Access unavailable <img src=x>`), { code });
    const handlers = functions(account, ['escapeHtml', 'bindForm', 'formString'], {
      tr: (value: string) => value, companyPage: () => 'https://pvade.com/support',
      PROBLEM_CODE_VERIFICATION_REQUIRED: 'PVADAPI010', PROBLEM_CODE_ACCOUNT_DISABLED: 'PVADAPI015',
      PROBLEM_CODE_ORGANIZATION_SUSPENDED: 'PVADAPI016',
    });
    try {
      handlers.bindForm('#login-test', async () => { throw failure; });
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      await new Promise(resolve => setTimeout(resolve, 0));
      const alert = form.querySelector<HTMLElement>('#form-error')!;
      expect(alert.textContent).toContain(`${code}: Access unavailable <img src=x>`);
      expect(alert.hidden).toBe(false);
      expect(alert.querySelector('img')).toBeNull();
      expect(form.querySelector<HTMLInputElement>('input[type="password"]')?.value).toBe('');
      expect(failure.message).toContain(code);
      expect(failure.code).toBe(code);
      expect(form.querySelector('button[type="submit"]')?.hasAttribute('disabled')).toBe(false);
    } finally { form.remove(); }
  });
});

describe('Admin diagnostic rendering', () => {
  test('login notices project API diagnostics without changing state', () => {
    const app = document.createElement('div'); document.body.appendChild(app);
    const state = { notice: message };
    const page = functions(admin, ['escapeHtml', 'renderLogin'], { app, state, handleLogin: jest.fn() });
    try {
      page.renderLogin();
      expect(app.querySelector('[role="status"]')?.textContent).toBe(shown);
      expect(app.querySelector('img')).toBeNull();
      expect(state.notice).toBe(message);
    } finally { app.remove(); }
  });

  test('password-reset action failures project text and keep recovery controls available', async () => {
    const element = document.createElement('div');
    element.innerHTML = '<form id="public-action-form"><input name="email" value="synthetic@example.test"><button>Reset</button></form><p id="public-action-error" hidden></p><p id="public-action-success" hidden></p>';
    document.body.appendChild(element);
    const failure = new Error(message);
    const page = functions(admin, ['bindPublicAction'], { requestPasswordReset: async () => { throw failure; } });
    try {
      page.bindPublicAction('request-reset');
      element.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      await new Promise(resolve => setTimeout(resolve, 0));
      expect(element.querySelector('#public-action-error')?.textContent).toBe(shown);
      expect(element.querySelector<HTMLElement>('#public-action-error')?.hidden).toBe(false);
      expect(element.querySelector('button')?.disabled).toBe(false);
      expect(failure.message).toBe(message);
    } finally { element.remove(); }
  });
  test.each([
    ['userDetail', 'userDetailPage'], ['organizationDetail', 'organizationDetailPage'],
    ['subscriptionDetail', 'subscriptionDetailPage'], ['licenseDetail', 'licenseDetailPage'],
    ['roleDetail', 'roleDetailPage'],
  ])('%s unavailable state projects escaped diagnostic text', (key, render) => {
    const detail = { loading: false, data: null, error: message };
    const page = functions(admin, ['escapeHtml', render], { state: { [key]: detail } });
    const element = host(page[render]());
    expect(element.textContent).toContain(shown);
    expect(element.querySelector('img')).toBeNull();
    expect(detail.error).toBe(message);
  });

  test('license rows and audit evidence retain signed and stored identifiers', () => {
    const license = { id: 'license-1', licenseNumber: 'PVAD-CUSTOMER-001', keyId: 'pvad-development-1',
      organizationName: 'PVAD Customer Ltd.', edition: 'Enterprise', deploymentMode: 'OnPremise',
      status: 'Active', validFrom: '2026-01-01', validUntil: '2027-01-01', instanceLimit: 1, algorithm: 'ES256' };
    const audit = [{ action: 'pvadp.export', actorName: 'PVAD Customer Ltd.', outcome: 'succeeded' }];
    const detail = { loading: false, saving: false, data: license, error: message, notice: 'PVAD_LICENSE_VALID', audit };
    const page = functions(admin, ['escapeHtml', 'licenseDetailPage'], {
      state: { licenseDetail: detail }, formatDate: (value: string) => value ?? '', isSystemAdministrator: () => false,
    });
    const original = JSON.stringify(detail);
    const element = host(page.licenseDetailPage());
    expect(element.querySelector('.admin-alert--error')?.textContent).toBe(shown);
    expect(element.querySelector('h1')?.textContent).toBe('PVAD-CUSTOMER-001');
    expect(element.textContent).toContain('pvad-development-1');
    expect(element.textContent).toContain('PVAD Customer Ltd.');
    expect(element.textContent).toContain('pvadp.export');
    expect(JSON.stringify(detail)).toBe(original);
  });

  test('login error uses projected textContent and keeps the original error', async () => {
    const form = document.createElement('form');
    form.innerHTML = '<input name="identifier" value="synthetic@example.test"><input name="password" value="synthetic"><button type="submit">Sign in</button>';
    const alert = document.createElement('div'); alert.id = 'login-error';
    document.body.append(form, alert);
    const failure = new Error(message);
    const handlers = functions(admin, ['handleLogin'], { login: async () => { throw failure; } });
    try {
      await handlers.handleLogin({ preventDefault() {}, currentTarget: form });
      expect(alert.textContent).toBe(shown);
      expect(alert.querySelector('img')).toBeNull();
      expect(form.querySelector('button')?.disabled).toBe(false);
      expect(failure.message).toBe(message);
    } finally { form.remove(); alert.remove(); }
  });
});
