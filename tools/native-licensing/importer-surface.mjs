import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync, execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, openSync, closeSync, existsSync, lstatSync } from 'node:fs';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { withNativeTestSession, nativeTestEnvironment, nativeTestAssemblyPath } from './test-session.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const inventoryFile = 'product-metadata/native-importer-public-surface.v1.json';
const assemblies = ['PVAD.Importer', 'PVAD.FileImporter', 'PVAD.FileImporter.ImageAnalysis', 'PVAD.FileImporter.ImageOcr'];
const categories = ['model', 'contract', 'utility', 'processing'];
const hash = value => createHash('sha256').update(value).digest('hex');
export const surfaceHash = type => hash(JSON.stringify([type.kind, type.baseType, type.interfaces, type.explicitBindings, type.members]));

export function validateInventory(inventory) {
  assert.deepEqual(Object.keys(inventory).sort(), ['assemblies', 'schemaVersion', 'scope']);
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'public-importer-assemblies-and-explicit-interfaces');
  assert.deepEqual(inventory.assemblies.map(entry => entry.assembly), assemblies);
  for (const assembly of inventory.assemblies) {
    assert.deepEqual(Object.keys(assembly).sort(), ['assembly', 'types']);
    const names = new Set();
    for (const type of assembly.types) {
      assert.deepEqual(Object.keys(type).sort(), ['category', 'evidence', 'freeMethods', 'protectedMembers', 'reason', 'surfaceHash', 'type']);
      assert.ok(type.type.startsWith(assembly.assembly + '.') && !names.has(type.type)); names.add(type.type);
      assert.ok(categories.includes(type.category));
      assert.match(type.surfaceHash, /^[a-f0-9]{64}$/);
      assert.ok(typeof type.reason === 'string' && type.reason.length >= 20);
      for (const members of [type.protectedMembers, type.freeMethods]) {
        assert.ok(Array.isArray(members) && members.every(member => /^(?:Method|Constructor):/.test(member)));
        assert.equal(new Set(members).size, members.length);
      }
      assert.ok(type.freeMethods.every(member => !type.protectedMembers.includes(member)));
      assert.equal(type.protectedMembers.length > 0, type.category === 'processing');
      assert.ok(Array.isArray(type.evidence));
      if (type.category === 'processing') assert.ok(type.evidence.length > 0);
      for (const path of type.evidence) {
        assert.ok(path.startsWith('tools/native-licensing/') && !path.split(/[\\/]/).includes('..') && existsSync(resolve(root, path)));
      }
    }
    assert.ok(names.size > 0);
  }
}

export function validateSurface(inventory, actual) {
  validateInventory(inventory);
  assert.equal(actual.schemaVersion, 1);
  assert.deepEqual(actual.assemblies.map(entry => entry.assembly), assemblies);
  let types = 0, members = 0, protectedMembers = 0, explicitBindings = 0;
  for (const [index, assembly] of actual.assemblies.entries()) {
    const reviewed = inventory.assemblies[index];
    assert.deepEqual(assembly.types.map(type => type.type), reviewed.types.map(type => type.type), assembly.assembly + ': unreviewed exported types');
    for (const [typeIndex, type] of assembly.types.entries()) {
      const expected = reviewed.types[typeIndex];
      assert.equal(surfaceHash(type), expected.surfaceHash, type.type + ': unreviewed public API or interface binding');
      assert.equal(type.isInterface, expected.category === 'contract', type.type + ': contract classification');
      const methods = assembly.methods.filter(method => method.type === type.type).map(method => method.member).sort();
      assert.deepEqual([...expected.protectedMembers.filter(member => member.startsWith('Method:')), ...expected.freeMethods].sort(), methods,
        type.type + ': unclassified processing/helper method');
      for (const member of expected.protectedMembers) assert.ok(type.members.includes(member), type.type + ': missing protected member');
      types++; members += type.members.length; protectedMembers += expected.protectedMembers.length; explicitBindings += type.explicitBindings.length;
    }
  }
  return { types, members, protectedMembers, explicitBindings };
}

