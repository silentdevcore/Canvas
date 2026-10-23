import { writePdfViewerHandoff, type PdfViewerInitialFitMode } from './handoff';

export interface MigratedPdfViewerConfig {
  schemaVersion: 1;
  providerKey: 'nutrient-viewer';
  route?: '/pdf/viewer';
  mountSelector?: string;
  documentSource?: string;
  initialPageNumber?: number;
  initialFitMode?: PdfViewerInitialFitMode;
  locale?: string;
  theme?: string;
  requestedToolbarItems?: unknown[];
  configurationReviewItems?: string[];
  sourceReviewRequired?: boolean;
  documentEngineReviewRequired?: boolean;
}

const isApplicationOwnedSource = (source: string): boolean => (
  (source.startsWith('/') && !source.startsWith('//'))
  || (source.startsWith('./') && !source.includes('../'))
);

export const openMigratedPdfViewer = (config: MigratedPdfViewerConfig): void => {
  if (!config.documentSource || !isApplicationOwnedSource(config.documentSource)) {
    throw new Error('The migrated Viewer requires an application-owned PDF source.');
  }

  writePdfViewerHandoff({
    url: config.documentSource,
    initialPageNumber: config.initialPageNumber,
    initialFitMode: config.initialFitMode,
  });

  window.location.assign(`${config.route ?? '/pdf/viewer'}?handoff=session`);
};
