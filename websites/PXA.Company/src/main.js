import './site.css';

const siteLinks = {
  company: 'http://localhost:5173/',
  documentation: 'http://localhost:5174/',
  demo: 'http://localhost:5175/',
};

const products = [
  {
    title: 'PXA Generator',
    label: 'Generate',
    text: 'Create PDFs and document output from code, templates, structured data, and reusable layouts.',
    points: ['PDF generation', 'Template-driven output', 'Reusable document primitives'],
  },
  {
    title: 'PXA Migration',
    label: 'Migrate',
    text: 'Move existing PDF code, report designs, and spreadsheet workflows from known providers into PXA.',
    points: ['Provider mapping', 'Code conversion', 'Designer migration'],
  },
  {
    title: 'PXA Importer',
    label: 'Import',
    text: 'Bring PDF, Office, image, and document inputs into normalized PXA flows.',
    points: ['File normalization', 'Office/PDF inputs', 'Import diagnostics'],
  },
  {
    title: 'PXA Designer',
    label: 'Design',
    text: 'Design document templates, preview output, and inspect generated JSON and code.',
    points: ['Visual editing', 'Live preview', 'JSON/code inspection'],
  },
  {
    title: 'PXA PDF Viewer',
    label: 'Review',
    text: 'Review, annotate, fill forms, and inspect PDF workflows in the browser.',
    points: ['Annotations', 'Forms', 'Review workflows'],
  },
  {
    title: 'PXA Spreadsheet',
    label: 'Model',
    text: 'Import, edit, map, and export workbook-driven document automation flows.',
    points: ['Workbook import', 'Formula-ready data', 'Export flows'],
  },
];

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
  },
  {
    title: 'Report migration gallery',
    text: 'Provider-focused examples for DevExpress, Syncfusion RDL, ActiveReports, JasperReports, and more.',
    status: 'Preview',
  },
  {
    title: 'PDF viewer workflows',
    text: 'Annotation, form, and review scenarios that connect product demos with implementation docs.',
    status: 'Preview',
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
    href: '#contact',
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
          <a href="#contact" aria-label="Learn more about ${item.title}">Learn more</a>
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
          <a href="${siteLinks.demo}">Open demo gallery</a>
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

document.querySelector('#app').innerHTML = `
  <div class="pxa-site pxa-site--company">
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="/" aria-label="PXA.Company home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Company</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}" aria-current="page">Company</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="#pricing">Pricing</a>
          <a href="#support">Support</a>
        </nav>
        <a class="pxa-button pxa-button--primary pxa-header-cta" href="#contact">Contact sales</a>
      </div>
    </header>

    <main class="pxa-site-main">
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

      <section class="pxa-section" id="products">
        <div class="pxa-container">
          <p class="pxa-kicker">Products</p>
          <h2 class="pxa-heading">A focused suite for document-heavy teams</h2>
          <div class="pxa-feature-grid pxa-company-grid">
            ${renderCards(products)}
          </div>
        </div>
      </section>

      <section class="pxa-section pxa-section--soft" id="use-cases">
        <div class="pxa-container">
          <p class="pxa-kicker">Use cases</p>
          <h2 class="pxa-heading">From legacy provider migration to new document workflows</h2>
          <div class="pxa-company-use-grid">
            ${renderUseCases(useCases)}
          </div>
        </div>
      </section>

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

      <section class="pxa-section pxa-section--soft" id="roadmap">
        <div class="pxa-container">
          <p class="pxa-kicker">Roadmap</p>
          <h2 class="pxa-heading">One brand, three focused web experiences</h2>
          <div class="pxa-company-roadmap">
            ${renderRoadmap(roadmap)}
          </div>
        </div>
      </section>

      <section class="pxa-section" id="pricing">
        <div class="pxa-container">
          <div>
            <p class="pxa-kicker">Pricing</p>
            <h2 class="pxa-heading">Trial, team, and enterprise paths</h2>
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

      <section class="pxa-section pxa-section--soft" id="support">
        <div class="pxa-container pxa-company-two-column">
          <div>
            <p class="pxa-kicker">Support</p>
            <h2 class="pxa-heading">Documentation, demos, and direct project support</h2>
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

      <section class="pxa-section pxa-section--compact" id="contact">
        <div class="pxa-container pxa-card pxa-company-contact">
          <p class="pxa-kicker">Contact</p>
          <h2 class="pxa-heading">Ready to plan a PXA rollout?</h2>
          <p class="pxa-lede">This placeholder will become the sales/contact entry once the preferred contact flow is selected.</p>
        </div>
      </section>
    </main>

    <footer class="pxa-site-footer">
      <div class="pxa-container pxa-company-footer">
        <div>
          <strong>Power Dox Automation</strong>
          <p>PXA.Company connects product, documentation, and demos.</p>
        </div>
        <nav aria-label="Footer navigation">
          <a href="${siteLinks.company}">Company</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="#pricing">Pricing</a>
          <a href="#support">Support</a>
        </nav>
      </div>
    </footer>
  </div>
`;
