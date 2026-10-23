import { createDefaultChartDefinition, normalizeChartDefinition, toLegacyChartData } from '@/chart/model';

describe('chart definition model', () => {
  test('normalizes legacy labels and arbitrary datasets into version 2', () => {
    const chart = normalizeChartDefinition(undefined, 'line', {
      labels: ['Q1', 'Q2', 'Q3'],
      datasets: [
        { label: 'Revenue', data: [10, -3, null], borderColor: '#2563eb' },
        { label: 'Margin', data: [2, 4, 6], backgroundColor: '#16a34a' },
      ],
    });

    expect(chart.schemaVersion).toBe(2);
    expect(chart.type).toBe('line');
    expect(chart.categories).toEqual(['Q1', 'Q2', 'Q3']);
    expect(chart.series).toHaveLength(2);
    expect(chart.series[0].values).toEqual([10, -3, null]);
    expect(chart.series[1].color).toBe('#16a34a');
  });

  test('bounds categories and series and converts non-finite values to null', () => {
    const chart = normalizeChartDefinition({
      schemaVersion: 2,
      type: 'combo',
      categories: Array.from({ length: 5100 }, (_, index) => `C${index}`),
      series: Array.from({ length: 40 }, (_, index) => ({
        id: `s${index}`,
        name: `Series ${index}`,
        values: [index, Number.NaN, Number.POSITIVE_INFINITY],
      })),
    });

    expect(chart.categories).toHaveLength(5000);
    expect(chart.series).toHaveLength(32);
    expect(chart.series[0].values).toEqual([0, null, null]);
  });

  test('keeps legacy export fields synchronized', () => {
    const chart = createDefaultChartDefinition();
    chart.type = 'stackedBar';
    chart.series.push({ id: 's2', name: 'Second', values: [1, 2, 3], stackGroup: 'default' });

    const legacy = toLegacyChartData(chart);

    expect(legacy.labels).toEqual(chart.categories);
    expect(legacy.datasets).toHaveLength(2);
  });
});
