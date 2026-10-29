import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { appendCampaignParams } from '../../shared/campaignAttribution.js';
import { renderPvadFooter } from '../../shared/footer.js';
import { pvadBrand } from '../../shared/brand.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import { consumeSignedInSignal } from '../../shared/signedInSignal.js';
import { initializeStorageNotice } from '../../shared/storageNotice.js';
import { bindStatusPage, renderStatusPage } from '../../shared/statusPages.ts';
import {
  loadPublishedLegalDocument,
  loadPublishedLegalHistory,
  loadPublishedLegalVersion,
} from './legalSnapshot.js';

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
    title: 'PVADE Company | PVADE',
    description: 'PVADE helps .NET teams generate, migrate, import, review, and automate business documents with one connected platform.',
  },
  '/products': {
    section: 'products',
    title: 'Products | PVADE',
    description: 'Explore the PVADE product family for document generation, migration, import, design, PDF review, and spreadsheet automation.',
  },
  '/products.html': {
    section: 'products',
    title: 'Products | PVADE',
    description: 'Explore the PVADE product family for document generation, migration, import, design, PDF review, and spreadsheet automation.',
  },
  '/pricing': {
    section: 'pricing',
    title: 'Pricing | PVADE',
    description: 'Review placeholder Trial, Team, and Enterprise paths for PVADE.',
  },
  '/pricing.html': {
    section: 'pricing',
    title: 'Pricing | PVADE',
    description: 'Review placeholder Trial, Team, and Enterprise paths for PVADE.',
  },
  '/about': {
    section: 'about',
    title: 'About | PVADE',
    description: 'Learn how PVADE connects product, documentation, demos, and migration-first document workflows.',
  },
  '/about.html': {
    section: 'about',
    title: 'About | PVADE',
    description: 'Learn how PVADE connects product, documentation, demos, and migration-first document workflows.',
  },
  '/support': {
    section: 'support',
    title: 'Support | PVADE',
    description: 'Find support paths through PVADE documentation, demos, provider parity planning, and migration guidance.',
  },
  '/support.html': {
    section: 'support',
    title: 'Support | PVADE',
    description: 'Find support paths through PVADE documentation, demos, provider parity planning, and migration guidance.',
  },
  '/contact': {
    section: 'contact',
    title: 'Contact Sales | PVADE',
    description: 'Contact the PVADE team to plan product evaluation, migration work, or enterprise rollout.',
  },
  '/contact.html': {
    section: 'contact',
    title: 'Contact Sales | PVADE',
    description: 'Contact the PVADE team to plan product evaluation, migration work, or enterprise rollout.',
  },
  '/terms.html': {
    section: 'terms',
    title: 'Terms | PVADE',
    description: 'Review the Terms and Conditions for PVADE.',
  },
  '/privacy.html': {
    section: 'privacy',
    title: 'Privacy | PVADE',
    description: 'Learn how PVADE processes and protects personal data.',
  },
  '/license.html': {
    section: 'license',
    title: 'License | PVADE',
    description: 'Review software, service, SDK, and On-Premise license rights for PVADE.',
  },
  '/cookie-storage.html': {
    section: 'cookie-storage',
    title: 'Cookie & Storage Policy | PVADE',
    description: 'Understand the necessary cookies and browser storage used by PVADE.',
  },
  '/imprint.html': {
    section: 'imprint',
    title: 'Imprint | PVADE',
    description: 'Provider information for PVADE.',
  },
  '/withdrawal.html': {
    section: 'withdrawal',
    title: 'Consumer Withdrawal | PVADE',
    description: 'Consumer withdrawal information for PVADE.',
  },
  '/dpa.html': {
    section: 'dpa',
    title: 'Data Processing Agreement | PVADE',
    description: 'Data processing agreement information for PVADE.',
  },
};

const fallbackRoute = companyRoutes['/'];

