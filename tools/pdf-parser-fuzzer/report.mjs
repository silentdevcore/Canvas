import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const canonicalParser = id => id === 'pxa' ? 'pvad' : id;
const statuses = new Set(['accepted', 'rejected', 'crashed', 'timeout']);

export function readFuzzReport(source) {
  assert.equal(source?.SchemaVersion, 2, 'Unsupported parser report schema.');
  const report = structuredClone(source);
  for (const name of ['Seed', 'MaximumMutations', 'MaximumInputBytes',
    'ChildTimeoutMilliseconds', 'MaximumCapturedOutputCharacters'])
    assert.ok(Number.isSafeInteger(report[name]) && report[name] > 0, `Invalid ${name}.`);
  assert.ok(Array.isArray(report.Parsers) && report.Parsers.length > 0, 'Missing parsers.');
  assert.ok(report.Parsers.every(id => typeof id === 'string' && /^[a-z][a-z0-9-]*$/.test(id)), 'Invalid parser ID.');
  report.Parsers = report.Parsers.map(canonicalParser);
  assert.equal(new Set(report.Parsers).size, report.Parsers.length, 'Duplicate or conflicting parser IDs.');
  assert.ok(report.Parsers.includes('pvad'), 'Missing canonical parser.');
  assert.ok(Array.isArray(report.Cases) && report.Cases.length > 0, 'Missing mutation cases.');
  assert.equal(report.MutationCount, report.Cases.length, 'Mutation count mismatch.');
  assert.ok(report.Cases.length <= report.MaximumMutations, 'Mutation limit exceeded.');
  const ids = new Set();
  for (const entry of report.Cases) {
    assert.ok(typeof entry.Id === 'string' && entry.Id.length > 0 && !ids.has(entry.Id), 'Invalid or duplicate mutation ID.');
    ids.add(entry.Id);
    assert.match(entry.Sha256, /^[a-f0-9]{64}$/);
    assert.ok(Array.isArray(entry.Outcomes), 'Missing parser outcomes.');
    const parsers = new Set();
    for (const outcome of entry.Outcomes) {
      outcome.Parser = canonicalParser(outcome.Parser);
      assert.ok(report.Parsers.includes(outcome.Parser) && !parsers.has(outcome.Parser), 'Unknown or duplicate outcome parser.');
      parsers.add(outcome.Parser);
      assert.ok(statuses.has(outcome.Status), 'Invalid parser status.');
      assert.ok(outcome.ExitCode === null || Number.isSafeInteger(outcome.ExitCode), 'Invalid parser exit code.');
      assert.ok(Number.isSafeInteger(outcome.DurationMilliseconds) && outcome.DurationMilliseconds >= 0, 'Invalid parser duration.');
      assert.ok(outcome.ErrorType === null || typeof outcome.ErrorType === 'string', 'Invalid parser error type.');
    }
    assert.equal(parsers.size, report.Parsers.length, 'Missing parser outcomes.');
    const decisions = new Set(entry.Outcomes.filter(outcome =>
      outcome.Status === 'accepted' || outcome.Status === 'rejected').map(outcome => outcome.Status));
    assert.equal(entry.Differential, decisions.size > 1, 'Differential flag mismatch.');
  }
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    assert.equal(process.argv.length, 3, 'Specify one parser report file.');
    const file = resolve(process.argv[2]);
    assert.ok(statSync(file).size <= 4 * 1024 * 1024, 'Parser report exceeds the size limit.');
    const report = readFuzzReport(JSON.parse(readFileSync(file, 'utf8')));
    const failures = report.Cases.flatMap(entry => entry.Outcomes)
      .filter(outcome => outcome.Status === 'crashed' || outcome.Status === 'timeout').length;
    console.log(JSON.stringify({ cases: report.MutationCount, parsers: report.Parsers, failures }));
    process.exitCode = failures > 0 ? 1 : 0;
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
