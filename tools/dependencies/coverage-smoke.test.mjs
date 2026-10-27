import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { collectorProjects, compareReports, readReports, validateSummary, xpath } from './coverage-smoke.mjs';

const good = () => ({
  project: 'PXA.Core.Tests', tests: { total: 2, passed: 2, executed: 2 }, testNames: ['A', 'B'],
  lines: { valid: 10, covered: 3 }, branches: { valid: 2, covered: 1 }, packages: ['PXA.Core'], hitLines: ['PXA.Core.dll:Core.cs:1'],
});

test('coverage rejects zero tests, skips, empty reports and tool/test assemblies', () => {
  validateSummary(good());
  for (const mutate of [
    (x) => { x.tests.total = 0; },
    (x) => { x.tests.executed = 1; },
    (x) => { x.tests.passed = 1; },
    (x) => { x.lines.valid = 0; },
    (x) => { x.lines.covered = 0; },
    (x) => { x.lines.covered = 20; },
    (x) => { x.branches.covered = 3; },
    (x) => { x.packages = []; },
    (x) => { x.packages.push('PXA.Core.Tests'); },
    (x) => { x.packages.push('coverlet.collector'); },
    (x) => { x.hitLines = []; },
    (x) => { x.hitLines = ['PXA.Unrelated.dll:Other.cs:1']; },
  ]) {
    const invalid = good();
    mutate(invalid);
    assert.throws(() => validateSummary(invalid));
  }
});

test('comparison blocks missing projects, changed tests and filter drift', () => {
  assert.throws(() => compareReports([good()], []), /project sets/u);
  const changed = good();
  changed.tests.total = 3;
  assert.throws(() => compareReports([good()], [changed]), /discovery drift/u);
  changed.tests.total = 2;
  changed.testNames = ['A', 'C'];
  assert.throws(() => compareReports([good()], [changed]), /Different test names/u);
  changed.testNames = ['A', 'B'];
  changed.packages.push('PXA.Domain');
  assert.throws(() => compareReports([good()], [changed]), /filter drift/u);
});

test('comparison reports instrumentation drift without hiding changed denominators', () => {
  const changed = good();
  changed.lines = { valid: 11, covered: 4 };
  changed.hitLines = ['PXA.Core.dll:Core.cs:2'];
  const [result] = compareReports([good()], [changed]);
  assert.deepEqual(result.before, { valid: 10, covered: 3 });
  assert.deepEqual(result.after, { valid: 11, covered: 4 });
  assert.deepEqual(result.lost, ['PXA.Core.dll:Core.cs:1']);
  assert.deepEqual(result.gained, ['PXA.Core.dll:Core.cs:2']);
});

test('real XML reports are required and stale, empty, skipped or malformed attachments fail', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pxa-coverage-contract-'));
  try {
    const attachment = join(directory, 'attachment');
    mkdirSync(attachment);
    const trx = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult testName="Synthetic" outcome="Passed" /></Results><ResultSummary><Counters total="1" executed="1" passed="1" /></ResultSummary></TestRun>';
    const xml = '<coverage lines-valid="1" lines-covered="1" branches-valid="0" branches-covered="0"><packages><package name="PXA.Core"><classes><class><lines><line number="1" hits="1" /></lines></class></classes></package></packages></coverage>';
    writeFileSync(join(directory, 'tests.trx'), trx);
    writeFileSync(join(attachment, 'coverage.cobertura.xml'), xml);
    writeFileSync(join(attachment, 'coverage.json'), JSON.stringify({ 'PXA.Core.dll': {
      '/synthetic/Core.cs': { Example: { Run: { Lines: { 1: 1 } } } },
    } }));
    assert.equal(readReports(directory).tests.passed, 1);
    writeFileSync(join(directory, 'tests.trx'), trx.replace('outcome="Passed"', 'outcome="NotExecuted"'));
    assert.throws(() => readReports(directory), /Non-passing/u);
    writeFileSync(join(directory, 'tests.trx'), trx);
    writeFileSync(join(attachment, 'coverage.cobertura.xml'), xml.replace('name="PXA.Core"', 'name="PXA.Other"'));
    assert.throws(() => readReports(directory), /module sets differ/u);
    writeFileSync(join(attachment, 'coverage.cobertura.xml'), xml.replace('hits="1"', 'hits="0"'));
    assert.throws(() => readReports(directory), /no executed lines/u);
    writeFileSync(join(attachment, 'coverage.cobertura.xml'), '<coverage>');
    assert.throws(() => readReports(directory));
    writeFileSync(join(attachment, 'coverage.cobertura.xml'), xml);
    mkdirSync(join(directory, 'stale'));
    writeFileSync(join(directory, 'stale', 'coverage.cobertura.xml'), xml);
    assert.throws(() => readReports(directory), /exactly one fresh/u);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('every collector host resolves compatible private VSTest tools on net10', () => {
  const projects = collectorProjects();
  assert.equal(projects.length, 50);
  for (const { name, path } of projects) {
    const reference = (id, attribute) => xpath(path, `string(/Project/ItemGroup/PackageReference[@Include='${id}']/@${attribute})`);
    assert.equal(reference('coverlet.collector', 'Version'), '10.1.0', name);
    assert.equal(reference('coverlet.collector', 'PrivateAssets'), 'all', name);
    assert.equal(reference('Microsoft.NET.Test.Sdk', 'Version'), '18.5.1', name);
    assert.equal(reference('Microsoft.NET.Test.Sdk', 'PrivateAssets'), 'all', name);
    assert.equal(xpath(path, 'string(/Project/PropertyGroup/TargetFramework)'), 'net10.0', name);
    const assets = JSON.parse(readFileSync(join(dirname(path), 'obj/project.assets.json'), 'utf8'));
    for (const id of ['coverlet.collector/10.1.0', 'Microsoft.NET.Test.Sdk/18.5.1', 'Microsoft.TestPlatform.TestHost/18.5.1']) {
      assert.ok(assets.libraries[id], `${name} must resolve ${id}`);
    }
    const imports = join(dirname(path), 'obj', `${name}.csproj.nuget.g.targets`);
    assert.equal(xpath(imports, "count(//*[local-name()='Import' and contains(@Project,'coverlet.collector/10.1.0/')])"), '1', `${name} must import the new collector target`);
    const deps = JSON.parse(readFileSync(join(dirname(path), 'bin/Release/net10.0', `${name}.deps.json`), 'utf8'));
    assert.ok(deps.libraries['Microsoft.TestPlatform.TestHost/18.5.1'], `${name} must build with the new test host`);
  }
});
