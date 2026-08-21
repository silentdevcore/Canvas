import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { designerAssetContentUrl } from '@/services/designerAssetApi';
import { CHART_PALETTE, CHART_TYPES, normalizeChartDefinition } from './model';
import type { ChartDefinition, ChartSeries, PxaChartType } from './types';

type ChartInspectorTab = 'data' | 'series' | 'appearance' | 'axes' | 'binding' | 'advanced';

interface ChartInspectorProps {
  chart?: ChartDefinition;
  legacyType?: PxaChartType;
  legacyData?: Record<string, unknown>;
  onChange: (chart: ChartDefinition) => void;
  onRestoreSource?: (assetId: string) => void;
}

const numberValue = (value: string): number | null => {
  if (value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

export const ChartInspector: React.FC<ChartInspectorProps> = ({
  chart, legacyType, legacyData, onChange, onRestoreSource
}) => {
  const { t } = useTranslation('editor');
  const definition = useMemo(
    () => normalizeChartDefinition(chart, legacyType, legacyData),
    [chart, legacyData, legacyType]
  );
  const [tab, setTab] = useState<ChartInspectorTab>('data');
  const [advancedDraft, setAdvancedDraft] = useState(() => JSON.stringify(definition, null, 2));
  const [advancedError, setAdvancedError] = useState('');

  useEffect(() => {
    setAdvancedDraft(JSON.stringify(definition, null, 2));
    setAdvancedError('');
  }, [definition]);

  const commit = (update: (next: ChartDefinition) => void) => {
    const next = structuredClone(definition);
    update(next);
    onChange(normalizeChartDefinition(next));
  };
  const tabs: ChartInspectorTab[] = ['data', 'series', 'appearance', 'axes', 'binding', 'advanced'];
  const recognition = definition.recognition;

  return (
    <div className="editor-form-stack">
      {recognition && recognition.status !== 'native' && (
        <section style={{ border: '1px solid #f59e0b', padding: 8, background: '#fffbeb' }}>
          <strong>{t('elementInspector.chart.recognitionTitle', { defaultValue: 'PDF chart recognition' })}</strong>
          <div style={{ fontSize: 11, marginTop: 4 }}>
            {t('elementInspector.chart.confidence', { defaultValue: 'Confidence' })}: {Math.round(recognition.confidence * 100)}%
            {' · '}{t(`elementInspector.chart.status.${recognition.status}`, { defaultValue: recognition.status })}
          </div>
          {recognition.sourceAssetId && (
            <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
              <a className="editor-toggle-btn" href={designerAssetContentUrl(recognition.sourceAssetId)} target="_blank" rel="noreferrer">
                {t('elementInspector.chart.compareOriginal', { defaultValue: 'Compare original' })}
              </a>
              <button type="button" className="editor-toggle-btn" onClick={() => onRestoreSource?.(recognition.sourceAssetId!)}>
                {t('elementInspector.chart.restoreOriginal', { defaultValue: 'Restore original' })}
              </button>
            </div>
          )}
        </section>
      )}

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }} role="tablist">
        {tabs.map(item => (
          <button key={item} type="button" role="tab" aria-selected={tab === item}
            className={`editor-toggle-btn${tab === item ? ' active' : ''}`} onClick={() => setTab(item)}>
            {t(`elementInspector.chart.tabs.${item}`, { defaultValue: item[0].toUpperCase() + item.slice(1) })}
          </button>
        ))}
      </div>

      {tab === 'data' && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
            <thead><tr><th>{t('elementInspector.chart.category', { defaultValue: 'Category' })}</th>
              {definition.series.map(series => <th key={series.id}>{series.name}</th>)}</tr></thead>
            <tbody>
              {definition.categories.map((category, rowIndex) => (
                <tr key={`${rowIndex}-${category}`}>
                  <td><input value={category} aria-label={`Category ${rowIndex + 1}`} onChange={event => commit(next => {
                    next.categories[rowIndex] = event.target.value;
                  })} /></td>
                  {definition.series.map((series, seriesIndex) => (
                    <td key={series.id}><input type="number" value={series.values[rowIndex] ?? ''}
                      aria-label={`${series.name} ${category}`} onChange={event => commit(next => {
                        next.series[seriesIndex].values[rowIndex] = numberValue(event.target.value);
                      })} /></td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="editor-toggle-btn" onClick={() => commit(next => {
            next.categories.push(`Category ${next.categories.length + 1}`);
            next.series.forEach(series => series.values.push(null));
          })}>{t('elementInspector.chart.addCategory', { defaultValue: 'Add category' })}</button>
        </div>
      )}

      {tab === 'series' && (
        <div className="editor-form-stack">
          {definition.series.map((series, index) => (
            <section key={series.id} style={{ borderTop: '1px solid #e2e8f0', paddingTop: 8 }}>
              <label><span>{t('elementInspector.chart.seriesName', { defaultValue: 'Series name' })}</span>
                <input value={series.name} onChange={event => commit(next => { next.series[index].name = event.target.value; })} /></label>
              <label><span>{t('elementInspector.chart.seriesType', { defaultValue: 'Series type' })}</span>
                <select value={series.type || definition.type} onChange={event => commit(next => {
                  next.series[index].type = event.target.value as PxaChartType;
                })}>{CHART_TYPES.filter(type => !['pie', 'doughnut', 'stackedBar', 'combo'].includes(type))
                    .map(type => <option key={type} value={type}>{t(`elementInspector.chart.types.${type}`)}</option>)}</select></label>
              <label><span>{t('elementInspector.chart.color', { defaultValue: 'Color' })}</span>
                <input type="color" value={series.color || CHART_PALETTE[index % CHART_PALETTE.length]}
                  onChange={event => commit(next => { next.series[index].color = event.target.value; })} /></label>
              <button type="button" className="editor-toggle-btn" disabled={definition.series.length === 1}
                onClick={() => commit(next => { next.series.splice(index, 1); })}>
                {t('elementInspector.chart.removeSeries', { defaultValue: 'Remove series' })}
              </button>
            </section>
          ))}
          <button type="button" className="editor-toggle-btn" onClick={() => commit(next => {
            const index = next.series.length;
            const series: ChartSeries = {
              id: `series-${Date.now()}`,
              name: `Series ${index + 1}`,
              values: next.categories.map(() => null),
              color: CHART_PALETTE[index % CHART_PALETTE.length],
              valueAxisId: 'primary',
              showMarkers: true
            };
            next.series.push(series);
          })}>{t('elementInspector.chart.addSeries', { defaultValue: 'Add series' })}</button>
        </div>
      )}

      {tab === 'appearance' && (
        <div className="editor-form-stack">
          <label><span>{t('elementInspector.chart.chartType')}</span><select value={definition.type}
            onChange={event => commit(next => { next.type = event.target.value as PxaChartType; })}>
            {CHART_TYPES.map(type => <option key={type} value={type}>{t(`elementInspector.chart.types.${type}`)}</option>)}
          </select></label>
          <label><span>{t('elementInspector.chart.title', { defaultValue: 'Title' })}</span>
            <input value={definition.title || ''} onChange={event => commit(next => { next.title = event.target.value || undefined; })} /></label>
          <label><span>{t('elementInspector.chart.subtitle', { defaultValue: 'Subtitle' })}</span>
            <input value={definition.subtitle || ''} onChange={event => commit(next => { next.subtitle = event.target.value || undefined; })} /></label>
          <label><span>{t('elementInspector.chart.locale')}</span>
            <input value={definition.locale || ''} placeholder="en-US" onChange={event => commit(next => { next.locale = event.target.value || undefined; })} /></label>
          <label><input type="checkbox" checked={definition.legend?.visible ?? true} onChange={event => commit(next => {
            next.legend = { visible: event.target.checked, position: next.legend?.position || 'bottom' };
          })} /> {t('elementInspector.chart.showLegend', { defaultValue: 'Show legend' })}</label>
          <label><input type="checkbox" checked={definition.dataLabels?.visible ?? false} onChange={event => commit(next => {
            next.dataLabels = { ...next.dataLabels, visible: event.target.checked };
          })} /> {t('elementInspector.chart.showDataLabels', { defaultValue: 'Show data labels' })}</label>
        </div>
      )}

      {tab === 'axes' && (() => {
        const axis = definition.valueAxes?.[0] || { id: 'primary', visible: true, gridLines: true };
        return <div className="editor-form-stack">
          <label><span>{t('elementInspector.chart.minimum', { defaultValue: 'Minimum' })}</span>
            <input type="number" value={axis.minimum ?? ''} onChange={event => commit(next => {
              next.valueAxes = [{ ...axis, minimum: numberValue(event.target.value) ?? undefined }];
            })} /></label>
          <label><span>{t('elementInspector.chart.maximum', { defaultValue: 'Maximum' })}</span>
            <input type="number" value={axis.maximum ?? ''} onChange={event => commit(next => {
              next.valueAxes = [{ ...axis, maximum: numberValue(event.target.value) ?? undefined }];
            })} /></label>
          <label><input type="checkbox" checked={axis.gridLines ?? true} onChange={event => commit(next => {
            next.valueAxes = [{ ...axis, gridLines: event.target.checked }];
          })} /> {t('elementInspector.chart.gridLines', { defaultValue: 'Grid lines' })}</label>
        </div>;
      })()}

      {tab === 'binding' && (
        <div className="editor-form-stack">
          {(['dataPath', 'categoryField', 'seriesField', 'valueField'] as const).map(key => (
            <label key={key}><span>{t(`elementInspector.chart.${key}`, { defaultValue: key })}</span>
              <input value={definition.binding?.[key] || ''} onChange={event => commit(next => {
                next.binding = { ...next.binding, [key]: event.target.value || undefined };
              })} /></label>
          ))}
        </div>
      )}

      {tab === 'advanced' && (
        <label><span>{t('elementInspector.chart.chartData')}</span>
          <textarea rows={14} value={advancedDraft} aria-invalid={Boolean(advancedError)} onChange={event => {
            setAdvancedDraft(event.target.value);
            try {
              const parsed = JSON.parse(event.target.value);
              onChange(normalizeChartDefinition(parsed));
              setAdvancedError('');
            } catch {
              setAdvancedError(t('elementInspector.chart.invalidJson', { defaultValue: 'Invalid chart JSON' }));
            }
          }} />
          {advancedError && <small role="alert" style={{ color: '#b91c1c' }}>{advancedError}</small>}
        </label>
      )}
    </div>
  );
};

export default ChartInspector;
