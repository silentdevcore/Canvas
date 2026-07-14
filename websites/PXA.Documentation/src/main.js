import './site.css';
import { siteLinks } from '../../shared/siteLinks.js';

const editorSections = [
  'Designer',
  'Templates',
  'Elements',
  'PDF Viewer',
  'Spreadsheet',
  'Importer',
  'Export',
];

const codeSections = [
  'PXA.Generator',
  'PXA.Migration',
  'PXA.Importer',
  'PXA.Infrastructure',
  'PXA.WebApi',
  'API Reference',
];

const migrationGuides = [
  'PDF Code Migration',
  'Report Designer Migration',
  'Spreadsheet Code Migration',
  'Provider Taxonomy',
];

const cookbook = ['PDF generation', 'Edit PDF', 'Forms', 'Annotations', 'Reports', 'Import/export'];

const demoExamples = [
  {
    title: 'Invoice / Booking Receipt',
    route: 'booking-receipt',
    docs: 'PDF generation',
    source: '/examples/booking-receipt/source.js',
    input: '/examples/booking-receipt/input.json',
    output: '/examples/booking-receipt/output.json',
  },
  {
    title: 'Master-detail report',
    route: 'master-detail-report',
    docs: 'Reports',
    source: '/examples/master-detail-report/source.js',
    input: '/examples/master-detail-report/input.json',
    output: '/examples/master-detail-report/output.json',
  },
  {
    title: 'Chart report',
    route: 'chart-report',
    docs: 'Elements',
    source: '/examples/chart-report/source.js',
    input: '/examples/chart-report/input.json',
    output: '/examples/chart-report/output.json',
  },
  {
    title: 'PDF viewer annotations/forms',
    route: 'pdf-viewer-annotations-forms',
    docs: 'PDF Viewer',
    source: '/examples/pdf-viewer-annotations-forms/source.js',
    input: '/examples/pdf-viewer-annotations-forms/input.json',
    output: '/examples/pdf-viewer-annotations-forms/output.json',
  },
  {
    title: 'Spreadsheet import/export',
    route: 'spreadsheet-import-export',
    docs: 'Spreadsheet',
    source: '/examples/spreadsheet-import-export/source.js',
    input: '/examples/spreadsheet-import-export/input.json',
    output: '/examples/spreadsheet-import-export/output.json',
  },
  {
    title: 'Provider migration examples',
    route: 'provider-migration-examples',
    docs: 'PXA.Migration',
    source: '/examples/provider-migration-examples/source.js',
    input: '/examples/provider-migration-examples/input.json',
    output: '/examples/provider-migration-examples/output.json',
  },
];

const quickstarts = [
  {
    title: 'Editor Quickstart',
    label: 'Editor',
    steps: ['Open PXA Designer', 'Choose or import a template', 'Preview output and inspect JSON'],
    command: 'npm run dev -- --host localhost',
  },
  {
    title: 'Code Quickstart',
    label: 'SDK',
    steps: ['Reference the generator package', 'Create a document model', 'Build and export output'],
    command: 'dotnet build',
  },
];

const referenceLinks = [
  {
    title: 'DocFX API Reference',
    text: 'Generated .NET API reference for PXA packages.',
    href: '../../docs/api/',
  },
  {
    title: 'OpenAPI Schema',
    text: 'WebApi contract for migration, import, and export endpoints.',
    href: '../../docs/schema/openapi.json',
  },
  {
    title: 'C# Cookbook',
    text: 'Task-oriented examples for generator and integration workflows.',
    href: '../../docs/csharp-cookbook.md',
  },
];

const checklistLinks = [
  'PXA.Web-Design-System',
  'PXA.Company',
  'PXA.Documentation',
  'PXA.Demo',
  'Migration-Namespace-Taxonomy',
  'PxaPdf-Provider-Feature-Gaps',
];

function renderNavList(items) {
  return items.map((item) => `<a href="#${slug(item)}">${item}</a>`).join('');
}

function renderCards(items, status = 'Planned') {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-card" id="${slug(item)}">
          <span class="pxa-status ${status === 'Ready' ? 'pxa-status--ready' : 'pxa-status--planned'}">${status}</span>
          <h3>${item}</h3>
          <p>${descriptionFor(item)}</p>
        </article>
      `,
    )
    .join('');
}

function renderQuickstarts(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-quickstart-card">
          <span class="pxa-status pxa-status--ready">${item.label}</span>
          <h3>${item.title}</h3>
          <ol>
            ${item.steps.map((step) => `<li>${step}</li>`).join('')}
          </ol>
          <pre class="pxa-code"><code>${item.command}</code></pre>
        </article>
      `,
    )
    .join('');
}

