import type { SimpleElement, Page } from '@/types';
import { templateOverrides as de } from './de';
import { templateOverrides as fr } from './fr';
import { templateOverrides as es } from './es';
import { templateOverrides as it } from './it';
import { templateOverrides as ar } from './ar';

export interface LocaleTemplateOverrides {
  /** Per-template element builders, keyed by template id. Only templates a translator has done need an entry. */
  elements?: Partial<Record<string, () => SimpleElement[]>>;
  /** Per-template multi-page builders, keyed by template id (mirrors `getTemplatePages`). */
  pages?: Partial<Record<string, () => Page[]>>;
}

export const OVERRIDES_BY_LOCALE: Partial<Record<string, LocaleTemplateOverrides>> = {
  de, fr, es, it, ar,
};
