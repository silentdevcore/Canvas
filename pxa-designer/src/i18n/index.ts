import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import resourcesToBackend from 'i18next-resources-to-backend';
import pdfViewerEn from '../locales/en/pdfViewer.json';
import pdfViewerDe from '../locales/de/pdfViewer.json';

export const SUPPORTED_LANGUAGES = ['en', 'de', 'fr', 'es', 'it', 'ar'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

export const RTL_LANGUAGES: SupportedLanguage[] = ['ar'];

export const NAMESPACES = [
  'common', 'home', 'templates', 'gallery', 'editor', 'create', 'importer',
  'convert', 'spreadsheet', 'migrations', 'docs', 'onboarding', 'pdfViewer',
  'codeEditor', 'preview',
] as const;

i18n
  .use(LanguageDetector)
  .use(
    resourcesToBackend((language: string, namespace: string) =>
      import(`../locales/${language}/${namespace}.json`)),
  )
  .use(initReactI18next)
  .init({
    fallbackLng: 'en',
    supportedLngs: SUPPORTED_LANGUAGES,
    ns: NAMESPACES,
    defaultNS: 'common',
    returnEmptyString: false,
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'pxa_locale',
      caches: ['localStorage'],
    },
    // `pdfViewer` has real, pre-authored en/de content (migrated from the old
    // features/pdf-viewer/i18n.ts) and PdfViewer.tsx can render standalone
    // outside any <Suspense> boundary (see pdfViewerSmoke.test.tsx) — preload
    // it synchronously so it never needs the async backend/Suspense path.
    // `partialBundledLanguages` is required so the backend loader above still
    // runs for every other namespace/language not covered by `resources`.
    partialBundledLanguages: true,
    resources: {
      en: { pdfViewer: pdfViewerEn },
      de: { pdfViewer: pdfViewerDe },
    },
    react: { useSuspense: true },
  });

export default i18n;
