import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { mkdirSync, readFileSync, readdirSync, existsSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

assert.equal(process.env.PVAD_RUN_NATIVE_INSTALL, '1');
assert.equal(process.platform, 'darwin');
assert.equal(process.env.PVAD_NATIVE_TEST_DISPOSABLE, '1', 'Only an explicitly disposable API, database and mail environment may be used.');
const root = fileURLToPath(new URL('../../', import.meta.url));
function localOrigin(key) {
  assert.ok(process.env[key], `${key} is required.`);
  const url = new URL(process.env[key]);
  assert.ok(['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) && url.protocol === 'http:'
    && url.pathname === '/' && !url.search && !url.hash && !url.username && !url.password, 'An isolated loopback HTTP origin is required.');
  return url.origin;
}
const origin = localOrigin('PVAD_NATIVE_TEST_ORIGIN'), account = localOrigin('PVAD_NATIVE_TEST_ACCOUNT_ORIGIN'),
  mail = localOrigin('PVAD_NATIVE_TEST_MAIL_ORIGIN');
const id = randomBytes(6).toString('hex'), output = resolve(root, 'artifacts/rename/native-os-open', id);
mkdirSync(output, { recursive: true });
const appDirectory = resolve(homedir(), 'Applications/Chrome Apps.localized');
const apps = () => existsSync(appDirectory) ? readdirSync(appDirectory).filter(n => /^PVADE(?: Designer)?(?: \(.*\))?\.app$/i.test(n)) : [];
assert.deepEqual(apps(), [], 'Never replace a pre-existing PVADE installation.');
const { chromium, expect } = createRequire(resolve(root, 'pvad-designer/package.json'))('@playwright/test');
const existingAccount = Boolean(process.env.PVAD_NATIVE_TEST_EMAIL);
assert.equal(Boolean(process.env.PVAD_NATIVE_TEST_PASSWORD), existingAccount, 'Supply both synthetic credentials or neither.');
const email = process.env.PVAD_NATIVE_TEST_EMAIL ?? `native-${id}@example.test`;
assert.match(email, /^[^@\s]+@example\.test$/, 'Only synthetic test accounts are allowed.');
const password = process.env.PVAD_NATIVE_TEST_PASSWORD ?? `Pv4d!${randomBytes(12).toString('hex')}`;
const report = { runId: id, sourceCommit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
  trackedSourceDirty: Boolean(execFileSync('git', ['status', '--porcelain', '--untracked-files=no'], { cwd: root, encoding: 'utf8' }).trim()),
  sourceFiles: Object.fromEntries(['pvad-designer/src/native/nativeProjectLaunch.ts', 'pvad-designer/src/store.ts',
    'tools/configuration/native-project-installed-smoke.mjs'].map(path => [path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex')])),
  os: execFileSync('sw_vers', ['-productVersion'], { encoding: 'utf8' }).trim(),
  scope: 'Actual macOS default-handler dispatch to a real Chrome PWA in an isolated profile. Refuses pre-existing PVADE apps and existing project-type default handlers. Automated installation; not manual install/first-run permission prompt acceptance.',
  syntheticDataOnly: true, origin, status: 'running', steps: [] };
const save = () => writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
const step = (name, details = {}) => { report.steps.push({ name, status: 'passed', ...details }); save(); console.log('PASS', name); };
function defaultHandler(path) {
  const script = `ObjC.import('AppKit'); const url=$.NSURL.fileURLWithPath(${JSON.stringify(path)}); const app=$.NSWorkspace.sharedWorkspace.URLForApplicationToOpenURL(url); JSON.stringify({handler:app.isNil()?null:ObjC.unwrap(app.path)});`;
  return JSON.parse(execFileSync('osascript', ['-l', 'JavaScript', '-e', script], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }));
}
let context, session, installed = false, current;
async function signIn(page) {
  await page.locator('[name="identifier"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('#login-form button[type="submit"]').click();
  await page.waitForURL(url => url.origin === origin && !url.pathname.startsWith('/auth/'), { timeout: 30000 });
}
async function openOs(file, name) {
  const old = new Set(context.pages());
  assert.equal(defaultHandler(file).handler, resolve(appDirectory, report.appBundles[0]), 'Only dispatch to the verified test default handler.');
  execFileSync('/usr/bin/open', [file]);
  console.log(`WAITING_FOR_NATIVE_PERMISSION ${name}: Allow the file handling prompt in the separate PVADE test app if Chrome asks.`);
  for (let i = 0; i < 600; i++) {
    const p = context.pages().find(p => !old.has(p) && p.url().startsWith(`${origin}/open-project`));
    if (p) { current = p; if (await p.getByText(name, { exact: true }).isVisible().catch(() => false)) return p; }
    await delay(500);
  }
  throw new Error('No real file arrived from macOS; permission or OS dispatch remains unverified.');
}
async function assertImported(page, kind, marker) {
  const before = uploads.length;
  const result = page.waitForResponse(response => new URL(response.url()).pathname === `/api/pvad/v1/designer/projects/${kind}/open`);
  await page.getByRole('button', { name: 'Open in Designer', exact: true }).click();
  assert.equal((await result).status(), 200);
  await page.waitForURL(`**/${kind}/create**`);
  await page.getByText(marker, { exact: true }).first().waitFor({ timeout: 30000 });
  await page.waitForFunction(() => !location.search.includes('nativeLaunch'));
  await expect.poll(() => uploads.length - before).toBe(1);
  const actual = uploads.slice(before);
  assert.equal(actual.length, 1, 'One explicit Open causes one upload.');
  assert.equal(actual[0].status, 200);
  assert.equal(actual[0].csrf, true);
  await page.screenshot({ path: resolve(output, `${kind}-import.png`), fullPage: true });
}
const uploads = [];
console.log('OUTPUT', output);
try {
  context = await chromium.launchPersistentContext(resolve(output, 'profile'), { channel: 'chrome', headless: false,
    locale: 'en-US', timezoneId: 'UTC', viewport: { width: 1280, height: 900 },
    args: ['--enable-devtools-pwa-handler', '--no-default-browser-check'], acceptDownloads: true });
  session = await context.browser().newBrowserCDPSession();
  context.setDefaultTimeout(30000); context.setDefaultNavigationTimeout(30000);
  report.browser = await session.send('Browser.getVersion');
  context.on('response', response => {
    const request = response.request(), path = new URL(response.url()).pathname;
    if (/\/designer\/projects\/(pdf|spreadsheet)\/open$/.test(path)) uploads.push({ path, status: response.status(),
      csrf: Boolean(request.headers()['x-pvad-csrf']), application: request.headers()['x-pvad-application'] });
  });
  const page = context.pages()[0]; current = page;
  await page.goto(`${origin}/pdf/create`); await page.waitForURL(`${account}/login**`);
  const notice = page.locator('[data-pvad-storage-notice]');
  if (await notice.isVisible()) await notice.getByRole('button', { name: 'Understood' }).click();
  if (!existingAccount) {
  await page.getByRole('link', { name: 'Create account' }).click();
  await expect(page.locator('#register-form button[type="submit"]')).toBeEnabled();
  await page.locator('[name="displayName"]').fill('PVADE Native Test User');
  await page.locator('[name="email"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('[name="country"]').fill('CH');
  await page.locator('[name="acceptTerms"]').check(); await page.locator('[name="acceptPrivacy"]').check();
  const registration = page.waitForResponse(response => new URL(response.url()).pathname.endsWith('/auth/register'));
  await page.locator('#register-form button[type="submit"]').click();
  const registered = await registration;
  assert.ok(registered.ok(), `Synthetic registration failed with HTTP ${registered.status()}; do not disable rate limiting.`);
  await page.waitForURL('**/registration-pending**');
  let verification;
  for (let i = 0; i < 90; i++) {
    const messages = await (await fetch(`${mail}/api/v1/messages`)).json();
    const message = messages.messages?.find(m => /verify/i.test(m.Subject) && m.To.some(r => r.Address === email));
    if (message) {
      const content = await (await fetch(`${mail}/api/v1/message/${message.ID}`)).json();
      verification = `${content.Text ?? ''}\n${content.HTML ?? ''}`.replaceAll('&amp;', '&').match(/http:\/\/localhost:\d+\/verify-email\?[^\s"'<>]+/)?.[0];
    }
    if (verification) break; await delay(500);
  }
  assert.ok(verification); assert.equal(new URL(verification).origin, account);
  await page.goto(verification); await page.getByRole('heading', { name: 'Your Trial is ready' }).waitFor();
  await page.getByRole('link', { name: 'Sign in' }).click();
  }
  await signIn(page);
  await expect.poll(async () => (await page.request.get(`${origin}/api/pvad/v1/auth/me`, { headers: { 'X-PVAD-Application': 'designer' } })).status()).toBe(200);
  step(existingAccount ? 'existing-synthetic-account-real-designer-handoff' : 'real-registration-verification-and-designer-handoff');
  const files = {};
  for (const kind of ['pdf', 'spreadsheet']) {
    const extension = kind === 'pdf' ? 'pvadp' : 'pvads';
    const marker = `PVADE Native ${kind} ${id}`;
    const document = kind === 'pdf'
      ? { id: `native-${id}`, name: marker, sharedElements: [], pages: [{ id: `page-${id}`, elements: [
        { id: `text-${id}`, type: 'text', x: 30, y: 30, width: 300, height: 40, content: marker }]}] }
      : { schemaVersion: '1.0', id: `book-${id}`, name: marker, definedNames: [], sheets: [{ id: `sheet-${id}`, name: marker,
        rowCount: 20, colCount: 6, rows: [], columns: [], cells: [{ row: 0, col: 0, type: 'text', value: 'Native project cell' }],
        merges: [], frozenRows: 0, frozenCols: 0 }] };
    const csrf = await (await page.request.get(`${origin}/api/pvad/v1/auth/csrf`, { headers: { 'X-PVAD-Application': 'designer' } })).json();
    const result = await page.request.post(`${origin}/api/pvad/v1/designer/projects/${kind}/export`, {
      headers: { 'X-PVAD-Application': 'designer', 'X-PVAD-CSRF': csrf.token }, data: document });
    assert.equal(result.status(), 200, `${kind} real archive export: ${await result.text()}`);
    const bytes = await result.body(); assert.equal(bytes.subarray(0, 2).toString(), 'PK');
    const name = `synthetic-${kind}.${extension}`, path = resolve(output, name); writeFileSync(path, bytes);
    files[kind] = { name, path, marker, sha256: createHash('sha256').update(bytes).digest('hex') };
  }
  step('two-real-archive-exports');
  report.defaultHandlersBefore = Object.fromEntries(Object.entries(files).map(([kind, file]) => [kind, defaultHandler(file.path)]));
  assert.ok(Object.values(report.defaultHandlersBefore).every(value => value.handler === null), 'Refuse to change an existing project default handler.');
  await context.clearCookies();
  await page.goto(`${origin}/open-project`); await page.getByRole('heading', { name: 'Open project', exact: true }).waitFor();
  await page.evaluate(() => navigator.serviceWorker.ready);
  installed = true;
  await session.send('PWA.install', { manifestId: `${origin}/`, installUrlOrBundleUrl: `${origin}/open-project` });
  await session.send('PWA.changeAppUserSettings', { manifestId: `${origin}/`, displayMode: 'standalone' });
  report.appBundles = apps(); assert.equal(report.appBundles.length, 1);
  const appInfo = JSON.parse(execFileSync('plutil', ['-convert', 'json', '-o', '-', resolve(appDirectory, report.appBundles[0], 'Contents/Info.plist')], { encoding: 'utf8' }));
  report.installedAppId = appInfo.CrAppModeShortcutID;
  report.installedDocumentTypes = appInfo.CFBundleDocumentTypes;
  report.osHandlers = await session.send('PWA.getOsAppState', { manifestId: `${origin}/` });
  report.defaultHandlersInstalled = Object.fromEntries(Object.entries(files).map(([kind, file]) => [kind, defaultHandler(file.path)]));
  step('real-pwa-installation');
  const originalPages = new Set(context.pages());
  await session.send('PWA.launch', { manifestId: `${origin}/`, url: `${origin}/open-project` });
  await expect.poll(() => context.pages().filter(p => !originalPages.has(p)).length).toBe(1);
  const ordinary = context.pages().find(p => !originalPages.has(p)); current = ordinary;
  await ordinary.getByRole('heading', { name: 'Open project', exact: true }).waitFor();
  await expect(ordinary.getByRole('alert')).toHaveCount(0);
  assert.equal(await ordinary.evaluate(() => matchMedia('(display-mode: standalone)').matches), true);
  step('fileless-standalone-launch-without-error');
  const intake = await openOs(files.pdf.path, files.pdf.name);
  await expect(intake.getByRole('button', { name: 'Open in Designer', exact: true })).toBeDisabled();
  assert.equal(uploads.length, 0);
  await intake.screenshot({ path: resolve(output, 'signed-out-os-intake.png'), fullPage: true });
  step('macos-default-file-open-pvadp-signed-out-no-upload');
  const popupPromise = context.waitForEvent('page');
  await intake.getByRole('link', { name: 'Sign in', exact: true }).click();
  const login = await popupPromise;
  await login.waitForURL(`${account}/login**`); await signIn(login);
  await intake.getByRole('button', { name: 'Check access', exact: true }).click();
  await expect(intake.getByRole('button', { name: 'Open in Designer', exact: true })).toBeEnabled();
  await assertImported(intake, 'pdf', files.pdf.marker);
  step('signed-out-pvadp-sign-in-separate-tab-real-import');
  await intake.getByRole('button', { name: `Rename ${files.pdf.marker}`, exact: true }).click();
  const rename = intake.getByRole('textbox', { name: 'Document name', exact: true });
  const editedName = `Edited native PDF ${id}`;
  await rename.fill(editedName); await rename.press('Enter');
  await expect(intake.getByRole('button', { name: `Rename ${editedName}`, exact: true })).toBeVisible();
  const priorUrl = intake.url();
  const spreadsheet = await openOs(files.spreadsheet.path, files.spreadsheet.name);
  await expect(spreadsheet.getByRole('button', { name: 'Open in Designer', exact: true })).toBeEnabled();
  await assertImported(spreadsheet, 'spreadsheet', files.spreadsheet.marker);
  assert.equal(intake.url(), priorUrl);
  await expect(intake.getByRole('button', { name: `Rename ${editedName}`, exact: true })).toBeVisible();
  step('macos-default-file-open-pvads-signed-in-real-import-existing-editor-preserved');
  const secondPdf = await openOs(files.pdf.path, files.pdf.name);
  await assertImported(secondPdf, 'pdf', files.pdf.marker);
  step('macos-default-file-open-pvadp-signed-in-real-import');
  await context.clearCookies();
  const signedOutSpreadsheet = await openOs(files.spreadsheet.path, files.spreadsheet.name);
  await expect(signedOutSpreadsheet.getByRole('button', { name: 'Open in Designer', exact: true })).toBeDisabled();
  const beforeLoginUploads = uploads.length;
  const nextLogin = context.waitForEvent('page');
  await signedOutSpreadsheet.getByRole('link', { name: 'Sign in', exact: true }).click();
  const loginSpreadsheet = await nextLogin;
  await loginSpreadsheet.waitForURL(`${account}/login**`); await signIn(loginSpreadsheet);
  assert.equal(uploads.length, beforeLoginUploads);
  await signedOutSpreadsheet.getByRole('button', { name: 'Check access', exact: true }).click();
  await expect(signedOutSpreadsheet.getByRole('button', { name: 'Open in Designer', exact: true })).toBeEnabled();
  await assertImported(signedOutSpreadsheet, 'spreadsheet', files.spreadsheet.marker);
  step('macos-default-file-open-pvads-signed-out-sign-in-separate-tab-real-import');
  const settings = await context.newPage();
  assert.ok(report.installedAppId);
  await settings.goto(`chrome://app-settings/${report.installedAppId}`);
  await settings.locator('app-management-file-handling-item').waitFor();
  const toggle = settings.locator('app-management-file-handling-item cr-toggle');
  await expect(toggle).toBeVisible();
  await expect.poll(() => toggle.evaluate(e => Boolean(e.checked))).toBe(true);
  await toggle.click();
  await expect.poll(() => toggle.evaluate(e => Boolean(e.checked))).toBe(false);
  await expect.poll(async () => (await session.send('PWA.getOsAppState', { manifestId: `${origin}/` })).fileHandlers.length).toBe(0);
  report.revokedHandlers = await session.send('PWA.getOsAppState', { manifestId: `${origin}/` });
  await settings.screenshot({ path: resolve(output, 'chrome-file-handling-revoked.png'), fullPage: true });
  step('real-chrome-file-handling-revocation-removes-os-handlers');
  await settings.close();
  for (const kind of ['pdf', 'spreadsheet']) {
    const fallback = await context.newPage(); current = fallback;
    await fallback.goto(`${origin}/open-project`);
    const chooserPromise = fallback.waitForEvent('filechooser');
    await fallback.getByRole('button', { name: 'Choose project', exact: true }).click();
    await (await chooserPromise).setFiles(files[kind].path);
    await expect(fallback.getByText(files[kind].name, { exact: true })).toBeVisible();
    await assertImported(fallback, kind, files[kind].marker);
    step(`manual-${kind}-chooser-after-file-handling-revocation`);
  }
  for (const file of Object.values(files)) assert.equal(createHash('sha256').update(readFileSync(file.path)).digest('hex'), file.sha256);
  step('original-project-bytes-unchanged');
  report.manualInstallAndPermissionPrompt = 'Not performed: automated CDP installation and real Chrome app-settings revocation are separate evidence.';
  report.status = 'passed';
} catch (error) {
  report.status = 'failed'; report.failure = error.message; process.exitCode = 1;
  if (current) { await current.screenshot({ path: resolve(output, 'failure.png'), fullPage: true }).catch(() => {});
    report.failurePage = { url: current.url(), body: (await current.locator('body').innerText().catch(() => '')).slice(0, 3000) }; }
  console.error(error.message);
} finally {
  if (installed && session) {
    try {
      await session.send('PWA.uninstall', { manifestId: `${origin}/` });
      for (let i = 0; i < 40 && apps().length; i++) await delay(200);
      report.remainingAppBundles = apps(); assert.deepEqual(report.remainingAppBundles, []);
      report.defaultHandlersAfter = Object.fromEntries(['pdf', 'spreadsheet'].map(kind => [kind, defaultHandler(resolve(output, `synthetic-${kind}.${kind === 'pdf' ? 'pvadp' : 'pvads'}`))]));
      assert.deepEqual(report.defaultHandlersAfter, report.defaultHandlersBefore);
      step('real-pwa-uninstall-and-bundle-cleanup');
    } catch (error) { report.cleanupError = error.message; report.status = 'failed'; process.exitCode = 1; }
  }
  await context?.close(); report.uploads = uploads; save(); console.log(JSON.stringify(report));
}