const products = [
  {
    slug: 'generator',
    title: pvadBrand.products.pdf,
    label: 'Generate',
    text: 'Generate PDF and document output from structured data, reusable templates, and code-first layout primitives.',
    points: ['PDF output from code', 'Template-backed generation', 'Reusable layout model'],
    audience: 'Engineering teams that need reliable server-side document output.',
    detail:
      'PVADE Generator is the code-first foundation for producing PDF and document output from structured data, reusable templates, and shared layout primitives.',
    workflows: ['Generate invoices, receipts, statements, and operational PDFs', 'Reuse shared layout primitives across applications', 'Connect generated output to preview and review flows'],
    capabilities: ['Code-first document composition', 'Template-backed PDF generation', 'Structured data binding', 'Reusable typography, table, and layout primitives'],
    integrations: ['PVADE Designer for template authoring', 'PVADP Viewer for review', 'PVADE Documentation for SDK guidance'],
    demo: 'booking-receipt',
    docs: 'code-path',
  },
  {
    slug: 'migration',
    title: 'PVADE Migration',
    label: 'Migrate',
    text: 'Move provider-specific PDF code, report designs, and spreadsheet workflows into a clearer PVADE target model.',
    points: ['Provider mapping', 'Conversion diagnostics', 'Designer and code paths'],
    audience: 'Teams replacing provider-specific SDKs, reports, or document automation code.',
    detail:
      'PVADE Migration tracks provider parity, diagnostics, and conversion flows so legacy document code and report designs can move toward PVADE deliberately.',
    workflows: ['Assess provider-specific code and report designs', 'Convert known patterns into PVADE migration targets', 'Track unsupported elements with diagnostics instead of silent loss'],
    capabilities: ['Code migration planning', 'Designer migration mapping', 'Provider taxonomy', 'Compatibility diagnostics and parity tracking'],
    integrations: ['DevExpress, Syncfusion, ActiveReports, and JasperReports migration work', 'PVADE Demo migration examples', 'PVADE Documentation migration guides'],
    demo: 'provider-migration-examples',
    docs: 'migration',
  },
  {
    slug: 'importer',
    title: 'PVADE Importer',
    label: 'Import',
    text: 'Normalize existing files so PDF, Office, image, and document inputs can enter controlled automation flows.',
    points: ['File normalization', 'Office and PDF inputs', 'Import diagnostics'],
    audience: 'Product teams that need to accept existing files and turn them into usable document models.',
    detail:
      'PVADE Importer normalizes incoming files so PDF, Office, image, and related inputs can participate in generation, migration, and designer workflows.',
    workflows: ['Accept existing customer or internal files', 'Normalize inputs before mapping or migration', 'Preserve diagnostics when a file cannot map cleanly'],
    capabilities: ['PDF and Office input normalization', 'Image and document import paths', 'Import diagnostics', 'Shared file-importer abstractions'],
    integrations: ['PVADE Designer for imported layouts', 'PVADE Migration for provider conversion', 'PVADE Generator for regenerated output'],
    demo: 'file-importer-flow',
    docs: 'editor-path',
  },
  {
    slug: 'designer',
    title: 'PVADE Designer',
    label: 'Design',
    text: 'Create and inspect document templates with a visual surface connected to JSON, previews, and generated output.',
    points: ['Visual template editing', 'Preview workflow', 'JSON and code inspection'],
    audience: 'Teams that need visual template editing connected to generated output.',
    detail:
      'PVADE Designer gives document-heavy teams a visual authoring surface for templates, reports, previews, JSON inspection, and code-oriented handoff.',
    workflows: ['Design reusable document templates visually', 'Inspect generated JSON and code-oriented output', 'Preview reports before connecting them to application workflows'],
    capabilities: ['Template editing canvas', 'Report and element layout', 'Preview and export paths', 'JSON and code inspection'],
    integrations: ['PVADE Generator for output', 'PVADE Migration for imported report designs', 'PVADE Demo for runnable examples'],
    demo: 'master-detail-report',
    docs: 'editor-path',
  },
  {
    slug: 'pdf-viewer',
    title: 'PVADP Viewer',
    label: 'Review',
    text: 'Review generated and imported PDFs in the browser with product-focused workflows for forms and annotations.',
    points: ['Browser PDF review', 'Forms and annotations', 'Inspection workflows'],
    audience: 'Applications that need browser review, forms, annotations, and PDF inspection.',
    detail:
      'PVADP Viewer is the browser-facing review surface for generated or imported PDFs, with forms, annotations, and workflow inspection as product goals.',
    workflows: ['Inspect generated PDFs before release', 'Review annotations and form scenarios in the browser', 'Connect viewer behavior to demos and documentation'],
    capabilities: ['Browser-based PDF preview', 'Annotation workflow planning', 'Form review paths', 'Viewer feature parity tracking'],
    integrations: ['PVADE Generator output', 'PVADE Importer inputs', 'PVADE Demo viewer scenarios'],
    demo: 'pdf-viewer-annotations-forms',
    docs: 'pdf-viewer',
  },
  {
    slug: 'spreadsheet',
    title: pvadBrand.products.spreadsheet,
    label: 'Model',
    text: 'Connect workbook data, mappings, and spreadsheet-driven workflows to document generation and export.',
    points: ['Workbook import', 'Data mapping', 'Document export flows'],
    audience: 'Teams whose document automation depends on workbook data, mapping, or export workflows.',
    detail:
      'PVADS connects workbook-driven data and document automation, covering import, editing, mapping, formulas, and export-oriented workflows.',
    workflows: ['Use workbook data as document input', 'Map spreadsheet structures into repeatable document flows', 'Prepare spreadsheet-backed exports and reports'],
    capabilities: ['Workbook import and mapping', 'Spreadsheet data modeling', 'Formula-aware workflow planning', 'Document export integration'],
    integrations: ['PVADE Generator for document output', 'PVADE Importer for workbook inputs', 'PVADE Migration for spreadsheet provider work'],
    demo: 'spreadsheet-import-export',
    docs: 'spreadsheet',
  },
];

for (const product of products) {
  companyRoutes[`/products/${product.slug}.html`] = {
    section: `product:${product.slug}`,
    title: `${product.title} | PVADE`,
    description: product.text,
  };
}

