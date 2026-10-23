import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { canonicalPath } from './rename-file-pairs.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const baseline = 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a';
const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 30 * 1024 * 1024 });
const currentFiles = [...new Set(git('ls-files', '-co', '--exclude-standard', '-z')
  .toString().split('\0').filter(file => file && existsSync(resolve(root, file))))];

test('baseline PDF, certificate and signed XML fixtures retain exact bytes after path moves', () => {
  const paths = git('ls-tree', '-r', '--name-only', baseline).toString().trim().split('\n')
    .filter(path => /\.(?:pdf|pfx|p12|pem|key|cer|crt)$/.test(path)
      || /\/pdf-trust\/pades\/.*\.xml$/.test(path));
  assert.ok(paths.length > 30);
  for (const path of paths) {
    const candidates = [path, canonicalPath(path)];
    if (/^websites\/PXA\.Documentation\/public\/downloads\/pdf-trust\/pades\//.test(path))
      candidates.push(path.replace('websites/PXA.Documentation/', 'websites/PVAD.Documentation/'));
    const target = candidates.find(candidate => existsSync(resolve(root, candidate)));
    assert.ok(target, path);
    assert.deepEqual(readFileSync(resolve(root, target)), git('show', `${baseline}:${path}`), path);
  }
});

test('staged paths exclude private runtime directories and actual credential files', () => {
  const paths = git('diff', '--cached', '--name-only', '-z').toString().split('\0').filter(Boolean);
  for (const path of paths) {
    assert.doesNotMatch(path, /(?:^|\/)(?:App_Data|\.aws|\.codex|secrets)\//, path);
    assert.doesNotMatch(path, /(?:^|\/)\.env(?:$|\.(?!example$|sample$|template$))/, path);
    assert.doesNotMatch(path, /\.(?:pem|pfx|p12|key)$/, path);
  }
});

test('rename preserves baseline published Legal candidate bytes without rewriting historical names', () => {
  const files = git('ls-tree', '-r', '--name-only', baseline, '--', 'product-metadata/legal-documents')
    .toString().trim().split('\n').filter(Boolean);
  assert.ok(files.length > 0);
  for (const file of files) {
    assert.deepEqual(readFileSync(resolve(root, file)), git('show', `${baseline}:${file}`), file);
  }
});

test('third-party .NET package identities and vendor namespace imports are retained', () => {
  const oldProjects = git('grep', '-h', '<PackageReference', baseline, '--', '*.csproj').toString();
  const newProjects = currentFiles.filter(file => file.endsWith('.csproj'))
    .map(file => readFileSync(resolve(root, file), 'utf8')).join('\n');
  const packages = text => new Set([...text.matchAll(/<PackageReference\s+Include="([^"]+)"/g)].map(match => match[1]));
  const currentPackages = packages(newProjects);
  for (const id of packages(oldProjects)) assert.ok(currentPackages.has(id), `Missing package identity: ${id}`);

  const oldSource = git('grep', '-h', '^using ', baseline, '--', 'src/**/*.cs').toString();
  const newSource = currentFiles.filter(file => file.startsWith('src/') && file.endsWith('.cs'))
    .map(file => readFileSync(resolve(root, file), 'utf8')).join('\n');
  const vendors = text => [...text.matchAll(/^using\s+((?:Aspose|DevExpress|Syncfusion|Nutrient|iText|Telerik|GemBox|OfficeOpenXml|NPOI|SkiaSharp|DocumentFormat)\.[\w.]+);/gm)]
    .map(match => match[1]);
  const expected = new Set(vendors(oldSource));
  assert.ok(expected.size > 0);
  const actual = new Set(vendors(newSource));
  for (const name of expected) assert.ok(actual.has(name), `Missing vendor import: ${name}`);
  assert.match(newSource, /class\s+PdfDocument\b/);
  assert.match(newSource, /class\s+SpreadsheetDto\b/);
});
