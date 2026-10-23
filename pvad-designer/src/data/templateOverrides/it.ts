import type { LocaleTemplateOverrides } from './index';

// Sparse per-template overrides for this locale. Add one entry per template
// as translations are done — templates without an entry here fall back to
// the English content in `templateContent.ts` via `templateContent.i18n.ts`.
export const templateOverrides: LocaleTemplateOverrides = {
  elements: {},
  pages: {},
};
