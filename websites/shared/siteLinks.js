import { readPvadEnvironment } from '../../tools/configuration/environment.mjs';

const defaultSiteLinks = {
  company: 'http://localhost:5173/',
  documentation: 'http://localhost:5174/',
  demo: 'http://localhost:5175/',
  designer: 'http://localhost:5176/',
  account: 'http://localhost:5178/',
  operator: 'http://localhost:3001/',
};

const productionSiteLinks = {
  company: 'https://powerdoxautomation.com/',
  documentation: 'https://docs.powerdoxautomation.com/',
  demo: 'https://demos.powerdoxautomation.com/',
  designer: 'https://designer.powerdoxautomation.com/',
  account: 'https://account.powerdoxautomation.com/',
  operator: 'https://operator.powerdoxautomation.com/',
};

function normalizeUrl(value) {
  if (!value) return value;
  return value.endsWith('/') ? value : `${value}/`;
}

export function resolveSiteLinks(mode, environment = {}) {
  const defaults = mode === 'production' ? productionSiteLinks : defaultSiteLinks;
  return Object.fromEntries(Object.entries(defaults).map(([name, fallback]) => {
    const key = `VITE_PVAD_${name.toUpperCase()}_URL`;
    const value = readPvadEnvironment(key, environment);
    if (!value) return [name, fallback];
    try {
      const url = new URL(value);
      if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash
        || /[\\\u0000-\u0020\u007f]/.test(value))
        throw new Error('invalid');
    } catch { throw new Error(`Invalid website URL configuration key: ${key}.`); }
    return [name, normalizeUrl(value)];
  }));
}

export const siteLinks = resolveSiteLinks(import.meta.env?.MODE, import.meta.env ?? {});

export const siteLinkDefaults = {
  local: defaultSiteLinks,
  production: productionSiteLinks,
};

export function companyPage(path = '') {
  const cleanPath = path.replace(/^\/+/, '');
  if (!cleanPath) return siteLinks.company;
  const staticPages = new Set([
    'products',
    'products/generator',
    'products/migration',
    'products/importer',
    'products/designer',
    'products/pdf-viewer',
    'products/spreadsheet',
    'pricing',
    'about',
    'support',
    'contact',
    'terms',
    'privacy',
    'cookie-storage',
    'imprint',
    'withdrawal',
    'dpa',
    'license',
  ]);
  const pagePath = staticPages.has(cleanPath) ? `${cleanPath}.html` : cleanPath;
  return `${siteLinks.company}${pagePath}`;
}
