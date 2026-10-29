import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const root = process.cwd();
const mappingPath = 'docs/pvad-documentation-path-migration.json';
const previous = existsSync(mappingPath) ? JSON.parse(readFileSync(mappingPath, 'utf8')).moves : [];
const paths = execFileSync('git', ['ls-files', '-co', '--exclude-standard', '-z'],
  { encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 }).split('\0').filter(Boolean);
const moves = [...new Set(paths)].filter(path => existsSync(path) &&
  /^(checklists|docs\/guides|operator-docs)\/(PXA\.|PxaPdf).*\.md$/.test(path))
  .map(from => ({ from, to: from.replace(/\/PXA\./, '/PVAD.').replace('/PxaPdf', '/PvadPdf') }));
for (const { from, to } of moves) {
  if (existsSync(to)) throw new Error(`Refusing to replace existing document: ${to}`);
  renameSync(from, to);
  if (to.startsWith('checklists/')) {
    const content = readFileSync(to, 'utf8');
    const newline = content.indexOf('\n');
    const note = '> Naming history: This checklist originated under PXA. Current technical names use PVAD; the platform brand is PVADE, with PVADP for PDF and PVADS for Spreadsheet. Historical implementation notes and compatibility identifiers below remain unchanged.\n';
    writeFileSync(to, content.slice(0, newline + 1) + '\n' + note + content.slice(newline + 1));
  }
}
const complete = [...previous, ...moves].filter((move, index, all) => all.findIndex(other => other.from === move.from) === index);
const substitutions = complete.map(({ from, to }) => [from.split('/').pop(), to.split('/').pop()]);
let references = 0;
for (const path of [...new Set([...paths, ...moves.map(move => move.to)])]) {
  if (!existsSync(path) || path === mappingPath || /^(\.agents|\.codex)\//.test(path) || /\.tsv$/.test(path) ||
      /(?:^|\/)(?:fixtures|test-assets|legal-documents|legal-evidence|bin|obj|dist|artifacts|node_modules)\//i.test(path) ||
      /(?:^|\/)(?:pvad|pxa)-releases\.json$/.test(path) ||
      /websites\/[^/]+\/public\/(?:downloads|legal)\//.test(path)) continue;
  const bytes = readFileSync(path);
  if (bytes.includes(0)) continue;
  let next = bytes.toString('utf8');
  for (const [from, to] of substitutions) next = next.replaceAll(from, to);
  if (next !== bytes.toString('utf8')) { writeFileSync(path, next); references++; }
}
writeFileSync(resolve(root, mappingPath),
  JSON.stringify({ legacyContentPreserved: true, moves: complete }, null, 2) + '\n');
console.log(JSON.stringify({ renamed: moves.length, referencesUpdated: references }));
