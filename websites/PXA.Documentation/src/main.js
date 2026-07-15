import './site.css';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';

const editorSections = [
  {
    title: 'Designer',
    status: 'Ready',
    text: 'Create and inspect PXA document templates in the visual designer, then preview output and review JSON.',
  },
  {
    title: 'Templates',
    status: 'Preview',
    text: 'Understand pages, shared elements, bindings, repeats, and template structure before connecting data.',
  },
  {
    title: 'Elements',
    status: 'Preview',
    text: 'Use text, tables, charts, images, forms, shapes, and layout primitives consistently across templates.',
  },
  {
    title: 'PDF Viewer',
    status: 'Preview',
    text: 'Review PDFs, forms, annotations, and browser-side inspection workflows connected to demos.',
  },
  {
    title: 'Spreadsheet',
    status: 'Preview',
    text: 'Import, map, edit, and export workbook-driven data flows for document automation.',
  },
  {
    title: 'Importer',
    status: 'Preview',
    text: 'Normalize incoming PDF, Office, image, and document files before migration or generation.',
  },
  {
    title: 'Export',
    status: 'Preview',
    text: 'Generate final outputs, downloadable artifacts, JSON, and code-oriented handoff files.',
  },
];

const codeSections = [
  {
    title: 'PXA.Generator',
    status: 'Ready',
    text: 'Generate PDFs and business documents from .NET code, structured data, and reusable layout primitives.',
  },
  {
    title: 'PXA.Migration',
    status: 'Preview',
    text: 'Convert provider-specific code and designer formats into PXA targets with diagnostics and follow-up notes.',
  },
  {
    title: 'PXA.Importer',
    status: 'Preview',
    text: 'Integrate file importers into document automation, migration, and designer workflows.',
  },
  {
    title: 'PXA.Infrastructure',
    status: 'Preview',
    text: 'Understand rendering, conversion, persistence, and integration boundaries across PXA services.',
  },
  {
    title: 'PXA.WebApi',
    status: 'Ready',
    text: 'Use HTTP endpoints for migration, import, export, rendering, and designer handoff flows.',
  },
  {
    title: 'API Reference',
    status: 'Planned',
    text: 'Connect generated DocFX and OpenAPI references to product-level integration guides.',
  },
];

const migrationGuides = [
  {
    title: 'PDF Code Migration',
    status: 'Preview',
    text: 'Map third-party PDF SDK calls into PXA code patterns and track missing API parity explicitly.',
  },
  {
    title: 'Report Designer Migration',
    status: 'Ready',
    text: 'Convert DevExpress, RDL/RDLC, ActiveReports, FastReport, Telerik, JasperReports, and Stimulsoft reports into editable PXA designs.',
  },
  {
    title: 'Spreadsheet Code Migration',
    status: 'Preview',
    text: 'Plan spreadsheet provider migrations and workbook-driven automation paths.',
  },
  {
    title: 'Provider Taxonomy',
    status: 'Ready',
    text: 'Use the PXA migration namespace taxonomy to distinguish domain, migration kind, and provider.',
  },
];

const cookbook = [
  {
    title: 'PDF generation',
    status: 'Ready',
    text: 'Create business documents from structured data and reusable layout primitives.',
    tasks: ['Choose a template or code model', 'Bind data', 'Render or export output'],
    href: `${siteLinks.demo}#demo/booking-receipt`,
  },
  {
    title: 'Edit PDF',
    status: 'Planned',
    text: 'Track planned editing workflows for existing PDFs and imported document surfaces.',
    tasks: ['Import source file', 'Inspect mapped objects', 'Export edited output'],
    href: companyPage('products/pdf-viewer'),
  },
  {
    title: 'Forms',
    status: 'Preview',
    text: 'Plan form review and field workflows through the PDF Viewer and generated outputs.',
    tasks: ['Open viewer workflow', 'Inspect form fields', 'Connect to review scenarios'],
    href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms`,
  },
  {
    title: 'Annotations',
    status: 'Preview',
    text: 'Review annotation workflows and viewer feature gaps before implementation.',
    tasks: ['Open viewer demo', 'Review planned tools', 'Track parity gaps'],
    href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms`,
  },
  {
    title: 'Reports',
    status: 'Ready',
    text: 'Work with migrated report layouts, report sections, grouped data, charts, and designer handoff.',
    tasks: ['Choose report provider', 'Run designer migration', 'Open output in PXA Designer'],
    href: `${siteLinks.demo}#demo/master-detail-report`,
  },
  {
    title: 'Import/export',
    status: 'Preview',
    text: 'Normalize incoming files and export generated JSON, PDF, and demo artifacts.',
    tasks: ['Choose input format', 'Normalize or migrate', 'Download output artifacts'],
    href: `${siteLinks.demo}#demo/file-importer-flow`,
  },
];

