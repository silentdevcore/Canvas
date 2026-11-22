import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { posix } from 'node:path';
import { test } from 'node:test';
import { createRequire } from 'node:module';
import { projects, importerProjects, executionProjects, migrationProjects, validateProjectClosure, validateConsumerAssets, comparableSemantics, main } from './joint-package-rehearsal.mjs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

test('the joint profile is exactly the current nine-assembly Generator project closure', () => {
  assert.equal(Object.keys(projects).length, 9);
  validateProjectClosure();
  const missing = { ...projects }; delete missing['PVAD.Core'];
  assert.throws(() => validateProjectClosure(missing), /dependency graph changed/);
  assert.throws(() => validateProjectClosure({ ...projects, extra: 'PVAD.WebApi/PVAD.WebApi.csproj' }), /dependency graph changed/);
});

test('independent restore rejects project references and incomplete or foreign first-party graphs', () => {
  const assets = { libraries: Object.fromEntries(Object.keys(projects).map(name => [name + '/1.1.0', { type: 'package' }])) };
  assert.equal(validateConsumerAssets(assets), 9);
  const project = structuredClone(assets); project.libraries['PVAD.Core/1.1.0'].type = 'project';
  assert.throws(() => validateConsumerAssets(project), /project references/);
  const incomplete = structuredClone(assets); delete incomplete.libraries['PVAD.Core/1.1.0'];
  assert.throws(() => validateConsumerAssets(incomplete));
  const extra = structuredClone(assets); extra.libraries['PVAD.Importer/1.1.0'] = { type: 'package' };
  assert.throws(() => validateConsumerAssets(extra));
});

test('joint packaging is an explicit opt-in and never falls back to unprotected files', () => {
  const targets = read('./protected-package.targets');
  assert.ok(targets.includes('PvadSelectJointProtectedAssembly'));
  assert.ok(targets.includes("!Exists('$(PvadJointAssembly)')"));
  assert.ok(targets.includes('BuiltProjectOutputGroupOutput Remove="@(BuiltProjectOutputGroupOutput)"'));
  assert.ok(targets.includes('$(PvadProtectedPackageDirectory)/$(TargetFileName)'));
  for (const property of ['IncludeSymbols', 'IncludeSource', 'EmbedAllSources', 'EnableSourceLink'])
    assert.ok(targets.includes(`<${property}>false</${property}>`));
  assert.ok(!targets.includes('PvadRejectNativeTestPublication'));
  assert.ok(targets.includes('PackageReference Update="Microsoft.Build.Tasks.Git" PrivateAssets="contentfiles;analyzers;build;buildMultitargeting;buildTransitive"'));
  assert.throws(() => main(['publish']), /no output, trust or publication overrides/);
});

test('the separate Linux rehearsal job uploads reports only and cannot publish packages', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs['generator-package-protection'];
  assert.equal(job['runs-on'], 'ubuntu-latest');
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/joint-package-rehearsal.mjs'));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@')).with.path;
  assert.deepEqual(upload.trim().split('\n').map(line => line.trim()), ['artifacts/native-joint-packages/*/report.json', 'artifacts/native-joint-packages/*/cleanup.json']);
  assert.ok(!JSON.stringify(job).includes('nuget push'));
});

test('semantic comparison normalizes only the two generated XLSX workspace IDs', () => {
  const sample = (id, value = 4) => ({ json: 'unchanged', wordXml: '<document/>', spreadsheet: JSON.stringify({ Id: id, Sheets: [{ Id: id, Cells: [{ Value: value }] }] }) });
  const first = sample('a'.repeat(32)), next = sample('b'.repeat(32));
  assert.deepEqual(comparableSemantics(first), comparableSemantics(next));
  assert.notDeepEqual(comparableSemantics(first), comparableSemantics(sample('a'.repeat(32), 99)));
  assert.notDeepEqual(comparableSemantics(first), comparableSemantics({ ...first, wordXml: '<different/>' }));
  assert.throws(() => comparableSemantics(sample('invalid')));
  assert.equal(JSON.parse(first.spreadsheet).Id, 'a'.repeat(32));
});

