#!/usr/bin/env node

import { existsSync } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const defaultRoot = path.resolve(toolDir, '../..');
const defaultManifest = 'product-metadata/document-fidelity/manifest.json';
const defaultResults = 'product-metadata/document-fidelity/reviewed-baseline.json';

const classifications = new Set(['exact', 'compatible', 'reviewRequired', 'unsupported']);
const migrationWorkflows = new Set(['import-roundtrip', 'code-migration', 'designer-migration']);
const domains = new Set(['pdf', 'word', 'spreadsheet', 'report']);
const workflows = new Set([...migrationWorkflows, 'export-roundtrip', 'render-parity', 'standards-validation']);
const provenances = new Set(['pxa-synthetic', 'third-party-generated', 'public-real-world', 'customer-anonymized']);

function requireString(value, label) {
  if (typeof value !== 'string' || value.trim() === '') throw new Error(`${label} must be a non-empty string.`);
}

function requireNumber(value, label, minimum, maximum = Number.POSITIVE_INFINITY) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < minimum || value > maximum) {
    throw new Error(`${label} must be a number between ${minimum} and ${maximum}.`);
  }
}

export function validateManifest(manifest, root = defaultRoot) {
  if (!manifest || manifest.schemaVersion !== 1) throw new Error('Manifest schemaVersion must be 1.');
  if (!manifest.policy || !Array.isArray(manifest.fixtures) || manifest.fixtures.length === 0) {
    throw new Error('Manifest requires policy and at least one fixture.');
  }

  requireNumber(manifest.policy.maximumGeometryDriftPoints, 'maximumGeometryDriftPoints', 0);
  requireNumber(manifest.policy.minimumTextPreservationRatio, 'minimumTextPreservationRatio', 0, 1);
  requireNumber(manifest.policy.maximumVisualDifferenceRatio, 'maximumVisualDifferenceRatio', 0, 1);
  requireNumber(manifest.policy.minimumAutomaticMigrationPercent, 'minimumAutomaticMigrationPercent', 0, 100);

  const ids = new Set();
  for (const fixture of manifest.fixtures) {
    requireString(fixture.id, 'fixture.id');
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(fixture.id)) throw new Error(`Invalid fixture id: ${fixture.id}`);
    if (ids.has(fixture.id)) throw new Error(`Duplicate fixture id: ${fixture.id}`);
    ids.add(fixture.id);
    requireString(fixture.domain, `${fixture.id}.domain`);
    requireString(fixture.workflow, `${fixture.id}.workflow`);
    requireString(fixture.sourceFormat, `${fixture.id}.sourceFormat`);
    requireString(fixture.source, `${fixture.id}.source`);
    requireString(fixture.provenance, `${fixture.id}.provenance`);
    requireString(fixture.redistribution, `${fixture.id}.redistribution`);
    if (!domains.has(fixture.domain)) throw new Error(`${fixture.id} has an invalid domain.`);
    if (!workflows.has(fixture.workflow)) throw new Error(`${fixture.id} has an invalid workflow.`);
    if (!provenances.has(fixture.provenance)) throw new Error(`${fixture.id} has an invalid provenance.`);
    if (!['allowed', 'restricted'].includes(fixture.redistribution)) throw new Error(`${fixture.id} has an invalid redistribution value.`);
    if (typeof fixture.realWorld !== 'boolean' || typeof fixture.officiallySupported !== 'boolean') {
      throw new Error(`${fixture.id} requires boolean realWorld and officiallySupported values.`);
    }
    if (!Array.isArray(fixture.tags)) throw new Error(`${fixture.id}.tags must be an array.`);
    if (!existsSync(path.resolve(root, fixture.source))) throw new Error(`${fixture.id} source does not exist: ${fixture.source}`);

    if (fixture.officiallySupported) {
      if (!fixture.realWorld) throw new Error(`${fixture.id} cannot be officially supported from synthetic evidence.`);
      if (fixture.redistribution !== 'allowed') throw new Error(`${fixture.id} cannot enter the public quality gate without redistribution permission.`);
      if (!migrationWorkflows.has(fixture.workflow)) throw new Error(`${fixture.id} is not a migration workflow and cannot enter the automatic-migration denominator.`);
    }
  }
  return manifest;
}

