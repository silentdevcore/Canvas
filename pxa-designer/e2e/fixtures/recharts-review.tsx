import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LivePreview from '../../src/components/Preview/LivePreview';
import SimplePxaSurface from '../../src/components/Editor/SimplePxaSurface';
import { DEFAULT_PAGE_SETTINGS, useEditorStore } from '../../src/store';
import type { SimpleElement } from '../../src/types';
import editor from '../../src/locales/en/editor.json';
import preview from '../../src/locales/en/preview.json';
import common from '../../src/locales/en/common.json';
import '../../src/styles/index.css';

await i18n.use(initReactI18next).init({
  lng: 'en', fallbackLng: 'en', resources: { en: { editor, preview, common } },
  interpolation: { escapeValue: false }, react: { useSuspense: false },
});

const query = new URLSearchParams(location.search);
const type = query.get('type') as 'bar' | 'line' | 'pie';
if (!['bar', 'line', 'pie'].includes(type)) throw new Error('Unknown fixture chart type');
const variant = query.get('variant') ?? 'standard';
const datasets: Record<string, { labels: string[]; values: (number | null)[][] }> = {
  standard: { labels: ['Jan', 'Feb', 'Mar'], values: [[12, 24, 18], [8, 16, 12]] },
  edge: { labels: ['Low', 'Zero', 'High'], values: type === 'pie'
    ? [[12, 0, 24], [3, 0, 9]] : [[-12, 0, 24], [8, -6, 12]] },
  gaps: { labels: ['First', 'Gap', 'Last'], values: [[12, null, 24], [0, 8, 0]] },
  empty: { labels: [], values: [[], []] },
  long: { labels: ['International department alpha', 'International department beta'], values: [[12, 24], [8, 16]] },
};
const data = datasets[variant];
if (!data) throw new Error('Unknown fixture variant');
const width = query.get('device') === 'mobile' ? 260 : 480;
const element: SimpleElement = {
  id: 'synthetic-chart', name: 'Synthetic chart', type: 'chart', x: 20, y: 20,
  width, height: 300, chartType: type,
  chartData: { labels: data.labels, datasets: data.values.map((values, index) => ({ label: `Series ${index + 1}`, data: values })) },
};
const pages = [{ id: 'synthetic-page', elements: [element] }];
const template = { id: 'synthetic-chart-template', name: 'Chart review fixture', category: 'test', description: '' };
const pageSettings = { ...DEFAULT_PAGE_SETTINGS, width: width + 40, height: 340, gridVisible: false, showMarginGuide: false };
useEditorStore.setState({ pageSettings });
const noop = () => {};
const canvas = query.get('view') === 'canvas';

createRoot(document.getElementById('root')!).render(
  <MemoryRouter>
    {canvas ? <SimplePxaSurface template={template} elements={[element]} pages={pages}
      currentPageIndex={0} sharedElements={[]} onElementAdd={noop} onElementUpdate={noop}
      onElementDelete={noop} onElementReorder={noop} onPreview={noop} onBack={noop}
      onPageAdd={noop} onPageDelete={noop} onPageDuplicate={noop} onPageSelect={noop}
      onPageMove={noop} onSharedElementAdd={noop} onSharedElementUpdate={noop} onSharedElementDelete={noop} />
      : <LivePreview template={template} pages={pages} pageSettings={pageSettings} onBack={noop} onExport={noop} />}
  </MemoryRouter>,
);
