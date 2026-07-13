import './site.css';

const siteLinks = {
  company: 'http://localhost:5173/',
  documentation: 'http://localhost:5174/',
  demo: 'http://localhost:5175/',
};

const categories = [
  'All',
  'PDF Generator',
  'PDF Viewer',
  'Designer',
  'Report Migration',
  'Code Migration',
  'Spreadsheet',
  'Import/Export',
];

const demos = [
  {
    title: 'Invoice / Booking Receipt',
    category: 'PDF Generator',
    status: 'Ready',
    text: 'Generate and preview a business document from structured data and reusable layout primitives.',
    tags: ['PDF', 'Invoice', 'Preview'],
  },
  {
    title: 'Master-detail report',
    category: 'Report Migration',
    status: 'Preview',
    text: 'Inspect a migrated master-detail report structure with grouped sections and child rows.',
    tags: ['Reports', 'Bands', 'Data'],
  },
  {
    title: 'Chart report',
    category: 'Designer',
    status: 'Preview',
    text: 'Review chart mapping expectations for report designers and generated document output.',
    tags: ['Charts', 'Designer', 'Reports'],
  },
  {
    title: 'Rich text / table report',
    category: 'Report Migration',
    status: 'Planned',
    text: 'Track rich text, table layout, and mixed-content report migration fidelity.',
    tags: ['Rich text', 'Tables', 'Migration'],
  },
  {
    title: 'PDF viewer annotations/forms',
    category: 'PDF Viewer',
    status: 'Preview',
    text: 'Explore annotation and form workflows for browser-based PDF review scenarios.',
    tags: ['Viewer', 'Forms', 'Annotations'],
  },
  {
    title: 'Spreadsheet import/export',
    category: 'Spreadsheet',
    status: 'Preview',
    text: 'Import workbook data, inspect mapped sheets, and prepare export-oriented workflows.',
    tags: ['Spreadsheet', 'Import', 'Export'],
  },
  {
    title: 'Provider migration examples',
    category: 'Code Migration',
    status: 'Preview',
    text: 'Compare migration outputs for known PDF and report providers across PXA migration tracks.',
    tags: ['Providers', 'Code', 'Reports'],
  },
  {
    title: 'File importer flow',
    category: 'Import/Export',
    status: 'Planned',
    text: 'Bring files into normalized PXA data and document workflows.',
    tags: ['Importer', 'Files', 'Output'],
  },
];

function statusClass(status) {
  if (status === 'Ready') return 'pxa-status--ready';
  if (status === 'Preview') return 'pxa-status--preview';
  return 'pxa-status--planned';
}

function renderCategoryButtons() {
  return categories
    .map(
      (category, index) => `
        <button class="pxa-demo-filter ${index === 0 ? 'is-active' : ''}" type="button" data-category="${category}">
          ${category}
        </button>
      `,
    )
    .join('');
}

function renderTags(tags) {
  return tags.map((tag) => `<span>${tag}</span>`).join('');
}

function renderDemoCards(items) {
  return items
    .map(
      (demo) => `
        <article class="pxa-demo-card" data-category="${demo.category}" data-search="${`${demo.title} ${demo.category} ${demo.tags.join(' ')}`.toLowerCase()}">
          <div class="pxa-demo-card__meta">
            <span class="pxa-status ${statusClass(demo.status)}">${demo.status}</span>
            <span>${demo.category}</span>
          </div>
          <h3>${demo.title}</h3>
          <p>${demo.text}</p>
          <div class="pxa-demo-tags">${renderTags(demo.tags)}</div>
          <div class="pxa-demo-actions">
            <a class="pxa-button pxa-button--primary" href="#demo-detail">Open demo</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Docs</a>
            <a class="pxa-demo-source" href="#source-context">View source</a>
          </div>
        </article>
      `,
    )
    .join('');
}

