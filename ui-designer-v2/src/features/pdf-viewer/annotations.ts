export type ReviewTool =
  | 'view'
  | 'note'
  | 'freeText'
  | 'stamp'
  | 'line'
  | 'rectangle'
  | 'circle'
  | 'ink'
  | 'highlight'
  | 'underline'
  | 'strikeout';

export type StampLabel = 'Draft' | 'Approved' | 'Final' | 'Confidential';

export type AnnotationType =
  | 'note'
  | 'freeText'
  | 'stamp'
  | 'line'
  | 'rectangle'
  | 'circle'
  | 'ink'
  | 'highlight'
  | 'underline'
  | 'strikeout';

export interface InkPoint {
  xPct: number;
  yPct: number;
}

export interface PdfAnnotation {
  id: string;
  type: AnnotationType;
  pageNumber: number;
  xPct: number;
  yPct: number;
  widthPct: number;
  heightPct: number;
  text: string;
  author: string;
  createdAt: string;
  color: string;
  locked?: boolean;
  points?: InkPoint[];
}

export interface PdfAnnotationSidecar {
  version: 1;
  sourceName: string | null;
  exportedAt: string;
  annotations: PdfAnnotation[];
}

export const STAMP_LABELS: StampLabel[] = ['Draft', 'Approved', 'Final', 'Confidential'];

export const stampColor = (label: StampLabel): string => {
  switch (label) {
    case 'Approved':
      return '#16a34a';
    case 'Final':
      return '#2563eb';
    case 'Confidential':
      return '#dc2626';
    case 'Draft':
    default:
      return '#9333ea';
  }
};

export const annotationTypeFromTool = (tool: ReviewTool): AnnotationType => {
  switch (tool) {
    case 'stamp':
      return 'stamp';
    case 'note':
      return 'note';
    case 'line':
      return 'line';
    case 'rectangle':
      return 'rectangle';
    case 'circle':
      return 'circle';
    case 'ink':
      return 'ink';
    case 'highlight':
      return 'highlight';
    case 'underline':
      return 'underline';
    case 'strikeout':
      return 'strikeout';
    case 'freeText':
    case 'view':
    default:
      return 'freeText';
  }
};

export const createAnnotationSidecar = (
  sourceName: string | null,
  annotations: PdfAnnotation[],
): PdfAnnotationSidecar => ({
  version: 1,
  sourceName,
  exportedAt: new Date().toISOString(),
  annotations,
});

export const parseAnnotationSidecar = (raw: string): PdfAnnotation[] => {
  const parsed = JSON.parse(raw) as { annotations?: PdfAnnotation[] } | PdfAnnotation[];
  const imported = Array.isArray(parsed) ? parsed : parsed.annotations;
  if (!Array.isArray(imported)) {
    throw new Error('Annotation sidecar does not contain an annotations array.');
  }

  return imported;
};