const trackGuides = {
  editor: {
    readFirst: ['Designer', 'Templates', 'Elements'],
    tasks: ['Open a template in PXA Designer', 'Inspect page size, margins, and shared elements', 'Preview output and export JSON'],
    related: [
      { label: 'Open live designer', href: siteLinks.designer },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Master-detail demo', href: `${siteLinks.demo}#demo/master-detail-report` },
    ],
  },
  code: {
    readFirst: ['PXA.Generator', 'PXA.WebApi', 'API Reference'],
    tasks: ['Start the backend API', 'Render or export a document model', 'Use diagnostics for failed imports or migrations'],
    related: [
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'Booking receipt demo', href: `${siteLinks.demo}#demo/booking-receipt` },
      { label: 'OpenAPI schema', href: '../../docs/schema/openapi.json' },
    ],
  },
  migration: {
    readFirst: ['Report Designer Migration', 'PDF Code Migration', 'Provider Taxonomy'],
    tasks: ['Choose code or designer migration', 'Review converted output and diagnostics', 'Open report output in PXA Designer when available'],
    related: [
      { label: 'Migration product page', href: companyPage('products/migration') },
      { label: 'Provider migration demo', href: `${siteLinks.demo}#demo/provider-migration-examples` },
      { label: 'Designer migration route', href: `${siteLinks.designer}migrations/pdf/designer` },
    ],
  },
};

const docEntryPoints = [
  {
    title: 'Use the Editor',
    label: 'Editor path',
    href: '#editor-path',
    text: 'Design templates, inspect elements, preview output, and move between visual workflows and generated JSON.',
  },
  {
    title: 'Integrate with Code',
    label: 'Code path',
    href: '#code-path',
    text: 'Start from .NET integration points: Generator, Importer, Migration, Infrastructure, WebApi, and references.',
  },
  {
    title: 'Migrate to PXA',
    label: 'Migration path',
    href: '#migration',
    text: 'Convert provider-specific PDF code, report designer files, and spreadsheet workflows with diagnostics.',
  },
  {
    title: 'Explore APIs',
    label: 'Reference path',
    href: '#api-reference',
    text: 'Find generated API reference, OpenAPI artifacts, cookbook links, and endpoint-oriented guidance.',
  },
];

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
    command: 'cd pxa-designer && npm run dev',
  },
  {
    title: 'Code Quickstart',
    label: 'SDK',
    steps: ['Start the backend API', 'Create or load a document model', 'Render, migrate, import, or export through PXA endpoints'],
    command: 'dotnet build',
  },
  {
    title: 'Migration Quickstart',
    label: 'Migration',
    steps: ['Choose code or designer migration', 'Upload or paste provider input', 'Review diagnostics before opening in designer'],
    command: 'open /migrations/pdf/designer',
  },
];