function validateMetrics(metrics, fixtureId) {
  if (!metrics || typeof metrics !== 'object') throw new Error(`${fixtureId} requires metrics.`);
  for (const key of ['sourceElements', 'matchedElements', 'missingElements', 'duplicateElements']) {
    if (key in metrics) requireNumber(metrics[key], `${fixtureId}.${key}`, 0);
  }
  if ('maximumGeometryDriftPoints' in metrics) requireNumber(metrics.maximumGeometryDriftPoints, `${fixtureId}.maximumGeometryDriftPoints`, 0);
  if ('textPreservationRatio' in metrics) requireNumber(metrics.textPreservationRatio, `${fixtureId}.textPreservationRatio`, 0, 1);
  if ('maximumVisualDifferenceRatio' in metrics) requireNumber(metrics.maximumVisualDifferenceRatio, `${fixtureId}.maximumVisualDifferenceRatio`, 0, 1);
  if ('pageCountMatches' in metrics && typeof metrics.pageCountMatches !== 'boolean') throw new Error(`${fixtureId}.pageCountMatches must be boolean.`);
  if ('sheetCountMatches' in metrics && typeof metrics.sheetCountMatches !== 'boolean') throw new Error(`${fixtureId}.sheetCountMatches must be boolean.`);
}

export function classifyResult(fixture, result, policy) {
  validateMetrics(result.metrics, fixture.id);
  const metrics = result.metrics;
  if (result.forcedClassification !== undefined) {
    if (!classifications.has(result.forcedClassification)) throw new Error(`${fixture.id} has an invalid forcedClassification.`);
    const isNegativeFinding = result.forcedClassification === 'reviewRequired' || result.forcedClassification === 'unsupported';
    if (!isNegativeFinding && fixture.workflow !== 'standards-validation') {
      throw new Error(`${fixture.id} may not force a positive fidelity classification.`);
    }
    return result.forcedClassification;
  }

  const structuralFailure = (metrics.missingElements ?? 0) > 0 ||
    (metrics.duplicateElements ?? 0) > 0 ||
    metrics.pageCountMatches === false || metrics.sheetCountMatches === false;
  const thresholdFailure = (metrics.maximumGeometryDriftPoints ?? 0) > policy.maximumGeometryDriftPoints ||
    (metrics.textPreservationRatio ?? 1) < policy.minimumTextPreservationRatio ||
    (metrics.maximumVisualDifferenceRatio ?? 0) > policy.maximumVisualDifferenceRatio;
  if (structuralFailure || thresholdFailure) return 'reviewRequired';

  const completeExactEvidence = ['maximumGeometryDriftPoints', 'textPreservationRatio', 'maximumVisualDifferenceRatio']
    .every(key => Object.hasOwn(metrics, key));
  const exact = completeExactEvidence && metrics.maximumGeometryDriftPoints === 0 &&
    metrics.textPreservationRatio === 1 && metrics.maximumVisualDifferenceRatio === 0 &&
    (metrics.missingElements ?? 0) === 0 && (metrics.duplicateElements ?? 0) === 0;
  return exact ? 'exact' : 'compatible';
}

export function fromVisualParity(report) {
  if (!report?.summary || !Array.isArray(report.elements)) throw new Error('Invalid visual-parity report.');
  const maxGeometry = Math.max(0, ...report.elements.map(item => item.geometry?.maxDrift ?? 0));
  const maxVisual = Math.max(0, ...report.elements.flatMap(item =>
    Object.values(item.comparisons ?? {}).map(comparison => comparison?.perceptualRmse ?? comparison?.rmse ?? 0)));
  return {
    fixtureId: 'designer-all-elements',
    metrics: {
      sourceElements: report.summary.elements,
      matchedElements: report.elements.length,
      missingElements: Math.max(0, report.summary.elements - report.elements.length),
      duplicateElements: 0,
      maximumGeometryDriftPoints: maxGeometry,
      maximumVisualDifferenceRatio: maxVisual,
      pageCountMatches: true
    },
    limitations: ['Perceptual image comparison tolerates deterministic browser and PDF anti-aliasing differences.']
  };
}

