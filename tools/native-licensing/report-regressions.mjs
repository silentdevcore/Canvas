import assert from 'node:assert/strict';
import { spawnSync, execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, openSync, closeSync } from 'node:fs';
import { basename, dirname, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { withNativeTestSession } from './test-session.mjs';
import { validateReportMigrationEntries } from './pvad-native-licensing.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const inventory = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-report-migration-entries.v1.json')));
validateReportMigrationEntries(inventory);
const selected = [
  ['PVAD.Api.Tests', ['PvadNativeReportMigrationLicenseTests', 'MigrationControllerTests', 'UnifiedMigrationsControllerTests',
    'PvadMigrationOutputTests', 'PvadMigrationArtifactIdentityTests', 'PvadDesignerMigrationAccessTests',
    'PvadNativeCodeMigrationLicenseTests', 'PvadNativeViewerMigrationLicenseTests']],
  ['PVAD.Licensing.Tests', []],
  ['PVAD.Migration.Report.Tests', []],
  ...inventory.entries.map(entry => [basename(dirname(entry.source)) + '.Tests', []]),
];
const hash = file => createHash('sha256').update(readFileSync(resolve(root, file))).digest('hex');
await withNativeTestSession(async () => {
  const output = dirname(process.env.ArtifactsPath);
  const files = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '--', 'src', 'tests', 'PVAD.WebApi', 'tools/native-licensing'],
    { cwd: root, encoding: 'utf8' }).trim().split('\n').filter(file => /\.(?:cs|csproj|mjs)$/.test(file)))].sort();
  const hashes = Object.fromEntries(files.map(file => [file, hash(file)]));
  writeFileSync(output + '/report-source-hashes.json', JSON.stringify(hashes, null, 2));
  const results = [];
  console.log('Evidence: ' + output);
  for (const [project, filter] of selected) {
    console.log('RUN ' + project);
    const args = ['test', `tests/${project}/${project}.csproj`, '--disable-build-servers', '-m:1', '-nr:false',
      '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '--results-directory', output,
      '--logger', `trx;LogFileName=report-${project}.trx`, '--verbosity', 'minimal'];
    if (filter.length) args.push('--filter', filter.map(name => 'FullyQualifiedName~' + name).join('|'));
    const fd = openSync(`${output}/${project}.log`, 'w');
    let result;
    try { result = spawnSync('dotnet', args, { cwd: root, env: process.env, stdio: ['ignore', fd, fd], timeout: 1_200_000 }); }
    finally { closeSync(fd); }
    results.push({ project, filter, status: result.status, signal: result.signal, error: result.error?.message });
    writeFileSync(output + '/report-process-results.json', JSON.stringify(results, null, 2));
    assert.equal(result.status, 0, project + ': inspect the scoped test log');
    assert.ok(!result.signal && !result.error);
    console.log('PASS ' + project);
  }
  for (const [file, expected] of Object.entries(hashes)) assert.equal(hash(file), expected, file);
  writeFileSync(output + '/report-sources-unchanged.json', JSON.stringify({ count: files.length, unchanged: true }));
});
