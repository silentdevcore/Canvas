import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const root = new URL('../../', import.meta.url);
const manifest = JSON.parse(readFileSync(new URL('assets/fonts/liberation/serif-mono-provenance.json', root), 'utf8'));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

test('all eight unmodified Serif and Mono faces have exact provenance and browser style coverage', () => {
  assert.equal(manifest.version, '2.1.5');
  assert.equal(manifest.faces.length, 8);
  const css = readFileSync(new URL('pvad-designer/src/styles/editor.css', root), 'utf8');
  for (const family of ['Serif', 'Mono']) {
    const faces = manifest.faces.filter(face => face.family === family);
    assert.deepEqual(faces.map(face => face.style).sort(), ['Bold', 'BoldItalic', 'Italic', 'Regular']);
    for (const face of faces) {
      assert.equal(hash(readFileSync(new URL('assets/fonts/liberation/' + face.file, root))), face.sha256);
      const rule = [...css.matchAll(/@font-face\s*\{([^}]+)\}/g)].find(match => match[1].includes(face.file))?.[1];
      assert.ok(rule, face.file);
      assert.ok(rule.includes(`font-family: 'PVADE ${family === 'Serif' ? 'Times' : 'Courier'}'`));
      assert.ok(rule.includes(`font-weight: ${face.style.includes('Bold') ? 700 : 400}`));
      assert.ok(rule.includes(`font-style: ${face.style.includes('Italic') ? 'italic' : 'normal'}`));
    }
  }
});

test('font licenses are shipped with the browser and API without changing their text', () => {
  const browser = readFileSync(new URL(manifest.browserLicense, root));
  assert.equal(hash(browser), manifest.browserLicenseSha256);
  const api = readFileSync(new URL('assets/fonts/liberation/' + manifest.licenseFile, root), 'utf8');
  assert.equal(browser.toString().replace(/\s/g, ''), api.replace(/\s/g, ''));
  const project = readFileSync(new URL('PVAD.WebApi/PVAD.WebApi.csproj', root), 'utf8');
  assert.match(project, /assets\/fonts\/liberation\/\*/);
  assert.match(project, /CopyToPublishDirectory="PreserveNewest"/);
});

test('the font parity pack covers both families, four styles and all scoped consumers', () => {
  const fixture = JSON.parse(readFileSync(new URL('test-assets/visual-parity/font-families.json', root), 'utf8'));
  const elements = fixture.pages.flatMap(page => page.elements);
  assert.equal(fixture.pages.length, 3);
  assert.equal(elements.length, 22);
  assert.equal(new Set(elements.map(element => element.id)).size, elements.length);
  for (const family of ['serif', 'mono']) {
    for (const style of ['regular', 'bold', 'italic', 'bold-italic', 'center', 'right', 'richtext', 'table', 'date', 'link', 'number'])
      assert.ok(elements.some(element => element.id === `${family}-${style}`));
  }
});

test('CI enforces the dedicated font pack and preserves evidence separately from the catalog', () => {
  const workflow = readFileSync(new URL('.github/workflows/ci.yml', root), 'utf8');
  const run = workflow.indexOf('- name: Enforce packaged font family parity');
  const archive = workflow.indexOf('- name: Preserve font family parity evidence');
  const catalog = workflow.indexOf('- name: Enforce approved visual parity');
  assert.ok(run > 0 && run < archive && archive < catalog);
  assert.match(workflow.slice(run, archive), /PVAD_VISUAL_PARITY_ENFORCE: true/);
  assert.match(workflow.slice(run, archive), /PVAD_VISUAL_PARITY_FIXTURE: test-assets\/visual-parity\/font-families.json/);
  assert.match(workflow.slice(archive, catalog), /if: always\(\) && steps\.font-parity\.outcome != 'skipped'/);
  assert.match(workflow, /path: artifacts\/font-family-parity/);
});

