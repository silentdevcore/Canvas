import Ajv2020, { type ErrorObject, type ValidateFunction } from 'ajv/dist/2020.js';

import type { PxaMcpDiagnostic } from './results.js';

const MAX_DESIGN_BYTES = 10 * 1024 * 1024;

export interface DesignValidationResult {
  valid: boolean;
  design?: unknown;
  diagnostics: PxaMcpDiagnostic[];
}

export class DesignValidator {
  private readonly validate: ValidateFunction;

  constructor(schema: unknown) {
    const ajv = new Ajv2020({
      allErrors: true,
      strict: false,
      allowUnionTypes: true,
    });
    this.validate = ajv.compile(schema as object);
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

function toDiagnostic(error: ErrorObject): PxaMcpDiagnostic {
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