export function fromWordFidelity(report) {
  if (!Array.isArray(report) || report.length === 0) throw new Error('Invalid Word fidelity report.');
  const generated = report.filter(item => item.ConvertedPdfGenerated === true);
  const measuredVisual = generated.map(item => item.PixelDiffRatio).filter(Number.isFinite);
  return {
    fixtureId: 'word-fidelity-sample-pack',
    metrics: {
      sourceElements: report.length,
      matchedElements: generated.length,
      missingElements: report.length - generated.length,
      duplicateElements: 0,
      maximumGeometryDriftPoints: Math.max(0, ...generated.flatMap(item => [item.WidthDriftPt, item.HeightDriftPt]).filter(Number.isFinite)),
      maximumVisualDifferenceRatio: measuredVisual.length ? Math.max(...measuredVisual) : 0,
      pageCountMatches: true
    },
    limitations: [
      'The current pack is synthetic.',
      ...(measuredVisual.length ? [] : ['No live pixel comparison was available for this run.'])
    ]
  };
}

export function fromSpreadsheetFidelity(report) {
  if (!report || report.fixtureId !== 'spreadsheet-xlsx-roundtrip') {
    throw new Error('Invalid Spreadsheet fidelity report.');
  }
  for (const key of ['sourceFeatures', 'matchedFeatures', 'duplicateFeatures']) {
    requireNumber(report[key], `spreadsheet.${key}`, 0);
  }
  if (!Array.isArray(report.missingFeatures) || !Array.isArray(report.limitations)) {
    throw new Error('Spreadsheet fidelity report requires missingFeatures and limitations.');
  }
  return {
    fixtureId: report.fixtureId,
    metrics: {
      sourceElements: report.sourceFeatures,
      matchedElements: report.matchedFeatures,
      missingElements: report.missingFeatures.length,
      duplicateElements: report.duplicateFeatures,
      sheetCountMatches: report.sheetCountMatches === true
    },
    limitations: report.limitations
  };
}

