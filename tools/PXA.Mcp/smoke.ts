/**
 * MCP smoke test: spawns the server over stdio, lists tools, and exercises the core tools
 * (list_elements, get_element_schema, get_example, validate_design with a valid + invalid design).
 * Run:  npx tsx smoke.ts
 */
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

function assert(cond: unknown, msg: string) {
  if (!cond) { console.error(`FAIL: ${msg}`); process.exit(1); }
  console.log(`ok: ${msg}`);
}
const firstText = (r: any): string => r.content?.find((c: any) => c.type === 'text')?.text ?? '';
const data = (r: any): Record<string, any> => r.structuredContent?.data ?? JSON.parse(firstText(r)).data;

const useDistribution = process.argv.includes('--dist');
const transport = new StdioClientTransport(useDistribution
  ? { command: 'node', args: ['dist/pxa-mcp.js'] }
  : { command: 'npx', args: ['tsx', 'src/index.ts'] });
const client = new Client({ name: 'smoke', version: '0.0.0' });
await client.connect(transport);

const listedTools = (await client.listTools()).tools;
const tools = listedTools.map((t) => t.name).sort();
assert(['get_access_context', 'list_elements', 'get_element_schema', 'get_example', 'search_docs', 'validate_design', 'render_preview', 'list_export_formats', 'list_migration_providers']
    .every((t) => tools.includes(t)), `all tools registered (${tools.join(', ')})`);
assert(listedTools.every((tool) => tool.outputSchema), 'all tools expose structured output schemas');
assert(listedTools.every((tool) => tool.annotations?.readOnlyHint === true), 'all current tools are annotated read-only');

const list = data(await client.callTool({ name: 'list_elements', arguments: {} })).items;
assert(list.length === 38, `list_elements returns 38 elements`);

const textDoc = data(await client.callTool({ name: 'get_element_schema', arguments: { type: 'text' } })).element;
assert(textDoc.type === 'text' && Array.isArray(textDoc.properties), `get_element_schema("text") returns a doc`);

const example = data(await client.callTool({ name: 'get_example', arguments: { type: 'table', surface: 'json' } })).example;
assert(example.pages?.[0]?.elements?.[0]?.type === 'table', `get_example("table") returns a DesignExportDto`);

const validRes = await client.callTool({ name: 'validate_design', arguments: { design: JSON.stringify(example) } });
assert(data(validRes).valid === true && validRes.isError !== true, `validate_design accepts a catalog example`);

const badRes = await client.callTool({
  name: 'validate_design',
  arguments: { design: JSON.stringify({ id: 'x', name: 'y', pages: [{ id: 'p', elements: [{ id: 'e', type: 'bogus', x: 0, y: 0, width: 1, height: 1 }] }] }) },
});
assert(badRes.isError === true && data(badRes).valid === false, `validate_design rejects an unknown element type`);

const resources = (await client.listResources()).resources.map((r) => r.uri);
assert(resources.includes('pxa://schema/design-export'), `PXA design-schema resource is exposed`);
assert(resources.includes('pxa://schema/pxa-workbook'), `PXA workbook-schema resource is exposed`);

await client.close();
console.log(`\nMCP ${useDistribution ? 'distribution ' : ''}smoke test passed.`);
process.exit(0);
