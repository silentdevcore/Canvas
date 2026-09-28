import type { SimpleElement } from '@/types';
import { sanitizeRichTextHtml } from '@/utils/sanitizeRichTextHtml';

type TextKind = 'date' | 'link' | 'number' | 'pagenumber';

/**
 * Which of the PDF engine's three font slots a CSS family list asks for.
 *
 * `sans-serif` is dropped before the serif test, because it is the generic *sans* keyword and ends
 * most family lists. Without that, a perfectly ordinary `Arial, Helvetica, sans-serif` would be
 * drawn in Times on the Canvas while the generated PDF used Helvetica.
 */
const pdfFontSlot = (value?: string): 'serif' | 'mono' | 'sans' => {
  const normalized = (value?.toLowerCase() ?? '').replace(/sans-serif/g, '');
  if (normalized.includes('courier') || normalized.includes('mono')) return 'mono';
  if (normalized.includes('times') || normalized.includes('serif')) return 'serif';
  return 'sans';
};

export const pdfFontFamily = (value?: string) => {
  switch (pdfFontSlot(value)) {
    case 'serif': return 'Times New Roman, Times, serif';
    case 'mono': return 'Courier New, Courier, monospace';
    default: return "'PXA Helvetica', Arial, Helvetica, sans-serif";
  }
};

const roman = (input: number) => {
  const values = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
  const symbols = ['M', 'CM', 'D', 'CD', 'C', 'XC', 'L', 'XL', 'X', 'IX', 'V', 'IV', 'I'];
  let value = Math.max(1, Math.floor(input));
  let result = '';
  values.forEach((candidate, index) => {
    while (value >= candidate) {
      result += symbols[index];
      value -= candidate;
    }
  });
  return result;
};

export function formatPageNumberForPdf(element: SimpleElement, pageIndex: number, totalPages: number) {
  const number = pageIndex + (element.startNumber ?? 1);
  const value = (() => {
    switch (element.numberingFormat ?? 'current') {
      case 'total': return String(totalPages);
      case 'pageOfTotal': return `${number} / ${totalPages}`;
      case 'roman': return roman(number);
      case 'alphabetic': return String.fromCharCode(65 + ((Math.max(1, number) - 1) % 26));
      default: return String(number);
    }
  })();
  return `${element.prefix ?? ''}${value}${element.suffix ?? ''}`;
}

export function formatNumberForPdf(element: SimpleElement) {
  const value = element.numberValue ?? 0;
  const decimals = element.numberDecimals ?? 2;
  const locale = element.numberLocale ?? 'en-US';
  try {
    switch ((element.numberStyle ?? 'decimal').toLowerCase()) {
      case 'currency': {
        const currency = element.numberCurrency ?? 'USD';
        const formatter = new Intl.NumberFormat(locale, {
          style: 'currency', currency, currencyDisplay: 'code',
          minimumFractionDigits: decimals, maximumFractionDigits: decimals,
        });
        if (locale.toLowerCase() === 'de-ch') {
          const numeric = new Intl.NumberFormat(locale, {
            minimumFractionDigits: decimals, maximumFractionDigits: decimals,
          }).format(Math.abs(value));
          return `${currency}${value < 0 ? '-' : ' '}${numeric}`;
        }
        return formatter.format(value);
      }
      case 'percent':
        return new Intl.NumberFormat(locale, {
          style: 'percent', minimumFractionDigits: decimals, maximumFractionDigits: decimals,
        }).format(value);
      case 'scientific':
        return value.toExponential(decimals).replace('e', 'E');
      case 'ordinal': {
        const rounded = Math.round(value);
        const remainder = Math.abs(rounded) % 100;
        const suffix = remainder >= 11 && remainder <= 13
          ? 'th'
          : ['th', 'st', 'nd', 'rd'][Math.abs(rounded) % 10] ?? 'th';
        return `${rounded}${suffix}`;
      }
      default:
        return new Intl.NumberFormat(locale, {
          minimumFractionDigits: decimals, maximumFractionDigits: decimals,
        }).format(value);
    }
  } catch {
    return value.toFixed(decimals);
  }
}

