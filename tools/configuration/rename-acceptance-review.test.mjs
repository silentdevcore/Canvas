import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { primaryAcceptance, inspectLocalEvidence, releaseGates } from './rename-acceptance-review.mjs';
import { screenshotNames } from './https-auth-evidence.mjs';
import { publicGuidesPlugin } from '../../websites/PVAD.Documentation/publicGuides.js';

const plan = readFileSync(new URL('../../checklists/PVAD.Rename-Plan.md', import.meta.url), 'utf8');
const digest = bytes => createHash('sha256').update(bytes).digest('hex');

test('only the 98 primary tasks determine acceptance and the eight original gates remain mapped', () => {
  const result = primaryAcceptance(plan);
  assert.equal(result.total, 98); assert.equal(result.completed, 90); assert.equal(result.remaining, 8);
  assert.equal(result.completedPercent, 91.8); assert.equal(result.remainingPercent, 8.2);
  assert.deepEqual(result.open.map(value => value.id), releaseGates.map(value => value.id));
});

test('historical percentages and extra supporting checklists do not inflate primary progress', () => {
  const extra = '\n## Additional Supporting Evidence\n' + '- [x] Passed synthetic rehearsal.\n'.repeat(100);
  assert.deepEqual(primaryAcceptance(plan + extra), primaryAcceptance(plan));
  assert.deepEqual(primaryAcceptance(plan.replace('86/98 are complete (87.8%)', '0/98 are complete (0.0%)')), primaryAcceptance(plan));
});

test('scope drift, missing sections, duplicate tasks and unclassified reopened tasks require review', () => {
  assert.throws(() => primaryAcceptance(plan.replace('## P0 - Baseline And Decisions', '## Removed Section')));
  assert.throws(() => primaryAcceptance(plan.replace('## P0 - Baseline And Decisions', '## P0 - Unknown Section')));
  assert.throws(() => primaryAcceptance(plan.replace('## Completion Criteria', '## Completion Criteria\n- [x] Extra primary task.')));
  assert.throws(() => primaryAcceptance(plan.replace('- [x] Create a dedicated rename branch', '- [ ] Create a dedicated rename branch')));
  assert.throws(() => primaryAcceptance(plan.replace('- [x] Create a dedicated rename branch', '- [?] Create a dedicated rename branch')));
});

test('a future reviewed gate closure is counted from its checkbox, not a hard-coded target percentage', () => {
  const task = releaseGates[0].task;
  const result = primaryAcceptance(plan.replace('- [ ] ' + task, '- [x] ' + task));
  assert.equal(result.completed, 91); assert.equal(result.remaining, 7);
  assert.equal(result.open.some(value => value.id === 'G01'), false);
});

function fixture(run) {
  const root = mkdtempSync(join(tmpdir(), 'pvad-acceptance-'));
  const path = 'artifacts/rename/https-authentication/report.json';
  const write = (path, bytes) => { mkdirSync(dirname(join(root, path)), { recursive: true }); writeFileSync(join(root, path), bytes); };
  write('tools/fixture.mjs', 'export const synthetic = true;');
  const png = Buffer.from('89504e470d0a1a0a', 'hex');
  const report = { status: 'passed', checks: Array.from({ length: 12 }, (_, index) => 'check-' + String.fromCharCode(97 + index)),
    sourceCommit: 'synthetic', syntheticOnly: true, deploymentAuthorized: false, apiMocking: false, temporaryResourcesRemoved: true,
    apiEnvironment: 'Staging', productionStartupBlocked: true, hostTrustModified: false, tlsValidationDisabled: false,
    hostOnlyCookiesVerified: true, platform: 'isolated-linux-chromium', screenshots: screenshotNames,
    screenshotChecksums: screenshotNames.map(path => ({ path, sha256: digest(png) })),
    sourceChecksums: [{ path: 'tools/fixture.mjs', sha256: digest('export const synthetic = true;') }] };
  for (const name of screenshotNames) write('artifacts/rename/https-authentication/browser/' + name, png);
  const save = () => write(path, JSON.stringify(report)); save();
  try { run({ root, path, report, write, save }); } finally { rmSync(root, { recursive: true, force: true }); }
}

