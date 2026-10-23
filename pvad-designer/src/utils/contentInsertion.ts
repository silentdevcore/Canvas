import type { Page, PageSettings, SimpleElement } from '@/types';

const FORM_TYPES = new Set(['field', 'textarea', 'checkbox', 'radio', 'optionlist', 'dropdown', 'button', 'signature']);
const REFERENCE_KEYS = new Set(['templateId', 'bookmarkTarget', 'commentId', 'elementGroup', 'pageId', 'targetElementId']);

function uniqueId(prefix: string): string {
  return `${prefix}-${crypto.randomUUID()}`;
}

function clone<T>(value: T): T {
  return structuredClone(value);
}

function rewriteReferences(value: unknown, ids: ReadonlyMap<string, string>, key?: string): unknown {
  if (typeof value === 'string') return key && REFERENCE_KEYS.has(key) ? ids.get(value) ?? value : value;
  if (Array.isArray(value)) return value.map(item => rewriteReferences(item, ids, key));
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([childKey, item]) => [childKey, rewriteReferences(item, ids, childKey)]));
  }
  return value;
}

function fieldNames(pages: readonly Page[]): Set<string> {
  return new Set(pages.flatMap(page => page.elements)
    .filter(element => FORM_TYPES.has(element.type) && element.fieldName)
    .map(element => element.fieldName!));
}

function resolveFieldName(name: string, used: Set<string>): string {
  if (!used.has(name)) {
    used.add(name);
    return name;
  }
  let suffix = 2;
  while (used.has(`${name}_${suffix}`)) suffix += 1;
  const result = `${name}_${suffix}`;
  used.add(result);
  return result;
}

function validateElements(source: readonly SimpleElement[]): void {
  const ids = source.map(element => element.id);
  if (ids.some(id => !id) || new Set(ids).size !== ids.length) {
    throw new Error('The content contains missing or duplicate element IDs.');
  }
  if (source.some(element => element.type === 'image' && !element.assetId && !element.content)) {
    throw new Error('The content contains an image without an available asset.');
  }
}

function remapElements(source: readonly SimpleElement[], existingPages: readonly Page[], additionalIds: ReadonlyMap<string, string> = new Map()): SimpleElement[] {
  validateElements(source);
  const ids = new Map(additionalIds);
  source.forEach(element => ids.set(element.id, uniqueId(element.type)));
  source.map(element => element.elementGroup).filter((value): value is string => Boolean(value)).forEach(value => {
    if (!ids.has(value)) ids.set(value, uniqueId('group'));
  });
  const usedFields = fieldNames(existingPages);
  return source.map(element => {
    const rewritten = rewriteReferences(clone(element), ids) as SimpleElement;
    rewritten.id = ids.get(element.id)!;
    if (FORM_TYPES.has(rewritten.type) && rewritten.fieldName) {
      rewritten.fieldName = resolveFieldName(rewritten.fieldName, usedFields);
    }
    return rewritten;
  });
}

export function prepareBlockInsertion(
  source: readonly SimpleElement[],
  existingPages: readonly Page[],
  pageSettings: PageSettings,
  target?: { x: number; y: number },
): SimpleElement[] {
  if (source.length === 0) throw new Error('The content block is empty.');
  const minX = Math.min(...source.map(element => element.x));
  const minY = Math.min(...source.map(element => element.y));
  const maxX = Math.max(...source.map(element => element.x + element.width));
  const maxY = Math.max(...source.map(element => element.y + element.height));
  const width = maxX - minX;
  const height = maxY - minY;
  const x = target?.x ?? Math.max(0, (pageSettings.width - width) / 2);
  const y = target?.y ?? Math.max(0, (pageSettings.height - height) / 2);
  return remapElements(source, existingPages).map(element => ({
    ...element,
    x: Math.max(0, x + element.x - minX),
    y: Math.max(0, y + element.y - minY),
  }));
}

export interface PreparedPageKit {
  pages: Page[];
  scaled: boolean;
  scale: number;
}

export function preparePageKitInsertion(
  sourcePages: readonly Page[],
  existingPages: readonly Page[],
  sourceSize: { width: number; height: number },
  targetSettings: PageSettings,
): PreparedPageKit {
  if (sourcePages.length === 0 || sourcePages.some(page => page.elements.length === 0)) {
    throw new Error('The page kit contains no insertable content.');
  }
  const scale = Math.min(targetSettings.width / sourceSize.width, targetSettings.height / sourceSize.height);
  const offsetX = (targetSettings.width - sourceSize.width * scale) / 2;
  const offsetY = (targetSettings.height - sourceSize.height * scale) / 2;
  const allSourceElements = sourcePages.flatMap(page => page.elements);
  const pageIds = new Map(sourcePages.map(page => [page.id, uniqueId('page')]));
  const remapped = remapElements(allSourceElements, existingPages, pageIds);
  let cursor = 0;
  const pages = sourcePages.map(page => {
    const elements = remapped.slice(cursor, cursor + page.elements.length).map(element => ({
      ...element,
      x: offsetX + element.x * scale,
      y: offsetY + element.y * scale,
      width: element.width * scale,
      height: element.height * scale,
      style: element.style ? {
        ...element.style,
        ...(typeof element.style.fontSize === 'number' ? { fontSize: element.style.fontSize * scale } : {}),
        ...(typeof element.style.borderWidth === 'number' ? { borderWidth: element.style.borderWidth * scale } : {}),
        ...(typeof element.style.padding === 'number' ? { padding: element.style.padding * scale } : {}),
      } : element.style,
    }));
    cursor += page.elements.length;
    return { id: pageIds.get(page.id)!, elements };
  });
  return { pages, scaled: Math.abs(scale - 1) > 0.001, scale };
}
