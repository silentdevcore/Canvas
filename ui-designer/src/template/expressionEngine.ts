/**
 * Safe expression evaluation engine for template dynamic behavior
 * Provides controlled evaluation of JavaScript expressions with data binding context
 */

export interface ExpressionContext {
  data: Record<string, any>;
  element?: Record<string, any>;
  index?: number;
  parent?: Record<string, any>;
}

export interface ExpressionResult {
  value: any;
  error?: string;
  isValid: boolean;
}

/**
 * Safely evaluates a JavaScript expression with controlled context
 */
export function evaluateExpression(
  expression: string,
  context: ExpressionContext,
  options: { safeMode?: boolean } = {}
): ExpressionResult {
  const { safeMode = true } = options;

  if (!expression || typeof expression !== 'string') {
    return { value: undefined, isValid: false, error: 'Invalid expression' };
  }

  try {
    // Create a safe evaluation context
    const safeContext = createSafeContext(context);

    // For safe mode, use a restricted evaluation
    if (safeMode) {
      return evaluateSafeExpression(expression, safeContext);
    } else {
      // Direct evaluation (use with caution)
      return evaluateUnsafeExpression(expression, safeContext);
    }
  } catch (error) {
    return {
      value: undefined,
      isValid: false,
      error: error instanceof Error ? error.message : 'Evaluation error'
    };
  }
}

/**
 * Creates a safe context object for expression evaluation
 */
function createSafeContext(context: ExpressionContext): Record<string, any> {
  const { data, element, index, parent } = context;

  // Create a context object with safe property access
  const safeContext = {
    // Direct access to data properties for convenience
    ...data,

    // Data access
    $data: data,
    $root: data,

    // Element context
    $element: element || {},
    $this: element || {},

    // Loop context
    $index: index,
    $parent: parent,

    // Utility functions
    $length: (arr: any[]) => Array.isArray(arr) ? arr.length : 0,
    $isEmpty: (value: any) => {
      if (value == null) return true;
      if (Array.isArray(value)) return value.length === 0;
      if (typeof value === 'string') return value.trim().length === 0;
      if (typeof value === 'object') return Object.keys(value).length === 0;
      return false;
    },
    $format: (value: any, format?: string) => {
      if (value == null) return '';
      if (format === 'currency') return `$${Number(value).toFixed(2)}`;
      if (format === 'date') return new Date(value).toLocaleDateString();
      return String(value);
    },

    // Math utilities
    Math: {
      abs: Math.abs,
      ceil: Math.ceil,
      floor: Math.floor,
      max: Math.max,
      min: Math.min,
      round: Math.round,
      sqrt: Math.sqrt,
      pow: Math.pow,
    },

    // String utilities
    String: {
      toUpperCase: (s: string) => s?.toUpperCase() || '',
      toLowerCase: (s: string) => s?.toLowerCase() || '',
      trim: (s: string) => s?.trim() || '',
      substring: (s: string, start: number, end?: number) => s?.substring(start, end) || '',
      replace: (s: string, search: string | RegExp, replacement: string) => s?.replace(search, replacement) || '',
    },

    // Array utilities
    Array: {
      includes: (arr: any[], item: any) => Array.isArray(arr) && arr.includes(item),
      indexOf: (arr: any[], item: any) => Array.isArray(arr) ? arr.indexOf(item) : -1,
      join: (arr: any[], separator?: string) => Array.isArray(arr) ? arr.join(separator || ', ') : '',
      slice: (arr: any[], start?: number, end?: number) => Array.isArray(arr) ? arr.slice(start, end) : [],
      filter: (arr: any[], predicate: (item: any, index: number) => boolean) =>
        Array.isArray(arr) ? arr.filter(predicate) : [],
      map: (arr: any[], mapper: (item: any, index: number) => any) =>
        Array.isArray(arr) ? arr.map(mapper) : [],
      reduce: (arr: any[], reducer: (acc: any, item: any, index: number) => any, initial?: any) =>
        Array.isArray(arr) ? arr.reduce(reducer, initial) : initial,
    },

    // Date utilities
    Date: {
      now: () => Date.now(),
      format: (date: any) => {
        try {
          return new Date(date).toLocaleDateString();
        } catch {
          return '';
        }
      },
    },

    // Type checking
    $isString: (value: any) => typeof value === 'string',
    $isNumber: (value: any) => typeof value === 'number' && !isNaN(value),
    $isBoolean: (value: any) => typeof value === 'boolean',
    $isArray: (value: any) => Array.isArray(value),
    $isObject: (value: any) => value != null && typeof value === 'object' && !Array.isArray(value),
    $isDate: (value: any) => value instanceof Date || (!isNaN(Date.parse(value))),
  };

  return safeContext;
}

