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
    expect(annotationTypeFromTool('ink')).toBe('ink');
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
        id: 'annotation-ink',
        type: 'ink',
        points: [{ xPct: 10, yPct: 10 }, { xPct: 20, yPct: 22 }],
      }),
    ];

    const sidecar = createAnnotationSidecar('document.pdf', annotations);

    expect(sidecar.version).toBe(1);
    expect(sidecar.sourceName).toBe('document.pdf');
    expect(sidecar.annotations).toEqual(annotations);
    expect(Date.parse(sidecar.exportedAt)).not.toBeNaN();
  });

  test('parses current object sidecar and legacy raw array sidecar', () => {
    const annotations = [sampleAnnotation({ locked: true })];
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
