import './site.css';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';

const companyRoutes = {
  '/': {
    section: null,
    title: 'PXA.Company | Power Dox Automation',
    description: 'Power Dox Automation marketing site for document generation, migration, import, viewer, and spreadsheet workflows.',
  },
  '/products': {
    section: 'products',
    title: 'Products | Power Dox Automation',
    description: 'Explore the PXA product family: Generator, Migration, Importer, Designer, PDF Viewer, and Spreadsheet workflows.',
  },
  '/products.html': {
    section: 'products',
    title: 'Products | Power Dox Automation',
    description: 'Explore the PXA product family: Generator, Migration, Importer, Designer, PDF Viewer, and Spreadsheet workflows.',
  },
  '/pricing': {
    section: 'pricing',
    title: 'Pricing | Power Dox Automation',
    description: 'Review placeholder Trial, Team, and Enterprise paths for Power Dox Automation.',
  },
  '/pricing.html': {
    section: 'pricing',
    title: 'Pricing | Power Dox Automation',
    description: 'Review placeholder Trial, Team, and Enterprise paths for Power Dox Automation.',
  },
  '/about': {
    section: 'about',
    title: 'About | Power Dox Automation',
    description: 'Learn how Power Dox Automation connects product, documentation, demos, and migration-first document workflows.',
  },
  '/about.html': {
    section: 'about',
    title: 'About | Power Dox Automation',
    description: 'Learn how Power Dox Automation connects product, documentation, demos, and migration-first document workflows.',
  },
  '/support': {
    section: 'support',
    title: 'Support | Power Dox Automation',
    description: 'Find support paths through PXA documentation, demos, provider parity planning, and migration guidance.',
  },
  '/support.html': {
    section: 'support',
    title: 'Support | Power Dox Automation',
    description: 'Find support paths through PXA documentation, demos, provider parity planning, and migration guidance.',
  },
  '/contact': {
    section: 'contact',
    title: 'Contact Sales | Power Dox Automation',
    description: 'Contact the Power Dox Automation team to plan product evaluation, migration work, or enterprise rollout.',
  },
  '/contact.html': {
    section: 'contact',
    title: 'Contact Sales | Power Dox Automation',
    description: 'Contact the Power Dox Automation team to plan product evaluation, migration work, or enterprise rollout.',
  },
  '/terms.html': {
    section: 'terms',
    title: 'Terms | Power Dox Automation',
    description: 'Review placeholder terms for evaluating and using Power Dox Automation.',
  },
  '/privacy.html': {
    section: 'privacy',
    title: 'Privacy | Power Dox Automation',
    description: 'Review placeholder privacy information for Power Dox Automation.',
  },
  '/license.html': {
    section: 'license',
    title: 'License | Power Dox Automation',
    description: 'Review placeholder license information for Power Dox Automation.',
  },
};

const fallbackRoute = companyRoutes['/'];

const products = [
  {
    slug: 'generator',
    title: 'PXA Generator',
    label: 'Generate',
    text: 'Create PDFs and document output from code, templates, structured data, and reusable layouts.',
    points: ['PDF generation', 'Template-driven output', 'Reusable document primitives'],
    audience: 'Engineering teams that need reliable server-side document output.',
    detail:
      'PXA Generator is the code-first foundation for producing PDF and document output from structured data, reusable templates, and shared layout primitives.',
    demo: 'booking-receipt',
    docs: 'code-path',
  },
  {
    slug: 'migration',
    title: 'PXA Migration',
    label: 'Migrate',
    text: 'Move existing PDF code, report designs, and spreadsheet workflows from known providers into PXA.',
    points: ['Provider mapping', 'Code conversion', 'Designer migration'],
    audience: 'Teams replacing provider-specific SDKs, reports, or document automation code.',
    detail:
      'PXA Migration tracks provider parity, diagnostics, and conversion flows so legacy document code and report designs can move toward PXA deliberately.',
    demo: 'provider-migration-examples',
    docs: 'migration',
  },
  {
    slug: 'importer',
    title: 'PXA Importer',
    label: 'Import',
    text: 'Bring PDF, Office, image, and document inputs into normalized PXA flows.',
    points: ['File normalization', 'Office/PDF inputs', 'Import diagnostics'],
    audience: 'Product teams that need to accept existing files and turn them into usable document models.',
    detail:
      'PXA Importer normalizes incoming files so PDF, Office, image, and related inputs can participate in generation, migration, and designer workflows.',
    demo: 'file-importer-flow',
    docs: 'editor-path',
  },
  {
    slug: 'designer',
    title: 'PXA Designer',
    label: 'Design',
    text: 'Design document templates, preview output, and inspect generated JSON and code.',
    points: ['Visual editing', 'Live preview', 'JSON/code inspection'],
    audience: 'Teams that need visual template editing connected to generated output.',
    detail:
      'PXA Designer gives document-heavy teams a visual authoring surface for templates, reports, previews, JSON inspection, and code-oriented handoff.',
    demo: 'master-detail-report',
    docs: 'editor-path',
  },
  {
    slug: 'pdf-viewer',
    title: 'PXA PDF Viewer',
    label: 'Review',
    text: 'Review, annotate, fill forms, and inspect PDF workflows in the browser.',
    points: ['Annotations', 'Forms', 'Review workflows'],
    audience: 'Applications that need browser review, forms, annotations, and PDF inspection.',
    detail:
      'PXA PDF Viewer is the browser-facing review surface for generated or imported PDFs, with forms, annotations, and workflow inspection as product goals.',
    demo: 'pdf-viewer-annotations-forms',
    docs: 'pdf-viewer',
  },
  {
    slug: 'spreadsheet',
    title: 'PXA Spreadsheet',
    label: 'Model',
    text: 'Import, edit, map, and export workbook-driven document automation flows.',
    points: ['Workbook import', 'Formula-ready data', 'Export flows'],
    audience: 'Teams whose document automation depends on workbook data, mapping, or export workflows.',
    detail:
      'PXA Spreadsheet connects workbook-driven data and document automation, covering import, editing, mapping, formulas, and export-oriented workflows.',
    demo: 'spreadsheet-import-export',
    docs: 'spreadsheet',
  },
];

