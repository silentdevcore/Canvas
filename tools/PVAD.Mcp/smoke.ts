/**
 * MCP smoke test: spawns the server over stdio, lists tools, and exercises the core tools
 * (list_elements, get_element_schema, get_example, validate_design with a valid + invalid design).
 * Run:  npx tsx smoke.ts
 */
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { fileURLToPath } from 'node:url';
import { findElement, PvadMcpContentRepository } from './src/core/content.js';
import { createSchemaRegistry } from './src/core/validation.js';

function assert(cond: unknown, msg: string) {
  if (!cond) { console.error(`FAIL: ${msg}`); process.exit(1); }
  console.log(`ok: ${msg}`);
}
const firstText = (r: any): string => r.content?.find((c: any) => c.type === 'text')?.text ?? '';
const data = (r: any): Record<string, any> => r.structuredContent?.data ?? JSON.parse(firstText(r)).data;

const useDistribution = process.argv.includes('--dist');
const transport = new StdioClientTransport(useDistribution
  ? { command: process.execPath, args: ['dist/pvad-mcp.js'] }
  : { command: process.execPath, args: ['--import', 'tsx', 'src/index.ts'] });
const client = new Client({ name: 'smoke', version: '0.0.0' });
await client.connect(transport);

const listedTools = (await client.listTools()).tools;
const tools = listedTools.map((t) => t.name).sort();
assert(['get_access_context', 'list_elements', 'get_element_schema', 'get_example', 'search_docs', 'validate_design', 'render_preview', 'list_export_formats', 'list_migration_providers']
    .every((t) => tools.includes(t)), `all tools registered (${tools.join(', ')})`);
assert(listedTools.every((tool) => tool.outputSchema), 'all tools expose structured output schemas');
assert(listedTools.every((tool) => tool.annotations?.readOnlyHint === true), 'all current tools are annotated read-only');
assert(listedTools.every(tool => !/pxa|power\s?dox/i.test(`${tool.title ?? ''} ${tool.description ?? ''}`)),
  'tool display metadata uses current product names');

const list = data(await client.callTool({ name: 'list_elements', arguments: {} })).items;
assert(list.length === 38, `list_elements returns 38 elements`);

for (const element of list) {
  const documented = findElement(element.type)!;
  for (const surface of ['json', 'csharp'] as const) {
    const result = await client.callTool({ name: 'get_example', arguments: { type: element.type, surface } });
    const example = data(result).example;
    const matches = surface === 'json'
      ? example?.pages?.[0]?.elements?.[0]?.type === element.type
      : example === documented.csharpExample;
    assert(!result.isError && matches, `${element.type}: ${surface} example retains its documented result`);
  }
}

const textDoc = data(await client.callTool({ name: 'get_element_schema', arguments: { type: 'text' } })).element;
assert(textDoc.type === 'text' && Array.isArray(textDoc.properties), `get_element_schema("text") returns a doc`);

const example = data(await client.callTool({ name: 'get_example', arguments: { type: 'table', surface: 'json' } })).example;
assert(example.pages?.[0]?.elements?.[0]?.type === 'table', `get_example("table") returns a DesignExportDto`);

const defaultExample = data(await client.callTool({ name: 'get_example', arguments: { type: 'table' } })).example;
assert(defaultExample.pages?.[0]?.elements?.[0]?.type === 'table', 'Zod default selects the JSON example surface');

const category = list[0].category;
const filtered = data(await client.callTool({ name: 'list_elements', arguments: { category } })).items;
assert(filtered.length > 0 && filtered.every((item: any) => item.category === category), 'Zod enum accepts a catalog category');

async function assertInvalidArguments(name: string, args: Record<string, unknown>) {
  let rejected = false;
  try {
    const result = await client.callTool({ name, arguments: args });
    rejected = result.isError === true;
  } catch (error) {
    rejected = error instanceof Error && /invalid|validation/i.test(error.message);
  }
  assert(rejected, `${name} rejects invalid arguments`);
}
await assertInvalidArguments('list_elements', { category: 'not-a-category' });
await assertInvalidArguments('get_example', { type: 'table', surface: 'unsupported' });
await assertInvalidArguments('search_docs', { query: 'text', limit: 0 });

const search = data(await client.callTool({ name: 'search_docs', arguments: { query: 'text' } }));
assert(Array.isArray(search.hits) && search.hits.length > 0, 'Zod default search limit executes successfully');

const validRes = await client.callTool({ name: 'validate_design', arguments: { design: JSON.stringify(example) } });
assert(data(validRes).valid === true && validRes.isError !== true, `validate_design accepts a catalog example`);

const badRes = await client.callTool({
  name: 'validate_design',
  arguments: { design: JSON.stringify({ id: 'x', name: 'y', pages: [{ id: 'p', elements: [{ id: 'e', type: 'bogus', x: 0, y: 0, width: 1, height: 1 }] }] }) },
});
assert(badRes.isError === true && data(badRes).valid === false, `validate_design rejects an unknown element type`);

const listedResources = (await client.listResources()).resources;
assert(listedResources.every(resource => !/pxa|power\s?dox/i.test(resource.title ?? '')
  && !/pxa|power\s?dox/i.test(resource.description ?? '')), 'resource display metadata uses current product names');
const resources = listedResources.map((r) => r.uri);
assert(resources.includes('pvad://schema/design-export'), `PVAD design-schema resource is exposed`);
assert(resources.includes('pvad://schema/pvad-workbook'), `PVAD workbook-schema resource is exposed`);
for (const resource of listedResources) {
  const result = await client.readResource({ uri: resource.uri });
  const kind = resource.uri.startsWith('pvad:') ? 'primary' : 'compatibility alias';
  assert(result.contents.some((item) => 'text' in item && item.text.length > 0),
    `${resource.title} (${kind}) contains packaged content`);
}

await client.close();
if (useDistribution) {
  const content = new PvadMcpContentRepository(fileURLToPath(new URL('./dist/resources/', import.meta.url)));
  const registry = createSchemaRegistry(content);
  const manifest = content.readJson('docs/schema/schema-aliases.json') as {
    schemas: Array<{ canonicalId: string; legacyIds: string[] }>;
  };
  for (const [schemaIndex, schema] of manifest.schemas.entries()) {
    for (const [aliasIndex, id] of [schema.canonicalId, ...schema.legacyIds].entries())
      assert(registry.getSchema(id),
        `offline packaged schema ${schemaIndex + 1} resolves (${aliasIndex === 0 ? 'primary' : `compatibility alias ${aliasIndex}`})`);
  }
}
console.log(`\nMCP ${useDistribution ? 'distribution ' : ''}smoke test passed.`);
process.exit(0);