/**
 * Safely evaluates an expression using a more robust parser
 */
function evaluateSafeExpression(expression: string, context: Record<string, any>): ExpressionResult {
  const expr = expression.trim();

  // Handle literal values
  if (expr === 'true') return { value: true, isValid: true };
  if (expr === 'false') return { value: false, isValid: true };
  if (expr === 'null') return { value: null, isValid: true };
  if (expr === 'undefined') return { value: undefined, isValid: true };

  // Handle string literals
  if ((expr.startsWith('"') && expr.endsWith('"')) || (expr.startsWith("'") && expr.endsWith("'"))) {
    return { value: expr.slice(1, -1), isValid: true };
  }

  // Handle number literals
  const numValue = Number(expr);
  if (!isNaN(numValue) && expr !== '') {
    return { value: numValue, isValid: true };
  }

  // Handle template literals (basic support)
  if (expr.startsWith('`') && expr.endsWith('`')) {
    return evaluateTemplateLiteral(expr, context);
  }

  // Handle new expressions (limited support)
  if (expr.startsWith('new ')) {
    return evaluateNewExpression(expr, context);
  }

  // Handle instanceof
  if (expr.includes(' instanceof ')) {
    return evaluateInstanceof(expr, context);
  }

  // Handle comparisons and logical operations first
  const comparisonResult = evaluateComparison(expr, context);
  if (comparisonResult.isValid !== undefined) {
    return comparisonResult;
  }

  // Handle arithmetic operations
  const arithmeticResult = evaluateArithmetic(expr, context);
  if (arithmeticResult.isValid !== undefined) {
    return arithmeticResult;
  }

  // Handle property access and function calls
  return evaluateComplexExpression(expr, context);
}

/**
 * Evaluates property access expressions
 */
function evaluatePropertyAccess(expression: string, context: Record<string, any>): ExpressionResult {
  const parts = expression.split('.');

  let current = context;
  for (const part of parts) {
    if (current && typeof current === 'object' && part in current) {
      current = current[part];
    } else {
      return { value: undefined, isValid: true }; // Property doesn't exist, return undefined
    }
  }

  return { value: current, isValid: true };
}

/**
 * Evaluates function calls
 */
function evaluateFunctionCall(expression: string, context: Record<string, any>): ExpressionResult {
  const match = expression.match(/^([^(]+)\((.*)\)$/);
  if (!match) {
    return { value: undefined, isValid: false, error: 'Invalid function call syntax' };
  }

  const [, funcName, argsStr] = match;
  const args = argsStr ? argsStr.split(',').map(arg => arg.trim()) : [];

  // Evaluate arguments
  const evaluatedArgs: any[] = [];
  for (const arg of args) {
    const result = evaluateSafeExpression(arg, context);
    if (!result.isValid) return result;
    evaluatedArgs.push(result.value);
  }

  // Find and call function
  const func = context[funcName];
  if (typeof func === 'function') {
    try {
      const result = func(...evaluatedArgs);
      return { value: result, isValid: true };
    } catch (error) {
      return { value: undefined, isValid: false, error: `Function call error: ${error}` };
    }
  }

  return { value: undefined, isValid: false, error: `Function ${funcName} not found` };
}

/**
 * Evaluates comparison and logical expressions
 */
function evaluateComparison(expression: string, context: Record<string, any>): ExpressionResult {
  // Simple comparison operators
  const operators = ['===', '!==', '==', '!=', '<=', '>=', '<', '>'];

  for (const op of operators) {
    if (expression.includes(op)) {
      const [left, right] = expression.split(op).map(s => s.trim());

      const leftResult = evaluateSafeExpression(left, context);
      if (!leftResult.isValid) return leftResult;

      const rightResult = evaluateSafeExpression(right, context);
      if (!rightResult.isValid) return rightResult;

      let result: boolean;
      switch (op) {
        case '===':
          result = leftResult.value === rightResult.value;
          break;
        case '!==':
          result = leftResult.value !== rightResult.value;
          break;
        case '==':
          result = leftResult.value == rightResult.value;
          break;
        case '!=':
          result = leftResult.value != rightResult.value;
          break;
        case '<=':
          result = leftResult.value <= rightResult.value;
          break;
        case '>=':
          result = leftResult.value >= rightResult.value;
          break;
        case '<':
          result = leftResult.value < rightResult.value;
          break;
        case '>':
          result = leftResult.value > rightResult.value;
          break;
        default:
          return { value: false, isValid: false, error: 'Unknown operator' };
      }

      return { value: result, isValid: true };
    }
  }

  // If no operator found, try to evaluate as a property access
  return evaluatePropertyAccess(expression, context);
}

/**
 * Evaluates template literals (basic support)
 */
function evaluateTemplateLiteral(expression: string, context: Record<string, any>): ExpressionResult {
  // Remove backticks
  const content = expression.slice(1, -1);

  // Simple template literal evaluation - replace ${...} with evaluated expressions
  const result = content.replace(/\$\{([^}]+)\}/g, (match, expr) => {
    const evalResult = evaluateSafeExpression(expr.trim(), context);
    return evalResult.isValid ? String(evalResult.value) : '';
  });

  return { value: result, isValid: true };
}

