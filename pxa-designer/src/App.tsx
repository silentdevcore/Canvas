import React, { Suspense } from 'react';
import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import IndexPage from '@/pages/IndexPage';
import TemplatePage from '@/pages/TemplatePage';
import CreatePage from '@/pages/CreatePage';
import DocsPage from '@/pages/DocsPage';
import MigrationsPage from '@/pages/MigrationsPage';
import ImporterPage from '@/pages/ImporterPage';
import ConvertToPdfPage from '@/pages/ConvertToPdfPage';
import SpreadsheetImportPage from '@/pages/SpreadsheetImportPage';
import PdfLayout from '@/components/Layout/PdfLayout';
import SpreadsheetLayout from '@/components/Layout/SpreadsheetLayout';
import LocaleProvider from '@/components/Layout/LocaleProvider';
import DesignerAuthGate from '@/auth/DesignerAuthGate';
import ProductExperienceProvider from '@/product/ProductExperienceProvider';
import RequireDesignerPermission from '@/auth/RequireDesignerPermission';
import { designerPermissions } from '@/auth/designerPermissions';

const PdfViewerPage = React.lazy(() => import('@/features/pdf-viewer/PdfViewerPage'));
const SpreadsheetEditorPage = React.lazy(() => import('@/pages/SpreadsheetEditorPage'));
const visualParityEnabled = typeof __PXA_VISUAL_PARITY_ENABLED__ !== 'undefined'
  && __PXA_VISUAL_PARITY_ENABLED__;
const VisualParityPage = visualParityEnabled
  ? React.lazy(() => import('@/pages/VisualParityPage'))
  : null;

const PdfCreateRoute: React.FC = () => {
  const location = useLocation();
  const permission = new URLSearchParams(location.search).get('mode') === 'code'
    ? designerPermissions.pdfEdit
    : designerPermissions.pdfCreate;
  return (
    <RequireDesignerPermission permission={permission}>
      <CreatePage />
    </RequireDesignerPermission>
  );
};

