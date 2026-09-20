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
  if (visualParityPath) replacements.push(fromVisualParity(await loadJson(root, visualParityPath)));
  if (wordFidelityPath) replacements.push(fromWordFidelity(await loadJson(root, wordFidelityPath)));
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