/**
 * Evaluates new expressions (limited support)
 */
function evaluateNewExpression(expression: string, context: Record<string, any>): ExpressionResult {
  // Only support new Date() for now
  if (expression.startsWith('new Date(')) {
    const match = expression.match(/new Date\(([^)]*)\)/);
    if (match) {
      const arg = match[1].trim();
      if (arg === '') {
        return { value: new Date(), isValid: true };
      } else {
        const argResult = evaluateSafeExpression(arg, context);
        if (argResult.isValid) {
          try {
            return { value: new Date(argResult.value), isValid: true };
          } catch {
            return { value: undefined, isValid: false, error: 'Invalid date' };
          }
        }
        return argResult;
      }
    }
  }

  return { value: undefined, isValid: false, error: 'Unsupported new expression' };
}

/**
 * Evaluates instanceof expressions
 */
function evaluateInstanceof(expression: string, context: Record<string, any>): ExpressionResult {
  const parts = expression.split(' instanceof ');
  if (parts.length !== 2) {
    return { value: undefined, isValid: false, error: 'Invalid instanceof syntax' };
  }

  const [leftExpr, rightExpr] = parts.map(p => p.trim());

  const leftResult = evaluateSafeExpression(leftExpr, context);
  if (!leftResult.isValid) return leftResult;

  // Only support Date for now
  if (rightExpr === 'Date') {
    return { value: leftResult.value instanceof Date, isValid: true };
  }

  return { value: false, isValid: true }; // Unknown type, return false
}

/**
 * Evaluates arithmetic expressions
 */
function evaluateArithmetic(expression: string, context: Record<string, any>): ExpressionResult {
  // Simple arithmetic operators
  const operators = ['+', '-', '*', '/', '%'];

  for (const op of operators) {
    if (expression.includes(op)) {
      // Split by operator, but be careful with multiple operators
      const parts = expression.split(op);
      if (parts.length === 2) {
        const [left, right] = parts.map(p => p.trim());

        const leftResult = evaluateSafeExpression(left, context);
        if (!leftResult.isValid) return leftResult;

        const rightResult = evaluateSafeExpression(right, context);
        if (!rightResult.isValid) return rightResult;

        let result: number;
        switch (op) {
          case '+':
            result = Number(leftResult.value) + Number(rightResult.value);
            break;
          case '-':
            result = Number(leftResult.value) - Number(rightResult.value);
            break;
          case '*':
            result = Number(leftResult.value) * Number(rightResult.value);
            break;
          case '/':
            result = Number(leftResult.value) / Number(rightResult.value);
            break;
          case '%':
            result = Number(leftResult.value) % Number(rightResult.value);
            break;
          default:
            return { value: undefined, isValid: false, error: 'Unknown operator' };
        }

        return { value: result, isValid: true };
      }
    }
  }

  return { value: undefined, isValid: true }; // Not an arithmetic expression, continue evaluation
}

/**
 * Evaluates complex expressions (property access, function calls, etc.)
 */