function renderReferenceLinks(items) {
  return items
    .map(
      (item) => `
        <a class="pxa-card pxa-doc-reference-card" href="${item.href}">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
        </a>
      `,
    )
    .join('');
}

function renderChecklistLinks(items) {
  return items
    .map((item) => `<span class="pxa-status pxa-status--preview">${item}</span>`)
    .join('');
}

function renderDemoExamples(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-demo-card">
          <span class="pxa-status pxa-status--ready">${item.docs}</span>
          <h3>${item.title}</h3>
          <div class="pxa-doc-demo-links">
            <a href="${siteLinks.demo}#demo/${item.route}">Open demo</a>
            <a href="${siteLinks.demo}${item.input.slice(1)}">Input</a>
            <a href="${siteLinks.demo}${item.output.slice(1)}">Output</a>
            <a href="${siteLinks.demo}${item.source.slice(1)}">Source</a>
          </div>
        </article>
      `,
    )
    .join('');
}

function slug(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
}

function descriptionFor(item) {
  const descriptions = {
    Designer: 'Build and inspect document templates with visual workflows.',
    Templates: 'Understand template structure, variables, repeats, and validation.',
    Elements: 'Use text, tables, charts, images, forms, shapes, and layout primitives.',
    'PDF Viewer': 'Review PDFs, forms, annotations, and viewer-side workflows.',
    Spreadsheet: 'Import, map, edit, and export workbook-driven data flows.',
    Importer: 'Normalize incoming PDF, Office, image, and document files.',
    Export: 'Generate final outputs and code-oriented artifacts.',
    'PXA.Generator': 'Generate PDFs and document output from .NET code.',
    'PXA.Migration': 'Convert provider-specific code and designer formats into PXA.',
    'PXA.Importer': 'Integrate file importers into automation flows.',
    'PXA.Infrastructure': 'Understand rendering, conversion, and persistence boundaries.',
    'PXA.WebApi': 'Use HTTP endpoints for migration, import, and export flows.',
    'API Reference': 'Open generated API docs and OpenAPI reference material.',
  };

  return descriptions[item] || 'Documentation entry planned for this product area.';
}

document.querySelector('#app').innerHTML = `
  <div class="pxa-site pxa-site--documentation">
    <header class="pxa-site-header">
      <div class="pxa-site-header__inner">
        <a class="pxa-brand" href="/" aria-label="PXA.Documentation home">
          <span class="pxa-brand__mark">PXA</span>
          <span class="pxa-brand__name">Power Dox Automation <small>Documentation</small></span>
        </a>
        <nav class="pxa-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}">Company</a>
          <a href="${siteLinks.company}#products">Products</a>
          <a href="${siteLinks.documentation}" aria-current="page">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="${siteLinks.company}#pricing">Pricing</a>
          <a href="${siteLinks.company}#support">Support</a>
        </nav>
        <a class="pxa-button pxa-button--primary pxa-header-cta" href="${siteLinks.company}#contact">Contact sales</a>
      </div>
    </header>

    <main class="pxa-site-main">
      <div class="pxa-page-header">
        <div class="pxa-docs-container">
          <p class="pxa-kicker">PXA Documentation</p>
          <h1 class="pxa-heading">Editor workflows and SDK integration in one place.</h1>
          <p class="pxa-lede">
            Start with product guides for the editor experience, then move into code,
            migration, cookbook examples, and generated API references.
          </p>
        </div>
      </div>

      <div class="pxa-docs-layout">
        <aside class="pxa-docs-sidebar">
          <input class="pxa-search" type="search" placeholder="Search documentation" aria-label="Search documentation">
          <nav class="pxa-card pxa-doc-nav" aria-label="Documentation sections">
            <strong>Editor</strong>
            ${renderNavList(editorSections)}
            <strong>Code SDK</strong>
            ${renderNavList(codeSections)}
            <strong>Migration</strong>
            ${renderNavList(migrationGuides)}
          </nav>
        </aside>

        <article class="pxa-docs-content">
          <section class="pxa-doc-hero-grid" aria-label="Documentation entry points">
            <a class="pxa-card pxa-doc-entry" href="#editor-path">
              <span class="pxa-status pxa-status--ready">Editor path</span>
              <h2>Editor benutzen</h2>
              <p>Learn how templates, elements, viewer workflows, spreadsheets, importers, and export paths fit together.</p>
            </a>
            <a class="pxa-card pxa-doc-entry" href="#code-path">
              <span class="pxa-status pxa-status--ready">Code path</span>
              <h2>Code integrieren</h2>
              <p>Use PXA Generator, Migration, Importer, Infrastructure, WebApi, and generated references from .NET apps.</p>
            </a>
          </section>

          <section class="pxa-doc-section" id="overview">
            <p class="pxa-kicker">Overview</p>
            <h2 class="pxa-heading">Four documentation tracks</h2>
            <div class="pxa-feature-grid">
              <article class="pxa-card"><h3>PXA Overview</h3><p>Product map, concepts, and how the PXA family fits together.</p></article>
              <article class="pxa-card"><h3>Quickstarts</h3><p>Fast paths for editor users and SDK developers.</p></article>
              <article class="pxa-card"><h3>Installation</h3><p>Project setup, service startup, package usage, and local workflows.</p></article>
              <article class="pxa-card"><h3>Concepts</h3><p>Design JSON, pages, elements, migration results, and provider taxonomy.</p></article>
            </div>
          </section>

          <section class="pxa-doc-section" id="quickstarts">
            <p class="pxa-kicker">Quickstarts</p>
            <h2 class="pxa-heading">Start with the editor or start with code</h2>
            <div class="pxa-doc-quickstart-grid">
              ${renderQuickstarts(quickstarts)}
            </div>
          </section>

          <section class="pxa-doc-section" id="editor-path">
            <p class="pxa-kicker">Editor documentation</p>
            <h2 class="pxa-heading">Product guides for visual document workflows</h2>
            <div class="pxa-doc-card-grid">
              ${renderCards(editorSections, 'Planned')}
            </div>
          </section>

          <section class="pxa-doc-section" id="code-path">
            <p class="pxa-kicker">Code documentation</p>
            <h2 class="pxa-heading">SDK and WebApi entry points</h2>
            <div class="pxa-doc-card-grid">
              ${renderCards(codeSections, 'Planned')}
            </div>
          </section>

          <section class="pxa-doc-section" id="migration">
            <p class="pxa-kicker">Migration guides</p>
            <h2 class="pxa-heading">Provider-oriented migration documentation</h2>
            <div class="pxa-doc-card-grid">
              ${renderCards(migrationGuides, 'Planned')}
            </div>
          </section>

          <section class="pxa-doc-section" id="cookbook">
            <p class="pxa-kicker">Cookbook</p>
            <h2 class="pxa-heading">Task-based examples</h2>
            <div class="pxa-company-badges">
              ${cookbook.map((item) => `<span class="pxa-status pxa-status--preview">${item}</span>`).join('')}
            </div>
          </section>

          <section class="pxa-doc-section" id="demo-examples">
            <p class="pxa-kicker">Demo examples</p>
            <h2 class="pxa-heading">Runnable examples connect docs, input, output, and source</h2>
            <p>
              PXA.Demo hosts lightweight example files for every demo card. These links make
              documentation topics directly traceable to the examples used in the demo gallery.
            </p>
            <div class="pxa-doc-demo-grid">
              ${renderDemoExamples(demoExamples)}
            </div>
          </section>

          <section class="pxa-doc-section" id="api-reference">
            <p class="pxa-kicker">API reference</p>
            <h2 class="pxa-heading">Generated references stay connected</h2>
            <p>
              The existing DocFX and OpenAPI outputs remain the source for generated reference material.
              This website provides product-first entry points that link into those generated docs.
            </p>
            <div class="pxa-doc-reference-grid">
              ${renderReferenceLinks(referenceLinks)}
            </div>
            <pre class="pxa-code"><code>docfx build docs/docfx.json</code></pre>
          </section>

          <section class="pxa-doc-section" id="history">
            <p class="pxa-kicker">History and planning</p>
            <h2 class="pxa-heading">Checklists stay as implementation history</h2>
            <p>
              Product documentation should describe current behavior. Checklists remain useful for
              roadmap decisions, migration status, implementation notes, and historical context.
            </p>
            <div class="pxa-company-badges">
              ${renderChecklistLinks(checklistLinks)}
            </div>
          </section>
        </article>

        <aside class="pxa-docs-toc">
          <div class="pxa-card pxa-doc-toc">
            <strong>On this page</strong>
            <a href="#overview">Overview</a>
            <a href="#quickstarts">Quickstarts</a>
            <a href="#editor-path">Editor path</a>
            <a href="#code-path">Code path</a>
            <a href="#migration">Migration</a>
            <a href="#cookbook">Cookbook</a>
            <a href="#demo-examples">Demo examples</a>
            <a href="#api-reference">API reference</a>
            <a href="#history">History</a>
          </div>
        </aside>
      </div>
    </main>

    <footer class="pxa-site-footer">
      <div class="pxa-docs-container pxa-doc-footer">
        <span>Power Dox Automation Documentation</span>
        <span><a href="${siteLinks.company}">Company</a> · <a href="${siteLinks.demo}">Demo</a></span>
      </div>
    </footer>
  </div>
`;
