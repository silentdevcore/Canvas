import React from 'react';
import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  FiFilePlus,
  FiCode,
  FiLayout as FiLayoutIcon,
  FiUpload,
  FiEye,
  FiFileText,
  FiGitMerge,
} from 'react-icons/fi';
import AppHeader from './AppHeader';
import HubSidebar, { type HubSidebarItem } from './HubSidebar';
import { useSidebarCollapsed } from '@/hooks/useSidebarCollapsed';

const PdfLayout: React.FC = () => {
  const { t } = useTranslation('common');
  const [collapsed, toggleCollapsed] = useSidebarCollapsed('pxa-designer:pdf-sidebar-collapsed');

  const pdfSidebarItems: HubSidebarItem[] = [
    {
      path: '/pdf/create',
      label: t('pdfSidebar.createPdf'),
      icon: FiFilePlus,
      // /pdf/create and /pdf/edit both land on the same route (?mode=code
      // just switches CreatePage's internal view), so pathname-only matching
      // would always highlight this item — check the query string too.
      isActive: (location) => location.pathname === '/pdf/create' && new URLSearchParams(location.search).get('mode') !== 'code',
    },
    {
      path: '/pdf/edit',
      label: t('pdfSidebar.editPdf'),
      icon: FiCode,
      isActive: (location) => location.pathname === '/pdf/create' && new URLSearchParams(location.search).get('mode') === 'code',
    },
    { path: '/pdf/template', label: t('pdfSidebar.useTemplate'), icon: FiLayoutIcon },
    { path: '/pdf/import', label: t('pdfSidebar.importPdf'), icon: FiUpload },
    { path: '/pdf/convert', label: t('pdfSidebar.convertToPdf'), icon: FiEye },
    { path: '/pdf/viewer', label: t('pdfSidebar.pdfViewer'), icon: FiFileText },
    { path: '/pdf/migrations', label: t('pdfSidebar.migrations'), icon: FiGitMerge },
  ];

  return (
    <div className="hub-layout">
      <AppHeader activePage="pdf" />
      <div className={`hub-layout-body${collapsed ? ' is-collapsed' : ''}`}>
        <HubSidebar items={pdfSidebarItems} collapsed={collapsed} onToggle={toggleCollapsed} />
        <main className="hub-layout-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default PdfLayout;