const referenceLinks = [
  {
    title: 'DocFX API Reference',
    status: 'Planned',
    text: 'Generated .NET API reference for PXA packages.',
    href: '../../docs/api/',
  },
  {
    title: 'OpenAPI Schema',
    status: 'Ready',
    text: 'WebApi contract for migration, import, and export endpoints.',
    href: '../../docs/schema/openapi.json',
  },
  {
    title: 'C# Cookbook',
    status: 'Preview',
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

function itemTitle(item) {
  return typeof item === 'string' ? item : item.title;
}

function renderNavList(items) {
  return items.map((item) => `<a href="#${slug(itemTitle(item))}">${itemTitle(item)}</a>`).join('');
}

function statusClass(status) {
  if (status === 'Ready') return 'pxa-status--ready';
  if (status === 'Preview') return 'pxa-status--preview';
  return 'pxa-status--planned';
}

function renderCards(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-card" id="${slug(itemTitle(item))}">
          <span class="pxa-status ${statusClass(item.status ?? 'Planned')}">${item.status ?? 'Planned'}</span>
          <h3>${itemTitle(item)}</h3>
          <p>${item.text ?? descriptionFor(itemTitle(item))}</p>
        </article>
      `,
    )
    .join('');
}

function renderEntryPoints(items) {
  return items
    .map(
      (item) => `
        <a class="pxa-card pxa-doc-entry" href="${item.href}">
          <span class="pxa-status pxa-status--ready">${item.label}</span>
          <h2>${item.title}</h2>
          <p>${item.text}</p>
        </a>
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
          <span class="pxa-status ${statusClass(item.status ?? 'Planned')}">${item.status ?? 'Planned'}</span>
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

function renderCookbook(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-doc-cookbook-card">
          <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <ul>
            ${item.tasks.map((task) => `<li>${task}</li>`).join('')}
          </ul>
          <a href="${item.href}">Open related resource</a>
        </article>
      `,
    )
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

function renderTrackGuide(guide) {
  return `
    <div class="pxa-doc-track-guide">
      <article class="pxa-card">
        <h3>Read first</h3>
        <ol>
          ${guide.readFirst.map((item) => `<li><a href="#${slug(item)}">${item}</a></li>`).join('')}
        </ol>
      </article>
      <article class="pxa-card">
        <h3>Common tasks</h3>
        <ul>
          ${guide.tasks.map((item) => `<li>${item}</li>`).join('')}
        </ul>
      </article>
      <article class="pxa-card">
        <h3>Related links</h3>
        <div class="pxa-doc-related-links">
          ${guide.related.map((item) => `<a href="${item.href}">${item.label}</a>`).join('')}
        </div>
      </article>
    </div>
  `;
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
          <a href="${companyPage('products')}">Products</a>
          <a href="${siteLinks.documentation}" aria-current="page">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="${companyPage('pricing')}">Pricing</a>
          <a href="${companyPage('about')}">About</a>
          <a href="${companyPage('support')}">Support</a>
        </nav>
        <div class="pxa-header-actions">
          <a class="pxa-button pxa-button--secondary pxa-header-cta" href="${siteLinks.designer}">Live demo</a>
          <a class="pxa-button pxa-button--primary pxa-header-cta" href="${companyPage('contact')}">Contact sales</a>
        </div>
      </div>
    </header>

    <main class="pxa-site-main">
      <div class="pxa-page-header">
        <div class="pxa-docs-container">
          <p class="pxa-kicker">PXA Documentation</p>
          <h1 class="pxa-heading">Build with PXA from the editor, from code, or from a migration path.</h1>
          <p class="pxa-lede">
            Use this documentation as the technical map for Power Dox Automation:
            visual authoring, .NET integration, provider migration, demos, and generated references.
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
            ${renderEntryPoints(docEntryPoints)}
          </section>

          <section class="pxa-doc-section" id="overview">
            <p class="pxa-kicker">Overview</p>
            <h2 class="pxa-heading">Four ways into the platform</h2>
            <div class="pxa-feature-grid">
              <article class="pxa-card"><h3>Product map</h3><p>Understand how Generator, Migration, Importer, Designer, PDF Viewer, and Spreadsheet connect.</p></article>
              <article class="pxa-card"><h3>Local setup</h3><p>Run the backend, designer, documentation site, and demo gallery on separate local ports.</p></article>
              <article class="pxa-card"><h3>Core concepts</h3><p>Learn design JSON, pages, elements, bindings, migration diagnostics, and provider taxonomy.</p></article>
              <article class="pxa-card"><h3>Examples first</h3><p>Use demo input, output, and source links to validate workflows before deeper integration.</p></article>
            </div>
          </section>

          <section class="pxa-doc-section" id="quickstarts">
            <p class="pxa-kicker">Quickstarts</p>
            <h2 class="pxa-heading">Start with the path closest to your task</h2>
            <div class="pxa-doc-quickstart-grid">
              ${renderQuickstarts(quickstarts)}
            </div>
          </section>

          <section class="pxa-doc-section" id="editor-path">
            <p class="pxa-kicker">Editor documentation</p>
            <h2 class="pxa-heading">Product guides for visual document workflows</h2>
            ${renderTrackGuide(trackGuides.editor)}
            <div class="pxa-doc-card-grid">
              ${renderCards(editorSections)}
            </div>
          </section>

          <section class="pxa-doc-section" id="code-path">
            <p class="pxa-kicker">Code documentation</p>
            <h2 class="pxa-heading">SDK and WebApi entry points</h2>
            ${renderTrackGuide(trackGuides.code)}
            <div class="pxa-doc-card-grid">
              ${renderCards(codeSections)}
            </div>
          </section>

          <section class="pxa-doc-section" id="migration">
            <p class="pxa-kicker">Migration guides</p>
            <h2 class="pxa-heading">Provider-oriented migration documentation</h2>
            ${renderTrackGuide(trackGuides.migration)}
            <div class="pxa-doc-card-grid">
              ${renderCards(migrationGuides)}
            </div>
          </section>

          <section class="pxa-doc-section" id="cookbook">
            <p class="pxa-kicker">Cookbook</p>
            <h2 class="pxa-heading">Task-based examples</h2>
            <p>
              Cookbook entries explain common implementation tasks and point to the closest demo,
              product page, or reference material while the full article set is being expanded.
            </p>
            <div class="pxa-doc-cookbook-grid">
              ${renderCookbook(cookbook)}
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
            <div class="pxa-doc-reference-note">
              <article class="pxa-card">
                <h3>Use references after choosing a product path</h3>
                <p>Start from Generator, Migration, Importer, WebApi, or Designer guidance, then jump into generated API details.</p>
              </article>
              <article class="pxa-card">
                <h3>Keep generated docs separate</h3>
                <p>Generated DocFX and OpenAPI outputs stay authoritative for signatures and contracts; this site stays task-oriented.</p>
              </article>
            </div>
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

    ${renderPxaFooter('PXA.Documentation')}
  </div>
`;
