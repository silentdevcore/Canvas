import { execFileSync } from 'node:child_process';
import { lstatSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export function extractLegacyPaths(markdown) {
  const records = [];
  for (const [index, line] of markdown.split('\n').entries()) {
    for (const match of line.matchAll(/[^\s`"'<>()[\],;|]+/g)) {
      const value = match[0].replace(/^[*]+/, '').replace(/[.:]$/, '');
      if (!/pxa|powerdox/i.test(value)) continue;
      if (!/[\\/]/.test(value) && !/\.(?:sln|csproj|md|tsx?|json|dll|zip|pxap|pxas)$/i.test(value)) continue;
      records.push({ line: index + 1, value });
    }
  }
  return records;
}

export function scanMarkdownPaths(root) {
  const paths = [...new Set(execFileSync('git', ['ls-files', '-z'],
    { cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 }).split('\0'))]
    .filter(path => /\.md$/i.test(path) && !/^(?:\.vscode|\.git)(?:\/|$)/.test(path)).sort();
  const records = [];
  let filesScanned = 0;
  for (const path of paths) {
    const absolute = resolve(root, path);
    // Do not follow tracked symlinks into private or unrelated local files.
    const stat = lstatSync(absolute, { throwIfNoEntry: false });
    if (!stat?.isFile()) continue;
    filesScanned++;
    for (const match of extractLegacyPaths(readFileSync(absolute, 'utf8')))
      records.push({ path, ...match });
  }
  return {
    schemaVersion: 1,
    scope: 'Tracked regular Markdown files only. Private IDE paths, untracked files, symlinks, ignored output and Git history are excluded. Lexical legacy-name path, URL, URI and project-file search across prose, links and code; not a complete Markdown grammar or a rename approval.',
    filesScanned,
    matchingFiles: new Set(records.map(record => record.path)).size,
    occurrences: records.length,
    records,
  };
}

export function renderMarkdownReport(report, root) {
  const cell = value => String(value).replaceAll('|', '&#124;').replaceAll('`', '&#96;');
  return [
    '# Markdown Path Audit', '',
    `Scanned ${report.filesScanned} tracked Markdown files; ${report.occurrences} legacy path-like occurrences in ${report.matchingFiles} files.`, '',
    report.scope, '',
    'Matches include original commands, historical paths and active compatibility identities. They must not be replaced blindly. This report does not certify external URLs, runtime migrations or legal publication.', '',
    '| Markdown source | Line | Path or identifier |', '| --- | ---: | --- |',
    ...report.records.map(record => `| [${cell(record.path)}](<${resolve(root, record.path)}:${record.line}>) | ${record.line} | \`${cell(record.value)}\` |`), '',
  ].join('\n');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const report = scanMarkdownPaths(root);
  const directory = resolve(root, 'artifacts/rename');
  mkdirSync(directory, { recursive: true });
  const output = resolve(directory, 'markdown-path-audit-current');
  writeFileSync(output + '.json', JSON.stringify(report, null, 2) + '\n');
  writeFileSync(output + '.md', renderMarkdownReport(report, root));
  console.log(JSON.stringify({ filesScanned: report.filesScanned, matchingFiles: report.matchingFiles,
    occurrences: report.occurrences, reports: [output + '.md', output + '.json'] }));
}
