import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, extname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

export function inspectArtifact(path, bytes) {
  const extension = extname(path).toLowerCase();
  const kind = bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])) ? 'png'
    : bytes.subarray(0, 5).toString() === '%PDF-' ? 'pdf'
    : ['.dll', '.exe', '.pdb'].includes(extension) ? 'assembly-or-symbols' : 'binary';
  return { path, kind, bytes: bytes.length, sha256: sha256(bytes),
    ...(kind === 'png' && bytes.length >= 24 ? { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) } : {}),
    review: 'required', ...(path.includes('AdminDocumentation/') ? { visibility: 'protected-admin' } : {}) };
}

export function inspectOldNames(path, source) {
  const findings = [];
  for (const [index, line] of source.split('\n').entries()) {
    const tokens = [...new Set([...line.matchAll(/PXA[\w.-]*|Pxa[\w.]*|pxa[\w.-]*|PowerDox[\w.]*|Canvas[\w.]*|canvas[\w.-]*/g)].map(match => match[0]))];
    if (!tokens.length) continue;
    // Never copy source lines: they may contain customer data, credentials or URLs.
    findings.push({ path, line: index + 1, lineHash: sha256(line), tokens: tokens.map(token =>
      /^(?:PXA|Pxa|pxa|PowerDox)/.test(token) ? token.slice(0, 3) : token.startsWith('Canvas') ? 'Canvas' : 'canvas'),
    classification: 'review-required' });
  }
  return findings;
}

export function runAudit() {
  const files = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '-z'],
    { cwd: root, maxBuffer: 30 * 1024 * 1024 }).toString().split('\0').filter(Boolean))].sort();
  const baseline = 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a';
  const baselineFiles = new Map(execFileSync('git', ['ls-tree', '-r', '-z', baseline], { cwd: root, maxBuffer: 30 * 1024 * 1024 })
    .toString().split('\0').filter(Boolean).map(entry => {
      const [metadata, path] = entry.split('\t'); return [path, metadata.split(' ')[2]];
    }));
  const binaryArtifacts = [], oldNames = [];
  for (const path of files) {
    const absolute = resolve(root, path);
    if (!existsSync(absolute) || !statSync(absolute).isFile() || path.endsWith('.tsv')) continue;
    const bytes = readFileSync(absolute);
    const textSource = /\.(?:cs|js|mjs|cjs|ts|tsx|jsx|json|md|yml|yaml|xml|sh|txt|props|csproj|sln|slnx)$/.test(path);
    if ((!textSource && bytes.includes(0)) || ['.png', '.jpg', '.jpeg', '.pdf', '.zip', '.pvadp', '.pvads', '.pxap', '.pxas'].includes(extname(path))) {
      const candidates = [path, path.replaceAll('PVAD', 'PXA').replaceAll('pvad-designer', 'pxa-designer')];
      const baselinePath = candidates.find(candidate => baselineFiles.has(candidate));
      const blob = createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
      binaryArtifacts.push({ ...inspectArtifact(path, bytes), baselinePath: baselinePath ?? null,
        baselineState: baselinePath ? baselineFiles.get(baselinePath) === blob ? 'unchanged' : 'changed' : 'new' });
    } else oldNames.push(...inspectOldNames(path, bytes.toString('utf8')));
  }
  const generatedArtifacts = [];
  function walk(directory) {
    if (!existsSync(resolve(root, directory))) return;
    for (const entry of readdirSync(resolve(root, directory), { withFileTypes: true })) {
      const path = `${directory}/${entry.name}`;
      if (entry.isSymbolicLink() || ['node_modules', '.git', 'artifacts', 'TestResults'].includes(entry.name)) continue;
      if (entry.isDirectory()) walk(path);
      else if (/(?:^|\/)(?:bin|obj)\//.test(path) && /^(?:PXA|PVAD)\..*\.(?:dll|exe|pdb|deps\.json|runtimeconfig\.json)$/.test(entry.name))
        generatedArtifacts.push({ ...inspectArtifact(path, readFileSync(resolve(root, path))),
          review: /^tools\/job-cutover\/(?:bin|obj)\/legacy\//.test(path) ? 'retained-paired-legacy-harness' : 'required' });
    }
  }
  for (const directory of ['src', 'tests', 'tools', 'PVAD.WebApi']) walk(directory);
  const output = resolve(root, 'artifacts/rename/name-and-artifact-audit.json');
  mkdirSync(dirname(output), { recursive: true });
  const report = { schemaVersion: 1, baseline, approval: 'not-approved', oldNames, binaryArtifacts, generatedArtifacts };
  writeFileSync(output, `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify({ output: 'artifacts/rename/name-and-artifact-audit.json',
    oldNameLocations: oldNames.length, binaryArtifacts: binaryArtifacts.length, generatedArtifacts: generatedArtifacts.length,
    staleGeneratedNames: generatedArtifacts.filter(item => /\/PXA\./.test(item.path) && item.review !== 'retained-paired-legacy-harness').length }));
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) runAudit();
