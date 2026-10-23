import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const source = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');
const guide = await readFile(new URL('../../../docs/native-designer-project-formats.md', import.meta.url), 'utf8');
const schema = JSON.parse(await readFile(
  new URL('../../../docs/schema/pvad-project-manifest.schema.json', import.meta.url),
  'utf8',
));
const pdfSchema = JSON.parse(await readFile(
  new URL('../../../docs/schema/design-export.schema.json', import.meta.url),
  'utf8',
));
const workbookSchema = JSON.parse(await readFile(
  new URL('../../../docs/schema/pvad-workbook.schema.json', import.meta.url),
  'utf8',
));
const codeWorkspaceSchema = JSON.parse(await readFile(
  new URL('../../../docs/schema/pvad-code-workspace.schema.json', import.meta.url),
  'utf8',
));
const openApi = JSON.parse(await readFile(
  new URL('../../../docs/schema/openapi.json', import.meta.url),
  'utf8',
));

test('native project formats are a focused public documentation topic', () => {
  assert.match(source, /title: 'Project Files'/);
  assert.match(source, /PVADP Project \(\.pvadp\)/);
  assert.match(source, /PVADS Project \(\.pvads\)/);
  assert.match(source, /Use Undo to return to the previous document/);
});

test('the technical guide documents all canonical project operations and boundaries', () => {
  for (const route of [
    '/pdf/export', '/pdf/open', '/pdf/validate', '/pdf/inspect',
    '/pdf/export-with-workspace',
    '/spreadsheet/export', '/spreadsheet/open', '/spreadsheet/validate', '/spreadsheet/inspect',
  ]) assert.match(guide, new RegExp(route.replaceAll('/', '\\/')));
  assert.match(guide, /25 MiB/);
  assert.match(guide, /PXAPROJECT014/);
  assert.match(guide, /passwords and password hashes are never saved/i);
});

test('the public manifest schema distinguishes PXAP and PXAS', () => {
  assert.deepEqual(schema.properties.formatId.enum, ['pvadp', 'pvads']);
  assert.deepEqual(schema.properties.magic.enum, ['PVAD-PVADP-1', 'PVAD-PVADS-1']);
  assert.equal(schema.properties.entryDocument.const, 'document.json');
  assert.equal(schema.properties.requiredFeatures.uniqueItems, true);
  assert.equal(schema.properties.diagnostics.items.$ref, '#/$defs/diagnostic');
  assert.deepEqual(schema.$defs.diagnostic.properties.severity.enum, ['info', 'warning', 'error']);
  assert.equal(schema.$defs.component.properties.kind.const, 'code-workspace');
  assert.equal(codeWorkspaceSchema.properties.schemaVersion.const, '1.0');
  assert.match(guide, /confidential-source/);
  assert.equal(pdfSchema.properties.importDiagnostics.items.$ref, '#/$defs/importDiagnostic');
  assert.equal(workbookSchema.properties.importDiagnostics.items.$ref, '#/$defs/importDiagnostic');
});

test('the guide explains feature compatibility and inert spreadsheet diagnostics', () => {
  assert.match(guide, /compatibility\.missingFeatures/);
  assert.match(guide, /external-workbook formulas/);
  assert.match(guide, /Macro and provider data remain inert/);
  assert.match(guide, /never executes it/);
});

test('the guide documents rich-text metadata and filename security boundaries', () => {
  assert.match(guide, /server-side HTML export apply matching allowlists/);
  assert.match(guide, /Malformed markup is displayed as encoded text/);
  assert.match(guide, /invisible format characters/);
  assert.match(guide, /stable diagnostic rather than being silently rewritten/);
});

test('the guide documents tenant-scoped data-minimized project audits', () => {
  assert.match(guide, /designer\.projects\.<format>\.<operation>/);
  assert.match(guide, /never contain the upload\s+filename/);
  assert.match(guide, /without an active organization and user are rejected/);
});

