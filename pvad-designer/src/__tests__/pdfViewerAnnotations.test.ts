import {
  STAMP_LABELS,
  annotationTypeFromTool,
  createAnnotationSidecar,
  parseAnnotationSidecar,
  stampColor,
  type PdfAnnotation,
} from '../features/pdf-viewer/annotations';

const sampleAnnotation = (overrides: Partial<PdfAnnotation> = {}): PdfAnnotation => ({
  id: 'annotation-1',
  type: 'highlight',
  pageNumber: 2,
  xPct: 12,
  yPct: 18,
  widthPct: 30,
  heightPct: 6,
  text: '',
  author: 'Reviewer',
  createdAt: '2026-06-25T10:00:00.000Z',
  color: '#fef08a',
  locked: false,
  ...overrides,
});

describe('pdf viewer annotation sidecar model', () => {
  test('maps review tools to annotation types', () => {
    expect(annotationTypeFromTool('view')).toBe('freeText');
    expect(annotationTypeFromTool('note')).toBe('note');
    expect(annotationTypeFromTool('stamp')).toBe('stamp');
    expect(annotationTypeFromTool('image')).toBe('image');
    expect(annotationTypeFromTool('ink')).toBe('ink');
    expect(annotationTypeFromTool('inkEraser')).toBe('ink');
    expect(annotationTypeFromTool('redaction')).toBe('redaction');
    expect(annotationTypeFromTool('strikeout')).toBe('strikeout');
  });

  test('defines the expected predefined stamps and colors', () => {
    expect(STAMP_LABELS).toEqual(['Draft', 'Approved', 'Final', 'Confidential']);
    expect(stampColor('Draft')).toBe('#9333ea');
    expect(stampColor('Approved')).toBe('#16a34a');
    expect(stampColor('Final')).toBe('#2563eb');
    expect(stampColor('Confidential')).toBe('#dc2626');
  });

  test('creates a versioned sidecar payload', () => {
    const annotations = [
      sampleAnnotation(),
      sampleAnnotation({
        id: 'annotation-markup-quads',
        quadPoints: [
          {
            x1Pct: 10,
            y1Pct: 20,
            x2Pct: 40,
            y2Pct: 20,
            x3Pct: 10,
            y3Pct: 24,
            x4Pct: 40,
            y4Pct: 24,
          },
        ],
      }),
      sampleAnnotation({
        id: 'annotation-ink',
        type: 'ink',
        opacity: 72,
        strokeWidth: 5,
        points: [{ xPct: 10, yPct: 10 }, { xPct: 20, yPct: 22 }],
      }),
      sampleAnnotation({
        id: 'annotation-image',
        type: 'image',
        imageDataUrl: 'data:image/png;base64,iVBORw0KGgo=',
      }),
      sampleAnnotation({
        id: 'annotation-redaction',
        type: 'redaction',
        color: '#111827',
        opacity: 88,
      }),
    ];

    const sidecar = createAnnotationSidecar('document.pdf', annotations);

    expect(sidecar.version).toBe(1);
    expect(sidecar.sourceName).toBe('document.pdf');
    expect(sidecar.annotations).toEqual(annotations);
    expect(sidecar.annotations[1].quadPoints?.[0].x2Pct).toBe(40);
    expect(sidecar.annotations[2].strokeWidth).toBe(5);
    expect(sidecar.annotations[2].opacity).toBe(72);
    expect(sidecar.annotations[3].imageDataUrl).toContain('data:image/png');
    expect(sidecar.annotations[4].type).toBe('redaction');
    expect(Date.parse(sidecar.exportedAt)).not.toBeNaN();
  });

  test('parses current object sidecar and legacy raw array sidecar', () => {
    const annotations = [sampleAnnotation({
      locked: true,
      fillEnabled: true,
      fillColor: '#ffffff',
      lineEndingStart: 'circle',
      lineEndingEnd: 'arrow',
    })];
    const objectPayload = JSON.stringify(createAnnotationSidecar('document.pdf', annotations));
    const arrayPayload = JSON.stringify(annotations);

    expect(parseAnnotationSidecar(objectPayload)).toEqual(annotations);
    expect(parseAnnotationSidecar(arrayPayload)).toEqual(annotations);
  });

  test('rejects malformed sidecar payloads', () => {
    expect(() => parseAnnotationSidecar(JSON.stringify({ version: 1 })))
      .toThrow('Annotation sidecar does not contain an annotations array.');
  });
});
