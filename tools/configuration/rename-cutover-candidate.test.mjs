import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { prepareCandidate, fileTypeDescriptors, canonicalComposeCandidate } from './rename-cutover-candidate.mjs';
import { createRequire } from 'node:module';
import { resolveSiteLinks } from '../../websites/shared/siteLinks.js';

const profile = () => JSON.parse(readFileSync(new URL('../../deploy/rename/pvade-targets.candidate.json', import.meta.url)));
const formats = () => JSON.parse(readFileSync(new URL('../../product-metadata/native-file-types.json', import.meta.url)));
const version = readFileSync(new URL('../../VERSION', import.meta.url), 'utf8').trim();
const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
const yaml = require('js-yaml');

test('one offline candidate aligns links, Designer handoff, CORS, registration and transactional mail', () => {
  const input = profile(), original = JSON.stringify(input);
  const result = prepareCandidate(input, version);
  assert.equal(JSON.stringify(input), original);
  const links = resolveSiteLinks('production', result.frontendEnvironment);
  for (const [role, value] of Object.entries(links)) assert.equal(value, `${result.hosts[role]}/`);
  assert.equal(result.designerRuntime.__PVAD_CONFIG__.accountUrl, links.account);
  assert.deepEqual(result.apiSettings.DesignerAuthentication.AllowedOrigins, [result.hosts.designer]);
  assert.deepEqual(result.apiSettings.Cors.AllowedOrigins, [result.hosts.api]);
  for (const role of ['company', 'designer', 'admin', 'operator'])
    assert.ok(!result.apiSettings.Cors.AllowedOrigins.includes(result.hosts[role]));
  assert.equal(result.apiSettings.Mail.AccountBaseUrl + '/', links.account);
  assert.equal(result.apiSettings.Mail.SupportUrl, 'https://pvade.com/support.html');
  for (const host of ['localhost', '127.0.0.1', 'pvad-webapi', 'pxa-webapi'])
    assert.ok(result.apiSettings.AllowedHosts.split(';').includes(host));
  assert.equal(result.apiSettings.Mail.Transport, 'Disabled');
  assert.equal(result.apiSettings.Mail.SenderAddress, input.senderAddress);
  assert.notEqual(result.apiSettings.Mail.SenderAddress, input.supportAddress);
  assert.equal(result.apiSettings.OAuth.Enabled, false);
  assert.equal(result.apiSettings.OAuth.Issuer + '/', links.account);
  assert.equal(result.apiSettings.OAuth.McpResource, 'https://mcp.pvade.com/mcp');
  for (const role of ['admin', 'operator']) assert.ok(!result.apiSettings.Registration.AllowedReturnOrigins.includes(result.hosts[role]));
  assert.ok(!Object.keys(result.frontendEnvironment).some(key => key.includes('ADMIN')));
  assert.deepEqual(result.images, { api: `ghcr.io/silentdevcore/pvad-webapi:${version}`,
    relay: `ghcr.io/silentdevcore/pvad-observability-webhook-relay:${version}` });
  assert.equal(result.deploymentAuthorized, false);
  assert.deepEqual(result.blockers.filter(value => value.startsWith('publisher-unselected:')), ['publisher-unselected:nuget', 'publisher-unselected:npm']);
  assert.equal(result.apiSettings.OAuth.ApiResource, undefined);
  assert.equal(result.apiSettings.DataProtection, undefined);
});

test('publisher selection computes candidate image names but never asserts ownership or authorizes deployment', () => {
  const input = profile();
  input.publishers = { nuget: 'Synthetic', npm: '@synthetic', container: 'ghcr.io/synthetic' };
  input.webhookUrl = 'https://webhook.example.test/events';
  const result = prepareCandidate(input, version);
  assert.equal(result.images.api, `ghcr.io/synthetic/pvad-webapi:${version}`);
  assert.equal(result.images.relay, `ghcr.io/synthetic/pvad-observability-webhook-relay:${version}`);
  assert.equal(result.deploymentAuthorized, false);
  assert.ok(result.blockers.includes('live-dns-tls-mail-key-and-ownership-validation-required'));
});

for (const value of ['http://designer.pvade.com', 'https://user:private@designer.pvade.com',
  'https://designer.pvade.com/path', 'https://designer.pvade.com?private=value',
  'https://designer.pvade.com#fragment', 'https://designer.pvade.com\\path', 'https://*.pvade.com', 'https://designer.pvade.com\n']) {
  test(`candidate rejects an invalid host without disclosing it: ${JSON.stringify(value).slice(0,28)}`, () => {
    const input = profile(); input.hosts.designer = value;
    assert.throws(() => prepareCandidate(input, version), { message: 'Invalid candidate host: designer.' });
  });
}

