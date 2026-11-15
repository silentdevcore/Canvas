import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import { apiValidationEnvironment, auditApiResults } from './api-full-validation.mjs';

const theory = 'PVAD.Api.Tests.PvadApplicationLifetimeTests.Other_failures_are_not_converted_to_a_retention_exit';
const catalog = ['PVAD.Api.Tests.Synthetic.Passing', theory];
const types = ['System.InvalidOperationException', 'System.IO.IOException', 'System.OperationCanceledException', 'System.AggregateException'];
const names = [catalog[0], ...types.map(type => `${theory}(failure: ${type}: Synthetic)` )];
const trx = (results = names, outcome = 'Passed', count = results.length) => `<TestRun><Results>${results.map((name, index) =>
  `<UnitTestResult testName="${name}" outcome="${outcome}" executionId="id-${index}" duration="00:00:01" startTime="2026-11-15T10:00:00Z" endTime="2026-11-15T10:00:01Z"/>`).join('')}</Results><ResultSummary><Counters total="${count}" executed="${count}" passed="${count}" failed="0" notExecuted="0" aborted="0"/></ResultSummary></TestRun>`;

test('API runner strips credentials and binds all mutable paths to owned temporary storage', () => {
  const env = apiValidationEnvironment({ PATH: '/bin', HOME: '/synthetic', PVAD_RUN_POSTGRES_TESTS: '0',
    PVAD_BOOTSTRAP_ADMIN_PASSWORD: 'secret', ConnectionStrings__PvadDatabase: 'customer-database',
    Storage__RootPath: '/installed', HTTP_PROXY: 'private' }, '/tmp/synthetic-api');
  assert.equal(env.PVAD_RUN_POSTGRES_TESTS, '1');
  assert.equal(env.PATH, '/bin');
  assert.equal(env.HOME, '/synthetic');
  for (const key of ['PVAD_BOOTSTRAP_ADMIN_PASSWORD', 'HTTP_PROXY']) assert.equal(env[key], undefined);
  assert.match(env.ConnectionStrings__PvadDatabase, /^Host=127\.0\.0\.1;Port=1;Database=isolated_api_validation;/);
  assert.doesNotMatch(env.ConnectionStrings__PvadDatabase, /customer-database/);
  for (const [key, value] of Object.entries(env))
    if (key.includes('__') && !key.startsWith('ConnectionStrings__')) assert.ok(value.startsWith('/tmp/synthetic-api/'));
});

test('API result audit accepts only the documented four-case late theory expansion', () => {
  const result = auditApiResults(catalog, trx());
  assert.equal(result.status, 'passed');
  assert.equal(result.discoveryEntries, 2);
  assert.equal(result.concreteTestCases, 5);
  assert.deepEqual(result.lateEnumeration.exceptionTypes, types);
});

test('API result audit rejects missing, substituted and duplicate concrete results', () => {
  assert.throws(() => auditApiResults(catalog, trx(names.slice(1))));
  assert.throws(() => auditApiResults(catalog, trx(['Other.Test', ...names.slice(1)])));
  assert.throws(() => auditApiResults([...catalog, 'Other.Test'], trx([names[0], ...names])));
  assert.throws(() => auditApiResults([...catalog, catalog[0]], trx()));
});

test('API result audit rejects unexpected theory variants and duplicate exception types', () => {
  assert.throws(() => auditApiResults(catalog, trx([...names, `${theory}(failure: System.Exception: Synthetic)`])));
  assert.throws(() => auditApiResults(catalog, trx([...names.slice(0, 4), `${theory}(failure: System.IO.IOException: Different)`])));
});

test('API result audit rejects non-passing outcomes, inconsistent counters and absent evidence', () => {
  for (const outcome of ['Failed', 'NotExecuted', 'Skipped', 'Aborted']) assert.throws(() => auditApiResults(catalog, trx(names, outcome)));
  assert.throws(() => auditApiResults(catalog, trx(names, 'Passed', 4)));
  assert.throws(() => auditApiResults(catalog, trx().replace('aborted="0"', 'aborted="1"')));
  assert.throws(() => auditApiResults(catalog, '<TestRun/>'));
  assert.throws(() => auditApiResults(catalog, trx().replaceAll('executionId="id-1"', 'executionId="id-0"')));
  assert.throws(() => auditApiResults(catalog, trx().replace('startTime="2026-11-15T10:00:00Z"', 'startTime="invalid"')));
});

test('serial settings use a forty-minute session budget without weakening per-test hang detection', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { JSDOM } = require('jsdom');
  const dom = new JSDOM(readFileSync(new URL('./api-serial.runsettings', import.meta.url), 'utf8'), { contentType: 'text/xml' });
  try {
    const xml = dom.window.document;
    for (const [selector, value] of [['MaxCpuCount', '1'], ['TestSessionTimeout', '2400000'],
      ['ParallelizeTestCollections', 'false'], ['MaxParallelThreads', '1']]) assert.equal(xml.querySelector(selector).textContent, value);
  } finally { dom.window.close(); }
  const source = readFileSync(new URL('./api-full-validation.mjs', import.meta.url), 'utf8');
  assert.match(source, /'--blame-hang-timeout', '180s'/);
  assert.match(source, /'-t:Rebuild'/);
  assert.doesNotMatch(source, /'--filter'/);
});