const useCases = [
  {
    title: 'Replace provider-specific PDF code',
    text: 'Turn legacy SDK calls into tracked migration tasks with diagnostics, parity notes, and a stable PVADE target API.',
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
  { value: '1', label: 'shared PVADE document model' },
];

const aboutPrinciples = [
  {
    title: 'Built for teams where documents are product behavior',
    text: 'PVADE treats PDFs, reports, templates, imports, and spreadsheets as part of the application architecture, not as disconnected side effects.',
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
    title: 'Why PVADE exists',
    text: 'Most document stacks grow by accident: one library for PDF generation, one reporting designer, one viewer, one spreadsheet workflow, and a set of migration scripts nobody wants to touch. PVADE exists to make those flows deliberate and connected.',
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
    text: 'Use product pages, demos, and documentation to understand where PVADE fits before starting a deeper implementation.',
    cta: 'Explore demos',
    href: siteLinks.demo,
  },
  {
    title: 'Migration review',
    text: 'Bring existing provider code, report files, or spreadsheet workflows and review them against PVADE migration coverage.',
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
    title: 'Terms and Conditions for PVADE',
    text:
      'The Terms govern PVADE websites, Cloud, APIs, SDK-backed services, Designer, document operations, downloadable software, and On-Premise deployments under Swiss law, while preserving mandatory local rights.',
    sections: [
      {
        title: 'Accounts, plans, and services',
        text: 'The binding version defines account responsibilities, Free, Trial, Premium and Enterprise plans, ordering, payment, service changes, and termination.',
      },
      {
        title: 'Customer content and acceptable use',
        text: 'Customers retain their content and grant only the processing rights required to provide the service. Lawful content, authorization, and output review remain customer responsibilities.',
      },
      {
        title: 'Rights, warranties, and disputes',
        text: 'The Terms preserve non-excludable liability and consumer rights, distinguish business limitations, and use Switzerland as the contractual-law baseline.',
      },
    ],
    notice: 'The verified published database version and its effective date control.',
  },
  privacy: {
    kicker: 'Privacy',
    title: 'How PVADE currently processes personal and customer data',
    text:
      'This Notice explains PVADE processing as a Swiss controller for account and service data and as a processor for Customer Content, with additional mandatory privacy laws applying by market.',
    sections: [
      {
        title: 'Accounts and organizations',
        text: 'PVADE processes identity, contact, authentication, session, organization, membership, role, subscription, entitlement, usage, and license data to provide and secure customer workspaces.',
      },
      {
        title: 'Customer documents and templates',
        text: 'Documents, source code, images, OCR text, templates, spreadsheet data, and generated results are processed for customer-requested operations. Transient queued content is deleted after download or within 24 hours; explicit retained mode currently lasts seven days.',
      },
      {
        title: 'Mail, Legal, and administration',
        text: 'Transactional mail metadata supports verification, invitations, recovery, and security notices. Legal publication, acceptance evidence, and minimized administrative audit events support account and compliance workflows.',
      },
      {
        title: 'Operational telemetry',
        text: 'PVADE records minimized route groups, outcomes, timings, service health, traces, and browser Web Vitals for reliability and security. Browser telemetry does not include user identifiers or document bodies, and no optional marketing storage is enabled.',
      },
      {
        title: 'Providers, regions, and transfers',
        text: 'PostgreSQL, object storage, OCR, and the default observability stack run in the selected PVADE Server region. SMTP, external OTLP, S3-compatible observability storage, and backup destinations remain deployment choices and require provider, region, contract, and transfer review before production activation.',
      },
      {
        title: 'Retention and user rights',
        text: 'Technical deletion exists for jobs, mail, temporary files, browser state, and observability data. Final periods for accounts, organizations, billing, templates, audit, legal evidence, and backups remain production blockers pending legal approval. The final Privacy Notice will state applicable rights and contact details.',
      },
    ],
    notice: 'The verified published database version and its effective date control.',
  },
  license: {
    kicker: 'License',
    title: 'Software and Service License Agreement',
    text:
      'The License defines permitted use of PVADE software, On-Premise components, CLI tools, SDK clients, examples, documentation, and signed offline licenses.',
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
    notice: 'The verified published database version and applicable order form control.',
  },
  'cookie-storage': {
    kicker: 'Cookie and storage policy',
    title: 'Necessary browser storage used by PVADE',
    text:
      'PVADE currently uses only first-party storage required for security, sessions, language, and application preferences explicitly requested by the user.',
    sections: [
      {
        title: 'Storage notice acknowledgement',
        text: 'The first-party PVAD.StorageNotice cookie stores only the notice version for 180 days. The previous PXA.StorageNotice acknowledgement is recognized during migration. Neither cookie contains a user or device identifier.',
      },
      {
        title: 'Security and authenticated sessions',
        text: 'Host-only PVADE session, Designer session, and anti-forgery cookies protect authenticated requests. They are HttpOnly, unavailable to application JavaScript, and retained for the browser session or up to eight hours with sliding renewal.',
      },
      {
        title: 'Language and interface preferences',
        text: 'Local keys pvad_locale, editor-storage, pvad_export_format, and the PDF and Spreadsheet sidebar keys retain interface choices. Earlier PXA preference keys are read during migration. Preferences remain until changed, cleared, or removed during a tenant-state reset where applicable.',
      },
      {
        title: 'Temporary tab handoffs',
        text: 'The pvad.designer.auth-transaction session key completes authentication. An already started PXA sign-in may be consumed once using its original verification state. Document keys pvad_migration_designer_handoff and pvad_pdf_viewer_handoff bind temporary transfers to the current user and organization. Document transfers expire after five minutes and are consumed by the target flow. Earlier unassigned document transfers are not loaded.',
      },
      {
        title: 'Local document working copies',
        text: 'Workbook keys beginning pvad-spreadsheet:v1: keep working copies for a specific user and organization until replacement or site-data deletion. Earlier unassigned workbook drafts can be downloaded explicitly for recovery; they are not automatically loaded. The pvad-code-editor-draft-v2 key offers an old code draft for explicit import. Legacy unassigned drafts are removed at sign-out or organization switch. All working copies may contain customer-entered content.',
      },
      {
        title: 'Retention and control',
        text: 'Sign-out and organization switching clear transient and unassigned workflow state. Scoped workbook drafts remain for the same user and organization; remove these through browser site-data controls. PVADE does not use this storage for cross-site tracking.',
      },
      {
        title: 'No optional tracking at launch',
        text: 'PVADE does not load optional analytics or marketing storage. A consent center will be introduced before that changes.',
      },
    ],
    notice: 'The verified published database version and machine-readable storage inventory control.',
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
    text: 'Swiss law has no general withdrawal right for ordinary online purchases. Mandatory withdrawal rights in an enabled consumer market remain unaffected and are disclosed at checkout.',
    sections: [
      { title: 'Applicable right', text: 'The checkout and confirmation identify the period, method, consequences, and exceptions required at the consumer\'s habitual residence.' },
      { title: 'Digital performance', text: 'Where required, early digital performance is requested separately from acceptance of the Terms.' },
      { title: 'Model form', text: 'Consumers may use the published model form or any other unambiguous withdrawal statement where a right applies.' },
    ],
    notice: 'Paid B2C checkout remains disabled until the relevant market workflow and published version are approved.',
  },
  dpa: {
    kicker: 'Data Processing Agreement',
    title: 'Processing Customer Content on behalf of organizations',
    text: 'The DPA covers the Swiss FADP and, where applicable, GDPR, UK GDPR, and mandatory local data-protection requirements.',
    sections: [
      { title: 'Processing scope', text: 'The agreement defines roles, instructions, products, purposes, data categories, data subjects, duration, return, and deletion.' },
      { title: 'Security and assistance', text: 'PVADE commits to confidentiality, risk-appropriate measures, incident notice, rights assistance, compliance support, and controlled audits.' },
      { title: 'Subprocessors and transfers', text: 'Production providers, processing locations, notice, objections, and lawful international-transfer safeguards are deployment-specific.' },
    ],
    notice: 'The DPA, security measures, and subprocessor list require legal and operational approval.',
  },
};

const legalDocumentNavigation = [
  { kind: 'terms', label: 'Terms and Conditions', href: '/terms.html' },
  { kind: 'privacy', label: 'Privacy Notice', href: '/privacy.html' },
  { kind: 'cookie-storage', label: 'Cookie and Storage', href: '/cookie-storage.html' },
  { kind: 'imprint', label: 'Imprint', href: '/imprint.html' },
  { kind: 'withdrawal', label: 'Consumer Withdrawal', href: '/withdrawal.html' },
  { kind: 'dpa', label: 'Data Processing Agreement', href: '/dpa.html' },
  { kind: 'license', label: 'License Agreement', href: '/license.html' },
];

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
    text: 'Explore the product family, run demos, review documentation, and validate whether PVADE fits your generation or migration scenario.',
    features: ['Access demos and examples', 'Evaluate product areas', 'Review documentation and migration coverage'],
    cta: 'Start with demos',
    href: appendCampaignParams(`${siteLinks.account}register`),
  },
  {
    title: 'Team',
    bestFor: 'Product teams building document automation into applications.',
    text: 'Adopt PVADE across generator, designer, importer, viewer, and spreadsheet workflows with a shared implementation model.',
    features: ['Build repeatable document workflows', 'Connect design, generation, and review', 'Use product documentation as the team baseline'],
    cta: 'Read docs',
    href: siteLinks.documentation,
  },
  {
    title: 'Enterprise',
    bestFor: 'Organizations planning provider migrations or broad rollout.',
    text: 'Plan larger migration programs, provider parity work, support expectations, and rollout guidance with the PVADE team.',
    features: ['Migration assessment', 'Provider parity planning', 'Rollout and support path definition'],
    cta: 'Contact sales',
    href: companyPage('contact'),
  },
];

