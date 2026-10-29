import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../..', import.meta.url));
const baseline = JSON.parse(readFileSync(resolve(root, 'product-metadata/legacy-migration-diagnostics.json'), 'utf8'));
function sourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    if (['bin', 'obj', 'node_modules', '.git'].includes(entry.name)) return [];
    const path = resolve(directory, entry.name);
    return entry.isDirectory() ? sourceFiles(path) : entry.name.endsWith('.cs') ? [path] : [];
  });
}

test('historical migration diagnostics remain source contracts, not product branding', () => {
  assert.equal(baseline.schemaVersion, 1);
  assert.equal(baseline.baselineCommit, 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a');
  assert.equal(baseline.identifiers.length, 432);
  assert.equal(new Set(baseline.identifiers).size, baseline.identifiers.length);
  const current = new Set(['src', 'PVAD.WebApi'].flatMap(directory =>
    sourceFiles(resolve(root, directory))).flatMap(path => readFileSync(path, 'utf8').match(/\bCANMIG[A-Z]+[0-9]{3}\b/g) ?? []));
  const missing = baseline.identifiers.filter(identifier => !current.has(identifier));
  assert.deepEqual(missing, [], `Historical diagnostic IDs disappeared: ${missing.join(', ')}`);
});

test('reviewed API, worker, license and Designer diagnostic families retain explicit aliases', () => {
  const catalog = readFileSync(resolve(root, 'src/Core/PVAD.Core/Contracts/PvadDiagnosticAliases.cs'), 'utf8');
  const pattern = /\b(?:PXAAPI[0-9]{3}|PXACHART[0-9]{3}|PXAPDF(?:REDACTVIEWER|PIPELINE|REDACTAPI|REDACT|A|ACONVERT|UA|PARSE|XREF)[0-9]{3}|PXALAYOUT[0-9]{3}|PXAPDFTRUSTAPI[0-9]{3}|PXAPDFTRUST[0-9]{3}|PXAEINV(?:API|ATTACH)?[0-9]{3}|PXAPDFACONVERTAPI[0-9]{3}|PXACODE[0-9]{3}|PXAMIG[0-9]{3}|PXADESIGNER[0-9]{3}|PXAPROJECT[A-Z_0-9]+|PXA_(?:AUTHENTICATION|AUDIT_EXPORT|USAGE|LICENSE|DESIGNER|ENTITLEMENT|ORGANIZATION|SUBSCRIPTION|TRIAL|GRACE)_[A-Z_]+)\b/g;
  const aliases = [...catalog.matchAll(pattern)].map(match => match[0]);
  assert.equal(new Set(aliases).size, aliases.length, 'Duplicate diagnostic IDs in alias catalog');
  const expected = new Set([
    ...sourceFiles(resolve(root, 'PVAD.WebApi')),
    ...sourceFiles(resolve(root, 'src/Execution/PVAD.CodeWorker')),
    ...sourceFiles(resolve(root, 'src/Generation')),
    ...sourceFiles(resolve(root, 'src/Infrastructure/PVAD.Infrastructure.Pdf/Charts')),
    ...sourceFiles(resolve(root, 'src/Importing')),
    ...sourceFiles(resolve(root, 'src/Core/PVAD.Core/ProjectFormats')),
    ...sourceFiles(resolve(root, 'src/Core/PVAD.Core/Primitives')),
    resolve(root, 'src/Core/PVAD.Core/Contracts/CodeWorkspaceDto.cs'),
  ].flatMap(path => readFileSync(path, 'utf8').match(pattern) ?? []));
  assert.deepEqual([...expected].filter(code => !aliases.includes(code)), [],
    'New diagnostics in reviewed families need an explicit compatibility decision');
});

test('all current numeric and symbolic branded production identifiers are classified', () => {
  const catalog = readFileSync(resolve(root, 'src/Core/PVAD.Core/Contracts/PvadDiagnosticAliases.cs'), 'utf8');
  const configurationKeys = new Set([
    'PXA_PDF_OWNER_PASSWORD', 'PXA_PDF_USER_PASSWORD',
    'PXA_METRIC_RETENTION', 'PXA_LOG_RETENTION', 'PXA_TRACE_RETENTION', 'PXA_ALERT_RETENTION',
  ]);
  const retainedCompilerContracts = new Set(['PXA0001']);
  const current = new Set(['src', 'PVAD.WebApi'].flatMap(directory => sourceFiles(resolve(root, directory)))
    .flatMap(path => readFileSync(path, 'utf8').match(/\b(?:PXA[A-Z]+[0-9]{3}|PXA[0-9]{4}|PXA_[A-Z_]+)\b/g) ?? []));
  assert.deepEqual([...current].filter(code => !configurationKeys.has(code)
    && !retainedCompilerContracts.has(code) && !catalog.includes(`"${code}"`)), [],
    'Production identifiers require reviewed aliases or explicit non-diagnostic classification');
  assert.match(readFileSync(resolve(root, 'src/Generation/PVAD.Pdf/PdfDocument.cs'), 'utf8'),
    /DiagnosticId\s*=\s*"PXA0001"/, 'Retained compiler warning ID protects existing pragma/NoWarn contracts');
});
