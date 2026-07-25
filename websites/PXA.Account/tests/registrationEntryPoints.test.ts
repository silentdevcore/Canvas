import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('Company and Designer delegate customer registration exclusively to PXA Account', async () => {
  const [company, designerAuth, designerGate, account, accountApi] = await Promise.all([
    readFile(new URL('../../PXA.Company/src/main.js', import.meta.url), 'utf8'),
    readFile(new URL('../../../pxa-designer/src/auth/designerAuth.ts', import.meta.url), 'utf8'),
    readFile(new URL('../../../pxa-designer/src/auth/DesignerAuthGate.tsx', import.meta.url), 'utf8'),
    readFile(new URL('../src/main.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/api.ts', import.meta.url), 'utf8'),
  ]);

  assert.match(company, /siteLinks\.account\}register/);
  assert.doesNotMatch(company, /id=["']register-form["']/);
  assert.doesNotMatch(company, /\/api\/(?:pxa\/v1\/)?auth\/register/);

  assert.match(designerAuth, /accountBaseUrl\(\)\}designer-authorize/);
  assert.doesNotMatch(designerAuth, /\/api\/(?:pxa\/v1\/)?auth\/register/);
  assert.doesNotMatch(`${designerAuth}\n${designerGate}`, /id=["']register-form["']/);

  assert.match(account, /id="register-form"/);
  assert.match(accountApi, /authBase\}\/register/);
});