function renderCards(items) {
  return items
    .map(
      (item) => `
        <article class="pvad-product-card">
          <span class="pvad-product-card__label">${item.label}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <ul>
            ${item.points.map((point) => `<li>${point}</li>`).join('')}
          </ul>
          <div class="pvad-company-card-actions">
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
  return items.map((item) => `<span class="pvad-status pvad-status--preview">${item}</span>`).join('');
}

function renderUseCases(items) {
  return items
    .map(
      (item) => `
        <article class="pvad-card pvad-company-use-card">
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
        <article class="pvad-card pvad-company-about-card">
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
        <article class="pvad-card ${className}">
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
        <article class="pvad-card pvad-company-contact-card">
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
        <article class="pvad-card pvad-company-legal-section">
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
        <article class="pvad-card pvad-company-roadmap-card">
          <span class="pvad-status ${item.status === 'Ready to shape' ? 'pvad-status--ready' : 'pvad-status--planned'}">${item.status}</span>
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
        <article class="pvad-card pvad-company-showcase-card">
          <span class="pvad-status ${item.status === 'Ready' ? 'pvad-status--ready' : 'pvad-status--preview'}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.text}</p>
          <div class="pvad-company-card-actions">
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
        <article class="pvad-card pvad-company-pricing-card">
          <h3>${item.title}</h3>
          <strong>${item.bestFor}</strong>
          <p>${item.text}</p>
          <ul>
            ${item.features.map((feature) => `<li>${feature}</li>`).join('')}
          </ul>
          <a class="pvad-button ${item.title === 'Enterprise' ? 'pvad-button--primary' : 'pvad-button--secondary'}" href="${item.href}">${item.cta}</a>
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

const routeExists = Object.hasOwn(companyRoutes, window.location.pathname);
const currentRoute = routeExists ? companyRoutes[window.location.pathname] : {
  ...fallbackRoute,
  section: 'notFound',
  title: 'Page not found | PVADE',
  description: 'The requested PVADE page could not be found.',
};
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
    <header class="pvad-site-header">
      <div class="pvad-site-header__inner">
        <a class="pvad-brand" href="${siteLinks.company}" aria-label="PVADE Company home">
          <span class="pvad-brand__mark">${pvadBrand.displayName}</span>
          <span class="pvad-brand__name">${pvadBrand.displayName} <small>Company</small></span>
        </a>
        <nav class="pvad-site-nav" aria-label="Primary navigation">
          <a href="${siteLinks.company}"${activeAttr(null)}>Company</a>
          <a href="${companyPage('products')}"${activeAttr('products')}>Products</a>
          <a href="${siteLinks.documentation}">Documentation</a>
          <a href="${siteLinks.demo}">Demo</a>
          <a href="${companyPage('pricing')}"${activeAttr('pricing')}>Pricing</a>
          <a href="${companyPage('about')}"${activeAttr('about')}>About</a>
          <a href="${companyPage('support')}"${activeAttr('support')}>Support</a>
        </nav>
        <div class="pvad-header-actions">
          <a class="pvad-button pvad-button--secondary pvad-header-cta" href="${siteLinks.designer}">Live demo</a>
          ${isSignedIn()
            ? `<a class="pvad-header-account-link" href="${siteLinks.account}dashboard">My account</a>`
            : `<a class="pvad-header-account-link" href="${signInUrl()}">Sign in</a>
          <a class="pvad-header-account-link" href="${appendCampaignParams(`${siteLinks.account}register`)}">Register</a>`}
          <a class="pvad-button pvad-button--primary pvad-header-cta" href="${companyPage('contact')}"${activeAttr('contact')}>Contact sales</a>
        </div>
      </div>
    </header>
  `;
}

function renderHeroSection() {
  return `
    <section class="pvad-company-hero">
      <div class="pvad-container pvad-company-hero__grid">
        <div>
          <p class="pvad-kicker">PVADE for .NET teams</p>
          <h1 class="pvad-heading">Generate, migrate, and review business documents with one connected PVADE platform.</h1>
          <p class="pvad-lede">
            PVADE brings document generation, provider migration, file import, template design,
            PDF review, and spreadsheet-driven automation into a single product family for teams
            that cannot afford fragmented document workflows.
          </p>
          <div class="pvad-action-row">
            <a class="pvad-button pvad-button--primary" href="${appendCampaignParams(`${siteLinks.account}register`)}">Start free trial</a>
            <a class="pvad-button pvad-button--secondary" href="${siteLinks.demo}">View demos</a>
            <a class="pvad-button pvad-button--secondary" href="${siteLinks.documentation}">Read documentation</a>
          </div>
          <p class="pvad-company-trial-note">30-day Premium Trial, full product access, no credit card required.</p>
          <div class="pvad-company-proof-strip" aria-label="PVADE proof points">
            ${renderProof(proofPoints)}
          </div>
        </div>
        <aside class="pvad-card pvad-company-snapshot" aria-label="PVADE platform snapshot">
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
    <section class="pvad-section" id="products">
      <div class="pvad-container">
        <p class="pvad-kicker">Product suite</p>
        <h1 class="pvad-heading">Everything around the document lifecycle, grouped into focused PVADE products.</h1>
        <p class="pvad-lede">
          Start with the product area that matches your current problem, then connect it
          to the rest of the platform as your automation grows.
        </p>
        <div class="pvad-feature-grid pvad-company-grid">
          ${renderCards(products)}
        </div>
      </div>
    </section>
  `;
}

function renderProductDetailPage(product) {
  if (!product) return renderProductsPage();
  return `
    <section class="pvad-section" id="${product.slug}">
      <div class="pvad-container">
        <div class="pvad-company-product-detail">
          <div>
            <p class="pvad-kicker">${product.label}</p>
            <h1 class="pvad-heading">${product.title}</h1>
            <p class="pvad-lede">${product.detail}</p>
            <div class="pvad-action-row">
              <a class="pvad-button pvad-button--primary" href="${siteLinks.demo}#demo/${product.demo}">Open demo</a>
              <a class="pvad-button pvad-button--secondary" href="${siteLinks.documentation}#${product.docs}">Read docs</a>
            </div>
          </div>
          <aside class="pvad-card pvad-company-product-summary">
            <strong>Best fit</strong>
            <p>${product.audience}</p>
            <ul>
              ${product.points.map((point) => `<li>${point}</li>`).join('')}
            </ul>
          </aside>
        </div>
        <div class="pvad-company-product-content">
          <article class="pvad-card">
            <h2>Typical workflows</h2>
            <ul>
              ${product.workflows.map((item) => `<li>${item}</li>`).join('')}
            </ul>
          </article>
          <article class="pvad-card">
            <h2>Core capabilities</h2>
            <ul>
              ${product.capabilities.map((item) => `<li>${item}</li>`).join('')}
            </ul>
          </article>
          <article class="pvad-card">
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
    <section class="pvad-section pvad-section--soft" id="use-cases">
      <div class="pvad-container">
        <p class="pvad-kicker">Use cases</p>
        <h2 class="pvad-heading">From legacy provider migration to new document workflows</h2>
        <div class="pvad-company-use-grid">
          ${renderUseCases(useCases)}
        </div>
      </div>
    </section>
  `;
}

function renderTrustSection() {
  return `
    <section class="pvad-section" id="trust">
      <div class="pvad-container pvad-company-two-column">
        <div>
          <p class="pvad-kicker">Provider coverage</p>
          <h2 class="pvad-heading">Designed around real provider migration work</h2>
          <p class="pvad-lede">
            PVADE is shaped by concrete migration scenarios from known PDF, reporting,
            spreadsheet, and document libraries. Parity gaps become documented tasks,
            not hidden surprises.
          </p>
        </div>
        <div class="pvad-card">
          <strong>Tracked ecosystem</strong>
          <div class="pvad-company-badges">
            ${renderBadges(providers)}
          </div>
        </div>
      </div>
    </section>
  `;
}

function renderExamplesSection() {
  return `
    <section class="pvad-section pvad-section--soft" id="examples">
      <div class="pvad-container">
        <p class="pvad-kicker">Example reports</p>
        <h2 class="pvad-heading">Examples that make document automation testable</h2>
        <p class="pvad-lede">
          Each example is meant to connect a realistic business document, a runnable demo,
          and the documentation needed to implement or migrate the workflow.
        </p>
        <div class="pvad-company-showcase-grid">
          ${renderShowcases(showcases)}
        </div>
      </div>
    </section>
  `;
}

function renderRoadmapSection() {
  return `
    <section class="pvad-section pvad-section--soft" id="roadmap">
      <div class="pvad-container">
        <p class="pvad-kicker">Roadmap</p>
        <h2 class="pvad-heading">A product website connected to docs and demos</h2>
        <div class="pvad-company-roadmap">
          ${renderRoadmap(roadmap)}
        </div>
      </div>
    </section>
  `;
}

function renderPricingPage() {
  return `
    <section class="pvad-section" id="pricing">
      <div class="pvad-container">
        <div>
          <p class="pvad-kicker">Pricing</p>
          <h1 class="pvad-heading">Choose the adoption path that matches your document workload.</h1>
          <p class="pvad-lede">
            PVADE pricing is organized around how teams adopt document automation:
            evaluate the platform, standardize team workflows, or plan a larger migration.
            Final commercial terms can stay flexible while the product paths remain clear.
          </p>
        </div>
        <div class="pvad-company-pricing-grid">
          ${renderPricing(pricingTiers)}
        </div>
        <div class="pvad-card pvad-company-pricing-note">
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
    <section class="pvad-section pvad-section--soft" id="about">
      <div class="pvad-container pvad-company-two-column">
        <div>
          <p class="pvad-kicker">About</p>
          <h1 class="pvad-heading">PVADE is built for teams modernizing document-heavy software.</h1>
          <p class="pvad-lede">
            PVADE exists to make document automation less fragmented: one family for generating output,
            migrating legacy providers, importing files, reviewing PDFs, and connecting spreadsheet-backed
            workflows to product-ready examples.
          </p>
          <div class="pvad-action-row">
            <a class="pvad-button pvad-button--primary" href="${siteLinks.documentation}#overview">Read docs</a>
            <a class="pvad-button pvad-button--secondary" href="${siteLinks.demo}">Explore demos</a>
          </div>
        </div>
        <div class="pvad-company-about-stack">
          ${renderAboutPrinciples(aboutPrinciples)}
        </div>
      </div>
      <div class="pvad-container">
        <div class="pvad-company-story-grid">
          ${renderTextCards(aboutStory, 'pvad-company-story-card')}
        </div>
      </div>
    </section>
  `;
}

function renderSupportPage() {
  return `
    <section class="pvad-section pvad-section--soft" id="support">
      <div class="pvad-container">
        <div>
          <p class="pvad-kicker">Support</p>
          <h1 class="pvad-heading">Support paths for evaluation, migration, and implementation.</h1>
          <p class="pvad-lede">
            Start with public docs and runnable demos, then bring migration-specific questions,
            provider parity gaps, or rollout planning into a direct project conversation.
          </p>
        </div>
        <div class="pvad-company-support-grid">
          ${renderTextCards(supportPaths, 'pvad-company-support-card')}
        </div>
        <div class="pvad-card pvad-company-support-topics">
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
    <section class="pvad-section" id="contact">
      <div class="pvad-container pvad-company-contact">
        <p class="pvad-kicker">Contact</p>
        <h1 class="pvad-heading">Talk to the PVADE team about product fit, migration scope, or technical evaluation.</h1>
        <p class="pvad-lede">
          Use this page to route the conversation before a final form or CRM integration is selected.
          The goal is to understand what you are building, what you are replacing, and which PVADE products
          should be evaluated first.
        </p>
        <div class="pvad-company-contact-grid">
          ${renderContactCards(contactPaths)}
        </div>
        <div class="pvad-card pvad-company-contact-note">
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
    <section class="pvad-section" id="${kind}">
      <div class="pvad-container">
        <div class="pvad-company-legal">
          <p class="pvad-kicker">${page.kicker}</p>
          <h1 class="pvad-heading">${page.title}</h1>
          <p class="pvad-lede">${page.text}</p>
        </div>
        <div class="pvad-company-legal-layout">
          <aside class="pvad-company-legal-sidebar" aria-label="Legal document navigation">
            <h2>Legal documents</h2>
            <nav>
              ${legalDocumentNavigation.map((item) => `
                <a href="${item.href}"${item.kind === kind ? ' aria-current="page"' : ''}>${item.label}</a>
              `).join('')}
            </nav>
          </aside>
          <div class="pvad-company-legal-main">
            <section class="pvad-company-legal-metadata" data-legal-metadata aria-label="Document status">
              <p role="status">Loading verified publication details...</p>
            </section>
            <article class="pvad-company-legal-grid" data-legal-content>
              ${renderLegalSections(page.sections)}
            </article>
            <div class="pvad-company-legal-notice" role="note">
              <strong>${page.notice}</strong>
            </div>
            <section class="pvad-company-legal-history" aria-labelledby="legal-history-title">
              <div>
                <p class="pvad-kicker">Change transparency</p>
                <h2 id="legal-history-title">Version history</h2>
                <p>Published versions remain available as an immutable public record.</p>
              </div>
              <div data-legal-history aria-live="polite">
                <p>Loading published versions...</p>
              </div>
            </section>
          </div>
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

if (!routeExists) {
  document.querySelector('#app').innerHTML = renderStatusPage('notFound', {
    product: 'PVADE Company',
    homeHref: siteLinks.company,
    supportHref: companyPage('support'),
    secondaryHref: companyPage('products'),
    secondaryLabel: 'View products',
  });
  bindStatusPage();
} else {
  document.querySelector('#app').innerHTML = `
    <div class="pvad-site pvad-site--company">
      ${renderHeader()}
      <main class="pvad-site-main">
        ${renderMainContent()}
      </main>

      ${renderPvadFooter('PVADE Company')}
    </div>
  `;
}

function formatLegalDate(value, includeTime = false) {
  return new Intl.DateTimeFormat('en', {
    dateStyle: 'long',
    ...(includeTime ? { timeStyle: 'short' } : {}),
  }).format(new Date(value));
}

function appendLegalMetadata(container, label, value, code = false) {
  const group = document.createElement('div');
  const term = document.createElement('dt');
  const description = document.createElement('dd');
  term.textContent = label;
  if (code) {
    const codeElement = document.createElement('code');
    codeElement.textContent = value;
    description.append(codeElement);
  } else {
    description.textContent = value;
  }
  group.append(term, description);
  container.append(group);
}

function renderLegalMetadata(result, requestedVersion) {
  const container = document.querySelector('[data-legal-metadata]');
  if (!container) return;
  const legalDocument = result.document;
  const status = document.createElement('div');
  status.className = 'pvad-company-legal-publication-status';
  const badge = document.createElement('span');
  badge.className = `pvad-status ${result.source === 'live' ? 'pvad-status--ready' : 'pvad-status--planned'}`;
  badge.textContent = requestedVersion ? 'Archived publication' : result.source === 'live' ? 'Current publication' : 'Verified snapshot';
  const statusText = document.createElement('span');
  statusText.textContent = result.source === 'live'
    ? 'Loaded from the public Legal API.'
    : `API unavailable. Snapshot generated ${formatLegalDate(result.generatedAt, true)}${result.stale ? ' and older than 30 days.' : '.'}`;
  status.append(badge, statusText);

  const details = document.createElement('dl');
  appendLegalMetadata(details, 'Version', legalDocument.version);
  appendLegalMetadata(details, 'Effective', formatLegalDate(legalDocument.effectiveAt));
  appendLegalMetadata(details, 'Language', `${legalDocument.locale.toUpperCase()} · ${legalDocument.isAuthoritative ? 'Authoritative' : 'Convenience translation'}`);
  appendLegalMetadata(details, 'Content hash', legalDocument.contentHash, true);

  const summary = document.createElement('p');
  summary.className = 'pvad-company-legal-change-summary';
  const summaryLabel = document.createElement('strong');
  summaryLabel.textContent = 'Change summary: ';
  summary.append(summaryLabel, legalDocument.changeSummary || 'No public change summary was provided for this version.');
  container.replaceChildren(status, details, summary);
}

function renderLegalHistory(kind, history, selectedVersion) {
  const container = document.querySelector('[data-legal-history]');
  if (!container) return;
  const list = document.createElement('ol');
  list.className = 'pvad-company-legal-version-list';
  for (const version of history.versions) {
    const item = document.createElement('li');
    const heading = document.createElement('div');
    const link = document.createElement('a');
    const isCurrent = version.version === history.currentVersion;
    const isSelected = version.version === (selectedVersion || history.currentVersion);
    link.href = isCurrent
      ? window.location.pathname
      : `${window.location.pathname}?version=${encodeURIComponent(version.version)}`;
    link.textContent = `Version ${version.version}`;
    if (isSelected) link.setAttribute('aria-current', 'page');
    const badge = document.createElement('span');
    badge.className = `pvad-status ${isCurrent ? 'pvad-status--ready' : 'pvad-status--planned'}`;
    badge.textContent = isCurrent ? 'Current' : 'Archived';
    heading.append(link, badge);

    const effective = document.createElement('p');
    effective.textContent = `Effective ${formatLegalDate(version.effectiveAt)}`;
    const summary = document.createElement('p');
    summary.textContent = version.changeSummary || 'No public change summary was provided.';
    item.append(heading, effective, summary);
    list.append(item);
  }
  container.replaceChildren(list);
}

async function hydratePublishedLegalDocument(kind) {
  if (!legalPages[kind]) return;
  const requestedVersion = new URLSearchParams(window.location.search).get('version');
  try {
    const result = requestedVersion
      ? await loadPublishedLegalVersion({ kind, version: requestedVersion, locale: 'en' })
      : await loadPublishedLegalDocument({ kind, locale: 'en' });
    const legalDocument = result.document;
    const content = document.querySelector('[data-legal-content]');
    if (!content) return;
    content.className = 'pvad-company-legal-document';
    content.dataset.legalSource = result.source;
    content.innerHTML = legalDocument.renderedHtml;
    renderLegalMetadata(result, requestedVersion);
    const notice = document.querySelector('.pvad-company-legal-notice strong');
    if (notice) {
      notice.textContent = requestedVersion
        ? `You are reading an archived version. Version ${legalDocument.version} is preserved for reference and does not replace the current publication.`
        : result.source === 'live'
          ? 'This is the current verified publication.'
          : 'This verified snapshot remains readable during the API outage. Transactions requiring current-version verification remain disabled.';
    }

    try {
      const history = await loadPublishedLegalHistory({ kind, locale: 'en' });
      renderLegalHistory(kind, history, requestedVersion);
    } catch {
      const historyContainer = document.querySelector('[data-legal-history]');
      if (historyContainer)
        historyContainer.innerHTML = '<p>Version history is temporarily unavailable while the Legal API cannot be reached.</p>';
    }
  } catch {
    const content = document.querySelector('[data-legal-content]');
    if (content) {
      content.className = 'pvad-company-legal-document pvad-company-legal-document--unavailable';
      content.dataset.legalSource = 'unavailable';
      content.innerHTML = `
        <section role="status" aria-live="polite">
          <h2>Verified legal content is temporarily unavailable</h2>
          <p>Neither the Legal API nor its last-known-good published snapshot could be verified. Please try again later.</p>
        </section>
      `;
    }
    const notice = document.querySelector('.pvad-company-legal-notice strong');
    if (notice)
      notice.textContent = 'Registration and other transactions requiring current legal versions remain disabled.';
    const metadata = document.querySelector('[data-legal-metadata]');
    if (metadata)
      metadata.innerHTML = '<p role="alert">Publication details could not be verified.</p>';
    const history = document.querySelector('[data-legal-history]');
    if (history)
      history.innerHTML = '<p>Version history is temporarily unavailable.</p>';
  }
}

initializeStorageNotice();
hydratePublishedLegalDocument(currentRoute.section);
