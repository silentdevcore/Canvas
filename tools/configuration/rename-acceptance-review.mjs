import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { validateBrowserEvidence } from './https-auth-evidence.mjs';

const sections = [
  'P0 - Baseline And Decisions', 'P1 - Projects And Internal Code', 'P1 - Designer, Websites And MCP',
  'P1 - API And Contract Migration', 'P1 - Identity, Database, Licenses And Jobs',
  'P1 - Native Project Formats', 'P1 - Infrastructure And Operations',
  'P1 - Documentation, Release And Evidence', 'Validation Gates', 'Completion Criteria',
];
export const releaseGates = [
  ['G01', sections[0], 'Confirm target domain, public publisher scopes, registry ownership and support addresses.'],
  ['G02', sections[4], 'Complete authorized retained-key access, recovery and protected-consumer cutover.'],
  ['G03', sections[6], 'Rename owned env variables, config keys, Docker ARGs, image names, services and telemetry identity.'],
  ['G04', sections[6], 'Update reverse proxy, CORS/CSP, OAuth redirect allowlists, sender identities and webhook callbacks together.'],
  ['G05', sections[6], 'Change production domains, registries, DNS, email and external systems only after explicit deployment authorization.'],
  ['G06', sections[8], 'Perform externally authorized staging/deployment smoke tests before production cutover.'],
  ['G07', sections[9], 'Breaking package/namespace changes have a major release and actionable migration instructions.'],
  ['G08', sections[9], 'External systems have confirmed owners and tested cutover/rollback evidence.'],
].map(([id, section, task]) => ({ id, section, task }));

export function primaryAcceptance(markdown) {
  let section;
  const seen = new Set(), tasks = [];
  for (const line of markdown.split(/\r?\n/)) {
    if (line.startsWith('## ')) {
      section = line.slice(3);
      if (/^P[01] - /.test(section)) assert.ok(sections.includes(section), 'Unknown primary acceptance section.');
      if (sections.includes(section)) {
        assert.ok(!seen.has(section), 'Duplicate primary acceptance section.'); seen.add(section);
      }
    }
    const match = /^- \[([^\]])\] (.+)$/.exec(line);
    if (!match || !sections.includes(section)) continue;
    assert.ok(['x', ' '].includes(match[1]), 'Invalid primary checkbox status.');
    tasks.push({ section, task: match[2], done: match[1] === 'x' });
  }
  assert.equal(seen.size, sections.length, 'Primary acceptance sections changed.');
  assert.equal(tasks.length, 98, 'Primary scope changed; review the denominator explicitly.');
  assert.equal(new Set(tasks.map(value => value.section + '\n' + value.task)).size, tasks.length, 'Duplicate primary task.');
  for (const gate of releaseGates)
    assert.equal(tasks.filter(value => value.section === gate.section && value.task === gate.task).length, 1, 'Release gate identity changed.');
  const open = tasks.filter(value => !value.done).map(value => {
    const gate = releaseGates.find(gate => gate.section === value.section && gate.task === value.task);
    assert.ok(gate, 'An additional primary task was reopened; review its acceptance classification.');
    return { ...gate };
  });
  const completed = tasks.length - open.length;
  return { total: tasks.length, completed, remaining: open.length,
    completedPercent: Number((completed / tasks.length * 100).toFixed(1)),
    remainingPercent: Number((open.length / tasks.length * 100).toFixed(1)), open };
}

const digest = value => createHash('sha256').update(value).digest('hex');
function safeFile(root, path) {
  assert.ok(typeof path === 'string' && path.length < 500 && !path.includes('\\') && !path.split('/').includes('..'));
  assert.ok(!/(?:^|\/)(?:\.git|\.aws|\.codex|\.vscode|App_Data|node_modules)(?:\/|$)/i.test(path));
  assert.ok(!/(?:^|\/)\.env(?:$|\.(?!example$|sample$))|\.(?:key|pem|pfx|p12|dump)$/i.test(path));
  const file = resolve(root, path), base = realpathSync(root) + sep;
  assert.ok(file.startsWith(resolve(root) + sep) && lstatSync(file).isFile());
  assert.equal(realpathSync(file), base + file.slice(resolve(root).length + 1), 'Symlink evidence paths are not accepted.');
  return file;
}

