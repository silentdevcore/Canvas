/**
 * @jest-environment node
 */
import fs from 'node:fs';
import path from 'node:path';

const LANGUAGES = ['en', 'de', 'fr', 'es', 'it', 'ar'] as const;
const LOCALES_ROOT = path.join(process.cwd(), 'src', 'locales');

type Catalog = Record<string, unknown>;

const flatten = (value: Catalog, prefix = '', result: Record<string, unknown> = {}) => {
  Object.entries(value).forEach(([key, child]) => {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (child && typeof child === 'object' && !Array.isArray(child)) {
      flatten(child as Catalog, fullKey, result);
    } else {
      result[fullKey] = child;
    }
  });
  return result;
};

const placeholders = (value: unknown) =>
  Array.from(String(value).matchAll(/{{\s*([^},\s]+).*?}}/g), match => match[1]).sort();

const readCatalog = (language: string, namespace: string) =>
  JSON.parse(fs.readFileSync(path.join(LOCALES_ROOT, language, namespace), 'utf8')) as Catalog;

describe('locale catalogs', () => {
  const namespaces = fs.readdirSync(path.join(LOCALES_ROOT, 'en'))
    .filter(file => file.endsWith('.json'));

  test.each(namespaces)('%s has matching keys and placeholders in every language', namespace => {
    const english = flatten(readCatalog('en', namespace));

    LANGUAGES.slice(1).forEach(language => {
      const translated = flatten(readCatalog(language, namespace));
      expect(Object.keys(translated).sort()).toEqual(Object.keys(english).sort());

      Object.keys(english).forEach(key => {
        expect(placeholders(translated[key])).toEqual(placeholders(english[key]));
      });
    });
  });

  test('critical PDF Viewer actions use domain-correct translations', () => {
    expect(readCatalog('de', 'pdfViewer.json')).toMatchObject({
      flattenPdf: 'PDF-Inhalte reduzieren',
      flattenFields: 'Formularfelder reduzieren',
    });
    expect(readCatalog('fr', 'pdfViewer.json')).toMatchObject({
      ink: 'Dessin à main levée',
    });
    expect(readCatalog('es', 'pdfViewer.json')).toMatchObject({
      redact: 'Censurar',
      applyRedactions: 'Aplicar censuras',
    });
  });
});
