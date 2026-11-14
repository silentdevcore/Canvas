import { useEffect, useState } from 'react';
import LivePreview from '@/components/Preview/LivePreview';
import SimplePvadSurface from '@/components/Editor/SimplePvadSurface';
import { normalizePageSettings, useEditorStore } from '@/store';
import type { Page, PageSettings, SimpleElement, Template } from '@/types';
import { DesignerAuthContextProvider } from '@/auth/DesignerAuthGate';
import { designerPermissions } from '@/auth/designerPermissions';
import allElementsFixture from '../../../test-assets/visual-parity/all-elements.json';
import mixedLayoutFixture from '../../../test-assets/page-layout/mixed-layout.json';
import styledTablesFixture from '../../../test-assets/visual-parity/styled-tables.json';
import sharedRendererFixture from '../../../test-assets/visual-parity/shared-renderer.json';
import imageFitFixture from '../../../test-assets/visual-parity/image-fit.json';
import dateFormatFixture from '../../../test-assets/visual-parity/date-format.json';
import dateLayoutFixture from '../../../test-assets/visual-parity/date-layout.json';
import fontFamiliesFixture from '../../../test-assets/visual-parity/font-families.json';
import eInvoiceFixture from '../../../test-assets/visual-parity/e-invoice.json';
import eInvoiceMultiPageFixture from '../../../test-assets/visual-parity/e-invoice-multipage.json';
import eInvoiceLongDescriptionFixture from '../../../test-assets/visual-parity/e-invoice-long-description.json';
import eInvoiceMultiTaxFixture from '../../../test-assets/visual-parity/e-invoice-multi-tax.json';

type Fixture = {
  id: string;
  name: string;
  category: string;
  description: string;
  pages: Page[];
  sharedElements: SimpleElement[];
  pageSettings: Partial<PageSettings>;
};

const noop = () => undefined;
const parityUser = {
  id: 'visual-parity-user',
  username: 'visual-parity',
  email: 'visual-parity@localhost.invalid',
  displayName: 'Visual parity',
  roles: ['Designer'],
  permissions: Object.values(designerPermissions),
  organizations: [],
  activeOrganizationId: null,
  lastLoginAt: null,
};

export default function VisualParityPage() {
  const [ready, setReady] = useState(false);
  const search = new URLSearchParams(location.search);
  const surface = search.get('surface') ?? 'preview';
  // Keyed by fixture filename, matching the key tools/visual-parity/run.mjs derives.
  const fixtures: Record<string, unknown> = {
    'all-elements': allElementsFixture,
    'mixed-layout': mixedLayoutFixture,
    'styled-tables': styledTablesFixture,
    'shared-renderer': sharedRendererFixture,
    'image-fit': imageFitFixture,
    'date-format': dateFormatFixture,
    'date-layout': dateLayoutFixture,
    'font-families': fontFamiliesFixture,
    'e-invoice': eInvoiceFixture,
    'e-invoice-multipage': eInvoiceMultiPageFixture,
    'e-invoice-long-description': eInvoiceLongDescriptionFixture,
    'e-invoice-multi-tax': eInvoiceMultiTaxFixture,
  };
  const selectedFixture = fixtures[search.get('fixture') ?? ''] ?? allElementsFixture;
  const design = selectedFixture as unknown as Fixture;
  const editorPageIndex = Math.max(0, Math.min(design.pages.length - 1, Number.parseInt(search.get('page') ?? '0', 10) || 0));
  const template: Template = {
    id: design.id,
    name: design.name,
    category: design.category,
    description: design.description,
  };

  useEffect(() => {
    useEditorStore.setState({
      pageSettings: normalizePageSettings({
        ...design.pageSettings,
        gridVisible: false,
        showMarginGuide: false,
        showSafeArea: false,
      }),
      currentPreviewLanguage: design.pageSettings.targetLanguage || design.pageSettings.systemLanguage || 'en',
      selectedElementId: null,
    });
    document.documentElement.dataset.visualParity = 'true';
    setReady(true);
    return () => { delete document.documentElement.dataset.visualParity; };
  }, [design.pageSettings]);

  if (!ready) return null;

  if (surface === 'canvas' || surface === 'editor') {
    return (
      <DesignerAuthContextProvider user={parityUser}>
      <SimplePvadSurface
        template={template}
        elements={design.pages[editorPageIndex]?.elements ?? []}
        pages={design.pages}
        currentPageIndex={editorPageIndex}
        sharedElements={design.sharedElements ?? []}
        onElementAdd={noop}
        onElementUpdate={noop}
        onElementDelete={noop}
        onElementReorder={noop}
        onPreview={noop}
        onBack={noop}
        onPageAdd={noop}
        onPageDelete={noop}
        onPageDuplicate={noop}
        onPageSelect={noop}
        onPageMove={noop}
        onSharedElementAdd={noop}
        onSharedElementUpdate={noop}
        onSharedElementDelete={noop}
        onTemplateRename={noop}
        visualParityMode={surface !== 'editor'}
      />
      </DesignerAuthContextProvider>
    );
  }

  return (
    // LivePreview calls useDesignerAuth for its export gating, so the preview surface needs the
    // same auth context the canvas surface already had. Without it the preview never renders and
    // the parity run hangs waiting for a surface that will never appear.
    <DesignerAuthContextProvider user={parityUser}>
      <LivePreview
        template={template}
        pages={design.pages}
        sharedElements={design.sharedElements ?? []}
        pageSettings={normalizePageSettings(design.pageSettings)}
        onBack={noop}
        onExport={noop}
        visualParityMode
      />
    </DesignerAuthContextProvider>
  );
}