export function fromMigrationFidelity(report) {
  if (!report || report.schemaVersion !== 1 || !Array.isArray(report.suites) || report.suites.length !== 3) {
    throw new Error('Migration fidelity report requires schemaVersion 1 and exactly three suites.');
  }

  const expectedSuites = new Map([
    ['pdf-code-migration-provider-smoke', {
      domain: 'pdf', workflow: 'code-migration',
      providers: ['active-pdf', 'apryse', 'aspose-pdf', 'devexpress-pdf', 'dspdf', 'foxit-pdf',
        'gembox-pdf', 'iron-pdf', 'itext7', 'leadtools-pdf', 'nutrient', 'pdfkitnet', 'pdftools',
        'pdftools-toolbox', 'spire-pdf', 'syncfusion-pdf']
    }],
    ['spreadsheet-code-migration-provider-smoke', {
      domain: 'spreadsheet', workflow: 'code-migration',
      providers: ['aspose-cells', 'closedxml', 'epplus', 'gembox-spreadsheet', 'npoi', 'spire-xls',
        'spreadsheetlight', 'syncfusion-xlsio']
    }],
    ['report-designer-migration-provider-smoke', {
      domain: 'report', workflow: 'designer-migration',
      providers: ['activereports-js', 'devexpress-report', 'fastreport', 'jasperreports', 'rdl', 'rpx',
        'stimulsoft', 'telerik']
    }]
  ]);
  const seen = new Set();

  return report.suites.map(suite => {
    requireString(suite.fixtureId, 'migration.fixtureId');
    if (seen.has(suite.fixtureId)) throw new Error(`Duplicate migration fidelity suite: ${suite.fixtureId}`);
    seen.add(suite.fixtureId);
    const expected = expectedSuites.get(suite.fixtureId);
    if (!expected || suite.domain !== expected.domain || suite.workflow !== expected.workflow) {
      throw new Error(`${suite.fixtureId} has an unexpected migration domain or workflow.`);
    }
    if (!Array.isArray(suite.providers) || suite.providers.length === 0 || new Set(suite.providers).size !== suite.providers.length) {
      throw new Error(`${suite.fixtureId} requires unique provider keys.`);
    }
    const providerKeys = [...suite.providers].sort();
    if (providerKeys.length !== expected.providers.length || providerKeys.some((key, index) => key !== [...expected.providers].sort()[index])) {
      throw new Error(`${suite.fixtureId} provider keys do not match the supported registry.`);
    }
    for (const key of ['sourceCases', 'executedCases', 'automaticCases', 'reviewRequiredCases', 'unsupportedCases', 'duplicateCases']) {
      requireNumber(suite[key], `${suite.fixtureId}.${key}`, 0);
    }
    if (!Number.isInteger(suite.sourceCases) || !Number.isInteger(suite.executedCases) ||
        !Number.isInteger(suite.automaticCases) || !Number.isInteger(suite.reviewRequiredCases) ||
        !Number.isInteger(suite.unsupportedCases) || !Number.isInteger(suite.duplicateCases)) {
      throw new Error(`${suite.fixtureId} case counts must be integers.`);
    }
    if (suite.executedCases !== suite.sourceCases || suite.providers.length !== suite.sourceCases) {
      throw new Error(`${suite.fixtureId} must execute one case for every declared provider.`);
    }
    if (suite.automaticCases + suite.reviewRequiredCases + suite.unsupportedCases !== suite.sourceCases) {
      throw new Error(`${suite.fixtureId} has inconsistent migration outcome counts.`);
    }
    if (!Array.isArray(suite.limitations) || suite.limitations.length === 0) {
      throw new Error(`${suite.fixtureId} requires explicit limitations.`);
    }

    const unresolved = suite.reviewRequiredCases + suite.unsupportedCases;
    return {
      fixtureId: suite.fixtureId,
      metrics: {
        sourceElements: suite.sourceCases,
        matchedElements: suite.automaticCases,
        missingElements: unresolved,
        duplicateElements: suite.duplicateCases
      },
      limitations: suite.limitations
    };
  });
}

export function fromRealWorldCorpus(report) {
  if (!report || report.schemaVersion !== 1 || !Array.isArray(report.fixtures) || report.fixtures.length !== 3) {
    throw new Error('Real-world corpus report requires schemaVersion 1 and exactly three fixtures.');
  }

  const expectedFixtures = new Map([
    ['real-world-pdf-ukgi-annual-report', { domain: 'pdf', workflow: 'import-roundtrip', structuralUnit: 'page' }],
    ['real-world-docx-suffolk-mapping-guidance', { domain: 'word', workflow: 'import-roundtrip', structuralUnit: 'document' }],
    ['real-world-xlsx-suffolk-buildings', { domain: 'spreadsheet', workflow: 'import-roundtrip', structuralUnit: 'sheet' }]
  ]);
  const seen = new Set();

  return report.fixtures.map(fixture => {
    requireString(fixture.fixtureId, 'realWorld.fixtureId');
    if (seen.has(fixture.fixtureId)) throw new Error(`Duplicate real-world corpus fixture: ${fixture.fixtureId}`);
    seen.add(fixture.fixtureId);
    const expected = expectedFixtures.get(fixture.fixtureId);
    if (!expected || fixture.domain !== expected.domain || fixture.workflow !== expected.workflow ||
        fixture.structuralUnit !== expected.structuralUnit) {
      throw new Error(`${fixture.fixtureId} has an unexpected corpus contract.`);
    }
    for (const key of ['expectedUnits', 'importedUnits', 'importedElements', 'diagnosticCount']) {
      requireNumber(fixture[key], `${fixture.fixtureId}.${key}`, 0);
      if (!Number.isInteger(fixture[key])) throw new Error(`${fixture.fixtureId}.${key} must be an integer.`);
    }
    if (fixture.importSucceeded !== true) throw new Error(`${fixture.fixtureId} did not import successfully.`);
    if (!Array.isArray(fixture.limitations) || fixture.limitations.length === 0) {
      throw new Error(`${fixture.fixtureId} requires explicit review limitations.`);
    }

    const matched = Math.min(fixture.expectedUnits, fixture.importedUnits);
    const metrics = {
      sourceElements: fixture.expectedUnits,
      matchedElements: matched,
      missingElements: Math.max(0, fixture.expectedUnits - fixture.importedUnits),
      duplicateElements: Math.max(0, fixture.importedUnits - fixture.expectedUnits)
    };
    metrics[expected.structuralUnit === 'sheet' ? 'sheetCountMatches' : 'pageCountMatches'] =
      fixture.importedUnits === fixture.expectedUnits;

    return {
      fixtureId: fixture.fixtureId,
      forcedClassification: 'reviewRequired',
      metrics,
      limitations: fixture.limitations
    };
  });
}

