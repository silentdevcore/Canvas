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

// Dataset-aggregate helpers: read a (optional) field from a row, and collect numeric column values.
// A bare identifier is a fast field read; a computed argument (Qty * Price, $iif(Paid, Total, 0)) is
// evaluated as a sub-expression against the row, so Sum(Qty*Price)/Sum(IIf(...)) work. Mirrors RowValue
// in CanvasExpressionEvaluator.
const BARE_IDENT = /^[A-Za-z_]\w*$/;
function aggField(row: any, field?: string): any {
  if (field == null) return row;
  if (BARE_IDENT.test(field)) return row && typeof row === 'object' ? row[field] : undefined;
  const r = evaluateExpression(field, { data: row && typeof row === 'object' ? row : {} });
  return r.isValid ? r.value : undefined;
}
function aggNums(rows: any, field?: string): number[] {
  if (!Array.isArray(rows)) return [];
  return rows
    .map(r => Number(aggField(r, field)))
    .filter(n => !Number.isNaN(n));
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

    // Helpers emitted by the migration ExpressionTranslator (RDL/DevExpress → Canvas grammar).
    $iif: (cond: any, a: any, b: any) => (cond ? a : b),
    $switch: (...args: any[]) => {
      for (let i = 0; i + 1 < args.length; i += 2) if (args[i]) return args[i + 1];
      return args.length % 2 === 1 ? args[args.length - 1] : undefined; // optional trailing default
    },
    $concat: (...parts: any[]) => parts.map(p => (p == null ? '' : String(p))).join(''),
    $and: (...xs: any[]) => xs.every(Boolean),
    $or: (...xs: any[]) => xs.some(Boolean),
    $not: (x: any) => !x,
    $coalesce: (...xs: any[]) => xs.find(x => x != null),

    // Dataset aggregates (rows = the dataset array, field = optional column name).
    // $sum(DataSet, "Total"), $count(DataSet), $first(DataSet, "Name"), … mirror CanvasExpressionEvaluator.
    $sum: (rows: any, field?: string) => aggNums(rows, field).reduce((a, b) => a + b, 0),
    $avg: (rows: any, field?: string) => { const n = aggNums(rows, field); return n.length ? n.reduce((a, b) => a + b, 0) / n.length : 0; },
    $min: (rows: any, field?: string) => { const n = aggNums(rows, field); return n.length ? Math.min(...n) : 0; },
    $max: (rows: any, field?: string) => { const n = aggNums(rows, field); return n.length ? Math.max(...n) : 0; },
    $count: (rows: any, field?: string) => !Array.isArray(rows) ? 0 : (field == null ? rows.length : rows.filter(r => aggField(r, field) != null).length),
    $first: (rows: any, field?: string) => Array.isArray(rows) && rows.length ? aggField(rows[0], field) : undefined,
    $last: (rows: any, field?: string) => Array.isArray(rows) && rows.length ? aggField(rows[rows.length - 1], field) : undefined,

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
 * Safely evaluates an expression. Literal/template/new/instanceof are handled as special cases; the rest
 * goes through a recursive-descent precedence parser that mirrors the server CanvasExpressionEvaluator.
 */
function evaluateSafeExpression(expression: string, context: Record<string, any>): ExpressionResult {
  const expr = expression.trim();
  if (expr === '') return { value: undefined, isValid: false, error: 'Empty expression' };

  // Preserved special forms (not part of the Canvas grammar, but supported by this engine).
  if (expr.startsWith('`') && expr.endsWith('`')) return evaluateTemplateLiteral(expr, context);
  if (expr.startsWith('new ')) return evaluateNewExpression(expr, context);
  if (expr.includes(' instanceof ')) return evaluateInstanceof(expr, context);

  try {
    const parser = new ExprParser(tokenize(expr), context);
    const value = parser.parse();
    return { value, isValid: true };
  } catch (error) {
    return { value: undefined, isValid: false, error: error instanceof Error ? error.message : 'Parse error' };
  }
}

// ── Tokenizer + recursive-descent parser (mirrors src/Canvas.Core/Primitives/CanvasExpressionEvaluator.cs) ──

type TokKind = 'num' | 'str' | 'ident' | 'op' | 'lparen' | 'rparen' | 'comma' | 'end';
interface Tok { kind: TokKind; text: string; num?: number; }

const MULTI_OPS = ['===', '!==', '==', '!=', '<=', '>=', '&&', '||', '??'];

function tokenize(s: string): Tok[] {
  const toks: Tok[] = [];
  let i = 0;
  while (i < s.length) {
    const c = s[i];
    if (c === ' ' || c === '\t' || c === '\n' || c === '\r') { i++; continue; }

    if (c === '"' || c === "'") {
      const quote = c; let buf = ''; i++;
      while (i < s.length && s[i] !== quote) {
        if (s[i] === '\\' && i + 1 < s.length) { buf += s[i + 1]; i += 2; }
        else buf += s[i++];
      }
      if (i >= s.length) throw new Error('Unterminated string');
      i++; // closing quote
      toks.push({ kind: 'str', text: buf });
      continue;
    }

    if (/[0-9]/.test(c) || (c === '.' && i + 1 < s.length && /[0-9]/.test(s[i + 1]))) {
      let start = i;
      while (i < s.length && /[0-9.]/.test(s[i])) i++;
      const text = s.slice(start, i);
      toks.push({ kind: 'num', text, num: Number(text) });
      continue;
    }

    // Identifiers: letters/digits/_/$ and dotted member access. Optional chaining `?.` collapses to `.`.
    if (/[A-Za-z_$]/.test(c)) {
      let start = i;
      while (i < s.length && (/[A-Za-z0-9_$.]/.test(s[i]) || (s[i] === '?' && s[i + 1] === '.'))) {
        if (s[i] === '?' ) i++; // skip the '?' of '?.'
        i++;
      }
      toks.push({ kind: 'ident', text: s.slice(start, i).replace(/\?\./g, '.') });
      continue;
    }

    if (c === '(') { toks.push({ kind: 'lparen', text: c }); i++; continue; }
    if (c === ')') { toks.push({ kind: 'rparen', text: c }); i++; continue; }
    if (c === ',') { toks.push({ kind: 'comma', text: c }); i++; continue; }

    const three = s.substr(i, 3), two = s.substr(i, 2);
    if (MULTI_OPS.includes(three)) { toks.push({ kind: 'op', text: three }); i += 3; continue; }
    if (MULTI_OPS.includes(two)) { toks.push({ kind: 'op', text: two }); i += 2; continue; }
    if ('<>+-*/%!'.includes(c)) { toks.push({ kind: 'op', text: c }); i++; continue; }

    throw new Error(`Unexpected character '${c}'`);
  }
  toks.push({ kind: 'end', text: '' });
  return toks;
}

function isNumericVal(v: any): boolean {
  if (typeof v === 'number') return !Number.isNaN(v);
  if (typeof v === 'string' && v.trim() !== '') { const n = Number(v); return !Number.isNaN(n); }
  return false;
}
function toNum(v: any): number {
  if (typeof v === 'number') return v;
  if (typeof v === 'boolean') return v ? 1 : 0;
  const n = Number(v);
  if (Number.isNaN(n)) throw new Error('non-numeric operand');
  return n;
}
function fmt(v: any): string {
  if (v == null) return '';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  return String(v);
}
function truthy(v: any): boolean {
  if (v == null) return false;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  if (typeof v === 'string') return v.length > 0;
  return true;
}
function looseEquals(a: any, b: any): boolean {
  if (a == null || b == null) return a == null && b == null;
  if (isNumericVal(a) && isNumericVal(b)) return toNum(a) === toNum(b);
  if (typeof a === 'boolean' || typeof b === 'boolean') return truthy(a) === truthy(b);
  return fmt(a) === fmt(b);
}
function compareVals(a: any, b: any): number {
  if (isNumericVal(a) && isNumericVal(b)) { const x = toNum(a), y = toNum(b); return x < y ? -1 : x > y ? 1 : 0; }
  const x = fmt(a), y = fmt(b); return x < y ? -1 : x > y ? 1 : 0;
}

class ExprParser {
  private i = 0;
  constructor(private toks: Tok[], private ctx: Record<string, any>) {}

  parse(): any {
    const v = this.parseOr();
    if (this.cur.kind !== 'end') throw new Error('trailing tokens');
    return v;
  }

  private get cur(): Tok { return this.toks[this.i]; }
  private isOp(...t: string[]): boolean { return this.cur.kind === 'op' && t.includes(this.cur.text); }

  private parseOr(): any {
    let left = this.parseAnd();
    while (this.isOp('||', '??')) {
      const op = this.cur.text; this.i++;
      const right = this.parseAnd();
      left = op === '||' ? (truthy(left) || truthy(right)) : (left ?? right);
    }
    return left;
  }
  private parseAnd(): any {
    let left = this.parseEquality();
    while (this.isOp('&&')) { this.i++; const r = this.parseEquality(); left = truthy(left) && truthy(r); }
    return left;
  }
  private parseEquality(): any {
    let left = this.parseComparison();
    while (this.isOp('==', '!=', '===', '!==')) {
      const op = this.cur.text; this.i++;
      const right = this.parseComparison();
      const eq = op === '===' ? left === right : op === '!==' ? left !== right : looseEquals(left, right);
      left = (op === '!=' ) ? !eq : (op === '!==') ? eq : eq;
    }
    return left;
  }
  private parseComparison(): any {
    let left = this.parseAdditive();
    while (this.isOp('<', '<=', '>', '>=')) {
      const op = this.cur.text; this.i++;
      const c = compareVals(left, this.parseAdditive());
      left = op === '<' ? c < 0 : op === '<=' ? c <= 0 : op === '>' ? c > 0 : c >= 0;
    }
    return left;
  }
  private parseAdditive(): any {
    let left = this.parseMultiplicative();
    while (this.isOp('+', '-')) {
      const op = this.cur.text; this.i++;
      const right = this.parseMultiplicative();
      if (op === '+') left = (isNumericVal(left) && isNumericVal(right)) ? toNum(left) + toNum(right) : fmt(left) + fmt(right);
      else left = toNum(left) - toNum(right);
    }
    return left;
  }
  private parseMultiplicative(): any {
    let left = this.parseUnary();
    while (this.isOp('*', '/', '%')) {
      const op = this.cur.text; this.i++;
      const a = toNum(left), b = toNum(this.parseUnary());
      left = op === '*' ? a * b : op === '/' ? a / b : a % b;
    }
    return left;
  }
  private parseUnary(): any {
    if (this.isOp('!')) { this.i++; return !truthy(this.parseUnary()); }
    if (this.isOp('-')) { this.i++; return -toNum(this.parseUnary()); }
    if (this.isOp('+')) { this.i++; return toNum(this.parseUnary()); }
    return this.parsePrimary();
  }
  private parsePrimary(): any {
    const t = this.cur;
    if (t.kind === 'num') { this.i++; return t.num; }
    if (t.kind === 'str') { this.i++; return t.text; }
    if (t.kind === 'lparen') {
      this.i++;
      const inner = this.parseOr();
      if (this.cur.kind !== 'rparen') throw new Error('expected )');
      this.i++;
      return inner;
    }
    if (t.kind === 'ident') {
      this.i++;
      if (this.cur.kind === 'lparen') return this.callFunction(t.text);
      switch (t.text) {
        case 'true': return true;
        case 'false': return false;
        case 'null': return null;
        case 'undefined': return undefined;
        default: return this.resolve(t.text);
      }
    }
    throw new Error(`unexpected token '${t.text}'`);
  }
  private callFunction(name: string): any {
    this.i++; // consume '('
    const args: any[] = [];
    if (this.cur.kind !== 'rparen') {
      args.push(this.parseOr());
      while (this.cur.kind === 'comma') { this.i++; args.push(this.parseOr()); }
    }
    if (this.cur.kind !== 'rparen') throw new Error('expected )');
    this.i++;
    const fn = this.resolve(name);
    if (typeof fn !== 'function') throw new Error(`Function ${name} not found`);
    return fn(...args);
  }
  // Dotted resolution against the context (a.b.c, Math.round). Missing path → undefined.
  private resolve(name: string): any {
    let current: any = this.ctx;
    for (const part of name.split('.')) {
      if (current != null && (typeof current === 'object' || typeof current === 'function') && part in current) current = current[part];
      else return undefined;
    }
    return current;
  }
}

/**
 * Evaluates template literals (basic support)
 */
function evaluateTemplateLiteral(expression: string, context: Record<string, any>): ExpressionResult {
  // Remove backticks
  const content = expression.slice(1, -1);

  // Simple template literal evaluation - replace ${...} with evaluated expressions
  const result = content.replace(/\$\{([^}]+)\}/g, (_, expr) => {
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
