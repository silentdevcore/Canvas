import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';
import { validateProjectClosure } from './joint-package-rehearsal.mjs';
import { main, webApiProjects } from './webapi-protection-rehearsal.mjs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

test('host protection explicitly covers all fifty-six native, persistence and WebApi projects', () => {
  assert.equal(Object.keys(webApiProjects).length, 56);
  validateProjectClosure(webApiProjects, ['PVAD.WebApi']);
  const partial = { ...webApiProjects }; delete partial['PVAD.Infrastructure.Persistence'];
  assert.throws(() => validateProjectClosure(partial, ['PVAD.WebApi']), /dependency graph changed/);
  assert.throws(() => main(['publish']), /No host, output, trust/);
});

test('host preservation is narrow and does not retain private implementation names in native libraries', () => {
  const source = read('./pvad-native-licensing.mjs');
  assert.ok(source.includes('preserveHostContracts = false'));
  assert.ok(source.includes("assert.equal(modules.length, 56"));
  assert.ok(source.includes("preserveHostContracts && ['PVAD.WebApi.dll', 'PVAD.Infrastructure.Persistence.dll'].includes(file)"));
  const runner = read('./webapi-protection-rehearsal.mjs');
  assert.ok(runner.includes('{ preserveHostContracts: true }'));
  assert.ok(runner.includes('regularOutputs(webApiProjects), beforeBuild'));
  assert.ok(runner.includes('No host, output, trust, preservation or publication overrides'));
  assert.ok(!runner.includes('AcceptNPOIOSMFLicense'));
  for (const flag of ['realBrowserSessionAcceptance: false', 'postgresAcceptance: false', 'crossPlatformAcceptance: false', 'workerSandboxDeploymentAcceptance: false', 'productWideAcceptance: false', 'publicationPerformed: false'])
    assert.ok(runner.includes(flag));
});

test('the protected host runs real HTTP middleware, native friend calls, audits and isolated worker execution', () => {
  const source = read('./WebApiProtectionProbe/Program.cs');
  for (const name of ['anonymous-denied', 'permission-denied', 'other-tenant-denied', 'csrf-denied', 'product-entitlement-denied',
    'organization-feature-disabled', 'nutrient-designer-friend-boundary', 'nutrient-designer-mixed-layout', 'real-codeworker-process',
    'worker-host-revocation-propagated', 'worker-forbidden-source-no-result', 'real-ef-permission-denial-audit', 'real-ef-usage-tenant-boundary'])
    assert.ok(source.includes(name));
  assert.ok(source.includes('host-starts-without-native-license'));
  assert.ok(source.includes('unlicensed-native-denied'));
  assert.ok(source.includes('WebApplicationFactory<global::Program>'));
  assert.ok(source.includes('services.AddHostedService<PvadNativeLicenseBootstrap>()'));
  assert.ok(!source.includes('RemoveAll<IPvadEntitlementService>'));
  assert.ok(!source.includes('RemoveAll<IPvadDesignerFeatureGate>'));
  assert.ok(!source.includes('RemoveAll<IAntiforgery>'));
  assert.ok(!source.includes('RemoveAll<IPvadCodeWorkerClient>'));
  const runner = read('./webapi-protection-rehearsal.mjs');
  assert.ok(runner.includes("assert.deepEqual(after, baseline"));
  assert.ok(runner.includes("[['export', 2], ['worker', 1]]"));
  assert.ok(runner.includes('rehearseSupportRetention({ original, protectedDirectory, modules, work, version })'));
  assert.ok(runner.includes('assert.deepEqual(afterUnlicensed, baselineUnlicensed'));
  assert.ok(runner.includes("assert.equal(protectedText, originalText"));
});

test('protected host CI uploads only explicit non-secret evidence files', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  assert.ok(workflow.on.pull_request.paths.includes('src/Migrations/Report/**'));
  const job = workflow.jobs['webapi-protection'];
  assert.equal(job['runs-on'], 'ubuntu-latest');
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/webapi-protection-rehearsal.mjs'));
  const paths = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@')).with.path;
  assert.deepEqual(paths.trim().split('\n').map(path => path.trim()), ['artifacts/native-webapi-protection/*/report.json', 'artifacts/native-webapi-protection/*/cleanup.json']);
  assert.ok(!JSON.stringify(job).includes('nuget push'));
  assert.ok(workflow.jobs.licensing.steps.some(step => step.run?.includes('FullyQualifiedName~PvadDesignerAccessProblemTests')));
});