test('local evidence is pinned without relabeling a historical commit or approving deployment', () => fixture(({ root, path, write }) => {
  const current = inspectLocalEvidence(root, path, 'https');
  assert.equal(current.status, 'current-pinned-sources'); assert.equal(current.sourceCommit, 'synthetic');
  assert.match(current.sha256, /^[a-f0-9]{64}$/);
  write('tools/fixture.mjs', 'export const synthetic = false;');
  const changed = inspectLocalEvidence(root, path, 'https');
  assert.equal(changed.status, 'historical-source-drift');
  assert.deepEqual(changed.changedSources, ['tools/fixture.mjs']);
}));

test('failed, incomplete, mocked or altered screenshots cannot pass the evidence review', () => fixture(({ root, path, report, write, save }) => {
  report.status = 'failed'; save(); assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
  report.status = 'passed'; report.apiMocking = true; save(); assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
  report.apiMocking = false; report.checks.pop(); save(); assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
  report.checks.push('check-final'); save();
  write('artifacts/rename/https-authentication/browser/' + screenshotNames[0], 'changed');
  assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
}));

test('unsafe, private, symlink and duplicate source pins are rejected without reading their contents', () => fixture(({ root, path, report, save }) => {
  for (const value of ['../outside', '/etc/passwd', '.aws/private.json', '.vscode/private.json', 'secret.pfx', '.env.production']) {
    report.sourceChecksums[0].path = value; save();
    assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
  }
  symlinkSync(join(root, 'tools/fixture.mjs'), join(root, 'tools/linked.mjs'));
  report.sourceChecksums[0].path = 'tools/linked.mjs'; save();
  assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
  report.sourceChecksums[0].path = 'tools/fixture.mjs'; report.sourceChecksums.push({ ...report.sourceChecksums[0] }); save();
  assert.equal(inspectLocalEvidence(root, path, 'https').status, 'invalid-evidence');
}));

test('missing evidence is unavailable rather than a passing execution', () => fixture(({ root }) => {
  assert.equal(inspectLocalEvidence(root, 'artifacts/absent.json', 'https').status, 'not-available');
}));

test('the offline CLI rejects deployment and target arguments before any external command', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/rename-acceptance-review.mjs', '--deploy'],
    { encoding: 'utf8', env: { ...process.env, PATH: '/nonexistent' } });
  assert.equal(result.status, 1); assert.match(result.stderr, /no release or deployment was authorized/);
});

test('the internal exit matrix is discoverable, maps every gate once and uses the actual primary count', () => {
  const guide = readFileSync(new URL('../../docs/pvad-rename-final-acceptance.md', import.meta.url), 'utf8');
  assert.deepEqual([...guide.matchAll(/^\| (G\d\d) \|/gm)].map(value => value[1]), releaseGates.map(value => value.id));
  const count = primaryAcceptance(plan);
  assert.ok(guide.replace(/\s+/g, ' ').includes(`${count.completed}/${count.total} complete (${count.completedPercent}%)`));
  for (const path of ['../../checklists/PVAD.Rename-Plan.md', '../../checklists/PVAD.Rename-Test-Report.md', '../../docs/pvad-rename-release-approvals.md'])
    assert.ok(readFileSync(new URL(path, import.meta.url), 'utf8').includes('pvad-rename-final-acceptance.md'));
  assert.match(guide, /not independent/);
  assert.match(guide, /12 of 13 retention policies/);
});

test('operator acceptance guidance is excluded from public Documentation and DocFX', () => {
  const emitted = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: value => emitted.push(value.fileName) });
  assert.ok(!emitted.includes('docs/pvad-rename-final-acceptance.md'));
  const docfx = JSON.parse(readFileSync(new URL('../../docs/docfx.json', import.meta.url)));
  const files = docfx.build.content.flatMap(value => value.files);
  for (const forbidden of ['pvad-rename-final-acceptance.md', '**.md', '*.md', '**/*.md']) assert.ok(!files.includes(forbidden));
});

test('all local links in the final acceptance guide resolve to real files', () => {
  const file = fileURLToPath(new URL('../../docs/pvad-rename-final-acceptance.md', import.meta.url));
  const guide = readFileSync(file, 'utf8');
  for (const match of guide.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)) {
    const link = match[1].split('#')[0];
    if (!link || /^[a-z]+:/i.test(link)) continue;
    assert.ok(readFileSync(resolve(dirname(file), link)).length > 0, link);
  }
});
