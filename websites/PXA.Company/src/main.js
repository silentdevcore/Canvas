import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { appendCampaignParams } from '../../shared/campaignAttribution.js';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import { consumeSignedInSignal } from '../../shared/signedInSignal.js';
import { initializeStorageNotice } from '../../shared/storageNotice.js';
import { loadPublishedLegalDocument } from './legalSnapshot.js';

initializeBrowserTelemetry({ application: 'company' });

let signedInForCurrentNavigation = false;

function captureSignedInSignal() {
  const result = consumeSignedInSignal(window.location.search);
  if (!result) return;
  signedInForCurrentNavigation = true;
  const query = result.cleanedSearch;
  history.replaceState({}, '', `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`);
}

captureSignedInSignal();

function isSignedIn() {
  return signedInForCurrentNavigation;
}

const companyRoutes = {
  '/': {
    section: null,
    title: 'PXA.Company | Power Dox Automation',
    description: 'Power Dox Automation helps .NET teams generate, migrate, import, review, and automate business documents with one connected platform.',
  },
  '/products': {
    section: 'products',
    title: 'Products | Power Dox Automation',
    description: 'Explore the PXA product family for document generation, migration, import, design, PDF review, and spreadsheet automation.',
  },
  '/products.html': {
    section: 'products',
    title: 'Products | Power Dox Automation',
    description: 'Explore the PXA product family for document generation, migration, import, design, PDF review, and spreadsheet automation.',
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
  '/cookie-storage.html': {
    section: 'cookie-storage',
    title: 'Cookie & Storage Policy | Power Dox Automation',
    description: 'Understand the necessary cookies and browser storage used by PXA.',
  },
  '/imprint.html': {
    section: 'imprint',
    title: 'Imprint | Power Dox Automation',
    description: 'Provider information for Power Dox Automation.',
  },
  '/withdrawal.html': {
    section: 'withdrawal',
    title: 'Consumer Withdrawal | Power Dox Automation',
    description: 'Consumer withdrawal information for Power Dox Automation.',
  },
  '/dpa.html': {
    section: 'dpa',
    title: 'Data Processing Agreement | Power Dox Automation',
    description: 'Data processing agreement information for Power Dox Automation.',
  },
};

const fallbackRoute = companyRoutes['/'];

const products = [
  {
    slug: 'generator',
    title: 'PXA Generator',
    label: 'Generate',
    text: 'Generate PDF and document output from structured data, reusable templates, and code-first layout primitives.',
    points: ['PDF output from code', 'Template-backed generation', 'Reusable layout model'],
    audience: 'Engineering teams that need reliable server-side document output.',
    detail:
      'PXA Generator is the code-first foundation for producing PDF and document output from structured data, reusable templates, and shared layout primitives.',
    workflows: ['Generate invoices, receipts, statements, and operational PDFs', 'Reuse shared layout primitives across applications', 'Connect generated output to preview and review flows'],
    capabilities: ['Code-first document composition', 'Template-backed PDF generation', 'Structured data binding', 'Reusable typography, table, and layout primitives'],
    integrations: ['PXA Designer for template authoring', 'PXA PDF Viewer for review', 'PXA Documentation for SDK guidance'],
    demo: 'booking-receipt',
    docs: 'code-path',
  },
  {
    slug: 'migration',
    title: 'PXA Migration',
    label: 'Migrate',
    text: 'Move provider-specific PDF code, report designs, and spreadsheet workflows into a clearer PXA target model.',
    points: ['Provider mapping', 'Conversion diagnostics', 'Designer and code paths'],
    audience: 'Teams replacing provider-specific SDKs, reports, or document automation code.',
    detail:
      'PXA Migration tracks provider parity, diagnostics, and conversion flows so legacy document code and report designs can move toward PXA deliberately.',
    workflows: ['Assess provider-specific code and report designs', 'Convert known patterns into PXA migration targets', 'Track unsupported elements with diagnostics instead of silent loss'],
    capabilities: ['Code migration planning', 'Designer migration mapping', 'Provider taxonomy', 'Compatibility diagnostics and parity tracking'],
    integrations: ['DevExpress, Syncfusion, ActiveReports, and JasperReports migration work', 'PXA Demo migration examples', 'PXA Documentation migration guides'],
    demo: 'provider-migration-examples',
    docs: 'migration',
  },
  {
    slug: 'importer',
    title: 'PXA Importer',
    label: 'Import',
    text: 'Normalize existing files so PDF, Office, image, and document inputs can enter controlled automation flows.',
    points: ['File normalization', 'Office and PDF inputs', 'Import diagnostics'],
    audience: 'Product teams that need to accept existing files and turn them into usable document models.',
    detail:
      'PXA Importer normalizes incoming files so PDF, Office, image, and related inputs can participate in generation, migration, and designer workflows.',
    workflows: ['Accept existing customer or internal files', 'Normalize inputs before mapping or migration', 'Preserve diagnostics when a file cannot map cleanly'],
    capabilities: ['PDF and Office input normalization', 'Image and document import paths', 'Import diagnostics', 'Shared file-importer abstractions'],
    integrations: ['PXA Designer for imported layouts', 'PXA Migration for provider conversion', 'PXA Generator for regenerated output'],
    demo: 'file-importer-flow',
    docs: 'editor-path',
  },
  {
    slug: 'designer',
    title: 'PXA Designer',
    label: 'Design',
    text: 'Create and inspect document templates with a visual surface connected to JSON, previews, and generated output.',
    points: ['Visual template editing', 'Preview workflow', 'JSON and code inspection'],
    audience: 'Teams that need visual template editing connected to generated output.',
    detail:
      'PXA Designer gives document-heavy teams a visual authoring surface for templates, reports, previews, JSON inspection, and code-oriented handoff.',
    workflows: ['Design reusable document templates visually', 'Inspect generated JSON and code-oriented output', 'Preview reports before connecting them to application workflows'],
    capabilities: ['Template editing canvas', 'Report and element layout', 'Preview and export paths', 'JSON and code inspection'],
    integrations: ['PXA Generator for output', 'PXA Migration for imported report designs', 'PXA Demo for runnable examples'],
    demo: 'master-detail-report',
    docs: 'editor-path',
  },
  {
    slug: 'pdf-viewer',
    title: 'PXA PDF Viewer',
    label: 'Review',
    text: 'Review generated and imported PDFs in the browser with product-focused workflows for forms and annotations.',
    points: ['Browser PDF review', 'Forms and annotations', 'Inspection workflows'],
    audience: 'Applications that need browser review, forms, annotations, and PDF inspection.',
    detail:
      'PXA PDF Viewer is the browser-facing review surface for generated or imported PDFs, with forms, annotations, and workflow inspection as product goals.',
    workflows: ['Inspect generated PDFs before release', 'Review annotations and form scenarios in the browser', 'Connect viewer behavior to demos and documentation'],
    capabilities: ['Browser-based PDF preview', 'Annotation workflow planning', 'Form review paths', 'Viewer feature parity tracking'],
    integrations: ['PXA Generator output', 'PXA Importer inputs', 'PXA Demo viewer scenarios'],
    demo: 'pdf-viewer-annotations-forms',
    docs: 'pdf-viewer',
  },
  {
    slug: 'spreadsheet',
    title: 'PXA Spreadsheet',
    label: 'Model',
    text: 'Connect workbook data, mappings, and spreadsheet-driven workflows to document generation and export.',
    points: ['Workbook import', 'Data mapping', 'Document export flows'],
    audience: 'Teams whose document automation depends on workbook data, mapping, or export workflows.',
    detail:
      'PXA Spreadsheet connects workbook-driven data and document automation, covering import, editing, mapping, formulas, and export-oriented workflows.',
    workflows: ['Use workbook data as document input', 'Map spreadsheet structures into repeatable document flows', 'Prepare spreadsheet-backed exports and reports'],
    capabilities: ['Workbook import and mapping', 'Spreadsheet data modeling', 'Formula-aware workflow planning', 'Document export integration'],
    integrations: ['PXA Generator for document output', 'PXA Importer for workbook inputs', 'PXA Migration for spreadsheet provider work'],
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
    title: 'Replace provider-specific PDF code',
    text: 'Turn legacy SDK calls into tracked migration tasks with diagnostics, parity notes, and a stable PXA target API.',
  },
  {
    title: 'Modernize report templates',
    text: 'Convert report layouts, bands, charts, tables, and data-bound elements into a normalized designer model.',
  },
  {
    title: 'Automate business documents',
    text: 'Generate invoices, receipts, reports, statements, and internal PDFs from data and reusable templates.',
  },
  {
    title: 'Validate output before rollout',
    text: 'Use demos, previews, and viewer workflows to inspect generated files before committing them to production flows.',
  },
];

const providers = ['DevExpress', 'Syncfusion', 'ActiveReports', 'JasperReports', 'GemBox', 'Aspose', 'iText', 'PDF Tools'];

const proofPoints = [
  { value: '6', label: 'connected product areas' },
  { value: '2', label: 'migration paths: code and designer' },
  { value: '1', label: 'shared PXA document model' },
];

const aboutPrinciples = [
  {
    title: 'Built for teams where documents are product behavior',
    text: 'PXA treats PDFs, reports, templates, imports, and spreadsheets as part of the application architecture, not as disconnected side effects.',
  },
  {
    title: 'Migration work should be explicit',
    text: 'Provider parity, unsupported features, manual follow-ups, and conversion diagnostics are visible so teams can plan real migrations.',
  },
  {
    title: 'Docs, demos, and product surfaces stay connected',
    text: 'Every workflow should be explainable, runnable, and traceable from product page to documentation to demo and implementation status.',
  },
];

const aboutStory = [
  {
    title: 'Why PXA exists',
    text: 'Most document stacks grow by accident: one library for PDF generation, one reporting designer, one viewer, one spreadsheet workflow, and a set of migration scripts nobody wants to touch. PXA exists to make those flows deliberate and connected.',
  },
  {
    title: 'Who it is for',
    text: '.NET teams building business software, internal platforms, reporting systems, migration tooling, and document-heavy workflows where correctness and maintainability matter.',
  },
  {
    title: 'How we build it',
    text: 'The platform is organized around focused products, shared document models, provider-aware migration paths, and public demos that keep product claims testable.',
  },
];

const supportPaths = [
  {
    title: 'Self-guided evaluation',
    text: 'Use product pages, demos, and documentation to understand where PXA fits before starting a deeper implementation.',
    cta: 'Explore demos',
    href: siteLinks.demo,
  },
  {
    title: 'Migration review',
    text: 'Bring existing provider code, report files, or spreadsheet workflows and review them against PXA migration coverage.',
    cta: 'Contact sales',
    href: companyPage('contact'),
  },
  {
    title: 'Implementation guidance',
    text: 'Use the documentation path for generator, designer, viewer, importer, spreadsheet, and migration integration work.',
    cta: 'Open docs',
    href: siteLinks.documentation,
  },
];

const supportTopics = [
  'Provider migration assessment',
  'Report designer conversion review',
  'PDF generation architecture',
  'Viewer and forms workflow planning',
  'Importer and spreadsheet data mapping',
  'Enterprise rollout and parity planning',
];

const contactPaths = [
  {
    title: 'Sales conversation',
    text: 'For product fit, rollout scope, procurement questions, or choosing the right adoption path.',
    cta: 'Discuss adoption',
  },
  {
    title: 'Migration assessment',
    text: 'For teams replacing DevExpress, Syncfusion, ActiveReports, JasperReports, Aspose, iText, or similar provider stacks.',
    cta: 'Review migration scope',
  },
  {
    title: 'Technical evaluation',
    text: 'For engineering teams validating generator, designer, viewer, importer, or spreadsheet workflows before implementation.',
    cta: 'Plan evaluation',
  },
];

const legalPages = {
  terms: {
    kicker: 'Terms',
    title: 'Terms for using PXA websites, demos, and evaluation materials',
    text:
      'These draft terms describe the intended structure for using Power Dox Automation web properties, documentation, demos, and evaluation materials. They are product-facing placeholders and should be reviewed before public launch.',
    sections: [
      {
        title: 'Website and demo use',
        text: 'Visitors may use public product pages, demos, and documentation to evaluate PXA capabilities and migration fit.',
      },
      {
        title: 'No production commitment from demos',
        text: 'Demo behavior is informational and may not represent final product, licensing, hosting, or support commitments.',
      },
      {
        title: 'Commercial terms',
        text: 'Pricing, licensing, support, and enterprise rollout terms should be agreed through a direct sales or evaluation process.',
      },
    ],
    notice: 'Draft notice: replace this page with reviewed legal terms before using the site publicly.',
  },
  privacy: {
    kicker: 'Privacy',
    title: 'How PXA currently processes personal and customer data',
    text:
      'This technical draft reflects the processing currently implemented across PXA websites, Account, Admin, Designer, API, workers, mail, storage, and observability. It is not the final counsel-approved Privacy Notice.',
    sections: [
      {
        title: 'Accounts and organizations',
        text: 'PXA processes identity, contact, authentication, session, organization, membership, role, subscription, entitlement, usage, and license data to provide and secure customer workspaces.',
      },
      {
        title: 'Customer documents and templates',
        text: 'Documents, source code, images, OCR text, templates, spreadsheet data, and generated results are processed to perform operations requested by the customer. Synchronous content is transient; queued input and result objects expire after seven days by default.',
      },
      {
        title: 'Mail, Legal, and administration',
        text: 'Transactional mail metadata supports verification, invitations, recovery, and security notices. Legal publication, acceptance evidence, and minimized administrative audit events support account and compliance workflows.',
      },
      {
        title: 'Operational telemetry',
        text: 'PXA records minimized route groups, outcomes, timings, service health, traces, and browser Web Vitals for reliability and security. Browser telemetry does not include user identifiers or document bodies, and no optional marketing storage is enabled.',
      },
      {
        title: 'Providers, regions, and transfers',
        text: 'PostgreSQL, object storage, OCR, and the default observability stack run in the selected PXA Server region. SMTP, external OTLP, S3-compatible observability storage, and backup destinations remain deployment choices and require provider, region, contract, and transfer review before production activation.',
      },
      {
        title: 'Retention and user rights',
        text: 'Technical deletion exists for jobs, mail, temporary files, browser state, and observability data. Final periods for accounts, organizations, billing, templates, audit, legal evidence, and backups remain production blockers pending legal approval. The final Privacy Notice will state applicable rights and contact details.',
      },
    ],
    notice: 'Launch blocker: verified operator details, legal bases, provider contracts, regions, transfers, retention periods, and data-subject procedures require counsel approval.',
  },
  license: {
    kicker: 'License',
    title: 'License and evaluation model',
    text:
      'This draft license page explains the intended evaluation paths for PXA while final product packaging and commercial terms are still being defined.',
    sections: [
      {
        title: 'Evaluation',
        text: 'Teams can start by reviewing demos and documentation, then validate specific workflows against product and migration requirements.',
      },
      {
        title: 'Team adoption',
        text: 'Team usage is expected to focus on shared generator, designer, importer, viewer, spreadsheet, and documentation workflows.',
      },
      {
        title: 'Enterprise migration',
        text: 'Migration-heavy adoption should include provider assessment, parity planning, conversion expectations, and support boundaries.',
      },
    ],
    notice: 'Draft notice: final license language should be reviewed before external distribution.',
  },
  'cookie-storage': {
    kicker: 'Cookie and storage policy',
    title: 'Necessary browser storage used by PXA',
    text:
      'PXA currently uses only first-party storage required for security, sessions, language, and application preferences explicitly requested by the user.',
    sections: [
      {
        title: 'Storage notice acknowledgement',
        text: 'The first-party PXA.StorageNotice cookie stores only the notice version for 180 days. It contains no user or device identifier.',
      },
      {
        title: 'Security and authenticated sessions',
        text: 'Host-only PXA session, Designer session, and anti-forgery cookies protect authenticated requests. They are HttpOnly, unavailable to application JavaScript, and retained for the browser session or up to eight hours with sliding renewal.',
      },
      {
        title: 'Language and interface preferences',
        text: 'Local keys pxa_locale, editor-storage, pxa-code-editor-lang-v2, pxa_export_format, and the PDF and Spreadsheet sidebar keys retain only choices explicitly made in the interface. They remain until changed, cleared, or removed during a tenant-state reset where applicable.',
      },
      {
        title: 'Temporary tab handoffs',
        text: 'Session keys pxa.designer.auth-transaction, pxa_migration_designer_handoff, and pdf_viewer_handoff transfer authentication or document workflow state. They are consumed by the target flow or removed when the browser tab closes.',
      },
      {
        title: 'Local document working copies',
        text: 'Keys pxa-code-editor-draft-v2 and pxa-spreadsheet keep a local working copy to recover from a refresh. These copies may contain customer-entered content and remain until replaced, cleared, or removed at sign-out or organization switch.',
      },
      {
        title: 'Retention and control',
        text: 'Users can remove local data through sign-out, organization switching, or browser site-data controls. PXA does not use the browser-storage inventory for cross-site tracking.',
      },
      {
        title: 'No optional tracking at launch',
        text: 'PXA does not load optional analytics or marketing storage. A consent center will be introduced before that changes.',
      },
    ],
    notice: 'This operational inventory requires final review before production launch.',
  },
  imprint: {
    kicker: 'Imprint',
    title: 'Provider information',
    text: 'The legally required operator details must be completed and approved before public production launch.',
    sections: [
      { title: 'Operator', text: '[Legal company name and legal form]' },
      { title: 'Address and representation', text: '[Registered address and authorized representative]' },
      { title: 'Register and tax details', text: '[Commercial register, registration number, and VAT ID]' },
    ],
    notice: 'Launch blocker: replace every bracketed placeholder with verified company information.',
  },
  withdrawal: {
    kicker: 'Consumer withdrawal',
    title: 'Withdrawal information for consumers',
    text: 'Paid consumer checkout remains unavailable until the withdrawal process and durable confirmation have been approved.',
    sections: [
      { title: 'Withdrawal right', text: 'The final period, conditions, and exercise instructions require counsel-approved wording.' },
      { title: 'Digital performance', text: 'Any request to begin digital performance early must be captured separately and explicitly.' },
      { title: 'Model form', text: 'A counsel-approved model withdrawal form will be provided before consumer sales begin.' },
    ],
    notice: 'No paid B2C contract may be concluded from this draft page.',
  },
  dpa: {
    kicker: 'Data Processing Agreement',
    title: 'Processing customer documents on behalf of organizations',
    text: 'PXA will provide an Article 28 agreement for business customers before production document processing.',
    sections: [
      { title: 'Processing scope', text: 'Products, data categories, purposes, duration, and controller instructions will be documented.' },
      { title: 'Security measures', text: 'Approved technical and organizational measures will accompany the agreement.' },
      { title: 'Subprocessors and transfers', text: 'Approved providers, processing regions, and transfer safeguards will be listed transparently.' },
    ],
    notice: 'The DPA, security measures, and subprocessor list require legal and operational approval.',
  },
};

const roadmap = [
  {
    title: 'Company site',
    text: 'Product positioning, product pages, trust signals, pricing paths, and contact entry points.',
    status: 'Active',
  },
  {
    title: 'Documentation site',
    text: 'Editor and SDK documentation with product-first navigation, migration guides, and API references.',
    status: 'Planned',
  },
  {
    title: 'Demo site',
    text: 'Runnable examples for generator, viewer, migration, spreadsheet, importer, and designer flows.',
    status: 'Planned',
  },
];

const showcases = [
  {
    title: 'Invoice / Booking Receipt',
    text: 'Generate a business-ready document from structured data and reusable layout primitives.',
    status: 'Ready',
    route: 'booking-receipt',
    docs: 'demo-examples',
  },
  {
    title: 'Report migration gallery',
    text: 'Compare report migration behavior across providers and track remaining conversion gaps.',
    status: 'Preview',
    route: 'master-detail-report',
    docs: 'migration',
  },
  {
    title: 'PDF viewer workflows',
    text: 'Review forms, annotations, and generated output in a browser workflow connected to docs.',
    status: 'Preview',
    route: 'pdf-viewer-annotations-forms',
    docs: 'pdf-viewer',
  },
];

const pricingTiers = [
  {
    title: 'Trial',
    bestFor: 'Developers and teams validating product fit.',
    text: 'Explore the product family, run demos, review documentation, and validate whether PXA fits your generation or migration scenario.',
    features: ['Access demos and examples', 'Evaluate product areas', 'Review documentation and migration coverage'],
    cta: 'Start with demos',
    href: appendCampaignParams(`${siteLinks.account}register`),
  },
  {
    title: 'Team',
    bestFor: 'Product teams building document automation into applications.',
    text: 'Adopt PXA across generator, designer, importer, viewer, and spreadsheet workflows with a shared implementation model.',
    features: ['Build repeatable document workflows', 'Connect design, generation, and review', 'Use product documentation as the team baseline'],
    cta: 'Read docs',
    href: siteLinks.documentation,
  },
  {
    title: 'Enterprise',
    bestFor: 'Organizations planning provider migrations or broad rollout.',
    text: 'Plan larger migration programs, provider parity work, support expectations, and rollout guidance with the PXA team.',
    features: ['Migration assessment', 'Provider parity planning', 'Rollout and support path definition'],
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

function renderTextCards(items, className = '') {
  return items
    .map(
      (item) => `
        <article class="pxa-card ${className}">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          ${item.cta && item.href ? `<a href="${item.href}">${item.cta}</a>` : ''}
        </article>
      `,
    )
    .join('');
}

function renderContactCards(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-contact-card">
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <span>${item.cta}</span>
        </article>
      `,
    )
    .join('');
}

function renderLegalSections(items) {
  return items
    .map(
      (item) => `
        <article class="pxa-card pxa-company-legal-section">
          <h2>${item.title}</h2>
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
          <strong>${item.bestFor}</strong>
          <p>${item.text}</p>
          <ul>
            ${item.features.map((feature) => `<li>${feature}</li>`).join('')}
          </ul>
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

function signInUrl() {
  return `${siteLinks.account}login?returnUrl=${encodeURIComponent(window.location.href)}`;
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
        <div class="pxa-header-actions">
          <a class="pxa-button pxa-button--secondary pxa-header-cta" href="${siteLinks.designer}">Live demo</a>
          ${isSignedIn()
            ? `<a class="pxa-header-account-link" href="${siteLinks.account}dashboard">My account</a>`
            : `<a class="pxa-header-account-link" href="${signInUrl()}">Sign in</a>
          <a class="pxa-header-account-link" href="${appendCampaignParams(`${siteLinks.account}register`)}">Register</a>`}
          <a class="pxa-button pxa-button--primary pxa-header-cta" href="${companyPage('contact')}"${activeAttr('contact')}>Contact sales</a>
        </div>
      </div>
    </header>
  `;
}

function renderHeroSection() {
  return `
    <section class="pxa-company-hero">
      <div class="pxa-container pxa-company-hero__grid">
        <div>
          <p class="pxa-kicker">Power Dox Automation for .NET teams</p>
          <h1 class="pxa-heading">Generate, migrate, and review business documents with one connected PXA platform.</h1>
          <p class="pxa-lede">
            PXA brings document generation, provider migration, file import, template design,
            PDF review, and spreadsheet-driven automation into a single product family for teams
            that cannot afford fragmented document workflows.
          </p>
          <div class="pxa-action-row">
            <a class="pxa-button pxa-button--primary" href="${appendCampaignParams(`${siteLinks.account}register`)}">Start free trial</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.demo}">View demos</a>
            <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Read documentation</a>
          </div>
          <p class="pxa-company-trial-note">30-day Premium Trial, full product access, no credit card required.</p>
          <div class="pxa-company-proof-strip" aria-label="PXA proof points">
            ${renderProof(proofPoints)}
          </div>
        </div>
        <aside class="pxa-card pxa-company-snapshot" aria-label="PXA platform snapshot">
          <strong>Platform snapshot</strong>
          <dl>
            <div><dt>Products</dt><dd>6</dd></div>
            <div><dt>Migration</dt><dd>Code and designer</dd></div>
            <div><dt>Primary audience</dt><dd>.NET teams</dd></div>
            <div><dt>Core output</dt><dd>PDF and document flows</dd></div>
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
        <p class="pxa-kicker">Product suite</p>
        <h1 class="pxa-heading">Everything around the document lifecycle, grouped into focused PXA products.</h1>
        <p class="pxa-lede">
          Start with the product area that matches your current problem, then connect it
          to the rest of the platform as your automation grows.
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
      <div class="pxa-container">
        <div class="pxa-company-product-detail">
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
            <strong>Best fit</strong>
            <p>${product.audience}</p>
            <ul>
              ${product.points.map((point) => `<li>${point}</li>`).join('')}
            </ul>
          </aside>
        </div>
        <div class="pxa-company-product-content">
          <article class="pxa-card">
            <h2>Typical workflows</h2>
            <ul>
              ${product.workflows.map((item) => `<li>${item}</li>`).join('')}
            </ul>
          </article>
          <article class="pxa-card">
            <h2>Core capabilities</h2>
            <ul>
              ${product.capabilities.map((item) => `<li>${item}</li>`).join('')}
            </ul>
          </article>
          <article class="pxa-card">
            <h2>Works with</h2>
            <ul>
              ${product.integrations.map((item) => `<li>${item}</li>`).join('')}
            </ul>
          </article>
        </div>
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
          <h2 class="pxa-heading">Designed around real provider migration work</h2>
          <p class="pxa-lede">
            PXA is shaped by concrete migration scenarios from known PDF, reporting,
            spreadsheet, and document libraries. Parity gaps become documented tasks,
            not hidden surprises.
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
        <h2 class="pxa-heading">Examples that make document automation testable</h2>
        <p class="pxa-lede">
          Each example is meant to connect a realistic business document, a runnable demo,
          and the documentation needed to implement or migrate the workflow.
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
        <h2 class="pxa-heading">A product website connected to docs and demos</h2>
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
          <h1 class="pxa-heading">Choose the adoption path that matches your document workload.</h1>
          <p class="pxa-lede">
            PXA pricing is organized around how teams adopt document automation:
            evaluate the platform, standardize team workflows, or plan a larger migration.
            Final commercial terms can stay flexible while the product paths remain clear.
          </p>
        </div>
        <div class="pxa-company-pricing-grid">
          ${renderPricing(pricingTiers)}
        </div>
        <div class="pxa-card pxa-company-pricing-note">
          <strong>Migration-heavy projects</strong>
          <p>
            If your team is replacing an existing provider stack, start with an Enterprise conversation.
            The useful first step is usually a migration assessment, not a generic license quote.
          </p>
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
          <h1 class="pxa-heading">Power Dox Automation is built for teams modernizing document-heavy software.</h1>
          <p class="pxa-lede">
            PXA exists to make document automation less fragmented: one family for generating output,
            migrating legacy providers, importing files, reviewing PDFs, and connecting spreadsheet-backed
            workflows to product-ready examples.
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
      <div class="pxa-container">
        <div class="pxa-company-story-grid">
          ${renderTextCards(aboutStory, 'pxa-company-story-card')}
        </div>
      </div>
    </section>
  `;
}

function renderSupportPage() {
  return `
    <section class="pxa-section pxa-section--soft" id="support">
      <div class="pxa-container">
        <div>
          <p class="pxa-kicker">Support</p>
          <h1 class="pxa-heading">Support paths for evaluation, migration, and implementation.</h1>
          <p class="pxa-lede">
            Start with public docs and runnable demos, then bring migration-specific questions,
            provider parity gaps, or rollout planning into a direct project conversation.
          </p>
        </div>
        <div class="pxa-company-support-grid">
          ${renderTextCards(supportPaths, 'pxa-company-support-card')}
        </div>
        <div class="pxa-card pxa-company-support-topics">
          <strong>Common support topics</strong>
          <ul>
            ${supportTopics.map((topic) => `<li>${topic}</li>`).join('')}
          </ul>
        </div>
      </div>
    </section>
  `;
}

function renderContactPage() {
  return `
    <section class="pxa-section" id="contact">
      <div class="pxa-container pxa-company-contact">
        <p class="pxa-kicker">Contact</p>
        <h1 class="pxa-heading">Talk to the PXA team about product fit, migration scope, or technical evaluation.</h1>
        <p class="pxa-lede">
          Use this page to route the conversation before a final form or CRM integration is selected.
          The goal is to understand what you are building, what you are replacing, and which PXA products
          should be evaluated first.
        </p>
        <div class="pxa-company-contact-grid">
          ${renderContactCards(contactPaths)}
        </div>
        <div class="pxa-card pxa-company-contact-note">
          <strong>Useful context to prepare</strong>
          <ul>
            <li>Which provider, report designer, PDF SDK, or spreadsheet workflow you use today.</li>
            <li>Which documents matter most: invoices, reports, forms, statements, receipts, or internal PDFs.</li>
            <li>Whether the first goal is generation, migration, import, viewer review, or spreadsheet-backed automation.</li>
          </ul>
        </div>
      </div>
    </section>
  `;
}

function renderLegalPage(kind) {
  const page = legalPages[kind] || legalPages.terms;
  return `
    <section class="pxa-section" id="${kind}">
      <div class="pxa-container">
        <div class="pxa-company-legal">
          <p class="pxa-kicker">${page.kicker}</p>
          <h1 class="pxa-heading">${page.title}</h1>
          <p class="pxa-lede">${page.text}</p>
        </div>
        <div class="pxa-company-legal-grid" data-legal-content>
          ${renderLegalSections(page.sections)}
        </div>
        <div class="pxa-card pxa-company-legal-notice">
          <strong>${page.notice}</strong>
        </div>
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
    case 'cookie-storage':
      return renderLegalPage('cookie-storage');
    case 'imprint':
      return renderLegalPage('imprint');
    case 'withdrawal':
      return renderLegalPage('withdrawal');
    case 'dpa':
      return renderLegalPage('dpa');
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

async function hydratePublishedLegalDocument(kind) {
  if (!legalPages[kind]) return;
  try {
    const result = await loadPublishedLegalDocument({ kind, locale: 'en' });
    const legalDocument = result.document;
    const content = document.querySelector('[data-legal-content]');
    if (!content) return;
    content.className = 'pxa-company-legal-document';
    content.dataset.legalSource = result.source;
    content.innerHTML = legalDocument.renderedHtml;
    const notice = document.querySelector('.pxa-company-legal-notice strong');
    if (notice) {
      const version = `Version ${legalDocument.version} · Effective ${new Date(legalDocument.effectiveAt).toLocaleDateString()}${legalDocument.isAuthoritative ? ' · Authoritative' : ' · Convenience translation'}`;
      notice.textContent = result.source === 'live'
        ? version
        : `${version} · Archived copy from ${new Date(result.generatedAt).toLocaleString()}${result.stale ? ' · Snapshot older than 30 days' : ''}. The Legal API is unavailable; transactions requiring current-version verification remain disabled.`;
    }
  } catch {
    const content = document.querySelector('[data-legal-content]');
    if (content) {
      content.className = 'pxa-company-legal-document pxa-company-legal-document--unavailable';
      content.dataset.legalSource = 'unavailable';
      content.innerHTML = `
        <section role="status" aria-live="polite">
          <h2>Verified legal content is temporarily unavailable</h2>
          <p>Neither the Legal API nor its last-known-good published snapshot could be verified. Please try again later.</p>
        </section>
      `;
    }
    const notice = document.querySelector('.pxa-company-legal-notice strong');
    if (notice)
      notice.textContent = 'Registration and other transactions requiring current legal versions remain disabled.';
  }
}

initializeStorageNotice();
hydratePublishedLegalDocument(currentRoute.section);
