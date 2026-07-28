import React, { Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
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

const PdfViewerPage = React.lazy(() => import('@/features/pdf-viewer/PdfViewerPage'));
const SpreadsheetEditorPage = React.lazy(() => import('@/pages/SpreadsheetEditorPage'));

const App: React.FC = () => (
  <LocaleProvider>
    <DesignerAuthGate>
      <ProductExperienceProvider>
      <Routes>
      <Route path="/" element={<IndexPage />} />
      <Route path="/docs" element={<DocsPage />} />

      <Route path="/pdf" element={<PdfLayout />}>
        <Route index element={<Navigate to="template" replace />} />
        <Route path="create" element={<CreatePage />} />
        <Route path="edit" element={<Navigate to="/pdf/create?mode=code" replace />} />
        <Route path="template" element={<TemplatePage />} />
        <Route path="import" element={<ImporterPage />} />
        <Route path="convert" element={<ConvertToPdfPage />} />
        <Route
          path="viewer"
          element={(
            <Suspense fallback={<div className="route-loading">Loading PDF viewer...</div>}>
              <PdfViewerPage />
            </Suspense>
          )}
        />
        {/* Distinct `key` per route forces a fresh MigrationsPage instance so each
            sub-tab is its own view (no cross-route state bleed). */}
        <Route path="migrations" element={<Navigate to="/pdf/migrations/code" replace />} />
        <Route path="migrations/code" element={<MigrationsPage key="pdf-code" mode="code" codeKind="pdf" />} />
        <Route path="migrations/designer" element={<MigrationsPage key="pdf-designer" mode="designer" />} />
      </Route>

      <Route path="/spreadsheet" element={<SpreadsheetLayout />}>
        <Route index element={<Navigate to="create" replace />} />
        <Route
          path="create"
          element={(
            <Suspense fallback={<div className="route-loading">Loading spreadsheet...</div>}>
              <SpreadsheetEditorPage />
            </Suspense>
          )}
        />
        <Route path="edit" element={<SpreadsheetImportPage variant="edit" />} />
        <Route path="import" element={<SpreadsheetImportPage variant="import" />} />
        <Route path="migrations" element={<Navigate to="/spreadsheet/migrations/code" replace />} />
        <Route path="migrations/code" element={<MigrationsPage key="spreadsheet-code" mode="code" codeKind="spreadsheet" />} />
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

export default App;
