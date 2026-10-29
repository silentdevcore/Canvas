import { spawn } from 'node:child_process';
import { mkdtempSync, realpathSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = fileURLToPath(new URL('../../', import.meta.url));
const baseline = 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a';
const legacyRoot = realpathSync(mkdtempSync(join(tmpdir(), 'pvad-cutover-baseline-')));
const project = 'tools/job-cutover/PVAD.JobCutover.csproj';
const source = process.env.PVAD_CUTOVER_NUGET_SOURCE || process.env.NUGET_PACKAGES || join(homedir(), '.nuget', 'packages');
const completed = child => new Promise((resolve, reject) => {
  child.once('error', reject);
  child.once('exit', code => code === 0 ? resolve() : reject(new Error(`Process exited with ${code}`)));
});
const run = (command, args, env = process.env) => completed(spawn(command, args, {
  cwd: repo, env, stdio: 'inherit',
}));

console.log(`Exporting immutable PXA baseline ${baseline} to ${legacyRoot}`);
const archive = spawn('git', ['archive', baseline, 'Directory.Build.props', 'VERSION',
  'PXA.WebApi', 'src', 'product-metadata', 'assets/fonts', 'fonts', 'operator-docs'], {
  cwd: repo, stdio: ['ignore', 'pipe', 'inherit'],
});
const extract = spawn('tar', ['-x', '-C', legacyRoot], { stdio: ['pipe', 'inherit', 'inherit'] });
archive.stdout.pipe(extract.stdin);
await Promise.all([completed(archive), completed(extract)]);

const buildFlags = ['-m:1', '-nr:false', '-p:NuGetAudit=false', '-p:UseSharedCompilation=false',
  '-p:RestoreDisableParallel=true', '-p:SkipOcrWorkerPackaging=true', '-p:SkipCodeWorkerPackaging=true'];
await run('dotnet', ['build', project, '--source', source, `-p:LegacyRoot=${legacyRoot}`, ...buildFlags]);
await run('dotnet', ['build', project, '--source', source, '-p:LegacyRoot=', ...buildFlags]);
await run('dotnet', ['test', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--no-restore',
  '--filter', 'FullyQualifiedName~PvadJobBinaryCutoverTests', ...buildFlags], {
  ...process.env,
  PVAD_RUN_BINARY_CUTOVER_TESTS: '1',
  PVAD_LEGACY_JOB_HARNESS: join(repo, 'tools/job-cutover/bin/legacy/Debug/net10.0/PXA.JobCutover.dll'),
  PVAD_CURRENT_JOB_HARNESS: join(repo, 'tools/job-cutover/bin/Debug/net10.0/PVAD.JobCutover.dll'),
});
console.log(`Paired binary cutover passed. Retained baseline source: ${legacyRoot}`);
