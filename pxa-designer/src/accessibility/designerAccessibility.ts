import type { Page, PageSettings, SimpleElement, Template } from '@/types';

export const PDF_STRUCTURE_ROLES = [
  'Part', 'Sect', 'Div', 'P', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6',
  'L', 'LI', 'Lbl', 'LBody', 'Table', 'TR', 'TH', 'TD', 'Span', 'Link',
  'Figure', 'Form',
] as const;

export type PdfStructureRole = (typeof PDF_STRUCTURE_ROLES)[number];

const ARTIFACT_TYPES = new Set<SimpleElement['type']>([
  'shape', 'rect', 'circle', 'line', 'arrow', 'draw', 'highlight', 'watermark',
  'pageboundary', 'checkmark',
]);

const FIGURE_TYPES = new Set<SimpleElement['type']>(['image', 'chart', 'qrcode', 'barcode']);
const FORM_TYPES = new Set<SimpleElement['type']>([
  'field', 'textarea', 'checkbox', 'radio', 'dropdown', 'optionlist', 'signature',
]);
const SUPPORTED_WIDGET_TYPES = new Set<SimpleElement['type']>(['field', 'textarea', 'checkbox']);

export type AccessibilityIssueSeverity = 'error' | 'warning';

export interface DesignerAccessibilityIssue {
  code: string;
  severity: AccessibilityIssueSeverity;
  pageIndex?: number;
  elementId?: string;
  elementName?: string;
}

export function suggestedAccessibility(element: SimpleElement, readingOrder?: number): NonNullable<SimpleElement['accessibility']> {
  if (ARTIFACT_TYPES.has(element.type) || element.type === 'pagenumber')
    return { role: 'Span', isArtifact: true };

  let role: PdfStructureRole = 'Span';
  if (element.type === 'text' || element.type === 'richtext')
    role = element.headingLevel ? (`H${element.headingLevel}` as PdfStructureRole) : 'P';
  else if (FIGURE_TYPES.has(element.type))
    role = 'Figure';
  else if (element.type === 'button' && isButtonLinkAction(element.buttonAction))
    role = 'Link';
  else if (element.type === 'button' && element.buttonAction)
    role = 'Form';
  else if (element.type === 'button')
    role = 'Span';
  else if (FORM_TYPES.has(element.type))
    role = 'Form';
  else if (element.type === 'table')
    role = 'Table';
  else if (element.type === 'optionlist' || element.type === 'toc')
    role = 'L';
  else if (element.type === 'link' || element.type === 'bookmark')
    role = 'Link';
  else if (element.type === 'subsection')
    role = 'Sect';
  else if (element.type === 'area' || element.type === 'contentcontrol')
    role = 'Div';
  else if (element.type === 'note' || element.type === 'comment' || element.type === 'footnote' || element.type === 'endnote')
    role = 'P';

  return {
    role,
    readingOrder,
    language: element.language,
  };
}

export function withSuggestedAccessibility(element: SimpleElement, readingOrder?: number): SimpleElement {
  if (element.accessibility) return element;
  return { ...element, accessibility: suggestedAccessibility(element, readingOrder) };
}

export function accessibilityForButtonAction(
  element: SimpleElement,
  buttonAction: string,
): SimpleElement['accessibility'] {
  const current = element.accessibility;
  if (!current || !['Span', 'Link', 'Form'].includes(current.role)) return current;
  const role: PdfStructureRole = isButtonLinkAction(buttonAction)
    ? 'Link'
    : buttonAction
      ? 'Form'
      : 'Span';
  return { ...current, role };
}

export function applySuggestedAccessibility(
  pages: Page[],
  sharedElements: SimpleElement[],
): { pages: Page[]; sharedElements: SimpleElement[] } {
  let order = 0;
  const map = (element: SimpleElement) => withSuggestedAccessibility(element, order++);
  return {
    pages: pages.map(page => ({ ...page, elements: page.elements.map(map) })),
    sharedElements: sharedElements.map(map),
  };
}