export function inspectLocalEvidence(root, path, kind) {
  if (!existsSync(resolve(root, path))) return { path, status: 'not-available' };
  try {
    const bytes = readFileSync(safeFile(root, path)); assert.ok(bytes.length < 8 * 1024 * 1024);
    const report = JSON.parse(bytes);
    assert.equal(report.status, 'passed'); assert.equal(report.checks.length, 12);
    assert.equal(new Set(report.checks).size, 12);
    assert.equal(report.syntheticOnly, true); assert.equal(report.deploymentAuthorized, false);
    assert.equal(report.apiMocking, false); assert.equal(report.temporaryResourcesRemoved, true);
    if (kind === 'https') {
      validateBrowserEvidence(report);
      assert.equal(report.apiEnvironment, 'Staging'); assert.equal(report.productionStartupBlocked, true);
      assert.equal(report.hostTrustModified, false);
      assert.equal(report.screenshotChecksums.length, 6);
      assert.deepEqual(report.screenshotChecksums.map(value => value.path), report.screenshots);
    }
    const pins = [...report.sourceChecksums, ...(report.harnessChecksums ?? [])];
    assert.ok(pins.length > 0 && pins.length < 500);
    assert.equal(new Set(pins.map(pin => pin.path)).size, pins.length);
    const changed = [];
    for (const pin of pins) {
      assert.match(pin.sha256, /^[a-f0-9]{64}$/);
      if (digest(readFileSync(safeFile(root, pin.path))) !== pin.sha256) changed.push(pin.path);
    }
    if (kind === 'https') for (const pin of report.screenshotChecksums) {
      assert.match(pin.path, /^[a-z-]+\.png$/); assert.match(pin.sha256, /^[a-f0-9]{64}$/);
      const file = path.slice(0, path.lastIndexOf('/') + 1) + 'browser/' + pin.path;
      assert.equal(digest(readFileSync(safeFile(root, file))), pin.sha256, 'Screenshot evidence changed.');
    }
    return { path, sha256: digest(bytes), status: changed.length ? 'historical-source-drift' : 'current-pinned-sources',
      sourceCommit: report.sourceCommit, checks: report.checks.length, pins: pins.length, changedSources: changed,
      limits: 'Synthetic local evidence, not production, public-host or whole-candidate approval.' };
  } catch { return { path, status: 'invalid-evidence', reason: 'Invalid, incomplete, unsafe or altered evidence; no approval inferred.' }; }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    assert.equal(process.argv.length, 2, 'No release, deployment, target or resource arguments are accepted.');
    const root = fileURLToPath(new URL('../../', import.meta.url));
    const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim();
    const acceptance = primaryAcceptance(readFileSync(resolve(root, 'checklists/PVAD.Rename-Plan.md'), 'utf8'));
    const inventory = JSON.parse(readFileSync(resolve(root, 'product-metadata/data-processing-inventory.json')));
    const evidence = [
      inspectLocalEvidence(root, 'artifacts/rename/https-authentication/report.json', 'https'),
      inspectLocalEvidence(root, 'artifacts/rename/monitoring-operator-rehearsal/report.json', 'monitoring'),
    ];
    const report = { schemaVersion: 1, reviewedAt: new Date().toISOString(), sourceCommit: git(['rev-parse', 'HEAD']),
      branch: git(['branch', '--show-current']), uncommittedTrackedChanges: git(['status', '--porcelain', '--untracked-files=no']) !== '',
      productVersion: readFileSync(resolve(root, 'VERSION'), 'utf8').trim(), acceptance, evidence,
      productionRetentionApproved: inventory.productionApproved === true,
      pendingRetentionPolicies: inventory.activities.filter(value => value.retention?.approvalStatus !== 'approved').length,
      status: acceptance.remaining ? 'release-blocked' : 'requires-human-release-approval',
      deploymentAuthorized: false, publicationAuthorized: false, fullCandidateRevalidationPerformed: false,
      remoteControls: 'Not queried by this offline reviewer; see the dated GET-only observation in the final acceptance guide.' };
    const directory = resolve(root, 'artifacts/rename/final-acceptance'); mkdirSync(directory, { recursive: true });
    writeFileSync(resolve(directory, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ status: report.status, acceptance, evidence, productionRetentionApproved: report.productionRetentionApproved,
      pendingRetentionPolicies: report.pendingRetentionPolicies, deploymentAuthorized: false, publicationAuthorized: false }, null, 2));
    if (evidence.some(value => value.status === 'invalid-evidence')) process.exitCode = 1;
  } catch { console.error('Rename acceptance review failed; no release or deployment was authorized.'); process.exitCode = 1; }
}
