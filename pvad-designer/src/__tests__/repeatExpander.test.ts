import { partitionByGroup } from '../template/repeatExpander';
import { evaluateExpression, ExpressionContext } from '../template/expressionEngine';

// Parity with the server-side DesignLayoutPlanner.PartitionByGroup + group-scoped aggregates:
// a group band partitions its rows by the group field, exposes each group's rows as $group,
// and a migrated aggregate ($sum($group, "Amount")) computes that group's own total.
describe('repeatExpander — group-scoped aggregates (server parity)', () => {
  const rows = [
    { Region: 'North', Amount: 10 },
    { Region: 'North', Amount: 20 },
    { Region: 'South', Amount: 5 },
  ];

  test('partitionByGroup keeps distinct groups in stable first-seen order', () => {
    const groups = partitionByGroup(rows, 'Region');
    expect(groups.map(g => g.key)).toEqual(['North', 'South']);
    expect(groups[0].rows).toHaveLength(2);
    expect(groups[1].rows).toHaveLength(1);
  });

  test('$sum($group, "Amount") computes each group total, not the grand total', () => {
    const totals = partitionByGroup(rows, 'Region').map(g => {
      const ctx: ExpressionContext = { data: { Region: g.key, $group: g.rows } };
      return evaluateExpression('$sum($group, "Amount")', ctx).value;
    });
    expect(totals).toEqual([30, 5]); // North 10+20, South 5 — not the grand total 35
  });

  test('partitionByGroup resolves dotted group fields', () => {
    const nested = [
      { customer: { country: 'DE' }, n: 1 },
      { customer: { country: 'DE' }, n: 2 },
      { customer: { country: 'FR' }, n: 3 },
    ];
    expect(partitionByGroup(nested, 'customer.country').map(g => g.key)).toEqual(['DE', 'FR']);
  });
});