function evaluateComplexExpression(expression: string, context: Record<string, any>): ExpressionResult {
  // Handle property access (dot notation)
  if (expression.includes('.')) {
    return evaluatePropertyAccess(expression, context);
  }

  // Handle function calls
  if (expression.includes('(') && expression.endsWith(')')) {
    return evaluateFunctionCall(expression, context);
  }

  // Handle logical operators
  if (expression.includes(' && ')) {
    const [left, right] = expression.split(' && ').map(p => p.trim());
    const leftResult = evaluateSafeExpression(left, context);
    if (!leftResult.isValid) return leftResult;
    if (!leftResult.value) return { value: false, isValid: true };

    const rightResult = evaluateSafeExpression(right, context);
    return rightResult.isValid ? { value: rightResult.value, isValid: true } : rightResult;
  }

  if (expression.includes(' || ')) {
    const [left, right] = expression.split(' || ').map(p => p.trim());
    const leftResult = evaluateSafeExpression(left, context);
    if (!leftResult.isValid) return leftResult;
    if (leftResult.value) return { value: leftResult.value, isValid: true };

    const rightResult = evaluateSafeExpression(right, context);
    return rightResult.isValid ? { value: rightResult.value, isValid: true } : rightResult;
  }

  if (expression.startsWith('!')) {
    const innerResult = evaluateSafeExpression(expression.slice(1), context);
    if (!innerResult.isValid) return innerResult;
    return { value: !innerResult.value, isValid: true };
  }

  // Handle optional chaining (basic support)
  if (expression.includes('?.')) {
    const parts = expression.split('?.');
    let current = context;

    for (let i = 0; i < parts.length; i++) {
      const part = parts[i].trim();
      if (current == null) {
        return { value: undefined, isValid: true };
      }

      if (i === parts.length - 1) {
        // Last part
        if (part in current) {
          return { value: current[part], isValid: true };
        } else {
          return { value: undefined, isValid: true };
        }
      } else {
        current = current[part];
      }
    }
  }

  // Handle nullish coalescing
  if (expression.includes(' ?? ')) {
    const [left, right] = expression.split(' ?? ').map(p => p.trim());
    const leftResult = evaluateSafeExpression(left, context);
    if (!leftResult.isValid) return leftResult;

    if (leftResult.value != null) {
      return { value: leftResult.value, isValid: true };
    }

    const rightResult = evaluateSafeExpression(right, context);
    return rightResult.isValid ? { value: rightResult.value, isValid: true } : rightResult;
  }

  // Try property access as fallback
  return evaluatePropertyAccess(expression, context);
}

/**
 * Unsafe evaluation using Function constructor (use with caution)
 */
function evaluateUnsafeExpression(expression: string, context: Record<string, any>): ExpressionResult {
  try {
    // Create parameter names and values
    const paramNames = Object.keys(context);
    const paramValues = Object.values(context);

    // Create a function with the expression
    const func = new Function(...paramNames, `return (${expression});`);

    // Execute the function with context values
    const result = func(...paramValues);

    return { value: result, isValid: true };
  } catch (error) {
    return {
      value: undefined,
      isValid: false,
      error: error instanceof Error ? error.message : 'Evaluation error'
    };
  }
}

/**
 * Validates an expression syntax without executing it
 */
export function validateExpression(expression: string): { isValid: boolean; error?: string } {
  if (!expression || typeof expression !== 'string') {
    return { isValid: false, error: 'Expression must be a non-empty string' };
  }

  // Basic syntax checks
  const trimmed = expression.trim();

  // Check for balanced parentheses
  let parenCount = 0;
  for (const char of trimmed) {
    if (char === '(') parenCount++;
    if (char === ')') parenCount--;
    if (parenCount < 0) return { isValid: false, error: 'Unbalanced parentheses' };
  }
  if (parenCount !== 0) return { isValid: false, error: 'Unbalanced parentheses' };

  // Check for potentially dangerous patterns
  const dangerousPatterns = [
    /eval\s*\(/,
    /Function\s*\(/,
    /setTimeout\s*\(/,
    /setInterval\s*\(/,
    /XMLHttpRequest/,
    /fetch\s*\(/,
    /import\s*\(/,
    /require\s*\(/,
    /process\./,
    /window\./,
    /document\./,
    /console\./,
    /alert\s*\(/,
    /prompt\s*\(/,
  ];

  for (const pattern of dangerousPatterns) {
    if (pattern.test(trimmed)) {
      return { isValid: false, error: 'Expression contains potentially unsafe code' };
    }
  }

  return { isValid: true };
}

/**
 * Gets available context variables for autocomplete/intellisense
 */
export function getContextVariables(): Array<{ name: string; type: string; description: string }> {
  return [
    { name: '$data', type: 'object', description: 'Root data object' },
    { name: '$root', type: 'object', description: 'Root data object (alias for $data)' },
    { name: '$element', type: 'object', description: 'Current element properties' },
    { name: '$this', type: 'object', description: 'Current element properties (alias for $element)' },
    { name: '$index', type: 'number', description: 'Current index in loops' },
    { name: '$parent', type: 'object', description: 'Parent context data' },
    { name: '$length', type: 'function', description: 'Get array length' },
    { name: '$isEmpty', type: 'function', description: 'Check if value is empty' },
    { name: '$format', type: 'function', description: 'Format values (currency, date)' },
    { name: '$isString', type: 'function', description: 'Check if value is string' },
    { name: '$isNumber', type: 'function', description: 'Check if value is number' },
    { name: '$isBoolean', type: 'function', description: 'Check if value is boolean' },
    { name: '$isArray', type: 'function', description: 'Check if value is array' },
    { name: '$isObject', type: 'function', description: 'Check if value is object' },
    { name: '$isDate', type: 'function', description: 'Check if value is date' },
  ];
}