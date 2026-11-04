import { createServer, request } from 'node:http';

const expectedIdentity = 'pvad-synthetic-operator';

async function readInput(stream, limit = 16_384) {
  const chunks = [];
  let size = 0;
  for await (const chunk of stream) {
    const bytes = Buffer.from(chunk);
    size += bytes.length;
    if (size > limit) throw new Error('Synthetic input limit exceeded.');
    chunks.push(bytes);
  }
  return Buffer.concat(chunks).toString('utf8');
}

export async function gatewayClient(input) {
  if (!['gateway', 'control'].includes(input.target) || typeof input.path !== 'string'
      || !input.path.startsWith('/') || /[\r\n]/.test(input.path)) throw new Error('Invalid synthetic client request.');
  const options = {
    hostname: input.target === 'control' ? '127.0.0.1' : 'pvad-gateway-lab',
    port: input.target === 'control' ? 8083 : 8080,
    path: input.path, method: input.method ?? 'GET', headers: input.headers ?? {},
  };
  return await new Promise((resolve, reject) => {
    const req = request(options, async response => {
      try {
        resolve({ status: response.statusCode, headers: response.headers, body: await readInput(response, 1_048_576) });
      } catch (error) { response.destroy(); reject(error); }
    });
    req.setTimeout(5_000, () => req.destroy(new Error('Synthetic client timeout.')));
    req.on('error', reject);
    if (input.body) req.write(input.body);
    req.end();
  });
}

export function startGatewayFixtures(protocol, controlToken) {
  const state = { auth: [], grafanaRequests: 0, grafanaCredentialsStripped: true,
    grafanaIdentityReplaced: true, apiIdentityHeadersStripped: true, uiRequests: 0 };
  const json = (response, status, value) => {
    response.writeHead(status, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(value));
  };
  const authHandler = (req, res) => {
    if (req.url === '/api/pvad/v1/admin/operator/access') {
      const cleared = ['authorization', ...protocol.credentialHeaders.map(value => value.toLowerCase())]
        .every(name => req.headers[name] === undefined);
      const decisions = new Map([
        ['lab_session=allowed', 204], ['lab_session=legacy', 204], ['lab_session=empty', 204],
        ['lab_session=forbidden', 403], ['lab_session=disabled', 403], ['lab_session=expired', 401],
        ['lab_session=error', 503], ['lab_session=redirect', 302],
      ]);
      const status = decisions.get(req.headers.cookie) ?? 401;
      state.auth.push({ status, credentialHeadersCleared: cleared,
        forwardedHostValid: req.headers['x-forwarded-host'] === 'operator.test',
        forwardedProtocolValid: req.headers['x-forwarded-proto'] === 'http' });
      if (req.headers.cookie === 'lab_session=allowed') res.setHeader('X-PVAD-Operator', expectedIdentity);
      if (['lab_session=allowed', 'lab_session=legacy'].includes(req.headers.cookie))
        res.setHeader(protocol.legacyOperatorHeader, expectedIdentity);
      res.writeHead(status);
      res.end();
      return;
    }
    if (req.url === '/api/pvad/v1/admin/operator/echo') {
      state.apiIdentityHeadersStripped &&= req.headers['x-webauth-user'] === undefined && req.headers['x-webauth-role'] === undefined;
      json(res, 200, { identityHeadersStripped: state.apiIdentityHeadersStripped,
        authorizationPreserved: req.headers.authorization === 'Bearer synthetic-api-credential' });
      return;
    }
    json(res, 404, { found: false });
  };
  let auth = createServer(authHandler);
  const ui = createServer((_req, res) => { state.uiRequests++; json(res, 200, { syntheticUi: true }); });
  const grafana = createServer((req, res) => {
    state.grafanaRequests++;
    const valid = req.headers['x-webauth-user'] === expectedIdentity && req.headers['x-webauth-role'] === 'Admin';
    state.grafanaIdentityReplaced &&= req.headers['x-webauth-user'] === expectedIdentity || req.headers['x-webauth-user'] === undefined;
    state.grafanaCredentialsStripped &&= req.headers.authorization === undefined && req.headers.cookie === undefined;
    if (!valid) { json(res, 401, { authenticated: false }); return; }
    res.setHeader('Set-Cookie', 'pvad_synthetic_grafana=value; Path=/; HttpOnly');
    json(res, 200, { authenticated: true });
  });
  const control = createServer(async (req, res) => {
    if (req.headers['x-lab-token'] !== controlToken) { json(res, 403, { allowed: false }); return; }
    if (req.url === '/state') { json(res, 200, state); return; }
    if (req.method === 'POST' && req.url === '/auth/stop') {
      auth.closeAllConnections();
      await new Promise(resolve => auth.close(resolve));
      json(res, 200, { stopped: true });
      return;
    }
    if (req.method === 'POST' && req.url === '/auth/start') {
      auth = createServer(authHandler);
      await new Promise(resolve => auth.listen(8081, '0.0.0.0', resolve));
      json(res, 200, { started: true });
      return;
    }
    json(res, 404, { found: false });
  });
  auth.listen(8081, '0.0.0.0'); ui.listen(8082, '0.0.0.0'); grafana.listen(3000, '0.0.0.0'); control.listen(8083, '0.0.0.0');
  return [auth, ui, grafana, control];
}

if (process.argv[2] === 'client') {
  try { console.log(JSON.stringify(await gatewayClient(JSON.parse(await readInput(process.stdin))))); }
  catch { console.error('Synthetic HTTP request failed.'); process.exitCode = 1; }
} else if (process.argv[2] === 'serve') {
  startGatewayFixtures(JSON.parse(process.env.PVAD_LAB_PROTOCOL), process.env.PVAD_LAB_CONTROL_TOKEN);
}
