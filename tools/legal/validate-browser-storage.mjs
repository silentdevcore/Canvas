import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, extname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const inventoryPath = join(root, 'product-metadata/browser-storage.json');
const inventory = JSON.parse(readFileSync(inventoryPath, 'utf8'));
const errors = [];
const productionRoots = ['pxa-designer/src', 'websites'];
const sourceExtensions = new Set(['.js', '.jsx', '.ts', '.tsx']);

function sourceFiles(path) {
  if (!existsSync(path)) return [];
  if (!statSync(path).isDirectory()) return [path];
  return readdirSync(path, { withFileTypes: true }).flatMap(entry => {
    if (entry.name === 'node_modules' || entry.name === 'dist' || entry.name === 'build' || entry.name === 'tests' || entry.name === '__tests__')
      return [];
    return sourceFiles(join(path, entry.name));
  });
}

const ids = new Set();
const registeredKeys = new Set();
const registeredSources = new Set();
for (const entry of inventory.entries ?? []) {
  if (ids.has(entry.id)) errors.push(`Duplicate inventory id: ${entry.id}`);
  ids.add(entry.id);
  if (entry.optional && !inventory.optionalStorageEnabled)
    errors.push(`Optional storage is disabled but ${entry.id} is marked optional.`);
  if (['analytics', 'marketing'].includes(entry.category) && !entry.optional)
    errors.push(`${entry.id} must be marked optional.`);
  for (const key of entry.keys ?? []) {
    if (/canvas/i.test(key)) errors.push(`Legacy Canvas storage key is forbidden: ${key}`);
    registeredKeys.add(key);
  }
  for (const source of entry.sourceFiles ?? []) {
    registeredSources.add(source);
    if (!existsSync(join(root, source))) errors.push(`Inventory source does not exist: ${source}`);
  }
}

const storageCall = /\b(?:localStorage|sessionStorage)\s*\.\s*(?:getItem|setItem|removeItem|clear)\s*\(/;
const cookieAccess = /\bdocument\s*\.\s*cookie\b/;
const literalCall = /\b(?:localStorage|sessionStorage)\s*\.\s*(?:getItem|setItem|removeItem)\s*\(\s*(['"])([^'"]+)\1/g;

for (const absolute of productionRoots.flatMap(path => sourceFiles(join(root, path)))) {
  if (!sourceExtensions.has(extname(absolute))) continue;
  const source = readFileSync(absolute, 'utf8');
  if (!storageCall.test(source) && !cookieAccess.test(source)) continue;
  const repoPath = relative(root, absolute);
  if (!registeredSources.has(repoPath)) errors.push(`Unregistered browser-storage source: ${repoPath}`);
  for (const match of source.matchAll(literalCall)) {
    if (!registeredKeys.has(match[2])) errors.push(`Unregistered browser-storage key ${match[2]} in ${repoPath}`);
  }
}

for (const entry of inventory.entries ?? []) {
  if (entry.serverManaged) continue;
  const combined = entry.sourceFiles.map(path => readFileSync(join(root, path), 'utf8')).join('\n');
  for (const key of entry.keys) {
    if (!combined.includes(key)) errors.push(`Inventory key ${key} is not present in its registered sources.`);
  }
}

if (errors.length) {
  console.error(errors.map(error => `- ${error}`).join('\n'));
  process.exit(1);
}

console.log(`Validated ${inventory.entries.length} browser-storage entries; optional storage is disabled.`);
