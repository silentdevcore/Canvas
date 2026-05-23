import { DesignerElement, ValidationConfig } from '../store';
import { validateExpression } from './expressionEngine';

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
  element: DesignerElement,
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

  // Validate table configuration
  if (element.table) {
    validateTable(element, samplePayload, result);
  }

  // Validate image configuration
  if (element.image) {
    validateImage(element, samplePayload, result);
  }

  // Add debug diagnostic if debug label is set
  if (element.validation?.debugLabel) {
    result.diagnostics.push({
      elementId: element.id,
      type: 'debug',
      message: `Debug: ${element.validation.debugLabel}`,
      data: {
        elementType: element.type,
        position: { x: element.x, y: element.y },
        props: element.props
      }
    });
  }

  // Update element's preflight status
  updatePreflightStatus(element, result);

  return result;
}

/**
 * Validates binding configuration
 */
function validateBinding(
  element: DesignerElement,
  samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const binding = element.binding!;
  const elementId = element.id;

  // Check if data path exists
  if (binding.dataPath) {
    try {
      const resolvedValue = resolveBindingValue(binding.dataPath, samplePayload, undefined);
      if (resolvedValue === undefined) {
        const isStrict = element.validation?.elementValidationMode === 'strict';
        if (isStrict) {
          result.errors.push({
            elementId,
            type: 'missing-path' as const,
            message: `Data path "${binding.dataPath}" not found in sample payload`,
            path: binding.dataPath,
            severity: 'error' as const
          });
          result.isValid = false;
        } else {
          result.warnings.push({
            elementId,
            type: 'missing-path' as const,
            message: `Data path "${binding.dataPath}" not found in sample payload`,
            path: binding.dataPath,
            severity: 'warning' as const
          });
        }
      }
    } catch (error) {
      result.errors.push({
        elementId,
        type: 'binding-error',
        message: `Invalid binding path "${binding.dataPath}": ${error instanceof Error ? error.message : 'Unknown error'}`,
        path: binding.dataPath,
        severity: 'error'
      });
      result.isValid = false;
    }
  }

  // Check for required fields without fallback
  if (binding.required && !binding.fallbackValue && !binding.dataPath) {
    result.errors.push({
      elementId,
      type: 'binding-error',
      message: 'Required binding has no data path or fallback value',
      severity: 'error'
    });
    result.isValid = false;
  }

  // Check for missing fallback when data path might fail
  if (binding.dataPath && !binding.fallbackValue && !binding.required) {
    result.warnings.push({
      elementId,
      type: 'missing-fallback',
      message: `Consider adding a fallback value for binding path "${binding.dataPath}"`,
      path: binding.dataPath,
      severity: 'warning'
    });
  }
}

/**
 * Validates expression configuration
 */
function validateExpressions(
  element: DesignerElement,
  samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const expression = element.expression!;
  const elementId = element.id;

  const expressionsToValidate = [
    { key: 'visibleWhen', expression: expression.visibleWhen },
    { key: 'enabledWhen', expression: expression.enabledWhen },
    { key: 'valueExpression', expression: expression.valueExpression }
  ];

  // Also validate style expressions
  if (expression.styleExpression) {
    Object.entries(expression.styleExpression).forEach(([styleProp, expr]) => {
      if (expr) {
        expressionsToValidate.push({
          key: `style.${styleProp}`,
          expression: expr
        });
      }
    });
  }

  expressionsToValidate.forEach(({ key, expression: expr }) => {
    if (expr) {
      const validation = validateExpression(expr);

      if (!validation.isValid) {
        const isStrict = element.validation?.elementValidationMode === 'strict';
        if (isStrict) {
          result.errors.push({
            elementId,
            type: 'expression-error' as const,
            message: `Expression error in "${key}": ${validation.error}`,
            severity: 'error' as const
          });
          result.isValid = false;
        } else {
          result.warnings.push({
            elementId,
            type: 'expression-error' as const,
            message: `Expression error in "${key}": ${validation.error}`,
            severity: 'warning' as const
          });
        }
      }
    }
  });
}

/**
 * Validates table configuration
 */
function validateTable(
  element: DesignerElement,
  samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const table = element.table!;
  const elementId = element.id;

  // Validate data path
  if (table.tableDataPath) {
    try {
      const resolvedData = resolveBindingValue(table.tableDataPath, samplePayload, []);
      if (!Array.isArray(resolvedData)) {
        result.errors.push({
          elementId,
          type: 'type-mismatch',
          message: `Table data path "${table.tableDataPath}" must resolve to an array`,
          path: table.tableDataPath,
          severity: 'error'
        });
        result.isValid = false;
      }
    } catch (error) {
      result.errors.push({
        elementId,
        type: 'binding-error',
        message: `Invalid table data path "${table.tableDataPath}": ${error instanceof Error ? error.message : 'Unknown error'}`,
        path: table.tableDataPath,
        severity: 'error'
      });
      result.isValid = false;
    }
  }

  // Validate column configurations
  if (table.columns) {
    table.columns.forEach((column, index) => {
      if (column.dataPath && table.tableDataPath) {
        // For now, just check if the column path is valid relative to table data
        // In a real implementation, we'd validate against sample data
        if (!column.dataPath.trim()) {
          result.warnings.push({
            elementId,
            type: 'binding-error',
            message: `Column ${index + 1} has empty data path`,
            severity: 'warning'
          });
        }
      }
    });
  }
}

/**
 * Validates image configuration
 */
function validateImage(
  element: DesignerElement,
  samplePayload: Record<string, any>,
  result: ValidationResult
): void {
  const image = element.image!;
  const elementId = element.id;

  // Validate remote fetch policy
  if (image.remoteFetchPolicy?.allowlist) {
    // Check if any URLs in the element might violate the allowlist
    const imageSrc = element.props.src;
    if (imageSrc && imageSrc.startsWith('http')) {
      try {
        const url = new URL(imageSrc);
        const isAllowed = image.remoteFetchPolicy.allowlist.some(domain =>
          url.hostname === domain || url.hostname.endsWith('.' + domain)
        );

        if (!isAllowed) {
          result.warnings.push({
            elementId,
            type: 'performance-issue',
            message: `Image URL domain "${url.hostname}" is not in the allowlist`,
            severity: 'warning'
          });
        }
      } catch (error) {
        // Invalid URL, ignore for validation
      }
    }
  }
}

/**
 * Updates the element's preflight status based on validation results
 */
function updatePreflightStatus(element: DesignerElement, result: ValidationResult): void {
  if (!element.validation) return;

  element.validation.preflightStatus = {
    hasMissingPaths: result.errors.some(e => e.type === 'missing-path') || result.warnings.some(w => w.type === 'missing-path'),
    hasTypeErrors: result.errors.some(e => e.type === 'type-mismatch'),
    hasExpressionErrors: result.errors.some(e => e.type === 'expression-error') || result.warnings.some(w => w.type === 'expression-error'),
    lastValidated: new Date().toISOString()
  };
}

/**
 * Validates all elements in a design
 */
export function validateDesign(
  elements: Record<string, DesignerElement>,
  samplePayload: Record<string, any>
): ValidationResult {
  const allResults: ValidationResult[] = [];

  Object.values(elements).forEach(element => {
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