test('the guide documents collision-safe imports and optimistic template revisions', () => {
  assert.match(guide, /preserves package IDs that do not collide/);
  assert.match(guide, /internal references/);
  assert.match(guide, /revision and `If-Match` contract/);
  assert.match(guide, /409 Conflict/);
  assert.match(guide, /without a persisted target remains\s+tenant-neutral/);
});

test('the guide publishes SDK stream helpers and reproducible synthetic fixtures', () => {
  assert.match(guide, /stable operation IDs/);
  assert.match(guide, /WritePdfAsync/);
  assert.match(guide, /WriteSpreadsheetAsync/);
  assert.match(guide, /docs\/fixtures\/native-projects/);
  assert.match(guide, /tools\/native-project-fixtures/);
});

test('the public OpenAPI snapshot exposes stable native project operations', () => {
  const operations = {
    '/api/pvad/v1/designer/projects/pdf/export': 'exportPdfProject',
    '/api/pvad/v1/designer/projects/pdf/export-with-workspace': 'exportPdfProjectWithWorkspace',
    '/api/pvad/v1/designer/projects/spreadsheet/export': 'exportSpreadsheetProject',
    '/api/pvad/v1/designer/projects/pdf/open': 'openPdfProject',
    '/api/pvad/v1/designer/projects/spreadsheet/open': 'openSpreadsheetProject',
    '/api/pvad/v1/designer/projects/pdf/validate': 'validatePdfProject',
    '/api/pvad/v1/designer/projects/spreadsheet/validate': 'validateSpreadsheetProject',
    '/api/pvad/v1/designer/projects/pdf/inspect': 'inspectPdfProject',
    '/api/pvad/v1/designer/projects/spreadsheet/inspect': 'inspectSpreadsheetProject',
  };
  for (const [route, operationId] of Object.entries(operations)) {
    assert.equal(openApi.paths[route].post.operationId, operationId);
  }

  for (const route of Object.keys(operations).filter((route) =>
    route.endsWith('/open') || route.endsWith('/validate') || route.endsWith('/inspect'))) {
    const properties = openApi.paths[route].post.requestBody
      .content['multipart/form-data'].schema.properties;
    assert.equal(properties.file.format, 'binary');
    assert.equal(Object.hasOwn(properties, 'reservedIds'), route.endsWith('/open'));
  }

  for (const schemaName of [
    'PvadProjectOpenResponseOfDesignExportDto',
    'PvadProjectOpenResponseOfSpreadsheetDto',
  ]) {
    assert.equal(
      openApi.components.schemas[schemaName].properties.upgradeReport.oneOf[1].$ref,
      '#/components/schemas/PvadProjectUpgradeReport',
    );
  }
  assert.equal(
    openApi.components.schemas.PvadProjectUpgradeReport.properties.steps.items.$ref,
    '#/components/schemas/PvadProjectUpgradeStepInfo',
  );
  assert.equal(
    openApi.components.schemas.PvadProjectManifest.properties.components.items.$ref,
    '#/components/schemas/PvadProjectComponentDescriptor',
  );
});

test('native project upgrades remain explicit, forward-only and recoverable', () => {
  assert.match(guide, /forward-only/);
  assert.match(guide, /never downgrades/);
  assert.match(guide, /upgradeReport/);
  assert.match(guide, /original package SHA-256/);
  assert.match(guide, /original\s+package\s+remains\s+the\s+recovery\s+source/i);
});

test('native project assets use typed folders and explicit safe kinds', () => {
  const asset = schema.$defs.asset;
  assert.ok(asset.required.includes('kind'));
  assert.deepEqual(asset.properties.kind.enum, ['image', 'font', 'preview', 'source', 'data']);
  for (const folder of ['images', 'fonts', 'previews', 'sources', 'data']) {
    assert.match(asset.properties.path.pattern, new RegExp(folder));
    assert.match(guide, new RegExp(`assets/${folder}/`));
  }
  assert.match(guide, /never\s+executes them or resolves package-provided URLs/);
});
