import type { SimpleElement, Page } from '@/types';
import { getTemplateElements, getTemplatePages } from './templateContent';
import { OVERRIDES_BY_LOCALE } from './templateOverrides';

/** Locale-aware `getTemplateElements` — returns a translator-provided override when one
 *  exists for this template id + locale, else falls back to the English content untouched. */
export function getTemplateElementsLocalized(templateId: string, locale: string): SimpleElement[] {
  const override = OVERRIDES_BY_LOCALE[locale]?.elements?.[templateId];
  return override ? override() : getTemplateElements(templateId);
}

/** Locale-aware `getTemplatePages` — same fallback behavior as `getTemplateElementsLocalized`. */
export function getTemplatePagesLocalized(templateId: string, locale: string): Page[] | null {
  const override = OVERRIDES_BY_LOCALE[locale]?.pages?.[templateId];
  return override ? override() : getTemplatePages(templateId);
}
