import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';

function indent(n: number) { return '    '.repeat(n); }
function str(v: string | undefined | null) { return v != null ? `"${v.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"` : 'null'; }
function num(v: number | undefined | null, fallback?: number) {
  return v != null ? String(v) : fallback != null ? String(fallback) : 'null';
}
function bool(v: boolean | undefined | null) { return v == null ? 'null' : v ? 'true' : 'false'; }

function csharpObjectValue(v: unknown, depth: number): string {
  if (v == null) return 'null';
  if (typeof v === 'string') return str(v);
  if (typeof v === 'number') return String(v);
  if (typeof v === 'boolean') return bool(v);
  if (Array.isArray(v)) {
    const values = v.map(item => csharpObjectValue(item, depth + 1)).join(', ');
    return `new object[] { ${values} }`;
  }
  if (typeof v === 'object') return renderObjectDictionary(v as Record<string, unknown>, depth);
  return str(String(v));
}

function renderObjectDictionary(obj: Record<string, unknown>, depth: number): string {
  const keys = Object.keys(obj).filter(k => obj[k] != null);
  if (keys.length === 0) return 'new Dictionary<string, object>()';
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const entries = keys.map(k => {
    const val = csharpObjectValue(obj[k], depth + 1);
    return `${i1}["${k}"] = ${val}`;
  });
  return `new Dictionary<string, object>\n${i}{\n${entries.join(',\n')}\n${i}}`;
}

function renderStringArray(values: unknown): string | null {
  if (!Array.isArray(values)) return null;
  return `new[] { ${values.map(v => str(String(v))).join(', ')} }`;
}

function renderNumberArray(values: unknown): string | null {
  if (!Array.isArray(values)) return null;
  return `new[] { ${values.map(v => String(Number(v))).join(', ')} }`;
}

function renderCellSide(side: any): string | null {
  if (!side) return null;
  const parts: string[] = [];
  if (side.color != null) parts.push(`Color = ${str(side.color)}`);
  if (side.width != null) parts.push(`Width = ${String(Number(side.width))}`);
  return parts.length ? `new() { ${parts.join(', ')} }` : null;
}

function renderCellStyles(cellStyles: any[], depth: number): string {
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const items = cellStyles.map((cs) => {
    const p: string[] = [`Row = ${Number(cs.row) || 0}`, `Col = ${Number(cs.col) || 0}`];
    if (cs.backgroundColor != null) p.push(`BackgroundColor = ${str(cs.backgroundColor)}`);
    if (cs.textAlign != null)       p.push(`TextAlign = ${str(cs.textAlign)}`);
    if (cs.borderColor != null)     p.push(`BorderColor = ${str(cs.borderColor)}`);
    if (cs.borderWidth != null)     p.push(`BorderWidth = ${String(Number(cs.borderWidth))}`);
    for (const [k, dto] of [['borderTop', 'BorderTop'], ['borderRight', 'BorderRight'], ['borderBottom', 'BorderBottom'], ['borderLeft', 'BorderLeft']] as const) {
      const side = renderCellSide(cs[k]);
      if (side) p.push(`${dto} = ${side}`);
    }
    if (cs.padding != null)    p.push(`Padding = ${String(Number(cs.padding))}`);
    if (cs.fontFamily != null) p.push(`FontFamily = ${str(cs.fontFamily)}`);
    if (cs.fontSize != null)   p.push(`FontSize = ${String(Number(cs.fontSize))}`);
    if (cs.bold === true)      p.push(`Bold = true`);
    if (cs.italic === true)    p.push(`Italic = true`);
    if (cs.color != null)      p.push(`Color = ${str(cs.color)}`);
    return `${i1}new() { ${p.join(', ')} }`;
  });
  return [`new CellStyleDto[]`, `${i}{`, items.join(',\n'), `${i}}`].join('\n');
}

