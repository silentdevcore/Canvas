import React, { useMemo } from 'react';
import PdfViewer from './PdfViewer';
import { resolvePdfViewerLaunchConfig } from './handoff';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { designerPermissions, hasDesignerPermission } from '@/auth/designerPermissions';

const PdfViewerPage: React.FC = () => {
  const launchConfig = useMemo(() => resolvePdfViewerLaunchConfig(), []);
  const { user } = useDesignerAuth();

  return (
    <div className="pdfv-page">
      <PdfViewer
        initialSource={launchConfig.source}
        initialPageNumber={launchConfig.initialPageNumber}
        initialFitMode={launchConfig.initialFitMode}
        canDownload={hasDesignerPermission(user, designerPermissions.pdfExport)}
        canPrint={hasDesignerPermission(user, designerPermissions.pdfPrint)}
        canInspectTrust={hasDesignerPermission(user, designerPermissions.pdfTrustInspect)}
        canAnnotate={hasDesignerPermission(user, designerPermissions.pdfViewerAnnotate)}
        canFillForms={hasDesignerPermission(user, designerPermissions.pdfViewerForms)}
        canRedact={hasDesignerPermission(user, designerPermissions.pdfViewerRedact)}
      />
    </div>
  );
};

export default PdfViewerPage;
