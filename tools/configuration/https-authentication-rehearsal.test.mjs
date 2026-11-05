import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { X509Certificate } from 'node:crypto';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { apiListeningAddress } from './operator-browser-rehearsal.mjs';
import { hosts, prepareCertificates, publicOrigins, tlsWebsiteConfiguration } from './https-auth-pki.mjs';
import { runHttpsAuthentication } from './https-authentication-rehearsal.mjs';
import { publicGuidesPlugin } from '../../websites/PVAD.Documentation/publicGuides.js';
import { safeFailureEvidence, screenshotNames, validateBrowserEvidence } from './https-auth-evidence.mjs';

const source = name => readFile(new URL(name, import.meta.url), 'utf8');

test('the three HTTPS hosts are separate and no arbitrary target is accepted', () => {
  assert.deepEqual(Object.keys(publicOrigins(8443)), ['account', 'designer', 'admin']);
  assert.equal(publicOrigins(8443).designer, 'https://designer.pvad.test:8443');
  for (const port of [0, -1, 65536, '8443', 3.5]) assert.throws(() => publicOrigins(port));
});

test('HTTPS lifetime parsing is explicit and never widens the existing HTTP listener contract', () => {
  const event = { category: 'Microsoft.Hosting.Lifetime', eventName: 'ListeningOnAddress', attributes: { address: 'https://127.0.0.1:12345' } };
  assert.equal(apiListeningAddress(JSON.stringify(event)), null);
  assert.equal(apiListeningAddress(JSON.stringify(event), 'https:'), event.attributes.address);
  for (const address of ['https://external.test:443', 'https://127.0.0.1:0', 'https://user@127.0.0.1:12345', 'https://127.0.0.1:12345/path'])
    assert.equal(apiListeningAddress(JSON.stringify({ ...event, attributes: { address } }), 'https:'), null);
});

test('the real website template retains API handling and validates second-hop TLS', async () => {
  const template = await source('../../deploy/websites/nginx.conf.template');
  for (const site of ['account', 'designer', 'admin']) {
    const options = { host: `${site}.pvad.test`, site, apiPort: 12345, routePattern: '^/(login|dashboard|pdf/create)$' };
    const result = tlsWebsiteConfiguration(template, options);
    assert.match(result, /listen 8443 ssl/);
    assert.match(result, /proxy_pass https:\/\/host\.docker\.internal:12345/);
    assert.match(result, /proxy_ssl_verify on/);
    assert.match(result, /proxy_ssl_name api\.pvad\.test/);
    assert.match(result, /proxy_intercept_errors off/);
    assert.ok(result.includes(`root /sites/${site};`));
    assert.equal(result.includes('include /etc/nginx/pvad/designer.headers.conf;'), site === 'designer');
    assert.throws(() => tlsWebsiteConfiguration(template, { ...options, host: 'installed.example' }));
    assert.throws(() => tlsWebsiteConfiguration(template, { ...options, apiPort: 0 }));
    assert.throws(() => tlsWebsiteConfiguration(template, { ...options, routePattern: 'unsafe' }));
  }
});

test('private test certificates validate only intended hosts and the deliberate negative leaf is untrusted', async () => {
  const work = await mkdtemp(join(tmpdir(), 'pvad-https-pki-'));
  try {
    const result = await prepareCertificates(work);
    const ca = new X509Certificate(await readFile(join(result.directory, 'ca.crt')));
    const leaf = new X509Certificate(await readFile(join(result.directory, 'server.crt')));
    const untrusted = new X509Certificate(await readFile(join(result.directory, 'untrusted.crt')));
    assert.ok(ca.ca && leaf.verify(ca.publicKey));
    for (const host of [...hosts, 'api.pvad.test', 'host.docker.internal']) assert.equal(leaf.checkHost(host), host);
    assert.equal(leaf.checkIP('127.0.0.1'), '127.0.0.1');
    assert.equal(leaf.checkHost('wrong-host.pvad.test'), undefined);
    assert.ok(!untrusted.verify(ca.publicKey));
    assert.match(result.caSha256, /^[a-f0-9]{64}$/);
  } finally { await rm(work, { recursive: true, force: true }); }
});

test('missing cached images fail before any resource or build can be created', async () => {
  const calls = [];
  await assert.rejects(runHttpsAuthentication({ docker: args => { calls.push(args); return 'missing'; } }), /Cached images/);
  assert.deepEqual(calls.map(value => value.slice(0, 2)), [['image', 'inspect']]);
});

test('CLI rejects installed-resource and deployment arguments before Docker access', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/https-authentication-rehearsal.mjs', '--apply'],
    { encoding: 'utf8', env: { ...process.env, DOCKER_HOST: 'unix:///nonexistent-synthetic.sock' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No arguments or installed resource names/);
});

