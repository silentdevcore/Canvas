import type { CSSProperties, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { FiBox } from 'react-icons/fi';
import type { ElementType, SimpleElement } from '@/types';
import PdfContractChartRenderer from '@/chart/PdfContractChartRenderer';
import FormFieldRenderer from './FormFieldRenderer';
import LineRenderer from './LineRenderer';
import BackendSymbolRenderer from './BackendSymbolRenderer';
import ArrowRenderer from './ArrowRenderer';
import HighlightRenderer from './HighlightRenderer';
import DocumentImageRenderer from './DocumentImageRenderer';
import LegacyTableRenderer from './LegacyTableRenderer';
import { formatNumberForPdf, formatPageNumberForPdf, PdfRichTextRenderer, PdfSingleLineText, PdfTextBlockRenderer } from './PdfTextRenderer';
import { PdfButtonRenderer, PdfCheckboxRenderer, PdfCheckmarkRenderer, PdfDropdownRenderer, PdfRadioRenderer, PdfSignatureRenderer } from './PdfControlRenderer';
import { PdfOptionListRenderer } from './PdfOptionListRenderer';
import { canRenderPdfTable, PdfTableRenderer } from './PdfTableRenderer';
import { PdfTableOfContentsRenderer, PdfWatermarkRenderer } from './PdfDocumentRenderer';
import { PdfCircleRenderer, PdfDrawingRenderer, PdfShapeRenderer } from './PdfGraphicsRenderer';
import { PdfCommentRenderer, PdfContentControlRenderer, PdfLayoutBoundaryRenderer, PdfNoteRenderer, PdfFootnoteRenderer } from './PdfStructureRenderer';
import { documentDateText, identityDocumentContent, resolveDocumentElement, type DocumentContentResolver } from './documentContent';

interface Context {
  pageIndex: number;
  pageCount: number;
  pageHeight: number;
  imageLabel: string;
  chartLabel: string;
  now?: Date;
  resolve: (value: string) => string | undefined;
}

const renderers: Record<ElementType, (element: SimpleElement, context: Context) => ReactNode> = {
  text: element => <PdfTextBlockRenderer element={element} text={element.content ?? ''} />,
  richtext: element => <PdfRichTextRenderer element={element} />,
  date: (element, context) => {
    const text = documentDateText(element, context.now, context.resolve);
    const printable = /[\u0600-\u06ff]/.test(text) || /^ar(?:-|$)/i.test(element.language ?? '') ? { ...element, textDirection: 'rtl' as const,
      style: { ...element.style, fontFamily: 'Noto Sans Arabic' } } : element;
    return <PdfTextBlockRenderer element={printable} text={text} />;
  },
  number: element => <PdfSingleLineText element={element} kind="number"
    text={`${element.prefix ?? ''}${formatNumberForPdf(element)}${element.suffix ?? ''}`} />,
  field: element => <FormFieldRenderer element={element} />,
  textarea: element => <FormFieldRenderer element={element} multiline />,
  checkbox: element => <PdfCheckboxRenderer element={element} />,
  radio: element => <PdfRadioRenderer element={element} />,
  optionlist: element => <PdfOptionListRenderer element={element} />,
  dropdown: element => <PdfDropdownRenderer element={element} />,
  button: element => <PdfButtonRenderer element={element} />,
  signature: element => <PdfSignatureRenderer element={element} />,
  table: element => canRenderPdfTable(element) ? <PdfTableRenderer element={element} /> : <LegacyTableRenderer element={element} />,
  chart: (element, context) => <PdfContractChartRenderer chart={element.chart}
    legacyType={element.chartType} legacyData={element.chartData} ariaLabel={element.name || context.chartLabel}
    width={element.width} height={element.height} />,
  image: (element, context) => <DocumentImageRenderer element={element} label={context.imageLabel} />,
  qrcode: element => <BackendSymbolRenderer element={element} type="qrcode" />,
  barcode: element => <BackendSymbolRenderer element={element} type="barcode" />,
  shape: element => <PdfShapeRenderer element={element} />,
  rect: element => <PdfShapeRenderer element={element} />,
  circle: element => <PdfCircleRenderer element={element} />,
  line: element => <LineRenderer element={element} />,
  arrow: element => <ArrowRenderer element={element} />,
  draw: element => <PdfDrawingRenderer element={element} />,
  area: element => <PdfLayoutBoundaryRenderer element={element} />,
  subsection: element => <PdfLayoutBoundaryRenderer element={element} />,
  highlight: element => <HighlightRenderer element={element} />,
  checkmark: element => <PdfCheckmarkRenderer element={element} />,
  link: element => <PdfSingleLineText element={element} kind="link" text={element.content || element.href || ''} />,
  note: element => <PdfNoteRenderer element={element} />,
  watermark: element => <PdfWatermarkRenderer element={element} />,
  pagenumber: (element, context) => <PdfSingleLineText element={element} kind="pagenumber"
    text={formatPageNumberForPdf(element, context.pageIndex, context.pageCount)} />,
  // Navigation metadata has no printable content. Editor markers live outside this renderer.
  pageboundary: () => null,
  bookmark: () => null,
  toc: element => <PdfTableOfContentsRenderer element={element} />,
  footnote: (element, context) => <PdfFootnoteRenderer element={element} pageHeight={context.pageHeight} />,
  endnote: (element, context) => <PdfFootnoteRenderer element={element} pageHeight={context.pageHeight} />,
  comment: element => <PdfCommentRenderer element={element} />,
  contentcontrol: element => <PdfContentControlRenderer element={element} />,
};

export const DOCUMENT_RENDERED_ELEMENT_TYPES = Object.keys(renderers) as ElementType[];
const framedTypes = new Set<ElementType>(['image', 'qrcode', 'barcode', 'chart', 'number', 'pagenumber']);

export function documentContentBoxStyle(element: SimpleElement): CSSProperties | undefined {
  if (!framedTypes.has(element.type)) return undefined;
  const source = element.style ?? {};
  const result: CSSProperties = {};
  const sides = ['Top', 'Right', 'Bottom', 'Left'] as const;
  if (sides.some(side => source[`border${side}Width`] != null)) {
    for (const side of sides) if (source[`border${side}Width`] != null)
      result[`border${side}`] = `${Number(source[`border${side}Width`]) || 0}px ${source[`border${side}Style`] || source.borderStyle || 'solid'} ${source[`border${side}Color`] || source.borderColor || '#000'}`;
  } else if (source.borderWidth > 0) result.border = `${source.borderWidth}px ${source.borderStyle || 'solid'} ${source.borderColor || '#000'}`;
  if (source.borderRadius) result.borderRadius = source.borderRadius;
  for (const side of sides) if (source[`padding${side}`] != null)
    result[`padding${side}`] = source[`padding${side}`];
  const background = source.backgroundColor ?? source.fill;
  if (background && background !== 'transparent') {
    result.backgroundColor = background;
    if ((source.backgroundOpacity ?? 1) < 1 && /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(background)) {
      const hex = background.slice(1);
      const full = hex.length === 3 ? [...hex].map(character => character + character).join('') : hex;
      result.backgroundColor = `rgba(${[0, 2, 4].map(index => parseInt(full.slice(index, index + 2), 16)).join(',')},${source.backgroundOpacity})`;
    }
  }
  return Object.keys(result).length ? { width: '100%', height: '100%', boxSizing: 'border-box', ...result } : undefined;
}

export function DocumentElementRenderer({ element, pageIndex, pageCount, pageHeight, resolveContent = identityDocumentContent, zoom = 1, now }: {
  element: SimpleElement;
  pageIndex: number;
  pageCount: number;
  pageHeight: number;
  resolveContent?: DocumentContentResolver;
  zoom?: number;
  now?: Date;
}) {
  const { t } = useTranslation('editor');
  const resolved = resolveDocumentElement(element, resolveContent);
  const render = Object.prototype.hasOwnProperty.call(renderers, resolved.type) ? renderers[resolved.type] : undefined;
  let content = render ? render(resolved, { pageIndex, pageCount, pageHeight, now,
    imageLabel: t('placeholders.image'), chartLabel: t('diagnostics.chart'), resolve: resolveContent }) : (
    <div className="editor-placeholder" data-unsupported-element={resolved.type}>
      <FiBox className="editor-placeholder-icon" /><span>{resolved.type}</span>
    </div>
  );
  const box = documentContentBoxStyle(resolved);
  if (box) content = <div style={box}>{content}</div>;
  return zoom === 1 ? <>{content}</> : <div data-document-scale={zoom} style={{ width: element.width, height: element.height,
    transform: `scale(${zoom})`, transformOrigin: 'top left', overflow: 'visible' }}>{content}</div>;
}
