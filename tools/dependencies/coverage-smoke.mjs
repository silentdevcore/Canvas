import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const settings = join(root, 'tools/dependencies/coverage.runsettings');
const filters = {
  'PXA.Api.Tests': 'FullyQualifiedName~PXA.Api.Tests.VersionControllerTests.',
  'PXA.Application.Tests': 'FullyQualifiedName~GenerateDocumentUseCaseTests|FullyQualifiedName~ApplyWatermarkUseCaseTests|FullyQualifiedName~CollectDiagnosticsUseCaseTests',
  'PXA.Core.Tests': null,
  'PXA.Domain.Tests': null,
  'PXA.Export.Tests': 'FullyQualifiedName~ExportDocumentUseCaseTests',
  'PXA.FileImporter.Image.Tests': 'FullyQualifiedName~ImageFileImporterTests',
  'PXA.FileImporter.ImageAnalysis.Tests': 'FullyQualifiedName~ColorAnalyzerTests',
  'PXA.FileImporter.ImageOcr.Tests': 'FullyQualifiedName~ConvertAsync_BuildsEditableTextAndBackgroundImage',
  'PXA.FileImporter.Tests': 'FullyQualifiedName~FileImporterFacadeTests.DocxImporter_ImportsTextDesign|FullyQualifiedName~FileImporterFacadeTests.OdtImporter_ImportsTextDesign',
  'PXA.Generator.Tests': 'FullyQualifiedName~PdfFacadeTests',
  'PXA.Importer.Tests': 'FullyQualifiedName~PdfImportFacadeTests',
  'PXA.Infrastructure.Converters.Tests': 'FullyQualifiedName~ConverterFacadeTests.TextExporters|FullyQualifiedName~ConverterFacadeTests.OdtExporter',
  'PXA.Infrastructure.Pdf.Tests': 'FullyQualifiedName~PdfInfrastructureFacadeTests',
  'PXA.Infrastructure.Persistence.Tests': 'FullyQualifiedName~PxaDbContextModelTests',
  'PXA.Infrastructure.Spreadsheet.Tests': 'FullyQualifiedName~ClosedXmlCompatibilityTests',
  'PXA.Infrastructure.Word.Tests': 'FullyQualifiedName~WordInfrastructureFacadeTests',
};

export function xpath(file, expression) {
  return execFileSync('xmllint', ['--nonet', '--xpath', expression, file], {
    encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 4 * 1024 * 1024,
  }).trim();
}

export function collectorProjects() {
  return readdirSync(join(root, 'tests')).sort().map((name) => ({
    name, path: join(root, 'tests', name, `${name}.csproj`),
  })).filter(({ path }) => existsSync(path) && xpath(path, 'count(/Project/ItemGroup/PackageReference[@Include="coverlet.collector"])') === '1');
}

export function validateSummary(summary) {
  const { tests, lines, branches, packages, hitLines } = summary;
  assert.ok(Number.isSafeInteger(tests.total) && tests.total > 0, 'No tests discovered');
  assert.equal(tests.passed, tests.total, 'Not every discovered test passed');
  assert.equal(tests.executed, tests.total, 'Tests skipped or not executed');
  assert.ok(Number.isSafeInteger(lines.valid) && lines.valid > 0, 'Empty coverage denominator');
  assert.ok(Number.isSafeInteger(lines.covered) && lines.covered > 0 && lines.covered <= lines.valid, 'Missing/invalid production hits');
  assert.ok(Number.isSafeInteger(branches.valid) && branches.valid >= 0 && Number.isSafeInteger(branches.covered) && branches.covered >= 0 && branches.covered <= branches.valid, 'Invalid branch counters');
  assert.ok(packages.length > 0 && packages.every((name) => /^PXA\.[A-Za-z0-9.]+$/u.test(name) && !/\.Tests(?:\.|$)/u.test(name)), 'Test/tool assembly leaked into coverage');
  assert.ok(hitLines.length > 0, 'JSON report has no production hits');
  if (summary.project) {
    const aliases = {
      'PXA.Api.Tests': 'PXA.WebApi',
      'PXA.Export.Tests': 'PXA.Application',
      'PXA.FileImporter.Image.Tests': 'PXA.FileImporter',
    };
    const module = aliases[summary.project] ?? summary.project.replace(/\.Tests$/u, '');
    assert.ok(hitLines.some((line) => line.startsWith(`${module}.dll:`)), `No hits in expected production module ${module}`);
  }
}

