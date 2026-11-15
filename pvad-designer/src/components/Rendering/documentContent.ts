import type { LocalizedProperty, SimpleElement } from '@/types';
import { formatDocumentDate } from './documentDate';

export type DocumentContentResolver = (value: string | undefined) => string | undefined;
export const identityDocumentContent: DocumentContentResolver = value => value;
export const baseDocumentLanguage = (value?: string) => value?.split('-')[0].toLowerCase() ?? '';

export function createDocumentContentResolver(properties: LocalizedProperty[], language: string, systemLanguage: string,
  customProperties: Array<{ name: string; value: string }> = []): DocumentContentResolver {
  const target = baseDocumentLanguage(language || systemLanguage);
  const system = baseDocumentLanguage(systemLanguage);
  const values = new Map<string, string>();
  for (const property of properties) {
    const owner = baseDocumentLanguage(property.ownerLanguage);
    if (property.scope === 'own' && owner !== target) continue;
    const value = property.scope === 'own' ? property.localizedValues[owner] ?? ''
      : property.localizedValues[target] || property.localizedValues[system] || '';
    values.set(property.key.toLowerCase(), value);
  }
  for (const property of customProperties) if (property.name.trim()) values.set(property.name.toLowerCase(), property.value ?? '');
  return value => value?.replace(/\{\{([^{}]+)\}\}/g,
    (token, key: string) => values.get(key.toLowerCase()) ?? token);
}

// Match the backend's localized string fields; stable IDs and form names are not display content.
export const DOCUMENT_LOCALIZED_FIELDS = ['content', 'htmlContent', 'fieldLabel', 'signatureLabel', 'qrValue', 'barcodeValue',
  'noteTitle', 'noteBody', 'footnoteText', 'href', 'buttonAction'] as const;

export function resolveDocumentElement(element: SimpleElement, resolve: DocumentContentResolver): SimpleElement {
  let result = element;
  for (const key of DOCUMENT_LOCALIZED_FIELDS) {
    if (element[key] === undefined) continue;
    const value = resolve(element[key]);
    if (value !== element[key]) result = { ...result, [key]: value };
  }
  return result;
}

export function effectiveDocumentElement(element: SimpleElement, language: string): SimpleElement {
  const override = element.langOverrides?.[language] ?? element.langOverrides?.[baseDocumentLanguage(language)];
  if (!override) return element;
  return { ...element, x: override.x ?? element.x, y: override.y ?? element.y,
    width: override.width ?? element.width, height: override.height ?? element.height,
    style: override.rotation == null ? element.style : { ...element.style, rotation: override.rotation } };
}

export const elementMatchesDocumentLanguage = (element: SimpleElement, language: string) =>
  !element.elementLanguage || baseDocumentLanguage(element.elementLanguage) === baseDocumentLanguage(language);

export function documentDateText(element: SimpleElement, now = new Date(), resolve?: (value: string) => string | undefined): string {
  return formatDocumentDate(element, now, resolve).text;
}
