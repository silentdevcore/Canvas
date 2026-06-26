import { evaluateExpression, ExpressionContext } from '../template/expressionEngine';

// Proves that the Canvas-grammar expressions emitted by the migration ExpressionTranslator
// (RDL/DevExpress → $iif/$concat/$and/operators) actually evaluate in the preview engine.
describe('expressionEngine — migrated expression helpers', () => {
  const ctx = (data: Record<string, any>): ExpressionContext => ({ data });

  test('$concat joins fields and literals', () => {
    const r = evaluateExpression('$concat(First, " ", Last)', ctx({ First: 'Ada', Last: 'Lovelace' }));
    expect(r.isValid).toBe(true);
    expect(r.value).toBe('Ada Lovelace');
  });

  test('$iif with a comparison condition', () => {
    expect(evaluateExpression('$iif(Paid == true, "Yes", "No")', ctx({ Paid: true })).value).toBe('Yes');
    expect(evaluateExpression('$iif(Paid == true, "Yes", "No")', ctx({ Paid: false })).value).toBe('No');
  });

  test('nested helpers parse via top-level arg splitting', () => {
    const r = evaluateExpression('$iif(Qty == 0, "n/a", $concat("x", Qty))', ctx({ Qty: 5 }));
    expect(r.value).toBe('x5');
  });

  test('$and / $or boolean logic', () => {
    expect(evaluateExpression('$and(A == 1, B == 2)', ctx({ A: 1, B: 2 })).value).toBe(true);
    expect(evaluateExpression('$or(A == 1, B == 2)', ctx({ A: 0, B: 2 })).value).toBe(true);
  });

  test('binary arithmetic', () => {
    expect(evaluateExpression('Qty * Price', ctx({ Qty: 3, Price: 4 })).value).toBe(12);
  });
});

// Dataset aggregates emitted as $sum(DataSet, "Field") etc. — parity with CanvasExpressionEvaluator.
describe('expressionEngine — dataset aggregates', () => {
  const data = {
    Orders: [
      { Total: 10, Name: 'A' },
      { Total: 20, Name: 'B' },
      { Total: 30, Name: 'C' },
    ],
  };
  const ctx: ExpressionContext = { data };

  test('$sum / $avg / $min / $max over a field', () => {
    expect(evaluateExpression('$sum(Orders, "Total")', ctx).value).toBe(60);
    expect(evaluateExpression('$avg(Orders, "Total")', ctx).value).toBe(20);
    expect(evaluateExpression('$min(Orders, "Total")', ctx).value).toBe(10);
    expect(evaluateExpression('$max(Orders, "Total")', ctx).value).toBe(30);
  });

  test('$count, $first, $last', () => {
    expect(evaluateExpression('$count(Orders)', ctx).value).toBe(3);
    expect(evaluateExpression('$first(Orders, "Name")', ctx).value).toBe('A');
    expect(evaluateExpression('$last(Orders, "Name")', ctx).value).toBe('C');
  });

  test('aggregate composes inside $concat', () => {
    expect(evaluateExpression('$concat("Total: ", $sum(Orders, "Total"))', ctx).value).toBe('Total: 60');
  });

  test('aggregate over a computed per-row sub-expression (Sum(Qty*Price) / Sum(IIf(...)))', () => {
    // Second arg is a per-row expression, not just a field name — parity with CanvasExpressionEvaluator.RowValue.
    expect(evaluateExpression('$sum(Orders, "Total * 2")', ctx).value).toBe(120);                 // (10+20+30)*2
    expect(evaluateExpression('$sum(Orders, "$iif(Total > 15, Total, 0)")', ctx).value).toBe(50); // 20 + 30 only
  });

  test('non-array dataset yields safe defaults', () => {
    expect(evaluateExpression('$sum(Missing, "Total")', ctx).value).toBe(0);
    expect(evaluateExpression('$count(Missing)', ctx).value).toBe(0);
  });
});

// Recursive-descent precedence parser — parity with the server CanvasExpressionEvaluator.
describe('expressionEngine — operator precedence', () => {
  const ctx = (data: Record<string, any>): ExpressionContext => ({ data });
  const val = (expr: string, data: Record<string, any> = {}) => evaluateExpression(expr, ctx(data)).value;

  test('arithmetic precedence and associativity', () => {
    expect(val('2 + 3 * 4')).toBe(14);
    expect(val('(2 + 3) * 4')).toBe(20);
    expect(val('10 - 3 - 2')).toBe(5);          // left-associative
    expect(val('2 * 3 + 4 * 5')).toBe(26);
    expect(val('-5 + 2')).toBe(-3);             // unary minus
  });

  test('+ is numeric add or string concat', () => {
    expect(val('Qty + 1', { Qty: 4 })).toBe(5);
    expect(val('First + " " + Last', { First: 'Ada', Last: 'Lovelace' })).toBe('Ada Lovelace');
  });

  test('logical operators with correct precedence (&& over ||)', () => {
    expect(val('A == 1 && B == 2', { A: 1, B: 2 })).toBe(true);
    expect(val('A == 1 && B == 2', { A: 1, B: 9 })).toBe(false);
    expect(val('A == 1 || B == 2', { A: 0, B: 2 })).toBe(true);
    expect(val('!(A == 1)', { A: 1 })).toBe(false);
    // && binds tighter than ||: parsed as (false && false) || true → true
    expect(val('A == 1 && B == 2 || C == 3', { A: 0, B: 0, C: 3 })).toBe(true);
  });

  test('comparison combined with arithmetic', () => {
    expect(val('Qty * Price > 100', { Qty: 11, Price: 10 })).toBe(true);
    expect(val('Qty * Price > 100', { Qty: 5, Price: 10 })).toBe(false);
  });

  test('chained comparison + helper still works', () => {
    expect(val('$iif(Qty * Price > 100, "big", "small")', { Qty: 11, Price: 10 })).toBe('big');
  });
});
