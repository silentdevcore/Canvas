import { createServer } from 'node:http';

if (process.env.PVAD_ALLOW_FRONTEND_SMOKE !== 'true')
  throw new Error('This synthetic loopback API requires PVAD_ALLOW_FRONTEND_SMOKE=true.');
const server = createServer((request, response) => {
  response.setHeader('Content-Type', 'application/json');
  response.setHeader('Cache-Control', 'no-store');
  if (request.url === '/health/ready') return response.end('{"synthetic":true}');
  if (request.url === '/api/pvad/v1/rename-proxy-probe')
    return response.end(JSON.stringify({ synthetic: true, application: request.headers['x-pvad-application'] ?? null }));
  response.statusCode = 401;
  response.end(JSON.stringify({ status: 401, code: 'PXAAPI001', canonicalCode: 'PVADAPI001' }));
});
server.listen(15086, '127.0.0.1');
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close(() => process.exit()));