for (const product of products) {
  companyRoutes[`/products/${product.slug}.html`] = {
    section: `product:${product.slug}`,
    title: `${product.title} | Power Dox Automation`,
    description: product.text,
  };
}

const useCases = [
  {
    title: 'Replace legacy PDF SDK code',
    text: 'Plan migrations from provider-specific APIs into PXA code patterns with tracked compatibility gaps.',
  },
  {
    title: 'Modernize report designers',
    text: 'Bring report layouts, bands, charts, and data-bound elements into a unified design model.',
  },
  {
    title: 'Create document workflows',
    text: 'Combine templates, imports, spreadsheets, and generated output into repeatable automation paths.',
  },
  {
    title: 'Inspect output in the browser',
    text: 'Use viewer, demo, and designer surfaces to validate generated PDFs before integrating deeper.',
  },
];

const providers = ['DevExpress', 'Syncfusion', 'ActiveReports', 'JasperReports', 'GemBox', 'Aspose', 'iText', 'PDF Tools'];

const proofPoints = [
  { value: '3', label: 'web properties planned' },
  { value: '20+', label: 'migration tracks documented' },
  { value: '6', label: 'core product areas' },
];

const aboutPrinciples = [
  {
    title: 'Built for document-heavy teams',
    text: 'PXA focuses on generation, migration, import, review, and spreadsheet-backed workflows where documents are part of the product.',
  },
  {
    title: 'Migration-first thinking',
    text: 'Provider parity, diagnostics, and practical migration paths are tracked as first-class product work.',
  },
  {
    title: 'Docs and demos stay connected',
    text: 'Every public workflow should connect marketing, documentation, runnable examples, and implementation status.',
  },
];

const roadmap = [
  {
    title: 'PXA.Company',
    text: 'Marketing site for product positioning, proof, pricing paths, and contact.',
    status: 'Ready to shape',
  },
  {
    title: 'PXA.Documentation',
    text: 'Editor and SDK documentation with product-first navigation and API references.',
    status: 'Planned',
  },
  {
    title: 'PXA.Demo',
    text: 'Interactive examples for generator, viewer, migration, spreadsheet, and import flows.',
    status: 'Planned',
  },
];

const showcases = [
  {
    title: 'Invoice / Booking Receipt',
    text: 'Business document generation with structured data, reusable layout, and demo-ready preview.',
    status: 'Ready',
    route: 'booking-receipt',
    docs: 'demo-examples',
  },
  {
    title: 'Report migration gallery',
    text: 'Provider-focused examples for DevExpress, Syncfusion RDL, ActiveReports, JasperReports, and more.',
    status: 'Preview',
    route: 'master-detail-report',
    docs: 'migration',
  },
  {
    title: 'PDF viewer workflows',
    text: 'Annotation, form, and review scenarios that connect product demos with implementation docs.',
    status: 'Preview',
    route: 'pdf-viewer-annotations-forms',
    docs: 'pdf-viewer',
  },
];

