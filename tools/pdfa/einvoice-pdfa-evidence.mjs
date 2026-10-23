#!/usr/bin/env node
import { readPvadEnvironment } from '../configuration/environment.mjs';
/**
 * Runs the pinned veraPDF workflow over the deterministic electronic-invoice PDF/A fixtures and
 * records the evidence.
 *
 * This does not validate anything itself. `validate-pdfa.sh` owns the pinned container and the
 * pass/fail rule; this reads the fixture manifest, checks each file is the one that was pinned,
 * hands it to that script, and normalizes what came back into one machine-readable record beside
 * the raw validator reports.
 *
 * What the evidence does and does not say:
 *   - veraPDF proves PDF/A properties. It is not a Factur-X, ZUGFeRD or XRechnung business
 *     conformance claim; that needs the official artifact sets PVAD does not ship.
 *   - When the validator cannot run, every fixture is recorded `notEvaluated`. Nothing here can
 *     produce `valid` without a report from the container.
 *   - The corpus carries a deliberately non-PDF/A control. A run that calls it compliant is not
 *     validating, so the control failing to fail is a failure.
 *
 * Usage:
 *   node tools/pdfa/einvoice-pdfa-evidence.mjs
 *
 * Regenerate the fixtures first:
 *   dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj \
 *     --filter FullyQualifiedName~EInvoicePdfaFixture
 */

import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const manifestPath = path.join(root, 'test-assets', 'e-invoicing', 'pdfa-fixtures.json');
const validator = path.join(root, 'tools', 'pdfa', 'validate-pdfa.sh');
const image = readPvadEnvironment('PVAD_VERAPDF_IMAGE') ?? 'ghcr.io/verapdf/cli:v1.31.166';

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
const fixtureDirectory = path.join(root, manifest.outputDirectory);
const reportDirectory = path.join(fixtureDirectory, 'reports');
const evidencePath = path.join(fixtureDirectory, 'evidence.json');

function sha256(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}

/** The pinned validator's own version string, or null when it cannot be reached. */
function validatorVersion() {
  const result = spawnSync(
    'docker',
    ['run', '--rm', '--platform', 'linux/amd64', image, '--version'],
    { encoding: 'utf8' },
  );
  if (result.status !== 0) return null;
  return String(result.stdout).split('\n')[0].trim() || null;
}

/**
 * veraPDF's own verdict, read from its XML report rather than from an exit code.
 *
 * `validate-pdfa.sh` exits non-zero for a non-compliant file, which is the expected outcome for the
 * control fixture, so the exit code cannot be the verdict here.
 */
function readVerdict(reportFile) {
  if (!existsSync(reportFile)) return { result: 'notEvaluated', failedRules: [] };
  const report = readFileSync(reportFile, 'utf8');
  const reportsCompliant = /isCompliant="true"/.test(report);
  const reportsNonCompliant = /isCompliant="false"/.test(report);
  // A missing, truncated, or contradictory verdict is not evidence. In particular, the negative
  // control must not pass merely because an empty report happens not to contain `true`.
  if (reportsCompliant === reportsNonCompliant) {
    return { result: 'notEvaluated', failedRules: [], reason: 'validator-report-invalid' };
  }
  const failedRules = [...report.matchAll(
    /<rule[^>]*specification="([^"]*)"[^>]*clause="([^"]*)"[^>]*testNumber="([^"]*)"[^>]*status="failed"[^>]*failedChecks="(\d+)"/g,
  )].map(match => ({
    specification: match[1],
    clause: match[2],
    testNumber: Number(match[3]),
    failedChecks: Number(match[4]),
  }));
  return {
    result: reportsCompliant ? 'compliant' : 'nonCompliant',
    failedRules,
  };
}