function renderMargins(margins: any, depth: number): string {
  const i = indent(depth);
  const i1 = indent(depth + 1);
  return [
    `new MarginsDto`,
    `${i}{`,
    `${i1}Top = ${num(margins?.top, 0)},`,
    `${i1}Right = ${num(margins?.right, 0)},`,
    `${i1}Bottom = ${num(margins?.bottom, 0)},`,
    `${i1}Left = ${num(margins?.left, 0)}`,
    `${i}}`,
  ].join('\n');
}

function renderLangOverrides(langOverrides: Record<string, any>, depth: number): string {
  const i = indent(depth);
  const i1 = indent(depth + 1);
  const i2 = indent(depth + 2);
  const entries = Object.entries(langOverrides).map(([lang, ov]) => {
    const lines = [
      `${i1}[${str(lang)}] = new LangOverrideDto`,
      `${i1}{`,
    ];
    if (ov.x != null) lines.push(`${i2}X = ${num(ov.x)},`);
    if (ov.y != null) lines.push(`${i2}Y = ${num(ov.y)},`);
    if (ov.width != null) lines.push(`${i2}Width = ${num(ov.width)},`);
    if (ov.height != null) lines.push(`${i2}Height = ${num(ov.height)},`);
    if (ov.rotation != null) lines.push(`${i2}Rotation = ${num(ov.rotation)},`);
    lines.push(`${i1}}`);
    return lines.join('\n');
  });
  return [
    `new Dictionary<string, LangOverrideDto>`,
    `${i}{`,
    entries.join(',\n'),
    `${i}}`,
  ].join('\n');
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

  if (el.name != null)         lines.push(`${i1}Name = ${str(el.name)},`);
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
  if (el.visibleExpression != null) lines.push(`${i1}VisibleExpression = ${str(el.visibleExpression)},`);
  if (el.pageScope != null)    lines.push(`${i1}PageScope = ${str(el.pageScope)},`);
  if (el.pageRange != null)    lines.push(`${i1}PageRange = ${str(el.pageRange)},`);

  if (el.style && Object.keys(el.style).length > 0) {
    lines.push(`${i1}Style = ${renderObjectDictionary(el.style, depth + 1)},`);
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

  if (Array.isArray(el.cellStyles) && el.cellStyles.length > 0) {
    lines.push(`${i1}CellStyles = ${renderCellStyles(el.cellStyles, depth + 1)},`);
  }

  const stringArrayFields: Array<[string, string]> = [
    ['options', 'Options'],
    ['columnAlignments', 'ColumnAlignments'],
  ];
  for (const [jsonKey, dtoKey] of stringArrayFields) {
    const rendered = renderStringArray(el[jsonKey]);
    if (rendered) lines.push(`${i1}${dtoKey} = ${rendered},`);
  }

  const numberArrayFields: Array<[string, string]> = [
    ['columnWidths', 'ColumnWidths'],
  ];
  for (const [jsonKey, dtoKey] of numberArrayFields) {
    const rendered = renderNumberArray(el[jsonKey]);
    if (rendered) lines.push(`${i1}${dtoKey} = ${rendered},`);
  }

  const scalarFields: Array<[string, string, 'string' | 'number' | 'bool' | 'object']> = [
    ['qrSize', 'QrSize', 'number'],
    ['headerRow', 'HeaderRow', 'bool'],
    ['footerRow', 'FooterRow', 'bool'],
    ['headerBgColor', 'HeaderBgColor', 'string'],
    ['zebraEnabled', 'ZebraEnabled', 'bool'],
    ['zebraColor', 'ZebraColor', 'string'],
    ['noteTitle', 'NoteTitle', 'string'],
    ['noteBody', 'NoteBody', 'string'],
    ['noteAuthor', 'NoteAuthor', 'string'],
    ['noteCollapsed', 'NoteCollapsed', 'bool'],
    ['fitMode', 'FitMode', 'string'],
    ['cropX', 'CropX', 'number'],
    ['cropY', 'CropY', 'number'],
    ['cropWidth', 'CropWidth', 'number'],
    ['cropHeight', 'CropHeight', 'number'],
    ['focalX', 'FocalX', 'number'],
    ['focalY', 'FocalY', 'number'],
    ['watermarkMode', 'WatermarkMode', 'string'],
    ['arrowMode', 'ArrowMode', 'string'],
    ['arrowDirection', 'ArrowDirection', 'string'],
    ['arrowRotation', 'ArrowRotation', 'number'],
    ['startMarker', 'StartMarker', 'string'],
    ['endMarker', 'EndMarker', 'string'],
    ['drawTool', 'DrawTool', 'string'],
    ['pathData', 'PathData', 'string'],
    ['language', 'Language', 'string'],
    ['textDirection', 'TextDirection', 'string'],
    ['elementLanguage', 'ElementLanguage', 'string'],
    ['elementGroup', 'ElementGroup', 'string'],
    ['dateMode', 'DateMode', 'string'],
    ['dateFormat', 'DateFormat', 'string'],
    ['locale', 'Locale', 'string'],
    ['timezone', 'Timezone', 'string'],
    ['fallbackText', 'FallbackText', 'string'],
    ['markMode', 'MarkMode', 'string'],
    ['checkState', 'CheckState', 'string'],
    ['pageBoundaryMode', 'PageBoundaryMode', 'string'],
    ['numberingFormat', 'NumberingFormat', 'string'],
    ['startNumber', 'StartNumber', 'number'],
    ['prefix', 'Prefix', 'string'],
    ['suffix', 'Suffix', 'string'],
    ['selectedValue', 'SelectedValue', 'string'],
    ['multiSelect', 'MultiSelect', 'bool'],
    ['ordered', 'Ordered', 'bool'],
    ['listStyle', 'ListStyle', 'string'],
    ['chartType', 'ChartType', 'string'],
    ['chartData', 'ChartData', 'object'],
    ['href', 'Href', 'string'],
    ['linkTarget', 'LinkTarget', 'string'],
    ['buttonAction', 'ButtonAction', 'string'],
    ['numberValue', 'NumberValue', 'number'],
    ['numberStyle', 'NumberStyle', 'string'],
    ['numberDecimals', 'NumberDecimals', 'number'],
    ['numberCurrency', 'NumberCurrency', 'string'],
    ['numberLocale', 'NumberLocale', 'string'],
    ['styleName', 'StyleName', 'string'],
    ['characterStyle', 'CharacterStyle', 'string'],
    ['footnoteText', 'FootnoteText', 'string'],
    ['footnoteRef', 'FootnoteRef', 'string'],
    ['bookmarkName', 'BookmarkName', 'string'],
    ['bookmarkTarget', 'BookmarkTarget', 'string'],
    ['commentAuthor', 'CommentAuthor', 'string'],
    ['commentDate', 'CommentDate', 'string'],
    ['commentText', 'CommentText', 'string'],
    ['commentId', 'CommentId', 'string'],
    ['contentControlType', 'ContentControlType', 'string'],
    ['contentControlTag', 'ContentControlTag', 'string'],
    ['contentControlTitle', 'ContentControlTitle', 'string'],
    ['contentControlPlaceholder', 'ContentControlPlaceholder', 'string'],
    ['revisionType', 'RevisionType', 'string'],
    ['revisionAuthor', 'RevisionAuthor', 'string'],
    ['revisionDate', 'RevisionDate', 'string'],
    ['revisionId', 'RevisionId', 'string'],
    ['autoHyphenation', 'AutoHyphenation', 'bool'],
  ];

  for (const [jsonKey, dtoKey, kind] of scalarFields) {
    const value = el[jsonKey];
    if (value == null) continue;
    if (kind === 'string') lines.push(`${i1}${dtoKey} = ${str(String(value))},`);
    else if (kind === 'number') lines.push(`${i1}${dtoKey} = ${num(Number(value))},`);
    else if (kind === 'bool') lines.push(`${i1}${dtoKey} = ${bool(Boolean(value))},`);
    else lines.push(`${i1}${dtoKey} = ${renderObjectDictionary(value as Record<string, unknown>, depth + 1)},`);
  }

  if (el.langOverrides && Object.keys(el.langOverrides).length > 0) {
    lines.push(`${i1}LangOverrides = ${renderLangOverrides(el.langOverrides, depth + 1)},`);
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

function renderLocalizedProperties(props: any[]): string[] {
  if (!props || props.length === 0) return [];
  const lines: string[] = [
    `        LocalizedProperties = new List<LocalizedPropertyDto>`,
    `        {`,
  ];
  for (const p of props) {
    lines.push(`            new LocalizedPropertyDto`);
    lines.push(`            {`);
    lines.push(`                Key = ${str(p.key)},`);
    lines.push(`                Scope = ${str(p.scope ?? 'global')},`);
    if (p.ownerLanguage) lines.push(`                OwnerLanguage = ${str(p.ownerLanguage)},`);
    if (p.localizedValues && Object.keys(p.localizedValues).length > 0) {
      lines.push(`                LocalizedValues = new Dictionary<string, string>`);
      lines.push(`                {`);
      for (const [lang, val] of Object.entries(p.localizedValues)) {
        lines.push(`                    [${str(lang)}] = ${str(val as string)},`);
      }
      lines.push(`                },`);
    }
    lines.push(`            },`);
  }
  lines.push(`        },`);
  return lines;
}

export function jsonToCSharp(design: ParsedDesign): string {
  const ps = design.pageSettings;
  const localizedProps = (ps as any)?.localizedProperties as any[] | undefined;
  const activeLanguages = (ps as any)?.activeLanguages as string[] | undefined;
  const systemLanguage = (ps as any)?.systemLanguage as string | undefined;
  const targetLanguage = (ps as any)?.targetLanguage as string | undefined;

  const lines: string[] = [
    '// DesignExportDto — paste this in the C# editor and edit freely.',
    '// The expression must return a DesignExportDto instance.',
    'new DesignExportDto',
    '{',
    `    Id = ${str(design.id ?? '')},`,
    `    Name = ${str(design.name ?? 'Untitled')},`,
    `    Category = ${str((design as any).category ?? '')},`,
    `    Description = ${str((design as any).description ?? '')},`,
  ];

  if (ps) {
    lines.push(`    PageSettings = new PageSettingsDto`);
    lines.push(`    {`);
    if (ps.width != null)  lines.push(`        Width = ${num(ps.width)},`);
    if (ps.height != null) lines.push(`        Height = ${num(ps.height)},`);
    if ((ps as any).orientation) lines.push(`        Orientation = ${str((ps as any).orientation)},`);
    if ((ps as any).unit) lines.push(`        Unit = ${str((ps as any).unit)},`);
    if ((ps as any).backgroundColor) lines.push(`        BackgroundColor = ${str((ps as any).backgroundColor)},`);
    if ((ps as any).backgroundImage) lines.push(`        BackgroundImage = ${str((ps as any).backgroundImage)},`);
    if ((ps as any).backgroundImageFit) lines.push(`        BackgroundImageFit = ${str((ps as any).backgroundImageFit)},`);
    if ((ps as any).margins) lines.push(`        Margins = ${renderMargins((ps as any).margins, 2)},`);
    if (systemLanguage) lines.push(`        SystemLanguage = ${str(systemLanguage)},`);
    if (activeLanguages && activeLanguages.length > 0) {
      const langs = activeLanguages.map(l => str(l)).join(', ');
      lines.push(`        ActiveLanguages = new List<string> { ${langs} },`);
    }
    if (targetLanguage) lines.push(`        TargetLanguage = ${str(targetLanguage)},`);
    lines.push(...renderLocalizedProperties(localizedProps ?? []));
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
