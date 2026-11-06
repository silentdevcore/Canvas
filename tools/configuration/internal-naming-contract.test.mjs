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
  assert.ok(template.includes('"/api/pxa/templates/validate"'));
  assert.ok(template.includes('"PVAD Template"'));
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
        env: { ...env, PVAD_RESTORE_CONFIRM: 'RESTORE PXA DATABASE' }, encoding: 'utf8',
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