const pricingTiers = [
  {
    title: 'Trial',
    text: 'Explore the product family, run demos, and validate migration fit before committing.',
    cta: 'Start with demos',
    href: siteLinks.demo,
  },
  {
    title: 'Team',
    text: 'Adopt PXA for product teams that need generator, designer, importer, and viewer workflows.',
    cta: 'Read docs',
    href: siteLinks.documentation,
  },
  {
    title: 'Enterprise',
    text: 'Plan larger migrations, provider parity work, support, and rollout guidance with the PXA team.',
    cta: 'Contact sales',
    href: companyPage('contact'),
  },
];

function renderCards(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-product-card">
          <span class="pxa-product-card__label">${item.label}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <ul>
            ${item.points.map((point) => `<li>${point}</li>`).join('')}
          </ul>
          <div class="pxa-company-card-actions">
            <a href="${companyPage(`products/${item.slug}`)}" aria-label="Open ${item.title} product page">Product page</a>
            <a href="${siteLinks.demo}#demo/${item.demo}" aria-label="Open ${item.title} demo">Open demo</a>
            <a href="${siteLinks.documentation}#${item.docs}" aria-label="Read documentation for ${item.title}">Read docs</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function renderBadges(items) {
  return items.map((item) => `<span class="pxa-status pxa-status--preview">${item}</span>`).join('');
}

function renderUseCases(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-use-card">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
        </article>
      `,
    )
    .join('');
}

function renderProof(items) {
  return items
    .map(
      (item) => `
        <div>
          <strong>${item.value}</strong>
          <span>${item.label}</span>
        </div>
      `,
    )
    .join('');
}

function renderAboutPrinciples(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-about-card">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
        </article>
      `,
    )
    .join('');
}

function renderRoadmap(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-roadmap-card">
          <span class="pxa-status ${item.status === 'Ready to shape' ? 'pxa-status--ready' : 'pxa-status--planned'}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
        </article>
      `,
    )
    .join('');
}

function renderShowcases(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-showcase-card">
          <span class="pxa-status ${item.status === 'Ready' ? 'pxa-status--ready' : 'pxa-status--preview'}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <div class="pxa-company-card-actions">
            <a href="${siteLinks.demo}#demo/${item.route}">Open demo</a>
            <a href="${siteLinks.documentation}#${item.docs}">Read docs</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function renderPricing(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-pricing-card">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <a class="pxa-button ${item.title === 'Enterprise' ? 'pxa-button--primary' : 'pxa-button--secondary'}" href="${item.href}">${item.cta}</a>
        </article>
      `,
    )
    .join('');
}

function updateRouteMetadata(route) {
  document.title = route.title;
  const description = document.querySelector('meta[name="description"]');
  if (description) {
    description.setAttribute('content', route.description);
  }
}

const currentRoute = companyRoutes[window.location.pathname] || fallbackRoute;
updateRouteMetadata(currentRoute);

function activeAttr(section) {
  if (!section && !currentRoute.section) return ' aria-current="page"';
  if (section === 'products' && currentRoute.section?.startsWith('product:')) return ' aria-current="page"';
  return currentRoute.section === section ? ' aria-current="page"' : '';
}

function renderHeader() {
  return `
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="${siteLinks.company}" aria-label="PXA.Company home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Company</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}"${activeAttr(null)}>Company</a>
          <a href="${companyPage('products')}"${activeAttr('products')}>Products</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="${companyPage('pricing')}"${activeAttr('pricing')}>Pricing</a>
          <a href="${companyPage('about')}"${activeAttr('about')}>About</a>
          <a href="${companyPage('support')}"${activeAttr('support')}>Support</a>
        </nav>
        <a class="pxa-button pxa-button--primary pxa-header-cta" href="${companyPage('contact')}"${activeAttr('contact')}>Contact sales</a>
      </div>
    </header>
  `;
}

function renderHeroSection() {
  return `
    <section class="pxa-company-hero">
      <div class="pxa-container pxa-company-hero__grid">
        <div>
          <p class="pxa-kicker">Power Dox Automation</p>
          <h1 class="pxa-heading">Build, migrate, and sell document automation with one PXA platform.</h1>
          <p class="pxa-lede">
            PXA brings PDF generation, provider migration, file import, spreadsheet workflows,
            and interactive document tooling into one developer-friendly product family.
          </p>
          <div class="pxa-action-row">
            <a class="pxa-button pxa-button--primary" href="${siteLinks.demo}">View demos</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Read documentation</a>
          </div>
          <div class="pxa-company-proof-strip" aria-label="PXA proof points">
            ${renderProof(proofPoints)}
          </div>
        </div>
        <aside class="pxa-card pxa-company-snapshot" aria-label="PXA platform snapshot">
          <strong>Platform snapshot</strong>
          <dl>
            <div><dt>Products</dt><dd>6</dd></div>
            <div><dt>Migration focus</dt><dd>Code and designer</dd></div>
            <div><dt>Primary audience</dt><dd>.NET teams</dd></div>
            <div><dt>Current phase</dt><dd>Web platform</dd></div>
          </dl>
        </aside>
      </div>
    </section>
  `;
}

