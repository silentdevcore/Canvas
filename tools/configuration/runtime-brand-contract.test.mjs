import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { publicGuidePaths } from '../../websites/PVAD.Documentation/publicGuides.js';

const root = fileURLToPath(new URL('../../', import.meta.url));

test('the runnable PDF sample emits current metadata and headings', () => {
  const sample = readFileSync(resolve(root, 'samples/PVAD.Demo/Program.cs'), 'utf8');
  assert.doesNotMatch(sample, /\bPXA\b|Pxa|pxa|Power Dox Automation|power-dox-automation/);
  assert.match(sample, /document.Info.Title = "PVADP Minimal PDF Demo"/);
  assert.match(sample, /document.Info.Creator = "PVADE Demo"/);
});

test('active feature and operational descriptions use current identities without changing stored keys', () => {
  const features = JSON.parse(readFileSync(resolve(root, 'product-metadata/designer-features.json'), 'utf8'));
  for (const feature of features.features) {
    assert.doesNotMatch(feature.fallbackDescription ?? '', /\bPXA\b|Power Dox Automation/);
  }
  const storage = JSON.parse(readFileSync(resolve(root, 'product-metadata/browser-storage.json'), 'utf8'));
  for (const entry of storage.entries) assert.doesNotMatch(entry.owner, /\bPXA\b/);
  assert.ok(storage.entries.some(entry => entry.keys.includes('PXA.StorageNotice')));
  const processing = JSON.parse(readFileSync(resolve(root, 'product-metadata/data-processing-inventory.json'), 'utf8'));
  assert.equal(processing.productionApproved, false);
  assert.doesNotMatch(JSON.stringify(processing), /\bPXA\b/);
  for (const path of ['deploy.sh', 'deploy/observability/README.md', 'deploy/observability/CAPACITY-PROFILES.md']) {
    const source = readFileSync(resolve(root, path), 'utf8')
      .replaceAll('X-PXA-Webhook-Signature', 'retained-signature-header')
      .replaceAll('X-PXA-Operator', 'retained-operator-header')
      .replaceAll('X-PXA-Webhook-Idempotency-Key', 'retained-idempotency-header');
    assert.doesNotMatch(source, /\bPXA\b/, path);
  }
});