export function PdfSingleLineText({
  element,
  kind,
  text,
}: {
  element: SimpleElement;
  kind: TextKind;
  text: string;
}) {
  const style = element.style ?? {};
  const size = Number(style.fontSize ?? (kind === 'number' ? 12 : 12));
  const weight = style.fontWeight === 'bold' || Number(style.fontWeight) >= 600 ? 700 : 400;
  const decoration = kind === 'link' || String(style.textDecoration ?? '').includes('underline')
    ? 'underline'
    : undefined;

  return (
    <svg
      aria-hidden="true"
      viewBox={`0 0 ${element.width} ${element.height}`}
      preserveAspectRatio="none"
      width="100%"
      height="100%"
      style={{ display: 'block', overflow: 'hidden' }}
    >
      <text
        x="0"
        y={size * 0.72}
        fill={style.color ?? (kind === 'link' ? '#2563eb' : '#101828')}
        fontFamily={pdfFontFamily(style.fontFamily)}
        fontSize={size}
        fontWeight={weight}
        fontStyle={style.fontStyle ?? 'normal'}
        textDecoration={decoration}
      >
        {text}
      </text>
    </svg>
  );
}

const wrapPdfText = (text: string, width: number, fontSize: number, family?: string) => {
  const slot = pdfFontSlot(family);
  const factor = slot === 'mono' ? 0.6 : slot === 'serif' ? 0.5 : 0.52;
  const lines: string[] = [];
  text.replace(/\r\n/g, '\n').split('\n').forEach(paragraph => {
    if (!paragraph) {
      lines.push('');
      return;
    }
    let current = '';
    paragraph.split(/\s+/).filter(Boolean).forEach(word => {
      const candidate = current ? `${current} ${word}` : word;
      if (!current || candidate.length * fontSize * factor <= width) current = candidate;
      else {
        lines.push(current);
        current = word;
      }
    });
    if (current) lines.push(current);
  });
  return lines;
};

export function PdfTextBlockRenderer({ element, text }: { element: SimpleElement; text: string }) {
  const style = element.style ?? {};
  const size = Number(style.fontSize ?? 12);
  const padding = Number(style.padding ?? 0);
  const left = Number(style.paddingLeft ?? padding);
  const right = Number(style.paddingRight ?? padding);
  const top = Number(style.paddingTop ?? padding);
  const lineHeightValue = Number(style.lineHeight ?? 1.2);
  const lineHeight = lineHeightValue < 8 ? lineHeightValue * size : lineHeightValue;
  const availableWidth = Math.max(1, element.width - left - right);
  const lines = wrapPdfText(text, availableWidth, size, style.fontFamily);
  const alignment = style.textAlign ?? 'left';
  const x = alignment === 'center' ? element.width / 2 : alignment === 'right' ? element.width - right : left;
  const anchor = alignment === 'center' ? 'middle' : alignment === 'right' ? 'end' : 'start';
  const weight = style.fontWeight === 'bold' || Number(style.fontWeight) >= 600 ? 700 : 400;
  const borderWidth = Number(style.borderWidth ?? 0);
  const sideBorders = (['Top', 'Right', 'Bottom', 'Left'] as const)
    .map(side => ({
      side,
      width: Number(style[`border${side}Width`] ?? 0),
      color: style[`border${side}Color`] ?? style.borderColor ?? '#000000',
    }))
    .filter(border => border.width > 0);

  return (
    <svg
      aria-hidden="true"
      viewBox={`0 0 ${element.width} ${element.height}`}
      preserveAspectRatio="none"
      width="100%"
      height="100%"
      style={{ display: 'block', overflow: 'hidden' }}
    >
      {style.backgroundColor && style.backgroundColor !== 'transparent' && (
        <rect width={element.width} height={element.height} fill={style.backgroundColor} />
      )}
      {borderWidth > 0 && sideBorders.length === 0 && (
        <rect
          x={borderWidth / 2}
          y={borderWidth / 2}
          width={element.width - borderWidth}
          height={element.height - borderWidth}
          fill="none"
          stroke={style.borderColor ?? '#000000'}
          strokeWidth={borderWidth}
        />
      )}
      {sideBorders.map(border => {
        const half = border.width / 2;
        const coordinates = border.side === 'Top'
          ? { x1: 0, y1: half, x2: element.width, y2: half }
          : border.side === 'Right'
            ? { x1: element.width - half, y1: 0, x2: element.width - half, y2: element.height }
            : border.side === 'Bottom'
              ? { x1: 0, y1: element.height - half, x2: element.width, y2: element.height - half }
              : { x1: half, y1: 0, x2: half, y2: element.height };
        return <line key={border.side} {...coordinates} stroke={border.color} strokeWidth={border.width} />;
      })}
      {lines.map((line, index) => line && (
        <text
          key={`${index}-${line}`}
          x={x}
          y={top + size * 0.72 + index * lineHeight}
          textAnchor={anchor}
          fill={style.color ?? '#101828'}
          fontFamily={pdfFontFamily(style.fontFamily)}
          fontSize={size}
          fontWeight={weight}
          fontStyle={style.fontStyle ?? 'normal'}
          textDecoration={style.textDecoration}
          letterSpacing={style.letterSpacing}
          direction={element.textDirection ?? 'ltr'}
        >
          {line}
        </text>
      ))}
    </svg>
  );
}

