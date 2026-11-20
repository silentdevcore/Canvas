import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { copyFileSync, cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { protect, checkRepository } from './pvad-native-licensing.mjs';
import { createTestLicenses } from './with-test-license.mjs';
import { rehearseSupportRetention } from './support-retention-rehearsal.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const here = join(root, 'tools/native-licensing');
const require = createRequire(join(root, 'pvad-designer/package.json'));
const { JSDOM } = require('jsdom');
export const projects = Object.freeze({
  'PVAD.Licensing': 'src/Licensing/PVAD.Licensing/PVAD.Licensing.csproj',
  'PVAD.Core': 'src/Core/PVAD.Core/PVAD.Core.csproj',
  'PVAD.Domain': 'src/Core/PVAD.Domain/PVAD.Domain.csproj',
  'PVAD.Application': 'src/Core/PVAD.Application/PVAD.Application.csproj',
  'PVAD.Pdf': 'src/Generation/PVAD.Pdf/PVAD.Pdf.csproj',
  'PVAD.Infrastructure.Pdf': 'src/Infrastructure/PVAD.Infrastructure.Pdf/PVAD.Infrastructure.Pdf.csproj',
  'PVAD.Infrastructure.Word': 'src/Infrastructure/PVAD.Infrastructure.Word/PVAD.Infrastructure.Word.csproj',
  'PVAD.Infrastructure.Spreadsheet': 'src/Infrastructure/PVAD.Infrastructure.Spreadsheet/PVAD.Infrastructure.Spreadsheet.csproj',
  'PVAD.Generator': 'src/Generation/PVAD.Generator/PVAD.Generator.csproj',
});
export const importerProjects = Object.freeze({
  ...projects,
  'PVAD.Importer': 'src/Importing/PVAD.Importer/PVAD.Importer.csproj',
  'PVAD.FileImporter': 'src/Importing/PVAD.FileImporter/PVAD.FileImporter.csproj',
  'PVAD.FileImporter.ImageAnalysis': 'src/Importing/PVAD.FileImporter.ImageAnalysis/PVAD.FileImporter.ImageAnalysis.csproj',
  'PVAD.FileImporter.ImageOcr': 'src/Importing/PVAD.FileImporter.ImageOcr/PVAD.FileImporter.ImageOcr.csproj',
  'PVAD.FileImporter.ImageOcr.Worker': 'src/Importing/PVAD.FileImporter.ImageOcr.Worker/PVAD.FileImporter.ImageOcr.Worker.csproj',
});
export const executionProjects = Object.freeze({
  ...importerProjects,
  'PVAD.Infrastructure.Converters': 'src/Infrastructure/PVAD.Infrastructure.Converters/PVAD.Infrastructure.Converters.csproj',
  'PVAD.CodeWorker': 'src/Execution/PVAD.CodeWorker/PVAD.CodeWorker.csproj',
});
const migrationNames = {
  Common: ['PVAD.Migration.Abstractions', 'PVAD.Migration.Roslyn'],
  PDF: ['PVAD.Migration.Pdf', 'PVAD.Migration.Pdf.Viewer.Nutrient',
    ...['ActivePdf', 'Apryse', 'Aspose', 'DevExpress', 'DsPdf', 'Foxit', 'GemBox', 'IronPdf', 'IText7', 'Leadtools', 'Nutrient', 'PdfKitNet', 'PdfTools', 'PdfToolsToolbox', 'Spire', 'Syncfusion'].map(name => 'PVAD.Migration.Pdf.Code.' + name)],
  Spreadsheet: ['PVAD.Migration.Spreadsheet', ...['Aspose', 'ClosedXml', 'Epplus', 'GemBox', 'Npoi', 'Spire', 'SpreadsheetLight', 'Syncfusion'].map(name => 'PVAD.Migration.Spreadsheet.Code.' + name)],
  Report: ['PVAD.Migration.Report', ...['ActiveReportsJs', 'DevExpress', 'FastReport', 'JasperReports', 'Rdl', 'Rpx', 'Stimulsoft', 'Telerik'].map(name => 'PVAD.Migration.Report.Designer.' + name)],
};
export const migrationProjects = Object.freeze({
  ...executionProjects,
  ...Object.fromEntries(Object.entries(migrationNames).flatMap(([folder, names]) => names.map(name => [name, `src/Migrations/${folder}/${name}/${name}.csproj`]))),
});
const migrationRoots = ['PVAD.Migration.Pdf', 'PVAD.Migration.Spreadsheet', 'PVAD.Migration.Report', 'PVAD.Migration.Pdf.Viewer.Nutrient'];
const migrationProbes = { 'migration-code': 'MigrationCodeEngineProbe', 'migration-report': 'MigrationReportEngineProbe', 'migration-viewer': 'MigrationViewerEngineProbe' };
const readJson = path => JSON.parse(readFileSync(path, 'utf8'));
const sha = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const xml = text => text.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

export function validateProjectClosure(profile = projects, roots = ['PVAD.Generator']) {
  const visited = new Set();
  function visit(path) {
    if (visited.has(path)) return;
    visited.add(path);
    const document = new JSDOM(readFileSync(path, 'utf8'), { contentType: 'text/xml' }).window.document;
    for (const item of document.querySelectorAll('ProjectReference')) visit(resolve(dirname(path), item.getAttribute('Include').replaceAll('\\', '/')));
  }
  for (const name of roots) { assert.ok(profile[name]); visit(resolve(root, profile[name])); }
  assert.deepEqual([...visited].sort(), Object.values(profile).map(path => resolve(root, path)).sort(), 'The selected dependency graph changed; review protection coverage before continuing.');
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 600_000, maxBuffer: 32 * 1024 * 1024, ...options });
  if (result.error || result.status !== 0) throw new Error(`${command} failed (${result.status}):\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  for (const line of (result.stdout ?? '').split(/\r?\n/).filter(line => !/^[\[{]/.test(line.trim()) && /warning|warnung|NPOI:/i.test(line))) console.error(line);
  return result.stdout;
}
const jsonResult = output => JSON.parse(output.trim().split(/\r?\n/).at(-1));

export function validateConsumerAssets(assets, profile = projects) {
  assert.ok(Object.values(assets.libraries).every(library => library.type === 'package'), 'The independent consumer must not resolve project references.');
  const own = Object.entries(assets.libraries).filter(([key]) => key.startsWith('PVAD.'));
  assert.deepEqual(own.map(([key]) => key.split('/')[0]).sort(), Object.keys(profile).sort());
  return own.length;
}

export function comparableSemantics(semantics) {
  const spreadsheet = JSON.parse(semantics.spreadsheet);
  // XLSX carries no PVAD workspace IDs; the importer intentionally creates new ones.
  const generatedId = /^[a-f0-9]{32}$/i;
  assert.match(spreadsheet.Id, generatedId);
  assert.equal(spreadsheet.Sheets.length, 1);
  assert.match(spreadsheet.Sheets[0].Id, generatedId);
  spreadsheet.Id = 'imported-workbook'; spreadsheet.Sheets[0].Id = 'imported-sheet';
  return { ...semantics, spreadsheet };
}

function regularOutputs(profile) {
  const snapshot = {};
  function visit(path) {
    if (!existsSync(path)) return;
    for (const item of readdirSync(path, { withFileTypes: true })) {
      if (item.isDirectory()) visit(join(path, item.name));
      else if (item.isFile()) snapshot[join(path, item.name)] = sha(join(path, item.name));
    }
  }
  for (const project of Object.values(profile)) for (const name of ['bin', 'obj']) visit(join(root, dirname(project), name));
  return snapshot;
}

export function main(args) {
  const includeMigrations = args.length === 1 && args[0] === '--migrations';
  const includeExecution = includeMigrations || (args.length === 1 && args[0] === '--execution');
  const includeImporter = includeExecution || (args.length === 1 && args[0] === '--importer');
  if (!includeImporter) assert.equal(args.length, 0, 'This rehearsal accepts no output, trust or publication overrides.');
  const profile = includeMigrations ? migrationProjects : includeExecution ? executionProjects : includeImporter ? importerProjects : projects;
  const workerName = 'PVAD.FileImporter.ImageOcr.Worker';
  const codeWorkerName = 'PVAD.CodeWorker';
  const packageProjects = Object.fromEntries(Object.entries(profile).filter(([name]) => name !== workerName && name !== codeWorkerName));
  const profileProperties = includeImporter ? ['-p:PvadIncludeImporter=true', ...(includeExecution ? ['-p:PvadIncludeExecution=true'] : []), ...(includeMigrations ? ['-p:PvadIncludeMigrations=true'] : [])] : [];
  validateProjectClosure(profile, includeExecution
    ? ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', workerName, codeWorkerName, 'PVAD.Infrastructure.Converters', ...(includeMigrations ? migrationRoots : [])]
    : includeImporter ? ['PVAD.Generator', 'PVAD.FileImporter.ImageAnalysis', workerName] : ['PVAD.Generator']);
  const { version } = checkRepository();
  const baselineTrust = sha(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'));
  const beforeBuild = regularOutputs(profile);
  const output = join(root, 'artifacts/native-joint-packages', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const work = join(output, 'private-candidate-work'); mkdirSync(work, { mode: 0o700 });
  const secret = mkdtempSync(join(tmpdir(), 'pvad-joint-packages-'));
  let succeeded = false;
  let report;
  try {
    const licenses = createTestLicenses(secret, Date.now(), includeMigrations, includeMigrations, false, true);
    const build = join(work, 'original-build'), consumerBuild = join(work, 'consumer-build');
    const protectedDirectory = join(work, 'protected-support-only');
    const protectionOptions = [`-p:CustomAfterMicrosoftCommonTargets=${join(here, 'protected-package.targets')}`, `-p:PvadProtectedPackageDirectory=${protectedDirectory}`];
    const probe = join(here, 'GeneratorPackageProbe/GeneratorPackageProbe.csproj');
    const common = ['--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false',
      '-p:DebugType=None', '-p:DebugSymbols=false', '-p:EmbedAllSources=false', '-p:EnableSourceLink=false', '-p:EnableSourceControlManagerQueries=false'];
    console.error('Building the isolated original product dependency graph.');
    run('dotnet', ['build', probe, '-c', 'Release', '--artifacts-path', build, ...common, ...protectionOptions, ...profileProperties,
      '-p:PvadOriginalBaseline=true', '-p:CopyLocalLockFileAssemblies=true', `-p:PvadLicenseTrustStorePath=${licenses.trustPath}`]);
    const original = join(build, 'bin/GeneratorPackageProbe/release');
    const originalWorker = join(build, `bin/${workerName}/release`);
    const originalCodeWorker = join(build, `bin/${codeWorkerName}/release`);
    if (includeImporter) copyFileSync(join(originalWorker, workerName + '.dll'), join(original, workerName + '.dll'));
    const workerVendorDifferences = {};
    if (includeExecution) {
      copyFileSync(join(originalCodeWorker, codeWorkerName + '.dll'), join(original, codeWorkerName + '.dll'));
      for (const name of readdirSync(originalCodeWorker).filter(name => name.endsWith('.dll') && !name.startsWith('PVAD.'))) {
        const target = join(original, name), source = join(originalCodeWorker, name);
        if (existsSync(target) && sha(target) !== sha(source) && includeMigrations && ['Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll'].includes(name))
          workerVendorDifferences[name] = { migrationResolutionSha256: sha(target), standaloneWorkerSha256: sha(source) };
        else if (existsSync(target)) assert.equal(sha(target), sha(source), 'Conflicting worker dependency: ' + name);
        else copyFileSync(source, target);
      }
    }
    const invoke = (directory, resultDirectory, extra = [], workerDirectory = originalWorker, codeDirectory = originalCodeWorker) => jsonResult(run('dotnet', [join(directory, 'GeneratorPackageProbe.dll'),
      licenses.integrated, licenses.importer, licenses.pdf, licenses.revocations, resultDirectory, ...extra], { env: { ...process.env,
        PVAD_PACKAGE_WRONG_LICENSE_PATH: licenses.word,
        PVAD_PACKAGE_OCR_WORKER_PATH: join(workerDirectory, workerName + '.dll'),
        PVAD_PACKAGE_CODE_WORKER_PATH: join(codeDirectory, codeWorkerName + '.dll'),
        PVAD_PACKAGE_CONVERTER_LICENSE_PATH: licenses.converter,
        PVAD_PACKAGE_EXPIRED_LICENSE_PATH: licenses.expired,
        PVAD_PACKAGE_MIGRATION_ASSEMBLIES: JSON.stringify(Object.keys(profile).filter(name => name.startsWith('PVAD.Migration.'))),
        PVAD_PACKAGE_TEST_FONT: join(root, 'assets/fonts/liberation/LiberationSans-Regular.ttf') } }));
    const before = invoke(original, join(work, 'original-result'));
    const originalMigrations = {};
    if (includeMigrations) for (const [kind, name] of Object.entries(migrationProbes))
      originalMigrations[kind] = jsonResult(run('dotnet', [join(build, `bin/${name}/release/${name}.dll`), licenses[kind], licenses.pdf, licenses.expired]));
    console.error(`Jointly protecting ${Object.keys(profile).length} first-party assemblies.`);
    run('dotnet', ['tool', 'restore', '--tool-manifest', join(here, '.config/dotnet-tools.json')]);
    const modules = protect(original, protectedDirectory, Object.keys(profile).map(name => name + '.dll'));
    const protectedWorker = join(work, 'protected-ocr-worker');
    const protectedCodeWorker = join(work, 'protected-code-worker');
    const workerHashes = {};
    const codeWorkerHashes = {};
    if (includeImporter) {
      cpSync(originalWorker, protectedWorker, { recursive: true, filter: path => !/\.(pdb|map|cs)$/i.test(path) });
      for (const name of readdirSync(protectedWorker).filter(name => name.startsWith('PVAD.') && name.endsWith('.dll'))) {
        assert.ok(modules.includes(name));
        copyFileSync(join(protectedDirectory, name), join(protectedWorker, name));
        workerHashes[name] = sha(join(protectedWorker, name));
      }
      assert.equal(Object.keys(workerHashes).length, 4, 'The OCR worker dependency graph changed.');
    }
    if (includeExecution) {
      cpSync(originalCodeWorker, protectedCodeWorker, { recursive: true, filter: path => !/\.(pdb|map|cs)$/i.test(path) });
      for (const name of readdirSync(protectedCodeWorker).filter(name => name.startsWith('PVAD.') && name.endsWith('.dll'))) {
        assert.ok(modules.includes(name));
        copyFileSync(join(protectedDirectory, name), join(protectedCodeWorker, name));
        codeWorkerHashes[name] = sha(join(protectedCodeWorker, name));
      }
      assert.equal(Object.keys(codeWorkerHashes).length, 10, 'The CodeWorker dependency graph changed.');
    }
    // Resolution inputs remain byte-identical and are never supplied as Obfuscar modules.
    const thirdParty = readdirSync(original).filter(name => name.endsWith('.dll') && !name.startsWith('PVAD.') && name !== 'GeneratorPackageProbe.dll');
    for (const name of thirdParty) copyFileSync(join(original, name), join(protectedDirectory, name));
    const surfaceBuild = join(work, 'surface-build');
    run('dotnet', ['build', join(here, 'ApiSurfaceProbe/ApiSurfaceProbe.csproj'), '-c', 'Release', '--artifacts-path', surfaceBuild, ...common]);
    const surfaceProbe = join(surfaceBuild, 'bin/ApiSurfaceProbe/release/ApiSurfaceProbe.dll');
    const surface = path => jsonResult(run('dotnet', [surfaceProbe, path]));
    const surfaces = {};
    for (const name of modules) {
      const originalSurface = surface(join(original, name));
      assert.deepEqual(surface(join(protectedDirectory, name)), originalSurface, `Public API drift: ${name}`);
      surfaces[name] = { types: originalSurface.length, apiSha256: createHash('sha256').update(JSON.stringify(originalSurface)).digest('hex') };
    }
    for (const [name, internalName] of [['PVAD.Licensing.dll', 'PvadLicenseAuthority'], ['PVAD.Pdf.dll', 'PdfWriter']]) {
      const types = directory => run('dotnet', ['tool', 'run', 'ilspycmd', '--', '--disable-updatecheck', '-l', 'c', join(directory, name)], { cwd: here });
      assert.ok(types(original).includes(internalName));
      assert.ok(!types(protectedDirectory).includes(internalName), `Internal implementation name retained: ${name}`);
    }
    const feed = join(work, 'feed'); mkdirSync(feed);
    console.error('Packing exact protected assemblies into an isolated local feed.');
    for (const [name, project] of Object.entries(packageProjects)) run('dotnet', ['pack', project, '-c', 'Release', '--no-build', '--no-restore',
      '--artifacts-path', build, '-o', feed, ...common, ...protectionOptions, `-p:PvadProtectedAssemblyPath=${join(protectedDirectory, name + '.dll')}`,
      `-p:PvadLicenseTrustStorePath=${licenses.trustPath}`]);
    const dependencies = join(work, 'third-party-feed'); mkdirSync(dependencies);
    const assets = readJson(join(build, 'obj/GeneratorPackageProbe/project.assets.json'));
    const dependencyAssets = [assets, ...(includeMigrations ? Object.values(migrationProbes).map(name => readJson(join(build, `obj/${name}/project.assets.json`))) : [])];
    const dependencyKeys = new Set();
    let dependencyCount = 0;
    for (const dependencyAsset of dependencyAssets)
    for (const [key, library] of Object.entries(dependencyAsset.libraries).filter(([, value]) => value.type === 'package')) {
      if (dependencyKeys.has(key.toLowerCase())) continue;
      dependencyKeys.add(key.toLowerCase());
      const [id, packageVersion] = key.toLowerCase().split('/');
      const file = `${id}.${packageVersion}.nupkg`;
      const source = Object.keys(dependencyAsset.packageFolders).map(path => join(path, library.path, file)).find(existsSync);
      assert.ok(source, `An offline third-party package is unavailable: ${key}`);
      copyFileSync(source, join(dependencies, file)); dependencyCount++;
    }
    const config = join(work, 'NuGet.Config');
    writeFileSync(config, `<?xml version="1.0"?><configuration><packageSources><clear/><add key="protected" value="${xml(feed)}"/><add key="dependencies" value="${xml(dependencies)}"/></packageSources><packageSourceMapping><clear/><packageSource key="protected"><package pattern="PVAD.*"/></packageSource><packageSource key="dependencies"><package pattern="*"/></packageSource></packageSourceMapping></configuration>`);
    const packages = join(work, 'consumer-packages');
    console.error('Restoring and running the independent consumer without network package sources.');
    run('dotnet', ['restore', probe, '--artifacts-path', consumerBuild, '--configfile', config, '--disable-parallel',
      `-p:RestorePackagesPath=${packages}`, '-p:NuGetAudit=false', ...profileProperties]);
    const consumerAssets = readJson(join(consumerBuild, 'obj/GeneratorPackageProbe/project.assets.json'));
    validateConsumerAssets(consumerAssets, packageProjects);
    assert.ok(consumerAssets.libraries['Microsoft.Build.Tasks.Git/10.0.403'], 'The external consumer lost the existing build-task version constraint.');
    for (const key of Object.keys(consumerAssets.libraries).filter(key => key.startsWith('PVAD.')))
      assert.equal(key.split('/')[1], version, 'A first-party package version differs from VERSION.');
    const consumerEnvironment = { ...process.env };
    delete consumerEnvironment.MSBUILDNOINPROCNODE;
    run('dotnet', ['build', probe, '-c', 'Release', '--no-restore', '--artifacts-path', consumerBuild, ...common, ...profileProperties,
      `-p:RestorePackagesPath=${packages}`], { env: consumerEnvironment });
    const after = invoke(join(consumerBuild, 'bin/GeneratorPackageProbe/release'), join(work, 'protected-result'), [feed], protectedWorker, protectedCodeWorker);
    const protectedMigrations = {};
    if (includeMigrations) for (const [kind, name] of Object.entries(migrationProbes)) {
      const project = join(here, name, name + '.csproj');
      const extra = ['-p:PvadProtectedMigrationConsumer=true', `-p:RestorePackagesPath=${packages}`];
      run('dotnet', ['restore', project, '--artifacts-path', consumerBuild, '--configfile', config, '--disable-parallel', '-p:NuGetAudit=false', ...extra]);
      const actualAssets = readJson(join(consumerBuild, `obj/${name}/project.assets.json`));
      const originalAssets = readJson(join(build, `obj/${name}/project.assets.json`));
      const expected = Object.fromEntries(Object.keys(originalAssets.libraries).filter(key => key.startsWith('PVAD.')).map(key => [key.split('/')[0], profile[key.split('/')[0]]]));
      assert.ok(Object.values(expected).every(Boolean), 'Unreviewed migration consumer dependency.');
      validateConsumerAssets(actualAssets, expected);
      for (const key of Object.keys(actualAssets.libraries).filter(key => key.startsWith('PVAD.'))) assert.equal(key.split('/')[1], version);
      for (const key of Object.keys(originalAssets.libraries).filter(key => originalAssets.libraries[key].type === 'package'))
        assert.ok(actualAssets.libraries[key], 'Migration dependency version drift: ' + name + ': ' + key);
      run('dotnet', ['build', project, '-c', 'Release', '--no-restore', '--artifacts-path', consumerBuild, ...common, ...extra], { env: consumerEnvironment });
      protectedMigrations[kind] = jsonResult(run('dotnet', [join(consumerBuild, `bin/${name}/release/${name}.dll`), licenses[kind], licenses.pdf, licenses.expired]));
      assert.deepEqual(protectedMigrations[kind], originalMigrations[kind], 'Migration provider output or admission drift: ' + kind);
      assert.equal(new Set(protectedMigrations[kind].checks).size, protectedMigrations[kind].checks.length);
    }
    for (const result of [before, after]) {
      assert.equal(new Set(result.checks).size, result.checks.length, 'Assertion names must be unique.');
      if (includeExecution) {
        assert.equal(Object.keys(result.semantics.execution.converters).length, 62, 'The Converter call matrix is incomplete.');
        assert.equal(result.checks.filter(name => name.startsWith('worker-exit-contract:')).length, 12, 'The real CodeWorker request matrix is incomplete.');
      }
    }
    assert.deepEqual(comparableSemantics(after.semantics), comparableSemantics(before.semantics), 'Protected output changes JSON, Word or Spreadsheet semantics.');
    for (const name of Object.keys(packageProjects)) assert.equal(after.assemblyHashes[name], sha(join(protectedDirectory, name + '.dll')));
    const pageHashes = (directory, file = 'document.pdf', prefix = 'page', count = 2) => {
      run('pdftoppm', ['-r', '72', '-png', join(directory, file), join(directory, prefix)]);
      const pages = readdirSync(directory).filter(name => new RegExp(`^${prefix}-\\d+\\.png$`).test(name)).sort();
      assert.equal(pages.length, count);
      return pages.map(name => sha(join(directory, name)));
    };
    const pages = pageHashes(join(work, 'original-result'));
    assert.deepEqual(pageHashes(join(work, 'protected-result')), pages, 'Protected PDF pixels differ from the unprotected baseline.');
    if (includeImporter) assert.deepEqual(pageHashes(join(work, 'protected-result'), 'regenerated.pdf', 'regenerated', 1),
      pageHashes(join(work, 'original-result'), 'regenerated.pdf', 'regenerated', 1), 'Protected regeneration pixels differ.');
    if (includeExecution) assert.deepEqual(pageHashes(join(work, 'protected-result'), 'code-worker.pdf', 'code-worker', 2),
      pageHashes(join(work, 'original-result'), 'code-worker.pdf', 'code-worker', 2), 'Protected CodeWorker PDF pixels differ.');
    for (const name of thirdParty) assert.equal(sha(join(protectedDirectory, name)), sha(join(original, name)));
    const supportRetention = rehearseSupportRetention({ original, protectedDirectory, modules, work, version });
    report = { schemaVersion: 1, scope: includeMigrations ? 'native-products-and-migrations-fifty-four-assembly-graph' : includeExecution ? 'generator-importer-converter-workers-sixteen-assembly-graph' : includeImporter ? 'generator-importer-ocr-fourteen-assembly-graph' : 'generator-nine-assembly-graph', version, syntheticOnly: true,
      platform: process.platform, architecture: after.processArchitecture, modules, publicApi: surfaces,
      baselineChecks: before.checks.length, protectedChecks: after.checks.length,
      baselineCheckNames: before.checks, protectedCheckNames: after.checks,
      protectedAssemblyHashes: after.assemblyHashes, pdfPages: pages.length, pdfPixelsEqual: true,
      jsonWordSpreadsheetEqual: true, thirdPartyAssembliesUnchanged: thirdParty.length, offlineDependencyPackages: dependencyCount,
      independentPackageReferences: Object.keys(packageProjects).length, productWideAcceptance: false,
      importerSmokeCompared: includeImporter, workerAssemblyHashes: workerHashes,
      codeWorkerAssemblyHashes: codeWorkerHashes, codeWorkerExecuted: includeExecution,
      converterSmokeCompared: includeExecution, codeWorkerPdfPixelsEqual: includeExecution,
      converterCallCases: includeExecution ? 62 : 0, codeWorkerRequestCases: includeExecution ? 12 : 0,
      executionNormalizations: includeExecution ? ['zip-container-timestamps-excluded', 'odt-meta-xml-parsed-with-generated-creation-date-replaced'] : [],
      migrationConsumerResults: protectedMigrations, migrationOriginalResults: originalMigrations,
      migrationProviderComparisonPassed: includeMigrations, workerVendorDifferences, supportRetention,
      nativeOcrExecuted: includeImporter && after.semantics.importer.nativeOcrExecuted,
      workerImporterMigrationAcceptance: false, productionTrustProvisioned: false, publicationPerformed: false };
    succeeded = true;
  } finally {
    rmSync(secret, { recursive: true, force: true }); rmSync(work, { recursive: true, force: true });
    assert.ok(!existsSync(secret) && !existsSync(work), 'Candidate keys, maps, packages or binaries remain.');
    assert.equal(sha(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json')), baselineTrust);
    assert.deepEqual(regularOutputs(profile), beforeBuild, 'Regular product build outputs changed.');
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, ephemeralArtifactsRemoved: true, sourceTrustUnchanged: true, regularProductBuildsUnchanged: true }, null, 2) + '\n');
  }
  writeFileSync(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(`Joint protected package rehearsal passed: ${output}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
