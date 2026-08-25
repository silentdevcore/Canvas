import assert from 'node:assert/strict';
import { unlink } from 'node:fs/promises';
import test from 'node:test';

import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';

import { createExample, findElement } from './content.js';
import { createPxaMcpServer, type PxaMcpServerOptions } from './server.js';

async function withClient(
  options: PxaMcpServerOptions,
  action: (client: Client) => Promise<void>,
): Promise<void> {
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  const server = createPxaMcpServer(options);
  const client = new Client({ name: 'pxa-mcp-test', version: '1.0.0' });
  await Promise.all([server.connect(serverTransport), client.connect(clientTransport)]);
  try {
    await action(client);
  } finally {
    await client.close();
    await server.close();
  }
}

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

test('read-only capability tools return validated PXA WebApi metadata', async () => {
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
