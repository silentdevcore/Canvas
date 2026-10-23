import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import test from 'node:test';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const exists = path => existsSync(resolve(root, path));

test('all renamed active documentation targets exist without duplicate legacy files', () => {
  const mapping = JSON.parse(read('docs/pvad-documentation-path-migration.json'));
  assert.equal(mapping.legacyContentPreserved, true);
  assert.ok(mapping.moves.length >= 70);
  assert.equal(new Set(mapping.moves.map(move => move.to)).size, mapping.moves.length);
  for (const { from, to } of mapping.moves) {
    assert.equal(exists(from), false, from);
    assert.equal(exists(to), true, to);
    if (to.startsWith('checklists/')) assert.match(read(to), /Naming history:/);
  }
});

test('main guides explain active technical and platform names without contradictory rename claims', () => {
  for (const path of ['README.md', 'ARCHITECTURE.md', 'PROJECT_SUMMARY.md', 'CONTRIBUTING_RENDERERS.md', 'TESTING.md']) {
    const source = read(path);
    assert.ok(source.includes('PVAD'), path);
    assert.doesNotMatch(source, /PXA-to-PXA|PXA is now the active|Use `PXA\.\*` projects/);
    assert.doesNotMatch(source, /\| `PXA\.\*` \| Active/);
  }
  assert.match(read('README.md'), /localhost:5086/);
  assert.match(read('README.md'), /PVADP \/ PVADS/);
});

test('retained checklist names have an explicit naming-history notice', () => {
  const paths = execFileSync('git', ['ls-files', 'checklists/*.md'],
    { cwd: fileURLToPath(new URL('../../', import.meta.url)), encoding: 'utf8' })
    .trim().split('\n').filter(path => exists(path)
      && !/^checklists\/PVAD\./.test(path) && path !== 'checklists/Rename-PXA-To-PVAD.md');
  for (const path of paths) {
    const source = read(path);
    if (/PXA|Power Dox|Canvas/.test(source)) assert.match(source, /Naming history:/, path);
  }
});

test('every checklist mentioning PXA explicitly identifies its technical rename and market name', () => {
  const paths = execFileSync('rg', ['--files', 'checklists', '-g', '*.md'],
    { cwd: root, encoding: 'utf8' }).trim().split('\n');
  let reviewed = 0;
  for (const path of paths) {
    const source = read(path);
    if (!/pxa/i.test(path + '\n' + source)) continue;
    const notices = [...source.matchAll(/^> Naming history:.*$/gm)];
    assert.equal(notices.length, 1, `${path}: expected one notice`);
    const notice = notices[0][0];
    assert.match(notice, /PXA has been renamed to PVAD \(technical name\)/, path);
    assert.match(notice, /PVADE is the market name/, path);
    assert.match(notice, /task statuses are unchanged/, path);
    assert.match(source, /^# [^\n]+\n\n> Naming history:/, path);
    reviewed++;
  }
  assert.ok(reviewed > 0, 'Expected checklists containing historical names');
});

test('shared style guide and current code samples use current technical symbols', () => {
  assert.doesNotMatch(read('websites/shared/README.md'), /\bPXA\b|pxa-/);
  assert.match(read('websites/shared/vitePvadVersion.js'), /name: 'pvad-static-status-pages'/);
  assert.doesNotMatch(read('websites/PVAD.Documentation/src/main.js'), /Keywords = "invoice,pdf,pxa"/);
  for (const path of ['websites/README.md', 'websites/HOSTING.md',
    'websites/PVAD.Company/COMPANY_STRATEGY.md', 'tools/document-fidelity/README.md',
    'tools/pdfua/MATTERHORN-REVIEW.md'])
    assert.doesNotMatch(read(path), /\bPXA\b|Power Dox Automation/, path);
});

test('relative file links in renamed documents resolve after the move', () => {
  const { moves } = JSON.parse(read('docs/pvad-documentation-path-migration.json'));
  for (const { to } of moves) {
    for (const match of read(to).matchAll(/\]\(([^\s)]+)\)/g)) {
      const href = match[1];
      if (href.startsWith('#') || /^[a-z]+:/i.test(href) || href.includes('<')) continue;
      const target = decodeURIComponent(href.split('#')[0]);
      assert.equal(existsSync(resolve(fileURLToPath(new URL('../../', import.meta.url)), dirname(to), target)), true, `${to}: ${href}`);
    }
  }
});