export function PdfRichTextRenderer({ element }: { element: SimpleElement }) {
  const style = element.style ?? {};
  const baseSize = Number(style.fontSize ?? 12);
  const baseColor = style.color ?? '#101828';
  const padding = Number(style.padding ?? 0);
  const left = Number(style.paddingLeft ?? padding);
  const right = Number(style.paddingRight ?? padding);
  const top = Number(style.paddingTop ?? padding);
  const maxWidth = Math.max(1, element.width - left - right);
  const paragraphs = parseRichTextRuns(sanitizeRichTextHtml(element.htmlContent ?? element.content ?? ''), baseSize, baseColor);
  const positioned: Array<RichTextRun & { x: number; y: number }> = [];
  let baseline = top + baseSize * 0.72;

  paragraphs.forEach(paragraph => {
    if (paragraph.length === 0) return;
    const maxSize = Math.max(...paragraph.map(run => run.size));
    const configuredLineHeight = Number(style.lineHeight ?? 0);
    const lineHeight = configuredLineHeight > 0
      ? configuredLineHeight < 8 ? configuredLineHeight * baseSize : configuredLineHeight
      : maxSize * 1.35;
    const lines = wrapRichTextRuns(paragraph, maxWidth);
    lines.forEach(line => {
      let x = left;
      line.forEach(run => {
        if (run.text.trim()) positioned.push({ ...run, x, y: baseline });
        x += richTextRunWidth(run);
      });
      baseline += lineHeight;
    });
    baseline += maxSize * 0.3;
  });

  return (
    <svg
      aria-hidden="true"
      viewBox={`0 0 ${element.width} ${element.height}`}
      preserveAspectRatio="none"
      width="100%"
      height="100%"
      style={{ display: 'block', overflow: 'hidden' }}
    >
      {positioned.map((run, index) => (
        <text
          key={`${index}-${run.x}-${run.y}`}
          x={run.x}
          y={run.y}
          fill={run.color}
          fontFamily={pdfFontFamily(style.fontFamily)}
          fontSize={run.size}
          fontWeight={run.bold ? 700 : 400}
          fontStyle={run.italic ? 'italic' : 'normal'}
          textDecoration={[run.underline && 'underline', run.strike && 'line-through'].filter(Boolean).join(' ') || undefined}
        >
          {run.text.trimEnd()}
        </text>
      ))}
    </svg>
  );
}

type RichTextRun = {
  text: string;
  size: number;
  color: string;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  strike: boolean;
};

const last = <T,>(values: T[]) => values[values.length - 1];