const App: React.FC = () => {
  if (visualParityEnabled && VisualParityPage && location.pathname === '/__visual-parity') {
    return (
      <LocaleProvider>
        <Suspense fallback={null}><VisualParityPage /></Suspense>
      </LocaleProvider>
    );
  }

  return (
    <LocaleProvider>
    <DesignerAuthGate>
      <ProductExperienceProvider>
      <Routes>
      <Route path="/" element={<IndexPage />} />
      <Route path="/docs" element={<DocsPage />} />

      <Route path="/pdf" element={(
        <RequireDesignerPermission permission={designerPermissions.pdfAccess}>
          <PdfLayout />
        </RequireDesignerPermission>
      )}>
        <Route index element={<Navigate to="template" replace />} />
        <Route path="create" element={<PdfCreateRoute />} />
        <Route path="edit" element={<Navigate to="/pdf/create?mode=code" replace />} />
        <Route path="template" element={(
          <RequireDesignerPermission permission={designerPermissions.pdfTemplatesRead}>
            <TemplatePage />
          </RequireDesignerPermission>
        )} />
        <Route path="import" element={(
          <RequireDesignerPermission permission={designerPermissions.pdfImport}>
            <ImporterPage />
          </RequireDesignerPermission>
        )} />
        <Route path="convert" element={(
          <RequireDesignerPermission permission={designerPermissions.pdfConvert}>
            <ConvertToPdfPage />
          </RequireDesignerPermission>
        )} />
        <Route
          path="viewer"
          element={(
            <RequireDesignerPermission permission={designerPermissions.pdfViewerRead}>
              <Suspense fallback={<div className="route-loading">Loading PDF viewer...</div>}>
                <PdfViewerPage />
              </Suspense>
            </RequireDesignerPermission>
          )}
        />
        {/* Distinct `key` per route forces a fresh MigrationsPage instance so each
            sub-tab is its own view (no cross-route state bleed). */}
        <Route path="migrations" element={<Navigate to="/pdf/migrations/code" replace />} />
        <Route path="migrations/code" element={(
          <RequireDesignerPermission permission={designerPermissions.migrationPdfExecute}>
            <MigrationsPage key="pdf-code" mode="code" codeKind="pdf" />
          </RequireDesignerPermission>
        )} />
        <Route path="migrations/designer" element={(
          <RequireDesignerPermission permission={designerPermissions.migrationReportExecute}>
            <MigrationsPage key="pdf-designer" mode="designer" />
          </RequireDesignerPermission>
        )} />
      </Route>

      <Route path="/spreadsheet" element={(
        <RequireDesignerPermission permission={designerPermissions.spreadsheetAccess}>
          <SpreadsheetLayout />
        </RequireDesignerPermission>
      )}>
        <Route index element={<Navigate to="create" replace />} />
        <Route
          path="create"
          element={(
            <RequireDesignerPermission permission={designerPermissions.spreadsheetCreate}>
              <Suspense fallback={<div className="route-loading">Loading spreadsheet...</div>}>
                <SpreadsheetEditorPage />
              </Suspense>
            </RequireDesignerPermission>
          )}
        />
        <Route path="edit" element={(
          <RequireDesignerPermission permission={designerPermissions.spreadsheetEdit}>
            <SpreadsheetImportPage variant="edit" />
          </RequireDesignerPermission>
        )} />
        <Route path="import" element={(
          <RequireDesignerPermission permission={designerPermissions.spreadsheetImport}>
            <SpreadsheetImportPage variant="import" />
          </RequireDesignerPermission>
        )} />
        <Route path="migrations" element={<Navigate to="/spreadsheet/migrations/code" replace />} />
        <Route path="migrations/code" element={(
          <RequireDesignerPermission permission={designerPermissions.migrationSpreadsheetExecute}>
            <MigrationsPage key="spreadsheet-code" mode="code" codeKind="spreadsheet" />
          </RequireDesignerPermission>
        )} />
      </Route>

      {/* Back-compat redirects from the old flat route tree */}
      <Route path="/template" element={<Navigate to="/pdf/template" replace />} />
      <Route path="/create" element={<Navigate to="/pdf/create" replace />} />
      <Route path="/importer" element={<Navigate to="/pdf/import" replace />} />
      <Route path="/pdf-viewer" element={<Navigate to="/pdf/viewer" replace />} />
      <Route path="/migrations" element={<Navigate to="/pdf/migrations" replace />} />
      <Route path="/migrations/pdf" element={<Navigate to="/pdf/migrations/code" replace />} />
      <Route path="/migrations/pdf/code" element={<Navigate to="/pdf/migrations/code" replace />} />
      <Route path="/migrations/pdf/designer" element={<Navigate to="/pdf/migrations/designer" replace />} />
      <Route path="/migrations/spreadsheet" element={<Navigate to="/spreadsheet/migrations/code" replace />} />
      <Route path="/migrations/spreadsheet/code" element={<Navigate to="/spreadsheet/migrations/code" replace />} />
      <Route path="/migrations/spreadsheet/datasource" element={<Navigate to="/spreadsheet/import" replace />} />
      <Route path="/migrations/code" element={<Navigate to="/pdf/migrations/code" replace />} />
      <Route path="/migrations/code/pdf" element={<Navigate to="/pdf/migrations/code" replace />} />
      <Route path="/migrations/code/spreadsheet" element={<Navigate to="/spreadsheet/migrations/code" replace />} />
      <Route path="/migrations/designer" element={<Navigate to="/pdf/migrations/designer" replace />} />
      <Route path="/migrations/format" element={<Navigate to="/pdf/migrations/designer" replace />} />
      <Route path="/migrations/format/designer" element={<Navigate to="/pdf/migrations/designer" replace />} />
      <Route path="/migrations/format/spreadsheet" element={<Navigate to="/spreadsheet/import" replace />} />
      <Route path="/migrations/format/documents" element={<Navigate to="/pdf/import" replace />} />
      </Routes>
      </ProductExperienceProvider>
    </DesignerAuthGate>
    </LocaleProvider>
  );
};

export default App;
