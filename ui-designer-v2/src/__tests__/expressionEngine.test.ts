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