const decodeRichText = (value: string) => value
  .replace(/&nbsp;/gi, ' ')
  .replace(/&amp;/gi, '&')
  .replace(/&lt;/gi, '<')
  .replace(/&gt;/gi, '>')
  .replace(/&quot;/gi, '"')
  .replace(/&#39;/gi, "'");

export function parseRichTextRuns(html: string, baseSize: number, baseColor: string) {
  const paragraphs: RichTextRun[][] = [];
  let current: RichTextRun[] = [];
  const bold = [false];
  const italic = [false];
  const underline = [false];
  const strike = [false];
  const colors = [baseColor];
  const sizes = [baseSize];
  const flush = () => {
    if (current.length > 0) paragraphs.push(current);
    current = [];
  };

  for (const token of html.match(/<[^>]+>|[^<]+/g) ?? []) {
    if (!token.startsWith('<')) {
      const text = decodeRichText(token);
      if (text) current.push({
        text,
        size: last(sizes) ?? baseSize,
        color: last(colors) ?? baseColor,
        bold: last(bold) ?? false,
        italic: last(italic) ?? false,
        underline: last(underline) ?? false,
        strike: last(strike) ?? false,
      });
      continue;
    }

    const closing = /^<\//.test(token);
    const name = token.match(/^<\/?([\w]+)/)?.[1]?.toLowerCase() ?? '';
    if (closing) {
      if (name === 'b' || name === 'strong') bold.length > 1 && bold.pop();
      else if (name === 'i' || name === 'em') italic.length > 1 && italic.pop();
      else if (name === 'u') underline.length > 1 && underline.pop();
      else if (['s', 'strike', 'del'].includes(name)) strike.length > 1 && strike.pop();
      else if (name === 'span') {
        colors.length > 1 && colors.pop();
        sizes.length > 1 && sizes.pop();
      } else if (/^h[1-6]$/.test(name)) {
        sizes.length > 1 && sizes.pop();
        bold.length > 1 && bold.pop();
        flush();
      } else if (['p', 'div', 'li', 'blockquote'].includes(name)) flush();
      continue;
    }

    if (name === 'b' || name === 'strong') bold.push(true);
    else if (name === 'i' || name === 'em') italic.push(true);
    else if (name === 'u') underline.push(true);
    else if (['s', 'strike', 'del'].includes(name)) strike.push(true);
    else if (name === 'br') flush();
    else if (name === 'li') {
      flush();
      current.push({ text: '• ', size: last(sizes) ?? baseSize, color: last(colors) ?? baseColor, bold: last(bold) ?? false, italic: false, underline: false, strike: false });
    } else if (name === 'span') {
      const inlineStyle = token.match(/style=["']([^"']*)["']/i)?.[1] ?? '';
      colors.push(inlineStyle.match(/color:\s*([^;]+)/i)?.[1]?.trim() ?? (last(colors) ?? baseColor));
      const parsedSize = Number(inlineStyle.match(/font-size:\s*(\d+(?:\.\d+)?)/i)?.[1]);
      sizes.push(Number.isFinite(parsedSize) && parsedSize > 0 ? parsedSize : (last(sizes) ?? baseSize));
    } else if (/^h[1-6]$/.test(name)) {
      const scales: Record<string, number> = { h1: 2, h2: 1.6, h3: 1.3, h4: 1.15, h5: 1, h6: 1 };
      sizes.push(baseSize * scales[name]);
      bold.push(true);
    }
  }
  flush();
  return paragraphs;
}

const richTextRunWidth = (run: RichTextRun) => run.text.length * run.size * (run.bold ? 0.57 : 0.52);

function wrapRichTextRuns(runs: RichTextRun[], maxWidth: number) {
  const lines: RichTextRun[][] = [];
  let line: RichTextRun[] = [];
  let width = 0;
  runs.forEach(run => {
    const words = run.text.split(' ');
    words.forEach((word, index) => {
      const part = index < words.length - 1 ? `${word} ` : word;
      if (!part) return;
      const partRun = { ...run, text: part };
      const partWidth = richTextRunWidth(partRun);
      if (width + partWidth > maxWidth && line.length > 0 && part.trim()) {
        lines.push(line);
        const trimmed = part.trimStart();
        line = trimmed ? [{ ...run, text: trimmed }] : [];
        width = trimmed ? richTextRunWidth({ ...run, text: trimmed }) : 0;
      } else {
        line.push(partRun);
        width += partWidth;
      }
    });
  });
  if (line.length > 0) lines.push(line);
  return lines;
}
