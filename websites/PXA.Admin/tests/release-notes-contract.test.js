import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [source, css, manifest, checklist] = await Promise.all([
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
  readFile(new URL('../../../product-metadata/pxa-releases.json', import.meta.url), 'utf8')
    .then(JSON.parse),
  readFile(
    new URL('../../../checklists/PXA.Application-Versioning-And-Releases.md', import.meta.url),
    'utf8',
  ),
]);

test('Admin exposes authenticated release notes from the shared PXA manifest', () => {
  assert.match(source, /pxaReleaseManifest from '\.\.\/\.\.\/\.\.\/product-metadata\/pxa-releases\.json'/);
  assert.match(source, /path: '\/release-notes', label: 'Release notes', group: 'Reference'/);
  assert.match(source, /if \(location\.pathname === '\/release-notes'\)/);
  assert.match(source, /renderShell\(releaseNotesPage\(\), 'Release notes'\)/);
});

test('Admin renders every release category and affected components', () => {
  for (const category of ['added', 'improved', 'fixed', 'security', 'deprecated', 'breaking'])
    assert.match(source, new RegExp(`\\['${category}',`));
  assert.match(source, /release\.components\.map/);
  assert.match(source, /release\.changes\[key\]\.map/);
  assert.match(css, /\.admin-release-changes \{ grid-template-columns: 1fr; \}/);
  assert.ok(manifest.releases.length > 0);
  assert.ok(manifest.releases.every((release) => release.components.length > 0));
});

test('the versioning checklist records the Admin release-notes integration', () => {
  assert.match(
    checklist,
    /\[x\] Show authenticated release notes in PXA Admin from the shared release manifest\./,
  );
});