test('active integration guides use current identities and current Designer concepts', () => {
  for (const path of ['docs/README.md', 'docs/charts.md',
    'docs/architecture/existing-pdf-boundaries.md',
    'docs/migrations/nutrient-java-document-engine-inventory.md'])
    assert.doesNotMatch(read(path), /\bPXA\b|Power Dox Automation/, path);
  const editor = JSON.parse(read('pvad-designer/src/locales/en/editor.json'));
  assert.ok(read('docs/charts.md').includes(`**${editor.toolGroups.tablesCharts}**`));
  assert.match(read('docs/code-designer-workspace.md'), /includes `pvad-element-id`/);
  assert.match(read('docs/code-designer-workspace.md'), /accepts historical `pxa-element-id`/);
  assert.match(read('docs/architecture/designer-assets.md'), /Saving a `\.pvadp` or `\.pvads`/);
  assert.match(read('docs/architecture/designer-assets.md'), /old `\.pxap` and `\.pxas` formats remain readable/);
});

test('public DocFX includes linked native and architecture sources but no operator navigation', () => {
  const config = JSON.parse(read('docs/docfx.json'));
  const files = config.build.content.flatMap(entry => entry.files);
  for (const path of ['native-designer-project-formats.md', 'architecture/designer-assets.md']) {
    assert.ok(files.includes(path), path);
    assert.ok(exists(`docs/${path}`), path);
  }
  assert.doesNotMatch(read('docs/toc.yml'), /operations\/|operator-docs|AdminDocumentation/);
  assert.ok(files.every(path => !/operations|operator-docs|AdminDocumentation/.test(path)));
});

test('hosting guidance separates candidate origins from actual source fallbacks and keeps canonical input names', async () => {
  const profile = JSON.parse(read('deploy/rename/pvade-targets.candidate.json'));
  const hosting = read('websites/HOSTING.md');
  const strategy = read('websites/PVAD.Company/COMPANY_STRATEGY.md');
  const { siteLinkDefaults } = await import('../../websites/shared/siteLinks.js');
  for (const document of [hosting, strategy]) {
    assert.match(document, /unverified/);
    assert.match(document, /Current source/);
    for (const role of ['company', 'documentation', 'demo', 'account'])
      assert.ok(document.includes(profile.hosts[role]), role);
  }
  for (const role of ['company', 'documentation', 'demo', 'designer', 'account', 'operator']) {
    assert.ok(hosting.includes(profile.hosts[role]), role);
    if (role !== 'operator') assert.ok(hosting.includes(siteLinkDefaults.production[role]), role);
    assert.ok(hosting.includes(`VITE_PVAD_${role.toUpperCase()}_URL`), role);
  }
  assert.match(hosting, /Old `VITE_PXA_\*` keys remain compatibility aliases/);
});

test('operator examples retain executable backup contracts without recommending obsolete sample paths', () => {
  const legal = read('tools/legal/README.md');
  const backup = read('tools/legal/backup-postgres.sh');
  const restore = read('tools/legal/restore-postgres.sh');
  assert.match(legal, /unverified deployment target example/);
  assert.ok(legal.includes(JSON.parse(read('deploy/rename/pvade-targets.candidate.json')).hosts.api));
  assert.match(legal, /\/protected\/pvad-backups\/pxa-UTC\.dump/);
  assert.match(legal, /compatibility values/);
  assert.ok(restore.includes("'RESTORE PXA DATABASE'"));
  assert.ok(legal.includes("PVAD_RESTORE_CONFIRM='RESTORE PXA DATABASE'"));
  assert.ok(backup.includes('/pxa-$timestamp-$$.dump'));
  assert.doesNotMatch(legal, /\/run\/secrets\/pxa_|\/protected\/pxa-backups/);
  assert.doesNotMatch(read('tools/pdfua/README.md'), /\/tmp\/pxa-/);
});

test('capacity examples show the current API-key prefix without changing old readers', () => {
  const source = read('PVAD.WebApi/Security/PvadApiKeySecret.cs');
  assert.ok(source.includes('var prefix = $"pvad_'));
  assert.ok(source.includes('secret.StartsWith("pxa_"'));
  const examples = read('deploy/observability/CAPACITY-PROFILES.md');
  assert.ok(examples.includes("PVAD_CAPACITY_API_KEY='pvad_<synthetic-service-account-key>'"));
  assert.doesNotMatch(examples, /PVAD_CAPACITY_API_KEY='pxa_/);
});
