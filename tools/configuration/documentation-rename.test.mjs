import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import test from 'node:test';
import { dirname, relative, resolve } from 'node:path';
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
  const paths = readdirSync(resolve(root, 'checklists'), { recursive: true, withFileTypes: true })
    .filter(entry => entry.isFile() && entry.name.endsWith('.md'))
    .map(entry => relative(root, resolve(entry.parentPath, entry.name)));
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
      const path = target.startsWith('/downloads/pdf-trust/')
        ? resolve(root, 'websites/PVAD.Documentation/public', `.${target}`)
        : resolve(root, dirname(to), target);
      assert.equal(existsSync(path), true, `${to}: ${href}`);
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
  assert.match(read('docs/code-designer-workspace.md'), /accepts historical element-ID comments/);
  assert.match(read('docs/architecture/designer-assets.md'), /Saving a `\.pvadp` or `\.pvads`/);
  assert.match(read('docs/architecture/designer-assets.md'), /Earlier project formats remain readable/);
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
  assert.match(legal, /\/protected\/pvad-backups\/pvad-UTC\.dump/);
  assert.match(legal, /compatibility values/);
  assert.ok(restore.includes("'RESTORE PXA DATABASE'"));
  assert.ok(legal.includes("PVAD_RESTORE_CONFIRM='RESTORE PXA DATABASE'"));
  assert.ok(backup.includes('/pvad-$timestamp-$$.dump'));
  assert.ok(legal.includes('Existing `pxa-*`'));
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

test('rename upgrade guidance distinguishes fresh storage, retained bindings and completed source work', () => {
  const guide = read('docs/pvad-rename-migration-notes.md');
  const licenses = read('docs/pvad-license-and-job-compatibility.md');
  assert.match(guide, /technical source rename is complete on `develop`/);
  assert.match(guide, /fresh API Compose definition mounts `pvad-api-data`/);
  assert.match(guide, /`PVAD_LEGACY_API_VOLUME`/);
  assert.match(guide, /pvad_license_private_key/);
  assert.doesNotMatch(guide, /deliberately retains `pxa-api-data`|retained-job encryption/);
  assert.match(licenses, /diagnostics remain stable compatibility identifiers/);
  assert.match(licenses, /Encryption at rest is a separate capability/);
  assert.doesNotMatch(licenses, /pending the\s+separate diagnostic-family migration/);
});

test('pending release guide links use existing portable Markdown downloads', () => {
  for (const file of readdirSync(resolve(root, 'product-metadata/release-fragments')).filter(file => file.endsWith('.json'))) {
    const fragment = JSON.parse(read(`product-metadata/release-fragments/${file}`));
    for (const href of fragment.documentation ?? []) {
      if (!href.startsWith('/docs/guides/')) continue;
      assert.ok(href.endsWith('.md'), `${file}: ${href}`);
      assert.ok(exists(href.slice(1)), `${file}: ${href}`);
    }
  }
});

test('active product copy and guide downloads reserve the old brand for one About notice', () => {
  const company = read('websites/PVAD.Company/src/main.js');
  assert.equal((company.match(/PXA/g) ?? []).length, 1);
  assert.match(company, /id="rename-history"/);
  assert.doesNotMatch(read('websites/PVAD.Documentation/src/main.js'), /PXA|Power Dox Automation/);
  for (const path of ['docs/charts.md', 'docs/code-designer-workspace.md',
    'docs/native-designer-project-formats.md', 'docs/architecture/designer-assets.md',
    'docs/guides/PVAD.EInvoicing.md']) {
    assert.doesNotMatch(read(path), /pxa|powerdox/i, path);
  }
  for (const language of ['en', 'de', 'fr', 'es', 'it', 'ar']) {
    const common = JSON.parse(read(`pvad-designer/src/locales/${language}/common.json`));
    assert.ok(common.aboutPvaDe.includes('PVADE'), language);
  }
});

const historicalChecklist = /\/(?:Rename-|PXA\.Dependency-|PVAD\.(?:Rename-|Account-Branch-Review|Designer-Canvas-Audit|Competitive-|Software-Valuation)|PHASE|CLEAN_ARCHITECTURE)/;
const trackedChecklists = () => execFileSync('git', ['ls-files', 'checklists/*.md'],
  { cwd: root, encoding: 'utf8' }).trim().split('\n').filter(exists);
const withoutLinkTargets = line => line.replace(/\]\([^)]*\)/g, ']');

test('current checklist titles and open tasks do not prescribe obsolete product names', () => {
  const oldName = /\bPXA\b|\bPxa[A-Z]|\bpxa-designer\b|powerdox(?:-pxa|\/pxa)/;
  for (const path of trackedChecklists().filter(path => !historicalChecklist.test(path))) {
    const lines = read(path).split('\n');
    assert.doesNotMatch(lines[0], oldName, path);
    for (const [index, line] of lines.entries()) {
      if (!/^\s*[-*] \[ \]/.test(line)) continue;
      assert.doesNotMatch(withoutLinkTargets(line), oldName, `${path}:${index + 1}`);
    }
  }
});

test('future SDK plans use canonical names without claiming registered publishers', () => {
  const names = ['SDK-Roadmap', 'Java-SDK', 'Python-SDK', 'TypeScript-JavaScript-SDK',
    'CSharp-REST-SDK', 'Go-SDK', 'PHP-SDK', 'PowerShell-SDK'];
  for (const name of names) {
    const source = read(`checklists/PVAD.${name}.md`).replace(/^> Naming history:.*$/gm, '');
    assert.doesNotMatch(source, /pxa|powerdox/i, name);
  }
  const roadmap = read('checklists/PVAD.SDK-Roadmap.md');
  assert.match(roadmap, /canonical routes under `\/api\/pvad\/v1`/);
  assert.match(roadmap, /No SDK publisher or package name is currently registered/);
  assert.match(roadmap, /display aliases do not rename existing wire identifiers/);
  assert.match(read('checklists/PVAD.Java-SDK.md'), /proposed `com\.pvade:pvad-java`/);
  assert.match(read('checklists/PVAD.TypeScript-JavaScript-SDK.md'), /proposed `@pvade\/pvad`/);
  assert.match(read('checklists/PVAD.Python-SDK.md'), /`pvad` import namespace/);
});

test('current logo and format briefs distinguish market identity from technical contracts', () => {
  const brand = read('checklists/PVAD.Brand-And-Logo-System.md').replace(/^> Naming history:.*$/gm, '');
  assert.doesNotMatch(withoutLinkTargets(brand), /\bPXA\b|Power Dox Automation|\bPXAP\b|\bPXAS\b/);
  assert.match(brand, /\*\*PVADE\*\* as the visible platform and Designer wordmark/);
  assert.match(brand, /still-unconfirmed legal company name/);
  const formats = read('checklists/PVAD.Native-Designer-Formats.md');
  assert.match(formats, /`\.pvadp` for PDF Designer/);
  assert.match(formats, /`\.pvads` for Spreadsheet Designer/);
  assert.match(formats, /Checked implementation entries below describe the original format generation/);
  assert.match(formats, /application\/vnd\.pvad\.pvadp\+zip/);
  assert.match(read('checklists/Documentation-Audit.md'), /pvad-checklist-naming\.md/);
});

test('checklist naming review links resolve to current guides or explicitly retained history', () => {
  const path = 'docs/pvad-checklist-naming.md';
  for (const [, href] of read(path).matchAll(/\]\(([^\s)]+)\)/g)) {
    assert.ok(existsSync(resolve(root, dirname(path), href.split('#')[0])), href);
  }
});
