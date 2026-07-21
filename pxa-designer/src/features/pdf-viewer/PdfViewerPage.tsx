import React, { useMemo } from 'react';
import PdfViewer from './PdfViewer';
import { resolvePdfViewerInitialSource } from './handoff';

const PdfViewerPage: React.FC = () => {
  const initialSource = useMemo(() => resolvePdfViewerInitialSource(), []);

  return (
    <div className="pdfv-page">
      <PdfViewer initialSource={initialSource} />
    </div>
  );
};

export default PdfViewerPage;
