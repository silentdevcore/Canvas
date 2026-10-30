import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import test from 'node:test';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
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
    assert.doesNotMatch(read(to), /^> Naming history:/m, to);
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

test('repository checklists no longer repeat the naming-history banner', () => {
  for (const path of trackedChecklists()) {
    assert.doesNotMatch(read(path), /^> Naming history:/m, path);
  }
  for (const path of ['Removing-PDFImporter.md', 'handoff.md', 'implementation_plan.md'])
    assert.doesNotMatch(read(path), /^> Naming history:/m, path);
  const migration = read('tools/configuration/rename-documentation.mjs');
  assert.doesNotMatch(migration, /Naming history:/);
  assert.match(JSON.parse(read('docs/pvad-documentation-path-migration.json')).preservationScope,
    /Original path-migration checkpoint/);
});

test('documentation path migration preserves task text and ignores untracked local notes', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'pvad-doc-path-fixture-'));
  try {
    mkdirSync(join(fixture, 'checklists'));
    mkdirSync(join(fixture, 'docs'));
    const source = '# Example\n\n- [ ] Keep work open.\n';
    writeFileSync(join(fixture, 'checklists/PXA.Example.md'), source);
    writeFileSync(join(fixture, 'checklists/PXA.Local.md'), 'Private synthetic local note.\n');
    execFileSync('git', ['init', '--quiet'], { cwd: fixture });
    execFileSync('git', ['add', '--', 'checklists/PXA.Example.md'], { cwd: fixture });
    execFileSync(process.execPath, [resolve(root, 'tools/configuration/rename-documentation.mjs')],
      { cwd: fixture });
    assert.equal(readFileSync(join(fixture, 'checklists/PVAD.Example.md'), 'utf8'), source);
    assert.ok(existsSync(join(fixture, 'checklists/PXA.Local.md')));
    assert.equal(existsSync(join(fixture, 'checklists/PVAD.Local.md')), false);
    const mapping = JSON.parse(readFileSync(join(fixture, 'docs/pvad-documentation-path-migration.json'), 'utf8'));
    assert.deepEqual(mapping.moves, [{ from: 'checklists/PXA.Example.md', to: 'checklists/PVAD.Example.md' }]);
  } finally {
    rmSync(fixture, { recursive: true, force: true });
  }
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

const historicalChecklist = /\/(?:Rename-|PXA\.Dependency-|PVAD\.(?:Rename-|Legacy-Rename-|Dependency-PR-Review|Account-Branch-Review|Designer-Canvas-Audit))/;
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
    const source = read(`checklists/PVAD.${name}.md`);
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
  const brand = read('checklists/PVAD.Brand-And-Logo-System.md');
  assert.doesNotMatch(withoutLinkTargets(brand), /\bPXA\b|Power Dox Automation|\bPXAP\b|\bPXAS\b/);
  assert.match(brand, /\*\*PVADE\*\* as the visible platform and Designer wordmark/);
  assert.match(brand, /still-unconfirmed legal company name/);
  const formats = read('checklists/PVAD.Native-Designer-Formats.md');
  assert.match(formats, /`\.pvadp` for PDF Designer/);
  assert.match(formats, /`\.pvads` for Spreadsheet Designer/);
  assert.match(formats, /Task wording uses current names/);
  assert.match(formats, /application\/vnd\.pvad\.pvadp\+zip/);
  assert.match(read('checklists/Documentation-Audit.md'), /pvad-checklist-naming\.md/);
});

