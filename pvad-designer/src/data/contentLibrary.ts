import type { Page, SimpleElement } from '@/types';
import { getTemplateElementsLocalized, getTemplatePagesLocalized } from './templateContent.i18n';
import { TEMPLATES } from './templates';

export type ContentCategory = 'forms' | 'invoices' | 'books' | 'certificates' | 'presentations' | 'reports';
export type ContentKind = 'block' | 'pageKit';

export interface ContentLibraryEntry {
  id: string;
  category: ContentCategory;
  kind: ContentKind;
  titleKey: string;
  descriptionKey: string;
  keywords: readonly string[];
  supportedModes: readonly ('pdf' | 'word')[];
  sourceTemplateId?: string;
  sourceTemplateIds?: readonly string[];
  sourceSize?: { width: number; height: number };
  configurable?: boolean;
  buildElements?: (options?: { fieldLabels?: string[] }) => SimpleElement[];
}

const text = (id: string, content: string, x: number, y: number, width: number, height: number, fontSize = 16): SimpleElement => ({
  id, type: 'text', content, x, y, width, height, style: { fontSize, color: '#172033' },
});

const field = (id: string, label: string, name: string, x: number, y: number, width = 250): SimpleElement => ({
  id, type: 'field', fieldLabel: label, fieldName: name, x, y, width, height: 44, style: { fontSize: 12 },
});

const table = (id: string, x: number, y: number, width: number, height: number): SimpleElement => ({
  id, type: 'table', x, y, width, height,
  cellData: [
    ['Description', 'Quantity', 'Amount'],
    ['Service', '1', '0.00'],
  ],
  style: { rows: 2, columns: 3, fontSize: 11, borderColor: '#cbd5e1', borderWidth: 1 },
});

const chart = (id: string, x: number, y: number, width: number, height: number): SimpleElement => ({
  id, type: 'chart', x, y, width, height, chartType: 'bar',
  chartData: { categories: ['A', 'B', 'C'], series: [{ id: 'series-1', name: 'Value', values: [42, 68, 54] }] },
});

export const CONTENT_CATEGORIES: readonly ContentCategory[] = [
  'forms', 'invoices', 'books', 'certificates', 'presentations', 'reports',
];

