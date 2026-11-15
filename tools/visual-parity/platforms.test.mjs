import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { PLATFORM_FIXTURES } from './platforms.mjs';

const read = file => readFileSync(new URL(file, import.meta.url), 'utf8');

test('native platform matrix covers the same five packs and retains each failed result', () => {
  assert.deepEqual(PLATFORM_FIXTURES, ['font-families', 'date-format', 'date-historical', 'date-layout', 'all-elements']);
  const source = read('./platforms.mjs');
  assert.match(source, /PVAD_VISUAL_PARITY_ENFORCE: 'true'/);
  assert.match(source, /PVAD_VISUAL_PARITY_REUSE_ARTIFACTS: 'false'/);
  assert.match(source, /'--bundled-browser'/);
  assert.match(source, /metadata\.results\.push/);
  assert.match(source, /if \(failed\) process\.exitCode = 1/);
  assert.doesNotMatch(source, /UPDATE_BASELINE|git push|git commit/);
});

test('renderer tooling uses native executables and portable ImageMagick null output', () => {
  const source = read('./run.mjs');
  assert.doesNotMatch(source, /spawnSync\('sh'|spawnSync\('compare'|\/dev\/null/);
  assert.match(source, /spawnSync\(name, \['-v'\]/);
  assert.match(source, /spawnImageMagick\(\['compare'/);
  assert.match(source, /'null:'/);
  assert.match(source, /process\.argv\.includes\('--bundled-browser'\)/);
});

test('platform CI uses real x64 hosts, immutable action pins and no deployment credentials', () => {
  const workflow = read('../../.github/workflows/visual-parity-platforms.yml');
  assert.match(workflow, /os: \[ubuntu-24\.04, windows-2025\]/);
  assert.match(workflow, /fail-fast: false/);
  assert.match(workflow, /core\.autocrlf false/);
  assert.match(workflow, /persist-credentials: false/);
  assert.match(workflow, /contents: read/);
  assert.match(workflow, /if: always\(\)/);
  assert.match(workflow, /path: artifacts\/platform-parity/);
  assert.match(workflow, /--version=26\.9\.0/);
  assert.doesNotMatch(workflow, /pull_request_target|secrets\.|packages: write|deploy|publish/);
  for (const action of workflow.matchAll(/uses: ([^\n]+)/g))
    assert.match(action[1], /@[a-f0-9]{40}(?:\s|$)/);
});

test('backend IANA data is pinned independently of registry history and ships original notices', () => {
  const root = new URL('../../', import.meta.url);
  const provenance = JSON.parse(readFileSync(new URL('assets/licenses/nodatime-provenance.json', root)));
  const project = readFileSync(new URL('src/Core/PVAD.Core/PVAD.Core.csproj', root), 'utf8');
  const formatter = readFileSync(new URL('src/Core/PVAD.Core/Primitives/DesignerDateFormatter.cs', root), 'utf8');
  assert.equal(provenance.version, '3.3.7');
  assert.match(project, /PackageReference Include="NodaTime" Version="3\.3\.7"/);
  assert.match(project, /third-party\/NodaTime\/LICENSE\.txt/);
  assert.match(project, /third-party\/NodaTime\/NOTICE\.txt/);
  assert.match(project, /CopyToPublishDirectory="PreserveNewest"/);
  assert.match(formatter, /DateTimeZoneProviders\.Tzdb/);
  assert.doesNotMatch(formatter, /FindSystemTimeZoneById/);
  for (const [file, expected] of [[provenance.licenseFile, provenance.licenseSha256], [provenance.noticeFile, provenance.noticeSha256]])
    assert.equal(createHash('sha256').update(readFileSync(new URL('assets/licenses/' + file, root))).digest('hex'), expected);
});

test('explicit Linux/x64 approval preserves seventeen cases and cannot bless Windows or new pixels', () => {
  const root = new URL('../../', import.meta.url);
  const directory = new URL('test-assets/visual-parity/approvals/2026-11-14-linux-x64/', root);
  const approval = JSON.parse(readFileSync(new URL('approval.json', directory)));
  const hash = file => createHash('sha256').update(readFileSync(file)).digest('hex');
  assert.equal(approval.confirmation, 'Alle 17 Vergleiche angesehen und f\u00fcr Linux/x64 freigegeben');
  assert.equal(approval.status, 'approved');
  assert.equal(approval.platform, 'linux');
  assert.equal(approval.architecture, 'x64');
  assert.match(approval.scope, /No changed pixels, Windows approval/);
  assert.equal(hash(new URL(approval.environmentFile, directory)), approval.environmentSha256);
  for (const fixture of approval.fixtures) assert.equal(hash(new URL(fixture.file, root)), fixture.sha256);
  assert.equal(approval.fonts.length, 13);
  for (const font of approval.fonts) assert.equal(hash(new URL('assets/fonts/' + font.file, root)), font.sha256);
  assert.equal(approval.cases.length, 17);
  assert.equal(new Set(approval.cases.map(entry => `${entry.fixture}/${entry.id}`)).size, 17);
  const expected = {
    'font-families': ['serif-bold', 'serif-bold-italic', 'mono-regular', 'mono-bold', 'mono-italic', 'mono-bold-italic', 'mono-date', 'mono-number'],
    'date-layout': ['line-height-points', 'short-height', 'unbroken-date', 'right-narrow'],
    'all-elements': ['text', 'date', 'number', 'field', 'link'],
  };
  for (const [fixture, ids] of Object.entries(expected))
    assert.deepEqual(approval.cases.filter(entry => entry.fixture === fixture).map(entry => entry.id).sort(), ids.sort());
  for (const entry of approval.cases) {
    assert.equal(entry.severity, 'P2');
    assert.deepEqual(entry.crops.map(crop => crop.surface).sort(), ['backend', 'canvas', 'csharp', 'preview']);
    for (const crop of entry.crops) {
      assert.equal(hash(new URL(crop.file, directory)), crop.sha256);
      assert.match(crop.pixelSha256, /^[a-f0-9]{64}$/);
    }
  }
  const continuity = JSON.parse(readFileSync(new URL('continuity.json', directory)));
  assert.equal(continuity.approvalSha256, hash(new URL('approval.json', directory)));
  assert.equal(continuity.originalSourceCommit, approval.sourceCommit);
  assert.equal(continuity.verifiedSourceCommit, '76ad71013788af674a7e5beb412926c3afd6fa4c');
  assert.equal(continuity.verifiedCropCount, 68);
  assert.equal(continuity.crops.length, 68);
  assert.equal(new Set(continuity.crops.map(crop => `${crop.fixture}/${crop.id}/${crop.surface}`)).size, 68);
  for (const crop of continuity.crops) {
    const original = approval.cases.find(entry => entry.fixture === crop.fixture && entry.id === crop.id)
      ?.crops.find(entry => entry.surface === crop.surface);
    assert.ok(original);
    assert.equal(crop.pixelSha256, original.pixelSha256);
    assert.match(crop.currentSha256, /^[a-f0-9]{64}$/);
  }
});

test('explicit Windows/x64 approval pins twenty-two cases without changing measured P2 results', () => {
  const root = new URL('../../', import.meta.url);
  const directory = new URL('test-assets/visual-parity/approvals/2026-11-15-windows-x64/', root);
  const approval = JSON.parse(readFileSync(new URL('approval.json', directory)));
  const hash = file => createHash('sha256').update(readFileSync(file)).digest('hex');
  assert.equal(approval.confirmation, 'Alle 22 Vergleiche angesehen und f\u00fcr Windows/x64 freigegeben');
  assert.equal(approval.status, 'approved');
  assert.equal(approval.platform, 'win32');
  assert.equal(approval.architecture, 'x64');
  assert.match(approval.scope, /P2 measurements and thresholds remain unchanged/);
  assert.match(approval.scope, /other Windows\/browser versions/);
  assert.equal(hash(new URL(approval.environmentFile, directory)), approval.environmentSha256);
  for (const fixture of approval.fixtures) assert.equal(hash(new URL(fixture.file, root)), fixture.sha256);
  assert.equal(approval.fonts.length, 13);
  for (const font of approval.fonts) assert.equal(hash(new URL('assets/fonts/' + font.file, root)), font.sha256);
  const expected = {
    'font-families': ['serif-regular', 'serif-bold', 'serif-italic', 'serif-bold-italic', 'mono-regular', 'mono-bold', 'mono-italic', 'mono-bold-italic', 'serif-richtext', 'mono-richtext', 'mono-number'],
    'date-layout': ['narrow-fr', 'narrow-de', 'bold-italic', 'line-height-factor', 'short-height', 'unbroken-date'],
    'all-elements': ['text', 'number', 'field', 'optionlist', 'button'],
  };
  assert.equal(approval.cases.length, 22);
  assert.equal(new Set(approval.cases.map(entry => `${entry.fixture}/${entry.id}`)).size, 22);
  for (const [fixture, ids] of Object.entries(expected))
    assert.deepEqual(approval.cases.filter(entry => entry.fixture === fixture).map(entry => entry.id).sort(), ids.sort());
  for (const entry of approval.cases) {
    assert.equal(entry.severity, 'P2');
    assert.deepEqual(entry.crops.map(crop => crop.surface).sort(), ['backend', 'canvas', 'csharp', 'preview']);
    for (const crop of entry.crops) {
      assert.equal(hash(new URL(crop.file, directory)), crop.sha256);
      assert.match(crop.pixelSha256, /^[a-f0-9]{64}$/);
    }
  }
});
