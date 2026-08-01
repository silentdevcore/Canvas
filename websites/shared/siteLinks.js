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

function envLink(name) {
  return normalizeUrl(import.meta.env?.[name]);
}

export const siteLinks =
  import.meta.env?.MODE === 'production'
    ? {
        company: envLink('VITE_PXA_COMPANY_URL') || productionSiteLinks.company,
        documentation: envLink('VITE_PXA_DOCUMENTATION_URL') || productionSiteLinks.documentation,
        demo: envLink('VITE_PXA_DEMO_URL') || productionSiteLinks.demo,
        designer: envLink('VITE_PXA_DESIGNER_URL') || productionSiteLinks.designer,
        account: envLink('VITE_PXA_ACCOUNT_URL') || productionSiteLinks.account,
        operator: envLink('VITE_PXA_OPERATOR_URL') || productionSiteLinks.operator,
      }
    : defaultSiteLinks;

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
