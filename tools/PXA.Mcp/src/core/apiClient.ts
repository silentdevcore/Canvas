import { z } from 'zod';
import crypto from 'node:crypto';

import type { PxaMcpDiagnostic } from './results.js';

const API_TIMEOUT_MS = 10_000;
const MAX_JSON_BYTES = 2 * 1024 * 1024;

const exportFormatSchema = z.object({
  key: z.string(),
  mimeType: z.string(),
  extension: z.string(),
  supportsMultiPage: z.boolean(),
  supportsImages: z.boolean(),
  supportsRichText: z.boolean(),
  supportsFormFields: z.boolean(),
  multiPagePackaging: z.string(),
}).passthrough();

const migrationProviderSchema = z.object({
  id: z.string(),
  name: z.string(),
  status: z.string(),
  description: z.string(),
  kind: z.string(),
  domain: z.string(),
  migrationKind: z.string(),
  provider: z.string(),
}).passthrough();

const templateSummarySchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().nullable(),
  tags: z.array(z.string()),
  status: z.string(),
  revision: z.number().int(),
  updatedAt: z.string(),
});

const templatePageSchema = z.object({
  items: z.array(templateSummarySchema),
  page: z.number().int(),
  pageSize: z.number().int(),
  total: z.number().int(),
});

const templateDocumentSchema = templateSummarySchema.extend({
  checksum: z.string(),
  design: z.unknown(),
});

export type ExportFormat = z.infer<typeof exportFormatSchema>;
export type MigrationProvider = z.infer<typeof migrationProviderSchema>;
export type TemplatePage = z.infer<typeof templatePageSchema>;
export type TemplateDocument = z.infer<typeof templateDocumentSchema>;

export interface PxaApiResult<T> {
  data?: T;
  diagnostic?: PxaMcpDiagnostic;
}

export interface PxaApiClientOptions {
  apiUrl: string;
  apiKey?: string;
  fetchImplementation?: typeof fetch;
  delegationSecret?: string;
  correlationId?: string;
}

export interface PxaDelegationContext {
  subject: string;
  organizationId: string;
  scopes: string[];
}

export class PxaApiClient {
  private readonly apiUrl: string;
  private readonly apiKey?: string;
  private readonly fetchImplementation: typeof fetch;
  private readonly delegationSecret?: string;
  private readonly correlationId?: string;

  constructor(options: PxaApiClientOptions) {
    this.apiUrl = options.apiUrl.replace(/\/$/, '');
    this.apiKey = options.apiKey;
    this.fetchImplementation = options.fetchImplementation ?? fetch;
    this.delegationSecret = options.delegationSecret;
    this.correlationId = options.correlationId;
  }

  listExportFormats(): Promise<PxaApiResult<ExportFormat[]>> {
    return this.getJson('/api/export/formats', z.array(exportFormatSchema));
  }

  listMigrationProviders(): Promise<PxaApiResult<MigrationProvider[]>> {
    return this.getJson('/api/migration/frameworks', z.array(migrationProviderSchema));
  }

  listTemplates(
    delegation: PxaDelegationContext,
    filters: { search?: string; tag?: string; archived?: boolean; page?: number; pageSize?: number },
  ): Promise<PxaApiResult<TemplatePage>> {
    const query = new URLSearchParams();
    if (filters.search) query.set('search', filters.search);
    if (filters.tag) query.set('tag', filters.tag);
    if (filters.archived) query.set('archived', 'true');
    if (filters.page) query.set('page', String(filters.page));
    if (filters.pageSize) query.set('pageSize', String(filters.pageSize));
    const suffix = query.size ? `?${query.toString()}` : '';
    return this.getJson(`/api/pxa/v1/mcp/templates${suffix}`, templatePageSchema, undefined, delegation);
  }

  getTemplate(id: string, delegation: PxaDelegationContext): Promise<PxaApiResult<TemplateDocument>> {
    return this.getJson(
      `/api/pxa/v1/mcp/templates/${encodeURIComponent(id)}`,
      templateDocumentSchema,
      undefined,
      delegation,
    );
  }

  private async getJson<T>(
    path: string,
    schema: z.ZodType<T>,
    accessToken?: string,
    delegation?: PxaDelegationContext,
  ): Promise<PxaApiResult<T>> {
    const abortController = new AbortController();
    const timeout = setTimeout(() => abortController.abort(), API_TIMEOUT_MS);
    try {
      const headers: Record<string, string> = { Accept: 'application/json' };
      if (this.correlationId) headers['X-Request-ID'] = this.correlationId;
      if (delegation) {
        if (!this.delegationSecret) {
          return { diagnostic: {
            code: 'PXAMCP045',
            severity: 'error',
            message: 'Tenant delegation to PXA WebApi is not configured.',
          } };
        }
        headers.Authorization = createDelegation(this.delegationSecret, delegation);
      } else if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
      else if (this.apiKey) headers.Authorization = `Bearer ${this.apiKey}`;
      const response = await this.fetchImplementation(`${this.apiUrl}${path}`, {
        method: 'GET',
        headers,
        signal: abortController.signal,
      });
      if (!response.ok) {
        return {
          diagnostic: {
            code: response.status === 401 || response.status === 403 ? 'PXAMCP041' : 'PXAMCP040',
            severity: 'error',
            message: response.status === 401 || response.status === 403
              ? 'PXA WebApi denied the internal capability request.'
              : `PXA WebApi capability request failed with HTTP ${response.status}.`,
          },
        };
      }

      const contentType = response.headers.get('content-type')?.split(';')[0].trim().toLowerCase();
      const declaredLength = Number(response.headers.get('content-length') ?? 0);
      if (contentType !== 'application/json' || declaredLength > MAX_JSON_BYTES) {
        return invalidResponse();
      }
      const bytes = Buffer.from(await response.arrayBuffer());
      if (bytes.length > MAX_JSON_BYTES) return invalidResponse();

      let parsed: unknown;
      try {
        parsed = JSON.parse(bytes.toString('utf8'));
      } catch {
        return invalidResponse();
      }
      const validated = schema.safeParse(parsed);
      return validated.success ? { data: validated.data } : invalidResponse();
    } catch {
      return {
        diagnostic: {
          code: abortController.signal.aborted ? 'PXAMCP042' : 'PXAMCP043',
          severity: 'error',
          message: abortController.signal.aborted
            ? `PXA WebApi capability request exceeded ${API_TIMEOUT_MS} milliseconds.`
            : 'PXA WebApi capability request could not be completed.',
        },
      };
    } finally {
      clearTimeout(timeout);
    }
  }
}

function createDelegation(secret: string, context: PxaDelegationContext): string {
  const now = Math.floor(Date.now() / 1000);
  const payload = Buffer.from(JSON.stringify({
    iss: 'pxa-mcp-server',
    aud: 'pxa-api',
    sub: context.subject,
    org: context.organizationId,
    scope: context.scopes,
    iat: now,
    exp: now + 30,
    jti: crypto.randomUUID(),
  })).toString('base64url');
  const signature = crypto.createHmac('sha256', secret).update(payload, 'ascii').digest('base64url');
  return `PXA-MCP ${payload}.${signature}`;
}

function invalidResponse<T>(): PxaApiResult<T> {
  return {
    diagnostic: {
      code: 'PXAMCP044',
      severity: 'error',
      message: 'PXA WebApi returned an invalid or oversized capability response.',
    },
  };
}
