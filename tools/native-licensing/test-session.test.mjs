import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { migrationCodeTestGrants, migrationReportTestGrant } from './with-test-license.mjs';
import { closeNativeTestSession, createNativeTestSession, nativeTestEnvironment,
  prepareGitHubSession, sessionEnvironment, withNativeTestSession } from './test-session.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const sourceTrust = () => readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'), 'utf8');

test('a signed session owns one separate build and removes licenses and binaries without altering source trust', () => {
  const before = sourceTrust();
  const session = createNativeTestSession();
  const build = session.environment.ArtifactsPath;
  const output = dirname(build);
  try {
  assert.deepEqual(nativeTestEnvironment(session.environment), session.environment);
  assert.equal(session.environment.NativeLicensing__LicensePath, session.environment.PVAD_NATIVE_LICENSE_PATH);
  const envelope = JSON.parse(readFileSync(session.environment.PVAD_NATIVE_LICENSE_PATH));
  assert.deepEqual(JSON.parse(Buffer.from(envelope.payload, 'base64')).grants,
    [...['pdf', 'word', 'spreadsheet', 'importer', 'converter'].map(product => ({ product, providers: [] })), ...migrationCodeTestGrants(),
      ...['viewer', 'designer'].map(kind => ({ product: `migration.pdf.${kind}`, providers: ['nutrient-viewer'] })), migrationReportTestGrant()]);
  assert.equal(session.environment.UseArtifactsOutput, 'true');
  mkdirSync(build, { recursive: true });
  writeFileSync(join(build, 'synthetic.dll'), 'synthetic');
  } finally { closeNativeTestSession(session.descriptor); }
  assert.ok(!existsSync(build) && !existsSync(dirname(session.descriptor)));
  assert.equal(sourceTrust(), before);
  assert.deepEqual(JSON.parse(readFileSync(join(output, 'cleanup.json'))), {
    syntheticOnly: true, temporaryFilesRemoved: true, sourceTrustUnchanged: true, artifactsUnchanged: true,
  });
});

