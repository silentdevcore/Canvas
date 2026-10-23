import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));

test('current and retained legacy private/generated files remain ignored without creating files', () => {
  const paths = [];
  for (const prefix of ['Canvas', 'PXA', 'PVAD']) {
    paths.push(`${prefix}.WebApi/App_Data/private-test.bin`);
    paths.push(`websites/${prefix}.Company/public/legal-snapshots/private-test.json`);
    for (const directory of ['dist', 'tmp', 'test-results', 'playwright-report', 'blob-report'])
      paths.push(`${prefix.toLowerCase()}-designer/${directory}/private-test.json`);
  }
  for (const designer of ['ui-designer', 'ui-designer-v2']) {
    for (const directory of ['dist', 'tmp', 'test-results', 'playwright-report', 'blob-report'])
      paths.push(`${designer}/${directory}/private-test.json`);
  }
  const ignored = execFileSync('git', ['check-ignore', '--no-index', '--stdin'], {
    cwd: root, input: `${paths.join('\n')}\n`, encoding: 'utf8',
  }).trim().split('\n');
  assert.deepEqual(ignored, paths);
});

test('ignore definitions use no obsolete branding while preserving private environment files', () => {
  const source = readFileSync(new URL('../../.gitignore', import.meta.url), 'utf8');
  assert.doesNotMatch(source, /pxa|power\s?dox|canvas/i);
  const paths = ['.env', '.env.local', '.env.production', '.env.production.local',
    'PVAD.WebApi/.env.private', 'deploy/rename/local-bindings.env',
    '.vscode/private-settings.json', '.vscode/workspaceStorage/state.json',
    'tests/PVAD.Api.Tests/bin/Debug/PVAD.Api.Tests.dll',
    'tests/PVAD.Api.Tests/obj/project.assets.json'];
  const ignored = execFileSync('git', ['check-ignore', '--no-index', '--stdin'], {
    cwd: root, input: `${paths.join('\n')}\n`, encoding: 'utf8',
  }).trim().split('\n');
  assert.deepEqual(ignored, paths);
});

test('source, templates, evidence and explicitly shared VS Code configuration stay visible', () => {
  const paths = ['.vscode/settings.json', '.vscode/tasks.json', '.vscode/launch.json', '.vscode/extensions.json',
    '.env.example', '.env.production.example', 'PVAD.WebApi/.env.example',
    'deploy/rename/legacy-bindings.env.example', 'docs/PVAD.Guide.md',
    'PVAD.WebApi/Program.cs', 'pvad-designer/src/main.tsx',
    'websites/PVAD.Company/public/legal-snapshots/README.md',
    'websites/PVAD.Documentation/public/downloads/pdf-trust/expected.json',
    'product-metadata/release-fragments/pvad-ci-artifact-identities.json'];
  const result = spawnSync('git', ['check-ignore', '--no-index', '--stdin'], {
    cwd: root, input: `${paths.join('\n')}\n`, encoding: 'utf8',
  });
  assert.equal(result.status, 1, result.stderr || result.stdout);
  assert.equal(result.stdout, '');
});
