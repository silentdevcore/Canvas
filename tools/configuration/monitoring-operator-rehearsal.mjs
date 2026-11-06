import { mkdir, readdir, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { runOperatorBrowserRehearsal } from './operator-browser-rehearsal.mjs';

const output = fileURLToPath(new URL('../../artifacts/rename/monitoring-operator-rehearsal/', import.meta.url));
try {
  if (process.argv.length !== 2) throw new Error('No arguments or external resource names are accepted.');
  await mkdir(output, { recursive: true });
  for (const file of ['failure.json', 'gateway.log']) await rm(resolve(output, file), { force: true });
  for (const file of await readdir(output))
    if (/^[a-z0-9-]+-diagnostic\.log$/.test(file)) await rm(resolve(output, file));
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ schemaVersion: 1, status: 'running', deploymentAuthorized: false }) + '\n');
  try {
    const report = await runOperatorBrowserRehearsal({ withMonitoring: true, recordFailure: true,
      progress: phase => console.log(`Integrated monitoring rehearsal: ${phase}`) });
    await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    console.log('Real telemetry, signed alert delivery and protected Grafana cutover/restore passed.');
  } catch (error) {
    await writeFile(resolve(output, 'report.json'), JSON.stringify({ schemaVersion: 1, status: 'failed', deploymentAuthorized: false }) + '\n');
    throw error;
  }
} catch (error) { console.error(error.message); process.exitCode = 1; }