test('the consumer defaults to packages and verifies exact hashes, JSON and native licensing', () => {
  const project = read('./GeneratorPackageProbe/GeneratorPackageProbe.csproj');
  assert.ok(project.includes('PackageReference Include="PVAD.Generator"'));
  assert.ok(project.includes("Condition=\"'$(PvadOriginalBaseline)' == 'true'\""));
  const source = read('./GeneratorPackageProbe/Program.cs');
  for (const check of ['package-consumer-exact-hash:', 'package-no-private-file:', 'no-private-debug:', 'PVADLIC014', 'PVADLIC012', 'public-dto-json-roundtrip', 'xlsx-formula-calculation'])
    assert.ok(source.includes(check));
  const runner = read('./joint-package-rehearsal.mjs');
  for (const invariant of ['regularOutputs(profile), beforeBuild', "rmSync(work, { recursive: true, force: true })", 'packageSourceMapping', 'pdfPixelsEqual: true', 'productWideAcceptance: false', 'workerImporterMigrationAcceptance: false'])
    assert.ok(runner.includes(invariant));
  assert.ok(!runner.includes('AcceptNPOIOSMFLicense'));
});

test('the Importer profile protects the executable but never packages it as a library', () => {
  assert.equal(Object.keys(importerProjects).length, 14);
  validateProjectClosure(importerProjects, ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', 'PVAD.FileImporter.ImageOcr.Worker']);
  const packages = { ...importerProjects }; delete packages['PVAD.FileImporter.ImageOcr.Worker'];
  const assets = { libraries: Object.fromEntries(Object.keys(packages).map(name => [name + '/1.1.0', { type: 'package' }])) };
  assert.equal(validateConsumerAssets(assets, packages), 13);
  assert.throws(() => validateProjectClosure(packages, ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', 'PVAD.FileImporter.ImageOcr.Worker']));
  const probe = read('./GeneratorPackageProbe/ImporterPackageChecks.cs');
  for (const invariant of ['WorkerDenial', 'typed-worker-revocation-error:', 'regeneration-reflection-field', 'real-protected-worker-native-ocr-text', 'image-analysis-native-and-overlay']) assert.ok(probe.includes(invariant));
});

test('preservation annotations are restricted to the four regenerated PDF contracts and the element field', () => {
  assert.ok(read('../../src/Generation/PVAD.Pdf/PdfImageData.cs').includes('[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]'));
  for (const path of ['Layout/ImageElement.cs', 'Layout/RectangleElement.cs', 'Layout/PolygonElement.cs'])
    assert.ok(read('../../src/Generation/PVAD.Pdf/' + path).includes('[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]'));
  assert.ok(read('../../src/Generation/PVAD.Pdf/PdfPage.cs').includes('[System.Reflection.Obfuscation(Exclude = true)]\n    private readonly List<PdfPageElement> _elements'));
  assert.ok(read('./pvad-native-licensing.mjs').includes('<Var name="SkipGenerated" value="true" />'));
  assert.ok(read('./pvad-native-licensing.mjs').includes('decorator="System.Runtime.CompilerServices.CompilerGeneratedAttribute" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true"'));
  assert.ok(read('./pvad-native-licensing.mjs').includes('<SkipType name="*&lt;*" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />'));
});

test('the execution profile keeps both workers outside its fourteen library packages', () => {
  assert.equal(Object.keys(executionProjects).length, 16);
  validateProjectClosure(executionProjects, ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', 'PVAD.FileImporter.ImageOcr.Worker', 'PVAD.CodeWorker', 'PVAD.Infrastructure.Converters']);
  const libraries = Object.fromEntries(Object.entries(executionProjects).filter(([name]) => !['PVAD.FileImporter.ImageOcr.Worker', 'PVAD.CodeWorker'].includes(name)));
  const assets = { libraries: Object.fromEntries(Object.keys(libraries).map(name => [name + '/1.1.0', { type: 'package' }])) };
  assert.equal(validateConsumerAssets(assets, libraries), 14);
  const extra = structuredClone(assets); extra.libraries['PVAD.CodeWorker/1.1.0'] = { type: 'package' };
  assert.throws(() => validateConsumerAssets(extra, libraries));
  const probe = read('./GeneratorPackageProbe/ExecutionPackageChecks.cs');
  for (const value of ['worker-builder-exact-model-and-map', 'worker-base64-exact-model-and-map', 'worker-actual-pdf-signature',
    'PVADLIC001', 'PVADLIC009', 'PVADLIC012', 'PVADLIC014', 'PVADCODE008', 'PVADCODE030', 'PVADCODE104', 'PVADCODE130',
    'converter-complete-public-and-interface-call-set', 'converter-input-unchanged', 'Kill(entireProcessTree: true)',
    'converter-odt-creation-date:']) assert.ok(probe.includes(value), value);
  const runner = read('./joint-package-rehearsal.mjs');
  assert.ok(runner.includes("'--execution'"));
  assert.ok(runner.includes('codeWorkerPdfPixelsEqual: includeExecution'));
  assert.ok(runner.includes("pageHashes(join(work, 'protected-result'), 'code-worker.pdf'"));
});

test('the separate execution CI job cannot upload candidates or publish packages', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs['execution-package-protection'];
  assert.equal(job['runs-on'], 'ubuntu-latest');
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/joint-package-rehearsal.mjs --execution'));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@')).with.path;
  assert.deepEqual(upload.trim().split('\n').map(line => line.trim()), ['artifacts/native-joint-packages/*/report.json', 'artifacts/native-joint-packages/*/cleanup.json']);
  assert.ok(!JSON.stringify(job).includes('nuget push'));
  const probe = read('./GeneratorPackageProbe/ExecutionPackageChecks.cs');
  assert.ok(probe.includes('date.Value = "generated-creation-date"'));
  assert.ok(!probe.includes('DateTime.UtcNow'));
  const worker = read('../../src/Execution/PVAD.CodeWorker/Program.cs');
  assert.match(worker, /if \(!response\.Success\)[^]*?response\.CanonicalDesign = null;[^]*?response\.PdfBytes = null;[^]*?response\.SourceMap\.Clear\(\);[^]*?response\.Fidelity = PvadCodeFidelity\.Unsupported;/);
});

test('the migration profile closes all thirty-eight migration projects and fifty-two packages', () => {
  assert.equal(Object.keys(migrationProjects).length, 54);
  assert.equal(Object.keys(migrationProjects).filter(name => name.startsWith('PVAD.Migration.')).length, 38);
  const found = [];
  function visit(directory) {
    for (const item of readdirSync(new URL('../../' + directory, import.meta.url), { withFileTypes: true })) {
      const path = posix.join(directory, item.name);
      if (item.isDirectory() && !['bin', 'obj'].includes(item.name)) visit(path + '/');
      else if (item.isFile() && item.name.endsWith('.csproj')) found.push(path);
    }
  }
  visit('src/Migrations/');
  assert.deepEqual(found.sort(), Object.entries(migrationProjects).filter(([name]) => name.startsWith('PVAD.Migration.')).map(([, path]) => path).sort());
  validateProjectClosure(migrationProjects, ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', 'PVAD.FileImporter.ImageOcr.Worker', 'PVAD.CodeWorker', 'PVAD.Infrastructure.Converters',
    'PVAD.Migration.Pdf', 'PVAD.Migration.Spreadsheet', 'PVAD.Migration.Report', 'PVAD.Migration.Pdf.Viewer.Nutrient']);
  const profile = Object.fromEntries(Object.entries(migrationProjects).filter(([name]) => !['PVAD.CodeWorker', 'PVAD.FileImporter.ImageOcr.Worker'].includes(name)));
  assert.equal(validateConsumerAssets({ libraries: Object.fromEntries(Object.keys(profile).map(name => [name + '/1.1.0', { type: 'package' }])) }, profile), 52);
  for (const [name, libraries] of [['MigrationCodeEngineProbe', ['PVAD.Migration.Pdf', 'PVAD.Migration.Spreadsheet']], ['MigrationReportEngineProbe', ['PVAD.Migration.Report']], ['MigrationViewerEngineProbe', ['PVAD.Migration.Pdf.Viewer.Nutrient']]]) {
    const project = read('./' + name + '/' + name + '.csproj');
    assert.ok(project.includes("Condition=\"'$(PvadProtectedMigrationConsumer)' != 'true'\""));
    for (const library of libraries) assert.ok(project.includes(`PackageReference Include="${library}"`));
  }
  const runner = read('./joint-package-rehearsal.mjs');
  assert.ok(runner.includes('assert.deepEqual(protectedMigrations[kind], originalMigrations[kind]'));
  assert.ok(runner.includes('migrationProviderComparisonPassed: includeMigrations'));
  assert.ok(runner.includes('const dependencyAssets = [assets,'));
  assert.ok(runner.includes('Migration dependency version drift: '));
  assert.ok(runner.includes("['Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll'].includes(name)"));
  const source = read('./MigrationCodeEngineProbe/Program.cs');
  assert.ok(source.includes('CSharpSyntaxTree.ParseText(File.ReadAllText(file))'));
  assert.ok(source.includes('fixtureSourceChecksums, fixtureOutputChecksums'));
});

