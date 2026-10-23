import { build } from 'esbuild';
import { cp, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const packageRoot = path.resolve(import.meta.dirname, '..');
const repositoryRoot = path.resolve(packageRoot, '../..');
const outputRoot = path.join(packageRoot, 'dist');
const resourceRoot = path.join(outputRoot, 'resources');
const schemaManifest = JSON.parse(await readFile(
  path.join(repositoryRoot, 'docs/schema/schema-aliases.json'), 'utf8'));

await mkdir(resourceRoot, { recursive: true });
await build({
  entryPoints: {
    'pvad-mcp': path.join(packageRoot, 'src', 'index.ts'),
    'pvad-mcp-server': path.join(packageRoot, 'src', 'remote.ts'),
  },
  outdir: outputRoot,
  bundle: true,
  platform: 'node',
  format: 'esm',
  target: 'node20',
  packages: 'external',
  banner: { js: '#!/usr/bin/env node' },
  sourcemap: true,
});

for (const relativePath of new Set([
  'VERSION',
  'llms-full.txt',
  'docs/csharp-cookbook.md',
  'docs/schema/pxa-workbook.schema.json',
  'docs/schema/schema-aliases.json',
  ...schemaManifest.schemas.map(entry => entry.path),
  'docs/schema/openapi.json',
])) {
  const destination = path.join(resourceRoot, relativePath);
  await mkdir(path.dirname(destination), { recursive: true });
  await cp(path.join(repositoryRoot, relativePath), destination);
}

for (const file of ['pvad-mcp.js', 'pvad-mcp-server.js']) {
  const output = path.join(outputRoot, file);
  const source = await readFile(output, 'utf8');
  await writeFile(output, source, { mode: 0o755 });
}
