import React, { Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import IndexPage from '@/pages/IndexPage';
import TemplatePage from '@/pages/TemplatePage';
import CreatePage from '@/pages/CreatePage';
import DocsPage from '@/pages/DocsPage';
import MigrationsHubPage from '@/pages/MigrationsHubPage';
import MigrationsPage from '@/pages/MigrationsPage';
import ImporterPage from '@/pages/ImporterPage';
import SpreadsheetImportPage from '@/pages/SpreadsheetImportPage';

const PdfViewerPage = React.lazy(() => import('@/features/pdf-viewer/PdfViewerPage'));
const SpreadsheetEditorPage = React.lazy(() => import('@/pages/SpreadsheetEditorPage'));

const App: React.FC = () => (
  <Routes>
    <Route path="/" element={<IndexPage />} />
    <Route path="/template" element={<TemplatePage />} />
    <Route path="/create" element={<CreatePage />} />
    <Route path="/docs" element={<DocsPage />} />
    <Route path="/migrations" element={<MigrationsHubPage />} />
    {/* Type 1 — Code Migration (PDF + Spreadsheet libraries → Canvas code) */}
    <Route path="/migrations/code" element={<MigrationsPage mode="code" />} />
    {/* Type 2 — DataSource / Format Migration (file/format → Canvas design/model) */}
    <Route path="/migrations/format" element={<Navigate to="/migrations/format/designer" replace />} />
    <Route path="/migrations/format/designer" element={<MigrationsPage mode="designer" />} />
    <Route path="/migrations/format/documents" element={<ImporterPage />} />
    <Route path="/migrations/format/spreadsheet" element={<SpreadsheetImportPage />} />
    {/* Back-compat redirects */}
    <Route path="/migrations/designer" element={<Navigate to="/migrations/format/designer" replace />} />
    <Route path="/importer" element={<Navigate to="/migrations/format/documents" replace />} />
    <Route
      path="/pdf-viewer"
      element={(
        <Suspense fallback={<div className="route-loading">Loading PDF viewer...</div>}>
          <PdfViewerPage />
        </Suspense>
      )}
    />
    <Route
      path="/spreadsheet"
      element={(
        <Suspense fallback={<div className="route-loading">Loading spreadsheet...</div>}>
          <SpreadsheetEditorPage />
        </Suspense>
      )}
    />
  </Routes>
);

export default App;
