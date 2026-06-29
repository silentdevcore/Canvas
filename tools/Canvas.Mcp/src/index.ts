/**
 * Canvas MCP server — exposes the element catalog, design JSON Schema, docs, and validate/render tools
 * to AI agents over the Model Context Protocol. The element catalog (ui-designer-v2/src/docs/elementCatalog.ts)
 * is the single source of truth; this server imports it directly (run via tsx, which erases the type-only
 * import of ElementType). Schema/OpenAPI/llms-full/cookbook are served as resources from the repo.
 *
 * Run:  CANVAS_API_URL=http://localhost:5086 npx tsx src/index.ts   (stdio transport)
 */
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  ELEMENT_CATALOG, getElementDoc, toDesign, elementsByCategory, CATEGORY_ORDER,
} from '../../../ui-designer-v2/src/docs/elementCatalog.ts';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(HERE, '../../..');
const API_URL = process.env.CANVAS_API_URL ?? 'http://localhost:5086';

const read = (rel: string) => fs.readFileSync(path.join(REPO_ROOT, rel), 'utf8');
const schema = JSON.parse(read('docs/schema/design-export.schema.json'));

const text = (s: string) => ({ content: [{ type: 'text' as const, text: s }] });
const json = (o: unknown) => text(JSON.stringify(o, null, 2));

const server = new McpServer({ name: 'canvas-mcp', version: '0.1.0' });

// ── Tools ──────────────────────────────────────────────────────────────────────────────────────────

server.registerTool(
  'list_elements',
  {
    title: 'List elements',
    description: 'List every Canvas element type with its category, description, and format support.',
    inputSchema: { category: z.enum(CATEGORY_ORDER as [string, ...string[]]).optional() },
  },
  async ({ category }) => {
    const items = (category ? ELEMENT_CATALOG.filter((e) => e.category === category) : ELEMENT_CATALOG)
      .map((e) => ({ type: e.type, label: e.label, category: e.category, description: e.description, formatSupport: e.formatSupport, bindable: e.bindable }));
    return json(items);
  },
);

server.registerTool(
  'get_element_schema',
  {
    title: 'Get element schema',
    description: 'Full documentation for one element type: properties (name/type/allowed values/default), format support, and an example.',
    inputSchema: { type: z.string().describe('The element type, e.g. "text" or "table".') },
  },
  async ({ type }) => {
    const doc = getElementDoc(type as any);
    if (!doc) return text(`Unknown element type "${type}". Use list_elements to see valid types.`);
    return json(doc);
  },
);

server.registerTool(
  'get_example',
  {
    title: 'Get example',
    description: 'A ready-to-use example for an element. surface="json" returns a complete DesignExportDto; surface="csharp" returns a Canvas.Pdf C# snippet (when available).',
    inputSchema: {
      type: z.string(),
      surface: z.enum(['json', 'csharp']).default('json'),
    },
  },
  async ({ type, surface }) => {
    const doc = getElementDoc(type as any);
    if (!doc) return text(`Unknown element type "${type}".`);
    if (surface === 'csharp') return text(doc.csharpExample ?? `No C# example for "${type}" — use the design JSON surface.`);
    return json(toDesign(doc.example, doc.label));
  },
);

server.registerTool(
  'search_docs',
  {
    title: 'Search docs',
    description: 'Full-text search across the AI reference (llms-full.txt) and the C# cookbook. Returns matching lines with context.',
    inputSchema: { query: z.string(), limit: z.number().int().min(1).max(50).default(15) },
  },
  async ({ query, limit }) => {
    const q = query.toLowerCase();
    const hits: string[] = [];
    for (const file of ['llms-full.txt', 'docs/csharp-cookbook.md']) {
      const lines = read(file).split('\n');
      lines.forEach((line, i) => {
        if (hits.length < limit && line.toLowerCase().includes(q)) {
          hits.push(`${file}:${i + 1}: ${line.trim()}`);
        }
      });
    }
    return text(hits.length ? hits.join('\n') : `No matches for "${query}".`);
  },
);

