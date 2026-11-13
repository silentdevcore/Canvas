export function expectedElementBounds(element, settings = {}) {
  const language = settings.targetLanguage || settings.systemLanguage || 'en';
  const base = language.split('-')[0].toLowerCase();
  const override = element.langOverrides?.[language] ?? element.langOverrides?.[base];
  return Object.fromEntries(['x', 'y', 'width', 'height'].map(key => [key, override?.[key] ?? element[key]]));
}