test('owner-approved Serif/Mono raster crops retain exact fixture, font and image hashes', () => {
  const directory = new URL('test-assets/visual-parity/approvals/2026-11-14-font-families/', root);
  const approval = JSON.parse(readFileSync(new URL('approval.json', directory), 'utf8'));
  assert.equal(approval.status, 'approved');
  assert.equal(approval.approvalSource, 'Explicit user confirmation after viewing all three 200% Serif/Mono comparisons.');
  assert.equal(hash(readFileSync(new URL(approval.fixture, root))), approval.fixtureSha256);
  assert.deepEqual(approval.cases.map(entry => entry.id).sort(), ['mono-bold', 'mono-bold-italic', 'serif-bold']);
  for (const face of approval.faces)
    assert.equal(hash(readFileSync(new URL('assets/fonts/liberation/' + face.file, root))), face.sha256);
  for (const entry of approval.cases) {
    assert.deepEqual(entry.crops.map(crop => crop.surface).sort(), ['backend', 'canvas', 'csharp', 'preview']);
    for (const crop of entry.crops)
      assert.equal(hash(readFileSync(new URL(crop.file, directory))), crop.sha256);
  }
  assert.match(approval.scope, /P2 metrics and regression thresholds remain unchanged/);
});

test('production document scopes use fractional text metrics in normal, shared and parity elements', () => {
  const css = readFileSync(new URL('pvad-designer/src/styles/editor.css', root), 'utf8');
  const scope = css.match(/\[data-element-id\],\s*\[data-element-id\] \*\s*\{([^}]+)\}/)?.[1];
  assert.ok(scope);
  assert.match(scope, /text-rendering:\s*geometricPrecision/);
  assert.match(scope, /font-kerning:\s*none/);
  const editor = readFileSync(new URL('pvad-designer/src/components/Editor/SimplePvadSurface.tsx', root), 'utf8');
  assert.equal([...editor.matchAll(/data-element-id=\{element\.id\}/g)].length, 3);
});

test('Linux owner approval pins thirteen platform-specific cases without waiving P2 metrics', () => {
  const directory = new URL('test-assets/visual-parity/approvals/2026-11-14-linux-arm64/', root);
  const approval = JSON.parse(readFileSync(new URL('approval.json', directory), 'utf8'));
  assert.equal(approval.status, 'approved');
  assert.equal(approval.confirmation, 'Alle 13 Vergleiche angesehen und freigegeben');
  assert.equal(approval.environment.platform, 'linux');
  assert.equal(approval.environment.architecture, 'arm64');
  assert.equal(approval.environment.browserVersion, '156.0.8078.80');
  assert.match(approval.scope, /P2 metrics and regression thresholds remain unchanged/);
  assert.match(approval.scope, /No changed pixels, Linux\/x64 CI, Windows/);
  assert.equal(approval.fixtures.length, 2);
  for (const fixture of approval.fixtures)
    assert.equal(hash(readFileSync(new URL(fixture.file, root))), fixture.sha256);
  assert.equal(approval.faces.length, 12);
  for (const face of approval.faces)
    assert.equal(hash(readFileSync(new URL('assets/fonts/liberation/' + face.file, root))), face.sha256);
  const expected = {
    'font-families': ['serif-bold', 'serif-bold-italic', 'mono-regular', 'mono-bold', 'mono-italic', 'mono-bold-italic', 'mono-date', 'mono-number'],
    'all-elements': ['text', 'date', 'number', 'field', 'link'],
  };
  assert.equal(approval.cases.length, 13);
  assert.equal(new Set(approval.cases.map(entry => `${entry.fixture}/${entry.id}`)).size, 13);
  for (const [fixture, ids] of Object.entries(expected))
    assert.deepEqual(approval.cases.filter(entry => entry.fixture === fixture).map(entry => entry.id).sort(), ids.sort());
  for (const entry of approval.cases) {
    assert.equal(entry.severity, 'P2');
    assert.deepEqual(entry.crops.map(crop => crop.surface).sort(), ['backend', 'canvas', 'csharp', 'preview']);
    for (const crop of entry.crops) {
      assert.equal(hash(readFileSync(new URL(crop.file, directory))), crop.sha256);
      assert.match(crop.pixelSha256, /^[a-f0-9]{64}$/);
    }
  }
});