function renderProductsPage() {
  return `
    <section class="pxa-section" id="products">
      <div class="pxa-container">
        <p class="pxa-kicker">Products</p>
        <h1 class="pxa-heading">A focused suite for document-heavy teams</h1>
        <p class="pxa-lede">
          Choose the PXA product area that matches your workflow: generation, migration,
          import, design, PDF review, or spreadsheet-backed automation.
        </p>
        <div class="pxa-feature-grid pxa-company-grid">
          ${renderCards(products)}
        </div>
      </div>
    </section>
  `;
}

function renderProductDetailPage(product) {
  if (!product) return renderProductsPage();
  return `
    <section class="pxa-section" id="${product.slug}">
      <div class="pxa-container pxa-company-product-detail">
        <div>
          <p class="pxa-kicker">${product.label}</p>
          <h1 class="pxa-heading">${product.title}</h1>
          <p class="pxa-lede">${product.detail}</p>
          <div class="pxa-action-row">
            <a class="pxa-button pxa-button--primary" href="${siteLinks.demo}#demo/${product.demo}">Open demo</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}#${product.docs}">Read docs</a>
          </div>
        </div>
        <aside class="pxa-card pxa-company-product-summary">
          <strong>Product fit</strong>
          <p>${product.audience}</p>
          <ul>
            ${product.points.map((point) => `<li>${point}</li>`).join('')}
          </ul>
        </aside>
      </div>
    </section>
  `;
}

function renderUseCasesSection() {
  return `
    <section class="pxa-section pxa-section--soft" id="use-cases">
      <div class="pxa-container">
        <p class="pxa-kicker">Use cases</p>
        <h2 class="pxa-heading">From legacy provider migration to new document workflows</h2>
        <div class="pxa-company-use-grid">
          ${renderUseCases(useCases)}
        </div>
      </div>
    </section>
  `;
}

function renderTrustSection() {
  return `
    <section class="pxa-section" id="trust">
      <div class="pxa-container pxa-company-two-column">
        <div>
          <p class="pxa-kicker">Provider coverage</p>
          <h2 class="pxa-heading">Built around practical migration targets</h2>
          <p class="pxa-lede">
            PXA tracks real provider gaps and migration status through checklists,
            demos, and documentation.
          </p>
        </div>
        <div class="pxa-card">
          <strong>Tracked ecosystem</strong>
          <div class="pxa-company-badges">
            ${renderBadges(providers)}
          </div>
        </div>
      </div>
    </section>
  `;
}

function renderExamplesSection() {
  return `
    <section class="pxa-section pxa-section--soft" id="examples">
      <div class="pxa-container">
        <p class="pxa-kicker">Example reports</p>
        <h2 class="pxa-heading">Showcases that connect sales, docs, and demos</h2>
        <p class="pxa-lede">
          These examples make the product concrete: each one should point to a demo,
          documentation, and implementation status.
        </p>
        <div class="pxa-company-showcase-grid">
          ${renderShowcases(showcases)}
        </div>
      </div>
    </section>
  `;
}

function renderRoadmapSection() {
  return `
    <section class="pxa-section pxa-section--soft" id="roadmap">
      <div class="pxa-container">
        <p class="pxa-kicker">Roadmap</p>
        <h2 class="pxa-heading">One brand, three focused web experiences</h2>
        <div class="pxa-company-roadmap">
          ${renderRoadmap(roadmap)}
        </div>
      </div>
    </section>
  `;
}

function renderPricingPage() {
  return `
    <section class="pxa-section" id="pricing">
      <div class="pxa-container">
        <div>
          <p class="pxa-kicker">Pricing</p>
          <h1 class="pxa-heading">Trial, team, and enterprise paths</h1>
          <p class="pxa-lede">
            Final licensing is still pending, but the product site already separates
            evaluation, team adoption, and migration-heavy enterprise conversations.
          </p>
        </div>
        <div class="pxa-company-pricing-grid">
          ${renderPricing(pricingTiers)}
        </div>
      </div>
    </section>
  `;
}

