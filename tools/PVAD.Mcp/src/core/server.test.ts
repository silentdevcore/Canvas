import assert from 'node:assert/strict';
import { unlink } from 'node:fs/promises';
import test from 'node:test';

import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';

import { createExample, elementCatalog, findElement, PvadMcpContentRepository } from './content.js';
import { toolResult } from './results.js';
import { createPvadMcpServer, type PvadMcpServerOptions } from './server.js';

async function withClient(
  options: PvadMcpServerOptions,
  action: (client: Client) => Promise<void>,
): Promise<void> {
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  const server = createPvadMcpServer(options);
  const client = new Client({ name: 'pvad-mcp-test', version: '1.0.0' });
  await Promise.all([server.connect(serverTransport), client.connect(clientTransport)]);
  try {
    await action(client);
  } finally {
    await client.close();
    await server.close();
  }
}

test('canonical resources and deprecated resource aliases remain readable', async () => {
  await withClient({}, async (client) => {
    const resources = (await client.listResources()).resources;
    assert.equal(resources.length, 10);
    assert.ok(resources.every(resource => !/pxa|power\s?dox/i.test(resource.title ?? '')));
    assert.ok(resources.every(resource => !/pxa|power\s?dox/i.test(resource.description ?? '')));
    for (const uri of [
      'pvad://schema/design-export', 'pvad://schema/pvad-workbook', 'pvad://openapi',
      'pvad://docs/llms-full', 'pvad://docs/cookbook',
      'pxa://schema/design-export', 'pxa://schema/pxa-workbook', 'pxa://openapi',
      'pxa://docs/llms-full', 'pxa://docs/cookbook',
    ]) {
      assert.ok(resources.some((resource) => resource.uri === uri), uri);
      const result = await client.readResource({ uri });
      assert.ok(result.contents.some((item) => 'text' in item && item.text.length > 0), uri);
    }
  });
});

test('every catalog element exposes current metadata and exact JSON/CSharp source examples', async () => {
  await withClient({}, async client => {
    const tools = (await client.listTools()).tools;
    for (const tool of tools)
      assert.doesNotMatch(`${tool.title ?? ''} ${tool.description ?? ''}`, /pxa|power\s?dox/i, tool.name);
    for (const element of elementCatalog) {
      const schema = await client.callTool({ name: 'get_element_schema', arguments: { type: element.type } });
      assert.deepEqual((schema.structuredContent as { data: { element: unknown } }).data.element, element);
      assert.doesNotMatch(JSON.stringify(element), /pxa|power\s?dox/i, element.type);
      const json = await client.callTool({ name: 'get_example', arguments: { type: element.type, surface: 'json' } });
      const example = (json.structuredContent as { data: { example: unknown } }).data.example;
      assert.deepEqual(example, createExample(element.example, element.label));
      const csharp = await client.callTool({ name: 'get_example', arguments: { type: element.type, surface: 'csharp' } });
      const output = csharp.structuredContent as { data: { example?: unknown }; diagnostics: Array<{ code: string }> };
      if (element.csharpExample) {
        assert.equal(output.data.example, element.csharpExample);
        assert.deepEqual(output.diagnostics, []);
      } else {
        assert.equal(output.data.example, undefined);
        assert.equal(output.diagnostics[0].code, 'PXAMCP011');
      }
    }
  });
});

test('MCP structured results and text payloads preserve customer data and raw diagnostics', () => {
  const data = { name: 'PXA supplied title', namespace: 'PXA.Customer', file: 'PXA customer.pdf' };
  const diagnostics = [{ code: 'PXAMCP010', severity: 'error' as const, message: 'PXA supplied detail', path: '/name' }];
  const before = JSON.stringify({ data, diagnostics });
  const result = toolResult(data, diagnostics, true);
  assert.equal(result.structuredContent.data, data);
  assert.equal(result.structuredContent.diagnostics, diagnostics);
  assert.deepEqual(JSON.parse(result.content[0].text), result.structuredContent);
  assert.equal(result.isError, true);
  assert.equal(JSON.stringify({ data, diagnostics }), before);
});

