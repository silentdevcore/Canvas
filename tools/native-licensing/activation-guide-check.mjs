import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { checkRepository } from './pvad-native-licensing.mjs';
import { projects, regularOutputs, validateProjectClosure } from './joint-package-rehearsal.mjs';
import { createTestLicenses } from './with-test-license.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(join(root, 'pvad-designer/package.json'));
const { marked } = await import(pathToFileURL(require.resolve('marked')).href);
const { JSDOM } = require('jsdom');
const guidePath = 'docs/guides/PVAD.Native-Library-Activation.md';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const sha = path => hash(readFileSync(path));

export function extractExample(markdown) {
  const blocks = marked.lexer(markdown).filter(token => token.type === 'code' && token.lang === 'csharp');
  assert.equal(blocks.length, 1, 'The activation guide must have exactly one complete C# example.');
  assert.ok(blocks[0].text.includes('public static class NativeActivationExample'));
  return blocks[0].text + '\n';
}

function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 600_000, maxBuffer: 32 * 1024 * 1024 });
  if (result.error || result.status !== 0) throw new Error(`${command} failed (${result.status}):\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  for (const line of (result.stdout ?? '').split(/\r?\n/).filter(line => !/^[\[{]/.test(line.trim()) && /warning|warnung|NPOI:/i.test(line))) console.error(line);
  return result.stdout;
}

export function main(args) {
  assert.equal(args.length, 0, 'No guide, output, trust, license or publication overrides are accepted.');
  validateProjectClosure();
  const { version } = checkRepository();
  const guide = join(root, guidePath), source = readFileSync(guide, 'utf8'), example = extractExample(source);
  const trust = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
  const trustHash = sha(trust), beforeBuild = regularOutputs(projects);
  const output = join(root, 'artifacts/native-activation-guide', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const work = join(output, 'private-work'); mkdirSync(work, { mode: 0o700 });
  const secret = mkdtempSync(join(tmpdir(), 'pvad-activation-guide-'));
  let succeeded = false;
  try {
    const licenses = createTestLicenses(secret, Date.now(), false, false, false, true);
    const project = join(work, 'GuideExample.csproj');
    const document = new JSDOM('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><ProjectReference/><Compile Include="Program.cs"/><Compile Include="NativeActivationExample.cs"/></ItemGroup></Project>', { contentType: 'text/xml' }).window.document;
    document.querySelector('ProjectReference').setAttribute('Include', join(root, projects['PVAD.Generator']));
    writeFileSync(project, document.documentElement.outerHTML);
    writeFileSync(join(work, 'NativeActivationExample.cs'), example);
    copyFileSync(join(root, 'tools/native-licensing/activation-guide-probe.cs'), join(work, 'Program.cs'));
    const build = join(work, 'build');
    run('dotnet', ['build', project, '-c', 'Release', '--artifacts-path', build, '--disable-build-servers', '-m:1', '-nr:false',
      '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '-p:DebugType=None', '-p:DebugSymbols=false',
      '-p:EmbedAllSources=false', '-p:EnableSourceLink=false', `-p:PvadLicenseTrustStorePath=${licenses.trustPath}`]);
    const pdf = join(work, 'example.pdf');
    const result = JSON.parse(run('dotnet', [join(build, 'bin/GuideExample/release/GuideExample.dll'), secret, pdf, licenses.revocations]).trim().split(/\r?\n/).at(-1));
    assert.equal(new Set(result.checks).size, result.checks.length);
    assert.equal(result.checks.length, 21);
    assert.ok(readFileSync(pdf).subarray(0, 5).equals(Buffer.from('%PDF-')));
    const text = run('pdftotext', ['-layout', pdf, '-']);
    assert.ok(text.includes('Native PVAD licensing example'), 'The guide must generate the expected visible PDF content.');
    assert.equal(sha(guide), hash(source), 'Documentation changed during verification.');
    writeFileSync(join(output, 'report.json'), JSON.stringify({ schemaVersion: 1, version, syntheticOnly: true,
      guide: guidePath, guideSha256: hash(source), exampleSha256: hash(example),
      checks: result.checks, architecture: result.architecture, platform: process.platform,
      generatedPdfVerified: true, productionTrustProvisioned: false, publicationPerformed: false }, null, 2) + '\n');
    succeeded = true;
  } finally {
    rmSync(secret, { recursive: true, force: true }); rmSync(work, { recursive: true, force: true });
    assert.ok(!existsSync(secret) && !existsSync(work)); assert.equal(sha(trust), trustHash);
    assert.deepEqual(regularOutputs(projects), beforeBuild, 'Normal product outputs changed.');
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, ephemeralArtifactsRemoved: true, sourceTrustUnchanged: true, regularProductBuildsUnchanged: true }, null, 2) + '\n');
  }
  console.log(`Native activation guide verified: ${output}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
