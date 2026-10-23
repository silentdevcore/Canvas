import { companyPage, siteLinks } from './siteLinks.js';
import { pvadCommit, pvadVersion } from './buildInfo.js';
import { pvadBrand } from './brand.js';

const footerGroups = [
  {
    title: 'Product',
    links: [
      { label: 'Products', href: companyPage('products') },
      { label: pvadBrand.products.pdf, href: companyPage('products/generator') },
      { label: 'Migration', href: companyPage('products/migration') },
      { label: 'Importer', href: companyPage('products/importer') },
      { label: 'Designer', href: companyPage('products/designer') },
      { label: 'PDF Viewer', href: companyPage('products/pdf-viewer') },
      { label: pvadBrand.products.spreadsheet, href: companyPage('products/spreadsheet') },
    ],
  },
  {
    title: 'Resources',
    links: [
      { label: 'Documentation', href: siteLinks.documentation },
      { label: 'Demo Gallery', href: siteLinks.demo },
      { label: 'Demo Examples', href: `${siteLinks.documentation}#demo-examples` },
      { label: 'API Reference', href: `${siteLinks.documentation}#api-reference` },
      { label: 'Migration Guides', href: `${siteLinks.documentation}#migration` },
    ],
  },
  {
    title: 'Company',
    links: [
      { label: 'About', href: companyPage('about') },
      { label: 'Pricing', href: companyPage('pricing') },
      { label: 'Support', href: companyPage('support') },
      { label: 'Contact Sales', href: companyPage('contact') },
    ],
  },
  {
    title: 'Developers',
    links: [
      { label: 'Provider Taxonomy', href: `${siteLinks.documentation}#migration` },
      { label: 'Security Notes', href: `${siteLinks.demo}SECURITY_AND_HOSTING.md` },
      { label: 'Hosting Notes', href: companyPage('support') },
      { label: 'Release Notes', href: `${siteLinks.documentation}#history` },
    ],
  },
  {
    title: 'Legal',
    links: [
      { label: 'Terms', href: companyPage('terms') },
      { label: 'Privacy', href: companyPage('privacy') },
      { label: 'Cookie & storage', href: companyPage('cookie-storage') },
      { label: 'Imprint', href: companyPage('imprint') },
      { label: 'Consumer withdrawal', href: companyPage('withdrawal') },
      { label: 'Data processing agreement', href: companyPage('dpa') },
      { label: 'License', href: companyPage('license') },
    ],
  },
];

const currentYear = new Date().getFullYear();

function renderFooterGroup(group) {
  return `
    <nav class="pvad-footer-group" aria-label="${group.title}">
      <strong>${group.title}</strong>
      ${group.links.map((link) => `<a href="${link.href}">${link.label}</a>`).join('')}
    </nav>
  `;
}

export function renderPvadFooter(siteName) {
  return `
    <footer class="pvad-site-footer">
      <div class="pvad-container pvad-footer">
        <div class="pvad-footer-brand">
          <strong>${pvadBrand.displayName}</strong>
          <p>${siteName} is part of the ${pvadBrand.displayName} product, documentation, and demo system.</p>
        </div>
        <div class="pvad-footer-grid">
          ${footerGroups.map(renderFooterGroup).join('')}
        </div>
        <div class="pvad-footer-bottom">
          <span>Copyright © ${currentYear} ${pvadBrand.displayName}. All rights reserved.</span>
          <span>
            <span title="Commit ${pvadCommit}">${pvadBrand.displayName} ${pvadVersion}</span> ·
            <a href="${companyPage('terms')}">Terms</a> ·
            <a href="${companyPage('privacy')}">Privacy</a> ·
            <button type="button" class="pvad-footer-link-button" data-pvad-storage-settings>Storage settings</button>
          </span>
        </div>
      </div>
    </footer>
  `;
}