export function fromRealWorldReview(report) {
  if (!report || report.schemaVersion !== 1 || !Array.isArray(report.fixtures) || report.fixtures.length !== 3) {
    throw new Error('Real-world review report requires schemaVersion 1 and exactly three fixtures.');
  }
  const expected = new Map([
    ['real-world-pdf-ukgi-annual-report', 'pdf'],
    ['real-world-docx-suffolk-mapping-guidance', 'word'],
    ['real-world-xlsx-suffolk-buildings', 'spreadsheet']
  ]);
  const seen = new Set();
  return report.fixtures.map(fixture => {
    if (!expected.has(fixture.fixtureId) || seen.has(fixture.fixtureId) || fixture.domain !== expected.get(fixture.fixtureId)) {
      throw new Error(`Unknown, duplicate, or mismatched real-world review fixture: ${fixture.fixtureId ?? '<missing>'}`);
    }
    seen.add(fixture.fixtureId);
    if (!['approved', 'rejected'].includes(fixture.humanDecision) ||
        !fixture.semantics || !Array.isArray(fixture.visualSamples) || fixture.visualSamples.length === 0 ||
        !Array.isArray(fixture.findings) || fixture.findings.length === 0) {
      throw new Error(`${fixture.fixtureId} has incomplete review evidence.`);
    }
    for (const key of ['sourceTokens', 'outputTokens', 'matchedTokens']) {
      requireNumber(fixture.semantics[key], `${fixture.fixtureId}.semantics.${key}`, 0);
    }
    for (const key of ['sourcePreservationRatio', 'outputPrecisionRatio']) {
      requireNumber(fixture.semantics[key], `${fixture.fixtureId}.semantics.${key}`, 0, 1);
    }
    const visualDifference = Math.max(...fixture.visualSamples.map((sample, index) => {
      requireNumber(sample.normalizedRmse, `${fixture.fixtureId}.visualSamples[${index}].normalizedRmse`, 0, 1);
      return sample.normalizedRmse;
    }));
    const missing = Math.max(0, fixture.semantics.sourceTokens - fixture.semantics.matchedTokens);
    return {
      fixtureId: fixture.fixtureId,
      ...(fixture.humanDecision === 'rejected' ? { forcedClassification: 'reviewRequired' } : {}),
      metrics: {
        sourceElements: fixture.semantics.sourceTokens,
        outputElements: fixture.semantics.outputTokens,
        matchedElements: fixture.semantics.matchedTokens,
        missingElements: missing,
        duplicateElements: 0,
        textPreservationRatio: fixture.semantics.sourcePreservationRatio,
        maximumVisualDifferenceRatio: visualDifference,
        pageCountMatches: fixture.pageCountMatches === true
      },
      limitations: fixture.findings
    };
  });
}

export function mergeEvidence(evidence, replacements) {
  const replacementMap = new Map(replacements.map(item => [item.fixtureId, item]));
  const retained = evidence.results.filter(item => !replacementMap.has(item.fixtureId));
  return { ...evidence, results: [...retained, ...replacements] };
}

function percentage(numerator, denominator) {
  return denominator === 0 ? null : Number(((numerator / denominator) * 100).toFixed(2));
}

