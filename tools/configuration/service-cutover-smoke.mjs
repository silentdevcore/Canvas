import { execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export async function runServiceCutover(docker = (args, env = {}) => execFileSync('docker', args,
  { encoding: 'utf8', env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] }).trim()) {
  const id = randomUUID();
  const volume = `pvad-cutover-${id}`;
  const legacy = `pxa-smoke-${id}`, current = `pvad-smoke-${id}`;
  const label = 'pvade.rename-smoke';
  const owned = (kind, name) => {
    try { return docker([kind, 'inspect', '--format', `{{index .${kind === 'volume' ? 'Labels' : 'Config.Labels'} "${label}"}}`, name]) === id; }
    catch { return false; }
  };
  const sql = (container, source) => docker(['exec', container, 'psql', '-U', 'pvad_smoke', '-d', 'pvad_cutover', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1', '-c', source]);
  const wait = async container => {
    for (let attempt = 0; attempt < 60; attempt++) {
      try { if (sql(container, 'SELECT 1') === '1') return; } catch { /* Fresh isolated database is starting. */ }
      await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error('Synthetic PostgreSQL readiness timed out.');
  };
  const create = name => docker(['run', '-d', '--name', name, '--label', `${label}=${id}`, '--network', 'none',
    '--mount', `type=volume,src=${volume},dst=/var/lib/postgresql/data`,
    '--env', 'POSTGRES_USER', '--env', 'POSTGRES_DB', '--env', 'POSTGRES_PASSWORD', image], {
    POSTGRES_USER: 'pvad_smoke', POSTGRES_DB: 'pvad_cutover', POSTGRES_PASSWORD: randomUUID(),
  });
  // Use a cached immutable image ID; this runner never pulls or replaces images.
  const image = docker(['image', 'inspect', '--format', '{{.Id}}', 'postgres:17-alpine']);
  if (!/^sha256:[a-f0-9]{64}$/.test(image)) throw new Error('A cached PostgreSQL 17 image is required.');
  let report, failure;
  let volumeCreated = false;
  try {
    docker(['volume', 'create', '--label', `${label}=${id}`, volume]);
    if (!owned('volume', volume)) throw new Error('Synthetic volume ownership could not be confirmed.');
    volumeCreated = true;
    create(legacy); await wait(legacy);
    sql(legacy, "CREATE TABLE retained(id integer PRIMARY KEY, tenant text NOT NULL, payload text NOT NULL); INSERT INTO retained VALUES (1,'synthetic-tenant','Original PXA customer text');");
    const before = sql(legacy, 'SELECT id,tenant,payload FROM retained ORDER BY id');
    docker(['stop', '--time', '30', legacy]);
    create(current); await wait(current);
    if (sql(current, 'SELECT id,tenant,payload FROM retained ORDER BY id') !== before) throw new Error('Retained data changed at cutover.');
    sql(current, "INSERT INTO retained VALUES (2,'synthetic-tenant','PVAD write after cutover');");
    const after = sql(current, 'SELECT id,tenant,payload FROM retained ORDER BY id');
    docker(['stop', '--time', '30', current]);
    docker(['start', legacy]); await wait(legacy);
    if (sql(legacy, 'SELECT id,tenant,payload FROM retained ORDER BY id') !== after) throw new Error('Rollback lost post-cutover data.');
    if (sql(legacy, 'SELECT count(*) FROM retained') !== '2') throw new Error('Unexpected retained row count.');
    report = { schemaVersion: 1, status: 'passed', image, retainedRows: 2,
      beforeSha256: createHash('sha256').update(before).digest('hex'), afterSha256: createHash('sha256').update(after).digest('hex'),
      rollbackVerified: true, concurrentWriters: false,
      scope: 'Synthetic PostgreSQL container/service-name and shared-volume cutover only; not application-version, production-key or customer rollback certification.' };
  } catch (error) { failure = error; }
  const cleanup = [];
  for (const name of [current, legacy]) {
    if (!owned('container', name)) continue;
    try { docker(['rm', '-f', name]); } catch { cleanup.push(name); }
  }
  if (volumeCreated && owned('volume', volume)) {
    try { docker(['volume', 'rm', volume]); } catch { cleanup.push(volume); }
  }
  if (cleanup.length) throw new Error(`Synthetic cleanup incomplete: ${cleanup.join(', ')}.`);
  if (failure) throw failure;
  return { ...report, labeledResourcesRemoved: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  if (process.argv.length !== 2) throw new Error('No arguments or external resource names are accepted.');
  try {
    const report = await runServiceCutover();
    const output = new URL('../../artifacts/rename/service-cutover/', import.meta.url);
    await mkdir(output, { recursive: true });
    await writeFile(new URL('report.json', output), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify(report));
  } catch { console.error('Isolated service cutover failed; inspect local Docker availability and labeled synthetic resources.'); process.exitCode = 1; }
}
