import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { closeNativeTestSession, createNativeTestSession, nativeTestEnvironment,
  prepareGitHubSession, sessionEnvironment, withNativeTestSession } from './test-session.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const sourceTrust = () => readFileSync(resolve(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'), 'utf8');

test('a signed session owns one separate build and removes licenses and binaries without altering source trust', () => {
  const before = sourceTrust();
  const session = createNativeTestSession();
  const build = session.environment.ArtifactsPath;
  const output = dirname(build);
  assert.deepEqual(nativeTestEnvironment(session.environment), session.environment);
  assert.equal(session.environment.NativeLicensing__LicensePath, session.environment.PVAD_NATIVE_LICENSE_PATH);
  const envelope = JSON.parse(readFileSync(session.environment.PVAD_NATIVE_LICENSE_PATH));
  assert.deepEqual(JSON.parse(Buffer.from(envelope.payload, 'base64')).grants,
    ['pdf', 'word', 'spreadsheet', 'importer'].map(product => ({ product, providers: [] })));
  assert.equal(session.environment.UseArtifactsOutput, 'true');
  mkdirSync(build, { recursive: true });
  writeFileSync(join(build, 'synthetic.dll'), 'synthetic');
  closeNativeTestSession(session.descriptor);
  assert.ok(!existsSync(build) && !existsSync(dirname(session.descriptor)));
  assert.equal(sourceTrust(), before);
  assert.deepEqual(JSON.parse(readFileSync(join(output, 'cleanup.json'))), {
    syntheticOnly: true, temporaryFilesRemoved: true, sourceTrustUnchanged: true, artifactsUnchanged: true,
  });
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

test('native CI covers Word/Spreadsheet admission, compatibility, and API failure propagation', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const workflow = load(readFileSync(resolve(root, '.github/workflows/native-library-licensing.yml'), 'utf8'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  const upload = workflow.jobs.licensing.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  for (const product of ['Word', 'Spreadsheet']) {
    assert.ok(workflow.on.pull_request.paths.includes(`src/Infrastructure/PVAD.Infrastructure.${product}/**`));
    assert.ok(commands.includes(`with-test-license.mjs ${product.toLowerCase()}`));
    assert.ok(commands.includes(`tests/PVAD.Infrastructure.${product}.Tests/PVAD.Infrastructure.${product}.Tests.csproj`));
    assert.ok(commands.includes(`FullyQualifiedName~PvadNative${product}LicenseTests`));
    assert.ok(upload.with.path.includes(`${product.toLowerCase()}-admission.json`));
  }
  assert.ok(!upload.with.path.includes('isolated-build'));
});

test('Importer entry coverage stays partial and the native workflow exercises its boundary', () => {
  const matrix = JSON.parse(readFileSync(resolve(root, 'product-metadata/native-library-products.v1.json')));
  const importer = matrix.products.find(product => product.id === 'importer');
  assert.equal(importer.enforced, false);
  assert.equal(importer.coverageStatus, 'partial-entry-points');
  assert.ok(importer.coveredEntryPoints.length > 0 && importer.remainingEntryPoints.length > 0);
  for (const name of ['PdfTokenizer.ReadToken/Seek', 'PdfStreamDecoderRegistry.Decode', 'PdfObjectParser', 'SemanticLayoutEngine', 'PdfSrgbColorConverter', 'PdfFunction.TryCreate/Evaluate', 'PdfFontResource.Decode/GetGlyphCodes', 'PdfEditingSession', 'PdfEditableDocument', 'PdfContentStreamRewriter'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing direct engine inventory: ${name}`);
  for (const name of ['PdfTrustInspector', 'PdfOcspEvidence', 'PdfDocumentDecryptor', 'PdfStandardSecurityHandler', 'PdfPadesSigningService', 'PdfPkcs12SigningKey', 'PdfRfc3161TimestampAuthority', 'PdfRedactionAuditVerifier', 'PdfEInvoiceAttacher', 'PdfAssociatedFileExtractor'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing Trust entry inventory: ${name}`);
  for (const name of ['PvadPdfGeneratorBridge', 'PdfGeneratorBridgePipeline.SaveAsync', 'PdfDebugOverlayBuilder.Build'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing regeneration entry inventory: ${name}`);
  for (const name of ['ImageAnalysisFileImporter', 'ImageToPdfConverter.ConvertAsync', 'DefaultOcrWorkerProcessRunner.RunAsync'])
    assert.ok(importer.coveredEntryPoints.some(entry => entry.includes(name)), `Missing image/OCR entry inventory: ${name}`);
  for (const name of ['dependency rights', 'admitted-operation lifetime'])
    assert.ok(importer.remainingEntryPoints.some(entry => entry.includes(name)), `Missing remaining Importer boundary: ${name}`);
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { load } = require('js-yaml');
  const workflow = load(readFileSync(resolve(root, '.github/workflows/native-library-licensing.yml'), 'utf8'));
  assert.ok(workflow.on.pull_request.paths.includes('src/Importing/**'));
  const commands = workflow.jobs.licensing.steps.map(step => step.run ?? '').join('\n');
  assert.ok(commands.includes('with-test-license.mjs importer'));
  assert.ok(commands.includes('with-test-license.mjs image'));
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
  assert.equal(matrix.continuationCoverage.status, 'partial-image-ocr-and-pdf-import-save-editing');
  assert.equal(matrix.continuationCoverage.publicAdmission, 'fresh-current-license');
  assert.equal(matrix.continuationCoverage.authorityBinding, 'originating-process-verifier');
  assert.equal(matrix.continuationCoverage.workerAdmission, 'fresh-at-dispatch-and-worker-start');
  assert.ok(matrix.continuationCoverage.covered.includes('designer-pdf-file-import-dto-analysis-mapping'));
  assert.ok(matrix.continuationCoverage.remaining.includes('composite-pdf-trust-advanced-regeneration'));
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
  assert.equal(matrix.products.find(p => p.id === 'importer').enforced, false);
});
