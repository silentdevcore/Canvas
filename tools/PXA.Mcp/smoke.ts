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

const transport = new StdioClientTransport({ command: 'npx', args: ['tsx', 'src/index.ts'] });
const client = new Client({ name: 'smoke', version: '0.0.0' });
await client.connect(transport);

const tools = (await client.listTools()).tools.map((t) => t.name).sort();
assert(['list_elements', 'get_element_schema', 'get_example', 'search_docs', 'validate_design', 'render_preview']
  .every((t) => tools.includes(t)), `all tools registered (${tools.join(', ')})`);

const list = JSON.parse(firstText(await client.callTool({ name: 'list_elements', arguments: {} })));
assert(list.length === 38, `list_elements returns 38 elements`);

const textDoc = JSON.parse(firstText(await client.callTool({ name: 'get_element_schema', arguments: { type: 'text' } })));
assert(textDoc.type === 'text' && Array.isArray(textDoc.properties), `get_element_schema("text") returns a doc`);

const example = JSON.parse(firstText(await client.callTool({ name: 'get_example', arguments: { type: 'table', surface: 'json' } })));
assert(example.pages?.[0]?.elements?.[0]?.type === 'table', `get_example("table") returns a DesignExportDto`);

const defaultExample = JSON.parse(firstText(await client.callTool({ name: 'get_example', arguments: { type: 'table' } })));
assert(defaultExample.pages?.[0]?.elements?.[0]?.type === 'table', 'Zod default selects the JSON example surface');

const category = list[0].category;
const filtered = JSON.parse(firstText(await client.callTool({ name: 'list_elements', arguments: { category } })));
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

const search = firstText(await client.callTool({ name: 'search_docs', arguments: { query: 'text' } }));
assert(search.includes('llms-full.txt:') || search.includes('docs/csharp-cookbook.md:'), 'Zod default search limit executes successfully');

const validRes = firstText(await client.callTool({ name: 'validate_design', arguments: { design: JSON.stringify(example) } }));
assert(validRes === 'valid', `validate_design accepts a catalog example`);

const badRes = firstText(await client.callTool({
  name: 'validate_design',
  arguments: { design: JSON.stringify({ id: 'x', name: 'y', pages: [{ id: 'p', elements: [{ id: 'e', type: 'bogus', x: 0, y: 0, width: 1, height: 1 }] }] }) },
}));
assert(badRes.includes('invalid type "bogus"'), `validate_design rejects an unknown element type`);

const resources = (await client.listResources()).resources.map((r) => r.uri);
assert(resources.includes('pxa://schema/design-export'), `PXA design-schema resource is exposed`);
assert(resources.includes('pxa://schema/pxa-workbook'), `PXA workbook-schema resource is exposed`);

await client.close();
console.log('\nMCP smoke test passed.');
process.exit(0);