// Structural validation against the committed JSON Schema (required fields + element type enum). Matches
// the rules enforced by ui-designer-v2/src/__tests__/designSchema.test.ts (no external validator needed).
function validateDesign(design: any): string[] {
  const errors: string[] = [];
  const enumTypes: string[] = schema.$defs.element.properties.type.enum;
  const need = (obj: any, keys: string[], where: string) => {
    if (obj == null || typeof obj !== 'object') { errors.push(`${where}: expected an object`); return; }
    for (const k of keys) if (!(k in obj)) errors.push(`${where}: missing required "${k}"`);
  };
  need(design, schema.required, 'design');
  if (!Array.isArray(design?.pages)) { errors.push('design.pages: expected an array'); return errors; }
  design.pages.forEach((page: any, pi: number) => {
    need(page, schema.$defs.page.required, `pages[${pi}]`);
    (page?.elements ?? []).forEach((el: any, ei: number) => {
      const at = `pages[${pi}].elements[${ei}]`;
      need(el, schema.$defs.element.required, at);
      if (el && !enumTypes.includes(el.type)) errors.push(`${at}: invalid type "${el?.type}"`);
      for (const d of ['x', 'y', 'width', 'height']) if (el && typeof el[d] !== 'number') errors.push(`${at}.${d}: expected a number`);
    });
  });
  return errors;
}

server.registerTool(
  'validate_design',
  {
    title: 'Validate design',
    description: 'Validate a DesignExportDto (JSON string) against the design schema. Returns "valid" or the list of problems.',
    inputSchema: { design: z.string().describe('The DesignExportDto as a JSON string.') },
  },
  async ({ design }) => {
    let parsed: any;
    try { parsed = JSON.parse(design); } catch (e) { return text(`Invalid JSON: ${(e as Error).message}`); }
    const errors = validateDesign(parsed);
    return text(errors.length ? `Invalid:\n- ${errors.join('\n- ')}` : 'valid');
  },
);

server.registerTool(
  'render_preview',
  {
    title: 'Render preview',
    description: `Validate a DesignExportDto then render it to PDF via the Canvas backend (${API_URL}). Writes the PDF to a temp file and returns its path + size.`,
    inputSchema: { design: z.string().describe('The DesignExportDto as a JSON string.') },
  },
  async ({ design }) => {
    let parsed: any;
    try { parsed = JSON.parse(design); } catch (e) { return text(`Invalid JSON: ${(e as Error).message}`); }
    const errors = validateDesign(parsed);
    if (errors.length) return text(`Not rendered — design is invalid:\n- ${errors.join('\n- ')}`);
    try {
      const res = await fetch(`${API_URL}/api/templates/render-design`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: design,
      });
      if (!res.ok) return text(`Render failed: HTTP ${res.status}`);
      const bytes = Buffer.from(await res.arrayBuffer());
      const out = path.join(os.tmpdir(), `canvas-preview-${Date.now()}.pdf`);
      fs.writeFileSync(out, bytes);
      return text(`Rendered ${bytes.length} bytes → ${out}`);
    } catch (e) {
      return text(`Render failed — is the backend running at ${API_URL}? (${(e as Error).message})`);
    }
  },
);

// ── Resources ──────────────────────────────────────────────────────────────────────────────────────

server.registerResource('design-schema', 'canvas://schema/design-export', {
  title: 'DesignExportDto JSON Schema', description: 'Validate a design before rendering.', mimeType: 'application/json',
}, async (uri) => ({ contents: [{ uri: uri.href, text: read('docs/schema/design-export.schema.json') }] }));

server.registerResource('workbook-schema', 'canvas://schema/canvas-workbook', {
  title: 'Canvas Workbook JSON Schema', description: 'Validate a spreadsheet workbook before posting to /api/spreadsheet/*.', mimeType: 'application/json',
}, async (uri) => ({ contents: [{ uri: uri.href, text: read('docs/schema/canvas-workbook.schema.json') }] }));

server.registerResource('openapi', 'canvas://openapi', {
  title: 'Canvas OpenAPI', description: 'Full HTTP API.', mimeType: 'application/json',
}, async (uri) => ({ contents: [{ uri: uri.href, text: read('docs/schema/openapi.json') }] }));

server.registerResource('llms-full', 'canvas://docs/llms-full', {
  title: 'Canvas AI reference', description: 'Capability map + all elements + examples.', mimeType: 'text/markdown',
}, async (uri) => ({ contents: [{ uri: uri.href, text: read('llms-full.txt') }] }));

server.registerResource('cookbook', 'canvas://docs/cookbook', {
  title: 'Canvas.Pdf C# Cookbook', description: 'Task-oriented C# recipes.', mimeType: 'text/markdown',
}, async (uri) => ({ contents: [{ uri: uri.href, text: read('docs/csharp-cookbook.md') }] }));

// ── Start ──────────────────────────────────────────────────────────────────────────────────────────

await server.connect(new StdioServerTransport());
console.error(`canvas-mcp ready — ${ELEMENT_CATALOG.length} elements, API ${API_URL}`);
