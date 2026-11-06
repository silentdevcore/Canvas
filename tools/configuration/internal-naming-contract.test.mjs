import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(join(root, path), 'utf8');

test('current account, admin and Designer test factories use canonical configuration and request headers', () => {
  const factories = [
    'AccountClosureControllerTests', 'AccountCrossTenantAccessTests',
    'AccountEntitlementsControllerTests', 'AccountMailNotificationsTests',
    'AccountOrganizationControllerTests', 'AccountProfileControllerTests',
    'AccountRegistrationControllerTests', 'AccountServiceAccountsAndSecurityControllerTests',
    'AccountSubscriptionAndLicensesControllerTests', 'AdminOrganizationsControllerTests',
    'AdminSubscriptionsControllerTests', 'AdminSystemControllerTests', 'AdminUsersControllerTests',
    'AuthControllerTests', 'DesignerAuthenticationControllerTests', 'IdentityMailFlowTests',
    'MailDeliveryLifecycleTests', 'RegistrationConflictTests',
  ];
  for (const name of factories) {
    const path = `tests/PVAD.Api.Tests/${name}.cs`;
    const source = read(path);
    assert.ok(source.includes('["ConnectionStrings:PvadDatabase"] = connectionString'), path);
    assert.doesNotMatch(source, /ConnectionStrings:PxaDatabase|https:\/\/account\.pxa\.test/, path);
    assert.doesNotMatch(source, /Headers\.Add\("X-PXA-(?:CSRF|Application|API-Key)"/, path);
  }
  const aliases = read('tests/PVAD.Api.Tests/PvadConfigurationAliasesTests.cs');
  assert.ok(aliases.includes('[InlineData("ConnectionStrings:PxaDatabase", "ConnectionStrings:PvadDatabase")]'));
  const csrf = read('tests/PVAD.Api.Tests/PvadAntiforgeryCookieCompatibilityTests.cs');
  for (const legacyCookie of ['false', 'true']) {
    for (const identity of ['PXA', 'PVAD']) {
      assert.ok(csrf.includes(`[InlineData(${legacyCookie}, "X-${identity}-CSRF")]`));
    }
  }
  const licenses = read('tests/PVAD.Api.Tests/AccountSubscriptionAndLicensesControllerTests.cs');
  assert.ok(licenses.includes('"application/vnd.pvad.license+json"'));
  assert.ok(licenses.includes('"PVAD-TEST-0001.pvad-license.json"'));
});

test('new PDF and OCR test data and explanatory names use the current identity', () => {
  for (const path of [
    'tests/PVAD.Api.Tests/AccountCrossTenantAccessTests.cs',
    'tests/PVAD.Api.Tests/TrialActivationServiceTests.cs',
    'tests/PVAD.Api.Tests/DesignerSymbolRendererTests.cs',
    'tests/PVAD.Core.Tests/ExpressionEvaluatorTests.cs',
    'tests/PVAD.FileImporter.ImageOcr.Tests/ImageToPdfConverterPvadContractTests.cs',
    'tests/PVAD.Importer.Tests/PdfFullRewriteWriterTests.cs',
    'tests/PVAD.Importer.Tests/PdfPageEditorTests.cs',
  ]) assert.doesNotMatch(read(path), /pxa/i, path);
  const encrypted = read('tests/PVAD.Importer.Tests/PdfEncryptedDocumentTests.cs');
  assert.ok(encrypted.includes('"PVAD-ENCRYPTION-PROBE-71923"'));
  assert.ok(encrypted.includes('CreateRecipientCertificate("PVAD Intended Recipient")'));
  assert.ok(encrypted.includes('CreateRecipientCertificate("PVAD Unrelated Recipient")'));
  assert.doesNotMatch(encrypted, /PxaMarker|PxaCompressed|PXA-(?:STRING|ENCRYPTION)|PXA (?:Intended|Unrelated)/);
  const redaction = read('tests/PVAD.Importer.Tests/PdfRedactionPreflightTests.cs');
  assert.ok(redaction.includes('"CONFIDENTIAL-PVAD-SECRET"'));
  assert.doesNotMatch(redaction, /CONFIDENTIAL-PXA-SECRET|PxaMarker|PXA redaction preflight fixture/);
});

test('current mail and database fixtures use reserved synthetic domains with current names', () => {
  for (const path of [
    'tests/PVAD.Api.Tests/IdentityMailFlowTests.cs',
    'tests/PVAD.Api.Tests/PvadMailTemplateRendererTests.cs',
    'tests/PVAD.Api.Tests/PvadJobProcessorTests.cs',
    'tests/PVAD.Infrastructure.Persistence.Tests/PostgreSqlPersistenceTests.cs',
    'tests/PVAD.Infrastructure.Persistence.Tests/PvadDbContextModelTests.cs',
  ]) assert.doesNotMatch(read(path), /pxa\.(?:test|local)|Database=pxa-model-tests|Username=pxa/i, path);
  assert.doesNotMatch(read('tests/PVAD.Api.Tests/AuthControllerTests.cs'), /pxa-integration-test/);
});

test('current frontend examples and synthetic facade documents use canonical names', () => {
  for (const path of [
    'pvad-designer/src/__tests__/backendSymbolRenderer.test.ts',
    'pvad-designer/src/__tests__/codePreviewParity.test.tsx',
    'pvad-designer/src/__tests__/codeViewerExport.test.tsx',
    'pvad-designer/src/__tests__/pageLayout.test.ts',
    'pvad-designer/e2e/account-accessibility.spec.ts',
    'pvad-designer/e2e/legal-account-admin.spec.ts',
    'websites/PVAD.Account/tests/accountContext.test.ts',
    'websites/PVAD.Account/tests/accountPermissions.test.ts',
    'tests/PVAD.Application.Tests/DesignUseCaseFacadeTests.cs',
    'tests/PVAD.Export.Tests/PvadExportCompositionTests.cs',
    'tests/PVAD.Infrastructure.Pdf.Tests/PdfInfrastructureFacadeTests.cs',
    'tests/PVAD.Generator.Tests/PdfFacadeTests.cs',
    'tests/PVAD.Importer.Tests/PdfImportFacadeTests.cs',
  ]) assert.doesNotMatch(read(path), /\bPXA\b|Power Dox Automation|powerdoxautomation\.com|pxa-(?:export|pdf|legal)|@pxa\.test/, path);
});

test('bulk example renaming preserves historical release text and explicit reader contracts', () => {
  const releases = read('pvad-designer/src/__tests__/productExperiencePanel.test.tsx');
  assert.ok(releases.includes("title: 'PXA 1.1.0'"));
  assert.ok(releases.includes("toHaveAccessibleName('PXA 1.1.0')"));
  assert.ok(releases.includes('acceptance in PVADE Account.'));
  const converters = read('tests/PVAD.Infrastructure.Converters.Tests/ConverterFacadeTests.cs');
  assert.ok(converters.includes('Assert.Equal("PxaDocument", legacy.Root!.Name.LocalName)'));
  assert.ok(converters.includes('Assert.Contains("Hello PVAD", canonical.ToString())'));
  const projects = read('tests/PVAD.Core.Tests/PvadProjectArchiveTests.cs');
  for (const value of ['PXA-PXAP-1', 'minimal.pxap', 'minimal.pxas', 'minimumPxaVersion'])
    assert.ok(projects.includes(value), value);
  const template = read('tests/PVAD.Api.Tests/TemplatesControllerTests.cs');
  assert.ok(template.includes('"/api/pvad/templates/validate"'));
  assert.ok(template.includes('"PVAD Template"'));
});

test('current API helper data uses canonical names without replacing wire identifiers', () => {
  for (const name of [
    'DocumentOpsControllerTests', 'PdfEInvoicesControllerTests', 'PdfSignaturesControllerTests',
    'PdfTrustInspectionJobTests', 'PdfViewerAnnotationsControllerTests', 'PvadJobProcessorTests',
    'PvadOfflineLicenseValidatorTests', 'PvadPdfLongTermValidationMaterialProviderTests',
    'PvadPdfRevocationEvidenceProviderTests', 'PvadPdfTrustPolicyProviderTests',
    'PvadPdfTrustSecurityTests', 'PvadRetentionGovernanceTests', 'PvadSystemOperatorAccessTests',
  ]) assert.doesNotMatch(read(`tests/PVAD.Api.Tests/${name}.cs`),
    /pxa-(?:trust-job|revocation-cache|aia-cache|trust-policy)|PXA (?:World|Clone|note|API signing fixture|service|security test)|PXA\.Retention\.Tests|operator@powerdoxautomation\.com/i, name);
  const observability = read('tests/PVAD.Api.Tests/PvadObservabilityTests.cs');
  assert.ok(observability.includes('ServiceName = "pvad-test"'));
  assert.ok(observability.includes('ServiceNamespace = "PVAD.Testing"'));
  assert.ok(observability.includes('legacy ? "pxa-json" : "pvad-json"'));
  const workspace = read('tests/PVAD.Api.Tests/CodeWorkspaceConversionTests.cs');
  assert.ok(workspace.includes('PXA and Canvas are customer text'));
  assert.ok(workspace.includes('return a safe PVAD document;'));
});

test('private version state is neutral while the public legacy JSON reader stays exact', () => {
  const model = read('src/Core/PVAD.Core/ProjectFormats/PvadProjectModels.cs');
  assert.ok(model.includes('private string? legacyMinimumVersion;'));
  assert.doesNotMatch(model, /private string\? minimumPxaVersion/);
  assert.ok(model.includes('[JsonPropertyName("minimumPxaVersion")]'));
  assert.ok(model.includes('public string? LegacyMinimumPxaVersion'));
  assert.ok(model.includes('minimumPvadVersion ?? legacyMinimumVersion ?? PvadProjectFormat.CurrentProductVersion'));
  const cleanup = read('tools/configuration/clean-legacy-build-artifacts.mjs');
  assert.ok(cleanup.includes('untracked generated legacy build artifacts'));
  assert.ok(cleanup.includes('/^PXA\\..*\\.(?:dll|exe|pdb|deps\\.json|runtimeconfig\\.json)$/'));
});

test('current recovery and converter examples use canonical names without rewriting historical inputs', () => {
  const workspace = read('tests/PVAD.Api.Tests/CodeWorkspaceConversionTests.cs');
  assert.ok(workspace.includes('<strong>PVAD</strong>'));
  assert.ok(workspace.includes('PXA and Canvas are customer text'));
  assert.ok(workspace.includes('[InlineData("pxa-element-id:")]'));
  assert.doesNotMatch(workspace, /pxa-worker-test-|<strong>PXA<\/strong>/);
  const migration = read('tests/PVAD.Api.Tests/MigrationControllerTests.cs');
  assert.ok(migration.includes('var generatedCode = body.GetProperty("pxaCode").GetString();'));
  assert.doesNotMatch(migration, /var pxaCode/);
  const fidelity = read('tools/document-fidelity/fidelity.test.mjs');
  assert.ok(fidelity.includes("provenance: 'pvad-synthetic'"));
  assert.ok(fidelity.includes("sourceFormat: 'pxa-json', provider: 'pxa-pdf-importer'"));
  const version = read('tools/versioning/pvad-version.test.mjs');
  assert.ok(version.includes("title: 'PXA 1.0.0'"));
  assert.ok(version.includes("'PVAD Release Test'"));
  for (const path of ['tools/legal/export-legal-snapshot.test.mjs',
    'tools/document-fidelity/real-world-review.test.mjs'])
    assert.doesNotMatch(read(path), /pxa/i, path);
  const restore = read('tools/legal/restore-postgres.sh');
  assert.ok(restore.includes("'RESTORE PVAD DATABASE'|'RESTORE PXA DATABASE'"));
  assert.ok(restore.includes('Set PVAD_RESTORE_CONFIRM="RESTORE PVAD DATABASE"'));
});

test('generic API workflows and current project examples exercise canonical identities', () => {
  for (const name of ['DocumentOpsControllerTests', 'TemplatesControllerTests',
    'SpreadsheetControllerTests', 'ExportControllerTests', 'PdfViewerFormsControllerTests',
    'PdfViewerAnnotationsControllerTests', 'AdminSubscriptionsControllerTests',
    'MigrationControllerTests']) {
    const source = read(`tests/PVAD.Api.Tests/${name}.cs`);
    assert.doesNotMatch(source, /\/api\/pxa\//, name);
    assert.match(source, /\/api\/pvad\//, name);
  }
  const aliases = read('tests/PVAD.Api.Tests/PvadLegacyApiMiddlewareTests.cs');
  assert.ok(aliases.includes('[InlineData("/api/pxa/templates", "/api/pvad/templates")]'));
  assert.ok(aliases.includes('"/api/pxa/export/formats"'));
  const headers = read('tests/PVAD.Api.Tests/PdfViewerAnnotationsControllerTests.cs');
  assert.ok(headers.includes('"PXA-Redaction-Audit"'));
  assert.ok(headers.includes('"pvad-review.pdf"'));
  assert.doesNotMatch(headers, /pxa-(?:pdf-viewer|doc-|review\.pdf)/);
  const projects = read('pvad-designer/src/__tests__/designerProjectApi.test.ts');
  assert.ok(projects.includes("'invoice.pvadp'"));
  assert.ok(projects.includes("'budget.pvads'"));
  assert.ok(projects.includes("title: 'Legacy', formatId: 'pxap'"));
  assert.ok(projects.includes("'legacy.pxap'"));
  const artifacts = read('tests/PVAD.Importer.Tests/PdfUaGenerationTests.cs');
  assert.doesNotMatch(artifacts, /pxa-pdfua1-/);
  for (const name of ['structure', 'custom-role', 'table', 'widgets'])
    assert.ok(artifacts.includes(`"pvad-pdfua1-${name}.pdf"`));
  assert.ok(artifacts.includes('"PXAPDFUA009"'));
});

test('generic project archive examples use current filenames, content and workbook schema', () => {
  const source = read('tests/PVAD.Core.Tests/PvadProjectArchiveTests.cs');
  for (const extension of ['pvadp', 'pvads']) {
    assert.ok(source.includes(`Path.Combine(directory, "project.${extension}")`));
    assert.ok(source.includes(`Directory.EnumerateFiles(directory, ".project.${extension}.*.tmp")`));
  }
  assert.doesNotMatch(source, /"\.?project\.pxa[ps]|Hello PXAP/);
  assert.equal((source.match(/"Hello PVADP"/g) ?? []).length, 2);
  const schema = JSON.parse(read('docs/schema/pvad-workbook.schema.json')).$id;
  const currentWorkbook = source.slice(source.indexOf('private static SpreadsheetDto CompleteSpreadsheet()'));
  assert.ok(currentWorkbook.includes(`Schema = "${schema}"`));
  assert.doesNotMatch(currentWorkbook, /pxa|powerdox/i);
  const schemaCompatibility = source.slice(
    source.indexOf('[InlineData("https://pvade.com/schemas/pvad-workbook.schema.json")]'),
    source.indexOf('public void Pdf_manifest_records_the_capabilities_required_by_the_document()'));
  assert.ok(schemaCompatibility.includes('[InlineData("https://powerdoxautomation.com/schemas/pxa-workbook.schema.json")]'));
  assert.ok(schemaCompatibility.includes('Assert.Equal(schema, package.Document.Schema)'));
  assert.ok(schemaCompatibility.includes('AssertJsonEquivalent(workbook, package.Document)'));
});

test('general smoke and browser workflows use current inputs with explicit retained cases', () => {
  const smoke = read('deploy/observability/smoke-test.sh');
  for (const path of ['auth/csrf', 'auth/login', 'admin/operator/access',
    'admin/operator/documentation', 'admin/operator/documentation/legal-backup-restore-recovery'])
    assert.ok(smoke.includes(`/api/pvad/v1/${path}"`), path);
  assert.doesNotMatch(smoke, /\/api\/pxa\/|RESTORE PXA DATABASE/);
  assert.ok(smoke.includes('contains("RESTORE PVAD DATABASE")'));
  for (const uid of ['pxa-prometheus', 'pxa-loki', 'pxa-tempo', 'pxa-platform-overview',
    'pxa-infrastructure', 'pxa-operations', 'pxa-document-operations', 'pxa-browser-health'])
    assert.ok(smoke.includes(uid), uid);
  const syntax = spawnSync('bash', ['-n', join(root, 'deploy/observability/smoke-test.sh')], { encoding: 'utf8' });
  assert.equal(syntax.status, 0, syntax.stderr);
  for (const [file, suffix] of [['e-invoice-authenticated', 'EINVOICE'], ['pdf-viewer-trust-authenticated', 'TRUST']]) {
    const source = read(`pvad-designer/e2e/${file}.spec.ts`);
    assert.ok(source.includes(`readPvadEnvironment('PVAD_RUN_LIVE_AUTH_${suffix}') !== '1'`));
    assert.doesNotMatch(source, /process\.env\.PXA_RUN_LIVE_AUTH_/);
  }
  const company = read('pvad-designer/e2e/company-account-flow.spec.ts');
  assert.ok(company.includes("['pvad_signed_in', 'pxa_signed_in']"));
  assert.ok(company.includes('localStorage.getItem(key), marker)).toBeNull()'));
  assert.doesNotMatch(company, /localStorage\.getItem\([^\n]*\)\)\)\.toBe\('1'\)/);
  const storage = read('pvad-designer/e2e/legal-storage-notice.spec.ts');
  const split = storage.indexOf("test('a legacy storage acknowledgement");
  assert.ok(split > 0);
  assert.ok(storage.slice(0, split).includes("name: 'PVAD.StorageNotice'"));
  assert.doesNotMatch(storage.slice(0, split), /PXA\.StorageNotice/);
  assert.ok(storage.slice(split).includes("name: 'PXA.StorageNotice'"));
});

test('project API, telemetry and recovery defaults use canonical test data', () => {
  const projects = read('tests/PVAD.Api.Tests/DesignerProjectsControllerTests.cs');
  assert.deepEqual(projects.match(/"[^"\n]+\.pxa[ps]"/g), ['"minimal.pxap"']);
  for (const filename of ['annual-report.pvadp', 'forecast.pvads', 'document.pvads',
    'import.pvadp', 'inspectable.pvads', 'private-name.pvadp', 'private-sheet.pvads'])
    assert.ok(projects.includes(`"${filename}"`), filename);
  for (const value of ['designer.projects.pxap.open', 'designer.projects.pxas.validate',
    '[InlineData("pxap", "pvadp")]', '[InlineData("pxas", "pvads")]', 'PXAPROJECT002'])
    assert.ok(projects.includes(value), value);
  for (const [extension, kind] of [['pvadp', 'Pdf'], ['pxap', 'Pdf'], ['pvads', 'Spreadsheet'], ['pxas', 'Spreadsheet']])
    assert.ok(projects.includes(`[InlineData(".${extension}", PvadProjectKind.${kind})]`));
  const telemetry = read('tests/PVAD.Api.Tests/PvadObservabilityTests.cs');
  assert.ok(telemetry.includes('"__Host-PVAD.Session=secret"'));
  assert.ok(telemetry.includes('RecordDependencyHealth("pvad-database", true,'));
  assert.doesNotMatch(telemetry, /"PXA\.Session=secret"|RecordDependencyHealth\("pxa-database"/);
  assert.ok(telemetry.includes('Assert.Equal("pxa-database", PersistenceServiceCollectionExtensions.LegacyDataSourceName)'));
  const recovery = read('pvad-designer/e2e/status-pages.spec.ts');
  assert.ok(recovery.includes("['current', 'pvad-status-recovery-v1']"));
  assert.ok(recovery.includes("['legacy', 'pxa-status-recovery-v1']"));
  assert.ok(recovery.includes('sessionStorage.setItem(key, JSON.stringify('));
  assert.ok(recovery.includes('(${generation} storage)'));
});

test('current MCP setups use canonical names and keep compatibility cases explicit', () => {
  const config = read('tools/PVAD.Mcp/src/remote/config.test.ts');
  const currentConfig = config.slice(config.indexOf("test('remote configuration requires"));
  assert.doesNotMatch(currentConfig, /PXA_|http:\/\/pxa-/);
  assert.ok(config.includes("[key.replace('PVAD_', 'PXA_'), value]"));
  assert.ok(config.includes("PXA_API_KEY: 'different-key'"));
  const auth = read('tools/PVAD.Mcp/src/remote/auth.test.ts');
  const currentAuth = auth.split("test('claim and scope aliases stay equivalent")[0];
  assert.ok(currentAuth.includes("organizationClaim: 'pvad_organization'"));
  assert.ok(currentAuth.includes("scope: 'pvad.docs.read pvad.documents.validate'"));
  assert.doesNotMatch(currentAuth, /pxa_organization:|scope: 'pxa\./);
  assert.ok(auth.includes("assert.deepEqual(auth.scopes, ['pxa.documents.render'])"));
  const app = read('tools/PVAD.Mcp/src/remote/app.test.ts');
  const defaultVerifier = app.split('for (const scope of')[0];
  assert.ok(defaultVerifier.includes("scopes: ['pvad.docs.read']"));
  assert.ok(app.includes("for (const scope of ['pvad.docs.read', 'pxa.docs.read'])"));
  assert.doesNotMatch(app, /organizationClaim: 'pxa_organization'/);
  const client = read('tools/PVAD.Mcp/src/core/apiClient.test.ts');
  const currentClient = client.split("test('legacy template grants")[0];
  assert.ok(currentClient.includes("scopes: ['pvad.templates.read']"));
  assert.doesNotMatch(currentClient, /scopes: \['pxa\./);
  assert.ok(client.includes("scopes: ['pxa.templates.read']"));
});

test('gateway renames local helpers but retains upstream and legacy header contracts', () => {
  const gateway = read('deploy/observability/operator-gateway/templates/default.conf.template');
  assert.match(gateway, /map \$http_upgrade \$pvad_connection_upgrade/);
  assert.match(gateway, /location = \/_pvad_operator_auth/);
  assert.equal((gateway.match(/auth_request \/_pvad_operator_auth;/g) ?? []).length, 4);
  assert.match(gateway, /auth_request_set \$pvad_operator \$upstream_http_x_pxa_operator;/);
  assert.match(gateway, /proxy_set_header X-WEBAUTH-USER \$pvad_operator;/);
  assert.match(gateway, /proxy_set_header Connection \$pvad_connection_upgrade;/);
  for (const generation of ['PXA', 'PVAD']) {
    for (const key of ['API-Key', 'Application'])
      assert.ok(gateway.includes(`proxy_set_header X-${generation}-${key} "";`));
  }
  assert.doesNotMatch(gateway, /\$pxa_operator|\$pxa_connection_upgrade|\/_pxa_operator_auth/);
  for (const path of ['tests/PVAD.Api.Tests/PvadObservabilityTests.cs',
    'tests/PVAD.Api.Tests/OperatorDocumentationContractTests.cs']) {
    assert.ok(read(path).includes('auth_request /_pvad_operator_auth;'));
    assert.ok(!read(path).includes('auth_request /_pxa_operator_auth;'));
  }
});

test('backup shell flow emits current names and accepts old names using mocked database tools', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-backup-contract-'));
  try {
    // Fake tools exercise file naming and checksum logic, not PostgreSQL validity.
    writeFileSync(join(directory, 'pg_dump'), '#!/bin/sh\nfor arg do\ncase "$arg" in --file=*) printf "synthetic backup" > "${arg#--file=}" ;; esac\ndone\n', { mode: 0o700 });
    writeFileSync(join(directory, 'pg_restore'), '#!/bin/sh\nexit 0\n', { mode: 0o700 });
    writeFileSync(join(directory, 'psql'), '#!/bin/sh\nprintf "0\\n"\n', { mode: 0o700 });
    const env = { PATH: `${directory}:${process.env.PATH}`, PVAD_DATABASE_URL: 'postgresql://synthetic.invalid/unused' };
    const output = join(directory, 'backups');
    for (let index = 0; index < 2; index++) {
      const result = spawnSync('sh', [join(root, 'tools/legal/backup-postgres.sh'), output], { env, encoding: 'utf8' });
      assert.equal(result.status, 0, result.stderr);
    }
    const backups = readdirSync(output).filter(name => name.endsWith('.dump'));
    assert.equal(backups.length, 2);
    for (const name of backups) {
      assert.match(name, /^pvad-\d{8}T\d{6}Z-\d+\.dump$/);
      const backup = join(output, name);
      const bytes = readFileSync(backup);
      assert.equal(readFileSync(`${backup}.sha256`, 'utf8'), `${createHash('sha256').update(bytes).digest('hex')}  ${name}\n`);
      assert.equal(statSync(backup).mode & 0o777, 0o600);
      assert.equal(statSync(`${backup}.sha256`).mode & 0o777, 0o600);
      const canonical = spawnSync('sh', [join(root, 'tools/legal/restore-postgres.sh'), backup], {
        env: { ...env, PVAD_RESTORE_CONFIRM: 'RESTORE PVAD DATABASE' }, encoding: 'utf8',
      });
      assert.equal(canonical.status, 0, canonical.stderr);
      const legacy = join(output, name.replace(/^pvad-/, 'pxa-'));
      writeFileSync(legacy, bytes);
      writeFileSync(`${legacy}.sha256`, `${createHash('sha256').update(bytes).digest('hex')}  ${legacy.split('/').at(-1)}\n`);
      const restored = spawnSync('sh', [join(root, 'tools/legal/restore-postgres.sh'), legacy], {
        env: { ...env, PXA_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }, encoding: 'utf8',
      });
      assert.equal(restored.status, 0, restored.stderr);
      assert.deepEqual(readFileSync(legacy), bytes);
    }
    assert.ok(read('tools/legal/run-backup-restore-smoke-test.sh').includes("-name 'pvad-*.dump'"));
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('ephemeral test identities and Mailpit-only addresses are consistent', () => {
  const runtime = read('deploy/observability/retention-runtime-test.sh');
  assert.ok(runtime.includes('project="pvad-retention-test-$(date +%s)-$$"'));
  for (const source of [runtime, read('deploy/observability/README.md')]) {
    assert.ok(source.includes('pvad_retention_marker'));
    assert.ok(source.includes('pvad-retention-marker'));
    assert.doesNotMatch(source, /pxa_retention_|pxa-retention-marker/);
  }
  const mail = read('deploy/observability/alertmanager-email-smoke-test.sh');
  const config = read('deploy/observability/alertmanager/alertmanager.yml');
  assert.ok(config.includes('pvad-alerts@example.test'));
  assert.ok(config.includes('pvad-operators@example.test'));
  assert.ok(mail.includes('pvad-alerts@example.test'));
  assert.ok(mail.includes('pvad-alert-email-'));
  assert.doesNotMatch(mail, /pxa-alert-email-|powerdox\.local|Service: pxa-observability/);
  assert.ok(mail.includes('PXA-OBS-TEST'));
  assert.ok(mail.includes('/d/pxa-platform-overview/pxa-platform-overview'));
  const status = read('deploy/websites/status-hosting-smoke-test.sh');
  assert.ok(status.includes('/not-a-pvad-route'));
  assert.ok(status.includes('/api/pxa/v1/version'));
  for (const path of ['tools/legal/run-backup-restore-smoke-test.sh',
    'deploy/observability/retention-runtime-test.sh',
    'deploy/observability/alertmanager-email-smoke-test.sh',
    'deploy/websites/status-hosting-smoke-test.sh']) {
    const syntax = spawnSync('bash', ['-n', join(root, path)], { encoding: 'utf8' });
    assert.equal(syntax.status, 0, syntax.stderr);
  }
});