test('candidate rejects incomplete, duplicate, cross-domain and non-origin host sets', () => {
  const missing = profile(); delete missing.hosts.account;
  assert.throws(() => prepareCandidate(missing, version), /account/);
  const duplicate = profile(); duplicate.hosts.designer = duplicate.hosts.account;
  assert.throws(() => prepareCandidate(duplicate, version), /distinct/);
  const external = profile(); external.hosts.account = 'https://pvade.com.evil.test';
  assert.throws(() => prepareCandidate(external, version), /outside/);
  const wrong = profile(); wrong.status = 'published';
  assert.throws(() => prepareCandidate(wrong, version), /metadata/);
  const address = profile(); address.supportAddress = 'private\n@example.test';
  assert.throws(() => prepareCandidate(address, version), /support/);
});

test('publisher and webhook validation rejects injected credentials or commands', () => {
  for (const [kind, value] of [['npm', '@scope;touch'], ['container', 'https://private@registry.test/scope'], ['nuget', 'a\nprivate']]) {
    const input = profile(); input.publishers[kind] = value;
    assert.throws(() => prepareCandidate(input, version), { message: `Invalid candidate publisher: ${kind}.` });
  }
  for (const value of ['http://webhook.test/events', 'https://user:private@webhook.test/events', 'https://webhook.test/events?secret=value']) {
    const input = profile(); input.webhookUrl = value;
    assert.throws(() => prepareCandidate(input, version), { message: 'Invalid candidate webhook URL.' });
  }
});

test('candidate requires a valid sender independently of support, without echoing rejected values', () => {
  for (const value of [undefined, null, '', 'support@example.test', 'sender@pvade.com.evil.test',
    'sender@pvade.com\n', 'sender@pvade.com\r\nBcc:private@example.test', 'Name <sender@pvade.com>']) {
    const input = profile();
    input.senderAddress = value;
    assert.throws(() => prepareCandidate(input, version), { message: 'Invalid candidate sender address.' });
  }
  const input = profile();
  input.supportAddress = 'help@pvade.com';
  assert.equal(prepareCandidate(input, version).apiSettings.Mail.SenderAddress, 'no-reply@pvade.com');
});