export function evaluate(manifest, evidence, root = defaultRoot) {
  validateManifest(manifest, root);
  if (!evidence || evidence.schemaVersion !== 1 || !Array.isArray(evidence.results)) {
    throw new Error('Evidence schemaVersion must be 1 and results must be an array.');
  }

  const fixtureById = new Map(manifest.fixtures.map(fixture => [fixture.id, fixture]));
  const seen = new Set();
  const results = evidence.results.map(result => {
    requireString(result.fixtureId, 'result.fixtureId');
    if (seen.has(result.fixtureId)) throw new Error(`Duplicate evidence result: ${result.fixtureId}`);
    seen.add(result.fixtureId);
    const fixture = fixtureById.get(result.fixtureId);
    if (!fixture) throw new Error(`Evidence references unknown fixture: ${result.fixtureId}`);
    if (!Array.isArray(result.limitations)) throw new Error(`${result.fixtureId}.limitations must be an array.`);
    const classification = classifyResult(fixture, result, manifest.policy);
    if ((classification === 'reviewRequired' || classification === 'unsupported') && result.limitations.length === 0) {
      throw new Error(`${result.fixtureId} requires a limitation for ${classification}.`);
    }
    if (fixture.officiallySupported) {
      const requiredCounts = ['sourceElements', 'matchedElements', 'missingElements', 'duplicateElements'];
      if (!requiredCounts.every(key => Object.hasOwn(result.metrics, key))) {
        throw new Error(`${result.fixtureId} requires complete element counts for the official quality gate.`);
      }
      if (result.metrics.matchedElements + result.metrics.missingElements !== result.metrics.sourceElements) {
        throw new Error(`${result.fixtureId} has inconsistent source, matched, and missing element counts.`);
      }
    }
    return { ...result, classification, fixture };
  });

  const missingEvidence = manifest.fixtures.filter(fixture => !seen.has(fixture.id)).map(fixture => fixture.id);
  const eligible = results.filter(result => result.fixture.officiallySupported);
  const automatic = eligible.filter(result => result.classification === 'exact' || result.classification === 'compatible');
  const automaticMigrationPercent = percentage(automatic.length, eligible.length);
  const targetMet = automaticMigrationPercent === null ? null : automaticMigrationPercent >= manifest.policy.minimumAutomaticMigrationPercent;

  const classificationsSummary = Object.fromEntries([...classifications].map(value => [value, results.filter(result => result.classification === value).length]));
  const domainSummaries = [...domains].sort().map(domain => {
    const domainFixtures = manifest.fixtures.filter(fixture => fixture.domain === domain);
    const domainResults = results.filter(result => result.fixture.domain === domain);
    const domainEligible = domainResults.filter(result => result.fixture.officiallySupported);
    const domainAutomatic = domainEligible.filter(result => ['exact', 'compatible'].includes(result.classification));
    return {
      domain,
      fixtures: domainFixtures.length,
      measured: domainResults.length,
      realWorld: domainFixtures.filter(fixture => fixture.realWorld).length,
      officiallySupported: domainEligible.length,
      automaticMigrationPercent: percentage(domainAutomatic.length, domainEligible.length)
    };
  });

  return {
    schemaVersion: 1,
    policy: manifest.policy,
    summary: {
      fixtures: manifest.fixtures.length,
      measured: results.length,
      missingEvidence: missingEvidence.length,
      realWorldFixtures: manifest.fixtures.filter(fixture => fixture.realWorld).length,
      officiallySupportedFixtures: eligible.length,
      automaticFixtures: automatic.length,
      automaticMigrationPercent: automaticMigrationPercent ?? 'notMeasured',
      targetMet: targetMet ?? 'notMeasured',
      classifications: classificationsSummary
    },
    domains: domainSummaries,
    missingEvidence,
    results: results.map(({ fixture, ...result }) => ({
      ...result,
      domain: fixture.domain,
      workflow: fixture.workflow,
      provider: fixture.provider ?? null,
      realWorld: fixture.realWorld,
      officiallySupported: fixture.officiallySupported
    }))
  };
}

