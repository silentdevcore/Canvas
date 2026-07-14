import { companyPage, siteLinks } from './siteLinks.js';

const footerGroups = [
  {
    title: 'Product',
    links: [
      { label: 'Products', href: companyPage('products') },
      { label: 'Generator', href: `${siteLinks.demo}#demo/booking-receipt` },
      { label: 'Migration', href: `${siteLinks.demo}#demo/provider-migration-examples` },
      { label: 'Importer', href: `${siteLinks.demo}#demo/file-importer-flow` },
      { label: 'Designer', href: `${siteLinks.demo}#demo/master-detail-report` },
      { label: 'PDF Viewer', href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms` },
      { label: 'Spreadsheet', href: `${siteLinks.demo}#demo/spreadsheet-import-export` },
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
      { label: 'Privacy', href: companyPage('contact') },
      { label: 'AGB', href: companyPage('contact') },
      { label: 'License', href: companyPage('pricing') },
    ],
  },
];

const currentYear = new Date().getFullYear();

function renderFooterGroup(group) {
  return `
    <nav class="pxa-footer-group" aria-label="${group.title}">
      <strong>${group.title}</strong>
      ${group.links.map((link) => `<a href="${link.href}">${link.label}</a>`).join('')}
    </nav>
  `;
}

export function renderPxaFooter(siteName) {
  return `
    <footer class="pxa-site-footer">
      <div class="pxa-container pxa-footer">
        <div class="pxa-footer-brand">
          <strong>Power Dox Automation</strong>
          <p>${siteName} is part of the PXA product, documentation, and demo system.</p>
        </div>
        <div class="pxa-footer-grid">
          ${footerGroups.map(renderFooterGroup).join('')}
        </div>
        <div class="pxa-footer-bottom">
          <span>Copyright © ${currentYear} Power Dox Automation. All rights reserved.</span>
          <span><a href="${companyPage('contact')}">AGB</a> · <a href="${companyPage('contact')}">Privacy</a></span>
        </div>
      </div>
    </footer>
  `;
}
