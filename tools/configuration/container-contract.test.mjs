import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(resolve(root, 'pvad-designer/package.json'));

test('default Compose files contain only canonical names and consistent dependencies', async () => {
  const { load } = require('js-yaml');
  for (const path of ['docker-compose.yml', 'deploy/api/docker-compose.api.yml']) {
    const source = await readFile(resolve(root, path), 'utf8');
    assert.doesNotMatch(source, /pxa/i);
    const model = load(source);
    for (const [name, service] of Object.entries(model.services)) {
      assert.ok(name.startsWith('pvad-'), `${path}: ${name}`);
      const dependencies = Array.isArray(service.depends_on)
        ? service.depends_on : Object.keys(service.depends_on ?? {});
      for (const dependency of dependencies) assert.ok(model.services[dependency], dependency);
      const networks = Array.isArray(service.networks) ? service.networks : Object.keys(service.networks);
      for (const network of networks) {
        assert.ok(network === 'default' || Object.hasOwn(model.networks, network), network);
      }
    }
    const database = model.services['pvad-database'];
    assert.equal(database.environment.POSTGRES_DB, '${PVAD_DATABASE_NAME:-pvad}');
    assert.equal(database.environment.POSTGRES_USER, '${PVAD_DATABASE_USER:-pvad}');
    assert.ok(database.volumes.includes('pvad-database-data:/var/lib/postgresql/data'));
    assert.equal(model.volumes['pvad-database-data'], null);
    assert.match(database.healthcheck.test[1], /\$\$\{POSTGRES_USER\}/);
    assert.match(database.healthcheck.test[1], /\$\$\{POSTGRES_DB\}/);
  }
});

test('Compose overlays use canonical networks and matching secret mounts', async () => {
  const { load } = require('js-yaml');
  const read = async path => {
    const source = await readFile(resolve(root, path), 'utf8');
    assert.doesNotMatch(source, /pxa/i, path);
    return load(source);
  };
  const base = await read('docker-compose.yml');
  const alerting = await read('deploy/observability/docker-compose.alerting-production.yml');
  assert.deepEqual(Object.keys(alerting.services), ['pvad-alertmanager']);
  assert.ok(base.services['pvad-alertmanager']);
  assert.deepEqual(alerting.services['pvad-alertmanager'].secrets, ['pvad_alertmanager_smtp_password']);
  const webhook = await read('deploy/observability/docker-compose.webhook.yml');
  assert.deepEqual(Object.keys(webhook.services), ['pvad-webhook-relay']);
  const relay = webhook.services['pvad-webhook-relay'];
  for (const [network, settings] of Object.entries(relay.networks)) {
    assert.ok(Object.hasOwn(base.networks, network));
    assert.equal(settings.aliases, undefined);
  }
  assert.deepEqual(relay.secrets, ['pvad_alert_webhook_signing_key']);
  const api = (await read('deploy/api/docker-compose.api.yml')).services['pvad-webapi'];
  assert.equal(api.environment.Licensing__PrivateKeyPath, '/run/secrets/pvad_license_private_key');
  assert.equal(api.environment.Licensing__PublicKeyPath, '/run/secrets/pvad_license_public_key');
});

test('MCP gateway routes use declared canonical upstream services', async () => {
  const source = await readFile(resolve(root, 'deploy/api/mcp-gateway.conf.template'), 'utf8');
  assert.match(source, /proxy_pass http:\/\/pvad-mcp:3000\/mcp;/);
  assert.match(source, /proxy_pass http:\/\/pvad-mcp:3000;/);
  assert.match(source, /proxy_pass http:\/\/pvad-webapi:8080;/);
  assert.doesNotMatch(source, /http:\/\/pxa-/);
});

test('API persistent stores share the retained data mount while worker executables use canonical package names', async () => {
  const { load } = require('js-yaml');
  const compose = load(await readFile(resolve(root, 'deploy/api/docker-compose.api.yml'), 'utf8'));
  const api = compose.services['pvad-webapi'];
  assert.ok(api.volumes.includes('pvad-api-data:/app/data'));
  for (const key of ['DataProtection__KeysDirectory', 'Storage__RootPath',
    'PdfViewer__AnnotationStoragePath', 'PdfTrust__RevocationEvidence__CacheDirectory']) {
    assert.ok(api.environment[key].startsWith('/app/data/'), key);
  }
  assert.equal(api.environment.Ocr__WorkerPath, '/app/ocr-worker/PVAD.FileImporter.ImageOcr.Worker.dll');
  assert.equal(api.environment.CodeWorker__WorkerPath, '${PVAD_CODE_WORKER_PATH:-/app/code-worker}');
  const project = await readFile(resolve(root, 'PVAD.WebApi/PVAD.WebApi.csproj'), 'utf8');
  assert.match(project, /src\/Execution\/PVAD\.CodeWorker\/PVAD\.CodeWorker\.csproj/);
  assert.match(project, /src\/Importing\/PVAD\.FileImporter\.ImageOcr\.Worker\/PVAD\.FileImporter\.ImageOcr\.Worker\.csproj/);
  assert.match(project, /OutputPath\)code-worker\//);
  assert.match(project, /OutputPath\)ocr-worker\//);
});

