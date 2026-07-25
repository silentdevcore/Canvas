const ALLOWED_TAGS = new Set([
  'A', 'B', 'BLOCKQUOTE', 'BR', 'CODE', 'DEL', 'DIV', 'EM', 'H1', 'H2', 'H3',
  'H4', 'H5', 'H6', 'HR', 'I', 'LI', 'OL', 'P', 'PRE', 'S', 'SPAN', 'STRONG',
  'SUB', 'SUP', 'TABLE', 'TBODY', 'TD', 'TH', 'THEAD', 'TR', 'U', 'UL',
]);

const ALLOWED_STYLE_PROPERTIES = new Set([
  'background-color', 'color', 'direction', 'font-family', 'font-size',
  'font-style', 'font-weight', 'letter-spacing', 'line-height', 'text-align',
  'text-decoration', 'white-space',
]);

const normalizeUrl = (value: string): string | null => {
  let normalized = value.trim();
  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const decoded = decodeURIComponent(normalized);
      if (decoded === normalized) break;
      normalized = decoded;
    } catch {
      return null;
    }
  }
  return normalized;
};

const isSafeUrl = (value: string) => {
  const normalized = normalizeUrl(value);
  if (normalized === null) return false;
  if (!normalized) return true;
  if (
    normalized.startsWith('#') ||
    normalized.startsWith('/') ||
    normalized.startsWith('./') ||
    normalized.startsWith('../') ||
    normalized.startsWith('?')
  ) {
    return !normalized.startsWith('//');
  }

  try {
    const url = new URL(normalized);
    return ['http:', 'https:', 'mailto:'].includes(url.protocol.toLowerCase());
  } catch {
    return !normalized.includes(':');
  }
};

const sanitizeStyle = (element: HTMLElement) => {
  Array.from(element.style).forEach(property => {
    if (!ALLOWED_STYLE_PROPERTIES.has(property.toLowerCase())) {
      element.style.removeProperty(property);
    }
  });
  if (!element.getAttribute('style')?.trim()) element.removeAttribute('style');
};

export const sanitizeRichTextHtml = (html: string): string => {
  if (!html || typeof document === 'undefined') return '';

  const template = document.createElement('template');
  template.innerHTML = html;
  const elements = Array.from(template.content.querySelectorAll('*'));

  elements.forEach(element => {
    if (!ALLOWED_TAGS.has(element.tagName)) {
      if (['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED'].includes(element.tagName)) {
        element.remove();
      } else {
        element.replaceWith(document.createTextNode(element.textContent ?? ''));
      }
      return;
    }

    Array.from(element.attributes).forEach(attribute => {
      const name = attribute.name.toLowerCase();
      const allowed =
        name === 'style' ||
        name === 'dir' ||
        name === 'lang' ||
        (['TD', 'TH'].includes(element.tagName) && ['colspan', 'rowspan'].includes(name)) ||
        (element.tagName === 'A' && ['href', 'title', 'target', 'rel'].includes(name));
      if (!allowed || name.startsWith('on')) element.removeAttribute(attribute.name);
    });

    if (element instanceof HTMLElement) sanitizeStyle(element);

    if (element.tagName === 'A') {
      const href = element.getAttribute('href');
      if (href && !isSafeUrl(href)) element.removeAttribute('href');
      if (element.getAttribute('target') === '_blank') {
        element.setAttribute('rel', 'noopener noreferrer');
      } else {
        element.removeAttribute('target');
        element.removeAttribute('rel');
      }
    }
  });

  return template.innerHTML;
};

export default sanitizeRichTextHtml;
