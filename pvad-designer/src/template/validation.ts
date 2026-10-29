import { SimpleElement, Template } from '../store'; // Adjusted to v2 store types; may need further adaptation
import { validateExpression } from './expressionEngine';

// Note: This is a port from ui-designer. Types like DesignerElement have been replaced with SimpleElement.
// ValidationConfig is not defined in v2 store; assuming it's part of element properties.

// Helper function to resolve data binding paths
function resolveBindingValue(dataPath: string | undefined, samplePayload: Record<string, any>, fallbackValue?: any): any {
  if (!dataPath) return fallbackValue;

  const pathParts = dataPath.split('.');
  let current = samplePayload;

  for (const part of pathParts) {
    if (current && typeof current === 'object' && part in current) {
      current = current[part];
    } else {
      return fallbackValue;
    }
  }

  return current !== undefined ? current : fallbackValue;
}

export interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
  diagnostics: ValidationDiagnostic[];
}

export interface ValidationError {
  elementId: string;
  type: 'missing-path' | 'type-mismatch' | 'expression-error' | 'binding-error';
  message: string;
  path?: string;
  severity: 'error';
}

export interface ValidationWarning {
  elementId: string;
  type: 'missing-fallback' | 'unused-binding' | 'performance-issue' | 'missing-path' | 'expression-error' | 'binding-error';
  message: string;
  path?: string;
  severity: 'warning';
}

export interface ValidationDiagnostic {
  elementId: string;
  type: 'info' | 'debug';
  message: string;
  data?: any;
}

/**
 * Validates a single element for binding, expression, and data issues
 */
export function validateElement(
  element: SimpleElement,
  samplePayload: Record<string, any>
): ValidationResult {
  const result: ValidationResult = {
    isValid: true,
    errors: [],
    warnings: [],
    diagnostics: []
  };

  // Validate binding configuration
  if (element.binding) {
    validateBinding(element, samplePayload, result);
  }

  // Validate expression configuration
  if (element.expression) {
    validateExpressions(element, samplePayload, result);
  }

  // Validate table configuration (adapt if needed for v2)
  // if (element.table) {
  //   validateTable(element, samplePayload, result);
  // }

  // Validate image configuration (adapt if needed)
  // if (element.image) {
  //   validateImage(element, samplePayload, result);
  // }

  // Add debug diagnostic if debug label is set (adapt for v2)
  // if (element.validation?.debugLabel) {
  //   result.diagnostics.push({
  //     elementId: element.id,
  //     type: 'debug',
  //     message: `Debug: ${element.validation.debugLabel}`,
  //     data: {
  //       elementType: element.type,
  //       position: { x: element.x, y: element.y },
  //       props: element.style // Adjusted
  //     }
  //   });
  // }

  // Update element's preflight status (adapt for v2)
  updatePreflightStatus(element, result);

  return result;
}

/**
 * Validates binding configuration
 */
function validateBinding(
  element: SimpleElement,
  samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const binding = element.binding!;
  const elementId = element.id;

  // Check if data path exists
  if (binding) {
    try {
      const resolvedValue = resolveBindingValue(binding, samplePayload, undefined);
      if (resolvedValue === undefined) {
        const isStrict = true; // Assuming strict for now; adjust as needed
        if (isStrict) {
          result.errors.push({
            elementId,
            type: 'missing-path' as const,
            message: `Data path "${binding}" not found in sample payload`,
            path: binding,
            severity: 'error' as const
          });
          result.isValid = false;
        } else {
          result.warnings.push({
            elementId,
            type: 'missing-path' as const,
            message: `Data path "${binding}" not found in sample payload`,
            path: binding,
            severity: 'warning' as const
          });
        }
      }
    } catch (error) {
      result.errors.push({
        elementId,
        type: 'binding-error',
        message: `Invalid binding path "${binding}": ${error instanceof Error ? error.message : 'Unknown error'}`,
        path: binding,
        severity: 'error'
      });
      result.isValid = false;
    }
  }

  // Additional binding validations can be added here
}

/**
 * Validates expression configuration
 */
function validateExpressions(
  element: SimpleElement,
  _samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const expression = element.expression!;
  const elementId = element.id;

  const expressionsToValidate = [
    { key: 'expression', expression } // Simplified for v2; expand as needed
  ];

  expressionsToValidate.forEach(({ key, expression: expr }) => {
    if (expr) {
      const validation = validateExpression(expr);
      if (!validation.isValid) {
        const isStrict = true; // Assuming strict
        if (isStrict) {
          result.errors.push({
            elementId,
            type: 'expression-error' as const,
            message: `Expression error in "${key}": ${validation.error || 'Invalid expression'}`,
            severity: 'error' as const
          });
          result.isValid = false;
        } else {
          result.warnings.push({
            elementId,
            type: 'expression-error' as const,
            message: `Expression error in "${key}": ${validation.error || 'Invalid expression'}`,
            severity: 'warning' as const
          });
        }
      }
    }
  });
}

/**
 * Updates the element's preflight status based on validation results
 */
function updatePreflightStatus(_element: SimpleElement, _result: ValidationResult): void {
  // Implement preflight status update if needed for v2
  // For example:
  // element.preflightStatus = {
  //   hasErrors: result.errors.length > 0,
  //   hasWarnings: result.warnings.length > 0
  // };
}

/**
 * Validates all elements in a template
 */
export function validateTemplate(
  template: Template,
  samplePayload: Record<string, any>
): ValidationResult {
  const allResults: ValidationResult[] = [];

  (template.pages?.flatMap((p: any) => p.elements) ?? []).forEach((element: any) => {
    const elementResult = validateElement(element, samplePayload);
    allResults.push(elementResult);
  });

  // Combine all results
  const combinedResult: ValidationResult = {
    isValid: allResults.every(r => r.isValid),
    errors: allResults.flatMap(r => r.errors),
    warnings: allResults.flatMap(r => r.warnings),
    diagnostics: allResults.flatMap(r => r.diagnostics)
  };

  return combinedResult;
}

/**
 * Gets a human-readable validation summary
 */
export function getValidationSummary(result: ValidationResult): string {
  const parts: string[] = [];

  if (result.errors.length > 0) {
    parts.push(`${result.errors.length} error${result.errors.length === 1 ? '' : 's'}`);
  }

  if (result.warnings.length > 0) {
    parts.push(`${result.warnings.length} warning${result.warnings.length === 1 ? '' : 's'}`);
  }

  if (result.diagnostics.length > 0) {
    parts.push(`${result.diagnostics.length} diagnostic${result.diagnostics.length === 1 ? '' : 's'}`);
  }

  if (parts.length === 0) {
    return 'No issues found';
  }

  return parts.join(', ');
}
