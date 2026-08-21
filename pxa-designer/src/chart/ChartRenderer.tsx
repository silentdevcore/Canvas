import React, { useMemo } from 'react';
import {
  Area,
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  LabelList,
  Legend,
  Line,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import { CHART_PALETTE, normalizeChartDefinition } from './model';
import type { ChartDefinition, ChartSeries, PxaChartType } from './types';
import { useTranslation } from 'react-i18next';

interface ChartRendererProps {
  chart?: ChartDefinition;
  legacyType?: PxaChartType;
  legacyData?: Record<string, unknown>;
  ariaLabel?: string;
  className?: string;
}

const seriesType = (chartType: PxaChartType, series: ChartSeries, index: number): PxaChartType => {
  if (chartType === 'combo') return series.type || (index === 0 ? 'bar' : 'line');
  if (chartType === 'stackedBar') return 'bar';
  return chartType;
};

export const ChartRenderer: React.FC<ChartRendererProps> = ({
  chart,
  legacyType,
  legacyData,
  ariaLabel = 'Chart',
  className
}) => {
  const { t } = useTranslation('editor');
  const definition = useMemo(
    () => normalizeChartDefinition(chart, legacyType, legacyData),
    [chart, legacyData, legacyType]
  );
  const rows = useMemo(() => definition.categories.map((category, categoryIndex) => {
    const row: Record<string, string | number | null> = { category };
    definition.series.forEach((series, seriesIndex) => {
      row[`series_${seriesIndex}`] = series.values[categoryIndex] ?? null;
    });
    return row;
  }), [definition]);
  const hasData = definition.series.some(series => series.values.some(value => value !== null));
  const primaryAxis = definition.valueAxes?.[0];
  const axisDomain: [number | 'auto', number | 'auto'] = [primaryAxis?.minimum ?? 'auto', primaryAxis?.maximum ?? 'auto'];
  const legendVisible = definition.legend?.visible ?? true;
  const dataLabelsVisible = definition.dataLabels?.visible ?? false;
  const numberFormatter = useMemo(() => {
    try { return new Intl.NumberFormat(definition.locale || undefined, { maximumFractionDigits: 2 }); }
    catch { return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }); }
  }, [definition.locale]);

  if (!hasData || rows.length === 0) {
    return (
      <div className={className} role="img" aria-label={ariaLabel} style={{
        width: '100%', height: '100%', minHeight: 0, display: 'grid', placeItems: 'center',
        border: '1px dashed #cbd5e1', color: '#64748b', background: '#f8fafc', fontSize: 12
      }}>
        {t('elementInspector.chart.emptyState')}
      </div>
    );
  }

  const isCircular = definition.type === 'pie' || definition.type === 'doughnut';
  const firstSeries = definition.series[0];
  const pieData = definition.categories.map((name, index) => ({
    name,
    value: Math.max(firstSeries?.values[index] ?? 0, 0)
  }));

  return (
    <div className={className} role="img" aria-label={ariaLabel} style={{
      width: '100%', height: '100%', minHeight: 0, display: 'flex', flexDirection: 'column',
      overflow: 'hidden', background: 'transparent'
    }}>
      {(definition.title || definition.subtitle) && (
        <header style={{ flex: '0 0 auto', textAlign: 'center', padding: '3px 8px 0' }}>
          {definition.title && <div style={{ fontSize: 12, fontWeight: 700, color: '#0f172a' }}>{definition.title}</div>}
          {definition.subtitle && <div style={{ fontSize: 10, color: '#64748b' }}>{definition.subtitle}</div>}
        </header>
      )}
      <div style={{ flex: '1 1 auto', minHeight: 0 }}>
        <ResponsiveContainer width="100%" height="100%">
          {isCircular ? (
            <PieChart>
              <Pie
                data={pieData}
                dataKey="value"
                nameKey="name"
                cx="50%"
                cy="50%"
                innerRadius={definition.type === 'doughnut' ? '42%' : 0}
                outerRadius="72%"
                isAnimationActive={false}
              >
                {pieData.map((entry, index) => (
                  <Cell
                    key={`${entry.name}-${index}`}
                    fill={definition.palette?.[index % definition.palette.length]
                      || CHART_PALETTE[index % CHART_PALETTE.length]}
                  />
                ))}
                {dataLabelsVisible && <LabelList dataKey="value" position="outside" />}
              </Pie>
              <Tooltip formatter={(value) => typeof value === 'number' ? numberFormatter.format(value) : value} />
              {legendVisible && <Legend verticalAlign={definition.legend?.position === 'top' ? 'top' : 'bottom'} />}
            </PieChart>
          ) : (
            <ComposedChart data={rows} margin={{ top: 12, right: 16, bottom: 10, left: 4 }}>
              {(primaryAxis?.gridLines ?? true) && <CartesianGrid stroke="#e2e8f0" strokeDasharray="3 3" />}
              <XAxis
                dataKey="category"
                hide={definition.categoryAxis?.visible === false}
                tick={{ fontSize: 10, fill: '#475569' }}
              />
              <YAxis
                hide={primaryAxis?.visible === false}
                domain={axisDomain}
                allowDataOverflow={primaryAxis?.minimum !== undefined || primaryAxis?.maximum !== undefined}
                tick={{ fontSize: 10, fill: '#475569' }}
                tickFormatter={(value) => numberFormatter.format(Number(value))}
              />
              <Tooltip formatter={(value) => typeof value === 'number' ? numberFormatter.format(value) : value} />
              {legendVisible && <Legend verticalAlign={definition.legend?.position === 'top' ? 'top' : 'bottom'} />}
              {definition.series.map((series, index) => {
                const type = seriesType(definition.type, series, index);
                const key = `series_${index}`;
                const color = series.color || definition.palette?.[index % definition.palette.length]
                  || CHART_PALETTE[index % CHART_PALETTE.length];
                if (type === 'line') {
                  return <Line key={series.id} dataKey={key} name={series.name} stroke={color} strokeWidth={2}
                    connectNulls={false} dot={series.showMarkers !== false} isAnimationActive={false} />;
                }
                if (type === 'area') {
                  return <Area key={series.id} dataKey={key} name={series.name} stroke={color} fill={color}
                    fillOpacity={0.25} connectNulls={false} isAnimationActive={false} />;
                }
                return (
                  <Bar key={series.id} dataKey={key} name={series.name} fill={color}
                    stackId={definition.type === 'stackedBar' ? series.stackGroup || 'default' : undefined}
                    isAnimationActive={false}>
                    {dataLabelsVisible && <LabelList dataKey={key} position="top"
                      formatter={(value: unknown) => typeof value === 'number' ? numberFormatter.format(value) : String(value ?? '')} />}
                  </Bar>
                );
              })}
            </ComposedChart>
          )}
        </ResponsiveContainer>
      </div>
    </div>
  );
};

export default ChartRenderer;
