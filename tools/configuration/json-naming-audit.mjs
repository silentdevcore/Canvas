import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { isAbsolute, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const ts = createRequire(new URL('../../pvad-designer/package.json', import.meta.url))('typescript');
export const auditPath = 'checklists/PVAD.JSON-Naming-Audit.json';
export const hash = value => createHash('sha256').update(value).digest('hex');
const legacy = () => /pxa|power[\s_-]*dox(?:[\s_-]*automation)?/gi;
const privatePath = path => /(?:^|\/)(?:\.vscode|\.git|\.aws|\.codex)(?:\/|$)/i.test(path);
const safePath = path => typeof path === 'string' && !isAbsolute(path) && !path.split(/[\\/]/).includes('..') && !privatePath(path);
const escape = value => value.replaceAll('~', '~0').replaceAll('/', '~1');

export function extractJsonNames(source, path = 'input.json') {
  const findings = [];
  function record(value, pointer, kind) {
    const matches = [...value.matchAll(legacy())].map(match => ({ offset: match.index, token: match[0] }));
    if (matches.length) findings.push({ pointer, kind, valueSha256: hash(value), matches });
  }
  const syntax = ts.parseJsonText(path, source);
  if (syntax.parseDiagnostics.length || syntax.statements.length !== 1) throw new Error(`Invalid JSON syntax: ${path}`);
  function visit(node, pointer) {
    if (ts.isObjectLiteralExpression(node)) {
      const keys = new Set();
      for (const property of node.properties) {
        if (!ts.isPropertyAssignment(property) ||
          !(ts.isStringLiteral(property.name) || ts.isIdentifier(property.name) || ts.isNumericLiteral(property.name)))
          throw new Error(`Unsupported JSON property: ${path}`);
        const key = property.name.text;
        if (keys.has(key)) throw new Error(`Duplicate JSON property: ${path}${pointer}`);
        keys.add(key);
        const child = pointer + '/' + escape(key);
        record(key, child, 'key');
        visit(property.initializer, child);
      }
    } else if (ts.isArrayLiteralExpression(node)) {
      node.elements.forEach((item, index) => visit(item, pointer + '/' + index));
    } else if (ts.isStringLiteral(node)) record(node.text, pointer, 'value');
    else if (!(ts.isNumericLiteral(node) || [ts.SyntaxKind.TrueKeyword, ts.SyntaxKind.FalseKeyword, ts.SyntaxKind.NullKeyword].includes(node.kind)
      || (ts.isPrefixUnaryExpression(node) && ts.isNumericLiteral(node.operand))))
      throw new Error(`Unsupported JSON value: ${path}${pointer}`);
  }
  for (const statement of syntax.statements) {
    if (!ts.isExpressionStatement(statement)) throw new Error(`Invalid JSON root: ${path}`);
    visit(statement.expression, '');
  }
  // Comments are not represented by JSON property nodes, but still need naming checks.
  const scanner = ts.createScanner(ts.ScriptTarget.Latest, false, ts.LanguageVariant.Standard, source);
  for (let token = scanner.scan(); token !== ts.SyntaxKind.EndOfFileToken; token = scanner.scan())
    if (token === ts.SyntaxKind.SingleLineCommentTrivia || token === ts.SyntaxKind.MultiLineCommentTrivia)
      record(scanner.getTokenText(), '/$comment/' + scanner.getTokenPos(), 'comment');
  record(path, '', 'filename');
  return findings;
}

export function trackedJsonPaths(repositoryRoot) {
  return [...new Set(execFileSync('git', ['ls-files', '-z'],
    { cwd: repositoryRoot, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 }).split('\0'))]
    .filter(path => /\.json(?:c|5)?$/i.test(path) && !privatePath(path)).sort();
}

export function scanJsonNames(repositoryRoot, paths = trackedJsonPaths(repositoryRoot)) {
  const files = [], errors = [];
  let filesScanned = 0;
  for (const path of paths) {
    if (!safePath(path)) continue;
    const absolute = resolve(repositoryRoot, path);
    if (!lstatSync(absolute, { throwIfNoEntry: false })?.isFile()) continue;
    filesScanned++;
    const bytes = readFileSync(absolute);
    try {
      const records = extractJsonNames(bytes.toString('utf8'), path);
      if (records.length) files.push({ path, sha256: hash(bytes), records });
    } catch (error) { errors.push({ path, message: error.message }); }
  }
  return { schemaVersion: 1, filesScanned, matchingFiles: files.length, files, errors };
}

const key = finding => `${finding.kind}:${finding.expectedPointer ?? finding.pointer}`;
export function applyReviewedJsonTransitions(audit, transitions, readSource) {
  if (!Array.isArray(transitions)) throw new Error('Invalid JSON naming transitions.');
  const current = structuredClone(audit), seen = new Set();
  for (const transition of transitions) {
    const identity = `${transition.path}:${transition.pointer}`;
    if (!safePath(transition.path) || !transition.path.endsWith('.json') || seen.has(identity)
      || typeof transition.pointer !== 'string' || !transition.pointer.startsWith('/')
      || typeof transition.currentValue !== 'string' || legacy().test(transition.currentValue)
      || typeof transition.reason !== 'string' || transition.reason.length < 20)
      throw new Error('Invalid or duplicate JSON naming transition.');
    seen.add(identity);
    const file = current.files.find(file => file.path === transition.path);
    if (!file || file.preserveBytes || current.protectedFiles?.some(file => file.path === transition.path))
      throw new Error('Cannot transition protected or unknown JSON evidence.');
    const index = file.records.findIndex(record => record.pointer === transition.pointer && record.kind === 'value');
    const record = file.records[index];
    if (index < 0 || record.classification !== 'compatibility-contract' || record.action !== 'preserve'
      || record.valueSha256 !== transition.previousValueSha256)
      throw new Error('JSON naming transition does not match the original audited value.');
    let value = JSON.parse(readSource(transition.path));
    for (const part of transition.pointer.slice(1).split('/').map(part => part.replaceAll('~1', '/').replaceAll('~0', '~'))) {
      if (!value || !Object.hasOwn(value, part)) throw new Error('Missing transitioned JSON value.');
      value = value[part];
    }
    if (value !== transition.currentValue) throw new Error('Changed transitioned JSON value.');
    file.records.splice(index, 1);
  }
  return current;
}

export function validateJsonNaming(report, audit) {
  if (audit.schemaVersion !== 1 || !/^[a-f0-9]{40}$/.test(audit.baseline) || !Array.isArray(audit.files))
    throw new Error('Invalid JSON naming audit.');
  const failures = report.errors.map(error => ({ path: error.path, reason: 'invalid-json' }));
  const expected = new Map();
  for (const file of audit.files) {
    if (!safePath(file.path) || file.path === auditPath || expected.has(file.path) || !/^[a-f0-9]{64}$/.test(file.sha256))
      throw new Error('Invalid or duplicate JSON audit path.');
    const records = new Map();
    for (const finding of file.records) {
      if (records.has(key(finding)) || !/^[a-f0-9]{64}$/.test(finding.valueSha256)
        || !['rename', 'compatibility-contract', 'historical-evidence', 'incidental-integrity'].includes(finding.classification)
        || !['rename', 'preserve'].includes(finding.action)
        || (finding.classification === 'rename') !== (finding.action === 'rename'))
        throw new Error('Invalid JSON finding disposition.');
      records.set(key(finding), finding);
    }
    expected.set(file.path, { file, records });
  }
  for (const current of report.files) {
    // This sole evidence container is hash-pinned separately, not approved by directory.
    if (current.path === auditPath) continue;
    const entry = expected.get(current.path);
    if (!entry) {
      failures.push({ path: current.path, reason: 'unclassified-file' });
      continue;
    }
    if (entry.file.preserveBytes && current.sha256 !== entry.file.sha256)
      failures.push({ path: current.path, reason: 'changed-protected-evidence' });
    for (const finding of current.records) {
      const previous = entry.records.get(key(finding));
      if (!previous || previous.action !== 'preserve' || previous.valueSha256 !== finding.valueSha256
        || JSON.stringify(previous.matches) !== JSON.stringify(finding.matches))
        failures.push({ path: current.path, pointer: finding.pointer, kind: finding.kind, reason: 'unreviewed-finding' });
      else entry.records.delete(key(finding));
    }
  }
  for (const { file, records } of expected.values())
    for (const finding of records.values())
      if (finding.action === 'preserve') failures.push({ path: file.path, pointer: finding.pointer, reason: 'stale-retained-finding' });
  return failures;
}

export function validateProtectedFiles(repositoryRoot, audit) {
  const seen = new Set(), failures = [];
  for (const file of audit.protectedFiles ?? []) {
    if (!safePath(file.path) || file.path === auditPath || seen.has(file.path) || !/^[a-f0-9]{64}$/.test(file.sha256))
      throw new Error('Invalid protected evidence path.');
    seen.add(file.path);
    const absolute = resolve(repositoryRoot, file.path);
    if (!lstatSync(absolute, { throwIfNoEntry: false })?.isFile() || hash(readFileSync(absolute)) !== file.sha256)
      failures.push({ path: file.path, reason: 'changed-protected-evidence' });
  }
  return failures;
}

export function validateAuditPin(repositoryRoot, bytes) {
  const ledger = JSON.parse(readFileSync(resolve(repositoryRoot, 'docs/pvad-rename-reviewed-exceptions.json')));
  const entry = ledger.files.find(file => file.path === auditPath);
  if (entry?.sha256 !== hash(bytes) || entry.reviewStatus !== 'reviewed' || !entry.reasons?.includes('retained-evidence'))
    throw new Error('The JSON naming audit requires an exact reviewed evidence hash.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  if (process.argv.slice(2).some(flag => flag !== '--check')) throw new Error('Only --check is supported.');
  const bytes = readFileSync(resolve(root, auditPath));
  validateAuditPin(root, bytes);
  const report = scanJsonNames(root);
  const originalAudit = JSON.parse(bytes);
  const review = JSON.parse(readFileSync(resolve(root, 'docs/pvad-rename-reviewed-exceptions.json')));
  const audit = applyReviewedJsonTransitions(originalAudit, review.jsonTransitions ?? [], path => {
    const absolute = resolve(root, path);
    if (!lstatSync(absolute, { throwIfNoEntry: false })?.isFile()) throw new Error('Missing regular transition source.');
    return readFileSync(absolute, 'utf8');
  });
  const failures = [...validateJsonNaming(report, audit), ...validateProtectedFiles(root, audit)];
  const directory = resolve(root, 'artifacts/rename');
  mkdirSync(directory, { recursive: true });
  writeFileSync(resolve(directory, 'json-name-audit-current.json'), JSON.stringify({ ...report, failures }, null, 2) + '\n');
  console.log(JSON.stringify({ filesScanned: report.filesScanned, matchingFiles: report.matchingFiles,
    unclassified: failures.length, failures }));
  if (failures.length) process.exitCode = 1;
}
