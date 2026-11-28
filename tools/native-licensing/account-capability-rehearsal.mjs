import assert from 'node:assert/strict';
import { createHash, randomUUID, sign } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync, execFileSync } from 'node:child_process';
import { createTestLicenses } from './with-test-license.mjs';

assert.equal(process.argv.length, 2, 'This synthetic rehearsal accepts no production inputs.');
assert.ok(!process.env.PVAD_NATIVE_TEST_SESSION, 'Run this separate synthetic trust profile outside another test session.');
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const output = join(root, 'artifacts/native-account-capabilities', `${Date.now()}-${randomUUID().slice(0, 8)}`);
const work = join(output, 'private-test-build');
mkdirSync(work, { recursive: true, mode: 0o700 });
const secret = mkdtempSync(join(tmpdir(), 'pvad-account-capabilities-'));
const sourceTrust = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const beforeTrust = hash(sourceTrust);
const sourceFiles = [...new Set([
  ...execFileSync('git', ['diff', '--name-only'], { cwd: root, encoding: 'utf8' }).trim().split('\n'),
  ...execFileSync('git', ['ls-files', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' }).trim().split('\n'),
])].filter(path => /^(src|tools|PVAD.WebApi)\//.test(path) && /\.(cs|csproj|mjs)$/.test(path));
const sourceHashes = Object.fromEntries(sourceFiles.map(path => [path, hash(join(root, path))]));
let succeeded = false;
try {
  const paths = createTestLicenses(secret, Date.now(), false, false, true);
  const start = new Date(Date.now() - 60_000);
  const payload = Buffer.from(JSON.stringify({ schemaVersion: 2, licenseId: randomUUID(),
    customerReference: 'synthetic-scoped-individual', model: 'trial', issuedAt: start.toISOString(), validFrom: start.toISOString(),
    validUntil: new Date(start.getTime() + 30 * 86400_000).toISOString(), updatesUntil: null, redistributionAllowed: false,
    grants: ['pdf', 'word', 'spreadsheet', 'importer', 'converter'].map(product => ({ product, providers: [] })),
    accountScope: { accountType: 'individualDeveloper', capabilities: ['document.create', 'document.edit', 'document.print',
      'import.md', 'export.pdf', 'export.docx', 'export.xlsx', 'export.odt'] } }));
  const scoped = join(secret, 'individual-scoped.json');
  writeFileSync(scoped, JSON.stringify({ schemaVersion: 2, keyId: 'ephemeral-native-engine-test',
    algorithm: 'ECDSA_P256_SHA256_P1363', payload: payload.toString('base64'),
    signature: sign('sha256', payload, { key: readFileSync(paths.issuerPrivateKey), dsaEncoding: 'ieee-p1363' }).toString('base64') }), { mode: 0o600 });
  const env = { ...process.env };
  for (const name of Object.keys(env))
    if (/AWS_|AZURE_|GITHUB_TOKEN|PASSWORD|SECRET|ACCESS_TOKEN/.test(name)) delete env[name];
  delete env.PVAD_NATIVE_LICENSE_PATH;
  delete env.NativeLicensing__LicensePath;
  const result = spawnSync('dotnet', ['run', '--project', 'tools/native-licensing/AccountCapabilityProbe/AccountCapabilityProbe.csproj',
    '--artifacts-path', work, '--disable-build-servers', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false',
    `-p:PvadLicenseTrustStorePath=${paths.trustPath}`, '-p:PvadNativeLicenseTestBootstrap=true',
    '--', paths.integrated, scoped], { cwd: root, env, encoding: 'utf8', timeout: 600_000, maxBuffer: 16 * 1024 * 1024 });
  writeFileSync(join(output, 'build.log'), `${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  assert.equal(result.status, 0, `The signed native capability probe failed; see ${join(output, 'build.log')}`);
  const report = JSON.parse(result.stdout.trim().split(/\r?\n/).at(-1));
  for (const [path, expected] of Object.entries(sourceHashes)) assert.equal(hash(join(root, path)), expected, 'Source changed during validation.');
  assert.equal(report.checks.length, 20);
  assert.equal(new Set(report.checks).size, 20);
  writeFileSync(join(output, 'report.json'), JSON.stringify({ schemaVersion: 1, ...report,
    sourceHashes, sourceUnchangedDuringRun: true,
    productionTrustProvisioned: false, publicationPerformed: false }, null, 2) + '\n');
  succeeded = true;
} finally {
  rmSync(secret, { recursive: true, force: true });
  rmSync(work, { recursive: true, force: true });
  const cleanup = { succeeded, syntheticKeysRemoved: !existsSync(secret), testBuildRemoved: !existsSync(work), sourceTrustUnchanged: hash(sourceTrust) === beforeTrust };
  writeFileSync(join(output, 'cleanup.json'), JSON.stringify(cleanup, null, 2) + '\n');
  assert.ok(cleanup.syntheticKeysRemoved && cleanup.testBuildRemoved && cleanup.sourceTrustUnchanged);
}
console.log(`Signed account capability evidence: ${output}`);
