#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const defaultRaw = 'artifacts/document-fidelity/real-world/raw';
const defaultOutput = 'artifacts/document-fidelity/real-world/review';
const defaultDecisions = 'product-metadata/document-fidelity/real-world-review-decisions.json';
const dpi = 72;

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: root,
    env: process.env,
    encoding: 'utf8',
    stdio: 'pipe',
    ...options
  });
  if (result.status !== 0 && !options.allowedStatuses?.includes(result.status)) {
    throw new Error(`${command} ${args.join(' ')} failed\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  }
  return result;
}

function requireTool(name) {
  const result = spawnSync('sh', ['-c', `command -v ${name}`], { encoding: 'utf8' });
  if (result.status !== 0) throw new Error(`Real-world fidelity review requires '${name}' on PATH.`);
}

function hasTool(name) {
  return spawnSync('sh', ['-c', `command -v ${name}`], { encoding: 'utf8' }).status === 0;
}

function imageRun(operation, args, options = {}) {
  if (!hasTool('magick')) return run(operation, args, options);
  return operation === 'convert'
    ? run('magick', args, options)
    : run('magick', [operation, ...args], options);
}

function option(args, name, fallback) {
  const index = args.indexOf(name);
  return index < 0 ? fallback : args[index + 1];
}

export function selectReviewPages(sourcePages, outputPages) {
  const common = Math.min(sourcePages, outputPages);
  if (common < 1) return [];
  return [...new Set([1, Math.ceil(common / 2), common])].sort((left, right) => left - right);
}

export function compareTokens(sourceText, outputText) {
  const tokenize = value => value.normalize('NFKC').toLocaleLowerCase('en')
    .match(/[\p{L}\p{N}]+/gu) ?? [];
  const source = tokenize(sourceText);
  const output = tokenize(outputText);
  const outputCounts = new Map();
  for (const token of output) outputCounts.set(token, (outputCounts.get(token) ?? 0) + 1);
  let matched = 0;
  for (const token of source) {
    const count = outputCounts.get(token) ?? 0;
    if (count > 0) {
      matched += 1;
      outputCounts.set(token, count - 1);
    }
  }
  return {
    sourceTokens: source.length,
    outputTokens: output.length,
    matchedTokens: matched,
    sourcePreservationRatio: source.length === 0 ? 1 : matched / source.length,
    outputPrecisionRatio: output.length === 0 ? (source.length === 0 ? 1 : 0) : matched / output.length
  };
}

export function validateReviewDecisions(value) {
  if (!value || value.schemaVersion !== 1 || !Array.isArray(value.fixtures) || value.fixtures.length !== 3) {
    throw new Error('Real-world review decisions require schemaVersion 1 and exactly three fixtures.');
  }
  const expected = new Set([
    'real-world-pdf-ukgi-annual-report',
    'real-world-docx-suffolk-mapping-guidance',
    'real-world-xlsx-suffolk-buildings'
  ]);
  const seen = new Set();
  for (const fixture of value.fixtures) {
    if (!expected.has(fixture.fixtureId) || seen.has(fixture.fixtureId)) {
      throw new Error(`Unknown or duplicate review decision: ${fixture.fixtureId ?? '<missing>'}`);
    }
    seen.add(fixture.fixtureId);
    if (!['approved', 'rejected'].includes(fixture.decision) ||
        typeof fixture.reviewedAt !== 'string' || fixture.reviewedAt.length === 0 ||
        typeof fixture.reviewer !== 'string' || fixture.reviewer.length === 0 ||
        !Array.isArray(fixture.findings) || fixture.findings.length === 0) {
      throw new Error(`${fixture.fixtureId} has an incomplete review decision.`);
    }
  }
  return value;
}

function pageCount(pdf) {
  const output = run('pdfinfo', [pdf]).stdout;
  const match = output.match(/^Pages:\s+(\d+)$/m);
  if (!match) throw new Error(`Unable to read page count from '${pdf}'.`);
  return Number(match[1]);
}

function extractText(pdf, target) {
  run('pdftotext', ['-enc', 'UTF-8', pdf, target]);
}

async function convertOffice(source, outputDirectory, profileName) {
  await mkdir(outputDirectory, { recursive: true });
  const profile = path.join(outputDirectory, `.lo-profile-${profileName}`);
  const profileUrl = `file://${profile}`;
  run('soffice', [
    `-env:UserInstallation=${profileUrl}`,
    '--headless', '--convert-to', 'pdf', '--outdir', outputDirectory, source
  ]);
  const expected = path.join(outputDirectory, `${path.basename(source, path.extname(source))}.pdf`);
  return expected;
}

function parseDimensions(file) {
  const output = imageRun('identify', ['-format', '%w %h', file]).stdout.trim();
  const [width, height] = output.split(/\s+/).map(Number);
  if (!Number.isInteger(width) || !Number.isInteger(height)) throw new Error(`Invalid image dimensions: ${output}`);
  return { width, height };
}

function rasterPage(pdf, page, targetPrefix) {
  run('pdftoppm', ['-f', String(page), '-l', String(page), '-singlefile', '-r', String(dpi), '-png', pdf, targetPrefix]);
  return `${targetPrefix}.png`;
}

function compareImages(source, output, diff) {
  const sourceSize = parseDimensions(source);
  const outputSize = parseDimensions(output);
  if (sourceSize.width !== outputSize.width || sourceSize.height !== outputSize.height) {
    imageRun('convert', [source, output, '+append', diff.replace('-diff.png', '-side-by-side.png')]);
    return { normalizedRmse: 1, geometryMatches: false, sourceSize, outputSize };
  }
  const result = imageRun('compare', ['-metric', 'RMSE', source, output, diff], { allowedStatuses: [1] });
  const metric = (result.stderr || result.stdout).match(/\(([0-9.]+(?:e[+-]?\d+)?)\)/i);
  if (!metric) throw new Error(`Unable to read RMSE for '${source}'.`);
  imageRun('convert', [source, output, '+append', diff.replace('-diff.png', '-side-by-side.png')]);
  return { normalizedRmse: Number(metric[1]), geometryMatches: true, sourceSize, outputSize };
}

async function reviewFixture(fixture, decision, outputRoot) {
  const directory = path.join(outputRoot, fixture.id);
  await mkdir(directory, { recursive: true });
  const sourceText = path.join(directory, 'source.txt');
  const outputText = path.join(directory, 'output.txt');
  extractText(fixture.sourcePdf, sourceText);
  extractText(fixture.outputPdf, outputText);
  const sourcePages = pageCount(fixture.sourcePdf);
  const outputPages = pageCount(fixture.outputPdf);
  const visualSamples = [];
  for (const page of selectReviewPages(sourcePages, outputPages)) {
    const label = `page-${String(page).padStart(3, '0')}`;
    const sourceImage = rasterPage(fixture.sourcePdf, page, path.join(directory, `${label}-source`));
    const outputImage = rasterPage(fixture.outputPdf, page, path.join(directory, `${label}-output`));
    const diffImage = path.join(directory, `${label}-diff.png`);
    visualSamples.push({
      page,
      sourceImage: path.relative(outputRoot, sourceImage),
      outputImage: path.relative(outputRoot, outputImage),
      diffImage: path.relative(outputRoot, diffImage),
      sideBySideImage: path.relative(outputRoot, diffImage.replace('-diff.png', '-side-by-side.png')),
      ...compareImages(sourceImage, outputImage, diffImage)
    });
  }
  const semantics = compareTokens(await readFile(sourceText, 'utf8'), await readFile(outputText, 'utf8'));
  const findings = [];
  if (sourcePages !== outputPages) findings.push(`Page count changed from ${sourcePages} to ${outputPages}.`);
  if (semantics.sourcePreservationRatio < 1) {
    findings.push(`Text token preservation is ${(semantics.sourcePreservationRatio * 100).toFixed(2)}%.`);
  }
  findings.push(...decision.findings);
  return {
    fixtureId: fixture.id,
    domain: fixture.domain,
    classification: 'reviewRequired',
    humanDecision: decision.decision,
    reviewedAt: decision.reviewedAt,
    reviewer: decision.reviewer,
    sourcePages,
    outputPages,
    pageCountMatches: sourcePages === outputPages,
    semantics,
    visualSamples,
    findings
  };
}

function reportHtml(report) {
  const fixtures = report.fixtures.map(fixture => `
    <section>
      <h2>${fixture.fixtureId}</h2>
      <p><strong>${fixture.classification}</strong> · decision ${fixture.humanDecision} · pages ${fixture.sourcePages} → ${fixture.outputPages} · text preservation ${(fixture.semantics.sourcePreservationRatio * 100).toFixed(2)}%</p>
      <p>Reviewed by ${fixture.reviewer} on ${fixture.reviewedAt}.</p>
      <ul>${fixture.findings.map(finding => `<li>${finding}</li>`).join('')}</ul>
      ${fixture.visualSamples.map(sample => `<figure><img src="${sample.sideBySideImage}" alt="Source and PXA output for page ${sample.page}"><figcaption>Page ${sample.page}: normalized RMSE ${sample.normalizedRmse.toFixed(4)}</figcaption></figure>`).join('')}
    </section>`).join('');
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>PXA real-world fidelity review</title><style>body{font:16px system-ui;max-width:1200px;margin:32px auto;padding:0 20px;color:#17202a}section{border-top:1px solid #ccd2d8;padding:20px 0}img{max-width:100%;border:1px solid #ccd2d8}figure{margin:24px 0}figcaption{margin-top:6px;color:#59636e}</style></head><body><h1>PXA real-world fidelity review</h1><p>Report-only evidence. Rejected fixtures remain unsupported until their findings are corrected and reviewed again.</p>${fixtures}</body></html>\n`;
}

async function main() {
  for (const tool of ['soffice', 'pdfinfo', 'pdftotext', 'pdftoppm']) requireTool(tool);
  if (!hasTool('magick')) for (const tool of ['identify', 'compare', 'convert']) requireTool(tool);
  const args = process.argv.slice(2);
  const raw = path.resolve(root, option(args, '--raw', defaultRaw));
  const output = path.resolve(root, option(args, '--output', defaultOutput));
  const decisions = validateReviewDecisions(JSON.parse(await readFile(
    path.resolve(root, option(args, '--decisions', defaultDecisions)), 'utf8')));
  const decisionById = new Map(decisions.fixtures.map(item => [item.fixtureId, item]));
  await rm(output, { recursive: true, force: true });
  await mkdir(output, { recursive: true });

  const realWorld = path.join(root, 'test-assets', 'document-fidelity', 'real-world');
  const sourceDocxPdf = await convertOffice(
    path.join(realWorld, 'suffolk-public-mapping-guidance.docx'), path.join(output, 'office-source-docx'), 'source-docx');
  const sourceXlsxPdf = await convertOffice(
    path.join(realWorld, 'suffolk-buildings-2026.xlsx'), path.join(output, 'office-source-xlsx'), 'source-xlsx');
  const outputXlsxPdf = await convertOffice(
    path.join(raw, 'generated', 'real-world-xlsx-suffolk-buildings.xlsx'), path.join(output, 'office-output-xlsx'), 'output-xlsx');

  const fixtures = [
    {
      id: 'real-world-pdf-ukgi-annual-report', domain: 'pdf',
      sourcePdf: path.join(realWorld, 'ukgi-annual-report-2019.pdf'),
      outputPdf: path.join(raw, 'generated', 'real-world-pdf-ukgi-annual-report.pdf')
    },
    {
      id: 'real-world-docx-suffolk-mapping-guidance', domain: 'word',
      sourcePdf: sourceDocxPdf,
      outputPdf: path.join(raw, 'generated', 'real-world-docx-suffolk-mapping-guidance.pdf')
    },
    {
      id: 'real-world-xlsx-suffolk-buildings', domain: 'spreadsheet',
      sourcePdf: sourceXlsxPdf,
      outputPdf: outputXlsxPdf
    }
  ];

  const reviewed = [];
  for (const fixture of fixtures) reviewed.push(await reviewFixture(fixture, decisionById.get(fixture.id), output));
  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    mode: 'report-only',
    tools: { office: 'LibreOffice', rasterizer: 'Poppler pdftoppm', comparator: 'ImageMagick RMSE', dpi },
    fixtures: reviewed,
    summary: {
      fixtures: reviewed.length,
      pageCountMatches: reviewed.filter(item => item.pageCountMatches).length,
      humanApproved: reviewed.filter(item => item.humanDecision === 'approved').length,
      humanRejected: reviewed.filter(item => item.humanDecision === 'rejected').length,
      reviewRequired: reviewed.filter(item => item.classification === 'reviewRequired').length
    }
  };
  await writeFile(path.join(output, 'real-world-review.json'), `${JSON.stringify(report, null, 2)}\n`);
  await writeFile(path.join(output, 'real-world-review.html'), reportHtml(report));
  process.stdout.write(`Real-world fidelity review: ${report.summary.reviewRequired}/${report.summary.fixtures} require review; ${report.summary.pageCountMatches}/${report.summary.fixtures} preserve page count.\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch(error => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });
}