export function toMarkdown(report) {
  const rows = report.domains.map(item => `| ${item.domain} | ${item.fixtures} | ${item.measured} | ${item.realWorld} | ${item.officiallySupported} | ${item.automaticMigrationPercent ?? 'notMeasured'} |`).join('\n');
  const findings = report.results.map(item => `- \`${item.fixtureId}\`: **${item.classification}**${item.limitations.length ? ` - ${item.limitations.join(' ')}` : ''}`).join('\n');
  return `# PXA Document Fidelity Report\n\n` +
    `Official automatic migration: **${report.summary.automaticMigrationPercent}** (target ${report.policy.minimumAutomaticMigrationPercent}%).\n\n` +
    `This percentage counts only reviewed, real-world fixtures explicitly marked as officially supported. \`notMeasured\` means that no fixture is eligible; it never means 100%.\n\n` +
    `| Domain | Fixtures | Measured | Real-world | Official | Automatic % |\n| --- | ---: | ---: | ---: | ---: | ---: |\n${rows}\n\n` +
    `## Findings\n\n${findings || '- No evidence results.'}\n`;
}

async function loadJson(root, file) {
  return JSON.parse(await readFile(path.resolve(root, file), 'utf8'));
}

function option(args, name, fallback) {
  const index = args.indexOf(name);
  return index < 0 ? fallback : args[index + 1];
}

async function main() {
  const args = process.argv.slice(2);
  const command = args[0] ?? 'evaluate';
  const root = path.resolve(option(args, '--root', defaultRoot));
  const manifestPath = option(args, '--manifest', defaultManifest);
  const manifest = await loadJson(root, manifestPath);

  if (command === 'validate') {
    validateManifest(manifest, root);
    process.stdout.write(`Document fidelity manifest valid: ${manifest.fixtures.length} fixtures.\n`);
    return;
  }
  if (command !== 'evaluate') throw new Error(`Unknown command: ${command}`);

  const evidencePath = option(args, '--results', defaultResults);
  let evidence = await loadJson(root, evidencePath);
  const replacements = [];
  const visualParityPath = option(args, '--visual-parity', null);
  const wordFidelityPath = option(args, '--word-fidelity', null);
  const spreadsheetFidelityPath = option(args, '--spreadsheet-fidelity', null);
  const migrationFidelityPath = option(args, '--migration-fidelity', null);
  const realWorldFidelityPath = option(args, '--real-world-fidelity', null);
  const realWorldReviewPath = option(args, '--real-world-review', null);
  if (visualParityPath) replacements.push(fromVisualParity(await loadJson(root, visualParityPath)));
  if (wordFidelityPath) replacements.push(fromWordFidelity(await loadJson(root, wordFidelityPath)));
  if (spreadsheetFidelityPath) replacements.push(fromSpreadsheetFidelity(await loadJson(root, spreadsheetFidelityPath)));
  if (migrationFidelityPath) replacements.push(...fromMigrationFidelity(await loadJson(root, migrationFidelityPath)));
  if (realWorldFidelityPath) replacements.push(...fromRealWorldCorpus(await loadJson(root, realWorldFidelityPath)));
  if (realWorldReviewPath) replacements.push(...fromRealWorldReview(await loadJson(root, realWorldReviewPath)));
  if (replacements.length) evidence = mergeEvidence(evidence, replacements);
  const report = evaluate(manifest, evidence, root);
  const output = option(args, '--output', null);
  if (output) {
    const outputDir = path.resolve(root, output);
    await mkdir(outputDir, { recursive: true });
    await writeFile(path.join(outputDir, 'document-fidelity-report.json'), `${JSON.stringify(report, null, 2)}\n`);
    await writeFile(path.join(outputDir, 'document-fidelity-report.md'), toMarkdown(report));
  }
  process.stdout.write(`${toMarkdown(report)}\n`);

  if (args.includes('--enforce')) {
    if (report.summary.automaticMigrationPercent === 'notMeasured') {
      throw new Error('The official automatic-migration gate cannot pass without eligible fixtures.');
    }
    if (report.summary.targetMet !== true) throw new Error(`Automatic migration is below ${report.policy.minimumAutomaticMigrationPercent}%.`);
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
}
