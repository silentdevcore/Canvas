import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
if (process.argv.length !== 2) throw new Error('This local validation command accepts no deployment arguments.');
const output = new URL('../../artifacts/rename/local-validation/', import.meta.url);
mkdirSync(output, { recursive: true });
const steps = [
  ['configuration', process.execPath, ['--test', ...['native-project-contract', 'service-cutover-smoke', 'rename-cutover-candidate', 'frontend-proxy-contract'].map(name => `tools/configuration/${name}.test.mjs`), 'tools/legal/recovery-contract.test.mjs', 'deploy/websites/designer-csp.test.mjs']],
  ['keyring-and-auth', 'dotnet', ['test', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--no-restore', '-p:SkipOcrWorkerPackaging=true',
    '--filter', 'FullyQualifiedName~PvadDataProtectionCompatibilityTests|FullyQualifiedName~PvadCorsConfigurationTests',
    '--logger', `trx;LogFileName=keyring-and-auth.trx`, '--results-directory', fileURLToPath(output)]],
  ['database-backup-restore', 'sh', ['tools/legal/run-backup-restore-smoke-test.sh']],
  ['service-cutover-rollback', process.execPath, ['tools/configuration/service-cutover-smoke.mjs']],
  ['operator-proxy', process.execPath, ['tools/configuration/operator-proxy-smoke.mjs']],
  ['website-hosting', 'sh', ['deploy/websites/status-hosting-smoke-test.sh']],
  ['native-production-hosting', process.execPath, ['tools/configuration/native-project-production-smoke.mjs']],
  ['native-intake-browser', process.execPath, ['tools/configuration/native-project-browser-smoke.mjs']],
  ['native-import-browser', process.execPath, ['tools/configuration/native-project-import-browser-smoke.mjs']],
  ['release-contracts', process.execPath, ['--test', 'tools/versioning/pvad-version.test.mjs', 'tools/versioning/pvad-release-author.test.mjs', 'tools/deployment/pvad-deployment.test.mjs']],
  ['version-check', process.execPath, ['tools/versioning/pvad-version.mjs', 'check']],
];
const immutablePaths = ['VERSION', 'product-metadata/pvad-releases.json', 'pvad-designer/package.json'];
const hashes = () => immutablePaths.map(path => createHash('sha256').update(readFileSync(new URL(`../../${path}`, import.meta.url))).digest('hex'));
const before = hashes(), results = [];
for (const [id, executable, args] of steps) {
  const started = Date.now();
  const run = spawnSync(executable, args, { cwd: root, encoding: 'utf8', timeout: 600_000, maxBuffer: 32 * 1024 * 1024,
    env: { ...process.env, PVAD_STATUS_SMOKE_PORT: '14183' } });
  writeFileSync(new URL(`${id}.log`, output), `${run.stdout ?? ''}${run.stderr ?? ''}`);
  const status = run.status === 0 && !run.error ? 'passed' : 'failed';
  results.push({ id, status, exitCode: run.status, durationMs: Date.now() - started });
  console.log(`${id}: ${status}`);
}
const preview = spawnSync(process.execPath, ['tools/versioning/pvad-version.mjs', 'prepare-fragments', '--dry-run', '--format', 'json',
  '--title', 'PVAD platform rename', '--summary', 'PVAD technical identity and PVADE platform branding with compatibility readers and coordinated migration guidance.',
  '--date', new Date().toISOString().slice(0, 10)], { cwd: root, encoding: 'utf8', timeout: 60_000 });
const unchanged = JSON.stringify(before) === JSON.stringify(hashes());
let release;
try { release = JSON.parse(preview.stdout); } catch { /* Report failed preview without promoting it to release evidence. */ }
results.push({ id: 'release-dry-run', status: preview.status === 0 && unchanged && release?.impact === 'major' ? 'passed' : 'failed',
  nextVersion: release?.version, fragments: release?.fragmentCount, versionAndManifestUnchanged: unchanged });
const report = { schemaVersion: 1, scope: 'Isolated local validation only; not a staging deployment or production authorization.',
  status: results.every(result => result.status === 'passed') ? 'passed' : 'failed', results,
  unverified: ['Real installed OS file associations and uninstall', 'Real customer/production key mounts and recovery',
    'Public DNS/TLS/mail/OAuth callbacks', 'Publisher ownership and release authorization',
    'Public Designer CSP policy and deployment approval', 'Full live multi-host staging deployment'] };
writeFileSync(new URL('report.json', output), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report));
if (report.status !== 'passed') process.exitCode = 1;
