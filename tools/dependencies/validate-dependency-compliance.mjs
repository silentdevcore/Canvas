import { readFile } from 'node:fs/promises';
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

console.log(`Dependency compliance metadata is valid (${pending.length} production blocker).`);