document.querySelector('#app').innerHTML = `
  <div class="pxa-site pxa-site--demo">
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="/" aria-label="PXA.Demo home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Demo</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}">Company</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}" aria-current="page">Demo</a>
          <a href="${siteLinks.company}#pricing">Pricing</a>
          <a href="${siteLinks.company}#support">Support</a>
        </nav>
      </div>
    </header>

    <main class="pxa-site-main" id="top">
      <div class="pxa-page-header">
        <div class="pxa-container pxa-demo-header">
          <div>
            <p class="pxa-kicker">PXA Demo Gallery</p>
            <h1 class="pxa-heading">Explore document automation workflows in action.</h1>
            <p class="pxa-lede">
              Browse examples for generator, viewer, designer, migration, spreadsheet,
              and import/export workflows. Each demo links back to documentation and source context.
            </p>
          </div>
          <div class="pxa-card pxa-demo-summary">
            <strong>Demo status</strong>
            <span><b>1</b> ready</span>
            <span><b>5</b> preview</span>
            <span><b>2</b> planned</span>
          </div>
        </div>
      </div>

      <section class="pxa-section pxa-section--compact">
        <div class="pxa-container">
          <div class="pxa-demo-toolbar" aria-label="Demo filters">
            <input class="pxa-search" type="search" placeholder="Search demos" aria-label="Search demos" data-demo-search>
            <div class="pxa-demo-filters">
              ${renderCategoryButtons()}
            </div>
          </div>

          <div class="pxa-demo-grid pxa-demo-gallery" data-demo-gallery>
            ${renderDemoCards(demos)}
          </div>
        </div>
      </section>

      <section class="pxa-section pxa-section--soft" id="demo-detail">
        <div class="pxa-container pxa-demo-detail">
          <div>
            <p class="pxa-kicker">Demo detail pattern</p>
            <h2 class="pxa-heading">Live preview, input data, result, JSON/code, and download</h2>
            <p class="pxa-lede">
              Detail pages will reuse this pattern when demos become fully interactive.
              Planned demos should keep showing status and next steps instead of empty pages.
            </p>
          </div>
          <pre class="pxa-code"><code>{
  "demo": "Invoice / Booking Receipt",
  "status": "Ready",
  "links": ["Docs", "Source", "Download"]
}</code></pre>
        </div>
      </section>

      <section class="pxa-section" id="source-context">
        <div class="pxa-container pxa-demo-source-panel">
          <div>
            <p class="pxa-kicker">Source context</p>
            <h2 class="pxa-heading">Each demo should point to source, docs, and checklist status.</h2>
            <p class="pxa-lede">
              Static cards link here until real source files and runnable detail routes are connected.
            </p>
          </div>
          <div class="pxa-card">
            <a href="${siteLinks.documentation}">Open related documentation</a>
            <a href="#demo-detail">Review demo detail pattern</a>
            <a href="#top">Back to gallery</a>
          </div>
        </div>
      </section>
    </main>

    <footer class="pxa-site-footer">
      <div class="pxa-container pxa-demo-footer">
        <span>Power Dox Automation Demo Gallery</span>
        <span><a href="${siteLinks.company}">Company</a> · <a href="${siteLinks.documentation}">Documentation</a></span>
      </div>
    </footer>
  </div>
`;

const gallery = document.querySelector('[data-demo-gallery]');
const search = document.querySelector('[data-demo-search]');
const filters = Array.from(document.querySelectorAll('[data-category]'));

function applyFilters() {
  const activeCategory = document.querySelector('.pxa-demo-filter.is-active')?.dataset.category || 'All';
  const query = search.value.trim().toLowerCase();

  Array.from(gallery.children).forEach((card) => {
    const matchesCategory = activeCategory === 'All' || card.dataset.category === activeCategory;
    const matchesSearch = !query || card.dataset.search.includes(query);
    card.hidden = !(matchesCategory && matchesSearch);
  });
}

filters.forEach((button) => {
  button.addEventListener('click', () => {
    filters.forEach((item) => item.classList.remove('is-active'));
    button.classList.add('is-active');
    applyFilters();
  });
});

search.addEventListener('input', applyFilters);
