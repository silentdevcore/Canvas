import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';

function indent(n: number) { return '    '.repeat(n); }
function str(v: string | undefined | null) { return v ? `"${v.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"` : 'null'; }
function num(v: number | undefined | null, fallback?: number) {
  return v != null ? String(v) : fallback != null ? String(fallback) : 'null';
}
function bool(v: boolean | undefined | null) { return v == null ? 'null' : v ? 'true' : 'false'; }

function renderStyle(style: Record<string, unknown>, depth: number): string {
  const keys = Object.keys(style).filter(k => style[k] != null);
  if (keys.length === 0) return 'new Dictionary<string, object>()';
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const entries = keys.map(k => {
    const v = style[k];
    const val = typeof v === 'string' ? str(v) : typeof v === 'number' ? String(v) : typeof v === 'boolean' ? bool(v as boolean) : str(String(v));
    return `${i1}["${k}"] = ${val}`;
  });
  return `new Dictionary<string, object>\n${i}{\n${entries.join(',\n')}\n${i}}`;
}

function renderElement(el: any, depth: number): string {
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const lines: string[] = [
    `new ElementDto`,
    `${i}{`,
    `${i1}Id = ${str(el.id)},`,
    `${i1}Type = ${str(el.type)},`,
    `${i1}X = ${num(el.x, 0)},`,
    `${i1}Y = ${num(el.y, 0)},`,
    `${i1}Width = ${num(el.width, 100)},`,
    `${i1}Height = ${num(el.height, 40)},`,
  ];

  if (el.content != null)      lines.push(`${i1}Content = ${str(el.content)},`);
  if (el.htmlContent != null)  lines.push(`${i1}HtmlContent = ${str(el.htmlContent)},`);
  if (el.fieldLabel != null)   lines.push(`${i1}FieldLabel = ${str(el.fieldLabel)},`);
  if (el.fieldName != null)    lines.push(`${i1}FieldName = ${str(el.fieldName)},`);
  if (el.required != null)     lines.push(`${i1}Required = ${bool(el.required)},`);
  if (el.signatureLabel != null) lines.push(`${i1}SignatureLabel = ${str(el.signatureLabel)},`);
  if (el.qrValue != null)      lines.push(`${i1}QrValue = ${str(el.qrValue)},`);
  if (el.barcodeValue != null) lines.push(`${i1}BarcodeValue = ${str(el.barcodeValue)},`);
  if (el.barcodeType != null)  lines.push(`${i1}BarcodeType = ${str(el.barcodeType)},`);
  if (el.hidden != null)       lines.push(`${i1}Hidden = ${bool(el.hidden)},`);
  if (el.locked != null)       lines.push(`${i1}Locked = ${bool(el.locked)},`);
  if (el.pageScope != null)    lines.push(`${i1}PageScope = ${str(el.pageScope)},`);

  if (el.style && Object.keys(el.style).length > 0) {
    lines.push(`${i1}Style = ${renderStyle(el.style, depth + 1)},`);
  }

  if (el.cellData && Array.isArray(el.cellData)) {
    const rows = (el.cellData as string[][]).map((row: string[]) => {
      const cells = row.map((c: string) => str(c)).join(', ');
      return `${indent(depth + 2)}new[] { ${cells} }`;
    });
    lines.push(`${i1}CellData = new string[][]`);
    lines.push(`${i1}{`);
    lines.push(rows.join(',\n'));
    lines.push(`${i1}},`);
  }

  lines.push(`${i}}`);
  return lines.join('\n');
}

function renderPage(page: { id: string; elements: any[] }, depth: number): string {
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const i2 = indent(depth + 2);
  const elements = (page.elements ?? []).map(el => `${i2}${renderElement(el, depth + 2)}`).join(',\n');
  return [
    `new PageDto`,
    `${i}{`,
    `${i1}Id = ${str(page.id)},`,
    `${i1}Elements = new List<ElementDto>`,
    `${i1}{`,
    elements,
    `${i1}}`,
    `${i}}`,
  ].join('\n');
}

export function jsonToCSharp(design: ParsedDesign): string {
  const ps = design.pageSettings;
  const lines: string[] = [
    '// DesignExportDto — paste this in the C# editor and edit freely.',
    '// The expression must return a DesignExportDto instance.',
    'new DesignExportDto',
    '{',
    `    Name = ${str(design.name ?? 'Untitled')},`,
  ];

  if (ps) {
    lines.push(`    PageSettings = new PageSettingsDto`);
    lines.push(`    {`);
    if (ps.width != null)  lines.push(`        Width = ${num(ps.width)},`);
    if (ps.height != null) lines.push(`        Height = ${num(ps.height)},`);
    if ((ps as any).orientation) lines.push(`        Orientation = ${str((ps as any).orientation)},`);
    if ((ps as any).backgroundColor) lines.push(`        BackgroundColor = ${str((ps as any).backgroundColor)},`);
    lines.push(`    },`);
  }

  const pageItems = (design.pages ?? []).map(p => `        ${renderPage(p, 2)}`).join(',\n');
  lines.push(`    Pages = new List<PageDto>`);
  lines.push(`    {`);
  lines.push(pageItems);
  lines.push(`    },`);

  if (design.sharedElements && design.sharedElements.length > 0) {
    const shared = design.sharedElements.map(el => `        ${renderElement(el, 2)}`).join(',\n');
    lines.push(`    SharedElements = new List<ElementDto>`);
    lines.push(`    {`);
    lines.push(shared);
    lines.push(`    },`);
  }

  lines.push('}');
  return lines.join('\n');
}
