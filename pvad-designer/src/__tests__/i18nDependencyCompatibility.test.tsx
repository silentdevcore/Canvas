/** @jest-environment jsdom */
import fs from 'node:fs';
import path from 'node:path';
import { act, render, screen } from '@testing-library/react';
import { createInstance } from 'i18next';
import { I18nextProvider, initReactI18next, Trans, useTranslation } from 'react-i18next';

const languages = ['en', 'de', 'fr', 'es', 'it', 'ar'] as const;
const resources = Object.fromEntries(languages.map(language => [language,
  Object.fromEntries(['common', 'editor'].map(namespace => [namespace,
    JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src/locales', language, `${namespace}.json`), 'utf8')),
  ])),
]));

async function instance() {
  const i18n = createInstance().use(initReactI18next);
  await i18n.init({ resources, lng: 'en', fallbackLng: 'en', defaultNS: 'common',
    ns: ['common', 'editor'], interpolation: { escapeValue: false }, react: { useSuspense: true } });
  return i18n;
}

function Probe({ namespaces = ['common', 'editor'] }: { namespaces?: string[] }) {
  const { t, i18n } = useTranslation(namespaces, { nsMode: 'fallback' });
  return <section data-testid="locale" dir={i18n.dir()}>
    <span data-testid="home">{t('nav.home')}</span>
    <span data-testid="chart">{t('diagnostics.chart')}</span>
    <span data-testid="notifications">{t('productExperience.openNotifications', { count: 2 })}</span>
  </section>;
}

test.each(languages)('real catalog language switching and direction: %s', async language => {
  const i18n = await instance();
  render(<I18nextProvider i18n={i18n}><Probe /></I18nextProvider>);
  await act(async () => { await i18n.changeLanguage(language); });
  expect(screen.getByTestId('home').textContent).toBe(resources[language].common.nav.home);
  expect(screen.getByTestId('chart').textContent).toBe(resources[language].editor.diagnostics.chart);
  expect(screen.getByTestId('notifications').textContent).toBe(
    resources[language].common.productExperience.openNotifications.replace('{{count}}', '2'));
  expect(screen.getByTestId('locale').getAttribute('dir')).toBe(language === 'ar' ? 'rtl' : 'ltr');
});

test('adding and removing fallback namespaces refreshes the same mounted hook', async () => {
  const i18n = await instance();
  const view = (namespaces: string[]) => <I18nextProvider i18n={i18n}><Probe namespaces={namespaces} /></I18nextProvider>;
  const { rerender } = render(view(['common']));
  expect(screen.getByTestId('chart').textContent).toBe('diagnostics.chart');
  rerender(view(['common', 'editor']));
  expect(screen.getByTestId('chart').textContent).toBe(resources.en.editor.diagnostics.chart);
  rerender(view(['common']));
  expect(screen.getByTestId('chart').textContent).toBe('diagnostics.chart');
});

test('Trans retains component markup and renders interpolation as literal text', async () => {
  const i18n = await instance();
  i18n.addResourceBundle('en', 'synthetic', {
    export: 'Export <strong>{{name}}</strong> as {{format}}',
  });
  render(<I18nextProvider i18n={i18n}><div data-testid="translation">
    <Trans ns="synthetic" i18nKey="export" values={{ name: '<img src=x onerror=alert(1)>', format: 'PDF' }}
      components={{ strong: <strong /> }} />
  </div></I18nextProvider>);
  const output = screen.getByTestId('translation');
  expect(output.querySelector('strong')?.textContent).toBe('<img src=x onerror=alert(1)>');
  expect(output.textContent).toBe('Export <img src=x onerror=alert(1)> as PDF');
  expect(output.querySelector('img')).toBeNull();
});