function main() {
  mkdirSync(reportDirectory, { recursive: true });

  const version = validatorVersion();
  const available = version !== null;
  if (!available) {
    process.stderr.write(
      `The pinned veraPDF container (${image}) is not available. Every fixture is recorded as `
      + 'notEvaluated; no result is inferred.\n',
    );
  }

  const results = [];
  let failures = 0;

  for (const fixture of manifest.fixtures) {
    const file = path.join(fixtureDirectory, fixture.path);
    const entry = {
      id: fixture.id,
      path: path.relative(root, file),
      expectedPdfaFlavour: fixture.pdfaFlavour,
      expectedResult: fixture.expectedResult,
      standard: fixture.standard,
      standardVersion: fixture.standardVersion,
      profile: fixture.profile,
      generator: fixture.generator,
      generatorVersion: fixture.generatorVersion,
      provenance: fixture.provenance,
      manifestSha256: fixture.sha256,
    };

    if (!existsSync(file)) {
      results.push({ ...entry, result: 'notEvaluated', reason: 'fixture-missing' });
      failures += 1;
      continue;
    }

    const actualSha256 = sha256(file);
    entry.observedSha256 = actualSha256;
    if (actualSha256 !== fixture.sha256) {
      // The file on disk is not the one the manifest pins, so any report about it would be
      // evidence for a different document.
      results.push({ ...entry, result: 'notEvaluated', reason: 'checksum-mismatch' });
      failures += 1;
      continue;
    }

    if (!available) {
      results.push({ ...entry, result: 'notEvaluated', reason: 'validator-unavailable' });
      failures += 1;
      continue;
    }

    const command = `tools/pdfa/validate-pdfa.sh ${fixture.pdfaFlavour} ${entry.path}`;
    spawnSync(validator, [fixture.pdfaFlavour, file], {
      cwd: root,
      encoding: 'utf8',
      env: { ...process.env, PXA_PDFA_REPORT_DIR: reportDirectory, PXA_VERAPDF_IMAGE: image },
    });

    const reportFile = path.join(
      reportDirectory,
      `${path.basename(fixture.path, '.pdf')}-${fixture.pdfaFlavour}-verapdf.xml`,
    );
    const verdict = readVerdict(reportFile);
    const matches = verdict.result === fixture.expectedResult;
    if (!matches) failures += 1;

    results.push({
      ...entry,
      command,
      rawReport: path.relative(root, reportFile),
      result: verdict.result,
      ...(verdict.reason ? { reason: verdict.reason } : {}),
      matchesExpectation: matches,
      failedRules: verdict.failedRules,
    });
  }

  const evidence = {
    schema: 'pxa.e-invoice.pdfa-evidence/1',
    description:
      'veraPDF results for the deterministic electronic-invoice PDF/A fixtures. veraPDF proves '
      + 'PDF/A properties only; this is not a Factur-X, ZUGFeRD or XRechnung conformance claim, and '
      + 'not an independent conformance approval, which additionally requires the second validator '
      + 'described in tools/pdfa/README.md.',
    generatedAt: new Date().toISOString(),
    validator: {
      name: 'veraPDF',
      image,
      version,
      available,
      wrapper: 'tools/pdfa/validate-pdfa.sh',
    },
    fixtureManifest: path.relative(root, manifestPath),
    fixtureManifestSha256: sha256(manifestPath),
    summary: {
      fixtures: results.length,
      compliant: results.filter(item => item.result === 'compliant').length,
      nonCompliant: results.filter(item => item.result === 'nonCompliant').length,
      notEvaluated: results.filter(item => item.result === 'notEvaluated').length,
      unexpected: results.filter(item => item.matchesExpectation === false).length,
    },
    results,
  };

  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`);
  process.stdout.write(`Evidence: ${path.relative(root, evidencePath)}\n`);
  process.stdout.write(`${JSON.stringify(evidence.summary)}\n`);

  if (failures > 0) {
    process.stderr.write(
      `${failures} fixture(s) did not reach their expected result. See the evidence file.\n`,
    );
    process.exitCode = 1;
  }
}

main();
