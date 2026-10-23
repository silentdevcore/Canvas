export type ElementGroupId =
  | 'textData'
  | 'formsInput'
  | 'tablesCharts'
  | 'mediaCodes'
  | 'shapesLayout'
  | 'documentNavigation'
  | 'wordElements';

export interface ElementRegistryEntry {
  type: string;
  group: ElementGroupId;
  supportedModes: readonly ('pdf' | 'word')[];
  keywords: readonly string[];
}

export const ELEMENT_GROUP_ORDER: readonly ElementGroupId[] = [
  'textData',
  'formsInput',
  'tablesCharts',
  'mediaCodes',
  'shapesLayout',
  'documentNavigation',
  'wordElements',
];

export const ELEMENT_REGISTRY = [
  { type: 'text', group: 'textData', supportedModes: ['pdf', 'word'], keywords: ['copy', 'paragraph', 'label'] },
  { type: 'richtext', group: 'textData', supportedModes: ['pdf', 'word'], keywords: ['html', 'formatted', 'paragraph'] },
  { type: 'date', group: 'textData', supportedModes: ['pdf', 'word'], keywords: ['time', 'calendar', 'locale'] },
  { type: 'number', group: 'textData', supportedModes: ['pdf', 'word'], keywords: ['currency', 'decimal', 'percent'] },
  { type: 'field', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['input', 'text field', 'form'] },
  { type: 'textarea', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['multiline', 'comments', 'form'] },
  { type: 'checkbox', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['check', 'boolean', 'form'] },
  { type: 'radio', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['choice', 'option', 'form'] },
  { type: 'optionlist', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['list', 'choice', 'form'] },
  { type: 'dropdown', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['select', 'choice', 'form'] },
  { type: 'button', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['submit', 'action', 'form'] },
  { type: 'signature', group: 'formsInput', supportedModes: ['pdf', 'word'], keywords: ['sign', 'approval', 'form'] },
  { type: 'table', group: 'tablesCharts', supportedModes: ['pdf', 'word'], keywords: ['rows', 'columns', 'data'] },
  { type: 'chart', group: 'tablesCharts', supportedModes: ['pdf', 'word'], keywords: ['graph', 'series', 'visualization'] },
  { type: 'image', group: 'mediaCodes', supportedModes: ['pdf', 'word'], keywords: ['photo', 'logo', 'asset'] },
  { type: 'qrcode', group: 'mediaCodes', supportedModes: ['pdf', 'word'], keywords: ['qr', 'code', 'scan'] },
  { type: 'barcode', group: 'mediaCodes', supportedModes: ['pdf', 'word'], keywords: ['code', 'scan', 'product'] },
  { type: 'shape', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['box', 'container', 'layout'] },
  { type: 'rect', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['rectangle', 'box', 'shape'] },
  { type: 'circle', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['ellipse', 'round', 'shape'] },
  { type: 'line', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['separator', 'rule', 'draw'] },
  { type: 'arrow', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['direction', 'connector', 'draw'] },
  { type: 'draw', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['freehand', 'pen', 'ink'] },
  { type: 'area', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['region', 'container', 'layout'] },
  { type: 'subsection', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['section', 'group', 'layout'] },
  { type: 'highlight', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['marker', 'emphasis', 'annotation'] },
  { type: 'checkmark', group: 'shapesLayout', supportedModes: ['pdf', 'word'], keywords: ['tick', 'approval', 'symbol'] },
  { type: 'link', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['url', 'hyperlink', 'navigation'] },
  { type: 'note', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['annotation', 'comment', 'sticky'] },
  { type: 'watermark', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['background', 'confidential', 'stamp'] },
  { type: 'pagenumber', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['page', 'footer', 'numbering'] },
  { type: 'pageboundary', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['break', 'boundary', 'pagination'] },
  { type: 'toc', group: 'documentNavigation', supportedModes: ['pdf', 'word'], keywords: ['contents', 'headings', 'navigation'] },
  { type: 'footnote', group: 'wordElements', supportedModes: ['word'], keywords: ['reference', 'note', 'citation'] },
  { type: 'endnote', group: 'wordElements', supportedModes: ['word'], keywords: ['reference', 'note', 'citation'] },
  { type: 'bookmark', group: 'wordElements', supportedModes: ['word'], keywords: ['anchor', 'navigation', 'reference'] },
  { type: 'comment', group: 'wordElements', supportedModes: ['word'], keywords: ['review', 'annotation', 'note'] },
  { type: 'contentcontrol', group: 'wordElements', supportedModes: ['word'], keywords: ['sdt', 'control', 'structured'] },
] as const;

export const ELEMENT_TYPES = ELEMENT_REGISTRY.map(entry => entry.type);
export type RegisteredElementType = (typeof ELEMENT_REGISTRY)[number]['type'];

export function registryEntry(type: RegisteredElementType): (typeof ELEMENT_REGISTRY)[number] | undefined {
  return ELEMENT_REGISTRY.find(entry => entry.type === type);
}