export const CONTENT_LIBRARY: readonly ContentLibraryEntry[] = [
  { id: 'form-address', category: 'forms', kind: 'block', titleKey: 'address', descriptionKey: 'addressDescription', keywords: ['address', 'contact', 'postal'], supportedModes: ['pdf', 'word'], buildElements: () => [field('name', 'Full name', 'name', 0, 0), field('street', 'Street', 'street', 0, 56), field('city', 'City', 'city', 0, 112), field('postal', 'Postal code', 'postal_code', 0, 168), field('country', 'Country', 'country', 0, 224)] },
  { id: 'form-contact', category: 'forms', kind: 'block', titleKey: 'contact', descriptionKey: 'contactDescription', keywords: ['email', 'phone', 'website'], supportedModes: ['pdf', 'word'], buildElements: () => [field('phone', 'Phone', 'phone', 0, 0), field('email', 'Email', 'email', 0, 56), field('website', 'Website', 'website', 0, 112)] },
  { id: 'form-personal', category: 'forms', kind: 'block', titleKey: 'personal', descriptionKey: 'personalDescription', keywords: ['personal', 'identity', 'profile'], supportedModes: ['pdf', 'word'], buildElements: () => [field('dob', 'Date of birth', 'date_of_birth', 0, 0), field('nationality', 'Nationality', 'nationality', 0, 56), field('occupation', 'Occupation', 'occupation', 0, 112)] },
  { id: 'form-custom', category: 'forms', kind: 'block', titleKey: 'customFields', descriptionKey: 'customFieldsDescription', keywords: ['custom', 'fields', 'form'], supportedModes: ['pdf', 'word'], configurable: true, buildElements: options => (options?.fieldLabels ?? ['First field', 'Second field', 'Third field']).filter(Boolean).map((label, index) => field(`custom-${index}`, label, `custom_${index + 1}`, 0, index * 56)) },
  { id: 'form-page', category: 'forms', kind: 'pageKit', titleKey: 'formPage', descriptionKey: 'formPageDescription', keywords: ['form', 'application', 'complete'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'form-job-application', sourceSize: { width: 595, height: 842 } },

  { id: 'invoice-header', category: 'invoices', kind: 'block', titleKey: 'invoiceHeader', descriptionKey: 'invoiceHeaderDescription', keywords: ['invoice', 'header', 'number'], supportedModes: ['pdf', 'word'], buildElements: () => [text('title', 'INVOICE', 0, 0, 250, 42, 28), text('number', 'Invoice #{{invoice.number}}', 300, 4, 210, 24, 12), text('date', '{{invoice.date}}', 300, 28, 210, 20, 11)] },
  { id: 'invoice-customer', category: 'invoices', kind: 'block', titleKey: 'customerDetails', descriptionKey: 'customerDetailsDescription', keywords: ['customer', 'billing', 'address'], supportedModes: ['pdf', 'word'], buildElements: () => [text('bill-to', 'BILL TO', 0, 0, 100, 20, 11), text('customer', '{{customer.name}}\n{{customer.address}}\n{{customer.city}}', 0, 26, 250, 72, 12)] },
  { id: 'invoice-items', category: 'invoices', kind: 'block', titleKey: 'invoiceItems', descriptionKey: 'invoiceItemsDescription', keywords: ['items', 'table', 'lines'], supportedModes: ['pdf', 'word'], buildElements: () => [table('items', 0, 0, 500, 150)] },
  { id: 'invoice-totals', category: 'invoices', kind: 'block', titleKey: 'invoiceTotals', descriptionKey: 'invoiceTotalsDescription', keywords: ['total', 'tax', 'amount'], supportedModes: ['pdf', 'word'], buildElements: () => [text('subtotal', 'Subtotal\nTax\nTOTAL', 0, 0, 110, 78, 12), text('amounts', '{{invoice.subtotal}}\n{{invoice.tax}}\n{{invoice.total}}', 120, 0, 120, 78, 12)] },
  { id: 'invoice-page', category: 'invoices', kind: 'pageKit', titleKey: 'invoicePage', descriptionKey: 'invoicePageDescription', keywords: ['invoice', 'complete', 'page'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'invoice-freelancer', sourceSize: { width: 595, height: 842 } },

  { id: 'book-chapter-heading', category: 'books', kind: 'block', titleKey: 'chapterHeading', descriptionKey: 'chapterHeadingDescription', keywords: ['book', 'chapter', 'heading'], supportedModes: ['pdf', 'word'], buildElements: () => [text('chapter-number', 'CHAPTER 01', 0, 0, 280, 24, 12), text('chapter-title', 'Chapter title', 0, 34, 420, 58, 30)] },
  { id: 'book-body', category: 'books', kind: 'block', titleKey: 'bodySection', descriptionKey: 'bodySectionDescription', keywords: ['book', 'body', 'paragraph'], supportedModes: ['pdf', 'word'], buildElements: () => [{ id: 'body', type: 'richtext', x: 0, y: 0, width: 420, height: 220, htmlContent: '<h2>Section heading</h2><p>Write your chapter content here.</p>', style: { fontSize: 12, lineHeight: 1.5 } }] },
  { id: 'book-bibliography', category: 'books', kind: 'pageKit', titleKey: 'bibliographyPage', descriptionKey: 'bibliographyPageDescription', keywords: ['book', 'references', 'bibliography'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'book-bibliography', sourceSize: { width: 420, height: 595 } },
  { id: 'book-kit', category: 'books', kind: 'pageKit', titleKey: 'bookKit', descriptionKey: 'bookKitDescription', keywords: ['book', 'chapters', 'multipage'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'book-10page', sourceSize: { width: 420, height: 595 } },

  { id: 'certificate-recipient', category: 'certificates', kind: 'block', titleKey: 'certificateRecipient', descriptionKey: 'certificateRecipientDescription', keywords: ['certificate', 'recipient', 'award'], supportedModes: ['pdf', 'word'], buildElements: () => [text('presented', 'THIS CERTIFICATE IS PRESENTED TO', 0, 0, 450, 22, 10), text('recipient', '{{recipient.name}}', 0, 34, 450, 52, 30), text('reason', 'For outstanding achievement in {{achievement}}', 0, 98, 450, 34, 13)] },
  { id: 'certificate-signatures', category: 'certificates', kind: 'block', titleKey: 'signatureRow', descriptionKey: 'signatureRowDescription', keywords: ['certificate', 'signature', 'approval'], supportedModes: ['pdf', 'word'], buildElements: () => [{ id: 'signature-1', type: 'signature', x: 0, y: 0, width: 190, height: 70, signatureLabel: 'Director' }, { id: 'signature-2', type: 'signature', x: 260, y: 0, width: 190, height: 70, signatureLabel: 'Instructor' }] },
  { id: 'certificate-page', category: 'certificates', kind: 'pageKit', titleKey: 'certificatePage', descriptionKey: 'certificatePageDescription', keywords: ['certificate', 'complete', 'page'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'cert-course', sourceSize: { width: 842, height: 595 } },

  { id: 'presentation-title', category: 'presentations', kind: 'pageKit', titleKey: 'titleSlide', descriptionKey: 'titleSlideDescription', keywords: ['presentation', 'title', 'slide'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'presentation-title', sourceSize: { width: 1280, height: 720 } },
  { id: 'presentation-content', category: 'presentations', kind: 'block', titleKey: 'contentSlideBlock', descriptionKey: 'contentSlideBlockDescription', keywords: ['presentation', 'content', 'slide'], supportedModes: ['pdf', 'word'], buildElements: () => [text('slide-title', 'Slide title', 0, 0, 500, 48, 28), { id: 'slide-body', type: 'richtext', x: 0, y: 70, width: 500, height: 220, htmlContent: '<ul><li>First key point</li><li>Second key point</li><li>Third key point</li></ul>', style: { fontSize: 18 } }] },
  { id: 'presentation-chart', category: 'presentations', kind: 'block', titleKey: 'chartSlideBlock', descriptionKey: 'chartSlideBlockDescription', keywords: ['presentation', 'chart', 'slide'], supportedModes: ['pdf', 'word'], buildElements: () => [text('chart-title', 'Performance overview', 0, 0, 500, 42, 26), chart('chart', 0, 58, 500, 260)] },
  { id: 'presentation-deck', category: 'presentations', kind: 'pageKit', titleKey: 'slideDeck', descriptionKey: 'slideDeckDescription', keywords: ['presentation', 'deck', 'slides'], supportedModes: ['pdf', 'word'], sourceTemplateIds: ['presentation-title', 'presentation-agenda', 'presentation-content', 'presentation-closing'], sourceSize: { width: 1280, height: 720 } },

  { id: 'report-header', category: 'reports', kind: 'block', titleKey: 'reportHeader', descriptionKey: 'reportHeaderDescription', keywords: ['report', 'header', 'period'], supportedModes: ['pdf', 'word'], buildElements: () => [text('report-title', 'Monthly Performance Report', 0, 0, 430, 42, 25), text('report-period', '{{report.period}}', 0, 48, 260, 20, 11)] },
  { id: 'report-kpi', category: 'reports', kind: 'block', titleKey: 'kpiSection', descriptionKey: 'kpiSectionDescription', keywords: ['report', 'kpi', 'metrics'], supportedModes: ['pdf', 'word'], buildElements: () => [text('kpi-1', 'Revenue\n{{kpi.revenue}}', 0, 0, 150, 72, 14), text('kpi-2', 'Orders\n{{kpi.orders}}', 170, 0, 150, 72, 14), text('kpi-3', 'Conversion\n{{kpi.conversion}}', 340, 0, 150, 72, 14)] },
  { id: 'report-chart', category: 'reports', kind: 'block', titleKey: 'reportChart', descriptionKey: 'reportChartDescription', keywords: ['report', 'chart', 'trend'], supportedModes: ['pdf', 'word'], buildElements: () => [chart('report-chart', 0, 0, 500, 240)] },
  { id: 'report-page', category: 'reports', kind: 'pageKit', titleKey: 'reportPage', descriptionKey: 'reportPageDescription', keywords: ['report', 'complete', 'dashboard'], supportedModes: ['pdf', 'word'], sourceTemplateId: 'report-kpi', sourceSize: { width: 595, height: 842 } },
  { id: 'report-kit', category: 'reports', kind: 'pageKit', titleKey: 'reportKit', descriptionKey: 'reportKitDescription', keywords: ['report', 'multipage', 'management'], supportedModes: ['pdf', 'word'], sourceTemplateIds: ['report-kpi', 'report-financial', 'report-project'], sourceSize: { width: 595, height: 842 } },
] as const;

function fallbackTemplateElements(entry: ContentLibraryEntry): SimpleElement[] {
  const template = TEMPLATES.find(value => value.id === entry.sourceTemplateId);
  const sourceWidth = entry.sourceSize?.width ?? 595;
  return [
    text('template-title', template?.name ?? 'Document page', sourceWidth * 0.1, 90, sourceWidth * 0.8, 64, Math.max(24, sourceWidth * 0.035)),
    text('template-description', template?.description ?? '', sourceWidth * 0.1, 170, sourceWidth * 0.8, 80, Math.max(12, sourceWidth * 0.016)),
  ];
}

export function buildContentPages(entry: ContentLibraryEntry, locale: string): Page[] {
  const templateIds = entry.sourceTemplateIds ?? (entry.sourceTemplateId ? [entry.sourceTemplateId] : []);
  return templateIds.flatMap((templateId, index) => {
    const sourceEntry = { ...entry, sourceTemplateId: templateId };
    const pages = getTemplatePagesLocalized(templateId, locale);
    if (pages?.length) return pages;
    const elements = getTemplateElementsLocalized(templateId, locale);
    return [{ id: `page-${entry.id}-${index + 1}`, elements: elements.length ? elements : fallbackTemplateElements(sourceEntry) }];
  });
}
