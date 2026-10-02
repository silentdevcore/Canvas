import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { renderPxaFooter } from '../../shared/footer.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import pxaReleaseManifest from '../../../product-metadata/pxa-releases.json';
import designerFeatureManifest from '../../../product-metadata/designer-features.json';
import { initializeStorageNotice } from '../../shared/storageNotice.js';
import { bindStatusPage, renderStatusPage } from '../../shared/statusPages.ts';

initializeBrowserTelemetry({ application: 'documentation' });

const pxaReleases = pxaReleaseManifest.releases;
const designerFeatures = designerFeatureManifest.features;

const editorSections = [
  {
    title: 'Designer',
    status: 'Ready',
    text: 'Create and inspect PXA document templates in the visual designer, then preview output and review JSON.',
  },
  {
    title: 'Project Files',
    status: 'Ready',
    text: 'Save and reopen portable PXA PDF (.pxap) and Spreadsheet (.pxas) projects with editable content and validated image assets.',
  },
  {
    title: 'Templates',
    status: 'Preview',
    text: 'Understand pages, shared elements, bindings, repeats, and template structure before connecting data.',
  },
  {
    title: 'Page Layout',
    status: 'Ready',
    text: 'Mix portrait, landscape, and custom page sizes while keeping Canvas, Preview, PDF, image, Word, ODT, and HTML output aligned.',
  },
  {
    title: 'Elements',
    status: 'Preview',
    text: 'Use text, tables, charts, images, forms, shapes, and layout primitives consistently across templates.',
  },
  {
    title: 'Element Library',
    status: 'Ready',
    text: 'Find elements, synchronize personal favorites, and insert reusable blocks or complete page kits.',
  },
  {
    title: 'Localization',
    status: 'Ready',
    text: 'Configure active languages, localized properties, RTL text, language-scoped elements, and multi-language export behavior.',
  },
  {
    title: 'Data Binding',
    status: 'Ready',
    text: 'Connect elements to runtime data through bindings, expressions, visibility conditions, repeats, and localized property values.',
  },
  {
    title: 'PDF Viewer',
    status: 'Preview',
    text: 'Review PDFs, forms, annotations, and browser-side inspection workflows connected to demos.',
  },
  {
    title: 'PDF Trust',
    status: 'Ready',
    text: 'Generate, inspect, migrate, and troubleshoot signed, redacted, encrypted, archival, and accessible PDFs with explicit evidence and limitations.',
  },
  {
    title: 'PDF/A compliance',
    status: 'Preview',
    text: 'Generate, preflight, convert, and independently validate exact PDF/A profiles with documented limitations.',
  },
  {
    title: 'PDF/UA accessibility',
    status: 'Preview',
    text: 'Create tagged PDF/UA-1 output, remediate semantics in Designer, and complete human accessibility checks.',
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

const editorDocs = [
  {
    title: 'Designer',
    status: 'Ready',
    purpose:
      'Use PXA Designer as the visual workspace for creating, inspecting, and validating document templates before they are generated or migrated into application workflows.',
    whenToUse: [
      'You need to create or inspect a document template visually.',
      'You want to preview migrated report output before committing it to code.',
      'You need JSON inspection for handoff between design and implementation.',
    ],
    concepts: ['Template canvas', 'Pages and margins', 'Preview output', 'Design JSON', 'Migration handoff'],
    tasks: ['Open the live designer', 'Create or load a template', 'Preview output', 'Inspect JSON', 'Open a migrated report in the designer'],
    related: [
      { label: 'Live designer', href: siteLinks.designer },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Master-detail demo', href: `${siteLinks.demo}#demo/master-detail-report` },
    ],
  },
  {
    title: 'Project Files',
    status: 'Ready',
    purpose:
      'Use PXAP and PXAS when editable Designer work must be transferred, reviewed, archived as a working project, or reopened on another authorized PXA installation.',
    whenToUse: [
      'You want to continue editing a PDF Designer document later or on another device.',
      'You need to exchange a Spreadsheet Designer workbook without converting it to XLSX first.',
      'You need a validated portable package whose images do not depend on the source organization asset store.',
    ],
    concepts: ['PXA PDF Project (.pxap)', 'PXA Spreadsheet Project (.pxas)', 'Editable document JSON', 'Package-local assets', 'SHA-256 integrity', 'Schema compatibility'],
    tasks: ['Save a PDF project', 'Open a PDF project', 'Save a Spreadsheet project', 'Open a Spreadsheet project', 'Validate or inspect a package', 'Troubleshoot PXAPROJECT diagnostics'],
    related: [
      { label: 'Native format specification', href: '../../docs/native-designer-project-formats.md' },
      { label: 'Designer image assets', href: '../../docs/architecture/designer-assets.md' },
      { label: 'Open Designer', href: siteLinks.designer },
    ],
    examples: [
      {
        title: 'Choose the correct project type',
        json: '{"pdfDesigner":"report.pxap","spreadsheetDesigner":"forecast.pxas","finalOutputs":["report.pdf","forecast.xlsx"]}',
        note: 'PXAP and PXAS are editable project packages. They are deliberately incompatible with each other and are not final PDF or Spreadsheet delivery files.',
      },
      {
        title: 'Open safely',
        json: '1. Choose Open project in the matching Designer.\n2. Select a .pxap or .pxas file.\n3. PXA validates the package before changing the current document.\n4. Packaged images receive fresh organization-owned asset IDs.\n5. Use Undo to return to the previous document.',
        note: 'Passwords, authentication data, tenant asset IDs, external image URLs, executable content, and unverified archive entries are not accepted as portable project content.',
      },
    ],
  },
  {
    title: 'PDF/A compliance',
    status: 'Preview',
    purpose:
      'Use PDF/A when a document must remain self-contained and reproducible for long-term preservation. PXA documents the exact profile, generated evidence, conversion risks, and the boundary between first-party checks and independent conformance validation.',
    whenToUse: [
      'You are generating an archive copy with a defined PDF/A profile.',
      'You need to preflight or convert an existing PDF before archival storage.',
      'You must verify a PDF/A claim without treating metadata alone as proof.',
      'You need to understand why encryption, forms, annotations, or attachments block a selected profile.',
    ],
    concepts: ['PDF/A-2b', 'PDF/A-3b', 'Self-contained files', 'Embedded fonts and ToUnicode', 'ICC output intent', 'Preflight versus conversion', 'veraPDF evidence', 'Conversion loss and signature invalidation'],
    tasks: ['Generate PDF/A-2b', 'Generate PDF/A-3b with controlled attachments', 'Validate a PDF/A profile in the Viewer', 'Preflight an existing PDF', 'Convert an eligible existing PDF', 'Review independent validator reports', 'Correct PDF/A findings after migration'],
    related: [
      { label: 'PDF Viewer trust workflow', href: '#pdf-viewer' },
      { label: 'PDF Trust roadmap', href: '../../checklists/PXA.PDF-Trust-Foundation.md' },
      { label: 'Download external PDF/A evidence', href: '/downloads/pdf-trust/external/' },
      { label: 'PDF encryption documentation', href: '../../checklists/Pdf-Encryption.md' },
    ],
    examples: [
      {
        title: 'Choose a profile and validate independently',
        json: '{"profile":"PDF/A-2b","generator":"PXA.Generator","firstPartyEndpoint":"POST /api/pxa/v1/pdf/compliance/validate","independentEvidence":"tools/pdfa/validate-pdfa.sh --profile 2b archive.pdf"}',
        note: 'The Generator creates PDF/A-2b only when PdfA2B is explicitly selected and supports PDF/A-3b where attachments have valid relationships. Ordinary PDF output remains the default. The Viewer and API expose profile, status, validator, evidence, and diagnostics. A production conformance claim requires the pinned official veraPDF report and a recorded second independent validator result. PXA does not claim PDF/A-4 support.',
      },
      {
        title: 'Preflight before converting an existing PDF',
        json: '{"preflight":"POST /api/pxa/v1/pdf/compliance/preflight","convert":"POST /api/pxa/v1/pdf/compliance/convert","profile":"PdfA2b","allowSignatureInvalidation":false}',
        note: 'Preflight is read-only and returns ready, reviewRequired, or unsupported with stable diagnostics. Conversion is a full rewrite and can invalidate signatures; it is blocked for unsupported encryption, fonts, actions, interactive content, unsafe colour spaces, and other structures named in the report. The original source is never replaced on failure.',
      },
    ],
  },
  {
    title: 'PDF/UA accessibility',
    status: 'Preview',
    purpose:
      'Use PDF/UA guidance to create tagged, navigable documents for assistive technology. PXA carries semantic roles, reading order, language, alternatives, table associations, and accessible form metadata from Designer JSON into tagged PDF, while separating automated evidence from human accessibility review.',
    whenToUse: [
      'You need a tagged PDF/UA-1 document with a defined accessibility profile.',
      'You are remediating missing roles, alternatives, labels, language, or table headers in Designer.',
      'You are importing or migrating a document and need to review semantic structure.',
      'You must combine automated PDF/UA validation with keyboard and screen-reader checks.',
    ],
    concepts: ['PDF/UA-1', 'Structure tree and parent tree', 'Reading order and MCIDs', 'Role mapping and artifacts', 'Language and document title', 'Alt text and ActualText', 'Table header associations', 'Accessible form labels and focus order', 'Matterhorn Protocol review'],
    tasks: ['Set document title and language', 'Inspect and correct element roles and reading order', 'Add image and chart alternatives', 'Define table headers and form labels', 'Generate PDF/UA-1', 'Run accessibility preflight and veraPDF', 'Perform keyboard, reading-order, and screen-reader review', 'Review imported semantic loss'],
    related: [
      { label: 'PDF Viewer trust workflow', href: '#pdf-viewer' },
      { label: 'PDF Trust roadmap', href: '../../checklists/PXA.PDF-Trust-Foundation.md' },
      { label: 'Accessibility element reference', href: '#element-reference' },
      { label: 'Download external PDF/UA evidence', href: '/downloads/pdf-trust/external/' },
      { label: 'PDF/UA manual evidence template', href: '/downloads/pdf-trust/matterhorn-evidence.template.json' },
    ],
    examples: [
      {
        title: 'Generate and validate PDF/UA-1',
        json: '{"accessibilityProfile":"PdfUa1","title":"Accessible invoice","language":"en","claimInspection":"POST /api/pxa/v1/pdf/compliance/validate","independentValidation":"tools/pdfua/validate-pdfua.sh document.pdf"}',
        note: 'The Generator preflight fails closed with PXAPDFUA diagnostics, page references, and element IDs when required title, language, tagging, alternatives, table semantics, embedded fonts, or supported annotation semantics are missing. The compliance endpoint only detects the PDF/UA metadata claim and reports it as indeterminate; it is not a PDF/UA validator. Use the pinned veraPDF ua1 wrapper, follow the PDF/UA manual evidence template, and complete manual assistive-technology review. Passing automated validation is evidence for the tested profile, not a guarantee of usable accessibility.',
      },
      {
        title: 'Remediate before export',
        json: '{"role":"H1","readingOrder":1,"language":"en","alternateText":"Revenue trend chart","tableHeaderScope":"Column"}',
        note: 'Use the Designer Accessibility Inspector to correct semantics before exporting. Then test keyboard focus, logical reading order, form labels, table navigation, link names, and image or chart alternatives with assistive technology. Imported or migrated structure that cannot be proven is reported for review rather than silently marked accessible.',
      },
    ],
  },
  {
    title: 'Templates',
    status: 'Preview',
    purpose:
      'Templates define the reusable document model: page setup, shared content, bindings, variables, repeats, and validation expectations.',
    whenToUse: [
      'You are building reusable layouts for invoices, reports, statements, or receipts.',
      'You need to connect structured data to document output.',
      'You want shared headers, footers, or repeated sections across pages.',
    ],
    concepts: ['Page settings', 'Margins', 'Shared elements', 'Bindings', 'Repeats', 'Validation'],
    tasks: ['Define page size and margins', 'Add shared header or footer content', 'Bind fields to data', 'Validate required template structure'],
    related: [
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'Booking receipt demo', href: `${siteLinks.demo}#demo/booking-receipt` },
      { label: 'Open designer', href: siteLinks.designer },
    ],
  },
  {
    title: 'Page Layout',
    status: 'Ready',
    purpose:
      'Configure the physical layout of each page independently. Document defaults remain available for legacy pages, while explicit overrides support mixed orientation, custom dimensions, margins, backgrounds, and bleed in one document.',
    whenToUse: [
      'A report combines portrait detail pages with a landscape summary table.',
      'A cover or certificate requires a custom size or background.',
      'Imported PDF, DOCX, or ODT pages expose different physical layouts.',
    ],
    concepts: ['Document default', 'This page override', 'Effective layout', 'Mixed orientation', 'Bleed and trim box', 'Overflow diagnostics'],
    tasks: ['Change the active page layout', 'Apply one layout to every page', 'Reset an override', 'Duplicate or add a page with inherited geometry', 'Review clipping warnings', 'Export mixed layouts'],
    related: [
      { label: 'Templates', href: '#templates-details' },
      { label: 'Export', href: '#export-details' },
      { label: 'Open designer', href: siteLinks.designer },
    ],
    image: {
      src: '/images/designer/mixed-page-layout.png',
      alt: 'PXA Designer preview showing portrait, landscape, and custom-sized pages in one document',
      caption: 'Each page uses its own effective dimensions, orientation, margins, background, and bleed settings.',
    },
    examples: [
      {
        title: 'Portrait, landscape, and custom pages',
        json: `{
  "pageSettings": {
    "width": 595,
    "height": 842,
    "orientation": "portrait",
    "margins": { "top": 36, "right": 36, "bottom": 36, "left": 36 }
  },
  "pages": [
    { "id": "portrait", "elements": [] },
    { "id": "landscape", "layout": { "width": 842, "height": 595, "orientation": "landscape" }, "elements": [] },
    {
      "id": "custom",
      "layout": {
        "width": 420,
        "height": 420,
        "orientation": "portrait",
        "backgroundColor": "#f8fafc",
        "margins": { "top": 24, "right": 30, "bottom": 24, "left": 30 },
        "bleedSize": 9
      },
      "elements": []
    }
  ]
}`,
        note: 'Pages without layout inherit pageSettings. Width and height are canonical; orientation normalizes contradictory dimensions.',
      },
      {
        title: 'Designer workflow',
        json: `1. Select the page in the page strip.
2. Open Page Settings and edit Paper, Background, Margins, or Bleed.
3. Review the Override badge and any clipping warning.
4. Choose Apply to all pages to make this the document default, or Reset to document default to remove only this page override.
5. Preview and export; each page keeps its effective physical dimensions.`,
        note: 'Changing page geometry never scales or moves existing elements automatically. This makes clipping explicit and reversible.',
      },
    ],
  },
  {
    title: 'Elements',
    status: 'Preview',
    purpose:
      'Elements are the building blocks of a PXA template: text, images, tables, charts, shapes, lines, form controls, and layout primitives.',
    whenToUse: [
      'You need to place content precisely on a document page.',
      'You are mapping report items from another designer into PXA.',
      'You want consistent styling and layout behavior across generated output.',
    ],
    concepts: ['Text elements', 'Images', 'Tables', 'Charts', 'Shapes and lines', 'Forms', 'Absolute positioning'],
    tasks: ['Add and position elements', 'Style text and tables', 'Map charts from report designers', 'Use lines and shapes for report fidelity'],
    related: [
      { label: 'Element Reference', href: '#element-reference' },
      { label: 'Text element', href: '#text-block-element' },
      { label: 'Chart report demo', href: `${siteLinks.demo}#demo/chart-report` },
      { label: 'Designer product page', href: companyPage('products/designer') },
      { label: 'Report migration guide', href: '#report-designer-migration' },
    ],
  },
  {
    title: 'Element Library',
    status: 'Ready',
    purpose:
      'Use the Designer sidebar to find individual elements, keep personal favorites, and insert reusable content without rebuilding common document sections.',
    whenToUse: [
      'You repeatedly use the same text, field, table, chart, or layout elements.',
      'You want a prepared invoice, book, certificate, presentation, report, or form section.',
      'You need to add complete pages while preserving undo, autosave, bindings, localization, and RTL settings.',
    ],
    concepts: ['Elements mode', 'Content mode', 'Personal favorites', 'Blocks', 'Page kits', 'Safe insertion'],
    tasks: ['Search the element catalog', 'Add or remove favorites', 'Preview and insert a block', 'Insert a complete page kit', 'Review scaling warnings'],
    related: [
      { label: 'Element Reference', href: '#element-reference' },
      { label: 'Templates', href: '#templates-details' },
      { label: 'Open designer', href: siteLinks.designer },
    ],
  },
  {
    title: 'Localization',
    status: 'Ready',
    purpose:
      'Localization controls document language variants: active language tabs, localized property values, own-language content, RTL text direction, language-specific element positions, and single or multi-language export.',
    whenToUse: [
      'You need one template to produce multiple language versions.',
      'You need Arabic, Hebrew, Farsi, Urdu, or other RTL output.',
      'Some text, positions, or elements differ per language.',
    ],
    concepts: ['System language', 'Active languages', 'Localized properties', 'Global vs Own property scope', 'Element language scope', 'RTL direction', 'Multi-language ZIP export'],
    tasks: ['Enable active languages', 'Add localized properties', 'Use {{KEY}} placeholders in element content', 'Set language and textDirection on text elements', 'Export current language or all languages'],
    related: [
      { label: 'Multi-language checklist', href: '../../checklists/multi-languages.md' },
      { label: 'Open designer', href: siteLinks.designer },
      { label: 'Export docs', href: '#export-details' },
    ],
    examples: [
      {
        title: 'Localized properties',
        json: `{
  "pageSettings": {
    "systemLanguage": "de",
    "activeLanguages": ["de", "en", "ar"],
    "localizedProperties": [
      {
        "key": "SUBJECT",
        "scope": "global",
        "localizedValues": {
          "de": "Rechnung",
          "en": "Invoice",
          "ar": "فاتورة"
        }
      },
      {
        "key": "LEGAL_NOTE",
        "scope": "own",
        "ownerLanguage": "de",
        "localizedValues": { "de": "Pflichtangabe fuer Deutschland" }
      }
    ]
  }
}`,
        note: 'Global means the placeholder exists in all languages, with one value per language. Own means the placeholder exists only for its owner language.',
      },
      {
        title: 'Localized element content',
        json: `{
  "type": "text",
  "content": "{{SUBJECT}} #1001",
  "language": "ar",
  "textDirection": "rtl",
  "elementLanguage": "ar",
  "langOverrides": {
    "ar": { "x": 330, "y": 40, "rotation": 0 }
  }
}`,
        note: 'The preview/export resolver replaces {{SUBJECT}} for the active language and hides own-language elements in other language tabs.',
      },
    ],
  },
  {
    title: 'Data Binding',
    status: 'Ready',
    purpose:
      'Data binding connects templates to runtime payloads. Elements can read values through binding paths, calculate values through expressions, hide or show through visibleExpression, and repeat tables or regions over collections.',
    whenToUse: [
      'Your template should render customer, invoice, order, report, or dataset values.',
      'A field needs formatting or conditional visibility.',
      'Tables or report sections should repeat for each row in a dataset.',
    ],
    concepts: ['binding path', 'expression', 'visibleExpression', 'repeat', 'localized property resolver', 'field and form binding', 'formatter locale'],
    tasks: ['Set binding on an element', 'Use expression for calculated content', 'Use visibleExpression for conditional rendering', 'Configure repeat for data-driven rows', 'Preview with representative data'],
    related: [
      { label: 'Templates docs', href: '#templates-details' },
      { label: 'Element Reference', href: '#element-reference' },
      { label: 'Generator docs', href: '#pdf-sdk' },
    ],
    examples: [
      {
        title: 'Bound text and conditional visibility',
        json: `{
  "type": "text",
  "content": "Customer: {{customer.name}}",
  "binding": "customer.name",
  "expression": "customer.name",
  "visibleExpression": "customer.active == true",
  "style": { "fontSize": 14, "fontWeight": "bold" }
}`,
        note: 'Use binding for the primary data path, expression for calculated values, and visibleExpression when output depends on runtime data.',
      },
      {
        title: 'Repeated table rows',
        json: `{
  "type": "table",
  "binding": "invoice.items",
  "repeat": {
    "dataPath": "invoice.items",
    "rowTemplate": ["{{name}}", "{{quantity}}", "{{total}}"]
  },
  "cellData": [["Item", "Qty", "Total"]],
  "headerRow": true
}`,
        note: 'Repeat configuration turns a single table template into data-driven rows while keeping header/style metadata on the element.',
      },
    ],
  },
  {
    title: 'PDF Viewer',
    status: 'Preview',
    purpose:
      'The PDF Viewer supports browser-side review workflows for generated or imported PDFs, including forms, annotations, signature validation, timestamps, certificate trust, and explicit revocation results.',
    whenToUse: [
      'You need to review generated PDF output before release.',
      'You want to inspect forms and annotations in a browser workflow.',
      'You need to distinguish cryptographic integrity from signer trust and revocation evidence.',
      'You are tracking viewer parity against established PDF viewer products.',
    ],
    concepts: ['PDF preview', 'Forms', 'Annotations', 'PAdES B-B, B-T, B-LT, and B-LTA', 'PDF/A-2b', 'Document timestamps', 'Certificate trust', 'Revocation policy', 'Review tools', 'Viewer parity'],
    tasks: ['Open a generated PDF preview', 'Inspect form fields', 'Generate and validate PDF/A-2b', 'Create or validate long-term signatures', 'Add an independent document timestamp', 'Interpret trust and revocation separately', 'Review annotation workflows', 'Track viewer feature gaps'],
    related: [
      { label: 'PDF Viewer product page', href: companyPage('products/pdf-viewer') },
      { label: 'Viewer demo', href: `${siteLinks.demo}#demo/pdf-viewer-annotations-forms` },
      { label: 'Feature gaps checklist', href: '../../checklists/PdfTools-WebViewer-Feature-Gaps.md' },
      { label: 'PDF Trust roadmap', href: '../../checklists/PXA.PDF-Trust-Foundation.md' },
      { label: 'Download synthetic PDF Trust corpus', href: '/downloads/pdf-trust/' },
      { label: 'Download independently validated PAdES corpus', href: '/downloads/pdf-trust/pades/' },
      { label: 'Download PAdES negative trust corpus', href: '/downloads/pdf-trust/pades/negative/' },
      { label: 'Download external producer corpus', href: '/downloads/pdf-trust/external/' },
    ],
    examples: [
      {
        title: 'Generate PDF/A-2b',
        json: `var document = Pdf.CreateDocument();
document.FontLoader = new PdfFontLoader("fonts");
document.Info.Title = "Archive copy";
document.AddPage().DrawText("Archived content", 40, 800, new PdfDrawTextOptions {
  Language = "en"
});

document.Save("archive.pdf", new PdfSaveOptions {
  ComplianceProfile = PdfComplianceProfile.PdfA2B
});`,
        note: 'PDF/A-2b output uses PDF 1.7, catalog-referenced XMP identification, an embedded RGB ICC output intent, ToUnicode mappings, and embedded fonts. Generation fails closed for encryption, unembedded text fonts, interactive form fields, and review annotations. Arbitrary attachments belong to a later PDF/A-3 workflow and are not accepted by this PDF/A-2b generator. Existing PDFs use the separate preflight and conversion workflow below.',
      },
      {
        title: 'Validate a PDF/A claim',
        json: `POST /api/pxa/v1/pdf/compliance/validate
Content-Type: multipart/form-data

file=@archive.pdf`,
        note: 'The Viewer and API report the exact profile, status, validator name, validation time, evidence, and stable PXAPDFTRUST rule diagnostics. A metadata string outside the catalog is only an unverified claim. The current PXA validator is structural. Run tools/pdfa/validate-pdfa.sh with profile 2b or 3b to retain a machine-readable report from the pinned official veraPDF container. A recorded pass from a second standards-based or commercial validator, such as Adobe Acrobat Preflight, is still required before making a production conformance claim; merely opening a PDF in a reader is not validation evidence.',
      },
      {
        title: 'Preflight and convert an existing PDF',
        json: `POST /api/pxa/v1/pdf/compliance/preflight
Content-Type: multipart/form-data

file=@source.pdf

POST /api/pxa/v1/pdf/compliance/convert
Content-Type: multipart/form-data

file=@source.pdf
allowSignatureInvalidation=false`,
        note: 'Preflight returns ready, reviewRequired, or unsupported without changing the source. The converter preserves visible PDF objects and replaces PDF/A identification metadata and the RGB output intent. It blocks encryption, unembedded fonts, unsupported color spaces, interactive forms, PDF/A-2 attachments, JavaScript and additional actions, forbidden Launch or multimedia actions, hidden or non-printing annotations, external stream dependencies, non-default transfer functions, and metadata that cannot be synchronized safely. URI, GoTo, and GoToR links are not rejected merely because they reference a destination. PDF/A-3 retains embedded files only when PXA can establish valid file relationships and associations. Findings include stable diagnostics and affected PDF object numbers. A source with invisible signature fields is reviewRequired because the required full rewrite invalidates and removes those signatures; conversion proceeds only after explicit approval. Visible signature appearances remain unsupported until they can be flattened without visual loss. A blocked or failed conversion returns no PDF and the original remains unchanged. PXA does not yet claim an exhaustive ISO rule implementation for every annotation appearance, optional-content, transparency-group, soft-mask, blend-space, and rendering-intent combination.',
      },
      {
        title: 'Inspect through a tenant-scoped job',
        json: `POST /api/pxa/v1/pdf/trust/inspect/jobs
Content-Type: multipart/form-data

file=@source.pdf

GET /api/pxa/v1/jobs/{jobId}
GET /api/pxa/v1/jobs/{jobId}/result
POST /api/pxa/v1/jobs/{jobId}/cancel`,
        note: 'The Viewer uses this asynchronous path. The upload, job, report, and immutable audit correlation are tenant-scoped. Inspection content expires after one hour by default and transient input and result objects are purged after the first successful result download. Cancelling the Viewer request also cancels queued work. The protected synchronous /api/pxa/v1/pdf/trust/inspect endpoint remains available for compatibility.',
      },
      {
        title: 'On-Premise trust policy',
        json: `{
  "PdfTrust": {
    "RevocationEvidence": {
      "Enabled": false,
      "AllowedHosts": ["pki.example.com"],
      "CrlPaths": ["App_Data/trust/company-root.crl"],
      "OcspResponsePaths": ["App_Data/trust/signer.ocsp"],
      "CacheDirectory": "App_Data/pdf-trust/revocation-cache",
      "MaximumResponseBytes": 4194304,
      "NetworkTimeoutSeconds": 10,
      "MaximumOcspAgeHours": 24
    },
    "ValidationPolicy": {
      "UseSystemTrustStore": true,
      "CustomTrustAnchorPaths": ["App_Data/trust/company-root.pem"],
      "IntermediateCertificatePaths": ["App_Data/trust/company-intermediates.pem"],
      "RevocationMode": "Offline",
      "RevocationFlag": "ExcludeRoot",
      "AllowCertificateDownloads": false,
      "NetworkTimeoutSeconds": 10
    }
  }
}`,
        note: 'Custom anchors are server configuration, never request input. Supplied CRLs and OCSP responses are server-owned configuration as well. NoCheck is the default. Offline uses configured or cached evidence. Online retrieval is disabled by default and accepts only certificate-declared HTTPS CRL and OCSP endpoints whose exact host is allowlisted. AIA issuer certificates use the same exact-host HTTPS policy. Redirects are rejected, responses are bounded, signatures, certificate binding, responder authorization, and validity windows are verified, and cache bytes are protected with ASP.NET Core Data Protection. Operating-system certificate downloads remain disabled; PXA resolves AIA intermediates only through this controlled provider.',
      },
      {
        title: 'Interpret validation results',
        json: `{
  "integrityStatus": 0,
  "certificateStatus": 0,
  "revocationStatus": 2,
  "padesProfile": "B-LTA",
  "timestampStatus": 0,
  "timestampRevocationStatus": 2,
  "historicalRevocationStatus": 0,
  "timestampHistoricalRevocationStatus": 0,
  "isDocumentTimestamp": false,
  "archiveTimestampStatus": 0,
  "archiveTimestampTime": "2026-08-28T08:05:00Z",
  "archiveTimestampAuthoritySubject": "CN=PXA Example TSA",
  "revocationEvidence": [{
    "kind": 0,
    "status": 0,
    "source": "cache:pki.example.com",
    "thisUpdate": "2026-08-28T08:00:00Z",
    "nextUpdate": "2026-08-29T08:00:00Z"
  }],
  "embeddedValidationMaterial": {
    "certificateCount": 3,
    "crlCount": 1,
    "ocspResponseCount": 2,
    "vriEntryCount": 1,
    "status": 0
  },
  "isCertification": true,
  "certificationStatus": 1,
  "certification": {
    "level": 2,
    "status": 1,
    "fieldLockStatus": 4,
    "disallowedChangeCount": 1,
    "disallowedChanges": [
      "Object 4 0 R: an object unrelated to form filling or signing was modified."
    ],
    "lockedFields": []
  }
}`,
        note: 'Status values are valid=0, invalid=1, indeterminate=2, unsupported=3, and notApplicable=4. Evidence kind 0 is CRL and 1 is OCSP. Valid integrity proves that signed bytes were not changed. It does not by itself prove signer identity or non-revocation. Indeterminate revocation must never be presented as valid. PXA rejects malformed or overlapping byte ranges, signature gaps that do not match /Contents, and non-whitespace bytes after the final complete revision. It also rejects later revisions that redefine a signature object, remove it from the current AcroForm tree, bind one signature to multiple fields, or wrap an older signature in a newly introduced field. PXA reports or creates B-LT only when a valid VRI key binds to the exact CMS signature value, the RFC 3161 signature timestamp is trusted, and embedded evidence validates every non-root signer and TSA certificate at that trusted time. B-LTA additionally requires a later trusted RFC 3161 document timestamp covering the B-LT revision. Independent document timestamps are created through POST /api/pxa/v1/pdf/signatures/timestamp and appear as separate DocTimeStamp entries in the Viewer. Long-term signing uses the server trust policy and fails closed when complete evidence is unavailable. A DocMDP certification signature must cover the first signed revision and must be the signature referenced by the catalog /Perms entry. PXA then enforces the certified permission level against every later incremental revision: level 1 allows no changes, level 2 allows form filling and additional signatures, and level 3 additionally allows annotations. Malformed, duplicate, mismatched, or late DocMDP declarations invalidate certification rather than producing a warning-only result. Document timestamps and DSS long-term validation material stay permitted at every level so that archival extension does not break certification. FieldMDP locks are enforced separately, so filling a locked field is reported as a certification breach even when DocMDP would allow form filling. PXA resolves objects the way a conforming reader does: it reads the trailer /XRefStm of hybrid-reference files, reports when the classic table and that stream disagree, and reports object definitions that no cross-reference revision references instead of treating appended bytes as document content. Parse limits bound nesting, collection sizes, object counts, decoded stream bytes, CCITT dimensions, and decompression ratio, and any document that trips a limit or contains unreferenced definitions is reported with an invalid structure rather than a valid one. Saving is an explicit choice between two strategies. An incremental update appends a revision and preserves the original bytes, which is what keeps existing signatures valid. A full rewrite emits a single revision containing the objects reachable from the trailer and removes orphaned payloads, which is the required strategy whenever earlier revisions or detached sensitive data must not survive; it renumbers objects deterministically, rebuilds the cross-reference table and /Size, derives /ID from the written body so the same input produces identical bytes, and refuses to run on a signed document unless the caller accepts that it invalidates those signatures. Both strategies reparse and validate their output before returning it. Redaction preflight is separate from applying a redaction. POST /api/pxa/v1/pdf/redactions/preflight never changes the uploaded document; it reports per region whether covered content is removable, reviewRequired, or unsupported, and it names what PXA cannot account for: pages whose content the importer did not model, partially covered text runs, any covered image whose reusable pixels cannot yet be purged, overlapping annotations and form field widgets whose values are not interpreted content, optional content, and recognized document-level carriers such as metadata, attachments, catalog actions, and earlier revisions. Counts and measurements are reported as evidence; covered text is never echoed back. A preflight result is not a redaction, and a removable result is not a claim that a document has been redacted. POST /api/pxa/v1/pdf/redactions/apply performs the redaction and requires its own permission, separate from preflight. It refuses when preflight reports content that cannot be confidently removed: sanitization resolves the recognized carriers it explicitly reports, while geometric ambiguity, images, layers, and unsupported structures block. Apply is a bounded edit, not a regeneration: only pages carrying a region are reserialized, reachable unaffected objects are preserved, unreachable payloads are pruned, and the result is saved as a full rewrite so no earlier revision retains the removed content. The result is then reparsed and verified by re-running region intersection and re-extracting text across the whole document, and a failed verification returns an error instead of the file. Each apply produces an audit record carrying the source and result hashes, the regions, the actor, the reason, the sanitizations applied, and the verification outcome, hashed in its canonical form so later change is detectable; the removed content never appears in it. The Viewer uses the same gated path and maps marks through each page CropBox and rotation: POST /api/pdf-viewer/annotations/redact/preflight reports coverage for the current marks, and POST /api/pdf-viewer/annotations/redact fails closed with 409 and the preflight report rather than returning a file it cannot stand behind. The audit record is returned in the PXA-Redaction-Audit and PXA-Redaction-Result-Sha256 response headers rather than written into the redacted document, because the reason and the actor would otherwise be disclosed to everyone who receives a file that was redacted to limit disclosure. New encrypted output from PXA.Pdf, the Designer, and the WebApi defaults to the PDF 2.0 Standard Security Handler at revision 6, using /V 5 /R 6, selected AESV3 crypt filters, and AES-256-CBC for strings and streams. RC4-128 remains available only through the explicit Rc4_128 legacy option; AES-128 is available as an interoperability bridge for older readers. X.509 recipient encryption uses Adobe.PubSec with the AESV2 public-key profile, and the matching private key is required to open the document. The typed existing-PDF reader opens legacy RC4 and AES-128 documents, then validates /StrF, /StmF, and /EFF, rejects unsupported explicit /Crypt configurations, decrypts object streams before expanding compressed objects, and leaves cross-reference streams unencrypted. Either a user or owner password can authenticate. A /Perms integrity mismatch rejects the document instead of returning untrusted rights, and restricted permissions require a distinct owner password. A wrong password is rejected without indicating which password check was closer. Revision-6 passwords use the RFC 4013 SASLprep mappings, Unicode 3.2 prohibited and unassigned tables, NFKC normalization, bidirectional checks, UTF-8 encoding, and the 127-byte limit in both writer and reader. The Designer evaluates password-strength guidance locally and does not transmit or log the entered value. Password-protected existing PDFs can be changed through a full rewrite when the owner password and replacement encryption settings are supplied; decrypted objects remain in memory, the old encryption dictionary is removed, and one fresh encrypted revision is emitted with a new file key. PXA refuses missing output encryption rather than returning plaintext, and encrypted incremental updates remain unsupported. Encrypting a document that already contains signatures is refused because the required signature-value exemption is not implemented. All cryptography comes from the platform, transient key material is cleared where runtime APIs permit, and passwords never appear in a report, diagnostic, or audit record. Archival conversion targets PDF/A-2b by default and accepts PDF/A-3b through the profile field on the compliance preflight and convert endpoints. PDF/A-3b generation is available for new documents, while existing-PDF conversion retains arbitrary embedded source files and attachments and gives each file specification the /AFRelationship and catalog /AF association the standard requires, while PDF/A-2b continues to block embedded files because that part does not permit them. Device-dependent colour in page and form content is converted to sRGB only for supported deterministic paths: DeviceCMYK, Indexed, and Separation or DeviceN with a supported tint transform and alternate space. Sampled functions use multilinear interpolation, and unknown calculator operators fail closed. The same deterministic paths convert bounded 8-bit samples in losslessly decodable image XObjects to DeviceRGB. Stencil and soft masks are preserved; JPEG, JPX, colour-key masks, oversized images, unsupported bit depths, and image colour spaces without a complete conversion path fail closed. Colored tiling Pattern streams are traversed and their deterministic colours are converted while the Pattern selection remains intact. Missing, malformed, uncolored, and shading patterns remain blocked because flattening or remapping them could change appearance or semantics. ICCBased without an explicit supported alternate, CalGray, CalRGB, Lab, unsafe inline images, shading resources, and unknown named spaces remain blocked. Only affected colour operators, image streams, and colored tiling pattern streams are rewritten, so unrelated content is not disturbed. The DeviceCMYK conversion is the one specified in ISO 32000-1 8.6.4.4: reproducible, not colorimetrically exact. PDF/A-4 is not offered, because it requires a mature PDF 2.0 writer and an independently corroborated validator policy; claiming it from identification metadata alone is what the trust rules forbid. Redaction preflight narrows coverage by the clipping paths in force, including those inherited from enclosing groups, so clipped content is not reported as covered where it never paints. It reports soft and stencil masks on images, soft masks set in the graphics state, pattern resources, and page or form transparency groups, because none of those can be resolved to a per-region answer without evaluating pixels. Apply sanitizes the carriers of covered content that are not page content: actions and additional actions wherever they occur, form field values on every field rather than only overlapping widgets, alternate and accessible descriptions, optional content, non-normal appearance streams such as down and rollover states, and page thumbnails. Covered image pixels are still not removed, so a document whose region covers an image remains blocked rather than partially redacted. Verification runs each recovery route as its own named check: residual covered elements and images, retained thumbnails, retained optional content, retained non-normal appearance streams, more than one revision, re-extracted text across the whole document, and a scan of the result bytes and every decodable stream for the removed text. That byte scan can miss residue inside a stream it cannot decode, but it never reports residue that is not there. The audit record is signed with a detached CMS signature over its canonical hash, using a dedicated audit key configured per deployment and deliberately separate from any document signing key, so compromising one cannot forge the other. When no audit key is configured the record keeps its hash and the response reports PXA-Redaction-Audit-Signed as false, because a hash detects change but does not prove origin. The Viewer presents the same three steps separately. Preflight reports coverage and findings without changing the document, apply stays disabled until preflight reports the marks as removable, and a refusal is shown as the structured report naming the content that blocked it rather than as a message string. After a successful apply the Viewer shows the verification outcome and whether the audit record was signed. The workflow is localized in all six Designer languages.',
      },
      {
        title: 'Understand archival color conversion boundaries',
        json: `{
  "targetProfile": "PdfA2b",
  "colorPolicy": "sRGB",
  "iccEngine": "LittleCMS2",
  "unsupported": [
    "DeviceCMYK mesh shading",
    "Separation mesh shading",
    "DeviceN mesh shading"
  ]
}`,
        note: 'PXA converts valid CalGray, CalRGB, and Lab colors from their complete calibration parameters. Embedded Gray, RGB, and CMYK ICC source profiles use LittleCMS and fail closed when the profile or native runtime is unavailable. Colored tiling Pattern streams are traversed; uncolored tiling patterns retain their identity while the base color space and every scn or SCN use are remapped to DeviceRGB. DeviceGray, DeviceRGB, calibrated, and embedded-ICC shadings are preserved. DeviceCMYK, Separation, DeviceN, mesh, missing, and malformed shadings remain blocked until interpolation and sample domains can be transformed without visual loss. Untagged DeviceCMYK uses the documented ISO 32000-1 approximation and is reproducible, not colorimetrically exact, because a destination output intent cannot reconstruct a missing source profile.',
      },
    ],
  },
  {
    title: 'PDF Trust',
    status: 'Ready',
    purpose:
      'Use the PDF Trust workflows when document integrity, signer trust, irreversible redaction, AES encryption, archival conformance, or accessibility evidence matters. PXA reports exact profiles and explicit valid, invalid, indeterminate, unsupported, or not-applicable outcomes instead of a generic secure or compliant label.',
    whenToUse: [
      'You need to create or validate PAdES B-B, B-T, B-LT, or B-LTA signatures.',
      'Sensitive content must be removed rather than covered with a visual rectangle.',
      'A generated or existing PDF requires AES-256 password protection and permissions.',
      'You need PDF/A-2b, PDF/A-3b, or PDF/UA-1 output with retained validation evidence.',
      'A provider migration contains security, signature, redaction, archival, or accessibility settings.',
    ],
    concepts: ['Generator trust settings', 'Viewer Trust panel', 'Migration diagnostics', 'Trust API', 'PAdES profiles', 'Applied redaction', 'AES-256 revision 6', 'PDF/A-2b and PDF/A-3b', 'PDF/UA-1', 'Fail-closed operations'],
    tasks: ['Generate trusted output', 'Inspect in the Viewer', 'Apply and verify a redaction', 'Validate signatures and revocation', 'Migrate provider settings', 'Call the Trust API', 'Resolve a diagnostic'],
    related: [
      { label: 'PDF Viewer workflow', href: '#pdf-viewer-details' },
      { label: 'PDF/A compliance', href: '#pdf-a-compliance-details' },
      { label: 'PDF/UA accessibility', href: '#pdf-ua-accessibility-details' },
      { label: 'Generator examples', href: '../../docs/guides/PXA.Pdf-Trust-Generator-Examples.md' },
      { label: 'Migration examples', href: '../../docs/guides/PXA.Pdf-Trust-Migration-Examples.md' },
      { label: 'Existing PDF boundaries', href: '../../docs/architecture/existing-pdf-boundaries.md' },
      { label: 'Download synthetic evidence', href: '/downloads/pdf-trust/' },
    ],
    images: [
      {
        src: '/images/pdf-trust/viewer-trust-desktop.png',
        alt: 'PXA PDF Viewer Trust panel showing a synthetic document inspection result',
        caption: 'Trust inspection reports structure, encryption, signatures, PDF/A, PDF/UA, revisions, and diagnostics separately.',
      },
      {
        src: '/images/pdf-trust/viewer-signature-desktop.png',
        alt: 'PXA PDF Viewer after creating a synthetic PAdES signature',
        caption: 'Signing reports the operation result without treating the visible appearance as validation evidence.',
      },
      {
        src: '/images/pdf-trust/viewer-compliance-desktop.png',
        alt: 'PXA PDF Viewer after PDF/A preflight and conversion of a synthetic document',
        caption: 'Compliance preflight and conversion remain distinct operations with an explicit target profile.',
      },
      {
        src: '/images/pdf-trust/viewer-redaction-review-desktop.png',
        alt: 'PXA PDF Viewer comparing an original synthetic PDF with its verified redacted result',
        caption: 'Applied redaction exposes preflight, verification, audit status, and original-versus-result comparison.',
      },
    ],
    examples: [
      {
        title: 'Generator path',
        json: `var document = Pdf.CreateDocument();
document.Info.Title = "Trusted example";
document.AddPage().DrawText("Approved content", 48, 760);
document.Save("trusted.pdf", new PdfSaveOptions {
  Encryption = PdfEncryptionOptions.Aes256(
    userPassword: "reader-password",
    ownerPassword: "owner-password",
    permissions: PdfPermissions.Print)
});`,
        note: 'New encrypted output uses the PDF 2.0 Standard Security Handler with AES-256 revision 6. Password encryption is incompatible with PDF/A. Signing, PDF/A, and PDF/UA use their dedicated typed options and preflight rules; PXA refuses contradictory settings instead of silently weakening them.',
      },
      {
        title: 'Viewer path',
        json: `1. Open the PDF Viewer and choose PDF Trust.
2. Run Inspect trust before changing the document.
3. Review integrity, certificate trust, revocation, profile, and diagnostics separately.
4. Run redaction preflight before Apply.
5. Compare the verified result with the preserved original and download the evidence.`,
        note: 'A redaction mark is only an instruction. Applied redaction uses a full rewrite, sanitizes supported carriers, reparses the output, verifies the covered regions, and blocks delivery if verification fails. Signature appearance is not evidence of cryptographic integrity or certificate trust.',
      },
      {
        title: 'Migration path',
        json: `{
  "sourceProvider": "Nutrient",
  "target": "PXA",
  "securityPolicy": "review",
  "expectedDiagnostics": [
    "unsupported provider-only trust-store behavior",
    "signature-invalidating full rewrite",
    "manual accessibility review required"
  ]
}`,
        note: 'Migration maps only behavior that PXA can prove. Provider-specific trust stores, remote signing services, unsupported encryption modes, or inferred accessibility remain review diagnostics. Automatic conversion never upgrades a visual mark or metadata claim into a security or conformance claim.',
      },
      {
        title: 'API path',
        json: `POST /api/pxa/v1/pdf/trust/inspect/jobs
POST /api/pxa/v1/pdf/signatures/sign
POST /api/pxa/v1/pdf/signatures/timestamp
POST /api/pxa/v1/pdf/redactions/preflight
POST /api/pxa/v1/pdf/redactions/apply
POST /api/pxa/v1/pdf/compliance/preflight
POST /api/pxa/v1/pdf/compliance/convert
POST /api/pxa/v1/pdf/compliance/validate`,
        note: 'All mutation endpoints require an authenticated tenant, the corresponding permission, entitlement and feature gate. Trust policy, anchors, revocation sources, TSA credentials, audit keys, and private keys remain server-owned. Responses use stable PXAPDFTRUST diagnostics and never echo passwords, keys, removed text, or document contents.',
      },
      {
        title: 'Troubleshooting path',
        json: `invalid       Evidence proves the requested property failed.
indeterminate  Available evidence cannot establish the result.
unsupported    PXA cannot safely process the encountered structure.
notApplicable  The property does not apply to this document.`,
        note: 'Keep the original file and the complete diagnostic report. Confirm the exact profile and validator version, check whether a signed file would require a full rewrite, verify fonts and color spaces for PDF/A, repair semantic roles for PDF/UA, and retry only after the blocking diagnostic is resolved. Never convert indeterminate or unsupported into success at the UI boundary.',
      },
    ],
  },
  {
    title: 'Electronic invoicing',
    status: 'Preview',
    purpose:
      'Produce, embed, read and validate Factur-X, ZUGFeRD and XRechnung invoice documents. PXA reports structural recognition, PXA business validation and official conformance as three separate states and never presents one as another. Peppol transport, national tax submission, accounting, payment and legal retention are external integrations, not PDF-engine capabilities.',
    whenToUse: [
      'You must issue a hybrid invoice whose visible page and embedded XML carry the same values.',
      'An existing PDF has to carry invoice XML without its visible pages changing.',
      'You need to read an invoice back out of a PDF byte for byte.',
      'You have to prove later what a validator said about a specific document, and with which artifact set.',
      'A provider migration contains invoice, PDF/A-3 attachment or invoice-validation calls.',
    ],
    concepts: ['Three validation states', 'Structural recognition', 'Official artifact sets', 'PDF/A-3 hybrid invoice', 'Incremental attachment', 'PDF and XML consistency', 'Retained validation evidence', 'Unknown XML preservation', 'Canonical invoice values'],
    tasks: ['Generate invoice XML', 'Create a hybrid PDF', 'Attach to an existing PDF', 'Inspect and extract', 'Validate and revalidate', 'Bind an invoice in the Designer', 'Migrate provider invoice calls'],
    related: [
      { label: 'Electronic invoicing guide', href: '../../docs/guides/PXA.EInvoicing.md' },
      { label: 'PDF Trust workflow', href: '#pdf-trust-details' },
      { label: 'PDF/A compliance', href: '#pdf-a-compliance-details' },
      { label: 'Provider coverage', href: '../../docs/guides/PXA.Unified-Migration-Provider-Coverage.md' },
    ],
    examples: [
      {
        title: 'Supported versions and profiles',
        json: `Factur-X   1.09.2  CII                       MINIMUM, BASIC WL, BASIC, EN 16931, EXTENDED, EXTENDED-CTC-FR  factur-x.xml
ZUGFeRD    2.5.2   CII                       MINIMUM, BASIC WL, BASIC, EN 16931, EXTENDED                     factur-x.xml
XRechnung  3.0.2   UBL Invoice, CreditNote   XRECHNUNG                                                        xrechnung.xml`,
        note: 'One version per standard is maintained; any other version is rejected with PXAEINV012 instead of being silently accepted. ZUGFeRD and Factur-X share one technical hybrid format while keeping separate product identities, so ZUGFeRD 2.5.2 output uses the harmonized factur-x.xml filename and the Factur-X XMP namespace. Embedding XRechnung XML in a PDF/A-3 document is a PXA workflow, not a claim that an XRechnung exchange requires a PDF.',
      },
      {
        title: 'The three validation states',
        json: `isStructurallyValid      Parsed under a bounded DTD-free reader and recognised as CII or UBL. Not a conformance claim.
diagnostics PXAEINV...   PXA business validation: standard and profile agreement, arithmetic totals, required fields.
isConformanceValidated   Official conformance only, from a pinned artifact set.
conformanceStatus        validated | invalid | notEvaluated
validatorArtifactSetId   Which pinned official artifact set produced the verdict.
validatorArtifactSetSha256`,
        note: 'conformanceStatus notEvaluated means no artifact set was configured for that standard, version, profile and syntax. It does not mean the invoice is fine. An invoice is never reported as conformant on the strength of structural validation alone. No official FeRD/FNFE or KoSIT artifact is shipped yet, so every profile currently reports notEvaluated, and no independent validation result is asserted anywhere.',
      },
      {
        title: 'Designer path',
        json: `/pdf/e-invoice

Permission   designer.pdf.e-invoices.read (plus generate, create and validate per action)
Entitlement  generator
Feature gate designer.e-invoice
Saving       the existing Designer template pipeline: two-second autosave, revision conflict,
             offline state and unsaved-changes warning
Actions      Validate, Generate XML, Create PDF/A-3 hybrid invoice, Download, Open in PXA Viewer`,
        note: 'The workspace edits one canonical invoice. The page elements the Canvas, the Live Preview and the exported PDF render are a projection of that object: regenerated whenever it changes, each keeping the binding path it came from, and never read back into the invoice. Tax groups and totals are read-only outputs computed by the same exact-decimal arithmetic the generator uses, because a total a user could overtype would be a second source of truth. Selecting a standard pins its maintained version and filters the profile list. Inserting a maintained layout is a single undoable transaction. Structural recognition, PXA business validation and official conformance appear as three separate rows, and only a backend valid verdict - which comes only from a pinned official artifact set - reads as validated. The workspace is localized in English, German, French, Spanish, Italian and Arabic, with Arabic rendering right-to-left.',
      },
      {
        title: 'Generator path',
        json: `var options = new PxaEInvoiceOptions {
  Standard = PxaEInvoiceStandard.FacturX,
  Version = EInvoice.CurrentFacturXVersion,
  Profile = "EN 16931",
};
var xml = EInvoice.GenerateXml(invoice, options);
var hybrid = EInvoice.CreatePdf(invoice, options, fontLoader);`,
        note: 'PXA calculates line nets, tax groups and totals: it rounds half away from zero at the currency minor-unit scale, groups tax by category code and rate, and orders the groups by rate then category. Choosing a rate, claiming an exemption and deciding what is taxable stay with the caller, because those are billing-domain decisions and PXA does not make them for you.',
      },
      {
        title: 'Attach to an existing PDF',
        json: `POST /api/pxa/v1/pdf/e-invoices/attach/preflight
POST /api/pxa/v1/pdf/e-invoices/attach

allowArchiveConversion       rewrite a source that is not PDF/A-3b
allowSignatureInvalidation   accept removing existing signatures
replaceExistingInvoice       replace an invoice the PDF already carries
allowInconsistentPdf         values were not found in the visible page
preserveUnrecognized         store XML PXA does not recognise, byte for byte`,
        note: 'A source that already identifies PDF/A-3b receives an incremental update, so the original bytes become the prefix of the result and no visible page can change. A source whose archival identification forbids embedded files is converted first, which is a rewrite and needs explicit approval. Every attachment is verified after it is written: the invoice is re-extracted byte for byte, the per-page content digests are compared, and the result is revalidated. A blocked operation returns 409 with the full preflight report and never modifies the source.',
      },
      {
        title: 'Inspect, extract and consistency',
        json: `POST /api/pxa/v1/pdf/e-invoices/inspect
POST /api/pxa/v1/pdf/e-invoices/extract

pdfConsistency.status  consistent | reviewRequired | notEvaluated
recognitionStatus      recognized | unrecognized | notXml
isEditable             false for preserved unknown XML`,
        note: 'A scanned or text-inaccessible PDF stays at notEvaluated. Missing text extraction is never treated as agreement, because that is the one false positive a reader could not detect. Amount matching covers the value own minor-unit scale as well as two-digit and thousand-separated forms, so an invoice in a currency with zero or three minor units is not flagged merely because of its scale. Extraction verifies the declared size and PDF checksum before returning any bytes.',
      },
      {
        title: 'Retained validation evidence',
        json: `GET  /api/pxa/v1/pdf/e-invoices/validations
GET  /api/pxa/v1/pdf/e-invoices/validations/{id}
GET  /api/pxa/v1/pdf/e-invoices/validations/export
POST /api/pxa/v1/pdf/e-invoices/validations/{id}/revalidate`,
        note: 'Each record is append-only and carries the tenant, actor, document checksum, container checksum, standard, version, profile, the three states, the validator artifact-set identifier and hash, the diagnostics as reported, and both timestamps. The database rejects an UPDATE outright and permits a DELETE only through the bounded retention path. Revalidation runs the currently configured artifact set over the same document bytes, proven identical by checksum, and stores the new verdict beside the old one so both remain provable.',
      },
      {
        title: 'Known limits',
        json: `No official FeRD/FNFE or KoSIT artifacts are shipped yet.
Independent validation results are not published yet.
Encrypted sources must be decrypted before an attachment.
An incremental attachment leaves earlier revisions intact.
notEvaluated consistency is not agreement.
100 MiB source PDF, 10 MiB invoice XML, 120 MiB output, 2000 synchronous lines.`,
        note: 'Peppol transport, national tax submission, accounting, payment and legal retention are external integrations, not PDF-engine capabilities. PXA hands you an invoice document and an honest account of what it checked; sending it to an access point, filing it with a tax authority, posting it to a ledger, settling it or retaining it for a statutory period are things your own systems do. PXA deliberately has no API for any of them.',
      },
    ],
  },
  {
    title: 'Spreadsheet',
    status: 'Preview',
    purpose:
      'Spreadsheet workflows connect workbook data, sheets, formulas, and mappings to document automation and export scenarios.',
    whenToUse: [
      'Your document output depends on workbook data.',
      'You need to import, map, or inspect spreadsheet structures.',
      'You are planning spreadsheet provider migration or export workflows.',
    ],
    concepts: ['Workbook import', 'Sheets', 'Cells', 'Formulas', 'Data mapping', 'Export flows'],
    tasks: ['Import workbook data', 'Inspect mapped sheets', 'Plan formula handling', 'Connect workbook data to document output'],
    related: [
      { label: 'Spreadsheet product page', href: companyPage('products/spreadsheet') },
      { label: 'Spreadsheet demo', href: `${siteLinks.demo}#demo/spreadsheet-import-export` },
      { label: 'Migration guide', href: '#spreadsheet-code-migration' },
    ],
  },
  {
    title: 'Importer',
    status: 'Preview',
    purpose:
      'Importer workflows normalize incoming files so PDF, Office, image, and document inputs can enter designer, migration, or generation flows.',
    whenToUse: [
      'You need to accept existing customer or internal files.',
      'You want diagnostics for files that cannot map cleanly.',
      'You are preparing imported content for designer or migration handoff.',
    ],
    concepts: ['File detection', 'Normalization', 'Importer diagnostics', 'PDF chart confidence', 'Format-specific importers', 'Designer handoff'],
    tasks: ['Choose an input format', 'Select off, safe, or review for PDF chart recognition', 'Normalize the file', 'Review chart confidence and diagnostics', 'Compare or restore the original visual when available', 'Send imported content to Designer or Migration'],
    related: [
      { label: 'Importer product page', href: companyPage('products/importer') },
      { label: 'Importer demo', href: `${siteLinks.demo}#demo/file-importer-flow` },
      { label: 'PXA.Importer', href: '#pxa-importer' },
    ],
    examples: [
      {
        title: 'PDF chart recognition modes',
        json: `POST /api/document/import-pdf-engine?chartRecognition=safe
Content-Type: multipart/form-data

file=@report.pdf`,
        note: 'safe converts confidence 0.85+ candidates. review also returns marked candidates from 0.60 to 0.849. off retains ordinary PDF primitives.',
      },
      {
        title: 'Recognition result',
        json: `{
  "type": "chart",
  "chart": {
    "schemaVersion": 2,
    "type": "bar",
    "recognition": {
      "status": "reviewRequired",
      "confidence": 0.72,
      "sourceKind": "pdfVector",
      "diagnosticCode": "PXA-PDF-CHART-102"
    }
  }
}`,
        note: 'PXA metadata round trips return confidence 1.0. Heuristic recognition is local and best effort.',
      },
    ],
  },
  {
    title: 'Export',
    status: 'Preview',
    purpose:
      'Export workflows turn designs, migrated reports, and generated document models into JSON, PDF output, demo artifacts, or code-oriented handoff files.',
    whenToUse: [
      'You need to download generated output or design JSON.',
      'You want to compare input, output, and source artifacts from demos.',
      'You need handoff artifacts for implementation or review.',
    ],
    concepts: ['Design JSON', 'PDF output', 'Demo artifacts', 'Code handoff', 'Download flows'],
    tasks: ['Export design JSON', 'Download generated output', 'Compare demo input and output', 'Use exported artifacts for implementation handoff'],
    related: [
      { label: 'Demo examples', href: '#demo-examples' },
      { label: 'Generator product page', href: companyPage('products/generator') },
      { label: 'API Reference', href: '#api-reference' },
    ],
  },
];

const pdfTrustDocumentation = editorDocs.find(({ title }) => title === 'PDF Viewer');
const pdfTrustValidationExample = pdfTrustDocumentation?.examples?.find(
  ({ title }) => title === 'Interpret validation results',
);
if (pdfTrustValidationExample) {
  pdfTrustValidationExample.note = pdfTrustValidationExample.note
    .replace(
      'partially covered text runs',
      'partially covered text runs whose original glyph bytes and positions cannot be resolved safely',
    )
    .replace(
      'Missing, malformed, uncolored, and shading patterns remain blocked because flattening or remapping them could change appearance or semantics.',
      'Uncolored tiling patterns retain their identity while their base color operands and resource declaration are converted atomically. DeviceGray, DeviceRGB, calibrated, and embedded-ICC shadings are preserved; DeviceCMYK, Separation, DeviceN, mesh, missing, and malformed shadings remain blocked.',
    )
    .replace(
      'ICCBased without an explicit supported alternate, CalGray, CalRGB, Lab, unsafe inline images, shading resources, and unknown named spaces remain blocked.',
      'Valid CalGray, CalRGB, and Lab spaces use their complete calibration parameters, and embedded Gray, RGB, and CMYK ICC profiles are transformed through LittleCMS. Invalid profiles, an unavailable native runtime, unsafe inline images, and unknown named spaces fail closed.',
    )
    .replace(
      'PXA does not yet claim an exhaustive ISO rule implementation for every annotation appearance, optional-content, transparency-group, soft-mask, blend-space, and rendering-intent combination.',
      'The bounded PXA structural validator checks annotation normal appearances, static AcroForm behavior, optional-content configuration names and order, ISO rendering intents, ISO blend modes, and decodable Form content used by transparency and soft masks. PDF/A-2 and PDF/A-3 transparency is preserved rather than incorrectly prohibited, and the required RGB output intent supplies the document blending colour space. Standards-level release evidence still comes from the pinned veraPDF profile and a recorded second independent validator, not from the first-party structural result alone.',
    )
    .replace(
      'The result is then reparsed and verified by re-running region intersection and re-extracting text across the whole document, and a failed verification returns an error instead of the file.',
      'The result is then reparsed and verified by re-running region intersection and re-extracting text inside each redacted region; equal text elsewhere remains legitimate. A failed verification returns an error instead of the file.',
    )
    .replace(
      'Verification runs each recovery route as its own named check: residual covered elements and images, retained thumbnails, retained optional content, retained non-normal appearance streams, more than one revision, re-extracted text across the whole document, and a scan of the result bytes and every decodable stream for the removed text. That byte scan can miss residue inside a stream it cannot decode, but it never reports residue that is not there.',
      'Verification runs supported recovery routes as named checks: residual covered elements and images, retained thumbnails, retained optional content, retained non-normal appearance streams, more than one revision, and re-extracted text inside each redacted region. PXA does not use global raw-string equality as proof because PDF encodings can hide text and an equal string outside the region is not residue. The independently maintained recovery corpus pins externally produced Chrome and LibreOffice PDFs by SHA-256 and exercises encoded glyphs, compressed content, reusable resources, and malformed Flate payloads. Poppler pdftotext, Poppler pdfimages, and POSIX strings independently probe supported redacted output. Chrome clipping groups, unresolved reusable resources, and malformed streams fail closed; the no-recovery guarantee applies only to the documented supported corpus.',
    )
    .replace(
      'The audit record is returned in the PXA-Redaction-Audit and PXA-Redaction-Result-Sha256 response headers rather than written into the redacted document, because the reason and the actor would otherwise be disclosed to everyone who receives a file that was redacted to limit disclosure.',
      'The response returns the audit-record and result hashes, signed and timestamped status, and an authenticated retrieval location rather than writing audit data into the redacted document. PXA persists the complete bounded record as append-only tenant evidence. When an RFC 3161 authority is configured, it timestamps the audit CMS signature and a timestamp failure prevents delivery of the redacted PDF. GET /api/pxa/v1/pdf/redactions/audits/{recordSha256} returns the evidence only within the active organization after rechecking its canonical hash, CMS signature, timestamp imprint, TSA signature, timestamping EKU, and recorded TSA identity; responses are never cached.',
    )
    .replace(
      'any covered image whose reusable pixels cannot yet be purged',
      'partially covered images whose samples, masks, filters, or colour spaces cannot be decoded and rewritten safely',
    )
    .replace(
      'while geometric ambiguity, images, layers, and unsupported structures block',
      'while malformed or oversized image codecs, unsupported JPX component layouts, unresolved graphics-state masks, malformed selected patterns, layers, and unsupported structures block',
    )
    .replace(
      'It reports soft and stencil masks on images, soft masks set in the graphics state, pattern resources, and page or form transparency groups, because none of those can be resolved to a per-region answer without evaluating pixels.',
      'It evaluates image masks and pattern selections only for paint operations that intersect a region. Bounded, decodable soft-mask and explicit-mask streams are cloned and scrubbed together with the main image; stencil images and colour-key masks are preserved safely. Valid tiling and shading pattern uses are removed with the containing paint element, and a pattern stream is pruned when its last use is removed. Unused patterns and uses outside the region do not block. Bounded grayscale/RGB JPX and embedded JBIG2 streams are decoded by bundled managed codecs; PXA validates dimensions, segment boundaries, and JBIG2Globals before rewriting them as lossless data. Unsupported JPX component layouts, malformed codec data, unresolved graphics-state masks, malformed selected patterns, and page or form transparency groups remain blocked.',
    )
    .replace(
      'Covered image pixels are still not removed, so a document whose region covers an image remains blocked rather than partially redacted.',
      'Fully contained images are removed with their unreachable resources. For bounded 1-, 2-, 4-, 8-, and 16-bit images in resolvable named or custom colour spaces using raw, Flate, LZW, RunLength, or DCT/JPEG data, and for bounded grayscale/RGB JPX or embedded JBIG2 data, PXA clones shared resources and zeroes only covered samples. Attached mask streams are cloned and scrubbed in the same region; inline replacements use an unambiguous ASCIIHex payload. Verification decodes the image and every attached mask and checks covered samples rather than trusting a marker. Malformed, mismatched, oversized, or unsupported codec cases fail closed.',
    );
}

if (pdfTrustDocumentation?.examples) {
  pdfTrustDocumentation.examples.push({
    title: 'Understand image redaction boundaries',
    json: `{
  "supportedPartialImages": [
    "Raw 1-, 2-, 4-, 8-, and 16-bit samples",
    "Resolvable named and custom colour spaces",
    "Flate, LZW, RunLength",
    "DCT/JPEG",
    "JPX with grayscale or RGB components",
    "Embedded JBIG2, including bounded JBIG2Globals",
    "Inline images without external mask streams",
    "Stencil, colour-key, soft-mask, and explicit-mask images"
  ],
  "blocked": [
    "JPX with unsupported component layouts",
    "standalone, malformed, or oversized JBIG2 data",
    "unresolvable colour spaces",
    "inline images with external mask streams",
    "recursive or malformed masks"
  ]
}`,
    note: 'A fully contained image is removed. A supported partially covered image is decoded locally, copied when its resource is shared, rewritten with zero-valued covered samples, and decoded again during verification. JPX and JBIG2 input is bounded and structurally checked before the managed codecs run; the original compressed payload is never copied into the replacement. The opt-in recovery harness checks supported text and masked-image output with pdftotext, pdfimages, and strings, while immutable external PDFs verify fail-closed behavior for unsupported structures. PXA blocks apply when it cannot prove that the covered pixels can be removed safely.',
  });
}

const designerWorkflowSteps = [
  {
    title: 'Create or open a template',
    description: 'Start from an empty page, a gallery template, imported file, or migrated report output.',
    actions: ['Open PXA Designer', 'Choose blank template, gallery item, import result, or migration handoff', 'Confirm page size and margins before placing content'],
    result: 'A template with at least one page and known page settings.',
  },
  {
    title: 'Add elements',
    description: 'Use the toolbar groups to place text, images, tables, charts, forms, shapes, and document helpers.',
    actions: ['Open the toolbar group', 'Click an element type', 'Place it on the active page', 'Use Element Reference when unsure about properties'],
    result: 'Editable elements are visible on the page and selectable.',
  },
  {
    title: 'Position and resize',
    description: 'Move elements using the design surface, then set exact x/y/width/height for predictable output.',
    actions: ['Drag the element on the page', 'Resize handles or edit geometry values', 'Use locked state for stable guides and shared elements'],
    result: 'The element layout matches the expected document geometry.',
  },
  {
    title: 'Edit properties',
    description: 'Use the properties panel for content, style, data binding, visibility, language, and element-specific options.',
    actions: ['Select an element', 'Edit content or element value', 'Set typography, color, border, and alignment', 'Add binding or visibleExpression when data-driven'],
    result: 'The selected element has the right visual style and data behavior.',
  },
  {
    title: 'Preview output',
    description: 'Preview often, especially after migration or when using bindings, tables, charts, forms, and page-level settings.',
    actions: ['Open preview', 'Check page flow and alignment', 'Review warnings/diagnostics if output differs', 'Return to Designer for corrections'],
    result: 'The visual output is close enough for export or review.',
  },
  {
    title: 'Export or hand off',
    description: 'Export JSON, generate PDF/output artifacts, or pass the design to code and demo workflows.',
    actions: ['Export design JSON', 'Generate PDF or selected output format', 'Download artifacts', 'Use source/output links in demos when available'],
    result: 'A portable design or generated artifact is ready for implementation or QA.',
  },
  {
    title: 'Open migrated report output',
    description: 'Report migrations produce editable PXA design JSON; use Designer to verify layout fidelity and finish manual follow-ups.',
    actions: ['Run report designer migration', 'Review diagnostics', 'Click Open in Designer', 'Compare original report layout with converted PXA design'],
    result: 'Migrated content is editable and remaining differences are visible.',
  },
];

const designerPanelReference = [
  ['Element Library', 'Switches between individual Elements and reusable Content blocks or page kits.'],
  ['Canvas / page surface', 'Shows the active page. Drag, resize, select, and inspect positioned elements.'],
  ['Properties panel', 'Edits geometry, content, style, binding, visibility, language, and element-specific fields.'],
  ['Preview', 'Renders the template output so layout, bindings, and export behavior can be checked.'],
  ['JSON / Export', 'Turns the design into handoff JSON or generated output artifacts.'],
  ['Migration handoff', 'Loads converted report designs so users can correct layout and complete manual follow-ups.'],
];

const elementLibraryGroups = [
  ['Favorites', 'Personal shortcuts synchronized across devices and organizations. The original category entry remains available.'],
  ['Text and Data', 'Text, rich text, numbers, dates, lists, variables, and data-oriented output.'],
  ['Forms and Input', 'Text fields, text areas, checkboxes, radio buttons, dropdowns, signatures, and form helpers.'],
  ['Tables and Charts', 'Tables, charts, repeat regions, and structured visual data.'],
  ['Media and Codes', 'Images, barcodes, QR codes, and media-oriented elements.'],
  ['Shapes and Layout', 'Lines, arrows, rectangles, shapes, guides, and layout regions.'],
  ['Document and Navigation', 'Links, bookmarks, comments, table of contents, page numbers, and document helpers.'],
  ['Word Elements', 'Word-specific elements. This group is shown only when the document mode supports it.'],
];

const contentLibraryReference = [
  ['Forms', 'Address, contact, personal, configurable field blocks, and a complete form page.'],
  ['Invoices', 'Header, customer details, item table, totals, and a complete invoice page.'],
  ['Books', 'Chapter title, body, bibliography, and a multi-page chapter kit.'],
  ['Certificates', 'Recipient, award text, signatures, and a complete certificate page.'],
  ['Presentations', 'Title, content, chart blocks, and a small slide deck.'],
  ['Reports', 'Report header, KPI, table, chart sections, and multi-page report kits.'],
];

const commonElementAttributes = [
  ['id', 'string', 'Unique id of the element inside the page. Generated by the designer.'],
  ['type', 'string', 'Element kind, for example "text", "image", "table", "chart", or "field".'],
  ['x / y', 'number', 'Position in page units from the top-left page corner. Defaults to points.'],
  ['width / height', 'number', 'Element box size in page units.'],
  ['name', 'string', 'Optional human-readable label for selection, search, and handoff.'],
  ['hidden', 'boolean', 'Keeps the element in the design but hides it from output.'],
  ['locked', 'boolean', 'Prevents accidental movement or editing in the designer.'],
  ['binding', 'string', 'Data path used to replace the value at render time.'],
  ['expression', 'string', 'Template expression evaluated during rendering.'],
  ['visibleExpression', 'string', 'Condition that decides whether the element is rendered.'],
  ['style', 'object', 'CSS-like style map. Unsupported keys are ignored by renderers.'],
];

const commonStyleAttributes = [
  ['fontSize', 'number', 'Font size in points.'],
  ['fontFamily', 'string', 'Font family name, for example Arial or Times New Roman.'],
  ['fontWeight', 'string', 'normal or bold.'],
  ['fontStyle', 'string', 'normal or italic.'],
  ['color', 'string', 'Text or foreground color as hex, rgb(), hsl(), or CSS color name.'],
  ['textAlign', 'string', 'left, center, right, or justify where supported.'],
  ['backgroundColor', 'string', 'Element fill/background color.'],
  ['borderColor / borderWidth', 'string / number', 'Border color and thickness.'],
  ['borderRadius', 'number', 'Corner radius in points where supported.'],
  ['padding', 'number', 'Inner spacing in points.'],
  ['lineHeight', 'number', 'Line height multiplier for text-like elements.'],
  ['opacity', 'number', 'Opacity from 0 to 1 where supported.'],
  ['rotation', 'number', 'Rotation in degrees.'],
];

const elementReferenceDocs = [
  {
    title: 'Text Block',
    type: 'text',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Single- or multi-line text for headings, labels, paragraphs, totals, and migrated report textboxes.',
    addSteps: ['Open PXA Designer.', 'In the toolbar open Text Elements.', 'Click Text.', 'Drag the element to the required position and resize the box.', 'Edit Content and typography in the properties panel.'],
    usage: ['Static label such as "Invoice".', 'Dynamic value with {{Customer.Name}} tokens.', 'Heading with headingLevel for Table of Contents generation.', 'Multi-line paragraph when the box height and lineHeight allow wrapping.'],
    attributes: [
      ['content', 'string', 'Text to render. Can contain {{tokens}}.'],
      ['headingLevel', '1 | 2 | 3', 'Marks text as a heading for the TOC element.'],
      ['style.fontSize', 'number', 'Font size in points.'],
      ['style.fontFamily', 'string', 'Font family.'],
      ['style.fontWeight', 'normal | bold', 'Text weight.'],
      ['style.fontStyle', 'normal | italic', 'Italic handling.'],
      ['style.color', 'string', 'Text color.'],
      ['style.textAlign', 'left | center | right | justify', 'Horizontal alignment inside the box.'],
      ['style.lineHeight', 'number', 'Useful for multi-line text.'],
    ],
    example: `{
  "id": "invoice-title",
  "type": "text",
  "x": 40,
  "y": 40,
  "width": 300,
  "height": 32,
  "content": "Invoice {{Number}}",
  "style": {
    "fontSize": 18,
    "fontWeight": "bold",
    "color": "#111827"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Text',
      properties: ['Content', 'Font family', 'Font size', 'Weight', 'Alignment', 'Line height'],
      preview: 'Invoice 1001',
    },
  },
  {
    title: 'Rich Text',
    type: 'richtext',
    category: 'Text Elements',
    status: 'Ready',
    description: 'HTML-formatted text for paragraphs with bold, italic, lists, and inline emphasis.',
    addSteps: ['Open Text Elements.', 'Click Rich Text.', 'Paste or edit restricted HTML content.', 'Resize the element to fit the expected text flow.'],
    usage: ['Terms paragraphs.', 'Formatted notes.', 'Imported DOCX/HTML text blocks.', 'Mixed inline styles that should stay together.'],
    attributes: [
      ['htmlContent', 'string', 'Restricted inline HTML rendered as rich text.'],
      ['style.fontSize', 'number', 'Base font size for the block.'],
      ['style.color', 'string', 'Default text color.'],
      ['style.lineHeight', 'number', 'Paragraph line height.'],
    ],
    example: `{
  "id": "payment-note",
  "type": "richtext",
  "x": 40,
  "y": 92,
  "width": 360,
  "height": 80,
  "htmlContent": "<p>Payment due within <b>30 days</b>.</p>"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Rich Text',
      properties: ['HTML content', 'Base font size', 'Color', 'Line height'],
      preview: 'Payment due within 30 days.',
    },
  },
  {
    title: 'Image',
    type: 'image',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Raster or vector image element for logos, signatures, imported images, stamps, and product visuals.',
    addSteps: ['Open Visual Elements.', 'Click Image.', 'Set the image URL or data URI in Content.', 'Choose fit mode and crop settings when needed.'],
    usage: ['Company logo in a header.', 'Imported PDF/image content.', 'Product picture in a quote.', 'Image watermark source.'],
    attributes: [
      ['content', 'string', 'Image URL or data URI.'],
      ['fitMode', 'contain | cover | fill | none', 'How the image fits the element box.'],
      ['preserveAspectRatio', 'boolean', 'Keeps image proportions when resizing.'],
      ['cropX / cropY / cropWidth / cropHeight', 'number', 'Optional crop rectangle.'],
      ['focalX / focalY', 'number', 'Focal point for cover/crop workflows.'],
    ],
    example: `{
  "id": "logo",
  "type": "image",
  "x": 40,
  "y": 28,
  "width": 120,
  "height": 48,
  "content": "https://example.com/logo.png",
  "fitMode": "contain",
  "preserveAspectRatio": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Image',
      properties: ['Image URL/data URI', 'Fit mode', 'Crop', 'Aspect ratio'],
      preview: 'Logo image box',
    },
  },
  {
    title: 'Table',
    type: 'table',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Fixed-column table with header rows, cell data, column widths, per-cell styling, and optional repeated data.',
    addSteps: ['Open Shapes & Layout.', 'Click Table.', 'Edit rows and columns through cellData.', 'Set headerRow, column widths, alignment, and zebra styling.', 'Use repeat when rows come from a dataset.'],
    usage: ['Invoice line items.', 'Report detail rows.', 'Summary totals.', 'Migrated RDL tablix/table output.'],
    attributes: [
      ['cellData', 'string[][]', 'Rows and cells. Cells may contain {{tokens}}.'],
      ['columnWidths', 'number[]', 'Per-column widths.'],
      ['columnAlignments', 'left | center | right[]', 'Per-column text alignment.'],
      ['headerRow', 'boolean', 'Treats first row as table header.'],
      ['footerRow', 'boolean', 'Treats last row as footer/total row.'],
      ['zebraEnabled / zebraColor', 'boolean / string', 'Alternating row backgrounds.'],
      ['cellStyles', 'object[]', 'Sparse cell styles addressed by row and col.'],
      ['repeat', 'object', 'Dataset repeat configuration for data-driven rows.'],
    ],
    example: `{
  "id": "items",
  "type": "table",
  "x": 40,
  "y": 180,
  "width": 500,
  "height": 120,
  "headerRow": true,
  "cellData": [
    ["Item", "Qty", "Total"],
    ["{{Name}}", "{{Qty}}", "{{Total}}"]
  ],
  "columnAlignments": ["left", "right", "right"]
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Table',
      properties: ['Rows/cells', 'Header row', 'Column widths', 'Cell styles', 'Repeat'],
      preview: 'Item | Qty | Total',
    },
  },
  {
    title: 'Chart',
    type: 'chart',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Professional multi-series bar, line, area, pie, doughnut, stacked-bar, and combo charts rendered consistently in the Designer and vector PDF output.',
    addSteps: ['Open Visual Elements.', 'Click Chart.', 'Edit categories and values under Data.', 'Configure series, appearance, axes, or binding.', 'Resize the chart and verify it in Live Preview.'],
    usage: ['Sales by period.', 'Report dashboard charts.', 'Migrated report charts.', 'Editable charts recovered from supported PDFs.'],
    attributes: [
      ['chart.schemaVersion', '2', 'Version of the typed chart contract.'],
      ['chart.type', 'bar | line | area | pie | doughnut | stackedBar | combo', 'Chart kind.'],
      ['chart.categories', 'string[]', 'Labels on the category axis or circular slices.'],
      ['chart.series', 'ChartSeries[]', 'Stable series with values, color, type, axis, and stack group.'],
      ['chart.binding', 'object', 'Optional data path and category/series/value field mapping.'],
      ['chart.recognition', 'object', 'Confidence and review state for charts recovered from PDFs.'],
      ['chartType / chartData', 'legacy', 'Read for compatibility and normalized to chart schema version 2.'],
    ],
    example: `{
  "id": "sales-chart",
  "type": "chart",
  "x": 40,
  "y": 340,
  "width": 320,
  "height": 200,
  "chart": {
    "schemaVersion": 2,
    "type": "combo",
    "title": "Quarterly performance",
    "categories": ["Q1", "Q2", "Q3"],
    "series": [
      { "id": "sales", "name": "Sales", "type": "bar", "values": [10, 20, 16], "color": "#2563eb" },
      { "id": "margin", "name": "Margin", "type": "line", "values": [3, 5, 4], "color": "#16a34a" }
    ]
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Chart',
      properties: ['Data', 'Series', 'Appearance', 'Axes', 'Binding', 'Advanced'],
      preview: 'Multi-series chart preview',
    },
  },
  {
    title: 'Line and Shapes',
    type: 'line',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Layout primitives for dividers, boxes, circles, backgrounds, report separators, and visual grouping.',
    addSteps: ['Open Shapes & Layout.', 'Choose Line, Shape, Rectangle, or Circle.', 'Place and resize on the canvas.', 'Set border, fill, opacity, and rotation.'],
    usage: ['Receipt separators.', 'Report boxes.', 'Background bands.', 'Migrated XRLine/RDL Line/FastReport LineObject output.'],
    attributes: [
      ['type', 'line | shape | rect | circle', 'Specific primitive type.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke width.'],
      ['style.backgroundColor', 'string', 'Fill color for boxes/shapes.'],
      ['style.opacity', 'number', 'Transparency from 0 to 1.'],
      ['style.rotation', 'number', 'Rotation in degrees.'],
    ],
    example: `{
  "id": "section-rule",
  "type": "line",
  "x": 40,
  "y": 156,
  "width": 500,
  "height": 1,
  "style": {
    "borderColor": "#9ca3af",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Line / Rectangle / Circle',
      properties: ['Stroke color', 'Stroke width', 'Fill', 'Opacity', 'Rotation'],
      preview: 'Horizontal divider line',
    },
  },
  {
    title: 'Form Fields',
    type: 'field',
    category: 'Form Elements',
    status: 'Preview',
    description: 'Interactive form controls: text fields, text areas, checkboxes, radio groups, dropdowns, option lists, buttons, and signatures.',
    addSteps: ['Open Form Elements.', 'Choose Field, Checkbox, Dropdown, Radio, Button, or Signature.', 'Set fieldName and visible label.', 'Configure options or required state.', 'Preview/export through PDF/Word-capable workflows.'],
    usage: ['Customer input forms.', 'Approval checklists.', 'Signature placeholders.', 'PDF viewer form review workflows.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field identifier.'],
      ['fieldLabel', 'string', 'Visible label.'],
      ['placeholder', 'string', 'Placeholder text for text input.'],
      ['required', 'boolean', 'Marks input as required.'],
      ['options', 'string[]', 'Dropdown, option list, or radio values.'],
      ['selectedValue', 'string', 'Initial selected value.'],
      ['checkState', 'checked | cross | dot | empty', 'Checkbox/checkmark visual state.'],
      ['buttonAction', 'string', 'URL or internal action for button elements.'],
      ['signatureLabel', 'string', 'Caption below signature line.'],
    ],
    example: `{
  "id": "customer-name",
  "type": "field",
  "x": 40,
  "y": 420,
  "width": 240,
  "height": 28,
  "fieldLabel": "Customer name",
  "fieldName": "customerName",
  "placeholder": "Enter name",
  "required": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Field / Checkbox / Dropdown / Signature',
      properties: ['Field name', 'Label', 'Placeholder', 'Required', 'Options'],
      preview: 'Customer name input',
    },
  },
  {
    title: 'Date',
    type: 'date',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Static, render-time, or bound date value with locale and timezone-aware formatting.',
    addSteps: ['Open Text Elements.', 'Click Date.', 'Choose dateMode.', 'Set dateFormat, locale, timezone, and fallback text as needed.'],
    usage: ['Invoice date.', 'Generated-at timestamp.', 'Bound order date.', 'Locale-aware report date.'],
    attributes: [
      ['dateMode', 'static | render | binding', 'Controls whether the value is fixed, generated at render time, or read from binding.'],
      ['dateFormat', 'string', 'Format pattern such as dd.MM.yyyy.'],
      ['locale', 'string', 'BCP-47 locale such as de-DE.'],
      ['timezone', 'string', 'IANA timezone such as Europe/Berlin.'],
      ['fallbackText', 'string', 'Shown when no value resolves.'],
    ],
    example: `{
  "id": "invoice-date",
  "type": "date",
  "x": 420,
  "y": 40,
  "width": 120,
  "height": 20,
  "dateMode": "render",
  "dateFormat": "dd.MM.yyyy",
  "locale": "de-DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Date',
      properties: ['Date mode', 'Format', 'Locale', 'Timezone', 'Fallback'],
      preview: '15.07.2026',
    },
  },
  {
    title: 'Page Number',
    type: 'pagenumber',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Automatic page number element for current page, total pages, page-of-total, roman, or alphabetic numbering.',
    addSteps: ['Open Text Elements.', 'Click Page Number.', 'Choose numberingFormat.', 'Set prefix, suffix, and startNumber when needed.'],
    usage: ['Footer page labels.', 'Page X of Y.', 'Appendix pages with roman numbering.', 'Migrated report footer page info.'],
    attributes: [
      ['numberingFormat', 'current | total | pageOfTotal | roman | alphabetic', 'What the element displays.'],
      ['startNumber', 'number', 'Number assigned to the first page.'],
      ['prefix', 'string', 'Text before the generated number.'],
      ['suffix', 'string', 'Text after the generated number.'],
    ],
    example: `{
  "id": "page-number",
  "type": "pagenumber",
  "x": 430,
  "y": 810,
  "width": 90,
  "height": 16,
  "numberingFormat": "pageOfTotal",
  "prefix": "Page "
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Page Number',
      properties: ['Format', 'Start number', 'Prefix', 'Suffix'],
      preview: 'Page 1 of 3',
    },
  },
  {
    title: 'Number',
    type: 'number',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Formatted numeric value for currency, percent, decimal, scientific, or ordinal output.',
    addSteps: ['Open Text Elements.', 'Click Number.', 'Set numberValue or binding.', 'Choose numberStyle, decimals, currency, and locale.'],
    usage: ['Currency totals.', 'Percent KPIs.', 'Localized amounts.', 'Bound numeric report fields.'],
    attributes: [
      ['numberValue', 'number', 'Value to format.'],
      ['numberStyle', 'decimal | currency | percent | scientific | ordinal', 'Formatting style.'],
      ['numberDecimals', 'number', 'Fraction digits.'],
      ['numberCurrency', 'string', 'ISO currency code such as EUR.'],
      ['numberLocale', 'string', 'Locale for grouping and decimal separators.'],
    ],
    example: `{
  "id": "total",
  "type": "number",
  "x": 430,
  "y": 300,
  "width": 110,
  "height": 20,
  "numberValue": 1234.5,
  "numberStyle": "currency",
  "numberCurrency": "EUR",
  "numberLocale": "de-DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Number',
      properties: ['Value', 'Style', 'Decimals', 'Currency', 'Locale'],
      preview: '1.234,50 EUR',
    },
  },
  {
    title: 'Table of Contents',
    type: 'toc',
    category: 'Text Elements',
    status: 'Ready',
    description: 'Generated table of contents based on text elements that define headingLevel.',
    addSteps: ['Create heading Text elements and set headingLevel.', 'Open Text Elements.', 'Click Table of Contents.', 'Set TOC title, page numbers, leader dots, and heading level range.'],
    usage: ['Long documents.', 'Books and manuals.', 'Generated report packages.', 'Word/PDF navigation output.'],
    attributes: [
      ['tocTitle', 'string', 'Heading shown above generated entries.'],
      ['tocShowPageNumbers', 'boolean', 'Shows page numbers.'],
      ['tocShowLeaderDots', 'boolean', 'Shows dotted leaders.'],
      ['tocMinLevel / tocMaxLevel', 'number', 'Heading levels included.'],
      ['tocPlacement', 'beginning | end', 'Where generated TOC pages are inserted.'],
    ],
    example: `{
  "id": "contents",
  "type": "toc",
  "x": 40,
  "y": 80,
  "width": 460,
  "height": 220,
  "tocTitle": "Contents",
  "tocShowPageNumbers": true,
  "tocShowLeaderDots": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Table of Contents',
      properties: ['Title', 'Page numbers', 'Leader dots', 'Min/max level', 'Placement'],
      preview: 'Contents ...... 1',
    },
  },
  {
    title: 'Text Area',
    type: 'textarea',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Multi-line fillable text input for notes, comments, descriptions, and long form answers.',
    addSteps: ['Open Form Elements.', 'Click Text Area.', 'Set fieldName and placeholder.', 'Resize height for expected lines.'],
    usage: ['Comments field.', 'Customer notes.', 'Approval remarks.', 'PDF form long answer input.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable form field id.'],
      ['placeholder', 'string', 'Placeholder shown before input.'],
      ['required', 'boolean', 'Marks the field as required where supported.'],
      ['style.fontSize', 'number', 'Input text size.'],
    ],
    example: `{
  "id": "comments",
  "type": "textarea",
  "x": 40,
  "y": 460,
  "width": 300,
  "height": 90,
  "fieldName": "comments",
  "placeholder": "Notes..."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Text Area',
      properties: ['Field name', 'Placeholder', 'Required', 'Size'],
      preview: 'Notes...',
    },
  },
  {
    title: 'Checkbox',
    type: 'checkbox',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Interactive boolean checkbox field with an initial visual state.',
    addSteps: ['Open Form Elements.', 'Click Checkbox.', 'Set fieldName.', 'Choose checkState for the initial value.'],
    usage: ['Terms accepted.', 'Approval flags.', 'Checklist rows.', 'Migrated yes/no report controls.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field id.'],
      ['checkState', 'checked | cross | dot | empty', 'Initial checkbox glyph/state.'],
      ['required', 'boolean', 'Marks the checkbox as required where supported.'],
    ],
    example: `{
  "id": "approved",
  "type": "checkbox",
  "x": 40,
  "y": 560,
  "width": 16,
  "height": 16,
  "fieldName": "approved",
  "checkState": "checked"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Checkbox',
      properties: ['Field name', 'Check state', 'Required'],
      preview: '[x] Approved',
    },
  },
  {
    title: 'Radio Group',
    type: 'radio',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Single-select radio group for mutually exclusive options.',
    addSteps: ['Open Form Elements.', 'Click Radio.', 'Add options.', 'Set selectedValue if one option should be preselected.'],
    usage: ['Yes/no/maybe choices.', 'Shipping method.', 'Priority selection.', 'Survey answers.'],
    attributes: [
      ['options', 'string[]', 'Available radio values.'],
      ['selectedValue', 'string', 'Currently selected option.'],
      ['fieldName', 'string', 'Shared field id for the radio group.'],
    ],
    example: `{
  "id": "priority",
  "type": "radio",
  "x": 40,
  "y": 590,
  "width": 220,
  "height": 60,
  "fieldName": "priority",
  "options": ["Low", "Normal", "High"],
  "selectedValue": "Normal"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Radio',
      properties: ['Field name', 'Options', 'Selected value'],
      preview: '( ) Low  (o) Normal',
    },
  },
  {
    title: 'Dropdown',
    type: 'dropdown',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Combo box / dropdown field with configurable choices.',
    addSteps: ['Open Form Elements.', 'Click Dropdown.', 'Set fieldName.', 'Add options and selectedValue.'],
    usage: ['Country selection.', 'Status values.', 'Predefined categories.', 'Provider-generated choice fields.'],
    attributes: [
      ['fieldName', 'string', 'Machine-readable field id.'],
      ['options', 'string[]', 'Dropdown choices.'],
      ['selectedValue', 'string', 'Initial selected value.'],
    ],
    example: `{
  "id": "country",
  "type": "dropdown",
  "x": 40,
  "y": 650,
  "width": 180,
  "height": 24,
  "fieldName": "country",
  "options": ["DE", "FR", "US"],
  "selectedValue": "DE"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Dropdown',
      properties: ['Field name', 'Options', 'Selected value'],
      preview: 'DE v',
    },
  },
  {
    title: 'Option List',
    type: 'optionlist',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Static or form-oriented ordered/unordered option list.',
    addSteps: ['Open Form Elements.', 'Click Option List.', 'Add options.', 'Choose ordered and listStyle.'],
    usage: ['Checklist text.', 'Selectable-looking static lists.', 'Numbered instructions.', 'Migrated option groups.'],
    attributes: [
      ['options', 'string[]', 'List entries.'],
      ['ordered', 'boolean', 'Numbered when true, bulleted when false.'],
      ['listStyle', 'decimal | alpha | roman | disc | square', 'Marker style.'],
    ],
    example: `{
  "id": "steps",
  "type": "optionlist",
  "x": 300,
  "y": 590,
  "width": 220,
  "height": 80,
  "options": ["Review", "Approve", "Archive"],
  "ordered": true,
  "listStyle": "decimal"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Option List',
      properties: ['Options', 'Ordered', 'List style'],
      preview: '1. Review',
    },
  },
  {
    title: 'Button',
    type: 'button',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Clickable button area that links to a URL or internal page action.',
    addSteps: ['Open Form Elements.', 'Click Button.', 'Set content as the caption.', 'Set buttonAction to a URL or internal page target.'],
    usage: ['Open external resource.', 'Jump to internal page.', 'Call-to-action in HTML/PDF contexts.', 'Interactive form navigation.'],
    attributes: [
      ['content', 'string', 'Button caption.'],
      ['buttonAction', 'string', 'URL or internal action such as page:2.'],
      ['style.backgroundColor', 'string', 'Button fill.'],
      ['style.color', 'string', 'Caption color.'],
    ],
    example: `{
  "id": "open-docs",
  "type": "button",
  "x": 40,
  "y": 700,
  "width": 120,
  "height": 32,
  "content": "Open docs",
  "buttonAction": "https://docs.powerdoxautomation.com"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Button',
      properties: ['Caption', 'Action', 'Fill', 'Text color'],
      preview: 'Open docs',
    },
  },
  {
    title: 'Signature',
    type: 'signature',
    category: 'Form Elements',
    status: 'Ready',
    description: 'Signature line placeholder for manual or digital signing workflows.',
    addSteps: ['Open Form Elements.', 'Click Signature.', 'Resize the line area.', 'Set signatureLabel.'],
    usage: ['Approval forms.', 'Contracts.', 'Delivery confirmation.', 'Generated sign-off pages.'],
    attributes: [
      ['signatureLabel', 'string', 'Caption under the signature line.'],
      ['width / height', 'number', 'Signature area size.'],
      ['style.borderColor', 'string', 'Line color where supported.'],
    ],
    example: `{
  "id": "signature",
  "type": "signature",
  "x": 320,
  "y": 700,
  "width": 220,
  "height": 50,
  "signatureLabel": "Authorized signature"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Form Elements -> Signature',
      properties: ['Signature label', 'Line size', 'Style'],
      preview: '________________',
    },
  },
  {
    title: 'QR Code',
    type: 'qrcode',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Generated QR code from a URL, id, or data string.',
    addSteps: ['Open Visual Elements.', 'Click QR Code.', 'Set qrValue.', 'Resize the square element.'],
    usage: ['Payment URL.', 'Verification link.', 'Ticket code.', 'Customer portal link.'],
    attributes: [
      ['qrValue', 'string', 'Encoded value.'],
      ['qrSize', 'number', 'Module size hint.'],
      ['width / height', 'number', 'Rendered QR box size.'],
    ],
    example: `{
  "id": "payment-qr",
  "type": "qrcode",
  "x": 430,
  "y": 360,
  "width": 96,
  "height": 96,
  "qrValue": "https://pay.example/invoice/1001"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> QR Code',
      properties: ['QR value', 'Size', 'Position'],
      preview: 'QR',
    },
  },
  {
    title: 'Barcode',
    type: 'barcode',
    category: 'Visual Elements',
    status: 'Ready',
    description: 'Linear barcode generated from a value and symbology.',
    addSteps: ['Open Visual Elements.', 'Click Barcode.', 'Set barcodeValue.', 'Choose barcodeType.', 'Resize for scanner readability.'],
    usage: ['Shipping labels.', 'SKU labels.', 'Receipt codes.', 'Inventory forms.'],
    attributes: [
      ['barcodeValue', 'string', 'Encoded value.'],
      ['barcodeType', 'Code128 | EAN13 | UPCA | Code39', 'Barcode symbology.'],
      ['width / height', 'number', 'Barcode box size.'],
    ],
    example: `{
  "id": "sku-barcode",
  "type": "barcode",
  "x": 330,
  "y": 470,
  "width": 200,
  "height": 60,
  "barcodeValue": "4006381333931",
  "barcodeType": "EAN13"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Visual Elements -> Barcode',
      properties: ['Barcode value', 'Barcode type', 'Size'],
      preview: '|||| ||| ||||',
    },
  },
  {
    title: 'Shape',
    type: 'shape',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Generic filled or stroked background/container block.',
    addSteps: ['Open Shapes & Layout.', 'Click Shape.', 'Resize the block.', 'Set fill, border, radius, opacity, and layering.'],
    usage: ['Section background.', 'Callout block.', 'Report band background.', 'Grouped visual container.'],
    attributes: [
      ['style.backgroundColor', 'string', 'Fill color.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
      ['style.borderRadius', 'number', 'Corner radius.'],
      ['style.opacity', 'number', 'Transparency.'],
    ],
    example: `{
  "id": "callout-bg",
  "type": "shape",
  "x": 40,
  "y": 120,
  "width": 500,
  "height": 80,
  "style": {
    "backgroundColor": "#f3f8ff",
    "borderColor": "#2468b2",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Shape',
      properties: ['Fill', 'Border', 'Radius', 'Opacity'],
      preview: 'Background block',
    },
  },
  {
    title: 'Rectangle',
    type: 'rect',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Specific rectangle primitive for boxes, borders, and report frames.',
    addSteps: ['Open Shapes & Layout.', 'Click Rectangle.', 'Position and resize.', 'Set border and fill style.'],
    usage: ['Table frame.', 'Input box outline.', 'Migrated rectangle/report frame.', 'Visual separator.'],
    attributes: [
      ['style.borderColor', 'string', 'Rectangle stroke.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
      ['style.backgroundColor', 'string', 'Optional fill.'],
    ],
    example: `{
  "id": "total-box",
  "type": "rect",
  "x": 380,
  "y": 300,
  "width": 160,
  "height": 52,
  "style": {
    "borderColor": "#111827",
    "borderWidth": 1
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Rectangle',
      properties: ['Border color', 'Border width', 'Fill'],
      preview: 'Rectangle frame',
    },
  },
  {
    title: 'Circle',
    type: 'circle',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Ellipse/circle primitive for badges, markers, and decorative report shapes.',
    addSteps: ['Open Shapes & Layout.', 'Click Circle.', 'Resize the ellipse.', 'Set fill and border.'],
    usage: ['Status badge.', 'Bullet marker.', 'Seal placeholder.', 'Chart legend marker.'],
    attributes: [
      ['style.backgroundColor', 'string', 'Circle fill.'],
      ['style.borderColor', 'string', 'Circle stroke.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
    ],
    example: `{
  "id": "status-dot",
  "type": "circle",
  "x": 40,
  "y": 330,
  "width": 18,
  "height": 18,
  "style": {
    "backgroundColor": "#0f766e"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Circle',
      properties: ['Fill', 'Border', 'Size'],
      preview: 'Circle marker',
    },
  },
  {
    title: 'Arrow',
    type: 'arrow',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Directional arrow with straight, elbow, or curved path and marker configuration.',
    addSteps: ['Open Shapes & Layout.', 'Click Arrow.', 'Choose arrowMode.', 'Configure startMarker and endMarker.'],
    usage: ['Process flows.', 'Callouts.', 'Diagram annotations.', 'Review explanations.'],
    attributes: [
      ['arrowMode', 'straight | elbow | curved', 'Path style.'],
      ['startMarker', 'none | filled | open | dot | diamond | square | circle | arrow', 'Start marker.'],
      ['endMarker', 'none | filled | open | dot | diamond | square | circle | arrow', 'End marker.'],
      ['style.borderColor', 'string', 'Stroke color.'],
    ],
    example: `{
  "id": "flow-arrow",
  "type": "arrow",
  "x": 80,
  "y": 240,
  "width": 180,
  "height": 2,
  "arrowMode": "straight",
  "endMarker": "filled"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Arrow',
      properties: ['Arrow mode', 'Start marker', 'End marker', 'Stroke'],
      preview: 'Start -> End',
    },
  },
  {
    title: 'Drawing',
    type: 'draw',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Freehand SVG-style stroke drawn with pen, highlighter, or eraser tools.',
    addSteps: ['Open Shapes & Layout.', 'Click Drawing.', 'Draw on the canvas.', 'Adjust drawTool, pathData, and stroke style.'],
    usage: ['Hand annotations.', 'Sketches.', 'Markup highlights.', 'Imported ink paths.'],
    attributes: [
      ['drawTool', 'pen | highlighter | eraser', 'Drawing tool.'],
      ['pathData', 'string', 'SVG path data.'],
      ['style.borderColor', 'string', 'Stroke color.'],
      ['style.borderWidth', 'number', 'Stroke thickness.'],
    ],
    example: `{
  "id": "ink-note",
  "type": "draw",
  "x": 40,
  "y": 500,
  "width": 120,
  "height": 60,
  "drawTool": "pen",
  "pathData": "M0,60 C40,0 80,120 120,40"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Drawing',
      properties: ['Tool', 'Path data', 'Stroke color', 'Stroke width'],
      preview: 'Freehand path',
    },
  },
  {
    title: 'Subsection',
    type: 'subsection',
    category: 'Shapes & Layout',
    status: 'Preview',
    description: 'Non-rendered designer guide used to group related content on the design surface.',
    addSteps: ['Open Shapes & Layout.', 'Click Subsection.', 'Place it around related elements.', 'Use content as a guide label.'],
    usage: ['Address block guide.', 'Report section guide.', 'Template authoring organization.', 'Non-output design notes.'],
    attributes: [
      ['content', 'string', 'Optional guide label.'],
      ['hidden', 'boolean', 'Can be hidden from the designer view.'],
      ['locked', 'boolean', 'Locks the guide.'],
    ],
    example: `{
  "id": "address-zone",
  "type": "subsection",
  "x": 40,
  "y": 120,
  "width": 300,
  "height": 160,
  "content": "Address block"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Subsection',
      properties: ['Label', 'Position', 'Size', 'Locked'],
      preview: 'Address block guide',
    },
  },
  {
    title: 'Area',
    type: 'area',
    category: 'Shapes & Layout',
    status: 'Preview',
    description: 'Non-rendered layout region that reserves or documents an intended content area.',
    addSteps: ['Open Shapes & Layout.', 'Click Area.', 'Place it where dynamic content should stay.', 'Label the area with content.'],
    usage: ['Reserved space.', 'Data repeat region.', 'Import/migration layout planning.', 'Designer handoff note.'],
    attributes: [
      ['content', 'string', 'Optional region label.'],
      ['x / y / width / height', 'number', 'Reserved region geometry.'],
      ['locked', 'boolean', 'Prevents accidental edits.'],
    ],
    example: `{
  "id": "details-area",
  "type": "area",
  "x": 40,
  "y": 220,
  "width": 500,
  "height": 280,
  "content": "Line items"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Area',
      properties: ['Label', 'Geometry', 'Locked'],
      preview: 'Reserved area',
    },
  },
  {
    title: 'Highlight',
    type: 'highlight',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Translucent rectangle or text-marker highlight for emphasizing existing content.',
    addSteps: ['Open Shapes & Layout.', 'Click Highlight.', 'Choose markMode.', 'Set translucent background color.'],
    usage: ['Review markup.', 'Important totals.', 'Imported PDF highlights.', 'Training/documentation examples.'],
    attributes: [
      ['markMode', 'rectangle | text', 'Highlight region or text-run mode.'],
      ['style.backgroundColor', 'string', 'Usually translucent highlight color.'],
      ['style.opacity', 'number', 'Transparency.'],
    ],
    example: `{
  "id": "total-highlight",
  "type": "highlight",
  "x": 380,
  "y": 300,
  "width": 160,
  "height": 22,
  "markMode": "rectangle",
  "style": {
    "backgroundColor": "rgba(255,235,59,0.5)"
  }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Highlight',
      properties: ['Mode', 'Color', 'Opacity'],
      preview: 'Highlighted total',
    },
  },
  {
    title: 'Check Mark',
    type: 'checkmark',
    category: 'Shapes & Layout',
    status: 'Ready',
    description: 'Static check/cross/dot mark used as a decorative mark, not an interactive field.',
    addSteps: ['Open Shapes & Layout.', 'Click Check Mark.', 'Choose checkState.', 'Set stroke color and size.'],
    usage: ['Static approval indicator.', 'Migrated check mark glyph.', 'Status summary.', 'Checklist output.'],
    attributes: [
      ['checkState', 'checked | cross | dot | empty', 'Displayed glyph.'],
      ['style.color', 'string', 'Glyph color.'],
      ['style.strokeWidth', 'number', 'Glyph thickness where supported.'],
    ],
    example: `{
  "id": "paid-mark",
  "type": "checkmark",
  "x": 500,
  "y": 120,
  "width": 18,
  "height": 18,
  "checkState": "checked",
  "style": { "color": "#0f766e" }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Shapes & Layout -> Check Mark',
      properties: ['State', 'Color', 'Stroke width'],
      preview: 'Check',
    },
  },
  {
    title: 'Watermark',
    type: 'watermark',
    category: 'Document',
    status: 'Ready',
    description: 'Text or image watermark with page scope, opacity, and rotation.',
    addSteps: ['Open Document Elements.', 'Click Watermark.', 'Choose text or image mode.', 'Set pageScope, opacity, and rotation.'],
    usage: ['Draft watermark.', 'Confidential stamp.', 'Brand watermark.', 'Page range-specific marks.'],
    attributes: [
      ['content', 'string', 'Watermark text or image source depending on mode.'],
      ['watermarkMode', 'text | image', 'Watermark source type.'],
      ['pageScope', 'current | all | first | last | range | odd | even', 'Pages where the watermark appears.'],
      ['pageRange', 'string', 'Range expression for range scope.'],
      ['style.opacity / style.rotation', 'number', 'Transparency and angle.'],
    ],
    example: `{
  "id": "draft",
  "type": "watermark",
  "x": 100,
  "y": 350,
  "width": 400,
  "height": 120,
  "content": "DRAFT",
  "watermarkMode": "text",
  "pageScope": "all",
  "style": { "opacity": 0.15, "rotation": -45 }
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Watermark',
      properties: ['Mode', 'Scope', 'Range', 'Opacity', 'Rotation'],
      preview: 'DRAFT',
    },
  },
  {
    title: 'Sticky Note',
    type: 'note',
    category: 'Document',
    status: 'Ready',
    description: 'Review note or callout with title, body, author, and collapsed state.',
    addSteps: ['Open Document Elements.', 'Click Sticky Note.', 'Set noteTitle, noteBody, and noteAuthor.', 'Use collapsed mode for compact review notes.'],
    usage: ['Review comment.', 'Internal instruction.', 'Migration diagnostic note.', 'Template handoff reminder.'],
    attributes: [
      ['noteTitle', 'string', 'Note heading.'],
      ['noteBody', 'string', 'Note content.'],
      ['noteAuthor', 'string', 'Author label.'],
      ['noteCollapsed', 'boolean', 'Compact/collapsed state.'],
    ],
    example: `{
  "id": "qa-note",
  "type": "note",
  "x": 360,
  "y": 120,
  "width": 180,
  "height": 90,
  "noteTitle": "QA",
  "noteBody": "Verify totals before sending.",
  "noteAuthor": "Reviewer"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Sticky Note',
      properties: ['Title', 'Body', 'Author', 'Collapsed'],
      preview: 'QA note',
    },
  },
  {
    title: 'Link',
    type: 'link',
    category: 'Document',
    status: 'Ready',
    description: 'Clickable hyperlink region with visible text and target URL.',
    addSteps: ['Open Document Elements.', 'Click Link.', 'Set content and href.', 'Choose linkTarget for HTML workflows.'],
    usage: ['External docs link.', 'Customer portal link.', 'Internal navigation target.', 'Clickable support URL.'],
    attributes: [
      ['content', 'string', 'Visible link text.'],
      ['href', 'string', 'Destination URL.'],
      ['linkTarget', '_blank | _self', 'HTML target behavior.'],
    ],
    example: `{
  "id": "portal-link",
  "type": "link",
  "x": 40,
  "y": 760,
  "width": 180,
  "height": 18,
  "content": "Open customer portal",
  "href": "https://example.com/portal",
  "linkTarget": "_blank"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Link',
      properties: ['Text', 'Href', 'Target'],
      preview: 'Open customer portal',
    },
  },
  {
    title: 'Bookmark',
    type: 'bookmark',
    category: 'Document',
    status: 'Preview',
    description: 'Named anchor for navigation outlines and cross-reference workflows.',
    addSteps: ['Open Document Elements.', 'Click Bookmark.', 'Set bookmarkName.', 'Place it near the target content.'],
    usage: ['Chapter anchor.', 'PDF outline target.', 'Cross-reference destination.', 'Word navigation marker.'],
    attributes: [
      ['bookmarkName', 'string', 'Anchor/outline name.'],
      ['bookmarkTarget', 'string', 'Optional explicit target reference.'],
      ['x / y', 'number', 'Anchor location.'],
    ],
    example: `{
  "id": "chapter-1-bookmark",
  "type": "bookmark",
  "x": 40,
  "y": 80,
  "width": 10,
  "height": 10,
  "bookmarkName": "chapter-1"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Bookmark',
      properties: ['Name', 'Target', 'Position'],
      preview: 'Bookmark anchor',
    },
  },
  {
    title: 'Comment',
    type: 'comment',
    category: 'Document',
    status: 'Preview',
    description: 'Review comment element with author, body, and timestamp metadata.',
    addSteps: ['Open Document Elements.', 'Click Comment.', 'Set commentAuthor and commentText.', 'Use commentDate for imported or audited comments.'],
    usage: ['Reviewer feedback.', 'DOCX comment migration.', 'QA notes.', 'Approval trace.'],
    attributes: [
      ['commentAuthor', 'string', 'Reviewer/author.'],
      ['commentText', 'string', 'Comment body.'],
      ['commentDate', 'string', 'ISO timestamp.'],
    ],
    example: `{
  "id": "legal-comment",
  "type": "comment",
  "x": 380,
  "y": 220,
  "width": 180,
  "height": 60,
  "commentAuthor": "Legal",
  "commentText": "Confirm this clause."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Comment',
      properties: ['Author', 'Text', 'Date'],
      preview: 'Legal comment',
    },
  },
  {
    title: 'Footnote',
    type: 'footnote',
    category: 'Document',
    status: 'Preview',
    description: 'Inline footnote reference with note body; strongest native support is in DOCX.',
    addSteps: ['Open Text or Document Elements.', 'Click Footnote.', 'Set footnoteText and optional footnoteRef.', 'Place next to referenced content.'],
    usage: ['Legal/source reference.', 'Book manuscript notes.', 'Academic-style documents.', 'DOCX import/export fidelity.'],
    attributes: [
      ['footnoteText', 'string', 'Footnote body.'],
      ['footnoteRef', 'string', 'Optional marker/reference.'],
    ],
    example: `{
  "id": "source-footnote",
  "type": "footnote",
  "x": 240,
  "y": 210,
  "width": 160,
  "height": 16,
  "footnoteText": "Source: internal data."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Footnote',
      properties: ['Text', 'Reference', 'Position'],
      preview: 'Footnote ref',
    },
  },
  {
    title: 'Endnote',
    type: 'endnote',
    category: 'Document',
    status: 'Preview',
    description: 'Inline endnote reference collected at the end of the document where supported.',
    addSteps: ['Open Text or Document Elements.', 'Click Endnote.', 'Set footnoteText as the endnote body.', 'Place next to referenced content.'],
    usage: ['Long-form references.', 'Book endnotes.', 'DOCX-native note workflows.', 'PDF fallback note rendering.'],
    attributes: [
      ['footnoteText', 'string', 'Endnote body.'],
      ['footnoteRef', 'string', 'Optional marker/reference.'],
    ],
    example: `{
  "id": "appendix-endnote",
  "type": "endnote",
  "x": 260,
  "y": 260,
  "width": 160,
  "height": 16,
  "footnoteText": "Published reference."
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Text Elements -> Endnote',
      properties: ['Text', 'Reference', 'Position'],
      preview: 'Endnote ref',
    },
  },
  {
    title: 'Content Control',
    type: 'contentcontrol',
    category: 'Document',
    status: 'Preview',
    description: 'Structured Word content control with PDF fallback box for template-driven content.',
    addSteps: ['Open Document Elements.', 'Click Content Control.', 'Choose contentControlType.', 'Set tag and title for code integration.'],
    usage: ['Word SDT fields.', 'Template placeholders.', 'Structured authoring.', 'DOCX import/export workflows.'],
    attributes: [
      ['contentControlType', 'richText | plainText | datePicker | comboBox | picture', 'Control kind.'],
      ['contentControlTag', 'string', 'Programmatic tag.'],
      ['contentControlTitle', 'string', 'Visible title in Word.'],
    ],
    example: `{
  "id": "customer-control",
  "type": "contentcontrol",
  "x": 40,
  "y": 520,
  "width": 240,
  "height": 24,
  "contentControlType": "plainText",
  "contentControlTag": "customer",
  "contentControlTitle": "Customer"
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Content Control',
      properties: ['Type', 'Tag', 'Title'],
      preview: 'Customer control',
    },
  },
  {
    title: 'Page Boundary',
    type: 'pageboundary',
    category: 'Document',
    status: 'Preview',
    description: 'Designer guide for page edges or margin boundaries; not rendered to final output.',
    addSteps: ['Open Document Elements.', 'Click Page Boundary.', 'Place or resize it to mark page regions.', 'Use locked mode to keep it stable.'],
    usage: ['Margin guide.', 'Import diagnostics.', 'Layout debugging.', 'Page-edge planning.'],
    attributes: [
      ['x / y / width / height', 'number', 'Boundary geometry.'],
      ['locked', 'boolean', 'Prevents accidental movement.'],
      ['hidden', 'boolean', 'Hides the guide from designer view.'],
    ],
    example: `{
  "id": "print-boundary",
  "type": "pageboundary",
  "x": 0,
  "y": 0,
  "width": 595,
  "height": 842,
  "locked": true
}`,
    visual: {
      title: 'Designer usage',
      toolbar: 'Document Elements -> Page Boundary',
      properties: ['Geometry', 'Locked', 'Hidden'],
      preview: 'Page edge guide',
    },
  },
];

const codeSdkExamples = [
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Text',
    status: 'Ready',
    description: 'Draw headings, labels, paragraphs, totals, and bound text values from C#.',
    preview: { kind: 'text', value: 'Invoice #1001' },
    csharp: `var document = PxaDocument.Create();
var page = document.Pages.Add();

page.Text("Invoice #1001")
    .At(40, 40)
    .Size(300, 32)
    .FontSize(18)
    .Bold()
    .Color("#111827");`,
    json: `{
  "type": "text",
  "x": 40,
  "y": 40,
  "width": 300,
  "height": 32,
  "content": "Invoice #1001",
  "style": { "fontSize": 18, "fontWeight": "bold", "color": "#111827" }
}`,
    model: 'Document -> Page -> TextElement -> TextStyle',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Image',
    status: 'Ready',
    description: 'Place logos, stamps, imported image assets, and document visuals.',
    preview: { kind: 'image', value: 'LOGO' },
    csharp: `var document = PxaDocument.Create();
var page = document.Pages.Add();

page.Image("https://example.com/logo.png")
    .At(40, 28)
    .Size(120, 48)
    .Fit(ImageFit.Contain)
    .PreserveAspectRatio();`,
    json: `{
  "type": "image",
  "x": 40,
  "y": 28,
  "width": 120,
  "height": 48,
  "content": "https://example.com/logo.png",
  "fitMode": "contain",
  "preserveAspectRatio": true
}`,
    model: 'Document -> Page -> ImageElement -> ImageFit',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Table',
    status: 'Ready',
    description: 'Create invoice lines, report detail rows, totals, and data-driven tabular output.',
    preview: { kind: 'table', value: 'Item | Qty | Total' },
    csharp: `var table = page.Table()
    .At(40, 180)
    .Size(500, 120)
    .HeaderRow(true)
    .Columns(300, 80, 120);

table.Row("Item", "Qty", "Total");
table.Row("Coffee", "2", "19.80");`,
    json: `{
  "type": "table",
  "x": 40,
  "y": 180,
  "width": 500,
  "height": 120,
  "headerRow": true,
  "cellData": [["Item", "Qty", "Total"], ["Coffee", "2", "19.80"]]
}`,
    model: 'Document -> Page -> TableElement -> Rows -> Cells',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Chart',
    status: 'Preview',
    description: 'Render typed multi-series charts as sharp PDF vectors with a controlled raster fallback.',
    preview: { kind: 'chart', value: 'Q1 Q2' },
    csharp: `page.Chart(ChartType.Bar)
    .At(40, 340)
    .Size(320, 200)
    .Labels("Q1", "Q2")
    .Dataset("Sales", 10, 20);`,
    json: `{
  "type": "chart",
  "x": 40,
  "y": 340,
  "width": 320,
  "height": 200,
  "chart": {
    "schemaVersion": 2,
    "type": "bar",
    "categories": ["Q1", "Q2"],
    "series": [{ "id": "sales", "name": "Sales", "values": [10, 20], "color": "#2563eb" }]
  }
}`,
    model: 'Document -> Page -> ChartElement -> ChartDefinition -> Series',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Barcode and QR',
    status: 'Ready',
    description: 'Generate machine-readable codes for invoices, tickets, labels, and verification workflows.',
    preview: { kind: 'barcode', value: '4006381333931' },
    csharp: `page.Barcode("4006381333931", BarcodeType.Ean13)
    .At(330, 470)
    .Size(200, 60);

page.QrCode("https://pay.example/invoice/1001")
    .At(430, 360)
    .Size(96, 96);`,
    json: `{
  "type": "barcode",
  "x": 330,
  "y": 470,
  "width": 200,
  "height": 60,
  "barcodeValue": "4006381333931",
  "barcodeType": "EAN13"
}`,
    model: 'Document -> Page -> BarcodeElement / QrCodeElement',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Form Field',
    status: 'Preview',
    description: 'Add fillable fields, required input, placeholders, and form metadata.',
    preview: { kind: 'field', value: 'Customer name' },
    csharp: `page.TextField("customerName")
    .At(40, 420)
    .Size(240, 28)
    .Label("Customer name")
    .Placeholder("Enter name")
    .Required();`,
    json: `{
  "type": "field",
  "x": 40,
  "y": 420,
  "width": 240,
  "height": 28,
  "fieldName": "customerName",
  "fieldLabel": "Customer name",
  "placeholder": "Enter name",
  "required": true
}`,
    model: 'Document -> Page -> FormFieldElement -> Validation',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Page Content',
    title: 'Line and Shape',
    status: 'Ready',
    description: 'Draw dividers, boxes, backgrounds, status markers, and report frames.',
    preview: { kind: 'shape', value: 'Divider + box' },
    csharp: `page.Line()
    .From(40, 156)
    .To(540, 156)
    .Stroke("#9ca3af", 1);

page.Rectangle()
    .At(40, 120)
    .Size(500, 80)
    .Fill("#f3f8ff")
    .Stroke("#2468b2", 1);`,
    json: `{
  "type": "line",
  "x": 40,
  "y": 156,
  "width": 500,
  "height": 1,
  "style": { "borderColor": "#9ca3af", "borderWidth": 1 }
}`,
    model: 'Document -> Page -> LineElement / ShapeElement -> StrokeStyle',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Styling',
    title: 'Watermark',
    status: 'Preview',
    description: 'Apply document-level watermark behavior before generated PDF output is saved or streamed.',
    preview: { kind: 'watermark', value: 'DRAFT' },
    csharp: `var document = PxaDocument.Create();

document.Watermark("DRAFT")
    .AllPages()
    .Opacity(0.15)
    .Rotate(-45);`,
    json: `{
  "type": "watermark",
  "content": "DRAFT",
  "watermarkMode": "text",
  "pageScope": "all",
  "style": { "opacity": 0.15, "rotation": -45 }
}`,
    model: 'Document -> WatermarkElement -> PageScope -> Style',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Generator',
    title: 'Document Setup and Save',
    status: 'Ready',
    description: 'Create a PDF document, configure page size, metadata, viewer preferences, encryption, and save or stream the result.',
    preview: { kind: 'text', value: 'A4 invoice.pdf' },
    csharp: `var document = PXA.Generator.Pdf.CreateDocument();
document.Info.Title = "Invoice #1001";
document.Info.Author = "PXA";

var page = document.AddPage(PdfPagePreset.A4);
page.DrawTextFromTop("Invoice #1001", 40, 40, 18);

var bytes = document.ToBytes(new PdfSaveOptions
{
    Encryption = new PdfEncryptionOptions
    {
        UserPassword = "view",
        OwnerPassword = "owner"
    }
});`,
    json: `{
  "type": "pdfDocument",
  "pagePreset": "A4",
  "metadata": { "title": "Invoice #1001", "author": "PXA" },
  "save": { "target": "bytes", "encryption": true }
}`,
    model: 'PdfDocument -> Info -> Page -> PdfSaveOptions -> Bytes',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Styling',
    title: 'Colors Fonts and Strokes',
    status: 'Ready',
    description: 'Use standard fonts, RGB/CMYK/gray colors, opacity, line joins, caps, stroke width, and text alignment.',
    preview: { kind: 'shape', value: 'Styled text + frame' },
    csharp: `var page = document.AddPage();

page.DrawTextFromTop(
    "Styled headline",
    40,
    56,
    20,
    new PdfDrawTextOptions
    {
        FillColor = PdfColor.FromRgb(17, 24, 39),
        Font = PdfStandardFonts.FromStyle(PdfFontFamily.Helvetica, bold: true)
    });

page.DrawRectangle(
    40,
    96,
    240,
    80,
    fill: true,
    strokeColor: new PdfCmykColor(1, 0.4, 0, 0),
    fillColor: new PdfGrayColor(0.95),
    strokeStyle: new PdfStrokeStyle
    {
        LineWidth = 1.5,
        LineJoin = PdfLineJoinStyle.Round
    });`,
    json: `{
  "type": "style",
  "text": { "font": "Helvetica-Bold", "color": "#111827", "fontSize": 20 },
  "shape": { "stroke": "cmyk(1,0.4,0,0)", "fill": "gray(0.95)", "width": 1.5 }
}`,
    model: 'PdfPage -> Text/Shape -> PdfDrawTextOptions/PdfStrokeStyle -> Color/Font',
  },
  {
    category: 'PDF SDK',
    subcategory: 'PDF Analysis / Diagnostics',
    title: 'Analysis and Diagnostics',
    status: 'Ready',
    description: 'Inspect generated documents for pages with text, images, links, shapes, rotations, watermarks, and generation diagnostics.',
    preview: { kind: 'table', rows: [['Query', 'Result'], ['Pages with text', '1, 2'], ['Pages with links', '2']] },
    csharp: `var document = PXA.Generator.Pdf.CreateDocument();
var page = document.AddPage();
page.DrawTextFromTop("Diagnostics sample", 40, 40, 12);

IReadOnlyList<int> textPages = document.GetPagesWithText();
IReadOnlyList<int> linkPages = document.GetPagesWithLinks();
IReadOnlyList<int> shapePages = document.GetPagesWithShapes();

_ = document.ToBytes(new PdfSaveOptions { CollectDiagnostics = true });
PdfGenerationDiagnostics? diagnostics = document.LastDiagnostics;`,
    json: `{
  "type": "diagnostics",
  "queries": ["GetPagesWithText", "GetPagesWithLinks", "GetPagesWithShapes"],
  "result": { "textPages": [1], "linkPages": [], "shapePages": [] }
}`,
    model: 'PdfDocument -> PageCoverageQueries -> PdfGenerationDiagnostics',
  },
  {
    category: 'Template SDK',
    subcategory: 'Data Binding and Localization',
    title: 'Page Settings and Metadata',
    status: 'Ready',
    description: 'Configure page size, orientation, units, margins, background, page numbers, metadata, watermark, and custom properties from C#.',
    preview: { kind: 'table', rows: [['Setting', 'Value'], ['Size', 'A4 portrait'], ['Margins', '36 pt'], ['Metadata', 'Invoice']] },
    csharp: `var design = new DesignExportDto
{
    Name = "Invoice template",
    PageSettings = new PageSettingsDto
    {
        Width = 595,
        Height = 842,
        Orientation = "portrait",
        Unit = "pt",
        BackgroundColor = "#ffffff",
        Margins = new MarginsDto
        {
            Top = 36,
            Right = 36,
            Bottom = 42,
            Left = 36
        },
        PageNumbering = new PageNumberingDto
        {
            Format = "current-total",
            Prefix = "Page ",
            Placement = "bottom-center",
            ShowOnFirstPage = true
        },
        Metadata = new PdfMetadataDto
        {
            Title = "Invoice #1001",
            Author = "PXA",
            Subject = "Customer invoice",
            Keywords = "invoice,pdf,pxa"
        },
        GlobalWatermark = new GlobalWatermarkDto
        {
            Mode = "text",
            Content = "DRAFT",
            PageScope = "all",
            Rotation = -45,
            FontSize = 48,
            Color = "#9ca3af"
        },
        CustomProperties =
        [
            new CustomDocumentPropertyDto
            {
                Name = "Department",
                Value = "Finance",
                Type = "text"
            }
        ]
    }
};`,
    json: `{
  "pageSettings": {
    "width": 595,
    "height": 842,
    "orientation": "portrait",
    "unit": "pt",
    "margins": { "top": 36, "right": 36, "bottom": 42, "left": 36 },
    "pageNumbering": { "format": "current-total", "placement": "bottom-center" },
    "metadata": { "title": "Invoice #1001", "author": "PXA" },
    "globalWatermark": { "mode": "text", "content": "DRAFT" }
  }
}`,
    model: 'DesignExportDto -> PageSettingsDto -> Margins/PageNumbering/Metadata/Watermark',
  },
  {
    category: 'Template SDK',
    subcategory: 'Data Binding and Localization',
    title: 'Document Encryption and Protection',
    status: 'Ready',
    description: 'Configure PDF encryption permissions and document protection settings from C# before export. The Designer also provides local password-strength guidance without sending the password to the server.',
    preview: { kind: 'table', rows: [['Security', 'Value'], ['Encryption', 'AES256'], ['Print', 'allowed'], ['Copy', 'blocked']] },
    csharp: `var design = new DesignExportDto
{
    Name = "Protected invoice",
    PageSettings = new PageSettingsDto
    {
        Encryption = new PdfEncryptionDto
        {
            Enabled = true,
            Algorithm = "Aes256",
            UserPassword = "view",
            OwnerPassword = "owner",
            Permissions = new PdfEncryptionPermissionsDto
            {
                Print = true,
                PrintHighResolution = false,
                Modify = false,
                Copy = false,
                Annotate = false,
                FillForms = true,
                ExtractAccessibility = true,
                Assemble = false
            }
        },
        Protection = new DocumentProtectionDto
        {
            Enabled = true,
            Mode = "readOnly",
            PasswordHash = "stored-password-hash"
        }
    }
};`,
    json: `{
  "pageSettings": {
    "encryption": {
      "enabled": true,
      "algorithm": "Aes256",
      "userPassword": "view",
      "ownerPassword": "owner",
      "permissions": {
        "print": true,
        "modify": false,
        "copy": false,
        "annotate": false,
        "fillForms": true
      }
    },
    "protection": {
      "enabled": true,
      "mode": "readOnly"
    }
  }
}`,
    model: 'DesignExportDto -> PageSettingsDto -> PdfEncryptionDto/DocumentProtectionDto',
  },
  {
    category: 'Template SDK',
    subcategory: 'Data Binding and Localization',
    title: 'Localized Template Properties',
    status: 'Ready',
    description: 'Create a localized design model from C# with active languages, localized placeholders, RTL text, and target-language export settings.',
    preview: { kind: 'table', rows: [['Language', 'SUBJECT'], ['de', 'Rechnung'], ['en', 'Invoice'], ['ar', 'فاتورة']] },
    csharp: `var design = new DesignExportDto
{
    Name = "Localized invoice",
    PageSettings = new PageSettingsDto
    {
        SystemLanguage = "de",
        ActiveLanguages = ["de", "en", "ar"],
        TargetLanguage = "ar",
        LocalizedProperties =
        [
            new LocalizedPropertyDto
            {
                Key = "SUBJECT",
                Scope = "global",
                LocalizedValues =
                {
                    ["de"] = "Rechnung",
                    ["en"] = "Invoice",
                    ["ar"] = "فاتورة"
                }
            }
        ]
    },
    Pages =
    [
        new PageDto
        {
            Id = "page-1",
            Elements =
            [
                new ElementDto
                {
                    Id = "headline",
                    Type = "text",
                    Content = "{{SUBJECT}} #1001",
                    Language = "ar",
                    TextDirection = "rtl",
                    ElementLanguage = "ar",
                    X = 330,
                    Y = 40,
                    Width = 220,
                    Height = 32
                }
            ]
        }
    ]
};`,
    json: `{
  "pageSettings": {
    "systemLanguage": "de",
    "activeLanguages": ["de", "en", "ar"],
    "targetLanguage": "ar",
    "localizedProperties": [{ "key": "SUBJECT", "scope": "global" }]
  },
  "element": {
    "content": "{{SUBJECT}} #1001",
    "language": "ar",
    "textDirection": "rtl",
    "elementLanguage": "ar"
  }
}`,
    model: 'DesignExportDto -> PageSettingsDto -> LocalizedPropertyDto -> ElementDto',
  },
  {
    category: 'Template SDK',
    subcategory: 'Data Binding and Localization',
    title: 'RTL and LTR Text Direction',
    status: 'Ready',
    description: 'Create left-to-right and right-to-left text elements from C# with explicit language tags, text direction, and language-scoped positioning.',
    preview: { kind: 'table', rows: [['Direction', 'Example'], ['LTR', 'Invoice #1001'], ['RTL', 'فاتورة #1001']] },
    csharp: `var design = new DesignExportDto
{
    Name = "Direction-aware invoice",
    PageSettings = new PageSettingsDto
    {
        SystemLanguage = "en",
        ActiveLanguages = ["en", "ar"]
    },
    Pages =
    [
        new PageDto
        {
            Id = "page-1",
            Elements =
            [
                new ElementDto
                {
                    Id = "headline-en",
                    Type = "text",
                    Content = "Invoice #1001",
                    Language = "en",
                    TextDirection = "ltr",
                    ElementLanguage = "en",
                    X = 40,
                    Y = 40,
                    Width = 240,
                    Height = 32
                },
                new ElementDto
                {
                    Id = "headline-ar",
                    Type = "text",
                    Content = "فاتورة #1001",
                    Language = "ar",
                    TextDirection = "rtl",
                    ElementLanguage = "ar",
                    X = 315,
                    Y = 40,
                    Width = 240,
                    Height = 32,
                    LangOverrides = new Dictionary<string, LangOverrideDto>
                    {
                        ["ar"] = new() { X = 315, Y = 40, Rotation = 0 }
                    }
                }
            ]
        }
    ]
};`,
    json: `{
  "ltr": {
    "language": "en",
    "textDirection": "ltr",
    "elementLanguage": "en"
  },
  "rtl": {
    "language": "ar",
    "textDirection": "rtl",
    "elementLanguage": "ar",
    "langOverrides": { "ar": { "x": 315, "y": 40, "rotation": 0 } }
  }
}`,
    model: 'DesignExportDto -> ElementDto.Language/TextDirection/ElementLanguage/LangOverrides',
  },
  {
    category: 'Template SDK',
    subcategory: 'Data Binding and Localization',
    title: 'Bound Elements and Repeated Rows',
    status: 'Ready',
    description: 'Create data-bound element definitions from C# with binding paths, expressions, conditional visibility, and repeat metadata.',
    preview: { kind: 'table', rows: [['Binding', 'Use'], ['customer.name', 'Text'], ['invoice.items', 'Repeat']] },
    csharp: `var design = new DesignExportDto
{
    Name = "Bound invoice",
    Pages =
    [
        new PageDto
        {
            Id = "page-1",
            Elements =
            [
                new ElementDto
                {
                    Id = "customer-name",
                    Type = "text",
                    Content = "Customer: {{customer.name}}",
                    Binding = "customer.name",
                    Expression = "customer.name",
                    VisibleExpression = "customer.active == true",
                    X = 40,
                    Y = 64,
                    Width = 260,
                    Height = 24
                },
                new ElementDto
                {
                    Id = "items",
                    Type = "table",
                    Binding = "invoice.items",
                    Repeat = new RepeatDto
                    {
                        DataPath = "invoice.items",
                        TemplateId = "item-row-template"
                    },
                    CellData = [["Item", "Qty", "Total"]],
                    HeaderRow = true,
                    X = 40,
                    Y = 120,
                    Width = 500,
                    Height = 160
                }
            ]
        }
    ]
};`,
    json: `{
  "text": {
    "binding": "customer.name",
    "expression": "customer.name",
    "visibleExpression": "customer.active == true"
  },
  "table": {
    "binding": "invoice.items",
    "repeat": {
      "dataPath": "invoice.items",
      "rowTemplate": ["{{name}}", "{{quantity}}", "{{total}}"]
    }
  }
}`,
    model: 'DesignExportDto -> ElementDto.Binding/Expression/VisibleExpression/Repeat',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Workbook and Sheet Authoring',
    title: 'Workbook and Cells',
    status: 'Ready',
    description: 'Create workbooks, sheets, cells, values, styles, hyperlinks, comments, and number formats from C#.',
    preview: { kind: 'spreadsheet', value: 'Invoice workbook', rows: [['A1', 'Invoice #1001'], ['B2', '19.80']] },
    csharp: `var workbook = PXA.Generator.Spreadsheet.CreateWorkbook("Invoice workbook");
var sheet = workbook.AddSheet("Invoice");

sheet.Cell("A1")
    .Value("Invoice #1001")
    .Style(style => style.Bold().FontSize(16));

sheet.Cell("B2")
    .Value(19.80)
    .NumberFormat("#,##0.00");`,
    json: `{
  "type": "spreadsheet",
  "name": "Invoice workbook",
  "sheets": [
    {
      "name": "Invoice",
      "cells": [
        { "address": "A1", "value": "Invoice #1001", "style": { "bold": true, "fontSize": 16 } },
        { "address": "B2", "value": 19.8, "numberFormat": "#,##0.00" }
      ]
    }
  ]
}`,
    model: 'Workbook -> Worksheet -> Cell -> Value -> Style',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Cells Styles and Columns',
    title: 'Cell Styling and Columns',
    status: 'Ready',
    description: 'Style individual cells and configure worksheet columns with width, hidden state, and outline levels.',
    preview: { kind: 'spreadsheet', value: 'Styled sheet', rows: [['A1', 'Header'], ['Column B', 'Hidden'], ['Column C', 'Outline 1']] },
    csharp: `var workbook = PXA.Generator.Spreadsheet.CreateWorkbook("Styled workbook");
var sheet = workbook.AddSheet("Styled");

sheet.Cell("A1")
    .Value("Header")
    .Style(style => style
        .Bold()
        .Italic()
        .Font("Inter")
        .FontSize(14)
        .Color("#111827")
        .Background("#e8f2ff")
        .Align("center"));

sheet.Cell("A2").Comment("Review this value");
sheet.Cell("A3").Hyperlink("https://powerdoxautomation.com");
sheet.Column(2).Width(24).Hidden();
sheet.Column(3).OutlineLevel(1);`,
    json: `{
  "type": "spreadsheetStyle",
  "cell": {
    "address": "A1",
    "style": {
      "bold": true,
      "italic": true,
      "fontFamily": "Inter",
      "fontSize": 14,
      "color": "#111827",
      "backgroundColor": "#e8f2ff",
      "textAlign": "center"
    }
  },
  "columns": [
    { "index": 2, "width": 24, "hidden": true },
    { "index": 3, "outlineLevel": 1 }
  ]
}`,
    model: 'Worksheet -> Cell/Column -> Style/Comment/Hyperlink/Layout',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Formulas Layout and Ranges',
    title: 'Formula and Layout',
    status: 'Ready',
    description: 'Use formulas, merged ranges, frozen panes, column widths, and XLSX export from C#.',
    preview: { kind: 'spreadsheet', value: 'Summary sheet', rows: [['A1:C1', 'Merged title'], ['C4', '=SUM(C2:C3)']] },
    csharp: `var workbook = PXA.Generator.Spreadsheet.CreateWorkbook("Monthly summary");
var sheet = workbook.AddSheet("Summary");

sheet.Range("A1:C1").Merge();
sheet.Cell("A1").Value("Monthly summary");
sheet.Cell("C4").Formula("SUM(C2:C3)");
sheet.Column(1).Width(24);
sheet.Freeze(1, 0);

var xlsx = workbook.ToXlsx(recalculate: true);`,
    json: `{
  "type": "spreadsheet",
  "name": "Monthly summary",
  "sheets": [
    {
      "name": "Summary",
      "merges": ["A1:C1"],
      "frozenRows": 1,
      "columns": [{ "index": 1, "width": 24 }],
      "cells": [
        { "address": "A1", "value": "Monthly summary" },
        { "address": "C4", "formula": "SUM(C2:C3)" }
      ]
    }
  ]
}`,
    model: 'Workbook -> Worksheet -> Range/Column/Cell -> XlsxExport',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Import Export and File IO',
    title: 'Import Export and Save',
    status: 'Ready',
    description: 'Import XLSX/XLS/CSV content, export workbook bytes, or save as XLSX, XLS, CSV, or TSV by file extension.',
    preview: { kind: 'spreadsheet', value: 'File IO', rows: [['Format', 'API'], ['XLSX', 'ToXlsx / Save'], ['CSV', 'CsvSheetIo']] },
    csharp: `var workbook = PXA.Generator.Spreadsheet.CreateWorkbook("Export sample");
var sheet = workbook.AddSheet("Data");
sheet.Cell("A1").Value("Customer");
sheet.Cell("B1").Value("Total");

byte[] xlsx = workbook.ToXlsx(recalculate: true);
workbook.Save("report.xlsx");
workbook.Save("report.xls");
workbook.Save("report.csv");
workbook.Save("report.tsv");

var importedXlsx = new ExcelWorkbookImporter().Import(stream, "source.xlsx");
var importedXls = new XlsWorkbookIo().Import(stream, "source.xls");
var csvSheet = CsvSheetIo.FromCsv(csvText, "CsvData");`,
    json: `{
  "type": "spreadsheetFileIo",
  "export": ["xlsx", "xls", "csv", "tsv"],
  "import": ["xlsx", "xls", "csv"],
  "apis": ["ToXlsx", "Save", "ExcelWorkbookImporter", "XlsWorkbookIo", "CsvSheetIo"]
}`,
    model: 'PxaWorkbook -> SpreadsheetDto -> Import/Export IO',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Operations Validation and Calculation',
    title: 'Validate Calculate and Sort',
    status: 'Ready',
    description: 'Validate workbook structure, calculate formulas, sort ranges, and run find/replace operations.',
    preview: { kind: 'spreadsheet', value: 'Workbook checks', rows: [['Task', 'API'], ['Validate', 'SpreadsheetValidator'], ['Sort', 'SortRange'], ['Replace', 'FindReplace']] },
    csharp: `SpreadsheetDto model = workbook.ToWorkbook();

var validation = new SpreadsheetValidator().Validate(model);
SpreadsheetDto calculated = new SpreadsheetCalculator().Calculate(model);

var operations = new SpreadsheetOperations();
operations.SortRange(model.Sheets[0], "A2:C20", keyColumnOffset: 2, ascending: false);
int replacements = operations.FindReplace(model, "Draft", "Final", matchCase: false);`,
    json: `{
  "type": "spreadsheetOperations",
  "validation": "SpreadsheetValidator.Validate",
  "calculation": "SpreadsheetCalculator.Calculate",
  "operations": ["SortRange", "FindReplace"]
}`,
    model: 'SpreadsheetDto -> Validator/Calculator/Operations -> Updated Workbook',
  },
  {
    category: 'Spreadsheet SDK',
    subcategory: 'Data and Design Conversion',
    title: 'Rows Fill and Design Conversion',
    status: 'Ready',
    description: 'Create sheets from row data, fill workbook placeholders from data, and convert a sheet into editable PXA design output.',
    preview: { kind: 'spreadsheet', value: 'Data mapping', rows: [['Source', 'Output'], ['Rows', 'SheetDto'], ['Workbook', 'DesignExportDto']] },
    csharp: `var data = new SpreadsheetData();
SheetDto sheet = data.FromRows(rows, "Orders");

SpreadsheetDto workbookModel = workbook.ToWorkbook();
int filledCells = data.Fill(workbookModel, payload);

DesignExportDto design = new SpreadsheetToDesignConverter()
    .Convert(workbookModel, sheetIndex: 0, gridlines: true);`,
    json: `{
  "type": "spreadsheetDataConversion",
  "fromRows": "SheetDto",
  "fill": "Dictionary data to workbook cells",
  "convert": "SpreadsheetDto to DesignExportDto"
}`,
    model: 'Rows/Data -> SheetDto/SpreadsheetDto -> DesignExportDto',
  },
];

const pdfSdkSubgroups = [
  {
    title: 'PDF Generator',
    description: 'Document creation, page setup, metadata, viewer preferences, encryption, save options, streaming, bookmarks, sections, named destinations, table of contents, headers, footers, page numbers, watermarks, and flow layout.',
  },
  {
    title: 'PDF Page Content',
    description: 'Page-level content: text, paragraphs, lists, tables, images, charts, barcodes, QR codes, form fields, annotations, links, lines, rectangles, circles, polygons, and Bezier curves.',
  },
  {
    title: 'PDF Styling',
    description: 'Fonts, embedded fonts, RGB/CMYK/gray colors, opacity, stroke styles, line caps, line joins, alignment, vertical alignment, text decorations, RTL helpers, and layout styling.',
  },
  {
    title: 'PDF Analysis / Diagnostics',
    description: 'Coverage and diagnostics queries for pages with text, images, links, shapes, rotations, transparency, boundaries, bookmarks, named destinations, watermarks, headers, footers, and generation diagnostics.',
  },
].map((subgroup) => ({
  ...subgroup,
  examples: codeSdkExamples.filter((item) => item.category === 'PDF SDK' && item.subcategory === subgroup.title),
}));

const spreadsheetSdkSubgroups = [
  {
    title: 'Workbook and Sheet Authoring',
    description: 'Workbook creation, naming, sheet creation, sheet lookup by index/name, sheet count, defined names, JSON model access, and workbook save/export entry points.',
  },
  {
    title: 'Cells Styles and Columns',
    description: 'A1 and row/column cell access, values, formulas, number formats, comments, hyperlinks, bold/italic, font, font size, text color, background color, alignment, column width, hidden columns, and outline levels.',
  },
  {
    title: 'Formulas Layout and Ranges',
    description: 'Formula cells, merged ranges, range helpers, frozen rows/columns, column layout, and recalculated XLSX export.',
  },
  {
    title: 'Import Export and File IO',
    description: 'XLSX export/import, XLS export/import, CSV/TSV save, CSV parsing, and extension-based workbook saving.',
  },
  {
    title: 'Operations Validation and Calculation',
    description: 'Workbook validation, formula calculation, range sorting, and workbook-wide find/replace operations.',
  },
  {
    title: 'Data and Design Conversion',
    description: 'Sheet creation from row data, data fill into workbook cells, and conversion from SpreadsheetDto into editable PXA design output.',
  },
].map((subgroup) => ({
  ...subgroup,
  examples: codeSdkExamples.filter((item) => item.category === 'Spreadsheet SDK' && item.subcategory === subgroup.title),
}));

const templateSdkSubgroups = [
  {
    title: 'Data Binding and Localization',
    description: 'C# construction of localized DesignExportDto models, language-scoped elements, binding paths, expressions, visibility conditions, and repeat metadata.',
  },
].map((subgroup) => ({
  ...subgroup,
  examples: codeSdkExamples.filter((item) => item.category === 'Template SDK' && item.subcategory === subgroup.title),
}));

const codeSdkGroups = [
  {
    title: 'PDF SDK',
    description: 'PDF generation comes first: page setup, content elements, forms, annotations, document navigation, watermarks, and save options.',
    subgroups: pdfSdkSubgroups,
    examples: codeSdkExamples.filter((item) => item.category === 'PDF SDK'),
  },
  {
    title: 'Spreadsheet SDK',
    description: 'Spreadsheet examples come after PDF: workbook creation, cells, formulas, ranges, formatting, validation, calculation, import, and XLSX export.',
    subgroups: spreadsheetSdkSubgroups,
    examples: codeSdkExamples.filter((item) => item.category === 'Spreadsheet SDK'),
  },
  {
    title: 'Template SDK',
    description: 'Template model examples for data binding, localization, language-specific elements, expressions, visibility rules, and repeat metadata.',
    subgroups: templateSdkSubgroups,
    examples: codeSdkExamples.filter((item) => item.category === 'Template SDK'),
  },
];

const exportSdkDocs = [
  {
    title: 'Word / Export',
    status: 'Ready',
    text: 'Document export covers DOCX generation, rich text, comments, footnotes, custom properties, document protection, and digital signing.',
    points: ['PXA.Generator.Word.Export(design)', 'WordDocumentExporter', 'DigitalSigningService', 'DocumentProtectionService'],
    examples: [
      {
        title: 'Export DOCX from Design JSON',
        csharp: `DesignExportDto design = LoadDesign();

byte[] docx = PXA.Generator.Word.Export(
    design,
    new ExportOptions
    {
        Format = "docx",
        FileName = "invoice.docx"
    });

File.WriteAllBytes("invoice.docx", docx);`,
        output: 'DOCX bytes with pages, text, tables, images, rich text where supported, comments, footnotes, custom properties, protection, and named styles.',
        model: 'DesignExportDto -> PXA.Generator.Word.Export -> DOCX',
      },
      {
        title: 'Use WordDocumentExporter directly',
        csharp: `DesignExportDto design = LoadDesign();

var exporter = new WordDocumentExporter();
byte[] docx = exporter.Export(design);

IExporterCapabilities capabilities = exporter.Capabilities;`,
        output: 'Direct exporter access when the application needs capabilities or dependency-injected exporter boundaries.',
        model: 'DesignExportDto -> WordDocumentExporter -> IExporterCapabilities/DOCX',
      },
      {
        title: 'Sign generated DOCX',
        csharp: `using var docxStream = File.OpenRead("invoice.docx");
byte[] pfx = File.ReadAllBytes("certificate.pfx");

byte[] signedDocx = DigitalSigningService.SignDocx(
    docxStream,
    pfx,
    pfxPassword: "secret");

File.WriteAllBytes("invoice.signed.docx", signedDocx);`,
        output: 'Signed DOCX package after generation. Use after export, because signing is applied to the final document package.',
        model: 'DOCX stream + PFX -> DigitalSigningService -> signed DOCX',
      },
    ],
  },
  {
    title: 'Converter / Exporter',
    status: 'Ready',
    text: 'Converter/exporter documentation belongs beside export workflows and explains DesignExportDto output into PDF, DOCX, HTML, Markdown, XML, CSV, SVG, Image, TIFF, ODT, and Excel.',
    points: ['DocumentExporter', 'HtmlDocumentExporter', 'MarkdownDocumentExporter', 'SvgDocumentExporter', 'ImageDocumentExporter', 'ExcelDocumentExporter'],
    examples: [
      {
        title: 'Export HTML Markdown XML and CSV',
        csharp: `DesignExportDto design = LoadDesign();

byte[] html = new HtmlDocumentExporter().Export(design);
byte[] markdown = new MarkdownDocumentExporter().Export(design);
byte[] xml = new XmlDocumentExporter().Export(design);
byte[] csv = new CsvDocumentExporter().Export(design);`,
        output: 'Text-oriented output for web previews, documentation, data extraction, interchange, and lightweight automation.',
        model: 'DesignExportDto -> Text/document exporters -> HTML/Markdown/XML/CSV',
      },
      {
        title: 'Export SVG Image JPEG TIFF and ODT',
        csharp: `DesignExportDto design = LoadDesign();

byte[] svg = new SvgDocumentExporter().Export(design);
byte[] png = new ImageDocumentExporter().Export(design);
byte[] jpeg = new JpegDocumentExporter().Export(design);
byte[] tiff = new TiffDocumentExporter().Export(design);
byte[] odt = new OdtDocumentExporter().Export(design);`,
        output: 'Visual and office-document output for previews, thumbnails, archive workflows, print workflows, and OpenDocument consumers.',
        model: 'DesignExportDto -> Visual/document exporters -> SVG/PNG/JPEG/TIFF/ODT',
      },
      {
        title: 'Check exporter capabilities',
        csharp: `IDocumentExporter exporter = new ImageDocumentExporter();
IExporterCapabilities capabilities = exporter.Capabilities;

if (!capabilities.SupportsFormFields)
{
    // Render form fields as visual elements or choose PDF/DOCX output.
}

byte[] output = exporter.Export(design);`,
        output: 'Capability-aware export flow for formats that do not support every design feature.',
        model: 'IDocumentExporter -> IExporterCapabilities -> Export decision',
      },
    ],
  },
];

const migrationGuides = [
  {
    title: 'PDF Code Migration',
    status: 'Preview',
    text: 'Map third-party PDF SDK calls into PXA code patterns and track missing API parity explicitly.',
  },
  {
    title: 'Nutrient Viewer to Designer Migration',
    status: 'Beta',
    text: 'Combine an original PDF with Nutrient Web SDK or PSPDFKit source and produce one editable PXA design with preview and Open in Designer.',
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
  {
    title: 'Migration Diagnostics',
    status: 'Ready',
    text: 'Understand severity, manual migration notes, and stable diagnostic IDs.',
  },
];

const migrationWorkflowSteps = [
  ['Choose the migration path', 'PDF Code, Report Designer, Spreadsheet Code, or Provider Taxonomy.'],
  ['Select provider/framework', 'Use the original technology, for example iText7, DevExpress XtraReport, RDL, ClosedXML, or Aspose.Cells.'],
  ['Submit source', 'Paste source code, upload a report designer file, or provide the original PDF together with Nutrient Viewer source.'],
  ['Review converted output', 'PDF and spreadsheet code produce PXA-compatible code; Designer migration produces one editable design JSON document.'],
  ['Read diagnostics', 'Warnings identify unsupported patterns, manual follow-ups, or API areas outside deterministic migration.'],
  ['Open or export result', 'Open report output in PXA Designer, copy generated code, generate preview, or download artifacts.'],
];

const pdfCodeMigrationProviders = [
  ['Nutrient .NET (formerly GdPicture.NET)', 'beta', 'Document creation, file/stream existing-PDF load, status-preserving control flow, page move/delete/clone/blank insert, CropBox read/set/reset, append-compatible one-based page-range extraction/split, path and loaded-document merge, static page selection, rotation, content, basic forms and annotations, AES-256 output, save, and disposal.', 'Encrypted cross-document sources, unsupported page-box kinds, bookmark/barcode/repeating-content split modes, rich appearance details, runtime signing, irreversible-redaction orchestration, PDF OCR, scanning, conversion, advanced permissions, and certificate security.'],
  ['Syncfusion PDF', 'full', 'Document/page/text/line/rectangle/image/save with top-left coordinate adapter.', 'Complex tables, forms, signatures, existing-PDF editing.'],
  ['iText7', 'full', 'PdfWriter/PdfDocument/Document, Paragraph, ShowTextAligned, PdfCanvas text/line/rectangle.', 'Advanced layout renderer model, forms, tagged PDF, signatures.'],
  ['Apryse (PDFTron)', 'full', 'PDFDoc creation, PageCreate/PagePushBack, document save.', 'ElementBuilder rich graphics, editing workflows, annotations.'],
  ['Aspose.PDF', 'full', 'Document, Pages.Add, TextFragment/TextBuilder positioned text.', 'Advanced DOM editing, forms, compliance, optimization.'],
  ['DsPdf (GrapeCity)', 'full', 'GcPdfDocument, NewPage, DrawString/DrawLine/DrawRectangle/FillRectangle, Save.', 'Advanced graphics, AcroForms, image processing.'],
  ['Foxit PDF SDK', 'full', 'PDFDoc, InsertPage/CreatePage, graphics draw calls, Save/SaveAs.', 'Runtime/library setup, editing APIs, rich annotations.'],
  ['DevExpress PDF', 'full', 'PdfDocumentProcessor, RenderNewPage, draw calls, SaveDocument.', 'Forms/signatures/report export produce follow-up diagnostics.'],
  ['Spire.PDF', 'full', 'PdfDocument, Pages.Add, Canvas.DrawString/DrawLine/DrawRectangle/FillRectangle, SaveToFile.', 'Tables, forms, annotations produce warnings.'],
  ['GemBox.Pdf', 'full', 'PdfDocument, Pages.Add, Content.DrawText/DrawLine/DrawRectangle, ComponentInfo.SetLicense removal.', 'Forms, encryption, annotations produce warnings.'],
  ['PDFKit.NET', 'full', 'Document/NewPage/Pages.Add, DrawText/DrawString/DrawLine/DrawRectangle, Save/Render.', 'Package identity must be manually verified; forms/encryption/annotations warning path.'],
  ['LEADTOOLS', 'full', 'PDFDocument, AddPage/Pages.Add, DrawText/DrawString/DrawLine/DrawRectangle, Save/Export.', 'Raster/OCR/barcode/conversion APIs are manual.'],
  ['IronPDF', 'pilot', 'ChromePdfRenderer scaffold, SaveAs to Save.', 'HTML/URL/Razor rendering converted to manual diagnostics.'],
  ['ActivePDF', 'pilot', 'Likely Toolkit-style generation only.', 'DocConverter, WebGrabber, COM/server, printer, merge, stamp workflows are manual.'],
  ['PDFTools / Pdftools SDK', 'pilot', 'Sdk.Initialize removal and cautious processing diagnostics.', 'SDK conversion/processing workflows remain manual; Toolbox direct generation is separate.'],
  ['PDF Toolbox SDK', 'pilot', 'Document.Create/Page.Create/TextGenerator.ShowLine to PXA-compatible code.', 'Existing-PDF editing and rich styling remain manual.'],
];

const reportDesignerProviders = [
  ['DevExpress XtraReport', '.Designer.cs / C# report class', 'Bands, labels, lines, tables, charts, barcodes, images, page header/footer.', 'PXA.Migration.Report.Designer.DevExpress'],
  ['RDL/RDLC / Syncfusion / Bold Reports', '.rdl / .rdlc', 'Textbox, tablix/table, line, rectangle, image, page header/footer, field bindings.', 'PXA.Migration.Report.Designer.Rdl'],
  ['ActiveReports RPX', '.rpx', 'Section bands, labels, textboxes, line, barcode, page header/footer, DataField binding.', 'PXA.Migration.Report.Designer.Rpx'],
  ['ActiveReportsJS', 'JSON report definition', 'Designer JSON report items where supported by the converter.', 'PXA.Migration.Report.Designer.ActiveReportsJs'],
  ['FastReport', '.frx', 'Bands, TextObject, LineObject, BarcodeObject, style/font/color basics.', 'PXA.Migration.Report.Designer.FastReport'],
  ['Telerik Reporting', '.trdx', 'Sections, named styles, text boxes, barcodes, page header/footer.', 'PXA.Migration.Report.Designer.Telerik'],
  ['JasperReports', '.jrxml', 'Bands, staticText/textField, images, charts where mapping is available, $F{} bindings.', 'PXA.Migration.Report.Designer.JasperReports'],
  ['Stimulsoft Reports', '.mrt', 'Bands, text, horizontal lines, page footer/header, {Source.Field} bindings.', 'PXA.Migration.Report.Designer.Stimulsoft'],
];

const spreadsheetCodeMigrationProviders = [
  ['Aspose.Cells', 'full', 'PXA.Migration.Spreadsheet.Code.Aspose', 'Workbook, Worksheets, Cells, values, styles, formulas.'],
  ['ClosedXML', 'full', 'PXA.Migration.Spreadsheet.Code.ClosedXml', 'Reference implementation for workbook/sheet/cell/range authoring.'],
  ['EPPlus', 'full', 'PXA.Migration.Spreadsheet.Code.Epplus', 'ExcelPackage, Worksheets, Cells indexers, values, formulas, styles.'],
  ['GemBox.Spreadsheet', 'full', 'PXA.Migration.Spreadsheet.Code.GemBox', 'ExcelFile authoring and SetLicense removal.'],
  ['NPOI', 'full', 'PXA.Migration.Spreadsheet.Code.Npoi', 'Workbook/sheet/row/cell creation patterns.'],
  ['Spire.XLS', 'full', 'PXA.Migration.Spreadsheet.Code.Spire', 'Workbook/worksheet/range authoring patterns.'],
  ['SpreadsheetLight', 'full', 'PXA.Migration.Spreadsheet.Code.SpreadsheetLight', 'SLDocument cells, formulas, styles where deterministic.'],
  ['Syncfusion XlsIO', 'full', 'PXA.Migration.Spreadsheet.Code.Syncfusion', 'ExcelEngine, IWorkbook/IWorksheet/range authoring patterns.'],
];

const taxonomyRows = [
  ['PDF code migration', 'PXA.Migration.Pdf.Code.<Provider>', 'Provider-specific C# PDF SDK code to PXA PDF generator code.'],
  ['PDF Viewer migration', 'PXA.Migration.Pdf.Viewer.<Provider>', 'Viewer configuration and interchange data to PXA Viewer integration artifacts.'],
  ['Report designer migration', 'PXA.Migration.Report.Designer.<Provider>', 'Report designer files/classes to editable PXA design JSON.'],
  ['Spreadsheet code migration', 'PXA.Migration.Spreadsheet.Code.<Provider>', 'Spreadsheet library C# code to PXA spreadsheet API code.'],
  ['Reserved spreadsheet datasource migration', 'PXA.Migration.Spreadsheet.Datasource.<Provider>', 'Reserved for concrete datasource/file migration providers; not fake-created.'],
  ['Shared abstractions', 'PXA.Migration.Abstractions', 'Common contracts and migration result shape.'],
  ['Roslyn infrastructure', 'PXA.Migration.Roslyn', 'Shared C# source rewriting infrastructure.'],
];

const migrationApiRows = [
  ['Catalog', 'GET /api/pxa/v1/migrations/catalog', 'Discover providers and filter by domain and kind.'],
  ['Provider', 'GET /api/pxa/v1/migrations/{domain}/{kind}/{provider}', 'Read inputs, artifacts, status, limits, permissions, and Documentation ID.'],
  ['Examples', 'GET /api/pxa/v1/migrations/{domain}/{kind}/{provider}/examples', 'Load a safe backend-owned example when the provider publishes one.'],
  ['Convert', 'POST /api/pxa/v1/migrations/{domain}/{kind}/{provider}/convert', 'Return the common envelope and typed provider artifacts.'],
  ['Preview', 'POST /api/pxa/v1/migrations/{domain}/{kind}/{provider}/preview', 'Render a preview only when the catalog declares preview support.'],
];

const nutrientViewerConfigurationMappings = [
  ['NutrientViewer.load / PSPDFKit.load', 'Generated typed PXA Viewer adapter', 'JavaScript is parsed as source and is never executed.'],
  ['container', 'mountSelector', 'Literal CSS selectors are preserved. Dynamic expressions require review.'],
  ['document', 'documentSource', 'Safe application-owned paths are retained. Local paths and remote customer URLs are removed.'],
  ['initialViewState.currentPageIndex', 'initialPageNumber', 'The zero-based Nutrient index becomes a one-based PXA page number.'],
  ['initialViewState.zoom / FIT_TO_WIDTH', 'fit', 'Supported literal fit values are normalized to the typed Viewer handoff.'],
  ['locale / theme', 'normalized settings', 'Literal values are retained for application integration.'],
  ['toolbarItems', 'requested toolbar capabilities', 'PXA permissions and entitlements remain authoritative.'],
  ['licenseKey / jwt / authPayload / serverUrl', 'omitted', 'Secrets and Document Engine authentication are never copied.'],
  ['event callbacks and custom UI', 'review diagnostic', 'Application-owned callback logic is not regenerated.'],
];

const nutrientCodeMappings = [
  ['NewPDF / NewPage / SelectPage', 'PdfDocument / AddPage / generated page variable', 'Static page selections are deterministic; dynamic and forward selections remain review items.'],
  ['DrawText / DrawTextBox', 'DrawText or DrawTextBoxFromTop', 'Measurement units, origin, standard font, size, and supported alignment are retained.'],
  ['DrawLine / DrawRectangle', 'DrawLine or DrawRectangle with origin-aware variants', 'Stateful line width, line color, fill color, and fill are retained; unsupported right-side origins and no-stroke rectangles receive review diagnostics.'],
  ['AddJpegImageFromFile + DrawImage', 'Image path + DrawImage', 'PXA loads the original local image path; opaque GdPicture image resource IDs are not guessed.'],
  ['AddLinkToWebAnnotation', 'AddWebLink', 'Geometry and URI are retained; source border appearance is reported when it cannot be represented.'],
  ['AddTextFormField / AddCheckBoxFormField', 'AddTextField, AddMultilineTextField, or AddCheckBox', 'Geometry, name, value, multiline state, size, and checked state are retained; provider-specific appearance requires review.'],
  ['AddStickyNoteAnnotation', 'AddStickyNoteAnnotation', 'Position, contents, color, and opacity are retained; icon, author, subject, and popup details are reported as gaps.'],
  ['Encrypt(owner, user)', 'PdfSaveOptions.Encryption with AES-256', 'Literal passwords are removed and replaced by runtime secret lookups. PDF/A combined with encryption fails closed and requires a policy decision.'],
  ['OcrPage / TwainAcquire', 'Explicit application-service or hardware action', 'PXA does not misrepresent image-to-design OCR as searchable-PDF OCR, and scanner acquisition remains customer-side.'],
  ['Aliases, fields, and parameters', 'Symbol-aware Roslyn evidence', 'Supported Nutrient/GdPicture symbols are resolved through aliases, fields, and method parameters; source locations are retained for review.'],
  ['ApplyRedaction', 'PXA Trust review evidence', 'The recognized call is reported with source location and a safe next action. Migration does not execute irreversible redaction, copy PFX paths or passwords, or claim that content was removed.'],
  ['SignPDF / signature APIs', 'PXA Trust review evidence', 'The recognized call is reported with source location and a safe next action. Migration does not sign, validate certificates, copy private-key material, PFX paths, passwords, or credentials.'],
];

const nutrientAnnotationMappings = [
  ['note, free text, stamp, image', 'Mapped', 'Bounds, page, identity, author, dates, opacity, lock state, and safe metadata are retained where valid.'],
  ['line, rectangle, ellipse', 'Mapped', 'Stroke, fill, width, opacity, and supported line caps are normalized.'],
  ['ink', 'Mapped', 'Independent point lines are retained with bounded point counts. Signature ink is omitted.'],
  ['highlight, underline, strikeout', 'Mapped', 'Text rectangles are converted to page-relative quad geometry.'],
  ['redaction mark', 'Review required', 'The visual mark is retained; PXA does not claim that source content was irreversibly removed.'],
  ['comments, attachments, actions', 'Omitted with diagnostic', 'Threading, binary attachments, URI, launch, form, and JavaScript actions are not copied.'],
  ['unknown annotation types', 'Omitted with diagnostic', 'The result never reports exact fidelity for omitted behavior.'],
];

const nutrientFormMappings = [
  ['Text / multiline text', 'Mapped', 'String values plus required, read-only, and default-value metadata.'],
  ['Checkbox', 'Mapped with review for groups', 'Boolean values are supported; multi-widget checkbox groups are reduced explicitly.'],
  ['Radio button', 'Mapped', 'Selected option values and option metadata are retained.'],
  ['Combo box', 'Mapped as dropdown', 'String value, options, required, read-only, and default value are retained.'],
  ['List box', 'Mapped as list', 'String or string-array values and options are retained.'],
  ['Signature / push button', 'Omitted with diagnostic', 'Saved signatures, biometric data, credentials, and active button behavior are never exported.'],
];

const nutrientDiagnosticRows = [
  ['CANMIGNUTWEB002-006', 'Source size, language, syntax, and Viewer bootstrap validation.'],
  ['CANMIGNUTWEB020-035', 'Dynamic values, unsafe document sources, removed credentials, toolbar requests, events, and configuration review.'],
  ['CANMIGNUTJSON001-013', 'Instant JSON size, format, page, annotation, geometry, and identifier validation.'],
  ['CANMIGNUTJSON020-037', 'Redaction, signature ink, actions, comments, attachments, ink limits, quad geometry, and line-cap review.'],
  ['CANMIGNUTJSON040-048', 'Form metadata, values, unsupported fields, duplicates, and checkbox-group review.'],
];

const diagnosticRows = [
  ['Info', 'Converted deterministic pattern, removed provider setup, or noted compatibility behavior.'],
  ['Warning', 'Output is usable, but a source pattern needs review or manual parity check.'],
  ['Error', 'Conversion could not produce a reliable target for that input.'],
  ['Migrate manually', 'The source feature is outside deterministic conversion, for example signatures, existing-PDF editing, advanced HTML rendering, or provider-specific runtime services.'],
  ['Stable diagnostic IDs', 'Existing CANMIG... IDs remain stable for compatibility while namespace names move to PXA.'],
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
      { label: 'Report Designer migration', href: `${siteLinks.designer}pdf/migrations/designer` },
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
    command: 'open /pdf/migrations/designer',
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

function renderDetailNavList(items) {
  return items.map((item) => `<a href="#${slug(itemTitle(item))}-details">${itemTitle(item)}</a>`).join('');
}

function renderReleaseNav() {
  return `
    <details class="pxa-doc-nav__section">
      <summary>Release Notes</summary>
      <a class="pxa-doc-nav__featured" href="#release-notes">Overview</a>
      <a href="#designer-feature-status">Feature status</a>
      <a href="#designer-notifications">Notifications</a>
      ${pxaReleases
        .map((release) => `<a href="#release-notes-${slug(release.version)}">PXA Designer ${release.version}</a>`)
        .join('')}
    </details>
  `;
}

function renderDesignerReleaseNotes() {
  const categories = [
    ['added', 'Added'],
    ['improved', 'Improved'],
    ['fixed', 'Fixed'],
    ['security', 'Security'],
    ['deprecated', 'Deprecated'],
    ['breaking', 'Breaking'],
  ];
  return `
    <div class="pxa-doc-release-filters" role="group" aria-label="Filter releases">
      <button class="is-active" type="button" data-release-filter="all">All</button>
      <button type="button" data-release-filter="stable">Stable</button>
      <button type="button" data-release-filter="beta">Beta</button>
      <button type="button" data-release-filter="alpha">Alpha</button>
    </div>
    <div class="pxa-doc-detail-stack pxa-doc-release-list">
      ${pxaReleases.map((release) => `
        <article
          class="pxa-card pxa-doc-detail pxa-doc-release"
          id="release-notes-${slug(release.version)}"
          data-release-channel="${escapeHtml(release.channel)}"
        >
          <div class="pxa-doc-detail__header">
            <span class="pxa-status pxa-status--${escapeHtml(release.channel)}">${escapeHtml(release.channel)}</span>
            <p class="pxa-kicker">${escapeHtml(release.publishedAt)}</p>
            <h3>${escapeHtml(release.title)}</h3>
            <p>${escapeHtml(release.summary)}</p>
          </div>
          <div class="pxa-doc-release-changes">
            ${categories.map(([key, label]) => release.changes[key]?.length ? `
              <section>
                <h4>${label}</h4>
                <ul>${release.changes[key].map((entry) => `<li>${escapeHtml(entry)}</li>`).join('')}</ul>
              </section>
            ` : '').join('')}
          </div>
          <div class="pxa-doc-feature-list">
            ${release.featureIds.map((featureId) => {
              const feature = designerFeatures.find((item) => item.id === featureId);
              return `<span>${escapeHtml(feature?.fallbackTitle ?? featureId)}</span>`;
            }).join('')}
          </div>
        </article>
      `).join('')}
    </div>
  `;
}

function renderElementNav(items) {
  const categories = [...new Set(items.map((item) => item.category))];
  return `
    <details class="pxa-doc-nav__section" open>
      <summary>Element Reference</summary>
      <a class="pxa-doc-nav__featured" href="#element-reference">Overview</a>
      ${categories
        .map((category) => `
          <details class="pxa-doc-nav__section pxa-doc-nav__section--nested">
            <summary>${category}</summary>
            ${items
              .filter((item) => item.category === category)
              .map((item) => `<a class="pxa-doc-nav__subitem" href="#${slug(item.title)}-element">${item.title}</a>`)
              .join('')}
          </details>
        `)
        .join('')}
    </details>
  `;
}

function renderCodeSdkNav(groups, exportItems) {
  return `
    ${groups
      .map((group) => `
        <details class="pxa-doc-nav__section" open>
          <summary>${group.title}</summary>
          <a class="pxa-doc-nav__featured" href="#${slug(group.title)}">${group.title} Overview</a>
          ${group.subgroups
            ? group.subgroups
                .map((subgroup) => `
                  <details class="pxa-doc-nav__section pxa-doc-nav__section--nested">
                    <summary>${subgroup.title}</summary>
                    ${subgroup.examples
                      .map((item) => `<a class="pxa-doc-nav__subitem" href="#${slug(item.title)}">${item.title}</a>`)
                      .join('')}
                  </details>
                `)
                .join('')
            : group.examples
                .map((item) => `<a class="pxa-doc-nav__subitem" href="#${slug(item.title)}">${item.title}</a>`)
                .join('')}
        </details>
      `)
      .join('')}
    ${exportItems.map((item) => `<a href="#${slug(item.title)}">${item.title}</a>`).join('')}
  `;
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

function renderCodePreview(preview) {
  const safeValue = escapeHtml(preview.value ?? '');
  const content = {
    text: `<div class="pxa-sdk-preview-text">${safeValue}</div>`,
    image: `<div class="pxa-sdk-preview-image">${safeValue}</div>`,
    table: `<div class="pxa-sdk-preview-table">${(preview.rows ?? [['Item', 'Qty', 'Total'], ['Coffee', '2', '19.80']]).flat().map((cell) => `<span>${escapeHtml(cell)}</span>`).join('')}</div>`,
    chart: `<div class="pxa-sdk-preview-chart">${(preview.values ?? [10, 20]).map((value) => `<i style="height:${Math.max(18, Math.min(92, Number(value) * 4))}%"></i>`).join('')}</div>`,
    barcode: `<div class="pxa-sdk-preview-barcode">|||| ||| ||||<small>${safeValue}</small></div>`,
    field: `<label class="pxa-sdk-preview-field"><span>${safeValue}</span><input value="${escapeHtml(preview.placeholder ?? 'Enter name')}" readonly></label>`,
    shape: `<div class="pxa-sdk-preview-shape"><span></span><strong>${safeValue}</strong></div>`,
    watermark: `<div class="pxa-sdk-preview-watermark">${safeValue}</div>`,
    spreadsheet: `<div class="pxa-sdk-preview-table pxa-sdk-preview-spreadsheet">${(preview.rows ?? [['A1', safeValue]]).flat().map((cell) => `<span>${escapeHtml(cell)}</span>`).join('')}</div>`,
  };

  return `
    <div class="pxa-sdk-preview-page">
      ${content[preview.kind] ?? `<div>${safeValue}</div>`}
    </div>
  `;
}

function renderCodeSdkExamples(items) {
  return items
    .map((item) => `
      <section class="pxa-card pxa-doc-detail pxa-code-sdk-example" id="${slug(item.title)}" data-example-index="${codeSdkExamples.indexOf(item)}">
        <div class="pxa-doc-detail__header">
          <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
          <h3>${item.title}</h3>
          <p>${item.description}</p>
        </div>
        <div class="pxa-code-playground">
          <div class="pxa-code-sdk-tabs pxa-code-language-tabs" aria-label="${item.title} language tabs">
            <button class="is-active" type="button" data-language="csharp">C#</button>
            <button type="button" data-language="java">Java</button>
            <button type="button" data-language="python">Python</button>
          </div>
          <div class="pxa-code-editor-shell">
            <textarea class="pxa-code-editor" spellcheck="false" aria-label="${item.title} C# code">${escapeHtml(item.csharp)}</textarea>
            <div class="pxa-code-playground-actions">
              <button class="pxa-button pxa-button--primary pxa-code-run" type="button">Run</button>
              <span class="pxa-code-run-status">Ready</span>
            </div>
          </div>
          <div class="pxa-code-sdk-tabs pxa-code-output-tabs" aria-label="${item.title} output tabs">
            <button class="is-active" type="button" data-output="preview">Preview</button>
            <button type="button" data-output="json">JSON</button>
            <button type="button" data-output="model">Model</button>
          </div>
          <div class="pxa-code-output-panel is-active" data-output-panel="preview">
            ${renderCodePreview(item.preview)}
          </div>
          <div class="pxa-code-output-panel" data-output-panel="json">
            <pre class="pxa-code"><code>${escapeHtml(item.json)}</code></pre>
          </div>
          <div class="pxa-code-output-panel" data-output-panel="model">
            <pre class="pxa-code"><code>${escapeHtml(item.model)}</code></pre>
          </div>
        </div>
      </section>
    `)
    .join('');
}

function renderCodeSdkGroups(groups) {
  return groups
    .map(
      (group) => `
        <section class="pxa-doc-sdk-group" id="${slug(group.title)}">
          <div class="pxa-doc-sdk-group__header">
            <p class="pxa-kicker">${group.title}</p>
            <h3>${group.title} examples</h3>
            <p>${group.description}</p>
          </div>
          ${group.subgroups
            ? group.subgroups
                .map((subgroup) => `
                  <section class="pxa-doc-sdk-subgroup" id="${slug(subgroup.title)}">
                    <div class="pxa-doc-sdk-subgroup__header">
                      <p class="pxa-kicker">${group.title}</p>
                      <h4>${subgroup.title}</h4>
                      <p>${subgroup.description}</p>
                      <div class="pxa-doc-sdk-subgroup__links" aria-label="${subgroup.title} examples">
                        ${subgroup.examples
                          .map((item) => `<a href="#${slug(item.title)}">${item.title}</a>`)
                          .join('')}
                      </div>
                    </div>
                    ${renderCodeSdkExamples(subgroup.examples)}
                  </section>
                `)
                .join('')
            : renderCodeSdkExamples(group.examples)}
        </section>
      `,
    )
    .join('');
}

function renderExportSdkDocs(items) {
  return items
    .map(
      (item) => `
        <section class="pxa-card pxa-doc-detail" id="${slug(item.title)}">
          <div class="pxa-doc-detail__header">
            <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
            <h3>${item.title}</h3>
            <p>${item.text}</p>
          </div>
          <div class="pxa-doc-feature-list">
            ${item.points.map((point) => `<span>${point}</span>`).join('')}
          </div>
          ${item.image
            ? `<figure class="pxa-doc-screenshot">
                <img src="${item.image.src}" alt="${item.image.alt}">
                <figcaption>${item.image.caption}</figcaption>
              </figure>`
            : ''}
          ${item.examples
            ? `
              <div class="pxa-export-example-grid">
                ${item.examples
                  .map((example) => `
                    <article class="pxa-export-example">
                      <h4>${example.title}</h4>
                      <pre class="pxa-code"><code>${escapeHtml(example.csharp)}</code></pre>
                      <dl>
                        <div>
                          <dt>Output</dt>
                          <dd>${example.output}</dd>
                        </div>
                        <div>
                          <dt>Model</dt>
                          <dd>${example.model}</dd>
                        </div>
                      </dl>
                    </article>
                  `)
                  .join('')}
              </div>
            `
            : ''}
        </section>
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

function renderDetailedDocs(items) {
  return items
    .map(
      (item) => `
        <section class="pxa-card pxa-doc-detail" id="${slug(item.title)}-details">
          <div class="pxa-doc-detail__header">
            <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
            <h3>${item.title}</h3>
            <p>${item.purpose}</p>
          </div>
          <div class="pxa-doc-detail__grid">
            <article>
              <h4>When to use</h4>
              <ul>
                ${item.whenToUse.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
            <article>
              <h4>Core concepts</h4>
              <ul>
                ${item.concepts.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
            <article>
              <h4>Common tasks</h4>
              <ul>
                ${item.tasks.map((text) => `<li>${text}</li>`).join('')}
              </ul>
            </article>
          </div>
          <div class="pxa-doc-related-links">
            ${item.related.map((link) => `<a href="${link.href}">${link.label}</a>`).join('')}
          </div>
          ${item.images
            ? `<div class="pxa-doc-screenshot-grid">
                ${item.images.map((image) => `
                  <figure class="pxa-doc-screenshot">
                    <img src="${image.src}" alt="${image.alt}" loading="lazy">
                    <figcaption>${image.caption}</figcaption>
                  </figure>
                `).join('')}
              </div>`
            : ''}
          ${item.examples
            ? `
              <div class="pxa-export-example-grid">
                ${item.examples
                  .map((example) => `
                    <article class="pxa-export-example">
                      <h4>${example.title}</h4>
                      <pre class="pxa-code"><code>${escapeHtml(example.json)}</code></pre>
                      <dl>
                        <div>
                          <dt>How it works</dt>
                          <dd>${example.note}</dd>
                        </div>
                      </dl>
                    </article>
                  `)
                  .join('')}
              </div>
            `
            : ''}
          ${item.title === 'Designer' ? renderDesignerWorkflow() : ''}
          ${item.title === 'Element Library' ? renderElementLibraryGuide() : ''}
        </section>
      `,
    )
    .join('');
}

function renderDesignerWorkflow() {
  return `
    <div class="pxa-doc-workflow" id="designer-workflow">
      <div class="pxa-doc-workflow__header">
        <h4>Designer workflow</h4>
        <p>Follow this path when building a template from scratch or reviewing a migrated report in PXA Designer.</p>
      </div>
      <div class="pxa-doc-workflow__steps">
        ${designerWorkflowSteps.map((step, index) => `
          <article class="pxa-doc-workflow-step">
            <span class="pxa-doc-workflow-step__number">${index + 1}</span>
            <div>
              <h5>${step.title}</h5>
              <p>${step.description}</p>
              <ul>
                ${step.actions.map((action) => `<li>${action}</li>`).join('')}
              </ul>
              <strong>Result: ${step.result}</strong>
            </div>
          </article>
        `).join('')}
      </div>
      <div class="pxa-doc-workflow__panels">
        ${renderMigrationProviderTable(['Designer area', 'What it is used for'], designerPanelReference)}
      </div>
    </div>
  `;
}

function renderElementLibraryGuide() {
  return `
    <div class="pxa-element-library-guide" id="element-library-workflow">
      <section class="pxa-doc-guide-section">
        <div>
          <p class="pxa-kicker">Elements mode</p>
          <h4>Find and favorite individual elements</h4>
          <ol>
            <li>Open <strong>Elements</strong> at the top of the left Designer panel.</li>
            <li>Enter a localized name, description, or keyword in <strong>Search elements</strong>.</li>
            <li>Expand the required category and click an element to add it to the active page.</li>
            <li>Select the star beside an element to add or remove it from <strong>Favorites</strong>.</li>
          </ol>
          <p>Favorites are personal to your user account, remain in their original categories, and synchronize independently of the active organization. A locally cached copy remains visible during a temporary connection failure.</p>
        </div>
        <figure class="pxa-doc-screenshot">
          <img src="/images/designer/element-library-favorites.png" alt="PXA Designer Elements panel showing search, Favorites, and organized element categories">
          <figcaption>Elements remain organized by purpose while Favorites provides a fast personal shortcut.</figcaption>
        </figure>
      </section>

      <section class="pxa-doc-guide-section">
        <div>
          <p class="pxa-kicker">Content mode</p>
          <h4>Insert reusable blocks and page kits</h4>
          <ol>
            <li>Switch from <strong>Elements</strong> to <strong>Content</strong>.</li>
            <li>Search the library or select Forms, Invoices, Books, Certificates, Presentations, or Reports.</li>
            <li>Use the eye action to inspect the visual preview and content description.</li>
            <li>Select <strong>Insert content</strong>, or drag a block to the required canvas position.</li>
          </ol>
          <p><strong>Blocks</strong> add multiple elements to the active page. <strong>Page kits</strong> add one or more complete pages immediately after the active page.</p>
        </div>
        <figure class="pxa-doc-screenshot">
          <img src="/images/designer/content-library-preview.png" alt="PXA Designer Content Library preview for an address form block">
          <figcaption>Preview content before inserting it. Configurable blocks also expose their editable field list here.</figcaption>
        </figure>
      </section>

      <section class="pxa-doc-guide-section pxa-doc-guide-section--single">
        <div>
          <p class="pxa-kicker">Catalog reference</p>
          <h4>Element categories</h4>
          ${renderMigrationProviderTable(['Category', 'Contains'], elementLibraryGroups)}
        </div>
        <div>
          <h4>Content categories</h4>
          ${renderMigrationProviderTable(['Category', 'Available building blocks'], contentLibraryReference)}
        </div>
      </section>

      <section class="pxa-doc-guide-section pxa-doc-guide-section--single">
        <div>
          <p class="pxa-kicker">Safe insertion</p>
          <h4>What the Designer preserves</h4>
          <ul>
            <li>Every inserted page and element receives a fresh identifier; internal references are rewritten automatically.</li>
            <li>Only conflicting form field names are namespaced. Existing non-conflicting field names remain unchanged.</li>
            <li>Bindings, localization, RTL, styles, assets, and extension data are retained.</li>
            <li>The complete insertion is one undoable transaction and continues through normal template autosave.</li>
            <li>Page kits are fitted proportionally to the document page size and centered. A warning identifies scaling or empty margins.</li>
          </ul>
          <aside class="pxa-doc-callout pxa-doc-callout--warning">
            <strong>Before inserting asset-based content</strong>
            <p>Required images or other assets must be available to the active organization. Validation failures leave the document unchanged.</p>
          </aside>
        </div>
        <div>
          <h4>Content Library versus Template Gallery</h4>
          <dl class="pxa-doc-definition-list">
            <div><dt>Content Library</dt><dd>Adds a block to the current page or inserts pages after the active page. Existing document content remains in place.</dd></div>
            <div><dt>Template Gallery</dt><dd>Starts or replaces the current design with a complete template. Use it when the gallery template should become the document foundation.</dd></div>
          </dl>
          <h4>Troubleshooting</h4>
          <ul>
            <li><strong>No Favorites group:</strong> mark at least one currently supported element with the star.</li>
            <li><strong>Favorite is hidden:</strong> clear search or switch to a document mode that supports the element.</li>
            <li><strong>Content is incompatible:</strong> choose an entry that supports the active PDF or Word mode.</li>
            <li><strong>Insert failed:</strong> restore connectivity, verify required assets, and retry; no partial elements are retained.</li>
          </ul>
          <aside class="pxa-doc-callout">
            <strong>Screen size</strong>
            <p>The current visual Designer requires a desktop or tablet-sized viewport. Mobile phones display the protected Desktop required state.</p>
          </aside>
        </div>
      </section>
    </div>
  `;
}

function renderAttributeTable(rows) {
  return `
    <div class="pxa-doc-table-wrap">
      <table class="pxa-doc-attribute-table">
        <thead>
          <tr>
            <th>Attribute</th>
            <th>Type / values</th>
            <th>Usage</th>
          </tr>
        </thead>
        <tbody>
          ${rows.map(([name, type, usage]) => `
            <tr>
              <td><code>${name}</code></td>
              <td>${type}</td>
              <td>${usage}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function renderElementVisual(visual) {
  return `
    <aside class="pxa-doc-element-visual" aria-label="${visual.title}">
      <div class="pxa-doc-element-visual__toolbar">
        <span>Toolbar</span>
        <strong>${visual.toolbar}</strong>
      </div>
      <div class="pxa-doc-element-visual__canvas">
        <div class="pxa-doc-element-visual__page">
          <div class="pxa-doc-element-visual__selection">${visual.preview}</div>
        </div>
      </div>
      <div class="pxa-doc-element-visual__properties">
        <span>Properties panel</span>
        <ul>
          ${visual.properties.map((property) => `<li>${property}</li>`).join('')}
        </ul>
      </div>
    </aside>
  `;
}

function renderElementJsonPlayground(item) {
  return `
    <div class="pxa-element-json-playground" data-element-title="${escapeHtml(item.title)}">
      <textarea class="pxa-element-json-editor" spellcheck="false" aria-label="${item.title} design JSON">${escapeHtml(item.example)}</textarea>
      <div class="pxa-code-playground-actions">
        <button class="pxa-button pxa-button--primary pxa-element-json-run" type="button">Run</button>
        <span class="pxa-code-run-status">Ready</span>
      </div>
      <div class="pxa-element-json-preview" data-element-preview>
        ${renderElementVisual(item.visual)}
      </div>
    </div>
  `;
}

function renderElementReference(items) {
  return `
    <section class="pxa-card pxa-doc-element-reference" id="element-reference">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Element Reference</h3>
        <p>
          This is the practical designer reference: how to add elements, which attributes matter,
          and what the generated design JSON looks like.
        </p>
        <a class="pxa-doc-show-all-elements" href="#element-reference">Show all elements</a>
      </div>

      <div class="pxa-doc-element-basics">
        <article>
          <h4>How every element is used</h4>
          <ol>
            <li>Open the live designer.</li>
            <li>Choose the element group in the toolbar.</li>
            <li>Click the element type to place it on the active page.</li>
            <li>Drag, resize, and align it on the canvas.</li>
            <li>Edit content, style, binding, and type-specific properties in the properties panel.</li>
            <li>Preview, export JSON, or open the output in the PDF workflow.</li>
          </ol>
        </article>
        <article>
          <h4>Common attributes</h4>
          ${renderAttributeTable(commonElementAttributes)}
        </article>
        <article>
          <h4>Common style keys</h4>
          ${renderAttributeTable(commonStyleAttributes)}
        </article>
      </div>

      <div class="pxa-doc-element-list">
        ${items.map((item) => `
          <section class="pxa-doc-element-card" id="${slug(item.title)}-element">
            <div class="pxa-doc-element-card__header">
              <div>
                <span class="pxa-status ${statusClass(item.status)}">${item.status}</span>
                <h4>${item.title}</h4>
                <p><code>${item.type}</code> - ${item.description}</p>
              </div>
              <span class="pxa-status pxa-status--preview">${item.category}</span>
            </div>

            <div class="pxa-doc-element-card__body">
              <div class="pxa-doc-element-card__content">
                <h5>How to use</h5>
                <ol>
                  ${item.addSteps.map((step) => `<li>${step}</li>`).join('')}
                </ol>

                <h5>Use cases</h5>
                <ul>
                  ${item.usage.map((usage) => `<li>${usage}</li>`).join('')}
                </ul>

                <h5>Attributes</h5>
                ${renderAttributeTable(item.attributes)}

                <h5>Design JSON example</h5>
                ${renderElementJsonPlayground(item)}
              </div>
            </div>
          </section>
        `).join('')}
      </div>
    </section>
  `;
}

function renderPageToc() {
  const tocGroups = [
    {
      title: 'Start',
      links: [
        ['Overview', '#overview'],
        ['Quickstarts', '#quickstarts'],
      ],
    },
    {
      title: 'Editor',
      links: [
        ['Editor path', '#editor-path'],
        ...editorSections.map((item) => [itemTitle(item), `#${slug(itemTitle(item))}-details`]),
        ['Element Reference', '#element-reference'],
      ],
    },
    {
      title: 'Code SDK',
      links: [
        ['Code path', '#code-path'],
        ...codeSdkGroups.map((group) => [group.title, `#${slug(group.title)}`]),
        ...exportSdkDocs.map((item) => [item.title, `#${slug(item.title)}`]),
      ],
    },
    {
      title: 'Migration',
      links: [
        ['Migration overview', '#migration'],
        ['Common workflow', '#migration-workflow'],
        ...migrationGuides.map((item) => [item.title, `#${slug(item.title)}`]),
      ],
    },
    {
      title: 'Reference',
      links: [
        ['Cookbook', '#cookbook'],
        ['Demo examples', '#demo-examples'],
        ['API reference', '#api-reference'],
        ['History', '#history'],
      ],
    },
  ];

  return `
    <strong>On this page</strong>
    ${tocGroups.map((group) => `
      <details class="pxa-doc-toc__group" open>
        <summary>${group.title}</summary>
        <div>
          ${group.links.map(([label, href]) => `<a href="${href}">${label}</a>`).join('')}
        </div>
      </details>
    `).join('')}
  `;
}

function renderMigrationProviderTable(headers, rows) {
  return `
    <div class="pxa-doc-table-wrap">
      <table class="pxa-doc-attribute-table">
        <thead>
          <tr>${headers.map((header) => `<th>${header}</th>`).join('')}</tr>
        </thead>
        <tbody>
          ${rows.map((row) => `
            <tr>${row.map((cell) => `<td>${cell}</td>`).join('')}</tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function renderMigrationDetails() {
  return `
    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="pdf-code-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--preview">Preview</span>
        <h3>PDF Code Migration</h3>
        <p>Convert third-party C# PDF SDK generation code into PXA-compatible PDF generator code, then review diagnostics for unsupported provider-specific features.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have existing C# PDF generation code.</li><li>You want generated PXA code as a starting point.</li><li>The source mostly creates pages, draws text, lines, rectangles, images, and saves output.</li></ul></article>
        <article><h4>Typical output</h4><ul><li>PXA-compatible PDF code.</li><li>Previewable PDF output where deterministic.</li><li>Diagnostics for manual follow-up patterns.</li></ul></article>
        <article><h4>Manual follow-ups</h4><ul><li>Existing-PDF editing.</li><li>Digital signatures and advanced forms.</li><li>HTML/Razor/URL rendering and provider runtime services.</li></ul></article>
      </div>
      ${renderMigrationProviderTable(['Provider', 'Status', 'Mapped patterns', 'Manual / diagnostic areas'], pdfCodeMigrationProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="nutrient-viewer-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--preview">Beta</span>
        <h3>Nutrient Viewer to Designer Migration</h3>
        <p>Convert Nutrient Web SDK and legacy PSPDFKit Viewer workflows without executing them. PXA imports the original PDF, maps supported annotations and form values, and returns one editable design with backend preview and Open in Designer.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>Prerequisites</h4><ul><li>An authenticated PXA Designer session.</li><li>An active organization and Nutrient Designer migration permission.</li><li>The original PDF plus JavaScript, TypeScript, or Instant JSON.</li></ul></article>
        <article><h4>Workflow</h4><ol><li>Open PDF &gt; Designer Migration.</li><li>Select Nutrient Viewer.</li><li>Attach the original PDF and choose the Nutrient input type.</li><li>Convert and inspect the editable design, preview, fidelity, and diagnostics.</li><li>Click Open in Designer or download the design JSON.</li></ol></article>
        <article><h4>Security boundary</h4><ul><li>Uploaded source is parsed, never executed.</li><li>License keys, JWTs, server authentication, unsafe paths, and remote customer URLs are removed.</li><li>Signature material and active annotation actions are never migrated.</li></ul></article>
      </div>
      <h4>Nutrient .NET and GdPicture code</h4>
      ${renderMigrationProviderTable(['Nutrient input', 'PXA output', 'Behavior'], nutrientCodeMappings)}
      <div class="pxa-callout pxa-callout--info"><strong>Existing PDF page editing.</strong> <code>PdfEditableDocument</code> preserves file and stream loading, stream ownership, status comparisons, conditions, page move/delete, blank insertion, cross-document cloning, merging, CropBox read/set/reset with symmetric point/millimeter/centimeter/inch conversion, explicit one-based page-range extraction, AES-256 output, and saving. <code>PdfPageComposer</code> remaps the complete reachable page object graph into collision-free target IDs instead of rebuilding the PDF through Designer elements. Exact CropBox mappings report <code>CANMIGNUTNET070</code>; exact page-range extraction mappings report <code>CANMIGNUTNET071</code>.</div>
      <div class="pxa-callout pxa-callout--warning"><strong>Fail-closed boundaries.</strong> PXA refuses to delete the last page or a referenced page. CropBox operations are limited to non-encrypted input. Page-range extraction remaps references within the selected set but rejects references to outside pages, malformed or duplicate ranges, and encrypted sources. <code>ClonePages</code> retains existing destination pages and appends the selection in range order. Bookmark/barcode/repeating-content split modes remain review cases. Nutrient merge options for including or excluding attachments produce <code>CANMIGNUTNET069</code> until catalog-level attachment migration is equivalent. Full rewrites invalidate existing signatures unless explicitly allowed.</div>
      <div class="pxa-callout pxa-callout--warning"><strong>Trust handoff.</strong> Official Nutrient signature and redaction calls are converted into bounded review evidence with source locations, affected operations, and safe next actions. The migrator does not execute signing, certificate validation, redaction preflight, or irreversible redaction. PFX paths, passwords, private keys, certificate material, and removed text are never copied into generated Trust review artifacts or evidence.</div>
      <h4>Viewer configuration</h4>
      ${renderMigrationProviderTable(['Nutrient input', 'PXA output', 'Behavior'], nutrientViewerConfigurationMappings)}
      <h4>Instant JSON annotations</h4>
      ${renderMigrationProviderTable(['Input', 'Outcome', 'Notes'], nutrientAnnotationMappings)}
      <h4>Instant JSON forms</h4>
      ${renderMigrationProviderTable(['Field', 'Outcome', 'Notes'], nutrientFormMappings)}
      <h4>API</h4>
      <pre class="pxa-code"><code>POST /api/pxa/v1/migrations/pdf/designer/nutrient-viewer/convert
Content-Type: multipart/form-data

pdf=@source.pdf
inputType=instantJson
source={ "format": "https://pspdfkit.com/instant-json/v1", "annotations": [] }</code></pre>
      <p>For TypeScript, PXA Designer compiles the source with the TypeScript Compiler API and submits both the original source and non-executed JavaScript compiler output. API clients must provide <code>transpiledSource</code> themselves; the migration API never executes either representation.</p>
      <h4>Comprehensive synthetic example</h4>
      <p>Use <strong>Load example</strong> in Designer or request <code>GET /api/pxa/v1/migrations/pdf/designer/nutrient-viewer/examples</code>. The examples contain TypeScript Viewer bootstrap and Instant JSON annotations and forms. The original PDF remains mandatory because it provides the editable page content and authoritative page geometry.</p>
      <ol>
        <li>Load the example and convert the TypeScript source. Confirm the document source, initial page, fit mode, toolbar request, event warning, and deterministic checksums.</li>
        <li>Switch to Instant JSON, attach the original PDF, paste the supplied payload, then confirm note, highlight, rectangle, and form conversion.</li>
        <li>Confirm that signature material and the unsupported attachment are absent, and that visual redaction is explicitly marked as non-destructive.</li>
        <li>Submit the GdPicture sample through PDF Code Migration. Confirm automatic page, content, web-link, basic form, and sticky-note mappings plus review markers for appearance gaps, OCR, signatures, and irreversible redaction.</li>
      </ol>
      <div class="pxa-callout pxa-callout--warning"><strong>Synthetic data only.</strong> The example contains no Nutrient binaries, valid license keys, customer URLs, certificates, or customer documents.</div>
      <p>The response contains one <code>design</code> artifact with editable imported PDF content and supported Nutrient overlays. Viewer settings, unmatched form values, and normalized sidecars remain in versioned design metadata; unsupported content is reported through stable diagnostics. The lower-level <code>pdf/viewer/nutrient-viewer</code> package API remains a compatibility and SDK contract, not the primary Designer workflow.</p>
      <h4>Troubleshooting</h4>
      ${renderMigrationProviderTable(['Diagnostic family', 'Meaning'], nutrientDiagnosticRows)}
      <div class="pxa-doc-related-links">
        <a href="../../checklists/PXA.Migration-Nutrient-Code-And-Viewer.md">Nutrient migration roadmap</a>
        <a href="#provider-taxonomy">Provider taxonomy</a>
        <a href="#migration-diagnostics">Migration diagnostics</a>
      </div>
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="report-designer-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Report Designer Migration</h3>
        <p>Convert report designer formats into editable PXA design JSON so the result can be opened in PXA Designer and refined visually.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have visual report definitions, not hand-written PDF code.</li><li>You need editable PXA Designer output.</li><li>You want banded report layout flattened into page elements.</li></ul></article>
        <article><h4>Common mapping</h4><ul><li>Report bands become absolute page positions.</li><li>Labels/textboxes become Text elements.</li><li>Lines, rectangles, tables, charts, barcodes, and images map to closest PXA elements.</li></ul></article>
        <article><h4>Validation workflow</h4><ul><li>Run report-to-design migration.</li><li>Inspect diagnostics and mapping notes.</li><li>Open in Designer and compare layout fidelity.</li></ul></article>
      </div>
      <div class="pxa-callout"><strong>Designer location.</strong> Report conversion remains the technical <code>report/designer</code> API domain, but the user-facing workflow is part of PDF Migration at <code>/pdf/migrations/designer</code>.</div>
      ${renderMigrationProviderTable(['Provider', 'Input', 'Mapped surface', 'Namespace'], reportDesignerProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="spreadsheet-code-migration">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--preview">Preview</span>
        <h3>Spreadsheet Code Migration</h3>
        <p>Convert C# spreadsheet library authoring code into PXA spreadsheet API code and preview workbook-like output through the spreadsheet migration flow.</p>
      </div>
      <div class="pxa-doc-detail__grid">
        <article><h4>When to use</h4><ul><li>You have existing workbook generation code.</li><li>You need PXA spreadsheet API output.</li><li>You want provider-specific object models normalized into one workbook model.</li></ul></article>
        <article><h4>Mapped patterns</h4><ul><li>Workbook and worksheet creation.</li><li>Cell values, formulas, ranges, and basic styles.</li><li>Save/export calls where deterministic.</li></ul></article>
        <article><h4>Datasource boundary</h4><ul><li>Code migration is not datasource/file import.</li><li>Spreadsheet datasource namespace is reserved.</li><li>File import stays in spreadsheet/importer workflows.</li></ul></article>
      </div>
      ${renderMigrationProviderTable(['Provider', 'Status', 'Namespace', 'Mapped surface'], spreadsheetCodeMigrationProviders)}
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="provider-taxonomy">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Provider Taxonomy</h3>
        <p>Migration namespaces identify domain, migration kind, and provider so code migration and designer migration are not confused.</p>
      </div>
      ${renderMigrationProviderTable(['Area', 'Namespace pattern', 'Meaning'], taxonomyRows)}
      <pre class="pxa-code"><code>PXA.Migration.&lt;Domain&gt;.&lt;Kind&gt;.&lt;Provider&gt;

PXA.Migration.Pdf.Code.Aspose
PXA.Migration.Pdf.Viewer.Nutrient
PXA.Migration.Report.Designer.Rdl
PXA.Migration.Spreadsheet.Code.Syncfusion</code></pre>
      <h4>Canonical API</h4>
      ${renderMigrationProviderTable(['Operation', 'Route', 'Purpose'], migrationApiRows)}
      <p>The route itself separates PDF, Spreadsheet, and Report. Provider aliases are accepted only at input boundaries; responses always return the canonical provider key.</p>
      <div class="pxa-callout pxa-callout--warning"><strong>Compatibility routes.</strong> <code>/api/migration/frameworks</code>, <code>/api/migration/convert</code>, <code>/api/migration/preview</code>, <code>/api/migration/report-to-design</code>, and the former Nutrient Viewer convert route remain adapters for one major release. New integrations should use the v1 routes above.</div>
    </section>

    <section class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="migration-diagnostics">
      <div class="pxa-doc-detail__header">
        <span class="pxa-status pxa-status--ready">Ready</span>
        <h3>Migration Diagnostics</h3>
        <p>Diagnostics explain what was converted, what needs review, and which provider features must be migrated manually.</p>
      </div>
      ${renderMigrationProviderTable(['Signal', 'Meaning'], diagnosticRows)}
      <div class="pxa-doc-related-links">
        <a href="../../docs/guides/PXA.Unified-Migration-Provider-Coverage.md">Provider coverage report</a>
        <a href="../../checklists/Migration-Namespace-Taxonomy.md">Namespace taxonomy checklist</a>
        <a href="../../checklists/PXA.Unified-Migration-Frameworks.md">Unified migration roadmap</a>
        <a href="../../checklists/Designer-Migration.md">Designer migration roadmap</a>
        <a href="../../checklists/Spreadsheet-Migration.md">Spreadsheet migration checklist</a>
      </div>
    </section>
  `;
}

function slug(value) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
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
            ${renderDetailNavList(editorSections)}
            ${renderElementNav(elementReferenceDocs)}
            <strong>Code SDK</strong>
            <a class="pxa-doc-nav__featured" href="#code-designer-workspace">Code Designer Workspace</a>
            ${renderCodeSdkNav(codeSdkGroups, exportSdkDocs)}
            <strong>Migration</strong>
            ${renderNavList(migrationGuides)}
            <strong>Product updates</strong>
            ${renderReleaseNav()}
          </nav>
          <p class="pxa-doc-search-empty" hidden>No documentation entries found.</p>
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

          <section class="pxa-doc-section" id="release-notes">
            <p class="pxa-kicker">Product updates</p>
            <h2 class="pxa-heading">PXA Designer release notes</h2>
            <p class="pxa-lede">Review curated Designer changes by version and release channel. API compatibility versions are managed separately from the Designer product version.</p>
            ${renderDesignerReleaseNotes()}
            <article class="pxa-card pxa-doc-detail" id="designer-feature-status">
              <div class="pxa-doc-detail__header">
                <span class="pxa-status pxa-status--ready">Reference</span>
                <h3>Designer feature status</h3>
                <p>Feature maturity describes expected stability. It does not replace subscription entitlements or provider coverage statuses.</p>
              </div>
              <div class="pxa-doc-release-status-grid">
                <section><h4>New</h4><p>A temporary marker for a recently introduced capability. It can appear together with Alpha or Beta.</p></section>
                <section><h4>Alpha</h4><p>Disabled by default. The organization must allow access and each user must opt in explicitly.</p></section>
                <section><h4>Beta</h4><p>Enabled by default and suitable for evaluation, but its behavior or contract may still change.</p></section>
                <section><h4>Stable</h4><p>Supported production behavior. Stable features do not display a maturity badge.</p></section>
              </div>
            </article>
            <article class="pxa-card pxa-doc-detail" id="designer-notifications">
              <div class="pxa-doc-detail__header">
                <span class="pxa-status pxa-status--ready">Ready</span>
                <h3>Toasts and notifications</h3>
                <p>Toasts confirm immediate actions. Releases and important system, security, subscription, or action-required messages remain available in the notification center.</p>
              </div>
              <ol>
                <li>Use the bell in the Designer header to open the notification center.</li>
                <li>Open a message to mark it as read, or use Mark all as read.</li>
                <li>Dismiss only messages that explicitly permit dismissal.</li>
                <li>Use What's New in the user menu to reopen release notes at any time.</li>
              </ol>
            </article>
          </section>

          <section class="pxa-doc-section" id="editor-path">
            <p class="pxa-kicker">Editor documentation</p>
            <h2 class="pxa-heading">Product guides for visual document workflows</h2>
            ${renderTrackGuide(trackGuides.editor)}
            <div class="pxa-doc-detail-stack">
              ${renderDetailedDocs(editorDocs)}
              ${renderElementReference(elementReferenceDocs)}
            </div>
          </section>

          <section class="pxa-doc-section" id="code-path">
            <p class="pxa-kicker">Code documentation</p>
            <h2 class="pxa-heading">SDK examples ordered by product area</h2>
            ${renderTrackGuide(trackGuides.code)}
            <div class="pxa-doc-detail-stack">
              <section class="pxa-card pxa-doc-detail" id="code-designer-workspace">
                <div class="pxa-doc-detail__header">
                  <span class="pxa-status pxa-status--preview">Beta</span>
                  <h3>Code Designer Workspace</h3>
                  <p>Keep independent JSON, readable C# Model, semantic C# PDF, and FromBase64String drafts beside a saved visual template, then convert and apply changes explicitly.</p>
                </div>
                <div class="pxa-doc-detail__grid">
                  <article><h4>Draft workflow</h4><ol><li>Save the visual template.</li><li>Edit one language without changing the others.</li><li>Validate or run the active draft.</li><li>Convert and review added, changed, and removed elements.</li><li>Apply the canonical result to Designer.</li></ol></article>
                  <article><h4>Four representations</h4><ul><li><strong>JSON:</strong> canonical <code>DesignExportDto</code>.</li><li><strong>C# Model:</strong> editable typed object initializer without hidden JSON.</li><li><strong>C# PDF:</strong> exact <code>PxaPdfCodeBuilder</code> with legacy low-level compatibility.</li><li><strong>FromBase64String:</strong> compact, strictly validated transport source.</li></ul></article>
                  <article><h4>Preview freshness</h4><p>JSON updates its Preview while you edit. C# Model, C# PDF, and FromBase64String show only the canonical result of the exact source last executed with <strong>Run</strong>. Editing that source invalidates the previous result, so stale generated output is never presented as current.</p></article>
                  <article><h4>Layout parity</h4><p>The generated-code Preview preserves the same complete document settings and individual page layouts as Designer and backend PDF export, including mixed dimensions, orientation, margins, backgrounds, bleed, localization, RTL, bindings, protection, and extension data.</p></article>
                  <article><h4>Export code panel</h4><p>The JSON tab exports the complete canonical <code>DesignExportDto</code> without removing content from other languages. The C# tab sends that exact JSON to the authenticated backend and returns semantic <code>PxaPdfCodeBuilder</code> code. Save a new document first so tenant access and feature gates can be enforced.</p></article>
                  <article><h4>Fidelity</h4><p><code>documentFidelity</code> reports the resulting document as exact, compatible, review required, or unsupported. <code>sourcePreservation</code> separately reports preserved, regenerated, or structure lost, so exact output never implies that handwritten code structure survived.</p></article>
                  <article><h4>Sandbox</h4><p>C# runs outside WebApi with a 32 MiB source limit, 10 MiB canonical document limit, 15-second execution limit, and bounded page, element, and output counts. Files, network, processes, environment, reflection, native interop, threads, unsafe code, dynamic dispatch, and package references are blocked.</p></article>
                  <article><h4>Assets and conflicts</h4><p>Use tenant-owned Designer asset IDs, never paths or URLs. Autosave updates only the active draft. HTTP 409 protects both template and workspace revisions from stale writes.</p></article>
                </div>
                <p><strong>API:</strong> <code>/api/pxa/v1/designer/templates/{templateId}/code-workspace</code> provides load, save, validate, convert, execute, apply, restore, and source-map operations.</p>
              </section>
              ${renderCodeSdkGroups(codeSdkGroups)}
              ${renderExportSdkDocs(exportSdkDocs)}
            </div>
          </section>

          <section class="pxa-doc-section" id="migration">
            <p class="pxa-kicker">Migration guides</p>
            <h2 class="pxa-heading">Provider-oriented migration documentation</h2>
            ${renderTrackGuide(trackGuides.migration)}
            <div class="pxa-card pxa-doc-detail pxa-doc-migration-detail" id="migration-workflow">
              <div class="pxa-doc-detail__header">
                <span class="pxa-status pxa-status--ready">Ready</span>
                <h3>Common Migration Workflow</h3>
                <p>Every migration path follows the same review loop: choose the source technology, convert, inspect diagnostics, then open or export the result.</p>
              </div>
              ${renderMigrationProviderTable(['Step', 'What happens'], migrationWorkflowSteps)}
            </div>
            <div class="pxa-doc-detail-stack">
              ${renderMigrationDetails()}
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
            ${renderPageToc()}
          </div>
        </aside>
      </div>
    </main>

    ${renderPxaFooter('PXA.Documentation')}
  </div>
`;
if (window.location.pathname !== '/') {
  document.title = 'Page not found | PXA Documentation';
  document.querySelector('#app').innerHTML = renderStatusPage('notFound', {
    product: 'PXA.Documentation',
    homeHref: '/',
    supportHref: companyPage('support'),
    allowedOrigins: new URL(siteLinks.company).origin,
  });
  bindStatusPage();
} else {
  initializeStorageNotice();
  initDocumentationScrollSpy();
  initDocumentationSearch();
  initReleaseFilters();
  initCodeSdkPlaygrounds();
  initElementJsonPlaygrounds();
}

function initReleaseFilters() {
  const buttons = [...document.querySelectorAll('[data-release-filter]')];
  const releases = [...document.querySelectorAll('[data-release-channel]')];
  buttons.forEach((button) => {
    button.addEventListener('click', () => {
      const channel = button.dataset.releaseFilter;
      buttons.forEach((item) => item.classList.toggle('is-active', item === button));
      releases.forEach((release) => {
        release.classList.toggle(
          'is-release-filtered',
          channel !== 'all' && release.dataset.releaseChannel !== channel,
        );
      });
    });
  });
}

function initElementJsonPlaygrounds() {
  const playgrounds = [...document.querySelectorAll('.pxa-element-json-playground')];
  playgrounds.forEach((playground) => {
    const item = elementReferenceDocs.find((entry) => entry.title === playground.dataset.elementTitle);
    if (!item) return;

    const editor = playground.querySelector('.pxa-element-json-editor');
    const status = playground.querySelector('.pxa-code-run-status');
    const preview = playground.querySelector('[data-element-preview]');

    playground.querySelector('.pxa-element-json-run')?.addEventListener('click', () => {
      try {
        const json = JSON.parse(editor.value);
        preview.innerHTML = renderElementVisual(elementVisualFromJson(item, json));
        status.textContent = 'Updated';
        status.classList.remove('is-warning');
      } catch (error) {
        status.textContent = `JSON error: ${error.message}`;
        status.classList.add('is-warning');
      }
    });
  });
}

function elementVisualFromJson(item, json) {
  const style = json.style && typeof json.style === 'object' ? json.style : {};
  const type = String(json.type ?? item.type ?? '').toLowerCase();
  const preview =
    json.content ??
    json.value ??
    json.label ??
    json.fieldLabel ??
    json.placeholder ??
    json.barcodeValue ??
    json.watermarkText ??
    json.href ??
    json.name ??
    item.visual.preview;

  const properties = [
    `type: ${json.type ?? item.type}`,
    json.x !== undefined || json.y !== undefined ? `position: ${json.x ?? 0}, ${json.y ?? 0}` : null,
    json.width !== undefined || json.height !== undefined ? `size: ${json.width ?? '-'} x ${json.height ?? '-'}` : null,
    style.fontSize ? `fontSize: ${style.fontSize}` : null,
    style.color ? `color: ${style.color}` : null,
    style.backgroundColor ? `background: ${style.backgroundColor}` : null,
    json.binding ? `binding: ${json.binding}` : null,
  ].filter(Boolean);

  return {
    title: `${item.title} preview`,
    toolbar: item.visual.toolbar,
    preview: elementPreviewMarkup(type, String(preview)),
    properties: properties.length ? properties : item.visual.properties,
  };
}

function elementPreviewMarkup(type, value) {
  const safeValue = escapeHtml(value);
  if (type.includes('image')) return `<span class="pxa-element-preview-image">${safeValue || 'Image'}</span>`;
  if (type.includes('table')) return `<span class="pxa-element-preview-table">${safeValue || 'Table'}</span>`;
  if (type.includes('chart')) return `<span class="pxa-element-preview-chart">${safeValue || 'Chart'}</span>`;
  if (type.includes('barcode')) return `<span class="pxa-element-preview-barcode">|||| ||| ||||<small>${safeValue}</small></span>`;
  if (type.includes('qr')) return `<span class="pxa-element-preview-qr">${safeValue || 'QR'}</span>`;
  if (type.includes('checkbox')) return `<span class="pxa-element-preview-field">[x] ${safeValue || 'Checked'}</span>`;
  if (type.includes('field') || type.includes('input') || type.includes('dropdown')) return `<span class="pxa-element-preview-field">${safeValue || 'Field'}</span>`;
  if (type.includes('line')) return '<span class="pxa-element-preview-line"></span>';
  if (type.includes('shape') || type.includes('rectangle') || type.includes('area') || type.includes('highlight')) return `<span class="pxa-element-preview-shape">${safeValue || 'Shape'}</span>`;
  if (type.includes('watermark')) return `<span class="pxa-element-preview-watermark">${safeValue || 'DRAFT'}</span>`;
  return safeValue;
}

function initCodeSdkPlaygrounds() {
  const playgrounds = [...document.querySelectorAll('.pxa-code-sdk-example')];
  playgrounds.forEach((playground) => {
    const example = codeSdkExamples[Number(playground.dataset.exampleIndex)];
    if (!example) return;

    const editor = playground.querySelector('.pxa-code-editor');
    const status = playground.querySelector('.pxa-code-run-status');
    const languageButtons = [...playground.querySelectorAll('[data-language]')];
    const outputButtons = [...playground.querySelectorAll('[data-output]')];
    const outputPanels = [...playground.querySelectorAll('[data-output-panel]')];

    const setOutput = (name) => {
      outputButtons.forEach((button) => button.classList.toggle('is-active', button.dataset.output === name));
      outputPanels.forEach((panel) => panel.classList.toggle('is-active', panel.dataset.outputPanel === name));
    };

    const run = () => {
      const state = parseCodeExample(example, editor.value);
      playground.querySelector('[data-output-panel="preview"]').innerHTML = renderCodePreview(state.preview);
      playground.querySelector('[data-output-panel="json"] code').textContent = JSON.stringify(state.json, null, 2);
      playground.querySelector('[data-output-panel="model"] code').textContent = state.model;
      status.textContent = state.warning ? `Parser warning: ${state.warning}` : 'Updated';
      status.classList.toggle('is-warning', Boolean(state.warning));
      setOutput('preview');
    };

    languageButtons.forEach((button) => {
      button.addEventListener('click', () => {
        languageButtons.forEach((item) => item.classList.toggle('is-active', item === button));
        if (button.dataset.language === 'csharp') {
          editor.value = example.csharp;
          editor.disabled = false;
          status.textContent = 'Ready';
        } else {
          editor.value = `${button.textContent} examples are coming soon.`;
          editor.disabled = true;
          status.textContent = `${button.textContent} coming soon`;
        }
      });
    });

    outputButtons.forEach((button) => {
      button.addEventListener('click', () => setOutput(button.dataset.output));
    });

    playground.querySelector('.pxa-code-run')?.addEventListener('click', run);
  });
}

function parseCodeExample(example, code) {
  const baseJson = JSON.parse(example.json);
  const result = {
    preview: { ...example.preview },
    json: baseJson,
    model: example.model,
    warning: '',
  };

  const firstString = (pattern) => code.match(pattern)?.[1];
  const allRows = [...code.matchAll(/\.Row\(([^)]+)\)/g)]
    .map((match) => [...match[1].matchAll(/"([^"]*)"/g)].map((item) => item[1]))
    .filter((row) => row.length);
  const dataset = code.match(/\.Dataset\("([^"]+)",\s*([\d.]+),\s*([\d.]+)\)/);

  if (example.title === 'Text') {
    const value = firstString(/\.Text\("([^"]+)"\)/);
    if (value) {
      result.preview.value = value;
      result.json.content = value;
    } else {
      result.warning = 'Could not find .Text("...").';
    }
  } else if (example.title === 'Image') {
    const value = firstString(/\.Image\("([^"]+)"\)/);
    if (value) {
      result.preview.value = value.includes('logo') ? 'LOGO' : 'IMAGE';
      result.json.content = value;
    } else {
      result.warning = 'Could not find .Image("...").';
    }
  } else if (example.title === 'Table') {
    if (allRows.length) {
      result.preview.rows = allRows;
      result.json.cellData = allRows;
    } else {
      result.warning = 'Could not find table.Row(...).';
    }
  } else if (example.title === 'Chart') {
    if (dataset) {
      const values = [Number(dataset[2]), Number(dataset[3])];
      result.preview.values = values;
      result.json.chart.series[0].name = dataset[1];
      result.json.chart.series[0].values = values;
    } else {
      result.warning = 'Could not find .Dataset("Label", n, n).';
    }
  } else if (example.title === 'Barcode and QR') {
    const value = firstString(/\.Barcode\("([^"]+)"\s*,/);
    if (value) {
      result.preview.value = value;
      result.json.barcodeValue = value;
    } else {
      result.warning = 'Could not find .Barcode("...").';
    }
  } else if (example.title === 'Form Field') {
    const fieldName = firstString(/\.TextField\("([^"]+)"\)/);
    const label = firstString(/\.Label\("([^"]+)"\)/);
    const placeholder = firstString(/\.Placeholder\("([^"]+)"\)/);
    if (fieldName || label || placeholder) {
      result.preview.value = label ?? fieldName ?? result.preview.value;
      result.preview.placeholder = placeholder ?? result.preview.placeholder;
      if (fieldName) result.json.fieldName = fieldName;
      if (label) result.json.fieldLabel = label;
      if (placeholder) result.json.placeholder = placeholder;
    } else {
      result.warning = 'Could not find TextField, Label, or Placeholder calls.';
    }
  } else if (example.title === 'Line and Shape') {
    const fill = firstString(/\.Fill\("([^"]+)"\)/);
    const stroke = firstString(/\.Stroke\("([^"]+)"/);
    if (fill || stroke) {
      result.preview.value = fill ? `Fill ${fill}` : `Stroke ${stroke}`;
      if (stroke) result.json.style.borderColor = stroke;
    } else {
      result.warning = 'Could not find .Fill("...") or .Stroke("...").';
    }
  } else if (example.title === 'Watermark') {
    const value = firstString(/\.Watermark\("([^"]+)"\)/);
    if (value) {
      result.preview.value = value;
      result.json.content = value;
    } else {
      result.warning = 'Could not find .Watermark("...").';
    }
  } else if (example.title === 'Workbook and Cells') {
    const workbookName = firstString(/CreateWorkbook\("([^"]+)"\)/);
    const sheetName = firstString(/AddSheet\("([^"]+)"\)/);
    const firstValue = firstString(/\.Value\("([^"]+)"\)/);
    if (workbookName || sheetName || firstValue) {
      if (workbookName) {
        result.preview.value = workbookName;
        result.json.name = workbookName;
      }
      if (sheetName) result.json.sheets[0].name = sheetName;
      if (firstValue) {
        result.preview.rows = [['A1', firstValue], ['B2', '19.80']];
        result.json.sheets[0].cells[0].value = firstValue;
      }
    } else {
      result.warning = 'Could not find CreateWorkbook, AddSheet, or Value calls.';
    }
  } else if (example.title === 'Formula and Layout') {
    const workbookName = firstString(/CreateWorkbook\("([^"]+)"\)/);
    const sheetName = firstString(/AddSheet\("([^"]+)"\)/);
    const formula = firstString(/\.Formula\("([^"]+)"\)/);
    if (workbookName || sheetName || formula) {
      if (workbookName) {
        result.preview.value = workbookName;
        result.json.name = workbookName;
      }
      if (sheetName) result.json.sheets[0].name = sheetName;
      if (formula) {
        result.preview.rows = [['A1:C1', 'Merged title'], ['C4', `=${formula}`]];
        result.json.sheets[0].cells[1].formula = formula;
      }
    } else {
      result.warning = 'Could not find CreateWorkbook, AddSheet, or Formula calls.';
    }
  } else if (example.category === 'Template SDK') {
    const designName = firstString(/Name\s*=\s*"([^"]+)"/);
    const content = firstString(/Content\s*=\s*"([^"]+)"/);
    const binding = firstString(/Binding\s*=\s*"([^"]+)"/);
    if (designName || content || binding) {
      result.preview.value = designName ?? content ?? binding ?? result.preview.value;
      result.preview.rows = [
        ['Design', designName ?? 'Template'],
        ['Content', content ?? 'Localized or bound content'],
        ['Binding', binding ?? 'No binding'],
      ];
      result.json.summary = { designName, content, binding };
    } else {
      result.warning = 'Could not find DesignExportDto Name, Content, or Binding assignments.';
    }
  } else if (example.category === 'Spreadsheet SDK') {
    const workbookName = firstString(/CreateWorkbook\("([^"]+)"\)/);
    const sheetName = firstString(/AddSheet\("([^"]+)"\)/);
    const firstValue = firstString(/\.Value\("([^"]+)"\)/);
    if (workbookName || sheetName || firstValue) {
      if (workbookName && 'name' in result.json) result.json.name = workbookName;
      result.preview.value = workbookName ?? sheetName ?? firstValue ?? result.preview.value;
      result.preview.rows = [
        ['Workbook', workbookName ?? result.preview.value],
        ['Sheet', sheetName ?? 'Sheet1'],
        ['Value', firstValue ?? 'Updated from code'],
      ];
    } else {
      result.warning = 'Could not find a workbook, sheet, or value call.';
    }
  }

  return result;
}

function initDocumentationSearch() {
  const search = document.querySelector('.pxa-search');
  const nav = document.querySelector('.pxa-doc-nav');
  const empty = document.querySelector('.pxa-doc-search-empty');
  if (!search || !nav) return;

  const links = [...nav.querySelectorAll('a')];
  const headings = [...nav.querySelectorAll('strong')];
  const sections = [...nav.querySelectorAll('details')];

  const setFiltered = (element, hidden) => {
    element.classList.toggle('is-search-hidden', hidden);
    element.setAttribute('aria-hidden', hidden ? 'true' : 'false');
  };

  const applyFilter = () => {
    const query = search.value.trim().toLowerCase();
    let visibleLinks = 0;

    links.forEach((link) => {
      const matches = !query || link.textContent.toLowerCase().includes(query);
      setFiltered(link, !matches);
      if (matches) visibleLinks += 1;
    });

    sections.forEach((section) => {
      const visibleChildLinks = [...section.querySelectorAll('a')].some((link) => !link.classList.contains('is-search-hidden'));
      const summaryMatches = section.querySelector('summary')?.textContent.toLowerCase().includes(query) ?? false;
      const isVisible = !query || visibleChildLinks || summaryMatches;
      setFiltered(section, !isVisible);
      if (query && isVisible) section.setAttribute('open', '');
    });

    headings.forEach((heading) => {
      let next = heading.nextElementSibling;
      let hasVisibleItem = false;
      while (next && next.tagName !== 'STRONG') {
        if (!next.classList.contains('is-search-hidden')) hasVisibleItem = true;
        next = next.nextElementSibling;
      }
      setFiltered(heading, query ? !hasVisibleItem : false);
    });

    if (empty) empty.hidden = !query || visibleLinks > 0;
  };

  search.addEventListener('input', applyFilter);
  search.addEventListener('keyup', applyFilter);
  search.addEventListener('search', applyFilter);
}

function initDocumentationScrollSpy() {
  const links = [...document.querySelectorAll('.pxa-doc-nav a[href^="#"], .pxa-doc-toc a[href^="#"]')];
  const sidebarLinks = [...document.querySelectorAll('.pxa-doc-nav a[href^="#"]')];
  const elementReference = document.querySelector('.pxa-doc-element-reference');
  const elementCards = [...document.querySelectorAll('.pxa-doc-element-card')];
  const contentBlocks = [...document.querySelectorAll('.pxa-docs-content > .pxa-doc-hero-grid, .pxa-docs-content > .pxa-doc-section')];
  const targets = links
    .map((link) => document.querySelector(link.getAttribute('href')))
    .filter(Boolean);

  if (!links.length || !targets.length) return;

  let activeSidebarFocusId = '';

  const isElementCardId = (id) => elementCards.some((card) => card.id === id);
  const isSidebarTargetId = (id) => {
    if (sidebarLinks.some((link) => link.getAttribute('href') === `#${id}`)) return true;
    return Boolean(document.getElementById(id)?.matches('.pxa-doc-sdk-group, .pxa-doc-sdk-subgroup'));
  };

  const getFocusContainer = (id) => {
    const target = document.getElementById(id);
    if (!target) return null;
    return target.closest('.pxa-doc-element-card, .pxa-doc-detail, .pxa-doc-sdk-subgroup, .pxa-doc-sdk-group, .pxa-doc-card, .pxa-doc-section');
  };

  const focusContainers = [...document.querySelectorAll('.pxa-doc-detail, .pxa-doc-sdk-subgroup, .pxa-doc-sdk-group, .pxa-doc-card, .pxa-doc-element-card')];

  const applySidebarFocus = (id, enabled) => {
    const selected = enabled ? getFocusContainer(id) : null;
    const selectedContentBlock = selected?.closest('.pxa-doc-section, .pxa-doc-hero-grid') ?? null;
    activeSidebarFocusId = selected ? id : '';
    elementReference?.classList.toggle('is-filtered', Boolean(selected && isElementCardId(id)));
    contentBlocks.forEach((block) => {
      const isHidden = Boolean(selectedContentBlock && block !== selectedContentBlock);
      block.classList.toggle('is-hidden-by-filter', isHidden);
      block.setAttribute('aria-hidden', isHidden ? 'true' : 'false');
    });
    focusContainers.forEach((container) => {
      const isFocused = container === selected;
      const isAncestorOfSelected = Boolean(selected && container.contains(selected));
      const isSdkOverview = selected?.classList.contains('pxa-doc-sdk-group') || selected?.classList.contains('pxa-doc-sdk-subgroup');
      const selectedHasSingleExample = Boolean(
        selected?.classList.contains('pxa-doc-sdk-subgroup') && selected.querySelectorAll('.pxa-code-sdk-example').length === 1,
      );
      const isDescendantOfSelected = Boolean(selected && selected.contains(container) && (!isSdkOverview || selectedHasSingleExample));
      const isHidden = Boolean(selected && !isFocused && !isAncestorOfSelected && !isDescendantOfSelected);
      container.classList.toggle('is-focused', isFocused);
      container.classList.toggle('is-hidden-by-filter', isHidden);
      container.setAttribute('aria-hidden', isHidden ? 'true' : 'false');
    });
  };

  const closeSiblingElementGroups = (activeLink) => {
    const activeGroup = activeLink?.closest('.pxa-doc-nav__section--nested');
    document.querySelectorAll('.pxa-doc-nav__section--nested[open]').forEach((section) => {
      if (section !== activeGroup) section.removeAttribute('open');
    });
  };

  const setActive = (id, options = {}) => {
    if (options.fromObserver && activeSidebarFocusId) {
      return;
    }

    applySidebarFocus(id, Boolean(options.fromSidebar));
    links.forEach((link) => {
      const isActive = link.getAttribute('href') === `#${id}`;
      link.classList.toggle('is-active', isActive);
      if (isActive) {
        link.closest('details')?.setAttribute('open', '');
        link.closest('details')?.parentElement?.closest('details')?.setAttribute('open', '');
        if (isElementCardId(id)) closeSiblingElementGroups(link);
      }
    });
  };

  links.forEach((link) => {
    link.addEventListener('click', () => {
      const id = link.getAttribute('href')?.slice(1);
      if (id) setActive(id, { fromSidebar: link.closest('.pxa-doc-nav') !== null });
    });
  });

  const initialId = window.location.hash.slice(1) || 'overview';
  if (document.getElementById(initialId)) setActive(initialId, { fromSidebar: isSidebarTargetId(initialId) });

  window.addEventListener('hashchange', () => {
    const id = window.location.hash.slice(1) || 'overview';
    if (document.getElementById(id)) setActive(id, { fromSidebar: isSidebarTargetId(id) });
  });

  if (!('IntersectionObserver' in window)) return;

  const observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);

      if (visible[0]?.target?.id) {
        setActive(visible[0].target.id, { fromObserver: true });
      }
    },
    {
      rootMargin: '-18% 0px -68% 0px',
      threshold: [0, 0.1, 0.25, 0.5],
    },
  );

  targets.forEach((target) => observer.observe(target));
}
