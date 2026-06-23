import React, { Suspense } from 'react';
import { Routes, Route } from 'react-router-dom';
import IndexPage from '@/pages/IndexPage';
import TemplatePage from '@/pages/TemplatePage';
import CreatePage from '@/pages/CreatePage';
import DocsPage from '@/pages/DocsPage';
import MigrationsHubPage from '@/pages/MigrationsHubPage';
import MigrationsPage from '@/pages/MigrationsPage';
import ImporterPage from '@/pages/ImporterPage';

const PdfViewerPage = React.lazy(() => import('@/features/pdf-viewer/PdfViewerPage'));

const App: React.FC = () => (
  <Routes>
    <Route path="/" element={<IndexPage />} />
    <Route path="/template" element={<TemplatePage />} />
    <Route path="/create" element={<CreatePage />} />
    <Route path="/docs" element={<DocsPage />} />
    <Route path="/migrations" element={<MigrationsHubPage />} />
    <Route path="/migrations/code" element={<MigrationsPage mode="code" />} />
    <Route path="/migrations/designer" element={<MigrationsPage mode="designer" />} />
    <Route path="/importer" element={<ImporterPage />} />
    <Route
      path="/pdf-viewer"
      element={(
        <Suspense fallback={<div className="route-loading">Loading PDF viewer...</div>}>
          <PdfViewerPage />
        </Suspense>
      )}
    />
  </Routes>
);

export default App;