test('browser validation uses real UI, isolated trust and no ignored certificates or saved sessions', async () => {
  const browser = await source('./https-auth-browser/scenarios.mjs');
  assert.match(browser, /ignoreHTTPSErrors: false/);
  assert.match(browser, /ERR_CERT_AUTHORITY_INVALID\|ERR_CERT_COMMON_NAME_INVALID/);
  assert.match(browser, /'certutil', \['-A'/);
  assert.match(browser, /#login-form/);
  assert.match(browser, /getByLabel\('Email or username'\)/);
  assert.match(browser, /sessionsBeforeMigration/);
  assert.match(browser, /designer-handoff\/exchange/);
  assert.doesNotMatch(browser, /ignoreHTTPSErrors: true|ignore-certificate-errors|route\.fulfill|storageState\(|tracing\.start|recordVideo/);
});

test('recovery reads ciphertext created before backup and the unchanged retention gate remains enforced', async () => {
  const runner = await source('./https-authentication-rehearsal.mjs');
  const helper = await source('../identity-browser-rehearsal/Program.cs');
  assert.match(runner, /step\('production-startup-denial'\)/);
  assert.match(runner, /RetentionApprovalRequired\|PVADSTART001/);
  assert.match(runner, /mode = 'Staging'/);
  assert.match(runner, /Action: 'create-protected-data'/);
  assert.match(runner, /pg_dump/);
  assert.match(runner, /pg_restore/);
  assert.match(runner, /assert\.deepEqual\(dataSnapshot\(\), backupSnapshot\)/);
  assert.match(helper, /File\.ReadAllTextAsync\(Path\.Combine\(request\.WorkDirectory, "protected-consumers.json"\)\)/);
  assert.match(helper, /value\.RevokedAt == null/);
  assert.match(helper, /PvadDataProtection\.ApplicationName/);
  assert.doesNotMatch(runner + helper, /ProductionApproved.*true|security.*add-trusted-cert|ignoreHTTPSErrors: true/);
});

test('synthetic organization changes respect the actual text-backed enum contract', async () => {
  const runner = await source('./https-authentication-rehearsal.mjs');
  const mapping = await source('../../src/Infrastructure/PVAD.Infrastructure.Persistence/Configurations/OrganizationConfiguration.cs');
  assert.match(mapping, /organization\.Status\)\s*\.HasConversion<string>\(\)/);
  assert.ok(runner.includes('SET "Status" = \'Active\''));
  assert.ok(runner.includes('SET "Status" = \'Suspended\''));
  assert.doesNotMatch(runner, /SET "Status" = [01]/);
});

test('internal recovery guidance stays outside public Documentation and DocFX output', async () => {
  const emitted = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: value => emitted.push(value.fileName) });
  assert.ok(!emitted.includes('docs/pvad-https-authentication-rehearsal.md'));
  assert.ok(!emitted.some(value => /identity-browser-rehearsal|operator-docs/.test(value)));
  const docfx = JSON.parse(await source('../../docs/docfx.json'));
  const markdown = docfx.build.content.flatMap(value => value.files).filter(value => value.endsWith('.md'));
  assert.ok(!markdown.includes('pvad-https-authentication-rehearsal.md'));
  assert.ok(!markdown.some(value => ['**.md', '**/*.md', '*.md'].includes(value)));
  const guide = await source('../../docs/pvad-https-authentication-rehearsal.md');
  assert.match(guide, /Positive browser scenarios use `Staging`/);
  assert.match(guide, /not a deployment procedure or production approval/);
});

test('failure evidence removes URL credentials, callback parameters, fragments and known secrets', () => {
  const secret = 'synthetic secret!';
  const error = new Error(`Failed https://user:password@designer.pvad.test/auth/callback?code=one-time&state=private#token ${secret} ${encodeURIComponent(secret)}`);
  error.stack += '\n    at https://designer.pvad.test/auth/callback?code=another-secret';
  const serialized = JSON.stringify(safeFailureEvidence(error, [secret]));
  for (const value of ['one-time', 'private', 'user:', 'password@', '#token', secret, encodeURIComponent(secret), 'another-secret'])
    assert.ok(!serialized.includes(value), value);
  assert.ok(serialized.includes('https://designer.pvad.test/auth/callback'));
  assert.ok(safeFailureEvidence({ name: 'unsafe', message: 'x'.repeat(10_000), stack: '' }).message.length <= 2000);
});

test('incomplete, duplicate, mocked, unsafe-TLS or missing-screenshot evidence cannot pass', () => {
  const valid = { checks: Array.from({ length: 12 }, (_, index) => 'scenario-' + String.fromCharCode(97 + index)),
    screenshots: screenshotNames, tlsValidationDisabled: false, apiMocking: false, hostOnlyCookiesVerified: true,
    platform: 'isolated-linux-chromium' };
  validateBrowserEvidence(valid);
  for (const changed of [{ checks: [] }, { checks: Array(12).fill('duplicate') }, { screenshots: screenshotNames.slice(1) },
    { tlsValidationDisabled: true }, { apiMocking: true }, { hostOnlyCookiesVerified: false }, { platform: 'unverified' }])
    assert.throws(() => validateBrowserEvidence({ ...valid, ...changed }));
});