test('the Migration job exposes reports only and checks the existing full provider probes', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs['migration-package-protection'];
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/joint-package-rehearsal.mjs --migrations'));
  assert.deepEqual(job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@')).with.path.trim().split('\n').map(line => line.trim()),
    ['artifacts/native-joint-packages/*/report.json', 'artifacts/native-joint-packages/*/cleanup.json']);
  assert.ok(!JSON.stringify(job).includes('nuget push'));
});

test('package consumers use single-product grants rather than relying on the integrated fixture', () => {
  const helper = read('./GeneratorPackageProbe/ProductGrantChecks.cs');
  const products = JSON.parse(read('../../product-metadata/native-library-products.v1.json')).products;
  for (const product of products) assert.ok(helper.includes('"' + product.id + '"'), product.id);
  for (const value of ['PvadLicense.Require(product)', 'public-pdf-denied', 'public-word-denied', 'public-spreadsheet-denied',
    'snapshot-unchanged', 'PVADLIC012']) assert.ok(helper.includes(value));
  const source = read('./GeneratorPackageProbe/Program.cs');
  for (const product of ['pdf', 'word', 'spreadsheet']) assert.ok(source.includes(`ProductGrantChecks.Only("${product}"`));
  assert.ok(source.includes('single-word-retained-pdf-denied'));
  const importer = read('./GeneratorPackageProbe/ImporterPackageChecks.cs');
  assert.ok(importer.includes('PVAD_PACKAGE_IMPORTER_LICENSE_PATH'));
  assert.ok(importer.includes('ProductGrantChecks.Only("importer"'));
  assert.ok(importer.includes('importer-denied-callback-preserves-output'));
  assert.ok(importer.includes('importer-output-callback-cannot-inherit-pdf-rights'));
  const execution = read('./GeneratorPackageProbe/ExecutionPackageChecks.cs');
  assert.ok(execution.includes('ProductGrantChecks.Only("converter"'));
  assert.ok(execution.includes('pdfOnly, args[3], "builder"'));
  assert.ok(execution.includes('pdfOnly, args[3], "legacy"'));
  const runner = read('./joint-package-rehearsal.mjs');
  for (const property of ['PDF', 'WORD', 'SPREADSHEET', 'IMPORTER']) assert.ok(runner.includes(`PVAD_PACKAGE_${property}_LICENSE_PATH:`));
  assert.ok(runner.includes('Every included product needs its own single-product license.'));
  assert.ok(runner.includes('dependencyChecks(after), dependencyChecks(before)'));
});