test('resource display names are canonical while aliases return the exact original schema content', async () => {
  const content = new PvadMcpContentRepository();
  await withClient({}, async client => {
    const listed = (await client.listResources()).resources;
    for (const [uri, path] of [
      ['pvad://schema/pvad-workbook', 'docs/schema/pvad-workbook.schema.json'],
      ['pxa://schema/pxa-workbook', 'docs/schema/pxa-workbook.schema.json'],
      ['pvad://openapi', 'docs/schema/openapi.json'],
      ['pxa://openapi', 'docs/schema/openapi.json'],
    ]) {
      const resource = listed.find(value => value.uri === uri)!;
      assert.ok(resource);
      const result = await client.readResource({ uri });
      const entry = result.contents[0];
      assert.equal('text' in entry ? entry.text : undefined, content.read(path));
      if (uri.startsWith('pxa:')) assert.match(resource.description ?? '', /Deprecated compatibility resource/);
    }
    for (const uri of ['pvad://docs/llms-full', 'pxa://docs/llms-full'])
      assert.equal(listed.find(value => value.uri === uri)?.title, 'PVADE AI reference');
  });
});

test('tools return structured content and stable error diagnostics', async () => {
  await withClient({}, async (client) => {
    const list = await client.callTool({ name: 'list_elements', arguments: {} });
    assert.equal(list.isError, false);
    const listContent = list.structuredContent as { data: { total: number } };
    assert.equal(listContent.data.total, 38);

    const unknown = await client.callTool({
      name: 'get_element_schema',
      arguments: { type: 'not-an-element' },
    });
    assert.equal(unknown.isError, true);
    const diagnostics = (unknown.structuredContent as {
      diagnostics: Array<{ code: string }>;
    }).diagnostics;
    assert.equal(diagnostics[0].code, 'PXAMCP010');
  });
});

test('render preview accepts only a bounded PDF response', async () => {
  const table = findElement('table');
  assert.ok(table);
  const design = JSON.stringify(createExample(table.example, table.label));
  const fakeFetch = (async () => new Response(Buffer.from('%PDF-1.7\n%%EOF'), {
    status: 200,
    headers: { 'content-type': 'application/pdf' },
  })) as typeof fetch;

  await withClient({ fetchImplementation: fakeFetch }, async (client) => {
    const rendered = await client.callTool({ name: 'render_preview', arguments: { design } });
    assert.equal(rendered.isError, false);
    const data = (rendered.structuredContent as {
      data: { path: string; mimeType: string };
    }).data;
    assert.equal(data.mimeType, 'application/pdf');
    await unlink(data.path);
  });
});

test('render preview rejects a successful non-PDF response', async () => {
  const text = findElement('text');
  assert.ok(text);
  const design = JSON.stringify(createExample(text.example, text.label));
  const fakeFetch = (async () => new Response('{}', {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })) as typeof fetch;

  await withClient({ fetchImplementation: fakeFetch }, async (client) => {
    const rendered = await client.callTool({ name: 'render_preview', arguments: { design } });
    assert.equal(rendered.isError, true);
    const diagnostics = (rendered.structuredContent as {
      diagnostics: Array<{ code: string }>;
    }).diagnostics;
    assert.equal(diagnostics[0].code, 'PXAMCP021');
  });
});

test('read-only capability tools return validated PVAD WebApi metadata', async () => {
  const fakeFetch = (async (input) => {
    const url = String(input);
    if (url.endsWith('/api/export/formats')) {
      return Response.json([{
        key: 'pdf',
        mimeType: 'application/pdf',
        extension: '.pdf',
        supportsMultiPage: true,
        supportsImages: true,
        supportsRichText: true,
        supportsFormFields: true,
        multiPagePackaging: 'native',
      }]);
    }
    return Response.json([{
      id: 'devexpress-pdf',
      name: 'DevExpress PDF',
      status: 'full',
      description: 'Code migration provider.',
      kind: 'code',
      domain: 'pdf',
      migrationKind: 'code',
      provider: 'devexpress',
    }]);
  }) as typeof fetch;

  await withClient({ fetchImplementation: fakeFetch }, async (client) => {
    const formats = await client.callTool({ name: 'list_export_formats', arguments: {} });
    const providers = await client.callTool({ name: 'list_migration_providers', arguments: {} });
    assert.equal((formats.structuredContent as { data: { total: number } }).data.total, 1);
    assert.equal((providers.structuredContent as { data: { total: number } }).data.total, 1);
  });
});
