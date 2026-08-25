import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { z } from 'zod';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

import {
  PxaMcpContentRepository,
  categoryOrder,
  createExample,
  elementCatalog,
  findElement,
} from './content.js';
import { PxaApiClient } from './apiClient.js';
import { toolOutputSchema, toolResult } from './results.js';
import { DesignValidator } from './validation.js';

const MAX_PREVIEW_BYTES = 50 * 1024 * 1024;
const RENDER_TIMEOUT_MS = 30_000;
const SEARCH_FILES = ['llms-full.txt', 'docs/csharp-cookbook.md'] as const;

export interface PxaMcpServerOptions {
  apiUrl?: string;
  apiKey?: string;
  contentRoot?: string;
  fetchImplementation?: typeof fetch;
  delegationSecret?: string;
  correlationId?: string;
}

export function createPxaMcpServer(options: PxaMcpServerOptions = {}): McpServer {
  const apiUrl = (options.apiUrl ?? 'http://localhost:5086').replace(/\/$/, '');
  const content = new PxaMcpContentRepository(options.contentRoot);
  const validator = new DesignValidator(content.readJson('docs/schema/design-export.schema.json'));
  const fetchImplementation = options.fetchImplementation ?? fetch;
  const apiClient = new PxaApiClient({
    apiUrl,
    apiKey: options.apiKey,
    delegationSecret: options.delegationSecret,
    correlationId: options.correlationId,
    fetchImplementation,
  });
  const server = new McpServer({ name: 'pxa-mcp', version: readVersion(content) });

  server.registerTool('get_access_context', {
    title: 'Get access context',
    description: 'Return the effective MCP client, subject, organization, and granted scopes.',
    inputSchema: {},
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async (_arguments, extra) => toolResult({
    authenticated: Boolean(extra.authInfo),
    clientId: extra.authInfo?.clientId ?? null,
    scopes: extra.authInfo?.scopes ?? [],
    subject: extra.authInfo?.extra?.subject ?? null,
    organizationId: extra.authInfo?.extra?.organizationId ?? null,
  }));

  server.registerTool('list_elements', {
    title: 'List elements',
    description: 'List every PXA element type with category, description, and format support.',
    inputSchema: { category: z.enum(categoryOrder as [string, ...string[]]).optional() },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ category }) => {
    const items = elementCatalog
      .filter((element) => !category || element.category === category)
      .map((element) => ({
        type: element.type,
        label: element.label,
        category: element.category,
        description: element.description,
        formatSupport: element.formatSupport,
        bindable: element.bindable,
      }));
    return toolResult({ items, total: items.length });
  });

  server.registerTool('get_element_schema', {
    title: 'Get element schema',
    description: 'Return the documented properties, format support, and example for one element.',
    inputSchema: { type: z.string().trim().min(1).max(64) },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ type }) => {
    const element = findElement(type);
    if (!element) {
      return toolResult({}, [{
        code: 'PXAMCP010',
        severity: 'error',
        message: `Unknown element type "${type}".`,
        path: '/type',
      }], true);
    }
    return toolResult({ element });
  });

  server.registerTool('get_example', {
    title: 'Get example',
    description: 'Return a ready-to-use Design JSON or PXA-compatible C# example for one element.',
    inputSchema: {
      type: z.string().trim().min(1).max(64),
      surface: z.enum(['json', 'csharp']).default('json'),
    },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ type, surface }) => {
    const element = findElement(type);
    if (!element) {
      return toolResult({}, [{
        code: 'PXAMCP010',
        severity: 'error',
        message: `Unknown element type "${type}".`,
        path: '/type',
      }], true);
    }
    if (surface === 'csharp' && !element.csharpExample) {
      return toolResult({ type, surface }, [{
        code: 'PXAMCP011',
        severity: 'warning',
        message: `No C# example is available for "${type}". Use the JSON surface.`,
      }]);
    }
    return toolResult({
      type,
      surface,
      example: surface === 'csharp'
        ? element.csharpExample
        : createExample(element.example, element.label),
    });
  });

  server.registerTool('search_docs', {
    title: 'Search documentation',
    description: 'Search the bounded PXA AI reference and C# cookbook and return contextual hits.',
    inputSchema: {
      query: z.string().trim().min(2).max(200),
      limit: z.number().int().min(1).max(50).default(15),
    },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ query, limit }) => {
    const normalizedQuery = query.toLocaleLowerCase('en');
    const hits: Array<{ file: string; line: number; text: string }> = [];
    for (const file of SEARCH_FILES) {
      const lines = content.read(file).split('\n');
      for (let index = 0; index < lines.length && hits.length < limit; index += 1) {
        if (lines[index].toLocaleLowerCase('en').includes(normalizedQuery)) {
          hits.push({ file, line: index + 1, text: lines[index].trim().slice(0, 500) });
        }
      }
    }
    return toolResult({ query, hits, total: hits.length });
  });

  server.registerTool('validate_design', {
    title: 'Validate design',
    description: 'Validate DesignExportDto JSON against the complete canonical JSON Schema.',
    inputSchema: { design: z.string().min(1).max(10 * 1024 * 1024) },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ design }, extra) => {
    if (extra.authInfo && !extra.authInfo.scopes.includes('pxa.documents.validate')) {
      return toolResult({ valid: false }, [{
        code: 'PXAMCP030',
        severity: 'error',
        message: 'The pxa.documents.validate scope is required for remote design validation.',
      }], true);
    }
    const validation = validator.validateJson(design);
    return toolResult({ valid: validation.valid }, validation.diagnostics, !validation.valid);
  });

  server.registerTool('render_preview', {
    title: 'Render preview',
    description: 'Validate DesignExportDto JSON and render it to PDF through PXA WebApi.',
    inputSchema: { design: z.string().min(1).max(10 * 1024 * 1024) },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ design }, extra) => {
    if (extra.authInfo) {
      return toolResult({ rendered: false }, [{
        code: 'PXAMCP032',
        severity: 'error',
        message: 'Remote preview rendering is unavailable until tenant-bound expiring artifact resources are enabled.',
      }], true);
    }
    const validation = validator.validateJson(design);
    if (!validation.valid) {
      return toolResult({ rendered: false }, validation.diagnostics, true);
    }

    const abortController = new AbortController();
    const timeout = setTimeout(() => abortController.abort(), RENDER_TIMEOUT_MS);
    try {
      const headers: Record<string, string> = { 'Content-Type': 'application/json' };
      if (options.apiKey) headers.Authorization = `Bearer ${options.apiKey}`;
      const response = await fetchImplementation(`${apiUrl}/api/templates/render-design`, {
        method: 'POST',
        headers,
        body: design,
        signal: abortController.signal,
      });
      if (!response.ok) {
        return toolResult({ rendered: false, httpStatus: response.status }, [{
          code: 'PXAMCP020',
          severity: 'error',
          message: `PXA WebApi rejected the render request with HTTP ${response.status}.`,
        }], true);
      }

      const contentType = response.headers.get('content-type')?.split(';')[0].trim().toLowerCase();
      const declaredLength = Number(response.headers.get('content-length') ?? 0);
      if (contentType !== 'application/pdf' || declaredLength > MAX_PREVIEW_BYTES) {
        return toolResult({ rendered: false }, [{
          code: 'PXAMCP021',
          severity: 'error',
          message: 'PXA WebApi returned an invalid or oversized preview response.',
        }], true);
      }

      const bytes = Buffer.from(await response.arrayBuffer());
      if (bytes.length > MAX_PREVIEW_BYTES || !bytes.subarray(0, 5).equals(Buffer.from('%PDF-'))) {
        return toolResult({ rendered: false }, [{
          code: 'PXAMCP021',
          severity: 'error',
          message: 'PXA WebApi returned bytes that are not a valid bounded PDF preview.',
        }], true);
      }

      const outputPath = path.join(os.tmpdir(), `pxa-preview-${crypto.randomUUID()}.pdf`);
      fs.writeFileSync(outputPath, bytes, { flag: 'wx', mode: 0o600 });
      return toolResult({ rendered: true, path: outputPath, bytes: bytes.length, mimeType: 'application/pdf' });
    } catch (error) {
      const timedOut = abortController.signal.aborted;
      return toolResult({ rendered: false }, [{
        code: timedOut ? 'PXAMCP022' : 'PXAMCP023',
        severity: 'error',
        message: timedOut
          ? `Preview rendering exceeded ${RENDER_TIMEOUT_MS} milliseconds.`
          : `Preview rendering failed: ${(error as Error).message}`,
      }], true);
    } finally {
      clearTimeout(timeout);
    }
  });

  server.registerTool('list_export_formats', {
    title: 'List export formats',
    description: 'List export formats and their current PXA WebApi capabilities.',
    inputSchema: {},
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async () => {
    const result = await apiClient.listExportFormats();
    if (!result.data) return toolResult({}, [result.diagnostic!], true);
    return toolResult({ items: result.data, total: result.data.length });
  });

  server.registerTool('list_migration_providers', {
    title: 'List migration providers',
    description: 'List migration providers and their current domain, kind, and coverage status.',
    inputSchema: {},
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async () => {
    const result = await apiClient.listMigrationProviders();
    if (!result.data) return toolResult({}, [result.diagnostic!], true);
    return toolResult({ items: result.data, total: result.data.length });
  });

  server.registerTool('list_templates', {
    title: 'List templates',
    description: 'List Designer templates belonging to the authenticated active organization.',
    inputSchema: {
      search: z.string().trim().min(1).max(200).optional(),
      tag: z.string().trim().min(1).max(80).optional(),
      archived: z.boolean().default(false),
      page: z.number().int().min(1).default(1),
      pageSize: z.number().int().min(1).max(100).default(25),
    },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async (filters, extra) => {
    if (!extra.authInfo?.scopes.includes('pxa.templates.read')) {
      return toolResult({}, [{
        code: 'PXAMCP030',
        severity: 'error',
        message: 'The pxa.templates.read scope is required to list organization templates.',
      }], true);
    }
    const result = await apiClient.listTemplates({
      subject: String(extra.authInfo.extra?.subject),
      organizationId: String(extra.authInfo.extra?.organizationId),
      scopes: extra.authInfo.scopes,
    }, filters);
    if (!result.data) return toolResult({}, [result.diagnostic!], true);
    return toolResult(result.data);
  });

  server.registerTool('get_template', {
    title: 'Get template',
    description: 'Get one Designer template from the authenticated active organization.',
    inputSchema: { id: z.string().uuid() },
    outputSchema: toolOutputSchema,
    annotations: readOnlyAnnotations,
  }, async ({ id }, extra) => {
    if (!extra.authInfo?.scopes.includes('pxa.templates.read')) {
      return toolResult({}, [{
        code: 'PXAMCP030',
        severity: 'error',
        message: 'The pxa.templates.read scope is required to read organization templates.',
      }], true);
    }
    const result = await apiClient.getTemplate(id, {
      subject: String(extra.authInfo.extra?.subject),
      organizationId: String(extra.authInfo.extra?.organizationId),
      scopes: extra.authInfo.scopes,
    });
    if (!result.data) return toolResult({}, [result.diagnostic!], true);
    return toolResult({ template: result.data });
  });

  registerResources(server, content);
  return server;
}

const readOnlyAnnotations = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
  openWorldHint: false,
} as const;

function readVersion(content: PxaMcpContentRepository): string {
  try {
    return content.read('VERSION').trim();
  } catch {
    return '0.0.0';
  }
}

function registerResources(server: McpServer, content: PxaMcpContentRepository): void {
  const resources = [
    ['pxa-design-schema', 'pxa://schema/design-export', 'DesignExportDto JSON Schema', 'docs/schema/design-export.schema.json', 'application/json'],
    ['pxa-workbook-schema', 'pxa://schema/pxa-workbook', 'PXA Workbook JSON Schema', 'docs/schema/pxa-workbook.schema.json', 'application/json'],
    ['pxa-openapi', 'pxa://openapi', 'PXA OpenAPI', 'docs/schema/openapi.json', 'application/json'],
    ['pxa-llms-full', 'pxa://docs/llms-full', 'PXA AI reference', 'llms-full.txt', 'text/markdown'],
    ['pxa-cookbook', 'pxa://docs/cookbook', 'PXA PDF C# Cookbook', 'docs/csharp-cookbook.md', 'text/markdown'],
  ] as const;

  for (const [name, uri, title, relativePath, mimeType] of resources) {
    server.registerResource(name, uri, {
      title,
      description: `Versioned PXA content from ${relativePath}.`,
      mimeType,
    }, async (resourceUri) => ({
      contents: [{ uri: resourceUri.href, text: content.read(relativePath), mimeType }],
    }));
  }
}