test('native type descriptors match canonical Core identities and do not invent a working native opener', () => {
  const input = formats(), result = fileTypeDescriptors(input);
  const core = readFileSync(new URL('../../src/Core/PVAD.Core/ProjectFormats/PvadProjectModels.cs', import.meta.url), 'utf8');
  for (const type of input.types) {
    assert.ok(core.includes(type.extension));
    assert.ok(core.includes(type.mediaType));
    assert.ok(result.linuxSharedMimeXml.includes(`pattern="*${type.extension}"`));
  }
  assert.doesNotMatch(result.linuxSharedMimeXml, /<magic|\.zip"/);
  assert.deepEqual(result.macExportedTypes.map(type => type.UTTypeTagSpecification['public.filename-extension']), [['pvadp'], ['pvads']]);
  assert.deepEqual(result.windowsTypes.map(type => type.progId), ['PVAD.Project.Pdf', 'PVAD.Project.Spreadsheet']);
  assert.equal(result.nativeFileOpenSupported, false);
  assert.deepEqual(result.installCommands, []);
  assert.deepEqual(result.openCommands, []);
  input.types[0].description = '<script>invalid</script>';
  assert.throws(() => fileTypeDescriptors(input), /identity/);
});

test('the CLI is read-only and fails readiness instead of faking external approval', () => {
  const script = new URL('./rename-cutover-candidate.mjs', import.meta.url).pathname;
  const source = readFileSync(script, 'utf8');
  assert.doesNotMatch(source, /writeFile|spawnSync|execFile|fetch\(/);
  const plan = JSON.parse(execFileSync(process.execPath, [script], { encoding: 'utf8' }));
  assert.equal(plan.deploymentAuthorized, false);
  const blocked = spawnSync(process.execPath, [script, '--require-ready'], { encoding: 'utf8' });
  assert.equal(blocked.status, 2);
  const unknown = spawnSync(process.execPath, [script, '--deploy'], { encoding: 'utf8' });
  assert.equal(unknown.status, 1);
  assert.equal(unknown.stdout, '');
});

test('canonical API service candidate preserves source volumes, keys, network settings and DNS', () => {
  const source = yaml.load(readFileSync(new URL('../../deploy/api/docker-compose.api.yml', import.meta.url), 'utf8'));
  const original = JSON.stringify(source);
  const bindings = { 'pvad-api-data': 'synthetic-original-api-data', 'pvad-database-data': 'synthetic-original-database-data' };
  const result = canonicalComposeCandidate(source, bindings);
  assert.equal(JSON.stringify(source), original);
  assert.deepEqual(Object.keys(result.compose.services), ['pvad-database', 'pvad-webapi', 'pvad-mcp', 'pvad-mcp-gateway']);
  const api = result.compose.services['pvad-webapi'];
  assert.deepEqual(api.depends_on, { 'pvad-database': { condition: 'service_healthy' } });
  assert.deepEqual(api.volumes, source.services['pvad-webapi'].volumes);
  assert.deepEqual(api.environment, source.services['pvad-webapi'].environment);
  assert.deepEqual(api.secrets, source.services['pvad-webapi'].secrets);
  assert.deepEqual(api.networks.default.aliases, ['pvad-webapi']);
  assert.equal(api.environment.ConnectionStrings__PvadDatabase, source.services['pvad-webapi'].environment.ConnectionStrings__PvadDatabase);
  for (const [logical, name] of Object.entries(bindings)) assert.deepEqual(result.compose.volumes[logical], { external: true, name });
  assert.equal(result.deploymentAuthorized, false);
  assert.ok(result.requirements.includes('stop-original-writers-before-cutover'));
});

test('actual Docker Compose accepts the candidate without starting services or creating volumes', t => {
  const available = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' });
  if (available.status !== 0) { t.skip('Docker Compose is unavailable; no real configuration validation claimed.'); return; }
  const source = yaml.load(readFileSync(new URL('../../deploy/api/docker-compose.api.yml', import.meta.url), 'utf8'));
  const { compose } = canonicalComposeCandidate(source, { 'pvad-api-data': 'synthetic-original-api-data', 'pvad-database-data': 'synthetic-original-database-data' });
  const configured = spawnSync('docker', ['compose', '--project-directory', new URL('../../deploy/api', import.meta.url).pathname,
    '-p', 'pvad-offline-candidate', '-f', '-', '--profile', 'mcp', 'config', '--format', 'json'], {
    input: yaml.dump(compose), encoding: 'utf8',
    env: { PATH: process.env.PATH, HOME: process.env.HOME,
      PVAD_LICENSE_PRIVATE_KEY_FILE: '/tmp/pvad-synthetic-private.pem', PVAD_LICENSE_PUBLIC_KEY_FILE: '/tmp/pvad-synthetic-public.pem' },
  });
  assert.equal(configured.status, 0, configured.stderr);
  const model = JSON.parse(configured.stdout);
  assert.equal(model.volumes['pvad-api-data'].name, 'synthetic-original-api-data');
  assert.equal(model.volumes['pvad-api-data'].external, true);
  assert.ok(model.services['pvad-webapi'].networks.default.aliases.includes('pvad-webapi'));
});

test('observability preparation preserves configured IPAM, network settings, rule paths and stored histories', () => {
  const source = yaml.load(readFileSync(new URL('../../docker-compose.yml', import.meta.url), 'utf8'));
  const bindings = Object.fromEntries(Object.keys(source.volumes).map((name, index) => [name, `synthetic-retained-volume-${index}`]));
  const { compose } = canonicalComposeCandidate(source, bindings);
  assert.deepEqual(compose.networks, source.networks);
  for (const [oldName, original] of Object.entries(source.services)) {
    const service = compose.services[oldName.replace(/^pxa-/, 'pvad-')];
    assert.ok(service, oldName);
    for (const field of ['image', 'volumes', 'environment', 'command', 'secrets', 'ports', 'read_only', 'security_opt'])
      assert.deepEqual(service[field], original[field], `${oldName}.${field}`);
  }
});

test('legacy service preparation retains its DNS alias and rewrites dependencies', () => {
  const source = { services: {
    'pxa-api': { depends_on: ['pxa-database'] },
    'pxa-database': {},
  }, volumes: { retained: {} } };
  const { compose } = canonicalComposeCandidate(source, { retained: 'synthetic-original-volume' });
  assert.deepEqual(Object.keys(compose.services), ['pvad-api', 'pvad-database']);
  assert.deepEqual(compose.services['pvad-api'].depends_on, ['pvad-database']);
  assert.deepEqual(compose.services['pvad-api'].networks.default.aliases, ['pxa-api', 'pvad-api']);
});

test('service preparation fails on missing volume IDs, collisions and unsupported bindings', () => {
  const model = { services: { 'pxa-api': {} }, volumes: { retained: {} } };
  assert.throws(() => canonicalComposeCandidate(model, {}), /volume binding/);
  assert.throws(() => canonicalComposeCandidate(model, { retained: 'bad/value' }), /volume binding/);
  assert.throws(() => canonicalComposeCandidate(model, { retained: 'original', extra: 'unknown' }), /Unknown retained/);
  assert.throws(() => canonicalComposeCandidate({ ...model, services: { 'pxa-api': {}, 'pvad-api': {} } }, { retained: 'original' }), /collide/);
  assert.throws(() => canonicalComposeCandidate({ ...model, services: { 'pxa-api': { container_name: 'existing' } } }, { retained: 'original' }), /unsupported/);
  assert.throws(() => canonicalComposeCandidate({ ...model, services: {
    'pxa-api': {}, other: { networks: { default: { aliases: ['pvad-api'] } } },
  } }, { retained: 'original' }), /aliases collide/);
});
