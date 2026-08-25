import assert from 'node:assert/strict';
import test from 'node:test';

import { PxaApiClient } from './apiClient.js';

test('capability client sends its internal credential and validates export formats', async () => {
  let observedAuthorization: string | null = null;
  let observedCorrelationId: string | null = null;
  const client = new PxaApiClient({
    apiUrl: 'http://api.example.test',
    apiKey: 'internal-key',
    correlationId: 'mcp-request-123',
    fetchImplementation: (async (_input, init) => {
      const headers = new Headers(init?.headers);
      observedAuthorization = headers.get('authorization');
      observedCorrelationId = headers.get('x-request-id');
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
    }) as typeof fetch,
  });

  const result = await client.listExportFormats();
  assert.equal(observedAuthorization, 'Bearer internal-key');
  assert.equal(observedCorrelationId, 'mcp-request-123');
  assert.equal(result.data?.[0].key, 'pdf');
  assert.equal(result.diagnostic, undefined);
});

test('capability client rejects a response that does not match the API contract', async () => {
  const client = new PxaApiClient({
    apiUrl: 'http://api.example.test',
    fetchImplementation: (async () => Response.json([{ key: 'pdf' }])) as typeof fetch,
  });

  const result = await client.listExportFormats();
  assert.equal(result.data, undefined);
  assert.equal(result.diagnostic?.code, 'PXAMCP044');
});

test('template client delegates the caller token and validates tenant-scoped results', async () => {
  let observedAuthorization: string | null = null;
  let observedUrl = '';
  const client = new PxaApiClient({
    apiUrl: 'http://api.example.test',
    apiKey: 'must-not-be-used',
    delegationSecret: 'test-delegation-secret-at-least-32-bytes',
    fetchImplementation: (async (input, init) => {
      observedUrl = String(input);
      observedAuthorization = new Headers(init?.headers).get('authorization');
      return Response.json({
        items: [{
          id: 'ce2af823-2c7c-4bf8-91bc-d07e2ebd1799',
          name: 'Invoice',
          description: null,
          tags: ['invoice'],
          status: 'Draft',
          revision: 2,
          updatedAt: '2026-08-23T18:00:00Z',
        }],
        page: 2,
        pageSize: 10,
        total: 11,
      });
    }) as typeof fetch,
  });

  const result = await client.listTemplates({
    subject: 'ce2af823-2c7c-4bf8-91bc-d07e2ebd1799',
    organizationId: '9daec2bd-0e7c-4a1f-a6ed-493b08e74e70',
    scopes: ['pxa.templates.read'],
  }, {
    search: 'Invoice',
    page: 2,
    pageSize: 10,
  });
  assert.match(observedAuthorization ?? '', /^PXA-MCP /);
  assert.doesNotMatch(observedAuthorization ?? '', /caller-token/);
  assert.match(observedUrl, /search=Invoice/);
  assert.equal(result.data?.items[0].name, 'Invoice');
  assert.equal(result.data?.total, 11);
});