export function validateCoverage(inventory, reports) {
  validateInventory(inventory);
  assert.equal(reports.length, 2);
  const actual = reports.flatMap(report => {
    assert.ok(Array.isArray(report.checks) && report.checks.length > 0 && new Set(report.checks).size === report.checks.length);
    assert.ok(Array.isArray(report.testedSurface) && report.testedSurface.length > 0);
    return report.testedSurface;
  });
  assert.equal(new Set(actual).size, actual.length, 'Duplicate native processing evidence.');
  const expected = inventory.assemblies.flatMap(assembly => assembly.types.flatMap(type =>
    type.protectedMembers.map(member => `${assembly.assembly}\n${type.type}\n${member}`)));
  assert.deepEqual([...actual].sort(), expected.sort(), 'A protected public API lacks native probe coverage or is incorrectly classified.');
  const phaseRows = reports.flatMap(report => {
    assert.ok(Array.isArray(report.phaseEvidence), 'Old symbol-only evidence cannot prove license phases.');
    const observed = new Set(report.checks);
    const referenced = { missing: new Set(), incorrect: new Set(), valid: new Set() };
    assert.deepEqual(report.phaseEvidence.map(row => row.member).sort(), [...report.testedSurface].sort());
    for (const row of report.phaseEvidence) for (const [phase, diagnostic] of [['missing', 'PVADLIC001'], ['incorrect', 'PVADLIC012'], ['valid', null]]) {
      const result = row[phase];
      if (phase === 'valid' && result === null) continue;
      assert.ok(result && observed.has(result.check), 'Referenced native phase did not execute.');
      assert.equal(result.diagnostic, diagnostic);
      if (result.mode === 'admissionPrerequisite') {
        assert.equal(phase, 'missing');
        const prerequisite = report.phaseEvidence.find(value => value.member === result.prerequisite);
        assert.ok(prerequisite && prerequisite.missing?.mode === 'invoked' && prerequisite.missing.check === result.check,
          'A prerequisite needs its own directly executed missing-license check.');
      } else {
        assert.equal(result.mode, 'invoked');
        assert.equal(result.prerequisite, null);
        assert.ok(!referenced[phase].has(result.check), 'One assertion cannot prove two directly invoked members.');
        referenced[phase].add(result.check);
      }
    }
    return report.phaseEvidence;
  });
  // Embedded OCR success is currently exercised on the existing macOS/x64 native profile.
  const pendingPositiveMembers = (reports[1].realNativeSucceeded ? [] : inventory.assemblies.slice(2).flatMap(assembly => assembly.types
    .filter(type => type.type === 'PVAD.FileImporter.ImageOcr.EmbeddedTesseractOcrEngine').flatMap(type =>
      type.protectedMembers.map(member => `${assembly.assembly}\n${type.type}\n${member}`)))).sort();
  const incomplete = phaseRows.filter(row => row.valid === null).map(row => row.member).sort();
  assert.deepEqual(incomplete, pendingPositiveMembers, 'Unreviewed missing or newly claimed positive phase evidence.');
  return { protectedMembers: expected.length, importerChecks: reports[0].checks.length, imageChecks: reports[1].checks.length,
    phaseVerifiedMembers: phaseRows.length - incomplete.length, pendingPositiveMembers: incomplete,
    allPhasesVerified: incomplete.length === 0 };
}

async function main(mode) {
  assert.ok(['capture', 'verify'].includes(mode), 'Usage: importer-surface.mjs [verify|capture]');
  await withNativeTestSession(async () => {
    nativeTestEnvironment();
    const session = JSON.parse(readFileSync(process.env.PVAD_NATIVE_TEST_SESSION));
    const output = session.output;
    console.log('Evidence: ' + relative(root, output));
    const inputs = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '--', 'src', 'tests', 'PVAD.WebApi', 'tools/native-licensing', 'product-metadata'],
      { cwd: root, encoding: 'utf8' }).trim().split('\n').filter(file => /\.(?:cs|csproj|mjs|json)$/.test(file) && lstatSync(resolve(root, file)).isFile()))].sort();
    const hashes = Object.fromEntries(inputs.map(file => [file, hash(readFileSync(resolve(root, file)))]));
    writeFileSync(resolve(output, 'surface-source-hashes.json'), JSON.stringify(hashes, null, 2));
    function run(args, name, json = false) {
      const fd = openSync(resolve(output, name + '.log'), 'w');
      let result;
      try { result = spawnSync('dotnet', args, { cwd: root, env: process.env, timeout: 600_000, maxBuffer: 16*1024*1024,
        stdio: json ? ['ignore', 'pipe', fd] : ['ignore', fd, fd], encoding: 'utf8' }); }
      finally { closeSync(fd); }
      assert.equal(result.status, 0, `${name}: exit ${result.status}, signal ${result.signal ?? 'none'}; inspect the isolated run log`);
      assert.ok(!result.signal && !result.error);
      if (json) return JSON.parse(result.stdout);
    }
    function build(name) {
      const project = `tools/native-licensing/${name}/${name}.csproj`;
      run(['build', project, '--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '--verbosity', 'minimal'], name);
      return nativeTestAssemblyPath(project);
    }
    const raw = run([build('ImporterSurfaceProbe')], 'surface-capture', true);
    writeFileSync(resolve(output, 'public-surface.json'), JSON.stringify(raw, null, 2));
    let report = { scope: 'local-compiled-public-api-capture-not-release-acceptance', types: raw.assemblies.reduce((n,a) => n+a.types.length, 0) };
    if (mode === 'verify') {
      const inventory = JSON.parse(readFileSync(resolve(root, inventoryFile)));
      report = { ...report, ...validateSurface(inventory, raw) };
      const importer = run([build('ImporterEngineProbe'), session.files.importer, session.files.pdf, session.files.expired], 'importer-native', true);
      writeFileSync(resolve(output, 'importer-admission.json'), JSON.stringify(importer, null, 2));
      const imageDll = build('ImageEngineProbe');
      const worker = nativeTestAssemblyPath('src/Importing/PVAD.FileImporter.ImageOcr.Worker/PVAD.FileImporter.ImageOcr.Worker.csproj');
      const image = run([imageDll, session.files.importer, session.files.pdf, session.files.expired, worker], 'image-native', true);
      writeFileSync(resolve(output, 'image-admission.json'), JSON.stringify(image, null, 2));
      report = { ...report, ...validateCoverage(inventory, [importer, image]) };
    }
    for (const [file, expected] of Object.entries(hashes)) assert.equal(hash(readFileSync(resolve(root, file))), expected, file);
    report.sourceInputsUnchanged = inputs.length;
    writeFileSync(resolve(output, 'surface-validation.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report));
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
  await main(process.argv[2] ?? 'verify');
