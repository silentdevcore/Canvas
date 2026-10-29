import Ajv2020, { type ErrorObject, type ValidateFunction } from 'ajv/dist/2020.js';

import type { PvadMcpDiagnostic } from './results.js';
import type { PvadMcpContentRepository } from './content.js';

const MAX_DESIGN_BYTES = 10 * 1024 * 1024;

export function createSchemaRegistry(content: PvadMcpContentRepository): Ajv2020 {
  const registry = new Ajv2020({ allErrors: true, strict: false, allowUnionTypes: true });
  const manifest = content.readJson('docs/schema/schema-aliases.json') as {
    schemaVersion: number; schemas: Array<{ canonicalId: string; path: string; legacyIds: string[] }>;
  };
  if (manifest.schemaVersion !== 1) throw new Error('Unsupported schema-alias manifest.');
  for (const entry of manifest.schemas) {
    const schema = content.readJson(entry.path) as { $id?: string };
    if (schema.$id !== entry.canonicalId) throw new Error('Schema identity does not match the alias manifest.');
    registry.addSchema(schema);
    // Register the same schema under each key so fragment references retain $defs.
    for (const alias of entry.legacyIds) registry.addSchema(schema, alias);
  }
  return registry;
}

export interface DesignValidationResult {
  valid: boolean;
  design?: unknown;
  diagnostics: PvadMcpDiagnostic[];
}

export class DesignValidator {
  private readonly validate: ValidateFunction;

  constructor(schema: unknown, registry?: Ajv2020) {
    const ajv = registry ?? new Ajv2020({
      allErrors: true,
      strict: false,
      allowUnionTypes: true,
    });
    const id = (schema as { $id?: string }).$id;
    this.validate = (id ? ajv.getSchema(id) : undefined) ?? ajv.compile(schema as object);
  }

  validateJson(source: string): DesignValidationResult {
    if (Buffer.byteLength(source, 'utf8') > MAX_DESIGN_BYTES) {
      return {
        valid: false,
        diagnostics: [{
          code: 'PXAMCP002',
          severity: 'error',
          message: `Design JSON exceeds the ${MAX_DESIGN_BYTES} byte limit.`,
        }],
      };
    }

    let design: unknown;
    try {
      design = JSON.parse(source);
    } catch (error) {
      return {
        valid: false,
        diagnostics: [{
          code: 'PXAMCP001',
          severity: 'error',
          message: `Invalid JSON: ${(error as Error).message}`,
        }],
      };
    }

    const valid = this.validate(design);
    return {
      valid,
      design,
      diagnostics: valid ? [] : (this.validate.errors ?? []).map(toDiagnostic),
    };
  }
}

function toDiagnostic(error: ErrorObject): PvadMcpDiagnostic {
  const property = typeof error.params === 'object' && error.params && 'missingProperty' in error.params
    ? `/${String(error.params.missingProperty)}`
    : '';
  return {
    code: 'PXAMCP003',
    severity: 'error',
    message: error.message ?? 'The design does not satisfy the canonical schema.',
    path: `${error.instancePath}${property}` || '/',
  };
}
