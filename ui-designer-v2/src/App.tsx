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
    {/* Domain 1 — PDF Migration */}
    <Route path="/migrations/pdf" element={<Navigate to="/migrations/pdf/code" replace />} />
    <Route path="/migrations/pdf/code" element={<MigrationsPage mode="code" codeKind="pdf" />} />
    <Route path="/migrations/pdf/designer" element={<MigrationsPage mode="designer" />} />
    {/* Domain 2 — Spreadsheet Migration */}
    <Route path="/migrations/spreadsheet" element={<Navigate to="/migrations/spreadsheet/code" replace />} />
    <Route path="/migrations/spreadsheet/code" element={<MigrationsPage mode="code" codeKind="spreadsheet" />} />
    <Route path="/migrations/spreadsheet/datasource" element={<SpreadsheetImportPage />} />
    {/* Document importer — standalone */}
    <Route path="/importer" element={<ImporterPage />} />
    {/* Back-compat redirects */}
    <Route path="/migrations/code" element={<Navigate to="/migrations/pdf/code" replace />} />
    <Route path="/migrations/code/pdf" element={<Navigate to="/migrations/pdf/code" replace />} />
    <Route path="/migrations/code/spreadsheet" element={<Navigate to="/migrations/spreadsheet/code" replace />} />
    <Route path="/migrations/designer" element={<Navigate to="/migrations/pdf/designer" replace />} />
    <Route path="/migrations/format" element={<Navigate to="/migrations/pdf/designer" replace />} />
    <Route path="/migrations/format/designer" element={<Navigate to="/migrations/pdf/designer" replace />} />
    <Route path="/migrations/format/spreadsheet" element={<Navigate to="/migrations/spreadsheet/datasource" replace />} />
    <Route path="/migrations/format/documents" element={<Navigate to="/importer" replace />} />
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
