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

  test('non-array dataset yields safe defaults', () => {
    expect(evaluateExpression('$sum(Missing, "Total")', ctx).value).toBe(0);
    expect(evaluateExpression('$count(Missing)', ctx).value).toBe(0);
  });
});
