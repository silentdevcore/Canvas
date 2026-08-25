import crypto from 'node:crypto';
import http from 'node:http';

const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const publicJwk = publicKey.export({ format: 'jwk' });
const keyId = 'pxa-mcp-smoke-key';
const issuer = 'https://account.pxa.test';
const audience = process.env.PXA_MCP_SMOKE_RESOURCE ?? 'https://localhost:3443/mcp';

const server = http.createServer((request, response) => {
  if (request.url === '/health/ready') return json(response, 200, { status: 'ready' });
  if (request.url === '/.well-known/jwks.json') {
    return json(response, 200, { keys: [{ ...publicJwk, kid: keyId, use: 'sig', alg: 'RS256' }] });
  }
  if (request.url === '/token') {
    const now = Math.floor(Date.now() / 1000);
    return json(response, 200, {
      access_token: signJwt({
        iss: issuer,
        aud: audience,
        sub: 'smoke-user',
        client_id: 'smoke-client',
        pxa_organization: 'smoke-organization',
        scope: 'pxa.docs.read pxa.templates.read pxa.documents.validate',
        iat: now,
        exp: now + 300,
      }),
      token_type: 'Bearer',
      expires_in: 300,
    });
  }
  json(response, 404, { error: 'not_found' });
});

server.listen(8080, '0.0.0.0');

function signJwt(payload) {
  const header = Buffer.from(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid: keyId })).toString('base64url');
  const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const data = `${header}.${body}`;
  const signature = crypto.sign('RSA-SHA256', Buffer.from(data), privateKey).toString('base64url');
  return `${data}.${signature}`;
}

function json(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, {
    'content-type': 'application/json',
    'content-length': Buffer.byteLength(body),
  });
  response.end(body);
}