export function validateDesignerAccessibility(
  template: Pick<Template, 'name'>,
  pages: Page[],
  sharedElements: SimpleElement[],
  pageSettings?: PageSettings,
): DesignerAccessibilityIssue[] {
  if (pageSettings?.accessibilityProfile !== 'PdfUa1') return [];

  const issues: DesignerAccessibilityIssue[] = [];
  const title = pageSettings.metadata?.title?.trim() || template.name.trim();
  const language = (pageSettings.targetLanguage || pageSettings.systemLanguage || '').trim();
  if (!title) issues.push({ code: 'PXAPDFUA002', severity: 'error' });
  if (!language) issues.push({ code: 'PXAPDFUA003', severity: 'error' });
  else if (!/^(?:[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*|[ix](?:-[A-Za-z0-9]{1,8})+)$/.test(language))
    issues.push({ code: 'PXAPDFUA015', severity: 'error' });
  if (pageSettings.encryption?.enabled) issues.push({ code: 'PXAPDFUA004', severity: 'error' });

  const allElements = [
    ...pages.flatMap((page, pageIndex) => page.elements.map(element => ({ element, pageIndex }))),
    ...sharedElements.map(element => ({ element, pageIndex: undefined })),
  ].filter(item => !item.element.hidden);
  const ids = new Set(allElements
    .filter(item => item.element.accessibility && !item.element.accessibility.isArtifact)
    .map(item => item.element.id));

  for (const { element, pageIndex } of allElements) {
    const context = { pageIndex, elementId: element.id, elementName: element.name || element.type };
    const accessibility = element.accessibility;
    if (!accessibility) {
      issues.push({ code: 'PXAPDFUA005', severity: 'error', ...context });
      continue;
    }
    if (accessibility.isArtifact) continue;
    if (!accessibility.role)
      issues.push({ code: 'PXAPDFUA021', severity: 'error', ...context });
    if (accessibility.readingOrder != null && accessibility.readingOrder < 0)
      issues.push({ code: 'PXAPDFUA016', severity: 'error', ...context });
    if (accessibility.parentElementId && !ids.has(accessibility.parentElementId))
      issues.push({ code: 'PXAPDFUA013', severity: 'error', ...context });
    if (hasParentCycle(element, allElements.map(item => item.element)))
      issues.push({ code: 'PXAPDFUA014', severity: 'error', ...context });
    if (accessibility.role === 'Figure' && !accessibility.alternateText?.trim())
      issues.push({ code: 'PXAPDFUA007', severity: 'error', ...context });
    if (accessibility.role === 'Link' && !meaningfulText(element, accessibility))
      issues.push({ code: 'PXAPDFUA017', severity: 'error', ...context });
    if (accessibility.role === 'Link' && !hasLinkTarget(element))
      issues.push({ code: 'PXAPDFUA010', severity: 'error', ...context });
    if (accessibility.role === 'Form' && !formLabel(element, accessibility))
      issues.push({ code: 'PXAPDFUA018', severity: 'error', ...context });
    if (accessibility.role === 'Form' && !hasSupportedWidget(element))
      issues.push({ code: 'PXAPDFUA010', severity: 'error', ...context });
    if (accessibility.role === 'TH' && (!accessibility.tableHeaderScope || accessibility.tableHeaderScope === 'None'))
      issues.push({ code: 'PXAPDFUA009', severity: 'error', ...context });
    if (accessibility.tableHeaderIds?.some(headerId => !ids.has(headerId)))
      issues.push({ code: 'PXAPDFUA011', severity: 'error', ...context });
    if (element.type === 'table') {
      if (!element.headerRow) issues.push({ code: 'PXAPDFUA019', severity: 'error', ...context });
      if (!element.cellData?.length || !element.cellData[0]?.length)
        issues.push({ code: 'PXAPDFUA020', severity: 'error', ...context });
    }
  }

  return issues;
}

export function accessibilityIssuesForElement(
  elementId: string,
  issues: DesignerAccessibilityIssue[],
): DesignerAccessibilityIssue[] {
  return issues.filter(issue => issue.elementId === elementId);
}

function meaningfulText(element: SimpleElement, accessibility: NonNullable<SimpleElement['accessibility']>): string {
  return (accessibility.actualText || accessibility.alternateText || element.content || element.fieldLabel || '').trim();
}

function formLabel(element: SimpleElement, accessibility: NonNullable<SimpleElement['accessibility']>): string {
  return (accessibility.alternateText || accessibility.actualText || element.fieldLabel || element.content || '').trim();
}

function hasLinkTarget(element: SimpleElement): boolean {
  if (element.type === 'link') return Boolean(element.href?.trim());
  if (element.type === 'button') return isButtonLinkAction(element.buttonAction);
  return element.type === 'bookmark' || element.type === 'toc';
}

function isButtonLinkAction(action?: string): boolean {
  const value = action?.trim() ?? '';
  return /^https?:\/\//i.test(value) || /^page:\d+$/i.test(value);
}

function hasSupportedWidget(element: SimpleElement): boolean {
  if (SUPPORTED_WIDGET_TYPES.has(element.type)) return true;
  return element.type === 'dropdown' && Boolean(element.options?.length);
}

function hasParentCycle(element: SimpleElement, elements: SimpleElement[]): boolean {
  const byId = new Map(elements.map(item => [item.id, item]));
  const visited = new Set<string>([element.id]);
  let parentId = element.accessibility?.parentElementId;
  while (parentId) {
    if (visited.has(parentId)) return true;
    visited.add(parentId);
    parentId = byId.get(parentId)?.accessibility?.parentElementId;
  }
  return false;
}
