import { CHART_PALETTE, normalizeChartDefinition } from './model';
import type { ChartDefinition, ChartSeries, PxaChartType } from './types';

interface Props {
  chart?: ChartDefinition;
  legacyType?: PxaChartType;
  legacyData?: Record<string, unknown>;
  ariaLabel?: string;
  width: number;
  height: number;
}

export function niceTicks(minimum: number, maximum: number, targetCount = 5): number[] {
  const rawStep = Math.max((maximum - minimum) / Math.max(targetCount - 1, 1), Number.EPSILON);
  const magnitude = 10 ** Math.floor(Math.log10(rawStep));
  const residual = rawStep / magnitude;
  const niceResidual = residual <= 1 ? 1 : residual <= 2 ? 2 : residual <= 5 ? 5 : 10;
  const step = niceResidual * magnitude;
  const start = Math.floor(minimum / step) * step;
  const end = Math.ceil(maximum / step) * step;
  const count = Math.min(20, Math.round((end - start) / step) + 1);
  return Array.from({ length: count }, (_, index) => start + index * step);
}

const effectiveType = (type: PxaChartType, series: ChartSeries, index: number): PxaChartType =>
  type === 'combo' ? series.type ?? (index === 0 ? 'bar' : 'line') : type;

const color = (series: ChartSeries, index: number, palette: string[]) =>
  series.color ?? palette[index % palette.length] ?? CHART_PALETTE[index % CHART_PALETTE.length];

const formatValue = (value: number, locale?: string) => {
  if (!locale) return Number(value.toFixed(12)).toString();
  try { return new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(value); }
  catch { return Number(value.toFixed(12)).toString(); }
};

