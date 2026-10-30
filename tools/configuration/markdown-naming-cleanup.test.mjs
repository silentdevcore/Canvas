import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, lstatSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { scanMarkdownNames } from './markdown-path-audit.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const json = path => JSON.parse(read(path));
const digest = bytes => createHash('sha256').update(bytes).digest('hex');
const publicPath = 'docs/reference/compatibility-identifiers.v1.json';
const operatorPath = 'operator-docs/compatibility-identifiers.v1.json';

test('all tracked regular Markdown is free of previous-name substrings without archive exceptions', () => {
  const report = scanMarkdownNames(root);
  assert.ok(report.filesScanned >= 272);
  assert.deepEqual(report.records, []);
  assert.doesNotMatch(read('checklists/PVAD.Markdown-Naming-Cleanup.md'), /pxa/i);
  assert.ok(read('.github/workflows/ci.yml').includes('markdown-path-audit.mjs --all --check'));
});

test('the complete occurrence inventory agrees with original Git bytes and positions', () => {
  const inventory = json('checklists/PVAD.Markdown-Naming-Audit.json');
  assert.equal(inventory.filesScanned, 272);
  assert.equal(inventory.matchingFiles, 113);
  assert.equal(inventory.occurrences, 2058);
  assert.equal(inventory.files.length, 113);
  assert.equal(new Set(inventory.files.map(file => file.path)).size, 113);
  let total = 0;
  for (const file of inventory.files) {
    assert.doesNotMatch(file.path, /^(?:\.vscode|\.git)\//);
    const bytes = execFileSync('git', ['show', `${inventory.baseline}:${file.path}`], { cwd: root });
    assert.equal(digest(bytes), file.sha256, file.path);
    const lines = bytes.toString('utf8').split('\n');
    assert.equal([...bytes.toString('utf8').matchAll(/pxa/gi)].length, file.occurrences);
    assert.equal(file.matches.length, file.occurrences);
    for (const match of file.matches) {
      assert.equal(lines[match.line - 1].slice(match.column - 1, match.column + 2), match.match);
      assert.ok(lines[match.line - 1].includes(match.token));
    }
    total += file.occurrences;
  }
  assert.equal(total, 2058);
});

test('compatibility registers have distinct stable identities and a pinned public Legal contract', () => {
  const ids = new Set();
  for (const [path, visibility] of [[publicPath, 'public'], [operatorPath, 'operator']]) {
    const registry = json(path);
    assert.equal(registry.schemaVersion, 1);
    assert.equal(registry.version, '1');
    assert.equal(registry.visibility, visibility);
    for (const entry of registry.entries) {
      assert.match(entry.id, /^compat-[a-f0-9]{12}$/);
      assert.equal(ids.has(entry.id), false, entry.id);
      ids.add(entry.id);
      assert.equal(entry.canonicalAlias, null);
      assert.ok(entry.value.length && entry.sources.length);
      for (const source of entry.sources) assert.ok(existsSync(resolve(root, source)), source);
    }
  }
  const manifest = json('product-metadata/legal-documents/en/manifest.json');
  assert.equal(manifest.compatibilityReference.file, publicPath);
  assert.equal(manifest.compatibilityReference.sha256, digest(readFileSync(resolve(root, publicPath))));
  assert.equal(manifest.operatorProfile.legalName, '[LEGAL ENTITY NAME]');
  for (const entry of json(publicPath).entries)
    assert.doesNotMatch(entry.value, /^(?:pxa-(?:staging|production|webapi|spreadsheet)|PxaDatabase|PXA_AES256_QPDF_IMAGE|PXA_BUILD_\*)$|__name__/);
});

test('register links resolve to the correct visibility and an existing identifier', () => {
  const paths = execFileSync('git', ['ls-files', '-z'], { cwd: root, encoding: 'utf8' }).split('\0')
    .filter(path => /\.md$/i.test(path) && !path.startsWith('.vscode/'));
  for (const path of paths) {
    if (!lstatSync(resolve(root, path), { throwIfNoEntry: false })?.isFile()) continue;
    for (const match of read(path).matchAll(/\[compatibility entry `([^`]+)`\]\(([^)]+)\)/g)) {
      const target = match[2].startsWith('/') ? resolve(root, '.' + match[2]) : resolve(root, dirname(path), match[2]);
      assert.ok(existsSync(target), `${path}: ${match[2]}`);
      assert.ok(JSON.parse(readFileSync(target)).entries.some(entry => entry.id === match[1]), `${path}: ${match[1]}`);
    }
  }
});

test('actual cookie and vendor metadata values are retained rather than invented aliases', () => {
  const publicEntries = json(publicPath).entries;
  const cookies = read('PVAD.WebApi/Security/PvadCookieNames.cs');
  for (const value of ['__Host-PXA.Session', '__Host-PXA.Designer.Session', '__Host-PXA.Antiforgery']) {
    assert.ok(cookies.includes(value));
    assert.ok(publicEntries.some(entry => entry.value === value));
  }
  const vendorSource = read('src/Migrations/Report/PVAD.Migration.Report.Designer.Rpx/RpxToDesignConverter.cs');
  assert.ok(vendorSource.includes('"rpxAutoSize"'));
  assert.ok(publicEntries.some(entry => entry.value === 'rpxAutoSize'));
});

test('public documentation packaging never includes the operator register', () => {
  const docfx = json('docs/docfx.json');
  assert.doesNotMatch(JSON.stringify(docfx.build), /operator-docs/);
  assert.ok(docfx.build.resource.some(resource => resource.files.includes('reference/compatibility-identifiers.v1.json')));
  for (const site of ['Company', 'Account', 'Admin', 'Documentation'])
    assert.ok(read(`websites/PVAD.${site}/vite.config.js`).includes('publicCompatibilityRegisterPlugin()'));
  const embedded = read('PVAD.WebApi/PVAD.WebApi.csproj');
  assert.ok(embedded.includes('PVAD.LegalContent.compatibility-identifiers.v1.json'));
});
