import { readdir, readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../..');
const read = (path) => readFile(resolve(root, path), 'utf8');

function fail(message) {
  throw new Error(`Dependency compliance validation failed: ${message}`);
}

const catalog = JSON.parse(await read('product-metadata/dependency-compliance.json'));
if (catalog.schemaVersion !== 1) fail('schemaVersion must be 1');
if (typeof catalog.productionReady !== 'boolean') fail('productionReady must be boolean');
if (catalog.vulnerabilityPolicy?.nuget?.scope !== 'direct-and-transitive') {
  fail('NuGet policy must cover direct and transitive dependencies');
}
if (catalog.vulnerabilityPolicy?.nuget?.maximumAllowedSeverity !== 'none') {
  fail('NuGet vulnerability findings must be rejected');
}
if (catalog.vulnerabilityPolicy?.npm?.maximumAllowedSeverity !== 'moderate') {
  fail('npm must reject high and critical production findings');
}

const artifacts = new Set(catalog.sbom?.artifacts ?? []);
if (catalog.sbom?.format !== 'SPDX-JSON' || catalog.sbom?.version !== '2.2-or-later') {
  fail('SBOM policy must require SPDX JSON 2.2 or later');
}
for (const artifact of ['webapi', 'designer', 'webapi-container']) {
  if (!artifacts.has(artifact)) fail(`missing SBOM artifact ${artifact}`);
}

const decisions = catalog.licenseDecisions ?? [];
const pending = decisions.filter((decision) => !decision.productionApproved);
if (catalog.productionReady === (pending.length > 0)) {
  fail('productionReady does not match unresolved license decisions');
}
const npoi = decisions.find((decision) => decision.id === 'npoi-osmf-eula');
if (!npoi || npoi.version !== '2.8.0' || npoi.status !== 'pending-legal-review') {
  fail('NPOI 2.8.0 must remain an explicit pending legal decision');
}

const spreadsheetProject = await read(
  'src/Infrastructure/PXA.Infrastructure.Spreadsheet/PXA.Infrastructure.Spreadsheet.csproj',
);
if (!spreadsheetProject.includes('Include="NPOI" Version="2.8.0"')) {
  fail('NPOI project version and compliance decision differ');
}
if (spreadsheetProject.includes('AcceptNPOIOSMFLicense')) {
  fail('NPOI EULA cannot be accepted before the legal decision is approved');
}

const converterProject = await read(
  'src/Infrastructure/PXA.Infrastructure.Converters/PXA.Infrastructure.Converters.csproj',
);
if (converterProject.includes('Include="NPOI"')) {
  fail('the converter project must not carry the NPOI runtime dependency');
}

const [webApi, spreadsheet, converters, word, dependabot, ci, mcpIgnore, mcpLock] = await Promise.all([
  read('PXA.WebApi/PXA.WebApi.csproj'),
  read('src/Infrastructure/PXA.Infrastructure.Spreadsheet/PXA.Infrastructure.Spreadsheet.csproj'),
  read('src/Infrastructure/PXA.Infrastructure.Converters/PXA.Infrastructure.Converters.csproj'),
  read('src/Infrastructure/PXA.Infrastructure.Word/PXA.Infrastructure.Word.csproj'),
  read('.github/dependabot.yml'),
  read('.github/workflows/ci.yml'),
  read('tools/PXA.Mcp/.gitignore'),
  read('tools/PXA.Mcp/package-lock.json'),
]);
if (mcpIgnore.split(/\r?\n/u).includes('package-lock.json') || !mcpLock.includes('"lockfileVersion"')) {
  fail('MCP must commit a valid package lock for reproducible audits');
}
if (!webApi.includes('Include="Microsoft.OpenApi" Version="2.7.5"')) {
  fail('Microsoft.OpenApi security pin is missing');
}
for (const source of [spreadsheet, converters, word]) {
  if (!source.includes('Include="System.Security.Cryptography.Xml" Version="10.0.10"')) {
    fail('System.Security.Cryptography.Xml security pin is missing');
  }
}
for (const directory of ['/', '/pxa-designer', '/websites/PXA.Admin', '/tools/PXA.Mcp', '/PXA.WebApi']) {
  if (!dependabot.includes(`directory: "${directory}"`)) {
    fail(`Dependabot does not cover ${directory}`);
  }
}
for (const marker of [
  'check-nuget-vulnerabilities.mjs',
  'audit --omit=dev --audit-level=high',
  'Microsoft.Sbom.DotNetTool',
  'anchore/sbom-action@e22c389904149dbc22b58101806040fa8d37a610',
  'webapi-container',
]) {
  if (!ci.includes(marker)) fail(`CI marker is missing: ${marker}`);
}

const workflowDirectory = resolve(root, '.github/workflows');
const workflowFiles = (await readdir(workflowDirectory)).filter((name) => name.endsWith('.yml'));
const workflowSources = await Promise.all(
  workflowFiles.map(async (name) => ({ name, source: await read(`.github/workflows/${name}`) })),
);
for (const { name, source } of workflowSources) {
  for (const match of source.matchAll(/^\s*(?:-\s*)?uses:\s*([^@\s]+)@([^\s#]+)/gmu)) {
    const [, action, reference] = match;
    if (action.startsWith('./')) continue;
    if (!/^[a-f0-9]{40}$/u.test(reference)) {
      fail(`${name} must pin ${action} to a full commit SHA`);
    }
  }
}

const requiredActionPins = new Map([
  ['actions/checkout', '3d3c42e5aac5ba805825da76410c181273ba90b1'],
  ['actions/setup-node', '820762786026740c76f36085b0efc47a31fe5020'],
  ['actions/setup-dotnet', 'a98b56852c35b8e3190ac28c8c2271da59106c68'],
  ['actions/upload-artifact', '043fb46d1a93c77aae656e7c1c64a875d1fc6a0a'],
  ['docker/setup-buildx-action', 'bb05f3f5519dd87d3ba754cc423b652a5edd6d2c'],
  ['docker/login-action', 'dbcb813823bdd20940b903addbd779551569679f'],
  ['docker/build-push-action', '53b7df96c91f9c12dcc8a07bcb9ccacbed38856a'],
]);
const allWorkflowSource = workflowSources.map(({ source }) => source).join('\n');
for (const [action, sha] of requiredActionPins) {
  if (!allWorkflowSource.includes(`uses: ${action}@${sha}`)) {
    fail(`required Node.js 24 action pin is missing: ${action}@${sha}`);
  }
}

console.log(`Dependency compliance metadata is valid (${pending.length} production blocker).`);