export default function PdfContractChartRenderer({
  chart,
  legacyType,
  legacyData,
  ariaLabel = 'Chart',
  width,
  height,
}: Props) {
  const definition = normalizeChartDefinition(chart, legacyType, legacyData);
  const palette = definition.palette?.length ? definition.palette : CHART_PALETTE;
  const values = definition.series.flatMap(series => series.values).filter((value): value is number => value !== null);
  if (definition.categories.length === 0 || values.length === 0) {
    return <svg role="img" aria-label={ariaLabel} viewBox={`0 0 ${width} ${height}`} width="100%" height="100%">
      <rect x="0" y="0" width={width} height={height} fill="#f8fafc" stroke="#e2e8f0" strokeWidth="0.7" />
      <text x="8" y={Math.max(10, height / 2)} fontSize="9" fill="#334155">No chart data</text>
    </svg>;
  }

  const titleHeight = definition.title ? 16 : 0;
  const legendHeight = definition.legend?.visible === false ? 0 : 16;
  const plotX = 36;
  const plotBottom = 22 + legendHeight;
  const plotWidth = Math.max(10, width - 48);
  const plotHeight = Math.max(10, height - 32 - legendHeight - titleHeight);
  const fontFamily = 'Helvetica, Arial, sans-serif';

  if (definition.type === 'pie' || definition.type === 'doughnut') {
    const circularValues = definition.series[0]?.values.map(value => Math.max(value ?? 0, 0)) ?? [];
    const total = circularValues.reduce((sum, value) => sum + value, 0);
    const centerX = plotX + plotWidth / 2;
    const centerY = height - (plotBottom + plotHeight / 2);
    const radius = Math.max(4, Math.min(plotWidth, plotHeight) * 0.42);
    let angle = -Math.PI / 2;
    const slices = circularValues.map((value, index) => {
      if (value <= 0 || total <= 0) return null;
      const end = angle + value / total * Math.PI * 2;
      const segments = Math.max(4, Math.ceil((end - angle) / (Math.PI / 18)));
      const points = [`${centerX},${centerY}`];
      for (let segment = 0; segment <= segments; segment += 1) {
        const current = angle + (end - angle) * segment / segments;
        points.push(`${centerX + Math.cos(current) * radius},${centerY + Math.sin(current) * radius}`);
      }
      angle = end;
      return <polygon key={index} points={points.join(' ')} fill={palette[index % palette.length]}
        stroke="#ffffff" strokeWidth="0.4" />;
    });
    return <svg role="img" aria-label={ariaLabel} viewBox={`0 0 ${width} ${height}`} width="100%" height="100%"
      style={{ fontFamily }}>
      {definition.title && <text x="4" y="12" fontSize="10" fontWeight="bold">{definition.title}</text>}
      {slices}
      {definition.type === 'doughnut' && <circle cx={centerX} cy={centerY} r={radius * 0.46}
        fill="#ffffff" stroke="#ffffff" strokeWidth="0.2" />}
      <ChartLegend definition={definition} width={width} height={height} palette={palette} />
    </svg>;
  }

  let minimum = Math.min(0, ...values);
  let maximum = Math.max(0, ...values);
  if (definition.type === 'stackedBar') {
    const stacks = definition.categories.map((_, category) => ({
      positive: definition.series.reduce((sum, series) => sum + Math.max(series.values[category] ?? 0, 0), 0),
      negative: definition.series.reduce((sum, series) => sum + Math.min(series.values[category] ?? 0, 0), 0),
    }));
    minimum = Math.min(0, ...stacks.map(stack => stack.negative));
    maximum = Math.max(0, ...stacks.map(stack => stack.positive));
  }
  minimum = definition.valueAxes?.[0]?.minimum ?? minimum;
  maximum = definition.valueAxes?.[0]?.maximum ?? maximum;
  if (minimum >= maximum) { minimum -= 1; maximum += 1; }
  const ticks = niceTicks(minimum, maximum);
  minimum = ticks[0];
  maximum = ticks[ticks.length - 1];
  const range = Math.max(maximum - minimum, Number.EPSILON);
  const mapY = (value: number) => height - (plotBottom + (value - minimum) / range * plotHeight);
  const zeroY = minimum <= 0 && maximum >= 0 ? mapY(0) : height - plotBottom;
  const slot = plotWidth / Math.max(definition.categories.length, 1);
  const bars = definition.series.map((series, index) => ({ series, index }))
    .filter(item => effectiveType(definition.type, item.series, item.index) === 'bar');

  return (
    <svg role="img" aria-label={ariaLabel} viewBox={`0 0 ${width} ${height}`} width="100%" height="100%"
      style={{ fontFamily, overflow: 'hidden' }}>
      {definition.title && <text x="4" y="12" fontSize="10" fontWeight="bold">{definition.title}</text>}
      {ticks.map(tick => <g key={tick}>
        {definition.valueAxes?.[0]?.gridLines !== false && <line x1={plotX} y1={mapY(tick)} x2={plotX + plotWidth}
          y2={mapY(tick)} stroke="#e2e8f0" strokeWidth="0.5" />}
        <text x="3" y={mapY(tick) + 3} fontSize="7" fill="#334155">{formatValue(tick, definition.locale)}</text>
      </g>)}
      <line x1={plotX} y1={height - plotBottom - plotHeight} x2={plotX} y2={height - plotBottom}
        stroke="#64748b" strokeWidth="0.8" />
      <line x1={plotX} y1={zeroY} x2={plotX + plotWidth} y2={zeroY} stroke="#64748b" strokeWidth="0.8" />
      {definition.categories.map((category, index) => <text key={category} x={plotX + index * slot + 2}
        y={height - plotBottom + 11} fontSize="7" fill="#334155">{category.length <= 14 ? category : `${category.slice(0, 13)}…`}</text>)}
      {definition.type === 'stackedBar'
        ? definition.categories.flatMap((_, categoryIndex) => {
            let positive = 0;
            let negative = 0;
            return definition.series.map((series, seriesIndex) => {
              const value = series.values[categoryIndex];
              if (value === null || value === undefined) return null;
              const start = value >= 0 ? positive : negative;
              const end = start + value;
              if (value >= 0) positive = end; else negative = end;
              const startY = value === 0 ? zeroY : mapY(start);
              const endY = mapY(end);
              return <rect key={`${series.id}-${categoryIndex}`} x={plotX + categoryIndex * slot + slot * 0.18}
                y={Math.min(startY, endY)} width={slot * 0.64} height={Math.max(0.5, Math.abs(endY - startY))}
                fill={color(series, seriesIndex, palette)} stroke={color(series, seriesIndex, palette)} strokeWidth="0.2" />;
            });
          })
        : bars.flatMap(({ series, index: seriesIndex }, barIndex) => definition.categories.map((_, categoryIndex) => {
            const value = series.values[categoryIndex];
            if (value === null || value === undefined) return null;
            const groupWidth = slot * 0.76;
            const barWidth = Math.max(1, groupWidth / Math.max(bars.length, 1));
            const valueY = mapY(value);
            return <rect key={`${series.id}-${categoryIndex}`} x={plotX + categoryIndex * slot + slot * 0.12 + barIndex * barWidth}
              y={Math.min(zeroY, valueY)} width={Math.max(0.5, barWidth - 1)} height={Math.max(0.5, Math.abs(valueY - zeroY))}
              fill={color(series, seriesIndex, palette)} stroke={color(series, seriesIndex, palette)} strokeWidth="0.2" />;
          }))}
      {definition.series.map((series, seriesIndex) => {
        const type = effectiveType(definition.type, series, seriesIndex);
        if (type !== 'line' && type !== 'area') return null;
        const points = series.values.map((value, index) => value === null ? null : ({
          x: plotX + index * slot + slot / 2,
          y: mapY(value),
        })).filter((point): point is { x: number; y: number } => point !== null);
        if (points.length < 2) return null;
        const stroke = color(series, seriesIndex, palette);
        return <g key={series.id}>
          {type === 'area' && <polygon points={[
            `${points[0].x},${zeroY}`,
            ...points.map(point => `${point.x},${point.y}`),
            `${points[points.length - 1].x},${zeroY}`,
          ].join(' ')} fill={stroke} fillOpacity="0.28" stroke={stroke} strokeWidth="0.5" />}
          <polyline points={points.map(point => `${point.x},${point.y}`).join(' ')} fill="none" stroke={stroke} strokeWidth="1.6" />
          {series.showMarkers !== false && points.map((point, index) => <circle key={index} cx={point.x} cy={point.y}
            r="2.2" fill={stroke} stroke={stroke} strokeWidth="0.5" />)}
        </g>;
      })}
      <ChartLegend definition={definition} width={width} height={height} palette={palette} />
    </svg>
  );
}

function ChartLegend({ definition, width, height, palette }: {
  definition: ChartDefinition;
  width: number;
  height: number;
  palette: string[];
}) {
  if (definition.legend?.visible === false) return null;
  let cursor = 4;
  return <g>{definition.series.map((series, index) => {
    if (cursor >= width - 25) return null;
    const current = cursor;
    cursor += Math.min(100, 18 + series.name.length * 4.2);
    const fill = color(series, index, palette);
    return <g key={series.id}>
      <rect x={current} y={height - 12} width="7" height="7" fill={fill} stroke={fill} strokeWidth="0.2" />
      <text x={current + 10} y={height - 5} fontSize="7" fill="#334155">
        {series.name.length <= 18 ? series.name : `${series.name.slice(0, 17)}…`}
      </text>
    </g>;
  })}</g>;
}
