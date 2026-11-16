import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeTestAssemblyPath, withNativeTestSession } from './test-session.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const project = 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj';
function run(args, expectedFailure = false) {
  const result = spawnSync('dotnet', args, { cwd: root, env: process.env, encoding: 'utf8',
    stdio: expectedFailure ? 'pipe' : 'inherit', timeout: 900_000, maxBuffer: 8 * 1024 * 1024 });
  assert.ok(!result.error, 'Dotnet did not complete.');
  if (expectedFailure) {
    assert.notEqual(result.status, 0, 'A synthetic build escaped its restriction.');
    return result.stdout + result.stderr;
  }
  assert.equal(result.status, 0, 'The isolated .NET command failed.');
}

await withNativeTestSession(async () => {
  const output = dirname(process.env.PVAD_NATIVE_TEST_BUILD_ROOT);
  run(['test', project, '--disable-build-servers', '-m:1', '--filter',
    'FullyQualifiedName~PvadNativePdfLicenseTests|FullyQualifiedName~CodeWorkspaceConversionTests|FullyQualifiedName~ExportControllerTests',
    '--logger', 'trx;LogFileName=session-api.trx', '--results-directory', output, '--verbosity', 'minimal']);
  const assembly = nativeTestAssemblyPath(project);
  assert.ok(existsSync(assembly));
  const checks = [];
  for (const target of ['publish', 'pack']) {
    const text = run([target, 'src/Core/PVAD.Core/PVAD.Core.csproj', '--no-build', '--no-restore'], true);
    assert.match(text, /Synthetic native-license test builds must not be packaged or published/);
    checks.push(`${target}-rejected`);
  }
  const escape = run(['msbuild', 'src/Core/PVAD.Core/PVAD.Core.csproj', '-t:PvadRequireIsolatedNativeTestBuild',
    `-p:OutputPath=${resolve(output, 'outside-build')}`], true);
  assert.match(escape, /output and intermediate directories must remain inside the isolated build/);
  assert.ok(!existsSync(join(output, 'outside-build')));
  checks.push('output-escape-rejected-before-write');
  writeFileSync(join(output, 'rehearsal.json'), JSON.stringify({ checks,
    assemblySha256: createHash('sha256').update(readFileSync(assembly)).digest('hex'),
    testAssemblyWasIsolated: true, syntheticOnly: true }, null, 2) + '\n');
  console.log(`Shared-session evidence: ${output}`);
});
