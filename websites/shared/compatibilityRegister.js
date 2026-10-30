import { readFileSync } from 'node:fs';

export const publicCompatibilityRegisterPath = 'docs/reference/compatibility-identifiers.v1.json';
const source = new URL(`../../${publicCompatibilityRegisterPath}`, import.meta.url);

export function readPublicCompatibilityRegister() {
  const bytes = readFileSync(source);
  const register = JSON.parse(bytes);
  if (register.schemaVersion !== 1 || register.version !== '1' || register.visibility !== 'public')
    throw new Error('The public compatibility register has invalid publication metadata.');
  return bytes;
}

export function publicCompatibilityRegisterPlugin() {
  return {
    name: 'pvad-public-compatibility-reference',
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: publicCompatibilityRegisterPath,
        source: readPublicCompatibilityRegister() });
    },
    configureServer(server) {
      server.middlewares.use((request, response, next) => {
        if (request.url?.split('?')[0] !== '/' + publicCompatibilityRegisterPath) return next();
        if (!['GET', 'HEAD'].includes(request.method)) {
          response.writeHead(405, { Allow: 'GET, HEAD' }); response.end(); return;
        }
        const bytes = readPublicCompatibilityRegister();
        response.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-cache' });
        response.end(request.method === 'HEAD' ? undefined : bytes);
      });
    },
  };
}