test('Admin screenshot and smoke producers accept canonical environment inputs through the shared alias resolver', () => {
  for (const path of ['capture-documentation-screenshots.mjs', 'smoke-documentation.mjs']) {
    const source = readFileSync(resolve(root, 'websites/PVAD.Admin/scripts', path), 'utf8');
    assert.match(source, /readPvadEnvironment\('PVAD_ADMIN_/);
    assert.doesNotMatch(source, /process\.env\.PXA_/);
  }
});

test('Designer text renderer requests the bundled font registered for every style', () => {
  const renderer = readFileSync(resolve(root,
    'pvad-designer/src/components/Rendering/PdfTextRenderer.tsx'), 'utf8');
  const css = readFileSync(resolve(root, 'pvad-designer/src/styles/editor.css'), 'utf8');
  const family = renderer.match(/default: return "'([^']+)'/)?.[1];
  assert.equal(family, 'PVADE Helvetica');
  const faces = [...css.matchAll(/@font-face\s*\{([^}]+)\}/g)]
    .map(match => match[1]).filter(face => face.includes(`font-family: '${family}'`));
  assert.equal(faces.length, 4);
  for (const [style, weight] of [['normal', '400'], ['normal', '700'], ['italic', '400'], ['italic', '700']]) {
    const face = faces.find(value => value.includes(`font-style: ${style};`)
      && value.includes(`font-weight: ${weight};`));
    assert.ok(face, `${style} ${weight}`);
    const asset = face.match(/url\('([^']+)'\)/)?.[1];
    assert.ok(asset);
    assert.ok(readFileSync(resolve(root, 'pvad-designer/src/styles', asset)).length > 0);
  }
  assert.doesNotMatch(css, /PXA Helvetica/);
});

test('Designer product notifications and their stylesheet use the same current class prefix', () => {
  const css = readFileSync(resolve(root, 'pvad-designer/src/styles/product-experience.css'), 'utf8');
  const paths = [
    'notifications/toast.tsx', 'product/FeatureBadge.tsx',
    'product/ProductExperiencePanel.tsx', 'components/Layout/AppHeader.tsx',
  ];
  for (const path of paths) {
    const source = readFileSync(resolve(root, 'pvad-designer/src', path), 'utf8');
    assert.doesNotMatch(source, /pxa-(?:toast|feature|product|release|notification|panel)/, path);
    const classes = new Set(source.match(/pvad-[a-z]+(?:-[a-z]+)*/g));
    assert.ok(classes.size > 0, path);
    // Toast positioning belongs to react-hot-toast; this class is only a DOM hook.
    for (const name of classes) {
      if (path === 'notifications/toast.tsx' && name === 'pvad-toast-region') continue;
      assert.ok(css.includes(`.${name}`), `${path}: ${name}`);
    }
  }
  assert.doesNotMatch(css, /\.pxa-/);
});

test('Designer help and starter use current names while MCP discovery keeps its compatibility scopes', () => {
  for (const path of [
    'pvad-designer/src/pages/DocsPage.tsx',
    'pvad-designer/src/components/CodeEditor/starterTemplates.ts',
    'pvad-designer/src/docs/elementCatalog.ts',
  ]) assert.doesNotMatch(readFileSync(resolve(root, path), 'utf8'), /\bPXA\b|Power Dox Automation/, path);
  const help = readFileSync(resolve(root, 'pvad-designer/src/pages/DocsPage.tsx'), 'utf8');
  assert.doesNotMatch(help, /15 PDF|4 spreadsheet|PXA\/TECHNICAL_DOCUMENTATION/);
  assert.match(help, /docs\/README\.md/);
  const remote = readFileSync(resolve(root, 'tools/PVAD.Mcp/src/remote/app.ts'), 'utf8');
  assert.match(remote, /resource_name: 'PVADE MCP'/);
  assert.match(remote, /'pxa\.documents\.validate'/);
});

function sources(directory) {
  return readdirSync(resolve(root, directory), { withFileTypes: true }).flatMap(entry => {
    if (['bin', 'obj'].includes(entry.name)) return [];
    const path = `${directory}/${entry.name}`;
    return entry.isDirectory() ? sources(path) : path.endsWith('.cs') ? [path] : [];
  });
}

test('generation, importing and infrastructure retain only documented legacy identities', () => {
  const exceptions = {
    'src/Generation/PVAD.Generator/EInvoicePageLayout.cs': ['"pxa.e-invoice.pagination/1"'],
    'src/Importing/PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs': ['"PXA.ImageOcr"'],
    'src/Importing/PVAD.FileImporter/PdfFileImporter.cs': ['"pxaMetadata"'],
    'src/Importing/PVAD.Importer/Redaction/PdfRedactionApplier.cs': ['_PXARedacted'],
    'src/Importing/PVAD.Importer/Redaction/PdfImageRedactionEngine.cs': ['"PXARedacted"'],
    'src/Infrastructure/PVAD.Infrastructure.Converters/XmlDocumentExporter.cs': ['"PxaDocument"'],
    'src/Infrastructure/PVAD.Infrastructure.Persistence/PersistenceServiceCollectionExtensions.cs': ['"pxa-database"'],
    'src/Infrastructure/PVAD.Infrastructure.Persistence/Configuration/PvadEnvironment.cs': ['"PXA_"'],
    'src/Infrastructure/PVAD.Infrastructure.Persistence/Migrations/20260927050514_AddEInvoiceValidationRecords.cs': [
      'pxa.e_invoice_retention_purge',
    ],
  };
  const seen = new Set();
  for (const path of ['src/Generation', 'src/Importing', 'src/Infrastructure'].flatMap(sources)) {
    let source = readFileSync(resolve(root, path), 'utf8');
    for (const literal of exceptions[path] ?? []) {
      assert.ok(source.includes(literal), `${path}: stale exception`);
      source = source.split(literal).join('reviewed-contract');
      seen.add(path);
    }
    source = source.replace(/\bPXA(?:[A-Z]+\d{3}|\d{4})\b/g, 'stable-diagnostic-id')
      .replace(/"PXA-(?:MD|PDF-CHART)-\d{3}"/g, '"stable-diagnostic-id"')
      .replace(/#pragma warning (?:disable|restore) PXA0001/g, 'retained-obsolete-diagnostic');
    assert.equal(/PXA|Pxa|pxa|Power\s?Dox/.test(source), false, path);
  }
  assert.deepEqual([...seen].sort(), Object.keys(exceptions).sort());
});

test('all joined Core legacy names are bounded by diagnostic or explicit serialized-reader contracts', () => {
  const exceptions = {
    'src/Core/PVAD.Core/Primitives/ChartMetadataCodec.cs': ['"PXAChartsV2"'],
    'src/Core/PVAD.Core/Contracts/PvadCodeElementMarker.cs': ['"pxa-element-id:"'],
    'src/Core/PVAD.Core/ProjectFormats/PvadProjectModels.cs': [
      '"PXA-PXAP-1"', '"PXA-PXAS-1"', '"pxap"', '"pxas"',
      '"application/vnd.powerdox.pxap+zip"', '"application/vnd.powerdox.pxas+zip"',
      '".pxap"', '".pxas"', 'minimumPxaVersion', 'LegacyMinimumPxaVersion',
    ],
  };
  const seen = new Set();
  for (const path of sources('src/Core')) {
    let source = readFileSync(resolve(root, path), 'utf8');
    for (const literal of exceptions[path] ?? []) {
      assert.ok(source.includes(literal), `${path}: stale exception ${literal}`);
      source = source.split(literal).join('reviewed-serialized-contract');
      seen.add(path);
    }
    source = source.replace(/"PXA[A-Z0-9_-]+"/g, '"stable-diagnostic-id"');
    assert.doesNotMatch(source, /PXA|Pxa|pxa|PowerDox/, path);
  }
  assert.deepEqual([...seen].sort(), Object.keys(exceptions).sort());
});

test('migration source uses current prose while preserving baseline diagnostic identities', () => {
  const paths = sources('src/Migrations');
  assert.ok(paths.length > 50);
  const current = paths.map(path => {
    const source = readFileSync(resolve(root, path), 'utf8');
    assert.doesNotMatch(source, /\bPXA\b/, path);
    return source;
  }).join('\n');
  const baseline = execFileSync('git', ['grep', '-h', '-o', '-E', 'CANMIG[A-Z0-9]+',
    'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a', '--', 'src/Migrations'], { cwd: root, encoding: 'utf8' });
  const codes = new Set(current.match(/CANMIG[A-Z0-9]+/g));
  for (const code of new Set(baseline.trim().split('\n'))) assert.ok(codes.has(code), code);
});

test('generated ICC identity is four bytes and migration guide points at a shipped public asset', () => {
  const profile = readFileSync(resolve(root, 'src/Generation/PVAD.Pdf/Serialization/PdfSrgbIccProfile.cs'), 'utf8');
  assert.match(profile, /WriteAscii\(profile, 4, "PVAD"\)/);
  assert.match(profile, /WriteAscii\(profile, 80, "PVAD"\)/);
  assert.doesNotMatch(profile, /\bPXA\b/);
  const catalog = readFileSync(resolve(root, 'src/Migrations/PDF/PVAD.Migration.Pdf/PdfEInvoiceMigrationCatalog.cs'), 'utf8');
  const guide = catalog.match(/private const string Guide = "([^"]+)";/)?.[1];
  assert.equal(guide, '/docs/guides/PVAD.EInvoicing.md');
  assert.ok(publicGuidePaths.includes(guide.slice(1)));
  const features = JSON.parse(readFileSync(resolve(root, 'product-metadata/designer-features.json'), 'utf8'));
  assert.equal(features.product, 'PVADE Designer');
});

test('Core and Infrastructure retain only the reviewed legacy project magic as standalone old branding', () => {
  const findings = [];
  for (const path of [...sources('src/Core'), ...sources('src/Infrastructure')]) {
    const source = readFileSync(resolve(root, path), 'utf8');
    for (const [index, line] of source.split('\n').entries()) {
      if (/\bPXA\b/.test(line)) findings.push({ path, line: index + 1, text: line.trim() });
    }
  }
  // Exact source exception: do not turn a broad directory into an old-name allowlist.
  assert.equal(findings.length, 1);
  assert.equal(findings[0].path, 'src/Core/PVAD.Core/ProjectFormats/PvadProjectModels.cs');
  assert.equal(findings[0].text, 'kind == PvadProjectKind.Pdf ? "PXA-PXAP-1" : "PXA-PXAS-1";');
  const word = readFileSync(resolve(root,
    'src/Infrastructure/PVAD.Infrastructure.Word/WordDocumentExporter.cs'), 'utf8');
  assert.match(word, /el\.CommentAuthor \?\? "PVADE"/);
  assert.match(word, /el\.RevisionAuthor \?\? "PVADE"/);
});

test('importer branding retains explicit legacy diagnostics and OCR activity alias only', () => {
  for (const path of sources('src/Importing')) {
    const source = readFileSync(resolve(root, path), 'utf8');
    const withoutStableCodes = source.replace(/"PXA-(?:PDF-CHART|MD)-\d{3}"/g, '"stable-diagnostic"');
    const reviewed = path === 'src/Importing/PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs'
      ? withoutStableCodes.replace('public const string LegacyActivitySourceName = "PXA.ImageOcr";', '')
      : withoutStableCodes;
    assert.doesNotMatch(reviewed, /\bPXA\b/, path);
  }
});

test('current report generators use the new identity without touching archived evidence', () => {
  for (const path of [
    'tools/legal/validate-legal-launch-readiness.mjs',
    'tools/document-fidelity/real-world-review.mjs',
    'tools/document-fidelity/fidelity.mjs',
    'tools/visual-parity/run.mjs',
    'tools/visual-parity/pdf-trust-operations.mjs',
    'tools/pdfa/einvoice-pdfa-evidence.mjs',
  ]) assert.doesNotMatch(readFileSync(resolve(root, path), 'utf8'), /\bPXA\b/, path);
});

test('WebApi standalone old identity occurs only in explicit compatibility contracts', () => {
  const exceptions = {
    'Security/PvadAuthenticationSchemes.cs': ['PXA.Combined', 'PXA.ApiKey', 'PXA.McpDelegation', 'PXA.Designer.Cookie'],
    'Security/PvadCookieNames.cs': ['PXA.Session', 'PXA.Designer.Session', 'PXA.Antiforgery'],
    'Security/PvadDataProtection.cs': ['PXA.Mail.Outbox.Payload.v1', 'PXA.PdfTrust.RevocationEvidence.v1'],
    'Security/PvadMcpDelegationAuthenticationHandler.cs': ['PXA-MCP '],
    'Infrastructure/PvadResponseHeaders.cs': ['X-PXA-{suffix}', 'PXA-{suffix}'],
    'Infrastructure/PvadSecurityHeaderMiddleware.cs': ['X-PXA-{name}'],
    'Infrastructure/OpenApi/PvadDesignerProjectsOpenApiTransformer.cs': ['legacy PXA extension'],
    'Controllers/SystemController.cs': ['LegacyProductName: "PXA"', 'LegacyNamespacePrefix: "PXA"'],
    'Services/Mail/SmtpMailTransport.cs': ['X-PXA-Outbox-Id'],
  };
  const seen = new Set();
  for (const path of sources('PVAD.WebApi')) {
    let source = readFileSync(resolve(root, path), 'utf8');
    const relative = path.slice('PVAD.WebApi/'.length);
    for (const literal of exceptions[relative] ?? []) {
      assert.ok(source.includes(literal), `Remove stale exception ${relative}: ${literal}`);
      source = source.split(literal).join('reviewed-legacy-contract');
      seen.add(relative);
    }
    assert.doesNotMatch(source, /\bPXA\b/, path);
  }
  assert.deepEqual([...seen].sort(), Object.keys(exceptions).sort());
});

test('unpublished release summaries use the current brand', () => {
  const directory = resolve(root, 'product-metadata/release-fragments');
  for (const name of readdirSync(directory).filter(name => name.endsWith('.json'))) {
    const fragment = JSON.parse(readFileSync(resolve(directory, name), 'utf8'));
    if (fragment.id === 'release-1.1.0-verification') {
      assert.equal(fragment.impact, 'none');
      assert.equal(fragment.summary,
        'Release records now document the verified PXA 1.1.0 publication and its protected branch workflow.');
      continue;
    }
    assert.doesNotMatch(fragment.summary, /\bPXA\b/, name);
  }
});