test('data-contract checks run before activation and cannot silently skip protected processing or use uninitialized objects', () => {
  const main = read('./GeneratorPackageProbe/Program.cs');
  assert.ok(main.indexOf('FreeContractChecks.Run') < main.indexOf('PvadLicense.LoadFromFile'));
  const free = read('./GeneratorPackageProbe/FreeContractChecks.cs');
  for (const value of ['type.Namespace == "PVAD.Core.Contracts"', 'type.GetConstructor(Type.EmptyTypes)',
    'Activator.CreateInstance(type)', 'JsonSerializer.Deserialize(json, type)', 'Unlicensed();',
    'PdfSaveOptions', 'PdfDrawTextOptions', 'DesignTemplate', 'SpreadsheetDto', 'IRendererCapabilities',
    'FileImporterRegistry.Create', 'MigrationProviderCatalog.All', 'pdf-migration-still-protected',
    'sheet-migration-still-protected', 'report-migration-still-protected', 'PVADLIC001']) assert.ok(free.includes(value), value);
  assert.ok(!free.includes('GetUninitializedObject'));
  assert.ok(!free.includes('PvadLicense.Load'));
  const surface = read('./ApiSurfaceProbe/Program.cs');
  assert.equal(surface.match(/RequireUnlicensed\(\);/g).length, 2);
  assert.ok(surface.indexOf('RequireUnlicensed();') < surface.indexOf('assembly.GetExportedTypes()'));
  assert.ok(surface.lastIndexOf('RequireUnlicensed();') > surface.indexOf('assembly.GetExportedTypes()'));
  const runner = read('./joint-package-rehearsal.mjs');
  assert.ok(runner.includes('after.unlicensedCoreDtoTypes, before.unlicensedCoreDtoTypes'));
  assert.ok(runner.includes('freeChecks(after), freeChecks(before)'));
  assert.ok(runner.includes('unlicensedMetadataAssemblies: Object.keys(surfaces).length'));
});

test('shared Core and Domain contracts have no native licensing or product-project dependency', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { JSDOM } = require('jsdom');
  const products = JSON.parse(read('../../product-metadata/native-library-products.v1.json')).products;
  const productProjects = products.flatMap(product => product.projectDirectories);
  for (const name of ['Core', 'Domain']) {
    const project = read(`../../src/Core/PVAD.${name}/PVAD.${name}.csproj`);
    const document = new JSDOM(project, { contentType: 'text/xml' }).window.document;
    for (const reference of document.querySelectorAll('ProjectReference, PackageReference')) {
      const path = reference.getAttribute('Include').replaceAll('\\', '/');
      assert.ok(!path.includes('PVAD.Licensing'));
      assert.ok(!productProjects.some(directory => path.includes(directory.split('/').at(-1))), path);
    }
  }
});
