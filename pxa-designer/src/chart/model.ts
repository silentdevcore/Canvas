import type { ChartDefinition, ChartSeries, PxaChartType } from './types';

export const CHART_PALETTE = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2'];
export const CHART_TYPES: PxaChartType[] = ['bar', 'line', 'area', 'pie', 'doughnut', 'stackedBar', 'combo'];

const safeType = (value: unknown): PxaChartType =>
  typeof value === 'string' && CHART_TYPES.includes(value as PxaChartType)
    ? value as PxaChartType
    : 'bar';

const safeNumber = (value: unknown): number | null => {
  if (value === null || value === undefined || value === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

export const createDefaultChartDefinition = (): ChartDefinition => ({
  schemaVersion: 2,
  type: 'bar',
  categories: ['Jan', 'Feb', 'Mar', 'Apr'],
  series: [{ id: 'series-1', name: 'Series 1', values: [12, 19, 14, 22], color: CHART_PALETTE[0] }],
  valueAxes: [{ id: 'primary', scale: 'linear', visible: true, gridLines: true }],
  legend: { visible: true, position: 'bottom' },
  dataLabels: { visible: false, position: 'auto' },
  palette: [...CHART_PALETTE]
});
export const normalizeChartDefinition = (
  chart: unknown,
  legacyType?: unknown,
  legacyData?: Record<string, unknown>
): ChartDefinition => {
  if (chart && typeof chart === 'object') {
    const source = chart as Partial<ChartDefinition>;
    const categories = Array.isArray(source.categories) ? source.categories.map(String).slice(0, 5000) : [];
    const series = Array.isArray(source.series)
      ? source.series.slice(0, 32).map((item, index): ChartSeries => ({
          id: item?.id || `series-${index + 1}`,
          name: item?.name || `Series ${index + 1}`,
          type: item?.type ? safeType(item.type) : undefined,
          values: Array.isArray(item?.values) ? item.values.slice(0, 5000).map(safeNumber) : [],
          color: item?.color || CHART_PALETTE[index % CHART_PALETTE.length],
          stackGroup: item?.stackGroup,
          valueAxisId: item?.valueAxisId || 'primary',
          fill: item?.fill ?? false,
          showMarkers: item?.showMarkers ?? true
        }))
      : [];
    return {
      ...createDefaultChartDefinition(),
      ...source,
      schemaVersion: 2,
      type: safeType(source.type),
      categories,
      series,
      palette: Array.isArray(source.palette) && source.palette.length ? source.palette : [...CHART_PALETTE]
    };
  }

  const labels = Array.isArray(legacyData?.labels) ? legacyData.labels.map(String).slice(0, 5000) : [];
  const datasets = Array.isArray(legacyData?.datasets) ? legacyData.datasets.slice(0, 32) : [];
  const type = safeType(legacyType);
  const series = datasets.map((value, index): ChartSeries => {
    const dataset = value && typeof value === 'object' ? value as Record<string, unknown> : {};
    return {
      id: `series-${index + 1}`,
      name: typeof dataset.label === 'string' ? dataset.label : `Series ${index + 1}`,
      values: Array.isArray(dataset.data) ? dataset.data.slice(0, 5000).map(safeNumber) : [],
      color: typeof dataset.backgroundColor === 'string'
        ? dataset.backgroundColor
        : typeof dataset.color === 'string' ? dataset.color : CHART_PALETTE[index % CHART_PALETTE.length],
      stackGroup: type === 'stackedBar' ? 'default' : undefined,
      valueAxisId: 'primary',
      fill: type === 'area',
      showMarkers: true
    };
  });

  return { ...createDefaultChartDefinition(), type, categories: labels, series };
};

export const toLegacyChartData = (chart: ChartDefinition): Record<string, unknown> => ({
  labels: chart.categories,
  datasets: chart.series.map(series => ({
    label: series.name,
    data: series.values,
    backgroundColor: series.color,
    color: series.color
  }))
});
