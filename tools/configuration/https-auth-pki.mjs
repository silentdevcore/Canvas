import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

export const hosts = ['account.pvad.test', 'designer.pvad.test', 'admin.pvad.test'];
export function publicOrigins(port) {
  assert.ok(Number.isInteger(port) && port > 0 && port <= 65535);
  return Object.fromEntries(hosts.map(host => [host.split('.')[0], `https://${host}:${port}`]));
}

export async function prepareCertificates(work) {
  const directory = resolve(work, 'certificates');
  await mkdir(directory);
  const run = args => execFileSync('openssl', args, { cwd: directory, stdio: 'pipe', timeout: 30_000 });
  run(['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-sha256', '-days', '1', '-subj', '/CN=PVAD isolated HTTPS test CA',
    '-addext', 'basicConstraints=critical,CA:TRUE', '-addext', 'keyUsage=critical,keyCertSign,cRLSign', '-keyout', 'ca.key', '-out', 'ca.crt']);
  for (const [name, names] of [['server', [...hosts, 'api.pvad.test', 'host.docker.internal', 'IP:127.0.0.1']], ['untrusted', ['untrusted.pvad.test']]]) {
    run(['req', '-new', '-newkey', 'rsa:2048', '-nodes', '-subj', `/CN=${names[0]}`, '-keyout', `${name}.key`, '-out', `${name}.csr`]);
    await writeFile(resolve(directory, `${name}.ext`), `subjectAltName=${names.map(value => value.startsWith('IP:') ? value : 'DNS:' + value).join(',')}\nbasicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth,clientAuth\n`);
    if (name === 'server') run(['x509', '-req', '-in', 'server.csr', '-CA', 'ca.crt', '-CAkey', 'ca.key', '-CAcreateserial',
      '-out', 'server.crt', '-days', '1', '-sha256', '-extfile', 'server.ext']);
    else run(['x509', '-req', '-in', 'untrusted.csr', '-signkey', 'untrusted.key', '-out', 'untrusted.crt', '-days', '1', '-sha256', '-extfile', 'untrusted.ext']);
  }
  run(['pkcs12', '-export', '-out', 'server.pfx', '-inkey', 'server.key', '-in', 'server.crt', '-passout', 'pass:']);
  return { directory, caSha256: createHash('sha256').update(await readFile(resolve(directory, 'ca.crt'))).digest('hex') };
}

export function tlsWebsiteConfiguration(template, { host, site, apiPort, routePattern }) {
  assert.ok(hosts.includes(host));
  assert.match(site, /^(account|designer|admin)$/);
  assert.ok(Number.isInteger(apiPort) && apiPort > 0 && apiPort <= 65535);
  assert.ok(routePattern.startsWith('^/') && routePattern.endsWith('$'));
  let value = template.replace('listen 8080;', 'listen 8443 ssl;')
    .replace('server_name _;', `server_name ${host};\n    ssl_certificate /certificates/server.crt;\n    ssl_certificate_key /certificates/server.key;\n    ssl_protocols TLSv1.2 TLSv1.3;`)
    .replace('root /usr/share/nginx/html;', `root /sites/${site};`)
    .replaceAll('${PVAD_API_UPSTREAM}', `https://host.docker.internal:${apiPort}`)
    .replaceAll('${PVAD_SPA_ROUTE_PATTERN}', routePattern)
    .replace('proxy_http_version 1.1;', `proxy_http_version 1.1;\n        proxy_ssl_verify on;\n        proxy_ssl_trusted_certificate /certificates/ca.crt;\n        proxy_ssl_name api.pvad.test;\n        proxy_ssl_server_name on;`);
  if (site === 'designer') value = value.replaceAll('include /etc/nginx/pvad/*.headers.conf;',
    'include /etc/nginx/pvad/designer.headers.conf;');
  else value = value.replaceAll('include /etc/nginx/pvad/*.headers.conf;', '');
  assert.ok(!value.includes('${PVAD_'));
  return value;
}
