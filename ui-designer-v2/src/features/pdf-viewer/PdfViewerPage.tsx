import React, { useMemo } from 'react';
import AppHeader from '@/components/Layout/AppHeader';
import PdfViewer from './PdfViewer';
import { resolvePdfViewerInitialSource } from './handoff';

const PdfViewerPage: React.FC = () => {
  const initialSource = useMemo(() => resolvePdfViewerInitialSource(), []);

  return (
    <div className="pdfv-page">
      <AppHeader activePage="viewer" />
      <PdfViewer initialSource={initialSource} />
    </div>
  );
};

export default PdfViewerPage;
