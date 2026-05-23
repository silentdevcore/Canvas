import type { SimpleElement, Page, PageSettings } from '@/types';
import type { Template } from '../store';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function esc(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

function stripHtml(html: string): string {
  return html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
}

function colorBrush(hex: string): string {
  return `new XSolidBrush(XColor.FromHtml("${hex}"))`;
}

function colorPen(hex: string, width: number): string {
  return `new XPen(XColor.FromHtml("${hex}"), ${width})`;
}

// ─── PdfSharp element renderers ────────────────────────────────────────────────

function renderElementPdfSharp(el: SimpleElement, gfx: string): string[] {
  const lines: string[] = [];
  const { x, y, width, height } = el;
  const style = el.style ?? {};

  lines.push(`// ${el.type}${el.name ? ` — ${el.name}` : el.content ? ` — "${el.content}"` : ''}`);

  switch (el.type) {
    case 'text': {
      const fontSize = Number(style.fontSize ?? 14);
      const bold = style.fontWeight === 'bold' || style.fontWeight === '700';
      const italic = style.fontStyle === 'italic';
      let xStyle = 'XFontStyle.Regular';
      if (bold && italic) xStyle = 'XFontStyle.BoldItalic';
      else if (bold) xStyle = 'XFontStyle.Bold';
      else if (italic) xStyle = 'XFontStyle.Italic';
      const color = style.color ?? '#101828';
      const text = esc(String(el.content ?? ''));
      lines.push(`var font_${el.id.replace(/-/g, '_')} = new XFont("Arial", ${fontSize}, ${xStyle});`);
      lines.push(`${gfx}.DrawString("${text}", font_${el.id.replace(/-/g, '_')}, ${colorBrush(color)}, new XPoint(${x}, ${y + fontSize}));`);
      break;
    }

    case 'richtext': {
      const text = esc(stripHtml(el.htmlContent ?? ''));
      const fontSize = 13;
      lines.push(`var font_${el.id.replace(/-/g, '_')} = new XFont("Arial", ${fontSize}, XFontStyle.Regular);`);
      lines.push(`${gfx}.DrawString("${text}", font_${el.id.replace(/-/g, '_')}, XBrushes.Black, new XRect(${x}, ${y}, ${width}, ${height}), XStringFormats.TopLeft);`);
      break;
    }

    case 'field': {
      const borderColor = style.borderColor ?? '#d1d5db';
      const fieldLabel = esc(el.fieldLabel ?? el.fieldName ?? '');
      lines.push(`${gfx}.DrawRectangle(${colorPen(borderColor, 1)}, XBrushes.White, new XRect(${x}, ${y + 20}, ${width}, ${height - 20}));`);
      lines.push(`var font_lbl_${el.id.replace(/-/g, '_')} = new XFont("Arial", 11, XFontStyle.Regular);`);
      lines.push(`${gfx}.DrawString("${fieldLabel}${el.required ? ' *' : ''}", font_lbl_${el.id.replace(/-/g, '_')}, ${colorBrush('#374151')}, new XPoint(${x}, ${y + 14}));`);
      break;
    }

    case 'checkbox': {
      const checkLabel = esc(el.fieldLabel ?? '');
      const boxSize = 14;
      lines.push(`${gfx}.DrawRectangle(${colorPen('#374151', 1.5)}, XBrushes.White, new XRect(${x}, ${y + (height - boxSize) / 2}, ${boxSize}, ${boxSize}));`);
      lines.push(`var font_chk_${el.id.replace(/-/g, '_')} = new XFont("Arial", 12, XFontStyle.Regular);`);
      lines.push(`${gfx}.DrawString("${checkLabel}", font_chk_${el.id.replace(/-/g, '_')}, XBrushes.Black, new XPoint(${x + boxSize + 8}, ${y + height / 2 + 4}));`);
      break;
    }

    case 'signature': {
      const sigLabel = esc(el.signatureLabel ?? 'Signature');
      const lineY = y + height - 14;
      lines.push(`${gfx}.DrawLine(${colorPen('#9ca3af', 1)}, new XPoint(${x}, ${lineY}), new XPoint(${x + width}, ${lineY}));`);
      lines.push(`var font_sig_${el.id.replace(/-/g, '_')} = new XFont("Arial", 10, XFontStyle.Regular);`);
      lines.push(`${gfx}.DrawString("${sigLabel}", font_sig_${el.id.replace(/-/g, '_')}, ${colorBrush('#9ca3af')}, new XPoint(${x}, ${lineY + 12}));`);
      break;
    }

    case 'rect':
    case 'shape': {
      const fill = style.backgroundColor ?? style.fill ?? '#f3f4f6';
      const border = style.borderColor ?? '#d1d5db';
      const bw = Number(style.borderWidth ?? 1);
      lines.push(`${gfx}.DrawRectangle(${colorPen(border, bw)}, ${colorBrush(fill)}, new XRect(${x}, ${y}, ${width}, ${height}));`);
      break;
    }

    case 'circle': {
      const fill = style.backgroundColor ?? '#f3f4f6';
      const border = style.borderColor ?? '#d1d5db';
      lines.push(`${gfx}.DrawEllipse(${colorPen(border, 1)}, ${colorBrush(fill)}, new XRect(${x}, ${y}, ${width}, ${height}));`);
      break;
    }

    case 'line': {
      const color = style.color ?? '#374151';
      const sw = Number(style.strokeWidth ?? 1);
      lines.push(`${gfx}.DrawLine(${colorPen(color, sw)}, new XPoint(${x}, ${y}), new XPoint(${x + width}, ${y + height}));`);
      break;
    }

    case 'image': {
      const src = el.content ?? '';
      const b64 = src.includes(',') ? src.split(',')[1] : src;
      const safe = el.id.replace(/-/g, '_');
      lines.push(`// Image element (embedded as base64)`);
      lines.push(`var imgBytes_${safe} = Convert.FromBase64String("${b64}");`);
      lines.push(`using var imgStream_${safe} = new MemoryStream(imgBytes_${safe});`);
      lines.push(`var ximg_${safe} = XImage.FromStream(imgStream_${safe});`);
      lines.push(`${gfx}.DrawImage(ximg_${safe}, new XRect(${x}, ${y}, ${width}, ${height}));`);
      break;
    }

    case 'qrcode':
      lines.push(`// QR code — value: "${esc(el.qrValue ?? '')}"`);
      lines.push(`// Use a QR library like QRCoder: dotnet add package QRCoder`);
      lines.push(`// var qr = QRCodeGenerator.CreateQrCode("${esc(el.qrValue ?? '')}", QRCodeGenerator.ECCLevel.Q);`);
      lines.push(`// Render QR bitmap and draw at: new XRect(${x}, ${y}, ${width}, ${height})`);
      break;

    case 'barcode':
      lines.push(`// Barcode — value: "${esc(el.barcodeValue ?? '')}"`);
      lines.push(`// Use BarcodeLib: dotnet add package BarcodeLib`);
      lines.push(`// Render barcode and draw at: new XRect(${x}, ${y}, ${width}, ${height})`);
      break;

    case 'table': {
      const cols = el.columnWidths ?? [100, 100, 100];
      const rows = el.cellData ?? [['Header', 'Header', 'Header'], ['Cell', 'Cell', 'Cell']];
      lines.push(`var font_tbl_${el.id.replace(/-/g, '_')} = new XFont("Arial", 11, XFontStyle.Regular);`);
      lines.push(`var font_tbl_hdr_${el.id.replace(/-/g, '_')} = new XFont("Arial", 11, XFontStyle.Bold);`);
      const rowH = rows.length > 0 ? Math.round(height / rows.length) : 24;
      rows.forEach((row, ri) => {
        let colX = x;
        row.forEach((cell, ci) => {
          const colW = cols[ci] ?? 100;
          const isHeader = ri === 0 && el.headerRow;
          const fnt = isHeader ? `font_tbl_hdr_${el.id.replace(/-/g, '_')}` : `font_tbl_${el.id.replace(/-/g, '_')}`;
          lines.push(`${gfx}.DrawRectangle(${colorPen('#e5e7eb', 1)}, XBrushes.White, new XRect(${colX}, ${y + ri * rowH}, ${colW}, ${rowH}));`);
          lines.push(`${gfx}.DrawString("${esc(String(cell))}", ${fnt}, XBrushes.Black, new XRect(${colX + 4}, ${y + ri * rowH}, ${colW - 8}, ${rowH}), XStringFormats.CenterLeft);`);
          colX += colW;
        });
      });
      break;
    }

    case 'date': {
      const dateText = el.dateMode === 'render' ? 'DateTime.Now.ToString("yyyy-MM-dd")' : `"${esc(el.content ?? new Date().toLocaleDateString())}"`;
      lines.push(`var font_date_${el.id.replace(/-/g, '_')} = new XFont("Arial", 12, XFontStyle.Regular);`);
      lines.push(`${gfx}.DrawString(${el.dateMode === 'render' ? dateText : `"${esc(el.content ?? '')}"`}, font_date_${el.id.replace(/-/g, '_')}, XBrushes.Black, new XPoint(${x}, ${y + 14}));`);
      break;
    }

    case 'pagenumber':
      lines.push(`// Page number — rendered at runtime`);
      lines.push(`// ${gfx}.DrawString("1", new XFont("Arial", 11, XFontStyle.Regular), XBrushes.Black, new XPoint(${x}, ${y + 14}));`);
      break;

    case 'watermark': {
      const wmText = esc(el.content ?? 'DRAFT');
      const wmOpacity = el.style?.opacity ?? 0.18;
      lines.push(`// Watermark: "${wmText}" (opacity ${wmOpacity})`);
      lines.push(`${gfx}.Save();`);
      lines.push(`${gfx}.RotateAtTransform(-24, new XPoint(${x + width / 2}, ${y + height / 2}));`);
      lines.push(`var font_wm_${el.id.replace(/-/g, '_')} = new XFont("Arial", 64, XFontStyle.Bold);`);
      lines.push(`${gfx}.DrawString("${wmText}", font_wm_${el.id.replace(/-/g, '_')}, new XSolidBrush(XColor.FromArgb((int)(255 * ${wmOpacity}), 0, 0, 0)), new XPoint(${x}, ${y + height / 2}));`);
      lines.push(`${gfx}.Restore();`);
      break;
    }

    default:
      lines.push(`// ${el.type} element at (${x}, ${y}) — ${width}×${height}`);
  }

  return lines;
}

// ─── Public: PdfSharp code generator ──────────────────────────────────────────

export function generatePdfSharpCode(
  template: Template,
  pages: Page[],
  sharedElements: SimpleElement[],
  pageSettings: PageSettings,
): string {
  const lines: string[] = [];

  lines.push(`// Generated by UIDesigner — ${template.name}`);
  lines.push(`// Library : PDFsharp  https://github.com/empira/PDFsharp`);
  lines.push(`// Install : dotnet add package PDFsharp`);
  lines.push('');
  lines.push('using PdfSharp.Drawing;');
  lines.push('using PdfSharp.Pdf;');
  lines.push('');
  lines.push('var document = new PdfDocument();');
  lines.push(`document.Info.Title = "${esc(template.name)}";`);
  lines.push(`document.Info.Subject = "${esc(template.category)}";`);
  lines.push('');

  pages.forEach((page, pageIndex) => {
    const gfx = `gfx${pageIndex + 1}`;
    lines.push(`// ─── Page ${pageIndex + 1} ${'─'.repeat(50)}`);
    lines.push(`var page${pageIndex + 1} = document.AddPage();`);
    lines.push(`page${pageIndex + 1}.Width  = XUnit.FromPoint(${pageSettings.width});`);
    lines.push(`page${pageIndex + 1}.Height = XUnit.FromPoint(${pageSettings.height});`);
    lines.push(`using var ${gfx} = XGraphics.FromPdfPage(page${pageIndex + 1});`);
    lines.push('');

    if (pageSettings.backgroundColor && pageSettings.backgroundColor !== '#ffffff') {
      lines.push(`${gfx}.DrawRectangle(${colorBrush(pageSettings.backgroundColor)}, new XRect(0, 0, ${pageSettings.width}, ${pageSettings.height}));`);
      lines.push('');
    }

    const allElements = [...sharedElements, ...page.elements].filter(e => !e.hidden);
    if (allElements.length === 0) {
      lines.push(`// No elements on this page`);
    } else {
      allElements.forEach(el => {
        renderElementPdfSharp(el, gfx).forEach(l => lines.push(l));
        lines.push('');
      });
    }
  });

  const filename = esc(template.name.toLowerCase().replace(/\s+/g, '-'));
  lines.push(`document.Save("${filename}.pdf");`);

  return lines.join('\n');
}

// ─── Public: JSON export generator ────────────────────────────────────────────

export function generateJSONExport(
  template: Template,
  pages: Page[],
  sharedElements: SimpleElement[],
  pageSettings: PageSettings,
): string {
  const payload = {
    id: template.id,
    name: template.name,
    category: template.category,
    description: template.description,
    pageSettings: {
      width: pageSettings.width,
      height: pageSettings.height,
      orientation: pageSettings.orientation,
      unit: pageSettings.unit,
      margins: pageSettings.margins,
    },
    pages: pages.map(p => ({
      id: p.id,
      elements: p.elements,
    })),
    sharedElements,
  };
  return JSON.stringify(payload, null, 2);
}

// ─── Legacy export (kept for backward compat) ─────────────────────────────────

export function generateCSharpCode(template: Template): string {
  return generatePdfSharpCode(
    template,
    template.pages ?? [],
    template.sharedElements ?? [],
    { width: 595, height: 842 } as PageSettings,
  );
}
