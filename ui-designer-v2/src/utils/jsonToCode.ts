import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';
import type { SimpleElement } from '@/types';

// ── Helpers ────────────────────────────────────────────────────────────────

function hexToColor(hex: string | undefined): string {
  if (!hex || hex === 'transparent') return 'PdfColor.White';
  const h = hex.replace('#', '');
  const full = h.length === 3
    ? h[0] + h[0] + h[1] + h[1] + h[2] + h[2]
    : h;
  if (full.length !== 6) return 'PdfColor.Black';
  const r = (parseInt(full.slice(0, 2), 16) / 255).toFixed(3);
  const g = (parseInt(full.slice(2, 4), 16) / 255).toFixed(3);
  const b = (parseInt(full.slice(4, 6), 16) / 255).toFixed(3);
  return `new PdfColor(${r}, ${g}, ${b})`;
}

function f(v: number): string { return v.toFixed(2); }

function getNum(style: Record<string, any> | undefined, key: string, fallback: number): number {
  const v = style?.[key];
  if (typeof v === 'number') return v;
  if (typeof v === 'string') {
    const p = parseFloat(v.replace(/px|%|pt/gi, '').trim());
    if (!isNaN(p)) return p;
  }
  return fallback;
}

function getStr(style: Record<string, any> | undefined, key: string): string | undefined {
  const v = style?.[key];
  return v != null ? String(v) : undefined;
}

function rectBottomY(pageH: number, cssTop: number, height: number) { return pageH - cssTop - height; }
function textY(pageH: number, cssTop: number, fs: number) { return pageH - cssTop - fs * 0.72; }

function fontFamilyStr(style: Record<string, any> | undefined): string {
  const fam = getStr(style, 'fontFamily')?.toLowerCase() ?? '';
  if (fam.includes('times') || fam.includes('georgia') || fam.includes('serif')) return 'PdfFontFamily.Times';
  if (fam.includes('courier') || fam.includes('mono') || fam.includes('consolas') || fam.includes('code')) return 'PdfFontFamily.Courier';
  return 'PdfFontFamily.Helvetica';
}

function textAlignStr(style: Record<string, any> | undefined): string {
  switch (getStr(style, 'textAlign')) {
    case 'center':  return 'PdfTextAlignment.Center';
    case 'right':   return 'PdfTextAlignment.Right';
    case 'justify': return 'PdfTextAlignment.Justify';
    default:        return 'PdfTextAlignment.Left';
  }
}

function strokeSuffix(style: Record<string, any> | undefined, lineW: number): string {
  const bs = getStr(style, 'borderStyle') ?? getStr(style, 'dashStyle');
  if (bs === 'dashed') return `, strokeStyle: new PdfStrokeStyle { LineWidth = ${f(lineW)}, DashArray = [8, 4] }`;
  if (bs === 'dotted') return `, strokeStyle: new PdfStrokeStyle { LineWidth = ${f(lineW)}, DashArray = [${f(lineW)}, ${f(lineW * 2)}] }`;
  return '';
}

function esc(s: string): string {
  return s.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\n/g, '\\n').replace(/\r/g, '').replace(/\t/g, '\\t');
}

function normalizeTag(tag: string | undefined | null): string {
  return (tag ?? '').trim().split('-')[0].toLowerCase();
}

function resolveTargetLanguage(design: ParsedDesign): string | undefined {
  const ps = design.pageSettings as any;
  return ps?.targetLanguage ?? ps?.systemLanguage;
}

function applyLangOverride(el: SimpleElement, targetLanguage: string | undefined): SimpleElement {
  if (!targetLanguage || !el.langOverrides) return el;
  const override = el.langOverrides[targetLanguage] ?? el.langOverrides[normalizeTag(targetLanguage)];
  if (!override) return el;
  return {
    ...el,
    x: override.x ?? el.x,
    y: override.y ?? el.y,
    width: override.width ?? el.width,
    height: override.height ?? el.height,
    style: override.rotation != null
      ? { ...(el.style ?? {}), rotation: override.rotation }
      : el.style,
  };
}

function shouldRenderForLanguage(el: SimpleElement, targetLanguage: string | undefined): boolean {
  if (!targetLanguage || !el.elementLanguage) return true;
  return normalizeTag(el.elementLanguage) === normalizeTag(targetLanguage);
}

function textLanguageOptions(el: SimpleElement): string[] {
  return [
    ...(el.language ? [`Language = "${esc(el.language)}"`] : []),
    ...(el.textDirection ? [`TextDirection = "${esc(el.textDirection)}"`] : []),
  ];
}

function textExpr(text: string): string {
  return text.includes('{{') ? `Resolve("${esc(text)}")` : `"${esc(text)}"`;
}