export function readReports(directory) {
  const reportDirs = readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && existsSync(join(directory, entry.name, 'coverage.cobertura.xml')));
  assert.equal(reportDirs.length, 1, 'Expected exactly one fresh coverage attachment');
  const folder = join(directory, reportDirs[0].name);
  const xml = join(folder, 'coverage.cobertura.xml');
  const trx = join(directory, 'tests.trx');
  const counters = "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']";
  const tests = Object.fromEntries(['total', 'executed', 'passed'].map((key) => [key, Number(xpath(trx, `string(${counters}/@${key})`))]));
  assert.equal(Number(xpath(trx, "count(//*[local-name()='UnitTestResult'])")), tests.total, 'TRX counters do not match actual results');
  assert.equal(Number(xpath(trx, "count(//*[local-name()='UnitTestResult' and not(@outcome='Passed')])")), 0, 'Non-passing test result');
  const testNames = Array.from({ length: tests.total }, (_, i) => xpath(trx, `string((//*[local-name()='UnitTestResult'])[${i + 1}]/@testName)`)).sort();
  assert.ok(testNames.every(Boolean), 'Test result has no name');
  const coverage = JSON.parse(readFileSync(join(folder, 'coverage.json'), 'utf8'));
  const packages = Object.keys(coverage).map((module) => basename(module, '.dll')).sort();
  assert.ok(packages.length > 0 && packages.every((name) => /^PXA\.[A-Za-z0-9.]+$/u.test(name)), 'Unexpected JSON modules');
  assert.equal(new Set(packages).size, packages.length, 'Duplicate JSON assembly names');
  // Parse large Cobertura documents once, checking the JSON/XML module sets agree.
  const names = packages.map((name) => `@name='${name}'`).join(' or ');
  const fields = [
    '/coverage/@lines-valid', '/coverage/@lines-covered',
    'count(/coverage/packages/package/classes/class/lines/line[@hits>0])',
    'count(/coverage/packages/package)', `count(/coverage/packages/package[${names}])`,
    '/coverage/@branches-valid', '/coverage/@branches-covered',
  ];
  const metrics = xpath(xml, `concat(${fields.join(",'|',")})`).split('|');
  assert.ok(metrics.every((value) => /^\d+$/u.test(value)), 'Missing/invalid coverage counters');
  const [valid, covered, executedLines, count, matchingCount, branchValid, branchCovered] = metrics.map(Number);
  assert.ok(executedLines > 0, 'Cobertura contains no executed lines');
  assert.equal(count, packages.length, 'JSON/XML module count differs');
  assert.equal(matchingCount, count, 'JSON/XML module sets differ');
  const lines = { valid, covered };
  const branches = { valid: branchValid, covered: branchCovered };
  const hitLines = new Set();
  for (const [module, files] of Object.entries(coverage)) {
    for (const [file, classes] of Object.entries(files)) {
      assert.ok(!file.replaceAll('\\', '/').includes('/obj/'), 'Generated obj source leaked into coverage');
      for (const methods of Object.values(classes)) {
        for (const method of Object.values(methods)) {
          for (const [line, hits] of Object.entries(method.Lines)) {
            if (hits > 0) hitLines.add(`${basename(module)}:${relative(root, file)}:${line}`);
          }
        }
      }
    }
  }
  const result = { tests, testNames, lines, branches, packages, hitLines: [...hitLines].sort() };
  validateSummary(result);
  return result;
}

export function compareReports(before, after) {
  assert.deepEqual(after.map((row) => row.project), before.map((row) => row.project), 'Different project sets');
  return before.map((old, i) => {
    const current = after[i];
    assert.deepEqual(current.tests, old.tests, `Test discovery drift: ${old.project}`);
    assert.deepEqual(current.testNames, old.testNames, `Different test names: ${old.project}`);
    assert.deepEqual(current.packages, old.packages, `Assembly filter drift: ${old.project}`);
    validateSummary(old);
    validateSummary(current);
    const oldHits = new Set(old.hitLines);
    const newHits = new Set(current.hitLines);
    return {
      project: old.project, tests: current.tests.total,
      before: old.lines, after: current.lines,
      branchesBefore: old.branches, branchesAfter: current.branches,
      lost: old.hitLines.filter((line) => !newHits.has(line)),
      gained: current.hitLines.filter((line) => !oldHits.has(line)),
    };
  });
}

function run(output) {
  assert.ok(!existsSync(output), 'Use a new output directory; never overwrite a baseline');
  const projects = collectorProjects();
  assert.equal(projects.length, 50, 'Collector project catalog changed; review smoke coverage');
  mkdirSync(output, { recursive: true });
  const results = [];
  for (const project of projects) {
    assert.ok(Object.hasOwn(filters, project.name) || project.name.startsWith('PXA.Migration.'), `Missing selection for ${project.name}`);
    const directory = join(output, project.name);
    const args = ['test', project.path, '--configuration', 'Release', '--no-build', '--no-restore',
      '--collect', 'XPlat Code Coverage', '--settings', settings,
      '--results-directory', directory, '--logger', 'trx;LogFileName=tests.trx'];
    if (filters[project.name]) args.push('--filter', filters[project.name]);
    execFileSync('dotnet', args, { cwd: root, stdio: 'inherit', timeout: 180_000 });
    const result = { project: project.name, ...readReports(directory) };
    validateSummary(result);
    results.push(result);
    writeFileSync(join(output, 'summary.json'), `${JSON.stringify(results, null, 2)}\n`);
    console.log(`${project.name}: ${result.tests.passed} passed, ${result.lines.covered}/${result.lines.valid} covered lines`);
  }
}

function audit(directory) {
  const projects = collectorProjects();
  assert.equal(projects.length, 50, 'Audit requires the complete collector catalog');
  const rows = projects.map(({ name }) => {
    const row = { project: name, ...readReports(join(directory, name)) };
    validateSummary(row);
    return row;
  });
  const file = join(directory, 'audited-summary.json');
  const source = `${JSON.stringify(rows, null, 2)}\n`;
  if (existsSync(file)) assert.equal(readFileSync(file, 'utf8'), source, 'Existing audit differs; retain it and investigate');
  else writeFileSync(file, source);
  console.log(`Audited ${rows.length} retained coverage attachments without changing raw evidence.`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv[2] === '--compare') {
    assert.equal(process.argv.length, 5, 'Usage: coverage-smoke.mjs --compare before/summary.json after/summary.json');
    const rows = compareReports(...process.argv.slice(3).map((file) => JSON.parse(readFileSync(file, 'utf8'))));
    assert.equal(rows.length, 50, 'Comparison requires two complete 50-host matrices');
    console.log(JSON.stringify(rows, null, 2));
  } else if (process.argv[2] === '--audit') {
    assert.equal(process.argv.length, 4, 'Usage: coverage-smoke.mjs --audit existing-output-directory');
    audit(resolve(process.argv[3]));
  } else {
    assert.equal(process.argv.length, 3, 'Usage: coverage-smoke.mjs new-output-directory');
    run(resolve(process.argv[2]));
  }
}
