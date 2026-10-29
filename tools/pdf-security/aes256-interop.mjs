#!/usr/bin/env node
import { readPvadEnvironment } from '../configuration/environment.mjs';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const root = path.resolve(import.meta.dirname, '../..');
const fixture = path.resolve(readPvadEnvironment('PVAD_AES256_FIXTURE') ?? path.join(
  root, 'tests/PVAD.Importer.Tests/Fixtures/PdfTrustExternal/qpdf-aes256-unicode.pdf'));
const reportPath = path.resolve(readPvadEnvironment('PVAD_AES256_REPORT') ?? path.join(
  root, 'artifacts/pdf-security/aes256-interop.json'));
const userPassword = readPvadEnvironment('PVAD_AES256_USER_PASSWORD') ?? 'PXA-reader-ä漢字';
const ownerPassword = readPvadEnvironment('PVAD_AES256_OWNER_PASSWORD') ?? 'PXA-owner-ßЖ';
const wrongPassword = 'PXA-intentionally-wrong';
const qpdfImage = readPvadEnvironment('PVAD_AES256_QPDF_IMAGE') ?? 'pvad-pdf-trust-external-tools:1';

if (!fs.existsSync(fixture)) throw new Error(`AES-256 fixture not found: ${fixture}`);

function run(id, tool, args, expectedSuccess) {
  const started = Date.now();
  const result = spawnSync(tool.command, [...tool.prefix, ...args], {
    encoding: 'utf8',
    timeout: 10_000,
    maxBuffer: 64 * 1024,
    env: { ...process.env, LC_ALL: 'C' }
  });
  const exitedSuccessfully = result.status === 0 && !result.error;
  return {
    id,
    tool: tool.id,
    expected: expectedSuccess ? 'accept' : 'reject',
    outcome: result.error?.code === 'ETIMEDOUT' ? 'timeout' : exitedSuccessfully ? 'accept' : 'reject',
    exitCode: result.status,
    durationMilliseconds: Date.now() - started
  };
}

function version(tool, args) {
  const result = spawnSync(tool.command, [...tool.prefix, ...args], { encoding: 'utf8', timeout: 10_000, maxBuffer: 4096 });
  if (result.error?.code === 'ENOENT') throw new Error(`Required interoperability tool is missing: ${tool.command}`);
  if (result.status !== 0) throw new Error(`Could not query ${tool.id}: ${result.stderr || result.stdout}`);
  return `${result.stdout ?? ''}${result.stderr ?? ''}`.trim().split(/\r?\n/, 1)[0];
}

const localQpdf = spawnSync('qpdf', ['--version'], { encoding: 'utf8', timeout: 5_000 });
const fixtureRelative = path.relative(root, fixture).split(path.sep).join('/');
const qpdf = localQpdf.status === 0
  ? { id: 'qpdf', command: 'qpdf', prefix: [], fixture }
  : {
      id: 'qpdf-container',
      command: 'docker',
      prefix: ['run', '--rm', '--volume', `${root}:/work:ro`, qpdfImage, 'qpdf'],
      fixture: `/work/${fixtureRelative}`
    };
const pdfinfo = { id: 'poppler-pdfinfo', command: 'pdfinfo', prefix: [] };
const pdftotext = { id: 'poppler-pdftotext', command: 'pdftotext', prefix: [] };
const tools = {
  qpdf: version(qpdf, ['--version']),
  pdfinfo: version(pdfinfo, ['-v']),
  pdftotext: version(pdftotext, ['-v'])
};
const checks = [
  run('qpdf-user-unicode', qpdf, [`--password=${userPassword}`, '--check', qpdf.fixture], true),
  run('qpdf-owner-unicode', qpdf, [`--password=${ownerPassword}`, '--check', qpdf.fixture], true),
  run('qpdf-wrong-password', qpdf, [`--password=${wrongPassword}`, '--check', qpdf.fixture], false),
  run('poppler-info-user', pdfinfo, ['-upw', userPassword, fixture], true),
  run('poppler-info-wrong-password', pdfinfo, ['-upw', wrongPassword, fixture], false),
  run('poppler-text-user', pdftotext, ['-upw', userPassword, fixture, '-'], true),
  run('poppler-text-wrong-password', pdftotext, ['-upw', wrongPassword, fixture, '-'], false)
];
const failed = checks.filter(check => check.outcome !== check.expected);
const report = {
  schemaVersion: 1,
  fixture: path.basename(fixture),
  fixtureSha256: crypto.createHash('sha256').update(fs.readFileSync(fixture)).digest('hex'),
  tools,
  checks,
  summary: { total: checks.length, passed: checks.length - failed.length, failed: failed.length }
};

fs.mkdirSync(path.dirname(reportPath), { recursive: true });
fs.writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);
console.log(`AES-256 interoperability: ${report.summary.passed}/${report.summary.total} checks passed (${reportPath})`);
if (failed.length) process.exit(1);