function renderMetadataBlock(design: ParsedDesign, pageW: number, pageH: number, targetLanguage: string | undefined): string[] {
  const ps = design.pageSettings as any;
  const systemLanguage = ps?.systemLanguage ?? '';
  const activeLanguages = Array.isArray(ps?.activeLanguages) ? ps.activeLanguages as string[] : [];
  const localizedProperties = Array.isArray(ps?.localizedProperties) ? ps.localizedProperties as any[] : [];
  const margins = ps?.margins ?? {};
  const lines: string[] = [
    `var designId = "${esc(design.id ?? '')}";`,
    `var designCategory = "${esc((design as any).category ?? '')}";`,
    `var designDescription = "${esc((design as any).description ?? '')}";`,
    `var systemLanguage = "${esc(systemLanguage)}";`,
    `var targetLanguage = "${esc(targetLanguage ?? systemLanguage)}";`,
    `var activeLanguages = new List<string> { ${activeLanguages.map(lang => `"${esc(lang)}"`).join(', ')} };`,
    `var pageSettings = new`,
    `{`,
    `    Width = ${f(pageW)},`,
    `    Height = ${f(pageH)},`,
    `    Orientation = "${esc(ps?.orientation ?? 'portrait')}",`,
    `    Unit = "${esc(ps?.unit ?? 'px')}",`,
    `    Margins = new { Top = ${f(margins.top ?? 0)}, Right = ${f(margins.right ?? 0)}, Bottom = ${f(margins.bottom ?? 0)}, Left = ${f(margins.left ?? 0)} }`,
    `};`,
    `var localizedProperties = new Dictionary<string, Dictionary<string, string>>`,
    `{`,
  ];

  localizedProperties.forEach(prop => {
    const values = prop?.localizedValues ?? {};
    lines.push(`    ["${esc(prop?.key ?? '')}"] = new Dictionary<string, string>`);
    lines.push(`    {`);
    Object.entries(values).forEach(([lang, value]) => {
      lines.push(`        ["${esc(lang)}"] = "${esc(String(value))}",`);
    });
    lines.push(`    }, // scope: ${esc(prop?.scope ?? 'global')}${prop?.ownerLanguage ? `, ownerLanguage: ${esc(prop.ownerLanguage)}` : ''}`);
  });

  lines.push(`};`);
  lines.push(`string Resolve(string value)`);
  lines.push(`{`);
  lines.push(`    if (string.IsNullOrEmpty(value) || !value.Contains("{{")) return value;`);
  lines.push(`    return System.Text.RegularExpressions.Regex.Replace(value, @"\\{\\{(\\w+)\\}\\}", match =>`);
  lines.push(`    {`);
  lines.push(`        var key = match.Groups[1].Value;`);
  lines.push(`        if (!localizedProperties.TryGetValue(key, out var values)) return match.Value;`);
  lines.push(`        if (!string.IsNullOrWhiteSpace(targetLanguage) && values.TryGetValue(targetLanguage, out var targetValue)) return targetValue;`);
  lines.push(`        if (!string.IsNullOrWhiteSpace(systemLanguage) && values.TryGetValue(systemLanguage, out var systemValue)) return systemValue;`);
  lines.push(`        return match.Value;`);
  lines.push(`    });`);
  lines.push(`}`);
  return lines;
}


interface TextRun {
  text: string;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  strike: boolean;
  color?: string;
}