function renderAboutPage() {
  return `
    <section class="pxa-section pxa-section--soft" id="about">
      <div class="pxa-container pxa-company-two-column">
        <div>
          <p class="pxa-kicker">About</p>
          <h1 class="pxa-heading">Power Dox Automation is a product system for modern document work.</h1>
          <p class="pxa-lede">
            PXA exists to make document automation less fragmented: one family for generating output,
            migrating legacy providers, importing files, reviewing PDFs, and connecting examples to docs.
          </p>
          <div class="pxa-action-row">
            <a class="pxa-button pxa-button--primary" href="${siteLinks.documentation}#overview">Read docs</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.demo}">Explore demos</a>
          </div>
        </div>
        <div class="pxa-company-about-stack">
          ${renderAboutPrinciples(aboutPrinciples)}
        </div>
      </div>
    </section>
  `;
}

function renderSupportPage() {
  return `
    <section class="pxa-section pxa-section--soft" id="support">
      <div class="pxa-container pxa-company-two-column">
        <div>
          <p class="pxa-kicker">Support</p>
          <h1 class="pxa-heading">Documentation, demos, and direct project support</h1>
          <p class="pxa-lede">
            Start with docs and demos, then bring migration-specific questions,
            provider parity gaps, or rollout planning to the PXA team.
          </p>
        </div>
        <div class="pxa-action-row">
          <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Open docs</a>
          <a class="pxa-button pxa-button--secondary" href="${siteLinks.demo}">Open demos</a>
        </div>
      </div>
    </section>
  `;
}

function renderContactPage() {
  return `
    <section class="pxa-section pxa-section--compact" id="contact">
      <div class="pxa-container pxa-card pxa-company-contact">
        <p class="pxa-kicker">Contact</p>
        <h1 class="pxa-heading">Ready to plan a PXA rollout?</h1>
        <p class="pxa-lede">This placeholder will become the sales/contact entry once the preferred contact flow is selected.</p>
      </div>
    </section>
  `;
}

function renderLegalPage(kind) {
  const pages = {
    terms: {
      kicker: 'Terms',
      title: 'Terms for evaluating Power Dox Automation',
      text:
        'These placeholder terms will be replaced by the final commercial and website terms before public launch.',
      points: ['Evaluation use', 'Sales contact path', 'Documentation and demo access'],
    },
    privacy: {
      kicker: 'Privacy',
      title: 'Privacy information for PXA web properties',
      text:
        'This placeholder privacy page documents the intended location for data handling, analytics, contact, and support information.',
      points: ['Website analytics', 'Contact requests', 'Demo and documentation usage'],
    },
    license: {
      kicker: 'License',
      title: 'License model placeholder',
      text:
        'This placeholder license page keeps licensing expectations visible while Trial, Team, and Enterprise terms are finalized.',
      points: ['Trial evaluation', 'Team adoption', 'Enterprise migration support'],
    },
  };
  const page = pages[kind] || pages.terms;
  return `
    <section class="pxa-section pxa-section--compact" id="${kind}">
      <div class="pxa-container pxa-card pxa-company-legal">
        <p class="pxa-kicker">${page.kicker}</p>
        <h1 class="pxa-heading">${page.title}</h1>
        <p class="pxa-lede">${page.text}</p>
        <ul>
          ${page.points.map((point) => `<li>${point}</li>`).join('')}
        </ul>
      </div>
    </section>
  `;
}

function renderHomePage() {
  return `
    ${renderHeroSection()}
    ${renderUseCasesSection()}
    ${renderTrustSection()}
    ${renderExamplesSection()}
    ${renderRoadmapSection()}
  `;
}

function renderMainContent() {
  if (currentRoute.section?.startsWith('product:')) {
    const slug = currentRoute.section.split(':')[1];
    return renderProductDetailPage(products.find((product) => product.slug === slug));
  }
  switch (currentRoute.section) {
    case 'products':
      return renderProductsPage();
    case 'pricing':
      return renderPricingPage();
    case 'about':
      return renderAboutPage();
    case 'support':
      return renderSupportPage();
    case 'contact':
      return renderContactPage();
    case 'terms':
      return renderLegalPage('terms');
    case 'privacy':
      return renderLegalPage('privacy');
    case 'license':
      return renderLegalPage('license');
    default:
      return renderHomePage();
  }
}

document.querySelector('#app').innerHTML = `
  <div class="pxa-site pxa-site--company">
    ${renderHeader()}
    <main class="pxa-site-main">
      ${renderMainContent()}
    </main>

    ${renderPxaFooter('PXA.Company')}
  </div>
`;