test('container context excludes local credentials, generated probes and runtime state', async () => {
  const ignored = (await readFile(resolve(root, '.dockerignore'), 'utf8')).split(/\r?\n/);
  for (const path of ['.git', 'artifacts', '**/App_Data', '**/.aws', '**/.codex', '**/.env', '**/.env.*', 'deploy/api/secrets', 'deploy/observability/secrets'])
    assert.ok(ignored.includes(path), path);
  const status = spawnSync('git', ['check-ignore', '.dockerignore'], { cwd: root, encoding: 'utf8' });
  assert.equal(status.status, 1, 'Docker context protection must be included in version control, not locally ignored.');
});

test('non-root API PDF Trust cache stays inside the owned persistent data directory', async () => {
  const dockerfile = await readFile(resolve(root, 'PVAD.WebApi/Dockerfile'), 'utf8');
  const compose = await readFile(resolve(root, 'deploy/api/docker-compose.api.yml'), 'utf8');
  assert.match(dockerfile, /PdfTrust__RevocationEvidence__CacheDirectory=\/app\/data\/pdf-trust\/revocation-cache/);
  assert.match(compose, /PdfTrust__RevocationEvidence__CacheDirectory: \/app\/data\/pdf-trust\/revocation-cache/);
  assert.match(dockerfile, /chown -R "\$APP_UID:\$APP_UID" \/app\/data/);
  assert.match(dockerfile, /USER \$APP_UID/);
});

test('API image explicitly services the base-image zlib package alongside OCR dependencies', async () => {
  const dockerfile = await readFile(resolve(root, 'PVAD.WebApi/Dockerfile'), 'utf8');
  assert.match(dockerfile, /apt-get update[\s\S]*?apt-get install --yes --no-install-recommends[\s\S]*?\n\s+zlib1g\s+\\/);
});

test('isolated OCR worker resolves distro libraries through the wrapper names for both Linux architectures', async () => {
  const dockerfile = await readFile(resolve(root, 'PVAD.WebApi/Dockerfile'), 'utf8');
  assert.match(dockerfile, /amd64\) native_dir=\/usr\/lib\/x86_64-linux-gnu/);
  assert.match(dockerfile, /arm64\) native_dir=\/usr\/lib\/aarch64-linux-gnu/);
  for (const [system, wrapper] of [['liblept.so.5', 'libleptonica-1.82.0.so'], ['libtesseract.so.5', 'libtesseract50.so']]) {
    assert.ok(dockerfile.includes(`ln -s "$native_dir/${system}" /app/ocr-worker/native/x64/${wrapper}`));
    assert.ok(dockerfile.includes(`test -r /app/ocr-worker/native/x64/${wrapper}`));
  }
  assert.ok(dockerfile.includes('ln -s "$native_dir/libdl.so.2" /app/ocr-worker/libdl.so'));
  assert.ok(dockerfile.includes('test -r /app/ocr-worker/libdl.so'));
});

test('MCP container defaults do not conflict with user-supplied legacy or canonical hosts', () => {
  for (const environment of [{}, { PVAD_MCP_HOST: '127.0.0.1' }, { PXA_MCP_HOST: '127.0.0.1' }]) {
    const run = spawnSync('sh', [resolve(root, 'tools/PVAD.Mcp/scripts/container-entrypoint.sh'),
      process.execPath, '-p',
      'JSON.stringify({current:process.env.PVAD_MCP_HOST,legacy:process.env.PXA_MCP_HOST})'],
    { encoding: 'utf8', env: environment });
    assert.equal(run.status, 0, run.stderr);
    assert.deepEqual(JSON.parse(run.stdout), Object.keys(environment).length
      ? Object.fromEntries(Object.entries(environment).map(([key, value]) =>
        [key.startsWith('PVAD') ? 'current' : 'legacy', value]))
      : { current: '0.0.0.0' });
  }
});

test('both dotnet container definitions pass canonical build metadata and retain legacy argument inputs', async () => {
  for (const relative of ['PVAD.WebApi/Dockerfile',
    'src/Observability/PVAD.Observability.WebhookRelay/Dockerfile']) {
    const source = await readFile(resolve(root, relative), 'utf8');
    assert.match(source, /ARG PVAD_COMMIT=\$\{PXA_COMMIT:-unknown\}/);
    assert.match(source, /ARG PVAD_VERSION=\$\{PXA_VERSION:-1\.1\.0\}/);
    assert.match(source, /PvadBuildMetadata=\$PVAD_COMMIT/);
    assert.doesNotMatch(source, /PxaBuildMetadata=/);
    assert.match(source, /Conflicting PVAD_VERSION and PXA_VERSION/);
  }
});

test('local WebApi publish excludes runtime data independently of the Docker context', async () => {
  const project = await readFile(resolve(root, 'PVAD.WebApi/PVAD.WebApi.csproj'), 'utf8');
  assert.match(project, /<DefaultItemExcludes>\$\(DefaultItemExcludes\);App_Data\/\*\*\/\*<\/DefaultItemExcludes>/);
  assert.match(project, /CopyToPublishDirectory="PreserveNewest"/);
  assert.match(project, /AdminDocumentation/);
  assert.match(project, /assets\/fonts\/liberation/);
});
