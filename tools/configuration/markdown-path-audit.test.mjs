import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import test from 'node:test';
import { extractLegacyPaths, renderMarkdownReport, scanMarkdownPaths } from './markdown-path-audit.mjs';

test('path extraction covers links, quoted paths, fenced commands, mixed casing and project filenames', () => {
  const source = [
    '[Writer](../PXA/Pdf/Serialization/PdfWriter.cs)',
    '`tests/PXA.Api.Tests/ExampleTests.cs`',
    '```bash',
    'dotnet build PXA.sln',
    'PXA_FIXTURE=test-assets/fixture.json node run.mjs',
    '```',
    'websites/PxA.Account/index.html',
    'https://docs.powerdoxautomation.com/guide',
    '`pxa://schema/workbook`',
    'PXA.Api.Tests.csproj',
  ].join('\n');
  assert.deepEqual(extractLegacyPaths(source), [
    { line: 1, value: '../PXA/Pdf/Serialization/PdfWriter.cs' },
    { line: 2, value: 'tests/PXA.Api.Tests/ExampleTests.cs' },
    { line: 4, value: 'PXA.sln' },
    { line: 5, value: 'PXA_FIXTURE=test-assets/fixture.json' },
    { line: 7, value: 'websites/PxA.Account/index.html' },
    { line: 8, value: 'https://docs.powerdoxautomation.com/guide' },
    { line: 9, value: 'pxa://schema/workbook' },
    { line: 10, value: 'PXA.Api.Tests.csproj' },
  ]);
  assert.deepEqual(extractLegacyPaths('PVAD.sln\nPXA is a previous name.\nPXACODE042\nstyle.rpxAutoSize'), []);
});

test('inventory scans tracked nested Markdown only and never follows IDE files or symlinks', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-markdown-path-audit-'));
  try {
    mkdirSync(join(root, 'nested'));
    mkdirSync(join(root, '.vscode'));
    writeFileSync(join(root, 'README.md'), '`PXA.sln`\n');
    writeFileSync(join(root, 'nested/guide.md'), '`tools/PXA.Mcp`\n');
    writeFileSync(join(root, 'nested/UPPER.MD'), '`PXA.Core.csproj`\n');
    writeFileSync(join(root, 'untracked.md'), 'synthetic-private/PXA-local.md\n');
    writeFileSync(join(root, '.vscode/notes.md'), 'synthetic-private/PXA-ide.md\n');
    symlinkSync('untracked.md', join(root, 'linked.md'));
    execFileSync('git', ['init', '--quiet'], { cwd: root });
    execFileSync('git', ['add', '--', 'README.md', 'nested/guide.md', 'nested/UPPER.MD', '.vscode/notes.md', 'linked.md'], { cwd: root });
    const report = scanMarkdownPaths(root);
    assert.equal(report.filesScanned, 3);
    assert.equal(report.matchingFiles, 3);
    assert.equal(report.occurrences, 3);
    assert.deepEqual(report.records.map(record => record.path), ['README.md', 'nested/UPPER.MD', 'nested/guide.md']);
    const markdown = renderMarkdownReport(report, root);
    assert.match(markdown, /not a complete Markdown grammar or a rename approval/);
    assert.doesNotMatch(markdown, /synthetic-private/);
    assert.match(markdown, /README\.md:1/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