function parseHtmlRuns(html: string): TextRun[][] {
  const decodeEntities = (s: string) =>
    s.replace(/&nbsp;/gi, ' ').replace(/&amp;/gi, '&').replace(/&lt;/gi, '<').replace(/&gt;/gi, '>').replace(/&quot;/gi, '"');

  // Split into block-level lines first
  const blocks = html
    .replace(/<\/p>|<\/div>|<\/h[1-6]>|<br\s*\/?>/gi, '\n')
    .replace(/<li[^>]*>/gi, '\n• ')
    .split('\n')
    .map(b => b.trim())
    .filter(Boolean);

  return blocks.map(block => {
    const runs: TextRun[] = [];
    let bold = false, italic = false, underline = false, strike = false;
    let color: string | undefined;
    let buf = '';

    const flush = () => {
      if (buf) { runs.push({ text: decodeEntities(buf), bold, italic, underline, strike, color }); buf = ''; }
    };

    const tokenRe = /<[^>]+>|[^<]+/g;
    let m: RegExpExecArray | null;
    while ((m = tokenRe.exec(block)) !== null) {
      const tok = m[0];
      if (!tok.startsWith('<')) { buf += tok; continue; }
      flush();
      const tl = tok.toLowerCase().replace(/\s+/g, ' ');
      if (/^<(strong|b)>/.test(tl))        bold = true;
      else if (/^<\/(strong|b)>/.test(tl)) bold = false;
      else if (/^<(em|i)>/.test(tl))       italic = true;
      else if (/^<\/(em|i)>/.test(tl))     italic = false;
      else if (/^<u>/.test(tl))             underline = true;
      else if (/^<\/u>/.test(tl))           underline = false;
      else if (/^<(s|del|strike)>/.test(tl))        strike = true;
      else if (/^<\/(s|del|strike)>/.test(tl))      strike = false;
      else if (/^<span/.test(tl)) {
        const cm = tok.match(/color\s*:\s*(#[0-9a-fA-F]{3,8})/i);
        if (cm) color = cm[1];
      }
      else if (/^<\/span>/.test(tl)) color = undefined;
    }
    flush();
    return runs.filter(r => r.text.trim().length > 0);
  });
}

function paraOpts(s: Record<string, any> | undefined, fs: number, el?: SimpleElement): string {
  const bold   = ['bold', '700', '600'].includes(getStr(s, 'fontWeight') ?? '');
  const italic = getStr(s, 'fontStyle') === 'italic';
  const deco   = getStr(s, 'textDecoration') ?? '';
  const lineH  = getNum(s, 'lineHeight', 0);
  const lineHPx = lineH > 0 ? (lineH < 8 ? lineH * fs : lineH) : null;
  const opts: string[] = [
    `FontSize = ${fs}`,
    `FontFamily = ${fontFamilyStr(s)}`,
    `FillColor = ${hexToColor(getStr(s, 'color') ?? '#000000')}`,
    `Alignment = ${textAlignStr(s)}`,
    ...(bold   ? ['Bold = true']        : []),
    ...(italic ? ['Italic = true']      : []),
    ...(lineHPx ? [`LineHeight = ${lineHPx.toFixed(2)}`] : []),
    ...(deco.includes('underline')    ? ['Underline = true']    : []),
    ...(deco.includes('line-through') ? ['Strikethrough = true'] : []),
    ...(el ? textLanguageOptions(el) : []),
  ];
  return opts.join(', ');
}

// ── Element renderers ──────────────────────────────────────────────────────

function renderText(p: string, el: SimpleElement, pageH: number): string {
  const s    = el.style;
  const text = el.content ?? '';
  if (!text.trim()) return `// text "${el.id}": empty`;
  const fs   = getNum(s, 'fontSize', 12);
  const padL = getNum(s, 'paddingLeft',  getNum(s, 'padding', 0));
  const padT = getNum(s, 'paddingTop',   getNum(s, 'padding', 0));
  const padR = getNum(s, 'paddingRight', getNum(s, 'padding', 0));
  const x    = el.x + padL;
  const y    = textY(pageH, el.y + padT, fs);
  const w    = Math.max(el.width - padL - padR, 1);
  return `${p}.DrawParagraph(${textExpr(text)}, x: ${f(x)}, y: ${f(y)}, maxWidth: ${f(w)}, new PdfParagraphOptions { ${paraOpts(s, fs, el)} });`;
}

function renderRichText(p: string, el: SimpleElement, pageH: number): string {
  const s         = el.style;
  const html      = el.htmlContent ?? el.content ?? '';
  if (!html.trim()) return `// richtext "${el.id}": empty`;
  const fs        = getNum(s, 'fontSize', 12);
  const baseColor = getStr(s, 'color') ?? '#000000';
  const padL      = getNum(s, 'paddingLeft',  getNum(s, 'padding', 0));
  const padT      = getNum(s, 'paddingTop',   getNum(s, 'padding', 0));
  const lineH     = getNum(s, 'lineHeight', 0);
  const lineHPt   = lineH > 0 ? (lineH < 8 ? lineH * fs : lineH) : fs * 1.4;
  const startX    = el.x + padL;

  const blocks = parseHtmlRuns(html);
  const output: string[] = [];

  blocks.forEach((runs, bi) => {
    if (!runs.length) return;
    const y = textY(pageH, el.y + padT + bi * lineHPt, fs);
    let x = startX;
    runs.forEach(run => {
      const color = hexToColor(run.color ?? baseColor);
      const opts: string[] = [`FontSize = ${fs}`, `FillColor = ${color}`, ...textLanguageOptions(el)];
      if (run.bold)      opts.push('Bold = true');
      if (run.italic)    opts.push('Italic = true');
      if (run.underline) opts.push('Underline = true');
      if (run.strike)    opts.push('Strikethrough = true');
      output.push(`${p}.DrawText(${textExpr(run.text)}, x: ${f(x)}, y: ${f(y)}, new PdfDrawTextOptions { ${opts.join(', ')} });`);
      x += run.text.length * (run.bold ? fs * 0.62 : fs * 0.55);
    });
  });

  return output.length ? output.join('\n') : `// richtext "${el.id}": no text content`;
}

function renderRect(p: string, el: SimpleElement, pageH: number): string {
  const s         = el.style;
  const fillStr   = getStr(s, 'backgroundColor') ?? getStr(s, 'fill') ?? '';
  const borderStr = getStr(s, 'borderColor') ?? '';
  const bw        = getNum(s, 'borderWidth', 0);
  const hasFill   = !!fillStr && fillStr !== 'transparent';
  const hasBorder = !!borderStr && bw > 0;
  const r         = getNum(s, 'borderRadius', 0);
  const strokeC   = hasBorder ? hexToColor(borderStr) : (hasFill ? hexToColor(fillStr) : 'ParseColor("#d1d5db")');
  const fillC     = hasFill ? hexToColor(fillStr) : 'PdfColor.White';
  const lineW     = hasBorder ? Math.max(bw, 0.5) : 0.01;
  const boxY      = rectBottomY(pageH, el.y, el.height);
  const ss        = strokeSuffix(s, lineW);
  if (r > 0) {
    const cr = Math.min(r, Math.min(el.width, el.height) / 2 - 0.01);
    return `${p}.DrawRoundedRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, cornerRadius: ${f(cr)}, lineWidth: ${f(lineW)}, fill: ${hasFill}, strokeColor: ${strokeC}, fillColor: ${fillC}${ss});`;
  }
  return `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, lineWidth: ${f(lineW)}, fill: ${hasFill}, strokeColor: ${strokeC}, fillColor: ${fillC}${ss});`;
}

function renderCircle(p: string, el: SimpleElement, pageH: number): string {
  const s         = el.style;
  const fillStr   = getStr(s, 'backgroundColor') ?? '#f3f4f6';
  const borderStr = getStr(s, 'borderColor') ?? '#d1d5db';
  const bw        = Math.max(getNum(s, 'borderWidth', 1), 0.5);
  const hasFill   = !!fillStr && fillStr !== 'transparent';
  const cx        = el.x + el.width / 2;
  const cy        = pageH - el.y - el.height / 2;
  return `${p}.DrawCircle(centerX: ${f(cx)}, centerY: ${f(cy)}, radius: ${f(el.width / 2)}, lineWidth: ${f(bw)}, fill: ${hasFill}, strokeColor: ${hexToColor(borderStr)}, fillColor: ${hasFill ? hexToColor(fillStr) : 'PdfColor.White'});`;
}

function renderLine(p: string, el: SimpleElement, pageH: number): string {
  const s     = el.style;
  const color = hexToColor(getStr(s, 'color') ?? getStr(s, 'borderColor') ?? '#374151');
  const sw    = Math.max(getNum(s, 'strokeWidth', 1), 0.5);
  const ss    = strokeSuffix(s, sw);
  if (el.width >= el.height) {
    const midY = pageH - el.y - el.height / 2;
    return `${p}.DrawLine(x1: ${f(el.x)}, y1: ${f(midY)}, x2: ${f(el.x + el.width)}, y2: ${f(midY)}, lineWidth: ${f(sw)}, strokeColor: ${color}${ss});`;
  }
  const midX = el.x + el.width / 2;
  return `${p}.DrawLine(x1: ${f(midX)}, y1: ${f(pageH - el.y)}, x2: ${f(midX)}, y2: ${f(pageH - el.y - el.height)}, lineWidth: ${f(sw)}, strokeColor: ${color}${ss});`;
}

function renderArrow(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const color   = hexToColor(getStr(s, 'color') ?? '#374151');
  const sw      = Math.max(getNum(s, 'strokeWidth', 1.5), 0.5);
  const lineY   = pageH - el.y - el.height / 2;
  const tipW    = Math.min(10, el.width * 0.2);
  const endM    = el.endMarker ?? 'arrow';
  const startM  = el.startMarker ?? 'none';
  const x0      = el.x + (startM !== 'none' ? tipW : 0);
  const x1      = el.x + el.width - (endM !== 'none' ? tipW : 0);
  const lines: string[] = [
    `${p}.DrawLine(x1: ${f(x0)}, y1: ${f(lineY)}, x2: ${f(x1)}, y2: ${f(lineY)}, lineWidth: ${f(sw)}, strokeColor: ${color});`,
  ];
  if (endM === 'arrow') {
    lines.push(`${p}.DrawPolygon([new PdfPoint(${f(el.x + el.width)}, ${f(lineY)}), new PdfPoint(${f(el.x + el.width - tipW)}, ${f(lineY + tipW / 2)}), new PdfPoint(${f(el.x + el.width - tipW)}, ${f(lineY - tipW / 2)})], lineWidth: 0.5, fill: true, strokeColor: ${color}, fillColor: ${color});`);
  }
  if (startM === 'arrow') {
    lines.push(`${p}.DrawPolygon([new PdfPoint(${f(el.x)}, ${f(lineY)}), new PdfPoint(${f(el.x + tipW)}, ${f(lineY + tipW / 2)}), new PdfPoint(${f(el.x + tipW)}, ${f(lineY - tipW / 2)})], lineWidth: 0.5, fill: true, strokeColor: ${color}, fillColor: ${color});`);
  }
  return lines.join('\n');
}

function renderTable(p: string, el: SimpleElement, pageH: number): string {
  const s           = el.style;
  const rows        = (el as any).cellData as string[][] | undefined;
  if (!rows || rows.length === 0) return `// table "${el.id}": no cell data — add rows manually`;
  const fs          = getNum(s, 'fontSize', 11);
  const textColor   = hexToColor(getStr(s, 'color') ?? '#101828');
  const borderColor = hexToColor(getStr(s, 'borderColor') ?? '#e5e7eb');
  const rowStrs     = rows.map(r => `        IReadOnlyList<string> { ${r.map(c => `"${esc(c)}"`).join(', ')} }`).join(',\n');
  return (
    `${p}.DrawSimpleTable(x: ${f(el.x)}, y: ${f(pageH - el.y)}, width: ${f(el.width)},\n` +
    `    [\n${rowStrs}\n    ],\n` +
    `    new PdfTableOptions { FontSize = ${fs}, TextColor = ${textColor}, BorderColor = ${borderColor} });`
  );
}

function renderButton(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const text    = el.content ?? '';
  const bgStr   = getStr(s, 'backgroundColor') ?? '#1d6fff';
  const textClr = hexToColor(getStr(s, 'color') ?? '#ffffff');
  const r       = Math.min(getNum(s, 'borderRadius', 6), Math.min(el.width, el.height) / 2 - 0.01);
  const bgColor = hexToColor(bgStr);
  const boxY    = rectBottomY(pageH, el.y, el.height);
  const fs      = getNum(s, 'fontSize', 12);
  const lines: string[] = [];
  if (r > 0) {
    lines.push(`${p}.DrawRoundedRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, cornerRadius: ${f(r)}, lineWidth: 0.5, fill: true, strokeColor: ${bgColor}, fillColor: ${bgColor});`);
  } else {
    lines.push(`${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, lineWidth: 0.5, fill: true, strokeColor: ${bgColor}, fillColor: ${bgColor});`);
  }
  if (text.trim()) {
    const ty = textY(pageH, el.y + (el.height - fs * 1.4) / 2, fs);
    lines.push(`${p}.DrawText("${esc(text)}", x: ${f(el.x + 8)}, y: ${f(ty)}, new PdfDrawTextOptions { FontSize = ${fs}, Bold = true, FillColor = ${textClr} });`);
  }
  return lines.join('\n');
}

function renderField(p: string, el: SimpleElement, pageH: number): string {
  const s           = el.style;
  const label       = el.fieldLabel ?? el.fieldName ?? '';
  const borderColor = hexToColor(getStr(s, 'borderColor') ?? '#d1d5db');
  const labelColor  = hexToColor(getStr(s, 'color') ?? '#374151');
  const fs          = getNum(s, 'fontSize', 11);
  const boxH        = Math.max(el.height - 20, 2);
  const boxY        = rectBottomY(pageH, el.y + 20, boxH);
  const lines: string[] = [
    `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(boxH)}, lineWidth: 1, fill: false, strokeColor: ${borderColor}, fillColor: PdfColor.White);`,
  ];
  if (label) {
    lines.push(`${p}.DrawText("${esc(label)}", x: ${f(el.x)}, y: ${f(textY(pageH, el.y, fs))}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = ${labelColor} });`);
  }
  if (el.fieldName) {
    lines.push(`${p}.DrawText("${esc(el.fieldName)}", x: ${f(el.x + 6)}, y: ${f(textY(pageH, el.y + 20 + (boxH - fs * 1.2) / 2, fs))}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = new PdfColor(0.612, 0.639, 0.659), Italic = true });`);
  }
  return lines.join('\n');
}

function renderCheckbox(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const boxSize = 14;
  const boxCssTop = el.y + (el.height - boxSize) / 2;
  const boxY      = rectBottomY(pageH, boxCssTop, boxSize);
  const bColor    = hexToColor(getStr(s, 'borderColor') ?? '#374151');
  const fs        = getNum(s, 'fontSize', 12);
  const state     = el.checkState ?? 'empty';
  const midBoxY   = boxY + boxSize / 2;
  const lines: string[] = [
    `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(boxSize)}, height: ${f(boxSize)}, lineWidth: 1.5, fill: false, strokeColor: ${bColor}, fillColor: PdfColor.White);`,
  ];
  if (state === 'checked') {
    lines.push(`${p}.DrawLine(x1: ${f(el.x + 2.5)}, y1: ${f(midBoxY - 0.5)}, x2: ${f(el.x + 5.5)}, y2: ${f(midBoxY + 3)}, lineWidth: 1.5, strokeColor: ${bColor});`);
    lines.push(`${p}.DrawLine(x1: ${f(el.x + 5.5)}, y1: ${f(midBoxY + 3)}, x2: ${f(el.x + 11.5)}, y2: ${f(midBoxY - 4)}, lineWidth: 1.5, strokeColor: ${bColor});`);
  } else if (state === 'cross') {
    lines.push(`${p}.DrawLine(x1: ${f(el.x + 3)}, y1: ${f(boxY + 3)}, x2: ${f(el.x + boxSize - 3)}, y2: ${f(boxY + boxSize - 3)}, lineWidth: 1.5, strokeColor: ${bColor});`);
    lines.push(`${p}.DrawLine(x1: ${f(el.x + boxSize - 3)}, y1: ${f(boxY + 3)}, x2: ${f(el.x + 3)}, y2: ${f(boxY + boxSize - 3)}, lineWidth: 1.5, strokeColor: ${bColor});`);
  }
  if (el.fieldLabel) {
    const labelY = textY(pageH, el.y + (el.height - fs) / 2, fs);
    lines.push(`${p}.DrawText("${esc(el.fieldLabel)}", x: ${f(el.x + boxSize + 8)}, y: ${f(labelY)}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = ${hexToColor(getStr(s, 'color') ?? '#101828')} });`);
  }
  return lines.join('\n');
}

function renderDropdown(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const label   = el.fieldLabel ?? el.content ?? '';
  const bColor  = hexToColor(getStr(s, 'borderColor') ?? '#d1d5db');
  const tColor  = hexToColor(getStr(s, 'color') ?? '#374151');
  const fs      = getNum(s, 'fontSize', 12);
  const boxY    = rectBottomY(pageH, el.y, el.height);
  const ty      = textY(pageH, el.y + (el.height - fs * 1.4) / 2, fs);
  const lines   = [
    `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, lineWidth: 1, fill: true, strokeColor: ${bColor}, fillColor: PdfColor.White);`,
    `${p}.DrawText("v", x: ${f(el.x + el.width - 14)}, y: ${f(ty)}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = new PdfColor(0.612, 0.639, 0.659) });`,
  ];
  if (label) lines.splice(1, 0, `${p}.DrawText("${esc(label)}", x: ${f(el.x + 6)}, y: ${f(ty)}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = ${tColor} });`);
  return lines.join('\n');
}

function renderOptionList(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const opts    = el.options ?? [];
  const fs      = getNum(s, 'fontSize', 11);
  const tColor  = hexToColor(getStr(s, 'color') ?? '#101828');
  const lineH   = fs * 1.7;
  const isRadio = el.type === 'radio';

  const items = opts.length > 0 ? opts : ['Option 1', 'Option 2', 'Option 3'];
  const lines: string[] = [];
  if (opts.length === 0) lines.push(`// ${el.type} "${el.id}": no options defined — using placeholders`);

  items.forEach((opt, i) => {
    const optY   = el.y + i * lineH;
    const bullet = isRadio ? (opt === el.selectedValue ? '* ' : 'o ') : '• ';
    lines.push(`${p}.DrawText("${bullet}${esc(opt)}", x: ${f(el.x)}, y: ${f(textY(pageH, optY, fs))}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = ${tColor} });`);
  });
  return lines.join('\n');
}

function renderSignature(p: string, el: SimpleElement, pageH: number): string {
  const s      = el.style;
  const label  = el.signatureLabel ?? 'Signature';
  const lColor = hexToColor(getStr(s, 'borderColor') ?? getStr(s, 'color') ?? '#9ca3af');
  const pdfLineY = pageH - (el.y + el.height - 14);
  return [
    `${p}.DrawLine(x1: ${f(el.x)}, y1: ${f(pdfLineY)}, x2: ${f(el.x + el.width)}, y2: ${f(pdfLineY)}, lineWidth: 1, strokeColor: ${lColor});`,
    `${p}.DrawText("${esc(label)}", x: ${f(el.x)}, y: ${f(pdfLineY - 6)}, new PdfDrawTextOptions { FontSize = 10, FillColor = ${lColor}, Italic = true });`,
  ].join('\n');
}

function renderWatermark(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const text    = el.content ?? '';
  if (!text.trim()) return `// watermark "${el.id}": empty`;
  const fs      = getNum(s, 'fontSize', 48);
  const rot     = getNum(s, 'rotation', 45);
  const color   = hexToColor(getStr(s, 'color') ?? '#d1d5db');
  const cx      = el.x + el.width / 2;
  const cy      = pageH - el.y - el.height / 2;
  return `${p}.DrawText("${esc(text)}", x: ${f(cx)}, y: ${f(cy)}, new PdfDrawTextOptions { FontSize = ${fs}, FillColor = ${color}, RotationDegrees = ${rot} });`;
}

function renderNote(p: string, el: SimpleElement, pageH: number): string {
  const s       = el.style;
  const title   = el.noteTitle ?? 'Note';
  const body    = el.noteBody ?? '';
  const bgColor = hexToColor(getStr(s, 'backgroundColor') ?? '#fef9c3');
  const bColor  = hexToColor(getStr(s, 'borderColor') ?? '#fbbf24');
  const boxY    = rectBottomY(pageH, el.y, el.height);
  const lines   = [
    `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, lineWidth: 1, fill: true, strokeColor: ${bColor}, fillColor: ${bgColor});`,
    `${p}.DrawText("${esc(title)}", x: ${f(el.x + 8)}, y: ${f(textY(pageH, el.y + 4, 11))}, new PdfDrawTextOptions { FontSize = 11, Bold = true, FillColor = new PdfColor(0.471, 0.204, 0.059) });`,
  ];
  if (body) lines.push(`${p}.DrawParagraph("${esc(body)}", x: ${f(el.x + 8)}, y: ${f(textY(pageH, el.y + 20, 10))}, maxWidth: ${f(el.width - 16)}, new PdfParagraphOptions { FontSize = 10, FillColor = new PdfColor(0.569, 0.251, 0.059) });`);
  return lines.join('\n');
}

function renderDate(p: string, el: SimpleElement, pageH: number): string {
  const s    = el.style;
  const fs   = getNum(s, 'fontSize', 12);
  const mode = el.dateMode ?? 'static';
  const fmt  = el.dateFormat ?? 'dd.MM.yyyy';
  const y    = textY(pageH, el.y, fs);
  if (mode === 'render') {
    return `${p}.DrawText(DateTime.Now.ToString("${esc(fmt)}"), x: ${f(el.x)}, y: ${f(y)}, new PdfDrawTextOptions { ${paraOpts(s, fs)} });`;
  }
  const text = el.content ?? el.fallbackText ?? '';
  return `${p}.DrawText("${esc(text)}", x: ${f(el.x)}, y: ${f(y)}, new PdfDrawTextOptions { ${paraOpts(s, fs)} });`;
}

function renderAreaDashed(p: string, el: SimpleElement, pageH: number): string {
  const boxY = rectBottomY(pageH, el.y, el.height);
  return `${p}.DrawRectangle(x: ${f(el.x)}, y: ${f(boxY)}, width: ${f(el.width)}, height: ${f(el.height)}, lineWidth: 0.5, fill: false, strokeColor: new PdfGrayColor(0.82), strokeStyle: new PdfStrokeStyle { LineWidth = 0.5, DashArray = [4, 4] });`;
}

const CHART_PALETTE = [
  'new PdfColor(0.11, 0.42, 1.0)',
  'new PdfColor(1.0, 0.42, 0.11)',
  'new PdfColor(0.11, 0.80, 0.42)',
  'new PdfColor(0.80, 0.11, 0.60)',
  'new PdfColor(1.0,  0.85, 0.0)',
  'new PdfColor(0.0,  0.75, 0.75)',
];

function renderChart(p: string, el: SimpleElement, pageH: number): string {
  const data     = (el.chartData ?? {}) as { labels?: string[]; datasets?: { label?: string; data: number[] }[] };
  const labels   = data.labels   ?? ['A', 'B', 'C'];
  const datasets = data.datasets ?? [{ data: [10, 20, 15] }];
  const type     = el.chartType  ?? 'bar';
  const n        = Math.max(labels.length, datasets[0]?.data?.length ?? 0, 1);
  const values   = datasets[0]?.data ?? [];
  const maxVal   = Math.max(...values.filter(v => typeof v === 'number'), 1);

  const mL = 35, mR = 10, mT = 15, mB = 25;
  const plotW    = Math.max(el.width  - mL - mR, 10);
  const plotH    = Math.max(el.height - mT - mB, 10);
  const originX  = el.x + mL;
  const botPdfY  = pageH - el.y - el.height + mB;
  const topPdfY  = botPdfY + plotH;
  const gray     = 'new PdfGrayColor(0.6)';
  const darkGray = 'new PdfGrayColor(0.3)';

  const out: string[] = [`// chart (${type}): ${labels.slice(0, 5).join(', ')}${labels.length > 5 ? '…' : ''}`];

  if (type === 'bar') {
    out.push(`${p}.DrawLine(x1: ${f(originX)}, y1: ${f(botPdfY)}, x2: ${f(originX + plotW)}, y2: ${f(botPdfY)}, lineWidth: 0.75, strokeColor: ${gray});`);
    out.push(`${p}.DrawLine(x1: ${f(originX)}, y1: ${f(botPdfY)}, x2: ${f(originX)}, y2: ${f(topPdfY)}, lineWidth: 0.75, strokeColor: ${gray});`);
    const slotW = plotW / n;
    datasets.forEach((ds, di) => {
      const color = CHART_PALETTE[di % CHART_PALETTE.length];
      const bw    = Math.max((slotW * 0.7) / Math.max(datasets.length, 1) - 2, 2);
      (ds.data ?? []).forEach((val, i) => {
        if (typeof val !== 'number') return;
        const bh = (val / maxVal) * plotH;
        const bx = originX + i * slotW + slotW * 0.15 + di * (bw + 2);
        out.push(`${p}.DrawRectangle(x: ${f(bx)}, y: ${f(botPdfY)}, width: ${f(bw)}, height: ${f(bh)}, lineWidth: 0.5, fill: true, strokeColor: ${color}, fillColor: ${color}); // ${labels[i] ?? i}: ${val}`);
      });
    });
    labels.slice(0, n).forEach((lbl, i) => {
      const lx = originX + i * slotW + slotW / 2 - String(lbl).length * 2.5;
      out.push(`${p}.DrawText("${esc(String(lbl))}", x: ${f(lx)}, y: ${f(botPdfY - 12)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
    });
    out.push(`${p}.DrawText("${maxVal}", x: ${f(el.x + 2)}, y: ${f(topPdfY + 3)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
    out.push(`${p}.DrawText("0", x: ${f(el.x + 2)}, y: ${f(botPdfY + 3)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
  }

  else if (type === 'line') {
    out.push(`${p}.DrawLine(x1: ${f(originX)}, y1: ${f(botPdfY)}, x2: ${f(originX + plotW)}, y2: ${f(botPdfY)}, lineWidth: 0.75, strokeColor: ${gray});`);
    out.push(`${p}.DrawLine(x1: ${f(originX)}, y1: ${f(botPdfY)}, x2: ${f(originX)}, y2: ${f(topPdfY)}, lineWidth: 0.75, strokeColor: ${gray});`);
    const stepX = n > 1 ? plotW / (n - 1) : plotW / 2;
    datasets.forEach((ds, di) => {
      const color = CHART_PALETTE[di % CHART_PALETTE.length];
      const pts   = (ds.data ?? []).map((val, i) => ({
        px: originX + i * stepX,
        py: botPdfY + (typeof val === 'number' ? (val / maxVal) * plotH : 0),
      }));
      for (let i = 0; i < pts.length - 1; i++)
        out.push(`${p}.DrawLine(x1: ${f(pts[i].px)}, y1: ${f(pts[i].py)}, x2: ${f(pts[i+1].px)}, y2: ${f(pts[i+1].py)}, lineWidth: 1.5, strokeColor: ${color});`);
      pts.forEach((pt, i) =>
        out.push(`${p}.DrawCircle(centerX: ${f(pt.px)}, centerY: ${f(pt.py)}, radius: 3, lineWidth: 1, fill: true, strokeColor: ${color}, fillColor: ${color}); // ${labels[i] ?? i}: ${ds.data[i]}`));
    });
    labels.slice(0, n).forEach((lbl, i) => {
      const lx = originX + i * stepX - String(lbl).length * 2.5;
      out.push(`${p}.DrawText("${esc(String(lbl))}", x: ${f(lx)}, y: ${f(botPdfY - 12)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
    });
    out.push(`${p}.DrawText("${maxVal}", x: ${f(el.x + 2)}, y: ${f(topPdfY + 3)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
    out.push(`${p}.DrawText("0", x: ${f(el.x + 2)}, y: ${f(botPdfY + 3)}, new PdfDrawTextOptions { FontSize = 8, FillColor = ${darkGray} });`);
  }

  else if (type === 'pie') {
    const cx     = el.x + el.width / 2;
    const cy     = pageH - el.y - el.height / 2;
    const radius = Math.min(el.width, el.height) / 2 - mT;
    const total  = values.reduce((a, b) => a + (typeof b === 'number' ? b : 0), 0) || 1;
    let angle    = -Math.PI / 2;
    values.forEach((val, i) => {
      if (typeof val !== 'number' || val <= 0) return;
      const sweep  = (val / total) * 2 * Math.PI;
      const steps  = Math.max(Math.ceil(sweep * 10), 4);
      const color  = CHART_PALETTE[i % CHART_PALETTE.length];
      const ptList = [`new PdfPoint(${f(cx)}, ${f(cy)})`];
      for (let s = 0; s <= steps; s++) {
        const a = angle + (s / steps) * sweep;
        ptList.push(`new PdfPoint(${f(cx + radius * Math.cos(a))}, ${f(cy + radius * Math.sin(a))})`);
      }
      out.push(`${p}.DrawPolygon([${ptList.join(', ')}], lineWidth: 0.5, fill: true, strokeColor: PdfColor.White, fillColor: ${color}); // ${labels[i] ?? i}: ${val}`);
      const mid = angle + sweep / 2;
      const pct = Math.round((val / total) * 100);
      if (pct >= 5)
        out.push(`${p}.DrawText("${pct}%", x: ${f(cx + radius * 0.6 * Math.cos(mid) - 6)}, y: ${f(cy + radius * 0.6 * Math.sin(mid))}, new PdfDrawTextOptions { FontSize = 8, Bold = true, FillColor = PdfColor.White });`);
      angle += sweep;
    });
  }

  return out.join('\n');
}

function renderElement(p: string, el: SimpleElement, pageH: number): string {
  if (el.hidden) return '';
  switch (el.type) {
    case 'text':                return renderText(p, el, pageH);
    case 'richtext':            return renderRichText(p, el, pageH);
    case 'rect':
    case 'shape':               return renderRect(p, el, pageH);
    case 'circle':              return renderCircle(p, el, pageH);
    case 'line':                return renderLine(p, el, pageH);
    case 'arrow':               return renderArrow(p, el, pageH);
    case 'table':               return renderTable(p, el, pageH);
    case 'button':              return renderButton(p, el, pageH);
    case 'field':               return renderField(p, el, pageH);
    case 'checkbox':            return renderCheckbox(p, el, pageH);
    case 'dropdown':            return renderDropdown(p, el, pageH);
    case 'optionlist':
    case 'radio':               return renderOptionList(p, el, pageH);
    case 'signature':           return renderSignature(p, el, pageH);
    case 'watermark':           return renderWatermark(p, el, pageH);
    case 'note':                return renderNote(p, el, pageH);
    case 'date':                return renderDate(p, el, pageH);
    case 'subsection':
    case 'area':                return renderAreaDashed(p, el, pageH);
    case 'image':
      return `// image "${el.id}": ${p}.DrawImage("path/to/file.jpg", x: ${f(el.x)}, y: ${f(rectBottomY(pageH, el.y, el.height))}, width: ${f(el.width)}, height: ${f(el.height)});`;
    case 'qrcode': {
      const qrVal   = esc(el.qrValue ?? 'https://example.com');
      const qrBoxY  = rectBottomY(pageH, el.y, el.height);
      return [
        `{ // qrcode: "${qrVal}"`,
        `    var _qrBytes = ScriptHelpers.GenerateQrPng("${qrVal}");`,
        `    var _qrTmp = Path.ChangeExtension(Path.GetTempFileName(), ".png");`,
        `    File.WriteAllBytes(_qrTmp, _qrBytes);`,
        `    try { ${p}.DrawImage(_qrTmp, x: ${f(el.x)}, y: ${f(qrBoxY)}, width: ${f(el.width)}, height: ${f(el.height)}); }`,
        `    finally { try { File.Delete(_qrTmp); } catch {} }`,
        `}`,
      ].join('\n');
    }
    case 'barcode': {
      const bcVal    = esc(el.barcodeValue ?? '');
      const bcFmt    = el.barcodeType ?? 'code128';
      const bcBoxY   = rectBottomY(pageH, el.y, el.height * 0.7);
      return [
        `{ // barcode: "${bcVal}" (${bcFmt})`,
        `    var _bcBytes = ScriptHelpers.GenerateBarcodePng("${bcVal}", "${bcFmt}", (int)${f(el.width)}, (int)${f(el.height * 0.7)});`,
        `    var _bcTmp = Path.ChangeExtension(Path.GetTempFileName(), ".png");`,
        `    File.WriteAllBytes(_bcTmp, _bcBytes);`,
        `    try { ${p}.DrawImage(_bcTmp, x: ${f(el.x)}, y: ${f(bcBoxY)}, width: ${f(el.width)}, height: ${f(el.height * 0.7)}); }`,
        `    finally { try { File.Delete(_bcTmp); } catch {} }`,
        `}`,
      ].join('\n');
    }
    case 'chart':               return renderChart(p, el, pageH);
    case 'pagenumber':
      return `// pagenumber "${el.id}": use document page numbering settings or replace with a literal page number`;
    default:
      return `// "${el.type}" (id: "${el.id}"): no code generator for this element type`;
  }
}

// ── Public entry point ─────────────────────────────────────────────────────

export function jsonToCode(design: ParsedDesign): string {
  const pageH = design.pageSettings?.height ?? 842;
  const pageW = design.pageSettings?.width  ?? 595;
  const targetLanguage = resolveTargetLanguage(design);

  const lines: string[] = [
    '// Canvas.Pdf Code Editor — generated from JSON design',
    '// Rendering script for the selected language view; use C# DTO for lossless round-trip editing.',
    '',
    ...renderMetadataBlock(design, pageW, pageH, targetLanguage),
    '',
    'var document = new PdfDocument();',
    ...(design.name ? [`document.Info.Title = "${esc(design.name)}";`] : []),
    '',
  ];

  const multiPage = design.pages.length > 1;

  design.pages.forEach((page, pi) => {
    const v = multiPage ? `page${pi + 1}` : 'page';
    lines.push(`var ${v} = document.AddPage(${pageW}, ${pageH});`);
    lines.push('');
    const renderable = (el: SimpleElement) =>
      shouldRenderForLanguage(el, targetLanguage)
        ? renderElement(v, applyLangOverride(el, targetLanguage), pageH)
        : '';
    const shared = (design.sharedElements ?? []).map(renderable).filter(Boolean);
    const own    = page.elements.map(renderable).filter(Boolean);
    [...shared, ...own].forEach(code => { lines.push(code); lines.push(''); });
  });

  lines.push('document');
  return lines.join('\n');
}