test('ordinary completed-task wording and code examples use current names without changing compatibility identifiers', () => {
  const gembox = read('checklists/Code-Migration-GemBoxPdf.md');
  assert.match(gembox, /using PVAD\.Pdf;/);
  assert.doesNotMatch(gembox, /PXA|pxa-designer/);
  const persistence = read('checklists/PVAD.Designer-Template-Persistence.md');
  assert.match(persistence, /`PVAD\.Infrastructure\.Persistence`/);
  assert.match(persistence, /`\/api\/pvad\/v1\/designer\/templates`/);
  assert.match(read('PVAD.WebApi/Controllers/DesignerTemplatesController.cs'),
    /\[Route\("api\/pvad\/v1\/designer\/templates"\)\]/);
  const trust = read('checklists/PVAD.PDF-Trust-Foundation.md');
  assert.match(trust, /PXAPDFTRUST/);
  assert.match(read('checklists/Code-Migration-SyncfusionPdf.md'),
    /Verified with `dotnet test tests\/PXA\.Migration/);
  assert.match(read('checklists/PVAD.Competitive-Readiness-And-Milestones.md'),
    /^# PVADE Competitive Readiness/m);
  assert.match(read('checklists/PVAD.Competitive-Readiness-And-Milestones-DE.md'),
    /^# PVADE Wettbewerbsreife/m);
});

test('checklist naming review links resolve to current guides or explicitly retained history', () => {
  const path = 'docs/pvad-checklist-naming.md';
  for (const [, href] of read(path).matchAll(/\]\(([^\s)]+)\)/g)) {
    assert.ok(existsSync(resolve(root, dirname(path), href.split('#')[0])), href);
  }
});

test('remaining checklist filenames and current links use the canonical paths', () => {
  const { moves } = JSON.parse(read('docs/pvad-documentation-path-migration.json'));
  const renamed = [
    ['PXA.Dependency-PR-Review-2026-10-26.md', 'PVAD.Dependency-PR-Review-2026-10-26.md'],
    ['Rename-PXA-To-PVAD.md', 'PVAD.Rename-Plan.md'],
    ['Rename-To-PXA.md', 'PVAD.Legacy-Rename-Archive.md'],
  ];
  for (const [from, to] of renamed) {
    assert.ok(moves.some(move => move.from === `checklists/${from}` && move.to === `checklists/${to}`));
    assert.equal(exists(`checklists/${from}`), false);
    assert.ok(exists(`checklists/${to}`));
  }
  assert.match(read('README.md'), /checklists\/PVAD\.Rename-Plan\.md/);
  assert.match(read('TESTING.md'), /checklists\/PVAD\.Dependency-PR-Review-2026-10-26\.md/);
  assert.match(read('checklists/PVAD.Rename-Plan.md'), /\]\(PVAD\.Legacy-Rename-Archive\.md\)/);
});

test('specialty milestone identifiers use PVAD without renaming compatibility diagnostics', () => {
  const roadmap = read('checklists/PVAD.PDF-Specialized-Features-Roadmap.md');
  const ids = ['EINV', 'LEGAL', 'FORMS', 'CAPTURE', 'PRINT', 'OPT', 'VT', 'GEO', 'ENG', 'RICH', 'PROV'];
  const headings = [...roadmap.matchAll(/^### (PVAD-PDF-[A-Z]+) -/gm)].map(match => match[1]);
  assert.deepEqual(headings, ids.map(id => `PVAD-PDF-${id}`));
  assert.doesNotMatch(roadmap, /PXA-PDF-/);
  const ledger = JSON.parse(read('docs/pvad-rename-reviewed-exceptions.json'));
  assert.ok(ledger.files.every(entry => !entry.reviewNotes.startsWith('Explicit naming-history notice states')));
});

test('migration instructions and current result examples match renamed source contracts', () => {
  const ui = read('checklists/Code-Migrations-UI.md');
  assert.doesNotMatch(ui, /pxa/i);
  assert.match(ui, /"pvadCode": "using PVAD\.Pdf;/);
  assert.match(ui, /Record: `PvadCode`, `Diagnostics`, `Summary`/);
  assert.match(read('PVAD.WebApi/Services/MigrationResult.cs'), /string PvadCode/);
  assert.match(read('PVAD.WebApi/Controllers/MigrationController.cs'), /pvadCode = result\.PvadCode/);
  const port = read('pvad-designer/vite.config.ts').match(/port:\s*(\d+)/)[1];
  assert.ok(ui.includes(`http://localhost:${port}/pdf/migrations/code`));
  assert.ok(ui.includes('npm --prefix pvad-designer run dev'));
  assert.match(read('checklists/Migration-Namespace-Taxonomy.md'),
    /Use `\/api\/pvad\/v1\/migrations\/\{domain\}\/\{kind\}\/\{provider\}` as the canonical/);
  assert.match(read('checklists/PVAD.Unified-Migration-Frameworks.md'),
    /`GET \/api\/pvad\/v1\/migrations\/catalog`/);
  assert.match(read('checklists/PVAD.Migration-Nutrient-Code-And-Viewer.md'),
    /`\/api\/pvad\/v1\/migrations\/pdf\/viewer\/nutrient\/convert`/);
});

test('migration test-project references resolve without pretending to rerun historical results', () => {
  for (const [provider, folder] of [['Aspose', 'Aspose'], ['EPPlus', 'Epplus'], ['GemBox', 'GemBox']]) {
    const path = `tests/PVAD.Migration.Spreadsheet.Code.${folder}.Tests/`;
    assert.ok(read(`checklists/Spreadsheet-Migration-${provider}.md`).includes(`\`${path}\``));
    assert.ok(exists(path));
  }
  for (const provider of ['DevExpressReport', 'SyncfusionRdl'])
    assert.match(read(`checklists/Code-Migration-${provider}.md`), /`PVAD\.Export\.Tests`/);
  assert.match(read('checklists/Spreadsheet-Migration.md'), /`PvadWorkbook`/);
});

test('migration checklists retain previous names only in bounded historical records or vendor keys', () => {
  const paths = execFileSync('git', ['ls-files', '-z', '--', 'checklists'],
    { cwd: root, encoding: 'utf8' }).split('\0').filter(path => /migration.*\.md$/i.test(path));
  for (const path of paths) {
    for (const [index, original] of read(path).split('\n').entries()) {
      let line = original;
      if (/\/Code-Migration-(?:DsPdf|IronPdf|SyncfusionPdf|iText7)\.md$/.test(path) &&
          /^- \[x\] Verified with /.test(line))
        line = line.replace(/`dotnet (?:test|build) [^`]+`/g, 'recorded command');
      if (path === 'checklists/Migration-Namespace-Taxonomy.md' &&
          /^(?:  - |  -?\s*)/.test(line))
        line = line.replace(/`(?:PXA\.[^`]+|dotnet test [^`]+|\/api\/pxa\/migration\/(?:convert|report-to-design))`/g,
          'recorded taxonomy input');
      if (path === 'checklists/Spreadsheet-Migration.md')
        line = line.replaceAll('`dotnet build PXA.sln`', 'recorded build command');
      if (path === 'checklists/PVAD.Migration-Nutrient-Code-And-Viewer.md' && line.includes('now a compatibility alias'))
        line = line.replace('`POST /api/pxa/v1/migrations/pdf/viewer/convert`', 'recorded initial route');
      if (path === 'checklists/Migration_Dataset-Aggregates.md')
        line = line.replace('https://github.com/silentdevcore/PXA/pull/17', 'original pull request');
      if (path === 'checklists/Code-Migration-ActiveReportsRpx.md')
        line = line.replaceAll('rpxAutoSize', 'vendor metadata key');
      assert.doesNotMatch(line, /pxa/i, `${path}:${index + 1}`);
    }
  }
});

test('current source references use existing canonical paths across documentation areas', () => {
  const cases = [
    ['checklists/PVAD.Account.Portal-Implementation.md', /tests\/PVAD\.Api\.Tests\/[A-Za-z0-9]+\.cs/g],
    ['checklists/PVAD.E-Invoicing.md', /tests\/PVAD\.Api\.Tests\/[A-Za-z0-9]+\.cs/g],
    ['checklists/multi-languages.md', /src\/Generation\/PVAD\.Pdf\/[A-Za-z0-9./]+\.cs/g],
    ['checklists/multi-languages.md', /tests\/PVAD\.Export\.Tests\/[A-Za-z0-9]+\.cs/g],
    ['checklists/Word-Converter.md', /tests\/PVAD\.Export\.Tests\/[A-Za-z0-9]+\.cs/g],
  ];
  for (const [path, pattern] of cases) {
    const source = read(path);
    assert.doesNotMatch(source, /tests\/PXA\.(?:Api|Export)\.Tests\//, path);
    const targets = [...source.matchAll(pattern)].map(match => match[0]);
    assert.ok(targets.length > 0, path);
    for (const target of targets) assert.ok(exists(target), `${path}: ${target}`);
  }
  const runbook = read('checklists/Word-Fidelity-Beta-Runbook.md');
  assert.doesNotMatch(runbook, /pxa/i);
  assert.ok(runbook.includes('dotnet test tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj'));
  assert.ok(runbook.includes('tests/PVAD.Export.Tests/Fidelity/artifacts/latest/'));
  assert.ok(exists('tests/PVAD.Export.Tests/PVAD.Export.Tests.csproj'));
  assert.match(read('tests/PVAD.Export.Tests/WordPdfFidelityTests.cs'),
    /Path\.Combine\(repoRoot, "tests", "PVAD.Export.Tests", "Fidelity", "artifacts", "latest"\)/);
});

test('current API checklist paths no longer prescribe the previous namespace', () => {
  for (const name of ['Account.Portal-Implementation', 'Account', 'Admin-Documentation', 'Admin',
    'Application-Versioning-And-Releases', 'Designer-Authentication', 'Designer-Element-Library-And-Favorites',
    'Designer-Template-Persistence', 'E-Invoicing', 'Legal-Terms-Privacy-And-Consent',
    'Logging-Monitoring-Alerting', 'Native-Designer-Formats', 'PDF-Parser-Hardening',
    'PDF-Redaction-Preflight', 'PDF-Trust-Foundation']) {
    const path = `checklists/PVAD.${name}.md`;
    assert.doesNotMatch(read(path), /\/api\/pxa\/v1\//, path);
    assert.match(read(path), /\/api\/pvad\/v1\//, path);
  }
});

test('project-format integration and active tool paths use current identities', () => {
  for (const name of ['Code-Designer-Roundtrip', 'Designer-Per-Page-Settings-And-Backend-Parity',
    'Designer-Template-Persistence']) {
    const path = `checklists/PVAD.${name}.md`;
    assert.doesNotMatch(read(path), /\.pxap/);
    assert.match(read(path), /\.pvadp/);
  }
  assert.match(read('checklists/PVAD.MCP-Server.md'), /`tools\/PVAD\.Mcp`/);
  assert.doesNotMatch(read('checklists/PVAD.MCP-Server.md'), /tools\/PXA\.Mcp|mcp\.powerdoxautomation\.com/);
  assert.match(read('checklists/PVAD.MCP-Server.md'), /proposed host; DNS, TLS/);
  assert.ok(exists('pvad-designer/src/features/pdf-viewer/annotations.ts'));
  assert.match(read('checklists/PdfTools-WebViewer-Feature-Gaps.md'), /`pvad-designer\/src\/features\/pdf-viewer\/annotations\.ts`/);
  for (const path of ['checklists/Pdf-Encryption.md', 'checklists/Documentation-Audit.md']) {
    for (const [, href] of read(path).matchAll(/\]\(([^\s)]+)\)/g)) {
      if (/^[a-z]+:/i.test(href) || href.startsWith('#')) continue;
      assert.ok(existsSync(resolve(root, dirname(path), href.split('#')[0])), `${path}: ${href}`);
    }
  }
});