test('Nutrient Viewer and Designer use separate grants and a narrow private source-import boundary', () => {
  const read = path => readFileSync(resolve(root, path), 'utf8');
  const scope = read('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(scope.includes('current.operation != PvadNativeOperation.PdfDesignerMigration'));
  assert.ok(scope.includes('Demand(PvadLicenseProducts.PdfDesignerMigration, expectedAuthority, "nutrient-viewer")'));
  assert.ok(!/\(PvadNativeOperation.Pdf(?:Designer|Viewer)Migration, PvadNativeDependency\./.test(scope));
  const source = read('src/Migrations/PDF/PVAD.Migration.Pdf.Viewer.Nutrient/NutrientViewerSourceConverter.cs');
  assert.equal(source.match(/BeginOperation\(PvadNativeOperation.PdfViewerMigration/g).length, 2);
  const instant = read('src/Migrations/PDF/PVAD.Migration.Pdf.Viewer.Nutrient/NutrientInstantJsonConverter.cs');
  assert.ok(instant.includes('BeginOperation(PvadNativeOperation.PdfViewerMigration, ProviderKey)'));
  for (const text of [source, instant]) assert.ok(text.includes('DemandMigrationContinuation(PvadLicenseProducts.PdfDesignerMigration'));
  const service = read('PVAD.WebApi/Services/NutrientViewerDesignMigrationService.cs');
  assert.ok(service.includes('BeginOperation(PvadNativeOperation.PdfDesignerMigration'));
  assert.ok(service.includes('PdfFileImporter.ImportNutrientDesignerSourceAsync('));
  assert.ok(!service.includes('PdfFileImporter.DoImportAsync('));
  assert.ok(service.includes('.ConvertForDesigner(') && service.includes('.ConvertTranspiledTypeScriptForDesigner('));
  const controller = read('PVAD.WebApi/Controllers/UnifiedMigrationsController.cs');
  assert.ok(controller.includes('when (exception is not PVAD.Licensing.PvadLicenseException)'));
  const importer = read('src/Importing/PVAD.FileImporter/PdfFileImporter.cs');
  assert.ok(importer.includes('using var operation = PvadLicense.BeginNutrientDesignerPdfImport();'));
  assert.ok(importer.includes('using var operation = PvadLicense.BeginOperation(PvadNativeOperation.DesignerPdfImport);'));
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('.github/workflows/native-library-licensing.yml'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  assert.ok(commands.includes('with-test-license.mjs migration-viewer'));
  assert.ok(commands.includes('FullyQualifiedName~PvadNativeViewerMigrationLicenseTests'));
  assert.ok(commands.includes('tests/PVAD.Migration.Pdf.Viewer.Nutrient.Tests/PVAD.Migration.Pdf.Viewer.Nutrient.Tests.csproj'));
  const upload = workflow.jobs.licensing.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  assert.ok(upload.with.path.includes('migration-viewer-admission.json'));
});

test('only validated owned session metadata is propagated through environment allowlists', () => {
  assert.deepEqual(nativeTestEnvironment({ ArtifactsPath: '/foreign', PvadLicenseTrustStorePath: '/foreign-key',
    NativeLicensing__LicensePath: '/customer-license', PVAD_NATIVE_LICENSE_PATH: '/customer-license' }), {});
  const session = createNativeTestSession();
  try {
    assert.throws(() => nativeTestEnvironment({ ...session.environment, ArtifactsPath: '/foreign' }), /configuration drift/);
    assert.throws(() => nativeTestEnvironment({ ...session.environment, PvadNativeLicenseTestBootstrap: 'false' }), /configuration drift/);
  } finally { closeNativeTestSession(session.descriptor); }
});

test('changed license fixtures fail closed and are still removed', () => {
  const session = createNativeTestSession();
  const output = dirname(session.environment.ArtifactsPath);
  writeFileSync(session.environment.PVAD_NATIVE_LICENSE_PATH, '{}');
  assert.throws(() => sessionEnvironment(session.descriptor), /artifacts changed/);
  assert.throws(() => closeNativeTestSession(session.descriptor), /artifacts changed/);
  assert.ok(!existsSync(dirname(session.descriptor)));
  assert.equal(JSON.parse(readFileSync(join(output, 'cleanup.json'))).artifactsUnchanged, false);
});

test('cleanup rejects foreign paths instead of deleting them', () => {
  const foreign = mkdtempSync(join(tmpdir(), 'pvad-foreign-guard-'));
  const path = join(foreign, 'session.json');
  writeFileSync(path, '{}');
  try {
    assert.throws(() => closeNativeTestSession(path), /owned temporary directory/);
    assert.ok(existsSync(path));
    assert.throws(() => closeNativeTestSession('relative/session.json'));
  } finally { rmSync(foreign, { recursive: true, force: true }); }
});

test('nested sessions reuse the owner and restore ambient configuration after failure', async () => {
  const before = process.env.PVAD_NATIVE_TEST_SESSION;
  let descriptor;
  await assert.rejects(withNativeTestSession(async () => {
    descriptor = process.env.PVAD_NATIVE_TEST_SESSION;
    await withNativeTestSession(async () => assert.equal(process.env.PVAD_NATIVE_TEST_SESSION, descriptor));
    assert.ok(existsSync(descriptor));
    throw new Error('synthetic failure');
  }), /synthetic failure/);
  assert.equal(process.env.PVAD_NATIVE_TEST_SESSION, before);
  assert.ok(!existsSync(descriptor));
});

test('GitHub environment setup is restricted to a runner-owned file', () => {
  assert.throws(() => prepareGitHubSession({}), /reserved for GitHub/);
  const temporary = mkdtempSync(join(tmpdir(), 'pvad-gh-env-'));
  const destination = join(temporary, 'environment');
  writeFileSync(destination, '');
  let descriptor;
  try {
    prepareGitHubSession({ GITHUB_ACTIONS: 'true', GITHUB_ENV: destination, RUNNER_TEMP: temporary });
    const environment = Object.fromEntries(readFileSync(destination, 'utf8').trim().split('\n').map(line => {
      const split = line.indexOf('='); return [line.slice(0, split), line.slice(split + 1)];
    }));
    descriptor = environment.PVAD_NATIVE_TEST_SESSION;
    assert.deepEqual(nativeTestEnvironment(environment), environment);
    assert.throws(() => prepareGitHubSession({ GITHUB_ACTIONS: 'true', GITHUB_ENV: destination,
      RUNNER_TEMP: join(temporary, 'elsewhere') }));
  } finally {
    if (descriptor) closeNativeTestSession(descriptor);
    rmSync(temporary, { recursive: true, force: true });
  }
});

test('generic command execution propagates signed configuration and cleans up after command failure', () => {
  const source = 'console.log(JSON.stringify({descriptor:process.env.PVAD_NATIVE_TEST_SESSION,build:process.env.ArtifactsPath}));process.exit(23);';
  const result = spawnSync(process.execPath, ['tools/native-licensing/test-session.mjs', 'exec', '--', process.execPath, '-e', source],
    { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 1);
  const output = JSON.parse(result.stdout.trim());
  assert.ok(!existsSync(output.descriptor) && !existsSync(output.build));
  assert.match(result.stderr, /failed \(23\)/);
});

test('CI test jobs prepare and always clean up while publication jobs never activate synthetic trust', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const ci = load(readFileSync(resolve(root, '.github/workflows/ci.yml'), 'utf8'));
  const platform = load(readFileSync(resolve(root, '.github/workflows/visual-parity-platforms.yml'), 'utf8'));
  const fuzz = load(readFileSync(resolve(root, '.github/workflows/pdf-trust-fuzz.yml'), 'utf8'));
  for (const job of [ci.jobs.dotnet, ci.jobs['visual-parity'], platform.jobs['platform-parity'], fuzz.jobs['parser-fuzz']]) {
    const prepare = job.steps.findIndex(step => step.run?.includes('test-session.mjs prepare-ci'));
    const cleanup = job.steps.findIndex(step => step.run?.includes('test-session.mjs cleanup-ci'));
    assert.ok(prepare >= 0 && cleanup > prepare);
    assert.equal(job.steps[cleanup].if, 'always()');
    const firstDotnet = job.steps.findIndex(step => /dotnet (restore|build|test)/.test(step.run ?? ''));
    assert.ok(firstDotnet > prepare);
  }
  for (const [name, job] of Object.entries(ci.jobs)) {
    if (name === 'dotnet' || name === 'visual-parity') continue;
    assert.ok(job.steps.every(step => !step.run?.includes('test-session.mjs prepare-ci')));
  }
  const release = readFileSync(resolve(root, '.github/workflows/release.yml'), 'utf8');
  assert.ok(!release.includes('test-session.mjs'));
});

test('build contracts prohibit publication and scope shared bootstrap to real test/tool harnesses', () => {
  const targets = readFileSync(resolve(root, 'Directory.Build.targets'), 'utf8');
  assert.match(targets, /BeforeTargets="Pack;Publish;GenerateNuspec;ComputeFilesToPublish"/);
  assert.match(targets, /PVAD.NativeTestArtifact/);
  assert.match(targets, /TargetDir/);
  assert.match(targets, /IntermediateOutputPath/);
  const props = readFileSync(resolve(root, 'tests/Directory.Build.props'), 'utf8');
  assert.match(props, /MSBuildProjectName\)' != 'PVAD.Licensing.Tests'/);
  assert.ok(!targets.includes("MSBuildProjectName)' == 'PVAD.CodeWorker'"));
});

test('native CI covers Word/Spreadsheet/Converter admission, compatibility, and API failure propagation', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const workflow = load(readFileSync(resolve(root, '.github/workflows/native-library-licensing.yml'), 'utf8'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  const upload = workflow.jobs.licensing.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  for (const product of ['Word', 'Spreadsheet', 'Converters']) {
    const singular = product === 'Converters' ? 'Converter' : product;
    assert.ok(workflow.on.pull_request.paths.includes(`src/Infrastructure/PVAD.Infrastructure.${product}/**`));
    assert.ok(commands.includes(`with-test-license.mjs ${singular.toLowerCase()}`));
    assert.ok(commands.includes(`tests/PVAD.Infrastructure.${product}.Tests/PVAD.Infrastructure.${product}.Tests.csproj`));
    assert.ok(commands.includes(`FullyQualifiedName~PvadNative${singular}LicenseTests`));
    assert.ok(upload.with.path.includes(`${singular.toLowerCase()}-admission.json`));
  }
  assert.ok(!upload.with.path.includes('isolated-build'));
});

test('Importer public entry closure retains separate package and platform acceptance gates', () => {
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  const importer = matrix.products.find(product => product.id === 'importer');
  assert.equal(importer.enforced, true);
  assert.equal(importer.coverageStatus, 'public-entry-points-and-built-in-continuations');
  assert.ok(importer.coveredEntryPoints.length > 0);
  assert.deepEqual(importer.remainingEntryPoints, []);
  for (const name of ['PdfTokenizer.ReadToken/Seek', 'PdfStreamDecoderRegistry.Decode', 'PdfObjectParser', 'SemanticLayoutEngine', 'PdfSrgbColorConverter', 'PdfFunction.TryCreate/Evaluate', 'PdfFontResource.Decode/GetGlyphCodes', 'PdfEditingSession', 'PdfEditableDocument', 'PdfContentStreamRewriter'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing direct engine inventory: ${name}`);
  for (const name of ['PdfTrustInspector', 'PdfOcspEvidence', 'PdfDocumentDecryptor', 'PdfStandardSecurityHandler', 'PdfPadesSigningService', 'PdfPkcs12SigningKey', 'PdfRfc3161TimestampAuthority', 'PdfRedactionAuditVerifier', 'PdfEInvoiceAttacher', 'PdfAssociatedFileExtractor'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing Trust entry inventory: ${name}`);
  for (const name of ['PvadPdfGeneratorBridge', 'PdfGeneratorBridgePipeline.SaveAsync', 'PdfDebugOverlayBuilder.Build'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing regeneration entry inventory: ${name}`);
  for (const name of ['ImageAnalysisFileImporter', 'ImageToPdfConverter.ConvertAsync', 'DefaultOcrWorkerProcessRunner.RunAsync'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing image/OCR entry inventory: ${name}`);
  assert.equal(importer.acceptanceStatus, 'partial-release-validation-and-distribution');
  for (const name of ['complete general compatibility and fuzz validation', 'manufacturer trust and production custody', 'final contractual and third-party distribution approval', 'per-platform direct embedded-OCR phase verification'])
    assert.ok(importer.remainingAcceptance.some(entry => entry.includes(name)), `Missing remaining Importer acceptance: ${name}`);
  assert.equal(importer.verifiedAcceptance.length, 3);
  assert.ok(importer.verifiedAcceptance.some(entry => entry.includes('all 201')));
  assert.ok(importer.verifiedAcceptance.some(entry => entry.includes('Windows/x64, Linux/x64 and macOS/Intel')));
  assert.ok(importer.admittedContinuations.some(entry => entry.includes('non-PDF file importers')));
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const workflow = load(readFileSync(resolve(root, '.github/workflows/native-library-licensing.yml'), 'utf8'));
  assert.ok(workflow.on.pull_request.paths.includes('src/Importing/**'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  assert.ok(commands.includes('node tools/native-licensing/importer-surface.mjs verify'));
  assert.ok(commands.includes('FullyQualifiedName~PvadNativeImporterLicenseTests'));
  assert.ok(commands.includes('FullyQualifiedName~PvadNativeTrustLicenseTests'));
  for (const name of ['PdfTrust', 'PdfSignature', 'PdfRedaction', 'PdfEInvoice'])
    assert.ok(commands.includes(`FullyQualifiedName~${name}`));
  for (const project of ['PVAD.Importer.Tests', 'PVAD.FileImporter.Tests'])
    assert.ok(commands.includes(`tests/${project}/${project}.csproj`));
  for (const project of ['PVAD.Generator.Tests', 'PVAD.Infrastructure.Pdf.Tests', 'PVAD.FileImporter.ImageAnalysis.Tests', 'PVAD.FileImporter.ImageOcr.Tests']) {
    assert.ok(workflow.on.pull_request.paths.includes(`tests/${project}/**`));
    assert.ok(commands.includes(`tests/${project}/${project}.csproj`));
  }
  assert.ok(commands.includes('FullyQualifiedName~MigrationGeneratedCodeExecutionTests'));
});

test('operation continuation policy stays internal and never replaces public or worker admission', () => {
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  assert.equal(matrix.continuationCoverage.status, 'reviewed-current-built-in-pipelines');
  assert.deepEqual(matrix.continuationCoverage.remaining, []);
  for (const name of ['word-built-in-docx-export-and-signing', 'code-provider-analysis-and-conversion', 'report-provider-resources-and-subreports', 'nutrient-viewer-and-designer-source-pdf'])
    assert.ok(matrix.continuationCoverage.covered.includes(name));
  assert.equal(matrix.continuationCoverage.publicAdmission, 'fresh-current-license');
  assert.equal(matrix.continuationCoverage.authorityBinding, 'originating-process-verifier');
  assert.equal(matrix.continuationCoverage.workerAdmission, 'fresh-at-dispatch-and-worker-start');
  assert.ok(matrix.continuationCoverage.covered.includes('designer-pdf-file-import-dto-analysis-mapping'));
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-trust-postprocessing-redaction-signing-final-validation'));
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-regeneration-mapping-xobject-and-debug-composites'));
  const scope = readFileSync(resolve(root, matrix.continuationCoverage.policySource), 'utf8');
  assert.match(scope, /internal sealed class PvadLicenseExecution/);
  assert.match(scope, /private static readonly AsyncLocal<PvadLicenseExecution\?> Slot/);
  assert.match(scope, /Volatile.Read\(ref current.closed\)/);
  assert.match(scope, /ReferenceEquals\(current.authority, expectedAuthority\)/);
  for (const name of ['RegenerationPdf', 'BlankPagePdf', 'OcrResultPdf'])
    assert.ok(scope.includes(`PvadNativeDependency.${name}`));
  const api = readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/PvadLicense.cs'), 'utf8');
  assert.match(api, /public static PvadLicenseOperation Require[^\n]+=> Authority.Value.Require\(product, provider\)/);
  assert.ok(!/public static[^\n]*(BeginOperation|DemandContinuation|DemandDependency)/.test(api));
  assert.match(api, /PvadLicenseExecution.Demand\(product, Authority.Value\)/);
  assert.match(api, /PvadLicenseExecution.DemandDependency\(dependency, Authority.Value\)/);
  const worker = readFileSync(resolve(root, 'src/Importing/PVAD.FileImporter.ImageOcr.Worker/Program.cs'), 'utf8');
  assert.ok(worker.indexOf('PvadLicense.LoadFromFile') < worker.indexOf('File.ReadAllTextAsync(requestPath)'));
  assert.ok(worker.indexOf('PvadLicense.Require(PvadLicenseProducts.Importer)') < worker.indexOf('File.ReadAllTextAsync(requestPath)'));
  assert.ok(!worker.includes('BeginOperation'));
});

test('typed PDF load/save use internal continuations without granting new public or custom calls', () => {
  const source = path => readFileSync(resolve(root, `src/Importing/PVAD.Importer/${path}`), 'utf8');
  const loader = source('PdfImporterOptions.cs');
  assert.match(loader, /BeginOperation\(PvadNativeOperation.PdfImport\)/);
  assert.match(loader, /PdfParseContext\(owner.Memory.ToArray\(\)/);
  for (const method of ['ParseAdmitted', 'ParseAndDecryptAdmitted', 'ParseDocumentGraphAdmitted', 'BuildAdmitted'])
    assert.ok(loader.includes(method));
  const reader = source('Parsing/PdfTokenReader.cs');
  assert.match(reader, /internal ref struct PdfTokenReader/);
  assert.ok(reader.includes('_tokenizer.ReadTokenAdmitted()') && reader.includes('_tokenizer.SeekAdmitted(position)'));
  assert.ok(!reader.includes('_tokenizer.ReadToken()') && !reader.includes('_tokenizer.Seek(position)'));
  for (const path of ['Saving/PdfDocumentSaver.cs', 'Saving/PdfFullRewriteWriter.cs', 'Saving/PdfIncrementalUpdateWriter.cs']) {
    const writer = source(path);
    assert.match(writer, /BeginOperation\(PvadNativeOperation.PdfSave\)/);
    assert.match(writer, /DemandContinuation\(PvadLicenseProducts.Importer\)/);
    assert.ok(!/public[^\n]*\b\w+Admitted\s*\(/.test(writer));
  }
  const decoder = source('Streams/PdfStreamDecoding.cs');
  assert.match(decoder, /_ => decoder.Decode\(/);
  assert.ok(decoder.includes('builtin.DecodeAdmitted('));
  assert.ok(source('Document/PdfDocumentBuilder.cs').includes('_fontParser.Parse('));
  assert.ok(source('Fonts/PdfFonts.cs').includes('Encoding.Decode('));
  const policy = readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs'), 'utf8');
  for (const purpose of ['PdfImport', 'PdfSave']) assert.ok(policy.includes(`PvadNativeOperation.${purpose}`));
  assert.ok(!/\(PvadNativeOperation.Pdf(?:Import|Save), PvadNativeDependency\./.test(policy));
});

test('Designer PDF mapping and all 19 analysis entries keep admission internal and output rules unchanged', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  const mapper = source('src/Importing/PVAD.FileImporter/PdfFileImporter.cs');
  assert.match(mapper, /BeginOperation\(PvadNativeOperation.DesignerPdfImport\)/);
  assert.ok(mapper.includes('.LoadAdmittedAsync(stream, cancellationToken)'));
  assert.ok(mapper.includes('.BuildPageAdmitted(pageNum - 1, page)'));
  const loader = source('src/Importing/PVAD.Importer/PdfImporterOptions.cs');
  assert.match(loader, /internal Task<Document.PdfDocumentModel> LoadAdmittedAsync/);
  assert.match(loader, /private async Task<Document.PdfDocumentModel> LoadAdmittedCoreAsync/);
  assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(loader));
  let wrappers = 0;
  for (const file of ['PrimitiveBuilder', 'BoundingBoxCalculator', 'ReadingOrderEngine', 'TextReconstructionEngine',
    'ClassificationEngines', 'GroupingEngine', 'SceneGraphEngine', 'SemanticLayout']) {
    const body = source(`src/Importing/PVAD.Importer/Analysis/${file}.cs`);
    const count = [...body.matchAll(/BeginOperation\(PvadNativeOperation.PdfAnalysis\)/g)].length;
    assert.ok(count > 0);
    assert.equal([...body.matchAll(/DemandContinuation\(PvadLicenseProducts.Importer\)/g)].length, count);
    assert.ok(!/public[^\n]*\w+Admitted\s*\(/.test(body));
    assert.ok(!body.includes('PvadLicense.Require('));
    wrappers += count;
  }
  assert.equal(wrappers, 19);
  const policy = source('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(!/\(PvadNativeOperation.(?:PdfAnalysis|DesignerPdfImport), PvadNativeDependency\./.test(policy));
  const probe = source('tools/native-licensing/ImporterEngineProbe/DesignerPdfContinuationChecks.cs');
  for (const fixture of ['labeled-chart', 'review-chart', 'invalid-metadata', 'mixed']) assert.ok(probe.includes(fixture));
  assert.ok(probe.includes('Fingerprint(result) == expected'));
});

test('editing continuations preserve fresh public entries and permit only private empty-page output', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  let wrappers = 0;
  for (const file of ['Editing/PdfPageEditor.cs', 'Editing/PdfPageComposer.cs', 'Content/PdfContentStreamRewriter.cs']) {
    const body = source(`src/Importing/PVAD.Importer/${file}`);
    const count = [...body.matchAll(/BeginOperation\(PvadNativeOperation.PdfEditing\)/g)].length;
    wrappers += count;
    assert.equal([...body.matchAll(/DemandContinuation\(PvadLicenseProducts.Importer\)/g)].length, count);
    assert.ok(!/public[^\n]*\w+Admitted\s*\(/.test(body));
    assert.ok(!body.includes('PvadLicense.Require('));
  }
  assert.equal(wrappers, 10);
  const facade = source('src/Importing/PVAD.Importer/Editing/PdfEditableDocument.cs');
  assert.match(facade, /private PdfPageOperationStatus ExecuteAdmitted\(Action action\)/);
  assert.match(facade, /private PdfPageOperationStatus InsertBlankPageAdmitted/);
  for (const method of ['Apply', 'ReadPageBox', 'Merge', 'GetPageCount', 'ExtractPages', 'AppendPages', 'Rewrite'])
    assert.ok(facade.includes(`.${method}Admitted(`), method);
  assert.ok(!/public[^\n]*\w+Admitted\s*\(/.test(facade));
  assert.ok(!facade.includes('PvadLicense.Require('));
  const writer = source('src/Generation/PVAD.Pdf/Serialization/PdfWriter.cs');
  assert.match(writer, /internal static byte\[\] CreateBlankPageForImporterAdmitted\(double width, double height\)/);
  assert.ok(writer.includes('PvadLicense.DemandDependency(PvadNativeDependency.BlankPagePdf)'));
  assert.ok(!writer.includes('public static byte[] CreateBlankPageForImporter'));
  const policy = source('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.match(policy, /\(PvadNativeOperation.PdfEditing, PvadNativeDependency.BlankPagePdf\) => true/);
  assert.ok(!/\(PvadNativeOperation.PdfEditing, PvadNativeDependency.(?:RegenerationPdf|OcrResultPdf)/.test(policy));
  const matrix = JSON.parse(source('product-metadata/native-library-products.v1.json'));
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-editing-page-composition-content-rewrite-and-facade'));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
});

test('Trust inspection and impact continuations stay internal without broadening dependency or callback rights', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  const trust = name => source(`src/Importing/PVAD.Importer/Trust/${name}.cs`);
  for (const [name, purpose] of [['PdfTrustInspector', 'PdfTrustInspection'], ['PdfChangeImpactAnalyzer', 'PdfChangeImpact']]) {
    const body = trust(name);
    assert.ok(body.includes(`BeginOperation(PvadNativeOperation.${purpose})`));
    assert.ok(body.includes('PvadLicense.DemandContinuation(PvadLicenseProducts.Importer)'));
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(body));
  }
  for (const name of ['PdfTrustInspector', 'PdfCertificationPolicyValidator', 'PdfEmbeddedValidationMaterial', 'PdfA2bConformanceValidator']) {
    const body = trust(name);
    assert.ok(!/\.(?:Parse|ParseDocumentGraph|Decode)\(/.test(body), name);
  }
  const evidence = trust('PdfEmbeddedValidationMaterial');
  assert.ok(trust('PdfTrustInspector').includes('PdfEmbeddedValidationMaterialExtractor.ExtractAdmitted(graph)'));
  assert.ok(evidence.includes('BeginOperation(PvadNativeOperation.PdfTrustInspection)'));
  assert.ok(evidence.includes('PdfCrlEvidenceValidator.ValidateHistoricalAdmitted('));
  assert.ok(evidence.includes('PdfOcspEvidence.ValidateHistoricalResponseAdmitted('));
  for (const name of ['PdfOcspEvidence', 'PdfRevocationEvidence']) {
    const body = trust(name);
    assert.ok(body.includes('BeginOperation(PvadNativeOperation.PdfTrustInspection)'));
    assert.ok(body.includes('PvadLicense.DemandContinuation(PvadLicenseProducts.Importer)'));
    assert.ok(!/public[^\n]*\w+Admitted\s*\(/.test(body));
  }
  const signature = trust('PdfSignatureValidator');
  assert.ok(signature.includes('.GetEvidenceAsync(') && signature.includes('.GetIssuerCertificatesAsync('));
  const policy = source('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(!/\(PvadNativeOperation.Pdf(?:TrustInspection|ChangeImpact), PvadNativeDependency\./.test(policy));
  const matrix = JSON.parse(source('product-metadata/native-library-products.v1.json'));
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-trust-inspection-embedded-evidence-and-change-impact'));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
  const probe = source('tools/native-licensing/ImporterEngineProbe/TrustContinuationChecks.cs');
  for (const value of ['certification-violation', 'long-term-evidence', 'tampered-signature', 'license-callback', 'same-complete-trust-report', 'no-false-preservation'])
    assert.ok(probe.includes(value));
  const runner = source('tools/native-licensing/with-test-license.mjs');
  assert.equal([...runner.matchAll(/'-nr:false', '-p:UseSharedCompilation=false'/g)].length, 2);
});

test('signing and timestamps retain sealed built-in dispatch without bypassing custom providers', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  const signing = name => source(`src/Importing/PVAD.Importer/Signing/${name}.cs`);
  const service = signing('PdfPadesSigningService');
  assert.ok(service.includes('PvadLicense.Require(PvadLicenseProducts.Importer);\n        return SignWithAdmissionAsync('));
  assert.ok(service.includes('using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfSigning)'));
  for (const method of ['CreateDetachedAdmitted', 'CreateDetachedWithTimestampAdmittedAsync', 'AppendAdmitted', 'AddAdmittedAsync', 'InspectAdmittedAsync'])
    assert.ok(service.includes(method));
  for (const name of ['PdfPadesSigningService', 'PdfLongTermValidationWriter', 'PdfDocumentTimestampService']) {
    const body = signing(name);
    assert.ok(!/new PdfCrossReferenceParser\(\)\.Parse\(|\.ParseDocumentGraph\(|decoder\.Decode\(|new PdfTrustInspector\(\)\.InspectAsync\(|new PdfIncrementalUpdateWriter\(\)\.Append\(/.test(body));
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(body));
  }
  const dependencies = signing('PdfSigningDependencies');
  assert.match(dependencies, /internal static class PdfSigningDependencies/);
  assert.ok(dependencies.includes('key is PdfPkcs12SigningKey builtin'));
  assert.ok(dependencies.includes(': key.SignHash(hash, algorithm)'));
  assert.ok(dependencies.includes('authority is PdfRfc3161TimestampAuthority builtin'));
  assert.ok(dependencies.includes(': authority.GetTimestampTokenAsync(digest, algorithm, cancellationToken)'));
  assert.match(signing('PdfRfc3161TimestampAuthority'), /public sealed class/);
  assert.match(signing('PdfSigningModels'), /public sealed class PdfPkcs12SigningKey/);
  const policy = source('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(!/\(PvadNativeOperation.Pdf(?:Signing|Timestamp), PvadNativeDependency\./.test(policy));
  const probe = source('tools/native-licensing/ImporterEngineProbe/SigningContinuationChecks.cs');
  for (const value of ['custom-key', 'custom-timestamp', 'material-provider', 'sync-source', 'synchronous-reentrant-denial', 'archive-timestamp'])
    assert.ok(probe.includes(value));
  assert.ok(source('tools/native-licensing/ImporterEngineProbe/Program.cs').includes('await SigningContinuationChecks.Run(args, checks)'));
});

test('redaction preserves built-in admission without bypassing custom preflight or changing evaluation rights', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  const importer = name => source(`src/Importing/PVAD.Importer/${name}.cs`);
  const apply = importer('Redaction/PdfRedactionApplier');
  assert.ok(apply.includes('preflight is PdfRedactionPreflight builtin'));
  assert.ok(apply.includes('builtin.PreflightAdmittedAsync(') && apply.includes(': preflight.PreflightAsync('));
  assert.match(importer('Redaction/PdfRedactionPreflight'), /public sealed class PdfRedactionPreflight/);
  let wrappers = 0;
  for (const name of ['PdfRedactionPreflight', 'PdfRedactionApplier', 'PdfRedactionAuditVerifier', 'PdfRedactionAuditSigner', 'PdfImageRedactionEngine', 'PdfRedactionResidueScanner']) {
    const body = importer('Redaction/' + name);
    wrappers += [...body.matchAll(/BeginOperation\(PvadNativeOperation.PdfRedaction(?:Audit)?\)/g)].length;
    assert.ok(!/new PdfCrossReferenceParser\(\)\.Parse\(|\.ParseDocumentGraph\(|new PdfIncrementalUpdateWriter\(\)\.Append\(|new PdfFullRewriteWriter\(\)\.Rewrite\(/.test(body));
  }
  assert.equal(wrappers, 9);
  assert.ok(apply.includes('PdfRedactionResidueScanner.ScanAdmitted('));
  for (const name of ['Color/PdfSrgbColorConverter', 'Color/PdfFunction']) {
    const body = importer(name);
    assert.ok(body.includes('BeginOperation(PvadNativeOperation.PdfColorResolution)'));
    assert.ok(body.includes('DemandContinuation(PvadLicenseProducts.Importer)'));
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(body));
  }
  assert.match(importer('Color/PdfFunction'), /public double\[\] Evaluate[^]*?BeginOperation\(PvadNativeOperation.PdfColorConversion\)/);
  assert.match(importer('Color/PdfSrgbColorConverter'), /public static double\[\]\? ToSrgb[^]*?BeginOperation\(PvadNativeOperation.PdfColorConversion\)/);
  const policy = source('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(!/\(PvadNativeOperation.Pdf(?:Redaction|RedactionAudit|ColorResolution), PvadNativeDependency\./.test(policy));
  const matrix = JSON.parse(source('product-metadata/native-library-products.v1.json'));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-redaction-preflight-apply-audit-and-image-sanitization'));
  const probe = source('tools/native-licensing/ImporterEngineProbe/RedactionContinuationChecks.cs');
  for (const name of ['metadata-and-attachment', 'identical-document-and-removal-counts', 'custom-provider-keeps-fresh-native-dispatch', 'tampering-remains-invalid', 'retained-function-stays-fresh'])
    assert.ok(probe.includes(name));
});

test('E-Invoice and archive continuations preserve public and custom-provider admission', () => {
  const read = name => readFileSync(resolve(root, `src/Importing/PVAD.Importer/${name}.cs`), 'utf8');
  const attach = read('EInvoicing/PdfEInvoiceAttacher');
  const archive = read('Trust/PdfA2bConverter');
  assert.ok(attach.includes('archiveConverter is PdfA2bConverter builtIn'));
  assert.ok(attach.includes(': await archiveConverter.ConvertAsync('));
  assert.ok(archive.includes('trustInspector is PdfTrustInspector builtIn'));
  assert.ok(archive.includes(': await trustInspector.InspectAsync('));
  for (const [file, purpose] of [
    ['EInvoicing/PdfEInvoiceAttacher', 'PdfEInvoice'],
    ['EInvoicing/PdfAssociatedFileExtractor', 'PdfAssociatedFiles'],
    ['Trust/PdfA2bConverter', 'PdfArchiveConversion'],
    ['Trust/PdfComplianceColorConversion', 'PdfColorConversion'],
    ['Color/PdfContentColorRewriter', 'PdfColorConversion'],
    ['Color/PdfImageColorConverter', 'PdfColorConversion'],
    ['Color/PdfIccColorTransform', 'PdfColorConversion'],
  ]) {
    const body = read(file);
    assert.ok(body.includes(`BeginOperation(PvadNativeOperation.${purpose})`), file);
    assert.ok(body.includes('DemandContinuation(PvadLicenseProducts.Importer)'), file);
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(body), file);
  }
  const functions = read('Color/PdfFunction');
  for (const name of ['PdfFunctionArray', 'PdfExponentialFunction', 'PdfStitchingFunction', 'PdfSampledFunction', 'PdfPostScriptFunction'])
    assert.ok(functions.includes(`internal sealed class ${name}`));
  assert.ok(functions.includes('_ => function.Evaluate(inputs)'));
  const policy = readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs'), 'utf8');
  assert.ok(!/\(PvadNativeOperation.Pdf(?:EInvoice|AssociatedFiles|ArchiveConversion|ColorConversion), PvadNativeDependency\./.test(policy));
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
  for (const name of ['pdf-einvoice-attachment-and-associated-file-extraction', 'pdf-archive-conversion-and-built-in-color-processing'])
    assert.ok(matrix.continuationCoverage.covered.includes(name));
  const probe = readFileSync(resolve(root, 'tools/native-licensing/ImporterEngineProbe/EInvoiceContinuationChecks.cs'), 'utf8');
  for (const name of ['xml-integrity', 'unapproved-signature', 'unapproved-conversion', 'fresh-callback', 'identical-conversion', 'custom-function:denied-before-custom-body'])
    assert.ok(probe.includes(name));
});

test('E-Invoice byte comparisons use a fixed metadata clock without weakening native admission', () => {
  const source = path => readFileSync(resolve(root, path), 'utf8');
  const attach = source('src/Importing/PVAD.Importer/EInvoicing/PdfEInvoiceAttacher.cs');
  assert.ok(attach.includes('public PdfEInvoiceAttacher(IPdfA2bConverter archiveConverter)'));
  assert.ok(attach.includes(': this(archiveConverter, TimeProvider.System)'));
  assert.ok(attach.includes('public PdfEInvoiceAttacher(IPdfA2bConverter archiveConverter, TimeProvider timeProvider)'));
  assert.ok(attach.includes('BuildAttachmentUpdates(working, payload, options, timeProvider.GetUtcNow())'));
  assert.ok(!attach.includes('DateTimeOffset.UtcNow'));
  const probe = source('tools/native-licensing/ImporterEngineProbe/EInvoiceContinuationChecks.cs');
  assert.ok(probe.includes('new PdfEInvoiceAttacher(converter, clock)'));
  assert.ok(probe.includes('return new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)'));
  assert.ok(probe.includes('result.Bytes.SequenceEqual(baseline.Bytes)'));
  assert.ok(probe.includes('clock.Reads == clockReads'));
  const tests = source('tests/PVAD.Importer.Tests/PdfEInvoiceAttachmentClockTests.cs');
  for (const text of ['Assert.Equal(first.Bytes, second.Bytes)', 'first.Bytes.SequenceEqual(third.Bytes)',
    'D:20260102030405Z', 'D:20260102030407Z', 'LegacyConstructor_UsesTheSystemUtcClock',
    'PreflightAndBlockedAttachment_DoNotReadTheMetadataClock']) assert.ok(tests.includes(text), text);
});

test('Trust post-processing keeps one internal admission without bypassing custom services or policy', () => {
  const read = name => readFileSync(resolve(root, `src/Importing/PVAD.Importer/${name}.cs`), 'utf8');
  const pipeline = read('Trust/PdfTrustPostProcessingPipeline');
  assert.ok(pipeline.includes('BeginOperation(PvadNativeOperation.PdfTrustPostProcessing)'));
  assert.ok(pipeline.includes('DemandContinuation(PvadLicenseProducts.Importer)'));
  for (const [service, type, method] of [
    ['redactionApplier', 'PdfRedactionApplier', 'Apply'], ['signingService', 'PdfPadesSigningService', 'Sign'], ['trustInspector', 'PdfTrustInspector', 'Inspect'],
  ]) {
    assert.ok(pipeline.includes(`${service} is ${type} builtIn`));
    assert.ok(pipeline.includes(`: await ${service}.${method}Async(`));
    assert.ok(pipeline.includes(`${method}AdmittedAsync(`));
  }
  assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(pipeline));
  const signing = read('Signing/PdfPadesSigningService');
  assert.match(signing, /public Task<PdfSigningResult> SignAsync[^]*?PvadLicense.Require\(PvadLicenseProducts.Importer\)/);
  assert.match(signing, /internal Task<PdfSigningResult> SignAdmittedAsync[^]*?DemandContinuation\(PvadLicenseProducts.Importer\)[^]*?return SignCoreAsync/);
  const policy = readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs'), 'utf8');
  assert.ok(!/\(PvadNativeOperation.PdfTrustPostProcessing, PvadNativeDependency\./.test(policy));
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-trust-postprocessing-redaction-signing-final-validation'));
  assert.ok(!matrix.continuationCoverage.remaining.includes('pdf-trust-postprocessing-composites'));
  const probe = readFileSync(resolve(root, 'tools/native-licensing/ImporterEngineProbe/TrustPipelineContinuationChecks.cs'), 'utf8');
  for (const text of ['ordered-stages', 'exact-final-byte-validation', 'fresh-native-dispatch', 'interface-not-bypassed', 'async-provider-and-historical-evidence', 'original-policy-and-report', 'failed-admission-not-reusable'])
    assert.ok(probe.includes(text));
});

test('regeneration retains purpose-bound rendering without widening callback, mapping or analysis rights', () => {
  const read = path => readFileSync(resolve(root, path), 'utf8');
  const bridge = read('src/Importing/PVAD.Importer/Generation/PvadPdfGeneratorBridge.cs');
  assert.ok(bridge.includes('BeginOperation(PvadNativeOperation.ImporterRegeneration)'));
  assert.ok(bridge.includes('PdfWriter.RegenerateForImporterAdmitted('));
  assert.ok(bridge.includes('if (configureDocument is not null)\n            PvadLicense.Require(PvadLicenseProducts.Pdf)'));
  assert.ok(bridge.includes('configureDocument(generated);\n            PvadLicense.Require(PvadLicenseProducts.Importer);\n            bytes = _renderer.Render(generated);'));
  assert.ok(!/new PdfStreamDecoderRegistry\(\)\.Decode\(|\.ParseDocumentGraph\(|new PdfIncrementalUpdateWriter\(\)\.Append\(/.test(bridge));
  const pipeline = read('src/Importing/PVAD.Importer/Generation/IPdfGeneratorBridge.cs');
  assert.ok(pipeline.includes('_bridge is PvadPdfGeneratorBridge builtin'));
  assert.ok(pipeline.includes('builtin.RegenerateAdmittedAsync('));
  assert.ok(pipeline.includes('else\n            await _bridge.RegenerateAsync('));
  assert.match(pipeline, /public Task SaveAsync[^]*?PvadLicense.Require\(PvadLicenseProducts.Importer\)[^]*?return SaveWithAdmissionAsync/);
  const writer = read('src/Generation/PVAD.Pdf/Serialization/PdfWriter.cs');
  assert.ok(writer.includes('internal static byte[] RegenerateForImporterAdmitted('));
  assert.match(writer, /RegenerateForImporterAdmitted\(Action<PdfDocument> populate\)[^]*?DemandDependency\(PvadNativeDependency.RegenerationPdf\)[^]*?populate\(document\);\n        PvadLicense.DemandDependency\(PvadNativeDependency.RegenerationPdf\)/);
  for (const path of ['Generation/PvadPdfGeneratorBridge', 'Analysis/XObjectResolver', 'Debugging/PdfDebugOverlay']) {
    const body = read(`src/Importing/PVAD.Importer/${path}.cs`);
    assert.ok(body.includes('BeginOperation(PvadNativeOperation.PdfAnalysis)'));
    assert.ok(body.includes('DemandContinuation(PvadLicenseProducts.Importer)'));
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(body));
  }
  const xobject = read('src/Importing/PVAD.Importer/Analysis/XObjectResolver.cs');
  assert.ok(!/\.Parse\(|\.Interpret\(|\.ComputeElementBounds\(/.test(xobject));
  const matrix = JSON.parse(read('product-metadata/native-library-products.v1.json'));
  assert.match(matrix.products.find(p => p.id === 'importer').acceptanceStatus, /^partial-/);
  assert.ok(matrix.continuationCoverage.covered.includes('pdf-regeneration-mapping-xobject-and-debug-composites'));
  assert.ok(!matrix.continuationCoverage.remaining.includes('advanced-regeneration-xobject-debug-composites'));
});

test('Spreadsheet continuations stay product-bound and preserve fresh authoring callbacks', () => {
  const read = path => readFileSync(resolve(root, path), 'utf8');
  const scope = read('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(scope.includes('PvadNativeOperation.SpreadsheetProcessing => PvadLicenseProducts.Spreadsheet'));
  assert.ok(!/\(PvadNativeOperation.SpreadsheetProcessing, PvadNativeDependency\./.test(scope));
  for (const file of ['ExcelWorkbookExporter', 'SpreadsheetCalculator', 'CsvSheetIo', 'XlsWorkbookIo', 'PvadWorkbookBuilder']) {
    const source = read(`src/Infrastructure/PVAD.Infrastructure.Spreadsheet/${file}.cs`);
    assert.ok(source.includes('BeginOperation(PvadNativeOperation.SpreadsheetProcessing)'));
    assert.ok(source.includes('DemandContinuation(PvadLicenseProducts.Spreadsheet)'));
    assert.ok(!/public[^\n]*\w+Admitted\w*\s*\(/.test(source));
  }
  const authoring = read('src/Infrastructure/PVAD.Infrastructure.Spreadsheet/PvadWorkbookBuilder.cs');
  assert.ok(authoring.includes('new XlsWorkbookIo().ExportAdmitted(_wb)'));
  assert.ok(authoring.includes('new ExcelWorkbookExporter().ExportAdmitted(_wb)'));
  assert.ok(authoring.includes('build(new PvadCellStyle(_dto.Style))'));
  assert.ok(authoring.includes('Bold(bool on = true) { PvadLicense.Require(PvadLicenseProducts.Spreadsheet)'));
  const matrix = JSON.parse(read('product-metadata/native-library-products.v1.json'));
  assert.equal(matrix.products.find(p => p.id === 'converter').enforced, true);
  assert.ok(matrix.continuationCoverage.covered.includes('spreadsheet-built-in-export-calculation-and-file-save'));
});

test('Converter exporters require fresh product admission and share no private PDF dependency', () => {
  const read = path => readFileSync(resolve(root, path), 'utf8');
  const scope = read('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(scope.includes('PvadNativeOperation.ConverterExport => PvadLicenseProducts.Converter'));
  assert.ok(!/\(PvadNativeOperation.ConverterExport, PvadNativeDependency\./.test(scope));
  for (const file of ['Html', 'Markdown', 'Csv', 'Xml', 'Svg', 'Odt', 'Image', 'Tiff']) {
    const source = read(`src/Infrastructure/PVAD.Infrastructure.Converters/${file}DocumentExporter.cs`);
    assert.ok(source.includes('BeginOperation(PvadNativeOperation.ConverterExport)'));
    assert.ok(!source.includes('PvadLicenseProducts.Pdf'));
  }
  const image = read('src/Infrastructure/PVAD.Infrastructure.Converters/ImageDocumentExporter.cs');
  assert.equal(image.match(/BeginOperation\(PvadNativeOperation.ConverterExport\)/g).length, 2);
  assert.ok(image.includes('DemandContinuation(PvadLicenseProducts.Converter)'));
  const probe = read('tools/native-licensing/ConverterEngineProbe/Program.cs');
  for (const marker of ['complete-thirteen-public-overloads', 'callback-native-call-fresh', 'original-error', 'admitted-completion'])
    assert.ok(probe.includes(marker));
  const matrix = JSON.parse(read('product-metadata/native-library-products.v1.json'));
  assert.equal(matrix.products.find(p => p.id === 'converter').enforced, true);
  assert.equal(matrix.products.find(p => p.id === 'migration.report.designer').enforced, true);
});

test('code migration CI keeps its provider isolation alongside Report Designer admission', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const workflow = load(readFileSync(resolve(root, '.github/workflows/native-library-licensing.yml'), 'utf8'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  assert.ok(commands.includes('with-test-license.mjs migration-code'));
  for (const product of ['Pdf', 'Spreadsheet']) {
    assert.ok(workflow.on.pull_request.paths.includes(`tests/PVAD.Migration.${product}*/**`));
    assert.ok(commands.includes(`tests/PVAD.Migration.${product}.Tests/PVAD.Migration.${product}.Tests.csproj`));
  }
  assert.ok(commands.includes('FullyQualifiedName~PvadNativeCodeMigrationLicenseTests'));
  const upload = workflow.jobs.licensing.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  assert.ok(upload.with.path.includes('migration-code-admission.json'));
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  assert.equal(matrix.products.find(product => product.id === 'migration.report.designer').enforced, true);
});

test('Code continuations bind provider and authority while preview rendering keeps independent rights', () => {
  const read = path => readFileSync(resolve(root, path), 'utf8');
  const scope = read('src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(scope.includes('provider != current.admission.Provider'));
  assert.ok(!/\(PvadNativeOperation\.(PdfCodeMigration|SpreadsheetCodeMigration), PvadNativeDependency\./.test(scope));
  const helper = read('src/Migrations/Common/PVAD.Migration.Roslyn/CodeMigrationAdmission.cs');
  assert.ok(helper.includes('migration is not IAdmittedCodeMigration native'));
  assert.ok(helper.includes('Demand(product, provider)'));
  const preview = read('PVAD.WebApi/Services/Converters/CodeMigrationPreview.cs');
  assert.ok(preview.includes('type.Assembly != typeof(CodeMigrationPreview).Assembly || !type.IsSealed'));
  const pdf = read('PVAD.WebApi/Services/Converters/BasePdfConverter.cs');
  assert.ok(pdf.includes('new PdfDocument()') && pdf.includes('document.ToBytes()'));
  assert.ok(!preview.includes('DemandDependency') && !preview.includes('PdfSave'));
  const spreadsheet = read('PVAD.WebApi/Services/Converters/BaseSpreadsheetConverter.cs');
  assert.ok(spreadsheet.includes('RenderHtml(converted, FrameworkName)'));
  assert.ok(!spreadsheet.includes('new PdfDocument') && !spreadsheet.includes('new PvadWorkbook'));
});
