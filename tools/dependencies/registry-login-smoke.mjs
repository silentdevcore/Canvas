import assert from 'node:assert/strict';
import { execFileSync, fork } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { createServer } from 'node:http';
import { createServer as createSecureServer } from 'node:https';
import { appendFile, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(import.meta.url);

// Implements only the authenticated Registry V2 ping: no images or publishing.
export function createRegistry(username, password, tlsOptions) {
  const authorization = `Basic ${Buffer.from(`${username}:${password}`).toString('base64')}`;
  const handler = (request, response) => {
    response.setHeader('Docker-Distribution-Api-Version', 'registry/2.0');
    if (request.headers.authorization !== authorization) {
      response.writeHead(401, { 'WWW-Authenticate': 'Basic realm="synthetic-registry"' });
    } else {
      response.writeHead(request.url === '/v2/' || request.url === '/v2' ? 200 : 404);
    }
    response.end('{}');
  };
  return tlsOptions ? createSecureServer(tlsOptions, handler) : createServer(handler);
}

export function verifyCredentials(config, state) {
  assert.deepEqual(Object.keys(config.auths ?? {}), [state.registry], 'Credentials must be registry-scoped');
  assert.equal(config.auths[state.registry].auth,
    Buffer.from(`${state.username}:${state.password}`).toString('base64'));
  assert.equal(config.credsStore, undefined, 'Synthetic test must not use a shared credential store');
}

function directory() {
  const path = process.env.PXA_REGISTRY_SMOKE_DIR;
  assert.ok(path, 'PXA_REGISTRY_SMOKE_DIR is required');
  const result = resolve(path);
  assert.equal(resolve(process.env.DOCKER_CONFIG ?? ''), join(result, 'docker'),
    'DOCKER_CONFIG must be isolated inside the synthetic directory');
  return result;
}

async function state() {
  return JSON.parse(await readFile(join(directory(), 'state.json'), 'utf8'));
}

async function start() {
  const path = directory();
  await mkdir(path, { mode: 0o700 });
  await mkdir(join(path, 'docker'), { mode: 0o700 });
  execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes',
    '-keyout', join(path, 'key.pem'), '-out', join(path, 'ca.pem'), '-days', '1',
    '-subj', '/CN=synthetic-registry', '-addext', 'subjectAltName=IP:127.0.0.1'], { stdio: 'pipe' });
  const credentials = { username: 'synthetic', password: randomBytes(32).toString('hex') };
  await writeFile(join(path, 'state.json'), JSON.stringify(credentials), { mode: 0o600 });
  const child = fork(script, ['serve'], { detached: true, stdio: ['ignore', 'ignore', 'ignore', 'ipc'] });
  let timeout;
  try {
    const registry = await new Promise((accept, reject) => {
      timeout = setTimeout(() => reject(new Error('Synthetic registry startup timed out')), 10_000);
      child.once('error', reject);
      child.once('exit', () => reject(new Error('Synthetic registry exited during startup')));
      child.once('message', accept);
    });
    clearTimeout(timeout);
    await writeFile(join(path, 'state.json'), JSON.stringify({ ...credentials, registry, pid: child.pid }), { mode: 0o600 });
    // An explicit auth entry prevents Docker from auto-selecting a host credential helper.
    await writeFile(join(path, 'docker/config.json'),
      JSON.stringify({ auths: { [registry]: { auth: '' } } }), { mode: 0o600 });
    if (process.env.GITHUB_OUTPUT) {
      console.log(`::add-mask::${credentials.password}`);
      await appendFile(process.env.GITHUB_OUTPUT,
        `registry=${registry}\nusername=${credentials.username}\npassword=${credentials.password}\n`);
    }
    console.log('Loopback-only synthetic registry is ready.');
  } catch (error) {
    child.kill();
    throw error;
  } finally {
    clearTimeout(timeout);
    if (child.connected) child.disconnect();
    child.unref();
  }
}

async function serve() {
  const credentials = await state();
  const server = createRegistry(credentials.username, credentials.password, {
    key: await readFile(join(directory(), 'key.pem')),
    cert: await readFile(join(directory(), 'ca.pem')),
  });
  await new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', accept);
  });
  process.send(`127.0.0.1:${server.address().port}`);
  process.once('SIGTERM', () => server.close(() => process.exit(0)));
}

async function verify() {
  const credentials = await state();
  const configPath = join(directory(), 'docker/config.json');
  verifyCredentials(JSON.parse(await readFile(configPath, 'utf8')), credentials);
  assert.throws(() => execFileSync('docker', ['login', credentials.registry,
    '--username', credentials.username, '--password-stdin'], {
    input: `${randomBytes(32).toString('hex')}\n`, stdio: ['pipe', 'pipe', 'pipe'], timeout: 30_000,
  }), (error) => error.status !== 0 && /unauthorized|401/iu.test(String(error.stderr)),
  'The registry must reject an invalid password, not merely fail to connect');
  verifyCredentials(JSON.parse(await readFile(configPath, 'utf8')), credentials);
  console.log('Registry-scoped login and invalid-password rejection verified.');
}

async function cleanup() {
  let credentials;
  try {
    credentials = await state();
  } catch (error) {
    if (error.code === 'ENOENT') return;
    throw error;
  }
  try {
    if (credentials.registry) {
      execFileSync('docker', ['logout', credentials.registry], { stdio: 'pipe', timeout: 30_000 });
      const config = JSON.parse(await readFile(join(directory(), 'docker/config.json'), 'utf8'));
      assert.equal(config.auths?.[credentials.registry], undefined, 'Logout must remove synthetic credentials');
      console.log('Synthetic registry logout verified.');
    }
  } finally {
    if (credentials.pid) {
      try { process.kill(credentials.pid, 'SIGTERM'); } catch (error) {
        if (error.code !== 'ESRCH') throw error;
      }
    }
    await rm(directory(), { recursive: true });
  }
}

if (process.argv[1] && resolve(process.argv[1]) === script) {
  const commands = { start, serve, verify, cleanup };
  assert.ok(Object.hasOwn(commands, process.argv[2]), 'Expected start, serve, verify or cleanup');
  await commands[process.argv[2]]();
}
